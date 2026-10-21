using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEngine.SceneManagement;using UnityEngine.Rendering;using UnityEditor;using UnityEditor.SceneManagement;using SushiAndFish.Casino;using Object=UnityEngine.Object;
public static partial class CityExpansionInstallation {
 public static void ApplyCasinoParkRoadGeometry(Scene scene){
  var data=JsonUtility.FromJson<IslandRevision20261018.UnifiedGeometry>(File.ReadAllText(Path.Combine(CityExpansionInspection.Reports,"casino-park-road-geometry.json")));
  if(data.humanMessage!="01a15c61-ab8a-7820-b50d-4132af723546")throw new Exception("Unexpected road plan owner");
  var network=scene.GetRootGameObjects().Single(g=>g.name=="TownRoadNetwork20261019").transform;
  string folder="Assets/SushiAndFish/Casino/DetailedRevision20261021/RoadMeshes";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
  foreach(var surface in data.surfaces){var owner=network.GetComponentsInChildren<Transform>(true).Single(t=>t.name==surface.name);float y=surface.kind=="asphalt"?data.asphaltY:surface.kind=="paint"?data.paintY:data.curbTopY;SetRoadMesh(owner,surface.vertices.Select(v=>new Vector3(v.x,y,v.z)).ToArray(),(int[])surface.triangles.Clone(),AssetDatabase.GenerateUniqueAssetPath(folder+"/"+surface.name+".asset"));}
  WallMesh(network.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Unified_CurbFaces"),data.curbInnerRings,data.curbTopY,p=>data.asphaltY,AssetDatabase.GenerateUniqueAssetPath(folder+"/CurbFaces.asset"));
  WallMesh(network.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Unified_OuterRoadbed"),data.outerPavingRings,data.curbTopY,p=>Ground(p.x,p.z)-.025f,AssetDatabase.GenerateUniqueAssetPath(folder+"/OuterRoadbed.asset"));
  foreach(var line in data.plan.roads){var holder=network.Find("RoadSections/"+line.id);if(!holder){holder=new GameObject(line.id).transform;holder.SetParent(network.Find("RoadSections"),false);}holder.position=new Vector3((line.a.x+line.b.x)/2,data.asphaltY,(line.a.z+line.b.z)/2);}
  foreach(var n in new[]{"ParkNorthApproach","ParkSouthApproach"}){var t=network.Find("RoadSections/"+n);if(t)Object.DestroyImmediate(t.gameObject);}
 }
}
public static class CasinoDetailedRepair {
 const string GS="Assets/SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity",CS="Assets/SushiAndFish/Casino/Imported/Scenes/CasinoScene.unity",DS="Assets/SushiAndFish/Modules/OriginalIsland/Scenes/DressingScenes/100_Dressing_City_01.unity";
 const string Output="Assets/SushiAndFish/Casino/DetailedRevision20261021";
 [Serializable]public class MeshChange{public string path,source,result;public int vertices;public float largestChange;}
 [Serializable]public class Receipt{public string utc,error;public bool saved,frontUnchanged;public Vector3 min,max;public List<MeshChange> meshes=new List<MeshChange>();public List<string> furniture=new List<string>();}
 static Receipt q;static string P(Transform t)=>t.parent?P(t.parent)+"/"+t.name:t.name;
 static Vector3 Contract(Vector3 p){float sign=Mathf.Sign(p.x);if(Mathf.Abs(p.x)>22)p.x=sign*(22+(Mathf.Abs(p.x)-22)*.20f);if(p.z>82)p.z=82+(p.z-82)*.25f;return p;}
 static bool IsEnvelope(Transform t,Transform root){string p=P(t);return p.Contains("/Casino/SM_CasinoBase01/")||p.EndsWith("/Casino/Cowboy_1")||p.EndsWith("/Casino/CasinoCigar")||p.EndsWith("/Casino/CasinoCigar2")||p.Contains("/BuildingEnvelopeCompletion/");}
 static Mesh CopyMesh(Transform owner,Transform root,Mesh src){
  var verts=src.vertices;float change=0;for(int i=0;i<verts.Length;i++){var original=verts[i];var n=root.InverseTransformPoint(owner.TransformPoint(original));verts[i]=owner.InverseTransformPoint(root.TransformPoint(Contract(n)));change=Mathf.Max(change,Vector3.Distance(original,verts[i]));}
  var m=Object.Instantiate(src);m.name=src.name+"_ParkFootprint";m.vertices=verts;m.RecalculateNormals();m.RecalculateBounds();string path=AssetDatabase.GenerateUniqueAssetPath(Output+"/Envelope_"+q.meshes.Count+".asset");AssetDatabase.CreateAsset(m,path);q.meshes.Add(new MeshChange{path=P(owner),source=AssetDatabase.GetAssetPath(src),result=path,vertices=verts.Length,largestChange=change});return m;
 }
 static void Patch(Transform root){
  if(root.Find("FootprintRevision_20261021"))return;
  foreach(var f in root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh&&IsEnvelope(f.transform,root))){var old=f.sharedMesh;f.sharedMesh=CopyMesh(f.transform,root,old);foreach(var c in f.GetComponents<MeshCollider>())if(c.sharedMesh==old){c.sharedMesh=null;c.sharedMesh=f.sharedMesh;}}
  foreach(var c in root.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.sharedMesh&&IsEnvelope(c.transform,root)&&!c.sharedMesh.name.EndsWith("_ParkFootprint"))){c.sharedMesh=CopyMesh(c.transform,root,c.sharedMesh);}
  foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Ground"&&P(t).Contains("/EntranceCasinoGRP/"))){t.gameObject.SetActive(true);var f=t.GetComponent<MeshFilter>();if(f){var c=t.GetComponent<MeshCollider>();if(!c)c=t.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;}}
  new GameObject("FootprintRevision_20261021").transform.SetParent(root,false);
 }
 public static void Apply(){q=new Receipt{utc=DateTime.UtcNow.ToString("o"),frontUnchanged=true};try{
  Directory.CreateDirectory(Output);AssetDatabase.Refresh();var scene=EditorSceneManager.OpenScene(GS);var a=Object.FindFirstObjectByType<ParkCasinoIntegration>();
  var protectedBefore=a.permanentExterior.GetComponentsInChildren<MeshFilter>(true).Where(f=>!IsEnvelope(f.transform,a.permanentExterior.transform)&&f.sharedMesh).ToDictionary(f=>f,f=>f.sharedMesh);
  Patch(a.permanentExterior.transform);CasinoHostRepair.TrimExterior(a.permanentExterior);foreach(var c in a.permanentExterior.GetComponentsInChildren<Collider>(true))c.enabled=false;
  q.frontUnchanged=protectedBefore.All(x=>x.Key.sharedMesh==x.Value);if(!q.frontUnchanged)throw new Exception("Protected entrance/lobby geometry changed");
  var b=new Bounds();bool init=false;foreach(var r in a.permanentExterior.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled)){if(!init){b=r.bounds;init=true;}else b.Encapsulate(r.bounds);}q.min=b.min;q.max=b.max;
  var road=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="CasinoFrontRoadOnly");if(road)Object.DestroyImmediate(road);CityExpansionInstallation.ApplyCasinoParkRoadGeometry(scene);
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
  var ds=EditorSceneManager.OpenScene(DS,OpenSceneMode.Additive);foreach(var seat in Object.FindObjectsByType<EmoteArea>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(s=>!s.GetComponentInParent<ParkCasinoIntegration>()))q.furniture.Add(P(seat.transform)+" @"+seat.transform.position);
  EditorSceneManager.CloseScene(ds,true);scene=EditorSceneManager.OpenScene(CS);var building=scene.GetRootGameObjects().Single(g=>g.name=="CasinoBuilding");Patch(building.transform);foreach(var root in scene.GetRootGameObjects())CasinoHostRepair.TrimExterior(root);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();q.saved=true;
  IslandFrontEndBuilder.RefreshMenuPresentation();EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
 }catch(Exception e){q.error=e.ToString();Debug.LogException(e);throw;}finally{File.WriteAllText(Path.Combine(IslandRevision20261018.Reports,"casino-detailed-repair-saved.json"),JsonUtility.ToJson(q,true));}}
 public static void ApplyRoadsOnly(){var scene=EditorSceneManager.OpenScene(GS);CityExpansionInstallation.ApplyCasinoParkRoadGeometry(scene);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");}
 public static void ApplyAndProbe(){Apply();CityExpansionPlayAudit.DetailedInput();}
}
