using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class IslandFinalInspection {
[Serializable] public class Row {public string scene,path,mesh,material;public Vector3 position,min,max;public float ground,gap;}
[Serializable] public class Result {public string utc;public Row[] floating,weather;public string[] duplicateMeshes,groups,materialIssues,navigation;}
static string P(Transform t)=>IslandFrontEndInventory.PathOf(t);
public static void Run(){
var scenes=IslandComprehensiveRepair.Load();var all=scenes.SelectMany(IslandWorldIssues.All).ToArray();var lands=all.Select(t=>t.GetComponent<Terrain>()).Where(t=>t).ToArray();var city=scenes.First(s=>s.name=="100_Dressing_City_01");var rows=new List<Row>();var weather=new List<Row>();
foreach(var t in all){var f=t.GetComponent<MeshFilter>();if(!f||!f.sharedMesh)continue;var path=P(t);var b=IslandComprehensiveRepair.BoundsOf(t);var r=t.GetComponent<Renderer>();var row=new Row{scene=t.gameObject.scene.name,path=path,position=t.position,min=b.min,max=b.max,mesh=f.sharedMesh.name,material=r?string.Join(";",r.sharedMaterials.Where(m=>m).Select(m=>m.name+"="+m.shader.name+" supported="+m.shader.isSupported)):""};
if(path.StartsWith("---PostProcess")){weather.Add(row);continue;}
if(!t.gameObject.activeInHierarchy||!(t.gameObject.scene==city||path.StartsWith("---Beach"))||t.GetComponentInParent<Canvas>()||!System.Text.RegularExpressions.Regex.IsMatch(t.name,@"^SM_(bench|bicycle|mofa|beach|sand|chair|table|tree|bush|wall|stairs|street|sidewalk|flower|lamp|fence)",System.Text.RegularExpressions.RegexOptions.IgnoreCase))continue;
float ground=-999;foreach(var land in lands){var a=land.transform.position;var s=land.terrainData.size;if(b.center.x>=a.x&&b.center.x<=a.x+s.x&&b.center.z>=a.z&&b.center.z<=a.z+s.z)ground=Mathf.Max(ground,land.SampleHeight(b.center)+a.y);}
foreach(var hit in Physics.RaycastAll(new Vector3(b.center.x,b.min.y+.12f,b.center.z),Vector3.down,1000,~0,QueryTriggerInteraction.Ignore)){if(hit.collider.transform.IsChildOf(t)||hit.normal.y<.5f)continue;ground=Mathf.Max(ground,hit.point.y);}row.ground=ground;row.gap=b.min.y-ground;if(row.gap>.18f&&row.gap<15)rows.Add(row);
}
var issues=all.Select(t=>t.GetComponent<Renderer>()).Where(r=>r&&r.enabled&&r.gameObject.activeInHierarchy&&(!(r is ParticleSystemRenderer particle)||particle.renderMode!=ParticleSystemRenderMode.None)&&r.sharedMaterials.Any(m=>!m||!m.shader||!m.shader.isSupported)).Select(r=>P(r.transform)+" type="+r.GetType().Name+" mats="+string.Join(";",r.sharedMaterials.Select(m=>m?m.name+"/"+(m.shader?m.shader.name:"null"):"NULL"))).ToArray();
var nav=new List<string>();foreach(var point in new[]{new Vector3(-134,3,-76),new Vector3(94,3,-76),new Vector3(26,0,77),new Vector3(-5,3,-10),new Vector3(183,3,-89),new Vector3(6,3,-136)}){bool found=UnityEngine.AI.NavMesh.SamplePosition(point,out var hit,5,UnityEngine.AI.NavMesh.AllAreas);nav.Add(point+" navigable="+found+" hit="+hit.position);}
var meshes=IslandWorldIssues.All(city).Where(t=>t.GetComponent<MeshFilter>()&&t.GetComponent<MeshFilter>().sharedMesh).ToArray();
var duplicates=meshes.GroupBy(t=>P(t)+"|"+t.position.ToString("F4")+"|"+t.rotation.ToString("F4")+"|"+t.GetComponent<MeshFilter>().sharedMesh.GetInstanceID()).Where(g=>g.Count()>1).Select(g=>g.Key+" count="+g.Count()).ToArray();
var groups=IslandWorldIssues.All(city).Where(t=>t.parent&&P(t.parent).StartsWith("Root/Comps/Comp")&&P(t).Count(c=>c=='/')==3).Select(t=>P(t)).ToArray();
File.WriteAllText(Path.Combine(IslandComprehensiveRepair.Reports,"island-final-inspection.json"),JsonUtility.ToJson(new Result{utc=DateTime.UtcNow.ToString("o"),floating=rows.ToArray(),weather=weather.ToArray(),materialIssues=issues,navigation=nav.ToArray(),duplicateMeshes=duplicates,groups=groups},true));EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");Debug.Log("FINAL_INSPECTION_READY");
}}
