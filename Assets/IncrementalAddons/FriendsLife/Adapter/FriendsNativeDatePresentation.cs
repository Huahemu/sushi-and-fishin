using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.FriendsLife.SourceGraphs;
using TomodachiLife;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendsLifeRuntime
    {
        sealed class NativeDateMotion
        {public ResidentController actor;public OriginalMotionRetargeter motion;public NativeDateFacialHold face;public bool visibilityHeld;}
        readonly Dictionary<string,NativeDateMotion> nativeDateMotions=new Dictionary<string,NativeDateMotion>();
        string replicatedAsJson;
        NativeSourceContextData replicatedAsData;
        float nextNativeDateSnapshot,nextClientDateDiscovery;
        static bool IsDate(SourceEpisode episode)=>episode!=null&&episode.committed&&episode.status=="SourceActivity"&&episode.familyChoice?.action=="Date";
        string DateKey(SourceEpisode episode,PersonBinding person)=>episode.episodeId+"|"+person.personId;
        bool DateHasOwner(string executionKey,string nativeResidentId)
        {
            var person=PersonFromNative(nativeResidentId);if(person==null||person.controllerKind!=PersonControllerKind.Npc)return false;
            return State.episodes.Any(e=>IsDate(e)&&DateKey(e,person)==executionKey&&State.leases.Any(l=>l.ownerEpisode==e.episodeId&&l.personId==person.personId&&l.phase=="Date"));
        }
        // This is also called before serialization, after a real lease release.
        // Disposing a local visual for a scene/model reload does not end AS.
        public void ReconcileNativeDateAsOwners()
        {
            if(!Ready||!IsAuthority)return;
            foreach(var actor in NativeSourceContextFacade.AnimationActors(this))
                if(actor.asModuleVersion==1&&actor.animation?.date?.active==true&&!DateHasOwner(actor.animation.date.executionKey,actor.nativeResidentId))
                    NativeDateAsRuntime.End(actor.animation,actor.animation.date.executionKey);
        }
        void AdvanceNativeDatePresentations(float elapsed)
        {
            if(!Ready||!IsAuthority)return;
            ReconcileNativeDateAsOwners();var active=new HashSet<string>(StringComparer.Ordinal);bool running=false;
            foreach(var episode in State.episodes.Where(IsDate).ToArray())
            {
                var leases=State.leases.Where(l=>l.ownerEpisode==episode.episodeId&&l.phase=="Date").ToArray();
                if(leases.Length==0){StopNativeDatePresentation(episode.episodeId);episode.status="Done";NotifyChanged();continue;}
                foreach(var lease in leases)
                {
                    var person=GetPerson(lease.personId);if(person?.controllerKind!=PersonControllerKind.Npc)continue;
                    string key=DateKey(episode,person);active.Add(key);var actor=ActorOf(person.personId);
                    try
                    {
                        var record=NativeSourceContextFacade.AnimationActor(this,person.nativeResidentId);
                        if(record.asModuleVersion==0){record.animation=new NativeDateActorAsState();record.asModuleVersion=1;}
                        if(record.asModuleVersion!=1)throw new OriginalBindingException("Unknown actor AS module version");
                        var state=record.animation.date;
                        if(state?.active!=true||state.executionKey!=key)
                        {
                            if(!actor||!actor.VisualAnimator)continue;
                            var document=JsonUtility.FromJson<ResidentEditDocument>(Core.FindResident(person.nativeResidentId)?.avatarJson??"");
                            if(document?.appearance==null)throw new OriginalBindingException("Date source body parameters have no actual avatar appearance owner");
                            state=NativeDateAsRuntime.Begin(record.animation,key,document.appearance.height,document.appearance.build,OriginalMotionRetargeter.SourceGraph,ref Core.State.eventRandom);
                        }
                        NativeDateAsRuntime.Tick(state,(float)(Math.Max(0,elapsed)*60f),ref Core.State.eventRandom);
                        lease.visualElapsed+=Math.Max(0,elapsed);running=true;
                        if(!actor||!actor.VisualAnimator)continue;
                        if(!ProjectNativeDate(actor,key,state,out var error))episode.fault=error;
                        else if(string.IsNullOrEmpty(episode.fault)||episode.fault.StartsWith("Date AS:",StringComparison.Ordinal))episode.fault="";
                    }
                    catch(Exception e){episode.fault="Date AS: "+e.Message;}
                }
            }
            foreach(string key in nativeDateMotions.Keys.Where(k=>!active.Contains(k)).ToArray())StopNativeDateMotion(key);
            // AS frame state is captured by the normal existing network/save
            // export. Request a snapshot at the world's existing5Hz cadence.
            if(running&&Time.unscaledTime>=nextNativeDateSnapshot){nextNativeDateSnapshot=Time.unscaledTime+.2f;NotifyChanged();}
        }
        bool ProjectNativeDate(ResidentController actor,string key,NativeDateAsState state,out string error)
        {
            error=null;
            if(!nativeDateMotions.TryGetValue(key,out var projected)||projected.actor!=actor||!projected.motion||!projected.motion.IsPlaying)
            {
                StopNativeDateMotion(key);
                var motion=actor.GetComponent<OriginalMotionRetargeter>();if(!motion)motion=actor.gameObject.AddComponent<OriginalMotionRetargeter>();
                motion.ProjectRootPose=true;
                if(!motion.PlayManualSourcePoseNode(state.bodyNode,key,state.rawHeight,state.rawBuild,"AnimationProduct",out error))return false;
                var face=actor.GetComponent<NativeDateFacialHold>();if(!face)face=actor.gameObject.AddComponent<NativeDateFacialHold>();
                projected=new NativeDateMotion{actor=actor,motion=motion,face=face};
                if(actor.Brain!=null){actor.Brain.BeginModelVisibilityHold();projected.visibilityHeld=true;}
                nativeDateMotions[key]=projected;
            }
            if(!projected.motion.SetManualSourceFrames(state,out error))return false;
            projected.face.Bind(actor,key,state.expressionCode);
            if(!string.IsNullOrEmpty(projected.face.LastError)){error="Date AS: "+projected.face.LastError;return false;}
            return true;
        }
        // Called on the existing runtime Update client branch. Only read the
        // authoritative snapshot. No client simulation, leases, RNG or save.
        void TickReplicatedDatePresentations()
        {
            if(!Ready)return;
            if(Time.unscaledTime>=nextClientDateDiscovery){nextClientDateDiscovery=Time.unscaledTime+2f;DiscoverBoundActors();}
            if(!ReferenceEquals(replicatedAsJson,State.nativeSourceContextJson))
            {
                replicatedAsJson=State.nativeSourceContextJson;replicatedAsData=null;
                if(!string.IsNullOrEmpty(replicatedAsJson))try
                {
                    var data=NativeSourceContextCodec.Decode(replicatedAsJson);
                    replicatedAsData=new NativeSourceContextLedger(State.worldId,data).Data;
                }
                catch(Exception e){LastError="Replicated Date AS: "+e.Message;}
            }
            var active=new HashSet<string>(StringComparer.Ordinal);
            if(replicatedAsData!=null)foreach(var record in replicatedAsData.actors.Where(a=>a.registered&&a.asModuleVersion==1&&a.animation?.date?.active==true))
            {
                var state=record.animation.date;if(!DateHasOwner(state.executionKey,record.nativeResidentId))continue;
                active.Add(state.executionKey);var person=PersonFromNative(record.nativeResidentId);var actor=ActorOf(person.personId);if(!actor||!actor.VisualAnimator)continue;
                if(Time.timeScale>0)NativeDateAsRuntime.PredictBody(state,(float)(Math.Min(.25f,Time.unscaledDeltaTime)*60f));
                if(!ProjectNativeDate(actor,state.executionKey,state,out var error))LastError=error;
            }
            foreach(string key in nativeDateMotions.Keys.Where(k=>!active.Contains(k)).ToArray())StopNativeDateMotion(key);
        }
        void StopNativeDateMotion(string key)
        {
            if(!nativeDateMotions.TryGetValue(key,out var value))return;
            if(value.motion&&value.motion.ExecutionKey==key){value.motion.Stop();value.motion.ProjectRootPose=false;}
            if(value.face)value.face.Stop(key);
            if(value.visibilityHeld)value.actor?.Brain?.ReleaseModelVisibilityHold();nativeDateMotions.Remove(key);
        }
        void StopNativeDatePresentation(string episodeId)
        {foreach(string key in nativeDateMotions.Keys.Where(k=>k.StartsWith(episodeId+"|",StringComparison.Ordinal)).ToArray())StopNativeDateMotion(key);}
    }
}
