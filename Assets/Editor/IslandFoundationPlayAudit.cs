using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections.Generic;
using Cysharp.Threading.Tasks;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;
using PaperPixelGames.Core.Managers;using PaperPixelGames.Framework.Patterns;using PaperPixelGames.Framework.Input;using PaperPixelGames.Framework.States.MainMenu;using SushiAndFish.FrontEnd;using SushiAndFish.Casino;
using Object=UnityEngine.Object;using PlayerSettings=UnityEditor.PlayerSettings;

[InitializeOnLoad]public static class IslandFoundationPlayAudit
{
    const string Key="SushiAndFish.FoundationQA.";
    static string Reports=>IslandFrontEndInventory.Reports;
    [Serializable]public class Check{public string category,source,shot;public bool top,sideX,sideZ,grounded,walked,ascended,descended;public float topError,footError,distance;public Vector3 walkDirection;public string[] blockedEast;}
    [Serializable]public class WalkCase{public string name;public Vector3 start,end;public float distance;public int samples,grounded;public bool passed;}
    [Serializable]public class Receipt{public string utc,stage,error,profile;public bool passed;public List<Check>checks=new List<Check>();public List<WalkCase>walks=new List<WalkCase>();public List<string>captures=new List<string>();public List<string>errors=new List<string>();}
    static Receipt report;static Keyboard keyboard;static bool finished;
    static string PathName=>SessionState.GetBool(Key+"visualOnly",false)?"island-foundation-final-views.json":SessionState.GetBool(Key+"complete",false)?"island-foundation-complete-runtime.json":"island-foundation-prototype-runtime.json";
    static IslandFoundationPlayAudit(){if(SessionState.GetBool(Key+"active",false))EditorApplication.playModeStateChanged+=Changed;}
    public static void PrototypeColdStart()=>Start(false);
    public static void CompleteColdStart()=>Start(true);
    public static void FinalViewColdStart()=>Start(true,true);
    public static void Start(bool complete,bool visualOnly=false)
    {
        SessionState.SetString(Key+"product",PlayerSettings.productName);PlayerSettings.productName="SushiAndFish WorldRepair QA 20261016";
        SessionState.SetBool(Key+"complete",complete);SessionState.SetBool(Key+"visualOnly",visualOnly);SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"started",false);
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");EditorApplication.playModeStateChanged+=Changed;EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode&&!SessionState.GetBool(Key+"started",false)){SessionState.SetBool(Key+"started",true);Run().Forget();}
        if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"active",false)){SessionState.SetBool(Key+"active",false);EditorApplication.playModeStateChanged-=Changed;PlayerSettings.productName=SessionState.GetString(Key+"product","Deep Blue Sushi");AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");if(Environment.GetCommandLineArgs().Contains("-foundationQAExit"))EditorApplication.delayCall+=()=>EditorApplication.Exit(0);}
    }
    static void Save(string stage){report.stage=stage;File.WriteAllText(Path.Combine(Reports,PathName),JsonUtility.ToJson(report,true));}
    static void Collect(string s,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors.Add(s+"\n"+stack);}
    static object Prior(string method)=>typeof(WorldIssuePlayAudit).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
    static async UniTask Pause(float seconds)=>await UniTask.Delay(TimeSpan.FromSeconds(seconds),ignoreTimeScale:true);
    static async UniTask Wait(Func<bool> f,int seconds=240){var end=DateTime.UtcNow.AddSeconds(seconds);while(!f()){if(DateTime.UtcNow>end)throw new TimeoutException(report.stage);await UniTask.Yield();}}
    static async UniTask Photo(Camera camera,Vector3 position,Vector3 target,string file)
    {
        var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;
        for(int i=0;i<10;i++){await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));if(i==6)ScreenCapture.CaptureScreenshot(Path.Combine(Reports,file));}
        await Pause(.3f);camera.transform.SetPositionAndRotation(oldPosition,oldRotation);
    }
    static async UniTask PerimeterAndJoins(PaperPixelGames.Core.Player.PPG_Player player,Camera camera)
    {
        var routes=new[]{("NorthWalk",new Vector3(69,3.2f,29),Vector3.right),("NorthWalkEast",new Vector3(92,3.2f,29),Vector3.right),("EastWalkNorth",new Vector3(114.5f,3.2f,11),Vector3.back),("EastWalkMiddle",new Vector3(114.5f,3.2f,-38),Vector3.back),("SouthWalkEast",new Vector3(91,3.2f,-78),Vector3.left),("SouthWalkMiddle",new Vector3(36,3.2f,-78),Vector3.left),("SouthWalkWest",new Vector3(-110,3.2f,-78),Vector3.right),("WestWalk",new Vector3(-142,3.2f,-40),Vector3.forward),("NorthWestWalk",new Vector3(-110,3.2f,29),Vector3.right),
            ("JoinMarketAvenue",new Vector3(3.8f,3.2f,3.267f),Vector3.right),("JoinResidentialAvenue",new Vector3(56.6f,3.2f,3.267f),Vector3.right),("JoinEastAvenue",new Vector3(100.6f,3.2f,3.267f),Vector3.right),("JoinSouthMarket",new Vector3(3.8f,3.2f,-67),Vector3.right),("JoinSouthResidential",new Vector3(56.6f,3.2f,-67),Vector3.right)};
        foreach(var route in routes)
        {
            Save("WALK_"+route.Item1);var origin=route.Item2;
            var hit=Physics.RaycastAll(origin+Vector3.up*2,Vector3.down,20,~0,QueryTriggerInteraction.Ignore).Where(h=>!h.transform.IsChildOf(player.transform)&&h.normal.y>.65f&&h.point.y<3.5f).OrderByDescending(h=>h.point.y).FirstOrDefault();
            if(!hit.collider)throw new Exception("Missing walking route floor: "+route.Item1);origin.y=hit.point.y;
            player.TeleportPlayer(origin+Vector3.up*.15f,true);player.FirstPersonCharacter.SetVelocity(Vector3.zero);await Pause(.6f);player.FirstPersonCharacter.TeleportRotation(Quaternion.LookRotation(route.Item3));
            var row=new WalkCase{name=route.Item1,start=player.transform.position};InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.W));float until=Time.realtimeSinceStartup+1.05f;
            while(Time.realtimeSinceStartup<until){row.samples++;if(player.FirstPersonCharacter.IsGrounded())row.grounded++;await UniTask.Yield(PlayerLoopTiming.FixedUpdate);}
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Pause(.4f);row.end=player.transform.position;row.distance=Vector3.ProjectOnPlane(row.end-row.start,Vector3.up).magnitude;row.passed=row.distance>1.8f&&row.grounded>=row.samples*.9f&&player.FirstPersonCharacter.IsGrounded();report.walks.Add(row);
        }
        var views=new[]{("north-sushi-side",new Vector3(91,2,21),new Vector3(76,.3f,30)),("north-beach-side",new Vector3(78,-.2f,41),new Vector3(83,1.4f,29)),("curved-under-rim",new Vector3(109,.3f,-25),new Vector3(119,1,-30)),("curved-coast-wide",new Vector3(130,0,-21),new Vector3(116,1.2f,-23)),("corner-native-assembly",new Vector3(117,1,36),new Vector3(109,2.7f,28)),("south-east-foundation",new Vector3(125,-.4f,-85),new Vector3(110,1,-77)),("south-west-foundation",new Vector3(-145,0,-90),new Vector3(-127,1,-78)),("west-foundation",new Vector3(-157,0,-30),new Vector3(-143,1,-30)),("island-overview",new Vector3(165,96,120),new Vector3(-10,1,-25))};
        foreach(var view in views){var filename="foundation-final-"+view.Item1+".png";await Photo(camera,view.Item2,view.Item3,filename);report.captures.Add(filename);}
    }
    static async UniTask Run()
    {
        report=new Receipt{utc=DateTime.UtcNow.ToString("o"),profile=Application.persistentDataPath};Application.logMessageReceived+=Collect;
        var settings=InputSystem.settings;var oldFocus=settings.editorInputBehaviorInPlayMode;var oldBackground=settings.backgroundBehavior;
        settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        try
        {
            if(!report.profile.Contains("WorldRepair QA"))throw new Exception("QA isolation failed");
            Save("MENU");await Wait(()=>{Prior("Dismiss");return IslandFrontEndFlow.Active&&IslandFrontEndFlow.Active.BackdropLoaded;});await Pause(1);Prior("Dismiss");
            if(SessionState.GetBool(Key+"complete",false)){ScreenCapture.CaptureScreenshot(Path.Combine(Reports,"foundation-final-menu.png"));await Pause(.5f);report.captures.Add("foundation-final-menu.png");}
            await Singleton<MainMenuManager>.Instance.CustomizationSceneHost.PreloadAsync();IslandFrontEndFlow.Active.ShowLoadGames();await Pause(1);
            Resources.FindObjectsOfTypeAll<SaveSlot>().Where(s=>s&&s.gameObject.scene.name=="MainMenuState"&&s.ContainsSave).OrderBy(s=>s.SlotIndex).First().LaunchOriginalSlot();
            Save("HOST");await Wait(()=>(bool)Prior("Ready"));await Wait(()=>ParkCasinoIntegration.Instance&&ParkCasinoIntegration.Instance.Ready);
            var result=JsonUtility.FromJson<IslandSolidFoundation.Result>(File.ReadAllText(Path.Combine(Reports,SessionState.GetBool(Key+"complete",false)?"island-foundation-complete.json":"island-foundation-prototype.json")));
            var player=Singleton<PlayersManager>.Instance.LocalPlayer;var camera=player.CinemachineBrain.GetComponent<Camera>();
            if(SessionState.GetBool(Key+"visualOnly",false))
            {
                Save("FINAL_VIEWS");
                var views=new[]{("sushi-platform-grounded",new Vector3(109,5.1f,24),new Vector3(78,2.1f,31)),("sushi-platform-beach",new Vector3(99,1.5f,34),new Vector3(81,1.5f,28)),("curved-rim-grounded",new Vector3(119,5.2f,-15),new Vector3(121,.8f,-29)),("curved-rim-low",new Vector3(130,0,-21),new Vector3(116,1.2f,-23))};
                foreach(var view in views){string file="foundation-final-"+view.Item1+".png";await Photo(camera,view.Item2,view.Item3,file);report.captures.Add(file);}
                report.passed=report.errors.Count==0;Save(report.passed?"PASS":"FAILED");return;
            }
            keyboard=InputSystem.AddDevice<Keyboard>("Foundation walk keyboard");Singleton<InputManager>.Instance.InputHandler.InputMap.asset.devices=new InputDevice[]{keyboard};
            foreach(var part in result.parts.GroupBy(p=>p.category).Select(g=>g.OrderByDescending(p=>p.maxGroundGap).First()))
            {
                Save("CHECK_"+part.category);var collider=Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Single(c=>c.sharedMesh&&AssetDatabase.GetAssetPath(c.sharedMesh)==part.mesh);
                var check=new Check{category=part.category,source=part.source};var p=part.point;
                check.top=collider.Raycast(new Ray(p+Vector3.up,Vector3.down),out var hit,3);check.topError=check.top?Mathf.Abs(hit.point.y-(p.y-.025f)):999;check.top&=check.topError<.03f;
                float floor=Terrain.activeTerrains.Where(t=>{var l=p-t.transform.position;return l.x>=0&&l.z>=0&&l.x<=t.terrainData.size.x&&l.z<=t.terrainData.size.z;}).Max(t=>t.SampleHeight(p)+t.transform.position.y);
                var middle=new Vector3(p.x,(p.y+floor)*.5f,p.z);float width=collider.bounds.size.x+3,depth=collider.bounds.size.z+3;
                check.sideX=collider.Raycast(new Ray(middle+Vector3.right*width,Vector3.left),out _,width*2);
                check.sideZ=collider.Raycast(new Ray(middle+Vector3.forward*depth,Vector3.back),out _,depth*2);
                player.TeleportPlayer(p+Vector3.up*.2f,true);player.FirstPersonCharacter.SetVelocity(Vector3.zero);await Pause(1.2f);
                check.grounded=player.FirstPersonCharacter.IsGrounded();check.footError=Mathf.Abs(player.transform.position.y-p.y);
                if(part.category=="Steps"||part.category=="WideSteps")
                {
                    var direction=Vector3.ProjectOnPlane(part.highStep-part.lowStep,Vector3.up).normalized;
                    player.TeleportPlayer(part.lowStep+Vector3.up*.15f,true);player.FirstPersonCharacter.SetVelocity(Vector3.zero);await Pause(.5f);
                    player.FirstPersonCharacter.TeleportRotation(Quaternion.LookRotation(direction));var start=player.transform.position;
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.W));float end=Time.realtimeSinceStartup+3;
                    while(Time.realtimeSinceStartup<end&&player.transform.position.y<part.highStep.y-.1f)await UniTask.Yield();
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Pause(.5f);
                    check.ascended=player.FirstPersonCharacter.IsGrounded()&&player.transform.position.y>=part.highStep.y-.2f;
                    check.distance=Vector3.ProjectOnPlane(player.transform.position-start,Vector3.up).magnitude;
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.S));end=Time.realtimeSinceStartup+3;
                    while(Time.realtimeSinceStartup<end&&player.transform.position.y>part.lowStep.y+.1f)await UniTask.Yield();
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Pause(.5f);
                    check.descended=player.FirstPersonCharacter.IsGrounded()&&player.transform.position.y<=part.lowStep.y+.2f;check.walked=check.ascended&&check.descended&&check.distance>.15f;
                }
                else if(part.category=="StraightPlatform")
                {
                    var start=player.transform.position;
                    check.blockedEast=Physics.RaycastAll(start+Vector3.up*.8f,Vector3.right,1.5f,~0,QueryTriggerInteraction.Ignore).Where(h=>!h.transform.IsChildOf(player.transform)).Select(h=>IslandFoundationDiagnosis.P(h.transform)).Distinct().ToArray();
                    var source=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).First(r=>IslandFoundationDiagnosis.P(r.transform)==part.source);
                    var inward=Vector3.ProjectOnPlane(source.bounds.center-start,Vector3.up).normalized;if(inward.sqrMagnitude<.1f)inward=Vector3.right;check.walkDirection=inward;player.FirstPersonCharacter.TeleportRotation(Quaternion.LookRotation(inward));
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.W));await Pause(.5f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Pause(.7f);
                    check.distance=Vector3.ProjectOnPlane(player.transform.position-start,Vector3.up).magnitude;check.walked=check.distance>.15f&&player.FirstPersonCharacter.IsGrounded();
                }
                else check.walked=true;
                var label=SessionState.GetBool(Key+"complete",false)?"complete":"prototype";check.shot="foundation-"+label+"-"+part.category+".png";
                bool curved=part.category.StartsWith("Curved");bool steps=part.category=="Steps"||part.category=="WideSteps";
                Vector3 view=curved?new Vector3(109,.3f,-25):steps?p+new Vector3(7,-2,8):part.category=="RetainingWall"||part.category=="StonePier"?p+new Vector3(3,-2,4):new Vector3(91,2,21);
                Vector3 target=curved?new Vector3(119,1,-30):steps?p-Vector3.up:part.category=="RetainingWall"||part.category=="StonePier"?p-Vector3.up*1.5f:new Vector3(76,.3f,30);
                await Photo(camera,view,target,check.shot);report.checks.Add(check);Save("CHECKED_"+part.category);
            }
            if(SessionState.GetBool(Key+"complete",false))await PerimeterAndJoins(player,camera);
            report.passed=report.checks.Count==result.parts.Select(p=>p.category).Distinct().Count()&&report.checks.All(c=>c.top&&c.sideX&&c.sideZ&&c.walked&&(c.category=="RetainingWall"||c.category=="StonePier"||c.grounded))&&report.walks.All(w=>w.passed)&&report.errors.Count==0;Save(report.passed?"PASS":"CHECK_FAILED");
        }
        catch(Exception e){report.error=e.ToString();Save("FAILED");}
        finally
        {if(keyboard!=null){Singleton<InputManager>.Instance.InputHandler.InputMap.asset.devices=null;InputSystem.RemoveDevice(keyboard);}settings.editorInputBehaviorInPlayMode=oldFocus;settings.backgroundBehavior=oldBackground;Application.logMessageReceived-=Collect;EditorApplication.ExitPlaymode();}
    }
}
