using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Netcode;
using SushiAndFish.CityExpansion;

public static partial class IslandRevision20261018
{
    [Serializable]public class CityTerrainReceipt
    {
        public string utc;public bool passed;public FullTerrainProof[] before,after;public string[] failures;
    }
    public static bool CityTerrainReadback()
    {
        var scenes=LoadIsland();
        var before=JsonUtility.FromJson<UnifiedRoadReceipt>(File.ReadAllText(Path.Combine(Reports,"town-unified-road-build.json"))).terrains;
        var lands=scenes.SelectMany(All).Select(t=>t.GetComponent<Terrain>()).Where(t=>t&&t.gameObject.activeInHierarchy).ToArray();
        var after=lands.Select(FullTerrain).ToArray();
        var failed=before.Where(b=>!after.Any(a=>a.path==b.path&&SameTerrain(a,b))).Select(b=>b.path).ToArray();
        var receipt=new CityTerrainReceipt{utc=DateTime.UtcNow.ToString("o"),before=before,after=after,failures=failed,passed=before.Length==6&&after.Length==6&&failed.Length==0};
        File.WriteAllText(Path.Combine(Reports,"city-terrain-final-proof.json"),JsonUtility.ToJson(receipt,true));
        return receipt.passed;
    }
}

public static partial class CityExpansionInstallation
{
    [Serializable]public class FinalSavedReceipt
    {
        public string utc,stage,error;public bool passed,terrainUnchanged;public int perimeterObjects,seatCount,uniqueSeatHashes,roadSamples,parcelRoots,missingScripts,auditoriumTierColliders,remainingCasinoStaticBatches;
        public float maximumPerimeterPoseError,maximumPerimeterBoundsError;public List<Conflict> roadConflicts=new List<Conflict>();public List<string> failures=new List<string>();
    }
    public static void FinalStaticAndAll()
    {
        VerifyFinalSaved();CityExpansionPlayAudit.All();
    }
    public static void VerifyFinalSaved()
    {
        var receipt=new FinalSavedReceipt{utc=DateTime.UtcNow.ToString("o")};
        string path=Path.Combine(CityExpansionInspection.Reports,"city-final-saved-audit.json");
        try
        {
            receipt.terrainUnchanged=IslandRevision20261018.CityTerrainReadback();
            receipt.stage="TERRAIN_READBACK_COMPLETE";File.WriteAllText(path,JsonUtility.ToJson(receipt,true));
            var nodes=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>!EditorUtility.IsPersistent(t)).ToArray();
            var perimeter=nodes.Single(t=>t.name=="RestoredIslandPerimeter_20261021");receipt.perimeterObjects=perimeter.childCount;
            var source=JsonUtility.FromJson<RestorePlan>(File.ReadAllText(Path.Combine(CityExpansionInspection.Reports,"city-perimeter-restore-plan.json")));
            if(source.objects.Length!=perimeter.childCount)receipt.failures.Add("Perimeter count differs from the classified source plan.");
            for(int i=0;i<Math.Min(source.objects.Length,perimeter.childCount);i++)
            {
                var expected=source.objects[i];var actual=perimeter.GetChild(i);var bounds=IslandComprehensiveRepair.BoundsOf(actual);
                receipt.maximumPerimeterPoseError=Mathf.Max(receipt.maximumPerimeterPoseError,Vector3.Distance(expected.position,actual.position),Vector3.Distance(expected.scale,actual.localScale));
                receipt.maximumPerimeterBoundsError=Mathf.Max(receipt.maximumPerimeterBoundsError,Vector3.Distance(expected.min,bounds.min),Vector3.Distance(expected.max,bounds.max));
                if(actual.name!=expected.path.Split('/').Last()||Quaternion.Angle(actual.rotation,expected.rotation)>.001f)receipt.failures.Add("Perimeter identity or rotation mismatch: "+expected.path);
            }
            receipt.stage="PERIMETER_READBACK_COMPLETE";File.WriteAllText(path,JsonUtility.ToJson(receipt,true));
            var seats=nodes.Select(t=>t.GetComponent<CityDiningSeat>()).Where(s=>s).ToArray();receipt.seatCount=seats.Length;
            var hashes=seats.Select(s=>(uint)new SerializedObject(s.GetComponent<NetworkObject>()).FindProperty("GlobalObjectIdHash").longValue).ToArray();receipt.uniqueSeatHashes=hashes.Where(h=>h!=0).Distinct().Count();
            receipt.parcelRoots=nodes.Single(t=>t.name=="YellowParcelReservations").childCount;
            receipt.missingScripts=nodes.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            var auditorium=nodes.Single(x=>x.name=="AR_CinemaAuditorium");
            receipt.auditoriumTierColliders=nodes.Where(t=>t.IsChildOf(auditorium))
                .Count(t=>(t.name.Contains("Tier_")||t.name.Contains("HalfStep")||t.name.Contains("RearCorridor"))&&t.GetComponent<MeshCollider>());
            receipt.stage="SEAT_READBACK_COMPLETE";File.WriteAllText(path,JsonUtility.ToJson(receipt,true));
            Physics.SyncTransforms();world=new WorldReceipt();ProbeRoads();receipt.roadSamples=world.roadSamples;receipt.roadConflicts=world.roadConflicts;
            receipt.stage="ROAD_PROBE_COMPLETE";File.WriteAllText(path,JsonUtility.ToJson(receipt,true));
            var casinoScene=EditorSceneManager.OpenScene("Assets/SushiAndFish/Casino/Imported/Scenes/CasinoScene.unity",OpenSceneMode.Additive);
            receipt.remainingCasinoStaticBatches=casinoScene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).Count(r=>new SerializedObject(r).FindProperty("m_StaticBatchInfo.subMeshCount").intValue>0);
            receipt.passed=receipt.terrainUnchanged&&receipt.perimeterObjects==565&&receipt.maximumPerimeterPoseError<.0001f&&receipt.maximumPerimeterBoundsError<.001f&&receipt.seatCount==12&&receipt.uniqueSeatHashes==12&&receipt.parcelRoots==13&&receipt.missingScripts==0&&receipt.auditoriumTierColliders>=7&&receipt.roadSamples>=5252&&receipt.roadConflicts.Count==0&&receipt.remainingCasinoStaticBatches==0&&receipt.failures.Count==0;
            if(!receipt.passed)throw new Exception("The final saved city invariants failed.");
            receipt.stage="PASS";
        }
        catch(Exception e){receipt.error=e.ToString();receipt.passed=false;throw;}
        finally
        {
            File.WriteAllText(path,JsonUtility.ToJson(receipt,true));EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        }
    }
}
