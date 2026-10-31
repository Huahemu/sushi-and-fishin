using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using SushiAndFish.ResidentialIsland;
using PaperPixelGames.Core.Tablet;
namespace SushiAndFish.FriendsLife
{
    // New visit/arrival UI layered on the original map; original callbacks remain installed.
    public static partial class FriendHousingUI
    {
        public static Action ReturnFromHome;
        public static Action<string> ResidentProfileRequested;
        public static bool HasHomeVisit{get;private set;}
        public static string VisitPersonId{get;private set;}
        static HousingInfoWindow visitWindow;
        static RectTransform visitReturn;static HousingMap visitMap;
        public static HousingInfoWindow NavigationWindow=>CurrentMap?.Window;
        static HousingMap CurrentMap=>FriendSceneObjects.Maps().FirstOrDefault(m=>m.IsVisible&&m.Overlay);
        public static bool OwnsNavigation=>HasHomeVisit||HasSplitTask||FriendHousingIsland.Instance?.HasArrivalTask==true||
            (NavigationWindow&&NavigationWindow.Building?.house&&NavigationWindow.Building.house.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone==true);
        public static void Tick()
        {
            TickSplit();if(visitReturn)visitReturn.gameObject.SetActive(HasHomeVisit&&visitMap&&visitMap.IsVisible);
        }
        public static bool TryNavigationBack()
        {
            var map=CurrentMap;if(!map||!OwnsNavigation)return false;
            var controls=map.Window?map.Window.GetComponent<FriendHousingManagementControls>():null;
            if(controls&&controls.TryBackConfirmation())return true;
            if(map.PreviewPlot!=null){map.CancelPreview();return true;}
            if(HasSplitTask){CancelSplit();return true;}
            if(HasHomeVisit){EndHomeVisit();return true;}
            if(map.Window&&map.Window.Panel){map.Window.Close();return true;}return false;
        }
        public static bool OpenHomeFromFriend(string personId,TabletController owner=null)
        {
            var person=FriendsLifeRuntime.Instance?.GetPerson(personId);var home=FriendHousingIsland.Instance?.Plots.FirstOrDefault(p=>p.id==person?.currentHomeId&&p.house);if(home==null)return false;
            if(!owner)owner=FriendSceneObjects.Tablets().FirstOrDefault(t=>t.IsInitialized&&FriendTabletAccess.IsMapActive(t));
            if(!owner)return false;var map=FriendMapVisitView.PrepareSurface(owner);var info=FriendMapBuildingInfo.ForPlot(home);if(info==null)return false;
            FriendMapVisitView.Focus(owner,home.house.transform.position);FriendMapBuildingInfo.Open(map,info,personId);BeginHomeVisitUI(map,personId);return true;
        }
        static void BeginHomeVisitUI(HousingMap map,string personId)
        {
            VisitPersonId=personId;HasHomeVisit=true;visitWindow=map.Window;
            visitMap=map;if(visitReturn)UnityEngine.Object.Destroy(visitReturn.gameObject);
            // 返回 lives on the map's fixed HUD (not the zoomed map content), top right,
            // above the centred info card. Scale matches the Friends pages.
            var mapScroll=map.Overlay.GetComponentInParent<UnityEngine.UI.ScrollRect>();var hud=mapScroll&&mapScroll.viewport?mapScroll.viewport.Find("HousingMapHUD") as RectTransform:null;
            var button=FriendUi.Button("T11_ReturnFromHome",hud?(Transform)hud:map.Overlay,24,18,208,54,"返回",()=>EndHomeVisit());visitReturn=(RectTransform)button.transform;
            if(hud){visitReturn.anchorMin=visitReturn.anchorMax=visitReturn.pivot=Vector2.one;visitReturn.localScale=Vector3.one*(hud.rect.height>0?hud.rect.height/720f*.85f:.19f);visitReturn.anchoredPosition=new Vector2(-6,-3);}
            foreach(var owner in FriendSceneObjects.TabletAccess())owner.SyncInput();
        }
        public static void RestoreMapFrame(UiReturnFrame frame,TabletController owner,bool hasBack)
        {
            EndHomeVisit(false);if(frame?.mapView==null)return;var map=FriendMapVisitView.Restore(owner,frame.mapView);
            if(map&&frame.mapView.homeVisit&&hasBack)BeginHomeVisitUI(map,frame.mapView.visitorId);
            if(owner&&owner.MapApplication)TabletNavigationStack.RestoreFocus(frame,owner.MapApplication.transform);
        }
        public static bool BeginArrival(string personId)
        {
            if(HasSplitTask)return false;
            var economy=FriendsEconomyService.Instance;var runtime=FriendsLifeRuntime.Instance;if(!economy||!runtime)return false;
            bool ok=runtime.IsAuthority?economy.BeginArrival(personId):economy.RequestHousing("begin-arrival",personId);
            if(!ok)return false;FriendHousingIsland.Instance?.BeginArrival(personId);
            foreach(var tablet in FriendSceneObjects.Tablets().Where(t=>t.IsInitialized))FriendTabletAccess.OpenMap(tablet);
            return true;
        }
        public static void EndHomeVisit(bool returnToFriend=true)
        {if(!HasHomeVisit)return;HasHomeVisit=false;VisitPersonId=null;if(visitReturn)UnityEngine.Object.Destroy(visitReturn.gameObject);visitReturn=null;visitMap=null;if(returnToFriend)ReturnFromHome?.Invoke();}
    }
    [DefaultExecutionOrder(31000)]
    public sealed class FriendArrivalMapOverlay:MonoBehaviour
    {
        HousingMap map;RectTransform root,splitBar;TMPro.TextMeshProUGUI splitText;UnityEngine.UI.Button splitConfirm,splitCancel,splitReset;string signature;readonly List<(HousingParcelButton source,HousingParcelButton clone)> clones=new List<(HousingParcelButton,HousingParcelButton)>();
        void Awake(){map=GetComponent<HousingMap>();}
        void LateUpdate()
        {
            var housing=FriendHousingIsland.Instance;var runtime=FriendsLifeRuntime.Instance;bool enabledNow=housing&&runtime&&runtime.Ready&&map&&map.IsVisible&&map.Overlay;
            if(!enabledNow){if(root)root.gameObject.SetActive(false);return;}
            if(!root){root=HousingUI.Rect("IncrementalArrivalParcels",map.Overlay,Vector2.zero);root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;}
            root.gameObject.SetActive(true);root.SetAsLastSibling();
            if(!housing.HasArrivalTask&&!FriendHousingUI.HasSplitTask&&map.PreviewPlot!=null)map.CancelPreview();
            var originals=map.Overlay.GetComponentsInChildren<HousingParcelButton>(true).Where(b=>!b.transform.IsChildOf(root)&&b.name.StartsWith("Parcel_P",StringComparison.Ordinal)).ToArray();
            string current=string.Join("/",originals.Select(x=>x.GetInstanceID()));if(current!=signature)
            {
                signature=current;foreach(var pair in clones)if(pair.clone)Destroy(pair.clone.gameObject);clones.Clear();
                foreach(var source in originals){var clone=Instantiate(source,root,false);clone.name="Incremental_"+source.name;string id=source.name.Substring("Parcel_".Length);clone.click=()=>Choose(id);clone.hover=null;clones.Add((source,clone));}
            }
            foreach(var pair in clones)if(pair.source&&pair.clone){var a=(RectTransform)pair.source.transform;var b=(RectTransform)pair.clone.transform;b.anchorMin=a.anchorMin;b.anchorMax=a.anchorMax;b.pivot=a.pivot;b.anchoredPosition=a.anchoredPosition;b.sizeDelta=a.sizeDelta;b.localRotation=a.localRotation;b.localScale=a.localScale;}
            UpdateSplitBar();
            if(map.Window&&map.Window.Building?.house&&map.Window.Building.house.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone==true&&!map.Window.GetComponent<FriendHousingManagementControls>())map.Window.gameObject.AddComponent<FriendHousingManagementControls>();
            if(map.Window&&map.Window.Panel)map.Window.Panel.SetAsLastSibling();
            foreach(var owner in FriendSceneObjects.TabletAccess())owner.SyncInput();
        }
        void UpdateSplitBar()
        {
            if(!FriendHousingUI.HasSplitTask){if(splitBar)splitBar.gameObject.SetActive(false);return;}
            if(!splitBar)
            {
                float w=map.Overlay.rect.width*.88f,h=Mathf.Max(90,map.Overlay.rect.height*.22f);
                var catalog=FriendHousingIsland.Instance.Catalog;
                splitBar=HousingUI.Image("IncrementalSplitSelection",root,new Vector2(w,h),catalog.panelColor,catalog.panelSprite).rectTransform;
                splitBar.anchorMin=splitBar.anchorMax=new Vector2(.5f,1);splitBar.pivot=new Vector2(.5f,1);splitBar.anchoredPosition=new Vector2(0,-8);
                splitText=HousingUI.Text("Selection",splitBar,new Vector2(w*.94f,h*.5f),"",h*.13f);splitText.rectTransform.anchoredPosition=new Vector2(0,h*.16f);
                splitCancel=HousingUI.Button("Cancel",splitBar,new Vector2(w*.26f,h*.28f),"取消",FriendHousingUI.CancelSplit);((RectTransform)splitCancel.transform).anchoredPosition=new Vector2(-w*.32f,-h*.3f);
                splitReset=HousingUI.Button("Reset",splitBar,new Vector2(w*.26f,h*.28f),"重新选择",FriendHousingUI.ResetSplitChoices);((RectTransform)splitReset.transform).anchoredPosition=new Vector2(0,-h*.3f);
                splitConfirm=HousingUI.Button("Confirm",splitBar,new Vector2(w*.26f,h*.28f),"确认分住",FriendHousingUI.ConfirmSplit);((RectTransform)splitConfirm.transform).anchoredPosition=new Vector2(w*.32f,-h*.3f);
            }
            splitBar.gameObject.SetActive(true);splitBar.SetAsLastSibling();splitText.text=FriendHousingUI.SplitMessage;
            splitConfirm.interactable=FriendHousingUI.SplitSelectionComplete&&!FriendHousingUI.SplitSubmitting;
            splitCancel.interactable=splitReset.interactable=!FriendHousingUI.SplitSubmitting;
        }
        void Choose(string id)
        {
            var h=FriendHousingIsland.Instance;var p=h?.Plots.FirstOrDefault(x=>x.id==id);if(p==null)return;
            if(p.Occupied)
            {map.CancelPreview();var info=FriendMapBuildingInfo.ForPlot(p);if(info!=null)FriendMapBuildingInfo.Open(map,info);return;}
            if(FriendHousingUI.HasSplitTask)
            {
                if(FriendHousingUI.SplitSubmitting||FriendHousingUI.SplitSelectionComplete||FriendHousingUI.SplitPlotSelected(id))return;
                if(map.PreviewPlot!=p)
                {
                    map.CancelPreview();map.SelectPlot(p);var type=FriendHousingUI.NextSplitType();int attempts=h.Catalog.homes.Length+1;
                    while(type!=null&&map.PreviewType?.id!=type.id&&attempts-->0)map.CycleType(1);return;
                }
                if(FriendHousingUI.ChooseSplitPlot(p,map.PreviewType,map.PreviewYaw))map.CancelPreview();return;
            }
            // Ordinary empty-parcel clicks never create an addon house.
            if(!h.HasArrivalTask){map.CancelPreview();return;}
            if(map.PreviewPlot!=p){map.CancelPreview();map.SelectPlot(p);return;}
            var arrivalType=map.PreviewType;if(arrivalType==null||arrivalType.level!=1)return;
            var economy=FriendsEconomyService.Instance;var runtime=FriendsLifeRuntime.Instance;
            if(runtime.IsAuthority)
            {if(h.Place(p,arrivalType,map.PreviewYaw,true,h.ArrivalPersonId))map.CancelPreview();}
            else economy.RequestHousing("place",h.ArrivalPersonId,new HousingPlanCommand{plotId=p.id,typeId=arrivalType.id,yaw=map.PreviewYaw});
        }
    }
}
