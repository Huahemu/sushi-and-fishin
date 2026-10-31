using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Staff;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    [Serializable]public sealed class FriendStaffEntry
    {
        public string personId,ownerId,phase="Queued",failure;
        public StaffRole role;public Vector3 destination;
        public bool arrived,sourceBodyObserved,exitObserved,exitUsedNativeRecovery;
        public int employeeId;public Vector3 exitPosition;public Quaternion exitRotation=Quaternion.identity;
    }
    public sealed partial class FriendsEconomySave
    {public List<FriendStaffEntry> staffEntries=new List<FriendStaffEntry>();}
    public sealed partial class FriendsEconomyService
    {
        FriendStaffEntry StaffEntry(string personId)
        {
            State.staffEntries=State.staffEntries??new List<FriendStaffEntry>();
            return State.staffEntries.LastOrDefault(x=>x.personId==personId&&x.phase!="Done"&&x.phase!="Cancelled");
        }
        public bool HasReadyStaffArrival(string personId)
        {
            var entry=StaffEntry(personId);var actor=runtime?.ActorOf(personId);
            return entry!=null&&entry.arrived&&entry.phase=="Arrived"&&actor&&
                FriendsBodyAuthority.IsOwned(runtime,personId,entry.ownerId)&&
                Vector3.Distance(actor.transform.position,entry.destination)<=.2f;
        }
        void AdvanceStaffArrival(AuthorizedSushiJob authorization,StaffManager manager,int keptEmployeeId)
        {
            if(HasActiveJob(authorization.personId))return;
            var source=StaffWorkplaceRegistry.Instance;var actor=runtime.ActorOf(authorization.personId);
            if(!source||!source.SpawnPoint||!actor||actor.Brain==null)return;
            var entry=StaffEntry(authorization.personId);
            if(entry==null)
            {
                entry=new FriendStaffEntry{personId=authorization.personId,ownerId="staff-entry/"+Guid.NewGuid().ToString("N"),role=authorization.role};
                State.staffEntries.Add(entry);
            }
            if(entry.role!=authorization.role){CancelStaffArrival(entry.personId);return;}
            if(!FriendsBodyAuthority.TryAcquire(runtime,entry.personId,entry.ownerId,"SushiWork",out var lease,out var failure))
            {entry.failure=failure;return;}
            if(entry.phase=="Queued"||entry.phase=="Travel")
            {
                var agent=actor.GetComponent<NavMeshAgent>();
                bool arrived=entry.phase=="Travel"&&agent&&agent.enabled&&agent.isOnNavMesh&&!agent.pathPending&&
                    agent.pathStatus==NavMeshPathStatus.PathComplete&&actor.Brain.Movement.HasArrived()&&
                    Vector3.Distance(actor.transform.position,entry.destination)<=.2f&&
                    Vector3.Distance(actor.transform.position,agent.nextPosition)<=.2f;
                if(arrived)
                {entry.arrived=true;entry.phase="Arrived";lease.phase="AtStaffEntry";lease.safeBoundary=true;Commit();}
                else if(entry.phase=="Queued"||!agent||!agent.hasPath)
                {
                    // Original StaffNpc.Awake resolves its authored spawn with radius2.
                    // Walk to that same supported projection before native body transfer.
                    if(!actor.Brain.Movement.TryResolveDestination(source.SpawnPoint.position,2f,out var originalEntry)||!OriginalNpcActivityPort.FootFits(actor,originalEntry))
                    {entry.failure="Original staff spawn has no physically supported native projection";return;}
                    if(!runtime.TryMoveWorldActor(entry.personId,originalEntry,out var target,.05f))
                    {entry.failure=runtime.LastWorldMoveDiagnostic;return;}
                    if(Vector3.Distance(target,originalEntry)>.15f){entry.failure="Original staff spawn and the supported arrival differ";return;}
                    entry.destination=target;entry.phase="Travel";entry.failure=null;lease.destination=target;if(lease.phase!="PrivateCommonTransit")lease.phase="Travel";lease.safeBoundary=false;Commit();
                }
            }
            // A kept employee starts its next shift; only a first hire creates an entry.
            if(entry.phase=="Arrived"){if(keptEmployeeId>0)FriendStaffPort.TryResume(manager,entry.personId,keptEmployeeId);else FriendStaffPort.TryHire(manager,entry.personId,entry.role);}
        }
        public void StaffEmployeeSpawned(string personId,int employeeId)
        {
            var entry=StaffEntry(personId);
            if(entry==null||!entry.arrived||entry.sourceBodyObserved&&entry.employeeId==employeeId)return;
            var npc=StaffNpcFor(employeeId,false);if(!npc)return;
            entry.employeeId=employeeId;entry.sourceBodyObserved=true;entry.phase="Working";
            var actor=runtime.ActorOf(personId);
            if(actor&&!actor.GetComponent<FriendResidentDormancy>())actor.gameObject.AddComponent<FriendResidentDormancy>().Suspend(actor);
            // Transfer the existing entry claim to its native employee contract.
            // The brain remains paused; the original StaffNpc is the visible body.
            runtime.State.leases.RemoveAll(x=>x.personId==personId&&x.ownerEpisode==entry.ownerId);Commit();
        }
        public void ObserveNativeStaffRemoval(int employeeId,StaffNpc npc)
        {
            var job=State.jobs.FirstOrDefault(x=>x.employeeId==employeeId&&x.state!=FriendJobPhase.Done);
            var entry=job==null?null:StaffEntry(job.personId);var registry=StaffWorkplaceRegistry.Instance;
            if(!Authority||job==null||entry==null||!entry.sourceBodyObserved||!npc||!registry||!registry.SpawnPoint||
                Vector3.Distance(npc.transform.position,registry.SpawnPoint.position)>.75f)return;
            var agent=npc.GetComponent<NavMeshAgent>();
            if(!agent||!agent.enabled||!agent.isOnNavMesh||agent.pathPending||Vector3.Distance(npc.transform.position,agent.nextPosition)>.25f)return;
            entry.exitObserved=true;entry.exitUsedNativeRecovery=npc.Recovered;entry.exitPosition=npc.transform.position;entry.exitRotation=npc.transform.rotation;entry.phase="Returning";
            runtime.NotifyChanged();
        }
        bool TryRestoreStaffBody(FriendJobContract job,ResidentController actor)
        {
            var entry=StaffEntry(job.personId);if(entry==null)return true;
            if(!entry.sourceBodyObserved){CancelStaffArrival(job.personId);return entry.phase=="Cancelled";}
            if(!entry.exitObserved)
            {
                // Scene loading destroys the old employee body; that is not a
                // witnessed walk out. Only this explicitly restored contract may
                // release to the already loaded, supported resident body.
                var current=actor?actor.GetComponent<NavMeshAgent>():null;
                if(!loadedStaffReturns.Contains(job.contractId)||!actor||actor.Brain==null||!current||!current.enabled||!current.isOnNavMesh||
                    Vector3.Distance(actor.transform.position,current.nextPosition)>.25f||!OriginalNpcActivityPort.FootFits(actor,actor.transform.position))
                {job.finishReason="AwaitOriginalStaffExitWitness";return false;}
                loadedStaffReturns.Remove(job.contractId);entry.phase="Done";entry.failure=null;job.finishReason="load-recovered";return true;
            }
            if(!actor||actor.Brain==null){job.finishReason="AwaitOriginalStaffExitWitness";return false;}
            if(!actor.Brain.Movement.TryResolveDestination(entry.exitPosition,.25f,out var destination)||
                Vector3.Distance(destination,entry.exitPosition)>.25f||!actor.Brain.Movement.Warp(destination,entry.exitRotation))
            {job.finishReason="OriginalStaffExitBodyHandoverPending";return false;}
            var agent=actor.GetComponent<NavMeshAgent>();
            if(!agent||Vector3.Distance(actor.transform.position,destination)>.25f||Vector3.Distance(actor.transform.position,agent.nextPosition)>.25f)return false;
            loadedStaffReturns.Remove(job.contractId);entry.phase="Done";entry.failure=null;return true;
        }
        void CancelStaffArrival(string personId)
        {
            var entry=StaffEntry(personId);if(entry==null||entry.sourceBodyObserved)return;
            var actor=runtime.ActorOf(personId);actor?.Brain?.Movement.Stop();
            if(!FriendsBodyAuthority.IsOwned(runtime,personId,entry.ownerId)||FriendsBodyAuthority.Release(runtime,personId,entry.ownerId,true))entry.phase="Cancelled";
        }
    }
}
