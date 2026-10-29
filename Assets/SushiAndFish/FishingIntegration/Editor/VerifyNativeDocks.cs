using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SushiAndFish.FishingIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class VerifyNativeDocks
{
    const string Out="D:/UnityProjects/_CodexWork/OriginalIslandDocks-20261028/";
    static readonly List<string> checks=new List<string>();
    static void Check(bool value,string text){checks.Add((value?"PASS ":"FAIL ")+text);File.WriteAllLines(Out+"Verification.txt",checks);if(!value)throw new Exception(text);}
    public static void Run()
    {
        Check(!EditorApplication.isPlaying,"Static geometry/reference inspection and in-memory save data only; no Play or gameplay/physics simulation");
        var settings=ArchipelagoSettings.Load();var source=AssetDatabase.LoadAssetAtPath<GameObject>(InstallNativeDocks.Dock);
        Check(Vector3.Distance(settings.boatSpawn,new Vector3(132.28311f,-3.75f,-18.784563f))<.01f,"Default boat berth is beside the existing city pier in the supplied screenshot");
        Check(Quaternion.Angle(settings.boatRotation,Quaternion.Euler(0,90,0))<.01f,"Boat is aligned with the existing wooden pier");
        var old=new SavedBoat{valid=true,position=new Vector3(49.62593f,-3.75f,93.01851f),rotation=Quaternion.identity};
        Check(ArchipelagoWorld.UsesLegacyDefaultBerth(old),"A saved boat still at the removed temporary berth is eligible for one-time relocation");
        var travelled=new SavedBoat{valid=true,position=settings.regions[1].dock,rotation=Quaternion.identity};
        Check(!ArchipelagoWorld.UsesLegacyDefaultBerth(travelled),"A previously sailed boat keeps its saved position");
        old.berthVersion=ArchipelagoWorld.NativeDockBerthVersion;
        Check(!ArchipelagoWorld.UsesLegacyDefaultBerth(old),"Once migrated, returning near the former berth does not relocate again");
        old.berthVersion=0;old.position=Vector3.positiveInfinity;
        Check(!ArchipelagoWorld.UsesLegacyDefaultBerth(old)&&!ArchipelagoWorld.UsesLegacyDefaultBerth(null),"Invalid/missing boat data is not treated as a legacy berth");
        var data=new FishingWorldSave{boat=travelled};var round=JsonUtility.FromJson<FishingWorldSave>(JsonUtility.ToJson(data));
        Check(round.boat.position==travelled.position&&round.boat.rotation==travelled.rotation,"Existing saved boat positions survive the added optional berth revision field");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        settings=ArchipelagoSettings.Load();source=AssetDatabase.LoadAssetAtPath<GameObject>(InstallNativeDocks.Dock);
        var access=(GameObject)PrefabUtility.InstantiatePrefab(settings.accessPrefab);
        Check(access.GetComponentsInChildren<MonoBehaviour>(true).Length==0,"Island access has no copied gameplay/NPC/network behaviors");
        for(int i=1;i<3;i++)
        {
            var region=settings.regions[i];var island=(GameObject)PrefabUtility.InstantiatePrefab(region.prefab);island.transform.position=region.center;
            var frame=access.transform.Cast<Transform>().Single(t=>t.name==region.title+" · 靠岸栈桥");
            var dock=access.transform.Cast<Transform>().Single(t=>t.name.StartsWith(region.title,StringComparison.Ordinal)&&t.name.Contains("原城镇木码头"));
            var floor=frame.GetComponent<BoxCollider>();Physics.SyncTransforms();
            int misses=0;float distance=floor.size.z-.8f;
            foreach(float side in new[]{-1.3f,0,1.3f})for(int n=0;n<=30;n++)
            {
                var p=frame.TransformPoint(new Vector3(side,0,-distance*.5f+n*distance/30));
                if(!floor.Raycast(new Ray(p+Vector3.up*2,Vector3.down),out var hit,4)||hit.normal.y<.8f)misses++;
            }
            Check(misses==0,"Island "+i+" ramp supports its full five-metre path with 93 static collision probes");
            var continuous=dock.Find("Continuous native dock walking surface").GetComponent<BoxCollider>();
            var bridgeEnd=frame.TransformPoint(new Vector3(0,0,distance*.5f));
            Check(continuous.bounds.Contains(bridgeEnd-Vector3.up*.1f),"Island "+i+" ramp overlaps the native dock floor without an edge gap");
            var terrain=island.GetComponentsInChildren<MeshCollider>(true).Single(c=>c.name=="Island1");
            var entry=frame.TransformPoint(new Vector3(0,0,-distance*.5f));
            Check(terrain.Raycast(new Ray(entry+Vector3.up*4,Vector3.down),out var ground,10)&&Mathf.Abs(entry.y-ground.point.y)<.2f,"Island "+i+" boarding path meets the preserved island terrain at its old flat-area entry");
            Check(PrefabUtility.GetCorrespondingObjectFromSourceAtPath(dock.gameObject,InstallNativeDocks.Dock)==source,"Island "+i+" uses the exact shared original city dock prefab");
            Check(dock.GetComponentsInChildren<MeshFilter>(true).All(f=>AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith("Assets/Mesh/",StringComparison.Ordinal)),"Island "+i+" retains native model meshes/UVs rather than the prior How to Fish pier");
            RenderIsland(access,island,i,region,dock,frame);Object.DestroyImmediate(island);
        }
        foreach(var ramp in access.transform.Cast<Transform>().Where(t=>t.name.Contains("水面登岸")))
        {
            var box=ramp.GetComponent<BoxCollider>();float run=box.size.z-.8f;
            var bottom=ramp.TransformPoint(new Vector3(0,0,-run*.5f));var top=ramp.TransformPoint(new Vector3(0,0,run*.5f));
            Check(bottom.y<=-5.5f&&top.y>-3&&box.size.x>=3.1f&&Vector3.Angle(top-bottom,Vector3.ProjectOnPlane(top-bottom,Vector3.up))<19,"Water entry starts beneath swimming feet and reaches native deck on a wide slope below 19 degrees: "+ramp.name);
            int misses=0;
            for(int n=0;n<=30;n++){var p=Vector3.Lerp(bottom,top,n/30f);if(!box.Raycast(new Ray(p+Vector3.up,Vector3.down),out var h,2))misses++;}
            Check(misses==0,"Continuous submerged-to-dry walking support: "+ramp.name);
        }
        var worldSource=File.ReadAllText("Assets/SushiAndFish/FishingIntegration/Runtime/ArchipelagoWorld.cs");
        var saveSource=File.ReadAllText("Assets/SushiAndFish/FishingIntegration/Runtime/MigrationHarvest.cs");
        Check(worldSource.Contains("!UsesLegacyDefaultBerth(saved)")&&saveSource.Contains("berthVersion=ArchipelagoWorld.NativeDockBerthVersion"),"Normal loading and normal save snapshots perform and remember the one-time old-default relocation");
        Check(File.ReadAllText(AssetDatabase.GetAssetPath(settings.accessPrefab)).IndexOf("guid: "+AssetDatabase.AssetPathToGUID("Assets/SushiAndFish/FishingIntegration/Archipelago/Prefabs/HowToFishDock.prefab"),StringComparison.Ordinal)<0,"Active access prefab no longer references the old temporary dock model");
        Object.DestroyImmediate(access);RenderBerth(settings,source);
        Debug.Log("NATIVE_DOCKS_VERIFIED checks="+checks.Count+" play=false simulation=false savesWritten=false");
    }
    static void RenderIsland(GameObject access,GameObject island,int index,FishingIslandRegion region,Transform dock,Transform ramp)
    {
        var center=(dock.position+ramp.position)*.5f;var outward=(dock.position-region.center).normalized;
        Capture("island"+index+"-native-dock.png",center+Vector3.up*22+outward*33+Vector3.Cross(Vector3.up,outward)*16,center);
    }
    static void RenderBerth(ArchipelagoSettings settings,GameObject model)
    {
        var dock=Object.Instantiate(model);dock.transform.SetPositionAndRotation(new Vector3(126.2031f,-2.722389f,-14.284563f),Quaternion.Euler(0,90,0));
        // Copy only saved boat mesh renderers, never its controller or Rigidbody.
        var visual=new GameObject("Static boat art preview");
        foreach(var original in settings.boatPrefab.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf=original.GetComponent<MeshFilter>();if(!mf||!mf.sharedMesh)continue;
            var part=new GameObject(original.name,typeof(MeshFilter),typeof(MeshRenderer));part.transform.SetParent(visual.transform,false);
            part.transform.SetPositionAndRotation(settings.boatSpawn+settings.boatRotation*original.transform.position,settings.boatRotation*original.transform.rotation);part.transform.localScale=original.transform.lossyScale;
            part.GetComponent<MeshFilter>().sharedMesh=mf.sharedMesh;part.GetComponent<MeshRenderer>().sharedMaterials=original.sharedMaterials;
        }
        Capture("main-native-pier-boat.png",new Vector3(148,9,-35),new Vector3(136,-3,-15));Object.DestroyImmediate(visual);Object.DestroyImmediate(dock);
    }
    public static void PreviewBerth(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderBerth(ArchipelagoSettings.Load(),AssetDatabase.LoadAssetAtPath<GameObject>(InstallNativeDocks.Dock));}
    static void Capture(string file,Vector3 at,Vector3 target)
    {
        var sea=GameObject.CreatePrimitive(PrimitiveType.Plane);sea.name="Static preview water level";sea.transform.position=new Vector3(target.x,-4,target.z);sea.transform.localScale=Vector3.one*500;
        var water=new Material(Shader.Find("Universal Render Pipeline/Unlit"));water.SetColor("_BaseColor",new Color(.12f,.55f,.64f));sea.GetComponent<MeshRenderer>().sharedMaterial=water;
        var light=new GameObject("Static preview light",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(45,-30,0);
        RenderSettings.ambientLight=new Color(.55f,.57f,.60f);var camera=new GameObject("Static dock inspection camera",typeof(Camera)).GetComponent<Camera>();camera.transform.position=at;camera.transform.LookAt(target);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.32f,.55f,.64f);camera.farClipPlane=1500;camera.nearClipPlane=.1f;
        var rt=new RenderTexture(1280,800,24);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
        var image=new Texture2D(1280,800,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(Out+file,image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;
        Object.DestroyImmediate(image);Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(light.gameObject);
        Object.DestroyImmediate(sea);Object.DestroyImmediate(water);
    }
}
