using System;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.Casino;
using SushiAndFish.Casino.Original;
using SushiAndFish.Casino.Original.Extensions;
using IncrementalAddons.Casino;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendsCasinoElevatorBridge
    {
        bool ActualOriginalGroundReady(ParkCasinoIntegration casino)
        {
            if(!NetworkSingleton<ElevatorManager>.TryGetInstance(out var original)||!original)return false;
            return GroundWorkPolicy.OriginalGroundReady(casino&&casino.Ready,casino?casino.CurrentFloor:-1,
                NativeCasinoButtonAccess.ReadNativeFloor(original),original.isTeleporting,original.IsLocked);
        }
        bool IsGroundWork(FriendCasinoJourney journey)=>journey.workKind=="CasinoElevator"&&journey.targetFloor==0;
        bool CanObserveGroundWorkWithoutTransfer(FriendCasinoJourney journey,ParkCasinoIntegration casino)
            =>IsGroundWork(journey)&&ActualOriginalGroundReady(casino)&&new[]{"NativeFloorReady","NativeWorkApproach","SameFloorWorkApproach","WorkContractTravel","OnDuty","NativeReturnQueued"}.Contains(journey.phase);
        FriendJobStation GroundStation(FriendCasinoJourney journey,ResidentController actor,out Vector3 destination)
        {
            destination=default;FriendJobStation best=null;float distance=float.PositiveInfinity;
            foreach(var station in FindObjectsByType<FriendJobStation>(FindObjectsSortMode.None).Where(s=>s&&s.sourceKind==journey.workKind&&s.Available&&s.stationTransform).OrderBy(s=>s.slotId,StringComparer.Ordinal))
            {
                if(!station.TryResolveRoute(actor,out var candidate))continue;float candidateDistance=Vector3.Distance(candidate,actor.transform.position);
                if(candidateDistance>=distance)continue;best=station;destination=candidate;distance=candidateDistance;
            }
            return best;
        }
        bool TryAdvanceSameFloorWork(FriendCasinoJourney journey,ParkCasinoIntegration casino,ResidentController actor)
        {
            if(!IsGroundWork(journey))return false;
            bool ready=ActualOriginalGroundReady(casino);
            var cabinJob=FriendsEconomyService.Instance.State.jobs.FirstOrDefault(j=>j.contractId==journey.workContractId&&j.cabinReturnRequired&&j.state==FriendJobPhase.Handover);
            if(!ready&&cabinJob!=null)
            {
                // Keep the physical owner while using the existing original
                // button protocol to return to Ground. This request is not a
                // friend-body transfer: its normal source ACK simply restores
                // floor readiness before the motor can walk the body out.
                var returned=buttons.Request(0,journey.personId,journey.visitId+"/ground-cabin-return");journey.lastFailure=returned.message;return true;
            }
            if(journey.phase=="NativeButtonQueued"&&journey.cancelRequested&&string.IsNullOrEmpty(journey.workContractId))
            {FinishGroundWork(journey,actor,true,ready);return true;}
            if(actor.Brain==null){journey.lastFailure="等待角色原Brain接口准备完成。";return true;}
            if(journey.phase=="SameFloorWorkApproach"&&!ready)
            {
                if(moved.Remove(journey.visitId))actor.Brain?.Movement.Stop();
                Record(journey,"NativeButtonQueued","原楼层ready事实已变化；后续换层继续使用原按钮协议。");return true;
            }
            if(!ready)return false;
            if(journey.phase=="NativeButtonQueued"||journey.phase=="NativeFloorReady"||journey.phase=="NativeWorkApproach")
            {
                if(journey.cancelRequested&&string.IsNullOrEmpty(journey.workContractId)){FinishGroundWork(journey,actor,true);return true;}
                bool ownsLease=runtime.State.leases.Any(l=>l.leaseId==journey.leaseId&&l.personId==journey.personId);
                if(actor.Runtime!=null&&actor.Runtime.IsSnapshotApplying&&!ownsLease){journey.lastFailure="等待角色原快照/动作接口释放。";return true;}
                var station=GroundStation(journey,actor,out var destination);
                if(!station)
                {journey.lastFailure="原0层已ready；等待可达的原电梯岗位与实际导航路线。";return true;}
                Lease(journey);actor.Brain.Pause();journey.stationId=station.slotId;journey.destination=destination;moved.Remove(journey.visitId);
                Record(journey,"SameFloorWorkApproach","实读integration/native floor=0且原电梯空闲；从角色实际位置走原导航到岗位，未发同层换层按钮。");return true;
            }
            if(journey.phase=="SameFloorWorkApproach")
            {
                if(journey.cancelRequested){if(moved.Remove(journey.visitId))actor.Brain?.Movement.Stop();FinishGroundWork(journey,actor,true);return true;}
                var station=FindObjectsByType<FriendJobStation>(FindObjectsSortMode.None).FirstOrDefault(s=>s&&s.slotId==journey.stationId&&s.sourceKind==journey.workKind);
                var agent=actor.GetComponent<NavMeshAgent>();
                if(!station||!station.Available||!agent||!agent.enabled||!agent.isOnNavMesh)
                {if(moved.Remove(journey.visitId))actor.Brain?.Movement.Stop();journey.lastFailure="等待原岗位及角色实际导航接口可用。";return true;}
                if(!moved.Contains(journey.visitId))
                {
                    if(!station.TryResolveRoute(actor,out var destination)||!actor.Brain.Movement.MoveTo(destination,actor.Brain.EffectiveMoveSpeed,.55f,true))
                    {journey.lastFailure="原公开MoveTo未接受完整岗位路径。";return true;}
                    journey.destination=actor.Brain.Movement.Destination;moved.Add(journey.visitId);
                }
                if(!GroundWorkPolicy.PhysicalArrival(actor.Brain.Movement.HasArrived(),Vector3.Distance(actor.transform.position,journey.destination),Vector3.Distance(actor.Brain.Movement.Destination,journey.destination)))return true;
                actor.Brain.Movement.Stop();moved.Remove(journey.visitId);runtime.State.leases.RemoveAll(l=>l.leaseId==journey.leaseId);
                if(FriendsEconomyService.Instance.TryBeginPublicWorkAtStation(journey.personId,journey.stationId,out var contract))
                {journey.workContractId=contract;Record(journey,"WorkContractTravel","角色实际抵达岗位接近点，原站点工作合同已受理；等待站点自身确认OnDuty。");}
                else{Lease(journey);journey.lastFailure="角色已抵达接近点，等待原站点接管。";}
                return true;
            }
            if(journey.phase=="WorkContractTravel"){AdvanceWorkContract(journey);return true;}
            if(journey.phase=="OnDuty")
            {
                if(journey.cancelRequested)FriendsEconomyService.Instance.RequestLeaveJob(journey.personId);
                var job=FriendsEconomyService.Instance.State.jobs.FirstOrDefault(j=>j.contractId==journey.workContractId);
                if(job==null){journey.lastFailure="等待已登记工作合同的真实回执，保留当前岗位身份。";return true;}
                if(job.state!=FriendJobPhase.Done)return true;
                Record(journey,"NativeReturnQueued","原站点工作已结束并执行交接；等待释放本次新增岗位租约。");return true;
            }
            if(journey.phase=="NativeReturnQueued")
            {
                var contract=FriendsEconomyService.Instance.State.jobs.FirstOrDefault(j=>j.contractId==journey.workContractId);
                if(contract==null&&!string.IsNullOrEmpty(journey.workContractId)){journey.lastFailure="原工作合同记录待确认，保留离岗身份。";return true;}
                if(contract==null&&!journey.cancelRequested){journey.lastFailure="岗位结束证据尚未形成。";return true;}
                if(contract!=null&&contract.state!=FriendJobPhase.Done)
                {if(journey.cancelRequested)FriendsEconomyService.Instance.RequestLeaveJob(journey.personId);journey.lastFailure="等待原岗位实际完成交接。";return true;}
                FinishGroundWork(journey,actor,journey.cancelRequested);return true;
            }
            return false;
        }
        void AdvanceWorkContract(FriendCasinoJourney journey)
        {
            var economy=FriendsEconomyService.Instance;
            if(journey.cancelRequested)economy.RequestLeaveJob(journey.personId);
            var job=economy.State.jobs.FirstOrDefault(j=>j.contractId==journey.workContractId);
            if(job==null){journey.lastFailure="等待已登记工作合同的真实回执，保留当前岗位身份。";return;}
            if(job.state==FriendJobPhase.Done){Record(journey,"NativeReturnQueued","工作合同已结束；到岗事实与回位继续按实际记录判定。");return;}
            var station=FindObjectsByType<FriendJobStation>(FindObjectsSortMode.None).FirstOrDefault(s=>s&&s.slotId==journey.stationId);
            if(station&&GroundWorkPolicy.OriginalStationDuty(job.state.ToString(),station.OnDuty,station.OwnerPersonId,journey.personId))
                Record(journey,"OnDuty","原站点已通过实际到达判定并接管可见岗位；分钟工资仍由原OnDuty计时链取得。");
        }
        void FinishGroundWork(FriendCasinoJourney journey,ResidentController actor,bool cancelled,bool groundReady=true)
        {
            // Same original floor is a real current-world fact. Releasing our
            // own lease is not a fake source transfer/door-completion event.
            moved.Remove(journey.visitId);runtime.State.leases.RemoveAll(l=>l.leaseId==journey.leaseId);
            Record(journey,cancelled?"Cancelled":"Done",groundReady?"原0层仍ready，原岗位交接/取消后释放新增租约；未调用同层按钮、未传送角色。":"当前排队岗位已取消，释放新增租约；保留原楼层状态与按钮协议。");
            if(!FriendsEconomyService.Instance.HasActiveJob(journey.personId)&&!runtime.State.leases.Any(l=>l.personId==journey.personId))
            {actor.Brain?.Resume();actor.Brain?.RequestScheduleChange();}
            runtime.SaveNow();
        }
    }
}
