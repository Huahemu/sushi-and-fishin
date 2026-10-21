using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Bounded verification of the original bootstrap. Separate product identity
// directs all Application.persistentDataPath consumers away from user saves.
[InitializeOnLoad]
public static class RecoveryPlayAudit
{
    private const string Key = "DeepBlueSushi.RecoveryPlayAudit.";
    private static readonly List<string> errors = new List<string>();
    private static string Reports => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../reports"));
    [Serializable] private class Report
    {
        public string utc, persistentDataPath, screenshot, gameplayScreenshot, activeScene, launchMethod;
        public string[] scenes, errors;
        public bool reachedMenu, reachedGame, screenshotExists, gameplayScreenshotExists;
        public bool loadingVisible,waitForLoadingInput;
        public float timeScale;
        public int frames;
    }
    static RecoveryPlayAudit()
    {
        if (!SessionState.GetBool(Key + "active", false)) return;
        Application.logMessageReceived += Collect;
        EditorApplication.update += Update;
    }
    public static void Start() { Begin(false); }
    [MenuItem("Tools/Recovered original game/Run startup verification with isolated saves")]
    public static void StartAndLaunchGame() { Begin(true); }
    private static void Begin(bool launchGame)
    {
        Directory.CreateDirectory(Reports);
        RestoredRenderPipelineSupport.RegisterOriginalAssets();
        SessionState.SetString(Key + "product", PlayerSettings.productName);
        PlayerSettings.productName = "Deep Blue Sushi Recovery QA";
        if (!Application.persistentDataPath.EndsWith("Deep Blue Sushi Recovery QA", StringComparison.Ordinal))
            throw new InvalidOperationException("Could not isolate verification saves: " + Application.persistentDataPath);
        SessionState.SetString(Key + "data", Application.persistentDataPath);
        SessionState.SetString(Key + "start", DateTime.UtcNow.ToString("o"));
        SessionState.SetInt(Key + "phase", 0);
        SessionState.SetBool(Key + "launchGame", launchGame);
        SessionState.SetBool(Key + "active", true);
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
        Application.logMessageReceived += Collect;
        EditorApplication.update += Update;
        EditorApplication.EnterPlaymode();
    }
    private static void Collect(string message, string trace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (errors.Count < 80 && !errors.Contains(message)) errors.Add(message + "\n" + trace);
    }
    private static void Update()
    {
        if (!SessionState.GetBool(Key + "active", false)) return;
        double elapsed = (DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Key + "start", ""),
            null, System.Globalization.DateTimeStyles.RoundtripKind)).TotalSeconds;
        int phase = SessionState.GetInt(Key + "phase", 0);
        if (phase == 0 && EditorApplication.isPlaying && elapsed > 45)
        {
            var motion=UnityEngine.Object.FindFirstObjectByType<PaperPixelGames.Framework.States.MainMenu.MotionSicknessPopup>();
            if(motion && motion.IsVisible)typeof(PaperPixelGames.Framework.States.MainMenu.MotionSicknessPopup).GetMethod("OnKeepDefaultsSelected",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(motion,null);
            var rapid=UnityEngine.Object.FindFirstObjectByType<PaperPixelGames.Framework.States.MainMenu.RapidPressBindingPopup>();
            if(rapid && rapid.IsVisible)rapid.UseMouse1AndClose();
            var menu=UnityEngine.Object.FindFirstObjectByType<PaperPixelGames.Framework.States.MainMenu.MainMenuManager>();
            if(menu)
            {
                var field=typeof(PaperPixelGames.Framework.States.MainMenu.MainMenuManager).GetField("_earlyAccessPopUp",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var popup=field.GetValue(menu) as GameObject;
                if(popup && popup.activeSelf)typeof(PaperPixelGames.Framework.States.MainMenu.MainMenuManager).GetMethod("OnClickOkButton",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(menu,null);
            }
            var patch=UnityEngine.Object.FindFirstObjectByType<PaperPixelGames.Framework.States.MainMenu.PatchInfoPopup>();
            if(patch && patch.IsVisible)patch.Dismiss();
            RecoveryFontDiagnostic.CaptureRuntime();
            var paths = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; ++i) paths.Add(SceneManager.GetSceneAt(i).name);
            string shot = Path.Combine(Reports, "original-bootstrap-play.png");
            var report = new Report { utc = DateTime.UtcNow.ToString("o"),
                persistentDataPath = Application.persistentDataPath, screenshot = shot,
                activeScene = SceneManager.GetActiveScene().name, scenes = paths.ToArray(),
                reachedMenu = paths.Contains("MainMenuState"), frames = Time.frameCount, errors = errors.ToArray() };
            File.WriteAllText(Path.Combine(Reports, "unity-original-play-audit.json"), JsonUtility.ToJson(report, true));
            ScreenCapture.CaptureScreenshot(shot);
            SessionState.SetInt(Key + "phase", 1);
            SessionState.SetString(Key + "capture", DateTime.UtcNow.ToString("o"));
        }
        else if (phase == 1 && (DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Key + "capture", "")).ToUniversalTime()).TotalSeconds > 4)
        {
            if(SessionState.GetBool(Key+"launchGame",false))
            {
                var menu=UnityEngine.Object.FindFirstObjectByType<PaperPixelGames.Framework.States.MainMenu.MainMenuManager>();
                typeof(PaperPixelGames.Framework.States.MainMenu.MainMenuManager).GetMethod("OnClickSingleplayer",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(menu,null);
                var slots=UnityEngine.Object.FindObjectsByType<PaperPixelGames.Framework.States.MainMenu.SaveSlot>(FindObjectsInactive.Include,FindObjectsSortMode.None);
                PaperPixelGames.Framework.States.MainMenu.SaveSlot selected=null; int lowest=int.MaxValue;
                var field=typeof(PaperPixelGames.Framework.States.MainMenu.SaveSlot).GetField("_saveSlotIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                foreach(var slot in slots) { int index=(int)field.GetValue(slot); if(index<lowest) {lowest=index;selected=slot;} }
                if(!selected) throw new InvalidOperationException("No original save-slot menu button was available");
                typeof(PaperPixelGames.Framework.States.MainMenu.SaveSlot).GetMethod("OnSaveSlotButtonClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(selected,null);
                SessionState.SetString(Key+"launch",DateTime.UtcNow.ToString("o"));
                SessionState.SetInt(Key+"phase",3);
            }
            else { SessionState.SetInt(Key + "phase", 2); EditorApplication.ExitPlaymode(); }
        }
        else if(phase==3 && (DateTime.UtcNow-DateTime.Parse(SessionState.GetString(Key+"launch","")).ToUniversalTime()).TotalSeconds>140)
        {
            var path=Path.Combine(Reports,"unity-original-play-audit.json");
            var report=JsonUtility.FromJson<Report>(File.ReadAllText(path));
            var paths=new List<string>(); for(int i=0;i<SceneManager.sceneCount;i++) paths.Add(SceneManager.GetSceneAt(i).name);
            report.scenes=paths.ToArray(); report.activeScene=SceneManager.GetActiveScene().name; report.reachedGame=paths.Contains("GameState");
            report.errors=errors.ToArray(); report.frames=Time.frameCount;
            report.timeScale=Time.timeScale;
            foreach(var loading in UnityEngine.Object.FindObjectsByType<Michsky.LSS.LSS_LoadingScreen>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(loading.gameObject.activeInHierarchy && loading.canvasGroup && loading.canvasGroup.alpha>0.05f){report.loadingVisible=true;report.waitForLoadingInput |= loading.waitForPlayerInput;}
            report.gameplayScreenshot=Path.Combine(Reports,"original-gameplay-start.png");
            report.launchMethod="Invoked the original single-player and save-slot button callbacks in an isolated QA save slot; this is an automated runtime test.";
            ScreenCapture.CaptureScreenshot(report.gameplayScreenshot);
            File.WriteAllText(path,JsonUtility.ToJson(report,true));
            SessionState.SetInt(Key+"phase",4); SessionState.SetString(Key+"capture",DateTime.UtcNow.ToString("o"));
        }
        else if(phase==4 && (DateTime.UtcNow-DateTime.Parse(SessionState.GetString(Key+"capture","")).ToUniversalTime()).TotalSeconds>4)
        {
            SessionState.SetInt(Key+"phase",2); EditorApplication.ExitPlaymode();
        }
        else if (phase == 2 && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.SetBool(Key + "active", false);
            PlayerSettings.productName = SessionState.GetString(Key + "product", "Deep Blue Sushi");
            var path = Path.Combine(Reports, "unity-original-play-audit.json");
            var report = JsonUtility.FromJson<Report>(File.ReadAllText(path));
            report.screenshotExists = File.Exists(report.screenshot);
            report.gameplayScreenshotExists = !string.IsNullOrEmpty(report.gameplayScreenshot) && File.Exists(report.gameplayScreenshot);
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log("RECOVERY_ORIGINAL_PLAY_COMPLETE menu=" + report.reachedMenu + " errors=" + report.errors.Length);
            if(RecoveryEditorAutomation.KeepOpen){EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);return;}
            EditorApplication.Exit(report.reachedMenu && report.screenshotExists && report.errors.Length == 0 && (!SessionState.GetBool(Key+"launchGame",false) || report.reachedGame && report.gameplayScreenshotExists) ? 0 : 2);
        }
        else if (elapsed > 260)
        {
            File.WriteAllText(Path.Combine(Reports, "unity-original-play-timeout.txt"), "Timed out in phase " + phase);
            SessionState.SetBool(Key + "active", false);
            PlayerSettings.productName = SessionState.GetString(Key + "product", "Deep Blue Sushi");
            EditorApplication.Exit(3);
        }
    }
}
