using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class IslandRevision20261018
{
    static bool RelocatedTown=>File.Exists(Path.Combine(Reports,"town-road-house-relocation-build.json"));
    [Serializable] public class HouseMoveAudit {public string path,globalId;public Vector3 before,after,offset;public int descendants,colliders;public bool childLocalSignaturesUnchanged;public string[] scripts;}
    [Serializable] public class ParkRoadSampleFailure {public string road;public Vector3 position;public float grassWeight,ground;}
    [Serializable] public class ParkRoadFootprintAudit {public bool passed;public int samples,greenAndFlatSamples;public float minimumGround,maximumGround,minimumGrassWeight;public ParkRoadSampleFailure[] failures;}
    static ParkRoadFootprintAudit InspectParkRoadFootprints(TownRoadPlan plan,Terrain[] lands) {
        var result=new ParkRoadFootprintAudit{minimumGround=float.MaxValue,maximumGround=float.MinValue,minimumGrassWeight=1};var failures=new List<ParkRoadSampleFailure>();
        var alphas=lands.ToDictionary(t=>t,t=>t.terrainData.GetAlphamaps(0,0,t.terrainData.alphamapWidth,t.terrainData.alphamapHeight));
        void Check(string name,float x,float z) {
            result.samples++;var land=lands.FirstOrDefault(t=>x>=t.transform.position.x&&z>=t.transform.position.z&&x<=t.transform.position.x+t.terrainData.size.x&&z<=t.transform.position.z+t.terrainData.size.z);
            if(!land){failures.Add(new ParkRoadSampleFailure{road=name,position=new Vector3(x,0,z),ground=-999});return;}
            var d=land.terrainData;var a=alphas[land];int layer=Array.FindIndex(d.terrainLayers,l=>l&&l.name=="SWS_Grass");int ax=Mathf.Clamp((int)((x-land.transform.position.x)/d.size.x*d.alphamapWidth),0,d.alphamapWidth-1),az=Mathf.Clamp((int)((z-land.transform.position.z)/d.size.z*d.alphamapHeight),0,d.alphamapHeight-1);float weight=layer>=0?a[az,ax,layer]:0;float ground=land.SampleHeight(new Vector3(x,0,z))+land.transform.position.y;
            result.minimumGround=Mathf.Min(result.minimumGround,ground);result.maximumGround=Mathf.Max(result.maximumGround,ground);result.minimumGrassWeight=Mathf.Min(result.minimumGrassWeight,weight);
            if(weight>=.95f&&ground>=2.8f&&ground<=3.3f)result.greenAndFlatSamples++;else failures.Add(new ParkRoadSampleFailure{road=name,position=new Vector3(x,ground,z),grassWeight=weight,ground=ground});
        }
        foreach(var road in plan.roads.Where(r=>r.role=="park_approach")){var dir=(road.b.Vector-road.a.Vector).normalized;var side=new Vector3(-dir.z,0,dir.x);int n=Mathf.CeilToInt(Vector3.Distance(road.a.Vector,road.b.Vector)/.5f);for(int i=0;i<=n;i++)for(int j=0;j<=28;j++){var p=Vector3.Lerp(road.a.Vector,road.b.Vector,(float)i/n)+side*Mathf.Lerp(-6.6f,6.6f,j/28f);Check(road.id,p.x,p.z);}}
        foreach(var turn in plan.turnarounds)for(float x=turn.x-turn.size/2;x<=turn.x+turn.size/2+.01f;x+=.5f)for(float z=turn.z-turn.size/2;z<=turn.z+turn.size/2+.01f;z+=.5f)Check("Turnaround_"+turn.z,x,z);
        result.failures=failures.ToArray();result.passed=result.samples==result.greenAndFlatSamples;return result;
    }
    static HouseMoveAudit[] MoveTownHouses(TownRoadPlan plan,Transform[] all,PlanningInventory inv) {
        var rows=new List<HouseMoveAudit>();
        foreach(var move in plan.houseMoves){var root=all.Single(t=>P(t)==move.path);var children=root.GetComponentsInChildren<Transform>(true);var before=children.Skip(1).ToDictionary(t=>t,t=>Signature(t));var row=new HouseMoveAudit{path=P(root),globalId=Id(root),before=root.position,offset=move.offset,descendants=children.Length,colliders=root.GetComponentsInChildren<Collider>(true).Length,scripts=root.GetComponentsInChildren<MonoBehaviour>(true).Where(m=>m).Select(m=>m.GetType().FullName).Distinct().ToArray()};root.position+=move.offset;row.after=root.position;row.childLocalSignaturesUnchanged=before.All(p=>Signature(p.Key)==p.Value);if(!row.childLocalSignaturesUnchanged)throw new Exception("Only root translation allowed: "+row.path);var original=inv.protectedNodes.Single(p=>p.globalId==row.globalId);original.signature=Signature(root);rows.Add(row);}
        var shops=plan.houseMoves.Select(m=>all.Single(t=>P(t)==m.path)).ToArray();if(GroundRect(PlanningBounds(shops[0])).Overlaps(GroundRect(PlanningBounds(shops[1]))))throw new Exception("Moved houses overlap each other");
        foreach(var shop in shops)foreach(var plot in plan.plots)if(GroundRect(PlanningBounds(shop)).Overlaps(PlotRect(plot)))throw new Exception("Moved house overlaps reserved parcel "+plot.id);
        return rows.ToArray();
    }
    public static void VerifyRelocatedTownSaved() {
        VerifyRoadTownSaved();revisionObjects=null;revisionIds=null;var scenes=LoadIsland();var all=scenes.SelectMany(All).ToArray();IndexRevisionObjects(all);
        var receipt=JsonUtility.FromJson<RoadCorrectionResult>(File.ReadAllText(Path.Combine(Reports,"town-road-house-relocation-build.json")));var rows=BuildingClearances(all,receipt.plan);var lands=all.Select(t=>t.GetComponent<Terrain>()).Where(t=>t&&t.gameObject.activeInHierarchy).ToArray();var park=InspectParkRoadFootprints(receipt.plan,lands);var road=JsonUtility.FromJson<RoadSavedAudit>(File.ReadAllText(Path.Combine(Reports,"town-road-saved-verification.json")));var failures=new List<string>();
        foreach(var move in receipt.movedHouses){var root=Resolve<Transform>(move.globalId);if(!root||Vector3.Distance(root.position,move.after)>.001f||root.GetComponentsInChildren<Transform>(true).Length!=move.descendants||root.GetComponentsInChildren<Collider>(true).Length!=move.colliders)failures.Add("Moved building did not reopen intact "+move.path);}
        foreach(var row in rows)if(row.overlaps||row.clearance<.5f)failures.Add(row.building+" / "+row.road);
        receipt.stage="REOPENED_FULL_ROADS_RELOCATED_HOUSES_AND_GRASS_BOUNDARY";receipt.buildingClearances=rows;receipt.minimumBuildingClearance=rows.Min(r=>r.clearance);receipt.parkRoadAudit=park;receipt.failures=failures.ToArray();receipt.passed=road.passed&&park.passed&&rows.Length==36&&failures.Count==0;
        File.WriteAllText(Path.Combine(Reports,"town-road-house-relocation-saved-verification.json"),JsonUtility.ToJson(receipt,true));revisionObjects=null;revisionIds=null;EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");Debug.Log("RELOCATED_TOWN_SAVED "+receipt.passed+" park="+park.passed+" greenSamples="+park.greenAndFlatSamples+"/"+park.samples+" buildings="+rows.Length);
    }
}
