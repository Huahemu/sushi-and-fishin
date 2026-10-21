using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using StylizedWater3;

public static class RestaurantCoastSourceRepair
{
    [Serializable] public class Backup {public float originalWaterY;public string backup;}
    [Serializable] public class Inventory {public Item[] objects;}
    [Serializable] public class Item {public string path,mesh;}
    [Serializable] public class TerrainChange {public string path,source;public int exactSamples,blendedSamples,changedSamples;public bool outsideUnchanged,innerMatchesSource;}
    [Serializable] public class Floor {public float x,z,y;public string collider;}
    [Serializable] public class Result {public string utc,stage;public float originalWaterY,oldWaterY,cellSpacing,innerX0,innerZ0,innerX1,innerZ1,feather;public int originalSupportMeshesRestored;public bool restaurantAndStairTransformsUnchanged,stairMeshesUnchanged;public TerrainChange[] terrains;public Floor[] floors;}
    static string Reports=>IslandFrontEndInventory.Reports;
    public static float OriginalWaterLevel {get {var path=Path.Combine(Reports,"restaurant-alignment-backup.json");if(!File.Exists(path))throw new InvalidOperationException("Verified original sea-level provenance is required");return JsonUtility.FromJson<Backup>(File.ReadAllText(path)).originalWaterY;}}
    static string P(Transform t)=>IslandFrontEndInventory.PathOf(t);
    static string Pose(Transform t)=>t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R");
    public static void Run() {
        var backup=JsonUtility.FromJson<Backup>(File.ReadAllText(Path.Combine(Reports,"restaurant-alignment-backup.json")));
        if(string.IsNullOrEmpty(backup.backup)||!Directory.Exists(backup.backup))throw new Exception("Alignment backup required");
        var scene=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene);
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var restaurant=all.First(t=>P(t)=="---Restaurant/Restaurant/RestaurantStatic");
        var poses=restaurant.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>Pose(t));
        var dockMeshes=restaurant.GetComponentsInChildren<MeshFilter>(true).Where(f=>P(f.transform).Contains("/Dock/")).ToDictionary(f=>f,f=>f.sharedMesh);
        var structures=restaurant.GetComponentsInChildren<MeshFilter>(true).Where(f=>P(f.transform).Contains("/Poles/")||P(f.transform).Contains("/Dock/")||f.name=="Ground").ToArray();
        var bounds=IslandComprehensiveRepair.BoundsOf(structures[0].transform);foreach(var f in structures.Skip(1))bounds.Encapsulate(IslandComprehensiveRepair.BoundsOf(f.transform));
        var maps=JsonUtility.FromJson<RestaurantAlignmentDiagnosis.Trim>(File.ReadAllText(Path.Combine(Reports,"island-trim-edit-audit.json"))).terrains;
        var lands=all.Select(t=>t.GetComponent<Terrain>()).Where(t=>t&&t.name!="IslandOuterSeabed").ToArray();
        float cell=lands.Max(t=>t.terrainData.size.x/(t.terrainData.heightmapResolution-1));
        float x0=bounds.min.x-4*cell,x1=bounds.max.x+4*cell,z0=bounds.min.z-4*cell,z1=bounds.max.z+4*cell,feather=4*cell;
        var report=new Result{utc=DateTime.UtcNow.ToString("o"),stage="STARTED",originalWaterY=backup.originalWaterY,cellSpacing=cell,innerX0=x0,innerX1=x1,innerZ0=z0,innerZ1=z1,feather=feather};
        float Distance(float x,float z)=>Mathf.Max(Mathf.Max(x0-x,x-x1),Mathf.Max(z0-z,z-z1));
        var changes=new List<TerrainChange>();
        foreach(var land in lands) {
            var map=maps.Single(m=>m.path==P(land.transform));var source=AssetDatabase.LoadAssetAtPath<TerrainData>(map.originalData);var data=land.terrainData;
            if(source.heightmapResolution!=data.heightmapResolution||source.size!=data.size)throw new Exception("Source/current terrain coordinates differ");
            int n=data.heightmapResolution;var h=data.GetHeights(0,0,n,n);var before=(float[,])h.Clone();var original=source.GetHeights(0,0,n,n);
            var row=new TerrainChange{path=P(land.transform),source=map.originalData,outsideUnchanged=true,innerMatchesSource=true};
            for(int z=0;z<n;z++)for(int x=0;x<n;x++) {
                float worldX=land.transform.position.x+x*data.size.x/(n-1),worldZ=land.transform.position.z+z*data.size.z/(n-1),distance=Distance(worldX,worldZ);
                if(distance>=feather)continue;
                float weight=distance<=0?1:1-Mathf.SmoothStep(0,1,distance/feather);
                float value=weight==1?original[z,x]:Mathf.Lerp(before[z,x],original[z,x],weight);
                if(value!=h[z,x]){h[z,x]=value;row.changedSamples++;}
                if(weight==1)row.exactSamples++;else row.blendedSamples++;
            }
            if(row.changedSamples>0){data.SetHeights(0,0,h);land.Flush();EditorUtility.SetDirty(data);}
            var after=data.GetHeights(0,0,n,n);
            for(int z=0;z<n;z++)for(int x=0;x<n;x++) {
                float distance=Distance(land.transform.position.x+x*data.size.x/(n-1),land.transform.position.z+z*data.size.z/(n-1));
                if(distance>=feather&&after[z,x]!=before[z,x])row.outsideUnchanged=false;
                if(distance<=0&&Mathf.Abs(after[z,x]-original[z,x])>1e-6f)row.innerMatchesSource=false;
            }
            if(!row.outsideUnchanged||!row.innerMatchesSource)throw new Exception("Source terrain restoration boundary failed");changes.Add(row);
        }
        var old=JsonUtility.FromJson<Inventory>(File.ReadAllText(Path.Combine(Reports,"island-revision-inventory.json"))).objects.Where(t=>t.path.StartsWith("---Restaurant/Restaurant/RestaurantStatic/Poles/")&&!string.IsNullOrEmpty(t.mesh)).ToDictionary(t=>t.path,t=>t.mesh);
        foreach(var filter in structures.Where(f=>P(f.transform).Contains("/Poles/"))) {
            var original=AssetDatabase.LoadAssetAtPath<Mesh>(old[P(filter.transform)]);if(!original)throw new Exception("Original support mesh is missing");
            if(filter.sharedMesh==original)continue;var altered=filter.sharedMesh;filter.sharedMesh=original;
            foreach(var collider in filter.GetComponents<MeshCollider>())if(collider.sharedMesh==altered)collider.sharedMesh=original;
            report.originalSupportMeshesRestored++;
        }
        var ocean=all.Select(t=>t.GetComponent<OceanFollowBehaviour>()).First(t=>t);report.oldWaterY=ocean.transform.position.y;
        float delta=backup.originalWaterY-report.oldWaterY;ocean.transform.position+=Vector3.up*delta;EditorUtility.SetDirty(ocean);
        foreach(var grid in all.Select(t=>t.GetComponent<WaterGrid>()).Where(g=>g&&g.name=="TownOceanContinuousGrid")){var pos=grid.transform.position;pos.y=backup.originalWaterY;grid.transform.position=pos;EditorUtility.SetDirty(grid);}
        foreach(var area in all.Select(t=>t.GetComponent<StylizedWater3.UnderwaterRendering.UnderwaterArea>()).Where(a=>a&&a.waterLevelSource==StylizedWater3.UnderwaterRendering.UnderwaterArea.WaterLevelSource.Ocean)) {
            area.transform.position+=Vector3.up*delta;area.waterLevel=backup.originalWaterY;EditorUtility.SetDirty(area);
        }
        report.restaurantAndStairTransformsUnchanged=poses.All(p=>p.Key&&Pose(p.Key)==p.Value);
        report.stairMeshesUnchanged=dockMeshes.All(p=>p.Key&&p.Key.sharedMesh==p.Value);
        if(!report.restaurantAndStairTransformsUnchanged||!report.stairMeshesUnchanged)throw new Exception("Restaurant/stair geometry moved");
        report.terrains=changes.ToArray();Physics.SyncTransforms();
        var floors=new List<Floor>();
        for(float z=48;z<=68;z+=.25f)for(float x=14;x<=29;x+=.25f) {
            var hits=Physics.RaycastAll(new Vector3(x,1.5f,z),Vector3.down,15,~(1<<4),QueryTriggerInteraction.Ignore).Where(h=>h.normal.y>.65f).OrderByDescending(h=>h.point.y).ToArray();
            if(hits.Length>0)floors.Add(new Floor{x=x,z=z,y=hits[0].point.y,collider=P(hits[0].collider.transform)});
        }
        report.floors=floors.ToArray();EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();report.stage="SAVED_NEEDS_WALKING_VERIFICATION";
        File.WriteAllText(Path.Combine(Reports,"restaurant-coast-source-repair.json"),JsonUtility.ToJson(report,true));
        IslandRevision20261018.RebuildRevisionNavigation();IslandFrontEndBuilder.RefreshMenuPresentation();EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        Debug.Log("RESTAURANT_ORIGINAL_COAST_RESTORED "+report.originalSupportMeshesRestored);
    }
}
