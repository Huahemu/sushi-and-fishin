using System;
using System.Linq;
using SushiAndFish.ResidentialIsland;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendsEconomyService
    {
        bool QueuePreparedHomeUpgrade(string homeId,string op,string reason)
        {
            LastFailure=reason;var home=Plot(homeId)?.house;if(!home)return false;
            if(!State.queuedUpgrades.Any(q=>q.opId==op))
            {State.queuedUpgrades.Add(new QueuedHomeUpgrade{homeId=homeId,opId=op,quoteCents=UpgradeCents,homesSignature=homeId,homeInstanceId=FriendHousingIsland.HomeIdentity(home)});Commit();}
            return true;
        }
        bool TryCommitHomeUpgrade(string homeId,bool playerPays,string op)
        {
            if(!Authority||housingTransition||string.IsNullOrEmpty(op))return false;if(HasReceipt(op))return State.receipts.Any(r=>r.opId==op&&r.domain=="home-upgrade-plan"&&r.counterparty==homeId);
            string error=null;if(housingProjectionPending||!CanUpgrade(homeId,playerPays,out error)){LastFailure=error??"住宅尚未准备完成";return false;}
            if(State.queuedUpgrades.Any(q=>q.homeId==homeId&&q.opId!=op)){LastFailure="住宅已有升级安排";return false;}
            var housing=FriendHousingIsland.Instance;var interiors=FriendsHomeInteriors.Instance;var plot=Plot(homeId);
            var target=housing?.Catalog.homes.FirstOrDefault(t=>t.level==2&&t.prefab);var group=State.groups.FirstOrDefault(g=>g.homeId==homeId);
            if(!housing||!housing.CanBuild||!interiors||plot?.house==null||target==null||!CanonicalMatchesLive(plot)||group!=null&&group.vacancySchema<HousingVacancySchema)
            {LastFailure="住宅升级资源尚未准备完成";return false;}
            if(!interiors.CanPrepareHomeChange(homeId,out error)){LastFailure=error;return playerPays?QueuePreparedHomeUpgrade(homeId,op,error):false;}
            var replacement=housing.PrepareReplacement(plot,target,out error);if(!replacement){LastFailure=error;return false;}
            var nextGroup=group==null?null:HousingCopy(group);if(nextGroup!=null){nextGroup.level=target.level;nextGroup.revision++;}
            if(nextGroup!=null&&!interiors.PrepareGroup(op,nextGroup,replacement)){housing.DisposeOwnedProjection(replacement);LastFailure="室内资源尚未准备完成";return false;}
            var changing=new[]{plot.house}.Concat(interiors.TransactionRooms(group==null?Array.Empty<string>():new[]{group.groupId}));
            if(!housing.TryPrepareTransitionExits(changing,new[]{replacement},out var exits,out error))
            {interiors.CancelPreparedGroup(op);housing.DisposeOwnedProjection(replacement);LastFailure=error;return false;}
            string economyBefore=JsonUtility.ToJson(State),coreBefore=runtime.Core.ExportJson(false);var bindings=HousingBindings();
            var row=FriendHousingIsland.CapturePlacement(replacement);string owner=OwnerOf(homeId);var payer=playerPays?"player":owner;
            FriendHousingIsland.DetachedHome detached=null;bool wasProjecting=projecting,wasPending=housingProjectionPending,wasCanonical=canonicalHousingLoaded,committed=false;housingTransition=true;projecting=true;
            try
            {
                if(!CanUpgrade(homeId,playerPays,out error))throw new InvalidOperationException(error);
                State.placements.RemoveAll(r=>r.plot==homeId);State.placements.Add(row);
                if(group!=null)
                {
                    group.level=nextGroup.level;group.revision=nextGroup.revision;
                    var template=group.returnHomes.FirstOrDefault(t=>t.personId==owner);if(template!=null)template.placement=HousingCopy(row);
                }
                if(!exits.Apply(out error))throw new InvalidOperationException(error);
                detached=housing.DetachOwnedHome(plot);plot.house=replacement;housing.ActivatePrivateInterior(replacement);
                if(!interiors.ActivatePreparedChange(op,group==null?Array.Empty<string>():new[]{group.groupId},out error))throw new InvalidOperationException(error);
                Receipt(op+"/home/"+homeId,"home-upgrade",payer,-UpgradeCents,homeId);Receipt(op,"home-upgrade-plan",payer,0,homeId);State.queuedUpgrades.RemoveAll(q=>q.opId==op);
                if(!playerPays){var wallet=Wallet(owner);wallet.balanceCents-=UpgradeCents;wallet.revision++;}
                State.version=Math.Max(State.version,2);canonicalHousingLoaded=true;housingProjectionPending=false;runtime.State.economyJson=JsonUtility.ToJson(State);
                // All fallible resources, exits and world activation complete
                // before the original shared wallet's one exact debit.
                if(playerPays)
                {
                    var originalWallet=PlayerWallet;if(!originalWallet)throw new InvalidOperationException("钱包暂不可用");
                    decimal before=originalWallet.ExactGoldBalance;bool infinite=originalWallet.InfiniteGold;bool accepted;
                    try{accepted=originalWallet.ServerTryChangeStoredGold(-UpgradeCents);}
                    catch
                    {
                        // A source notification can throw after the exact bank
                        // value changed. Keep the already-realized matching
                        // receipt; never report a veto and retry a second debit.
                        if(!infinite&&originalWallet.ExactGoldBalance==before-UpgradeCents)committed=true;
                        throw;
                    }
                    if(!accepted)throw new InvalidOperationException("金币不足或钱包暂不可用");
                }
                committed=true;Commit();interiors.FinalizePreparedChange(op);housing.FinalizeDetachedHome(detached);exits.Commit();housing.NotifyChanged();LastFailure=null;return true;
            }
            catch(Exception exception)
            {
                if(committed)
                {
                    try{interiors.FinalizePreparedChange(op);housing.FinalizeDetachedHome(detached);}catch(Exception cleanup){Debug.LogWarning(cleanup);}
                    try{runtime.NotifyChanged();runtime.SaveNow();}catch(Exception notification){Debug.LogWarning(notification);}
                    LastFailure="已升级";housingProjectionPending=true;Debug.LogWarning(exception);return true;
                }
                interiors.RollbackPreparedChange(op);if(plot.house==replacement)plot.house=null;housing.DisposeOwnedProjection(replacement);if(detached!=null)housing.RestoreDetachedHome(detached);exits.Rollback();
                State=JsonUtility.FromJson<FriendsEconomySave>(economyBefore);RestoreHousingBindings(bindings);runtime.State.economyJson=economyBefore;
                if(!runtime.Core.TryImportJson(coreBefore,out var restoreError))throw new InvalidOperationException("住宅升级回滚失败: "+restoreError,exception);
                housingProjectionPending=wasPending;canonicalHousingLoaded=wasCanonical;Debug.LogWarning("[FriendsLife] Housing upgrade failed: "+exception+(string.IsNullOrEmpty(exits.LastRollbackFailure)?"":"\nRollback: "+exits.LastRollbackFailure));LastFailure="住宅操作失败，已恢复原样。请重试。";return false;
            }
            finally{projecting=wasProjecting;housingTransition=false;}
        }
    }
}
