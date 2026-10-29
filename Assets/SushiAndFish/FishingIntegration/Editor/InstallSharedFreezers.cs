using System;
using System.IO;
using System.Linq;
using System.Text;
using PaperPixelGames.Core.FishFridge;
using PaperPixelGames.Core.Tablet;
using SushiAndFish.FishingIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Edit-time asset inspection/construction only. Never starts a gameplay session.
public static class InstallSharedFreezers
{
    public const string Out="D:/UnityProjects/_CodexWork/FridgeBoatRecall-20261029/";
    public const string Prefab="Assets/SushiAndFish/FishingIntegration/Archipelago/Prefabs/SharedIslandFreezer.prefab";
    const string Scene="Assets/SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity";
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Static construction only");
        var scene=EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);
        var fridge=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Fridge>(true)).Single();
        var source=fridge.transform.Find("Graphic/Fridge");if(!source)throw new Exception("Native freezer body not found");
        var root=new GameObject("Shared native fish freezer");
        var visual=Object.Instantiate(source.gameObject,root.transform);visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;
        visual.transform.localScale=source.lossyScale;
        foreach(var c in visual.GetComponentsInChildren<Component>(true))if(c&&!(c is Transform)&&!(c is MeshFilter)&&!(c is MeshRenderer))Object.DestroyImmediate(c);
        foreach(var t in visual.GetComponentsInChildren<Transform>(true))t.gameObject.SetActive(true);
        Bounds bounds=visual.GetComponentsInChildren<MeshRenderer>().First().bounds;
        foreach(var r in visual.GetComponentsInChildren<MeshRenderer>())bounds.Encapsulate(r.bounds);
        visual.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        root.AddComponent<SharedFishFreezer>();var box=root.AddComponent<BoxCollider>();box.center=Vector3.up*bounds.size.y*.5f;box.size=bounds.size;
        PrefabUtility.SaveAsPrefabAsset(root,Prefab);Object.DestroyImmediate(root);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var config=MigrationConfig.Load();var settings=ArchipelagoSettings.Load();config.freezerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        config.nativeInventoryRow=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/InventoryItem.prefab").GetComponent<InventoryItemUI>();
        config.freezerPoints=new FishFreezerPoint[2];var report=new StringBuilder();
        for(int i=1;i<3;i++)
        {
            var region=settings.regions[i];var island=Object.Instantiate(region.prefab,region.center,Quaternion.identity);
            var terrain=island.GetComponentsInChildren<MeshCollider>(true).Single(c=>c.name=="Island1");
            var access=settings.accessPrefab.transform.Cast<Transform>().Single(t=>t.name==region.title+" · 靠岸栈桥");
            float length=access.GetComponent<BoxCollider>().size.z-.8f;var entry=access.position-access.forward*length*.5f;
            var pos=entry-access.forward*5+access.right*6;Physics.SyncTransforms();
            if(!terrain.Raycast(new Ray(pos+Vector3.up*20,Vector3.down),out var hit,40))throw new Exception("No dry ground for island freezer "+i);
            pos.y=hit.point.y+.015f;if(pos.y<config.seaHeight+.5f)throw new Exception("Freezer must stand on dry island plateau");
            config.freezerPoints[i-1]=new FishFreezerPoint{position=pos,rotation=access.rotation};
            report.AppendLine("Island "+i+" freezer "+pos.ToString("F4")+" native size "+bounds.size.ToString("F3"));
            Object.DestroyImmediate(island);
        }
        EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();File.WriteAllText(Out+"FreezerPlacement.txt",report.ToString());
        VerifySharedFreezers.Run();Debug.Log("SHARED_FREEZERS_BUILT play=false simulation=false");
    }
    public static void Inspect()
    {
        if(EditorApplication.isPlaying)throw new Exception("Static inspection only");
        var scene=EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);
        var fridge=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Fridge>(true)).Single();
        var report=new StringBuilder();
        report.AppendLine("Native fridge "+fridge.transform.position+" scale "+fridge.transform.lossyScale);
        foreach(var t in fridge.GetComponentsInChildren<Transform>(true))
        {
            report.AppendLine(AnimationUtility.CalculateTransformPath(t,fridge.transform)+" active="+t.gameObject.activeSelf+" components="+string.Join(",",t.GetComponents<Component>().Select(c=>c?c.GetType().Name:"missing")));
        }
        var so=new SerializedObject(fridge);report.AppendLine(EditorJsonUtility.ToJson(fridge,true));
        var apps=AssetDatabase.FindAssets("t:Prefab").Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.Contains("Inventory"));
        foreach(var p in apps)report.AppendLine("Inventory prefab "+p);
        File.WriteAllText(Out+"NativeFridgeInspection.txt",report.ToString());
        Debug.Log("SHARED_FREEZER_INSPECTED play=false");
    }
}
