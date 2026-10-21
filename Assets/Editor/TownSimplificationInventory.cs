using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PaperPixelGames.Core.Ai.NPCs;

public static partial class IslandRevision20261018
{
    const string TownLibrary="Assets/SushiAndFish/ModularTown";
    static readonly HashSet<string> MainStreetNames=new HashSet<string>{"SM_street (64)","SM_street (50)","SM_street (49)","SM_street (51)","SM_street (52)","SM_street (69)","SM_street (17)","SM_street (40)","SM_street (41)"};
    [Serializable] public class TownRow {public string scene,path,id,category,action,key,prefab,components;public Vector3 position,scale,min,max;public Quaternion rotation;public int transforms,meshes;}
    [Serializable] public class MarineRow {public string scene,path,support;public Vector3 position;public float bottom,currentGround,originalGround,gap,originalGap;public bool sourceCovered;}
    [Serializable] public class UiRow {public string asset,path,components,text,references;public Vector2 size;public bool active;}
    [Serializable] public class SourceTerrain {public string path,data;public Vector3 position,size;}
    [Serializable] public class SourceTerrainReport {public SourceTerrain[] terrains;}
    [Serializable] public class TownPlan {public string utc,stage;public TownRow[] objects,assemblies;public MarineRow[] marine;public UiRow[] ui;public Protection[] protectedNodes;public string[] mainRoads;public int removeRoots,keepFurniture,variants;public string[] unknown;}
    static bool MarketPath(string p)=>p=="Root/Comps/GabeMarket"||p.StartsWith("Root/Comps/GabeMarket/")||p=="Root/Comps/MarinaMarket"||p.StartsWith("Root/Comps/MarinaMarket/");
    static bool ExteriorPropName(string n)=>Regex.IsMatch(n,@"^SM_(utility_pole|pole_street|hydrant|firehydrant|fire_hydrant|traffic|sign_|street|lamp|mirror_round|trash|bikestand|vendingmachine|bicycle|mofa|carton|tree|flowerpot|bush|plants_box|chair)",RegexOptions.IgnoreCase);
    static bool InRoot(Transform t,HashSet<Transform> roots){for(var p=t;p;p=p.parent)if(roots.Contains(p))return true;return false;}
    static float TerrainAt(Terrain[] lands,Vector3 point,out bool covered) {
        var found=lands.Where(t=>point.x>=t.transform.position.x&&point.z>=t.transform.position.z&&point.x<=t.transform.position.x+t.terrainData.size.x&&point.z<=t.transform.position.z+t.terrainData.size.z).ToArray();covered=found.Length>0;
        return covered?found.Max(t=>t.SampleHeight(point)+t.transform.position.y):-100000;
    }
    static float OriginalTerrainAt(SourceTerrain[] sources,Dictionary<string,TerrainData> data,Vector3 p,out bool covered) {
        var found=sources.Where(t=>p.x>=t.position.x&&p.z>=t.position.z&&p.x<=t.position.x+t.size.x&&p.z<=t.position.z+t.size.z).ToArray();covered=found.Length>0;
        return covered?found.Max(t=>data[t.data].GetInterpolatedHeight((p.x-t.position.x)/t.size.x,(p.z-t.position.z)/t.size.z)+t.position.y):-100000;
    }
    static string TownCategory(Transform t) {
        string p=P(t),n=t.name;
        if(p.StartsWith("RemovedBuildingFootprints"))return "OldBuildingFootprintMarkers";
        if(p.StartsWith("---World/Swing"))return "Swings";
        if(p.StartsWith("---Beach/BeachModels")||p.StartsWith("---Beach/BeachBall"))return "BeachProps";
        if(p.StartsWith("Root/Roads/"))return n.StartsWith("SM_street (")?"RoadTiles":n.Contains("decal")?"RoadMarkings":"PavingAndCurbs";
        if(n.IndexOf("trash",StringComparison.OrdinalIgnoreCase)>=0)return "Bins";
        if(Regex.IsMatch(n,"pole|hydrant|traffic|lamp|sign|bikestand",RegexOptions.IgnoreCase))return "StreetFurniture";
        if(Regex.IsMatch(p,@"^Root/Comps/(Comp11|CompOtopark|Comp9 \(17\))"))return "Structures";
        return "TownProps";
    }
    static string UiReferences(MonoBehaviour component) {
        var list=new List<string>();var state=new SerializedObject(component);var it=state.GetIterator();
        while(it.NextVisible(true))if(it.propertyType==SerializedPropertyType.ObjectReference&&it.objectReferenceValue) {
            var value=it.objectReferenceValue;var t=value is Component c?c.transform:value is GameObject g?g.transform:null;
            list.Add(it.propertyPath+"="+(t?P(t):AssetDatabase.GetAssetPath(value)));
        }
        return string.Join("\n",list);
    }
    static UiRow[] UiInspect(GameObject root,string asset) {
        return root.GetComponentsInChildren<Transform>(true).Where(t=>t.GetComponents<MonoBehaviour>().Any(m=>m&&(m is PaperPixelGames.Core.Tablet.MapApp||m is PaperPixelGames.Core.Tablet.TabletController||m is PaperPixelGames.Framework.States.Game.PauseScreen||m is PaperPixelGames.Framework.States.MainMenu.MainMenuManager))||Regex.IsMatch(t.name,"Version|Feedback|Fedback|Report|Runner|Map|Parkour",RegexOptions.IgnoreCase)).Select(t=>new UiRow{asset=asset,path=P(t),active=t.gameObject.activeSelf,components=string.Join(";",t.GetComponents<Component>().Where(c=>c).Select(c=>c.GetType().FullName)),text=t.GetComponent<TMPro.TMP_Text>()?t.GetComponent<TMPro.TMP_Text>().text:"",size=t is RectTransform rect?rect.sizeDelta:Vector2.zero,references=string.Join("\n\n",t.GetComponents<MonoBehaviour>().Where(m=>m&&(m is PaperPixelGames.Core.Tablet.MapApp||m is PaperPixelGames.Core.Tablet.TabletController||m is PaperPixelGames.Framework.States.Game.PauseScreen||m is PaperPixelGames.Framework.States.MainMenu.MainMenuManager)).Select(UiReferences))}).ToArray();
    }
    public static void TownInventory() {
        revisionObjects=null;revisionIds=null;var scenes=LoadAll();var all=scenes.SelectMany(All).ToArray();IndexRevisionObjects(all);
        var original=JsonUtility.FromJson<DecorationPlan>(File.ReadAllText(Path.Combine(Reports,"island-decoration-export.json")));
        var furniture=new HashSet<Transform>(original.decorations.Where(x=>x.action=="PRESERVE_NPC_FURNITURE").Select(x=>Resolve<Transform>(x.globalId)).Where(t=>t));
        var mainRoads=new HashSet<Transform>(all.Where(t=>t.gameObject.scene.name=="100_Dressing_City_01"&&P(t).StartsWith("Root/Roads/SM_street_Root/")&&MainStreetNames.Contains(t.name)));
        var chosen=new Dictionary<Transform,string>();
        foreach(var t in all) {
            bool city=t.gameObject.scene.name.StartsWith("1");
            string p=P(t);bool game=t.gameObject.scene.name=="GameState"&&(p.StartsWith("RemovedBuildingFootprints")||p.StartsWith("---World/Swing/")||p.StartsWith("---Beach/BeachModels/")||p.StartsWith("---Beach/BeachBall/"));
            if(!city&&!game)continue;if(!t.GetComponent<MeshFilter>()&&!t.GetComponent<LODGroup>()&&!t.GetComponent<Renderer>())continue;
            var root=AtomicRoot(t);
            if(InRoot(root,furniture)){chosen[root]="KEEP_NPC_FURNITURE";continue;}
            if(InRoot(root,mainRoads)){chosen[root]="KEEP_MAIN_ROAD";continue;}
            if(MarketPath(P(root))&&!ExteriorPropName(root.name))continue;
            if(root.GetComponentsInChildren<Transform>(true).Any(x=>furniture.Contains(x)||mainRoads.Contains(x)))root=t;
            chosen[root]="REMOVE_AFTER_EXPORT";
        }
        var fullSet=new HashSet<Transform>(chosen.Keys);
        foreach(var t in fullSet)for(var p=t.parent;p;p=p.parent)if(fullSet.Contains(p)&&chosen[p]==chosen[t]){chosen.Remove(t);break;}
        var rows=chosen.Select(x=>{var t=x.Key;var b=IslandComprehensiveRepair.BoundsOf(t);return new TownRow{scene=t.gameObject.scene.path,path=P(t),id=Id(t),category=TownCategory(t),action=x.Value,key=VariantKey(t),position=t.position,rotation=t.rotation,scale=t.lossyScale,min=b.min,max=b.max,transforms=t.GetComponentsInChildren<Transform>(true).Length,meshes=t.GetComponentsInChildren<MeshFilter>(true).Length,components=string.Join(";",t.GetComponentsInChildren<MonoBehaviour>(true).Where(c=>c).Select(c=>c.GetType().FullName).Distinct())};}).OrderBy(x=>x.scene).ThenBy(x=>x.path).ToArray();
        var sources=JsonUtility.FromJson<SourceTerrainReport>(File.ReadAllText(Path.Combine(Reports,"island-trim-heights.json"))).terrains;
        var sourceData=sources.ToDictionary(s=>s.data,s=>AssetDatabase.LoadAssetAtPath<TerrainData>(s.data));var lands=All(scenes[0]).Select(t=>t.GetComponent<Terrain>()).Where(t=>t).ToArray();
        var marine=new List<MarineRow>();var candidates=new HashSet<Transform>(all.Where(t=>Regex.IsMatch(t.gameObject.scene.name,@"^[2-5]\d\d_")&&t.GetComponent<MeshFilter>()).Select(AtomicRoot));
        foreach(var t in candidates) {
            var b=IslandComprehensiveRepair.BoundsOf(t);bool covered;float old=OriginalTerrainAt(sources,sourceData,b.center,out covered);float current=TerrainAt(lands,b.center,out bool now);
            string support="TerrainHeight";foreach(var hit in Physics.RaycastAll(new Vector3(b.center.x,b.min.y+.12f,b.center.z),Vector3.down,1000,~0,QueryTriggerInteraction.Ignore))if(!hit.collider.transform.IsChildOf(t)&&hit.collider.transform!=t&&hit.normal.y>.5f&&hit.point.y>current){current=hit.point.y;support=P(hit.collider.transform);}
            marine.Add(new MarineRow{scene=t.gameObject.scene.name,path=P(t),position=t.position,bottom=b.min.y,currentGround=current,originalGround=old,gap=b.min.y-current,originalGap=b.min.y-old,sourceCovered=covered,support=support});
        }
        var ui=new List<UiRow>();const string playerPath="Assets/GameObject/PlayerNGO.prefab";var prefab=PrefabUtility.LoadPrefabContents(playerPath);ui.AddRange(UiInspect(prefab,playerPath));PrefabUtility.UnloadPrefabContents(prefab);
        foreach(var path in new[]{"Assets/Scenes/MainMenuState.unity","Assets/SushiAndFish/FrontEnd/Scenes/MainMenuState.unity"}){var s=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);foreach(var root in s.GetRootGameObjects())ui.AddRange(UiInspect(root,path));EditorSceneManager.CloseScene(s,true);}
        var report=new TownPlan{utc=DateTime.UtcNow.ToString("o"),stage="INVENTORY_BEFORE_NEW_REMOVAL",objects=rows,marine=marine.ToArray(),ui=ui.ToArray(),mainRoads=mainRoads.Select(P).ToArray(),removeRoots=rows.Count(x=>x.action=="REMOVE_AFTER_EXPORT"),keepFurniture=furniture.Count,variants=rows.Where(x=>x.action=="REMOVE_AFTER_EXPORT").Select(x=>x.key).Distinct().Count(),unknown=Array.Empty<string>()};
        File.WriteAllText(Path.Combine(Reports,"town-simplification-plan.json"),JsonUtility.ToJson(report,true));revisionObjects=null;revisionIds=null;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");Debug.Log("TOWN_SIMPLIFICATION_INVENTORY_READY "+report.removeRoots+" / "+report.variants);
    }
}
