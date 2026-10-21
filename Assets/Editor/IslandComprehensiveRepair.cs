using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PaperPixelGames.Core.DressingSceneSystem;

public static class IslandComprehensiveRepair
{
    [Serializable] public class Item { public string scene,path,mesh; public Vector3 position,min,max; public float terrain,ground,gap; public string support; public bool active; }
    [Serializable] public class Report { public string utc,stage; public Item[] current,deleted; public string[] groups; }
    public static string Reports=>IslandFrontEndInventory.Reports;
    static IEnumerable<Transform> All(Scene s)=>IslandWorldIssues.All(s);
    static string P(Transform t)=>IslandFrontEndInventory.PathOf(t);
    public static Scene[] Load()
    {
        var list=new List<Scene>{EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene,OpenSceneMode.Single)};
        foreach(var path in Directory.GetFiles(IslandFrontEndBuilder.DressingRoot,"*.unity").OrderBy(p=>p))list.Add(EditorSceneManager.OpenScene(path,OpenSceneMode.Additive));
        foreach(var root in list.SelectMany(All).Select(t=>t.GetComponent<DressingSceneRoot>()).Where(r=>r))root.ActivateAllImmediate();
        foreach(var lod in list.SelectMany(All).Select(t=>t.GetComponent<LODGroup>()).Where(r=>r&&r.enabled&&r.gameObject.activeInHierarchy))lod.ForceLOD(0);
        foreach(var r in list.SelectMany(All).Select(t=>t.GetComponent<Renderer>()).Where(r=>r))r.forceRenderingOff=false;
        Physics.SyncTransforms();return list.ToArray();
    }
    public static Bounds BoundsOf(Transform t)
    {
        bool first=true;var b=new Bounds(t.position,Vector3.zero);
        foreach(var f in t.GetComponentsInChildren<MeshFilter>(true))
        {if(!f.sharedMesh||f.name.Contains("Impostor"))continue;var m=f.sharedMesh.bounds;for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){var p=f.transform.TransformPoint(m.center+Vector3.Scale(m.extents,new Vector3(x,y,z)));if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);}}
        return b;
    }
    static Item Describe(Transform t,Terrain[] terrains)
    {
        var b=BoundsOf(t);float ground=float.NegativeInfinity;string support="";
        foreach(var land in terrains){var p=land.transform.position;var s=land.terrainData.size;if(b.center.x>=p.x&&b.center.x<=p.x+s.x&&b.center.z>=p.z&&b.center.z<=p.z+s.z)ground=Mathf.Max(ground,land.SampleHeight(b.center)+p.y);}
        float terrain=ground;
        foreach(var hit in Physics.RaycastAll(new Vector3(b.center.x,b.min.y+.12f,b.center.z),Vector3.down,1000,~0,QueryTriggerInteraction.Ignore))
        {if(hit.collider.transform==t||hit.collider.transform.IsChildOf(t)||hit.normal.y<.5f)continue;if(hit.point.y>ground){ground=hit.point.y;support=P(hit.collider.transform);}}
        var f=t.GetComponent<MeshFilter>();return new Item{scene=t.gameObject.scene.name,path=P(t),mesh=f&&f.sharedMesh?AssetDatabase.GetAssetPath(f.sharedMesh):"",position=t.position,min=b.min,max=b.max,terrain=terrain,ground=ground,gap=b.min.y-ground,support=support,active=t.gameObject.activeInHierarchy};
    }
    public static void Inspect()
    {
        var scenes=Load();var terrains=scenes.SelectMany(All).Select(t=>t.GetComponent<Terrain>()).Where(t=>t).ToArray();
        var city=scenes.Single(s=>s.name=="100_Dressing_City_01");
        var current=scenes.SelectMany(All).Where(t=>t.GetComponent<MeshFilter>()&&!t.GetComponentInParent<Canvas>()&&!t.name.Contains("Impostor")).Select(t=>Describe(t,terrains)).ToArray();
        var groups=All(city).Where(t=>P(t).StartsWith("Root/Comps/")&&P(t).Count(c=>c=='/')==2).Select(t=>P(t)+" | "+t.childCount).ToArray();
        string backup=Path.GetFullPath(Path.Combine(Reports,"../backup/island-before-city-trim-20261016-095412"));
        const string temp="Assets/Editor/ComprehensiveSourceCity.unity";
        if(!File.Exists(temp))File.Copy(Path.Combine(backup,IslandFrontEndBuilder.DressingRoot,"100_Dressing_City_01.unity"),temp);
        AssetDatabase.ImportAsset(temp,ImportAssetOptions.ForceSynchronousImport);
        var paths=new HashSet<string>(All(city).Select(P));
        var source=EditorSceneManager.OpenScene(temp,OpenSceneMode.Additive);
        var deleted=All(source).Where(t=>t.GetComponent<MeshFilter>()&&!paths.Contains(P(t))).Select(t=>Describe(t,terrains)).ToArray();
        File.WriteAllText(Path.Combine(Reports,"comprehensive-inventory.json"),JsonUtility.ToJson(new Report{utc=DateTime.UtcNow.ToString("o"),stage="ALL_SCENES_ALL_GROUPS_LOD0",current=current,deleted=deleted,groups=groups},true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);Debug.Log("COMPREHENSIVE_INVENTORY_READY");
    }
    [Serializable] public class Change {public string path,action;public float delta;}
    [Serializable] public class Result {public string utc;public Change[] changes;public int restoredSupports,removedInteriorRoots,groundedRoots,terrainSamples,missingScripts;public string[] survivingInterior;}
    static bool PrivateLot(string p)=>System.Text.RegularExpressions.Regex.IsMatch(p,@"^Root/Comps/Comp(?!11(?:/| \())\d+(?: \(\d+\))?/");
    static bool Indoor(string n)=>System.Text.RegularExpressions.Regex.IsMatch(n,@"^SM_(airconditioner|lamp_|lantern_|chair|table_|counter_|shel|book_|bed|sofa|bottle|carton|bucket|broom|flowerpot|plants_box|mailbox|mirror|board_|sticker_|sign_3d|vendingmachine|warning_panel|stairs|entrance|cloth_|fusebox|drain_)",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    static float Surface(Terrain[] lands,Vector3 p)
    {float y=-999;foreach(var t in lands){var a=t.transform.position;var s=t.terrainData.size;if(p.x>=a.x&&p.x<=a.x+s.x&&p.z>=a.z&&p.z<=a.z+s.z)y=Mathf.Max(y,t.SampleHeight(p)+a.y);}return y;}
    public static void Repair()
    {
        var scenes=Load();var game=scenes[0];var city=scenes.Single(s=>s.name=="100_Dressing_City_01");var changes=new List<Change>();var result=new Result{utc=DateTime.UtcNow.ToString("o")};
        var map=All(city).GroupBy(P).ToDictionary(g=>g.Key,g=>g.First());
        var source=EditorSceneManager.OpenScene("Assets/Editor/ComprehensiveSourceCity.unity",OpenSceneMode.Additive);
        foreach(var t in All(source).ToArray())
        {
            var path=P(t);bool stairs=System.Text.RegularExpressions.Regex.IsMatch(path,@"^Root/Comps/Comp11(?:/| \()");
            bool planter=t.name.StartsWith("SM_wall_flowerbed")&&(path.StartsWith("Root/City_Park_Forest/")||path.StartsWith("Root/Comps/ParkWalkComp/"));
            if(!((stairs&&t.name.StartsWith("SM_wall"))||planter)||map.ContainsKey(path))continue;
            if(!map.TryGetValue(P(t.parent),out var parent))continue;
            var copy=UnityEngine.Object.Instantiate(t.gameObject,parent,false);copy.name=t.name;copy.transform.localPosition=t.localPosition;copy.transform.localRotation=t.localRotation;copy.transform.localScale=t.localScale;
            result.restoredSupports++;changes.Add(new Change{path=path,action="restore_original_structural_support"});
        }
        EditorSceneManager.CloseScene(source,true);
        foreach(var t in All(city).ToArray())
        {
            if(!t)continue;string p=P(t);
            if(PrivateLot(p)&&Indoor(t.name)){changes.Add(new Change{path=p,action="remove_deleted_building_furniture_and_wall_props"});result.removedInteriorRoots++;UnityEngine.Object.DestroyImmediate(t.gameObject);}
        }
        var lands=All(game).Select(t=>t.GetComponent<Terrain>()).Where(t=>t&&t.name!="IslandOuterSeabed").ToArray();
        // The authored beach props define the dry recreation terrace. Restore a
        // connected sandy surface under their feet, then feather into the coast.
        var anchors=new List<Vector3>();
        foreach(var t in All(city).Where(t=>P(t).StartsWith("Root/Others/Beach/")&&t.GetComponent<MeshFilter>()))
        {var b=BoundsOf(t);if(b.min.y>-5f&&b.min.y<-2.8f){var p=b.center;p.y=Mathf.Min(-3.7f,b.min.y-.025f);anchors.Add(p);}}
        foreach(var t in All(game).Where(t=>t.parent&&P(t.parent)=="---Beach"))
        {var b=BoundsOf(t);if(b.min.y>-5f&&b.min.y<-2.8f){var p=b.center;p.y=Mathf.Min(-3.7f,b.min.y-.025f);anchors.Add(p);}}
        foreach(var land in lands)
        {
            var data=land.terrainData;int n=data.heightmapResolution;var h=data.GetHeights(0,0,n,n);bool changed=false;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {var p=land.transform.position+new Vector3(x*data.size.x/(n-1),h[z,x]*data.size.y,z*data.size.z/(n-1));float target=p.y;
                foreach(var a in anchors){float dx=p.x-a.x,dz=p.z-a.z;float d=Mathf.Sqrt(dx*dx+dz*dz);if(d>6)continue;float blend=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((d-2)/4));target=Mathf.Max(target,Mathf.Lerp(p.y,a.y,blend));}
                if(target>p.y+.001f){h[z,x]=(target-land.transform.position.y)/data.size.y;result.terrainSamples++;changed=true;}}
            if(changed){data.SetHeights(0,0,h);EditorUtility.SetDirty(data);land.Flush();}
        }
        Physics.SyncTransforms();
        // Fix independent grounded decorations, never individual LOD meshes or
        // subparts of a vehicle, dock, character, interactive shop or furniture.
        var candidates=new List<Transform>();
        candidates.AddRange(All(city).Where(t=>t.GetComponent<MeshFilter>()&&(PrivateLot(P(t))&&(t.name.StartsWith("SM_bush")||t.name.StartsWith("SM_tree")||t.name.StartsWith("SM_bicycle")||t.name.StartsWith("SM_mofa"))||P(t).StartsWith("Root/Bench/"))));
        foreach(var foliage in scenes.Where(s=>s.name=="110_Dressing_City_Folliage_01"))candidates.AddRange(All(foliage).Where(t=>t.parent&&P(t.parent)=="Root/Dock_Beach_Foliage"));
        foreach(var t in candidates.Distinct())
        {if(!t)continue;var b=BoundsOf(t);float ground=Surface(lands,b.center);float delta=ground-b.min.y;if(delta<-.18f&&delta>-5f){t.position+=Vector3.up*(delta+.015f);result.groundedRoots++;changes.Add(new Change{path=P(t),action="ground_remaining_outdoor_decoration",delta=delta+.015f});}}
        // Remove the last road slab from the deleted bridge approach.
        var road=All(city).FirstOrDefault(t=>P(t)=="Root/Roads/SM_street_Root/SM_street (19)");if(road){changes.Add(new Change{path=P(road),action="remove_last_bridge_approach_slab"});UnityEngine.Object.DestroyImmediate(road.gameObject);}
        foreach(var s in scenes)
        {
            foreach(var lod in All(s).Select(t=>t.GetComponent<LODGroup>()).Where(l=>l))
            {var levels=lod.GetLODs();if(levels.Length==0)continue;for(int i=0;i<levels.Length;i++)levels[i].renderers=levels[i].renderers.Where(r=>r).ToArray();levels[levels.Length-1].screenRelativeTransitionHeight=.00005f;lod.SetLODs(levels);if(lod.enabled&&lod.gameObject.activeInHierarchy)lod.ForceLOD(-1);}
            foreach(var t in All(s)){result.missingScripts+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);}
        }
        // Old baked occlusion volumes still describe the deleted houses.
        foreach(var camera in All(game).Select(t=>t.GetComponent<Camera>()).Where(c=>c)){camera.useOcclusionCulling=false;camera.farClipPlane=Mathf.Max(camera.farClipPlane,800);}
        result.survivingInterior=All(city).Where(t=>PrivateLot(P(t))&&Indoor(t.name)).Select(P).ToArray();
        foreach(var s in scenes.Where(s=>s.name=="GameState"||s.name.StartsWith("1")))EditorSceneManager.SaveScene(s);
        AssetDatabase.SaveAssets();result.changes=changes.ToArray();File.WriteAllText(Path.Combine(Reports,"comprehensive-repair.json"),JsonUtility.ToJson(result,true));
        IslandFrontEndBuilder.RefreshMenuPresentation();Debug.Log("COMPREHENSIVE_REPAIR_SAVED");
    }
}
