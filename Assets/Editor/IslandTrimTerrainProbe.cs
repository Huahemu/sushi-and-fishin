using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class IslandTrimTerrainProbe
{
    [Serializable] public class Land { public string path, data, heights; public Vector3 position,size; public int resolution; }
    [Serializable] public class Report { public Land[] terrains; }
    public static void Run()
    {
        var scene=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene,OpenSceneMode.Single);
        var result=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Terrain>(true)).Select(t=>{
            var d=t.terrainData;var h=d.GetHeights(0,0,d.heightmapResolution,d.heightmapResolution);var bytes=new byte[h.Length*4];Buffer.BlockCopy(h,0,bytes,0,bytes.Length);
            return new Land{path=IslandFrontEndInventory.PathOf(t.transform),data=AssetDatabase.GetAssetPath(d),position=t.transform.position,size=d.size,resolution=d.heightmapResolution,heights=Convert.ToBase64String(bytes)};
        }).ToArray();
        File.WriteAllText(Path.Combine(IslandFrontEndInventory.Reports,"island-trim-heights.json"),JsonUtility.ToJson(new Report{terrains=result}));
        EditorApplication.Exit(0);
    }
}
