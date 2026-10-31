using System;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendJobStation
    {
        static string CabinLease(FriendJobContract job)=>"casino-cabin/"+job.contractId;
        bool CabinOwner(FriendJobContract job)=>owner==job.personId&&ownerContractId==job.contractId;
        void HoldCabinLease(FriendsLifeRuntime runtime,FriendJobContract job,string phase)
        {
            var lease=runtime.State.leases.FirstOrDefault(l=>l.leaseId==CabinLease(job));
            if(lease==null){lease=new ActivityLease{leaseId=CabinLease(job),personId=job.personId,ownerEpisode=job.contractId,facilityId=slotId};runtime.State.leases.Add(lease);}
            lease.phase=phase;lease.destination=job.state==FriendJobPhase.Handover?job.cabinGate:job.cabinDuty;lease.safeBoundary=false;
        }
        static bool OriginalAgentAtBody(ResidentController actor)
        {
            var agent=actor?actor.GetComponent<NavMeshAgent>():null;
            return agent&&agent.enabled&&agent.isOnNavMesh&&Vector3.Distance(actor.transform.position,agent.nextPosition)<=.15f;
        }
        static bool AtGate(ResidentController actor,Vector3 gate)
        {
            var agent=actor?actor.GetComponent<NavMeshAgent>():null;
            return OriginalAgentAtBody(actor)&&!agent.pathPending&&agent.pathStatus==NavMeshPathStatus.PathComplete&&
                actor.Brain.Movement.HasArrived()&&Vector3.Distance(actor.transform.position,gate)<=.2f&&Vector3.Distance(actor.Brain.Movement.Destination,gate)<=.15f;
        }
        bool TickCabinContract(FriendJobContract job,long micros,FriendsEconomyService economy)
        {
            if(sourceKind!="CasinoElevator")return false;
            var runtime=FriendsLifeRuntime.Instance;var actor=runtime.ActorOf(job.personId);
            if(!CabinOwner(job))
            {
                if(job.cabinReturnRequired)
                {
                    job.state=FriendJobPhase.Handover;
                    if(string.IsNullOrEmpty(owner)){owner=job.personId;ownerContractId=job.contractId;travelling=false;OnDuty=false;}
                }
                else{job.finishReason="station-contract-ownership-changed";job.state=FriendJobPhase.Done;economy.RestoreActor(job);return true;}
            }
            if(!actor||actor.Brain==null)
            {job.finishReason="waiting-for-actual-cabin-actor";return true;}
            if(actor.Runtime!=null&&!actor.Runtime.IsSnapshotApplying)actor.Brain.Pause();
            if(!service||!service.isActiveAndEnabled||!(IsOpen?.Invoke()??true))job.state=FriendJobPhase.Handover;
            actor.Brain.SetModelVisible(true);
            if(job.state==FriendJobPhase.Handover)
            {
                if(serviceDepth>0||(OnDuty||job.cabinReturnRequired)&&!(SafeBoundary?.Invoke()??true))return true;
                if(job.cabinReturnRequired)
                {
                    HoldCabinLease(runtime,job,"CasinoCabinPhysicalExit");
                    if(!FriendCasinoGroundNavigation.OriginalGroundAvailable())
                    {job.finishReason="waiting-for-original-ground-button-return";return true;}
                    var motor=actor.GetComponent<FriendCasinoCabinMotor>();
                    if(motor&&!string.Equals(motor.Owner,job.contractId,StringComparison.Ordinal))
                    {job.finishReason="waiting-for-own-cabin-motor";return true;}
                    if(!motor)
                    {
                        // A normal restore starts public jobs in Handover. If
                        // its body is already outside on real navigation, walk
                        // the original complete route to the retained gate;
                        // never call MoveTo with an actual body inside the hole.
                        if(OriginalAgentAtBody(actor)&&Vector3.Distance(actor.transform.position,job.cabinGate)>.2f)
                        {
                            if(!travelling)
                            {
                                if(!FriendCasinoGroundNavigation.TryResolve(this,actor,out var gate)||
                                    !actor.Brain.Movement.MoveTo(gate,actor.Brain.EffectiveMoveSpeed,.15f,true))
                                {job.finishReason="waiting-for-real-loaded-return-gate-route";return true;}
                                job.cabinGate=gate;travelling=true;
                            }
                            if(!AtGate(actor,job.cabinGate))return true;
                            travelling=false;
                        }
                        if(!FriendCasinoCabinMotor.TryBegin(actor,job.contractId,stationTransform,job.cabinGate,job.cabinDuty,true,out motor,out var reason))
                        {job.finishReason="physical-return-pending/"+reason;return true;}
                    }
                    motor.RequestExit();
                    if(!motor.Exited||!motor.SafeBoundary||!OriginalAgentAtBody(actor)||Vector3.Distance(actor.transform.position,job.cabinGate)>.15f)
                    {job.finishReason=motor.Failure??"waiting-for-physical-return-and-agent-readback";return true;}
                    motor.Forget(job.contractId);job.cabinReturnRequired=false;
                    runtime.State.leases.RemoveAll(l=>l.leaseId==CabinLease(job)&&l.ownerEpisode==job.contractId);
                }
                if(CabinOwner(job))Restore();
                job.state=FriendJobPhase.Done;job.finishReason="actual-cabin-exit-and-original-agent-restored";economy.RestoreActor(job);return true;
            }
            if(!CabinOwner(job)){job.state=FriendJobPhase.Handover;return true;}
            if(job.state==FriendJobPhase.Travel)
            {
                if(!job.cabinReturnRequired)
                {
                    if(!travelling)
                    {
                        if(!TryResolveRoute(actor,out var gate)||!actor.Brain.Movement.MoveTo(gate,actor.Brain.EffectiveMoveSpeed,.15f,true))
                        {job.finishReason="exterior-cabin-gate-route-unavailable";job.state=FriendJobPhase.Handover;return true;}
                        job.workPosition=gate;travelling=true;travelActiveMicros=0;
                    }
                    if(micros>0)travelActiveMicros=micros>long.MaxValue-travelActiveMicros?long.MaxValue:travelActiveMicros+micros;
                    if(travelActiveMicros>90000000){job.finishReason="exterior-cabin-gate-travel-timeout";job.state=FriendJobPhase.Handover;return true;}
                    if(!AtGate(actor,job.workPosition))return true;
                    if(runtime.State.leases.Any(l=>l.personId==job.personId&&l.leaseId!=CabinLease(job)))
                    {job.finishReason="waiting-for-exclusive-cabin-owner";return true;}
                    if(!FriendCasinoGroundNavigation.TryGetCabinRoute(this,actor,out var gatePoint,out var duty)||Vector3.Distance(actor.transform.position,gatePoint)>.2f)
                    {job.finishReason="actual-front-opening-route-unavailable";return true;}
                    job.cabinGate=gatePoint;job.cabinDuty=duty;HoldCabinLease(runtime,job,"CasinoCabinPhysicalTravel");
                    if(!FriendCasinoCabinMotor.TryBegin(actor,job.contractId,stationTransform,gatePoint,duty,false,out var started,out var reason))
                    {
                        runtime.State.leases.RemoveAll(l=>l.leaseId==CabinLease(job)&&l.ownerEpisode==job.contractId);
                        job.finishReason="physical-cabin-travel-rejected/"+reason;return true;
                    }
                    job.cabinReturnRequired=true;job.workPosition=duty;travelling=false;runtime.NotifyChanged();
                }
                var motor=actor.GetComponent<FriendCasinoCabinMotor>();
                if(!motor||motor.Owner!=job.contractId){job.finishReason="actual-cabin-motor-missing";job.state=FriendJobPhase.Handover;return true;}
                if(motor.Phase=="Blocked"){job.finishReason=motor.Failure;job.state=FriendJobPhase.Handover;return true;}
                if(!motor.Arrived||Vector3.Distance(actor.transform.position,job.cabinDuty)>.15f||serviceDepth>0||!(SafeBoundary?.Invoke()??true))return true;
                // The motor has already walked the real body here. This final
                // transition changes rotation/appearance only, never position.
                actor.Brain.Movement.ApplyRotation(stationTransform.rotation);
                TakeAppearance();OnDuty=true;job.state=FriendJobPhase.OnDuty;job.finishReason=null;HoldCabinLease(runtime,job,"CasinoCabinOnDuty");runtime.NotifyChanged();return true;
            }
            if(job.state==FriendJobPhase.OnDuty)
            {
                var motor=actor.GetComponent<FriendCasinoCabinMotor>();
                if(!job.cabinReturnRequired||!motor||motor.Owner!=job.contractId||!motor.Arrived||Vector3.Distance(actor.transform.position,job.cabinDuty)>.15f)
                {job.finishReason="actual-cabin-duty-body-or-owner-changed";job.state=FriendJobPhase.Handover;return true;}
                economy.PayWork(job,micros);
            }
            return true;
        }
    }
}
