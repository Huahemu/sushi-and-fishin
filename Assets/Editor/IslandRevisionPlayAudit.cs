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
using ECM2.Examples.FirstPerson;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States.MainMenu;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.DressingSceneSystem;
using PaperPixelGames.Framework.Enums;
using SushiAndFish.FrontEnd;

[InitializeOnLoad]
public static class IslandRevisionPlayAudit
{
    const string Key="SushiAndFish.IslandRevisionPlay.";
    static string Reports=>IslandFrontEndInventory.Reports;
    static readonly List<string> Errors=new List<string>();
    static Audit report;
    [Serializable] public class Frame {public string file;public float seconds,pinkFraction,cameraY;public int effects;}
    [Serializable] public class Seat {public string anchor,furniture,npc;public bool resolved,authoredPose,arrived;public Vector3 authored,resolvedPosition,npcPosition;public float error;}
    [Serializable] public class Audit {public string utc,stage,error,profile,locale;public bool ready,passed,deckDry,polesGrounded,seatsResolved,seatingAnimationVerified,transitionEffectObserved,shadersRestored,deckIsStaticBatch,localizationReady;public int groundedPoles,sceneCount,grassBatches,totalPoles;public float waterLevel,maxPinkFraction,deckCombinedMeshMinY,deckRendererMinY,deckRendererTopY;public Frame[] frames;public Seat[] seats;public string[] errors,captures;public string computeShader,transitionShader;}
    static IslandRevisionPlayAudit() {if(SessionState.GetBool(Key+"active",false)){EditorApplication.update+=Update;Application.logMessageReceived+=Collect;}}
    public static void Start() {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play before revision verification");
        Errors.Clear();PlayerSettings.productName="SushiAndFish WorldRepair QA 20261016";
        SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"started",false);SessionState.SetBool(Key+"done",false);
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");EditorApplication.update+=Update;Application.logMessageReceived+=Collect;EditorApplication.EnterPlaymode();
    }
    static void Collect(string text,string stack,LogType type) {if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&Errors.Count<50)Errors.Add(text+"\n"+stack);}
    static void Update() {
        if(EditorApplication.isPlaying&&Application.isPlaying&&!SessionState.GetBool(Key+"started",false)){SessionState.SetBool(Key+"started",true);Run().Forget();}
        if(!EditorApplication.isPlayingOrWillChangePlaymode&&SessionState.GetBool(Key+"done",false)) {
            SessionState.SetBool(Key+"active",false);SessionState.SetBool(Key+"done",false);EditorApplication.update-=Update;Application.logMessageReceived-=Collect;
            PlayerSettings.productName="Deep Blue Sushi";EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");AssetDatabase.SaveAssets();
        }
    }
    static void Stage(string stage) {report.stage=stage;report.errors=Errors.ToArray();File.WriteAllText(Path.Combine(Reports,"island-revision-play-audit.json"),JsonUtility.ToJson(report,true));}
    static async UniTask Pause(float seconds)=>await UniTask.Delay(TimeSpan.FromSeconds(seconds),ignoreTimeScale:true);
    static async UniTask Wait(Func<bool> predicate,int seconds=120) {var end=DateTime.UtcNow.AddSeconds(seconds);while(!predicate()){if(DateTime.UtcNow>end)throw new TimeoutException(report.stage);await UniTask.Yield();}}
    static object Prior(string name)=>typeof(WorldIssuePlayAudit).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
    static Camera Camera()=>(Camera)Prior("WorldCamera");
    static string P(Transform t)=>IslandFrontEndInventory.PathOf(t);
    static async UniTask Capture(FirstPersonCharacter character,Vector3 cameraPosition,Vector3 target,string file) {
        character.SetVelocity(Vector3.zero);character.SetPosition(cameraPosition-Vector3.up*1.6f,true);character.LookAtInstant(target);await Pause(.7f);
        var camera=Camera();
        for(int i=0;i<5;i++){await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);camera.transform.SetPositionAndRotation(cameraPosition,Quaternion.LookRotation(target-cameraPosition));if(i==3)ScreenCapture.CaptureScreenshot(Path.Combine(Reports,file));}
        await Pause(.5f);
    }
    static async UniTask<Frame> CaptureFrame(string file,float start,float water) {
        var path=Path.Combine(Reports,file);if(File.Exists(path))File.Delete(path);ScreenCapture.CaptureScreenshot(path);await Pause(.10f);
        await Wait(()=>File.Exists(path),5);var tex=new Texture2D(2,2,TextureFormat.RGBA32,false);ImageConversion.LoadImage(tex,File.ReadAllBytes(path));
        var pixels=tex.GetPixels32();int pink=0,total=0;for(int i=0;i<pixels.Length;i+=16){var c=pixels[i];if(c.r>200&&c.b>200&&c.g<85)pink++;total++;}
        UnityEngine.Object.Destroy(tex);
        return new Frame{file=file,seconds=Time.realtimeSinceStartup-start,pinkFraction=(float)pink/total,cameraY=Camera().transform.position.y-water,effects=UnityEngine.Object.FindObjectsByType<WaterExitDropletBlurEffect>(FindObjectsSortMode.None).Length};
    }
    static async UniTaskVoid Run() {
        await UniTask.Yield();report=new Audit{utc=DateTime.UtcNow.ToString("o"),profile=Application.persistentDataPath};
        var captures=new List<string>();var frames=new List<Frame>();var seats=new List<Seat>();
        try {
            if(!report.profile.Contains("WorldRepair QA"))throw new Exception("Formal save profile must remain isolated");
            Stage("BOOTSTRAP");await Wait(()=>{Prior("Dismiss");return IslandFrontEndFlow.Active&&IslandFrontEndFlow.Active.BackdropLoaded;},180);await Pause(2);Prior("Dismiss");
            // The previous failed initialization reset only the isolated QA
            // profile to English. Keep the visual regression captures Chinese.
            var chinese=UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.Locales.FirstOrDefault(l=>l.Identifier.Code=="zh-Hans"||l.Identifier.Code=="zh-CN");
            if(chinese){UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale=chinese;await Pause(.5f);}
            await Singleton<MainMenuManager>.Instance.CustomizationSceneHost.PreloadAsync();
            IslandFrontEndFlow.Active.ShowLoadGames();await Pause(1);
            var slot=Resources.FindObjectsOfTypeAll<SaveSlot>().Where(s=>s&&s.gameObject.scene.name=="MainMenuState"&&s.ContainsSave).OrderBy(s=>s.SlotIndex).First();slot.LaunchOriginalSlot();
            await Wait(()=>(bool)Prior("Ready"),210);await Pause(4);report.ready=true;
            var localization=UnityEngine.Localization.Settings.LocalizationSettings.InitializationOperation;
            report.localizationReady=localization.IsDone&&localization.Status==UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded&&UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
            report.locale=report.localizationReady?UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale.Identifier.Code:"";
            var sceneArray=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();report.sceneCount=sceneArray.Length;
            var all=sceneArray.SelectMany(IslandRevision20261018.All).ToArray();
            foreach(var culler in all.Select(t=>t.GetComponent<DressingRendererDistanceCuller>()).Where(c=>c))culler.enabled=false;
            foreach(var root in all.Select(t=>t.GetComponent<DressingSceneRoot>()).Where(r=>r))root.ActivateAllImmediate();
            foreach(var lod in all.Select(t=>t.GetComponent<LODGroup>()).Where(l=>l&&l.enabled&&l.gameObject.activeInHierarchy))lod.ForceLOD(0);
            OriginalNativeShaderRestoration.Apply();
            var transition=AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/WaterExit_DropletBlur.mat");report.transitionShader=transition.shader.name+" supported="+transition.shader.isSupported+" passes="+transition.passCount;
            var features=Resources.FindObjectsOfTypeAll<StylizedWater3.StylizedWaterRenderFeature>();var compute=features.First().heightReadbackCS;compute.GetKernelThreadGroupSizes(compute.FindKernel("SampleOffsets"),out uint x,out uint y,out uint z);report.computeShader=compute.name+" threadGroup="+x+","+y+","+z+" editorAsset="+AssetDatabase.GetAssetPath(compute);
            report.shadersRestored=transition.shader.isSupported&&x>0;
            Stage("STRUCTURE_AND_NPC_BINDINGS");
            report.waterLevel=UnityEngine.Object.FindFirstObjectByType<StylizedWater3.OceanFollowBehaviour>().transform.position.y;
            var deck=all.First(t=>P(t)=="---Restaurant/Restaurant/RestaurantStatic/Ground");
            report.deckCombinedMeshMinY=IslandComprehensiveRepair.BoundsOf(deck).min.y;
            var deckRenderer=deck.GetComponent<Renderer>();report.deckIsStaticBatch=deckRenderer.isPartOfStaticBatch;
            var deckBounds=deckRenderer.bounds;report.deckRendererMinY=deckBounds.min.y;report.deckRendererTopY=deckBounds.max.y;
            report.deckDry=report.deckRendererMinY>report.waterLevel+.5f;
            report.grassBatches=all.Count(t=>t.GetComponent<PaperPixelGames.Core.Optimization.GrassInstancedChunkRenderer>());
            var lands=UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            var poles=all.Where(t=>P(t).StartsWith("---Restaurant/Restaurant/RestaurantStatic/Poles/")&&t.GetComponent<MeshFilter>()).ToArray();
            report.totalPoles=poles.Length;
            report.groundedPoles=poles.Count(t=>{var b=t.GetComponent<Renderer>().bounds;return lands.Where(l=>b.center.x>=l.transform.position.x&&b.center.z>=l.transform.position.z&&b.center.x<=l.transform.position.x+l.terrainData.size.x&&b.center.z<=l.transform.position.z+l.terrainData.size.z).Any(l=>b.min.y<=l.SampleHeight(b.center)+l.transform.position.y+.01f);});
            report.polesGrounded=report.groundedPoles==poles.Length;
            var export=JsonUtility.FromJson<IslandRevision20261018.DecorationPlan>(File.ReadAllText(Path.Combine(Reports,"island-decoration-export.json")));
            foreach(var binding in export.seats) {
                var anchor=all.First(t=>P(t)==binding.anchor).GetComponent<WaypointAnchor>();
                var resolver=new ResidentWaypointResolver(Vector3.zero,Quaternion.identity);
                bool resolved=resolver.ResolveScheduleTarget(anchor.Category,anchor.Index,out var position,out var rotation);
                seats.Add(new Seat{anchor=binding.anchor,furniture=binding.furniture,resolved=resolved,authoredPose=anchor.UseAuthoredPose,authored=anchor.transform.position,resolvedPosition=position,error=Vector3.Distance(position,anchor.transform.position)});
            }
            report.seats=seats.ToArray();report.seatsResolved=seats.Count==21&&seats.All(s=>s.resolved&&s.authoredPose&&s.error<.002f);
            var character=UnityEngine.Object.FindFirstObjectByType<FirstPersonCharacter>();character.SetMovementMode(ECM2.Character.MovementMode.Flying);
            var camera=Camera();camera.farClipPlane=1200;camera.useOcclusionCulling=false;camera.layerCullDistances=new float[32];
            Stage("SAVED_MAP_VIEWS");
            foreach(var view in new[]{
                new[]{new Vector3(-30,40,-20),new Vector3(-30,3,-20)},
                new[]{new Vector3(-110,9,-8),new Vector3(-85,3,-15)},
                new[]{new Vector3(68,9,-8),new Vector3(45,3,-20)},
                new[]{new Vector3(-14,5,1),new Vector3(-5,3,-15)},
                new[]{new Vector3(8,3,66),new Vector3(26,1,77)},
                new[]{new Vector3(30,report.waterLevel-1,67),new Vector3(26,-9,78)},
                new[]{new Vector3(35,0,53),new Vector3(53,-3.7f,49)},
                new[]{new Vector3(-134,0,-88),new Vector3(-134,1,-76)},
                new[]{new Vector3(94,0,-89),new Vector3(94,1,-76)}}) {
                string file="revision-map-"+captures.Count.ToString("00")+".png";await Capture(character,view[0],view[1],file);captures.Add(file);
            }
            Stage("NPC_MOVEMENT_TO_AUTHORED_FURNITURE");
            var resident=UnityEngine.Object.FindObjectsByType<ResidentController>(FindObjectsSortMode.None).First(r=>r.Runtime!=null);
            var brain=(ResidentBrain)typeof(ResidentController).GetField("_brain",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(resident);
            int checkedSeats=0;
            foreach(var category in new[]{ResidentActivityType.SeasideBench,ResidentActivityType.ParkBench,ResidentActivityType.BeachSunbathe}) {
                var binding=export.seats.First(s=>s.category==category.ToString());var anchor=all.First(t=>P(t)==binding.anchor).GetComponent<WaypointAnchor>();
                var row=seats.First(s=>s.anchor==binding.anchor);var target=anchor.transform.position;
                brain.Movement.Warp(target-anchor.transform.forward*.8f,anchor.transform.rotation);
                brain.Runtime.TargetDestination=target;brain.Runtime.TargetRotation=anchor.transform.eulerAngles;brain.Runtime.HasValidTarget=true;
                brain.StateMachine.ChangeState(new ResidentMoveToScheduleTargetState(brain,category,3.5f),ResidentRequestPriority.HardReset);
                await Wait(()=>brain.Runtime.HasArrived&&Vector3.Distance(resident.transform.position,target)<.02f,12);await Pause(.6f);
                row.npc=resident.name;row.npcPosition=resident.transform.position;row.arrived=brain.Runtime.HasArrived;
                var animator=resident.GetComponentsInChildren<Animator>(true).FirstOrDefault(a=>a.isHuman);bool animated=animator&&(category==ResidentActivityType.BeachSunbathe?animator.GetBool("LayDown"):animator.GetBool("Sit"));
                if(!animated)throw new Exception("NPC furniture animation did not activate for "+category);
                string file="revision-npc-"+category+".png";await Capture(character,target+anchor.transform.right*2f+anchor.transform.forward*2f+Vector3.up*1.1f,target+Vector3.up*.6f,file);captures.Add(file);checkedSeats++;
            }
            report.seatingAnimationVerified=checkedSeats==3;
            Stage("WATER_SURFACE_CROSSING_SEQUENCE");
            Directory.CreateDirectory(Path.Combine(Reports,"revision-transition-frames"));
            for(int cycle=0;cycle<2;cycle++) {
                character.SetVelocity(Vector3.zero);character.SetPosition(new Vector3(60,report.waterLevel-3.4f,90),true);character.LookAtInstant(new Vector3(26,0,77));await Pause(1);
                float start=Time.realtimeSinceStartup;
                for(int i=0;i<40;i++) {
                    if(i<18)character.SetPosition(new Vector3(60,report.waterLevel-3.4f+i*.25f,90),true);
                    var frame=await CaptureFrame("revision-transition-frames/cycle"+cycle+"-"+i.ToString("00")+".png",start,report.waterLevel);frames.Add(frame);
                    report.maxPinkFraction=Mathf.Max(report.maxPinkFraction,frame.pinkFraction);report.transitionEffectObserved|=frame.effects>0;
                }
            }
            report.frames=frames.ToArray();report.seats=seats.ToArray();report.captures=captures.ToArray();
            report.passed=report.ready&&report.shadersRestored&&report.deckDry&&report.polesGrounded&&report.seatsResolved&&report.seatingAnimationVerified&&report.transitionEffectObserved&&report.maxPinkFraction<.01f&&Errors.Count==0&&report.grassBatches==0&&report.localizationReady;
            Stage(report.passed?"PASS":"CHECK_FAILED");
        }catch(Exception e){report.error=e.ToString();report.frames=frames.ToArray();report.seats=seats.ToArray();report.captures=captures.ToArray();Stage("FAILED");}
        finally {SessionState.SetBool(Key+"done",true);EditorApplication.ExitPlaymode();}
    }
}
