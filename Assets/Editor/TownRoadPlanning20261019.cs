using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class IslandRevision20261018
{
    const string RoadLibrary="Assets/SushiAndFish/TownRoadPlanning";
    [Serializable] public class RoadPoint {public float x,z;public Vector3 Vector=>new Vector3(x,0,z);}
    [Serializable] public class RoadLine {public string id,role;public RoadPoint a,b;}
    [Serializable] public class RoadPlot {public string id;public float x,z,width,depth,buildingWidth,buildingDepth;public string[] sourceReferenceIds,sourceReferences;}
    [Serializable] public class ProtectedPark {public float xMin,xMax,zMin,zMax;public Rect Rect=>Rect.MinMaxRect(xMin,zMin,xMax,zMax);}
    [Serializable] public class HouseMove {public string path;public Vector3 offset;}
    [Serializable] public class RoadTurn {public float x,z,size;}
    [Serializable] public class TownRoadPlan {public string ownerChat,humanMessage,utc,stage;public ProtectedPark park;public float asphaltWidth,curbWidth,sidewalkWidth,roadTopY;public RoadLine[] roads;public RoadPlot[] plots;public HouseMove[] houseMoves;public RoadPoint[] signalJunctions,binJunctions;public RoadTurn[] turnarounds;public int binsPerSignalJunction,powerPoles,sourceHouseRecords,excludedParkGazebos;}
    [Serializable] public class PlanningTerrain {public string path,data,heightHash,holeHash;public Vector3 position,size;public string[] layers;public float[] parkLayerWeights;}
    [Serializable] public class PlanningRoad {public string id;public float minGround,maxGround,maxRequiredFill,maxGroundAboveNominal;}
    [Serializable] public class PlanningPlot {public string id;public Vector3 centre;public float minGround,maxGround;public string[] overlappingProtectedObjects;}
    [Serializable] public class PlanningProp {public string prefab,shader,materials;public Vector3 min,max;}
    [Serializable] public class PlanningInventory {public string utc,stage;public TownRoadPlan plan;public PlanningTerrain[] terrains;public PlanningRoad[] roads;public PlanningPlot[] plots;public TownRow[] shops;public Protection[] protectedNodes;public PlanningProp[] prefabs;public string[] failures;}
    static TownRoadPlan RoadPlan()=>JsonUtility.FromJson<TownRoadPlan>(File.ReadAllText(Path.Combine(Reports,"town-road-layout-plan.json")));
    static string BytesHash(byte[] bytes){using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(bytes).Select(b=>b.ToString("x2")));}
    static string DataHeightHash(TerrainData data){var h=data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution);var b=new byte[h.Length*4];Buffer.BlockCopy(h,0,b,0,b.Length);return BytesHash(b);}
    static string DataHoleHash(TerrainData data){var h=data.GetHoles(0,0,data.holesResolution,data.holesResolution);return BytesHash(h.Cast<bool>().Select(v=>(byte)(v?1:0)).ToArray());}
    static float PlanningGround(Terrain[] lands,float x,float z)=>TerrainAt(lands,new Vector3(x,0,z),out _);
    static Bounds PlanningBounds(Transform root){var filters=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh).ToArray();if(filters.Length==0)return new Bounds(root.position,Vector3.zero);var b=IslandComprehensiveRepair.BoundsOf(filters[0].transform);foreach(var f in filters.Skip(1))b.Encapsulate(IslandComprehensiveRepair.BoundsOf(f.transform));return b;}
    static Rect GroundRect(Bounds b)=>Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z);
    static Rect PlotRect(RoadPlot p)=>new Rect(p.x-p.width/2,p.z-p.depth/2,p.width,p.depth);
    static Rect LineRect(RoadLine r,float half)=>Rect.MinMaxRect(Mathf.Min(r.a.x,r.b.x)-half,Mathf.Min(r.a.z,r.b.z)-half,Mathf.Max(r.a.x,r.b.x)+half,Mathf.Max(r.a.z,r.b.z)+half);
    public static void InventoryTownRoads() {
        var plan=RoadPlan();if(plan.ownerChat!="01a13e18-e061-7930-8a81-af528c1d37d5")throw new Exception("Plan owner mismatch");
        var scenes=LoadIsland();var all=scenes.SelectMany(All).ToArray();IndexRevisionObjects(all);var lands=all.Where(t=>t.gameObject.activeInHierarchy).Select(t=>t.GetComponent<Terrain>()).Where(t=>t).ToArray();
        var old=JsonUtility.FromJson<TownPlan>(File.ReadAllText(Path.Combine(Reports,"town-module-export.json")));var mainIds=new HashSet<string>(old.objects.Where(x=>x.action=="KEEP_MAIN_ROAD").Select(x=>x.id));
        var protectedNodes=old.protectedNodes.Where(p=>!mainIds.Contains(p.globalId)).ToArray();var failures=new List<string>();
        foreach(var p in protectedNodes){var t=Resolve<Transform>(p.globalId);if(!t||Signature(t)!=p.signature)failures.Add("Protected baseline changed: "+p.path);}
        var furniture=old.objects.Where(x=>x.action=="KEEP_NPC_FURNITURE").Select(x=>Resolve<Transform>(x.id)).Where(t=>t).ToArray();
        var shops=all.Where(t=>P(t)=="Root/Comps/GabeMarket"||P(t)=="Root/Comps/MarinaMarket").Select(t=>TownDescribe(t,"FunctionalStore","PRESERVE")).ToArray();
        foreach(var shop in shops){var t=all.First(x=>P(x)==shop.path);var b=PlanningBounds(t);shop.min=b.min;shop.max=b.max;}
        var terra=new List<PlanningTerrain>();foreach(var land in lands){var data=land.terrainData;float nx=Mathf.Clamp01((-110-land.transform.position.x)/data.size.x),nz=Mathf.Clamp01((-30-land.transform.position.z)/data.size.z);int ax=Mathf.Clamp((int)(nx*data.alphamapWidth),0,data.alphamapWidth-1),az=Mathf.Clamp((int)(nz*data.alphamapHeight),0,data.alphamapHeight-1);var weights=data.GetAlphamaps(ax,az,1,1);terra.Add(new PlanningTerrain{path=P(land.transform),data=AssetDatabase.GetAssetPath(data),heightHash=DataHeightHash(data),holeHash=DataHoleHash(data),position=land.transform.position,size=data.size,layers=data.terrainLayers.Select(x=>x?x.name:"null").ToArray(),parkLayerWeights=Enumerable.Range(0,data.alphamapLayers).Select(i=>weights[0,0,i]).ToArray()});}
        var roadRows=new List<PlanningRoad>();float half=plan.asphaltWidth/2+plan.curbWidth+plan.sidewalkWidth;
        foreach(var road in plan.roads){if(LineRect(road,half).Overlaps(plan.park.Rect))failures.Add("Road in protected park: "+road.id);var values=new List<float>();var direction=(road.b.Vector-road.a.Vector).normalized;var side=new Vector3(-direction.z,0,direction.x);float length=Vector3.Distance(road.a.Vector,road.b.Vector);for(float d=0;d<=length;d+=2)foreach(float offset in new[]{-half,0,half}){var p=road.a.Vector+direction*d+side*offset;values.Add(PlanningGround(lands,p.x,p.z));}roadRows.Add(new PlanningRoad{id=road.id,minGround=values.Min(),maxGround=values.Max(),maxRequiredFill=plan.roadTopY-values.Min(),maxGroundAboveNominal=values.Max()-plan.roadTopY});}
        var plotRows=new List<PlanningPlot>();foreach(var plot in plan.plots){var rect=PlotRect(plot);var overlaps=new List<string>();if(rect.Overlaps(plan.park.Rect))overlaps.Add("ProtectedPark");foreach(var shop in shops)if(rect.Overlaps(Rect.MinMaxRect(shop.min.x,shop.min.z,shop.max.x,shop.max.z)))overlaps.Add(shop.path);foreach(var f in furniture)if(rect.Overlaps(GroundRect(PlanningBounds(f))))overlaps.Add(P(f));var h=new List<float>();for(float x=rect.xMin;x<=rect.xMax;x+=2)for(float z=rect.yMin;z<=rect.yMax;z+=2)h.Add(PlanningGround(lands,x,z));plotRows.Add(new PlanningPlot{id=plot.id,centre=new Vector3(plot.x,PlanningGround(lands,plot.x,plot.z),plot.z),minGround=h.Min(),maxGround=h.Max(),overlappingProtectedObjects=overlaps.ToArray()});if(overlaps.Count>0)failures.Add("Plot "+plot.id+" overlaps "+string.Join(";",overlaps));}
        var props=new List<PlanningProp>();foreach(var path in old.objects.Where(x=>x.category=="Bins"||x.category=="StreetFurniture"||x.category=="PavingAndCurbs"||x.category=="RoadMarkings").Select(x=>x.prefab).Distinct()){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!prefab)continue;var b=PlanningBounds(prefab.transform);props.Add(new PlanningProp{prefab=path,min=b.min,max=b.max,shader=string.Join(";",prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m).Select(m=>m.shader.name).Distinct()),materials=string.Join(";",prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m).Select(AssetDatabase.GetAssetPath).Distinct())});}
        var result=new PlanningInventory{utc=DateTime.UtcNow.ToString("o"),stage=failures.Count==0?"GEOMETRY_REFERENCE_VALIDATED":"REVIEW_REQUIRED",plan=plan,terrains=terra.ToArray(),roads=roadRows.ToArray(),plots=plotRows.ToArray(),shops=shops,protectedNodes=protectedNodes,prefabs=props.ToArray(),failures=failures.ToArray()};
        File.WriteAllText(Path.Combine(Reports,"town-road-planning-inventory.json"),JsonUtility.ToJson(result,true));revisionObjects=null;revisionIds=null;EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");Debug.Log("TOWN_ROAD_PLANNING_INVENTORY "+result.stage);
    }
}
