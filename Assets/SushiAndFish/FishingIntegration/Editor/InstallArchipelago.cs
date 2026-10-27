using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using HowToFish;
using SushiAndFish.FishingIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
public static class InstallArchipelago {
 const string Root="Assets/SushiAndFish/FishingIntegration/Archipelago/",Out="D:/UnityProjects/_CodexWork/Archipelago-20261026/";
 [Serializable] sealed class Sub{public int[] triangles;}
 [Serializable] sealed class Geometry{public string key,name;public Vector3[] vertices,normals;public Vector2[] uv;public Sub[] submeshes;}
 [Serializable] sealed class Mat{public string key,name,texture;public Color color;}
 [Serializable] sealed class Catalog{public Mat[] materials;}
 [Serializable] sealed class Col{public string kind,mesh;public Vector3 center,size;public float radius,height;public int direction;public bool convex;}
 [Serializable] sealed class Node{public int id,parent;public string name,mesh;public Vector3 p,s;public Quaternion q;public bool active;public string[] materials;public Col[] colliders;}
 [Serializable] sealed class Model{public string title,file,sourceSha256;public int root;public Node[] nodes;}
 static readonly List<string> checks=new List<string>();static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
 static void Check(bool v,string text){checks.Add((v?"PASS ":"FAIL ")+text);if(!v){File.WriteAllLines(Out+"Checks.txt",checks);throw new Exception(text);}}
 static T Save<T>(T value,string path)where T:Object{var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old){EditorUtility.CopySerialized(value,old);Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(value,path);return value;}
 public static void BuildReviewed(){BuildAndInspect();VerifyArchipelago.Run();}
 public static void BuildAndInspect(){
  Check(!EditorApplication.isPlaying,"Static import and map inspection only; no Play or physics stepping");Directory.CreateDirectory(Root+"Meshes");Directory.CreateDirectory(Root+"Materials");Directory.CreateDirectory(Root+"Prefabs");AssetDatabase.Refresh();
  foreach(var path in Directory.GetFiles(Root+"Source","*.json")){
   var dto=JsonUtility.FromJson<Geometry>(File.ReadAllText(path));if(dto.vertices==null)continue;var mesh=new Mesh{name=dto.name,indexFormat=dto.vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=dto.vertices;if(dto.normals!=null&&dto.normals.Length==dto.vertices.Length)mesh.normals=dto.normals;if(dto.uv!=null&&dto.uv.Length==dto.vertices.Length)mesh.uv=dto.uv;mesh.subMeshCount=dto.submeshes.Length;for(int i=0;i<dto.submeshes.Length;i++)mesh.SetTriangles(dto.submeshes[i].triangles,i);if(mesh.normals.Length==0)mesh.RecalculateNormals();mesh.RecalculateBounds();meshes[dto.key]=Save(mesh,Root+"Meshes/"+dto.key+".asset");
  }
  foreach(var path in Directory.GetFiles(Root+"Source","*.png")){var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Default;imp.filterMode=FilterMode.Point;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.mipmapEnabled=false;imp.SaveAndReimport();}
  var cat=JsonUtility.FromJson<Catalog>(File.ReadAllText(Root+"Source/Catalog.json"));foreach(var m in cat.materials){var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=m.name};mat.SetColor("_BaseColor",m.color);if(!string.IsNullOrEmpty(m.texture))mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Source/"+m.texture+".png"));mat.SetFloat("_Smoothness",.12f);mat.SetFloat("_Cull",0);materials[m.key]=Save(mat,Root+"Materials/"+m.key+".mat");}
  var boat=Build("OriginalBoat",true,out var boatNodes);var rig=boat.AddComponent<OriginalBoatRig>();rig.body=boat.AddComponent<Rigidbody>();rig.body.mass=100;rig.body.linearDamping=.75f;rig.body.angularDamping=2;rig.body.interpolation=RigidbodyInterpolation.Interpolate;rig.body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
  rig.forcePoints=new[]{5375,4011,4517,5786,4961,4352}.Select(i=>boatNodes[i]).ToArray();rig.motor=boatNodes[4590];rig.propeller=boatNodes[4959];rig.steering=boatNodes[5506];rig.throttle=boatNodes[5087];rig.driverSeat=boatNodes[5481];rig.hull=boat.GetComponentsInChildren<Collider>(true);
  Check(rig.hull.Length==17&&rig.hull.All(c=>c is BoxCollider||c is CapsuleCollider||c is MeshCollider mc&&mc.convex),"Boat reuses its original 17-part compound physical hull without duplicate proxy collider rigs");
  var boatPrefab=PrefabUtility.SaveAsPrefabAsset(boat,Root+"Prefabs/HowToFishBoat.prefab").GetComponent<OriginalBoatRig>();ReportBoat(rig);Render(boat,"boat.png");Object.DestroyImmediate(boat);
  var one=Build("Island1",false,out var oneNodes);var two=Build("Island2",false,out var twoNodes);var dock=Build("OriginalDock",false,out _);foreach(var mesh in dock.GetComponentsInChildren<MeshFilter>())mesh.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh.sharedMesh;
  var report=new StringBuilder();foreach(var pair in new[]{("Island1",one), ("Island2",two), ("Dock",dock)}){report.AppendLine(pair.Item1+": "+Bounds(pair.Item2));Render(pair.Item2,pair.Item1+".png");}
  var island1=PrefabUtility.SaveAsPrefabAsset(one,Root+"Prefabs/HowToFishIsland1.prefab");var island2=PrefabUtility.SaveAsPrefabAsset(two,Root+"Prefabs/HowToFishIsland2.prefab");var dockPrefab=PrefabUtility.SaveAsPrefabAsset(dock,Root+"Prefabs/HowToFishDock.prefab");
  float radius1=IslandRadius(one),radius2=IslandRadius(two);Object.DestroyImmediate(one);Object.DestroyImmediate(two);Object.DestroyImmediate(dock);
  AssetDatabase.SaveAssets();
  EditorSceneManager.OpenScene("Assets/SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity",OpenSceneMode.Single);
  var settings=ScriptableObject.CreateInstance<ArchipelagoSettings>();settings.boatPrefab=AssetDatabase.LoadAssetAtPath<OriginalBoatRig>(Root+"Prefabs/HowToFishBoat.prefab");
  settings.motorStart=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Audio/BONUS_MotorboatStart_Mono_01.wav");settings.motorStop=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Audio/BONUS_MotorboatStop_Mono_01.wav");settings.motorRun=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Audio/BONUS_MotorboatFast_Loop_Mono_01.wav");settings.motorIdle=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Audio/BONUS_MotorboatIdle_Loop_Mono_01.wav");settings.waterMoving=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Audio/BoatIdle_WithWaves_Loop.wav");
  var restaurant=Object.FindFirstObjectByType<RestaurantManager>(FindObjectsInactive.Include);Check(restaurant&&restaurant.WorldAnchor,"Main scene contains the original sushi restaurant world anchor");Vector3 anchor=restaurant.WorldAnchor.position;report.AppendLine("Restaurant anchor: "+anchor.ToString("F3"));Physics.SyncTransforms();
  const float sea=-4;Vector3 spawn=FindBoatWater(anchor,sea);settings.boatSpawn=spawn+Vector3.up*.25f;var outward=Vector3.ProjectOnPlane(spawn-anchor,Vector3.up).normalized;settings.boatRotation=Quaternion.LookRotation(outward);
  var center1=FindIslandWater(spawn,Quaternion.Euler(0,-25,0)*outward,230,radius1+15,sea);var center2=FindIslandWater(spawn,Quaternion.Euler(0,35,0)*outward,440,radius2+20,sea);
  settings.regions=new[]{new FishingIslandRegion{title="主岛",center=anchor,dock=spawn,fishingRadius=150},new FishingIslandRegion{title="小岛1 · 岩塔岛",center=center1,dock=center1-outward*(radius1+7),prefab=island1,fishingRadius=radius1+95},new FishingIslandRegion{title="小岛2 · 白骨岛",center=center2,dock=center2-outward*(radius2+7),prefab=island2,fishingRadius=radius2+110}};
  var staticIslands=new[]{Object.Instantiate(island1,center1,Quaternion.identity),Object.Instantiate(island2,center2,Quaternion.identity)};Physics.SyncTransforms();
  var access=BuildAccess(settings,dockPrefab,report);settings.accessPrefab=PrefabUtility.SaveAsPrefabAsset(access,Root+"Prefabs/HowToFishAccessDocks.prefab");
  AssignFish(settings,MigrationConfig.Load().profile.fish);settings=Save(settings,"Assets/SushiAndFish/FishingIntegration/Resources/FishingArchipelago.asset");
  report.AppendLine("Boat spawn: "+settings.boatSpawn.ToString("F3"));foreach(var region in settings.regions)report.AppendLine(region.title+": "+region.center.ToString("F3")+" radius="+region.fishingRadius+" fish="+region.fish.Length);
  File.WriteAllText(Out+"Placement.txt",report.ToString());AssetDatabase.SaveAssets();
  // Static placement preview only; no gameplay script callbacks are invoked.
  var b=Object.Instantiate(boatPrefab.gameObject,settings.boatSpawn,settings.boatRotation);b.GetComponent<Rigidbody>().isKinematic=true;foreach(var script in b.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
  RenderMap(anchor,settings.boatSpawn,center1,center2);Object.DestroyImmediate(b);foreach(var item in staticIslands)Object.DestroyImmediate(item);Object.DestroyImmediate(access);
  Verify();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
 }
 static GameObject Build(string name,bool boat,out Dictionary<int,Transform> transforms){
  var source=JsonUtility.FromJson<Model>(File.ReadAllText(Root+"Source/"+name+".json"));var root=new GameObject(name);var nodes=source.nodes.ToDictionary(n=>n.id);transforms=source.nodes.ToDictionary(n=>n.id,n=>new GameObject(n.name).transform);
  bool InHidden(int id){while(id!=0){if(id==5009)return true;id=nodes[id].parent;}return false;}
  foreach(var n in source.nodes){var t=transforms[n.id];t.SetParent(n.parent==0?root.transform:transforms[n.parent],false);t.localPosition=n.p;t.localRotation=boat&&n.parent==0?Quaternion.identity:n.q;t.localScale=n.s;
   // Original static-batch helper meshes contain a shared, scene-wide sea
   // floor and baked label batch. They are not the island terrain mesh.
   if(!boat&&(n.name.StartsWith("UnderWaterTerrain")||n.name=="NameText")){t.gameObject.SetActive(false);continue;}
   if(!string.IsNullOrEmpty(n.mesh)){t.gameObject.AddComponent<MeshFilter>().sharedMesh=meshes[n.mesh];var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterials=n.materials.Select(m=>materials[m]).ToArray();}
   if(!boat||InHidden(n.id))foreach(var c in n.colliders??Array.Empty<Col>()){
    if(c.kind=="BoxCollider"){var col=t.gameObject.AddComponent<BoxCollider>();col.center=c.center;col.size=c.size;}
    else if(c.kind=="MeshCollider"&&(!boat||c.convex)){var col=t.gameObject.AddComponent<MeshCollider>();col.sharedMesh=meshes[c.mesh];col.convex=c.convex;}
    else if(c.kind=="SphereCollider"){var col=t.gameObject.AddComponent<SphereCollider>();col.center=c.center;col.radius=c.radius;}
    else if(c.kind=="CapsuleCollider"){var col=t.gameObject.AddComponent<CapsuleCollider>();col.center=c.center;col.radius=c.radius;col.height=c.height;col.direction=c.direction;}
   }
   t.gameObject.SetActive(n.active);
  }
  return root;
 }
 static Bounds Bounds(GameObject root){var bounds=new Bounds(root.transform.position,Vector3.zero);bool any=false;foreach(var r in root.GetComponentsInChildren<Renderer>()){if(!any){bounds=r.bounds;any=true;}else bounds.Encapsulate(r.bounds);}return bounds;}
 static float IslandRadius(GameObject root){var r=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Island")&&!r.name.Contains("Source")).ToArray();if(r.Length==0)return 35;var b=r[0].bounds;foreach(var next in r.Skip(1))b.Encapsulate(next.bounds);return Mathf.Max(b.extents.x,b.extents.z);}
 static bool OpenWater(Vector3 p,float sea,float margin=0){foreach(var offset in new[]{Vector3.zero,Vector3.right*margin,Vector3.left*margin,Vector3.forward*margin,Vector3.back*margin})if(Physics.Raycast(p+offset+Vector3.up*100,Vector3.down,out var hit,300,~0,QueryTriggerInteraction.Ignore)&&hit.point.y>sea-1.3f)return false;return true;}
 static Vector3 FindBoatWater(Vector3 anchor,float sea){foreach(float radius in new[]{32f,40,50,24,18,55,70})for(int i=0;i<32;i++){float angle=Mathf.PI/4+i*Mathf.PI/16;var p=new Vector3(anchor.x+Mathf.Sin(angle)*radius,sea,anchor.z+Mathf.Cos(angle)*radius);if(OpenWater(p,sea,5))return p;}throw new Exception("No clear boat water found near restaurant");}
 static Vector3 FindIslandWater(Vector3 start,Vector3 direction,float distance,float radius,float sea){for(int i=0;i<16;i++){var p=start+Quaternion.Euler(0,(i%2==0?1:-1)*(i/2)*12,0)*direction*(distance+(i/4)*40);p.y=sea;if(OpenWater(p,sea,radius)&&OpenWater(p+Vector3.right*radius,sea,radius*.5f)&&OpenWater(p-Vector3.right*radius,sea,radius*.5f))return p;}throw new Exception("No clear nearby island site");}
 static void AssignFish(ArchipelagoSettings settings,FishDefinition[] all){
  var pools=new[]{new List<FishDefinition>(),new List<FishDefinition>(),new List<FishDefinition>()};
  foreach(var group in all.GroupBy(f=>f.rarity)){int cursor=0;foreach(var fish in group.OrderBy(f=>f.originalId)){int island=group.Key==CatchRarity.Legendary?(cursor==0?0:cursor%2+1):cursor%3;pools[island].Add(fish);cursor++;}}
  for(int i=0;i<3;i++){var region=settings.regions[i];region.fish=pools[i].OrderBy(f=>f.originalId).ToArray();region.nearRare=new[]{.10f,.20f,.27f}[i];region.deepRare=new[]{.15f,.28f,.36f}[i];region.maxRare=new[]{.20f,.34f,.42f}[i];region.nearLegendary=new[]{.01f,.05f,.08f}[i];region.deepLegendary=new[]{.025f,.08f,.12f}[i];region.maxLegendary=new[]{.04f,.10f,.14f}[i];}
 }
 static GameObject BuildAccess(ArchipelagoSettings settings,GameObject dockPrefab,StringBuilder report){
  var root=new GameObject("Original wooden docks and shore gangplanks");var material=dockPrefab.GetComponentInChildren<MeshRenderer>().sharedMaterial;var dockBounds=Bounds(dockPrefab);
  var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);var slat=Object.Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);slat.name="Dock palette bridge slat";slat.uv=Enumerable.Repeat(new Vector2(.176f,.3911f),slat.vertexCount).ToArray();slat=Save(slat,Root+"Meshes/BridgeSlat.asset");Object.DestroyImmediate(cube);
  foreach(var region in settings.regions){var water=region.dock;bool main=region==settings.regions[0];var land=FindShore(water,-4,region.center,main?22:0,main ? .35f : 2.5f,main?3.5f:9.5f);var outward=Vector3.ProjectOnPlane(water-land,Vector3.up).normalized;var rotation=Quaternion.LookRotation(outward);
   var dock=Object.Instantiate(dockPrefab,root.transform);dock.name=region.title+" · 原版码头";dock.transform.SetPositionAndRotation(water,rotation);dock.transform.GetChild(0).localPosition-=new Vector3(dockBounds.center.x,0,dockBounds.center.z);
   var end=water+Vector3.up*dockBounds.max.y;var start=land+Vector3.up*.06f;var line=end-start;var frame=new GameObject(region.title+" · 靠岸栈桥");frame.transform.SetParent(root.transform,false);frame.transform.SetPositionAndRotation((start+end)/2,Quaternion.LookRotation(line,Vector3.up));var collider=frame.AddComponent<BoxCollider>();collider.size=new Vector3(2.2f,.14f,line.magnitude+.6f);collider.center=Vector3.down*.07f;
   // Reuse the dock's original palette. Slats are visual; one continuous
   // collision surface prevents feet catching in the gaps between boards.
   int boards=Mathf.CeilToInt(line.magnitude/.5f);for(int i=0;i<=boards;i++){var board=new GameObject("原木桥板",typeof(MeshFilter),typeof(MeshRenderer));board.transform.SetParent(frame.transform,false);board.transform.localPosition=new Vector3(0,-.07f,(i/(float)boards-.5f)*line.magnitude);board.transform.localScale=new Vector3(2.2f,.14f,.48f);board.GetComponent<MeshFilter>().sharedMesh=slat;board.GetComponent<MeshRenderer>().sharedMaterial=material;}
   for(float z=-line.magnitude/2;z<line.magnitude/2;z+=4)foreach(float x in new[]{-1f,1f}){var post=new GameObject("原木支撑柱",typeof(MeshFilter),typeof(MeshRenderer));post.transform.SetParent(frame.transform,false);post.transform.localPosition=new Vector3(x,-1.3f,z);post.transform.localScale=new Vector3(.14f,2.6f,.14f);post.GetComponent<MeshFilter>().sharedMesh=slat;post.GetComponent<MeshRenderer>().sharedMaterial=material;}
   report.AppendLine(region.title+" shore="+land.ToString("F3")+" dock="+water.ToString("F3")+" ramp="+line.magnitude.ToString("F2")+" slope="+Vector3.Angle(line,Vector3.ProjectOnPlane(line,Vector3.up)).ToString("F1"));
   Check(Vector3.Angle(line,Vector3.ProjectOnPlane(line,Vector3.up))<32,region.title+": gangplank has a walkable slope and continuous collision");
   if(region==settings.regions[0]){Physics.SyncTransforms();bool found=false;foreach(float side in new[]{5.5f,-5.5f,6.5f,-6.5f,8,-8}){var candidate=water+rotation*Vector3.right*side;bool clear=true;foreach(float x in new[]{-1.7f,0,1.7f})foreach(float z in new[]{-4.8f,0,4.8f})if(!OpenWater(candidate+rotation*new Vector3(x,0,z),-4))clear=false;if(clear){settings.boatSpawn=candidate+Vector3.up*.25f;found=true;break;}}Check(found,"Boat starts beside the sushi restaurant dock with clearance under the whole hull");settings.boatRotation=rotation;region.dock=settings.boatSpawn-Vector3.up*.25f;}
  }return root;
 }
 static Vector3 FindShore(Vector3 water,float sea,Vector3 avoid,float avoidRadius,float minimumHeight,float maximumHeight){Vector3 best=default;float score=float.PositiveInfinity;for(float radius=2;radius<=65;radius+=1.5f)for(int i=0;i<64;i++){float angle=i*Mathf.PI/32;var p=water+new Vector3(Mathf.Sin(angle)*radius,100,Mathf.Cos(angle)*radius);if(Vector3.ProjectOnPlane(p-avoid,Vector3.up).sqrMagnitude<avoidRadius*avoidRadius||!Physics.Raycast(p,Vector3.down,out var hit,150,~0,QueryTriggerInteraction.Ignore)||hit.point.y<sea+minimumHeight||hit.point.y>sea+maximumHeight||hit.normal.y<.65f)continue;float value=radius+Mathf.Abs(hit.point.y-sea)*2;if(value<score){score=value;best=hit.point;}}if(!float.IsFinite(score))throw new Exception("No accessible shore near "+water);return best;}
 static void ReportBoat(OriginalBoatRig rig){File.WriteAllText(Out+"BoatRig.txt","mass="+rig.body.mass+" forcePoints="+rig.forcePoints.Length+" hull="+rig.hull.Length+"\nseat="+rig.driverSeat.position+"\npropeller="+rig.propeller.position+" right="+rig.propeller.right+"\nbounds="+Bounds(rig.gameObject));}
 static void Render(GameObject model,string filename){var all=model.GetComponentsInChildren<Transform>(true);var layers=all.Select(t=>t.gameObject.layer).ToArray();foreach(var t in all)t.gameObject.layer=29;var b=Bounds(model);var cam=new GameObject("Static source model camera",typeof(Camera)).GetComponent<Camera>();cam.cullingMask=1<<29;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.18f,.33f,.40f);cam.transform.position=b.center+new Vector3(1,.8f,-1).normalized*Mathf.Max(10,b.size.magnitude*1.2f);cam.transform.LookAt(b.center);cam.nearClipPlane=.05f;cam.farClipPlane=3000;var light=new GameObject("Static source light",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(50,-30,0);light.intensity=1.5f;RenderSettings.ambientLight=Color.gray;Capture(cam,filename);for(int i=0;i<all.Length;i++)all[i].gameObject.layer=layers[i];Object.DestroyImmediate(cam.gameObject);Object.DestroyImmediate(light.gameObject);}
 static void RenderMap(Vector3 anchor,Vector3 spawn,Vector3 one,Vector3 two){var cam=new GameObject("Static archipelago map camera",typeof(Camera)).GetComponent<Camera>();cam.orthographic=true;cam.orthographicSize=350;var center=(anchor+one+two)/3;cam.transform.SetPositionAndRotation(center+Vector3.up*650,Quaternion.Euler(90,0,0));cam.farClipPlane=1500;Capture(cam,"map-overview.png");cam.orthographic=false;var outward=Vector3.ProjectOnPlane(spawn-anchor,Vector3.up).normalized;cam.transform.position=spawn+outward*22+Vector3.up*8;cam.transform.LookAt(spawn+Vector3.up);Capture(cam,"restaurant-boat-placement.png");Object.DestroyImmediate(cam.gameObject);}
 static void Capture(Camera camera,string name){var target=new RenderTexture(1600,1000,24);camera.targetTexture=target;camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var tex=new Texture2D(1600,1000,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();File.WriteAllBytes(Out+name,tex.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;Object.DestroyImmediate(tex);Object.DestroyImmediate(target);}
 public static void Verify(){var settings=ArchipelagoSettings.Load();Check(settings&&settings.boatPrefab&&settings.regions.Length==3,"Native main island plus two original imported island prefabs configured");Check(settings.regions.Skip(1).All(r=>r.prefab&&r.prefab.GetComponentsInChildren<MeshCollider>().Length>0),"Both islands retain walkable original collision geometry");Check(settings.regions.SelectMany(r=>r.fish).Distinct().Count()==60&&settings.regions.Sum(r=>r.fish.Length)==60,"All 60 existing fish preserved in distinct regional pools");foreach(var r in settings.regions)Check(r.fish.Any(f=>f.shallowWaterFish&&f.rarity==CatchRarity.Common),r.title+": shallow-water guaranteed catch remains available");File.WriteAllLines(Out+"Checks.txt",checks);Debug.Log("ARCHIPELAGO_IMPORT_PASS checks="+checks.Count+" play=false simulation=false");}
}
