using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    // Reads original public NPC activity/motion ownership. It neither exits nor
    // synchronizes an original anchored activity on behalf of a new owner.
    public static class FriendOriginalNpcActivityBoundary
    {
        public static bool CanAcquire(ResidentController actor,out string reason)=>CanAcquireCore(actor,out reason,false);
        public static bool CanUpgradeHandover(FriendsLifeRuntime runtime,string personId,string ownerId,out string reason)
        {
            reason="Same original completed job handover required";
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!runtime.IsWorldRunning||FriendsEconomyService.Instance?.HasActiveJob(personId)!=false)return false;
            var leases=runtime.State.leases.Where(l=>l.personId==personId).ToArray();
            if(leases.Length!=1||leases[0].ownerEpisode!=ownerId||leases[0].phase!="JobHandover"||
                (leases[0].facilityId!="SourceWorkBoundary"&&leases[0].facilityId!="SourceRecovery")||
                !runtime.State.episodes.Any(e=>e.episodeId==ownerId&&!e.committed&&new[]{e.focusPersonId,e.targetPersonId,e.anotherPersonId}.Contains(personId)))return false;
            return CanAcquireCore(runtime.ActorOf(personId),out reason,true);
        }
        static bool CanAcquireCore(ResidentController actor,out string reason,bool ownHandover)
        {
            reason=null;
            if(!actor||actor.Brain==null||actor.Runtime==null){reason="OriginalNpcNotReady";return false;}
            var state=actor.Runtime;var machine=actor.Brain.StateMachine;
            bool ownSnapshotPause=ownHandover&&machine!=null&&machine.IsSuspended&&state.IsSnapshotApplying&&state.SystemState==ResidentSystemState.SnapshotApplying;
            if(state.SystemState!=ResidentSystemState.Normal&&!ownSnapshotPause||state.IsInteracting||state.IsWorking||state.IsInRestaurant||state.HasRestaurantAssignment||state.IsSwimming||
                state.IsSnapshotApplying&&!ownSnapshotPause||machine==null||machine.IsSuspended&&!ownSnapshotPause||actor.GetComponent<FriendResidentDormancy>())
            {reason="OriginalNpcActivityOwned";return false;}
            if(machine.Current!=null&&!machine.Current.CanInterrupt(ResidentRequestPriority.PlayerInteraction))
            {reason="OriginalNpcCannotInterrupt";return false;}
            var agent=actor.GetComponent<NavMeshAgent>();
            if(!agent||!agent.enabled||!agent.isOnNavMesh){reason="OriginalNpcNavigationNotReady";return false;}
            // Original MoveToScheduleTargetState owns its arrived anchored pose
            // (beach, benches, fishing, etc.) until its own schedule releases it.
            if(state.HasArrived&&state.CurrentActivity!=ResidentActivityType.Home&&!agent.updatePosition)
            {reason="OriginalNpcAnchoredPose";return false;}
            if(Vector3.Distance(actor.transform.position,agent.nextPosition)>.25f)
            {reason="OriginalNpcBodyAgentDiscontinuity";return false;}
            return true;
        }
    }
}
