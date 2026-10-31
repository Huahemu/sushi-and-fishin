using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Framework.Enums;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    [Serializable] public sealed class OriginalAmbientActivityRecord
    {
        public string worldId,personId,nativeResidentId,requestOwner,phase;
        public int activity,index,patrolIndex=-1;
        public Vector3 originalTarget,rotation;
    }

    // Save/rebind only an original ambient request. Recovery always begins at
    // the actual restored actor and walks again. It never places a seated body,
    // recreates a resource claim, or replays a TL/economy/source result.
    [DefaultExecutionOrder(2140)]
    public sealed class OriginalNpcActivityPersistence:MonoBehaviour
    {
        FriendsLifeSave observed;
        readonly List<OriginalAmbientActivityRecord> pending=new List<OriginalAmbientActivityRecord>();
        static OriginalNpcActivityPersistence instance;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install(){if(!instance)new GameObject("Original resident activity save rebind").AddComponent<OriginalNpcActivityPersistence>();}
        void Awake(){if(instance&&instance!=this){Destroy(gameObject);return;}instance=this;DontDestroyOnLoad(gameObject);}
        static bool ForeignOwner(FriendsLifeRuntime runtime,string personId)
            =>runtime.State.leases.Any(l=>l.personId==personId)||FriendsEconomyService.Instance?.HasActiveJob(personId)==true;

        static void DiscardPending(FriendsLifeRuntime runtime,string personId)
        {if(instance&&ReferenceEquals(instance.observed,runtime.State))instance.pending.RemoveAll(r=>r.personId==personId);}
        static bool AwaitingRebind(FriendsLifeRuntime runtime,OriginalAmbientActivityRecord record)
            =>!instance||!ReferenceEquals(instance.observed,runtime.State)||instance.pending.Any(r=>r.personId==record.personId&&r.nativeResidentId==record.nativeResidentId);

        sealed class HomeRevision{public int value=-1;}
        static readonly ConditionalWeakTable<FriendResidentBindingState,HomeRevision> homeRevisions=new ConditionalWeakTable<FriendResidentBindingState,HomeRevision>();
        public static void LiveHomeBindingChanged(FriendResidentBindingState binding)
        {
            if(!binding||!binding.hasHome||!binding.actor||binding.actor.Brain==null)return;
            var revision=homeRevisions.GetOrCreateValue(binding);if(revision.value==binding.homeRevision)return;revision.value=binding.homeRevision;
            var runtime=FriendsLifeRuntime.Instance;var actor=binding.actor;var identity=actor.GetComponent<FriendWorldActor>();
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!runtime.IsWorldRunning||!identity||ForeignOwner(runtime,identity.personId)||!OriginalNpcActivityPort.CanAmbientOwn(actor))return;
            var current=actor.Brain.StateMachine.Current;
            if(current is ResidentHomeState||current is ResidentReturnHomeState)actor.Brain.RequestScheduleChange();
        }

        public static void Capture(FriendsLifeRuntime runtime)
        {
            if(!runtime||!runtime.Ready||!runtime.IsAuthority)return;
            var previous=runtime.State.originalActivities??new List<OriginalAmbientActivityRecord>();
            var result=new List<OriginalAmbientActivityRecord>();
            foreach(var person in runtime.People.Where(p=>p.controllerKind==PersonControllerKind.Npc&&!p.existingHostResident))
            {
                if(ForeignOwner(runtime,person.personId)){DiscardPending(runtime,person.personId);continue;}
                var actor=runtime.ActorOf(person.personId);
                if(actor&&actor.Runtime!=null&&actor.Runtime.IsInteracting){DiscardPending(runtime,person.personId);continue;}
                var old=previous.FirstOrDefault(v=>v!=null&&v.worldId==runtime.State.worldId&&v.personId==person.personId&&v.nativeResidentId==person.nativeResidentId);
                // Save may run before the first recovery Update or while the
                // live housing binding is still initializing. A temporary
                // default Home must not erase an unhandled saved request.
                if(old!=null&&AwaitingRebind(runtime,old)){result.Add(old);continue;}
                if(!actor||actor.Brain==null||actor.Runtime==null||actor.Runtime.IsSnapshotApplying)
                {if(old!=null)result.Add(old);continue;}
                var state=actor.Runtime;var current=actor.Brain.StateMachine.Current;
                if(state.CurrentActivity==ResidentActivityType.Restaurant||state.IsInteracting||state.HasRestaurantAssignment)continue;
                string owner="schedule/"+state.CurrentScheduleEntryIndex,phase=current?.GetDebugName();
                if(current is ResidentSeatActivityState authored)
                {
                    var view=authored.Snapshot();
                    if(view.safeBoundary||view.phase=="StandingUp"||view.phase=="ReturningToApproach"||view.phase=="ExitBlocked")continue;
                    owner=authored.RequestOwner;phase=view.phase;
                }
                // Natural Home is derived from the same original schedule.
                // No pose, hide flag or private-room teleport is serialized.
                if(state.CurrentActivity==ResidentActivityType.Home)continue;
                result.Add(new OriginalAmbientActivityRecord{worldId=runtime.State.worldId,personId=person.personId,nativeResidentId=person.nativeResidentId,
                    requestOwner=owner,phase=phase,activity=(int)state.CurrentActivity,index=(int)state.CurrentWaypointIndex,patrolIndex=state.CurrentPatrolEntryPointIndex,
                    originalTarget=state.TargetDestination,rotation=state.TargetRotation});
            }
            runtime.State.originalActivities=result;
        }

        void Update()
        {
            var runtime=FriendsLifeRuntime.Instance;
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!runtime.IsWorldRunning)return;
            if(!ReferenceEquals(observed,runtime.State))
            {
                observed=runtime.State;pending.Clear();
                pending.AddRange((observed.originalActivities??new List<OriginalAmbientActivityRecord>()).Where(r=>r!=null&&r.worldId==observed.worldId));
            }
            foreach(var record in pending.ToArray())
            {
                var person=runtime.GetPerson(record.personId);
                if(person==null||person.nativeResidentId!=record.nativeResidentId||person.controllerKind!=PersonControllerKind.Npc||person.existingHostResident||
                    !Enum.IsDefined(typeof(ResidentActivityType),record.activity)||ForeignOwner(runtime,record.personId))
                {pending.Remove(record);continue;}
                var actor=runtime.ActorOf(record.personId);
                if(!actor||actor.Brain==null||actor.Runtime==null||!OriginalNpcActivityPort.CanAmbientOwn(actor)||actor.Brain.StateMachine.IsSuspended)continue;
                var binding=actor.GetComponent<FriendResidentBindingState>();
                if(!binding||!binding.hasHome)continue;
                var activity=(ResidentActivityType)record.activity;var index=(WayPointIndex)record.index;
                string owner=record.requestOwner??"schedule/"+actor.Runtime.CurrentScheduleEntryIndex;
                if(!actor.Brain.IsOriginalScheduleRequestCurrent(activity,index,owner))
                {actor.Brain.RequestScheduleChange();pending.Remove(record);continue;}
                // The original Brain checks whether a natural schedule request
                // still belongs to the current clock before dispatching it.
                // A private->public lease persists its own physical transfer.
                bool handled;
                if(OriginalNpcActivityPort.IsAuthoredActivity(activity))
                    handled=OriginalNpcActivityPort.TryBeginActivity(actor,activity,index,owner,out _);
                else
                {
                    actor.Runtime.TargetDestination=record.originalTarget;actor.Runtime.TargetRotation=record.rotation;
                    actor.Runtime.CurrentPatrolEntryPointIndex=record.patrolIndex;actor.Runtime.HasValidTarget=true;
                    if(FriendsOriginalActivityTransit.TryRouteResolvedSchedule(actor,activity,index,owner,out bool accepted,out _))handled=accepted;
                    else handled=actor.Brain.ContinueResolvedScheduleAfterPublicTransit(activity,index,owner,record.originalTarget,record.rotation,record.patrolIndex);
                }
                // A definitive local refusal remains a pending presentation
                // rebind; no new request identity or source command is made.
                if(handled)pending.Remove(record);
            }
        }
        void OnDestroy(){if(instance==this)instance=null;}
    }
}
