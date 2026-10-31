using System;
using System.Collections.Generic;
using System.Linq;
using Bozo.ModularCharacters;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.UI;
using SushiAndFish.ResidentialIsland;
using TMPro;
using TomodachiLife;
using TomodachiLife.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendTabletPresenter : BaseApp
    {
        public static FriendTabletPresenter Instance {get;private set;}
        public readonly TabletNavigationStack Navigation=new TabletNavigationStack();
        TabletController tablet;RectTransform view;ScrollRect scroll;string personId,tab="资料",page="List",pairId,filter="";
        SourceTextResolver texts;FriendsLifeRuntime bound;TMP_InputField nameField,phraseField,hobbyField;
        string editNativeJson;readonly Stack<UiReturnFrame> pages=new Stack<UiReturnFrame>();bool pendingWorldRefresh,preserveWorldModal;
        PhotoFriendPipeline photos;PhotoFriendDraft draft;PhotoFriendStage renderedPhotoStage;bool renderedNativeMaskPending;float lastRefresh;bool suppressRefresh;
        public string CurrentPersonId=>personId; public string CurrentTab=>tab;public string CurrentPage=>page;
        const float OuterMargin=6f;
        Action previousReturnFromHome;Action<string> previousProfileRequested;FriendTabletPresenter previousInstance;
        public static FriendTabletPresenter Install(TabletController controller,BaseApp sibling)
        {
            RectTransform r=null;FriendTabletPresenter presenter=null;
            try
            {
                r=FriendUi.Rect("FriendsApp",sibling.transform.parent,0,0,1200,720);FriendUi.Stretch(r);
                presenter=r.gameObject.AddComponent<FriendTabletPresenter>();presenter.tablet=controller;
                presenter.previousInstance=Instance;presenter.previousReturnFromHome=FriendHousingUI.ReturnFromHome;presenter.previousProfileRequested=FriendHousingUI.ResidentProfileRequested;
                presenter.Initialize();presenter.Hide();
                var icon=controller.GetComponent<FriendAppSlotVisual>();if(!icon)icon=controller.gameObject.AddComponent<FriendAppSlotVisual>();icon.Bind(controller.transform);
                return presenter;
            }
            catch
            {
                if(presenter)presenter.RollbackInstall();else if(r){r.gameObject.SetActive(false);if(Application.isPlaying)Destroy(r.gameObject);else DestroyImmediate(r.gameObject);}
                throw;
            }
        }
        public void RollbackInstall()
        {
            if(FriendHousingUI.ReturnFromHome==(Action)ReturnFromHome)FriendHousingUI.ReturnFromHome=previousReturnFromHome;
            if(FriendHousingUI.ResidentProfileRequested==(Action<string>)OpenPerson)FriendHousingUI.ResidentProfileRequested=previousProfileRequested;
            if(Instance==this)Instance=previousInstance;gameObject.SetActive(false);
            if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
        }
        public override void Initialize()
        {
            Instance=this;if(view)return;
            view=FriendUi.Rect("FriendsContent",transform,0,0,1200,720);view.anchorMin=view.anchorMax=new Vector2(.5f,.5f);view.pivot=new Vector2(.5f,.5f);view.anchoredPosition=Vector2.zero;
            texts=new SourceTextResolver();photos=PhotoFriendPipeline.GetOrCreate();
            FriendHousingUI.ReturnFromHome=ReturnFromHome;FriendHousingUI.ResidentProfileRequested=OpenPerson;
        }
        void OnDestroy(){if(bound)bound.Changed-=OnChanged;UnbindActionFeedback();if(Instance==this)Instance=null;}
        void LateUpdate()
        {
            UpdateActionFeedback();
            // Fit the 1200x720 page inside the app area with an outer margin, so the
            // paper never touches the tablet's bottom edge or the top bar.
            var parent=(RectTransform)transform;if(view&&parent.rect.width>2*OuterMargin&&parent.rect.height>2*OuterMargin)view.localScale=Vector3.one*Mathf.Min((parent.rect.width-2*OuterMargin)/1200,(parent.rect.height-2*OuterMargin)/720);
            if(bound!=FriendsLifeRuntime.Instance){if(bound)bound.Changed-=OnChanged;bound=FriendsLifeRuntime.Instance;if(bound)bound.Changed+=OnChanged;}
            if(page=="Photo"&&draft!=null&&Time.unscaledTime-lastRefresh>.5f){lastRefresh=Time.unscaledTime;if(renderedPhotoStage!=draft.stage||renderedNativeMaskPending!=draft.nativeMaskPending)Render();else RenderPhoto(false);}
            if(pendingWorldRefresh&&isActiveAndEnabled&&!suppressRefresh&&!IsEditing()&&!PointerIsPressed())
            {pendingWorldRefresh=false;if(!SourceGameControlsUnchanged()){float pos=scroll?scroll.verticalNormalizedPosition:1;preserveWorldModal=true;try{Render();}finally{preserveWorldModal=false;}if(scroll)scroll.verticalNormalizedPosition=pos;}}
        }
        void OnChanged(){if(isActiveAndEnabled&&!suppressRefresh)pendingWorldRefresh=true;}
        static bool PointerIsPressed()=>UnityEngine.InputSystem.Mouse.current!=null&&(UnityEngine.InputSystem.Mouse.current.leftButton.isPressed||UnityEngine.InputSystem.Mouse.current.rightButton.isPressed||UnityEngine.InputSystem.Mouse.current.middleButton.isPressed)||UnityEngine.InputSystem.Touchscreen.current!=null&&UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.isPressed;
        bool IsEditing()=>page=="Profile"||page=="Photo"||page=="Add"||page=="Questionnaire"||page.StartsWith("Source:design/",StringComparison.Ordinal)||
            (EventSystem.current&&EventSystem.current.currentSelectedGameObject&&EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>()?.isFocused==true);
        public override void Show(){base.Show();Render();}
        public override void Hide(){ClearActionFeedback();base.Hide();}
        UiReturnFrame Capture()=>new UiReturnFrame{page=page,personId=personId,entityId=page=="Pair"?personId+"/"+pairId:personId,tab=tab,selectedRelation=pairId,scroll=scroll?scroll.verticalNormalizedPosition:1,
            focus=EventSystem.current?EventSystem.current.currentSelectedGameObject:null,focusName=EventSystem.current&&EventSystem.current.currentSelectedGameObject?EventSystem.current.currentSelectedGameObject.name:""};
        void Restore(UiReturnFrame frame){page=frame.page;personId=frame.personId;tab=frame.tab;pairId=frame.selectedRelation;Render();Canvas.ForceUpdateCanvases();if(scroll)scroll.verticalNormalizedPosition=frame.scroll;TabletNavigationStack.RestoreFocus(frame,view);}
        void Push(){pages.Push(Capture());}
        UiReturnFrame CaptureNavigation(){var frame=Capture();frame.pageHistory=pages.ToArray();return frame;}
        void RestoreNavigationFrame(UiReturnFrame frame)
        {
            if(frame==null)return;FriendHousingUI.EndHomeVisit(false);pages.Clear();
            if(frame.app=="Map"){FriendTabletAccess.OpenMap(tablet);FriendHousingUI.RestoreMapFrame(frame,tablet,Navigation.HasVisit);return;}
            if(frame.pageHistory!=null)for(int i=frame.pageHistory.Length-1;i>=0;i--)pages.Push(frame.pageHistory[i]);
            FriendTabletAccess.ShowFriends(tablet);Restore(frame);
        }
        public bool Back()
        {
            var input=EventSystem.current&&EventSystem.current.currentSelectedGameObject?EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>():null;
            if(input&&input.isFocused){input.DeactivateInputField();return true;}
            var modal=view.Find("Status");if(modal){Destroy(modal.gameObject);return true;}
            if(pages.Count>0){Restore(pages.Pop());return true;}
            if(Navigation.HasVisit){FriendHousingUI.EndHomeVisit(false);ReturnFromHome();return true;}
            if(page!="List"){OpenList();return true;}return false;
        }
        public void CloseTablet(){Navigation.CloseTablet();FriendHousingUI.EndHomeVisit(false);pages.Clear();}
        public void OpenNormalApp(){Navigation.OpenNormalApp();pages.Clear();}
        public UiReturnFrame SuspendTablet()
        {
            UiReturnFrame origin=null;while(Navigation.HasVisit)origin=Navigation.Back();
            if(origin==null&&tablet&&FriendTabletAccess.IsMapActive(tablet))origin=FriendMapVisitView.Capture(tablet,personId);
            if(origin==null&&gameObject.activeSelf)origin=CaptureNavigation();
            FriendHousingUI.EndHomeVisit(false);pages.Clear();return origin;
        }
        public void ResumeTablet(UiReturnFrame frame)
        {
            RestoreNavigationFrame(frame);
        }
        public void OpenList(){page="List";personId=null;pairId=null;pages.Clear();Render();}
        public void OpenPerson(string id)
        {
            if(string.IsNullOrEmpty(id))return;
            if(isActiveAndEnabled&&page=="Detail"&&personId==id)return;
            UiReturnFrame existing;if(Navigation.TryReturnTo("Friends","Detail",id,out existing)){RestoreNavigationFrame(existing);return;}
            bool fromMap=tablet&&FriendTabletAccess.IsMapActive(tablet);
            if(fromMap){Navigation.PushVisit(FriendMapVisitView.Capture(tablet,id));FriendHousingUI.EndHomeVisit(false);pages.Clear();}
            else if(isActiveAndEnabled&&page!="List"&&!(page=="Detail"&&personId==id))Push();personId=id;tab="资料";page="Detail";
            FriendTabletAccess.ShowFriends(tablet);Render();
        }
        public void OpenSourcePage(string id,string sourcePage){Push();personId=id;page="Source:"+sourcePage;Render();}
        public void ReturnFromHome()
        {
            RestoreNavigationFrame(Navigation.Back());
        }
        void VisitHome()
        {
            var person=FriendsLifeRuntime.Instance?.GetPerson(personId);var home=FriendHousingIsland.Instance?.Plots.FirstOrDefault(p=>p.id==person?.currentHomeId&&p.house);
            if(home==null){Status("暂无住宅");return;}
            UiReturnFrame existing;if(Navigation.TryReturnTo("Map","Map",FriendMapBuildingInfo.Entity(home.id,home.house),out existing)){RestoreNavigationFrame(existing);return;}
            var frame=CaptureNavigation();frame.purpose="HomeVisit";Navigation.PushVisit(frame);FriendTabletAccess.OpenMap(tablet);
            if(!FriendHousingUI.OpenHomeFromFriend(personId,tablet)){RestoreNavigationFrame(Navigation.Back());Status("住宅地图暂不可用");}
        }
        // ---- C009 shell -------------------------------------------------------
        // Detail tabs. Each body has a round-2 hook (below) so M1-M4 can supply
        // real content from their own partial files without editing this file.
        static readonly string[] DetailTabs={"资料","关系","烦恼·社交","工作"};
        FriendUi.FriendPage current;
        // Round-2 extension points. Implement in your own partial file:
        //   M1 FriendTabletPresenter.Life.cs      DescribeFriendStatus, RenderProfileTab
        //   M3 FriendTabletPresenter.Relations.cs RenderRelationsTab (today the default calls M3's row renderer)
        //   M2 FriendTabletPresenter.Problems.cs  RenderProblemsTab
        //   M4 FriendPublicJobsPage.cs            RenderWorkTab
        // content is the tab's scroll content (ContentWidth(content) wide); advance y
        // with Field/Menu/SourceRow/FriendUi.Flow. Set handled=true to replace the
        // default body. A hook with no implementation compiles away.
        partial void DescribeFriendStatus(string id,ref string status);
        partial void RenderProfileTab(Transform content,ref float y,ref bool handled);
        partial void RenderRelationsTab(Transform content,ref float y,ref bool handled);
        partial void RenderProblemsTab(Transform content,ref float y,ref bool handled);
        partial void RenderWorkTab(Transform content,ref float y,ref bool handled);
        void Render()
        {
            if(!view)return;Transform modal=null;if(preserveWorldModal)for(int i=view.childCount-1;i>=0;i--){var child=view.GetChild(i);if(child.name=="Status"&&child.gameObject.activeSelf){modal=child;break;}}
            // Keep a retained modal in its Canvas while replacing only the page.
            for(int i=view.childCount-1;i>=0;i--){var child=view.GetChild(i);if(child==modal)continue;var go=child.gameObject;if(Application.isPlaying){go.SetActive(false);Destroy(go);}else DestroyImmediate(go);}
            scroll=null;current=null;
            switch(page){case "List":RenderList();break;case "Detail":RenderDetail();break;case "Pair":RenderPair();break;case "Add":RenderAdd();break;case "Drafts":RenderDrafts();break;case "Profile":RenderProfile();break;case "Photo":RenderPhoto(true);break;case "Questionnaire":RenderQuestionnaire();break;case "OriginalCasino":RenderOriginalCasino();break;case "OriginalActivity":RenderOriginalActivity();break;default:RenderSource();break;}
            RestoreInlineFeedback();
            if(modal){modal.SetAsLastSibling();modal.GetComponent<FriendTabletStatusScope>()?.Bind(current?.safe);}
        }
        FriendUi.FriendPage Page(string template,string title,bool toolbar=false){current=FriendUi.Page(view,template,title,toolbar);return current;}
        void BackButton(Action action=null)=>FriendUi.FooterButton(current,"返回",action??(()=>Back()));
        static float ContentWidth(Transform p)=>p is RectTransform r&&r.sizeDelta.x>0?r.sizeDelta.x:1120;
        static void TopStretch(RectTransform r,float left,float right,float top,float height)
        {r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);r.offsetMin=new Vector2(left,-(top+height));r.offsetMax=new Vector2(-right,-top);}
        static void RightMiddle(RectTransform r,float right){r.anchorMin=r.anchorMax=r.pivot=new Vector2(1,.5f);r.anchoredPosition=new Vector2(-right,0);}
        // One short line: what the friend is doing now (M1 supplies the real status).
        string StatusLine(string id)
        {
            string status=null;DescribeFriendStatus(id,ref status);if(!string.IsNullOrEmpty(status))return status;
            status=FriendLifeStatus.Describe(id);if(!string.IsNullOrEmpty(status))return status;
            string header=FriendOriginalActivityUi.Header(id);if(string.IsNullOrEmpty(header))return "";
            var lines=header.Split('\n');return (lines.Length>1?lines[1]:lines[0]).Replace("当前活动：","");
        }
        void RenderList()
        {
            var p=Page("T01_FriendList","朋友",true);var search=FriendUi.Flexible(FriendUi.Input("Search",p.toolbar,0,0,600,FriendUi.ToolbarH,filter,"搜索"));
            search.onValueChanged.AddListener(value=>{filter=value;RefreshListRows(p.body);});
            var add=FriendUi.Button("Add",p.toolbar,0,0,240,FriendUi.ToolbarH,"添加朋友",()=>{Push();page="Add";Render();},true);FriendUi.FitCaption(add);FriendUi.Flexible(add,240,0);
            RefreshListRows(p.body);FriendUi.FooterButton(p,"草稿",()=>{Push();page="Drafts";Render();},260);
            FriendUi.FooterButton(p,"返回应用",()=>tablet.GetComponent<FriendTabletAccess>()?.ReturnToAppGrid(),220);
        }
        void RefreshListRows(RectTransform body)
        {
            var old=body.Find("List");if(old){old.gameObject.SetActive(false);Destroy(old.gameObject);}
            scroll=FriendUi.List(body,out var content,12);int shown=0;var skin=FriendUiSkin.Load();
            foreach(var person in FriendsLifeRuntime.Instance?.GetPeople()??Array.Empty<PersonBinding>())
            {
                if(!string.IsNullOrEmpty(filter)&&(person.displayName??"").IndexOf(filter,StringComparison.CurrentCultureIgnoreCase)<0)continue;
                var r=FriendUi.ListRow(content,116);r.name="T10_PortraitRow_"+person.personId;shown++;
                var slot=FriendUi.Image("ToolSlot",r,0,0,1000,116,skin?skin.toolSlot:null);FriendUi.Fill(slot.rectTransform,0,0,0,0);
                var raw=FriendUi.Portrait("Portrait",r,12,10,96,96);FriendPortraitCache.Bind(raw,person.personId);
                var name=FriendUi.Text("Name",r,0,0,0,0,person.displayName,26);TopStretch(name.rectTransform,128,214,14,40);name.alignment=TextAlignmentOptions.MidlineLeft;name.textWrappingMode=TextWrappingModes.NoWrap;
                var activity=FriendUi.Text("Activity",r,0,0,0,0,StatusLine(person.personId),20);TopStretch(activity.rectTransform,128,214,62,34);activity.alignment=TextAlignmentOptions.MidlineLeft;activity.textWrappingMode=TextWrappingModes.NoWrap;
                string id=person.personId;var open=FriendUi.Button("Open_"+id,r,0,0,178,54,"打开",()=>OpenPerson(id));RightMiddle((RectTransform)open.transform,18);
            }
            if(shown==0)FriendUi.Paragraph(content,string.IsNullOrEmpty(filter)?"还没有朋友。点“添加朋友”来添加。":"没有找到这个名字。");
        }
        void RenderDetail()
        {
            var runtime=FriendsLifeRuntime.Instance;var person=runtime?.GetPerson(personId);if(person==null){page="List";RenderList();return;}
            if(Array.IndexOf(DetailTabs,tab)<0)tab=DetailTabs[0];
            var r=runtime.Core.FindResident(person.nativeResidentId);var p=Page("T02_FriendDetail","朋友",true);
            // Left column 300 wide: portrait, name, home. Tabs and the tab body sit right of it.
            FriendUi.Flexible(FriendUi.Rect("PortraitColumn",p.toolbar,0,0,300,10),308,0);
            foreach(string t in DetailTabs){string value=t;var b=FriendUi.Button("Tab_"+t,p.toolbar,0,0,170,FriendUi.ToolbarH,t,()=>{tab=value;Render();});FriendUi.FitCaption(b);FriendUi.Flexible(b,170,1);b.SetSelected(tab==t);}
            var left=FriendUi.Band("ProfileColumn",p.body,Vector2.zero,new Vector2(0,1),Vector2.zero,new Vector2(300,0));
            var portrait=FriendUi.Portrait("Portrait",left,0,0,300,300);FriendPortraitCache.Bind(portrait,personId);
            var name=FriendUi.Text("Name",left,0,312,300,44,person.displayName,30);name.textWrappingMode=TextWrappingModes.NoWrap;name.enableAutoSizing=true;name.fontSizeMin=22;name.fontSizeMax=30;
            FriendUi.Button("HomeButton",left,0,372,300,56,"查看住宅",VisitHome);
            var right=FriendUi.Band("TabBody",p.body,Vector2.zero,Vector2.one,new Vector2(324,0),Vector2.zero);
            // Tab rows are authored 750 wide (M3's relation rows use the same width).
            scroll=FriendUi.FitScroll("Content",right,750,out var c);float y=0;bool handled=false;
            if(tab=="资料"){RenderProfileTab(c,ref y,ref handled);if(!handled)RenderProfileTabDefault(person,r,c,ref y);}
            else if(tab=="关系"){RenderRelationsTab(c,ref y,ref handled);if(!handled)RenderRelationsTab(c,ref y,person);}
            else if(tab=="烦恼·社交"){RenderProblemsTab(c,ref y,ref handled);if(!handled)RenderSourceSocialPage("social",c,ref y);}
            else{RenderWorkTab(c,ref y,ref handled);if(!handled)RenderWorkTabDefault(c,ref y);}
            FriendUi.FitContent(c,y);BackButton();
        }
        void RenderProfileTabDefault(PersonBinding person,LifeSimulationResident r,Transform c,ref float y)
        {
            Field(c,ref y,"现在",StatusLine(personId));
            Field(c,ref y,"等级",r==null?"":"Lv "+r.displayedLevel);Field(c,ref y,"心情",r?.emotion);Field(c,ref y,"口头禅",r?.catchphrase);Field(c,ref y,"爱好",r?.hobby);
            float half=(ContentWidth(c)-20)/2;
            FriendUi.Button("EditProfile",c,0,y,half,56,"修改资料",()=>{Push();draft=null;page="Profile";Render();});
            FriendUi.Button("EditAppearance",c,half+20,y,half,56,"修改外貌",()=>EditAppearance(person));y+=76;
            RenderGatedMenus(c,ref y);
        }
        void RenderWorkTabDefault(Transform c,ref float y)
        {
            if(!RenderPublicJobs(c,ref y))
            {
                var economy=FriendsEconomyService.Instance;Field(c,ref y,"余额",economy?(economy.BalanceCents(personId)/100m).ToString("0.00")+" 金币":"");
                Field(c,ref y,"工作",economy?economy.JobTitle(personId):"暂无工作");
            }
            if(FriendsLifeFeatureFlags.Casino||HasCasinoJourney(personId)){FriendUi.Button("OriginalCasino",c,0,y,ContentWidth(c),56,FriendsLifeFeatureFlags.Casino?"去赌场":"赌场进度",()=>OpenCasino(personId));y+=72;}
        }
        // Entries of features gated off in FriendsLifeFeatureFlags. Nothing shows while all flags are false.
        void RenderGatedMenus(Transform c,ref float y)
        {
            if(FriendsLifeFeatureFlags.DebugActivityPage){FriendUi.Button("OriginalActivity",c,0,y,ContentWidth(c),56,"当前活动",()=>OpenOriginalActivity(personId));y+=72;}
            if(ActivitiesMenu().Length>0)Menu(c,ref y,"活动","activities");
            if(FriendsLifeFeatureFlags.Creative){Menu(c,ref y,"物品","items");Menu(c,ref y,"微个性","personal");}
            if(FriendsLifeFeatureFlags.Minigames)Menu(c,ref y,"小游戏","games");
            if(FriendsLifeFeatureFlags.Photography)Menu(c,ref y,"相册","memories");
            if(FriendsLifeFeatureFlags.Cohabit&&HasFamilyHomeSelection())Menu(c,ref y,"同住计划","family-housing");
            var economy=FriendsEconomyService.Instance;
            if(FriendsLifeFeatureFlags.HousingUpgrade&&economy&&economy.SplitHomeMembers(personId).Length>0){FriendUi.Button("SeparateHome",c,0,y,ContentWidth(c),56,"分住",()=>{if(!FriendHousingUI.BeginSplit(personId))Status("住宅暂不可调整，请稍后再试。");});y+=72;}
        }
        void Field(Transform p,ref float y,string label,string value)
        {
            float w=Mathf.Max(200,ContentWidth(p)-230);
            var caption=FriendUi.Text("Field_"+label,p,0,y,220,56,label,24);var text=FriendUi.Text("Value_"+label,p,230,y,w,56,value,24);
            float height=Mathf.Max(56,Mathf.Ceil(text.GetPreferredValues(value??"",w,Mathf.Infinity).y)+8);
            text.rectTransform.sizeDelta=new Vector2(w,height);caption.rectTransform.sizeDelta=new Vector2(220,height);y+=height+14;
        }
        void Menu(Transform p,ref float y,string label,string source){FriendUi.Button("Source_"+source,p,0,y,ContentWidth(p),56,label,()=>OpenSourcePage(personId,source));y+=72;}
        void RenderPair()
        {
            var runtime=FriendsLifeRuntime.Instance;var a=runtime?.GetPerson(personId);var b=runtime?.GetPerson(pairId);if(a==null||b==null){page="Detail";RenderDetail();return;}
            var p=Page("T03_RelationPair",a.displayName+" · "+b.displayName);var body=p.body;
            var relations=runtime.GetRelationships(personId);var forward=relations.FirstOrDefault(x=>x.fromId==a.nativeResidentId&&x.toId==b.nativeResidentId);var reverse=relations.FirstOrDefault(x=>x.fromId==b.nativeResidentId&&x.toId==a.nativeResidentId);
            PairColumn(body,"A",a,0);PairColumn(body,"B",b,1088-240);
            float y=20;FriendUi.Flow(body,ref y,"Forward",RelationPairText(a,b,forward),24,560,24,264);FriendUi.Flow(body,ref y,"Reverse",RelationPairText(b,a,reverse),24,560,24,264);
            BackButton();
        }
        void PairColumn(Transform body,string slot,PersonBinding person,float x)
        {
            var portrait=FriendUi.Portrait("Person"+slot+"Portrait",body,x,0,240,240);FriendPortraitCache.Bind(portrait,person.personId);
            var name=FriendUi.Text("Person"+slot,body,x,252,240,44,person.displayName,28);name.textWrappingMode=TextWrappingModes.NoWrap;
            string id=person.personId;FriendUi.Button("Open"+slot,body,x,308,240,56,"查看资料",()=>OpenPerson(id));
        }
        void RenderAdd()
        {
            var p=Page("T04_SourcePicker","添加朋友");var body=p.body;float x=(1088-680)/2f;
            FriendUi.Button("Photo",body,x,40,680,70,"照片制作",()=>{draft=photos.Create();page="Photo";Render();},true);
            FriendUi.Button("Manual",body,x,150,680,70,"手动制作",()=>{draft=photos.Create();BeginCreator(null);});
            FriendUi.Button("Question",body,x,260,680,70,"问答制作",StartQuestionnaire);BackButton();
        }
        void RenderDrafts()
        {
            var p=Page("T01_FriendList","草稿");scroll=FriendUi.List(p.body,out var c,12);int shown=0;
            foreach(var item in photos.Drafts)
            {
                var id=item.draftId;var row=FriendUi.ListRow(c,72);row.name="Draft_"+id;shown++;
                var name=FriendUi.Text("Name_"+id,row,0,0,0,0,item.displayName+"  "+PhotoFriendPipeline.Label(item),24);TopStretch(name.rectTransform,10,360,0,72);name.alignment=TextAlignmentOptions.MidlineLeft;name.textWrappingMode=TextWrappingModes.NoWrap;
                var resume=FriendUi.Button("Continue_"+id,row,0,0,160,54,"继续",()=>{draft=photos.Find(id);
                    page=PhotoFriendPipeline.IsNative(draft)?(draft.stage==PhotoFriendStage.Profile||draft.stage==PhotoFriendStage.AwaitingHome?"Profile":"Photo")
                        :(string.IsNullOrEmpty(draft.sourcePath)?"Profile":"Photo");Render();});RightMiddle((RectTransform)resume.transform,180);
                var remove=FriendUi.Button("Remove_"+id,row,0,0,160,54,"移除",()=>{photos.RemoveDraft(id);Render();});RightMiddle((RectTransform)remove.transform,0);
            }
            if(shown==0)FriendUi.Paragraph(c,"没有草稿。");
            BackButton();
        }
        void RenderProfile()
        {
            if(draft!=null&&!PhotoFriendPipeline.IsNative(draft))draft=photos.CreateNativeFromLegacy(draft);
            RenderProfileFields();
        }
        void SaveDraftFields(){if(draft==null)return;draft.displayName=nameField?nameField.text:draft.displayName;draft.catchphrase=phraseField?phraseField.text:draft.catchphrase;draft.hobby=hobbyField?hobbyField.text:draft.hobby;photos.Save(draft);}
        async void SaveProfile()
        {
            var savingDraft=draft;string savingPersonId=personId;
            var runtime=FriendsLifeRuntime.Instance;if(runtime==null||!runtime.Ready)return;string name=nameField.text.Trim();if(name.Length==0){Status("请输入姓名");return;}
            if(suppressRefresh)return;suppressRefresh=true;
            try
            {
            CaptureNativeProfile();
            if(!runtime.IsAuthority){await SaveRemoteProfile();return;}
            if(draft==null){if(runtime.UpdatePersonProfile(personId,name,editNativeJson,phraseField.text,hobbyField.text)){ApplyFamilyFields(personId);page="Detail";Render();}else Fail(runtime.LastError,"资料保存失败，请重试。");return;}
            SaveDraftFields();if(draft.characterData==null){Status("请先完成外貌");return;}
            // Native profile JSON and BoZo CharacterData remain separate documents.
            if(string.IsNullOrEmpty(draft.createCommandId)){draft.createCommandId=Guid.NewGuid().ToString("N");photos.Save(draft);}
            var person=string.IsNullOrEmpty(draft.personId)?null:runtime.GetPerson(draft.personId);person=person??runtime.CreatePerson(name,draft.nativeAvatarJson??"",PersonControllerKind.Npc,draft.createCommandId);if(person==null){Fail(runtime.LastError,"添加朋友失败，请重试。");return;}
            personId=person.personId;draft.personId=personId;photos.Save(draft);
            if(!runtime.UpdatePersonProfile(personId,name,draft.nativeAvatarJson??"",draft.catchphrase,draft.hobby)){Fail(runtime.LastError,"资料保存失败，请重试。");return;}
            string previousAppearance=person.appearanceJson;if(person.appearanceJson!=JsonUtility.ToJson(draft.characterData)&&!runtime.SetAppearance(personId,JsonUtility.ToJson(draft.characterData))){Fail(runtime.LastError,"外貌保存失败，请重试。");return;}NativePhotoSourceStore.Commit(draft,personId,draft.characterData,previousAppearance,person.appearanceRevision);if(draft.editingExisting){page="Detail";photos.RemoveDraft(draft.draftId);draft=null;Render();return;}ApplyFamilyFields(personId);draft.stage=PhotoFriendStage.AwaitingHome;photos.Save(draft);
            Navigation.PushVisit(new UiReturnFrame{page="Detail",personId=personId,tab="资料",purpose="Arrival"});FriendTabletAccess.OpenMap(tablet);FriendHousingUI.BeginArrival(personId);
            }
            catch(Exception error){Debug.LogWarning("[FriendsLife] Profile save: "+error);if(ReferenceEquals(draft,savingDraft)&&(savingDraft!=null||personId==savingPersonId))Status("资料保存失败，请重试。");}
            finally{suppressRefresh=false;}
        }
        void EditAppearance(PersonBinding person)
        {
            draft=photos.Create(person.personId,person.appearanceRevision);draft.displayName=person.displayName;
            try{draft.characterData=string.IsNullOrEmpty(person.appearanceJson)?null:JsonUtility.FromJson<CharacterData>(person.appearanceJson);}catch(ArgumentException){}
            if(FriendAppearancePort.HasGenerated(draft.characterData))
            {
                if(photos.AttachRetainedSource(draft,draft.characterData)){draft.characterData=NativePhotoParameterFit.NativeBase(draft.characterData);page="Photo";Render();return;}
                draft.characterData=NativePhotoParameterFit.NativeBase(draft.characterData);
            }
            else photos.AttachRetainedSource(draft,draft.characterData);
            BeginCreator(draft.characterData);
        }
        async void BeginCreator(CharacterData data)
        {
            var workingDraft=draft;if(workingDraft==null){Status("照片草稿不存在");return;}string targetPersonId=workingDraft.personId;
            try{if(PhotoFriendPipeline.IsNative(workingDraft))data=NativePhotoParameterFit.NativeBase(data);
                await FriendCharacterCreatorSession.Open(data,updated=>ApplyAppearance(workingDraft,targetPersonId,updated),()=>{if(ReferenceEquals(draft,workingDraft)){FriendTabletAccess.ShowFriends(tablet);Render();}});}
            catch(Exception ex){if(ReferenceEquals(draft,workingDraft))Status(PhotoFriendPipeline.Friendly(ex,"外貌编辑无法打开，请重试。"));else Debug.LogWarning("[FriendsLife] Submitted appearance: "+ex);}
        }
        // Shared by the creator's confirm and the photo page's 使用外貌 button.
        async void ApplyAppearance(PhotoFriendDraft workingDraft,string targetPersonId,CharacterData updated)
        {
            try{
            if(PhotoFriendPipeline.IsNative(workingDraft))NativePhotoParameterFit.Validate(updated);workingDraft.characterData=updated;workingDraft.appearanceRevision++;photos.Save(workingDraft);
            if(workingDraft.editingExisting&&!string.IsNullOrEmpty(targetPersonId))
            {if(!await CommitAppearance(targetPersonId,updated,workingDraft))throw new InvalidOperationException(FriendsLifeRuntime.Instance.LastError);photos.RemoveDraft(workingDraft.draftId);if(!ReferenceEquals(draft,workingDraft))return;personId=targetPersonId;page="Detail";draft=null;}
            else{workingDraft.stage=PhotoFriendStage.Profile;photos.Save(workingDraft);if(!ReferenceEquals(draft,workingDraft))return;page="Profile";}FriendTabletAccess.ShowFriends(tablet);Render();
            }catch(Exception error){if(ReferenceEquals(draft,workingDraft)){FriendTabletAccess.ShowFriends(tablet);Render();Status(PhotoFriendPipeline.Friendly(error,"外貌保存失败，请重试。"));}else Debug.LogWarning("[FriendsLife] Submitted appearance: "+error);}
        }
        void RenderSource()
        {
            string category=page.StartsWith("Source:")?page.Substring(7):"activities";var runtime=FriendsLifeRuntime.Instance;var person=runtime?.GetPerson(personId);
            // A page of a hidden feature (or one restored from an old frame) falls back to the friend.
            if(!SourceCategoryEnabled(category)){if(person!=null){page="Detail";RenderDetail();}else{page="List";RenderList();}return;}
            var p=Page("T04_SourcePicker",SourceTitle(category));
            scroll=FriendUi.FitScroll("ScrollRows",p.body,1120,out var c);float y=0;
            if(category=="jobs"&&RenderPublicJobs(c,ref y)){FriendUi.FitContent(c,y);BackButton();return;}
            if(RenderSourceContent(category,p.paper,c,ref y)){FriendUi.FitContent(c,y);BackButton();return;}
            if(person!=null&&category=="games")foreach(var item in LifeActivitiesCatalog.Minigames.Where(g=>g.id!="Dummy"))
            {
                string id=item.id;int need=Math.Max(item.minimumResidents,item.requiredPopulation);
                int population=runtime.Core.State.residents.Count(r=>!runtime.IsAwaitingArrival(r.id));
                string title=preparingGamePortraitsId==id?"准备中…":item.title+(population<need?"（需要"+need+"位居民）":"");
                var start=SourceRow(c,ref y,title,()=>StartSourceMinigame(id));start.interactable=!preparingGamePortraits&&population>=need;
            }
            else if(person!=null&&category=="travel")foreach(var item in LifeActivitiesCatalog.Travels){string id=item.id;SourceRow(c,ref y,item.title,()=>Command("travel:"+id));}
            else if(person!=null&&category=="items")
            {
                foreach(var item in person.inventory){FriendUi.Text("OwnedItem_"+item.itemName,c,0,y,1120,70,NativeLifeLocalization.Text(item.itemName)+" ×"+item.quantity,24);y+=86;}
                if(person.inventory.Count==0)FriendUi.Flow(c,ref y,"NoItems","还没有物品。");
            }
            else if(person!=null&&category=="dream"){SourceRow(c,ref y,"梦",()=>Command("dream"));SourceRow(c,ref y,"醒来",()=>Command("wake"));}
            else if(person!=null&&category=="jobs")
            {foreach(PaperPixelGames.Core.Staff.StaffRole role in Enum.GetValues(typeof(PaperPixelGames.Core.Staff.StaffRole))){var copy=role;string label=role==PaperPixelGames.Core.Staff.StaffRole.SushiChef?"寿司师傅":role==PaperPixelGames.Core.Staff.StaffRole.PrepCook?"备料员":role==PaperPixelGames.Core.Staff.StaffRole.Waitress?"服务员":"补货员";SourceRow(c,ref y,label,()=>{var service=FriendsEconomyService.Instance;bool accepted=runtime.IsAuthority?service.InviteToWork(personId,copy):service.RequestHousing("invite-work",personId,new HousingPlanCommand{typeId=copy.ToString()});if(!accepted)Fail(service.LastFailure,"目前无法安排这份工作。");});}}
            else if(person!=null)
            {
                var human=runtime.LocalHuman();foreach(var episode in runtime.GetEpisodes(personId).Where(e=>human!=null&&(e.focusPersonId==human.personId||e.targetPersonId==human.personId||e.anotherPersonId==human.personId)))
                {
                    if(episode.familyChoice!=null)foreach(var option in episode.familyChoice.options){string action="family:"+episode.familyChoice.id;string id=option.id;
                        // R labels are functions, never used as NPC spoken lines.
                        var button=SourceRow(c,ref y,SourceMenuLabel(option.id),()=>Command(action,id));button.interactable=option.enabled;}
                }
            }
            if(y<=0)FriendUi.Flow(c,ref y,"Empty","现在没有可做的事。");
            FriendUi.FitContent(c,y);BackButton();
        }
        static string SourceTitle(string category)
        {
            switch(category){case "games":return "小游戏";case "travel":return "旅行";case "items":return "物品";case "personal":return "微个性";case "jobs":return "工作";case "social":return "社交消息";
                case "memories":return "相册";case "photography":return "拍照";case "family-housing":return "同住计划";default:return "活动";}
        }
        BaseButton SourceRow(Transform p,ref float y,string caption,Action action)
        {
            float w=ContentWidth(p);var b=FriendUi.Button("SourceRow_"+y,p,0,y,w,70,caption,action);var text=b.GetComponentInChildren<TMP_Text>();text.enableAutoSizing=false;
            float height=Mathf.Max(70,Mathf.Ceil(text.GetPreferredValues(caption??"",w-24,Mathf.Infinity).y)+16);
            ((RectTransform)b.transform).sizeDelta=new Vector2(w,height);text.rectTransform.sizeDelta=new Vector2(w-24,height-4);
            text.enableAutoSizing=false;text.overflowMode=TextOverflowModes.Ellipsis;y+=height+16;return b;
        }
        public static string SourceMenuLabel(string id)
        {
            switch(id){case "chat":case "talk":return "交谈";case "promise":return "约定";case "wait":case "later":return "稍后";case "apologize":return "道歉";case "confess":return "表白";case "friends":return "朋友";case "picnic":return "野餐";case "walk":return "散步";case "propose":return "求婚";case "simple":return "婚礼";case "party":return "庆祝";case "separate":return "分开";case "restart":return "重新开始";case "pregnancy":return "生育";case "adopt":return "领养";case "welcome":return "迎接";case "feed":return "喂食";case "play":return "玩耍";case "lullaby":return "摇篮曲";case "grow":return "成长";case "listen":return "倾听";case "suggest":return "同住";default:return "选择";}
        }
        void Command(string action,string option=null)
        {
            var runtime=FriendsLifeRuntime.Instance;
            if(runtime&&!runtime.IsAuthority){SendTrackedAction(action,option);return;}
            var result=runtime?.SubmitSourceAction(personId,action,option);if(!string.IsNullOrEmpty(result)){if(action.StartsWith("minigame",StringComparison.Ordinal))ShowInlineFeedback(result);else Status(result);}
        }
        // Failure with a curated fallback: raw or empty messages never reach the player.
        void Fail(string message,string fallback)=>Status(FriendUi.PlayerText(message,fallback));
        void Status(string value)
        {
            if(string.IsNullOrEmpty(value))return;value=FriendUi.PlayerText(value,"没有完成，请稍后再试。");var old=view.Find("Status");if(old){old.gameObject.SetActive(false);Destroy(old.gameObject);}
            var shield=FriendUi.Rect("Status",view,0,0,1200,720);var block=shield.gameObject.AddComponent<Image>();block.color=new Color(0,0,0,.001f);block.raycastTarget=true;
            shield.gameObject.AddComponent<FriendTabletStatusScope>().Bind(current?.safe);
            var p=FriendUi.Rect("Dialog",shield,260,200,680,320);var skin=FriendUiSkin.Load();FriendUi.Image("Paper",p,0,0,680,320,skin?skin.paper:null);
            FriendUi.Text("Title",p,40,30,600,44,"提示",30);var body=FriendUi.Text("Body",p,40,84,600,128,value,24);body.enableAutoSizing=true;body.fontSizeMin=18;body.fontSizeMax=24;
            FriendUi.Button("Back",p,210,226,260,54,"返回",()=>Destroy(shield.gameObject));
        }
        TMP_Text photoProgress;
        string PhotoProgressText()=>PhotoFriendPipeline.Label(draft)+(string.IsNullOrEmpty(draft.failure)?"":"\n"+FriendUi.PlayerText(draft.failure,"没有完成，请重试。"));
        // Shows a photo inside its box with the photo's own aspect ratio (no stretching).
        static RawImage PhotoImage(string name,Transform p,float x,float y,float w,float h,Texture texture)
        {
            float fw=w,fh=h;if(texture&&texture.width>0&&texture.height>0){float aspect=(float)texture.width/texture.height;if(aspect>w/h)fh=w/aspect;else fw=h*aspect;}
            var raw=FriendUi.Rect(name,p,x+(w-fw)*.5f,y+(h-fh)*.5f,fw,fh).gameObject.AddComponent<RawImage>();FriendUi.UseTabletGraphic(raw);raw.texture=texture;return raw;
        }
        void RenderPhoto(bool rebuild)
        {
            // The 0.5s refresh only updates the cached status text; no hierarchy search.
            if(draft==null){page="Drafts";Render();return;}if(!rebuild){if(photoProgress)photoProgress.text=PhotoProgressText();return;}
            renderedPhotoStage=draft.stage;renderedNativeMaskPending=draft.nativeMaskPending;var p=Page("T12_PhotoFlow","照片制作");var body=p.body;
            // Pictures fill the body (1088 x 506); actions go in the footer, 返回 in the header.
            FriendUi.HeaderButton(p,"返回朋友",()=>{page="Drafts";Render();});
            var source=PhotoImage("Source",body,0,0,340,440,photos.Source(draft));source.raycastTarget=false;
            var cutout=PhotoImage("Cutout",body,360,0,340,440,photos.Cutout(draft));var brush=cutout.gameObject.AddComponent<PhotoMaskBrush>();brush.Bind(photos,draft,cutout);
            photoProgress=FriendUi.Text("Progress",body,720,0,368,440,PhotoProgressText(),24);photoProgress.alignment=TextAlignmentOptions.TopLeft;photoProgress.overflowMode=TextOverflowModes.Ellipsis;
            FriendUi.FooterButton(p,"选择图片",()=>{photos.Select(draft);Render();},190);
            if(!PhotoFriendPipeline.IsNative(draft)){FriendUi.FooterButton(p,"按照片匹配外貌",()=>{draft=photos.CreateNativeFromLegacy(draft);Render();},420,true);return;}
            if(draft.stage==PhotoFriendStage.Image)FriendUi.FooterButton(p,"手动制作",()=>BeginCreator(draft.characterData),240);
            else if(draft.stage==PhotoFriendStage.Mask)
            {
                FriendUi.FooterButton(p,"擦除",()=>brush.restore=false,140);FriendUi.FooterButton(p,"恢复",()=>brush.restore=true,140);
                FriendUi.FooterButton(p,"撤销",()=>{photos.UndoMask(draft);cutout.texture=photos.Cutout(draft);},140);
                FriendUi.FooterButton(p,"重做",()=>{photos.RedoMask(draft);cutout.texture=photos.Cutout(draft);},140);
                FriendUi.FooterButton(p,"确定",()=>{photos.ConfirmMask(draft);Render();},200,true).interactable=!draft.nativeMaskPending;
            }
            else if(draft.stage==PhotoFriendStage.Ready||draft.stage==PhotoFriendStage.Failed||draft.stage==PhotoFriendStage.Cancelled)
            {FriendUi.FooterButton(p,draft.stage==PhotoFriendStage.Ready?"匹配外貌":"重新匹配",()=>{photos.Generate(draft);Render();},240,true);
                FriendUi.FooterButton(p,"修改轮廓",()=>{photos.EditMask(draft);Render();},240);FriendUi.FooterButton(p,"手动制作",()=>BeginCreator(draft.characterData),240);}
            else if(draft.stage==PhotoFriendStage.Generating)FriendUi.FooterButton(p,"取消",()=>{photos.Cancel(draft);Render();},240);
            else if(draft.stage==PhotoFriendStage.Editor)
            {
                // Portrait renders are square; the status text is hidden so the preview owns the right column.
                var preview=FriendUi.Rect("NativePreview",body,720,40,360,360).gameObject.AddComponent<RawImage>();FriendUi.UseTabletGraphic(preview);preview.raycastTarget=false;FriendPortraitCache.Bind(preview,draft);photoProgress.gameObject.SetActive(false);
                BaseButton use=null;use=FriendUi.FooterButton(p,"使用外貌",()=>{if(draft?.characterData==null)return;use.interactable=false;ApplyAppearance(draft,draft.personId,NativePhotoParameterFit.Copy(draft.characterData));},200,true);
                FriendUi.FooterButton(p,"调整外貌",()=>BeginCreator(draft.characterData),200);
                FriendUi.FooterButton(p,"重新匹配",()=>{photos.Generate(draft);Render();},200);
                FriendUi.FooterButton(p,"修改轮廓",()=>{photos.EditMask(draft);Render();},200);
            }
            else if(draft.stage==PhotoFriendStage.Profile)
            {FriendUi.FooterButton(p,"填写资料",()=>{page="Profile";Render();},240,true);FriendUi.FooterButton(p,"调整外貌",()=>BeginCreator(draft.characterData),240);}
        }
        public void OpenPhotoDraft(string id)
        {var selected=photos.Find(id);if(selected==null)throw new InvalidOperationException("照片草稿不存在");draft=selected;page="Photo";FriendTabletAccess.ShowFriends(tablet);Render();}
        public static void ShowWorldInteraction(string id)
        {
            FriendDialoguePresenter.ShowWorldInteraction(id);
        }
    }
    // Overlay-font page content would otherwise draw through the modal paper.
    // Keep the page paper and scope only its Safe content to the modal lifetime.
    public sealed class FriendTabletStatusScope:MonoBehaviour
    {
        Transform page;CanvasGroup group;bool ownsGroup,captured,wasEnabled,wasInteractable,wasBlocking;
        float wasAlpha;
        public void Bind(Transform root)
        {
            if(page==root){if(isActiveAndEnabled)Suppress();return;}
            Restore();if(ownsGroup&&group)Destroy(group);
            page=root;group=null;ownsGroup=false;if(isActiveAndEnabled)Suppress();
        }
        void OnEnable(){Suppress();}
        void Suppress()
        {
            if(captured||!page)return;
            if(!group){group=page.GetComponent<CanvasGroup>();if(!group){group=page.gameObject.AddComponent<CanvasGroup>();ownsGroup=true;}}
            wasEnabled=group.enabled;wasAlpha=group.alpha;wasInteractable=group.interactable;wasBlocking=group.blocksRaycasts;captured=true;
            group.enabled=true;group.alpha=0;group.interactable=false;group.blocksRaycasts=false;
        }
        void Restore()
        {
            if(!captured)return;
            if(group){group.alpha=wasAlpha;group.interactable=wasInteractable;group.blocksRaycasts=wasBlocking;group.enabled=wasEnabled;}
            captured=false;
        }
        void OnDisable(){Restore();}
        void OnDestroy(){Restore();if(ownsGroup&&group)Destroy(group);}
    }
}
