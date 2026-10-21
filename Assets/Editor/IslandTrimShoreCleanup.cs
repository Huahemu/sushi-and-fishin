using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class IslandTrimShoreCleanup
{
    public static void Run()
    {
        var scene=EditorSceneManager.OpenScene(IslandFrontEndBuilder.DressingRoot+"/100_Dressing_City_01.unity",OpenSceneMode.Single);
        string[] paths={"Root/Roads/SM_street_Root/SM_street (20)","Root/Roads/SM_street_Root/SM_street (21)","Root/Roads/SM_sidewalk_Root/SM_sidewalk_thin_long (29)","Root/Roads/SM_sidewalk_Root/SM_sidewalk_thin_long (30)","Root/Roads/SM_street_decal_Root/SM_street_decal (73)","Root/Roads/SM_street_decal_Root/SM_street_decal (86)","Root/Roads/SM_street_decal_Root/SM_street_decal (80)","Root/Roads/SM_street_decal_Root/SM_street_decal (81)"};
        var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        foreach(var path in paths){var t=all.SingleOrDefault(t=>t&&IslandFrontEndInventory.PathOf(t)==path);if(t)UnityEngine.Object.DestroyImmediate(t.gameObject);}
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllLines(Path.Combine(IslandFrontEndInventory.Reports,"island-trim-shore-road-removal.txt"),paths);
        IslandFrontEndBuilder.RefreshMenuPresentation();IslandTrimNavigationRepair.Run();
    }
}
