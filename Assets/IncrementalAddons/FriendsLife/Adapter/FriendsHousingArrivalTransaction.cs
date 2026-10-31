using System;
using System.Linq;
using SushiAndFish.ResidentialIsland;
using UnityEngine;
using Unity.Netcode;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendsEconomyService
    {
        public bool TryPlaceArrivalHome(string personId,HousingPlot plot,HousingType type,float yaw)
        {
            LastFailure=null;if(plot==null||!CanPlaceArrivalHome(personId,plot.id,type))return false;
            var housing=FriendHousingIsland.Instance;var person=runtime.GetPerson(personId);
            var prepared=housing.PrepareEmptyHome(plot,type,yaw,null,out var error);if(!prepared){LastFailure=error;return false;}
            FriendHouseMetadata.For(prepared).legacyPlayerOwned=person.controllerKind==PersonControllerKind.Human;
            if(!housing.TryPrepareTransitionExits(Array.Empty<HousingHouse>(),new[]{prepared},out var exits,out error))
            {housing.DisposeOwnedProjection(prepared);LastFailure=error;return false;}
            string economyBefore=JsonUtility.ToJson(State),coreBefore=runtime.Core.ExportJson(false);var bindings=HousingBindings();var actorBefore=runtime.ActorOf(personId);
            bool wasProjecting=projecting,wasPending=housingProjectionPending,wasCanonical=canonicalHousingLoaded,committed=false;housingTransition=true;projecting=true;
            try
            {
                if(!housing.IsEmptyForNewHousing(plot.id)||State.placements.Any(r=>r.plot==plot.id&&!IsRetiredHome(r)))throw new InvalidOperationException("地块或入住资料已变化");
                State.placements.RemoveAll(r=>r.plot==plot.id&&IsRetiredHome(r));State.placements.Add(FriendHousingIsland.CapturePlacement(prepared));
                if(!exits.Apply(out error))throw new InvalidOperationException(error);
                plot.house=prepared;housing.ActivatePrivateInterior(prepared);Physics.SyncTransforms();
                if(!CommitArrival(personId,plot.id))throw new InvalidOperationException("原入住登记尚未完成");
                committed=true;canonicalHousingLoaded=true;housingProjectionPending=false;exits.Commit();housing.NotifyChanged();LastFailure=null;return true;
            }
            catch(Exception exception)
            {
                if(committed){LastFailure="已入住";housingProjectionPending=true;Debug.LogWarning(exception);return true;}
                if(plot.house==prepared)plot.house=null;housing.DisposeOwnedProjection(prepared);exits.Rollback();
                var actorNow=runtime.ActorOf(personId);
                if(!actorBefore&&actorNow&&FriendResidentBindings.IsOwned(actorNow))
                {var net=actorNow.GetComponent<NetworkObject>();if(net&&net.IsSpawned&&NetworkManager.Singleton&&NetworkManager.Singleton.IsServer)net.Despawn(true);else Destroy(actorNow.gameObject);}
                State=JsonUtility.FromJson<FriendsEconomySave>(economyBefore);RestoreHousingBindings(bindings);runtime.State.economyJson=economyBefore;
                if(!runtime.Core.TryImportJson(coreBefore,out var restoreError))throw new InvalidOperationException("入住事务回滚失败: "+restoreError,exception);
                housingProjectionPending=wasPending;canonicalHousingLoaded=wasCanonical;Debug.LogWarning("[FriendsLife] Housing arrival failed: "+exception+(string.IsNullOrEmpty(exits.LastRollbackFailure)?"":"\nRollback: "+exits.LastRollbackFailure));LastFailure="住宅操作失败，已恢复原样。请重试。";return false;
            }
            finally{projecting=wasProjecting;housingTransition=false;}
        }
    }
}
