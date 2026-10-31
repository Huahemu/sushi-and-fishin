using System;
using System.Collections.Generic;
using System.Linq;
using SushiAndFish.ResidentialIsland;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    [Serializable] public sealed class SharedHomeReturnTemplate
    {public string personId;public FriendHousingPlacement placement;}
    public sealed partial class FriendsEconomyService
    {
        const int HousingVacancySchema=1;
        bool housingTransition;
        sealed class HomeBindingBackup
        {public string personId,owned,current,returnHome;public FriendResidentBindingState projectedHome;public Vector3 homePosition;public Quaternion homeRotation;public bool hasHome,arrivalGrantUsed;public int homeRevision,hostResidentId;}
        static T HousingCopy<T>(T value)=>JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        bool IsRetiredHome(FriendHousingPlacement row)=>row!=null&&HousingVacancyRules.IsRetired(State.vacatedHomes,row.plot,row.instanceId);
        bool VisitMatchesGeneration(HomeVisitPermit permit)
        {
            var home=Plot(permit.homeId)?.house;if(!home)return false;
            if(string.IsNullOrEmpty(permit.homeInstanceId))return !State.vacatedHomes.Any(v=>v.plotId==permit.homeId);
            return permit.homeInstanceId==FriendHousingIsland.HomeIdentity(home);
        }

        void NormalizeHousingVacancySave()
        {
            State.vacatedHomes=State.vacatedHomes??new List<HomeVacatedGeneration>();
            foreach(var retired in State.vacatedHomes)retired.instanceId=HousingVacancyRules.Generation(retired.plotId,retired.instanceId);
            foreach(var group in State.groups){group.ownerHomes=group.ownerHomes??new List<string>();group.members=group.members??new List<string>();group.returnHomes=group.returnHomes??new List<SharedHomeReturnTemplate>();}
            State.placements.RemoveAll(IsRetiredHome);
            foreach(var row in State.placements.Where(r=>r.friendsOwnedClone))row.instanceId=HousingVacancyRules.Generation(row.plot,row.instanceId);
        }
        bool RemoveRetiredHousingProjections()
        {
            bool complete=true;var housing=FriendHousingIsland.Instance;
            foreach(var plot in housing.Plots.Where(p=>p.house).ToArray())
            {
                var metadata=plot.house.GetComponent<FriendHouseMetadata>();
                // Never delete an unmarked original or a different live NEW
                // generation, including a new friend's house on the old plot.
                if(metadata?.friendsOwnedClone!=true||!HousingVacancyRules.IsRetired(State.vacatedHomes,plot.id,metadata.instanceId))continue;
                if(!housing.TryRemoveRetiredProjection(plot,out var error)){complete=false;LastFailure=error;}
            }
            return complete;
        }
        bool AdvanceHousingVacancyMigrations()
        {
            if(housingTransition||housingProjectionPending)return false;
            foreach(var group in State.groups.Where(g=>g.vacancySchema<HousingVacancySchema).ToArray())
            {
                // These are already committed player/source family choices.
                // Normalize their realization without inventing a new choice.
                if(!State.receipts.Any(r=>r.domain=="home-share"&&"homegroup/"+r.opId==group.groupId&&r.counterparty==group.homeId))
                {LastFailure="这份同住计划已失效";return false;}
                if(!TryCommitSharedHome(group.members.ToArray(),group.homeId,"home-vacancy-migration/"+group.groupId,group))return false;
            }
            return true;
        }
        List<HomeBindingBackup> HousingBindings()=>runtime.People.Select(p=>
        {
            var projected=runtime.ActorOf(p.personId)?.GetComponent<FriendResidentBindingState>();
            return new HomeBindingBackup{personId=p.personId,owned=p.ownedHomeId,current=p.currentHomeId,returnHome=p.returnHomeId,arrivalGrantUsed=p.arrivalGrantUsed,hostResidentId=p.hostResidentId,projectedHome=projected,
                homePosition=projected?projected.homePosition:default,homeRotation=projected?projected.homeRotation:Quaternion.identity,hasHome=projected&&projected.hasHome,homeRevision=projected?projected.homeRevision:0};
        }).ToList();
        void RestoreHousingBindings(IEnumerable<HomeBindingBackup> before)
        {foreach(var saved in before){var person=runtime.GetPerson(saved.personId);if(person!=null){person.ownedHomeId=saved.owned;person.currentHomeId=saved.current;person.returnHomeId=saved.returnHome;person.arrivalGrantUsed=saved.arrivalGrantUsed;person.hostResidentId=saved.hostResidentId;}if(saved.projectedHome){saved.projectedHome.homePosition=saved.homePosition;saved.projectedHome.homeRotation=saved.homeRotation;saved.projectedHome.hasHome=saved.hasHome;saved.projectedHome.homeRevision=saved.homeRevision;}}}
        bool CanonicalMatchesLive(HousingPlot plot)
        {
            var row=State.placements.FirstOrDefault(r=>r.plot==plot.id&&!IsRetiredHome(r));
            return row==null||HousingVacancyRules.Generation(row.plot,row.instanceId)==FriendHousingIsland.HomeIdentity(plot.house);
        }
        HomeGroup GroupOf(string personId)=>State.groups.FirstOrDefault(g=>g.members.Contains(personId));
        public HomeGroup SharedHomeOf(string personId)=>GroupOf(personId);
        public bool CanPlaceArrivalHome(string personId,string plotId,HousingType type)
        {
            var person=runtime?.GetPerson(personId);var housing=FriendHousingIsland.Instance;
            if(!Authority||housingTransition||housingProjectionPending||person==null||!housing||type?.level!=1||
               person.arrivalGrantUsed||!string.IsNullOrEmpty(person.ownedHomeId)||!string.IsNullOrEmpty(person.currentHomeId)||
               !housing.IsEmptyForNewHousing(plotId)||State.placements.Any(r=>r.plot==plotId&&!IsRetiredHome(r)))
            {LastFailure="请选择新到来角色可用的空置地块";return false;}
            return true;
        }
        public bool CanRequestHomeSplit(string personId,string humanPersonId)
        {
            var person=runtime?.GetPerson(personId);var group=GroupOf(personId);
            return person!=null&&group!=null&&group.vacancySchema>=HousingVacancySchema&&(Authority?FamilyHumanIsBound(humanPersonId):runtime.LocalHuman()?.personId==humanPersonId)&&
                (person.controllerKind==PersonControllerKind.Npc||personId==humanPersonId)&&SplitHomeMembers(personId).Length>0;
        }
        public SharedHomeReturnTemplate[] SplitHomeMembers(string personId)
        {
            var group=GroupOf(personId);if(group==null||group.vacancySchema<HousingVacancySchema)return Array.Empty<SharedHomeReturnTemplate>();
            string owner=OwnerOf(group.homeId);return group.returnHomes.Where(t=>t!=null&&t.personId!=owner&&runtime.GetPerson(t.personId)!=null).OrderBy(t=>t.personId,StringComparer.Ordinal).ToArray();
        }
        public HousingType SplitHomeType(string personId,string ownerId)
        {
            var group=GroupOf(personId);var housing=FriendHousingIsland.Instance;if(group==null||!housing)return null;
            var template=group.returnHomes.FirstOrDefault(t=>t.personId==ownerId);if(template==null)return null;
            return housing.Catalog.homes.FirstOrDefault(t=>t.id==template.placement?.type&&t.level==group.level&&t.prefab)??
                housing.Catalog.homes.Where(t=>t.level==group.level&&t.prefab).OrderBy(t=>t.id,StringComparer.Ordinal).FirstOrDefault();
        }
        bool TryCommitSharedHome(string[] personIds,string selectedHomeId,string op,HomeGroup migration)
        {
            LastFailure=null;
            if(!Authority||housingTransition||string.IsNullOrEmpty(op)||personIds==null||personIds.Length<2||personIds.Length>8)return false;
            if(HasReceipt(op))
            {
                if(!State.receipts.Any(r=>r.opId==op&&r.domain==(migration==null?"home-share":"home-vacate-migration")&&r.counterparty==selectedHomeId))return false;
                var priorCommitted=State.groups.FirstOrDefault(g=>g.groupId==(migration?.groupId??"homegroup/"+op)&&g.homeId==selectedHomeId&&g.vacancySchema>=HousingVacancySchema);
                return priorCommitted!=null&&personIds.All(id=>priorCommitted.members.Contains(id)&&runtime.GetPerson(id)?.currentHomeId==selectedHomeId);
            }
            var housing=FriendHousingIsland.Instance;var interiors=FriendsHomeInteriors.Instance;
            if(!housing||!housing.CanBuild||!interiors||housingProjectionPending){LastFailure="住宅加载中，请稍候";return false;}
            var ids=personIds.Distinct(StringComparer.Ordinal).ToList();if(ids.Count!=personIds.Length)return false;
            var parents=ids.Select(runtime.GetPerson).Where(p=>p!=null).Select(p=>p.nativeResidentId).ToArray();
            foreach(var child in runtime.Core.State.family.children.Where(c=>c.stage!="Adult"&&(parents.Contains(c.parentAId)||parents.Contains(c.parentBId))))
            {var person=runtime.People.FirstOrDefault(p=>p.nativeResidentId==child.residentId);if(person!=null&&!ids.Contains(person.personId))ids.Add(person.personId);}
            if(ids.Count>8||ids.Any(id=>runtime.GetPerson(id)==null))return false;
            var priorGroups=State.groups.Where(g=>g.members.Any(ids.Contains)).ToArray();
            if(priorGroups.Any(g=>g.members.Any(id=>!ids.Contains(id)))){LastFailure="请先分住，或选全部同住成员";return false;}
            var people=ids.Select(runtime.GetPerson).ToArray();
            var homes=people.SelectMany(p=>new[]{p.ownedHomeId,p.currentHomeId}).Where(id=>!string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Select(Plot).ToArray();
            if(homes.Length==0||homes.Any(p=>p?.house==null||p.house.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone!=true||!CanonicalMatchesLive(p)))
            {LastFailure="成员的新增住宅尚未准备完成";return false;}
            var host=homes.FirstOrDefault(p=>p.id==selectedHomeId);if(host==null){LastFailure="请选择成员拥有的住宅";return false;}
            int level=host.house.definition.level;
            if(homes.Any(p=>p.house.definition.level!=level)||runtime.People.Any(p=>!ids.Contains(p.personId)&&homes.Any(h=>h.id==p.currentHomeId||h.id==p.ownedHomeId)))
            {LastFailure="只能转换同等级且属于所选成员的住宅";return false;}
            var templates=new Dictionary<string,SharedHomeReturnTemplate>(StringComparer.Ordinal);
            foreach(var old in priorGroups)foreach(var template in old.returnHomes??new List<SharedHomeReturnTemplate>())
                if(template?.placement!=null&&ids.Contains(template.personId))templates[template.personId]=HousingCopy(template);
            foreach(var person in people.Where(p=>!string.IsNullOrEmpty(p.ownedHomeId)))
            {
                var plot=Plot(person.ownedHomeId);if(plot?.house==null)return false;
                templates[person.personId]=new SharedHomeReturnTemplate{personId=person.personId,placement=FriendHousingIsland.CapturePlacement(plot.house)};
            }
            foreach(var person in people.Where(p=>!templates.ContainsKey(p.personId)))
                if(!runtime.Core.State.family.children.Any(c=>c.residentId==person.nativeResidentId&&(parents.Contains(c.parentAId)||parents.Contains(c.parentBId))))
                {LastFailure="成员缺少可用于同级转换的住宅资料";return false;}
            string hostOwner=people.FirstOrDefault(p=>p.ownedHomeId==host.id)?.personId;
            if(string.IsNullOrEmpty(hostOwner)||!templates.ContainsKey(hostOwner)){LastFailure="所选住宅缺少实际屋主";return false;}
            foreach(var home in homes)if(!interiors.CanPrepareHomeChange(home.id,out var busy)){LastFailure=busy;return false;}
            var group=new HomeGroup{groupId=migration?.groupId??"homegroup/"+op,homeId=host.id,level=level,members=ids,ownerHomes=new List<string>{host.id},returnHomes=templates.Values.OrderBy(t=>t.personId,StringComparer.Ordinal).ToList(),vacancySchema=HousingVacancySchema,revision=(migration?.revision??0)+1};
            var nativeHost=runtime.Core.State.households.FirstOrDefault(h=>h.id==host.id);
            if(nativeHost==null||nativeHost.residentIds.Count(id=>!people.Any(p=>p.nativeResidentId==id))+ids.Count>nativeHost.capacity)
            {LastFailure="所选住宅容量不足";return false;}
            // Capture the selected house BEFORE preparing new internal doors.
            var selectedRow=FriendHousingIsland.CapturePlacement(host.house);var retiring=homes.Where(p=>p!=host).ToArray();
            var retiredRows=retiring.Select(p=>FriendHousingIsland.CapturePlacement(p.house)).ToArray();
            if(!interiors.PrepareGroup(op,group)){LastFailure="室内资源尚未准备完成";return false;}
            var changed=retiring.Select(p=>p.house).Concat(interiors.TransactionRooms(priorGroups.Select(g=>g.groupId))).ToArray();
            if(!housing.TryPrepareTransitionExits(changed,Array.Empty<HousingHouse>(),out var exits,out var error))
            {interiors.CancelPreparedGroup(op);LastFailure=error;return false;}
            string economyBefore=JsonUtility.ToJson(State),coreBefore=runtime.Core.ExportJson(false);var bindings=HousingBindings();
            var detached=new List<FriendHousingIsland.DetachedHome>();bool wasProjecting=projecting,wasPending=housingProjectionPending,wasCanonical=canonicalHousingLoaded;bool committed=false;housingTransition=true;projecting=true;
            try
            {
                foreach(var old in priorGroups)State.groups.Remove(old);State.groups.Add(group);
                foreach(var person in people){person.currentHomeId=host.id;person.ownedHomeId=person.personId==hostOwner?host.id:"";person.returnHomeId=person.ownedHomeId;}
                foreach(var row in retiredRows)
                {
                    string generation=HousingVacancyRules.Generation(row.plot,row.instanceId);
                    if(!State.vacatedHomes.Any(r=>r.plotId==row.plot&&r.instanceId==generation))State.vacatedHomes.Add(new HomeVacatedGeneration{plotId=row.plot,instanceId=generation,operationId=op});
                    State.placements.RemoveAll(r=>r.plot==row.plot&&HousingVacancyRules.Generation(r.plot,r.instanceId)==generation);
                    State.homeVisits.RemoveAll(v=>v.homeId==row.plot);State.queuedUpgrades.RemoveAll(q=>q.homeId==row.plot);
                }
                State.placements.RemoveAll(r=>r.plot==host.id);State.placements.Add(selectedRow);
                if(!runtime.SyncHomeBindings(false))throw new InvalidOperationException(runtime.LastError??"同住绑定尚未完成");
                if(!exits.Apply(out error))throw new InvalidOperationException(error);
                foreach(var plot in retiring)detached.Add(housing.DetachOwnedHome(plot));
                if(!interiors.ActivatePreparedChange(op,priorGroups.Select(g=>g.groupId),out error))throw new InvalidOperationException(error);
                Receipt(op,migration==null?"home-share":"home-vacate-migration",personIds[0],0,host.id);
                State.version=Math.Max(State.version,2);canonicalHousingLoaded=true;housingProjectionPending=false;
                runtime.State.economyJson=JsonUtility.ToJson(State);Commit();committed=true;
                interiors.FinalizePreparedChange(op);foreach(var old in detached)housing.FinalizeDetachedHome(old);
                exits.Commit();housing.NotifyChanged();LastFailure=null;return true;
            }
            catch(Exception exception)
            {
                if(committed)
                {
                    try{interiors.FinalizePreparedChange(op);foreach(var old in detached)housing.FinalizeDetachedHome(old);}catch(Exception cleanup){Debug.LogWarning(cleanup);}
                    LastFailure="已完成";housingProjectionPending=true;Debug.LogWarning(exception);return true;
                }
                interiors.RollbackPreparedChange(op);for(int n=detached.Count-1;n>=0;n--)housing.RestoreDetachedHome(detached[n]);exits.Rollback();
                State=JsonUtility.FromJson<FriendsEconomySave>(economyBefore);RestoreHousingBindings(bindings);runtime.State.economyJson=economyBefore;
                if(!runtime.Core.TryImportJson(coreBefore,out var restoreError))throw new InvalidOperationException("住宅事务回滚失败: "+restoreError,exception);
                housingProjectionPending=wasPending;canonicalHousingLoaded=wasCanonical;Debug.LogWarning("[FriendsLife] Housing transaction failed: "+exception+(string.IsNullOrEmpty(exits.LastRollbackFailure)?"":"\nRollback: "+exits.LastRollbackFailure));LastFailure="住宅操作失败，已恢复原样。请重试。";return false;
            }
            finally{projecting=wasProjecting;housingTransition=false;}
        }
        public bool SeparateHomeGroup(string personId,string groupId,long expectedRevision,IReadOnlyList<HomeSplitChoice> choices,string op)
        {
            LastFailure=null;if(!Authority||housingTransition||string.IsNullOrEmpty(op))return false;
            if(HasReceipt(op))return State.receipts.Any(r=>r.opId==op&&r.domain=="home-split"&&r.actorId==personId&&r.counterparty==groupId);
            var group=GroupOf(personId);var housing=FriendHousingIsland.Instance;var interiors=FriendsHomeInteriors.Instance;
            if(group==null||group.groupId!=groupId||group.revision!=expectedRevision||group.vacancySchema<HousingVacancySchema||!housing||!housing.CanBuild||!interiors||housingProjectionPending)
            {LastFailure="合住资料已变化，请重新选择分住方案";return false;}
            var common=Plot(group.homeId);string owner=OwnerOf(group.homeId);var required=SplitHomeMembers(personId);
            if(common?.house==null||common.house.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone!=true||!CanonicalMatchesLive(common)||string.IsNullOrEmpty(owner)||common.house.definition.level!=group.level)
            {LastFailure="合住住宅尚未准备完成";return false;}
            if(!HousingVacancyRules.ValidateSplit(required.Select(t=>t.personId),choices,group.level,housing.IsEmptyForNewHousing,
                id=>housing.Catalog.homes.FirstOrDefault(t=>t.id==id&&t.prefab)?.level??-1,out var error))
            {LastFailure=error;return false;}
            if(!interiors.CanPrepareHomeChange(group.homeId,out error)){LastFailure=error;return false;}
            var commonRow=FriendHousingIsland.CapturePlacement(common.house);
            var owners=new Dictionary<string,string>(StringComparer.Ordinal){{owner,group.homeId}};
            foreach(var choice in choices)owners[choice.personId]=choice.plotId;
            IEnumerable<string> ParentsOf(string memberId)
            {
                var person=runtime.GetPerson(memberId);var child=person==null?null:runtime.Core.State.family.children.FirstOrDefault(c=>c.residentId==person.nativeResidentId);
                if(child==null)return Array.Empty<string>();
                return new[]{runtime.PersonFromNative(child.parentAId)?.personId,runtime.PersonFromNative(child.parentBId)?.personId};
            }
            if(!HousingVacancyRules.PlanDependents(group.members,owners,ParentsOf,out var destinations,out error)){LastFailure=error;return false;}
            var prepared=new List<(HomeSplitChoice choice,HousingPlot plot,HousingHouse house)>();
            var partitions=new List<(string token,HomeGroup group)>();
            void CancelResources()
            {foreach(var part in partitions)interiors.CancelPreparedGroup(part.token);foreach(var item in prepared)housing.DisposeOwnedProjection(item.house);}
            foreach(var choice in choices)
            {
                var template=required.First(t=>t.personId==choice.personId);var plot=Plot(choice.plotId);var type=housing.Catalog.homes.First(t=>t.id==choice.typeId);
                var house=housing.PrepareEmptyHome(plot,type,choice.yaw,template.placement,out error);
                if(!house){CancelResources();LastFailure=error;return false;}
                FriendHouseMetadata.For(house).legacyPlayerOwned=runtime.GetPerson(choice.personId)?.controllerKind==PersonControllerKind.Human;prepared.Add((choice,plot,house));
            }
            // A dependent child follows a real parent, retaining a private room
            // in that parent's new household. No extra child house/grant is made.
            foreach(var family in destinations.GroupBy(p=>p.Value).Where(g=>g.Count()>1))
            {
                string familyOwner=owners.First(p=>p.Value==family.Key).Key;
                var house=family.Key==common.id?common.house:prepared.First(p=>p.plot.id==family.Key).house;
                var childGroup=new HomeGroup{groupId="homegroup/"+op+"/family/"+family.Key,homeId=family.Key,level=group.level,vacancySchema=HousingVacancySchema,revision=1,
                    members=family.Select(p=>p.Key).OrderBy(id=>id,StringComparer.Ordinal).ToList(),ownerHomes=new List<string>{family.Key},
                    returnHomes=new List<SharedHomeReturnTemplate>{new SharedHomeReturnTemplate{personId=familyOwner,placement=family.Key==common.id?HousingCopy(commonRow):FriendHousingIsland.CapturePlacement(house)}}};
                string token=op+"/interior/"+family.Key;partitions.Add((token,childGroup));
                if(!interiors.PrepareGroup(token,childGroup,house)){CancelResources();LastFailure="分住室内资源尚未准备完成";return false;}
            }
            if(!housing.TryPrepareTransitionExits(interiors.TransactionRooms(new[]{group.groupId}),prepared.Select(p=>p.house),out var exits,out error))
            {CancelResources();LastFailure=error;return false;}
            string economyBefore=JsonUtility.ToJson(State),coreBefore=runtime.Core.ExportJson(false);var bindings=HousingBindings();
            bool wasProjecting=projecting,wasPending=housingProjectionPending,wasCanonical=canonicalHousingLoaded,committed=false;housingTransition=true;projecting=true;
            try
            {
                if(!HousingVacancyRules.ValidateSplit(required.Select(t=>t.personId),choices,group.level,housing.IsEmptyForNewHousing,
                    id=>housing.Catalog.homes.FirstOrDefault(t=>t.id==id&&t.prefab)?.level??-1,out error))throw new InvalidOperationException(error);
                foreach(var memberId in group.members)
                {
                    var person=runtime.GetPerson(memberId);if(person==null)throw new InvalidOperationException("分住成员已变化");
                    person.currentHomeId=destinations[memberId];person.ownedHomeId=person.returnHomeId=owners.TryGetValue(memberId,out var own)?own:"";
                    var projected=runtime.ActorOf(memberId)?.GetComponent<FriendResidentBindingState>();if(projected&&person.currentHomeId!=group.homeId)projected.hasHome=false;
                }
                State.groups.Remove(group);State.groups.AddRange(partitions.Select(p=>p.group));
                State.placements.RemoveAll(r=>r.plot==common.id);State.placements.Add(commonRow);
                foreach(var item in prepared)
                {State.placements.RemoveAll(r=>r.plot==item.plot.id&&IsRetiredHome(r));if(State.placements.Any(r=>r.plot==item.plot.id))throw new InvalidOperationException("所选地块已有另一代住宅记录");State.placements.Add(FriendHousingIsland.CapturePlacement(item.house));}
                if(!runtime.SyncHomeBindings(false))throw new InvalidOperationException(runtime.LastError??"分住绑定尚未完成");
                if(!exits.Apply(out error))throw new InvalidOperationException(error);
                foreach(var item in prepared){if(item.plot.Occupied)throw new InvalidOperationException("所选地块已被使用");item.plot.house=item.house;housing.ActivatePrivateInterior(item.house);}
                if(!interiors.ActivatePreparedChange(op,new[]{group.groupId},out error))throw new InvalidOperationException(error);
                foreach(var part in partitions)if(!interiors.ActivatePreparedChange(part.token,Array.Empty<string>(),out error))throw new InvalidOperationException(error);
                Receipt(op,"home-split",personId,0,groupId);State.version=Math.Max(State.version,2);housingProjectionPending=false;canonicalHousingLoaded=true;
                runtime.State.economyJson=JsonUtility.ToJson(State);Commit();committed=true;
                interiors.FinalizePreparedChange(op);foreach(var part in partitions)interiors.FinalizePreparedChange(part.token);
                exits.Commit();housing.NotifyChanged();LastFailure=null;return true;
            }
            catch(Exception exception)
            {
                if(committed)
                {
                    try{interiors.FinalizePreparedChange(op);foreach(var part in partitions)interiors.FinalizePreparedChange(part.token);}catch(Exception cleanup){Debug.LogWarning(cleanup);}
                    LastFailure="已完成";housingProjectionPending=true;Debug.LogWarning(exception);return true;
                }
                for(int n=partitions.Count-1;n>=0;n--)interiors.RollbackPreparedChange(partitions[n].token);interiors.RollbackPreparedChange(op);
                foreach(var item in prepared){if(item.plot.house==item.house)item.plot.house=null;housing.DisposeOwnedProjection(item.house);}exits.Rollback();
                State=JsonUtility.FromJson<FriendsEconomySave>(economyBefore);RestoreHousingBindings(bindings);runtime.State.economyJson=economyBefore;
                if(!runtime.Core.TryImportJson(coreBefore,out var restoreError))throw new InvalidOperationException("住宅事务回滚失败: "+restoreError,exception);
                housingProjectionPending=wasPending;canonicalHousingLoaded=wasCanonical;Debug.LogWarning("[FriendsLife] Housing transaction failed: "+exception+(string.IsNullOrEmpty(exits.LastRollbackFailure)?"":"\nRollback: "+exits.LastRollbackFailure));LastFailure="住宅操作失败，已恢复原样。请重试。";return false;
            }
            finally{projecting=wasProjecting;housingTransition=false;}
        }
    }
}
