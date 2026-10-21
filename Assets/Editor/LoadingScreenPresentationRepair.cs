using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using PaperPixelGames.Framework.UI;
using SushiAndFish.FrontEnd;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using Object=UnityEngine.Object;
using PlayerSettings=UnityEditor.PlayerSettings;

// Bounded presentation edit and preview; normal editor sessions never auto-run it.
[InitializeOnLoad]
public static class LoadingScreenPresentationRepair
{
    const string Key="SushiAndFish.LoadingPresentation.";
    static string Reports=>IslandFrontEndInventory.Reports;
    [Serializable] public class Receipt
    {public string utc,stage,error,titleBefore,titleAfter,profile;public bool saved,noPreview,noShadow,noImageLoop,passed;}
    static Receipt report;
    static LoadingScreenPresentationRepair()
    {if(SessionState.GetBool(Key+"active",false))EditorApplication.playModeStateChanged+=Changed;}
    static void Set(SerializedObject so,string key,bool value)=>so.FindProperty(key).boolValue=value;
    static void Save(string stage)
    {report.stage=stage;File.WriteAllText(Path.Combine(Reports,"loading-presentation.json"),JsonUtility.ToJson(report,true));}
    public static void ApplyAndPreview()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play before editing the loading screen");
        var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Main.unity");
        if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var loading=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LoadingScreenBehaviour>(true)).Single();
        var so=new SerializedObject(loading);Set(so,"_useRandomImages",false);Set(so,"_changeImageImmediatelyOnShow",false);so.ApplyModifiedPropertiesWithoutUndo();
        var content=(RectTransform)so.FindProperty("_contentRoot").objectReferenceValue;
        content.Find("Mask").gameObject.SetActive(false);content.Find("Shadow").gameObject.SetActive(false);
        var table=AssetDatabase.LoadAssetAtPath<StringTable>("Assets/Settings/Localization/Tables/UiTable/UiTable_zh-CN.asset");
        table.GetEntry("loading.status.waiting_for_players").Value="加载中";EditorUtility.SetDirty(table);
        EditorUtility.SetDirty(loading);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        SessionState.SetString(Key+"product",PlayerSettings.productName);PlayerSettings.productName="SushiAndFish WorldRepair QA 20261016";
        SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"started",false);
        EditorApplication.playModeStateChanged+=Changed;EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode&&!SessionState.GetBool(Key+"started",false))
        {SessionState.SetBool(Key+"started",true);Preview().Forget();}
        if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"active",false))
        {
            SessionState.SetBool(Key+"active",false);EditorApplication.playModeStateChanged-=Changed;
            PlayerSettings.productName=SessionState.GetString(Key+"product","Deep Blue Sushi");AssetDatabase.SaveAssets();
        }
    }
    static async UniTask Pause(float seconds)=>await UniTask.Delay(TimeSpan.FromSeconds(seconds),ignoreTimeScale:true);
    static T Field<T>(LoadingScreenBehaviour loading,string name)where T:Object=>(T)new SerializedObject(loading).FindProperty(name).objectReferenceValue;
    static async UniTask Preview()
    {
        report=new Receipt{utc=DateTime.UtcNow.ToString("o"),saved=true,profile=Application.persistentDataPath};
        try
        {
            if(!report.profile.Contains("WorldRepair QA"))throw new Exception("Preview profile is not isolated");
            Save("WAIT_MENU");var deadline=DateTime.UtcNow.AddMinutes(4);
            while(!LocalizationSettings.InitializationOperation.IsDone||!IslandFrontEndFlow.Active||!IslandFrontEndFlow.Active.BackdropLoaded)
            {if(DateTime.UtcNow>deadline)throw new TimeoutException("Loading preview menu");await UniTask.Yield();}
            var locale=LocalizationSettings.AvailableLocales.Locales.First(l=>l.Identifier.Code=="zh-CN");LocalizationSettings.SelectedLocale=locale;
            LoadingScreen.ShowLocalized("loading.status.waiting_for_players","Waiting for everyone to finish loading...",string.Empty,string.Empty);
            await Pause(1);
            var loading=Object.FindFirstObjectByType<LoadingScreenBehaviour>(FindObjectsInactive.Include);
            var so=new SerializedObject(loading);var content=(RectTransform)so.FindProperty("_contentRoot").objectReferenceValue;
            report.titleBefore=Field<TextMeshProUGUI>(loading,"_titleText").text;
            report.noPreview=!content.Find("Mask").gameObject.activeSelf;
            report.noShadow=!content.Find("Shadow").gameObject.activeSelf;
            report.noImageLoop=!so.FindProperty("_useRandomImages").boolValue;
            Save("WAIT_IMAGE_INTERVAL");await Pause(6);
            report.titleAfter=Field<TextMeshProUGUI>(loading,"_titleText").text;
            report.passed=report.titleBefore=="加载中"&&report.titleAfter=="加载中"&&report.noPreview&&report.noShadow&&report.noImageLoop;
            ScreenCapture.CaptureScreenshot(Path.Combine(Reports,"loading-screen-simplified.png"));await Pause(1);
            LoadingScreen.Hide();Save(report.passed?"PASS":"CHECK_FAILED");
        }
        catch(Exception e){report.error=e.ToString();report.passed=false;Save("FAILED");}
        finally{EditorApplication.ExitPlaymode();}
    }
}
