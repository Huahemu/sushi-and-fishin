using System;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using PaperPixelGames.Core.DressingSceneSystem;

public static class IslandTrimNavigationRepair
{
    [Serializable] public class Report {public string utc,asset,originalAsset;public Bounds bounds;public bool shopsReachable,restaurantReachable,removedLighthouseAbsent,removedTunnelAbsent;}
    public static void Run()
    {
        var game=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene,OpenSceneMode.Single);
        foreach(var name in new[]{"100_Dressing_City_01","110_Dressing_City_Folliage_01","120_Dressing_City_Grass_01","130_Dressing_City_Rocks_01"})
        {var scene=EditorSceneManager.OpenScene(IslandFrontEndBuilder.DressingRoot+"/"+name+".unity",OpenSceneMode.Additive);foreach(var root in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DressingSceneRoot>(true)))root.ActivateAllImmediate();}
        var surface=game.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<NavMeshSurface>(true)).Single(s=>IslandFrontEndInventory.PathOf(s.transform)=="---AI/ResidentManager/NavMesh Surface");
        string original=AssetDatabase.GetAssetPath(surface.navMeshData);
        surface.collectObjects=CollectObjects.Volume;surface.center=new Vector3(-25,7.5f,7.5f);surface.size=new Vector3(360,45,215);
        surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        if(!surface.navMeshData)throw new InvalidOperationException("No rebuilt island navigation data");
        const string path="Assets/SushiAndFish/Modules/OriginalIsland/TerrainData/TownIsland_ResidentsNavMesh_ShoreFinal.asset";
        if(AssetDatabase.LoadAssetAtPath<NavMeshData>(path))throw new InvalidOperationException("Refusing to overwrite earlier navigation bake");
        AssetDatabase.CreateAsset(surface.navMeshData,path);EditorUtility.SetDirty(surface);EditorSceneManager.SaveScene(game);AssetDatabase.SaveAssets();
        bool Sample(Vector3 p)=>NavMesh.SamplePosition(p,out var hit,5,NavMesh.AllAreas);
        var report=new Report{utc=DateTime.UtcNow.ToString("o"),asset=path,originalAsset=original,bounds=surface.navMeshData.sourceBounds,
            shopsReachable=Sample(new Vector3(-5,3,-10)),restaurantReachable=Sample(new Vector3(26,0,77)),removedLighthouseAbsent=!Sample(new Vector3(183,3,-89)),removedTunnelAbsent=!Sample(new Vector3(6,3,-136))};
        File.WriteAllText(Path.Combine(IslandFrontEndInventory.Reports,"island-trim-navigation-repair.json"),JsonUtility.ToJson(report,true));
        if(!report.shopsReachable||!report.restaurantReachable||!report.removedLighthouseAbsent||!report.removedTunnelAbsent)throw new InvalidOperationException("Trimmed island navigation checks failed");
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);
    }
}
