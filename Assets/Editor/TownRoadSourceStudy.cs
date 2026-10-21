using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static partial class IslandRevision20261018
{
    [Serializable] public class NativeGroundMesh {public string store,path,mesh,material;public Vector3 min,max;public Vector3[] triangles;}
    [Serializable] public class NativeRoadStudy {public string utc,source;public int roadAndPavingInstances;public NativeGroundMesh[] storeGroundMeshes;}
    static void StudyPhoto(Camera camera,Vector3 position,Vector3 target,float size,string file) {
        camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));camera.orthographic=size>0;camera.orthographicSize=size;
        var rt=new RenderTexture(2048,1152,24,RenderTextureFormat.ARGB32);rt.Create();camera.targetTexture=rt;
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});var old=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);t.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);t.Apply();File.WriteAllBytes(Path.Combine(Reports,file),t.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(t);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
    }
    public static void StudyOriginalRoadsAndStorePaving()=>BuildNativeRoadStudy(false);
    public static void StartNativeRoadVisualStudy()=>BuildNativeRoadStudy(true);
    static void BuildNativeRoadStudy(bool live) {
        var scenes=LoadIsland();var all=scenes.SelectMany(All).ToArray();var shops=all.Where(t=>P(t)=="Root/Comps/GabeMarket"||P(t)=="Root/Comps/MarinaMarket").ToArray();
        var inv=JsonUtility.FromJson<PlanningInventory>(File.ReadAllText(Path.Combine(Reports,"town-road-planning-inventory.json")));var shopCopies=shops.Select(t=>{var copy=UnityEngine.Object.Instantiate(t.gameObject);copy.name=t.name;copy.transform.SetParent(null);copy.transform.position=inv.shops.Single(x=>x.path==P(t)).position;copy.transform.rotation=Quaternion.identity;return copy;}).ToArray();
        foreach(var copy in shopCopies)foreach(var m in copy.GetComponentsInChildren<MonoBehaviour>(true).Reverse())if(m)UnityEngine.Object.DestroyImmediate(m);
        var study=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);foreach(var copy in shopCopies)UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy,study);foreach(var scene in scenes)EditorSceneManager.CloseScene(scene,true);UnityEngine.SceneManagement.SceneManager.SetActiveScene(study);
        var catalog=JsonUtility.FromJson<TownPlan>(File.ReadAllText(Path.Combine(Reports,"town-module-export.json")));int count=0;
        foreach(var row in catalog.objects.Where(r=>r.category=="RoadTiles"||r.category=="PavingAndCurbs"||r.category=="RoadMarkings"||r.action=="KEEP_MAIN_ROAD")){var source=AssetDatabase.LoadAssetAtPath<GameObject>(row.prefab);if(!source)throw new Exception("Original road prefab missing: "+row.prefab);var copy=UnityEngine.Object.Instantiate(source);copy.name=row.path.Split('/').Last();copy.transform.SetPositionAndRotation(row.position,row.rotation);copy.transform.localScale=row.scale;count++;}
        var rows=new List<NativeGroundMesh>();
        foreach(var store in shopCopies)foreach(var f in store.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh)) {
            var mesh=f.sharedMesh;var vertices=mesh.vertices;var renderer=f.GetComponent<Renderer>();if(!renderer)continue;var b=IslandComprehensiveRepair.BoundsOf(f.transform);if(b.min.y<2.7f||b.min.y>3.5f)continue;
            for(int sub=0;sub<mesh.subMeshCount;sub++){var material=renderer.sharedMaterials.ElementAtOrDefault(sub);var triangles=mesh.GetTriangles(sub);var top=new List<Vector3>();for(int i=0;i<triangles.Length;i+=3){var a=f.transform.TransformPoint(vertices[triangles[i]]);var c=f.transform.TransformPoint(vertices[triangles[i+1]]);var d=f.transform.TransformPoint(vertices[triangles[i+2]]);var n=Vector3.Cross(c-a,d-a).normalized;if(n.y<.95f||Mathf.Max(a.y,Mathf.Max(c.y,d.y))>3.5f)continue;top.AddRange(new[]{a,c,d});}if(top.Count>0)rows.Add(new NativeGroundMesh{store=store.name,path=P(f.transform),mesh=AssetDatabase.GetAssetPath(mesh),material=material?material.name:"null",min=b.min,max=b.max,triangles=top.ToArray()});}
        }
        var lamp=new GameObject("StudySun").AddComponent<Light>();lamp.type=LightType.Directional;lamp.intensity=1.4f;lamp.transform.rotation=Quaternion.Euler(45,-30,0);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);RenderSettings.sun=lamp;
        var camera=new GameObject("NativeRoadReferenceCamera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.29f,.43f,.18f);camera.farClipPlane=500;camera.nearClipPlane=.1f;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        if(!live){StudyPhoto(camera,new Vector3(25,180,-28),new Vector3(25,0,-28),74,"source-native-road-network.png");
        StudyPhoto(camera,new Vector3(4,22,15),new Vector3(-5,3,-6),0,"source-native-greenmarket-join.png");
        StudyPhoto(camera,new Vector3(18,22,-3),new Vector3(-11,3,-19),0,"source-native-fishstore-corner.png");}
        File.WriteAllText(Path.Combine(Reports,"town-native-road-and-store-study.json"),JsonUtility.ToJson(new NativeRoadStudy{utc=DateTime.UtcNow.ToString("o"),source="Original native road/pavement/marking prefabs and authored instance poses archived before town removal; stores reset to original source poses for this reference render",roadAndPavingInstances=count,storeGroundMeshes=rows.ToArray()},true));
        if(live){NativeRoadStudyPlayCapture.Begin(camera);return;}
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");Debug.Log("NATIVE_ROAD_STUDY instances="+count+" storeGroundSurfaces="+rows.Count);
    }
}
