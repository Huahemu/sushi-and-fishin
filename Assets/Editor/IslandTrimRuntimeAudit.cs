using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Bozo.ModularCharacters;
using SushiAndFish.FrontEnd;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States.MainMenu;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Networking;
using PaperPixelGames.Core.SaveSystem;

[InitializeOnLoad]
public static class IslandTrimRuntimeAudit
{
    const string Key="SushiAndFish.TrimAudit.";
    static string Reports=>IslandFrontEndInventory.Reports;
    static readonly List<string> errors=new List<string>();
    [Serializable] public class ObjectRow {public string path;public int objects,renderers,colliders;}
    [Serializable] public class Report {public string utc,stage,error,profile,captureCamera;public string[] errors,scenes,navmesh;public ObjectRow[] stores;public bool passed,originalGameStarted,removedAreaAbsent;public int terrains;}
    static Report report;
    [Serializable] public class FootprintSource {public BuildingFootprintBuilder.Site[] sites;}
    [Serializable] public class FootprintReport {public string utc,profile,shader;public int expected,renderers,colliders;public bool passed,shaderSupported;}
    static IslandTrimRuntimeAudit(){if(SessionState.GetBool(Key+"active",false)){EditorApplication.update+=Update;Application.logMessageReceived+=Collect;}}
    public static void Start()
    {
        if(Application.isBatchMode)throw new InvalidOperationException("The actual game frame loop is required");
        string original=PlayerSettings.productName;
        if(original.Contains(" QA "))original="Deep Blue Sushi";
        SessionState.SetString(Key+"product",original);
        PlayerSettings.productName=File.ReadAllText(Path.Combine(Reports,"island-trim-qa-product.txt")).Trim();
        SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"started",false);SessionState.SetBool(Key+"done",false);
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);
        EditorApplication.update+=Update;Application.logMessageReceived+=Collect;EditorApplication.EnterPlaymode();
    }
    static void Collect(string m,string s,LogType t){if((t==LogType.Error||t==LogType.Exception||t==LogType.Assert)&&errors.Count<40)errors.Add(m+"\n"+s);}
    static void Update()
    {
        if(!SessionState.GetBool(Key+"active",false))return;
        if(EditorApplication.isPlaying&&!SessionState.GetBool(Key+"started",false)){SessionState.SetBool(Key+"started",true);Scenario().Forget();}
        if(!EditorApplication.isPlayingOrWillChangePlaymode&&!Application.isPlaying&&SessionState.GetBool(Key+"done",false))
        {
            PlayerSettings.productName=SessionState.GetString(Key+"product","Deep Blue Sushi");SessionState.SetBool(Key+"active",false);
            EditorApplication.update-=Update;Application.logMessageReceived-=Collect;
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);AssetDatabase.SaveAssets();
        }
    }
    static void Stage(string s){report.stage=s;File.WriteAllText(Path.Combine(Reports,"island-trim-runtime-audit.json"),JsonUtility.ToJson(report,true));Debug.Log("ISLAND_TRIM_PLAY "+s);}
    static async UniTask Wait(Func<bool> predicate,string description,int seconds=180)
    {var start=DateTime.UtcNow;while(!predicate()){if((DateTime.UtcNow-start).TotalSeconds>seconds)throw new TimeoutException(description);await UniTask.Yield();}}
    static async UniTask Pause(double seconds)=>await UniTask.Delay(TimeSpan.FromSeconds(seconds),ignoreTimeScale:true);
    static void Dismiss()
    {
        var motion=UnityEngine.Object.FindFirstObjectByType<MotionSicknessPopup>();if(motion&&motion.IsVisible)typeof(MotionSicknessPopup).GetMethod("OnKeepDefaultsSelected",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(motion,null);
        var rapid=UnityEngine.Object.FindFirstObjectByType<RapidPressBindingPopup>();if(rapid&&rapid.IsVisible)rapid.UseMouse1AndClose();
        var patch=UnityEngine.Object.FindFirstObjectByType<PatchInfoPopup>();if(patch&&patch.IsVisible)patch.Dismiss();
        var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuManager>();if(menu){var popup=new SerializedObject(menu).FindProperty("_earlyAccessPopUp").objectReferenceValue as GameObject;if(popup&&popup.activeSelf)typeof(MainMenuManager).GetMethod("OnClickOkButton",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(menu,null);}
    }
    static bool Ready()=>SceneManager.GetSceneByName("GameState").isLoaded&&Singleton<PlayersManager>.TryGetInstance(out var p)&&p.LocalPlayer&&Time.timeScale>0.9f
        &&UnityEngine.Object.FindObjectsByType<GameStartGate>(FindObjectsSortMode.None).Any(g=>g.IsGameStarted&&g.BootstrapCompletedClientCount>=1)
        &&!UnityEngine.Object.FindObjectsByType<PaperPixelGames.Framework.UI.LoadingScreenBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(l=>l.IsVisible);
    static async UniTask Capture(string name,Vector3 position,Vector3 target,float ortho=0)
    {
        var camera=Camera.main;if(!camera)throw new InvalidOperationException("Original player camera absent");
        var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;
        var data=camera.GetUniversalAdditionalCameraData();bool shadows=data.renderShadows;
        data.renderShadows=position.y<40;
        report.captureCamera="original player camera="+camera.name+" mask="+camera.cullingMask;
        for(int frame=0;frame<6;frame++)
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
            if(frame==3)ScreenCapture.CaptureScreenshot(Path.Combine(Reports,name));
        }
        await Pause(1);
        camera.transform.SetPositionAndRotation(oldPosition,oldRotation);data.renderShadows=shadows;
    }
    static async UniTaskVoid Scenario()
    {
        report=new Report{utc=DateTime.UtcNow.ToString("o"),profile=Application.persistentDataPath};
        try
        {
            if(!report.profile.Contains("IslandTrim QA"))throw new InvalidOperationException("Formal save isolation failed");
            Stage("menu");await Wait(()=>{Dismiss();return IslandFrontEndFlow.Active&&IslandFrontEndFlow.Active.BackdropLoaded;},"Trimmed island menu did not load");await Pause(7);Dismiss();
            ScreenCapture.CaptureScreenshot(Path.Combine(Reports,"island-trim-menu.png"));await Pause(2);
            if(Singleton<SaveManager>.Instance.SaveSlotsData.All(s=>s.GameProgress.HasSave))
            {
                Stage("load_existing_isolated_world");IslandFrontEndFlow.Active.ShowLoadGames();await Pause(1);
                var slot=Resources.FindObjectsOfTypeAll<SaveSlot>().First(s=>s&&s.gameObject.scene.name=="MainMenuState"&&s.ContainsSave);
                slot.LaunchOriginalSlot();
            }
            else
            {
                Stage("creator");await IslandFrontEndFlow.Active.BeginNewGameAsync();
                var creator=UnityEngine.Object.FindFirstObjectByType<CharacterCreator>();
                await Wait(()=>creator&&creator.character&&creator.character.initalized&&!creator.character.isloading&&!creator.character.isMerging&&!creator.character.isUpdatingTexture,"Original character creation not ready");
                Stage("original_game_start");await IslandFrontEndFlow.Active.ConfirmNewCharacterAsync();
            }
            await Wait(Ready,"Original game did not start after island edit",240);await Pause(10);
            report.originalGameStarted=true;report.scenes=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i).name).ToArray();
            var all=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var rows=new List<ObjectRow>();
            foreach(var path in new[]{"---Restaurant","Root/Comps/GabeMarket","Root/Comps/MarinaMarket"})
            {
                var t=all.FirstOrDefault(t=>IslandFrontEndInventory.PathOf(t)==path&&t.gameObject.scene.name!="IslandMenuBackdrop");if(!t)throw new InvalidOperationException("Required store missing: "+path);
                rows.Add(new ObjectRow{path=path,objects=t.GetComponentsInChildren<Transform>(true).Length,renderers=t.GetComponentsInChildren<Renderer>(true).Length,colliders=t.GetComponentsInChildren<Collider>(true).Length});
            }
            report.stores=rows.ToArray();report.terrains=all.Count(t=>t.GetComponent<Terrain>()&&t.gameObject.scene.name=="GameState");
            report.removedAreaAbsent=!all.Any(t=>t.gameObject.scene.name=="GameState"&&t.name=="BossParent"||t.gameObject.scene.name=="100_Dressing_City_01"&&(t.name=="TunnelComp"||t.name=="CompBridge"||t.name=="LighthouseComp"));
            report.navmesh=all.SelectMany(t=>t.GetComponents<MonoBehaviour>()).Where(b=>b&&b.GetType().Name=="NavMeshSurface").Select(b=>IslandFrontEndInventory.PathOf(b.transform)+" "+EditorJsonUtility.ToJson(b)).ToArray();
            Stage("capture_map_and_stores");
            var footprintRoot=all.FirstOrDefault(t=>t.gameObject.scene.name=="GameState"&&t.name=="RemovedBuildingFootprints");
            if(footprintRoot)
            {
                var expected=JsonUtility.FromJson<FootprintSource>(File.ReadAllText(Path.Combine(Reports,"building-footprint-markers.json")));
                var renderers=footprintRoot.GetComponentsInChildren<Renderer>(true);var material=renderers.First().sharedMaterial;
                var footprints=new FootprintReport{utc=DateTime.UtcNow.ToString("o"),profile=report.profile,expected=expected.sites.Length,renderers=renderers.Length,colliders=footprintRoot.GetComponentsInChildren<Collider>(true).Length,shader=material.shader.name,shaderSupported=material.shader.isSupported};
                footprints.passed=footprints.expected==24&&footprints.renderers==footprints.expected&&footprints.colliders==0&&footprints.shaderSupported&&footprints.shader=="SushiAndFish/SoftYellowDashedFootprints";
                File.WriteAllText(Path.Combine(Reports,"building-footprint-play-audit.json"),JsonUtility.ToJson(footprints,true));
                if(!footprints.passed)throw new InvalidOperationException("Footprint runtime structure or shader check failed");
                foreach(var id in new[]{"RemovedBuildingFootprint_08","RemovedBuildingFootprint_12","RemovedBuildingFootprint_19"})
                {
                    var site=expected.sites.Single(s=>s.id==id);
                    await Capture("building-footprints-"+id+".png",site.centre+Vector3.up*15-site.forward*6,site.centre);
                }
            }
            await Capture("island-trim-overhead.png",new Vector3(-26,250,-16),new Vector3(-26,0,-16),105);
            await Capture("island-trim-oblique.png",new Vector3(-30,130,160),new Vector3(-26,1,-16));
            await Capture("island-trim-blue-market.png",new Vector3(9,8,6),new Vector3(-6,5,-3));
            await Capture("island-trim-green-market.png",new Vector3(13,10,-5),new Vector3(-1,5,-19));
            await Capture("island-trim-restaurant.png",new Vector3(12,12,46),new Vector3(26,2,77));
            await Capture("island-trim-former-tunnel-shore.png",new Vector3(5,15,-60),new Vector3(5,0,-135));
            ScreenCapture.CaptureScreenshot(Path.Combine(Reports,"island-trim-player-spawn.png"));await Pause(2);
            report.errors=errors.ToArray();report.passed=errors.Count==0&&report.originalGameStarted&&report.removedAreaAbsent&&report.terrains==4;Stage(report.passed?"PASS":"CHECK_FAILED");
        }
        catch(Exception e){report.error=e.ToString();report.errors=errors.ToArray();Stage("FAILED");}
        finally{File.WriteAllText(Path.Combine(Reports,"island-trim-runtime-audit.json"),JsonUtility.ToJson(report,true));SessionState.SetBool(Key+"done",true);EditorApplication.ExitPlaymode();}
    }
}
