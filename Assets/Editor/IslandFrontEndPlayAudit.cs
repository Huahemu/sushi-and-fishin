using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Bozo.ModularCharacters;
using Cysharp.Threading.Tasks;
using SushiAndFish.FrontEnd;
using TMPro;
using PaperPixelGames.Core;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player.Visuals;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States.MainMenu;
using PaperPixelGames.Framework.States.Game;
using PaperPixelGames.Framework.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class IslandFrontEndPlayAudit
{
    const string Key="SushiAndFish.FrontEndAudit.";
    static string Reports=>IslandFrontEndInventory.Reports;
    static readonly List<string> Errors=new List<string>();
    static Report report;
    static Keyboard keyboard;
    static Mouse pointerMouse;
    static readonly List<string> PointerChecks=new List<string>();
    [Serializable] public class Report
    {
        public string utc,stage,error,saveProfile;
        public bool steamClientValidAtStart,steamBlockerHidden,localOfflineTransport,menuFourButtons,backdropLoaded,cameraPositionFixed,cameraForwardVertical,cameraRotates,settingsOpened,emptyLoadList,newGameOpensOriginalCreator,originalCreatorRandomized,confirmStyleMatches,confirmedAvatarSaved,spawnedAtOriginalPoint,pauseOpenedFromEscape,pauseKeyboardGraphicHidden,pauseButtonsPreserved,worldSaved,returnedToMenu,existingSaveVisible,loadSkippedCreator,avatarRestoredOnLoad,loadedGame,passed;
        public string[] mainButtonLabels,loadedGameplayScenes,errors;
        public float headingDelta,spawnDistance;
        public int slotIndex;
    }
    static IslandFrontEndPlayAudit()
    {
        if(SessionState.GetBool(Key+"active",false)){EditorApplication.update+=Update;Application.logMessageReceived+=Collect;}
    }
    [MenuItem("Tools/Sushi & Fish/Verify complete front-end flow with isolated saves")]
    public static void Start()
    {
        if(Application.isBatchMode)throw new InvalidOperationException("This scenario requires the visible Editor frame loop.");
        string original=PlayerSettings.productName;
        if(original.Contains("FrontEnd QA"))original="Deep Blue Sushi";
        SessionState.SetString(Key+"originalProduct",original);
        PlayerSettings.productName="SushiAndFish FrontEnd QA "+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"started",false);SessionState.SetBool(Key+"done",false);
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);
        EditorApplication.update+=Update;Application.logMessageReceived+=Collect;EditorApplication.EnterPlaymode();
    }
    static void Collect(string message,string trace,LogType type)
    {
        if((type==LogType.Error || type==LogType.Exception || type==LogType.Assert) && Errors.Count<40)Errors.Add(message+"\n"+trace);
    }
    static void Update()
    {
        if(!SessionState.GetBool(Key+"active",false))return;
        if(EditorApplication.isPlaying && !SessionState.GetBool(Key+"started",false))
        {SessionState.SetBool(Key+"started",true);Scenario().Forget();}
        if(!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(Key+"done",false))
        {
            PlayerSettings.productName=SessionState.GetString(Key+"originalProduct","Deep Blue Sushi");
            SessionState.SetBool(Key+"active",false);
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);
            if(RecoveryEditorAutomation.KeepOpen){EditorApplication.update-=Update;Application.logMessageReceived-=Collect;return;}
            EditorApplication.Exit(SessionState.GetBool(Key+"passed",false)?0:2);
        }
    }
    static void Stage(string stage)
    {
        report.stage=stage;File.WriteAllText(Path.Combine(Reports,"island-front-end-play-audit.json"),JsonUtility.ToJson(report,true));Debug.Log("ISLAND_FRONT_END_STAGE "+stage);
    }
    static async UniTask WaitFor(Func<bool> predicate,string description,double seconds=120)
    {
        var start=DateTime.UtcNow;
        while(!predicate())
        {
            if((DateTime.UtcNow-start).TotalSeconds>seconds)throw new TimeoutException(description);
            await UniTask.Yield();
        }
    }
    static async UniTask Pause(double seconds){await UniTask.Delay(TimeSpan.FromSeconds(seconds),ignoreTimeScale:true);}
    static T Field<T>(UnityEngine.Object owner,string name) where T:UnityEngine.Object=>new SerializedObject(owner).FindProperty(name).objectReferenceValue as T;
    static async UniTask Submit(BaseButton button)
    {
        if(!button || !button.isActiveAndEnabled || !button.interactable)throw new InvalidOperationException("Button is not available: "+(button?button.name:"null"));
        if(pointerMouse==null){pointerMouse=InputSystem.AddDevice<Mouse>("IslandFrontEndVerificationMouse");await Pause(0.5);}
        Canvas.ForceUpdateCanvases();var rect=(RectTransform)button.transform;var canvas=button.GetComponentInParent<Canvas>();
        var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        Vector2 position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=position},hits);
        var module=EventSystem.current.currentInputModule as UnityEngine.InputSystem.UI.InputSystemUIInputModule;
        string info=button.name+" at "+position+" eventSystem="+EventSystem.current.name+" module="+(module?module.name:"null")+" point="+(module&&module.point!=null&&module.point.action!=null?module.point.action.ToString()+" enabled="+module.point.action.enabled:"NULL")+" click="+(module&&module.leftClick!=null&&module.leftClick.action!=null?module.leftClick.action.ToString()+" enabled="+module.leftClick.action.enabled:"NULL")+" raycasts="+string.Join(";",hits.Take(8).Select(h=>h.gameObject.name+" handler="+(ExecuteEvents.GetEventHandler<IPointerClickHandler>(h.gameObject)?ExecuteEvents.GetEventHandler<IPointerClickHandler>(h.gameObject).name:"null")));
        PointerChecks.Add(info);File.WriteAllLines(Path.Combine(Reports,"island-front-end-pointer-events.txt"),PointerChecks);
        int fired=0;Action clicked=()=>fired++;button.Clicked+=clicked;
        InputSystem.QueueStateEvent(pointerMouse,new MouseState{position=position});await Pause(0.4);
        InputSystem.QueueStateEvent(pointerMouse,new MouseState{position=position}.WithButton(MouseButton.Left));await Pause(0.2);
        InputSystem.QueueStateEvent(pointerMouse,new MouseState{position=position});await Pause(0.4);
        button.Clicked-=clicked;
        PointerChecks.Add("clicked="+fired+" "+button.name);File.WriteAllLines(Path.Combine(Reports,"island-front-end-pointer-events.txt"),PointerChecks);
        if(fired!=1)throw new InvalidOperationException("Pointer did not click exactly once: "+info+" clicked="+fired);
    }
    static async UniTask Screenshot(string name)
    {ScreenCapture.CaptureScreenshot(Path.Combine(Reports,name));await Pause(2);}
    static void DismissFirstLaunch()
    {
        var motion=UnityEngine.Object.FindFirstObjectByType<MotionSicknessPopup>();if(motion&&motion.IsVisible)typeof(MotionSicknessPopup).GetMethod("OnKeepDefaultsSelected",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(motion,null);
        var rapid=UnityEngine.Object.FindFirstObjectByType<RapidPressBindingPopup>();if(rapid&&rapid.IsVisible)rapid.UseMouse1AndClose();
        var menu=Singleton<MainMenuManager>.Instance;
        if(menu){var popup=Field<GameObject>(menu,"_earlyAccessPopUp");if(popup&&popup.activeSelf)typeof(MainMenuManager).GetMethod("OnClickOkButton",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(menu,null);}
    }
    static bool GameReady()=>SceneManager.GetSceneByName("GameState").isLoaded
        && Singleton<PlayersManager>.TryGetInstance(out var players) && players.LocalPlayer && Time.timeScale>0.9f
        && UnityEngine.Object.FindObjectsByType<PaperPixelGames.Networking.GameStartGate>(FindObjectsSortMode.None).Any(g=>g.IsGameStarted && g.BootstrapCompletedClientCount>=1)
        && !UnityEngine.Object.FindObjectsByType<LoadingScreenBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(s=>s.IsVisible || (s.gameObject.activeInHierarchy && s.GetComponentsInChildren<CanvasGroup>(true).Any(c=>c.alpha>0.05f)))
        && !UnityEngine.Object.FindObjectsByType<Michsky.LSS.LSS_LoadingScreen>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(s=>s.gameObject.activeInHierarchy && s.canvasGroup && s.canvasGroup.alpha>0.05f);
    static async UniTask Escape()
    {
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.Escape));await Pause(0.25);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Pause(1);
    }
    static async UniTaskVoid Scenario()
    {
        report=new Report{utc=DateTime.UtcNow.ToString("o"),saveProfile=Application.persistentDataPath,steamClientValidAtStart=Steamworks.SteamClient.IsValid};
        var inputSettings=InputSystem.settings;
        var oldFocus=inputSettings.editorInputBehaviorInPlayMode;var oldBackground=inputSettings.backgroundBehavior;
        inputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        inputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        try
        {
            if(!report.saveProfile.Contains("SushiAndFish FrontEnd QA"))throw new InvalidOperationException("Verification save profile was not isolated");
            Stage("boot_and_menu");
            await WaitFor(()=>IslandFrontEndFlow.Active && Singleton<MainMenuManager>.TryGetInstance(out var menu) && menu.gameObject.activeInHierarchy,"Composed menu did not initialize");
            await WaitFor(()=>{DismissFirstLaunch();return IslandFrontEndFlow.Active.BackdropLoaded;},"Island backdrop did not load");
            await Pause(5);DismissFirstLaunch();await Pause(2);
            var flow=IslandFrontEndFlow.Active;var manager=Singleton<MainMenuManager>.Instance;
            report.steamBlockerHidden=!Field<GameObject>(manager,"_steamConnectionBlocker").activeInHierarchy;
            var buttons=Field<RectTransform>(manager,"_buttons").GetComponentsInChildren<BaseButton>(false);
            report.mainButtonLabels=buttons.Select(b=>b.GetComponentInChildren<TMP_Text>().text).ToArray();report.menuFourButtons=buttons.Length==4;report.backdropLoaded=flow.BackdropLoaded;
            Vector3 firstPosition=flow.OrbitCamera.transform.position;float heading=flow.OrbitCamera.Heading;
            await Screenshot("island-start-menu.png");await Pause(6);
            File.WriteAllLines(Path.Combine(Reports,"island-menu-live-water.txt"),new[]{"camera="+flow.OrbitCamera.GetComponent<Camera>().orthographic+" "+flow.OrbitCamera.transform.position}.Concat(UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.gameObject.layer==4).Take(40).Select(r=>r.name+" enabled="+r.enabled+" visible="+r.isVisible+" bounds="+r.bounds+" material="+(r.sharedMaterial?r.sharedMaterial.name:"null")+" shader="+(r.sharedMaterial&&r.sharedMaterial.shader?r.sharedMaterial.shader.name:"null"))));
            report.cameraPositionFixed=Vector3.Distance(firstPosition,flow.OrbitCamera.transform.position)<0.001f;report.cameraForwardVertical=Vector3.Dot(flow.OrbitCamera.transform.forward,Vector3.down)>0.9999f;
            report.headingDelta=Mathf.Abs(Mathf.DeltaAngle(heading,flow.OrbitCamera.Heading));report.cameraRotates=report.headingDelta>4;
            await Screenshot("island-start-menu-rotated.png");
            Stage("settings_and_empty_load_list");await Submit(Field<BaseButton>(manager,"_settingsButton"));await Pause(2);report.settingsOpened=Field<SettingsScreen>(manager,"_settingsScreen").IsVisible;
            await Screenshot("island-start-settings.png");manager.CustomizationScreenClose();await Pause(1);
            await Submit(Field<BaseButton>(manager,"_playButton"));await Pause(1);
            var saveScreen=Field<SaveSlotScreen>(manager,"_saveSlotScreen");report.emptyLoadList=saveScreen.IsVisible && !saveScreen.GetComponentsInChildren<SaveSlot>(true).Any(s=>s.gameObject.activeInHierarchy);
            manager.CustomizationScreenClose();await Pause(1);
            Stage("new_save_original_creator");await Submit(Field<BaseButton>(manager,"_singleplayerButton"));
            await WaitFor(()=>flow.IsCreatingCharacter && !flow.IsBusy,"Original creator did not open",180);
            report.slotIndex=flow.PendingSlot;
            var creator=UnityEngine.Object.FindFirstObjectByType<CharacterCreator>();report.newGameOpensOriginalCreator=creator && creator.gameObject.scene.name=="StylizedCharacterCreator";
            await WaitFor(()=>creator.character && creator.character.initalized && !creator.character.isloading && !creator.character.isMerging && !creator.character.isUpdatingTexture,"Original outfit system was not ready",180);
            var before=JsonUtility.ToJson(BMAC_SaveSystem.GetCharacterData(creator.character));
            await creator.RandomizeCharacterAsync();
            await WaitFor(()=>!creator.character.isloading && !creator.character.isMerging && !creator.character.isUpdatingTexture,"Original randomizer did not finish",180);
            string confirmed=JsonUtility.ToJson(BMAC_SaveSystem.GetCharacterData(creator.character));report.originalCreatorRandomized=confirmed!=before;
            var confirm=UnityEngine.Object.FindFirstObjectByType<CharacterCreationConfirmButton>();
            var template=Field<BaseButton>(manager,"_singleplayerButton");
            report.confirmStyleMatches=confirm && confirm.GetComponentInChildren<TMP_Text>().font==template.GetComponentInChildren<TMP_Text>(true).font;
            await Screenshot("original-character-creator-with-confirm.png");
            Stage("confirm_and_spawn");await Submit(confirm.GetComponent<BaseButton>());
            string characterPath=Path.Combine(report.saveProfile,"CharacterCustomization","SaveSlot_"+report.slotIndex+".json");
            await WaitFor(()=>File.Exists(characterPath),"Confirmed avatar sidecar was not saved");
            string savedAvatar=JsonUtility.ToJson(JsonUtility.FromJson<CharacterData>(File.ReadAllText(characterPath)));
            report.confirmedAvatarSaved=PlayerCustomizationSession.GetCurrentCharacterData()!=null;
            await WaitFor(()=>Singleton<PlayersManager>.TryGetInstance(out var initialPlayers) && initialPlayers.LocalPlayer,"New game did not spawn a local player",200);
            var players=Singleton<PlayersManager>.Instance;var spawn=players.GetPlayerSpawnPointForSlot(0);if(!spawn)spawn=players.PlayerSpawnPoint;
            report.localOfflineTransport=Unity.Netcode.NetworkManager.Singleton.NetworkConfig.NetworkTransport is Netcode.Transports.Local.LocalSingleplayerTransport;
            report.spawnDistance=Vector3.Distance(players.LocalPlayer.transform.position,spawn.position);report.spawnedAtOriginalPoint=report.spawnDistance<3;
            await WaitFor(GameReady,"New game did not finish loading",200);await Pause(3);
            report.loadedGameplayScenes=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i).name).ToArray();
            await Screenshot("new-character-at-original-spawn.png");
            Stage("escape_menu_and_save");keyboard=InputSystem.AddDevice<Keyboard>("IslandFrontEndVerificationKeyboard");await Pause(1);await Escape();
            var pause=Singleton<GameUIManager>.Instance.PauseScreen;report.pauseOpenedFromEscape=pause.IsVisible;
            if(!pause.IsVisible)throw new InvalidOperationException("The actual Escape action did not open the original pause screen");
            report.pauseKeyboardGraphicHidden=!pause.transform.Find("ControlSchema").gameObject.activeSelf;
            report.pauseButtonsPreserved=new[]{"_resumeButton","_playersButton","_imStuckButton","_settingsButton","_saveButton","_mainMenuButton"}.All(field=>Field<BaseButton>(pause,field));
            await Screenshot("original-escape-menu-without-keyboard-diagram.png");await Submit(Field<BaseButton>(pause,"_saveButton"));
            await WaitFor(()=>!Singleton<SaveManager>.Instance.SaveFlag && Singleton<SaveManager>.Instance.CurrentSaveSlotData.GameProgress.HasSave && File.Exists(Path.Combine(report.saveProfile,"SaveSlot_"+report.slotIndex+".es3")),"Original save action did not produce a saved world",90);report.worldSaved=true;
            Stage("return_to_menu_and_load");await Submit(Field<BaseButton>(pause,"_mainMenuButton"));
            await WaitFor(()=>IslandFrontEndFlow.Active && IslandFrontEndFlow.Active.BackdropLoaded && SceneManager.GetSceneByName("MainMenuState").isLoaded && Singleton<GameSessionLifecycle>.TryGetInstance(out var lifecycle) && !lifecycle.ReturnToMenuInProgress,"Original return-to-menu flow did not complete",180);await Pause(3);DismissFirstLaunch();
            report.returnedToMenu=true;flow=IslandFrontEndFlow.Active;manager=Singleton<MainMenuManager>.Instance;
            var presets=Resources.LoadAll<CharacterObject>(string.Empty).Where(p=>p.ShowInCharacterList && p.data?.outfitDatas!=null).ToArray();
            foreach(var preset in presets)if(JsonUtility.ToJson(preset.data)!=savedAvatar){PlayerCustomizationSession.SetCurrentCharacterData(preset.data,true);break;}
            await Submit(Field<BaseButton>(manager,"_playButton"));await Pause(2);saveScreen=Field<SaveSlotScreen>(manager,"_saveSlotScreen");
            var slot=saveScreen.GetComponentsInChildren<SaveSlot>(false).First(s=>s.SlotIndex==report.slotIndex);report.existingSaveVisible=slot.ContainsSave;
            await Screenshot("existing-save-load-menu.png");
            var slotButton=Field<BaseButton>(slot,"_saveSlotButton");await Submit(slotButton);
            report.loadSkippedCreator=!flow.IsCreatingCharacter && !manager.CustomizationSceneHost.IsOpen;
            report.avatarRestoredOnLoad=JsonUtility.ToJson(PlayerCustomizationSession.GetCurrentCharacterData())==savedAvatar;
            await WaitFor(GameReady,"Existing save did not finish loading",200);report.loadedGame=true;await Screenshot("loaded-original-island-game.png");
            report.errors=Errors.ToArray();
            var checks=new[]{report.steamBlockerHidden,report.localOfflineTransport,report.menuFourButtons,report.backdropLoaded,report.cameraPositionFixed,report.cameraForwardVertical,report.cameraRotates,report.settingsOpened,report.emptyLoadList,report.newGameOpensOriginalCreator,report.originalCreatorRandomized,report.confirmStyleMatches,report.confirmedAvatarSaved,report.spawnedAtOriginalPoint,report.pauseOpenedFromEscape,report.pauseKeyboardGraphicHidden,report.pauseButtonsPreserved,report.worldSaved,report.returnedToMenu,report.existingSaveVisible,report.loadSkippedCreator,report.avatarRestoredOnLoad,report.loadedGame};
            report.passed=checks.All(value=>value)&&Errors.Count==0;Stage(report.passed?"PASS":"CHECK_FAILED");
        }
        catch(Exception exception){report.error=exception.ToString();report.errors=Errors.ToArray();report.passed=false;Stage("FAILED");}
        finally
        {
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            if(pointerMouse!=null)InputSystem.RemoveDevice(pointerMouse);
            inputSettings.editorInputBehaviorInPlayMode=oldFocus;inputSettings.backgroundBehavior=oldBackground;
            File.WriteAllText(Path.Combine(Reports,"island-front-end-play-audit.json"),JsonUtility.ToJson(report,true));
            SessionState.SetBool(Key+"passed",report.passed);SessionState.SetBool(Key+"done",true);EditorApplication.ExitPlaymode();
        }
    }
}
