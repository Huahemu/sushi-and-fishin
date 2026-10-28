using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SushiAndFish.FishingIntegration;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Only reads the real prefab and renders static geometry/UI. Never enters
// Play or binds a game world, starts a player, or steps boat/physics logic.
public static class VerifyBoatCompassScreen
{
    const string Out="D:/UnityProjects/_CodexWork/BoatScreenCompass-20261028/";
    static readonly List<string> checks=new List<string>();
    static void Check(bool condition,string label)
    {
        checks.Add((condition?"PASS ":"FAIL ")+label);File.WriteAllLines(Out+"Checks.txt",checks);
        if(!condition)throw new Exception(label);
    }
    public static void Run()
    {
        Directory.CreateDirectory(Out);Check(!EditorApplication.isPlaying,"Static prefab/UI inspection only, without gameplay or physics simulation");
        var settings=ArchipelagoSettings.Load();var config=MigrationConfig.Load();
        var model=Object.Instantiate(settings.boatPrefab);model.name="Static original boat screen preview";
        var display=new BoatCompassDisplay();
        try
        {
            var anchor=model.transform.Find(BoatCompassDisplay.AnchorPath);
            Check(anchor,"The imported HowToFish boat retains the original MapCanvas screen anchor");
            var mesh=model.transform.Find("Boat/VisualPhysicsBoat/VisualBoat/Radar/Map").GetComponent<MeshFilter>();
            Check(mesh&&mesh.sharedMesh,"Original black screen and bezel mesh remain present");
            display.Render(model,config,true,settings.regions);
            Check(display.Screen&&display.Screen.renderMode==RenderMode.WorldSpace&&display.Screen.transform.parent==anchor,
                "Compass is mounted in world space directly under the original boat screen, rather than the screen overlay");
            var oldMesh=mesh.sharedMesh;var oldMaterial=mesh.GetComponent<Renderer>().sharedMaterial;
            Check(!display.Screen.GetComponent<GraphicRaycaster>()&&!display.Screen.GetComponent<Camera>()
                &&display.Screen.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget),"Physical display has no raycaster or extra camera and cannot intercept boat controls");
            Check(display.Compass.GetComponentsInChildren<TMP_Text>().Select(t=>t.text).OrderBy(t=>t)
                .SequenceEqual(new[]{"北","东","南","西"}.OrderBy(t=>t)),"Only four compass directions remain; all island names and the legend are removed");
            var dots=display.Compass.GetComponentsInChildren<Image>().Where(i=>i.name.Contains("original map dot")).ToArray();
            Check(dots.Length==3,"Each island still has exactly one original radar dot");
            var corners=new Vector3[4];display.Compass.Bounds.GetWorldCorners(corners);
            Check(corners.Select(mesh.transform.InverseTransformPoint).All(p=>Mathf.Abs(p.x)<.14f&&Mathf.Abs(p.y)<.105f),
                "The complete compass stays inside the real black screen aperture, clear of the original bezel");
            Check(corners.Select(mesh.transform.InverseTransformPoint).All(p=>p.z>.003f&&p.z<.006f),
                "Display sits just ahead of the screen surface, avoiding z fighting");
            var before=display.Screen.GetInstanceID();display.Render(model,config,true,settings.regions);display.Render(model,config,true,settings.regions);
            Check(display.Screen.GetInstanceID()==before&&anchor.GetComponentsInChildren<Canvas>().Length==1,
                "Repeated HUD updates reuse one mounted canvas without duplicating displays");
            Render(model,display,"helm",true);
            Render(model,display,"screen-close",false);
            var localPosition=display.Screen.transform.localPosition;var localRotation=display.Screen.transform.localRotation;
            model.transform.SetPositionAndRotation(new Vector3(30,5,-20),Quaternion.Euler(8,123,-4));
            display.Render(model,config,true,settings.regions);
            Check(display.Screen.transform.localPosition==localPosition&&Quaternion.Angle(display.Screen.transform.localRotation,localRotation)<.001f,
                "Boat movement and tilt preserve the authored screen attachment and alignment");
            var eastDot=dots[2].rectTransform.anchoredPosition.normalized;
            var expected=SailingCompassMath.Direction(model.transform.position,settings.regions[2].dock,model.transform.eulerAngles.y);
            Check(Vector2.Distance(eastDot,expected)<.001f,"Physical compass bearing uses the boat heading, independent of where the player looks");
            Render(model,display,"turned-screen",false);
            display.Render(model,config,false,settings.regions);
            Check(!display.Screen.enabled,"Disembarking or opening the tablet powers down the boat display");
            display.Render(model,config,true,settings.regions);
            Check(display.Screen.enabled&&display.Screen.GetInstanceID()==before,"Reboarding powers up the same display");
            display.Dispose();
            Check(!display.Screen&&anchor.GetComponentsInChildren<Canvas>().Length==0&&mesh.sharedMesh==oldMesh
                &&mesh.GetComponent<Renderer>().sharedMaterial==oldMaterial,"Cleanup removes only the new display, retaining original boat geometry and material");
            display.Render(model,config,true,settings.regions);display.Render(null,config,false,null);
            Check(!display.Screen&&anchor.GetComponentsInChildren<Canvas>().Length==0,"Losing the boat removes its canvas without leaving a stale HUD");
            string hud=File.ReadAllText("Assets/SushiAndFish/FishingIntegration/Runtime/FishingHud.cs");
            Check(!hud.Contains("Original HowToFish sailing compass")&&hud.Contains("boatCompass.Render(")&&hud.Contains("aboard&&!blocked")
                &&hud.Contains("void OnDisable(){boatCompass.Dispose();}")&&hud.Contains("void OnDestroy(){boatCompass.Dispose();"),
                "Local HUD owns physical-display visibility and cleanup; the old right-side floating compass is no longer constructed");
        }
        finally{display.Dispose();Object.DestroyImmediate(model.gameObject);}
        Debug.Log("BOAT_COMPASS_SCREEN_VERIFIED checks="+checks.Count+" play=false simulation=false");
    }
    static void Render(OriginalBoatRig model,BoatCompassDisplay display,string name,bool helm)
    {
        var all=model.GetComponentsInChildren<Transform>(true);var layers=all.Select(t=>t.gameObject.layer).ToArray();
        foreach(var t in all)t.gameObject.layer=29;
        var cameraObject=new GameObject("Static boat screen camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
        camera.cullingMask=1<<29;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.3f,.5f,.55f);camera.nearClipPlane=.01f;camera.fieldOfView=65;
        var screen=display.Screen.transform;
        camera.transform.position=helm?model.driverSeat.position+Vector3.up*1.2f:screen.position-screen.forward*.4f;
        camera.transform.LookAt(screen.position,helm?Vector3.up:screen.up);
        var lightObject=new GameObject("Static boat preview sunlight");var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;
        lightObject.transform.rotation=Quaternion.Euler(40,-35,0);
        var target=new RenderTexture(1400,1000,24);camera.targetTexture=target;
        try
        {
            foreach(var text in display.Compass.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate(true,true);
            Canvas.ForceUpdateCanvases();camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Out+name+".png",image.EncodeToPNG());
            Object.DestroyImmediate(image);RenderTexture.active=previous;
            File.AppendAllText(Out+"ScreenPose.txt",name+": screen="+screen.position+" front="+(-screen.forward)+" up="+screen.up+" camera="+camera.transform.position+"\n");
        }
        finally
        {
            for(int i=0;i<all.Length;i++)all[i].gameObject.layer=layers[i];camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);
            Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(lightObject);
        }
    }
}
