using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class IslandRevision20261018
{
    [Serializable] public class SavedVerification {
        public string utc;public bool passed;
        public int protectedNodes,preservedFurniture,libraryPrefabs,remainingDecorationRoots,missingScripts,remainingTerrainDetails,remainingInstancedGrassBatches;
        public string[] protectionFailures,remainingPlannedObjects,missingFurniture,invalidPrefabs,terrainHeightChanges,unknownCityMeshes;
    }
    public static void VerifySaved() {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play before reading saved island scenes");
        revisionObjects=null;revisionIds=null;
        var scenes=LoadIsland();var all=scenes.SelectMany(All).ToArray();IndexRevisionObjects(all);
        var exported=JsonUtility.FromJson<DecorationPlan>(File.ReadAllText(Path.Combine(Reports,"island-decoration-export.json")));
        var details=JsonUtility.FromJson<TerrainDetailArchive>(File.ReadAllText(Path.Combine(Reports,"island-terrain-details-export.json")));
        var residual=JsonUtility.FromJson<ResidualArchive>(File.ReadAllText(Path.Combine(Reports,"island-revision-residual-export.json")));
        var exemptions=new System.Collections.Generic.HashSet<string>(residual.objects.SelectMany(x=>x.excludedProtectionIds));
        var report=new SavedVerification{utc=DateTime.UtcNow.ToString("o"),protectedNodes=exported.protectedObjects.Length,preservedFurniture=exported.decorations.Count(d=>d.action=="PRESERVE_NPC_FURNITURE")};
        report.protectedNodes-=exemptions.Count;
        report.protectionFailures=exported.protectedObjects.Where(p=>!exemptions.Contains(p.globalId)).Where(p=>{var t=Resolve<Transform>(p.globalId);return !t||Signature(t)!=p.signature;}).Select(p=>p.path).ToArray();
        report.remainingPlannedObjects=exported.decorations.Where(p=>p.action=="REMOVE_AFTER_EXPORT"&&Resolve<Transform>(p.globalId)).Select(p=>p.path).Concat(residual.objects.Where(p=>Resolve<Transform>(p.globalId)).Select(p=>p.path)).ToArray();
        report.missingFurniture=exported.decorations.Where(p=>p.action=="PRESERVE_NPC_FURNITURE"&&!Resolve<Transform>(p.globalId)).Select(p=>p.path).ToArray();
        report.missingScripts=all.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        var paths=exported.decorations.Select(p=>p.prefab).Concat(details.layers.Select(p=>p.prefab)).Concat(residual.objects.Select(p=>p.prefab)).Distinct().ToArray();report.libraryPrefabs=paths.Length;
        report.invalidPrefabs=paths.Where(p=>{
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(p);
            return !prefab||prefab.GetComponentsInChildren<Transform>(true).Any(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)||prefab.GetComponentsInChildren<MeshFilter>(true).Any(f=>!f.sharedMesh)||prefab.GetComponentsInChildren<Renderer>(true).Any(r=>r.sharedMaterials.Any(m=>!m||!m.shader));
        }).ToArray();
        report.terrainHeightChanges=details.layers.Where(p=>HeightHash(AssetDatabase.LoadAssetAtPath<TerrainData>(p.terrainData))!=p.heightHash).Select(p=>p.terrainData).ToArray();
        var current=CreatePlan(scenes);report.remainingDecorationRoots=current.rootsToRemove;report.unknownCityMeshes=current.unknownMeshes;
        report.remainingTerrainDetails=current.terrains.Sum(t=>t.detailInstances);
        report.remainingInstancedGrassBatches=ResidualRoots(all).Length;
        report.passed=report.protectionFailures.Length==0&&report.remainingPlannedObjects.Length==0&&report.missingFurniture.Length==0&&report.missingScripts==0&&report.invalidPrefabs.Length==0&&report.terrainHeightChanges.Length==0&&report.remainingDecorationRoots==0&&report.remainingTerrainDetails==0&&report.unknownCityMeshes.Length==0&&report.remainingInstancedGrassBatches==0;
        File.WriteAllText(Path.Combine(Reports,"island-revision-saved-verification.json"),JsonUtility.ToJson(report,true));
        revisionObjects=null;revisionIds=null;EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);
        Debug.Log("ISLAND_REVISION_SAVED_VERIFICATION "+report.passed);
    }
}
