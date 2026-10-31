using System;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    // Thin host binding around the recovered original owner; world/save methods
    // invoke these ports explicitly. No independent Update loop owns gameplay.
    public static class NativeSourceContextFacade
    {
        sealed class Session
        {
            public FriendsLifeRuntime runtime;
            public FriendsLifeSave save;
            public NativeSourceContextLedger ledger;
            public int rosterFrame=int.MinValue,rosterCount=-1,wrapperFrame=int.MinValue;
        }
        static readonly ConditionalWeakTable<FriendsLifeSave,Session> sessions=new ConditionalWeakTable<FriendsLifeSave,Session>();
        static int clockFrame=int.MinValue;
        static long? frameUserPosixSeconds;
        static long? UserClock()
        {
            // 00566678 supplies one raw adjustable user POSIX-second snapshot.
            // Keep it constant across this Unity frame's owners and wrappers.
            if(clockFrame!=Time.frameCount){clockFrame=Time.frameCount;frameUserPosixSeconds=DateTimeOffset.UtcNow.ToUnixTimeSeconds();}
            return frameUserPosixSeconds;
        }
        static NativeSourceContextLedger Restore(FriendsLifeSave save)
        {
            // Pre-module saves have never had these runtime owners. Construct
            // their recovered original ctor now; do not replay past outcomes.
            if(string.IsNullOrEmpty(save.nativeSourceContextJson))
            {
                if(save.schema>=2)throw new OriginalBindingException("This snapshot declares the native context module but its context JSON is absent");
                return new NativeSourceContextLedger(save.worldId);
            }
            var data=NativeSourceContextCodec.Decode(save.nativeSourceContextJson);
            if(data==null)throw new OriginalBindingException("Saved native source context is not a valid context object");
            return new NativeSourceContextLedger(save.worldId,data);
        }
        static Session For(FriendsLifeRuntime runtime)
        {
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||runtime.State==null)throw new OriginalBindingException("Native source context requires the current authoritative world");
            if(!sessions.TryGetValue(runtime.State,out var result))
            {
                var save=runtime.State;result=new Session{runtime=runtime,save=save,ledger=Restore(save)};sessions.Add(save,result);
            }
            if(result.runtime!=runtime)throw new OriginalBindingException("Source context runtime/identity changed without a restored world snapshot");
            // Register actual projected Mii members in saved roster order, not
            // host transient resident IDs or the order of predicate queries.
            if(result.rosterFrame!=Time.frameCount||result.rosterCount!=runtime.State.people.Count)
            {
                result.rosterFrame=Time.frameCount;result.rosterCount=runtime.State.people.Count;
                foreach(var person in runtime.State.people)
                    if(!string.IsNullOrEmpty(person.nativeResidentId)&&HasBody(runtime,person)&&!result.ledger.Data.actors.Any(a=>a.nativeResidentId==person.nativeResidentId&&!a.registered))result.ledger.Register(person.nativeResidentId);
            }
            // Restore the live runtime wrapper, not its economic/relationship
            // result. Exact persisted receipts make repeated restore inert.
            if(result.wrapperFrame!=Time.frameCount)
            {
                result.wrapperFrame=Time.frameCount;
                // No wrapper can be restored without a lease. Preserve the
                // original scan for malformed collections and live leases.
                if(runtime.State.leases == null || runtime.State.leases.Count != 0 || runtime.State.episodes == null ||
                    result.ledger.Data.tasks == null || result.ledger.Data.tasks.Any(t => t == null))
                {
                    foreach(var episode in runtime.State.episodes.Where(IsSocial).ToArray())
                        if(!result.ledger.Data.tasks.Any(t=>t.taskId==episode.episodeId)&&runtime.State.leases.Any(l=>l.ownerEpisode==episode.episodeId)&&Participants(runtime,episode,out var people))
                            AcquireBound(result,episode,people);
                }
            }
            return result;
        }
        static bool IsSocial(SourceEpisode episode)=>episode!=null&&(episode.sourceKind=="MiiEvent"||episode.sourceKind=="Family");
        static bool Participants(FriendsLifeRuntime runtime,SourceEpisode episode,out PersonBinding[] people)
        {
            people=new[]{episode.focusPersonId,episode.targetPersonId,episode.anotherPersonId}.Where(id=>!string.IsNullOrEmpty(id)).Select(id=>runtime.GetPerson(id)).ToArray();
            return people.Length>0&&people.All(p=>p!=null&&!string.IsNullOrEmpty(p.nativeResidentId)&&HasBody(runtime,p)&&runtime.State.leases.Any(l=>l.personId==p.personId&&l.ownerEpisode==episode.episodeId));
        }
        static void AcquireBound(Session session,SourceEpisode episode,PersonBinding[] people)
        {
            foreach(var person in people)session.ledger.Register(person.nativeResidentId);
            var participants=people.Select(p=>p.nativeResidentId).ToArray();
            if(session.ledger.Begin(episode.episodeId,episode.sourceKind,episode.sourceId,participants,participants,UserClock()))Store(session);
        }
        public static bool CanAcquireEpisode(FriendsLifeRuntime runtime,SourceEpisode episode,out string reason)
        {
            reason=null;
            if(episode==null||episode.committed){reason="No active source episode to acquire";return false;}
            if(episode.sourceKind!="MiiEvent"&&episode.sourceKind!="Family"){reason="This port requires a real social source task";return false;}
            For(runtime);
            var people=new[]{episode.focusPersonId,episode.targetPersonId,episode.anotherPersonId}.Where(id=>!string.IsNullOrEmpty(id)).Select(id=>runtime.GetPerson(id)).ToArray();
            if(people.Length==0||people.Any(p=>p==null||string.IsNullOrEmpty(p.nativeResidentId)||!HasBody(runtime,p))){reason="Original source participants are not actual bound world members";return false;}
            return true;
        }
        static bool HasBody(FriendsLifeRuntime runtime,PersonBinding person)=>person.controllerKind==PersonControllerKind.Npc?runtime.ActorOf(person.personId)!=null:runtime.TransformOf(person.personId)!=null;
        static void Store(Session session)
        {session.save.nativeSourceContextJson=NativeSourceContextCodec.Encode(session.ledger.Data);session.save.schema=Math.Max(session.save.schema,2);session.runtime.MarkChanged();}
        public static bool? CooldownReady(FriendsLifeRuntime runtime,string nativeResidentId)
        {
            if(!runtime||!runtime.Ready||!runtime.IsAuthority)return null;
            var person=runtime.PersonFromNative(nativeResidentId);
            if(person==null||!HasBody(runtime,person))return null;
            var session=For(runtime);if(session.ledger.Data.actors.Any(a=>a.nativeResidentId==nativeResidentId&&!a.registered))return null;session.ledger.Register(nativeResidentId);
            // A saved live task whose real bodies/leases have not been rebuilt
            // is unavailable for its participants until actual binding occurs.
            if(runtime.State.episodes.Any(e=>IsSocial(e)&&new[]{e.focusPersonId,e.targetPersonId,e.anotherPersonId}.Contains(person.personId)&&runtime.State.leases.Any(l=>l.ownerEpisode==e.episodeId)&&!session.ledger.Data.tasks.Any(t=>t.taskId==e.episodeId)))return null;
            return session.ledger.Ready(nativeResidentId,runtime.Core?.NativeEventEngine!=null,UserClock());
        }
        public static void AcquireEpisode(FriendsLifeRuntime runtime,SourceEpisode episode)
        {
            if(episode==null||episode.committed)throw new OriginalBindingException("No active source episode to acquire");
            if(episode.sourceKind!="MiiEvent"&&episode.sourceKind!="Family")throw new OriginalBindingException("This port requires a real social source task, not an arbitrary resource lease");
            if(!CanAcquireEpisode(runtime,episode,out var reason))throw new OriginalBindingException(reason);
            var session=For(runtime);
            if(!Participants(runtime,episode,out var people))throw new OriginalBindingException("Source task participants have not acquired actual bound bodies and world leases");
            AcquireBound(session,episode,people);
        }
        public static void SynchronizeCompleted(FriendsLifeRuntime runtime)
        {
            var session=For(runtime);bool changed=false;long? now=UserClock();
            foreach(var task in session.ledger.Data.tasks.Where(t=>!t.ended).ToArray())
            {
                var episode=runtime.State.episodes.FirstOrDefault(e=>e.episodeId==task.taskId);
                // Resource loss/Suspended/BindingFault/SourceRejected is not an
                // original task-end. A committed R Date still owns its real
                // SourceActivity leases until the source activity ends.
                if(episode==null||!episode.committed||runtime.State.leases.Any(l=>l.ownerEpisode==task.taskId))continue;
                changed|=session.ledger.End(task.taskId,now);
            }
            if(changed)Store(session);
        }
        public static void EndForCommittedRemoval(FriendsLifeRuntime runtime,string nativeResidentId)
        {
            // Call only from the successful source removal commit, before the
            // source actor is retired; never use this for a missing projection.
            var session=For(runtime);bool changed=false;long? now=UserClock();
            foreach(var task in session.ledger.Data.tasks.Where(t=>!t.ended&&(t.ownerNativeIds.Contains(nativeResidentId)||t.participants.Any(p=>p.nativeResidentId==nativeResidentId))).ToArray())changed|=session.ledger.End(task.taskId,now);
            changed|=session.ledger.Retire(nativeResidentId);if(changed)Store(session);
        }
        // Explicit actor AS owner access. This is authority-only; clients read
        // the replicated JSON and never receive a mutable source owner.
        public static NativeSourceActorRecord[] AnimationActors(FriendsLifeRuntime runtime)=>For(runtime).ledger.Data.actors.ToArray();
        public static NativeSourceActorRecord AnimationActor(FriendsLifeRuntime runtime,string nativeResidentId)
        {
            var actor=For(runtime).ledger.Actor(nativeResidentId);
            if(actor==null)throw new OriginalBindingException("No actual registered source actor for Date AS: "+nativeResidentId);
            return actor;
        }
        public static string Capture(FriendsLifeRuntime runtime)
        {
            SynchronizeCompleted(runtime);runtime.ReconcileNativeDateAsOwners();var session=For(runtime);
            session.save.nativeSourceContextJson=NativeSourceContextCodec.Encode(session.ledger.Data);session.save.schema=Math.Max(session.save.schema,2);return session.save.nativeSourceContextJson;
        }
        // Capture's live reconciliation without encoding the context JSON.
        public static void SynchronizeLive(FriendsLifeRuntime runtime)
        {
            SynchronizeCompleted(runtime);runtime.ReconcileNativeDateAsOwners();For(runtime);
        }
    }
}
