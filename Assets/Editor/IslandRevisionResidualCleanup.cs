using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PaperPixelGames.Core.Optimization;
using PaperPixelGames.Core.DressingSceneSystem;
using Unity.AI.Navigation;
using UnityEngine.AI;

public static partial class IslandRevision20261018
{
    public static void RebuildRevisionNavigation() {
        var scenes=LoadIsland();var all=scenes.SelectMany(All).ToArray();
        var activation=all.ToDictionary(t=>t.gameObject,t=>t.gameObject.activeSelf);
        foreach(var root in all.Select(t=>t.GetComponent<DressingSceneRoot>()).Where(t=>t))root.ActivateAllImmediate();
        var surface=all.Select(t=>t.GetComponent<NavMeshSurface>()).First(t=>t&&P(t.transform)=="---AI/ResidentManager/NavMesh Surface");
        surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();
        string path=AssetDatabase.GenerateUniqueAssetPath("Assets/SushiAndFish/Modules/OriginalIsland/TerrainData/TownIsland_RevisionFinal_20261019.asset");AssetDatabase.CreateAsset(surface.navMeshData,path);
        foreach(var pair in activation)if(pair.Key&&pair.Key.activeSelf!=pair.Value)pair.Key.SetActive(pair.Value);
        foreach(var scene in scenes)EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine(Reports,"island-revision-final-navigation.json"),JsonUtility.ToJson(new NavigationReceipt{utc=DateTime.UtcNow.ToString("o"),asset=path},true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);
    }
    [Serializable] public class NavigationReceipt {public string utc,asset;}
    [Serializable] public class ResidualDecoration {
        public string scene,path,globalId,prefab,category,componentArchive;
        public int instances;public Vector3 position,scale;public Quaternion rotation;
        public string[] excludedProtectionIds;
    }
    [Serializable] public class ResidualArchive {public string utc,stage;public int batchDrawInstances,rootsRemoved;public ResidualDecoration[] objects;public string[] protectionFailures;}
    static bool ResidualMarketDecoration(Transform t) {
        string p=P(t);
        if(!p.StartsWith("Root/Comps/MarinaMarket/")&&!p.StartsWith("Root/Comps/GabeMarket/"))return false;
        return Regex.IsMatch(t.name,@"^SM_(tree|bush|flowerpot|plants_box|bicycle|mofa|carton|chair)(?:_|\b)",RegexOptions.IgnoreCase);
    }
    static Transform[] ResidualRoots(Transform[] all) {
        var chosen=new HashSet<Transform>(all.Where(t=>t.GetComponent<GrassInstancedChunkRenderer>()||ResidualMarketDecoration(t)).Select(t=>t.GetComponent<GrassInstancedChunkRenderer>()?t:AtomicRoot(t)));
        return chosen.Where(t=>{for(var p=t.parent;p;p=p.parent)if(chosen.Contains(p))return false;return true;}).ToArray();
    }
    public static void PlanResiduals() {
        revisionObjects=null;revisionIds=null;
        var scenes=LoadIsland();var all=scenes.SelectMany(All).ToArray();IndexRevisionObjects(all);
        var baseline=JsonUtility.FromJson<DecorationPlan>(File.ReadAllText(Path.Combine(Reports,"island-decoration-export.json")));
        var roots=ResidualRoots(all);var rows=new List<ResidualDecoration>();
        foreach(var t in roots) {
            var batch=t.GetComponent<GrassInstancedChunkRenderer>();
            var childIds=new HashSet<string>(t.GetComponentsInChildren<Transform>(true).Select(x=>Id(x)));
            rows.Add(new ResidualDecoration{scene=t.gameObject.scene.path,path=P(t),globalId=Id(t),category=batch?"InstancedGrass":"MarketExteriorDecorations",instances=batch?batch.MatrixCount:1,position=t.position,rotation=t.rotation,scale=t.lossyScale,componentArchive=batch?EditorJsonUtility.ToJson(batch):"",excludedProtectionIds=baseline.protectedObjects.Where(x=>childIds.Contains(x.globalId)).Select(x=>x.globalId).ToArray()});
        }
        var report=new ResidualArchive{utc=DateTime.UtcNow.ToString("o"),stage="PLAN_BEFORE_REMOVAL",objects=rows.ToArray(),batchDrawInstances=rows.Where(x=>x.category=="InstancedGrass").Sum(x=>x.instances)};
        File.WriteAllText(Path.Combine(Reports,"island-revision-residual-plan.json"),JsonUtility.ToJson(report,true));
        revisionObjects=null;revisionIds=null;EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);
    }
    public static void ExportResiduals() {
        var report=JsonUtility.FromJson<ResidualArchive>(File.ReadAllText(Path.Combine(Reports,"island-revision-residual-plan.json")));
        revisionObjects=null;revisionIds=null;var scenes=LoadIsland();IndexRevisionObjects(scenes.SelectMany(All).ToArray());
        string folder=LibraryRoot+"/ResidualDecorations";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        foreach(var row in report.objects) {
            var source=Resolve<Transform>(row.globalId);if(!source)throw new Exception("Residual source disappeared: "+row.path);
            GameObject model;string key;
            var batch=source.GetComponent<GrassInstancedChunkRenderer>();
            if(batch) {
                var state=new SerializedObject(batch);var near=state.FindProperty("_nearMesh").objectReferenceValue as Mesh;var far=state.FindProperty("_farMesh").objectReferenceValue as Mesh;var material=state.FindProperty("_material").objectReferenceValue as Material;
                key=Sha(AssetDatabase.GetAssetPath(near)+AssetDatabase.GetAssetPath(far)+AssetDatabase.GetAssetPath(material)).Substring(0,12);
                model=new GameObject("ReusableInstancedGrass_"+key);var lod=model.AddComponent<LODGroup>();var levels=new List<LOD>();int level=0;
                foreach(var mesh in new[]{near,far}.Where(x=>x)) {var child=new GameObject("LOD"+level);child.transform.SetParent(model.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;levels.Add(new LOD(level==0?.4f:.01f,new[]{renderer}));level++;}
                lod.SetLODs(levels.ToArray());lod.RecalculateBounds();
            } else {
                key=VariantKey(source).Substring(0,12);model=UnityEngine.Object.Instantiate(source.gameObject);model.name=source.name;
                model.transform.SetParent(null);model.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);model.transform.localScale=source.lossyScale;
                foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true).Reverse())if(script)UnityEngine.Object.DestroyImmediate(script);
                foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))renderer.forceRenderingOff=false;
                foreach(var lod in model.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(-1);
            }
            model.SetActive(true);row.prefab=folder+"/"+row.category+"_"+key+".prefab";
            if(!File.Exists(row.prefab)){PrefabUtility.SaveAsPrefabAsset(model,row.prefab,out bool success);if(!success)throw new Exception("Residual prefab export failed");}
            UnityEngine.Object.DestroyImmediate(model);
        }
        AssetDatabase.SaveAssets();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(var row in report.objects)if(!AssetDatabase.LoadAssetAtPath<GameObject>(row.prefab))throw new Exception("Residual prefab cannot be reopened");
        report.stage="EXPORTED_AND_REOPENED_BEFORE_REMOVAL";File.WriteAllText(Path.Combine(Reports,"island-revision-residual-export.json"),JsonUtility.ToJson(report,true));
        revisionObjects=null;revisionIds=null;EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);
    }
    public static void ApplyResiduals() {
        var report=JsonUtility.FromJson<ResidualArchive>(File.ReadAllText(Path.Combine(Reports,"island-revision-residual-export.json")));
        if(report.stage!="EXPORTED_AND_REOPENED_BEFORE_REMOVAL")throw new Exception("Residual export must be verified before removal");
        revisionObjects=null;revisionIds=null;var scenes=LoadIsland();IndexRevisionObjects(scenes.SelectMany(All).ToArray());
        var baseline=JsonUtility.FromJson<DecorationPlan>(File.ReadAllText(Path.Combine(Reports,"island-decoration-export.json")));
        var exemptions=new HashSet<string>(report.objects.SelectMany(x=>x.excludedProtectionIds));
        foreach(var row in report.objects) {
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(row.prefab))throw new Exception("Residual prefab missing: "+row.path);
            var source=Resolve<Transform>(row.globalId);if(!source)throw new Exception("Residual planned object missing: "+row.path);
            UnityEngine.Object.DestroyImmediate(source.gameObject);report.rootsRemoved++;
        }
        report.protectionFailures=baseline.protectedObjects.Where(x=>!exemptions.Contains(x.globalId)).Where(x=>{var t=Resolve<Transform>(x.globalId);return !t||Signature(t)!=x.signature;}).Select(x=>x.path).ToArray();
        if(report.protectionFailures.Length>0)throw new Exception("Unexpected protected object change");
        foreach(var scene in scenes) {
            foreach(var lod in All(scene).Select(t=>t.GetComponent<LODGroup>()).Where(x=>x)) {var levels=lod.GetLODs();for(int i=0;i<levels.Length;i++)levels[i].renderers=levels[i].renderers.Where(x=>x).ToArray();lod.SetLODs(levels);}
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();report.stage="SAVED_NEEDS_RUNTIME_VERIFICATION";
        File.WriteAllText(Path.Combine(Reports,"island-revision-residual-applied.json"),JsonUtility.ToJson(report,true));
        revisionObjects=null;revisionIds=null;IslandFrontEndBuilder.RefreshMenuPresentation();EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);
    }
}
