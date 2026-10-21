using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using ECM2;
using ECM2.Examples.FirstPerson;
using SushiAndFish.FrontEnd;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States.MainMenu;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Enums;

[InitializeOnLoad]
public static class TownSimplificationPlayAudit
{
    const string Key="SushiAndFish.TownSimplification.";
    static string Reports=>IslandFrontEndInventory.Reports;
    static bool UnifiedRoads=>File.Exists(Path.Combine(Reports,"town-unified-road-build.json"));
    static bool NewRoadMode=>File.Exists(Path.Combine(Reports,"town-road-build.json"));
    static bool MovedHouses=>File.Exists(Path.Combine(Reports,"town-road-house-relocation-build.json"));
    static float CurrentMarketZ=>MovedHouses?3.267f:CorrectedRoads?6.7f:3.267f;
    static bool CorrectedRoads=>File.Exists(Path.Combine(Reports,"town-road-correction-build.json"));
    static string CaptureName(string name)=>UnifiedRoads?name.Replace("town-","townfinal-"):MovedHouses?name.Replace("town-","townmove-"):CorrectedRoads?name.Replace("town-","roadfix-"):NewRoadMode?name.Replace("town-","roadtown-"):name;
    [Serializable] public class Walk {public Vector3 target,arrived;public bool reached;}
    [Serializable] public class Report {
        public string utc,stage,error,profile,captureMode;public bool passed,ready,menuVersionHidden,menuFeedbackHidden,pauseVersionHidden,pauseFeedbackHidden,runnerHidden,runnerUnregistered,tabOpened,mapButtonOpened,mapClosedCamera,geometryUpdates,zoomWorks,markerTracksPlayer,locationClickRoundTrip,underwaterSwitched,walkedToRestaurant;
        public int mainRoads,npcFurniture,remainingRemoved,shops,changedMapPixels,mapFrames,walkingSamples,groundedWalkingSamples,npcAnchorsResolved,reservedParcels,streetWalkingStages;
        public bool newRoadLayout,streetAndParcelWalked;public int signals,shopClearanceWalks;public bool movedHousePositionsMatch,throughMarketAvenue,marketGameplayPointsMatch,nativePavingSupported,unifiedSurfaceMeshesMatch;public int junctionWalkingStages,junctionWalkingSamples,junctionGroundedSamples,nativePavingSupportSamples;
        public float playerMarkerError,locationWorldError;public Rect surfaceBounds,underwaterBounds;public Walk[] walks;public string[] errors,captures,visibleVersionTexts;
    }
    static readonly List<string> Errors=new List<string>();static readonly List<string> Captures=new List<string>();static Report report;static Keyboard keyboard;
    static TownSimplificationPlayAudit(){if(SessionState.GetBool(Key+"active",false)){EditorApplication.update+=Update;Application.logMessageReceived+=Collect;}if(SessionState.GetBool(Key+"batch",false))EditorApplication.update+=BatchTick;}
    public static void RunUnifiedTownBatchQA(){SessionState.SetBool(Key+"batch",true);SessionState.SetFloat(Key+"batchFinish",0);EditorApplication.update+=BatchTick;Start();}
    static void BatchTick(){if(EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool(Key+"active",false)||!SessionState.GetBool(Key+"started",false))return;var file=Path.Combine(Reports,"town-unified-road-play-audit.json");if(!File.Exists(file))return;var r=JsonUtility.FromJson<Report>(File.ReadAllText(file));if(r.stage!="PASS"&&r.stage!="FAILED"&&r.stage!="CHECK_FAILED")return;float finish=SessionState.GetFloat(Key+"batchFinish",0);if(finish==0){SessionState.SetFloat(Key+"batchFinish",(float)EditorApplication.timeSinceStartup);return;}if(EditorApplication.timeSinceStartup-finish>4)EditorApplication.Exit(r.passed?0:2);}
    public static void Start(){Errors.Clear();Captures.Clear();PlayerSettings.productName="SushiAndFish WorldRepair QA 20261016";SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"started",false);SessionState.SetBool(Key+"done",false);EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");EditorApplication.update+=Update;Application.logMessageReceived+=Collect;EditorApplication.EnterPlaymode();}
    static void Collect(string message,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&Errors.Count<50)Errors.Add(message+"\n"+stack);}
    static void Update(){if(EditorApplication.isPlaying&&Application.isPlaying&&!SessionState.GetBool(Key+"started",false)){SessionState.SetBool(Key+"started",true);Run().Forget();}if(!EditorApplication.isPlayingOrWillChangePlaymode&&SessionState.GetBool(Key+"done",false)){SessionState.SetBool(Key+"active",false);SessionState.SetBool(Key+"done",false);EditorApplication.update-=Update;Application.logMessageReceived-=Collect;PlayerSettings.productName="Deep Blue Sushi";EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");AssetDatabase.SaveAssets();}}
    static object Get(object o,string field)=>o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(o);
    static object Invoke(object o,string method,params object[] args)=>o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(o,args);
    static object Prior(string name)=>typeof(WorldIssuePlayAudit).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
    static string P(Transform t)=>IslandFrontEndInventory.PathOf(t);
    static bool Hidden(object owner,string field)=>Get(owner,field) is Component c&&!c.gameObject.activeSelf;
    static void Stage(string stage){report.stage=stage;report.errors=Errors.ToArray();report.captures=Captures.ToArray();File.WriteAllText(Path.Combine(Reports,UnifiedRoads?"town-unified-road-play-audit.json":MovedHouses?"town-road-house-relocation-play-audit.json":CorrectedRoads?"town-road-correction-play-audit.json":NewRoadMode?"town-road-play-audit.json":"town-simplification-play-audit.json"),JsonUtility.ToJson(report,true));}
    static async UniTask Pause(float sec)=>await UniTask.Delay(TimeSpan.FromSeconds(sec),ignoreTimeScale:true);
    static async UniTask Wait(Func<bool> predicate,int seconds=180){var end=DateTime.UtcNow.AddSeconds(seconds);while(!predicate()){if(DateTime.UtcNow>end)throw new TimeoutException(report.stage);await UniTask.Yield();}}
    static async UniTask KeyPress(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));await Pause(.2f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Pause(.8f);}
    static async UniTask CaptureRenderTexture(string file,Camera original=null){
        if(!original)original=Camera.allCameras.FirstOrDefault(c=>c.enabled&&c.cameraType==CameraType.Game&&c.GetUniversalAdditionalCameraData().renderType==CameraRenderType.Base&&c.targetTexture==null);
        if(!original)original=Camera.allCameras.FirstOrDefault(c=>c.enabled&&c.cameraType==CameraType.Game);
        if(!original)throw new Exception("No active camera for Editor Play capture");
        var go=new GameObject("TemporaryRoadAuditCamera");var camera=go.AddComponent<Camera>();camera.CopyFrom(original);camera.transform.SetPositionAndRotation(original.transform.position,original.transform.rotation);camera.depth=1000;
        var originalData=original.GetUniversalAdditionalCameraData();var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=originalData.renderPostProcessing;data.volumeLayerMask=originalData.volumeLayerMask;data.volumeTrigger=camera.transform;data.SetRenderer(new SerializedObject(originalData).FindProperty("m_RendererIndex").intValue);
        var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);rt.Create();camera.targetTexture=rt;camera.enabled=true;await Pause(.5f);var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();RenderTexture.active=old;var pixels=tex.GetPixels32();int coloured=pixels.Count(c=>c.r+c.g+c.b>15);if(coloured<pixels.Length/10)throw new Exception("Editor Play camera capture is blank: "+file);File.WriteAllBytes(Path.Combine(Reports,file),tex.EncodeToPNG());UnityEngine.Object.Destroy(tex);camera.targetTexture=null;UnityEngine.Object.Destroy(go);rt.Release();UnityEngine.Object.Destroy(rt);
    }
    static async UniTask Capture(string file){file=CaptureName(file);if(Application.isBatchMode)await CaptureRenderTexture(file);else{ScreenCapture.CaptureScreenshot(Path.Combine(Reports,file));await Pause(.7f);}Captures.Add(file);}
    static Texture2D ReadMap(RenderTexture source){var old=RenderTexture.active;RenderTexture.active=source;var tex=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,source.width,source.height),0,0);tex.Apply();RenderTexture.active=old;return tex;}
    static void SaveMap(Texture2D map,string file){file=CaptureName(file);File.WriteAllBytes(Path.Combine(Reports,file),map.EncodeToPNG());Captures.Add(file);}
    static async UniTask View(FirstPersonCharacter character,Vector3 position,Vector3 target,string file){file=CaptureName(file);var viewModels=Camera.allCameras.Where(c=>c.name=="ViewModelCam").ToArray();var previousViewModelStates=viewModels.Select(c=>c.enabled).ToArray();if(UnifiedRoads)foreach(var c in viewModels)c.enabled=false;character.SetVelocity(Vector3.zero);character.SetMovementMode(Character.MovementMode.Flying);character.SetPosition(position-Vector3.up*1.6f,true);character.LookAtInstant(target);await Pause(NewRoadMode?3.1f:.6f);var camera=(Camera)Prior("WorldCamera");for(int i=0;i<5;i++){await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));if(i==3&&!Application.isBatchMode)ScreenCapture.CaptureScreenshot(Path.Combine(Reports,file));}if(Application.isBatchMode)await CaptureRenderTexture(file,camera);else await Pause(.6f);for(int i=0;i<viewModels.Length;i++)if(viewModels[i])viewModels[i].enabled=previousViewModelStates[i];Captures.Add(file);}
    static async UniTaskVoid Run(){
        await UniTask.Yield();report=new Report{utc=DateTime.UtcNow.ToString("o"),profile=Application.persistentDataPath,captureMode=Application.isBatchMode?"Unity Editor Play camera RenderTexture; screen-space overlay UI may be excluded":"Unity Editor Play screen"};var walks=new List<Walk>();FirstPersonCharacterInput input=null;
        var inputSettings=InputSystem.settings;var oldFocus=inputSettings.editorInputBehaviorInPlayMode;var oldBackground=inputSettings.backgroundBehavior;inputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;inputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        try {
            if(!report.profile.Contains("WorldRepair QA"))throw new Exception("Isolated QA profile required");keyboard=InputSystem.AddDevice<Keyboard>("TownSimplificationQA");
            Stage("BOOTSTRAP");await Wait(()=>{Prior("Dismiss");return IslandFrontEndFlow.Active&&IslandFrontEndFlow.Active.BackdropLoaded;});await Pause(2);Prior("Dismiss");
            var chinese=UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.Locales.FirstOrDefault(l=>l.Identifier.Code=="zh-CN"||l.Identifier.Code=="zh-Hans");if(chinese)UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale=chinese;await Pause(.5f);
            var menu=Singleton<MainMenuManager>.Instance;report.menuVersionHidden=Hidden(menu,"_versionText");report.menuFeedbackHidden=Hidden(menu,"_fedbackButton");await Capture("town-main-menu.png");
            await menu.CustomizationSceneHost.PreloadAsync();IslandFrontEndFlow.Active.ShowLoadGames();await Pause(1);Resources.FindObjectsOfTypeAll<SaveSlot>().Where(s=>s&&s.gameObject.scene.name=="MainMenuState"&&s.ContainsSave).OrderBy(s=>s.SlotIndex).First().LaunchOriginalSlot();
            await Wait(()=>(bool)Prior("Ready"),210);await Pause(4);report.ready=true;
            Stage("SAVED_SCENE_AND_VISIBLE_UI");var all=Enumerable.Range(0,SceneManager.sceneCount).SelectMany(i=>IslandRevision20261018.All(SceneManager.GetSceneAt(i))).ToArray();
            var plan=JsonUtility.FromJson<IslandRevision20261018.TownPlan>(File.ReadAllText(Path.Combine(Reports,"town-module-export.json")));
            var presentPaths=new HashSet<string>(all.Select(t=>t.gameObject.scene.path+"|"+P(t)));
            bool Present(IslandRevision20261018.TownRow row)=>presentPaths.Contains(row.scene+"|"+row.path);
            report.mainRoads=plan.objects.Count(x=>x.action=="KEEP_MAIN_ROAD"&&Present(x));report.npcFurniture=plan.objects.Count(x=>x.action=="KEEP_NPC_FURNITURE"&&Present(x));report.remainingRemoved=plan.objects.Count(x=>x.action=="REMOVE_AFTER_EXPORT"&&Present(x));
            if(NewRoadMode){var roadRoot=all.Single(t=>t&&t.name=="TownRoadNetwork20261019");report.newRoadLayout=true;report.mainRoads=roadRoot.Find("RoadSections").childCount;report.reservedParcels=roadRoot.Find("YellowParcelReservations").childCount;}
            report.signals=all.Count(t=>t.name.StartsWith("TrafficSignal_")||t.name.StartsWith("SM_trafficlight"));
            if(MovedHouses&&!UnifiedRoads){var relocated=JsonUtility.FromJson<IslandRevision20261018.RoadCorrectionResult>(File.ReadAllText(Path.Combine(Reports,"town-road-house-relocation-build.json")));report.movedHousePositionsMatch=relocated.movedHouses.All(m=>all.Any(t=>P(t)==m.path&&Vector3.Distance(t.position,m.after)<.002f));report.throughMarketAvenue=true;for(float z=-68;z<=1;z+=1)if(!Physics.RaycastAll(new Vector3(5.2f,15,z),Vector3.down,20,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.name=="MarketAvenue_Carriageway"))report.throughMarketAvenue=false;}
            if(UnifiedRoads){
                var unified=JsonUtility.FromJson<IslandRevision20261018.UnifiedRoadReceipt>(File.ReadAllText(Path.Combine(Reports,"town-unified-road-build.json")));
                var geometry=JsonUtility.FromJson<IslandRevision20261018.UnifiedGeometry>(File.ReadAllText(Path.Combine(Reports,"town-unified-road-geometry.json")));
                var roadRoot=all.Single(t=>t.name=="TownRoadNetwork20261019");
                report.movedHousePositionsMatch=unified.stores.All(m=>all.Any(t=>P(t)==m.path&&Vector3.Distance(t.position,m.position)<.002f));
                report.marketGameplayPointsMatch=unified.gameplayMoved.All(m=>all.Any(t=>P(t)==m.path&&Vector3.Distance(t.position,m.position)<.002f));
                var avenue=unified.plan.roads.Single(r=>r.id=="MarketAvenue");report.throughMarketAvenue=true;for(float z=avenue.a.z;z<=avenue.b.z;z+=.5f)if(!Physics.RaycastAll(new Vector3(avenue.a.x,15,z),Vector3.down,20,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.name=="Unified_Carriageway"))report.throughMarketAvenue=false;
                report.nativePavingSupported=true;report.unifiedSurfaceMeshesMatch=true;
                foreach(var surface in geometry.surfaces){var owner=string.IsNullOrEmpty(surface.nativeOwner)?roadRoot.Find("ContinuousSurfaces/"+surface.name):all.Single(t=>P(t)==surface.nativeOwner);var filter=owner.GetComponent<MeshFilter>();if(!filter||!AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(unified.assets+"/"))report.unifiedSurfaceMeshesMatch=false;if(string.IsNullOrEmpty(surface.nativeOwner))continue;var mesh=filter.sharedMesh;var vs=mesh.vertices;var tris=mesh.triangles;for(int i=0;i<tris.Length;i+=3*Mathf.Max(1,tris.Length/24)){var point=owner.TransformPoint((vs[tris[i]]+vs[tris[i+1]]+vs[tris[i+2]])/3);report.nativePavingSupportSamples++;if(!Physics.RaycastAll(point+Vector3.up*.3f,Vector3.down,.6f,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.transform==owner&&Mathf.Abs(h.point.y-3.159f)<.001f&&h.normal.y>.9f))report.nativePavingSupported=false;}}
            }
            report.shops=all.Count(t=>P(t)=="Root/Comps/GabeMarket"||P(t)=="Root/Comps/MarinaMarket");
            var seats=JsonUtility.FromJson<IslandRevision20261018.DecorationPlan>(File.ReadAllText(Path.Combine(Reports,"island-decoration-export.json"))).seats;
            foreach(var binding in seats){var node=all.First(t=>t&&P(t)==binding.anchor);var anchor=node.GetComponent<PaperPixelGames.Core.Ai.NPCs.WaypointAnchor>();var resolver=new PaperPixelGames.Core.Ai.NPCs.ResidentWaypointResolver(Vector3.zero,Quaternion.identity);if(resolver.ResolveScheduleTarget(anchor.Category,anchor.Index,out var position,out var rotation)&&anchor.UseAuthoredPose&&Vector3.Distance(position,anchor.transform.position)<.002f)report.npcAnchorsResolved++;}
            var character=UnityEngine.Object.FindFirstObjectByType<FirstPersonCharacter>();input=character.GetComponent<FirstPersonCharacterInput>();
            var pause=Singleton<GameUIManager>.Instance.PauseScreen;await KeyPress(UnityEngine.InputSystem.Key.Escape);await Pause(.5f);report.pauseVersionHidden=Hidden(pause,"_versionText");report.pauseFeedbackHidden=Hidden(pause,"_debugButton");await Capture("town-pause-menu.png");await KeyPress(UnityEngine.InputSystem.Key.Escape);
            await KeyPress(UnityEngine.InputSystem.Key.Tab);await Wait(()=>Resources.FindObjectsOfTypeAll<TabletController>().Any(t=>t&&t.IsTabletActive),10);var tablet=Resources.FindObjectsOfTypeAll<TabletController>().First(t=>t&&t.IsTabletActive);report.tabOpened=tablet.IsTabletActive;
            var runner=(BaseButton)Get(tablet,"_runnerGameAppButton");report.runnerHidden=!runner.transform.parent.gameObject.activeSelf;
            var apps=(IDictionary)Get(tablet,"_appsByType");report.runnerUnregistered=!apps.Contains(TabletAppType.RunnerGameApp);await Capture("town-tablet-home.png");
            ((BaseButton)Get(tablet,"_mapAppButton")).OnSubmit(new BaseEventData(EventSystem.current));await Pause(1);
            var map=tablet.MapApplication;var live=map.GetComponent<LiveSceneTabletMap>();report.mapButtonOpened=map.gameObject.activeInHierarchy&&live.MapCamera.enabled;
            await Wait(()=>live.RenderedFrames>3,15);report.surfaceBounds=live.WorldBounds;report.mapFrames=live.RenderedFrames;await Capture("town-live-map-tablet.png");
            var before=ReadMap(live.Texture);SaveMap(before,"town-live-map-surface.png");
            Stage("REAL_TIME_GEOMETRY_UPDATE");var probe=GameObject.CreatePrimitive(PrimitiveType.Cube);probe.name="TemporaryLiveMapVerification";probe.transform.position=new Vector3(live.WorldBounds.center.x,22,live.WorldBounds.center.y);probe.transform.localScale=new Vector3(14,2,14);
            var material=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/M_asphalt_01 1.mat"));material.SetTexture("_BaseMap",null);material.SetColor("_BaseColor",new Color(1,.08f,.75f));probe.GetComponent<Renderer>().sharedMaterial=material;int frame=live.RenderedFrames;await Wait(()=>live.RenderedFrames>frame+3,15);
            var after=ReadMap(live.Texture);SaveMap(after,"town-live-map-geometry-probe.png");var a=before.GetPixels32();var b=after.GetPixels32();int changed=0;
            for(int y=after.height/2-30;y<after.height/2+30;y++)for(int x=after.width/2-45;x<after.width/2+45;x++){int i=y*after.width+x;if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>80)changed++;}
            report.changedMapPixels=changed;report.geometryUpdates=changed>100;UnityEngine.Object.Destroy(probe);UnityEngine.Object.Destroy(material);UnityEngine.Object.Destroy(before);UnityEngine.Object.Destroy(after);await Pause(.4f);
            var content=(RectTransform)Get(map,"_contentRect");var zoom=(MapZoomer)Get(map,"_mapZoomer");float scale=content.localScale.x;zoom.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,8)});report.zoomWorks=content.localScale.x>scale;
            var projection=(MapProjection)Get(map,"_projection");var entries=(IDictionary)Get(map,"_playerMarkers");float maxError=0;
            foreach(DictionaryEntry item in entries){var rect=(RectTransform)Get(item.Value,"Rect");var player=(PaperPixelGames.Core.Player.PPG_Player)Get(item.Value,"Player");if(!rect.gameObject.activeInHierarchy)continue;projection.TryWorldToMapUV(player.transform.position,out var uv);var parent=(RectTransform)rect.parent;maxError=Mathf.Max(maxError,Vector2.Distance(rect.anchoredPosition,(uv-Vector2.one*.5f)*parent.rect.size));}
            report.playerMarkerError=maxError;report.markerTracksPlayer=entries.Count>0&&maxError<.2f;
            var raycaster=map.GetComponentInParent<UnityEngine.UI.GraphicRaycaster>();var viewport=(RectTransform)content.parent;var screen=RectTransformUtility.WorldToScreenPoint(raycaster.eventCamera,viewport.TransformPoint(viewport.rect.center+new Vector2(viewport.rect.width*.2f,-viewport.rect.height*.15f)));
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,clickCount=2,position=screen};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);if(hits.Count==0)throw new Exception("Live map did not receive UI raycasts");pointer.pointerPressRaycast=hits[0];ExecuteEvents.ExecuteHierarchy<IPointerClickHandler>(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(content,screen,raycaster.eventCamera,out var clicked);var clickUv=new Vector2((clicked.x-content.rect.xMin)/content.rect.width,(clicked.y-content.rect.yMin)/content.rect.height);projection.TryMapUVToWorldXZ(clickUv,out var expectedWorld);
            if(Get(map,"_locationWorldTarget") is Component locationTarget){var world=new Vector2(locationTarget.transform.position.x,locationTarget.transform.position.z);report.locationWorldError=Vector2.Distance(world,expectedWorld);report.locationClickRoundTrip=report.locationWorldError<.01f;}
            var dropdown=(Michsky.MUIP.CustomDropdown)Get(map,"_mapDropdown");dropdown.onValueChanged.Invoke(1);await Pause(.6f);report.underwaterSwitched=live.IsUnderwater;report.underwaterBounds=live.WorldBounds;SaveMap(ReadMap(live.Texture),"town-live-map-underwater.png");await Capture("town-underwater-map-tablet.png");
            tablet.CloseTablet();await Pause(.4f);report.mapClosedCamera=!live.MapCamera.enabled;
            Stage("TOWN_AND_UNDERWATER_VIEWS");
            await View(character,new Vector3(24,8,-8),new Vector3(-4,2,-15),"town-main-road-and-shops.png");
            if(!UnifiedRoads)await View(character,new Vector3(62,5,29),new Vector3(40,0,17),"town-removed-retaining-structure.png");
            if(!UnifiedRoads)await View(character,new Vector3(-91,5,-17),new Vector3(-65,1,-17),"town-removed-tunnel-spur.png");
            if(NewRoadMode){await View(character,new Vector3(16,7,-8),MovedHouses?new Vector3(5.2f,4,3.267f):new Vector3(-4,5,6.7f),"town-junction-and-diagonal-bins.png");await View(character,new Vector3(49,14,-25),new Vector3(55,3,-36),"town-residential-parcels.png");await View(character,new Vector3(-108,18,-35),new Vector3(-100,3,-35),"town-park-uniform-green.png");}
            if(CorrectedRoads){await View(character,new Vector3(7,6.5f,8),new Vector3(-7,5,MovedHouses?-8:-4),"town-shop-front-clearance.png");await View(character,new Vector3(15,7,-32),new Vector3(-8,4,-24),MovedHouses?"town-shop-side-and-avenue.png":"town-shop-side-and-turnaround.png");await View(character,new Vector3(7,5.5f,15),new Vector3(3.5f,3,11.5f),MovedHouses?"town-seafront-benches.png":"town-seafront-bench-bay.png");}
            if(MovedHouses){await View(character,new Vector3(-118,5,-2),new Vector3(-70,3,-2),"town-park-north-road-on-grass.png");await View(character,new Vector3(-118,5,-64),new Vector3(-70,3,-64),"town-park-south-road-on-grass.png");}
            if(UnifiedRoads){await View(character,new Vector3(11,8,10),new Vector3(-6,4,-5),"town-fishstore-corner-seam.png");await View(character,new Vector3(12,7,-14),new Vector3(-7,4,-20),"town-greenmarket-front-seam.png");await View(character,new Vector3(-56,10,12),new Vector3(-42,3,1),"town-northwest-junction.png");await View(character,new Vector3(116,11,16),new Vector3(100,3,1),"town-northeast-junction.png");await View(character,new Vector3(114,11,-84),new Vector3(99,3,-67),"town-southeast-junction.png");await View(character,new Vector3(-55,10,-80),new Vector3(-43,3,-66),"town-southwest-junction.png");}
            var marine=JsonUtility.FromJson<IslandRevision20261018.BathymetryResult>(File.ReadAllText(Path.Combine(Reports,"marine-bathymetry-repair.json")));
            var sample=marine.marine.First(t=>t.sourceGrounded&&t.beforeGap>.5f);var prop=all.FirstOrDefault(t=>t&&t.gameObject.scene.name==sample.scene&&P(t)==sample.path);
            if(prop){var bounds=IslandComprehensiveRepair.BoundsOf(prop);var pos=bounds.center+new Vector3(3,2,-4);pos.y=Mathf.Min(-4.5f,Mathf.Max(sample.afterGround+.7f,pos.y));await View(character,pos,bounds.center,"town-underwater-grounded-decor.png");}
            if(NewRoadMode){Stage("ROAD_CURB_AND_RESERVED_PARCEL_WALK");input.InputMove=Vector2.zero;character.SetVelocity(Vector3.zero);character.SetPosition(new Vector3(23,3.16f,CurrentMarketZ),true);character.SetMovementMode(Character.MovementMode.Falling);await Pause(.6f);foreach(var target in new[]{new Vector3(37,0,CurrentMarketZ),new Vector3(37,0,-4),new Vector3(37,0,-15)}){float begin=Time.realtimeSinceStartup;bool reached=false;while(Time.realtimeSinceStartup-begin<14){var delta=target-character.transform.position;delta.y=0;if(delta.magnitude<.22f){reached=true;break;}var direction=delta.normalized;character.LookAtInstant(character.transform.position+Vector3.up*1.6f+direction*10);input.InputMove=new Vector2(Vector3.Dot(direction,character.GetRightVector()),Vector3.Dot(direction,character.GetForwardVector()))*Mathf.Min(.45f,delta.magnitude);await UniTask.Yield();}input.InputMove=Vector2.zero;if(reached)report.streetWalkingStages++;else break;}report.streetAndParcelWalked=report.streetWalkingStages==3;await Capture("town-walked-over-curb-into-parcel.png");}
            if(CorrectedRoads){
                Stage("ACTUAL_SHOP_CLEARANCE_WALK");
                foreach(var route in new[]{new[]{new Vector3(-17,3.3f,MovedHouses?-1.7f:1.4f),new Vector3(-1,0,MovedHouses?-1.7f:1.4f)},new[]{new Vector3(UnifiedRoads?.55f:MovedHouses?-.2f:5.2f,3.3f,UnifiedRoads?-29:MovedHouses?-31:-29),new Vector3(UnifiedRoads?.55f:MovedHouses?-.2f:5.2f,0,MovedHouses?-24:-20),new Vector3(UnifiedRoads?.55f:MovedHouses?-.2f:5.2f,0,UnifiedRoads?-16:MovedHouses?-15:-11),new Vector3(UnifiedRoads?.55f:MovedHouses?-.2f:5.2f,0,UnifiedRoads?-10:-2)}}){
                    input.InputMove=Vector2.zero;character.SetVelocity(Vector3.zero);character.SetPosition(route[0],true);character.SetMovementMode(Character.MovementMode.Falling);await Pause(.6f);
                    foreach(var target in route.Skip(1)){float begin=Time.realtimeSinceStartup;bool reached=false;while(Time.realtimeSinceStartup-begin<22){var delta=target-character.transform.position;delta.y=0;if(delta.magnitude<.22f){reached=true;break;}var direction=delta.normalized;character.LookAtInstant(character.transform.position+Vector3.up*1.6f+direction*10);input.InputMove=new Vector2(Vector3.Dot(direction,character.GetRightVector()),Vector3.Dot(direction,character.GetForwardVector()))*Mathf.Min(.45f,delta.magnitude);await UniTask.Yield();}input.InputMove=Vector2.zero;if(reached)report.shopClearanceWalks++;else throw new Exception("Shop clearance walk blocked at "+character.transform.position+" toward "+target);}
                }
                await Capture("town-shop-clearance-walk-completed.png");
            }
            if(UnifiedRoads){Stage("CONTINUOUS_CORNER_WALKING");
                foreach(var route in new[]{new[]{new Vector3(92,3.1f,3.267f),new Vector3(100,0,3.267f),new Vector3(102,0,-6),new Vector3(102,0,-18)},new[]{new Vector3(-58,3.1f,-2),new Vector3(-46,0,-2),new Vector3(-42,0,-8),new Vector3(-42,0,-20)}}){
                    input.InputMove=Vector2.zero;character.SetVelocity(Vector3.zero);character.SetPosition(route[0],true);character.SetMovementMode(Character.MovementMode.Falling);await Pause(.6f);
                    foreach(var target in route.Skip(1)){float begin=Time.realtimeSinceStartup;bool reached=false;float lastSample=0;while(Time.realtimeSinceStartup-begin<20){var delta=target-character.transform.position;delta.y=0;if(delta.magnitude<.22f){reached=true;break;}var direction=delta.normalized;character.LookAtInstant(character.transform.position+Vector3.up*1.6f+direction*10);input.InputMove=new Vector2(Vector3.Dot(direction,character.GetRightVector()),Vector3.Dot(direction,character.GetForwardVector()))*Mathf.Min(.45f,delta.magnitude);if(Time.realtimeSinceStartup-lastSample>.1f){report.junctionWalkingSamples++;if(character.IsGrounded()&&character.movementMode==Character.MovementMode.Walking)report.junctionGroundedSamples++;lastSample=Time.realtimeSinceStartup;}await UniTask.Yield();}input.InputMove=Vector2.zero;if(reached)report.junctionWalkingStages++;else throw new Exception("Junction walk blocked at "+character.transform.position+" toward "+target);}
                }
                await Capture("town-continuous-junctions-walk-completed.png");
            }
            Stage("RESTAURANT_WALKING_REGRESSION");var terrains=Terrain.activeTerrains;var beach=new Vector3(28,0,49);var land=terrains.Single(t=>beach.x>=t.transform.position.x&&beach.z>=t.transform.position.z&&beach.x<=t.transform.position.x+t.terrainData.size.x&&beach.z<=t.transform.position.z+t.terrainData.size.z);beach.y=land.SampleHeight(beach)+land.transform.position.y+.08f;
            input.InputMove=Vector2.zero;character.SetVelocity(Vector3.zero);character.SetPosition(beach,true);character.SetMovementMode(Character.MovementMode.Falling);await Pause(.6f);float last=0;
            foreach(var target in new[]{new Vector3(28,0,55),new Vector3(24,0,55),new Vector3(21.5f,0,55),new Vector3(21.5f,0,60),new Vector3(21.5f,0,67.3f)}) {
                float begin=Time.realtimeSinceStartup;bool reached=false;
                while(Time.realtimeSinceStartup-begin<14){var delta=target-character.transform.position;delta.y=0;if(delta.magnitude<.22f){reached=true;break;}var direction=delta.normalized;character.LookAtInstant(character.transform.position+Vector3.up*1.6f+direction*10);input.InputMove=new Vector2(Vector3.Dot(direction,character.GetRightVector()),Vector3.Dot(direction,character.GetForwardVector()))*Mathf.Min(.45f,delta.magnitude);if(Time.realtimeSinceStartup-last>.1f){report.walkingSamples++;if(character.IsGrounded()&&character.movementMode==Character.MovementMode.Walking)report.groundedWalkingSamples++;last=Time.realtimeSinceStartup;}await UniTask.Yield();}
                input.InputMove=Vector2.zero;walks.Add(new Walk{target=target,arrived=character.transform.position,reached=reached});if(!reached)break;
            }
            report.walks=walks.ToArray();report.walkedToRestaurant=walks.Count==5&&walks.All(x=>x.reached);await Capture("town-restaurant-walk-completed.png");
            report.visibleVersionTexts=UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).Where(t=>t.isActiveAndEnabled&&System.Text.RegularExpressions.Regex.IsMatch(t.text??"",@"\bv?\d+\.\d+\.\d+\b")).Select(t=>P(t.transform)+"="+t.text).ToArray();
            report.passed=report.ready&&report.menuVersionHidden&&report.menuFeedbackHidden&&report.pauseVersionHidden&&report.pauseFeedbackHidden&&report.tabOpened&&report.runnerHidden&&report.runnerUnregistered&&report.mapButtonOpened&&report.geometryUpdates&&report.zoomWorks&&report.markerTracksPlayer&&report.locationClickRoundTrip&&report.underwaterSwitched&&report.mapClosedCamera&&report.mainRoads==(NewRoadMode?10:9)&&(!NewRoadMode||(report.reservedParcels==13&&report.streetAndParcelWalked))&&report.npcFurniture==24&&report.remainingRemoved==0&&report.shops==2&&(!CorrectedRoads||(report.signals==0&&report.shopClearanceWalks==4&&(!MovedHouses||(report.movedHousePositionsMatch&&report.throughMarketAvenue))))&&(!UnifiedRoads||(report.marketGameplayPointsMatch&&report.nativePavingSupported&&report.unifiedSurfaceMeshesMatch&&report.junctionWalkingStages==6&&report.junctionWalkingSamples==report.junctionGroundedSamples))&&report.npcAnchorsResolved==21&&report.walkedToRestaurant&&report.visibleVersionTexts.Length==0&&Errors.Count==0;
            Stage(report.passed?"PASS":"CHECK_FAILED");
        }catch(Exception e){report.error=e.ToString();TownMapDiagnostics.Capture();Stage("FAILED");}
        finally {inputSettings.editorInputBehaviorInPlayMode=oldFocus;inputSettings.backgroundBehavior=oldBackground;if(input)input.InputMove=Vector2.zero;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(!report.passed&&File.Exists(Path.Combine(Reports,"town-map-debug.flag"))){SessionState.SetBool(Key+"done",false);}else{SessionState.SetBool(Key+"done",true);EditorApplication.ExitPlaymode();}}
    }
}




