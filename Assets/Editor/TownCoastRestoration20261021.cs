using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine.AI;
using PaperPixelGames.Core.DressingSceneSystem;
using SushiAndFish.CityExpansion;
using Object=UnityEngine.Object;

public static class TownCoastRestoration20261021 {
 const string GS="Assets/SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity";
 const string DS="Assets/SushiAndFish/Modules/OriginalIsland/Scenes/DressingScenes/100_Dressing_City_01.unity";
 const string Source="Assets/SushiAndFish/RevisionReference_20261021/OriginalCityBeforeTrim.unity";
 const string RootName="RestoredCoastConnections_20261021";
 const string AssetRoot="Assets/SushiAndFish/CityExpansion/CoastRevision20261021";
 static string Reports=>IslandRevision20261018.Reports;
 [Serializable]public class Item {public string source,path,reason;public Vector3 min,max;public bool existed;public float boundsError;}
 [Serializable]public class TerrainEdit {public string terrain;public int samples;public float maxLowering;}
 [Serializable]public class Receipt {public string utc,error,stage,navigation;public bool saved;public int selected,restored,alreadyPresent,northWideStairs,northEastStairs,coastWalkways,guardrails,terrainSamples;public List<Item> objects=new List<Item>();public List<TerrainEdit> terrain=new List<TerrainEdit>();public List<string> notes=new List<string>();}
 static string P(Transform t)=>t.parent?P(t.parent)+"/"+t.name:t.name;
 static IEnumerable<Transform> All(Scene s)=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true));
 static Bounds B(Transform t)=>t.GetComponent<Renderer>()?t.GetComponent<Renderer>().bounds:new Bounds(t.position,Vector3.zero);
 static bool Same(Bounds a,Bounds b)=>Vector3.Distance(a.min,b.min)<.018f&&Vector3.Distance(a.max,b.max)<.018f;
 static bool North(string p)=>Regex.IsMatch(p,@"^Root/Comps/Comp9(?:/| \((22|23|24)\)/)");
 static string Select(Transform t){
  string p=P(t),n=t.name.ToLowerInvariant();var b=B(t);
  bool structural=n.StartsWith("sm_wall")&&!n.Contains("flowerbed")||n.StartsWith("sm_stairs")||n.StartsWith("sm_sidewalk")||n.StartsWith("sm_fence");
  if(!structural)return null;
  if(North(p))return "north_beach_stair_and_landing_assembly";
  if(Regex.IsMatch(p,@"^Root/Comps/Comp9 \((17|20|21)\)/"))return "north_east_beach_stair_and_landing_assembly";
  if(p.StartsWith("Root/Comps/Comp11"))return "south_and_east_coast_complete_original_assembly";
  if(p.StartsWith("Root/Comps/CompOtopark/")&&n.StartsWith("sm_wall"))return "north_east_seawall";
  if(p.StartsWith("Root/DockSide/"))return "harbour_coastal_guardrail_and_walkway";
  if(p.StartsWith("Root/TunnelComp/")&&n.StartsWith("sm_fence"))return "original_tunnel_edge_guardrail";
  if(n.StartsWith("sm_sidewalk")||n.StartsWith("sm_curbs")){
   if(b.min.x>=108.7f&&b.max.x<=127&&b.min.z>=-94&&b.max.z<=32)return "east_seawall_walkway_capping";
   if(b.min.z>=-94&&b.max.z<=-72.7f&&b.min.x>=-156&&b.max.x<=127)return "south_seawall_walkway_capping";
   if(b.min.z>=26.8f&&b.max.z<=31.5f&&b.min.x>=60&&b.max.x<=116)return "north_east_seawall_walkway_capping";
  }
  return null;
 }
 static void Write(Receipt r){File.WriteAllText(Path.Combine(Reports,"town-coast-restoration-applied.json"),JsonUtility.ToJson(r,true));Debug.Log("COAST_REVISION "+r.stage);}
 public static void Apply(){
  var r=new Receipt{utc=DateTime.UtcNow.ToString("o"),stage="START"};Write(r);
  try{
   Directory.CreateDirectory(AssetRoot);AssetDatabase.Refresh();
   var gs=EditorSceneManager.OpenScene(GS);var ds=EditorSceneManager.OpenScene(DS,OpenSceneMode.Additive);
   var current=All(gs).Concat(All(ds)).Where(t=>t.GetComponent<MeshRenderer>()).ToList();
   var source=EditorSceneManager.OpenScene(Source,OpenSceneMode.Additive);Physics.SyncTransforms();
   var targets=All(source).Where(t=>t.GetComponent<MeshFilter>()&&t.GetComponent<MeshRenderer>()).Select(t=>(t,reason:Select(t))).Where(x=>x.reason!=null).ToArray();
   var root=ds.GetRootGameObjects().FirstOrDefault(g=>g.name==RootName);if(!root){root=new GameObject(RootName);SceneManager.MoveGameObjectToScene(root,ds);}
   var addedStairs=new List<Collider>();
   foreach(var x in targets){
    var sourceBounds=B(x.t);r.selected++;var item=new Item{source=P(x.t),reason=x.reason,min=sourceBounds.min,max=sourceBounds.max};
    var existing=current.FirstOrDefault(t=>Same(B(t),sourceBounds));
    if(existing){item.path=P(existing);item.existed=true;r.alreadyPresent++;r.objects.Add(item);continue;}
    var f=x.t.GetComponent<MeshFilter>();var sr=x.t.GetComponent<MeshRenderer>();var go=new GameObject(x.t.name);go.transform.SetParent(root.transform,false);
    go.transform.SetPositionAndRotation(x.t.position,x.t.rotation);go.transform.localScale=x.t.lossyScale;
    var mf=go.AddComponent<MeshFilter>();mf.sharedMesh=f.sharedMesh;var mr=go.AddComponent<MeshRenderer>();mr.sharedMaterials=sr.sharedMaterials;mr.shadowCastingMode=sr.shadowCastingMode;mr.receiveShadows=sr.receiveShadows;
    var bounds=B(go.transform);float error=Mathf.Max(Vector3.Distance(bounds.min,sourceBounds.min),Vector3.Distance(bounds.max,sourceBounds.max));
    if(error>.005f){
     var mesh=Object.Instantiate(f.sharedMesh);mesh.name="CoastOriginal_"+r.selected;var matrix=go.transform.worldToLocalMatrix*x.t.localToWorldMatrix;
     mesh.vertices=mesh.vertices.Select(v=>matrix.MultiplyPoint3x4(v)).ToArray();var normalMatrix=matrix.inverse.transpose;mesh.normals=mesh.normals.Select(v=>normalMatrix.MultiplyVector(v).normalized).ToArray();mesh.RecalculateBounds();
     var meshPath=AssetDatabase.GenerateUniqueAssetPath(AssetRoot+"/OriginalCoastMesh_"+r.selected+".asset");AssetDatabase.CreateAsset(mesh,meshPath);mf.sharedMesh=mesh;
     bounds=B(go.transform);error=Mathf.Max(Vector3.Distance(bounds.min,sourceBounds.min),Vector3.Distance(bounds.max,sourceBounds.max));
    }
    if(error>.025f)throw new Exception("Restored coast mesh bounds mismatch: "+P(x.t)+" "+error);
    var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=mf.sharedMesh;
    if(x.t.name.IndexOf("stairs",StringComparison.OrdinalIgnoreCase)>=0)addedStairs.Add(collider);
    if(North(P(x.t))&&x.t.name.Contains("stairs_wide"))r.northWideStairs++;
    if(x.reason.StartsWith("north_east")&&x.t.name.IndexOf("stairs",StringComparison.OrdinalIgnoreCase)>=0)r.northEastStairs++;
    if(x.t.name.IndexOf("sidewalk",StringComparison.OrdinalIgnoreCase)>=0)r.coastWalkways++;
    if(x.t.name.IndexOf("fence",StringComparison.OrdinalIgnoreCase)>=0)r.guardrails++;
    item.path=P(go.transform);item.boundsError=error;r.objects.Add(item);r.restored++;current.Add(go.transform);
   }
   // The reference scene must not contribute physics or navigation.
   EditorSceneManager.CloseScene(source,true);Physics.SyncTransforms();r.stage="ORIGINAL_COAST_RESTORED";Write(r);
   var walkSurfaces=root.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.name.IndexOf("stairs",StringComparison.OrdinalIgnoreCase)>=0 || c.name.IndexOf("sidewalk",StringComparison.OrdinalIgnoreCase)>=0 || c.bounds.size.y<.3f).ToArray();
   foreach(var terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None)){
    var data=terrain.terrainData;var origin=terrain.transform.position;var size=data.size;int n=data.heightmapResolution;var heights=data.GetHeights(0,0,n,n);int changes=0;float maxDrop=0;
    foreach(var c in walkSurfaces){var b=c.bounds;if(b.max.x<origin.x||b.min.x>origin.x+size.x||b.max.z<origin.z||b.min.z>origin.z+size.z)continue;
     int x0=Mathf.Clamp(Mathf.FloorToInt((b.min.x-origin.x)/size.x*(n-1)),0,n-1),x1=Mathf.Clamp(Mathf.CeilToInt((b.max.x-origin.x)/size.x*(n-1)),0,n-1);
     int z0=Mathf.Clamp(Mathf.FloorToInt((b.min.z-origin.z)/size.z*(n-1)),0,n-1),z1=Mathf.Clamp(Mathf.CeilToInt((b.max.z-origin.z)/size.z*(n-1)),0,n-1);
     for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++){
      var p=new Vector3(origin.x+x/(float)(n-1)*size.x,b.max.y+1,origin.z+z/(float)(n-1)*size.z);
      if(!c.Raycast(new Ray(p,Vector3.down),out var hit,b.size.y+3))continue;
      float old=origin.y+heights[z,x]*size.y,target=hit.point.y-.055f;if(old>target+.015f){heights[z,x]=(target-origin.y)/size.y;changes++;maxDrop=Mathf.Max(maxDrop,old-target);}
     }
    }
    if(changes>0){data.SetHeights(0,0,heights);EditorUtility.SetDirty(data);r.terrain.Add(new TerrainEdit{terrain=terrain.name,samples=changes,maxLowering=maxDrop});r.terrainSamples+=changes;}
   }
   r.stage="STAIR_TERRAIN_CONTACT_REPAIRED";Write(r);
   // Sidewalk already spans the seawall; this narrow infill joins the new east street edge to its original cap.
   var infill=root.transform.Find("EastStreetToOriginalCoastWalkway");if(!infill){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="EastStreetToOriginalCoastWalkway";g.transform.SetParent(root.transform,false);g.transform.position=new Vector3(109.0f,3.005f,-23f);g.transform.localScale=new Vector3(.90f,.13f,63.0f);var native=targets.SelectMany(x=>x.t?x.t.GetComponent<MeshRenderer>().sharedMaterials:Array.Empty<Material>()).FirstOrDefault(m=>m&&m.name.StartsWith("M_tiles_ground"));if(native)g.GetComponent<MeshRenderer>().sharedMaterial=native;}
   // Do not recreate buildings/seats; preserve their scene identities and interaction references.
   foreach(var s in new[]{gs,ds}){EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);}AssetDatabase.SaveAssets();r.saved=true;r.stage="SAVED";Write(r);
   EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
  }catch(Exception e){r.error=e.ToString();r.stage="FAILED";Write(r);Debug.LogException(e);throw;}
 }
}
