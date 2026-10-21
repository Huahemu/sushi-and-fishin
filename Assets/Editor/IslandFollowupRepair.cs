using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
public static class IslandFollowupRepair {
[Serializable] public class Result{public string utc,navMesh;public int restoredRetainingMeshes,groundedGreenery,removedDecals;public bool shopsUnchanged;public string[] changes;}
static string P(Transform t)=>IslandFrontEndInventory.PathOf(t);
public static void Run(){
var scenes=IslandComprehensiveRepair.Load();var city=scenes.First(s=>s.name=="100_Dressing_City_01");var game=scenes[0];var changes=new List<string>();var report=new Result{utc=DateTime.UtcNow.ToString("o")};
var stores=IslandWorldIssues.All(city).Where(t=>P(t)=="Root/Comps/GabeMarket"||P(t)=="Root/Comps/MarinaMarket").Concat(IslandWorldIssues.All(game).Where(t=>P(t)=="---Restaurant")).ToArray();
Func<Transform,string> signature=t=>string.Join(";",t.GetComponentsInChildren<Transform>(true).Select(x=>P(x)+EditorJsonUtility.ToJson(x)+string.Join("",x.GetComponents<Component>().Where(c=>c).Select(c=>EditorJsonUtility.ToJson(c)))));
var before=stores.Select(signature).ToArray();var map=IslandWorldIssues.All(city).GroupBy(P).ToDictionary(g=>g.Key,g=>g.First());var source=EditorSceneManager.OpenScene("Assets/Editor/ComprehensiveSourceCity.unity",OpenSceneMode.Additive);
foreach(var t in IslandWorldIssues.All(source).ToArray()){
var path=P(t);if(!(path.StartsWith("Root/Comps/CompOtopark/")||path.StartsWith("Root/Comps/Comp9 (17)/"))||!t.name.StartsWith("SM_wall")||map.ContainsKey(path)||!map.TryGetValue(P(t.parent),out var parent))continue;
var copy=UnityEngine.Object.Instantiate(t.gameObject,parent,false);copy.name=t.name;copy.transform.localPosition=t.localPosition;copy.transform.localRotation=t.localRotation;copy.transform.localScale=t.localScale;changes.Add("restore coastal parking retaining structure "+path);report.restoredRetainingMeshes++;
}EditorSceneManager.CloseScene(source,true);Physics.SyncTransforms();
foreach(var t in IslandWorldIssues.All(city).Where(t=>t.GetComponent<MeshFilter>()&&(P(t).StartsWith("Root/Comps/ParkWalkComp/")||P(t).StartsWith("Root/City_Park_Forest/"))&&(t.name.StartsWith("SM_bush")||t.name.StartsWith("SM_tree")))){
var b=IslandComprehensiveRepair.BoundsOf(t);float ground=-999;foreach(var h in Physics.RaycastAll(b.center+Vector3.up*15,Vector3.down,80,~0,QueryTriggerInteraction.Ignore)){if(h.collider.transform.IsChildOf(t)||h.normal.y<.6f||h.point.y>b.min.y+.15f)continue;ground=Mathf.Max(ground,h.point.y);}float gap=b.min.y-ground;if(gap>.18f&&gap<.5f){t.position+=Vector3.down*(gap-.015f);report.groundedGreenery++;changes.Add("ground greenery "+P(t));}}
foreach(var path in new[]{"Root/Roads/SM_street_decal_Root/SM_street_decal (76)","Root/Roads/SM_street_decal_Root/SM_street_decal (4)"}){var t=IslandWorldIssues.All(city).FirstOrDefault(t=>P(t)==path);if(t){UnityEngine.Object.DestroyImmediate(t.gameObject);report.removedDecals++;changes.Add("remove decal above deleted bridge approach "+path);}}
report.shopsUnchanged=before.SequenceEqual(stores.Select(signature));if(!report.shopsUnchanged)throw new Exception("Shop preservation check failed");
foreach(var s in scenes.Where(s=>s.name=="GameState"||s.name.StartsWith("1")))EditorSceneManager.SaveScene(s);
var surface=IslandWorldIssues.All(game).Select(t=>t.GetComponent<NavMeshSurface>()).First(s=>s&&P(s.transform)=="---AI/ResidentManager/NavMesh Surface");surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();report.navMesh="Assets/SushiAndFish/Modules/OriginalIsland/TerrainData/TownIsland_Comprehensive_20261017.asset";if(AssetDatabase.LoadAssetAtPath<NavMeshData>(report.navMesh))throw new Exception("Bake already exists");AssetDatabase.CreateAsset(surface.navMeshData,report.navMesh);EditorUtility.SetDirty(surface);EditorSceneManager.SaveScene(game);AssetDatabase.SaveAssets();
report.changes=changes.ToArray();File.WriteAllText(Path.Combine(IslandComprehensiveRepair.Reports,"comprehensive-followup-repair.json"),JsonUtility.ToJson(report,true));IslandFrontEndBuilder.RefreshMenuPresentation();EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");Debug.Log("FOLLOWUP_REPAIR_SAVED");
}}

