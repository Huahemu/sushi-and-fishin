using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.Casino;
using SushiAndFish.Casino.Original;
using SushiAndFish.Casino.Original.Extensions;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendsEconomySave
    {public List<CasinoCore.CasinoJourney> originalCasinoJourneys=new List<CasinoCore.CasinoJourney>();
     public List<CasinoCore.CasinoUnknownCancelReceipt> originalCasinoUnknownCancellations=new List<CasinoCore.CasinoUnknownCancelReceipt>();}
}
namespace SushiAndFish.FriendsLife.CasinoCore
{
    [Serializable] public sealed class CasinoJourneyReceipt
    {public string phase,detail,transfer;public int floor,frame;public double minute;public Vector3 body;}
    [Serializable] public sealed class CasinoUnknownCancelReceipt
    {
        public string id,journey,person,world,phase,priorUnknownJson,priorUnknownFile,oldTransferId,bodyLeaseId,reason;
        public Vector3 restoredActualBody;public int actorInstance,frame,actualOriginalFloor;public bool playedRoundTrip;
    }
    [Serializable] public sealed partial class CasinoJourney
    {
        public string id,personId,operation,targetId,layout,type,phase="Queued",failure,transferId,roundId;
        public int targetFloor,actualFloor,worldAgentType;public long bet;
        public bool cancelRequested,returnRequested,transferMapped,transferCompleted,roundTripOnly;
        public Vector3 savedBody,gate,cab,machineGoal;public Quaternion savedRotation;
        public List<CasinoJourneyReceipt> receipts=new List<CasinoJourneyReceipt>();
    }
    [DefaultExecutionOrder(190)] public sealed partial class FriendsCasinoJourneyService:MonoBehaviour
    {
        public static FriendsCasinoJourneyService Instance{get;private set;}
        FriendsLifeRuntime runtime;FriendsEconomySave boundSave;
        readonly Dictionary<string,CasinoResidentPassenger> passengers=new Dictionary<string,CasinoResidentPassenger>();
        readonly Dictionary<string,Vector3> walking=new Dictionary<string,Vector3>();
        sealed class SurfaceWalk{public Vector3 goal,issued;public Vector3[] targets;public int index;public bool sent;public string proof;public Bounds region;}
        readonly Dictionary<string,SurfaceWalk> surfaceWalks=new Dictionary<string,SurfaceWalk>();
        public string WalkingDiagnostic(string id)=>surfaceWalks.TryGetValue(id,out var walk)?("leg="+(walk.index+1)+"/"+walk.targets.Length+" / sent="+walk.sent+" / target="+walk.issued.ToString("F4")+" / "+walk.proof):null;
        void ForgetWalk(string id){walking.Remove(id);surfaceWalks.Remove(id);routeRetryAt.Remove(id);}
        // A rejected route plan is re-evaluated at this interval, not every
        // frame: each plan traces the full physical surface of the route.
        readonly Dictionary<string,float> routeRetryAt=new Dictionary<string,float>();
        bool RetryRoute(CasinoJourney j){routeRetryAt[j.id]=Time.unscaledTime+.25f;return false;}
        public event Action<string> SelectionRequested;
        public string PendingSelectionPerson{get;private set;}
        public IReadOnlyList<CasinoJourney> Journeys=>FriendsEconomyService.Instance?.State.originalCasinoJourneys??new List<CasinoJourney>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){Instance=null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {if(!Instance){var go=new GameObject("OriginalCasinoFriendsJourney");DontDestroyOnLoad(go);go.AddComponent<FriendsCasinoJourneyService>();}}
        void Awake(){if(Instance&&Instance!=this){Destroy(this);return;}Instance=this;}
        void OnDestroy(){if(Instance==this)Instance=null;}
        public void RequestSelection(string personId){PendingSelectionPerson=personId;SelectionRequested?.Invoke(personId);runtime?.NotifyChanged();}
        public CasinoJourney ActiveFor(string person)=>Journeys.FirstOrDefault(j=>j.personId==person&&!Terminal(j));
        public static bool Terminal(CasinoJourney j)=>j.phase=="Done"||j.phase=="Cancelled"||j.phase==SettledRestoredGround;
        public bool TryStartRoundTrip(string person,string target,string operation,out string journeyId,out string reason)
            =>TryStart(person,target,operation,0,true,out journeyId,out reason);
        public bool TryStart(string person,string target,string operation,long bet,bool roundTripOnly,out string journeyId,out string reason)
        {
            journeyId=null;reason=null;runtime=FriendsLifeRuntime.Instance;
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!runtime.IsWorldRunning||!FriendsEconomyService.Instance||string.IsNullOrEmpty(operation))
            {reason="World authority required";return false;}
            var prior=Journeys.FirstOrDefault(j=>j.operation==operation&&j.personId==person);
            if(prior!=null){journeyId=prior.id;reason=prior.failure;return prior.phase!="Cancelled";}
            var binding=runtime.GetPerson(person);var actor=runtime.ActorOf(person);var casino=ParkCasinoIntegration.Instance;
            if(binding?.controllerKind!=PersonControllerKind.Npc||!binding.addedByPlayer||!actor||ActiveFor(person)!=null||!casino||!casino.Ready)
            {reason="An available bound friend and original casino required";return false;}
            if(!NativeCasinoTargetDirectory.TryGet(target,out var row)||row.floor<1||row.floor>4)
            {reason="Select an actual planned ordinary machine before departure";return false;}
            if(!NetworkSingleton<ElevatorManager>.TryGetInstance(out var originalElevator)||!originalElevator.NativeFloorButtonAvailable(row.floor))
            {reason="该楼层的原按钮尚未开放，请先选择已开放楼层。";return false;}
            if(!roundTripOnly&&row.type!=CasinoGameType.SlotMachine.ToString()){reason="This machine's original input adapter is not installed yet";return false;}
            long quoted=bet;
            if(!roundTripOnly)
            {
                if(row.minBet<=0||row.maxBet<row.minBet){reason="等待已选原机台的正式下注范围，尚未出发。";return false;}
                quoted=bet>0?bet:row.minBet;
                if(quoted<row.minBet||quoted>row.maxBet||FriendsEconomyService.Instance.AvailableCents(person)/100<quoted)
                {reason="个人余额不足或超出原机台下注范围，尚未出发。";return false;}
            }
            var agent=actor.GetComponent<NavMeshAgent>();if(!agent){reason="Actual actor agent missing";return false;}
            var j=new CasinoJourney{id="casino/original/"+Guid.NewGuid().ToString("N"),personId=person,operation=operation,
                targetId=row.id,layout=row.layout,type=row.type,targetFloor=row.floor,actualFloor=0,worldAgentType=agent.agentTypeID,
                bet=quoted,roundTripOnly=roundTripOnly,savedBody=actor.transform.position,savedRotation=actor.transform.rotation};
            FriendsEconomyService.Instance.State.originalCasinoJourneys.Add(j);journeyId=j.id;Record(j,"Queued","已选定 "+row.floor+" 层 "+row.type+"，等待实际入口。");return true;
        }
        public bool Cancel(string id)
        {var j=Journeys.FirstOrDefault(x=>x.id==id);if(j==null)return false;if(Terminal(j))return true;j.cancelRequested=j.returnRequested=true;runtime?.NotifyChanged();return true;}
        public bool Return(string id)
        {var j=Journeys.FirstOrDefault(x=>x.id==id);if(j==null||Terminal(j))return false;j.returnRequested=true;runtime?.NotifyChanged();return true;}
        public bool CanYield(string person)=>ActiveFor(person)==null;
        public bool Resume(string id,out string reason)
        {
            reason=null;var j=Journeys.FirstOrDefault(x=>x.id==id);
            if(j==null||Terminal(j)){reason="Journey is not active";return false;}
            if(j.phase=="AwaitInspection"){reason=j.failure??"A source transaction or round needs inspection; it cannot be replayed";return false;}
            if(j.phase=="LobbyApproach")
            {
                var actor=FriendsLifeRuntime.Instance?.ActorOf(j.personId);
                if(actor&&FriendCasinoGroundNavigation.Instance&&FriendCasinoGroundNavigation.Instance.Phase=="Rejected"&&
                    !FriendCasinoGroundNavigation.RequestVisitorRetry(actor,j.id,out reason))return false;
            }
            j.failure=null;return true;
        }
        void Record(CasinoJourney j,string phase,string detail=null)
        {
            j.phase=phase;j.failure=detail;var actor=runtime.ActorOf(j.personId);
            j.savedBody=actor?actor.transform.position:j.savedBody;j.savedRotation=actor?actor.transform.rotation:j.savedRotation;
            j.receipts.Add(new CasinoJourneyReceipt{phase=phase,detail=detail,floor=j.actualFloor,body=j.savedBody,transfer=j.transferId,minute=runtime.HostGameMinute,frame=Time.frameCount});
            runtime.NotifyChanged();runtime.SaveNow();
        }
        void Update()
        {
            runtime=FriendsLifeRuntime.Instance;var economy=FriendsEconomyService.Instance;var casino=ParkCasinoIntegration.Instance;
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!runtime.IsWorldRunning||!economy||!casino||!casino.Ready)return;
            economy.State.originalCasinoJourneys=economy.State.originalCasinoJourneys??new List<CasinoJourney>();
            if(boundSave!=economy.State)
            {
                bool restoring=boundSave!=null;boundSave=economy.State;walking.Clear();surfaceWalks.Clear();
                foreach(var j in Journeys.Where(x=>!Terminal(x)&&x.phase!="Queued"))
                {
                    // A freshly loaded unknown original transaction/round is
                    // never converted into a fresh input or spawn operation.
                    if(!passengers.ContainsKey(j.id)||restoring)
                    {
                        bool wasUnknown=j.phase=="AwaitInspection";j.phase="AwaitInspection";
                        if(!wasUnknown||string.IsNullOrEmpty(j.failure))j.failure="Saved original source continuity requires actual live body/session reconciliation; no replay or placement issued";
                    }
                }
            }
            if(!NetworkSingleton<ElevatorManager>.TryGetInstance(out var elevator)||!elevator)return;
            foreach(var j in Journeys.Where(x=>!Terminal(x)).ToArray())
                try{Advance(j,casino,elevator);}catch(Exception e){j.failure=e.GetBaseException().Message;j.phase="AwaitInspection";runtime.NotifyChanged();}
        }
        void Advance(CasinoJourney j,ParkCasinoIntegration casino,ElevatorManager elevator)
        {
            var actor=runtime.ActorOf(j.personId);if(!actor||actor.Brain==null){j.failure="Actual friend body unavailable";return;}
            if(j.phase=="AwaitInspection"){TryReleaseSettledRestoredGround(j,casino,actor);return;}
            if(j.phase=="Queued")
            {
                if(j.cancelRequested){Record(j,"Cancelled","尚未取得身体或发出按钮请求。");return;}
                if(casino.CurrentFloor!=0||elevator.isTeleporting){j.failure="等待原 Ground 楼层与电梯空闲。";return;}
                if(!NativeCasinoTargetDirectory.TryGet(j.targetId,out var selected)||selected.layout!=j.layout){Record(j,"Cancelled","原布局已改变，请重新选定机台。");return;}
                if(!FriendsBodyAuthority.TryAcquire(runtime,j.personId,j.id,"Casino",out var bodyLease,out var reason)){j.failure=reason;return;}
                j.bodyLeaseId=bodyLease.leaseId;j.departureBodyInstance=actor.GetInstanceID();j.departureHomeId=runtime.GetPerson(j.personId)?.currentHomeId;
                if(NeedsHomeDeparture(j,actor)){Record(j,"HomeDeparturePlanning");return;}
                AdmitPublicJourney(j,actor);return;
            }
            if(!FriendsBodyAuthority.IsOwned(runtime,j.personId,j.id)){j.failure="Persisted body owner changed";j.phase="AwaitInspection";return;}
            if(j.phase=="HomeDeparturePlanning"||j.phase=="HomeDepartureTransit"||j.phase=="HomeDepartureWalk"||j.phase=="HomeDepartureReady")
            {AdvanceHomeDeparture(j,actor,casino);return;}
            if(elevator.isTeleporting||NativeCasinoTransportTransaction.Current!=null)return;
            if(j.actualFloor!=casino.CurrentFloor){j.failure="Actual original floor differs from passenger's last complete source transaction";return;}
            if(j.phase=="LobbyApproach"||j.phase=="ReturnApproach"||j.phase=="MachineApproach")
            {
                if(j.phase=="MachineApproach"&&j.returnRequested){ForgetWalk(j.id);actor.Brain.Movement.Stop();Record(j,"ReturnApproach");return;}
                if(j.phase=="LobbyApproach"&&j.returnRequested)
                {
                    if(Continuous(actor)&&FriendsBodyAuthority.Release(runtime,j.personId,j.id,true))
                    {NativeCasinoTransportTransaction.Unregister(j.personId,j.id);passengers.Remove(j.id);Record(j,j.cancelRequested?"Cancelled":"Done");}return;
                }
                Vector3 goal;
                if(j.phase=="ReturnApproach"&&NativeCasinoTargetDirectory.TryGet(j.targetId,out var occupied)&&occupied.machine&&
                    !occupied.machine.NativeTryRelease(j.personId,j.id)&&occupied.machine.NativeHasActiveLease)
                {j.failure="Original source session has not reached its safe release boundary";return;}
                if(j.phase=="MachineApproach")
                {
                    if(!NativeCasinoTargetDirectory.TryGet(j.targetId,out var row)||!row.machine){j.failure="等待已选定原机台实例。";return;}
                    if(!row.machine.NativeTryReserve(j.personId,j.id)&&!row.machine.NativeReservationMatches(j.personId,j.id)){j.failure="原机台正在服务其他人。";return;}
                    if(!CasinoMachineApproach.TryResolve(actor,row.machine,out goal,out var reason)){j.failure=reason;return;}j.machineGoal=goal;
                }
                else
                {
                    if(j.actualFloor==0&&!FriendCasinoGroundNavigation.TryPrepareVisitor(actor,j.id,false,out var reason)){j.failure=reason;return;}
                    if(!CasinoCabGeometry.TryResolve(actor,elevator,out j.gate,out j.cab,out var geometryReason)){j.failure=geometryReason;return;}goal=j.gate;
                }
                if(!Walk(actor,j,goal,elevator.playerSpawnPosition))return;
                if(j.phase=="MachineApproach")
                {
                    if(!NativeCasinoTargetDirectory.TryGet(j.targetId,out var row)||!row.machine||!row.machine.NativeTryClaim(j.personId,j.id,j.targetId))return;
                    Record(j,"MachineReady","实际到达已选原机台并取得独占使用权。");
                }
                else Record(j,j.phase=="LobbyApproach"?"LobbyCabEntry":"ReturnCabEntry");
                return;
            }
            if(j.phase=="LobbyCabEntry"||j.phase=="ReturnCabEntry")
            {
                var existingMotor=actor.GetComponent<CasinoCabinWalker>();
                if(j.actualFloor==0&&(!existingMotor||existingMotor.Owner!=j.id)&&
                    !FriendCasinoGroundNavigation.TryPrepareVisitor(actor,j.id,false,out var preparedReason)){j.failure=preparedReason;return;}
                if(!CasinoCabinWalker.TryBegin(actor,j.id,elevator.playerSpawnPosition,j.gate,j.cab,false,j.actualFloor,out var motor,out var reason)){j.failure=reason;return;}
                if(!motor.Arrived){j.failure=motor.Failure;return;}
                Record(j,j.returnRequested?"ReturnButton":"OutboundButton");return;
            }
            if(j.phase=="OutboundButton"||j.phase=="ReturnButton")
            {
                if(!passengers.TryGetValue(j.id,out var passenger)){j.phase="AwaitInspection";j.failure="Original passenger binding absent";return;}
                int floor=j.phase=="ReturnButton"?0:j.targetFloor;
                if(j.returnRequested&&j.actualFloor==0){Record(j,"ReturnCabExit");return;}
                string transfer=j.id+"/transfer/"+j.receipts.Count;
                if(!NativeCasinoTransportTransaction.TryBeginNpcTransfer(elevator,floor,transfer,passenger,out var reason)){j.failure=reason;return;}
                Record(j,"OriginalTransfer","原服务已受理，等待实际 map 与开门回读。");return;
            }
            if(j.phase=="FloorCabExit"||j.phase=="ReturnCabExit")
            {
                if(!BindActualFloorProfile(actor,j,out var reason)){j.failure=reason;return;}
                if(j.actualFloor==0&&!FriendCasinoGroundNavigation.TryPrepareVisitor(actor,j.id,true,out reason)){j.failure=reason;return;}
                if(!CasinoCabGeometry.TryResolve(actor,elevator,out j.gate,out j.cab,out reason)){j.failure=reason;return;}
                if(!CasinoCabinWalker.TryBegin(actor,j.id,elevator.playerSpawnPosition,j.gate,j.cab,true,j.actualFloor,out var motor,out reason)){j.failure=reason;return;}
                if(!motor.Exited){j.failure=motor.Failure;return;}motor.Forget(j.id);
                if(j.actualFloor==0&&j.returnRequested)
                {
                    if(!Continuous(actor)||!FriendsBodyAuthority.Release(runtime,j.personId,j.id,true)){j.failure="实际返程身体/agent 尚未同步，保留 owner。";return;}
                    NativeCasinoTransportTransaction.Unregister(j.personId,j.id);passengers.Remove(j.id);Record(j,j.cancelRequested?"Cancelled":"Done","已真实走出 Ground 舱口并交还原日程。");
                }
                else Record(j,j.actualFloor==0?"LobbyApproach":j.returnRequested||j.actualFloor!=j.targetFloor?"ReturnApproach":"MachineApproach");
                return;
            }
            if(j.phase=="MachineReady")
            {
                if(j.roundTripOnly)j.returnRequested=true;
                if(j.returnRequested)
                {
                    if(NativeCasinoTargetDirectory.TryGet(j.targetId,out var row)&&row.machine&&!row.machine.NativeTryRelease(j.personId,j.id)){j.failure="原机台尚有未结结果，继续持有。";return;}
                    Record(j,"ReturnApproach");return;
                }
                AdvanceMachine(j,actor);
            }
            else if(j.phase=="OriginalRound")AdvanceMachine(j,actor);
        }
        void AdvanceMachine(CasinoJourney j,ResidentController actor)
        {
            if(!NativeCasinoTargetDirectory.TryGet(j.targetId,out var target)||!target.machine||!target.machine.NativeLeaseMatches(j.personId,j.id))
            {
                if(j.phase=="MachineReady"&&string.IsNullOrEmpty(j.roundId))
                {j.returnRequested=true;Record(j,"ReturnApproach","原玩家优先使用该机，本次尚未收注，朋友按原路返程。");}
                else Record(j,"AwaitInspection","已受理原机台身份或占用发生变化，保留本次活动，不重放。");return;
            }
            var machine=target.machine;
            if(j.phase=="MachineReady")
            {
                if(!string.IsNullOrEmpty(j.roundId)){Record(j,"AwaitInspection","原开始请求已登记，禁止重复发送。");return;}
                long bet=j.bet>0?j.bet:machine.MinBet;
                if(bet<machine.MinBet||bet>machine.MaxBet||FriendsEconomyService.Instance.AvailableCents(j.personId)/100<bet)
                {j.returnRequested=true;j.failure="个人余额不足或原机台下注范围不符，未收注，准备返程。";return;}
                if(!NativeCasinoResidentProfile.TryGet(j.personId,out var input,out var reason)){j.failure=reason;return;}
                j.bet=bet;j.roundId=j.id+"/round/0";Record(j,"SourceStartPending","原开始请求已登记，等待同次原收注回读。");
                if(machine.NativeTryStartOriginal(j.personId,j.id,j.roundId,bet,input,out reason))Record(j,"OriginalRound","原机台已收注，使用原转轴、RNG 与结果流程。");
                else{j.returnRequested=true;Record(j,"MachineReady",reason??"原机台在收注前拒绝，本次不重发。");}
                return;
            }
            if(j.phase=="OriginalRound")
            {
                if(!string.IsNullOrEmpty(machine.NativeFailure)){Record(j,"AwaitInspection",machine.NativeFailure);return;}
                if(!machine.NativeLeaseMatches(j.personId,j.id,j.roundId)){Record(j,"AwaitInspection","原回合 owner 不匹配。");return;}
                if(!machine.NativeRoundResultReady){j.failure="等待原机台真实结果、个人入账与原 Reset 安全边界。";return;}
                var result=FriendsEconomyService.Instance.State.originalCasinoResults.LastOrDefault(r=>r.actor==j.personId&&r.round==j.roundId);
                if(result==null){Record(j,"AwaitInspection","原机已停止，但当前回合个人结果收据缺失。");return;}
                if(!machine.NativeTryRelease(j.personId,j.id)){j.failure="原机台尚不能安全交还。";return;}
                j.returnRequested=true;Record(j,"ReturnApproach","本轮原结果：下注 "+result.bet+"，返回 "+result.payout+"；个人结算已登记。");
            }
        }
        public bool CanCancelUnchargedUnknownAtRestoredGround(string id,out string reason)
            =>UnknownGroundReadback(id,out _,out _,out reason);
        bool UnknownGroundReadback(string id,out CasinoJourney j,out ResidentController actor,out string reason)
        {
            runtime=FriendsLifeRuntime.Instance;j=null;actor=null;reason=null;
            var economy=FriendsEconomyService.Instance;var casino=ParkCasinoIntegration.Instance;
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!runtime.IsWorldRunning||!economy||!casino||!casino.Ready)
            {reason="Original world authority/readback not ready";return false;}
            j=Journeys.FirstOrDefault(x=>x.id==id);
            if(j==null||j.phase!="AwaitInspection"||!string.IsNullOrEmpty(j.roundId)||string.IsNullOrEmpty(j.transferId)||j.transferMapped||j.transferCompleted||j.actualFloor!=0)
            {reason="Only preserved unmapped prewager transfer Unknown can be cancelled by this route";return false;}
            if(NativeCasinoTransportTransaction.Current!=null||casino.CurrentFloor!=0||!FriendCasinoGroundNavigation.OriginalGroundAvailable()||
                !NetworkSingleton<ElevatorManager>.TryGetInstance(out var elevator)||elevator.NativeCurrentFloor!=0)
            {reason="Actual original Ground source must be ready with no transfer";return false;}
            string recoveryPerson=j.personId;actor=runtime.ActorOf(recoveryPerson);var leases=runtime.State.leases.Where(l=>l.personId==recoveryPerson).ToArray();
            if(!actor||!FriendResidentBindings.IsOwned(actor)||actor.Brain==null||actor.Runtime==null||leases.Length!=1||leases[0].ownerEpisode!=id||
                !FriendsBodyAuthority.IsOwned(runtime,j.personId,id)||economy.HasActiveJob(j.personId))
            {reason="Same unique prewager body owner/identity required";return false;}
            bool ownSnapshotPause=actor.Brain.StateMachine!=null&&actor.Brain.StateMachine.IsSuspended&&actor.Runtime.IsSnapshotApplying&&actor.Runtime.SystemState==ResidentSystemState.SnapshotApplying;
            if(FriendResidentBindings.IsApplyingAppearance(actor)||actor.IsCustomizationApplying||actor.Runtime.IsSnapshotApplying&&!ownSnapshotPause||actor.Runtime.IsInteracting||
                actor.Runtime.IsWorking||actor.Runtime.IsInRestaurant||actor.Runtime.HasRestaurantAssignment||actor.Runtime.IsSwimming)
            {reason="Restored body/source appearance or activity is not at a safe readback boundary";return false;}
            var agent=actor.GetComponent<NavMeshAgent>();
            if(!agent||!agent.enabled||!agent.isOnNavMesh||agent.agentTypeID!=j.worldAgentType||agent.pathPending||
                Vector3.Distance(actor.transform.position,agent.nextPosition)>.15f||agent.velocity.sqrMagnitude>.0025f)
            {reason="Actual original type/agent/body must be stationary and continuous";return false;}
            var station=FindObjectsByType<FriendJobStation>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(x=>x.sourceKind=="CasinoElevator"&&x.stationTransform);
            if(!station){reason="Original source anchor missing";return false;}
            var shape=new FriendGroundRoutePhysics(actor,station.stationTransform);
            if(!FriendCasinoGroundNavigation.TryReadPhysicalRegion(shape,out var sourceRegion,out reason))return false;
            var body=actor.transform.position;
            if(body.x>=sourceRegion.min.x&&body.x<=sourceRegion.max.x&&body.z>=sourceRegion.min.z&&body.z<=sourceRegion.max.z||elevator.IsInElevator(body))
            {reason="Body remains inside source region/cab; real original physical exit is still required";return false;}
            if(!shape.Foot(body,body.y+.12f,out var floor)||Mathf.Abs(floor.point.y-body.y)>.12f||!shape.Clear(body)||
                !NavMesh.SamplePosition(body+Vector3.up*.04f,out var nav,.15f,shape.Filter)||Vector3.Distance(nav.position,body)>.05f)
            {reason="Actual restored body has not passed original grounded/typed/capsule readback";return false;}
            var journeyId=j.id;
            if(FindObjectsByType<GameBase>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(g=>g&&g.NativeOwner==journeyId))
            {reason="Original machine still owns this Unknown; cancellation refused";return false;}
            var motor=actor.GetComponent<CasinoCabinWalker>();
            if(motor&&!string.IsNullOrEmpty(motor.Owner)&&!motor.Exited)
            {reason="Owned physical cab traversal has not exited";return false;}
            return true;
        }
        public bool TryCancelUnchargedUnknownAtRestoredGround(string id,string recoveryOperation,out string reason)
        {
            reason=null;var economy=FriendsEconomyService.Instance;
            if(string.IsNullOrEmpty(recoveryOperation)||recoveryOperation.Length>96||recoveryOperation.Any(c=>!char.IsLetterOrDigit(c)&&c!='-'&&c!='_'))
            {reason="Stable safe recovery operation key required";return false;}
            if(!economy){reason="Economy/readback not ready";return false;}
            var prior=economy.State.originalCasinoUnknownCancellations?.FirstOrDefault(x=>x.id==recoveryOperation);
            if(prior!=null){reason=prior.reason;return prior.journey==id&&prior.phase=="CancelledAfterGroundReadback";}
            if(!UnknownGroundReadback(id,out var j,out var actor,out reason))return false;
            string original=JsonUtility.ToJson(j);string directory=Path.Combine(Application.persistentDataPath,"CasinoUnknownRecovery",runtime.State.worldId);
            string file=Path.Combine(directory,recoveryOperation+".unknown.json");
            try
            {
                Directory.CreateDirectory(directory);
                using(var stream=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.Read))
                using(var writer=new StreamWriter(stream,new System.Text.UTF8Encoding(false)))writer.Write(original);
            }
            catch(Exception error){reason="Original Unknown was not preserved as a new immutable record / "+error.Message;return false;}
            var receipt=new CasinoUnknownCancelReceipt{id=recoveryOperation,journey=id,person=j.personId,world=runtime.State.worldId,phase="Prepared",
                priorUnknownJson=original,priorUnknownFile=file,oldTransferId=j.transferId,bodyLeaseId=j.bodyLeaseId,
                restoredActualBody=actor.transform.position,actorInstance=actor.GetInstanceID(),frame=Time.frameCount,actualOriginalFloor=0,playedRoundTrip=false};
            economy.State.originalCasinoUnknownCancellations=economy.State.originalCasinoUnknownCancellations??new List<CasinoUnknownCancelReceipt>();
            economy.State.originalCasinoUnknownCancellations.Add(receipt);
            if(!FriendsBodyAuthority.Release(runtime,j.personId,id,true))
            {receipt.phase="RejectedBodyRelease";receipt.reason=reason="Safe original body release refused; original Unknown retained";runtime.NotifyChanged();runtime.SaveNow();return false;}
            NativeCasinoTransportTransaction.Unregister(j.personId,id);passengers.Remove(id);ForgetWalk(id);
            j.cancelRequested=j.returnRequested=true;
            receipt.phase="CancelledAfterGroundReadback";receipt.reason="Unwagered Unknown cancelled after restored Ground body readback; original transfer failure remains in immutable history; no played/return success";
            Record(j,"Cancelled",receipt.reason);return true;
        }
        bool BindActualFloorProfile(ResidentController actor,CasinoJourney j,out string reason)
        {
            reason=null;var agent=actor.GetComponent<NavMeshAgent>();if(!agent){reason="Actual body agent absent";return false;}
            var walkingCab=actor.GetComponent<CasinoCabinWalker>();
            if(walkingCab&&walkingCab.Owner==j.id&&walkingCab.ExpectedFloor==j.actualFloor)return true;
            int type=j.worldAgentType;
            if(j.actualFloor!=0)
            {
                if(!NetworkSingleton<NavMeshManager>.TryGetInstance(out var source)){reason="Original floor nav source missing";return false;}
                var surface=source.GetComponent<NavMeshSurface>();
                if(!surface||!surface.navMeshData){reason="Original floor nav data not built";return false;}type=surface.agentTypeID;
            }
            if(agent.enabled){reason="Actual cab passenger agent must remain disabled until physical gate exit";return false;}
            agent.agentTypeID=type;return true;
        }
        bool Walk(ResidentController actor,CasinoJourney j,Vector3 goal,Transform source)
        {
            if(!Continuous(actor)){j.failure="Actual floor agent is not continuously bound";return false;}
            var shape=new FriendGroundRoutePhysics(actor,source);
            var nav=FriendCasinoGroundNavigation.Instance;
            bool seam=j.actualFloor==0&&nav&&nav.Phase=="Ready";
            Vector3 linkA=seam?nav.SeamStart:default,linkB=seam?nav.SeamEnd:default;
            bool cached=surfaceWalks.TryGetValue(j.id,out var walk)&&Vector3.Distance(walk.goal,goal)<=.1f;
            var region=cached?walk.region:default;
            if(!cached)
            {
                if(routeRetryAt.TryGetValue(j.id,out var retryAt)&&Time.unscaledTime<retryAt)return false;
                if(!FriendCasinoGroundNavigation.TryReadPhysicalRegion(shape,out region,out var boundsReason)){j.failure=boundsReason;return false;}
                Vector3[] targets;string reason;
                if(seam)
                {
                    if(!FriendCasinoGroundNavigation.TryPlanExplicitApproach(actor,source,nav.SeamStart,nav.SeamEnd,goal,false,out _,out targets,out region,out reason))
                    {j.failure="Actual explicit source walking route rejected / "+reason;return false;}
                }
                else
                {
                    var path=new NavMeshPath();
                    if(!NavMesh.CalculatePath(actor.transform.position,goal,shape.Filter,path)||path.status!=NavMeshPathStatus.PathComplete)
                    {j.failure="Actual original source walking route not ready / "+path.status;return RetryRoute(j);}
                    if(!FriendGroundSurfacePath.TryPlan(shape,path,region,linkA,linkB,seam,out targets,out reason))
                    {
                        string reverseReason=null;
                        if(j.actualFloor==0||j.phase!="ReturnApproach"||
                            !CasinoReturnRoute.TryReverseOutbound(shape,actor.transform.position,goal,region,out targets,out reverseReason))
                        {j.failure="Actual original source walking route rejected / "+reason+" / reverse="+reverseReason;return RetryRoute(j);}
                        reason=reverseReason;
                    }
                }
                if(targets.Length==0||Vector3.Distance(targets[targets.Length-1],goal)>.1f){j.failure="Actual source route has another final endpoint";return false;}
                walk=new SurfaceWalk{goal=goal,targets=targets,proof=reason??"explicit original public/source route",region=region};surfaceWalks[j.id]=walk;
            }
            if(!walk.sent)
            {
                var target=walk.targets[walk.index];var actualPath=new NavMeshPath();string legReason=null;
                if(!NavMesh.CalculatePath(actor.transform.position,target,shape.Filter,actualPath)||actualPath.status!=NavMeshPathStatus.PathComplete||
                    !FriendGroundSurfacePath.Validate(shape,actualPath,region,linkA,linkB,seam,out legReason))
                {j.failure="Next actual original walking leg rejected / "+legReason;ForgetWalk(j.id);return false;}
                var privacy=FriendHousingIsland.Instance?.GetComponent<FriendHousingPrivacy>();
                if(privacy&&privacy.ExcludesPathFor(actor,actualPath)){j.failure="Original walking leg enters a private home exclusion";ForgetWalk(j.id);return false;}
                SetCasinoLease(j,"CasinoWalking",target,false);
                if(!runtime.TryMoveWorldActor(j.personId,target,out var resolved,.05f)){j.failure=runtime.LastWorldMoveDiagnostic;ForgetWalk(j.id);return false;}
                if(Vector3.Distance(resolved,target)>.1f){actor.Brain.Movement.Stop();j.failure="Original movement resolved another leg endpoint";ForgetWalk(j.id);return false;}
                walk.issued=resolved;walk.sent=true;walking[j.id]=resolved;return false;
            }
            if(Vector3.Distance(actor.Brain.Movement.Destination,walk.issued)>.1f)
            {actor.Brain.Movement.Stop();j.failure="Original movement destination changed under this same journey";ForgetWalk(j.id);return false;}
            if(!actor.Brain.Movement.HasArrived()||Vector3.Distance(actor.transform.position,walk.issued)>.1f)return false;
            actor.Brain.Movement.Stop();walk.index++;walk.sent=false;
            if(walk.index<walk.targets.Length)return false;
            if(Vector3.Distance(actor.transform.position,goal)>.1f||!Continuous(actor)){j.failure="Actual final walking body/agent readback differs";return false;}
            ForgetWalk(j.id);j.failure=null;return true;
        }
        static bool Continuous(ResidentController actor)
        {var a=actor?actor.GetComponent<NavMeshAgent>():null;return actor&&a&&a.enabled&&a.isOnNavMesh&&Vector3.Distance(a.nextPosition,actor.transform.position)<.15f;}
    }
}
