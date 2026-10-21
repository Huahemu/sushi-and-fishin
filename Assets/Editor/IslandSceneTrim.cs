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
using PaperPixelGames.Core.DressingSceneSystem;

// Bounded scene edit for the human's Oct 16 request. Original source assets
// remain available; terrain edits use dedicated copies in the island module.
public static class IslandSceneTrim
{
    [Serializable] public class Grid { public float x0,z0,step; public int width,height; }
    [Serializable] public class Removed { public string scene,path,reason; public int objects,renderers,colliders; }
    [Serializable] public class Land { public string path,originalData,workingData; public int modifiedSamples,retainedIslandSamples; public bool retainedIslandHeightsExact; }
    [Serializable] public class Store { public string path,before,after; public int objects,renderers,colliders; public bool unchanged; }
    [Serializable] public class Audit { public string utc,stage; public Removed[] removed; public Land[] terrains; public Store[] stores; public int clearedDeletedReferences,missingScripts; public string[] survivingDecorativeBuildings; public bool scenesReopened; }
    static string Reports=>IslandFrontEndInventory.Reports;
    static Grid grid;static float[] distance;
    static readonly List<Removed> removed=new List<Removed>();
    static readonly List<Land> lands=new List<Land>();
    static readonly List<Store> stores=new List<Store>();
    static readonly HashSet<int> deletedIds=new HashSet<int>();
    static readonly Regex Building=new Regex(@"^(SM_(house_residential_(building|attachment)|shop_building|store_rowbuilding)|Building_)",RegexOptions.IgnoreCase);
    static readonly Regex WallAttachment=new Regex(@"^SM_(antenna|ventilation|fusebox_wall|cables_hanging|sunscreen_shop|signboard|sticker_store|entrance_edge)",RegexOptions.IgnoreCase);
    static readonly string[] CityScenes={"100_Dressing_City_01","110_Dressing_City_Folliage_01","120_Dressing_City_Grass_01","130_Dressing_City_Rocks_01"};
    static IEnumerable<Transform> All(Scene s)=>s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true));
    static Transform Find(Scene s,string path)=>All(s).FirstOrDefault(t=>IslandFrontEndInventory.PathOf(t)==path);
    static float Distance(Vector3 point)
    {
        int x=Mathf.RoundToInt((point.x-grid.x0)/grid.step),z=Mathf.RoundToInt((point.z-grid.z0)/grid.step);
        if(x<0||z<0||x>=grid.width||z>=grid.height)return 10000;
        return distance[z*grid.width+x];
    }
    static void Remove(Transform t,string reason)
    {
        if(!t)return;
        removed.Add(new Removed{scene=t.gameObject.scene.name,path=IslandFrontEndInventory.PathOf(t),reason=reason,objects=t.GetComponentsInChildren<Transform>(true).Length,renderers=t.GetComponentsInChildren<Renderer>(true).Length,colliders=t.GetComponentsInChildren<Collider>(true).Length});
        foreach(var c in t.GetComponentsInChildren<Component>(true))if(c)deletedIds.Add(c.GetInstanceID());
        foreach(var child in t.GetComponentsInChildren<Transform>(true))deletedIds.Add(child.gameObject.GetInstanceID());
        UnityEngine.Object.DestroyImmediate(t.gameObject);
    }
    static Vector3 VisualPoint(Renderer r)
    {
        if(r.TryGetComponent<MeshFilter>(out var f)&&f.sharedMesh)return f.transform.TransformPoint(f.sharedMesh.bounds.center);
        return r.bounds.center;
    }
    static void Crop(Transform t)
    {
        if(!t)return;
        if(t.name=="GabeMarket"||t.name=="MarinaMarket")return;
        var renderers=t.GetComponentsInChildren<Renderer>(true);
        if(renderers.Length>0 && renderers.All(r=>Distance(VisualPoint(r))>18f)) {Remove(t,"outside_retained_town_island");return;}
        if(renderers.Length==0 && t.GetComponentsInChildren<Collider>(true).Length>0 && Distance(t.position)>18f) {Remove(t,"collider_or_trigger_on_removed_land");return;}
        foreach(var child in t.Cast<Transform>().ToArray())Crop(child);
    }
    static string Fingerprint(Transform t)
    {
        var b=new StringBuilder();
        foreach(var c in t.GetComponentsInChildren<Component>(true))if(c)b.Append(c.GetType().FullName).Append('|').Append(EditorJsonUtility.ToJson(c)).Append('\n');
        using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(b.ToString()))).Replace("-","");
    }
    static Store Record(Transform t)
    {
        if(!t)throw new InvalidOperationException("Required original store was absent");
        var row=new Store{path=IslandFrontEndInventory.PathOf(t),before=Fingerprint(t),objects=t.GetComponentsInChildren<Transform>(true).Length,renderers=t.GetComponentsInChildren<Renderer>(true).Length,colliders=t.GetComponentsInChildren<Collider>(true).Length};stores.Add(row);return row;
    }
    static int ClearReferences(Scene scene)
    {
        int count=0;
        foreach(var c in All(scene).SelectMany(t=>t.GetComponents<Component>()).Where(c=>c))
        {
            var state=new SerializedObject(c);var p=state.GetIterator();bool changed=false;
            while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference && deletedIds.Contains(p.objectReferenceInstanceIDValue))
            {p.objectReferenceValue=null;changed=true;count++;}
            if(changed)state.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(var root in All(scene).Select(t=>t.GetComponent<DressingSceneRoot>()).Where(r=>r))
        {
            var remaining=root.Groups.Where(g=>g).ToArray();var state=new SerializedObject(root);var groups=state.FindProperty("_groups");groups.arraySize=remaining.Length;
            for(int i=0;i<remaining.Length;i++)groups.GetArrayElementAtIndex(i).objectReferenceValue=remaining[i];
            state.ApplyModifiedPropertiesWithoutUndo();
        }
        return count;
    }
    static void TrimTerrain(Scene scene)
    {
        const string folder="Assets/SushiAndFish/Modules/OriginalIsland/TerrainData";
        Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        foreach(var t in All(scene).Select(t=>t.GetComponent<Terrain>()).Where(t=>t).ToArray())
        {
            var original=t.terrainData;int n=original.heightmapResolution;var h=original.GetHeights(0,0,n,n);var baseline=(float[,])h.Clone();bool hasTown=false;int retained=0,changed=0;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                var world=t.transform.position+new Vector3(x*original.size.x/(n-1),h[z,x]*original.size.y,z*original.size.z/(n-1));
                float d=Distance(world);if(d==0){hasTown=true;retained++;}
                if(d<=8)continue;
                float ceiling=Mathf.Max(t.transform.position.y,-4f-(d-8f)*2f);
                if(world.y>ceiling){h[z,x]=Mathf.Max(0,(ceiling-t.transform.position.y)/original.size.y);changed++;}
            }
            if(!hasTown){Remove(t.transform,"terrain_tile_without_town_island");continue;}
            string source=AssetDatabase.GetAssetPath(original),dest=source;
            if(changed>0)
            {
                dest=folder+"/TownIsland_"+Path.GetFileName(source);
                if(AssetDatabase.LoadAssetAtPath<TerrainData>(dest))throw new InvalidOperationException("Refusing to overwrite an existing trim terrain: "+dest);
                if(!AssetDatabase.CopyAsset(source,dest))throw new IOException("Cannot copy terrain data");
                var data=AssetDatabase.LoadAssetAtPath<TerrainData>(dest);data.SetHeights(0,0,h);EditorUtility.SetDirty(data);t.terrainData=data;
                if(t.TryGetComponent<TerrainCollider>(out var collider))collider.terrainData=data;
            }
            bool exact=true;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {var p=t.transform.position+new Vector3(x*original.size.x/(n-1),0,z*original.size.z/(n-1));if(Distance(p)==0&&h[z,x]!=baseline[z,x])exact=false;}
            lands.Add(new Land{path=IslandFrontEndInventory.PathOf(t.transform),originalData=source,workingData=dest,modifiedSamples=changed,retainedIslandSamples=retained,retainedIslandHeightsExact=exact});
        }
    }
    public static void FinalizeMetadata()
    {
        var module=AssetDatabase.LoadAssetAtPath<SushiAndFish.FrontEnd.SceneModuleDefinition>("Assets/SushiAndFish/Modules/OriginalIsland/OriginalIslandModule.asset");
        if(!module)throw new InvalidOperationException("Island module definition absent");
        module.displayName="城镇岛（两间商店与寿司店）";
        module.description="仅保留隧道前的城镇岛；跨海桥、隧道、后方山体及其他装饰建筑已从场景移除。保留原版 GabeMarket、MarinaMarket、Restaurant 的完整对象与依赖，海域布景场景继续按原逻辑加载。";
        EditorUtility.SetDirty(module);AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine(Reports,"island-trim-metadata-finalized.txt"),DateTime.UtcNow.ToString("o"));
    }
    [MenuItem("Tools/Sushi & Fish/Apply backed up town island trim")]
    public static void Run()
    {
        if(File.Exists(Path.Combine(Reports,"restaurant-coast-source-repair.json")))throw new InvalidOperationException("The historical trim is superseded: preserve the restored restaurant shore and regenerate a source-sea-level mask before planning any new trim");
        if(!File.Exists(Path.Combine(Reports,"island-trim-backup-path.txt")))throw new InvalidOperationException("No before-edit scene backup");
        grid=JsonUtility.FromJson<Grid>(File.ReadAllText(Path.Combine(Reports,"island-trim-distance-grid.json")));
        var bytes=File.ReadAllBytes(Path.Combine(Reports,"island-trim-distance-grid.bytes"));distance=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,distance,0,bytes.Length);
        var game=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene,OpenSceneMode.Single);int cleared=0;
        var restaurant=Find(game,"---Restaurant");var restaurantRecord=Record(restaurant);
        TrimTerrain(game);
        Remove(Find(game,"---World/BossParent"),"rear_tunnel_area");
        Remove(Find(game,"---World/LightHouseDoor"),"removed_lighthouse");
        restaurantRecord.after=Fingerprint(restaurant);restaurantRecord.unchanged=restaurantRecord.before==restaurantRecord.after;
        if(!restaurantRecord.unchanged)throw new InvalidOperationException("Restaurant subtree changed unexpectedly");
        cleared+=ClearReferences(game);EditorSceneManager.SaveScene(game);AssetDatabase.SaveAssets();
        foreach(var name in CityScenes)
        {
            var scene=EditorSceneManager.OpenScene(IslandFrontEndBuilder.DressingRoot+"/"+name+".unity",OpenSceneMode.Single);
            if(name==CityScenes[0])
            {
                var gabe=Find(scene,"Root/Comps/GabeMarket");var marina=Find(scene,"Root/Comps/MarinaMarket");var a=Record(gabe);var b=Record(marina);
                Remove(Find(scene,"Root/LighthouseComp"),"decorative_lighthouse");
                Remove(Find(scene,"Root/TunnelComp"),"tunnel_on_removed_land");
                Remove(Find(scene,"Root/CompBridge"),"bridge_to_removed_land");
                Remove(Find(scene,"Root/City_Park_Forest/Gazebo"),"decorative_gazebo");
                Remove(Find(scene,"Root/BambooPark/Gazebo"),"decorative_gazebo");
                foreach(var t in All(scene).Where(t=>Building.IsMatch(t.name)||WallAttachment.IsMatch(t.name)).ToArray())
                {
                    if(!t)continue;var path=IslandFrontEndInventory.PathOf(t);
                    if(path.StartsWith("Root/Comps/GabeMarket/",StringComparison.Ordinal)||path.StartsWith("Root/Comps/MarinaMarket/",StringComparison.Ordinal))continue;
                    Remove(t,Building.IsMatch(t.name)?"decorative_building":"attachment_to_decorative_building");
                }
                Crop(Find(scene,"Root"));
                a.after=Fingerprint(gabe);a.unchanged=a.before==a.after;b.after=Fingerprint(marina);b.unchanged=b.before==b.after;
                if(!a.unchanged||!b.unchanged)throw new InvalidOperationException("Original market subtree changed unexpectedly");
            }
            else Crop(Find(scene,"Root"));
            cleared+=ClearReferences(scene);EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();IslandFrontEndBuilder.RefreshMenuPresentation();
        int missing=0;var survivors=new List<string>();
        foreach(var path in new[]{IslandFrontEndBuilder.GameScene}.Concat(CityScenes.Select(n=>IslandFrontEndBuilder.DressingRoot+"/"+n+".unity")))
        {
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            foreach(var t in All(scene))
            {
                missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                var full=IslandFrontEndInventory.PathOf(t);
                if((Building.IsMatch(t.name)||t.name=="Gazebo"||t.name=="LighthouseComp")&&!full.StartsWith("---Restaurant/")&&!full.StartsWith("Root/Comps/GabeMarket/")&&!full.StartsWith("Root/Comps/MarinaMarket/"))survivors.Add(scene.name+":"+full);
            }
        }
        var audit=new Audit{utc=DateTime.UtcNow.ToString("o"),stage="SAVED_AND_REOPENED",removed=removed.ToArray(),terrains=lands.ToArray(),stores=stores.ToArray(),clearedDeletedReferences=cleared,missingScripts=missing,survivingDecorativeBuildings=survivors.ToArray(),scenesReopened=true};
        File.WriteAllText(Path.Combine(Reports,"island-trim-edit-audit.json"),JsonUtility.ToJson(audit,true));
        if(missing>0||survivors.Count>0||lands.Any(l=>!l.retainedIslandHeightsExact))throw new InvalidOperationException("Island trim audit failed");
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);Debug.Log("ISLAND_TRIM_SAVED_AND_REOPENED");EditorApplication.Exit(0);
    }
}
