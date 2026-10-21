using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.SceneManagement;
public static class TownCoastRepair20261021{
 static string Reports=>IslandRevision20261018.Reports;
 static string GS="Assets/SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity";
 static string DS="Assets/SushiAndFish/Modules/OriginalIsland/Scenes/DressingScenes/100_Dressing_City_01.unity";
 [Serializable] public class Node {public string scene,path,prefab,mesh,materials,components;public bool active;public Vector3 position,scale,min,max;public Quaternion rotation;public float groundCentre,groundInside,groundOutside;}
 [Serializable] public class Receipt {public string utc,error;public List<Node> structures=new List<Node>();public List<Node> counters=new List<Node>();public List<Node> terrains=new List<Node>();public List<string> main=new List<string>();}
 static string P(Transform t)=>t.parent?P(t.parent)+"/"+t.name:t.name;
 static IEnumerable<Transform> All(Scene s)=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true));
 static float Ground(Vector3 pos){float h=-999;foreach(var t in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None)){var p=t.transform.position;var z=t.terrainData.size;if(pos.x>=p.x&&pos.x<=p.x+z.x&&pos.z>=p.z&&pos.z<=p.z+z.z)h=Mathf.Max(h,t.SampleHeight(pos)+p.y);}return h;}
 static Node Row(Transform t){var rr=t.GetComponent<Renderer>();var mf=t.GetComponent<MeshFilter>();Bounds b=rr?rr.bounds:new Bounds(t.position,Vector3.zero);var co=t.GetComponent<Collider>();if(!rr&&co)b=co.bounds;var v=b.center;var side=b.size.x>b.size.z?Vector3.forward:Vector3.right;return new Node{scene=t.gameObject.scene.path,path=P(t),active=t.gameObject.activeInHierarchy,prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject),mesh=mf&&mf.sharedMesh?AssetDatabase.GetAssetPath(mf.sharedMesh):"",materials=rr?string.Join(";",rr.sharedMaterials.Select(m=>m?m.name:"NULL")):"",components=string.Join(";",t.GetComponents<Component>().Where(c=>c).Select(c=>c.GetType().Name)),position=t.position,rotation=t.rotation,scale=t.lossyScale,min=b.min,max=b.max,groundCentre=Ground(v),groundInside=Ground(v+side*.6f),groundOutside=Ground(v-side*.6f)};}
 public static void Inspect(){var r=new Receipt{utc=DateTime.UtcNow.ToString("o")};try{
 var main=EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");foreach(var t in All(main))foreach(var m in t.GetComponents<MonoBehaviour>())if(m)r.main.Add(P(t)+" "+m.GetType().FullName+" active="+t.gameObject.activeInHierarchy+" enabled="+m.enabled);
 var gs=EditorSceneManager.OpenScene(GS);var ds=EditorSceneManager.OpenScene(DS,OpenSceneMode.Additive);Physics.SyncTransforms();
 foreach(var t in All(gs).Concat(All(ds))){
 if(t.GetComponent<Terrain>()){var q=Row(t);q.max=t.position+t.GetComponent<Terrain>().terrainData.size;r.terrains.Add(q);}
 if(t.name.IndexOf("Counter",StringComparison.OrdinalIgnoreCase)>=0&&P(t).Contains("AR_CinemaLobby"))r.counters.Add(Row(t));
 if(t.GetComponent<MeshFilter>()&&(P(t).StartsWith("RestoredIslandPerimeter")||System.Text.RegularExpressions.Regex.IsMatch(t.name,@"stair|railing|SideWalkTerrainEdge|SideWalkWall|CornerFence",System.Text.RegularExpressions.RegexOptions.IgnoreCase)))r.structures.Add(Row(t));
 }
 }catch(Exception e){r.error=e.ToString();Debug.LogException(e);}File.WriteAllText(Path.Combine(Reports,"town-coast-repair-inspection.json"),JsonUtility.ToJson(r,true));EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");}

 public static void InspectOriginalAndRepairCounter(){
 var r=new Receipt{utc=DateTime.UtcNow.ToString("o")};try{
 var gs=EditorSceneManager.OpenScene(GS);
 foreach(var t in All(gs).Where(t=>P(t).Contains("AR_CinemaLobby")&&(t.name.EndsWith("ConcessionCounterBody")||t.name.EndsWith("ConcessionCounterTop")))){var f=t.GetComponent<MeshFilter>();if(!f||!f.sharedMesh)continue;var c=t.GetComponent<MeshCollider>();if(!c)c=t.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;c.enabled=true;r.counters.Add(Row(t));}
 EditorSceneManager.MarkSceneDirty(gs);EditorSceneManager.SaveScene(gs);
 var prefab=PrefabUtility.LoadPrefabContents("Assets/SushiAndFish/CityExpansion/Prefabs/AR_CinemaLobby.prefab");
 try{foreach(var t in prefab.GetComponentsInChildren<Transform>(true).Where(t=>t.name.EndsWith("ConcessionCounterBody")||t.name.EndsWith("ConcessionCounterTop"))){var f=t.GetComponent<MeshFilter>();if(!f)continue;var c=t.GetComponent<MeshCollider>();if(!c)c=t.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;}PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/SushiAndFish/CityExpansion/Prefabs/AR_CinemaLobby.prefab");}finally{PrefabUtility.UnloadPrefabContents(prefab);}
 var source=EditorSceneManager.OpenScene("Assets/SushiAndFish/RevisionReference_20261021/OriginalCityBeforeTrim.unity",OpenSceneMode.Additive);Physics.SyncTransforms();
 foreach(var t in All(source).Where(t=>t.GetComponent<MeshFilter>())){
  var n=t.name.ToLowerInvariant();var path=P(t);
  bool candidate=System.Text.RegularExpressions.Regex.IsMatch(path,@"^Root/Comps/Comp9(?:/| \(22\)/| \(23\)/| \(24\)/| \(17\)/| \(20\)/| \(21\)/)") || n.Contains("wall") || n.Contains("stairs")||n.Contains("railing")||n.Contains("fence")||n.Contains("SideWalkTerrainEdge".ToLowerInvariant())||n.Contains("sidewalk")&&t.position.z>27;
  if(candidate)r.structures.Add(Row(t));
 }
 AssetDatabase.SaveAssets();
 }catch(Exception e){r.error=e.ToString();Debug.LogException(e);}File.WriteAllText(Path.Combine(Reports,"town-coast-original-structures.json"),JsonUtility.ToJson(r,true));EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
 }
}
