using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Tablet;
using SushiAndFish.ResidentialIsland;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    public static partial class FriendHousingUI
    {
        sealed class SplitDraft
        {public string personId,groupId,commandId,message,worldId;public long revision;public int level;public string[] owners;public readonly List<HomeSplitChoice> choices=new List<HomeSplitChoice>();public bool submitted,accepted;}
        static SplitDraft split;
        static FriendsLifeNetwork splitNetwork;
        public static bool HasSplitTask=>split!=null;
        public static bool SplitSelectionComplete=>split!=null&&split.choices.Count==split.owners.Length;
        public static bool SplitSubmitting=>split?.submitted==true;
        public static bool SplitTransportReady=>splitNetwork&&splitNetwork.CanSubmitHousingCommand;
        public static string SplitNextOwner=>split!=null&&split.choices.Count<split.owners.Length?split.owners[split.choices.Count]:null;
        public static int SplitLevel=>split?.level??0;
        public static string SplitMessage
        {
            get
            {
                if(split==null)return null;if(!string.IsNullOrEmpty(split.message))return split.message;
                if(split.submitted)return split.accepted?"等待住宅同步":"分住处理中…";
                if(SplitSelectionComplete)return "已选择 "+split.choices.Count+" 处空地\n确认后按 Lv"+split.level+" 分住，转换不另收费";
                string name=FriendsLifeRuntime.Instance?.GetPerson(SplitNextOwner)?.displayName??"成员";
                return "为"+name+"选择 Lv"+split.level+" 住宅地块\n已选择 "+split.choices.Count+" / "+split.owners.Length+" 处空地";
            }
        }
        static void BindSplitNetwork()
        {
            var current=FriendsLifeNetwork.Instance;if(splitNetwork==current)return;
            if(splitNetwork)splitNetwork.ResponseReceived-=OnSplitResponse;splitNetwork=current;if(splitNetwork)splitNetwork.ResponseReceived+=OnSplitResponse;
        }
        static void OnSplitResponse(FriendsWorldResponse response)
        {
            if(split==null||response==null||response.commandId!=split.commandId)return;
            if(FriendsLifeRuntime.Instance?.State?.worldId!=split.worldId){FinishSplit(false);return;}
            if(response.unresolved){split.message="操作结果待确认";return;}
            if(response.accepted){split.accepted=true;split.message=null;}
            else{split.submitted=false;split.accepted=false;split.message=response.message??"分住尚未提交，请调整后重试";}
        }
        public static bool BeginSplit(string personId)
        {
            var runtime=FriendsLifeRuntime.Instance;var economy=FriendsEconomyService.Instance;var human=runtime?.LocalHuman();var housing=FriendHousingIsland.Instance;
            if(split!=null||!runtime||!runtime.Ready||!economy||human==null||!housing||housing.HasArrivalTask||!economy.CanRequestHomeSplit(personId,human.personId))return false;
            var group=economy.SharedHomeOf(personId);var owners=economy.SplitHomeMembers(personId).Select(t=>t.personId).ToArray();if(group==null||owners.Length==0)return false;
            split=new SplitDraft{personId=personId,worldId=runtime.State.worldId,groupId=group.groupId,revision=group.revision,level=group.level,owners=owners};BindSplitNetwork();
            foreach(var tablet in FriendSceneObjects.Tablets().Where(t=>t.IsInitialized))FriendTabletAccess.OpenMap(tablet);
            return true;
        }
        public static void TickSplit()
        {
            BindSplitNetwork();if(split==null)return;var runtime=FriendsLifeRuntime.Instance;var economy=FriendsEconomyService.Instance;
            if(!runtime||!runtime.Ready||!economy){if(!split.submitted)CancelSplit();return;}
            if(runtime.State.worldId!=split.worldId){FinishSplit(false);return;}
            var group=economy.SharedHomeOf(split.personId);
            if(split.submitted)
            {
                var exactReceipt=economy.State.receipts.FirstOrDefault(r=>r.opId=="housing/"+split.commandId&&r.domain=="home-split"&&r.actorId==split.personId&&r.counterparty==split.groupId);
                if(exactReceipt!=null)split.accepted=true; // Actual authority receipt, not timeout success.
                // An ACK alone is not a projected-state result. Unknown requests
                // stay pending; there is no timeout-created success or reissue.
                if(split.accepted&&(group==null||group.groupId!=split.groupId)&&split.choices.All(c=>runtime.GetPerson(c.personId)?.currentHomeId==c.plotId&&FriendHousingIsland.Instance.Plots.Any(p=>p.id==c.plotId&&p.house&&p.house.definition.level==split.level&&p.house.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone==true)))FinishSplit();return;
            }
            if(group==null||group.groupId!=split.groupId||group.revision!=split.revision){CancelSplit();return;}
        }
        public static bool SplitPlotSelected(string plotId)=>split?.choices.Any(c=>c.plotId==plotId)==true;
        public static HousingType NextSplitType()=>split==null?null:FriendsEconomyService.Instance?.SplitHomeType(split.personId,SplitNextOwner);
        public static bool ChooseSplitPlot(HousingPlot plot,HousingType type,float yaw)
        {
            if(split==null||split.submitted||SplitSelectionComplete)return false;
            if(!FriendHousingIsland.Instance.IsEmptyForNewHousing(plot.id)||SplitPlotSelected(plot.id)){split.message="请选择当前未使用的空地";return false;}
            if(type==null||type.level!=split.level){split.message="分住住宅须为 Lv"+split.level;return false;}
            split.choices.Add(new HomeSplitChoice{personId=SplitNextOwner,plotId=plot.id,typeId=type.id,yaw=yaw});split.message=null;return true;
        }
        public static void ConfirmSplit()
        {
            if(split==null||split.submitted||!SplitSelectionComplete)return;BindSplitNetwork();var runtime=FriendsLifeRuntime.Instance;var human=runtime?.LocalHuman();
            if(!splitNetwork||!splitNetwork.CanSubmitHousingCommand||human==null){split.message="正在连接，请稍后再试";return;}
            split.commandId=Guid.NewGuid().ToString("N");split.submitted=true;split.accepted=false;split.message=null;
            var plan=new HousingPlanCommand{splitGroupId=split.groupId,splitRevision=split.revision,splitHomes=split.choices.Select(c=>new HomeSplitChoice{personId=c.personId,plotId=c.plotId,typeId=c.typeId,yaw=c.yaw}).ToList()};
            splitNetwork.Submit(new FriendsWorldCommand{commandId=split.commandId,kind="housing",actorPersonId=human.personId,targetPersonId=split.personId,actionId="split",optionId=JsonUtility.ToJson(plan)});
        }
        public static void ResetSplitChoices(){if(split==null||split.submitted)return;split.choices.Clear();split.message=null;CancelMapPreview();}
        public static void CancelSplit(){if(split?.submitted==true)return;FinishSplit();}
        static void FinishSplit(bool cancelPreview=true){split=null;if(cancelPreview)CancelMapPreview();}
        static void CancelMapPreview(){foreach(var map in FriendSceneObjects.Maps())if(map.PreviewPlot!=null)map.CancelPreview();}
    }
}
