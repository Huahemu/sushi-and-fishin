using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.Rendering;using UnityEngine.SceneManagement;using Unity.AI.Navigation;using UnityEngine.AI;using PaperPixelGames.Core.DressingSceneSystem;using SushiAndFish.CityExpansion;using Object=UnityEngine.Object;
public static class TownBuildingRoofIntegration20261021 {
 const string Root="Assets/SushiAndFish/CityExpansion/BuildingRevision20261021";
 const string GS="Assets/SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity";
 const string DS="Assets/SushiAndFish/Modules/OriginalIsland/Scenes/DressingScenes/100_Dressing_City_01.unity";
 static string Reports=>IslandRevision20261018.Reports;
 [Serializable]public class Overlay {public string fbx,target_existing_asset_root;public string[] hide_exact_old_mesh_nodes;}
 [Serializable]public class OverlayList {public Overlay restaurant,cinema;}
 [Serializable]public class Building {public string name;public Vector3 min,max;public int meshes,colliders,hiddenOldMeshes,retainedSeats,retainedDoorLeaves;}
 [Serializable]public class Receipt {public string utc,stage,error,navigation;public bool saved;public List<Building> buildings=new List<Building>();public int materials,counterColliders,seatNavigationPaths;}
 static Receipt report;
 static IEnumerable<Transform> All(Scene s)=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true));
 static void Stage(string s){report.stage=s;File.WriteAllText(Path.Combine(Reports,"town-building-roof-integration.json"),JsonUtility.ToJson(report,true));Debug.Log("ROOF_INTEGRATION "+s);}
 static Dictionary<string,Material> Materials(){
  var result=new Dictionary<string,Material>();Directory.CreateDirectory(Root+"/Materials");var doc=JsonUtility.FromJson<CityExpansionModelImport.MaterialList>(File.ReadAllText(Root+"/SourceMaterials.json"));
  foreach(var x in doc.materials){var path=Root+"/Materials/"+x.name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}var c=new Color(x.color[0],x.color[1],x.color[2],x.color[3]);c=new Color(c.gamma.r,c.gamma.g,c.gamma.b,c.a);m.SetColor("_BaseColor",c);m.SetFloat("_Metallic",x.metallic);m.SetFloat("_Smoothness",1-x.roughness);
   if(!string.IsNullOrEmpty(x.image)){var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Models/"+x.asset+"/Textures/"+x.image+".png");if(!texture)throw new Exception("Missing roof source texture "+x.image);m.SetTexture("_BaseMap",texture);}
   if(x.alpha=="BLEND"||x.name.EndsWith("_glass",StringComparison.OrdinalIgnoreCase)){c.a=x.name.EndsWith("_glass",StringComparison.OrdinalIgnoreCase)?.18f:c.a;m.SetColor("_BaseColor",c);m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_ZWrite",0);m.SetFloat("_SrcBlend",(int)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);m.SetOverrideTag("RenderType","Transparent");m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=(int)RenderQueue.Transparent;}else{m.SetFloat("_Surface",0);m.SetFloat("_ZWrite",1);m.renderQueue=-1;m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");}
   m.SetFloat("_Cull",x.doubleSided?0:2);if(x.emission!=null&&x.emission.Any(v=>v>0)){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(x.emission[0],x.emission[1],x.emission[2]));}EditorUtility.SetDirty(m);result[x.name]=m;
  }report.materials=result.Count;return result;
 }
 static Bounds LocalBounds(Transform root,Transform content){var all=content.GetComponentsInChildren<Renderer>(true);bool initialized=false;var bounds=new Bounds();foreach(var r in all){var b=r.bounds;for(int i=0;i<8;i++){var w=new Vector3((i&1)==0?b.min.x:b.max.x,(i&2)==0?b.min.y:b.max.y,(i&4)==0?b.min.z:b.max.z);var p=root.InverseTransformPoint(w);if(!initialized){bounds=new Bounds(p,Vector3.zero);initialized=true;}else bounds.Encapsulate(p);}}return bounds;}
 static Building Patch(Transform target,string name,string fbx,Overlay spec,Dictionary<string,Material> mats){
  var row=new Building{name=name};var old=target.Find("RoofRevision_20261021");if(old)Object.DestroyImmediate(old.gameObject);
  var content=new GameObject("RoofRevision_20261021");content.transform.SetParent(target,false);var asset=AssetDatabase.LoadAssetAtPath<GameObject>(fbx);if(!asset)throw new Exception("Roof FBX did not import "+fbx);Object.Instantiate(asset,content.transform,false);
  foreach(var animator in content.GetComponentsInChildren<Animator>(true))Object.DestroyImmediate(animator);
  foreach(var r in content.GetComponentsInChildren<MeshRenderer>(true)){r.sharedMaterials=r.sharedMaterials.Select(m=>{string n=m.name.Replace(" (Instance)","");if(!mats.TryGetValue(n,out var replacement))throw new Exception("Unmapped roof material "+n);return replacement;}).ToArray();r.shadowCastingMode=ShadowCastingMode.On;var f=r.GetComponent<MeshFilter>();if(f&&r.bounds.size.sqrMagnitude>1){var collider=r.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=f.sharedMesh;} }
  var modifier=content.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;
  var local=LocalBounds(target,content.transform);row.min=local.min;row.max=local.max;row.meshes=content.GetComponentsInChildren<MeshRenderer>(true).Length;row.colliders=content.GetComponentsInChildren<Collider>(true).Length;
  if(local.size.x<8||local.size.x>13||local.size.z<7||local.size.z>13||local.min.y<2.7f||local.max.y>10||local.max.y<(name=="AR_Restaurant"?4.6f:6.5f))throw new Exception("Incorrect overlay scale or axis: "+name+" "+local);
  var hide=new HashSet<string>(spec.hide_exact_old_mesh_nodes);foreach(var t in target.GetComponentsInChildren<Transform>(true)){if(t.IsChildOf(content.transform))continue;if(!hide.Contains(t.name))continue;var r=t.GetComponent<MeshRenderer>();if(r){r.enabled=false;row.hiddenOldMeshes++;}foreach(var collider in t.GetComponents<Collider>())collider.enabled=false;}
  if(row.hiddenOldMeshes<(name=="AR_Restaurant"?25:40))throw new Exception("Old roof mapping incomplete for "+name+" count="+row.hiddenOldMeshes);
  row.retainedSeats=target.GetComponentsInChildren<CityDiningSeat>(true).Length;row.retainedDoorLeaves=target.GetComponentsInChildren<Transform>(true).Count(t=>t.name.EndsWith("Entrance_L")||t.name.EndsWith("Entrance_R")||t.name.EndsWith("MainEntrance_L")||t.name.EndsWith("MainEntrance_R"));
  foreach(var t in target.GetComponentsInChildren<Transform>(true).Where(t=>t.name.EndsWith("ConcessionCounterBody")||t.name.EndsWith("ConcessionCounterTop"))){var f=t.GetComponent<MeshFilter>();if(!f)continue;var c=t.GetComponent<MeshCollider>();if(!c)c=t.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;c.enabled=true;report.counterColliders++;}
  return row;
 }
 public static void Apply(){report=new Receipt{utc=DateTime.UtcNow.ToString("o")};try{
  AssetDatabase.Refresh();var mats=Materials();var spec=JsonUtility.FromJson<OverlayList>(File.ReadAllText(Root+"/OverlayNodes.json"));
  var names=new[]{("AR_Restaurant","Restaurant_RoofRevision",spec.restaurant),("AR_CinemaLobby","Cinema_UpperStoreyRoofRevision",spec.cinema)};
  foreach(var n in names){string path=Root+"/Models/"+n.Item1+"/"+n.Item2+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=true;importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.addCollider=false;importer.importNormals=ModelImporterNormals.Import;importer.SaveAndReimport();}
  Stage("ASSETS_IMPORTED");var scene=EditorSceneManager.OpenScene(GS);var city=scene.GetRootGameObjects().Single(g=>g.name=="CityBuildings_20261021").transform;
  foreach(var n in names){var target=city.Find(n.Item1);if(!target)throw new Exception("Missing existing building "+n.Item1);report.buildings.Add(Patch(target,n.Item1,Root+"/Models/"+n.Item1+"/"+n.Item2+".fbx",n.Item3,mats));}
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Stage("SCENE_ROOFS_SAVED");
  foreach(var n in names){string path="Assets/SushiAndFish/CityExpansion/Prefabs/"+n.Item1+".prefab";var prefab=PrefabUtility.LoadPrefabContents(path);try{Patch(prefab.transform,n.Item1,Root+"/Models/"+n.Item1+"/"+n.Item2+".fbx",n.Item3,mats);PrefabUtility.SaveAsPrefabAsset(prefab,path);}finally{PrefabUtility.UnloadPrefabContents(prefab);}}
  var ds=EditorSceneManager.OpenScene(DS,OpenSceneMode.Additive);var connection=All(ds).FirstOrDefault(t=>t.name=="EastStreetToOriginalCoastWalkway");if(connection){var cap=All(ds).FirstOrDefault(t=>t.GetComponent<MeshRenderer>()&&t.name.StartsWith("SM_sidewalk")&&t.GetComponent<MeshRenderer>().sharedMaterials.Any(m=>m&&m.name.StartsWith("M_tiles_ground")));if(cap)connection.GetComponent<MeshRenderer>().sharedMaterial=cap.GetComponent<MeshRenderer>().sharedMaterials.First(m=>m&&m.name.StartsWith("M_tiles_ground"));EditorSceneManager.MarkSceneDirty(ds);EditorSceneManager.SaveScene(ds);}
  var terrains=All(scene).Where(t=>t.GetComponent<Terrain>()).ToArray();Stage("PREFABS_SAVED");
  var all=All(scene).Concat(All(ds)).ToArray();var activation=all.ToDictionary(t=>t.gameObject,t=>t.gameObject.activeSelf);foreach(var group in all.Select(t=>t.GetComponent<DressingSceneRoot>()).Where(g=>g))group.ActivateAllImmediate();
  var surface=all.Select(t=>t.GetComponent<NavMeshSurface>()).First(s=>s);surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();string nav=AssetDatabase.GenerateUniqueAssetPath(Root+"/TownCoastNavigation.asset");AssetDatabase.CreateAsset(surface.navMeshData,nav);report.navigation=nav;EditorUtility.SetDirty(surface);
  var filter=new NavMeshQueryFilter{agentTypeID=surface.agentTypeID,areaMask=NavMesh.AllAreas};foreach(var seat in all.Select(t=>t.GetComponent<CityDiningSeat>()).Where(s=>s)){var path=new NavMeshPath();if(NavMesh.CalculatePath(new Vector3(66,3.1f,-13),seat.approach.position,filter,path)&&path.status==NavMeshPathStatus.PathComplete)report.seatNavigationPaths++;}
  if(report.seatNavigationPaths!=12)throw new Exception("Seat navigation regression after coast revision: "+report.seatNavigationPaths);
  foreach(var state in activation)if(state.Key&&state.Key.activeSelf!=state.Value)state.Key.SetActive(state.Value);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorSceneManager.SaveScene(ds);AssetDatabase.SaveAssets();report.saved=true;Stage("SAVED");
  IslandFrontEndBuilder.RefreshMenuPresentation();EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");Stage("MENU_PRESENTATION_REFRESHED");
 }catch(Exception e){report.error=e.ToString();Stage("FAILED");Debug.LogException(e);throw;}}
}
