using System;
using System.Linq;
using SushiAndFish.CityExpansion;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    [Serializable] public sealed class FriendFacilityVisit
    {
        public string visitId,personId,facility,billId,phase="Queued",leaseId;
        // Same clock domain as Core resident.activityUntilGameMinutes.
        public double sourceUntil,sourceDecisionUntil;public string sourceActivity;
        public bool pausedBrain,exitRequested;public Vector3 returnPosition,destination;public Quaternion returnRotation;
    }
    [DefaultExecutionOrder(-60)]
    public sealed class FriendsFacilityController : MonoBehaviour
    {
        FriendsLifeRuntime runtime;float nextTick;
        public static FriendsFacilityController Instance {get;private set;}
        void Awake(){Instance=this;}
        void Update()
        {
            if(runtime!=FriendsLifeRuntime.Instance)
            {if(runtime)runtime.ActivityPresentationRequested-=Requested;runtime=FriendsLifeRuntime.Instance;if(runtime)runtime.ActivityPresentationRequested+=Requested;}
            var economy=FriendsEconomyService.Instance;if(!runtime||!runtime.Ready||!runtime.IsAuthority||!runtime.IsWorldRunning||!economy||Time.unscaledTime<nextTick)return;nextTick=Time.unscaledTime+.2f;
            foreach(var visit in economy.State.facilityVisits.Where(v=>v.phase!="Done").ToArray())Advance(visit,economy);
        }
        void Requested(string personId,string action)
        {
            // Exact source facility identities; unrelated source facilities keep
            // their existing host bindings instead of being guessed by substring.
            if(action=="facility:Cafe")RequestVisit(personId,"TownRestaurant","facility/"+Guid.NewGuid().ToString("N"),0);
            else if(action=="facility:Cinema")RequestVisit(personId,"Cinema","facility/"+Guid.NewGuid().ToString("N"),0);
        }
        public bool RequestVisit(string personId,string hostFacility,string sourceOp,double sourceUntil)
        {
            runtime=FriendsLifeRuntime.Instance;
            var economy=FriendsEconomyService.Instance;var person=runtime?.GetPerson(personId);
            if(!economy||person==null||person.controllerKind!=PersonControllerKind.Npc||string.IsNullOrEmpty(sourceOp)||(hostFacility!="Cinema"&&hostFacility!="TownRestaurant"))return false;
            var prior=economy.State.facilityVisits.FirstOrDefault(v=>v.visitId==sourceOp);if(prior!=null)return prior.personId==personId&&prior.facility==hostFacility;
            var native=runtime.Core.FindResident(person.nativeResidentId);
            economy.State.facilityVisits.Add(new FriendFacilityVisit{visitId=sourceOp,personId=personId,facility=hostFacility,billId="facility-bill/"+sourceOp,
                sourceUntil=sourceUntil,sourceDecisionUntil=native?.activityUntilGameMinutes??0,sourceActivity=native?.activity});runtime.NotifyChanged();return true;
        }
        void Advance(FriendFacilityVisit visit,FriendsEconomyService economy)
        {
            var actor=runtime.ActorOf(visit.personId);if(!actor||actor.Brain==null)return;
            if(economy.HasActiveJob(visit.personId)||actor.Runtime.IsInteracting)return;
            if(visit.facility=="TownRestaurant")
            {
                if(visit.phase=="Queued")
                {
                    if(runtime.State.leases.Any(l=>l.personId==visit.personId&&l.ownerEpisode!=visit.visitId))return;
                    if(visit.exitRequested){Finish(visit);return;}
                    if(runtime.HasActiveInPlaceTalk(visit.personId))return;
                    int stay=visit.sourceUntil>runtime.Core.State.gameMinutes?Mathf.CeilToInt((float)(visit.sourceUntil-runtime.Core.State.gameMinutes)):0;
                    if(!FriendFacilities.TryStartFriendVisit(TownMealScheduler.Instance,actor,visit.billId,stay))return;
                    var lease=new ActivityLease{leaseId="facility-lease/"+visit.visitId,personId=visit.personId,ownerEpisode=visit.visitId,facilityId="TownRestaurant",phase="Dining",safeBoundary=true};runtime.State.leases.Add(lease);visit.leaseId=lease.leaseId;visit.phase="Dining";
                }
                else if(visit.exitRequested&&actor.Brain.StateMachine.Current is ResidentTownMealState)actor.Brain.RequestScheduleChange();
                else if(!(actor.Brain.StateMachine.Current is ResidentTownMealState))Finish(visit);
                return;
            }
            var entrance=FindObjectsByType<CityCinemaPortal>(FindObjectsSortMode.None).FirstOrDefault(FriendFacilities.IsEntrance);
            if(!entrance)return;
            if(visit.phase=="Queued")
            {
                if(visit.exitRequested){Finish(visit);return;}
                if(runtime.HasActiveInPlaceTalk(visit.personId))return;
                if(runtime.State.leases.Any(l=>l.personId==visit.personId&&l.ownerEpisode!=visit.visitId))return;
                if(!economy.QuoteAndReserve(visit.personId,visit.billId,"Cinema",2000))return;
                if(!actor.Runtime.IsSnapshotApplying){actor.Brain.Pause();visit.pausedBrain=true;}
                if(!runtime.TryMoveWorldActor(visit.personId,entrance.transform.position,out var actual,.5f))
                {if(visit.pausedBrain)actor.Brain.Resume();visit.pausedBrain=false;economy.ReleaseUnstartedBill(visit.billId);return;}
                visit.destination=actual;
                visit.returnPosition=entrance.transform.position;visit.returnRotation=entrance.transform.rotation;
                var lease=new ActivityLease{leaseId="facility-lease/"+visit.visitId,personId=visit.personId,ownerEpisode=visit.visitId,facilityId="Cinema",phase="FacilityTravel",safeBoundary=true,destination=actual};runtime.State.leases.Add(lease);visit.leaseId=lease.leaseId;visit.phase="Travel";
            }
            else if(visit.phase=="Travel")
            {
                if(visit.exitRequested){actor.Brain.Movement.Stop();Finish(visit);return;}
                if(!Arrived(actor,visit.destination))return;
                if(!FriendFacilities.AdmitFriend(entrance,visit.personId,visit.billId))return;
                visit.phase="Viewing";runtime.NotifyChanged();
            }
            else if(visit.phase=="Viewing")
            {
                var person=runtime.GetPerson(visit.personId);var native=person==null?null:runtime.Core.FindResident(person.nativeResidentId);
                bool sourceEnded=visit.sourceUntil>0?runtime.Core.State.gameMinutes>=visit.sourceUntil:
                    native==null||native.activity!=visit.sourceActivity||native.activityUntilGameMinutes!=visit.sourceDecisionUntil;
                if(!visit.exitRequested&&!sourceEnded)return;
                var exit=FindObjectsByType<CityCinemaPortal>(FindObjectsSortMode.None).Where(p=>!FriendFacilities.IsEntrance(p)&&p.destination)
                    .OrderBy(p=>Vector3.Distance(p.transform.position,actor.transform.position)).FirstOrDefault();
                if(!exit)return; // The original exit is required; no fallback teleport.
                if(Vector3.Distance(actor.transform.position,exit.transform.position)<=.9f)
                {if(FriendFacilities.AdmitFriend(exit,visit.personId,visit.billId))Finish(visit);return;}
                if(runtime.TryMoveWorldActor(visit.personId,exit.transform.position,out var actual,.5f))
                {visit.destination=actual;visit.phase="ReturnTravel";runtime.NotifyChanged();}
            }
            else if(visit.phase=="ReturnTravel")
            {
                if(!Arrived(actor,visit.destination))return;
                var exit=FindObjectsByType<CityCinemaPortal>(FindObjectsSortMode.None).Where(p=>!FriendFacilities.IsEntrance(p)&&p.destination)
                    .OrderBy(p=>Vector3.Distance(p.transform.position,visit.destination)).FirstOrDefault();
                if(exit&&Vector3.Distance(exit.transform.position,visit.destination)<=.6f&&FriendFacilities.AdmitFriend(exit,visit.personId,visit.billId))Finish(visit);
            }
        }
        static bool Arrived(PaperPixelGames.Core.Ai.NPCs.ResidentController actor,Vector3 target)
        {
            var agent=actor.GetComponent<NavMeshAgent>();return agent&&agent.enabled&&agent.isOnNavMesh&&!agent.pathPending&&agent.pathStatus==NavMeshPathStatus.PathComplete&&
                actor.Brain.Movement.HasArrived()&&Vector3.Distance(actor.transform.position,target)<=.9f&&Vector3.Distance(actor.Brain.Movement.Destination,target)<=.15f;
        }
        public bool Cancel(string visitId)
        {
            var economy=FriendsEconomyService.Instance;var visit=economy?.State.facilityVisits.FirstOrDefault(v=>v.visitId==visitId);if(visit==null)return false;
            if(visit.phase=="Done")return true;visit.exitRequested=true;runtime.NotifyChanged();return true;
        }
        void Finish(FriendFacilityVisit visit)
        {
            FriendsEconomyService.Instance?.CancelBill(visit.billId);
            runtime.State.leases.RemoveAll(l=>l.leaseId==visit.leaseId);var actor=runtime.ActorOf(visit.personId);
            if(visit.pausedBrain&&actor&&!runtime.State.leases.Any(l=>l.personId==visit.personId)&&FriendsEconomyService.Instance?.HasActiveJob(visit.personId)!=true)
            {actor.Brain.Resume();actor.Brain.RequestScheduleChange();}visit.phase="Done";runtime.NotifyChanged();runtime.SaveNow();
        }
        void OnDestroy(){if(runtime)runtime.ActivityPresentationRequested-=Requested;if(Instance==this)Instance=null;}
    }
}
