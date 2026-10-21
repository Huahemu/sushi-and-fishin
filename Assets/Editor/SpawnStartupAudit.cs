using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.States.MainMenu;
using SushiAndFish.FrontEnd;
using SushiAndFish.Casino;
using ECM2.Examples.FirstPerson;
using Object=UnityEngine.Object;
using PlayerSettings=UnityEditor.PlayerSettings;

// Explicit, editor-only diagnostic. Startup samples never move or change the
// character's velocity. An optional later check places it in the elevator.
[InitializeOnLoad]
public static class SpawnStartupAudit
{
    const string Key="SushiAndFish.SpawnStartupAudit.";
    static string Reports=>IslandFrontEndInventory.Reports;
    [Serializable] public class Sample
    {
        public float time,delta,timeScale,walkSpeed,swimSpeed;
        public Vector3 position,velocity;
        public Vector2 input;
        public string phase,mode,ground;
        public bool grounded,gate,casino;
    }
    [Serializable] public class Spawn
    {public string name,path;public Vector3 position,rotation;public string[] support;}
    [Serializable] public class Receipt
    {
        public string utc,stage,profile,error;
        public float prefabWalk,prefabSwim,prefabSprint;
        public Spawn[] spawns;
        public List<Sample> samples=new List<Sample>();
        public List<string> logs=new List<string>();
        public List<string> errors=new List<string>();
        public bool ready,passed,elevatorUp,elevatorReturned;
        public float idleHorizontalDrift,idleVerticalRange,walkPeak,sprintPeak,restSpeed;
    }
    static Receipt report;static bool running,exiting;static double lastSample,finished;
    static Keyboard keyboard;static InputDevice[] oldDevices;
    static InputSettings.EditorInputBehaviorInPlayMode oldFocus;
    static InputSettings.BackgroundBehavior oldBackground;
    static SpawnStartupAudit(){if(SessionState.GetBool(Key+"active",false))EditorApplication.update+=Tick;}
    public static void Start()
    {
        SessionState.SetBool(Key+"elevator",false);StartCore(true);
    }
    public static void RecheckWithElevator()
    {
        SessionState.SetBool(Key+"elevator",true);StartCore(false);
    }
    static void StartCore(bool inspect)
    {
        if(inspect)InspectSpawnArea();
        PlayerSettings.productName="SushiAndFish WorldRepair QA 20261016";
        SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"started",false);SessionState.SetBool(Key+"done",false);
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
    }
    static void Save(string stage)
    {report.stage=stage;File.WriteAllText(Path.Combine(Reports,"spawn-startup-audit.json"),JsonUtility.ToJson(report,true));}
    static object Prior(string method)=>typeof(WorldIssuePlayAudit).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
    static async UniTask Pause(float seconds)=>await UniTask.Delay(TimeSpan.FromSeconds(seconds),ignoreTimeScale:true);
    static async UniTask Wait(Func<bool> condition,int seconds=240)
    {var end=DateTime.UtcNow.AddSeconds(seconds);while(!condition()){if(DateTime.UtcNow>end)throw new TimeoutException(report.stage);await UniTask.Yield();}}
    [Serializable] public class AreaReceipt {public string utc;public List<string> rows=new List<string>();}
    public static void InspectSpawnArea()
    {
        var area=new AreaReceipt{utc=DateTime.UtcNow.ToString("o")};
        var point=new Vector3(116.45f,.71f,57.67f);
        var gs=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene);
        var ds=EditorSceneManager.OpenScene(IslandFrontEndBuilder.DressingRoot+"/100_Dressing_City_01.unity",OpenSceneMode.Additive);
        foreach(var s in new[]{gs,ds})foreach(var root in s.GetRootGameObjects())
            foreach(var d in root.GetComponentsInChildren<PaperPixelGames.Core.DressingSceneSystem.DressingSceneRoot>(true))d.ActivateAllImmediate();
        Physics.SyncTransforms();
        foreach(var h in Physics.RaycastAll(point+Vector3.up*20,Vector3.down,40,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            area.rows.Add("current ray: "+IslandFrontEndInventory.PathOf(h.transform)+" y="+h.point.y);
        foreach(var t in Terrain.activeTerrains)
        {
            var local=point-t.transform.position;var size=t.terrainData.size;
            if(local.x<0||local.z<0||local.x>size.x||local.z>size.z)continue;
            float x=local.x/size.x,z=local.z/size.z;
            area.rows.Add("current terrain "+t.name+" height="+(t.terrainData.GetInterpolatedHeight(x,z)+t.transform.position.y));
            var src=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/TerrainData/"+t.name+".asset");
            if(src)area.rows.Add("source terrain "+t.name+" height="+(src.GetInterpolatedHeight(x,z)+t.transform.position.y));
        }
        foreach(var s in new[]{gs,ds})foreach(var r in s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).Where(r=>r.bounds.SqrDistance(point)<100).OrderBy(r=>r.bounds.SqrDistance(point)).Take(25))
            area.rows.Add("current nearby "+IslandFrontEndInventory.PathOf(r.transform)+" bounds="+r.bounds+" collider="+r.GetComponent<Collider>());
        var original=EditorSceneManager.OpenScene(ScreenshotFollowupRepair.Source);
        foreach(var root in original.GetRootGameObjects())
            foreach(var d in root.GetComponentsInChildren<PaperPixelGames.Core.DressingSceneSystem.DressingSceneRoot>(true))d.ActivateAllImmediate();
        Physics.SyncTransforms();
        foreach(var h in Physics.RaycastAll(point+Vector3.up*20,Vector3.down,40,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            area.rows.Add("source ray: "+IslandFrontEndInventory.PathOf(h.transform)+" y="+h.point.y);
        foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(r=>r.bounds.SqrDistance(point)<100).OrderBy(r=>r.bounds.SqrDistance(point)).Take(35))
            area.rows.Add("source nearby "+IslandFrontEndInventory.PathOf(r.transform)+" bounds="+r.bounds+" collider="+r.GetComponent<Collider>());
        File.WriteAllText(Path.Combine(Reports,"spawn-area-inspection.json"),JsonUtility.ToJson(area,true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
    }
    static void Tick()
    {
        if(EditorApplication.isPlaying&&Application.isPlaying&&!SessionState.GetBool(Key+"started",false))
        {SessionState.SetBool(Key+"started",true);Run().Forget();}
        if(running&&EditorApplication.isPlaying&&EditorApplication.timeSinceStartup-lastSample>.05)
        {lastSample=EditorApplication.timeSinceStartup;SamplePlayer();}
        if(!EditorApplication.isPlayingOrWillChangePlaymode&&SessionState.GetBool(Key+"done",false)&&!exiting)
        {
            exiting=true;PlayerSettings.productName="Deep Blue Sushi";
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");AssetDatabase.SaveAssets();
            SessionState.SetBool(Key+"active",false);finished=EditorApplication.timeSinceStartup;
        }
        if(exiting&&EditorApplication.timeSinceStartup-finished>3)EditorApplication.Exit(0);
    }
    static void Collect(string message,string stack,LogType type)
    {
        if(report==null)return;
        if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors.Add(message+"\n"+stack);
        else if(message.Contains("Teleport")||message.Contains("teleport")||message.Contains("[Spawn]")||message.Contains("Initial locomotion"))report.logs.Add(message);
    }
    static void SamplePlayer()
    {
        if(!Singleton<PlayersManager>.TryGetInstance(out var manager)||!manager.LocalPlayer)return;
        var p=manager.LocalPlayer;var c=p.FirstPersonCharacter;if(!c)return;
        var ground=Physics.RaycastAll(p.transform.position+Vector3.up*.4f,Vector3.down,6,~0,QueryTriggerInteraction.Ignore)
            .Where(h=>!h.transform.IsChildOf(p.transform)).OrderBy(h=>h.distance).FirstOrDefault();
        report.samples.Add(new Sample{phase=report.stage,time=Time.realtimeSinceStartup,delta=Time.deltaTime,timeScale=Time.timeScale,
            position=p.transform.position,velocity=c.velocity,walkSpeed=c.maxWalkSpeed,swimSpeed=c.maxSwimSpeed,
            mode=c.movementMode.ToString(),grounded=c.IsGrounded(),input=p.GetComponent<FirstPersonCharacterInput>().InputMove,
            gate=Object.FindObjectsByType<PaperPixelGames.Networking.GameStartGate>(FindObjectsSortMode.None).Any(g=>g.IsGameStarted),
            casino=ParkCasinoIntegration.Instance&&ParkCasinoIntegration.Instance.Ready,
            ground=ground.collider?ground.collider.name+" y="+ground.point.y+" layer="+ground.collider.gameObject.layer:"none"});
    }
    static async UniTask Run()
    {
        report=new Receipt{utc=DateTime.UtcNow.ToString("o"),profile=Application.persistentDataPath};
        Application.logMessageReceived+=Collect;running=true;
        oldFocus=InputSystem.settings.editorInputBehaviorInPlayMode;oldBackground=InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        try
        {
            if(!report.profile.Contains("WorldRepair QA"))throw new Exception("QA profile isolation failed");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/PlayerNGO.prefab");
            var pc=prefab.GetComponent<FirstPersonCharacter>();
            report.prefabWalk=pc.maxWalkSpeed;report.prefabSwim=pc.maxSwimSpeed;
            report.prefabSprint=prefab.GetComponent<ECM2.Walkthrough.Ex42.SprintAbility>().maxSprintSpeed;
            var pm=Singleton<PlayersManager>.Instance;
            report.spawns=Enumerable.Range(0,5).Select(i=>pm.GetPlayerSpawnPointForSlot(i)).Where(t=>t).Select(t=>new Spawn{
                name=t.name,path=IslandFrontEndInventory.PathOf(t),position=t.position,rotation=t.eulerAngles,
                support=Physics.RaycastAll(t.position+Vector3.up*10,Vector3.down,25,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance)
                    .Select(h=>h.collider.name+" y="+h.point.y+" layer="+h.collider.gameObject.layer).Take(12).ToArray()}).ToArray();
            Save("MENU");await Wait(()=>{Prior("Dismiss");return IslandFrontEndFlow.Active&&IslandFrontEndFlow.Active.BackdropLoaded;});
            await Pause(2);Prior("Dismiss");
            await Singleton<MainMenuManager>.Instance.CustomizationSceneHost.PreloadAsync();IslandFrontEndFlow.Active.ShowLoadGames();await Pause(1);
            Resources.FindObjectsOfTypeAll<SaveSlot>().Where(s=>s&&s.gameObject.scene.name=="MainMenuState"&&s.ContainsSave).OrderBy(s=>s.SlotIndex).First().LaunchOriginalSlot();
            Save("BOOTSTRAP");await Wait(()=>(bool)Prior("Ready"));report.ready=true;
            Save("WAIT_CASINO");await Wait(()=>ParkCasinoIntegration.Instance&&ParkCasinoIntegration.Instance.Ready,180);
            Save("IDLE");await Pause(8);Save("BEFORE_WALK");
            keyboard=InputSystem.AddDevice<Keyboard>("Startup audit keyboard");
            var action=Singleton<InputManager>.Instance.InputHandler.InputMap.asset;oldDevices=action.devices?.ToArray();action.devices=new InputDevice[]{keyboard};
            Save("WALK");InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.W));await Pause(1.2f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Pause(1.2f);
            Save("SPRINT");InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.S,UnityEngine.InputSystem.Key.LeftShift));await Pause(1.2f);
            Save("REST");InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Pause(1.5f);
            var idle=report.samples.Where(s=>s.phase=="IDLE").ToArray();
            var first=idle.First().position;
            report.idleHorizontalDrift=idle.Max(s=>Vector3.ProjectOnPlane(s.position-first,Vector3.up).magnitude);
            report.idleVerticalRange=idle.Max(s=>s.position.y)-idle.Min(s=>s.position.y);
            report.walkPeak=report.samples.Where(s=>s.phase=="WALK").Max(s=>Vector3.ProjectOnPlane(s.velocity,Vector3.up).magnitude);
            report.sprintPeak=report.samples.Where(s=>s.phase=="SPRINT").Max(s=>Vector3.ProjectOnPlane(s.velocity,Vector3.up).magnitude);
            report.restSpeed=report.samples.Last().velocity.magnitude;
            report.passed=report.errors.Count==0&&report.idleHorizontalDrift<.02f&&report.idleVerticalRange<.03f&&idle.All(s=>s.grounded&&s.velocity.magnitude<.05f)&&
                Mathf.Abs(report.walkPeak-3)<.1f&&Mathf.Abs(report.sprintPeak-7)<.1f&&report.restSpeed<.05f&&
                !report.logs.Any(s=>s.Contains("TeleportPlayer to"));
            if(SessionState.GetBool(Key+"elevator",false))
            {
                Save("ELEVATOR_REGRESSION");
                var casino=ParkCasinoIntegration.Instance;
                var elevator=Object.FindFirstObjectByType<SushiAndFish.Casino.Original.ElevatorManager>();
                casino.HostPlayer.TeleportPlayer(elevator.playerSpawnPosition.position-Vector3.up*(.4f*casino.CurrentScale),true);
                casino.HostPlayer.FirstPersonCharacter.SetVelocity(Vector3.zero);await Pause(.6f);
                var buttons=SushiAndFish.Casino.Original.CasinoLab.CasinoLabBootstrap.Field<List<Transform>>(elevator,"buttonList");
                buttons[1].GetComponent<SushiAndFish.Casino.Original.InteractableEventTrigger>().ServerOnInteract(casino.LogicInteractor);
                await Wait(()=>!elevator.isTeleporting&&casino.CurrentFloor==1&&casino.Bootstrap.ready,60);await Pause(1);
                report.elevatorUp=casino.HostPlayer.FirstPersonCharacter.IsGrounded()&&casino.HostPlayer.transform.position.y>999;
                Save("ELEVATOR_RETURN");
                buttons[0].GetComponent<SushiAndFish.Casino.Original.InteractableEventTrigger>().ServerOnInteract(casino.LogicInteractor);
                await Wait(()=>!elevator.isTeleporting&&casino.CurrentFloor==0&&casino.Bootstrap.ready,60);await Pause(1);
                report.elevatorReturned=casino.HostPlayer.FirstPersonCharacter.IsGrounded()&&casino.HostPlayer.transform.position.y<10&&casino.HostPlayer.FirstPersonCharacter.velocity.magnitude<.05f;
                report.passed&=report.elevatorUp&&report.elevatorReturned&&report.errors.Count==0;
            }
            Save(report.passed?"PASS":"CHECK_FAILED");
        }
        catch(Exception e){report.passed=false;report.error=e.ToString();Save("FAILED");}
        finally
        {
            running=false;if(keyboard!=null){if(Singleton<InputManager>.TryGetInstance(out var input)&&input.InputHandler!=null)input.InputHandler.InputMap.asset.devices=oldDevices;InputSystem.RemoveDevice(keyboard);}
            InputSystem.settings.editorInputBehaviorInPlayMode=oldFocus;InputSystem.settings.backgroundBehavior=oldBackground;
            Application.logMessageReceived-=Collect;SessionState.SetBool(Key+"done",true);EditorApplication.ExitPlaymode();
        }
    }
}
