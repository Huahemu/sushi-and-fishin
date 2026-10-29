using System.IO;
using System.Linq;
using SushiAndFish.FishingIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class PreviewSharedFreezers
{
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new System.Exception("Static UI/model render only");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var c=MigrationConfig.Load();var settings=ArchipelagoSettings.Load();
        var light=new GameObject("Preview light",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(40,-40,0);RenderSettings.ambientLight=Color.gray;
        var camera=new GameObject("Static preview camera",typeof(Camera)).GetComponent<Camera>();camera.backgroundColor=new Color(.65f,.79f,.82f);camera.clearFlags=CameraClearFlags.SolidColor;
        for(int i=1;i<3;i++){
            var region=settings.regions[i];var point=c.freezerPoints[i-1];var island=Object.Instantiate(region.prefab,region.center,Quaternion.identity);
            var freezer=Object.Instantiate(c.freezerPrefab,point.position,point.rotation);var access=Object.Instantiate(settings.accessPrefab);
            camera.transform.position=point.position+point.rotation*new Vector3(6,4,8);camera.transform.LookAt(point.position+Vector3.up);
            Capture(camera,"island"+i+"-freezer.png");Object.DestroyImmediate(island);Object.DestroyImmediate(freezer);Object.DestroyImmediate(access);
        }
        var go=new GameObject("Static freezer UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));var ui=go.AddComponent<FishingFreezerPanel>();
        ui.BuildStaticPreview(c,c.species.Take(4).Select((b,i)=>new WarehouseLine{itemId=(i==1&&b.shiny?b.shiny:b.original).Id,count=i+1}).ToArray());
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=5;
        var rt=new RenderTexture(1600,1000,24);camera.targetTexture=rt;Canvas.ForceUpdateCanvases();
        foreach(var rect in go.GetComponentsInChildren<RectTransform>())LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        Canvas.ForceUpdateCanvases();Capture(camera,"shared-freezer-ui.png",rt);
        Object.DestroyImmediate(go);Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(light.gameObject);
        Debug.Log("SHARED_FREEZERS_PREVIEWED play=false simulation=false");
    }
    static void Capture(Camera camera,string name,RenderTexture target=null){
        var rt=target?target:new RenderTexture(1600,1000,24);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();File.WriteAllBytes(InstallSharedFreezers.Out+name,tex.EncodeToPNG());
        RenderTexture.active=old;camera.targetTexture=null;Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);
    }
}
