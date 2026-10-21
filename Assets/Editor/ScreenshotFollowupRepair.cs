using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static partial class ScreenshotFollowupRepair {
 public const string GS="Assets/SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity";
 public const string DS="Assets/SushiAndFish/Modules/OriginalIsland/Scenes/DressingScenes/100_Dressing_City_01.unity";
 public const string Source="Assets/SushiAndFish/RevisionReference_20261021/OriginalCityBeforeTrim.unity";
 public static string Reports=>IslandFrontEndInventory.Reports;
 public static void ConfigureScreensAndTest(){
  const string path="Assets/SushiAndFish/Casino/Rendering/CasinoOffscreenRenderer.asset";
  Directory.CreateDirectory("Assets/SushiAndFish/Casino/Rendering");AssetDatabase.Refresh();
  var data=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(path);
  if(!data){data=Object.Instantiate(AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>("Assets/SushiAndFish/Casino/Imported/MonoBehaviour/GWYF_RenderTextureRenderer.asset"));data.name="CasinoOffscreenRenderer";AssetDatabase.CreateAsset(data,path);}
  var ds=new SerializedObject(data);foreach(var n in new[]{"m_OpaqueLayerMask","m_TransparentLayerMask","m_PrepassLayerMask"}){var prop=ds.FindProperty(n);if(prop!=null)prop.intValue=-1;}ds.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(data);
  foreach(var n in new[]{"Low","Medium","High","Ultra"}){
   string p="Assets/MonoBehaviour/URP_"+n+".asset";BackupAsset(p);
   var pipeline=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(p);var so=new SerializedObject(pipeline);var array=so.FindProperty("m_RendererDataList");bool exists=false;for(int i=0;i<array.arraySize;i++)exists|=array.GetArrayElementAtIndex(i).objectReferenceValue==data;
   if(!exists){array.InsertArrayElementAtIndex(array.arraySize);array.GetArrayElementAtIndex(array.arraySize-1).objectReferenceValue=data;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);}
  }AssetDatabase.SaveAssets();CityExpansionPlayAudit.ScreensAndCoast();
 }
 static void BackupAsset(string path){var dst=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../backup/screenshot-followup-20261021",path));Directory.CreateDirectory(Path.GetDirectoryName(dst));if(!File.Exists(dst))File.Copy(path,dst);}
 public static string P(Transform t)=>t.parent?P(t.parent)+"/"+t.name:t.name;
 public static Transform[] All(Scene s)=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
 [Serializable]public class Maps {public Map[] terrains;}
 [Serializable]public class Map {public string path,originalData;}
 [Serializable]public class Node {public string path,mesh,material;public Vector3 position,min,max;public bool enabled,active;public string[] components;}
 [Serializable]public class Study {public string utc;public List<Node> current=new List<Node>(),original=new List<Node>();public List<Sample> ground=new List<Sample>();}
 [Serializable]public class Sample {public float x,z,current,original;}
 static Node N(Transform t){var r=t.GetComponent<Renderer>();var f=t.GetComponent<MeshFilter>();return new Node{path=P(t),position=t.position,min=r?r.bounds.min:t.position,max=r?r.bounds.max:t.position,mesh=f&&f.sharedMesh?AssetDatabase.GetAssetPath(f.sharedMesh):"",material=r?string.Join(";",r.sharedMaterials.Select(m=>m?m.name:"null")):"",enabled=!r||r.enabled,active=t.gameObject.activeInHierarchy,components=t.GetComponents<Component>().Where(c=>c).Select(c=>c.GetType().Name).ToArray()};}
 public static void StudyScene(){
  var data=new Study{utc=DateTime.UtcNow.ToString("o")};
  var gs=EditorSceneManager.OpenScene(GS);var ds=EditorSceneManager.OpenScene(DS,OpenSceneMode.Additive);Physics.SyncTransforms();
  var all=All(gs).Concat(All(ds)).ToArray();
  foreach(var t in all.Where(t=>t.GetComponent<MeshRenderer>()||t.GetComponent<Terrain>()||t.GetComponent<Camera>()||t.GetComponent<TMPro.TMP_Text>()||t.name.Contains("EmoteArea"))) {
   if(Regex.IsMatch(P(t),@"Restored|Comp9|Comp11|Tunnel|TownRoadNetwork|AR_Restaurant|RoofRevision|Bench|bench|EmoteArea|Door|Terrain|AR_CinemaAuditorium"))data.current.Add(N(t));
  }
  var maps=JsonUtility.FromJson<Maps>(File.ReadAllText(Path.Combine(Reports,"island-trim-edit-audit.json")));
  foreach(var land in all.Select(t=>t.GetComponent<Terrain>()).Where(t=>t&&t.name!="IslandOuterSeabed")) {
   var map=maps.terrains.FirstOrDefault(m=>m.path==P(land.transform));if(map==null){Debug.Log("STUDY_SKIP_TERRAIN "+P(land.transform));continue;}var original=AssetDatabase.LoadAssetAtPath<TerrainData>(map.originalData);var origin=land.transform.position;var size=land.terrainData.size;
   for(float z=-100;z<=105;z+=1)for(float x=-160;x<=130;x+=1) {
    if(x<origin.x||z<origin.z||x>origin.x+size.x||z>origin.z+size.z)continue;
    data.ground.Add(new Sample{x=x,z=z,current=land.SampleHeight(new Vector3(x,0,z))+origin.y,original=original.GetInterpolatedHeight((x-origin.x)/size.x,(z-origin.z)/size.z)+origin.y});
   }
  }
  var src=EditorSceneManager.OpenScene(Source,OpenSceneMode.Additive);Physics.SyncTransforms();
  data.original=All(src).Where(t=>t.GetComponent<MeshRenderer>()&&Regex.IsMatch(P(t),@"Root/(Comps|Roads|TunnelComp|DockSide)")).Select(N).ToList();
  File.WriteAllText(Path.Combine(Reports,"screenshot-followup-study.json"),JsonUtility.ToJson(data,false));
  EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");Debug.Log("SCREENSHOT_STUDY "+data.current.Count+" / "+data.original.Count);EditorApplication.Exit(0);
 }
}
