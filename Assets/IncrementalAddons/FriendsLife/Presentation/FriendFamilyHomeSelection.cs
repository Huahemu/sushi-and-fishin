using System;
using System.Collections.Generic;
using System.Linq;
using TomodachiLife.Simulation;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendTabletPresenter
    {
        readonly System.Collections.Generic.Dictionary<string,string> familyBirthNames=new System.Collections.Generic.Dictionary<string,string>();
        IEnumerable<LifeFamilyHousingSuggestion> PendingHumanFamilyHomePlans()
        {
            var runtime=FriendsLifeRuntime.Instance;var human=runtime?.LocalHuman();var focus=runtime?.GetPerson(personId);
            if(!runtime||!runtime.Ready||human==null||focus==null||runtime.Core.State.family==null)return Enumerable.Empty<LifeFamilyHousingSuggestion>();
            return runtime.Core.State.family.housingSuggestions.Where(p=>(p.status=="Pending"||p.status=="Ready")&&p.residentIds!=null&&
                p.residentIds.Contains(human.nativeResidentId)&&p.residentIds.Contains(focus.nativeResidentId));
        }
        bool HasFamilyHomeSelection()=>PendingHumanFamilyHomePlans().Any();
        void RenderFamilyHomeSelection(Transform content,ref float y)
        {
            var runtime=FriendsLifeRuntime.Instance;var human=runtime?.LocalHuman();var economy=FriendsEconomyService.Instance;var plans=PendingHumanFamilyHomePlans().ToArray();
            if(human==null||!economy||plans.Length==0){FriendUi.Text("NoFamilyHomePlan",content,0,y,1120,70,"暂无待选择的同住计划。",24);y+=86;return;}
            foreach(var plan in plans)
            {
                string names=string.Join("、",plan.residentIds.Select(runtime.PersonFromNative).Where(p=>p!=null).Select(p=>p.displayName));
                FriendUi.Text("FamilyHomePlan_"+plan.id,content,0,y,1120,70,names+" · 选择同住住宅",24);y+=86;
                var options=economy.FamilyHomeOptions(human.personId,plan.id);
                if(options.Length==0){FriendUi.Text("NoFamilyHomeOption_"+plan.id,content,0,y,1120,70,"需要成员拥有同级且容量足够的住宅。",24);y+=86;continue;}
                foreach(var home in options)
                {
                    string suggestion=plan.id,selected=home.id;var owner=runtime.People.FirstOrDefault(p=>p.ownedHomeId==selected);
                    string title=(owner?.displayName??selected)+"的住宅 · "+selected+" · Lv"+home.house.definition.level;
                    SourceRow(content,ref y,title,()=>SendTrackedHousing("choose-family-home",new HousingPlanCommand{familySuggestionId=suggestion,homeId=selected},human.personId));
                }
            }
        }

        void StartSelectedFamilyAction(string action,string selectedPerson)
        {
            var runtime=FriendsLifeRuntime.Instance;var human=runtime?.LocalHuman();
            if(human==null){ShowInlineFeedback("玩家资料尚未就绪。");return;}
            OpenSourcePage(selectedPerson,"family-choose/"+action);
        }
        void RenderPendingHumanFamilyChoices(Transform content,ref float y)
        {
            var runtime=FriendsLifeRuntime.Instance;var human=runtime?.LocalHuman();
            if(!runtime||human==null)return;
            foreach(var episode in runtime.GetEpisodes(personId).Where(e=>!e.committed&&e.sourceKind=="Family"&&e.familyChoice!=null&&
                (e.focusPersonId==human.personId||e.targetPersonId==human.personId)))
            {
                FriendUi.Text("FamilyChoice_"+episode.sourceId,content,0,y,1120,70,episode.familyChoice.title,24);y+=86;
                TMPro.TMP_InputField babyName=null;
                if(episode.familyChoice.action=="Birth")
                {
                    string choiceId=episode.sourceId;if(!familyBirthNames.ContainsKey(choiceId))familyBirthNames[choiceId]="宝宝"+(runtime.Core.State.family.children.Count+1);
                    babyName=FriendUi.Input("BabyName_"+choiceId,content,0,y,1120,56,familyBirthNames[choiceId],"给宝宝起名字");babyName.onEndEdit.AddListener(value=>familyBirthNames[choiceId]=value);y+=76;
                }
                foreach(var option in episode.familyChoice.options)
                {
                    string choice=episode.sourceId,id=option.id;
                    var nameInput=babyName;
                    var button=SourceRow(content,ref y,option.label,()=>Command("family:"+choice,nameInput?new Newtonsoft.Json.Linq.JObject{["optionId"]=id,["childName"]=nameInput.text}.ToString(Newtonsoft.Json.Formatting.None):id));
                    button.interactable=option.enabled;
                    if(!option.enabled&&!string.IsNullOrEmpty(option.reason)){FriendUi.Text("FamilyChoiceReason_"+id,content,0,y,1120,60,option.reason,22);y+=76;}
                }
            }
        }
    }
}
