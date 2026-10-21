using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SushiAndFish.CityExpansion;
using PaperPixelGames.Core;
using UnityEngine.SceneManagement;

public static partial class CityExpansionInstallation
{
    public static void RenameCasinoRegion()
    {
        var scene=EditorSceneManager.OpenScene(IslandFrontEndBuilder.DressingRoot+"/100_Dressing_City_01.unity");
        int count=0;
        foreach(var trigger in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LocationTrigger>(true)))
            if(string.Equals(trigger.LocationName,"PARK",StringComparison.OrdinalIgnoreCase)){trigger.LocationName="CASINO";EditorUtility.SetDirty(trigger);count++;}
        if(count!=1)throw new Exception("Expected exactly one former Park location trigger, got "+count);
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
    }
    public static void FixReturnAndPlotOcclusion()
    {
        gameScene=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene);
        var room=All(gameScene).Single(t=>t.name=="AR_CinemaAuditorium");
        var portal=room.GetComponentsInChildren<CityCinemaPortal>(true).Single();
        portal.transform.position=Find(room,"AR_CinemaAuditorium_ReturnEntryClosedLeaf").GetComponent<Renderer>().bounds.center-room.forward*.05f;
        const string path=CityRoot+"/Materials/ReservedParcelOpaque.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="ReservedParcelOpaque"};AssetDatabase.CreateAsset(material,path);}
        material.SetColor("_BaseColor",new Color(.98f,.72f,.12f));material.SetFloat("_Surface",0);material.SetFloat("_ZWrite",1);material.SetFloat("_ZTest",4);material.SetFloat("_Smoothness",.1f);material.renderQueue=2000;EditorUtility.SetDirty(material);
        int count=0;
        foreach(var renderer in All(gameScene).Where(t=>t.name.EndsWith("_YellowDashedBoundary")).Select(t=>t.GetComponent<MeshRenderer>()).Where(r=>r))
        {renderer.sharedMaterial=material;count++;}
        var result=JsonUtility.FromJson<WorldReceipt>(File.ReadAllText(WorldPath));result.diagnostics.Add("Runtime correction: moved return interaction onto the actual auditorium exit door, outside the arrival camera. Replaced "+count+" remaining empty-plot outline material bindings with an owned opaque URP material so distant planning marks obey building occlusion; original source materials unchanged.");
        EditorSceneManager.SaveScene(gameScene);AssetDatabase.SaveAssets();File.WriteAllText(WorldPath,JsonUtility.ToJson(result,true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
    }
    public static void FixWalkableAuditorium()
    {
        gameScene=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene);
        var room=All(gameScene).Single(t=>t.name=="AR_CinemaAuditorium");
        int count=0;
        foreach(var renderer in room.GetComponentsInChildren<MeshRenderer>(true).Where(r=>Regex.IsMatch(r.name,@"Tier_|HalfStep|RearCorridor")))
            if(!renderer.GetComponent<Collider>()){var c=renderer.gameObject.AddComponent<MeshCollider>();c.sharedMesh=renderer.GetComponent<MeshFilter>().sharedMesh;count++;}
        HidePlotReservation("P08");HidePlotReservation("P13");
        var result=JsonUtility.FromJson<WorldReceipt>(File.ReadAllText(WorldPath));result.diagnostics.Add("Runtime correction: added "+count+" original auditorium tier/half-step/rear-corridor mesh colliders. Consumed P08/P13 reservation meshes removed while plot records remain.");
        EditorSceneManager.SaveScene(gameScene);AssetDatabase.SaveAssets();File.WriteAllText(WorldPath,JsonUtility.ToJson(result,true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");Debug.Log("CITY_AUDITORIUM_WALKABLE_COLLIDERS "+count);
    }
}
