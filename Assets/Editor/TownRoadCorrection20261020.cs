using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class IslandRevision20261018
{
    [Serializable] public class BuildingRoadClearance {public string building,road;public Vector3 buildingMin,buildingMax;public float clearance;public bool overlaps;}
    [Serializable] public class RoadCorrectionResult {public string ownerChat,humanMessage,utc,stage,assets;public bool passed,protectedUnchanged,terrainUnchanged;public int removedSignals,remainingSignals,roads,parcels,bins,poles,crosswalks;public float minimumBuildingClearance;public string[] failures;public BuildingRoadClearance[] buildingClearances;public HouseMoveAudit[] movedHouses;public ParkRoadFootprintAudit parkRoadAudit;public TownRoadPlan plan;}
    static bool correctionBenchBay;
    static float CorrectionPavementY(float x,float z) {
        float normal=DeckY(x,z)+.18f;
        if(!correctionBenchBay)return normal;
        // Retain the authored bench and NPC poses. A shallow paved recess meets the feet.
        float distance=Mathf.Max(Mathf.Max(1.8f-x,x-6.8f),Mathf.Max(11.3f-z,z-12.65f));
        return Mathf.Lerp(2.998f,normal,Mathf.Clamp01(distance/1.2f));
    }
    static float RectClearance(Rect a,Rect b) {
        float dx=Mathf.Max(0,Mathf.Max(a.xMin-b.xMax,b.xMin-a.xMax));
        float dz=Mathf.Max(0,Mathf.Max(a.yMin-b.yMax,b.yMin-a.yMax));
        return Mathf.Sqrt(dx*dx+dz*dz);
    }
    static BuildingRoadClearance[] BuildingClearances(Transform[] all,TownRoadPlan plan) {
        var rows=new List<BuildingRoadClearance>();
        foreach(var building in all.Where(t=>P(t)=="Root/Comps/GabeMarket"||P(t)=="Root/Comps/MarinaMarket"||P(t)=="---Restaurant/Restaurant/RestaurantStatic")) {
            var b=PlanningBounds(building);var rect=GroundRect(b);
            foreach(var road in plan.roads){var rr=LineRect(road,6.6f);rows.Add(new BuildingRoadClearance{building=P(building),road=road.id,buildingMin=b.min,buildingMax=b.max,clearance=RectClearance(rect,rr),overlaps=rect.Overlaps(rr)});}
            foreach(var turn in plan.turnarounds){var rr=new Rect(turn.x-turn.size/2,turn.z-turn.size/2,turn.size,turn.size);rows.Add(new BuildingRoadClearance{building=P(building),road="Turnaround_"+turn.x+"_"+turn.z,buildingMin=b.min,buildingMax=b.max,clearance=RectClearance(rect,rr),overlaps=rect.Overlaps(rr)});}
        }
        return rows.ToArray();
    }
    static void CorrectionTurnPad(Transform parent,RoadTurn turn,TownRoadPlan plan,Material asphalt,Material sidewalk,Material foundation) {
        var root=RoadGroup(parent,"Turnaround_"+turn.x+"_"+turn.z,new Vector3(turn.x,DeckY(turn.x,turn.z),turn.z));
        var surface=new RoadGeometry();surface.FlatRect(new Rect(turn.x-5.6f,turn.z-5.6f,11.2f,11.2f),DeckY);RoadMesh(root,root.name+"_Carriageway",surface,asphalt,true);
        float h=turn.size/2;var cuts=new[]{-h,-5.6f,-4.2f,4.2f,5.6f,h};var pave=new RoadGeometry();
        bool Mouth(float x,float z)=>plan.roads.Any(r=>LineRect(r,4.2f).Contains(new Vector2(x,z)));
        for(int i=0;i<cuts.Length-1;i++)for(int j=0;j<cuts.Length-1;j++) {
            float x=(cuts[i]+cuts[i+1])/2,z=(cuts[j]+cuts[j+1])/2;
            if(Mathf.Abs(x)<5.6f&&Mathf.Abs(z)<5.6f||Mouth(turn.x+x,turn.z+z))continue;
            pave.FlatRect(new Rect(turn.x+cuts[i],turn.z+cuts[j],cuts[i+1]-cuts[i],cuts[j+1]-cuts[j]),CorrectionPavementY);
        }
        RoadMesh(root,root.name+"_Walkway",pave,sidewalk,true);
        var walls=new RoadGeometry();var corners=new[]{new Vector3(turn.x-h,0,turn.z-h),new Vector3(turn.x-h,0,turn.z+h),new Vector3(turn.x+h,0,turn.z+h),new Vector3(turn.x+h,0,turn.z-h)};
        for(int edge=0;edge<4;edge++){var a=corners[edge];var b=corners[(edge+1)%4];for(int i=0;i<16;i++){var x=Vector3.Lerp(a,b,i/16f);var y=Vector3.Lerp(a,b,(i+1)/16f);var mid=(x+y)/2;if(Mouth(mid.x,mid.z))continue;x.y=DeckY(x.x,x.z)+.18f;y.y=DeckY(y.x,y.z)+.18f;walls.Wall(x,y,PlanningGround(roadLands,x.x,x.z)-.06f,PlanningGround(roadLands,y.x,y.z)-.06f);}}
        RoadMesh(root,root.name+"_Foundation",walls,foundation,true);
    }
    static void CorrectionBenchBay(Transform parent,Material sidewalk,Material foundation) {
        var root=RoadGroup(parent,"SeafrontBenchBay",new Vector3(4.3f,3,13.7f));var g=new RoadGeometry();
        for(float x=.6f;x<8;x+=.4f)for(float z=13.3f;z<14.6f;z+=.25f)g.FlatRect(new Rect(x,z,.4f,.25f),CorrectionPavementY);
        RoadMesh(root,"SeafrontBenchBay_Pavement",g,sidewalk,true);
    }
    public static void CorrectRoadTown()=>BuildRoadTownCorrection(false);
    public static void RelocateHousesAndRepairParkRoads()=>BuildRoadTownCorrection(true);
    static void BuildRoadTownCorrection(bool relocate) {
        var receiptName=relocate?"town-road-house-relocation-build.json":"town-road-correction-build.json";
        if(File.Exists(Path.Combine(Reports,receiptName)))throw new Exception("Correction exists; inspect receipt before replay");
        if(!File.Exists(Path.Combine(Reports,relocate?"town-road-house-relocation-backup.json":"town-road-correction-backup.json")))throw new Exception("Correction backup missing");
        var plan=JsonUtility.FromJson<TownRoadPlan>(File.ReadAllText(Path.Combine(Reports,relocate?"town-road-house-relocation-plan.json":"town-road-correction-plan.json")));
        if(plan.ownerChat!="01a13e18-e061-7930-8a81-af528c1d37d5"||plan.signalJunctions.Length!=0)throw new Exception("Correction scope mismatch");
        var scenes=LoadIsland();var all=scenes.SelectMany(All).ToArray();IndexRevisionObjects(all);
        var inv=JsonUtility.FromJson<PlanningInventory>(File.ReadAllText(Path.Combine(Reports,"town-road-planning-inventory.json")));
        if(inv.protectedNodes.Any(p=>{var t=Resolve<Transform>(p.globalId);return !t||Signature(t)!=p.signature;}))throw new Exception("Protected baseline drift");
        var supportLands=all.Select(t=>t.GetComponent<Terrain>()).Where(t=>t&&t.gameObject.activeInHierarchy).ToArray();
        var parkAudit=relocate?InspectParkRoadFootprints(plan,supportLands):null;
        if(relocate){File.WriteAllText(Path.Combine(Reports,"town-road-house-relocation-park-preflight.json"),JsonUtility.ToJson(parkAudit,true));if(!parkAudit.passed)throw new Exception("Park road must stay on flat green terrain; inspect preflight");}
        var moves=relocate?MoveTownHouses(plan,all,inv):new HouseMoveAudit[0];
        var rows=BuildingClearances(all,plan);if(rows.Any(x=>x.overlaps||x.clearance<.5f))throw new Exception("Full building/road clearance failed: "+JsonUtility.ToJson(rows.First(x=>x.overlaps||x.clearance<.5f)));
        foreach(var p in plan.plots){var rect=PlotRect(p);if(plan.roads.Any(r=>rect.Overlaps(LineRect(r,6.6f)))||plan.turnarounds.Any(t=>rect.Overlaps(new Rect(t.x-t.size/2,t.z-t.size/2,t.size,t.size))))throw new Exception("Parcel infringed: "+p.id);}
        if(!relocate&&plan.roads.Any(r=>plan.park.Rect.Overlaps(LineRect(r,6.6f))))throw new Exception("Park infringed");
        var old=all.Single(t=>t.name=="TownRoadNetwork20261019");int removed=old.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("TrafficSignal_"));
        var lands=all.Select(t=>t.GetComponent<Terrain>()).Where(t=>t&&t.gameObject.activeInHierarchy).ToArray();var terrainHashes=lands.ToDictionary(t=>P(t.transform),t=>DataHeightHash(t.terrainData)+DataHoleHash(t.terrainData)+BytesHash(FloatBytes(t.terrainData.GetAlphamaps(0,0,t.terrainData.alphamapWidth,t.terrainData.alphamapHeight))));
        roadOutput=RoadLibrary+"/Generated/"+(relocate?"HouseMove_":"Correction_")+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");foreach(var folder in new[]{"Meshes","Materials","Prefabs"})Directory.CreateDirectory(roadOutput+"/"+folder);AssetDatabase.Refresh();roadMeshCount=0;builtCrosswalks=builtSignals=builtBins=builtPoles=0;PrepareDeck(plan,lands);correctionBenchBay=!relocate;
        var asphalt=AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/M_asphalt_01 1.mat");var sidewalk=AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/M_tiles_ground.mat");var curb=AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/M_curbstone_2.mat");var yellow=AssetDatabase.LoadAssetAtPath<Material>("Assets/SushiAndFish/Modules/OriginalIsland/FootprintMarkers/SoftYellowDashes.mat");var cable=AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/M_cables_dark.mat");var white=RoadColor("RoadMarkingWhite",new Color(.96f,.95f,.89f));var foundation=RoadColor("RoadbedConcrete",new Color(.42f,.44f,.42f));foundation.SetFloat("_Cull",0);
        var go=new GameObject("TownRoadNetwork20261019_Correction");SceneManager.MoveGameObjectToScene(go,scenes[0]);var root=go.transform;
        var roads=RoadGroup(root,"RoadSections",Vector3.zero);var junctions=RoadGroup(root,"Junctions",Vector3.zero);var props=RoadGroup(root,"StreetFurniture",Vector3.zero);var parcels=RoadGroup(root,"YellowParcelReservations",Vector3.zero);var nodes=RoadNodes(plan);
        foreach(var r in plan.roads)BuildRoadLine(roads,r,plan,asphalt,sidewalk,curb,white,foundation,nodes);
        foreach(var n in nodes){var arms=RoadArms(n,plan);if(arms.Length<2||arms.Length==2&&Vector2.Dot(arms[0],arms[1])<-.9f)continue;RoadCorners(junctions,n,plan,sidewalk,curb);RoadCrosswalks(junctions,n,plan,white);}
        foreach(var t in plan.turnarounds)CorrectionTurnPad(junctions,t,plan,asphalt,sidewalk,foundation);if(!relocate)CorrectionBenchBay(junctions,sidewalk,foundation);
        foreach(var point in plan.binJunctions){var group=RoadGroup(props,"SidewalkJunction_"+point.x+"_"+point.z,point.Vector);var offsets=point.z>0?new[]{new Vector2(5.4f,-11.3f),new Vector2(-11.3f,5.4f)}:new[]{new Vector2(5.4f,11.3f),new Vector2(-11.3f,-5.4f)};foreach(var offset in offsets){float x=point.x+offset.x,z=point.z+offset.y;PlaceTownPrefab("Assets/SushiAndFish/ModularTown/Bins/SM_trash_can_01_14d73a6b95e7.prefab",group,"SidewalkBin_"+(++builtBins),new Vector3(x,0,z),offset.x>0?90:-90,DeckY(x,z)+.18f);}}
        PowerPoles(props,cable);foreach(var p in plan.plots)ParcelOutline(parcels,p,yellow);FlatRoadPrototype(asphalt,sidewalk,curb,white);
        foreach(Transform section in roads)PrefabUtility.SaveAsPrefabAsset(section.gameObject,roadOutput+"/Prefabs/"+section.name+".prefab");PrefabUtility.SaveAsPrefabAsset(junctions.gameObject,roadOutput+"/Prefabs/Junctions_NoSignals.prefab");PrefabUtility.SaveAsPrefabAsset(props.gameObject,roadOutput+"/Prefabs/StreetFurniture_NoSignals.prefab");
        UnityEngine.Object.DestroyImmediate(old.gameObject);go.name="TownRoadNetwork20261019";
        var failures=inv.protectedNodes.Where(p=>{var t=Resolve<Transform>(p.globalId);return !t||Signature(t)!=p.signature;}).Select(p=>p.path).ToArray();
        bool unchanged=lands.All(t=>terrainHashes[P(t.transform)]==DataHeightHash(t.terrainData)+DataHoleHash(t.terrainData)+BytesHash(FloatBytes(t.terrainData.GetAlphamaps(0,0,t.terrainData.alphamapWidth,t.terrainData.alphamapHeight))));
        if(failures.Length>0||!unchanged)throw new Exception("Preservation failed");
        if(relocate)File.WriteAllText(Path.Combine(Reports,"town-road-relocated-inventory.json"),JsonUtility.ToJson(inv,true));
        PlayerSettings.productName="Deep Blue Sushi";foreach(var s in scenes)EditorSceneManager.SaveScene(s);AssetDatabase.SaveAssets();
        var result=new RoadCorrectionResult{ownerChat=plan.ownerChat,humanMessage=plan.humanMessage,utc=DateTime.UtcNow.ToString("o"),stage="SAVED_NEEDS_REOPEN_AND_RUNTIME",assets=roadOutput,protectedUnchanged=true,terrainUnchanged=true,removedSignals=removed,remainingSignals=0,roads=10,parcels=13,bins=builtBins,poles=builtPoles,crosswalks=builtCrosswalks,minimumBuildingClearance=rows.Min(x=>x.clearance),failures=failures,buildingClearances=rows,movedHouses=moves,parkRoadAudit=parkAudit,plan=plan};
        File.WriteAllText(Path.Combine(Reports,receiptName),JsonUtility.ToJson(result,true));
        var build=JsonUtility.FromJson<TownRoadBuildResult>(File.ReadAllText(Path.Combine(Reports,"town-road-build.json")));build.assets=roadOutput;build.plan=plan;build.utc=result.utc;build.signalPosts=0;build.signalJunctions=0;build.crosswalks=builtCrosswalks;build.centrelineMetres=plan.roads.Sum(r=>Vector3.Distance(r.a.Vector,r.b.Vector));build.prefabs=Directory.GetFiles(roadOutput+"/Prefabs","*.prefab").Length;build.stage=result.stage;File.WriteAllText(Path.Combine(Reports,"town-road-build.json"),JsonUtility.ToJson(build,true));
        File.WriteAllText(Path.Combine(Reports,"town-road-layout-plan.json"),JsonUtility.ToJson(plan,true));
        var obsolete="Assets/SushiAndFish/TownRoadPlanning/Generated/20261019_143813/Prefabs/SignalJunction_Reference.prefab";if(AssetDatabase.LoadAssetAtPath<GameObject>(obsolete))AssetDatabase.DeleteAsset(obsolete);
        correctionBenchBay=false;revisionObjects=null;revisionIds=null;RebuildRevisionNavigation();IslandFrontEndBuilder.RefreshMenuPresentation();EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");Debug.Log("ROAD_CORRECTION_SAVED signals=0 clearance="+result.minimumBuildingClearance);
    }
    static byte[] FloatBytes(float[,,] a){var b=new byte[a.Length*4];Buffer.BlockCopy(a,0,b,0,b.Length);return b;}
}
