using System;
using System.Linq;
using SushiAndFish.ResidentialIsland;
using UnityEngine;
using Unity.Netcode;
namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendsEconomyService
    {
        bool FamilyHumanIsBound(string personId)
        {
            if(!Authority||runtime.GetPerson(personId)?.controllerKind!=PersonControllerKind.Human)return false;
            var network=NetworkManager.Singleton;
            if(network&&network.IsListening&&network.IsServer)
                return network.ConnectedClientsIds.Any(client=>runtime.HumanForClient(client)?.personId==personId);
            return runtime.LocalHuman()?.personId==personId;
        }
        public HousingPlot[] FamilyHomeOptions(string humanPersonId,string suggestionId)
        {
            var human=runtime?.GetPerson(humanPersonId);var plan=runtime?.Core?.State.family?.housingSuggestions.FirstOrDefault(p=>p.id==suggestionId);
            if(human?.controllerKind!=PersonControllerKind.Human||plan==null||plan.residentIds==null||!plan.residentIds.Contains(human.nativeResidentId)||
               (plan.status!="Pending"&&plan.status!="Ready"))return Array.Empty<HousingPlot>();
            return FamilyHousingOptions(suggestionId);
        }
        HousingPlot[] FamilyHousingOptions(string suggestionId)
        {
            var plan=runtime?.Core?.State.family?.housingSuggestions.FirstOrDefault(p=>p.id==suggestionId);
            if(plan?.residentIds==null||(plan.status!="Pending"&&plan.status!="Ready"))return Array.Empty<HousingPlot>();
            var members=plan.residentIds.Select(runtime.PersonFromNative).ToArray();if(members.Any(p=>p==null))return Array.Empty<HousingPlot>();
            var homes=members.Where(p=>!string.IsNullOrEmpty(p.ownedHomeId)).Select(p=>Plot(p.ownedHomeId)).Distinct().ToArray();
            if(homes.Length==0||homes.Any(p=>p?.house==null)||homes.Select(p=>p.house.definition.level).Distinct().Count()!=1)return Array.Empty<HousingPlot>();
            // A baseline scene resident/house has no additive home route.
            // Do not claim a new shared-home projection for an unowned source instance.
            if(homes.Any(p=>p.house.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone!=true))return Array.Empty<HousingPlot>();
            return homes.Where(p=>{
                var house=runtime.Core.State.households.FirstOrDefault(h=>h.id==p.id);
                return house!=null&&house.residentIds.Count(id=>!plan.residentIds.Contains(id))+members.Length<=house.capacity;
            }).OrderBy(p=>p.id,StringComparer.Ordinal).ToArray();
        }
        bool FamilyHomeCommitted(string op,string homeId,string[] people)
        {
            var receipt=State.receipts.FirstOrDefault(r=>r.opId==op&&r.domain=="home-share");
            var group=State.groups.FirstOrDefault(g=>g.groupId=="homegroup/"+op&&g.homeId==homeId);
            return receipt!=null&&receipt.counterparty==homeId&&group!=null&&group.vacancySchema>=1&&people.All(id=>group.members.Contains(id)&&runtime.GetPerson(id)?.currentHomeId==homeId);
        }
        void FamilyHomeReceipt(string op,string suggestionId)
        {
            if(runtime.State.receipts.Any(r=>r.opId==op))return;
            runtime.State.receipts.Add(new FriendsKernelReceipt{opId=op,causeEpisode=suggestionId,domain="FamilyHousing",success=true,
                revision=++runtime.State.revision,gameMinute=runtime.Core.State.gameMinutes,message="已提交。"});
        }
        public bool ChooseFamilyHome(string humanPersonId,string suggestionId,string selectedHomeId)
        {
            LastFailure=null;var human=runtime?.GetPerson(humanPersonId);
            if(!Authority||human?.controllerKind!=PersonControllerKind.Human||!FamilyHumanIsBound(humanPersonId))
            {LastFailure="当前玩家身份尚未绑定。";return false;}
            var plan=runtime.Core.State.family?.housingSuggestions.FirstOrDefault(p=>p.id==suggestionId);
            if(plan==null||plan.residentIds==null||!plan.residentIds.Contains(human.nativeResidentId))
            {LastFailure="这项同住计划不属于当前玩家。";return false;}
            if(plan.status!="Applied"&&!FamilyHomeOptions(humanPersonId,suggestionId).Any(p=>p.id==selectedHomeId))
            {LastFailure="请选择计划成员拥有的同级且容量足够的住宅。";return false;}
            return CommitFamilyHousing(suggestionId,selectedHomeId);
        }
        public bool TryApplyAutomaticFamilyHousing(string suggestionId)
        {
            LastFailure=null;if(!Authority||!runtime||!runtime.Ready||!runtime.IsWorldRunning)return false;
            var plan=runtime.Core.State.family?.housingSuggestions.FirstOrDefault(p=>p.id==suggestionId);
            if(plan?.residentIds==null)return false;
            var members=plan.residentIds.Select(runtime.PersonFromNative).ToArray();
            // A source housing suggestion is the original decision. We never
            // infer consent from a relationship score, a marriage or a breakup.
            if(members.Length<2||members.Any(p=>p==null||p.controllerKind!=PersonControllerKind.Npc))return false;
            string op="source-family-home/"+suggestionId;
            if(plan.status=="Applied")
            {var receipt=State.receipts.FirstOrDefault(r=>r.opId==op&&r.domain=="home-share");return receipt!=null&&FamilyHomeCommitted(op,receipt.counterparty,members.Select(p=>p.personId).ToArray());}
            if(plan.status!="Pending"&&plan.status!="Ready")return false;
            var homes=FamilyHousingOptions(suggestionId);if(homes.Length==0){LastFailure="同住住宅尚未准备完成。";return false;}
            string selected=plan.houseId;
            if(!string.IsNullOrEmpty(selected))
            {if(!homes.Any(p=>p.id==selected)){LastFailure="这栋住宅现在不能选。";return false;}}
            else
            {
                // The source participant order identifies the initiating person;
                // homeId ordering only breaks the remaining spatial alternatives.
                string initiatorHome=members[0].currentHomeId;
                selected=homes.Any(p=>p.id==initiatorHome)?initiatorHome:homes[0].id;
            }
            if(members.Any(p=>runtime.State.leases.Any(l=>l.personId==p.personId)||HasActiveJob(p.personId)))return false;
            return CommitFamilyHousing(suggestionId,selected);
        }
        bool CommitFamilyHousing(string suggestionId,string selectedHomeId)
        {
            var plan=runtime.Core.State.family?.housingSuggestions.FirstOrDefault(p=>p.id==suggestionId);if(plan?.residentIds==null)return false;
            string op="source-family-home/"+suggestionId;var prior=State.receipts.FirstOrDefault(r=>r.opId==op);
            if(prior!=null&&(prior.domain!="home-share"||prior.counterparty!=selectedHomeId))
            {LastFailure="同住计划已经选择了其他住宅。";return false;}
            if(plan.status=="Applied"){bool same=prior!=null&&prior.counterparty==selectedHomeId&&FamilyHomeCommitted(op,selectedHomeId,plan.residentIds.Select(runtime.PersonFromNative).Where(p=>p!=null).Select(p=>p.personId).ToArray());if(!same)LastFailure="情况有变化，请重新选择。";return same;}
            var members=plan.residentIds.Select(runtime.PersonFromNative).ToArray();
            if(members.Any(p=>p==null)||!FamilyHousingOptions(suggestionId).Any(p=>p.id==selectedHomeId))
            {LastFailure="请选择计划成员拥有的同级且容量足够的住宅。";return false;}
            var ids=members.Select(p=>p.personId).ToArray();
            if(prior!=null&&!FamilyHomeCommitted(op,selectedHomeId,ids))
            {LastFailure="已提交的同住住宅资料发生变化。";return false;}
            string coreBefore=runtime.Core.ExportJson(false),economyBefore=JsonUtility.ToJson(State);
            var bindings=runtime.People.ToDictionary(p=>p.personId,p=>(p.ownedHomeId,p.currentHomeId,p.returnHomeId));
            var sourceCommit=runtime.DeferFamilyHomeCommit();if(sourceCommit==null){LastFailure="当前生活操作尚未完成。";return false;}
            using(sourceCommit)using(DeferExternalCommit())
            {
                Action rollback=()=>{
                    FriendsHomeInteriors.Instance?.CancelPreparedGroup(op);State=JsonUtility.FromJson<FriendsEconomySave>(economyBefore);
                    foreach(var person in runtime.People)if(bindings.TryGetValue(person.personId,out var old)){person.ownedHomeId=old.ownedHomeId;person.currentHomeId=old.currentHomeId;person.returnHomeId=old.returnHomeId;}
                    if(!runtime.Core.TryImportJson(coreBefore,out var error))throw new InvalidOperationException(error);
                };
                try
                {
                    var selected=runtime.Core.ChooseFamilyHouse(suggestionId,selectedHomeId);
                    if(!selected.success){LastFailure=selected.message;rollback();return false;}
                    // Complete the source choice inside the suppressed-save section
                    // before the fallible world projection. A rejected projection
                    // restores the entire pre-choice source state below.
                    runtime.Core.CompleteFamilyHousingSuggestion(suggestionId,true);
                    if(runtime.Core.State.family.housingSuggestions.First(p=>p.id==suggestionId).status!="Applied")throw new InvalidOperationException("同住计划尚未确认。");
                    if(prior==null&&!ShareHome(ids,selectedHomeId,op)){LastFailure="住宅尚未准备完成，请稍后重新选择。";rollback();return false;}
                    if(!FamilyHomeCommitted(op,selectedHomeId,ids))throw new InvalidOperationException("同住住宅尚未完成提交。");
                    FamilyHomeReceipt(op,suggestionId);Commit();return true;
                }
                catch(Exception error)
                {
                    if(FamilyHomeCommitted(op,selectedHomeId,ids)&&runtime.Core.State.family.housingSuggestions.FirstOrDefault(p=>p.id==suggestionId)?.status=="Applied")
                    {FamilyHomeReceipt(op,suggestionId);LastFailure="已搬到一起。";Commit();return true;}
                    rollback();Debug.LogWarning("[FriendsLife] Family housing failed: "+error);LastFailure="住宅操作失败，已恢复原样。请重试。";return false;
                }
            }
        }
    }
}

