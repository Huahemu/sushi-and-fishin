using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using IncrementalAddons.Casino;
using SushiAndFish.Casino;
using SushiAndFish.Casino.Original;
using SushiAndFish.Casino.Original.Extensions;
namespace SushiAndFish.FriendsLife
{
    [Serializable] public sealed class FriendCasinoTravelReceipt
    {
        public string phase,detail; public double hostMinute; public int floor;public Vector3 position;
        public float lobbyWalkMeters,floorWalkMeters,returnWalkMeters; public bool insideOriginalCab;
    }
    [Serializable] public sealed class FriendCasinoJourney
    {
        public string visitId,personId,sourceOperation,phase="Queued",leaseId,machinePath,machineSourcePath,roundId,lastFailure;
        public string workKind,stationId,workContractId;
        public bool entranceStepping;public int entrancePointIndex;public List<Vector3> entrancePoints=new List<Vector3>();
        public int targetFloor=1,savedFloor,transferDestinationFloor;public long quoteCoins;public bool cancelRequested,autonomous,hasSavedPose;
        public Vector3 destination,savedPosition,cabinLocalPosition;public Quaternion savedRotation,cabinLocalRotation;
        public float lobbyWalkMeters,floorWalkMeters,returnWalkMeters;public int navigationDiscontinuities;
        public List<FriendCasinoTravelReceipt> receipts=new List<FriendCasinoTravelReceipt>();
    }
    [Serializable] public sealed class FriendsCasinoTravelSave
    {
        public int version=2,currentFloor;public bool retainedHumansOutside;public double nextAutonomousGameMinute;public List<FriendCasinoJourney> journeys=new List<FriendCasinoJourney>();
    }

    // NEW coordinator: original button request -> observed original floor ->
    // NEW NPC only teleports to the authored playerSpawnPosition. No original
    // file/field hooks, door sequence, carving, navigation bake or cab walk.
    [DefaultExecutionOrder(-54)] public sealed partial class FriendsCasinoElevatorBridge:MonoBehaviour
    {
        public static FriendsCasinoElevatorBridge Instance{get;private set;}public FriendsCasinoTravelSave State{get;private set;}=new FriendsCasinoTravelSave();public string LastFailure{get;private set;}
        public bool NavigationReady=>ParkCasinoIntegration.Instance&&ParkCasinoIntegration.Instance.Ready;
        public bool IsRestorePending=>false;public int ActiveJourneyCount=>State.journeys.Count(j=>j.phase!="Done"&&j.phase!="Cancelled");
        FriendsLifeRuntime runtime;NativeCasinoButtonObserver buttons;readonly Dictionary<string,int> originalAgentTypes=new Dictionary<string,int>();readonly HashSet<string> moved=new HashSet<string>();readonly HashSet<string> restoredActors=new HashSet<string>();readonly string restoreRun=Guid.NewGuid().ToString("N");
        void Awake(){Instance=this;buttons=gameObject.AddComponent<NativeCasinoButtonObserver>();buttons.Completed+=CompleteNativeButton;}
        void OnDestroy(){if(buttons)buttons.Completed-=CompleteNativeButton;if(runtime)runtime.ActivityPresentationRequested-=Requested;if(Instance==this)Instance=null;}
        void Requested(string person,string action){if(action=="host:Casino"||action=="facility:Casino")CasinoCore.FriendsCasinoJourneyService.Instance?.RequestSelection(person);}
        public bool RequestVisit(string personId,string sourceOperation,int floor,long quote,bool autonomous,out string visitId,string workKind=null)
        {
            visitId=null;var service=CasinoCore.FriendsCasinoJourneyService.Instance;
            if(!service||!string.IsNullOrEmpty(workKind))return false;
            var target=CasinoCore.NativeCasinoTargetDirectory.Observe().FirstOrDefault(t=>t.floor==floor&&t.type==CasinoGameType.SlotMachine.ToString());
            return target!=null&&service.TryStart(personId,target.id,sourceOperation,quote,false,out visitId,out _);
        }

        public bool CancelVisit(string id){var j=State.journeys.FirstOrDefault(x=>x.visitId==id);if(j==null)return false;if(j.phase=="Done"||j.phase=="Cancelled")return true;j.cancelRequested=true;runtime?.NotifyChanged();return true;}
        void Lease(FriendCasinoJourney j){if(!runtime.State.leases.Any(l=>l.leaseId==j.leaseId))runtime.State.leases.Add(new ActivityLease{leaseId=j.leaseId,personId=j.personId,ownerEpisode=j.visitId,facilityId="Casino",phase=j.phase,safeBoundary=false});}
        void Record(FriendCasinoJourney j,string phase,string detail=null){j.phase=phase;j.lastFailure=detail;j.receipts.Add(new FriendCasinoTravelReceipt{phase=phase,detail=detail,hostMinute=runtime.HostGameMinute,floor=ParkCasinoIntegration.Instance?ParkCasinoIntegration.Instance.CurrentFloor:0,position=runtime.ActorOf(j.personId)?runtime.ActorOf(j.personId).transform.position:Vector3.zero});runtime.NotifyChanged();}
        void Update()
        {
            if(runtime!=FriendsLifeRuntime.Instance){if(runtime)runtime.ActivityPresentationRequested-=Requested;runtime=FriendsLifeRuntime.Instance;if(runtime)runtime.ActivityPresentationRequested+=Requested;}
            // C008 owns all new casino travel. Historic records remain readable;
            // the legacy spawn-after-button coordinator is never advanced.
        }
        void Advance(FriendCasinoJourney j,ParkCasinoIntegration casino)
        {
            var actor=runtime.ActorOf(j.personId);if(!actor)return;
            if(TryAdvanceSameFloorWork(j,casino,actor))return;
            if(j.hasSavedPose&&j.phase!="NativeButtonQueued"&&j.phase!="NativeButtonTransfer"&&!restoredActors.Contains(j.visitId)&&!CanObserveGroundWorkWithoutTransfer(j,casino))
            {
                var resume=buttons.Request(j.savedFloor,j.personId,j.visitId+"/restore/"+restoreRun);j.lastFailure=resume.message;return;
            }
            if(j.phase=="NativeButtonQueued")
            {
                if(j.cancelRequested){Record(j,"Cancelled","未发出原按钮请求。");return;}
                bool entry=casino.CurrentFloor!=0&&j.targetFloor!=0&&!j.hasSavedPose;var r=buttons.Request(entry?0:j.targetFloor,j.personId,j.visitId+(entry?"/entry":"/out"));j.lastFailure=r.message;
                if(r.sourceStarted){Lease(j);actor.Brain.Pause();Record(j,"NativeButtonTransfer");}return;
            }
            if(j.phase=="NativeButtonTransfer"||j.phase=="NativeReturnTransfer")
            {
                bool returning=j.phase=="NativeReturnTransfer";
                if(!buttons.Requests.Any(r=>r.operationId==j.visitId+(returning?"/return":"/out")&&r.sourceStarted))
                {var resumed=buttons.Request(returning?0:j.targetFloor,j.personId,j.visitId+(returning?"/return-resume/":"/out-resume/")+restoreRun);j.lastFailure=resumed.message;}
                return;
            }
            if(j.phase=="NativeFloorReady")
            {
                if(j.cancelRequested){Record(j,"NativeReturnQueued");return;}
                if(!string.IsNullOrEmpty(j.workKind))
                {
                    var station=FindObjectsByType<FriendJobStation>(FindObjectsSortMode.None).Where(s=>s.sourceKind==j.workKind&&s.Available).OrderBy(s=>Vector3.Distance(s.stationTransform.position,actor.transform.position)).FirstOrDefault();if(!station)return;
                    j.stationId=station.slotId;j.destination=station.stationTransform.position;Record(j,"NativeWorkApproach");return;
                }
                var source=FriendsCasinoController.Instance.ActualAvailableSlots().OrderBy(s=>Vector3.Distance(s.transform.position,actor.transform.position)).FirstOrDefault();if(!source||!FriendsCasinoController.Instance.TryPrepareMachine(j.personId,source,out var machine))return;
                j.machineSourcePath=FriendsCasinoController.PathOf(source);j.machinePath=FriendsCasinoController.PathOf(machine);j.destination=machine.keypadSpawnPoint?machine.keypadSpawnPoint.position:machine.transform.position;Record(j,"NativeMachineApproach");return;
            }
            if(j.phase=="NativeMachineApproach"||j.phase=="NativeWorkApproach")
            {
                if(j.cancelRequested){moved.Remove(j.visitId);Record(j,"NativeReturnQueued");return;}
                var agent=actor.GetComponent<NavMeshAgent>();if(!agent||!agent.enabled||!agent.isOnNavMesh)return;
                if(!moved.Contains(j.visitId)){if(!actor.Brain.Movement.MoveTo(j.destination,actor.Brain.EffectiveMoveSpeed,.55f,true))return;j.destination=actor.Brain.Movement.Destination;moved.Add(j.visitId);}
                if(!actor.Brain.Movement.HasArrived()||Vector3.Distance(actor.transform.position,j.destination)>.9f)return;actor.Brain.Movement.Stop();moved.Remove(j.visitId);
                if(j.phase=="NativeWorkApproach")
                {runtime.State.leases.RemoveAll(l=>l.leaseId==j.leaseId);if(FriendsEconomyService.Instance.TryBeginPublicWorkAtStation(j.personId,j.stationId,out var job)){j.workContractId=job;Record(j,"WorkContractTravel","工作合同已受理，等待原站点实际到岗确认。");}else Lease(j);}
                else if(FriendsCasinoController.Instance.TryStartSlotsAtStation(j.personId,FindMachine(j.machinePath),j.quoteCoins,j.visitId,out var round)){j.roundId=round;Record(j,"ActiveRound");}return;
            }
            if(j.phase=="ActiveRound")
            {var round=FriendsEconomyService.Instance.State.casinoRounds.FirstOrDefault(r=>r.roundId==j.roundId);if(round!=null&&new[]{"Settled","RejectedBySource","CancelledBySource"}.Contains(round.phase))Record(j,"NativeReturnQueued");return;}
            if(j.phase=="WorkContractTravel"){AdvanceWorkContract(j);return;}
            if(j.phase=="OnDuty")
            {if(j.cancelRequested)FriendsEconomyService.Instance.RequestLeaveJob(j.personId);var job=FriendsEconomyService.Instance.State.jobs.FirstOrDefault(x=>x.contractId==j.workContractId);if(job==null){j.lastFailure="原岗位合同回执待确认，保留当前身份。";return;}if(job.state==FriendJobPhase.Done)Record(j,"NativeReturnQueued");return;}
            if(j.phase=="NativeReturnQueued")
            {var r=buttons.Request(0,j.personId,j.visitId+"/return");j.lastFailure=r.message;if(r.sourceStarted)Record(j,"NativeReturnTransfer");}
        }
        void CompleteNativeButton(NativeFloorRequest request)
        {
            var j=State.journeys.FirstOrDefault(x=>request.operationId==x.visitId+"/out"||request.operationId==x.visitId+"/entry"||request.operationId==x.visitId+"/return"||request.operationId==x.visitId+"/restore/"+restoreRun||request.operationId==x.visitId+"/out-resume/"+restoreRun||request.operationId==x.visitId+"/return-resume/"+restoreRun);
            if(j==null||!runtime||!NetworkSingleton<ElevatorManager>.TryGetInstance(out var elevator)||!elevator||!elevator.playerSpawnPosition)return;
            var person=runtime.GetPerson(j.personId);var actor=runtime.ActorOf(j.personId);if(person?.addedByPlayer!=true||!actor)return;
            actor.Brain.Pause();actor.Brain.Movement.Stop();
            var agent=actor.GetComponent<NavMeshAgent>();if(agent){if(!originalAgentTypes.ContainsKey(j.personId))originalAgentTypes[j.personId]=agent.agentTypeID;agent.enabled=false;}
            actor.transform.SetPositionAndRotation(elevator.playerSpawnPosition.position,elevator.playerSpawnPosition.rotation);actor.Brain.SetModelVisible(true);
            if(agent){int type=request.nativeFloor==0&&originalAgentTypes.TryGetValue(j.personId,out var original)?original:FindObjectsByType<SushiAndFish.Casino.Original.NPC>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(n=>n.Agent).Where(a=>a).Select(a=>a.agentTypeID).DefaultIfEmpty(0).First();agent.agentTypeID=type;agent.enabled=true;}
            j.savedFloor=request.nativeFloor;j.savedPosition=actor.transform.position;j.savedRotation=actor.transform.rotation;j.hasSavedPose=true;
            restoredActors.Add(j.visitId);
            if(request.operationId.Contains("/restore/"))
            {
                if(j.phase=="NativeMachineApproach"&&FindMachine(j.machineSourcePath) is Slots template&&FriendsCasinoController.Instance.TryPrepareMachine(j.personId,template,out var game))
                {j.machinePath=FriendsCasinoController.PathOf(game);j.destination=game.keypadSpawnPoint?game.keypadSpawnPoint.position:game.transform.position;moved.Remove(j.visitId);}
                runtime.NotifyChanged();return;
            }
            if(request.operationId.EndsWith("/entry",StringComparison.Ordinal)){Record(j,"NativeButtonQueued","原 Ground 按钮入口操作完成。");return;}
            if(request.operationId.EndsWith("/return",StringComparison.Ordinal)||request.operationId.Contains("/return-resume/"))
            {runtime.State.leases.RemoveAll(l=>l.leaseId==j.leaseId);Record(j,j.cancelRequested?"Cancelled":"Done","原 Ground 按钮操作已完成并到达原出生位。");if(!runtime.State.leases.Any(l=>l.personId==j.personId)){actor.Brain.Resume();actor.Brain.RequestScheduleChange();}}
            else Record(j,"NativeFloorReady","原按钮换层完成后到达原出生位。");runtime.SaveNow();
        }
        public bool CanYieldForManualActivity(string personId)=>!State.journeys.Any(j=>j.personId==personId&&j.phase!="NativeButtonQueued"&&j.phase!="Done"&&j.phase!="Cancelled")&&(FriendsCasinoController.Instance==null||FriendsCasinoController.Instance.IsAtSafeBoundary(personId));
        public bool YieldQueuedForManualActivity(string personId){if(!CanYieldForManualActivity(personId))return false;foreach(var j in State.journeys.Where(j=>j.personId==personId&&j.phase=="NativeButtonQueued").ToArray())Record(j,"Cancelled","新活动开始前取消未发出的按钮请求。");return true;}
        public static GameBase FindMachine(string path)=>FindObjectsByType<GameBase>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(s=>FriendsCasinoController.PathOf(s)==path);
        public string Capture()=>JsonUtility.ToJson(State);
        public void Restore(string json){runtime=FriendsLifeRuntime.Instance;if(!runtime||!runtime.IsAuthority)return;State=string.IsNullOrEmpty(json)?new FriendsCasinoTravelSave():JsonUtility.FromJson<FriendsCasinoTravelSave>(json)??new FriendsCasinoTravelSave();State.journeys=State.journeys??new List<FriendCasinoJourney>();restoredActors.Clear();}
    }
}
