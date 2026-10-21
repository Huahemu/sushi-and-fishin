using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Bozo.ModularCharacters;
using PaperPixelGames.Core.Ai.NPCs;
using Cysharp.Threading.Tasks;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States.MainMenu;
using PaperPixelGames.Framework.UI;
using SushiAndFish.Casino;
using SushiAndFish.FrontEnd;
using SushiAndFish.ResidentialIsland;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.AI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class MergedHousingAudit
{
    static void DiagnoseFloor3(Transform[] roots)
    {
        var lines=new List<string>();
        foreach(var source in roots.SelectMany(t=>t.GetComponentsInChildren<MeshRenderer>(true)).Where(r=>r.name=="polySurface201"||r.name=="polySurface202"||r.name.Contains("Floor")))
        {
            var mesh=source.GetComponent<MeshFilter>()?.sharedMesh;if(!mesh)continue;
            lines.Add(source.name+" pos="+source.transform.position.ToString("F3")+" scale="+source.transform.lossyScale.ToString("F3")+" bounds="+source.bounds+" meshBounds="+mesh.bounds+" mesh="+mesh.name+" submeshes="+mesh.subMeshCount+" materials="+source.sharedMaterials.Length);
            foreach(var material in source.sharedMaterials)
                if(material)lines.Add("  "+material.name+" shader="+material.shader.name+" main="+(material.HasProperty("_MainTex")?material.GetTexture("_MainTex")?.name:"absent")+" base="+(material.HasProperty("_BaseMap")?material.GetTexture("_BaseMap")?.name:"absent"));
        }
        using(var stage=new HousingPreviewStage(1200,900))
        {
            stage.SetBuilding(roots);
            var camera=Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(c=>c.targetTexture==stage.Texture);
            foreach(var renderer in camera.transform.parent.GetComponentsInChildren<MeshRenderer>())
            {
                if(renderer.name=="polySurface201")foreach(var mat in renderer.sharedMaterials)lines.Add("CLONE "+renderer.transform.position+" cut="+mat.GetFloat("_CutHeight")+" tex="+mat.GetTexture("_BaseMap")?.name);
                renderer.enabled=renderer.name=="polySurface201";
                foreach(var material in renderer.sharedMaterials){material.SetFloat("_CutHeight",99999);material.SetTexture("_BaseMap",Texture2D.whiteTexture);material.SetColor("_BaseColor",new Color(.3f,.5f,.7f,1));}
            }
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=stage.Texture;
            var image=new Texture2D(stage.Texture.width,stage.Texture.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,image.width,image.height),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(Reports,"housing-floor3-runtime-deck.png"),image.EncodeToPNG());Object.Destroy(image);RenderTexture.active=previous;
        }
        File.WriteAllLines(Path.Combine(Reports,"housing-floor3-runtime-diagnostic.txt"),lines);
    }
    const string StateKey="ResidentialIsland.MergedAudit.";
    static string Hierarchy(Transform t)=>t.parent?Hierarchy(t.parent)+"/"+t.name:t.name;
    static string Reports=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../reports/residential-merged"));
    static bool PreviewOnly=>Environment.GetCommandLineArgs().Contains("-housingQaPreviewOnly");
    static Keyboard keyboard;
    static Mouse mouse;
    static Receipt report;
    static bool running;
    static readonly List<string> errors=new List<string>();
    [Serializable]public class Receipt
    {
        public string utc,stage,error,profile,avatarFamily,scope;
        public bool anatomyStable,motionContinuous,toiletLid,toiletSoles,npcPrivate,npcSpawn,npcPlacement,npcLoad,npcSchedule;
        public List<MotionRow> motion=new List<MotionRow>();public List<FrameRow> frames=new List<FrameRow>();
        public bool entranceMatchesBoundary,bedSkinClearance,bedLiftBounded,l2Bed;
        public float entranceWorldWidth,boundaryWorldWidth;
        public List<BedContactRow> bedContacts=new List<BedContactRow>();
        public bool passed,originalGameplayStarted,tabletViaTab,allCardinalPlacementsFit,nonCardinalRejected,preview,wasd,qeIgnored,rightCancel,escapeCancel,mapStayedOpen,placed,doorInteraction,enteredHouse,nativeChair,chairExit,bed,toilet,bedExit,toiletExit,windowDrag,windowClose,portrait,bedHorizontal,braceContact,toiletFeet,previewImage,saveReload,textFits,typeChoice,previewKeepsPlayerStill,npcRoute,occupiedPlotRejected,earlyRestCancel;
        public bool nativeSpawnGrounded,uniformPreviews,foundationPreserved;public Vector3 nativeSpawn;public string actualProject;public int plots,foundationCount;public List<string> diagnostics=new List<string>(),captures=new List<string>(),errors=new List<string>();
        public List<FloorRow> floors=new List<FloorRow>();public List<PoseRow> poses=new List<PoseRow>();
    }
    [Serializable]public class FloorRow{public string building,title,description;public string[] sourcePaths,meshAssets;public int roots;public bool image,arrows,residence;public Vector2 previewSize;public Rect visible;public float longest;}
    [Serializable]public class PoseRow{public string kind,phase;public float elapsed,handError;public Vector3 hip,head,leftFoot,rightFoot;}
    [Serializable]public class FrameRow{public string file,clip;public float time;}
    static readonly Dictionary<string,int> frameCounts=new Dictionary<string,int>();
    static readonly Dictionary<string,float> frameTimes=new Dictionary<string,float>();
    [Serializable]public class MotionRow{public string home,kind,phase,segmentBone,angularBone;public float time,scaleError,segmentChange,angularSpeed,kneeBend,minimumSole,minimumMeshY,lid,handError;}
    [Serializable]public class BedContactRow{public string home,phase,region;public Vector3 point;public float elapsed,minimumClearance,supportLift;public int checkedPoints,penetratingPoints;}
    static async UniTask ProbeUntil(HousingRestSession session,float until,MergedHousingBedMeshProbe probe,string home,MergedHousingMotionProbe motion=null)
    {
        while(session.Active&&session.Elapsed<until)
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            if(!session.Active)break;
            if(probe!=null)report.bedContacts.Add(probe.Measure(home,session));if(motion!=null)
            {
                report.motion.Add(motion.Measure(home));var key=report.avatarFamily+"-"+home+"-"+session.Target.kind;
                if(!frameTimes.TryGetValue(key,out var last)||session.Elapsed-last>.13f)
                {
                    int count=frameCounts.TryGetValue(key,out var current)?current:0;string file="motion/"+key+"/"+count.ToString("D4")+".png";
                    frameCounts[key]=count+1;frameTimes[key]=session.Elapsed;report.frames.Add(new FrameRow{file=file,clip=key,time=session.Elapsed});await Capture(file,null,true);
                }
            }
            await Pause(.06);
        }
    }
    static MergedHousingAudit()
    {
        EditorApplication.update+=Tick;
        if(SessionState.GetBool(StateKey+"active",false))Application.logMessageReceived+=Collect;
    }
    public static void Start()
    {
        Directory.CreateDirectory(Reports);MergedHousingInstallation.Validate();
        SessionState.SetString(StateKey+"product",UnityEditor.PlayerSettings.productName);
        SessionState.SetInt(StateKey+"focus",(int)InputSystem.settings.editorInputBehaviorInPlayMode);SessionState.SetInt(StateKey+"background",(int)InputSystem.settings.backgroundBehavior);
        UnityEditor.PlayerSettings.productName="Residential Merged QA "+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        SessionState.SetBool(StateKey+"active",true);SessionState.SetBool(StateKey+"started",false);SessionState.SetBool(StateKey+"done",false);
        Application.logMessageReceived-=Collect;Application.logMessageReceived+=Collect;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        var request=Path.Combine(Reports,"housing-audit.request");
        if(!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(request))
        {File.Delete(request);Start();return;}
        if(SessionState.GetBool(StateKey+"active",false)&&EditorApplication.isPlaying&&!SessionState.GetBool(StateKey+"started",false)&&!running)
        {SessionState.SetBool(StateKey+"started",true);running=true;Scenario().Forget();}
        if(SessionState.GetBool(StateKey+"done",false)&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.SetBool(StateKey+"active",false);SessionState.SetBool(StateKey+"done",false);
            UnityEditor.PlayerSettings.productName=SessionState.GetString(StateKey+"product","Deep Blue Sushi");
            EditorApplication.Exit(SessionState.GetBool(StateKey+"passed",false)?0:2);
        }
    }
    static void Collect(string message,string trace,LogType type)
    {
        if(trace.Contains("UnityEditor.Search.SearchDatabase"))return;
        if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&errors.Count<60)errors.Add(message+"\n"+trace);
    }
    static void Stage(string stage)
    {
        report.stage=stage;report.errors=errors.ToList();File.WriteAllText(Path.Combine(Reports,PreviewOnly?"merged-housing-preview-audit.json":"merged-housing-play-audit.json"),JsonUtility.ToJson(report,true));Debug.Log("HOUSING_AUDIT "+stage);
    }
    static async UniTask Wait(Func<bool> test,string description,double seconds=180)
    {var start=DateTime.UtcNow;while(!test()){if((DateTime.UtcNow-start).TotalSeconds>seconds)throw new TimeoutException(description);await UniTask.Yield();}}
    static async UniTask Pause(double seconds)=>await UniTask.Delay(TimeSpan.FromSeconds(seconds),ignoreTimeScale:true);
    static T Field<T>(Object source,string field) where T:Object=>new SerializedObject(source).FindProperty(field).objectReferenceValue as T;
    static void Dismiss()
    {
        var motion=Object.FindFirstObjectByType<MotionSicknessPopup>();if(motion&&motion.IsVisible)typeof(MotionSicknessPopup).GetMethod("OnKeepDefaultsSelected",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(motion,null);
        var rapid=Object.FindFirstObjectByType<RapidPressBindingPopup>();if(rapid&&rapid.IsVisible)rapid.UseMouse1AndClose();
        var menu=Singleton<MainMenuManager>.Instance;
        if(menu){var popup=Field<GameObject>(menu,"_earlyAccessPopUp");if(popup&&popup.activeSelf)typeof(MainMenuManager).GetMethod("OnClickOkButton",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(menu,null);}
    }
    static async UniTask KeyPress(Key key,double hold=.2)
    {InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));await Pause(hold);InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Pause(.35);}
    static Vector2 ScreenPoint(RectTransform r)
    {
        Canvas.ForceUpdateCanvases();
        var canvas=r.GetComponentInParent<Canvas>().rootCanvas;return RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,r.TransformPoint(r.rect.center));
    }
    static async UniTask PointerMove(Vector2 p)
    {InputSystem.QueueStateEvent(mouse,new MouseState{position=p});await Pause(.3);}
    static async UniTask Click(RectTransform r,bool right=false)
    {
        var p=ScreenPoint(r);await PointerMove(p);
        var data=new PointerEventData(EventSystem.current){position=p};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        report.diagnostics.Add("pointer "+r.name+" screen="+p+" hits="+string.Join(";",hits.Take(5).Select(x=>x.gameObject.name)));
        InputSystem.QueueStateEvent(mouse,new MouseState{position=p,buttons=(ushort)(right?2:1)});await Pause(.18);
        InputSystem.QueueStateEvent(mouse,new MouseState{position=p});await Pause(.6);
    }
    static async UniTask Capture(string name,RectTransform focus=null,bool quick=false)
    {
        if(PreviewOnly)name="uniform-"+name;
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
        var player=HousingIsland.Instance?.Player;
        var tablet=player?player.GetComponentInChildren<PaperPixelGames.Core.Tablet.TabletController>(true):null;
        var source=tablet&&tablet.IsTabletActive?tablet.GetComponent<Canvas>().worldCamera:SushiAndFish.Casino.Original.CasinoRuntimeContext.PlayerCamera;
        if(!source){ScreenCapture.CaptureScreenshot(Path.Combine(Reports,name));await Pause(.7);return;}
        GameObject copyRoot=null;var camera=source;
        if(tablet&&tablet.IsTabletActive)
        {
            copyRoot=new GameObject("HousingAuditTabletCamera");camera=copyRoot.AddComponent<Camera>();camera.CopyFrom(source);camera.enabled=false;
            camera.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
            camera.GetUniversalAdditionalCameraData().SetRenderer(new SerializedObject(source.GetUniversalAdditionalCameraData()).FindProperty("m_RendererIndex").intValue);
            if(focus)
            {
                var points=new Vector3[4];focus.GetWorldCorners(points);var uv=points.Select(p=>source.WorldToViewportPoint(p)).ToArray();var center=source.WorldToViewportPoint(focus.position);
                float width=Mathf.Max(uv.Max(p=>p.x)-uv.Min(p=>p.x),(uv.Max(p=>p.y)-uv.Min(p=>p.y))/source.aspect)*1.65f;float height=width*source.aspect;
                var crop=Matrix4x4.identity;crop.m00=1/width;crop.m11=1/height;crop.m03=-(center.x*2-1)/width;crop.m13=-(center.y*2-1)/height;
                camera.projectionMatrix=crop*source.projectionMatrix;
            }
        }
        var rt=RenderTexture.GetTemporary(quick?800:focus?1024:1600,quick?500:focus?1024:1000,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
        try
        {
            OriginalNativeShaderRestoration.ApplyPending();SushiAndFish.Casino.Original.RecoveryShaderSupport.BeginRendering();Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});RenderTexture.active=rt;
            var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Reports,name)));File.WriteAllBytes(Path.Combine(Reports,name),texture.EncodeToPNG());Object.Destroy(texture);
            if(!quick)report.captures.Add(name);
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);if(copyRoot)Object.Destroy(copyRoot);SushiAndFish.Casino.Original.RecoveryShaderSupport.EndRendering();}
        if(!quick)await Pause(.3);
    }
    static void DiagnoseTablet(PPG_Player player,PaperPixelGames.Core.Tablet.TabletController tablet)
    {
        report.diagnostics.Add("player="+player.transform.position+" rotation="+player.transform.eulerAngles+" view="+player.CameraTransform.position+"/"+player.CameraTransform.eulerAngles+" screen="+Screen.width+"x"+Screen.height);
        foreach(var camera in Camera.allCameras)report.diagnostics.Add("CAM "+camera.name+" position="+camera.transform.position+" rotation="+camera.transform.eulerAngles+" pixel="+camera.pixelRect+" mask="+camera.cullingMask);
        foreach(var canvas in tablet.GetComponentsInChildren<Canvas>(true))report.diagnostics.Add("CANVAS "+canvas.name+" mode="+canvas.renderMode+" root="+canvas.rootCanvas.name+" camera="+canvas.worldCamera?.name+" rootCamera="+canvas.rootCanvas.worldCamera?.name);
    }
    static async UniTask LookAt(PPG_Player player,Vector3 point)
    {
        for(int i=0;i<15;i++)
        {
            var delta=point-player.CameraTransform.position;player.FirstPersonCharacter.TeleportRotation(Quaternion.Euler(0,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,0));
            player.FirstPersonCharacter.PitchInstant(-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg);await Pause(.025);
        }
    }
    static PoseRow Pose(HousingRestSession session,HousingRestInteractable rest,PPG_Player player)
    {
        var a=player.TPS_Character.GetComponent<Animator>();Vector3 Bone(HumanBodyBones b)=>a.GetBoneTransform(b).position;
        return new PoseRow{kind=rest.kind.ToString(),phase=session.Phase,elapsed=session.Elapsed,hip=Bone(HumanBodyBones.Hips),head=Bone(HumanBodyBones.Head),leftFoot=Bone(HumanBodyBones.LeftFoot),rightFoot=Bone(HumanBodyBones.RightFoot),handError=rest.handSupport?Vector3.Distance(Bone(HumanBodyBones.RightHand),rest.handSupport.position):-1};
    }
    static bool NonEmpty(RenderTexture source,string captureName=null)
    {
        if(!source)return false;var previous=RenderTexture.active;RenderTexture.active=source;
        var texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,source.width,source.height),0,0);texture.Apply();
        var pixels=texture.GetPixels32();var colors=new HashSet<int>();int opaque=0;
        if(captureName!=null){File.WriteAllBytes(Path.Combine(Reports,captureName),texture.EncodeToPNG());report.captures.Add(captureName);}
        for(int i=0;i<pixels.Length;i+=31){var p=pixels[i];if(p.a>24){opaque++;colors.Add((p.r/16)*256+(p.g/16)*16+p.b/16);}}
        Object.Destroy(texture);RenderTexture.active=previous;return opaque>200&&colors.Count>15;
    }
    static bool RoadPath(HousingHouse house)
    {
        var start=house.transform.position+Vector3.back*10.5f;var end=house.transform.position+Vector3.forward*10.5f;
        if(!NavMesh.SamplePosition(start,out var from,3,NavMesh.AllAreas)||!NavMesh.SamplePosition(end,out var to,3,NavMesh.AllAreas))return false;
        var path=new NavMeshPath();if(!NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
        for(int i=1;i<path.corners.Length;i++)
        {
            int count=Mathf.CeilToInt(Vector3.Distance(path.corners[i-1],path.corners[i])/.2f);
            for(int n=0;n<=count;n++)
            {
                var p=house.transform.InverseTransformPoint(Vector3.Lerp(path.corners[i-1],path.corners[i],(float)n/Mathf.Max(1,count)));
                if(Mathf.Abs(p.x)<house.definition.footprint.x/2-.05f&&Mathf.Abs(p.z)<house.definition.footprint.y/2-.05f)return false;
            }
        }
        report.diagnostics.Add("NPC route corners="+path.corners.Length+" complete="+path.status);return true;
    }
    static async UniTaskVoid Scenario()
    {
        report=new Receipt{utc=DateTime.UtcNow.ToString("o"),profile=Application.persistentDataPath};
        try
        {
            Application.runInBackground=true;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>("Housing audit keyboard");mouse=InputSystem.AddDevice<Mouse>("Housing audit pointer");keyboard.MakeCurrent();mouse.MakeCurrent();
            Stage("original_new_game");
            await Wait(()=>{Dismiss();return IslandFrontEndFlow.Active&&IslandFrontEndFlow.Active.BackdropLoaded;},"Original menu backdrop");await Pause(2);Dismiss();
            await IslandFrontEndFlow.Active.BeginNewGameAsync();await Wait(()=>IslandFrontEndFlow.Active.IsCreatingCharacter&&!IslandFrontEndFlow.Active.IsBusy,"Original character creator");
            var creator=Object.FindFirstObjectByType<CharacterCreator>();await Wait(()=>creator&&creator.character&&creator.character.initalized&&!creator.character.isloading,"Created avatar");
            var arguments=Environment.GetCommandLineArgs();int familyArgument=Array.IndexOf(arguments,"-housingQaAvatar");
            report.avatarFamily=familyArgument>=0&&familyArgument+1<arguments.Length?arguments[familyArgument+1]:"Anime";
            var familyType=typeof(CharacterCreator).GetNestedType("CharacterFamily",BindingFlags.NonPublic);
            var presetMethod=typeof(CharacterCreator).GetMethods(BindingFlags.NonPublic|BindingFlags.Instance).Single(m=>m.Name=="GetDefaultCharacterData"&&m.GetParameters().Length==1);
            var preset=presetMethod.Invoke(creator,new[]{Enum.ToObject(familyType,report.avatarFamily=="Stylized"?0:1)});
            await (System.Threading.Tasks.Task)typeof(CharacterCreator).GetMethod("ApplyCharacterDataAsync",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(creator,new[]{preset});
            await Wait(()=>creator.character&&!creator.character.isloading,"Fixture avatar family");
            await IslandFrontEndFlow.Active.ConfirmNewCharacterAsync();
            await Wait(()=>HousingIsland.Instance&&HousingIsland.Instance.CanBuild&&HousingIsland.Instance.Plots.Count>0&&Time.timeScale>.9f,"Original player and parcels",240);
            await Wait(()=>ParkCasinoIntegration.Instance&&ParkCasinoIntegration.Instance.Ready,"Casino initialization",240);await Pause(5);
            var island=HousingIsland.Instance;var player=island.Player;report.originalGameplayStarted=player.IsSpawned;report.plots=island.Plots.Count;
            report.actualProject=Path.GetFullPath(Path.Combine(Application.dataPath,".."));report.nativeSpawn=player.transform.position;
            report.nativeSpawnGrounded=player.FirstPersonCharacter.IsGrounded()&&Mathf.Abs(report.nativeSpawn.x)<500&&Mathf.Abs(report.nativeSpawn.z)<500;
            var foundation=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>t.name=="NativeContourSolidFoundations_20261021");
            report.foundationCount=foundation?foundation.childCount:0;report.foundationPreserved=report.foundationCount==673;
            if(!report.profile.Contains("Residential Merged QA"))throw new Exception("Actual project QA profile isolation failed: "+report.profile);
            if(!report.nativeSpawnGrounded)throw new Exception("Native merged-project spawn is not grounded: "+report.nativeSpawn);
            player.FirstPersonCharacter.PitchInstant(0);await Pause(2);
            var clock=Singleton<GameProgressManager>.Instance.CozyWeatherManager;clock.ClientPauseTime();clock.ClientSetTime(12,0);clock.TriggerBlackBoardTimeEvents(true);await Pause(.5);
            report.allCardinalPlacementsFit=island.Plots.All(p=>island.Catalog.homes.Where(t=>t.prefab).All(t=>new[]{0f,90,180,270}.All(y=>island.Fits(p,t,y,out _))));
            report.nonCardinalRejected=!island.Fits(island.Plots[0],island.Catalog.homes[0],45,out _);
            Stage("tablet_map_input");
            await KeyPress(Key.Tab);await Pause(2.2);await Capture("housing-tablet-home.png");var tablet=player.GetComponentInChildren<PaperPixelGames.Core.Tablet.TabletController>(true);
            if(!tablet)tablet=Object.FindFirstObjectByType<PaperPixelGames.Core.Tablet.TabletController>(FindObjectsInactive.Include);
            DiagnoseTablet(player,tablet);
            report.tabletViaTab=Field<GameObject>(tablet,"_tabletUI").activeInHierarchy;
            var mapButton=Field<BaseButton>(tablet,"_mapAppButton");await Click((RectTransform)mapButton.transform);
            await Wait(()=>Object.FindObjectsByType<HousingMap>(FindObjectsSortMode.None).Any(m=>m.IsVisible),"Tablet map visible",30);
            var map=Object.FindObjectsByType<HousingMap>(FindObjectsSortMode.None).First(m=>m.IsVisible);await Pause(2);
            await Capture("housing-map-before.png");
            var plot=island.Plots.First(p=>p.id=="P03");var button=map.Overlay.Find("Parcel_"+plot.id) as RectTransform;
            await PointerMove(ScreenPoint(button));await Capture("housing-parcel-hover.png");await Click(button);
            report.preview=map.PreviewPlot==plot;await Pause(.4);report.previewImage=NonEmpty(map.PreviewTexture,"housing-preview-render.png")&&map.PreviewSize.x>7&&map.PreviewSize.y>7;
            var nativeMap=map.GetComponent<PaperPixelGames.Core.Tablet.LiveSceneTabletMap>();
            report.entranceWorldWidth=map.EntranceRect.sizeDelta.y/map.Overlay.rect.height*nativeMap.WorldBounds.height;
            report.boundaryWorldWidth=plot.BoundaryWorldWidth;report.entranceMatchesBoundary=Mathf.Abs(report.entranceWorldWidth-report.boundaryWorldWidth)<.001f;
            report.diagnostics.Add("ghost size="+map.PreviewSize+" image="+report.previewImage+" layer="+map.PreviewRect.gameObject.layer+" screen="+ScreenPoint(map.PreviewRect)+" rect="+map.PreviewRect.rect.size+" culled="+map.PreviewRect.GetComponent<CanvasRenderer>().cull);await Capture("housing-preview-north.png");
            await Capture("housing-preview-detail.png",map.PreviewRect);
            var nextType=map.GetComponentsInChildren<RectTransform>(true).First(t=>t.name=="NextHome");var previousType=map.GetComponentsInChildren<RectTransform>(true).First(t=>t.name=="PreviousHome");
            await Click(nextType);report.typeChoice=map.PreviewType.level==2;await Click(previousType);report.typeChoice&=map.PreviewType.level==1;
            var positionBeforeDirections=player.transform.position;
            var directions=new[]{(Key.W,180f),(Key.A,90f),(Key.S,0f),(Key.D,270f)};report.wasd=true;
            foreach(var d in directions){await KeyPress(d.Item1);report.wasd&=Mathf.Abs(Mathf.DeltaAngle(map.PreviewYaw,d.Item2))<.01f;}
            report.previewKeepsPlayerStill=Vector3.Distance(positionBeforeDirections,player.transform.position)<.1f;
            float before=map.PreviewYaw;await KeyPress(Key.Q);await KeyPress(Key.E);report.qeIgnored=map.PreviewYaw==before;
            await Click(button,true);report.rightCancel=map.PreviewPlot==null;
            button=map.Overlay.Find("Parcel_"+plot.id) as RectTransform;await Click(button);await KeyPress(Key.Escape);
            report.escapeCancel=map.PreviewPlot==null;report.mapStayedOpen=map.IsVisible;
            button=map.Overlay.Find("Parcel_"+plot.id) as RectTransform;await Click(button);await KeyPress(Key.S);await Capture("housing-preview-south.png");await Click(button);
            report.placed=plot.house;await Capture("housing-map-placed.png");
            if(!plot.house)throw new Exception("House was not placed by actual map pointer input.");
            report.npcRoute=RoadPath(plot.house);
            Stage("building_windows");
            report.textFits=true;
            foreach(var info in HousingBuildingInfo.Collect())
            {
                map.OpenInfo(info);await Pause(.7);
                for(int floor=0;floor<info.floors.Count;floor++)
                {
                    if(floor>0)await Click(map.Window.Panel.Find("FloorUp") as RectTransform);await Pause(.5);var panel=map.Window.Panel;
                    var picture=panel.Find("FloorPlan").GetComponent<RawImage>();
                    var row=new FloorRow{building=info.id,title=info.floors[floor].title,roots=info.floors[floor].roots.Length,description=map.Window.FullDescription,image=picture.texture,arrows=panel.Find("FloorUp").gameObject.activeSelf};
                    row.sourcePaths=info.floors[floor].roots.Select(t=>t.gameObject.scene.name+"/"+Hierarchy(t)).ToArray();row.meshAssets=info.floors[floor].roots.SelectMany(t=>t.GetComponentsInChildren<MeshFilter>(true)).Where(f=>f.sharedMesh).Select(f=>AssetDatabase.GetAssetPath(f.sharedMesh)).Distinct().ToArray();
                    row.residence=info.residence;row.previewSize=picture.rectTransform.rect.size;row.visible=map.Window.PreviewContentRect;row.longest=Mathf.Max(row.visible.width,row.visible.height);
                    report.floors.Add(row);await Capture("housing-info-"+info.id+"-"+floor+".png");
                    foreach(var text in panel.GetComponentsInChildren<TMP_Text>(false)){text.ForceMeshUpdate();report.textFits&=!text.isTextTruncated;}
                    if(info.id=="restaurant")
                    {
                        var paper=panel.GetComponent<Image>();report.diagnostics.Add("paper ppu="+paper.pixelsPerUnit+" multiplier="+paper.pixelsPerUnitMultiplier+" canvasPPU="+paper.canvas.referencePixelsPerUnit+" rect="+panel.rect.size);
                    }
                }
                if(info.residence)report.portrait=map.Window.Panel.Find("BuildingName").GetComponent<TMP_Text>().text=="户主";
                if(info.id=="restaurant")
                {
                    var panel=map.Window.Panel;var initial=panel.anchoredPosition;
                    var canvas=panel.GetComponentInParent<Canvas>().rootCanvas;var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
                    var from=RectTransformUtility.WorldToScreenPoint(camera,panel.TransformPoint(new Vector3(0,panel.rect.height*.41f,0)));
                    await PointerMove(from);InputSystem.QueueStateEvent(mouse,new MouseState{position=from,buttons=1});await Pause(.15);
                    for(int n=1;n<=8;n++){InputSystem.QueueStateEvent(mouse,new MouseState{position=from+new Vector2(n*7,-n*3),buttons=1});await Pause(.05);}
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=from+new Vector2(56,-24)});await Pause(.4);
                    report.windowDrag=Vector2.Distance(initial,panel.anchoredPosition)>5;await Capture("housing-window-dragged.png");
                    await Click(panel.Find("Close") as RectTransform);report.windowClose=!map.Window;
                }
                else map.Window.Close();await Pause(.3);
            }
            if(PreviewOnly)
            {
                report.scope="Map/tablet previews and information windows only. Previous 1.0.1 motion/privacy regression is retained separately.";
                var plans=report.floors.Where(f=>!f.residence).ToArray();
                report.passed=report.originalGameplayStarted&&report.tabletViaTab&&report.preview&&report.previewImage&&report.placed&&report.wasd&&report.qeIgnored&&report.rightCancel&&report.escapeCancel&&report.windowDrag&&report.windowClose&&report.textFits&&report.portrait&&plans.Length>=9&&plans.All(f=>f.longest>.925f&&f.longest<.955f&&Vector2.Distance(f.visible.center,new Vector2(.5f,.5f))<.01f&&Vector2.Distance(f.previewSize,plans[0].previewSize)<.01f)&&errors.Count==0;
                Stage(report.passed?"PASS":"CHECK_FAILED");return;
            }
            var mergedPlans=report.floors.Where(f=>!f.residence).ToArray();
            report.uniformPreviews=mergedPlans.Length==9&&mergedPlans.All(f=>f.longest>.925f&&f.longest<.955f&&Vector2.Distance(f.visible.center,new Vector2(.5f,.5f))<.01f&&Vector2.Distance(f.previewSize,mergedPlans[0].previewSize)<.01f)&&report.floors.Single(f=>f.building=="cinema"&&f.title.Contains("放映厅")).sourcePaths.Any(p=>p.Contains("AR_CinemaAuditorium"));
            await KeyPress(Key.Tab);await Pause(1);
            Stage("house_interactions");
            var house=plot.house;var entrance=house.GetComponentsInChildren<HousingDoor>().First(d=>d.name.Contains("Entrance"));
            var occupied=island.Plots.First(p=>p.id=="P05");player.TeleportPlayer(occupied.Center+Vector3.up*.1f,true);await Pause(.2);
            report.occupiedPlotRejected=!island.Place(occupied,island.Catalog.homes[0],0)&&!occupied.house;island.ClearFailure();
            player.TeleportPlayer(house.entry.position+house.transform.forward*-2,house.transform.rotation,true);await Pause(.7);await LookAt(player,entrance.transform.position+Vector3.up*1.2f+house.transform.right*.6f);
            await KeyPress(Key.E);await Pause(1.6);report.doorInteraction=entrance.IsOpen;
            await KeyPress(Key.W,1.25);report.enteredHouse=Vector3.Dot(player.transform.position-house.entry.position,house.transform.forward)>.3f;
            await Capture("housing-entry.png");
            var chair=house.spawnedSeats.Select(g=>g.GetComponent<SushiAndFish.CityExpansion.CityDiningSeat>()).First(s=>s&&s.GetComponent<Collider>().enabled);
            player.TeleportPlayer(chair.approach.position,chair.seatedPose.rotation,true);await Pause(.5);await LookAt(player,chair.transform.position+Vector3.up*.5f);await KeyPress(Key.E);await Pause(1.5);
            report.nativeChair=player.IsUsingEmoteArea&&player.CurrentEmoteArea==chair.Area;await Capture("housing-native-chair.png");await KeyPress(Key.Escape);await Pause(1);report.chairExit=!player.IsUsingEmoteArea;
            foreach(var rest in house.GetComponentsInChildren<HousingRestInteractable>())
            {
                player.TeleportPlayer(rest.exit.position,rest.exit.rotation,true);await Pause(.5);await LookAt(player,rest.transform.position);await KeyPress(Key.E);
                var session=player.GetComponent<HousingRestSession>();await Wait(()=>session&&session.Active,"Furniture interaction "+rest.kind,10);
                using(var motion=new MergedHousingMotionProbe(session))
                using(var probe=rest.kind==HousingRestKind.Bed?new MergedHousingBedMeshProbe(player.TPS_Character,rest):null)
                {
                report.diagnostics.Add(rest.kind+" nativeSit="+session.NativeSitClips+" nativeLay="+session.NativeLayClips);
                var sampleTimes=new[]{.6f,2.1f,3.3f,4.5f,5.6f,6.7f,8.1f};
                for(int n=0;n<sampleTimes.Length;n++)
                {
                    await ProbeUntil(session,sampleTimes[n],probe,"L1",motion);
                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);report.poses.Add(Pose(session,rest,player));await Capture("housing-"+rest.kind.ToString().ToLower()+"-"+n+".png");
                }
                bool active=session.Active;if(rest.kind==HousingRestKind.Bed)report.bed=active;else report.toilet=active;
                await KeyPress(Key.E);await ProbeUntil(session,session.Elapsed+7,probe,"L1",motion);if(rest.kind==HousingRestKind.Bed)report.bedExit=!session.Active&&!player.IsUsingEmoteArea;else report.toiletExit=!session.Active&&!player.IsUsingEmoteArea;
                }
            }
            report.bedHorizontal=report.poses.Any(p=>p.kind=="Bed"&&p.phase=="Sleep")&&report.poses.Where(p=>p.kind=="Bed"&&p.phase=="Sleep").All(p=>Mathf.Abs(p.head.y-p.hip.y)<.12f&&p.head.z>p.hip.z+.35f);
            report.braceContact=report.motion.Any(p=>p.kind=="Bed"&&p.phase=="HandSupport"&&p.handError<.06f);
            var toiletRest=house.GetComponentsInChildren<HousingRestInteractable>().First(r=>r.kind==HousingRestKind.Toilet);
            report.toiletFeet=report.poses.Where(p=>p.kind=="Toilet"&&p.phase=="Seated").All(p=>Mathf.Abs(p.leftFoot.y-toiletRest.exit.position.y-.08f)<.06f&&Mathf.Abs(p.rightFoot.y-toiletRest.exit.position.y-.08f)<.06f);
            var abortBed=house.GetComponentsInChildren<HousingRestInteractable>().First(r=>r.kind==HousingRestKind.Bed);
            player.TeleportPlayer(abortBed.exit.position,abortBed.exit.rotation,true);await Pause(.3);await LookAt(player,abortBed.transform.position);await KeyPress(Key.E);await Pause(.3);await KeyPress(Key.Escape);await Pause(1.5);
            report.earlyRestCancel=!player.GetComponent<HousingRestSession>().Active&&!player.IsUsingEmoteArea&&!Singleton<GameUIManager>.Instance.PauseScreen.IsVisible;
            var l2Plot=island.Plots.First(p=>p.id=="P05");
            var privacy=island.GetComponent<HousingNpcExclusion>();
            var resident=Object.FindObjectsByType<ResidentController>(FindObjectsSortMode.None).First(r=>r.Brain!=null);
            resident.Brain.Movement.StopAtPose(l2Plot.Center,resident.transform.rotation);
            report.npcPlacement=island.Place(l2Plot,island.Catalog.homes[1],90,true)&&l2Plot.house&&HousingPrivacyVolume.Of(l2Plot.house).Distance(resident.transform.position)>=1.98f;
            report.diagnostics.Add("NPC placement relocation distance from wall="+HousingPrivacyVolume.Of(l2Plot.house).Distance(resident.transform.position));
            var loadPlot=island.Plots.First(p=>p.id=="P06");resident.Brain.Movement.StopAtPose(loadPlot.Center,resident.transform.rotation);
            if(!island.Place(loadPlot,island.Catalog.homes[0],180,false))throw new Exception("L2 bed regression fixture placement failed.");
            report.npcLoad=!privacy.Excludes(resident.transform.position)&&HousingPrivacyVolume.Of(loadPlot.house).Distance(resident.transform.position)>=1.98f;
            var l2Rest=l2Plot.house.GetComponentsInChildren<HousingRestInteractable>().First(r=>r.kind==HousingRestKind.Bed);
            player.TeleportPlayer(l2Rest.exit.position,l2Rest.exit.rotation,true);await Pause(.4);await LookAt(player,l2Rest.transform.position);await KeyPress(Key.E);
            var l2Session=player.GetComponent<HousingRestSession>();await Wait(()=>l2Session&&l2Session.Active,"L2 bed",10);
            using(var motion=new MergedHousingMotionProbe(l2Session))
            using(var probe=new MergedHousingBedMeshProbe(player.TPS_Character,l2Rest))
            {
                int frame=0;foreach(float time in new[]{.6f,2.1f,3.3f,4.5f,5.6f,6.7f,8.1f})
                {await ProbeUntil(l2Session,time,probe,"L2",motion);await Capture("housing-L2-bed-"+(frame++)+".png");}
                report.l2Bed=l2Session.Active;await KeyPress(Key.E);await ProbeUntil(l2Session,l2Session.Elapsed+7,probe,"L2",motion);report.l2Bed&=!l2Session.Active&&!player.IsUsingEmoteArea;
            }
            report.bedSkinClearance=report.bedContacts.Count>80&&report.bedContacts.Any(c=>c.home=="L1"&&c.checkedPoints>0)&&report.bedContacts.Any(c=>c.home=="L2"&&c.checkedPoints>0)&&report.bedContacts.All(c=>c.penetratingPoints==0);
            report.bedLiftBounded=report.bedContacts.All(c=>c.supportLift<.12f);
            Stage("privacy_and_motion");
            var homeField=typeof(ResidentController).GetField("_homePosition",BindingFlags.NonPublic|BindingFlags.Instance);
            homeField.SetValue(resident,house.transform.position);
            resident.Runtime.TargetDestination=house.transform.position;resident.Runtime.HasValidTarget=true;
            await Pause(.2);report.npcSchedule=!privacy.Excludes((Vector3)homeField.GetValue(resident))&&!privacy.Excludes(resident.Runtime.TargetDestination);
            var anchor=Object.FindObjectsByType<WaypointAnchor>(FindObjectsSortMode.None).First();var oldAnchor=anchor.transform.position;
            anchor.transform.position=house.transform.position;privacy.SanitizeTargets();report.npcSchedule&=!privacy.Excludes(anchor.transform.position);anchor.transform.position=oldAnchor;anchor.RefreshNavMeshPosition();
            var manager=Singleton<ResidentManager>.Instance;manager.Residents.Remove(resident);resident.Brain.Movement.StopAtPose(house.transform.position,resident.transform.rotation);manager.RegisterRuntimeResident(resident);
            report.npcSpawn=!privacy.Excludes(resident.transform.position);
            report.npcPrivate=true;
            for(int n=0;n<100;n++)
            {
                if(n==0||n==30)resident.Brain.Movement.StopAtPose(house.transform.position,resident.transform.rotation);
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                report.npcPrivate&=Object.FindObjectsByType<ResidentController>(FindObjectsSortMode.None).All(r=>!privacy.Excludes(r.transform.position));
                await Pause(.035);
            }
            report.diagnostics.Add("privacy relocations="+privacy.Relocations+" targets="+privacy.RedirectedTargets);
            report.anatomyStable=report.motion.Count>150&&report.motion.All(m=>m.scaleError<.001f&&m.segmentChange<.03f&&m.kneeBend<165);
            report.motionContinuous=report.motion.All(m=>m.angularSpeed<650);
            report.toiletLid=report.motion.Where(m=>m.kind=="Toilet"&&m.phase=="Seated").All(m=>m.lid>.99f)&&toiletRest.toilet.OpenAmount<.01f;
            report.toiletSoles=report.motion.Where(m=>m.kind=="Toilet"&&(m.phase=="Seated"||m.phase=="Sit")).All(m=>m.minimumSole>-.012f&&m.minimumMeshY>-.012f);
            Stage("native_save_and_reload");
            var savedPosition=house.transform.position;var savedYaw=house.transform.eulerAngles.y;string savedType=house.definition.id;int savedSlot=Singleton<SaveManager>.Instance.CurrentSaveSlotIndex;
            await KeyPress(Key.Escape);var pauseScreen=Singleton<GameUIManager>.Instance.PauseScreen;
            await Wait(()=>pauseScreen.IsVisible,"Native pause menu",10);Field<BaseButton>(pauseScreen,"_saveButton").OnSubmit(new BaseEventData(EventSystem.current));
            await Wait(()=>!Singleton<SaveManager>.Instance.SaveFlag&&Singleton<SaveManager>.Instance.CurrentSaveSlotData.GameProgress.HasSave,"Native saved world",90);
            Field<BaseButton>(pauseScreen,"_mainMenuButton").OnSubmit(new BaseEventData(EventSystem.current));
            await Wait(()=>IslandFrontEndFlow.Active&&IslandFrontEndFlow.Active.BackdropLoaded&&!IslandFrontEndFlow.Active.IsBusy&&SceneManager.GetSceneByName("MainMenuState").isLoaded,"Native menu return",240);await Pause(2);Dismiss();
            IslandFrontEndFlow.Active.ShowLoadGames();await Pause(.7);
            var saves=Field<SaveSlotScreen>(Singleton<MainMenuManager>.Instance,"_saveSlotScreen");var slot=saves.GetComponentsInChildren<SaveSlot>(true).First(s=>s.SlotIndex==savedSlot);
            Field<BaseButton>(slot,"_saveSlotButton").OnSubmit(new BaseEventData(EventSystem.current));
            await Wait(()=>HousingIsland.Instance&&HousingIsland.Instance.CanBuild&&HousingIsland.Instance.Plots.Any(p=>p.id=="P03"&&p.house),"Housing restored through original load",240);
            var restored=HousingIsland.Instance.Plots.First(p=>p.id=="P03").house;
            report.saveReload=restored.definition.id==savedType&&Vector3.Distance(restored.transform.position,savedPosition)<.05f&&Mathf.Abs(Mathf.DeltaAngle(restored.transform.eulerAngles.y,savedYaw))<.01f&&restored.spawnedSeats.Count==7;
            report.passed=report.nativeSpawnGrounded&&report.foundationPreserved&&report.uniformPreviews&&report.anatomyStable&&report.motionContinuous&&report.toiletLid&&report.toiletSoles&&report.npcPrivate&&report.npcSpawn&&report.npcPlacement&&report.npcLoad&&report.npcSchedule&&report.bedLiftBounded&&report.entranceMatchesBoundary&&report.bedSkinClearance&&report.l2Bed&&report.originalGameplayStarted&&report.tabletViaTab&&report.allCardinalPlacementsFit&&report.nonCardinalRejected&&report.preview&&report.previewImage&&report.wasd&&report.qeIgnored&&report.rightCancel&&report.escapeCancel&&report.mapStayedOpen&&report.placed&&report.doorInteraction&&report.enteredHouse&&report.nativeChair&&report.chairExit&&report.bed&&report.toilet&&report.bedExit&&report.toiletExit&&report.portrait&&report.windowDrag&&report.windowClose&&report.floors.All(f=>f.roots>0&&f.image)&&report.bedHorizontal&&report.braceContact&&report.toiletFeet&&report.saveReload&&report.textFits&&report.typeChoice&&report.previewKeepsPlayerStill&&report.npcRoute&&report.occupiedPlotRejected&&report.earlyRestCancel&&errors.Count==0;
            Stage(report.passed?"PASS":"CHECK_FAILED");
        }
        catch(Exception e){report.error=e.ToString();await Capture("housing-failure.png");Stage("FAILED");Debug.LogException(e);}
        finally
        {
            InputSystem.settings.editorInputBehaviorInPlayMode=(InputSettings.EditorInputBehaviorInPlayMode)SessionState.GetInt(StateKey+"focus",0);
            InputSystem.settings.backgroundBehavior=(InputSettings.BackgroundBehavior)SessionState.GetInt(StateKey+"background",0);
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);
            SessionState.SetBool(StateKey+"passed",report.passed);SessionState.SetBool(StateKey+"done",true);EditorApplication.ExitPlaymode();
        }
    }
}
