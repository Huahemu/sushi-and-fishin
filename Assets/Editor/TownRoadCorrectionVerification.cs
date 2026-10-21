using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class IslandRevision20261018
{
    [Serializable] public class RoadCorrectionSaved {public string utc,stage;public bool passed,benchFeetSupported;public int checkedBuildings,buildingRoadPairs,signals,parcels,protectedNodes;public float minimumBuildingClearance;public string[] signalObjects,failures;public BuildingRoadClearance[] clearances;public RoadSavedAudit roadAudit;}
    public static void FinishRoadCorrectionAndVerify() {
        var receipt=JsonUtility.FromJson<RoadCorrectionResult>(File.ReadAllText(Path.Combine(Reports,"town-road-correction-build.json")));
        if(!File.Exists(Path.Combine(Reports,"town-road-correction-bench-refinement.json"))) {
            var scenes=LoadIsland();var all=scenes.SelectMany(All).ToArray();var root=all.Single(t=>t.name=="TownRoadNetwork20261019");var plan=receipt.plan;
            var lands=all.Select(t=>t.GetComponent<Terrain>()).Where(t=>t&&t.gameObject.activeInHierarchy).ToArray();PrepareDeck(plan,lands);correctionBenchBay=true;roadOutput=receipt.assets;roadMeshCount=1000;
            var asphalt=AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/M_asphalt_01 1.mat");var sidewalk=AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/M_tiles_ground.mat");var curb=AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/M_curbstone_2.mat");var white=AssetDatabase.LoadAssetAtPath<Material>(roadOutput+"/Materials/RoadMarkingWhite.mat");var foundation=AssetDatabase.LoadAssetAtPath<Material>(roadOutput+"/Materials/RoadbedConcrete.mat");
            var old=root.Find("RoadSections/MarketStreet");old.name="MarketStreet_BeforeBenchRefinement";
            BuildRoadLine(root.Find("RoadSections"),plan.roads.Single(r=>r.id=="MarketStreet"),plan,asphalt,sidewalk,curb,white,foundation,RoadNodes(plan));UnityEngine.Object.DestroyImmediate(old.gameObject);
            UnityEngine.Object.DestroyImmediate(root.Find("Junctions/SeafrontBenchBay").gameObject);CorrectionBenchBay(root.Find("Junctions"),sidewalk,foundation);
            PrefabUtility.SaveAsPrefabAsset(root.Find("RoadSections/MarketStreet").gameObject,roadOutput+"/Prefabs/MarketStreet.prefab");PrefabUtility.SaveAsPrefabAsset(root.Find("Junctions").gameObject,roadOutput+"/Prefabs/Junctions_NoSignals.prefab");
            EditorSceneManager.SaveScene(scenes[0]);AssetDatabase.SaveAssets();correctionBenchBay=false;
            File.WriteAllText(Path.Combine(Reports,"town-road-correction-bench-refinement.json"),"{\"saved\":true}");RebuildRevisionNavigation();IslandFrontEndBuilder.RefreshMenuPresentation();
        }
        VerifyRoadTownSaved();revisionObjects=null;revisionIds=null;
        var loaded=LoadIsland();var transforms=loaded.SelectMany(All).ToArray();var roadRoot=transforms.Single(t=>t.name=="TownRoadNetwork20261019");Physics.SyncTransforms();
        var inv=JsonUtility.FromJson<PlanningInventory>(File.ReadAllText(Path.Combine(Reports,"town-road-planning-inventory.json")));IndexRevisionObjects(transforms);
        var rows=BuildingClearances(transforms,receipt.plan);var failures=new List<string>();
        foreach(var p in inv.protectedNodes){var t=Resolve<Transform>(p.globalId);if(!t||Signature(t)!=p.signature)failures.Add(p.path);}
        foreach(var row in rows.Where(r=>r.overlaps||r.clearance<.5f))failures.Add(row.building+" / "+row.road);
        var signals=transforms.Where(t=>t.gameObject.activeInHierarchy&&(t.name.StartsWith("TrafficSignal_")||t.name.StartsWith("SM_trafficlight")||t.name=="VehicleSignalHead")).Select(P).ToArray();
        bool bench=true;foreach(var path in new[]{"Root/Bench/BenchWood2 (16)","Root/Bench/BenchWood2 (18)"}){var b=PlanningBounds(transforms.Single(t=>P(t)==path));var hits=Physics.RaycastAll(new Vector3(b.center.x,b.min.y+.3f,b.center.z),Vector3.down,1,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(roadRoot)&&h.collider.name.EndsWith("_Sidewalks")).ToArray();if(!hits.Any(h=>Mathf.Abs(h.point.y-b.min.y)<.05f))bench=false;}
        var road=JsonUtility.FromJson<RoadSavedAudit>(File.ReadAllText(Path.Combine(Reports,"town-road-saved-verification.json")));
        var result=new RoadCorrectionSaved{utc=DateTime.UtcNow.ToString("o"),stage="REOPENED_FULL_BUILDING_AND_ROAD_CHECK",roadAudit=road,checkedBuildings=rows.Select(r=>r.building).Distinct().Count(),buildingRoadPairs=rows.Length,minimumBuildingClearance=rows.Min(r=>r.clearance),signals=signals.Length,signalObjects=signals,parcels=roadRoot.Find("YellowParcelReservations").childCount,protectedNodes=inv.protectedNodes.Length,failures=failures.ToArray(),clearances=rows,benchFeetSupported=bench};
        result.passed=road.passed&&rows.Length==39&&signals.Length==0&&result.parcels==13&&failures.Count==0&&bench;
        File.WriteAllText(Path.Combine(Reports,"town-road-correction-saved-verification.json"),JsonUtility.ToJson(result,true));revisionObjects=null;revisionIds=null;EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");Debug.Log("ROAD_CORRECTION_SAVED_VERIFICATION "+result.passed+" bench="+bench+" signals="+signals.Length+" fullBuildingPairs="+rows.Length);
    }
}
