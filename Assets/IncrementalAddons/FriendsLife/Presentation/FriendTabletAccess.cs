using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Components;
using TMPro;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Enums;
namespace SushiAndFish.FriendsLife
{
    // A new app host. Original app dictionary, callbacks, enum and source stay unchanged.
    [DefaultExecutionOrder(-1000)]
    public sealed class FriendTabletAccess:MonoBehaviour
    {
        TabletController tablet;FriendTabletPresenter presenter;BaseButton launcher;RectTransform launcherHost;
        readonly List<BaseButton> observed=new List<BaseButton>();
        FriendTabletInputBridge input;bool wasOpen;UiReturnFrame resume;
        float nextLauncherTry;int launcherInstallAttempts;string launcherInstallReason;
        public bool OwnsInput=>tablet&&tablet.IsTabletActive&&((presenter&&presenter.gameObject.activeInHierarchy)||(IsMapActive(tablet)&&FriendHousingUI.OwnsNavigation));
        public bool BackFriends()=>presenter&&presenter.gameObject.activeInHierarchy&&presenter.Back();
        public void ReturnToAppGrid()
        {
            if(!tablet||!tablet.IsTabletActive)return;
            ExplicitNavigation();FriendHousingUI.EndHomeVisit(false);presenter?.OpenNormalApp();Hide();
            // Show() hid the old app but its controller reference may remain.
            // Let the original close-app branch clear it and refresh hints;
            // with no old app that callback would close the whole tablet.
            try{if(OriginalActiveApp?.GetValue(tablet) is BaseApp active&&active)OriginalCloseApp.Invoke(tablet,new object[]{UnityEngine.InputSystem.InputActionPhase.Started});}
            finally{SyncInput();}
        }
        public void CloseOwnedTablet()
        {
            if(!tablet||!tablet.IsTabletActive)return;
            resume=presenter?.SuspendTablet();if(resume==null&&IsMapActive(tablet))resume=new UiReturnFrame{app="Map",page="Map"};
            FriendHousingUI.EndHomeVisit(false);Hide();tablet.CloseTablet();wasOpen=false;
        }
        static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        static readonly MethodInfo OriginalOpen=typeof(TabletController).GetMethod("OpenTablet",Fields);
        static readonly FieldInfo OriginalActiveApp=typeof(TabletController).GetField("_currentActiveApp",Fields);
        static readonly MethodInfo OriginalCloseApp=typeof(TabletController).GetMethod("OnCloseTablet",Fields);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install(){var go=new GameObject("Incremental friend tablet installer");DontDestroyOnLoad(go);go.AddComponent<FriendTabletInstaller>();}
        public static bool IsMapActive(TabletController owner)=>owner&&owner.IsTabletActive&&owner.MapApplication&&owner.MapApplication.gameObject.activeInHierarchy;
        public static void OpenMap(TabletController owner)
        {if(!owner||!owner.IsInitialized)return;OriginalOpen.Invoke(owner,null);var access=owner.GetComponent<FriendTabletAccess>();access?.ExplicitNavigation();access?.Hide();owner.SwitchMenuStatus(true,TabletAppType.MapApp);access?.SyncInput();}
        public static void ShowFriends(TabletController owner)
        {if(!owner||!owner.IsInitialized)return;OriginalOpen.Invoke(owner,null);owner.GetComponent<FriendTabletAccess>()?.Show();}
        public void Bind(TabletController owner)
        {
            tablet=owner;var sibling=owner.MapApplication;if(!sibling)return;
            presenter=FriendTabletPresenter.Install(owner,sibling);
            input=owner.gameObject.AddComponent<FriendTabletInputBridge>();input.Bind(this);wasOpen=owner.IsTabletActive;
            foreach(var field in typeof(TabletController).GetFields(Fields))
                if(field.FieldType==typeof(BaseButton)&&field.GetValue(owner) is BaseButton button){observed.Add(button);button.Clicked+=OnOriginalApp;}
            var staff=typeof(TabletController).GetField("_staffAppbutton",Fields)?.GetValue(owner) as BaseButton;
            var grid=staff&&staff.transform.parent?staff.transform.parent.parent:null;
            if(grid&&!presenter.transform.IsChildOf(grid))presenter.gameObject.AddComponent<FriendTabletGridScope>().Bind(grid);
            TryInstallLauncher();
        }
        bool TryInstallLauncher()
        {
            if(launcher&&launcherHost)return true;
            nextLauncherTry=Time.unscaledTime+.5f;launcherInstallAttempts++;
            if(!tablet||!presenter||!input){launcherInstallReason="WaitingForBoundTabletPresenterInput";return false;}
            var template=typeof(TabletController).GetField("_staffAppbutton",Fields)?.GetValue(tablet) as BaseButton;
            if(!template||!(template.transform.parent is RectTransform reference)||!reference.parent)
            {launcherInstallReason="WaitingForOriginalStaffGrid";return false;}
            if(reference.rect.width<=0||reference.rect.height<=0)
            {launcherInstallReason="WaitingForOriginalGridGeometry";return false;}
            if(!TryNextOriginalAppPosition(reference,out var position))
            {launcherInstallReason="WaitingForOriginalAuthoredRow";return false;}
            // Retire only a partial host created by this access before retrying.
            if(launcher)launcher.Clicked-=Show;
            if(launcherHost){launcherHost.gameObject.SetActive(false);Destroy(launcherHost.gameObject);launcherHost=null;}
            launcher=null;
            // Original layout may drive these values; an ignored clone must
            // preserve the live source geometry explicitly, not its zero defaults.
            var sourceAnchorMin=reference.anchorMin;var sourceAnchorMax=reference.anchorMax;
            var sourcePivot=reference.pivot;var sourceSize=reference.sizeDelta;
            // The original grid item owns both Button and its sibling NameText.
            // Clone that complete item; the stretch Button is not a grid cell.
            launcherHost=Instantiate(reference,reference.parent,false);launcherHost.name="T00_TabletApp_FriendsHost";
            // A rebind may see the previous addon child in the source item.
            // Remove only its NEW copied child, never the original Staff item.
            foreach(Transform child in launcherHost)if(child.name=="T00_TabletApp_Friends")
            {child.gameObject.SetActive(false);Destroy(child.gameObject);}
            var layout=reference.parent.GetComponent<LayoutGroup>();
            if(layout&&layout.enabled)
            {var element=launcherHost.GetComponent<LayoutElement>();if(!element)element=launcherHost.gameObject.AddComponent<LayoutElement>();element.ignoreLayout=true;}
            launcherHost.anchorMin=sourceAnchorMin;launcherHost.anchorMax=sourceAnchorMax;
            launcherHost.pivot=sourcePivot;launcherHost.sizeDelta=sourceSize;
            launcherHost.anchoredPosition=position;
            launcher=launcherHost.GetChild(template.transform.GetSiblingIndex()).GetComponent<BaseButton>();
            if(!launcher){launcherHost.gameObject.SetActive(false);Destroy(launcherHost.gameObject);launcherHost=null;launcherInstallReason="WaitingForOriginalButtonClone";return false;}
            launcher.name="T00_TabletApp_Friends";
            foreach(var label in launcherHost.GetComponentsInChildren<TMP_Text>(true))
            {var localizer=label.GetComponent<LocalizeStringEvent>();if(localizer)localizer.enabled=false;label.text="朋友";}
            launcher.gameObject.SetActive(true);launcherHost.gameObject.SetActive(true);launcher.Clicked+=Show;
            launcherInstallReason="Installed";return true;
        }
        bool TryNextOriginalAppPosition(RectTransform reference,out Vector2 position)
        {
            position=reference.anchoredPosition;
            // Compare authored grid-item coordinates sharing the exact anchor
            // basis, independently of transient original menu visibility.
            // Existing app items and their positions are never changed.
            var row=observed.Where(button=>button).Select(button=>button.transform.parent as RectTransform)
                .Where(item=>item&&item.parent==reference.parent&&item.anchorMin==reference.anchorMin&&item.anchorMax==reference.anchorMax&&item.pivot==reference.pivot&&
                    Mathf.Abs(item.anchoredPosition.y-reference.anchoredPosition.y)<1f).Distinct().ToArray();
            var left=row.Where(item=>item!=reference&&item.anchoredPosition.x<reference.anchoredPosition.x)
                .OrderByDescending(item=>item.anchoredPosition.x).FirstOrDefault();
            if(!left)return false;
            float nextX=reference.anchoredPosition.x+(reference.anchoredPosition.x-left.anchoredPosition.x);
            position.x=nextX;
            return !row.Any(item=>Mathf.Abs(item.anchoredPosition.x-nextX)<1f);
        }
        public void Show()
        {
            if(!presenter)return;
            ExplicitNavigation();
            // Hide through each original app's public contract, without replacing its state machine.
            foreach(var app in tablet.GetComponentsInChildren<BaseApp>(true))if(app!=presenter&&app.gameObject.activeSelf)app.Hide();
            presenter.Show();presenter.transform.SetAsLastSibling();SyncInput();
        }
        public void Hide(){if(presenter&&presenter.gameObject.activeSelf)presenter.Hide();}
        void ExplicitNavigation(){resume=null;wasOpen=tablet&&tablet.IsTabletActive;}
        void OnOriginalApp(){Hide();resume=null;presenter?.OpenNormalApp();FriendHousingUI.EndHomeVisit(false);SyncInput();}
        public void SyncInput(){input?.Sync();}
        void Update()
        {
            if(!tablet)return;
            if((!launcher||!launcherHost)&&Time.unscaledTime>=nextLauncherTry)TryInstallLauncher();
            if(tablet.IsTabletActive&&!wasOpen&&resume!=null){var frame=resume;resume=null;presenter.ResumeTablet(frame);}
            wasOpen=tablet.IsTabletActive;SyncInput();
        }
        void LateUpdate()
        {
            if(tablet&&!tablet.IsTabletActive&&presenter&&presenter.gameObject.activeSelf){resume=presenter.SuspendTablet();Hide();}
            SyncInput();
        }
        void OnDestroy(){if(input)Destroy(input);foreach(var button in observed)if(button)button.Clicked-=OnOriginalApp;if(launcher)launcher.Clicked-=Show;if(launcherHost)Destroy(launcherHost.gameObject);if(presenter)presenter.RollbackInstall();}
    }
    // The original launcher grid is a sibling of Applications, so hiding each
    // BaseApp does not hide it. Scope only its rendering and input to this app.
    public sealed class FriendTabletGridScope:MonoBehaviour
    {
        Transform grid;CanvasGroup group;bool ownsGroup,captured,wasEnabled,wasInteractable,wasBlocking;
        float wasAlpha;
        public void Bind(Transform root){grid=root;if(isActiveAndEnabled)Suppress();}
        void OnEnable(){Suppress();}
        void Suppress()
        {
            if(captured||!grid)return;
            group=grid.GetComponent<CanvasGroup>();
            if(!group){group=grid.gameObject.AddComponent<CanvasGroup>();ownsGroup=true;}
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
    public sealed class FriendTabletInstaller:MonoBehaviour
    {
        float next;
        void Update(){FriendTabletInputBridge.FlushReleasedEscapes();if(Time.unscaledTime<next)return;next=Time.unscaledTime+.5f;foreach(var owner in FriendSceneObjects.Tablets())if(owner.IsInitialized&&!owner.GetComponent<FriendTabletAccess>())owner.gameObject.AddComponent<FriendTabletAccess>().Bind(owner);foreach(var map in FriendSceneObjects.Maps())if(!map.GetComponent<FriendArrivalMapOverlay>())map.gameObject.AddComponent<FriendArrivalMapOverlay>();FriendHousingUI.Tick();}
    }
}
