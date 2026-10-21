using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ECM2;
using ECM2.Examples.FirstPerson;
using SushiAndFish.FrontEnd;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States.MainMenu;

[InitializeOnLoad]
public static class RestaurantAlignmentPlayAudit
{
    const string Key="SushiAndFish.RestaurantAlignment.";
    static string Reports=>IslandFrontEndInventory.Reports;
    [Serializable] public class Point {public float seconds;public Vector3 position;public bool grounded;public string mode;}
    [Serializable] public class Walk {public Vector3 target,arrived;public float seconds;public bool reached;}
    [Serializable] public class Report {public string utc,stage,error,profile;public bool passed,ready,walkedFromBeachToDeck,dryBeach,deckDry,polesGrounded;public float waterY,deckY,beachY,stepOffset,slopeLimit;public int groundedPoles,totalPoles,grassBatches;public Point[] trajectory;public Walk[] walks;public string[] errors,captures;}
    static readonly List<string> Errors=new List<string>();static Report report;
    static RestaurantAlignmentPlayAudit(){if(SessionState.GetBool(Key+"active",false)){EditorApplication.update+=Update;Application.logMessageReceived+=Collect;}}
    public static void Start(){Errors.Clear();PlayerSettings.productName="SushiAndFish WorldRepair QA 20261016";SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"started",false);SessionState.SetBool(Key+"done",false);EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");EditorApplication.update+=Update;Application.logMessageReceived+=Collect;EditorApplication.EnterPlaymode();}
    static void Collect(string message,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&Errors.Count<30)Errors.Add(message+"\n"+stack);}
    static void Update(){if(EditorApplication.isPlaying&&Application.isPlaying&&!SessionState.GetBool(Key+"started",false)){SessionState.SetBool(Key+"started",true);Run().Forget();}if(!EditorApplication.isPlayingOrWillChangePlaymode&&SessionState.GetBool(Key+"done",false)){SessionState.SetBool(Key+"active",false);SessionState.SetBool(Key+"done",false);EditorApplication.update-=Update;Application.logMessageReceived-=Collect;PlayerSettings.productName="Deep Blue Sushi";EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");AssetDatabase.SaveAssets();}}
    static object Prior(string name)=>typeof(WorldIssuePlayAudit).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
    static Camera Camera()=>(Camera)Prior("WorldCamera");
    static string P(Transform t)=>IslandFrontEndInventory.PathOf(t);
    static async UniTask Pause(float sec)=>await UniTask.Delay(TimeSpan.FromSeconds(sec),ignoreTimeScale:true);
    static async UniTask Wait(Func<bool> predicate,int seconds=180){var end=DateTime.UtcNow.AddSeconds(seconds);while(!predicate()){if(DateTime.UtcNow>end)throw new TimeoutException(report.stage);await UniTask.Yield();}}
    static void Stage(string stage){report.stage=stage;report.errors=Errors.ToArray();File.WriteAllText(Path.Combine(Reports,"restaurant-alignment-play-audit.json"),JsonUtility.ToJson(report,true));}
    static async UniTask Snapshot(string file){var path=Path.Combine(Reports,file);if(File.Exists(path))File.Delete(path);ScreenCapture.CaptureScreenshot(path);await Pause(.6f);}
    static async UniTask View(FirstPersonCharacter character,Vector3 position,Vector3 target,string file){character.SetVelocity(Vector3.zero);character.SetMovementMode(Character.MovementMode.Flying);character.SetPosition(position-Vector3.up*1.6f,true);character.LookAtInstant(target);await Pause(.4f);var camera=Camera();for(int i=0;i<5;i++){await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));if(i==3)ScreenCapture.CaptureScreenshot(Path.Combine(Reports,file));}await Pause(.5f);}
    static async UniTaskVoid Run(){
        await UniTask.Yield();report=new Report{utc=DateTime.UtcNow.ToString("o"),profile=Application.persistentDataPath};var captures=new List<string>();var trajectory=new List<Point>();var walks=new List<Walk>();FirstPersonCharacterInput input=null;
        try{
            if(!report.profile.Contains("WorldRepair QA"))throw new Exception("QA profile isolation required");
            Stage("BOOTSTRAP");await Wait(()=>{Prior("Dismiss");return IslandFrontEndFlow.Active&&IslandFrontEndFlow.Active.BackdropLoaded;});await Pause(2);Prior("Dismiss");
            var chinese=UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.Locales.FirstOrDefault(l=>l.Identifier.Code=="zh-CN"||l.Identifier.Code=="zh-Hans");if(chinese)UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale=chinese;
            await Singleton<MainMenuManager>.Instance.CustomizationSceneHost.PreloadAsync();IslandFrontEndFlow.Active.ShowLoadGames();await Pause(1);
            Resources.FindObjectsOfTypeAll<SaveSlot>().Where(s=>s&&s.gameObject.scene.name=="MainMenuState"&&s.ContainsSave).OrderBy(s=>s.SlotIndex).First().LaunchOriginalSlot();
            await Wait(()=>(bool)Prior("Ready"),210);await Pause(3);report.ready=true;
            var all=Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).SelectMany(i=>IslandRevision20261018.All(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i))).ToArray();
            report.waterY=UnityEngine.Object.FindFirstObjectByType<StylizedWater3.OceanFollowBehaviour>().transform.position.y;
            var deck=all.First(t=>P(t)=="---Restaurant/Restaurant/RestaurantStatic/Ground");report.deckY=deck.GetComponent<Renderer>().bounds.max.y;report.deckDry=report.deckY>report.waterY;
            var terrains=UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);var poles=all.Where(t=>P(t).StartsWith("---Restaurant/Restaurant/RestaurantStatic/Poles/")&&t.GetComponent<Renderer>()).ToArray();report.totalPoles=poles.Length;
            report.groundedPoles=poles.Count(t=>{var b=t.GetComponent<Renderer>().bounds;return terrains.Where(l=>b.center.x>=l.transform.position.x&&b.center.z>=l.transform.position.z&&b.center.x<=l.transform.position.x+l.terrainData.size.x&&b.center.z<=l.transform.position.z+l.terrainData.size.z).Any(l=>b.min.y<=l.SampleHeight(b.center)+l.transform.position.y+.03f);});report.polesGrounded=report.groundedPoles==report.totalPoles;
            report.grassBatches=all.Count(t=>t.GetComponent<PaperPixelGames.Core.Optimization.GrassInstancedChunkRenderer>());
            var character=UnityEngine.Object.FindFirstObjectByType<FirstPersonCharacter>();input=character.GetComponent<FirstPersonCharacterInput>();report.stepOffset=character.characterMovement.stepOffset;report.slopeLimit=character.characterMovement.slopeLimit;
            Stage("WALK_FROM_BEACH_WITH_ORIGINAL_CHARACTER_PHYSICS");
            var beach=new Vector3(28,0,49);var land=terrains.Where(t=>t.name!="IslandOuterSeabed"&&beach.x>=t.transform.position.x&&beach.z>=t.transform.position.z&&beach.x<=t.transform.position.x+t.terrainData.size.x&&beach.z<=t.transform.position.z+t.terrainData.size.z).Single();report.beachY=land.SampleHeight(beach)+land.transform.position.y;report.dryBeach=report.beachY>report.waterY;
            input.InputMove=Vector2.zero;character.SetVelocity(Vector3.zero);character.SetPosition(new Vector3(beach.x,report.beachY+.08f,beach.z),true);character.SetMovementMode(Character.MovementMode.Falling);await Pause(.6f);character.LookAtInstant(new Vector3(26,report.beachY+1.6f,55));await Snapshot("alignment-beach-entry.png");captures.Add("alignment-beach-entry.png");
            float start=Time.realtimeSinceStartup,lastSample=0;
            foreach(var target in new[]{new Vector3(28,0,55),new Vector3(24,0,55),new Vector3(21.5f,0,55),new Vector3(21.5f,0,60),new Vector3(21.5f,0,67.3f)}){
                float begin=Time.realtimeSinceStartup;bool reached=false;
                while(Time.realtimeSinceStartup-begin<14){
                    var delta=target-character.transform.position;delta.y=0;if(delta.magnitude<.22f){reached=true;break;}
                    var direction=delta.normalized;character.LookAtInstant(character.transform.position+Vector3.up*1.6f+direction*10);
                    input.InputMove=new Vector2(Vector3.Dot(direction,character.GetRightVector()),Vector3.Dot(direction,character.GetForwardVector()))*Mathf.Min(.45f,delta.magnitude);
                    if(Time.realtimeSinceStartup-lastSample>.1f){trajectory.Add(new Point{seconds=Time.realtimeSinceStartup-start,position=character.transform.position,grounded=character.IsGrounded(),mode=character.movementMode.ToString()});lastSample=Time.realtimeSinceStartup;}
                    await UniTask.Yield();
                }
                input.InputMove=Vector2.zero;walks.Add(new Walk{target=target,arrived=character.transform.position,reached=reached,seconds=Time.realtimeSinceStartup-begin});report.walks=walks.ToArray();report.trajectory=trajectory.ToArray();Stage("WALK_WAYPOINT_"+walks.Count);
                if(!reached){await Snapshot("alignment-walk-blocked.png");captures.Add("alignment-walk-blocked.png");throw new Exception("Original character could not reach walking target "+target+"; arrived "+character.transform.position);}
            }
            report.walkedFromBeachToDeck=walks.Count==5&&walks.All(w=>w.reached)&&Mathf.Abs(character.transform.position.y-report.deckY)<.25f;
            await Snapshot("alignment-walked-to-deck.png");captures.Add("alignment-walked-to-deck.png");
            Stage("RESTAURANT_SHORE_AND_ORIGINAL_SUPPORT_VIEWS");
            var underwater=new Vector3(32,0,84);
            var seabed=terrains.Where(t=>t.name!="IslandOuterSeabed"&&underwater.x>=t.transform.position.x&&underwater.z>=t.transform.position.z&&underwater.x<=t.transform.position.x+t.terrainData.size.x&&underwater.z<=t.transform.position.z+t.terrainData.size.z).Single();
            underwater.y=seabed.SampleHeight(underwater)+seabed.transform.position.y+.65f;
            if(underwater.y>=report.waterY-.1f)throw new Exception("The underwater inspection camera must be above the seabed and below water");
            foreach(var view in new[]{new[]{new Vector3(30,-2.2f,48),new Vector3(21.5f,-1,57)},new[]{new Vector3(44,1,59),new Vector3(26,0,78)},new[]{underwater,underwater+new Vector3(-5,-.3f,-5)}}){string file="alignment-view-"+(captures.Count-2)+".png";await View(character,view[0],view[1],file);captures.Add(file);}
            report.passed=report.ready&&report.walkedFromBeachToDeck&&report.dryBeach&&report.deckDry&&report.polesGrounded&&Mathf.Abs(report.waterY+4)<.001f&&report.grassBatches==0&&Errors.Count==0;
            report.walks=walks.ToArray();report.trajectory=trajectory.ToArray();report.captures=captures.ToArray();Stage(report.passed?"PASS":"CHECK_FAILED");
        }catch(Exception e){report.error=e.ToString();report.walks=walks.ToArray();report.trajectory=trajectory.ToArray();report.captures=captures.ToArray();Stage("FAILED");}
        finally{if(input)input.InputMove=Vector2.zero;SessionState.SetBool(Key+"done",true);EditorApplication.ExitPlaymode();}
    }
}
