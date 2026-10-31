using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.Casino;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife.CasinoCore
{
    public sealed partial class FriendsCasinoJourneyService
    {
        public const string SettledRestoredGround="SettledRestoredGround";
        // Recovery contract case 4 (C009/casino-return/RECOVERY_CONTRACT.md):
        // a charged, settled round whose body the cold load restored to the
        // original Ground can no longer play a physical return. Only after the
        // whole settlement lineage and the restored body are validated is the
        // existing body owner released to the original schedule, under its own
        // terminal label. Nothing is replayed, debited, paid or moved.
        void TryReleaseSettledRestoredGround(CasinoJourney j,ParkCasinoIntegration casino,ResidentController actor)
        {
            if(routeRetryAt.TryGetValue(j.id+"/settled",out var retryAt)&&Time.unscaledTime<retryAt)return;
            routeRetryAt[j.id+"/settled"]=Time.unscaledTime+.5f;
            var economy=FriendsEconomyService.Instance;if(!economy||string.IsNullOrEmpty(j.roundId)||j.receipts==null)return;
            // Settlement: exactly one personal result for this round, booked
            // before the return leg began.
            var results=economy.State.originalCasinoResults?.Where(r=>r.actor==j.personId&&r.round==j.roundId).ToArray();
            if(results==null||results.Length!=1||results[0].bet!=j.bet||!j.receipts.Any(r=>r.phase=="ReturnApproach"))return;
            // No outstanding source machine use or original transfer.
            if(NativeCasinoTransportTransaction.Current!=null)return;
            if(!NativeCasinoTargetDirectory.TryGet(j.targetId,out var row)||!row.machine||row.machine.NativeHasActiveLease)return;
            // The same unique persisted body owner.
            var leases=runtime.State.leases.Where(l=>l.personId==j.personId).ToArray();
            if(leases.Length!=1||leases[0].ownerEpisode!=j.id||!FriendsBodyAuthority.IsOwned(runtime,j.personId,j.id))return;
            // The restored body stands still on the original Ground, outside the
            // casino floors and the lobby cab, on its original agent type with
            // supported, clear feet.
            if(casino.CurrentFloor!=0||actor.IsCustomizationApplying||!Continuous(actor))return;
            var agent=actor.GetComponent<NavMeshAgent>();var body=actor.transform.position;
            if(agent.agentTypeID!=j.worldAgentType||agent.velocity.sqrMagnitude>.0025f||agent.hasPath&&agent.remainingDistance>.1f)return;
            if(Vector3.Distance(body,j.gate)<50||Vector3.Distance(body,j.cab)<50)return;
            foreach(var cabReceipt in j.receipts.Where(r=>r.phase=="LobbyCabEntry"||r.phase=="OutboundButton"))
                if(Vector3.Distance(body,cabReceipt.body)<3)return;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            if(!NavMesh.SamplePosition(body,out var nav,.25f,filter)||Vector3.Distance(nav.position,body)>.15f)return;
            if(!Physics.Raycast(body+Vector3.up*.5f,Vector3.down,out var ground,1f,~0,QueryTriggerInteraction.Ignore)||Mathf.Abs(ground.point.y-body.y)>.2f)return;
            if(!FriendsBodyAuthority.Release(runtime,j.personId,j.id,true))return;
            NativeCasinoTransportTransaction.Unregister(j.personId,j.id);passengers.Remove(j.id);ForgetWalk(j.id);
            Record(j,SettledRestoredGround,"本局已结算（下注 "+results[0].bet+"，返还 "+results[0].payout+"）；读档后身体位于原地面，未重放，已交还原日程。");
        }
    }
}
