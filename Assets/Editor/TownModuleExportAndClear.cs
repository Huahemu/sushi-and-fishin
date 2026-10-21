using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.DressingSceneSystem;

public static partial class IslandRevision20261018
{
    static TownRow TownDescribe(Transform t,string category,string action) {
        var b=IslandComprehensiveRepair.BoundsOf(t);
        return new TownRow{scene=t.gameObject.scene.path,path=P(t),id=Id(t),category=category,action=action,key=VariantKey(t),position=t.position,rotation=t.rotation,scale=t.lossyScale,min=b.min,max=b.max,transforms=t.GetComponentsInChildren<Transform>(true).Length,meshes=t.GetComponentsInChildren<MeshFilter>(true).Length};
    }
    public static void TownExport() {
        if(!File.Exists(Path.Combine(Reports,"town-simplification-backup.json")))throw new Exception("Town backup is required");
        var plan=JsonUtility.FromJson<TownPlan>(File.ReadAllText(Path.Combine(Reports,"town-simplification-plan.json")));
        revisionObjects=null;revisionIds=null;var scenes=LoadIsland();var all=scenes.SelectMany(All).ToArray();IndexRevisionObjects(all);
        var removals=new HashSet<Transform>(plan.objects.Where(x=>x.action=="REMOVE_AFTER_EXPORT").Select(x=>Resolve<Transform>(x.id)).Where(t=>t));
        if(removals.Count!=plan.removeRoots)throw new Exception("Planned source objects changed before export");
        var protectedRoots=new HashSet<Transform>(plan.objects.Where(x=>x.action.StartsWith("KEEP_")).Select(x=>Resolve<Transform>(x.id)).Where(t=>t));
        plan.protectedNodes=all.Where(t=>!InRoot(t,removals)&&(MarketPath(P(t))||P(t).StartsWith("---Restaurant")||InRoot(t,protectedRoots)||t.GetComponent<WaypointAnchor>())).Select(t=>new Protection{scene=t.gameObject.scene.path,path=P(t),globalId=Id(t),signature=Signature(t),reason="functional_building_main_road_or_npc_binding"}).ToArray();
        var assemblyPaths=plan.objects.Where(x=>x.category=="Structures").Select(x=>string.Join("/",x.path.Split('/').Take(3))).Distinct().ToArray();
        plan.assemblies=all.Where(t=>t.gameObject.scene.name=="100_Dressing_City_01"&&assemblyPaths.Contains(P(t))).Select(t=>TownDescribe(t,"StructureAssemblies","EXPORT_COMPOSITE_ONLY")).ToArray();
        var records=plan.objects.Where(x=>x.action!="KEEP_NPC_FURNITURE").Concat(plan.assemblies).ToArray();
        Directory.CreateDirectory(TownLibrary);foreach(var category in records.Select(x=>x.category).Distinct())Directory.CreateDirectory(TownLibrary+"/"+category);AssetDatabase.Refresh();
        var variants=new Dictionary<string,string>();int count=0;
        AssetDatabase.StartAssetEditing();
        try {
            foreach(var row in records) {
                if(variants.TryGetValue(row.key,out var existing)){row.prefab=existing;continue;}
                var source=Resolve<Transform>(row.id);if(!source)throw new Exception("Export source missing: "+row.path);
                if(Vector3.Distance(source.position,row.position)>.0001f)throw new Exception("Source pose changed after plan: "+row.path);
                var model=UnityEngine.Object.Instantiate(source.gameObject);model.name=Regex.Replace(source.name,@" \(\d+\)$","");
                model.transform.SetParent(null);model.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);model.transform.localScale=source.lossyScale;model.SetActive(true);
                foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true).Reverse())if(script)UnityEngine.Object.DestroyImmediate(script);
                foreach(var body in model.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(body);
                foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))renderer.forceRenderingOff=false;
                foreach(var lod in model.GetComponentsInChildren<LODGroup>(true)){lod.ForceLOD(-1);var levels=lod.GetLODs();for(int i=0;i<levels.Length;i++)levels[i].renderers=levels[i].renderers.Where(x=>x).ToArray();lod.SetLODs(levels);}
                string name=Regex.Replace(model.name,@"[^\p{L}\p{Nd}_-]","_");row.prefab=TownLibrary+"/"+row.category+"/"+name+"_"+row.key.Substring(0,12)+".prefab";
                PrefabUtility.SaveAsPrefabAsset(model,row.prefab,out bool success);UnityEngine.Object.DestroyImmediate(model);if(!success)throw new Exception("Module export failed: "+row.prefab);
                variants.Add(row.key,row.prefab);count++;if(count%25==0)File.WriteAllText(Path.Combine(Reports,"town-module-export-progress.txt"),count+" variants exported");
            }
        } finally {AssetDatabase.StopAssetEditing();}
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(var path in variants.Values) {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!prefab||prefab.GetComponentsInChildren<MeshFilter>(true).Any(f=>!f.sharedMesh)||prefab.GetComponentsInChildren<Transform>(true).Any(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0))throw new Exception("Module failed reopening validation: "+path);
        }
        plan.variants=variants.Count;plan.stage="EXPORTED_AND_REOPENED_BEFORE_REMOVAL";
        File.WriteAllText(Path.Combine(Reports,"town-module-export.json"),JsonUtility.ToJson(plan,true));
        var csv=new StringBuilder("category,prefab,source_scene,source_path,action\n");
        foreach(var row in records)csv.AppendLine(string.Join(",",new[]{row.category,row.prefab,row.scene,row.path,row.action}.Select(x=>"\""+x.Replace("\"","\"\"")+"\"")));
        File.WriteAllText(Path.Combine(Reports,"town-module-catalog.csv"),csv.ToString(),new UTF8Encoding(true));
        File.WriteAllText(TownLibrary+"/README.txt","小镇模块资源库\n删除前保存并重新加载验证的道路、铺装、垃圾桶、街道设施和结构预制体。StructureAssemblies 为组合模块，其余为可独立放置的部件。\n保留原网格、材质、LOD 和碰撞，移除玩法/网络脚本及刚体；根位置、旋转归零，保留代表实例比例。原实例世界变换见 reports/town-module-export.json，完整目录见 town-module-catalog.csv。\n");
        revisionObjects=null;revisionIds=null;AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");Debug.Log("TOWN_MODULES_REOPENED "+variants.Count);
    }
    [Serializable] public class TownClearResult {public string utc,stage;public int removedRoots,removedTransforms,prefabs,mainRoads,npcFurniture,clearedReferences,protectedNodes;public string[] protectionFailures,remainingRemovedRoots;public string navigation;}
    static int TownClearReferences(IEnumerable<Transform> nodes,HashSet<int> deleted) {
        int count=0;
        foreach(var component in nodes.Where(t=>t).SelectMany(t=>t.GetComponents<MonoBehaviour>()).Where(c=>c)) {
            var state=new SerializedObject(component);var it=state.GetIterator();bool enter=true,changed=false;
            while(it.Next(enter)) {
                enter=true;
                if(it.isArray&&it.propertyType!=SerializedPropertyType.String&&it.arraySize>0) {
                    var type=it.GetArrayElementAtIndex(0).propertyType;
                    if(type!=SerializedPropertyType.ObjectReference&&type!=SerializedPropertyType.Generic)enter=false;
                }
                if(it.propertyType==SerializedPropertyType.ObjectReference&&deleted.Contains(it.objectReferenceInstanceIDValue)){it.objectReferenceValue=null;count++;changed=true;}
            }
            if(changed)state.ApplyModifiedPropertiesWithoutUndo();
        }
        return count;
    }
    public static void TownClear() {
        var plan=JsonUtility.FromJson<TownPlan>(File.ReadAllText(Path.Combine(Reports,"town-module-export.json")));
        if(plan.stage!="EXPORTED_AND_REOPENED_BEFORE_REMOVAL")throw new Exception("Verified module export required before clear");
        if(File.Exists(Path.Combine(Reports,"town-simplification-cleared.json")))throw new Exception("Clear already completed; inspect saved receipt before replay");
        revisionObjects=null;revisionIds=null;var scenes=LoadIsland();IndexRevisionObjects(scenes.SelectMany(All).ToArray());
        foreach(var row in plan.protectedNodes){var t=Resolve<Transform>(row.globalId);if(!t||Signature(t)!=row.signature)throw new Exception("Protected baseline changed: "+row.path);}
        var result=new TownClearResult{utc=DateTime.UtcNow.ToString("o"),stage="STARTED",prefabs=plan.variants,mainRoads=plan.mainRoads.Length,npcFurniture=plan.keepFurniture,protectedNodes=plan.protectedNodes.Length};var deleted=new HashSet<int>();
        foreach(var row in plan.objects.Where(x=>x.action=="REMOVE_AFTER_EXPORT")) {
            if(string.IsNullOrEmpty(row.prefab)||!AssetDatabase.LoadAssetAtPath<GameObject>(row.prefab))throw new Exception("Required module missing: "+row.path);
            var t=Resolve<Transform>(row.id);if(!t)throw new Exception("Planned object missing: "+row.path);
            foreach(var child in t.GetComponentsInChildren<Transform>(true)){deleted.Add(child.gameObject.GetInstanceID());foreach(var c in child.GetComponents<Component>())if(c)deleted.Add(c.GetInstanceID());}
            result.removedTransforms+=t.GetComponentsInChildren<Transform>(true).Length;UnityEngine.Object.DestroyImmediate(t.gameObject);result.removedRoots++;
        }
        foreach(var scene in scenes) {
            var nodes=All(scene);result.clearedReferences+=TownClearReferences(nodes,deleted);
            foreach(var lod in nodes.Select(t=>t.GetComponent<LODGroup>()).Where(l=>l)) {var levels=lod.GetLODs();for(int i=0;i<levels.Length;i++)levels[i].renderers=levels[i].renderers.Where(x=>x).ToArray();lod.SetLODs(levels);}
        }
        result.protectionFailures=plan.protectedNodes.Where(p=>{var t=Resolve<Transform>(p.globalId);return !t||Signature(t)!=p.signature;}).Select(p=>p.path).ToArray();
        result.remainingRemovedRoots=plan.objects.Where(x=>x.action=="REMOVE_AFTER_EXPORT"&&Resolve<Transform>(x.id)).Select(x=>x.path).ToArray();
        if(result.protectionFailures.Length>0||result.remainingRemovedRoots.Length>0)throw new Exception("Clear preservation check failed");
        foreach(var scene in scenes)EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();result.stage="SAVED_NEEDS_RUNTIME_VERIFICATION";
        revisionObjects=null;revisionIds=null;
        File.WriteAllText(Path.Combine(Reports,"town-simplification-cleared.json"),JsonUtility.ToJson(result,true));
        RebuildRevisionNavigation();result.navigation=JsonUtility.FromJson<NavigationReceipt>(File.ReadAllText(Path.Combine(Reports,"island-revision-final-navigation.json"))).asset;
        File.WriteAllText(Path.Combine(Reports,"town-simplification-cleared.json"),JsonUtility.ToJson(result,true));
        IslandFrontEndBuilder.RefreshMenuPresentation();EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");Debug.Log("TOWN_SIMPLIFICATION_SAVED "+result.removedRoots);
    }
}
