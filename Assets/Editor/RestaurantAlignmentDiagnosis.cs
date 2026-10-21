using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Read-only comparison against the original pre-trim terrain assets.
public static class RestaurantAlignmentDiagnosis
{
    [Serializable] public class Mapping {public string path,originalData,workingData;}
    [Serializable] public class Trim {public Mapping[] terrains;}
    [Serializable] public class GroundPoint {public string label,terrain;public Vector3 point;public float originalHeight,currentHeight,delta;}
    [Serializable] public class ObjectRow {public string path,mesh;public Vector3 position,min,max;}
    [Serializable] public class TerrainRow {public string path,originalData,currentData;public Vector3 position,size;public int resolution;public string heights,originalHeights;}
    [Serializable] public class Report {public string utc;public float waterY;public GroundPoint[] samples;public ObjectRow[] objects;public TerrainRow[] terrains;}
    static string Reports=>IslandFrontEndInventory.Reports;
    static string P(Transform t)=>IslandFrontEndInventory.PathOf(t);
    public static void Run()=>Read("restaurant-alignment-diagnosis.json");
    public static void After()=>Read("restaurant-alignment-diagnosis-after.json");
    static void Read(string output) {
        var scene=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene);
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var mapping=JsonUtility.FromJson<Trim>(File.ReadAllText(Path.Combine(Reports,"island-trim-edit-audit.json"))).terrains;
        var lands=all.Select(t=>t.GetComponent<Terrain>()).Where(t=>t&&t.name!="IslandOuterSeabed").ToArray();
        var rows=new List<TerrainRow>();foreach(var terrain in lands) {
            var entry=mapping.Single(m=>m.path==P(terrain.transform));var d=terrain.terrainData;var h=d.GetHeights(0,0,d.heightmapResolution,d.heightmapResolution);var b=new byte[h.Length*4];Buffer.BlockCopy(h,0,b,0,b.Length);
            var source=AssetDatabase.LoadAssetAtPath<TerrainData>(entry.originalData);var sourceHeights=source.GetHeights(0,0,source.heightmapResolution,source.heightmapResolution);var sourceBytes=new byte[sourceHeights.Length*4];Buffer.BlockCopy(sourceHeights,0,sourceBytes,0,sourceBytes.Length);
            rows.Add(new TerrainRow{path=P(terrain.transform),originalData=entry.originalData,currentData=AssetDatabase.GetAssetPath(d),position=terrain.transform.position,size=d.size,resolution=d.heightmapResolution,heights=Convert.ToBase64String(b),originalHeights=Convert.ToBase64String(sourceBytes)});
        }
        var samples=new List<GroundPoint>();
        void Sample(string label,Vector3 point) {
            var land=lands.Where(t=>point.x>=t.transform.position.x&&point.z>=t.transform.position.z&&point.x<=t.transform.position.x+t.terrainData.size.x&&point.z<=t.transform.position.z+t.terrainData.size.z).FirstOrDefault();if(!land)return;
            var entry=mapping.Single(m=>m.path==P(land.transform));var original=AssetDatabase.LoadAssetAtPath<TerrainData>(entry.originalData);float nx=(point.x-land.transform.position.x)/original.size.x,nz=(point.z-land.transform.position.z)/original.size.z;
            float before=original.GetInterpolatedHeight(nx,nz)+land.transform.position.y,after=land.SampleHeight(point)+land.transform.position.y;
            samples.Add(new GroundPoint{label=label,terrain=entry.originalData,point=point,originalHeight=before,currentHeight=after,delta=after-before});
        }
        var structures=all.Where(t=>P(t).StartsWith("---Restaurant/Restaurant/RestaurantStatic/")&&t.GetComponent<MeshFilter>()&&(P(t).Contains("/Poles/")||P(t).Contains("/Dock/")||t.name=="Ground")).ToArray();
        foreach(var t in structures){var b=IslandComprehensiveRepair.BoundsOf(t);Sample(P(t),b.center);}
        for(int z=30;z<=110;z+=5)for(int x=-5;x<=55;x+=5)Sample("shore-grid",new Vector3(x,0,z));
        var objects=structures.Select(t=>{var b=IslandComprehensiveRepair.BoundsOf(t);return new ObjectRow{path=P(t),position=t.position,min=b.min,max=b.max,mesh=AssetDatabase.GetAssetPath(t.GetComponent<MeshFilter>().sharedMesh)};}).ToArray();
        var ocean=all.Select(t=>t.GetComponent<StylizedWater3.OceanFollowBehaviour>()).First(t=>t);
        File.WriteAllText(Path.Combine(Reports,output),JsonUtility.ToJson(new Report{utc=DateTime.UtcNow.ToString("o"),waterY=ocean.transform.position.y,samples=samples.ToArray(),objects=objects,terrains=rows.ToArray()},true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");Debug.Log("RESTAURANT_ALIGNMENT_DIAGNOSIS_READ_ONLY_DONE");
    }
}
