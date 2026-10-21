using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States.MainMenu;
using SushiAndFish.FrontEnd;

[InitializeOnLoad]
public static class UnifiedPlayerTeardownAudit
{
    const string Key="SushiAndFish.UnifiedTeardown.";
    static string Reports=>IslandRevision20261018.Reports;
    [Serializable] public class Result {public string utc,profile,stage,error;public bool ready,passed;public string[] errors;}
    static double finish;
    static UnifiedPlayerTeardownAudit(){if(SessionState.GetBool(Key+"active",false)){EditorApplication.update+=Tick;Application.logMessageReceived+=Collect;}}
    public static void Start(){string errors=Path.Combine(Reports,"unified-player-teardown-errors.txt");if(File.Exists(errors))File.Delete(errors);PlayerSettings.productName="SushiAndFish WorldRepair QA 20261016";SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"started",false);SessionState.SetBool(Key+"done",false);SessionState.SetBool(Key+"exit",false);EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");EditorApplication.update+=Tick;Application.logMessageReceived+=Collect;EditorApplication.EnterPlaymode();}
    static object Prior(string name)=>typeof(WorldIssuePlayAudit).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
    static void Collect(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)File.AppendAllText(Path.Combine(Reports,"unified-player-teardown-errors.txt"),message+"\n"+stack+"\n");}
    static void Tick(){if(EditorApplication.isPlaying&&!SessionState.GetBool(Key+"started",false)){SessionState.SetBool(Key+"started",true);Run().Forget();}if(!EditorApplication.isPlayingOrWillChangePlaymode&&SessionState.GetBool(Key+"done",false)&&!SessionState.GetBool(Key+"exit",false)){var result=JsonUtility.FromJson<Result>(File.ReadAllText(Path.Combine(Reports,"unified-player-teardown-audit.json")));var path=Path.Combine(Reports,"unified-player-teardown-errors.txt");result.errors=File.Exists(path)?File.ReadAllLines(path):new string[0];result.passed=result.ready&&string.IsNullOrEmpty(result.error)&&result.errors.Length==0;result.stage=result.passed?"PASS":"FAILED";File.WriteAllText(Path.Combine(Reports,"unified-player-teardown-audit.json"),JsonUtility.ToJson(result,true));PlayerSettings.productName="Deep Blue Sushi";EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");AssetDatabase.SaveAssets();SessionState.SetBool(Key+"exit",true);finish=EditorApplication.timeSinceStartup;}if(SessionState.GetBool(Key+"exit",false)&&EditorApplication.timeSinceStartup-finish>4)EditorApplication.Exit(0);}
    static async UniTask Wait(Func<bool> predicate,int seconds=210){var end=DateTime.UtcNow.AddSeconds(seconds);while(!predicate()){if(DateTime.UtcNow>end)throw new TimeoutException("Teardown bootstrap");await UniTask.Yield();}}
    static async UniTaskVoid Run(){var result=new Result{utc=DateTime.UtcNow.ToString("o"),profile=Application.persistentDataPath,stage="BOOTSTRAP"};try{if(!result.profile.Contains("WorldRepair QA"))throw new Exception("QA profile required");await Wait(()=>{Prior("Dismiss");return IslandFrontEndFlow.Active&&IslandFrontEndFlow.Active.BackdropLoaded;});await UniTask.Delay(TimeSpan.FromSeconds(1),ignoreTimeScale:true);var menu=Singleton<MainMenuManager>.Instance;await menu.CustomizationSceneHost.PreloadAsync();IslandFrontEndFlow.Active.ShowLoadGames();await UniTask.Delay(TimeSpan.FromSeconds(1),ignoreTimeScale:true);Resources.FindObjectsOfTypeAll<SaveSlot>().Where(s=>s&&s.gameObject.scene.name=="MainMenuState"&&s.ContainsSave).OrderBy(s=>s.SlotIndex).First().LaunchOriginalSlot();await Wait(()=>(bool)Prior("Ready"));await UniTask.Delay(TimeSpan.FromSeconds(2),ignoreTimeScale:true);result.ready=true;result.stage="EXITING_PLAY";}catch(Exception e){result.error=e.ToString();}finally{File.WriteAllText(Path.Combine(Reports,"unified-player-teardown-audit.json"),JsonUtility.ToJson(result,true));SessionState.SetBool(Key+"done",true);EditorApplication.ExitPlaymode();}}
}
