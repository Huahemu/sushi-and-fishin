using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using SushiAndFish.FrontEnd;
using Bozo.ModularCharacters;
using PaperPixelGames.Framework.States.MainMenu;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class CreatorButtonRepairAudit
{
    const string Key="SushiAndFish.CreatorButtonRepair.";
    static string Reports=>IslandFrontEndInventory.Reports;
    [Serializable] public class Report {public string utc,stage,error,profile;public bool noOverlap,raycastHitsConfirm,clickedOnce,playerSpawned,passed;public int eventSystems;public string[] hits;}
    static Report report;static Mouse mouse;static string product;static bool started;
    static CreatorButtonRepairAudit(){if(SessionState.GetBool(Key+"active",false))EditorApplication.update+=Update;}
    public static void Start()
    {
        product=PlayerSettings.productName;if(product.Contains("SushiAndFish")&&product.Contains("QA"))product="Deep Blue Sushi";SessionState.SetString(Key+"product",product);
        PlayerSettings.productName="SushiAndFish CreatorButton QA Fixed";
        Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AppData/LocalLow/PaperPixelGames",PlayerSettings.productName));
        SessionState.SetBool(Key+"active",true);started=false;
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        EditorApplication.update+=Update;EditorApplication.EnterPlaymode();
    }
    static void Update(){if(EditorApplication.isPlaying&&Application.isPlaying&&!started){started=true;Run().Forget();}}
    static void Save(string stage){report.stage=stage;File.WriteAllText(Path.Combine(Reports,"creator-button-repair-audit.json"),JsonUtility.ToJson(report,true));}
    static async UniTask Wait(Func<bool> condition,int seconds=120){var end=DateTime.UtcNow.AddSeconds(seconds);while(!condition()){if(DateTime.UtcNow>end)throw new TimeoutException(report.stage);await UniTask.Yield();}}
    static async UniTask Pause(float seconds)=>await UniTask.Delay(TimeSpan.FromSeconds(seconds),ignoreTimeScale:true);
    static void Dismiss()
    {
        var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuManager>();
        if(menu){var p=new SerializedObject(menu).FindProperty("_earlyAccessPopUp").objectReferenceValue as GameObject;if(p&&p.activeSelf)typeof(MainMenuManager).GetMethod("OnClickOkButton",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(menu,null);}
        var motion=UnityEngine.Object.FindFirstObjectByType<MotionSicknessPopup>();if(motion&&motion.IsVisible)typeof(MotionSicknessPopup).GetMethod("OnKeepDefaultsSelected",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(motion,null);
        var rapid=UnityEngine.Object.FindFirstObjectByType<RapidPressBindingPopup>();if(rapid&&rapid.IsVisible)rapid.UseMouse1AndClose();
    }
    static Rect Bounds(RectTransform t){var c=new Vector3[4];t.GetWorldCorners(c);return Rect.MinMaxRect(c.Min(v=>v.x),c.Min(v=>v.y),c.Max(v=>v.x),c.Max(v=>v.y));}
    static async UniTaskVoid Run()
    {
        await UniTask.Yield();
        report=new Report{utc=DateTime.UtcNow.ToString("o"),profile=Application.persistentDataPath};
        var settings=InputSystem.settings;var oldFocus=settings.editorInputBehaviorInPlayMode;var oldBackground=settings.backgroundBehavior;
        settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        try
        {
            if(!report.profile.Contains("CreatorButton QA"))throw new Exception("Save isolation failed");
            Save("menu");await Wait(()=>{Dismiss();return IslandFrontEndFlow.Active&&IslandFrontEndFlow.Active.BackdropLoaded;});await Pause(2);Dismiss();
            Save("creator");await IslandFrontEndFlow.Active.BeginNewGameAsync();
            await Wait(()=>IslandFrontEndFlow.Active.IsCreatingCharacter&&!IslandFrontEndFlow.Active.IsBusy);
            var creator=UnityEngine.Object.FindFirstObjectByType<CharacterCreator>();await Wait(()=>creator&&creator.character&&creator.character.initalized&&!creator.character.isloading&&!creator.character.isMerging&&!creator.character.isUpdatingTexture);
            var confirm=UnityEngine.Object.FindFirstObjectByType<CharacterCreationConfirmButton>();var button=confirm.GetComponent<BaseButton>();
            Canvas.ForceUpdateCanvases();await Pause(1);
            var area=Bounds((RectTransform)button.transform);
            report.noOverlap=!creator.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<UnityEngine.UI.Selectable>(false)).Any(s=>s!=button&&!s.transform.IsChildOf(button.transform)&&area.Overlaps(Bounds((RectTransform)s.transform)));
            report.eventSystems=UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(e=>e.isActiveAndEnabled);
            var canvas=button.GetComponentInParent<Canvas>();Vector2 point=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,button.transform.TransformPoint(((RectTransform)button.transform).rect.center));
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
            report.hits=hits.Take(10).Select(h=>h.gameObject.name).ToArray();report.raycastHitsConfirm=hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject;
            ScreenCapture.CaptureScreenshot(Path.Combine(Reports,"creator-button-repaired.png"));await Pause(1);
            Save("pointer_click");int clicked=0;Action handler=()=>clicked++;button.Clicked+=handler;
            mouse=InputSystem.AddDevice<Mouse>("CreatorButtonRepairMouse");
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});await Pause(.5f);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));await Pause(.3f);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});await Pause(.5f);button.Clicked-=handler;report.clickedOnce=clicked==1;
            Save("wait_for_spawn");if(!report.clickedOnce)throw new Exception("Actual InputSystem pointer did not fire confirm");
            await Wait(()=>Singleton<PlayersManager>.TryGetInstance(out var p)&&p.LocalPlayer,180);report.playerSpawned=true;
            report.passed=report.noOverlap&&report.eventSystems==1&&report.raycastHitsConfirm&&report.clickedOnce&&report.playerSpawned;
            Save(report.passed?"PASS":"CHECK_FAILED");
        }
        catch(Exception e){report.error=e.ToString();Save("FAILED");}
        finally
        {
            if(mouse!=null)InputSystem.RemoveDevice(mouse);settings.editorInputBehaviorInPlayMode=oldFocus;settings.backgroundBehavior=oldBackground;
            SessionState.SetBool(Key+"active",false);EditorApplication.update-=Update;
            EditorApplication.ExitPlaymode();
            EditorApplication.delayCall+=()=>{PlayerSettings.productName=SessionState.GetString(Key+"product","Deep Blue Sushi");AssetDatabase.SaveAssets();};
        }
    }
}
