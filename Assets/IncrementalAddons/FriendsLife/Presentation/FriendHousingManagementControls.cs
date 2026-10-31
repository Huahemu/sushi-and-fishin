using System;
using System.Linq;
using SushiAndFish.ResidentialIsland;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SushiAndFish.FriendsLife
{
    // Controls are attached only to information panels for actual NEW homes.
    // Original HousingInfoWindow, its callbacks and original houses are unchanged.
    public sealed class FriendHousingManagementControls:MonoBehaviour
    {
        HousingInfoWindow window;RectTransform bar,confirmation;TextMeshProUGUI occupants,feedback;Button upgrade,separate,confirm;
        FriendsLifeNetwork transport;string pending,message;long shownQuote;
        void Awake(){window=GetComponent<HousingInfoWindow>();}
        public bool TryBackConfirmation()
        {
            if(!confirmation||!confirmation.gameObject.activeInHierarchy)return false;
            if(string.IsNullOrEmpty(pending)){confirmation.gameObject.SetActive(false);Destroy(confirmation.gameObject);confirmation=null;}
            return true;
        }
        void Bind()
        {
            var network=FriendsLifeNetwork.Instance;if(transport==network)return;if(transport)transport.ResponseReceived-=Response;transport=network;if(transport)transport.ResponseReceived+=Response;
        }
        void Response(FriendsWorldResponse response)
        {if(response==null||response.commandId!=pending)return;if(response.unresolved){message="请稍等";return;}pending=null;message=FriendUi.PlayerText(response.message,"没有完成，请稍后再试。");if(response.accepted&&confirmation)Destroy(confirmation.gameObject);}
        void Update()
        {
            if(!window||!window.Panel||!window.Building?.house||window.Building.house.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone!=true){Destroy(this);return;}
            Bind();var service=FriendsEconomyService.Instance;var runtime=FriendsLifeRuntime.Instance;if(!service||!runtime||!runtime.Ready)return;
            if(!string.IsNullOrEmpty(pending)&&service.State.receipts.Any(r=>r.opId=="housing/"+pending&&r.domain=="home-upgrade-plan"&&r.counterparty==window.Building.id))
            {pending=null;message="升级已提交";if(confirmation)Destroy(confirmation.gameObject);}
            else if(!string.IsNullOrEmpty(pending)&&service.State.queuedUpgrades.Any(q=>q.opId=="housing/"+pending&&q.homeId==window.Building.id))
            {pending=null;message="升级安排已提交";if(confirmation)Destroy(confirmation.gameObject);}
            if(!bar)
            {
                float w=window.Panel.rect.width,h=window.Panel.rect.height;var catalog=FriendHousingIsland.Instance.Catalog;
                // Keep controls inside the original card and tablet raycast bounds.
                // This component only owns information windows for NEW homes.
                var portrait=window.Panel.Find("FloorPlan") as RectTransform;
                if(portrait){portrait.sizeDelta=Vector2.one*h*.42f;portrait.anchoredPosition=new Vector2(0,h*.08f);}
                var description=window.Panel.Find("Description") as RectTransform;
                if(description){description.sizeDelta=new Vector2(w*.9f,h*.07f);description.anchoredPosition=new Vector2(0,-h*.17f);}
                // Bar bottom at -0.41h stays clear of the card's frame band; texts are one line with an ellipsis.
                bar=HousingUI.Image("NewResidenceManagement",window.Panel,new Vector2(w*.94f,h*.20f),catalog.panelColor,catalog.panelSprite).rectTransform;bar.anchoredPosition=new Vector2(0,-h*.31f);
                occupants=HousingUI.Text("Residents",bar,new Vector2(w*.90f,h*.065f),"",w*.021f);occupants.rectTransform.anchoredPosition=new Vector2(0,h*.06f);occupants.overflowMode=TextOverflowModes.Ellipsis;occupants.textWrappingMode=TextWrappingModes.NoWrap;
                upgrade=HousingUI.Button("Upgrade",bar,new Vector2(w*.33f,h*.075f),"升级",OpenUpgrade);((RectTransform)upgrade.transform).anchoredPosition=new Vector2(-w*.22f,-h*.015f);
                separate=HousingUI.Button("Separate",bar,new Vector2(w*.33f,h*.075f),"分住",()=>{var target=CommandTarget();if(target!=null&&FriendHousingUI.BeginSplit(target)){window.Close();}else message="住宅暂不可调整";});((RectTransform)separate.transform).anchoredPosition=new Vector2(w*.22f,-h*.015f);
                feedback=HousingUI.Text("Result",bar,new Vector2(w*.90f,h*.045f),"",w*.018f);feedback.rectTransform.anchoredPosition=new Vector2(0,-h*.07f);feedback.overflowMode=TextOverflowModes.Ellipsis;feedback.textWrappingMode=TextWrappingModes.NoWrap;
                // Upgrade and split stay hidden while HousingUpgrade is off (C009 core).
                if(!FriendsLifeFeatureFlags.HousingUpgrade){upgrade.gameObject.SetActive(false);separate.gameObject.SetActive(false);}
            }
            string home=window.Building.id;var people=service.MembersOf(home).ToArray();
            occupants.text="Lv"+window.Building.house.definition.level+" · "+string.Join("、",people.Select(p=>p.displayName));feedback.text=message??"";
            long quote=service.UpgradeQuote(home);bool pendingUpgrade=service.IsUpgradePending(home);upgrade.GetComponentInChildren<TMP_Text>().text=pendingUpgrade?"升级中…":quote>0?"升级 "+(quote/100)+" 金币":"最高等级";
            upgrade.interactable=FriendsLifeFeatureFlags.HousingUpgrade&&string.IsNullOrEmpty(pending)&&!pendingUpgrade&&quote>0;
            var targetPerson=FriendsLifeFeatureFlags.HousingUpgrade?CommandTarget():null;separate.gameObject.SetActive(targetPerson!=null&&service.SplitHomeMembers(targetPerson).Length>0);
            separate.interactable=string.IsNullOrEmpty(pending);
            if(confirmation&&confirm)confirm.interactable=string.IsNullOrEmpty(pending)&&transport&&transport.CanSubmitHousingCommand&&shownQuote==quote&&service.CanUpgrade(home,true,out _);
        }
        string CommandTarget()
        {
            var runtime=FriendsLifeRuntime.Instance;var service=FriendsEconomyService.Instance;var human=runtime?.LocalHuman();if(!window||!service||human==null)return null;
            var members=service.MembersOf(window.Building.id).ToArray();if(members.Any(p=>p.personId==human.personId))return human.personId;
            return members.FirstOrDefault(p=>p.controllerKind==PersonControllerKind.Npc)?.personId;
        }
        void OpenUpgrade()
        {
            var service=FriendsEconomyService.Instance;if(!FriendsLifeFeatureFlags.HousingUpgrade||!service||confirmation)return;shownQuote=service.UpgradeQuote(window.Building.id);if(shownQuote<=0)return;
            float w=window.Panel.rect.width,h=window.Panel.rect.height;var catalog=FriendHousingIsland.Instance.Catalog;
            confirmation=HousingUI.Image("NewResidenceUpgradeConfirmation",window.Panel,new Vector2(w*.78f,h*.58f),catalog.panelColor,catalog.panelSprite).rectTransform;confirmation.SetAsLastSibling();
            var label=HousingUI.Text("Price",confirmation,new Vector2(w*.7f,h*.25f),"升级住宅\n"+(shownQuote/100)+" 金币",w*.03f);label.rectTransform.anchoredPosition=new Vector2(0,h*.09f);
            var cancel=HousingUI.Button("Cancel",confirmation,new Vector2(w*.24f,h*.09f),"取消",()=>{if(string.IsNullOrEmpty(pending)&&confirmation)Destroy(confirmation.gameObject);});((RectTransform)cancel.transform).anchoredPosition=new Vector2(-w*.17f,-h*.18f);
            confirm=HousingUI.Button("Confirm",confirmation,new Vector2(w*.24f,h*.09f),"确认",SubmitUpgrade);((RectTransform)confirm.transform).anchoredPosition=new Vector2(w*.17f,-h*.18f);
        }
        void SubmitUpgrade()
        {
            var service=FriendsEconomyService.Instance;var runtime=FriendsLifeRuntime.Instance;var human=runtime?.LocalHuman();string target=CommandTarget();
            if(!service||human==null||target==null||!transport||!transport.CanSubmitHousingCommand||!string.IsNullOrEmpty(pending))return;
            string reason=null;if(service.UpgradeQuote(window.Building.id)!=shownQuote||!service.CanUpgrade(window.Building.id,true,out reason)){message=reason??"住宅方案已变化";return;}
            pending=Guid.NewGuid().ToString("N");message="升级处理中…";
            transport.Submit(new FriendsWorldCommand{commandId=pending,kind="housing",actorPersonId=human.personId,targetPersonId=target,actionId="upgrade",optionId=JsonUtility.ToJson(new HousingPlanCommand{homeId=window.Building.id})});
        }
        void OnDestroy(){if(transport)transport.ResponseReceived-=Response;if(bar)Destroy(bar.gameObject);if(confirmation)Destroy(confirmation.gameObject);}
    }
}
