using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using SushiAndFish.FrontEnd;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States.MainMenu;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Networking;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using ECM2.Examples.FirstPerson;

[InitializeOnLoad]
public static class WorldIssuePlayAudit
{
    const string Key="SushiAndFish.WorldIssues.";
    static string Reports=>IslandFrontEndInventory.Reports;
    [Serializable] public class TextRow {public string page,path,text,font,missing;public int visibleCharacters,materialCount;}
    [Serializable] public class Depth {public Vector3 point;public float bottom,depth;public bool hit;public string collider;}
    [Serializable] public class Report {public string utc,stage,error,profile,captureCamera;public bool gameReady,pauseFromEscape,settingsOpened,textsSupported,bottomReached,passed,controlsLayoutSupported,controlsBottomReachable;public TextRow[] texts;public Depth[] depths;public float waterLevel,diveStartY,diveEndY,diveBottomY,diveSeconds;public string[] errors,cameras,unrestoredRenderers,controlsOverlaps,controlsGeometry,terrainState;}
    static Report report;static bool started;static Keyboard keyboard;static readonly List<TextRow> texts=new List<TextRow>();static readonly List<string> errors=new List<string>();
    static WorldIssuePlayAudit(){if(SessionState.GetBool(Key+"active",false)){EditorApplication.update+=Update;Application.logMessageReceived+=Collect;}}
    public static void Start()
    {
        PlayerSettings.productName="SushiAndFish WorldRepair QA 20261016";SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"done",false);SessionState.SetBool(Key+"started",false);started=false;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");EditorApplication.update+=Update;Application.logMessageReceived+=Collect;EditorApplication.EnterPlaymode();
    }
    static void Update()
    {
        if(EditorApplication.isPlaying&&Application.isPlaying&&!SessionState.GetBool(Key+"done",false)&&!SessionState.GetBool(Key+"started",false)){started=true;SessionState.SetBool(Key+"started",true);Run().Forget();}
        if(!EditorApplication.isPlayingOrWillChangePlaymode&&SessionState.GetBool(Key+"done",false))
        {SessionState.SetBool(Key+"active",false);SessionState.SetBool(Key+"done",false);EditorApplication.update-=Update;Application.logMessageReceived-=Collect;PlayerSettings.productName="Deep Blue Sushi";EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");AssetDatabase.SaveAssets();}
    }
    static void Collect(string message,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&errors.Count<25)errors.Add(message+"\n"+stack);}
    static void Stage(string stage){report.stage=stage;report.errors=errors.ToArray();report.texts=texts.ToArray();File.WriteAllText(Path.Combine(Reports,"world-issues-play-audit.json"),JsonUtility.ToJson(report,true));}
    static async UniTask Pause(float sec)=>await UniTask.Delay(TimeSpan.FromSeconds(sec),ignoreTimeScale:true);
    static async UniTask Wait(Func<bool> predicate,int seconds=150){var end=DateTime.UtcNow.AddSeconds(seconds);while(!predicate()){if(DateTime.UtcNow>end)throw new TimeoutException(report.stage);await UniTask.Yield();}}
    static T Field<T>(UnityEngine.Object o,string key)where T:UnityEngine.Object=>new SerializedObject(o).FindProperty(key).objectReferenceValue as T;
    static void Dismiss()
    {
        var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuManager>();if(menu){var p=Field<GameObject>(menu,"_earlyAccessPopUp");if(p&&p.activeSelf)typeof(MainMenuManager).GetMethod("OnClickOkButton",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(menu,null);}
        var motion=UnityEngine.Object.FindFirstObjectByType<MotionSicknessPopup>();if(motion&&motion.IsVisible)typeof(MotionSicknessPopup).GetMethod("OnKeepDefaultsSelected",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(motion,null);
        var rapid=UnityEngine.Object.FindFirstObjectByType<RapidPressBindingPopup>();if(rapid&&rapid.IsVisible)rapid.UseMouse1AndClose();
    }
    static bool Ready()=>SceneManager.GetSceneByName("GameState").isLoaded&&Singleton<PlayersManager>.TryGetInstance(out var p)&&p.LocalPlayer&&Time.timeScale>.9f&&UnityEngine.Object.FindObjectsByType<GameStartGate>(FindObjectsSortMode.None).Any(g=>g.IsGameStarted&&g.BootstrapCompletedClientCount>=1)&&!UnityEngine.Object.FindObjectsByType<LoadingScreenBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(s=>s.IsVisible);
    static async UniTask PressKey(UnityEngine.InputSystem.Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));await Pause(.25f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Pause(.8f);}
    static void InspectText(string page,Transform root)
    {
        foreach(var t in root.GetComponentsInChildren<TMP_Text>(false))
        {
            if(string.IsNullOrWhiteSpace(t.text)||!t.font)continue;t.ForceMeshUpdate(true,true);
            string plain=System.Text.RegularExpressions.Regex.Replace(t.text,"<[^>]*>","");t.font.HasCharacters(plain,out uint[] missing,true,false);
            texts.Add(new TextRow{page=page,path=IslandFrontEndInventory.PathOf(t.transform),text=t.text,font=t.font.name,missing=missing==null?"":string.Concat(missing.Where(c=>!char.IsControl((char)c)).Select(c=>char.ConvertFromUtf32((int)c))),visibleCharacters=t.textInfo.characterInfo.Take(t.textInfo.characterCount).Count(c=>c.isVisible),materialCount=t.textInfo.materialCount});
        }
    }
    static Camera WorldCamera()
    {
        var owner=Singleton<PlayersManager>.Instance.LocalPlayer;
        var cameras=owner.GetComponentsInChildren<Camera>(true).Where(c=>c.isActiveAndEnabled&&c.targetTexture==null&&(c.cullingMask&1)!=0&&c.GetUniversalAdditionalCameraData().renderType==CameraRenderType.Base).ToArray();
        if(cameras.Length==0)throw new Exception("Original player world camera was not found");
        return cameras.OrderByDescending(c=>c.CompareTag("MainCamera")).ThenByDescending(c=>c.depth).First();
    }
    static async UniTask Capture(string name,Vector3 position,Vector3 target)
    {
        var camera=WorldCamera();Vector3 p=camera.transform.position;Quaternion q=camera.transform.rotation;
        for(int i=0;i<6;i++){await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));if(i==3)ScreenCapture.CaptureScreenshot(Path.Combine(Reports,name));}
        await Pause(.5f);camera.transform.SetPositionAndRotation(p,q);
    }
    static async UniTask ShoreCapture(FirstPersonCharacter character,Vector3 ground,Vector3 target,string name)
    {
        if(!Physics.Raycast(ground+Vector3.up*40,Vector3.down,out var hit,100,~0,QueryTriggerInteraction.Ignore))throw new Exception("Missing shore collision at "+ground);
        ground.y=hit.point.y+.1f;character.SetVelocity(Vector3.zero);character.SetPosition(ground,true);await Pause(1);
        character.LookAtInstant(target);await Pause(.6f);ScreenCapture.CaptureScreenshot(Path.Combine(Reports,name));await Pause(.8f);
    }
    static Rect BoundsIn(RectTransform root,RectTransform target)
    {
        var corners=new Vector3[4];target.GetWorldCorners(corners);var points=corners.Select(root.InverseTransformPoint).ToArray();
        return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
    }
    static Rect TextBounds(RectTransform root,TMP_Text text)
    {
        text.ForceMeshUpdate(true,true);var points=text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c=>c.isVisible).SelectMany(c=>new[]{c.bottomLeft,c.topRight}).Select(p=>root.InverseTransformPoint(text.transform.TransformPoint(p))).ToArray();
        return points.Length==0?new Rect():Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
    }
    static async UniTask InspectControls(SettingsScreen settings)
    {
        var root=Field<RectTransform>(settings,"_controlsContainer");var content=root.Find("Content") as RectTransform;var keyboard=content.Find("KeyboardLayout") as RectTransform;var scroll=Field<UnityEngine.UI.ScrollRect>(settings,"_scrollRect");
        var overlaps=new List<string>();float lastOptionBottom=float.MaxValue;
        foreach(RectTransform row in content)
        {
            if(row==keyboard||!row.gameObject.activeInHierarchy)continue;
            var label=row.Find("Label")?.GetComponent<TMP_Text>();if(!label)continue;
            var labelBounds=TextBounds(root,label);var controlBounds=BoundsIn(root,row);
            if(labelBounds.Overlaps(controlBounds))overlaps.Add("Option label crosses control: "+row.name);
            lastOptionBottom=Mathf.Min(lastOptionBottom,labelBounds.yMin,controlBounds.yMin);
        }
        if(BoundsIn(root,keyboard).yMax>lastOptionBottom-20)overlaps.Add("Keyboard bindings start above the end of the option list");
        foreach(RectTransform binding in keyboard)
        {
            if(!binding.gameObject.activeInHierarchy||!binding.TryGetComponent<UnityEngine.UI.Button>(out var button))continue;
            var label=binding.Find("ButtonDefaultUGUI (VisualsOnly)/Label")?.GetComponent<TMP_Text>();
            var key=binding.Find("KeyWithTextNormal") as RectTransform;
            if(label&&key&&TextBounds(root,label).Overlaps(BoundsIn(root,key)))overlaps.Add("Binding action label crosses its key: "+binding.name);
        }
        report.controlsOverlaps=overlaps.ToArray();report.controlsLayoutSupported=overlaps.Count==0;
        settings.ScrollTo(keyboard);await Pause(1);ScreenCapture.CaptureScreenshot(Path.Combine(Reports,"world-repaired-controls-bindings.png"));await Pause(.6f);
        scroll.verticalNormalizedPosition=0;await Pause(.8f);Canvas.ForceUpdateCanvases();
        var final=keyboard.Cast<RectTransform>().Where(t=>t.gameObject.activeInHierarchy).Last();var finalBounds=BoundsIn(scroll.viewport,final);report.controlsBottomReachable=finalBounds.yMin>=scroll.viewport.rect.yMin-2&&finalBounds.yMax<=scroll.viewport.rect.yMax+2;
        report.controlsGeometry=new[]{"category size="+root.sizeDelta+" rect="+root.rect+" position="+root.anchoredPosition,"content size="+content.sizeDelta+" rect="+content.rect,"keyboard size="+keyboard.sizeDelta+" rect="+keyboard.rect+" position="+keyboard.anchoredPosition,"scroll content size="+scroll.content.sizeDelta+" rect="+scroll.content.rect+" position="+scroll.content.anchoredPosition,"viewport="+scroll.viewport.rect,"final "+final.name+" bounds="+finalBounds+" local="+final.anchoredPosition,"key children="+keyboard.childCount};
        ScreenCapture.CaptureScreenshot(Path.Combine(Reports,"world-repaired-controls-bottom.png"));await Pause(.6f);
    }
    static async UniTaskVoid Run()
    {
        await UniTask.Yield();report=new Report{utc=DateTime.UtcNow.ToString("o"),profile=Application.persistentDataPath};
        var input=InputSystem.settings;var oldFocus=input.editorInputBehaviorInPlayMode;var oldBackground=input.backgroundBehavior;
        input.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;input.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        try
        {
            if(!report.profile.Contains("WorldRepair QA"))throw new Exception("Formal saves are not isolated");
            Stage("menu");await Wait(()=>{Dismiss();return IslandFrontEndFlow.Active&&IslandFrontEndFlow.Active.BackdropLoaded;});await Pause(2);Dismiss();
            await Singleton<MainMenuManager>.Instance.CustomizationSceneHost.PreloadAsync();
            IslandFrontEndFlow.Active.ShowLoadGames();await Pause(1);var slot=Resources.FindObjectsOfTypeAll<SaveSlot>().Where(s=>s&&s.gameObject.scene.name=="MainMenuState"&&s.ContainsSave).OrderBy(s=>s.SlotIndex).First();slot.LaunchOriginalSlot();
            Stage("game_loading");await Wait(Ready,210);await Pause(4);report.gameReady=true;
            report.cameras=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Select(c=>c.name+" scene="+c.gameObject.scene.name+" mask="+c.cullingMask+" type="+c.GetUniversalAdditionalCameraData().renderType+" depth="+c.GetUniversalAdditionalCameraData().requiresDepthTexture+" color="+c.GetUniversalAdditionalCameraData().requiresColorTexture).ToArray();
            report.unrestoredRenderers=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.sharedMaterials.Any(m=>m&&(!m.shader||!m.shader.isSupported||m.shader.name=="Hidden/InternalErrorShader"))).Select(r=>r.name+" layer="+r.gameObject.layer+" path="+IslandFrontEndInventory.PathOf(r.transform)+" materials="+string.Join(";",r.sharedMaterials.Where(m=>m).Select(m=>m.name+" shader="+(m.shader?m.shader.name:"null")))).Take(40).ToArray();
            Stage("pause_labels");keyboard=InputSystem.AddDevice<Keyboard>("WorldRepairVerificationKeyboard");await PressKey(UnityEngine.InputSystem.Key.Escape);
            var pause=Singleton<GameUIManager>.Instance.PauseScreen;report.pauseFromEscape=pause.IsVisible;if(!pause.IsVisible)throw new Exception("Escape did not open pause");InspectText("pause",pause.transform);ScreenCapture.CaptureScreenshot(Path.Combine(Reports,"world-repaired-pause.png"));await Pause(1);
            Field<BaseButton>(pause,"_settingsButton").OnSubmit(new UnityEngine.EventSystems.BaseEventData(UnityEngine.EventSystems.EventSystem.current));await Pause(2);
            var settings=UnityEngine.Object.FindFirstObjectByType<SettingsScreen>(FindObjectsInactive.Include);report.settingsOpened=settings&&settings.IsVisible;
            if(!report.settingsOpened)throw new Exception("Settings did not open");InspectText("settings",settings.transform);ScreenCapture.CaptureScreenshot(Path.Combine(Reports,"world-repaired-settings.png"));await Pause(1);
            foreach(var category in new[]{"_graphicsButton","_soundButton","_controlsButton"})
            {var tab=Field<BaseButton>(settings,category);if(!tab||!tab.gameObject.activeInHierarchy)continue;tab.OnSubmit(new UnityEngine.EventSystems.BaseEventData(UnityEngine.EventSystems.EventSystem.current));await Pause(1);InspectText(category,settings.transform);ScreenCapture.CaptureScreenshot(Path.Combine(Reports,"world-repaired-settings"+category+".png"));await Pause(.5f);if(category=="_controlsButton")await InspectControls(settings);}
            report.textsSupported=texts.Count>10&&texts.All(t=>string.IsNullOrEmpty(t.missing));settings.Hide();await PressKey(UnityEngine.InputSystem.Key.Escape);if(pause.IsVisible)await PressKey(UnityEngine.InputSystem.Key.Escape);
            Stage("comprehensive_island_review");await IslandComprehensivePlayAudit.Run(WorldCamera());
            Stage("shore_and_seabed");var depths=new List<Depth>();report.waterLevel=UnityEngine.Object.FindFirstObjectByType<StylizedWater3.OceanFollowBehaviour>().transform.position.y;
            var actualCamera=WorldCamera();report.captureCamera=actualCamera.name+" mask="+actualCamera.cullingMask+" scene="+actualCamera.gameObject.scene.name;
            foreach(var point in new[]{new Vector3(60,0,75),new Vector3(60,0,90),new Vector3(-100,0,90),new Vector3(130,0,-100),new Vector3(-230,0,0),new Vector3(0,0,-180),new Vector3(700,0,0),new Vector3(-700,0,0),new Vector3(0,0,-600),new Vector3(1900,0,1900)})
            {bool hit=Physics.Raycast(point,Vector3.down,out var ray,100,~0,QueryTriggerInteraction.Ignore);depths.Add(new Depth{point=point,hit=hit,bottom=hit?ray.point.y:-999,depth=hit?report.waterLevel-ray.point.y:999,collider=hit?ray.collider.name:""});}
            report.depths=depths.ToArray();
            var character=UnityEngine.Object.FindFirstObjectByType<FirstPersonCharacter>();
            await ShoreCapture(character,new Vector3(40,0,44),new Vector3(62,-3.3f,53),"world-repaired-beach.png");
            await ShoreCapture(character,new Vector3(35,0,40),new Vector3(39,-3.5f,44),"world-repaired-sand.png");
            var terrains=UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            report.terrainState=terrains.Select(t=>t.name+" pos="+t.transform.position+" enabled="+t.enabled+" draw="+t.drawHeightmap+" instanced="+t.drawInstanced+" material="+t.materialTemplate.name+" shader="+t.materialTemplate.shader.name+" supported="+t.materialTemplate.shader.isSupported+" passes="+t.materialTemplate.passCount+" keywords="+string.Join(",",t.materialTemplate.shaderKeywords)).ToArray();
            await Capture("world-repaired-cleared-lots.png",new Vector3(45,65,-20),new Vector3(30,1,-20));
            Stage("actual_character_dive");var original=character.GetPosition();
            var dive=depths[1];if(!dive.hit)throw new Exception("No physical seabed at dive point");report.diveBottomY=dive.bottom;
            character.SetVelocity(Vector3.zero);character.SetPosition(new Vector3(60,report.waterLevel-1.5f,90),true);report.diveStartY=character.GetPosition().y;
            character.Crouch();var start=DateTime.UtcNow;
            await Wait(()=>{report.diveEndY=character.GetPosition().y;return character.GetPosition().y-dive.bottom<.5f;},15);
            report.diveSeconds=(float)(DateTime.UtcNow-start).TotalSeconds;report.bottomReached=character.GetPosition().y>=dive.bottom-.2f&&report.diveStartY-character.GetPosition().y>.5f;
            await Capture("world-repaired-underwater-bottom.png",Camera.main.transform.position,new Vector3(63,dive.bottom,92));
            character.UnCrouch();character.SetVelocity(Vector3.zero);character.SetPosition(original,true);await Pause(1);
            report.passed=report.gameReady&&report.pauseFromEscape&&report.settingsOpened&&report.textsSupported&&report.bottomReached&&report.controlsLayoutSupported&&report.controlsBottomReachable&&depths.All(d=>d.hit&&d.depth>0&&d.depth<16)&&errors.Count==0;
            Stage(report.passed?"PASS":"CHECK_FAILED");
        }
        catch(Exception e){report.error=e.ToString();Stage("FAILED");}
        finally{if(keyboard!=null)InputSystem.RemoveDevice(keyboard);input.editorInputBehaviorInPlayMode=oldFocus;input.backgroundBehavior=oldBackground;SessionState.SetBool(Key+"done",true);EditorApplication.ExitPlaymode();}
    }
}
