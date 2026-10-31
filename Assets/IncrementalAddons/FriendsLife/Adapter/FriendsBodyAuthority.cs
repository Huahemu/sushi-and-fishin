using System;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    // The persisted ActivityLease is the body claim. This is a shared boundary
    // around the existing native activity, job and source-episode owners.
    public static class FriendsBodyAuthority
    {
        public static string IdentityKey(ResidentController actor)
        {
            var runtime=FriendsLifeRuntime.Instance;
            var id=actor?actor.GetComponent<FriendWorldActor>()?.personId:null;
            if(!runtime||!runtime.Ready||string.IsNullOrEmpty(id))return null;
            var lease=runtime.State.leases.FirstOrDefault(l=>l.personId==id);
            var job=FriendsEconomyService.Instance?.State.jobs.FirstOrDefault(j=>j.personId==id&&j.state!=FriendJobPhase.Done);
            return runtime.State.worldId+"/"+id+"/"+(lease!=null?lease.ownerEpisode+"/"+lease.leaseId:job!=null?job.contractId:"ambient");
        }
        public static bool CanAmbientOwn(ResidentController actor)
        {
            if(!actor)return false;
            if(!FriendResidentBindings.IsOwned(actor))return true;
            var runtime=FriendsLifeRuntime.Instance;var id=actor.GetComponent<FriendWorldActor>()?.personId;
            return runtime&&runtime.Ready&&runtime.IsAuthority&&runtime.IsWorldRunning&&!string.IsNullOrEmpty(id)&&
                !runtime.State.leases.Any(l=>l.personId==id)&&FriendsEconomyService.Instance?.HasActiveJob(id)!=true;
        }
        public static bool IsOwned(FriendsLifeRuntime runtime,string personId,string ownerId)
            =>runtime&&runtime.Ready&&runtime.State.leases.Any(l=>l.personId==personId&&l.ownerEpisode==ownerId&&l.phase!="JobHandover");
        static bool CanContinueOriginalHome(ResidentController actor,string facilityId,out string reason)
        {
            reason=null;
            if(string.IsNullOrEmpty(facilityId)||!(facilityId.StartsWith("OriginalActivityTransit",StringComparison.Ordinal)||facilityId=="SushiWork"||facilityId=="Casino")||
                !CanAmbientOwn(actor)||actor.Runtime==null||actor.Brain?.StateMachine==null)return false;
            var state=actor.Runtime;
            if(state.CurrentActivity!=ResidentActivityType.Home&&state.CurrentActivity!=ResidentActivityType.Idle)return false;
            var id=actor.GetComponent<FriendWorldActor>()?.personId;var runtime=FriendsLifeRuntime.Instance;
            if(state.SystemState!=ResidentSystemState.Normal||state.IsSnapshotApplying||state.IsInteracting||state.IsWorking||
                state.IsInRestaurant||state.HasRestaurantAssignment||state.IsSwimming||actor.GetComponent<FriendResidentDormancy>()||
                FriendsHomeWorldActions.Controls(id)||FriendsHomeStandingNavigation.Instance&&FriendsHomeStandingNavigation.Instance.Controls(id)||
                runtime.GetComponent<FriendsNpcRestController>()?.IsResting(id)==true)
            {reason="OriginalHomeStillOwned";return false;}
            var machine=actor.Brain.StateMachine;
            if(machine.IsSuspended||machine.Current!=null&&!machine.Current.CanInterrupt(ResidentRequestPriority.PlayerInteraction))
            {reason="OriginalHomeCannotYield";return false;}
            var agent=actor.GetComponent<NavMeshAgent>();
            if(!agent||!agent.enabled||!agent.isOnNavMesh||Vector3.Distance(actor.transform.position,agent.nextPosition)>.25f)
            {reason="OriginalHomeBodyNotContinuous";return false;}
            return true;
        }
        public static bool TryAcquire(FriendsLifeRuntime runtime,string personId,string ownerId,string facilityId,out ActivityLease lease,out string reason)
        {
            lease=null;reason=null;
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!runtime.IsWorldRunning||string.IsNullOrEmpty(ownerId))
            {reason="BodyAuthorityNotReady";return false;}
            var person=runtime.GetPerson(personId);var actor=runtime.ActorOf(personId);
            bool human=person?.controllerKind==PersonControllerKind.Human;
            if(person==null||(!human&&(!actor||!FriendResidentBindings.IsOwned(actor)))||human&&!runtime.TransformOf(personId))
            {reason="BoundPersonIdentityRequired";return false;}
            var existing=runtime.State.leases.Where(l=>l.personId==personId).ToArray();
            if(existing.Length>0)
            {
                if(existing.Length==1&&existing[0].ownerEpisode==ownerId)
                {
                    lease=existing[0];
                    if(lease.phase=="JobHandover")
                    {
                        // Waiting handle is not yet body ownership.
                        if(runtime.HasActiveInPlaceTalk(personId)){reason="ActiveInPlaceTalk";return false;}
                        if(FriendsEconomyService.Instance?.HasActiveJob(personId)==true||!human&&!FriendOriginalNpcActivityBoundary.CanUpgradeHandover(runtime,personId,ownerId,out reason))
                        {if(string.IsNullOrEmpty(reason))reason="WaitingForOriginalJobHandover";return false;}
                        lease.leaseId=Guid.NewGuid().ToString("N");lease.phase="Reserved";lease.facilityId=facilityId;lease.safeBoundary=true;
                        if(!human)actor.Brain.Pause();runtime.NotifyChanged();
                    }
                    else if(!human&&!actor.Brain.StateMachine.IsSuspended)actor.Brain.Pause();
                    return true;
                }
                reason="BodyOwnedByAnotherOperation";return false;
            }
            // Existing leases above retain their owner. An in-place talk has
            // no lease, so reject only a NEW body claim and do not request an exit.
            if(runtime.HasActiveInPlaceTalk(personId))
            {reason="ActiveInPlaceTalk";return false;}
            bool originalHome=!human&&CanContinueOriginalHome(actor,facilityId,out reason);
            if(!originalHome&&(!runtime.IsWorldAvailable(personId)||!human&&!FriendOriginalNpcActivityBoundary.CanAcquire(actor,out reason)))
            {
                if(!human&&OriginalNpcActivityPort.RequestSafeExit(actor,ownerId))reason="WaitingForOriginalSafeExit";
                if(string.IsNullOrEmpty(reason))reason="OriginalActivityHasNotReleased";
                return false;
            }
            lease=new ActivityLease{leaseId=Guid.NewGuid().ToString("N"),personId=personId,ownerEpisode=ownerId,
                facilityId=facilityId,phase="Reserved",resumeToken=human?"Human":actor.Runtime.CurrentActivity.ToString(),safeBoundary=true};
            if(!human)actor.Brain.Pause();runtime.State.leases.Add(lease);runtime.NotifyChanged();return true;
        }
        public static bool Release(FriendsLifeRuntime runtime,string personId,string ownerId,bool nativeSafeBoundary)
        {
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!nativeSafeBoundary)return false;
            var leases=runtime.State.leases.Where(l=>l.personId==personId).ToArray();
            if(leases.Length!=1||leases[0].ownerEpisode!=ownerId)return false;
            if(runtime.GetPerson(personId)?.controllerKind==PersonControllerKind.Human)
            {runtime.State.leases.Remove(leases[0]);runtime.NotifyChanged();return true;}
            var actor=runtime.ActorOf(personId);var agent=actor?actor.GetComponent<NavMeshAgent>():null;
            if(!actor||actor.Brain==null||!agent||!agent.enabled||!agent.isOnNavMesh||
                Vector3.Distance(actor.transform.position,agent.nextPosition)>.25f)return false;
            runtime.State.leases.Remove(leases[0]);actor.Brain.Resume();actor.Brain.RequestScheduleChange();
            FriendOwnedAmbientPathGuardian.ProtectAfterScheduleChange(actor);runtime.NotifyChanged();return true;
        }
    }
}
