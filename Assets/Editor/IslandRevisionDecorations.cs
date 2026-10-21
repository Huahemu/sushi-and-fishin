using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.DressingSceneSystem;

public static partial class IslandRevision20261018
{
    const string LibraryRoot="Assets/SushiAndFish/DecorationLibrary";
    [Serializable] public class Decoration {
        public string scene,path,globalId,category,action,reason,key,prefab;
        public Vector3 position,scale;public Quaternion rotation;public int meshes,renderers;
    }
    [Serializable] public class SeatBinding {
        public string anchor,anchorId,furniture,furnitureId,category;
        public Vector3 anchorPosition,modelPosition;public float distance;
    }
    [Serializable] public class Protection {public string scene,path,globalId,signature,reason;}
    [Serializable] public class TerrainRecord {public string name;public int trees,detailPrototypes,detailInstances;}
    [Serializable] public class DecorationPlan {
        public string utc,stage;public Decoration[] decorations;public SeatBinding[] seats;
        public Protection[] protectedObjects;public TerrainRecord[] terrains;public string[] unknownMeshes;
        public int rootsToRemove,preservedFurniture,uniqueVariants;public string error;
    }
    static Scene[] LoadIsland() {
        var list=new List<Scene>{EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene,OpenSceneMode.Single)};
        foreach(var name in new[]{"100_Dressing_City_01","110_Dressing_City_Folliage_01","120_Dressing_City_Grass_01","130_Dressing_City_Rocks_01"})
            list.Add(EditorSceneManager.OpenScene(IslandFrontEndBuilder.DressingRoot+"/"+name+".unity",OpenSceneMode.Additive));
        Physics.SyncTransforms();return list.ToArray();
    }
    static Dictionary<string,UnityEngine.Object> revisionObjects;
    static Dictionary<int,string> revisionIds;
    static void ApplyProgress(string stage) {
        var value=DateTime.UtcNow.ToString("o")+" "+stage;
        File.WriteAllText(Path.Combine(Reports,"island-revision-apply-progress.txt"),value);
        Debug.Log("ISLAND_REVISION_PROGRESS "+value);
    }
    static void IndexRevisionObjects(Transform[] all) {
        var objects=all.Cast<UnityEngine.Object>().ToArray();
        var ids=new GlobalObjectId[objects.Length];
        GlobalObjectId.GetGlobalObjectIdsSlow(objects,ids);
        revisionObjects=new Dictionary<string,UnityEngine.Object>(objects.Length);
        revisionIds=new Dictionary<int,string>(objects.Length);
        for(int i=0;i<objects.Length;i++) {
            string id=ids[i].ToString();revisionObjects.Add(id,objects[i]);revisionIds.Add(objects[i].GetInstanceID(),id);
        }
    }
    static string Id(UnityEngine.Object o)=>revisionIds!=null&&revisionIds.TryGetValue(o.GetInstanceID(),out var cached)?cached:GlobalObjectId.GetGlobalObjectIdSlow(o).ToString();
    static T Resolve<T>(string id) where T:UnityEngine.Object {
        if(revisionObjects!=null)return revisionObjects.TryGetValue(id,out var cached)?cached as T:null;
        return GlobalObjectId.TryParse(id,out var value)?GlobalObjectId.GlobalObjectIdentifierToObjectSlow(value) as T:null;
    }
    static string Sha(string value) {using(var hash=SHA256.Create())return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(b=>b.ToString("x2")));}
    static bool FurnitureName(string name)=>Regex.IsMatch(name,@"BenchWood|Sunbed|BeachChair|BeachTowel|SM_chair",RegexOptions.IgnoreCase);
    static string KeepReason(Transform t) {
        var p=P(t);
        if(t.gameObject.scene.name=="100_Dressing_City_01") {
            if(p.StartsWith("Root/Comps/MarinaMarket")||p.StartsWith("Root/Comps/GabeMarket"))return "functional_market_building";
            if(p.StartsWith("Root/Roads/"))return "road_sidewalk_curb_and_road_marking";
            if(Regex.IsMatch(p,@"^Root/Comps/Comp11(?:/| \()"))return "stairs_and_original_support";
            if(FurnitureName(t.name))return "";
            if(Regex.IsMatch(t.name,@"^SM_(wall|fence|utility_pole|pole_street|hydrant|firehydrant|fire_hydrant|traffic|sign_|street|sidewalk|curbs|drain|bikestand|trash)|^Dock|^Park_Wheel",RegexOptions.IgnoreCase))return "municipal_infrastructure_and_retaining_structure";
            if(Regex.IsMatch(t.name,@"^SM_(platform|manhole|lamp|mirror_round)|^WorldCollider$|^zen$|^SideWalkTerrainEdge$|^SideWalkWalls|^CornerFences",RegexOptions.IgnoreCase))return "original_paving_lighting_and_city_structure";
            if(p.StartsWith("Root/DockSide/")&&!Regex.IsMatch(t.name,@"tree|plant|bush|grass|palm|rock",RegexOptions.IgnoreCase))return "dock_structure";
            if(p.Contains("/SwingPole"))return "interactive_swing_support";
            var renderer=t.GetComponent<Renderer>();
            if(renderer&&renderer.sharedMaterials.Any(m=>m&&Regex.IsMatch(m.name,@"M_tiles_ground|M_curbstone|M_walls_japan|M_base_wall|M_fence",RegexOptions.IgnoreCase)))return "original_paving_and_structural_mesh";
        }
        return "";
    }
    static Transform AtomicRoot(Transform t) {
        var p=P(t);
        if(t.gameObject.scene.name=="GameState"&&p.StartsWith("---Beach/SandCastle")){while(t.parent&&t.parent.name!="---Beach")t=t.parent;return t;}
        if(p.StartsWith("Root/Others/Cars/")){while(t.parent&&P(t.parent)!="Root/Others/Cars")t=t.parent;return t;}
        if(p.StartsWith("Root/Others/Beach/")){while(t.parent&&P(t.parent)!="Root/Others/Beach")t=t.parent;return t;}
        Transform lod=null;
        for(var at=t;at&&at.parent;at=at.parent)if(at.GetComponent<LODGroup>()){lod=at;break;}
        return lod?lod:t;
    }
    static string Category(Transform t) {
        string p=P(t),n=t.name,s=t.gameObject.scene.name;
        if(s=="GameState")return p.StartsWith("---Beach/SandCastle")?"Sandcastles":"";
        if(!s.StartsWith("1"))return "";
        if(KeepReason(t)!="")return "";
        if(s.StartsWith("110"))return "Foliage";
        if(s.StartsWith("120"))return "Grass";
        if(s.StartsWith("130"))return "Rocks";
        if(p.StartsWith("Root/Others/Cars/"))return "Vehicles";
        if(FurnitureName(n))return "Furniture";
        if(Regex.IsMatch(n,@"grass",RegexOptions.IgnoreCase))return "Grass";
        if(Regex.IsMatch(n,@"bamboo",RegexOptions.IgnoreCase))return "Bamboo";
        if(Regex.IsMatch(n,@"tree|palm|TSI_",RegexOptions.IgnoreCase))return "Trees";
        if(Regex.IsMatch(n,@"bush|shrub|flower|fern|plant",RegexOptions.IgnoreCase))return "Foliage";
        if(Regex.IsMatch(n,@"rock|stone|boulder",RegexOptions.IgnoreCase))return "Rocks";
        if(Regex.IsMatch(n,@"SM_bicycle|SM_mofa|SM_car",RegexOptions.IgnoreCase))return "Vehicles";
        if(Regex.IsMatch(n,@"BeachUmbrella|Surfboard|SandCastle|SM_board_foldable|SM_vendingmachine|SM_bottle",RegexOptions.IgnoreCase))return "Props";
        return "";
    }
    static string VariantKey(Transform t) {
        var builder=new StringBuilder();
        foreach(var f in t.GetComponentsInChildren<MeshFilter>(true)) {
            var r=f.GetComponent<Renderer>();builder.Append(AssetDatabase.GetAssetPath(f.sharedMesh)).Append('|');
            if(r)foreach(var m in r.sharedMaterials)builder.Append(AssetDatabase.GetAssetPath(m)).Append('|');
            if(f.transform!=t)builder.Append(f.transform.localPosition.ToString("R")).Append(f.transform.localRotation.ToString("R")).Append(f.transform.localScale.ToString("R"));
        }
        foreach(var c in t.GetComponentsInChildren<Collider>(true)) {
            builder.Append(c.GetType().FullName).Append(c.isTrigger);
            if(c is BoxCollider box)builder.Append(box.center.ToString("R")).Append(box.size.ToString("R"));
            if(c is SphereCollider sphere)builder.Append(sphere.center.ToString("R")).Append(sphere.radius.ToString("R"));
            if(c is CapsuleCollider capsule)builder.Append(capsule.center.ToString("R")).Append(capsule.radius.ToString("R")).Append(capsule.height.ToString("R")).Append(capsule.direction);
            if(c is MeshCollider mesh)builder.Append(AssetDatabase.GetAssetPath(mesh.sharedMesh)).Append(mesh.convex);
        }
        return Sha(builder.ToString());
    }
    static string Signature(Transform t) {
        var f=t.GetComponent<MeshFilter>();var r=t.GetComponent<Renderer>();
        return Sha(P(t)+t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R")+(f?AssetDatabase.GetAssetPath(f.sharedMesh):"")+(r?string.Join("|",r.sharedMaterials.Select(AssetDatabase.GetAssetPath)):"")+string.Join("|",t.GetComponents<Collider>().Select(c=>c.GetType().Name+":"+c.enabled+":"+c.isTrigger+(c is BoxCollider box?box.center.ToString("R")+box.size.ToString("R"):c is MeshCollider mesh?AssetDatabase.GetAssetPath(mesh.sharedMesh):c is SphereCollider sphere?sphere.center.ToString("R")+sphere.radius.ToString("R"):""))));
    }
    static DecorationPlan CreatePlan(Scene[] scenes) {
        var all=scenes.SelectMany(All).ToArray();var roots=new Dictionary<Transform,string>();
        foreach(var t in all) {
            if(!t.GetComponent<MeshFilter>()&&!t.GetComponent<LODGroup>())continue;
            string cat=Category(t);if(cat=="")continue;var root=AtomicRoot(t);
            if(KeepReason(root)!="")continue;
            if(!roots.ContainsKey(root))roots.Add(root,cat);
        }
        var originalRoots=new HashSet<Transform>(roots.Keys);
        foreach(var root in originalRoots)for(var parent=root.parent;parent;parent=parent.parent)if(originalRoots.Contains(parent)){roots.Remove(root);break;}
        var furniture=roots.Keys.Where(t=>FurnitureName(t.name)).ToArray();
        var seats=new List<SeatBinding>();var needed=new HashSet<Transform>();
        foreach(var anchor in all.Select(t=>t.GetComponent<WaypointAnchor>()).Where(a=>a&&(a.Category==ResidentActivityType.SeasideBench||a.Category==ResidentActivityType.ParkBench||a.Category==ResidentActivityType.BeachSunbathe))) {
            bool beach=anchor.Category==ResidentActivityType.BeachSunbathe;
            var compatible=furniture.Where(f=>beach?Regex.IsMatch(f.name,"Sunbed|BeachTowel",RegexOptions.IgnoreCase):f.name.Contains("Bench")).ToArray();
            var nearest=compatible.OrderBy(t=>Vector2.SqrMagnitude(new Vector2(t.position.x-anchor.transform.position.x,t.position.z-anchor.transform.position.z))).FirstOrDefault();
            if(!nearest)throw new InvalidOperationException("No visual furniture for "+P(anchor.transform));
            float distance=Vector2.Distance(new Vector2(nearest.position.x,nearest.position.z),new Vector2(anchor.transform.position.x,anchor.transform.position.z));
            if(distance>1.6f)throw new InvalidOperationException("Furniture binding too distant: "+P(anchor.transform)+" "+distance);
            needed.Add(nearest);seats.Add(new SeatBinding{anchor=P(anchor.transform),anchorId=Id(anchor.transform),furniture=P(nearest),furnitureId=Id(nearest),category=anchor.Category.ToString(),anchorPosition=anchor.transform.position,modelPosition=nearest.position,distance=distance});
        }
        // Keep each sunbed: these authored models are the resident relaxation
        // furniture family and can acquire schedules during later map edits.
        foreach(var t in furniture.Where(t=>t.name.StartsWith("Sunbed")))needed.Add(t);
        var rows=roots.Select(pair=>new Decoration{scene=pair.Key.gameObject.scene.path,path=P(pair.Key),globalId=Id(pair.Key),category=pair.Value,action=needed.Contains(pair.Key)?"PRESERVE_NPC_FURNITURE":"REMOVE_AFTER_EXPORT",reason=needed.Contains(pair.Key)?"resident_seat_or_relaxation_model":"island_decoration",key=VariantKey(pair.Key),position=pair.Key.position,rotation=pair.Key.rotation,scale=pair.Key.lossyScale,meshes=pair.Key.GetComponentsInChildren<MeshFilter>(true).Count(f=>f.sharedMesh),renderers=pair.Key.GetComponentsInChildren<Renderer>(true).Length}).OrderBy(r=>r.scene).ThenBy(r=>r.path).ToArray();
        var selected=new HashSet<Transform>(roots.Keys);
        bool InsideSelection(Transform t){for(var p=t;p;p=p.parent)if(selected.Contains(p))return true;return false;}
        var protectedRows=all.Where(t=>KeepReason(t)!=""&&!InsideSelection(t)).Select(t=>new Protection{scene=t.gameObject.scene.path,path=P(t),globalId=Id(t),signature=Signature(t),reason=KeepReason(t)}).ToArray();
        var unknown=all.Where(t=>t.gameObject.scene.name=="100_Dressing_City_01"&&t.GetComponent<MeshFilter>()&&!InsideSelection(t)&&KeepReason(t)=="").Select(t=>P(t)).ToArray();
        return new DecorationPlan{utc=DateTime.UtcNow.ToString("o"),stage="PLAN_BEFORE_ANY_REMOVAL",decorations=rows,seats=seats.ToArray(),protectedObjects=protectedRows,unknownMeshes=unknown,terrains=all.Select(t=>t.GetComponent<Terrain>()).Where(t=>t).Select(DescribeTerrain).ToArray(),rootsToRemove=rows.Count(r=>r.action=="REMOVE_AFTER_EXPORT"),preservedFurniture=needed.Count,uniqueVariants=rows.Select(r=>r.key).Distinct().Count()};
    }
    static TerrainRecord DescribeTerrain(Terrain t) {
        int count=0;var d=t.terrainData;
        for(int layer=0;layer<d.detailPrototypes.Length;layer++)foreach(int n in d.GetDetailLayer(0,0,d.detailWidth,d.detailHeight,layer))count+=n;
        return new TerrainRecord{name=t.name,trees=d.treeInstanceCount,detailPrototypes=d.detailPrototypes.Length,detailInstances=count};
    }
    static void SavePlan(DecorationPlan plan,string file)=>File.WriteAllText(Path.Combine(Reports,file),JsonUtility.ToJson(plan,true));
    public static void BuildDecorationPlan() {
        var scenes=LoadIsland();var plan=CreatePlan(scenes);SavePlan(plan,"island-decoration-plan.json");
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);Debug.Log("ISLAND_DECORATION_PLAN_READY "+plan.rootsToRemove+" remove, "+plan.uniqueVariants+" variants");
    }
    public static void ExportDecorations() {
        var scenes=LoadIsland();var plan=CreatePlan(scenes);SavePlan(plan,"island-decoration-plan.json");
        Directory.CreateDirectory(LibraryRoot);AssetDatabase.Refresh();
        var representative=new Dictionary<string,string>();int count=0;
        foreach(var row in plan.decorations) {
            if(!representative.TryGetValue(row.key,out string path)) {
                var source=Resolve<Transform>(row.globalId);if(!source)throw new Exception("Missing planned model: "+row.path);
                string folder=LibraryRoot+"/"+row.category;Directory.CreateDirectory(folder);
                string name=Regex.Replace(Regex.Replace(source.name,@" \(\d+\)$",""),@"[^\p{L}\p{Nd}_-]","_");
                path=folder+"/"+name+"_"+row.key.Substring(0,12)+".prefab";
                if(!File.Exists(path)) {
                    var copy=UnityEngine.Object.Instantiate(source.gameObject);copy.name=name;
                    copy.transform.SetParent(null);copy.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);copy.transform.localScale=source.lossyScale;
                    foreach(var b in copy.GetComponentsInChildren<MonoBehaviour>(true).Reverse())if(b)UnityEngine.Object.DestroyImmediate(b);
                    foreach(var body in copy.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(body);
                    copy.SetActive(true);foreach(var renderer in copy.GetComponentsInChildren<Renderer>(true))renderer.forceRenderingOff=false;
                    foreach(var lod in copy.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(-1);
                    PrefabUtility.SaveAsPrefabAsset(copy,path,out bool success);UnityEngine.Object.DestroyImmediate(copy);
                    if(!success)throw new IOException("Prefab save failed: "+path);
                }
                representative.Add(row.key,path);count++;
                if(count%40==0)File.WriteAllText(Path.Combine(Reports,"island-decoration-export-progress.txt"),count+" / "+plan.uniqueVariants);
            }
            row.prefab=path;
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(var path in representative.Values) {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!prefab||prefab.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))!=0||prefab.GetComponentsInChildren<MeshFilter>(true).Any(f=>!f.sharedMesh))throw new Exception("Invalid library prefab: "+path);
        }
        plan.stage="EXPORTED_AND_REOPENED_BEFORE_REMOVAL";SavePlan(plan,"island-decoration-export.json");
        var csv=new StringBuilder("category,prefab,source_scene,source_path,action\n");
        foreach(var row in plan.decorations)csv.AppendLine(string.Join(",",new[]{row.category,row.prefab,row.scene,row.path,row.action}.Select(x=>"\""+x.Replace("\"","\"\"")+"\"")));
        File.WriteAllText(Path.Combine(Reports,"island-decoration-catalog.csv"),csv.ToString(),new UTF8Encoding(true));
        File.WriteAllText(LibraryRoot+"/README.txt","岛屿装饰资源库\n按类别拖入场景即可使用。每个预制体保留原模型、材质、LOD与碰撞，已移除场景玩法脚本和刚体。根位置与旋转归零，比例沿用代表实例；每个原始实例的世界位置、旋转和比例、导出路径记录于 reports/island-decoration-export.json。材质、网格为工程原资源依赖，不删除源模型。NPC 使用的家具只导出，清理时保留。\n");
        AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);Debug.Log("ISLAND_DECORATION_EXPORT_VERIFIED "+count);
    }
    [Serializable] public class PoleRepair {public string path,mesh;public float oldBottom,newBottom,ground,top;}
    [Serializable] public class SeatRepair {public string path,furniture;public Vector3 before,after;}
    [Serializable] public class RevisionResult {public string utc,stage,navMesh;public int removedRoots,removedTransforms,prefabs,terrainTrees,terrainDetailInstances;public PoleRepair[] poles;public SeatRepair[] seats;public string[] protectionFailures,remainingDecorations;public float waterY,deckMinY,deckTopY;}
    static float LandHeight(Terrain[] lands,Vector3 p) {
        var valid=lands.Where(t=>p.x>=t.transform.position.x&&p.z>=t.transform.position.z&&p.x<=t.transform.position.x+t.terrainData.size.x&&p.z<=t.transform.position.z+t.terrainData.size.z).ToArray();
        if(valid.Length==0)throw new Exception("No seabed under "+p);return valid.Max(t=>t.SampleHeight(p)+t.transform.position.y);
    }
    public static void ApplyRevision() {
        if(File.Exists(Path.Combine(Reports,"restaurant-coast-source-repair.json")))throw new InvalidOperationException("This historical removal/support-extension pass is superseded by the original-coast alignment repair; do not replay it on the corrected island");
        revisionObjects=null;revisionIds=null;
        ApplyProgress("CHECK_BACKUP_AND_TERRAIN_EXPORT");
        if(!File.Exists(Path.Combine(Reports,"island-revision-backup.json")))throw new Exception("Revision backup required");
        ExportTerrainDetails();
        var exported=JsonUtility.FromJson<DecorationPlan>(File.ReadAllText(Path.Combine(Reports,"island-decoration-export.json")));
        if(exported.stage!="EXPORTED_AND_REOPENED_BEFORE_REMOVAL")throw new Exception("Verified prefab export required before removal");
        var scenes=LoadIsland();var all=scenes.SelectMany(All).ToArray();var result=new RevisionResult{utc=DateTime.UtcNow.ToString("o"),stage="STARTED",prefabs=exported.uniqueVariants};
        ApplyProgress("INDEX_OBJECTS "+all.Length);IndexRevisionObjects(all);
        ApplyProgress("CHECK_EXPORTED_PREFABS_AND_PROTECTED_BASELINE");
        foreach(var row in exported.decorations)if(!AssetDatabase.LoadAssetAtPath<GameObject>(row.prefab))throw new Exception("Prefab missing; no scene objects have been removed: "+row.prefab);
        foreach(var row in exported.protectedObjects) {var t=Resolve<Transform>(row.globalId);if(!t||Signature(t)!=row.signature)throw new Exception("Protected baseline changed: "+row.path);}
        var poles=new List<PoleRepair>();var seatRepairs=new List<SeatRepair>();var lands=All(scenes[0]).Select(t=>t.GetComponent<Terrain>()).Where(t=>t).ToArray();
        var poleFilters=all.Where(t=>P(t).StartsWith("---Restaurant/Restaurant/RestaurantStatic/Poles/")).Select(t=>t.GetComponent<MeshFilter>()).Where(f=>f&&f.sharedMesh).ToArray();
        string meshFolder=LibraryRoot+"/RepairedRestaurantSupports";Directory.CreateDirectory(meshFolder);AssetDatabase.Refresh();
        ApplyProgress("REPAIR_RESTAURANT_SUPPORTS");
        foreach(var filter in poleFilters) {
            var t=filter.transform;var b=IslandComprehensiveRepair.BoundsOf(t);float ground=LandHeight(lands,b.center);
            float bottom=ground-.15f;
            if(b.min.y<=ground-.06f)continue;
            var original=filter.sharedMesh;var mesh=UnityEngine.Object.Instantiate(original);var vertices=mesh.vertices;
            for(int i=0;i<vertices.Length;i++) {var world=t.TransformPoint(vertices[i]);float ratio=Mathf.Clamp01((world.y-b.min.y)/b.size.y);world.y=Mathf.Lerp(bottom,b.max.y,ratio);vertices[i]=t.InverseTransformPoint(world);}
            mesh.vertices=vertices;mesh.RecalculateBounds();mesh.RecalculateNormals();mesh.RecalculateTangents();
            var path=meshFolder+"/"+t.name.Replace(" ","_")+"_"+Sha(Id(t)).Substring(0,12)+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh);}
            else AssetDatabase.CreateAsset(mesh,path);
            filter.sharedMesh=mesh;
            foreach(var collider in t.GetComponents<MeshCollider>())if(collider.sharedMesh==original)collider.sharedMesh=mesh;
            poles.Add(new PoleRepair{path=P(t),mesh=path,oldBottom=b.min.y,newBottom=bottom,top=b.max.y,ground=ground});
        }
        ApplyProgress("REPAIR_NPC_FURNITURE_BINDINGS");
        foreach(var binding in exported.seats) {
            var anchor=Resolve<Transform>(binding.anchorId);var model=Resolve<Transform>(binding.furnitureId);
            if(!anchor||!model)throw new Exception("Missing protected furniture binding");
            var before=anchor.position;var after=before;
            if(binding.category!="BeachSunbathe")after.y=model.position.y+.05f;
            anchor.position=after;
            var data=new SerializedObject(anchor.GetComponent<WaypointAnchor>());data.FindProperty("_autoSnapInEditor").boolValue=false;
            data.FindProperty("_useAuthoredPose").boolValue=true;data.FindProperty("_sampleRadius").floatValue=1.5f;data.ApplyModifiedPropertiesWithoutUndo();
            seatRepairs.Add(new SeatRepair{path=P(anchor),furniture=P(model),before=before,after=after});
        }
        ApplyProgress("REMOVE_EXPORTED_DECORATIONS");
        foreach(var row in exported.decorations.Where(r=>r.action=="REMOVE_AFTER_EXPORT")) {
            var t=Resolve<Transform>(row.globalId);if(!t)throw new Exception("Planned decoration disappeared: "+row.path);
            result.removedTransforms+=t.GetComponentsInChildren<Transform>(true).Length;UnityEngine.Object.DestroyImmediate(t.gameObject);result.removedRoots++;
            if(result.removedRoots%1000==0)ApplyProgress("REMOVED "+result.removedRoots+" / "+exported.rootsToRemove);
        }
        foreach(var land in lands) {
            // Only city terrain prototypes are in scope; terrain height/texture
            // and the offshore gameplay scenes are not modified by this pass.
            result.terrainTrees+=land.terrainData.treeInstanceCount;
            if(land.terrainData.treeInstanceCount>0)throw new Exception("Unexported terrain trees detected; manual prototype export required");
        }
        var details=JsonUtility.FromJson<TerrainDetailArchive>(File.ReadAllText(Path.Combine(Reports,"island-terrain-details-export.json")));
        if(details.stage!="EXPORTED_AND_VERIFIED")throw new Exception("Terrain grass resource export required");
        result.prefabs+=details.layers.Select(l=>l.prefab).Distinct().Count();
        foreach(var entry in details.layers) {
            var land=lands.Single(t=>t.name==entry.terrain);var data=land.terrainData;
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefab))throw new Exception("Missing terrain detail prefab");
            string heights=HeightHash(data);
            data.SetDetailLayer(0,0,entry.layer,new int[data.detailHeight,data.detailWidth]);EditorUtility.SetDirty(data);
            if(HeightHash(data)!=heights)throw new Exception("Terrain height changed while clearing grass");
            result.terrainDetailInstances+=entry.instances;
        }
        ApplyProgress("CHECK_PROTECTION_AND_CLEAN_REFERENCES");
        var failures=new List<string>();foreach(var row in exported.protectedObjects) {var t=Resolve<Transform>(row.globalId);if(!t||Signature(t)!=row.signature)failures.Add(row.path);}
        result.protectionFailures=failures.ToArray();if(failures.Count>0)throw new Exception("Protected infrastructure changed");
        foreach(var scene in scenes) {
            foreach(var lod in All(scene).Select(t=>t.GetComponent<LODGroup>()).Where(l=>l)) {
                var levels=lod.GetLODs();bool changed=false;
                for(int i=0;i<levels.Length;i++)if(levels[i].renderers.Any(r=>!r)){levels[i].renderers=levels[i].renderers.Where(r=>r).ToArray();changed=true;}
                if(changed){lod.SetLODs(levels);lod.RecalculateBounds();}
            }
            // Remove only deleted references from render activation lists.
            foreach(var group in All(scene).Select(t=>t.GetComponent<DressingActivationGroup>()).Where(g=>g)) {
                var state=new SerializedObject(group);var it=state.GetIterator();while(it.NextVisible(true))if(it.isArray&&it.propertyType!=SerializedPropertyType.String) {
                    for(int i=it.arraySize-1;i>=0;i--) {var child=it.GetArrayElementAtIndex(i);if(child.propertyType==SerializedPropertyType.ObjectReference&&child.objectReferenceValue==null){it.DeleteArrayElementAtIndex(i);}}
                }state.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        var activation=scenes.SelectMany(All).ToDictionary(t=>t.gameObject,t=>t.gameObject.activeSelf);
        foreach(var root in scenes.SelectMany(All).Select(t=>t.GetComponent<DressingSceneRoot>()).Where(r=>r))root.ActivateAllImmediate();
        var surface=All(scenes[0]).Select(t=>t.GetComponent<NavMeshSurface>()).First(t=>t&&P(t.transform)=="---AI/ResidentManager/NavMesh Surface");
        ApplyProgress("BAKE_RESIDENT_NAVIGATION");
        surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();
        string nav="Assets/SushiAndFish/Modules/OriginalIsland/TerrainData/TownIsland_DecorationCleared_20261018.asset";
        nav=AssetDatabase.GenerateUniqueAssetPath(nav);AssetDatabase.CreateAsset(surface.navMeshData,nav);result.navMesh=nav;
        foreach(var pair in activation)if(pair.Key&&pair.Key.activeSelf!=pair.Value)pair.Key.SetActive(pair.Value);
        var water=all.First(t=>t&&t.GetComponent<StylizedWater3.OceanFollowBehaviour>());result.waterY=water.position.y;
        var deck=all.First(t=>t&&P(t)=="---Restaurant/Restaurant/RestaurantStatic/Ground");var deckBounds=IslandComprehensiveRepair.BoundsOf(deck);result.deckMinY=deckBounds.min.y;result.deckTopY=deckBounds.max.y;
        if(result.deckMinY<result.waterY+.5f)throw new Exception("Restaurant deck is submerged");
        result.poles=poles.ToArray();result.seats=seatRepairs.ToArray();result.remainingDecorations=exported.decorations.Where(d=>d.action=="REMOVE_AFTER_EXPORT"&&Resolve<Transform>(d.globalId)).Select(d=>d.path).ToArray();
        if(result.remainingDecorations.Length>0)throw new Exception("Planned decorative roots survived");
        ApplyProgress("SAVE_SCENES");
        foreach(var scene in scenes)EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        result.stage="SAVED_NEEDS_RUNTIME_VERIFICATION";File.WriteAllText(Path.Combine(Reports,"island-revision-applied.json"),JsonUtility.ToJson(result,true));
        revisionObjects=null;revisionIds=null;
        ApplyProgress("REFRESH_MENU_PRESENTATION");
        IslandFrontEndBuilder.RefreshMenuPresentation();EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);Debug.Log("ISLAND_REVISION_SAVED "+result.removedRoots);
        ApplyProgress("SAVED_NEEDS_RUNTIME_VERIFICATION");
    }
}
