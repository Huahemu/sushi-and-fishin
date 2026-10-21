using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PaperPixelGames.Core.Tablet;

public static class TownMapDiagnostics
{
    public static void Capture() {
        var lines=new List<string>{DateTime.UtcNow.ToString("o"),"screen="+Screen.width+"x"+Screen.height};
        foreach(var camera in Camera.allCameras) {
            var data=camera.GetUniversalAdditionalCameraData();var so=new SerializedObject(data);
            lines.Add("CAMERA "+camera.name+" pos="+camera.transform.position+" type="+camera.cameraType+" near="+camera.nearClipPlane+" far="+camera.farClipPlane+" ortho="+camera.orthographic+" mask="+camera.cullingMask+" target="+(camera.targetTexture?camera.targetTexture.name:"screen")+" renderer="+so.FindProperty("m_RendererIndex")?.intValue+" pipeline="+GraphicsSettings.currentRenderPipeline?.name+" mode="+data.renderType);
        }
        foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(canvas.GetComponentInChildren<MapApp>(true)||canvas.GetComponentsInParent<LiveSceneTabletMap>(true).Length>0||canvas.name.Contains("Tablet")) {
                var ray=canvas.GetComponent<GraphicRaycaster>();var rect=canvas.transform as RectTransform;
                lines.Add("CANVAS "+IslandFrontEndInventory.PathOf(canvas.transform)+" active="+canvas.gameObject.activeInHierarchy+" enabled="+canvas.enabled+" mode="+canvas.renderMode+" worldCamera="+canvas.worldCamera?.name+" raycamera="+ray?.eventCamera?.name+" rayActive="+ray?.isActiveAndEnabled+" rect="+rect?.rect+" pos="+rect?.position+" graphics="+GraphicRegistry.GetGraphicsForCanvas(canvas).Count);
            }
        foreach(var app in UnityEngine.Object.FindObjectsByType<MapApp>(FindObjectsInactive.Include,FindObjectsSortMode.None)) {
            foreach(var rect in app.GetComponentsInChildren<RectTransform>(true).Where(t=>t.name.Contains("Content")||t.name.Contains("Viewport")||t.name.Contains("MapBackground")||t.name.Contains("LiveScene")))lines.Add("RECT "+rect.name+" rect="+rect.rect+" world="+rect.position+" local="+rect.anchoredPosition+" scale="+rect.lossyScale+" layer="+rect.gameObject.layer+" groups="+string.Join(";",rect.GetComponentsInParent<CanvasGroup>(true).Select(g=>g.name+":"+g.alpha+":"+g.blocksRaycasts+":"+g.interactable)));
        }
        foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if(renderer.sharedMaterials.Any(m=>m&&m.shader&&(m.shader.name.Contains("Waterline")||m.shader.name.Contains("Underwater")||m.shader.name.Contains("InternalError"))))lines.Add("EFFECT "+IslandFrontEndInventory.PathOf(renderer.transform)+" layer="+renderer.gameObject.layer+" bounds="+renderer.bounds+" enabled="+renderer.enabled+" forceOff="+renderer.forceRenderingOff+" shaders="+string.Join(";",renderer.sharedMaterials.Select(m=>m?m.shader.name:"null")));
        lines.Add("EVENTS "+string.Join(";",UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(e=>e.name+":"+e.isActiveAndEnabled)));
        File.WriteAllLines(Path.Combine(IslandRevision20261018.Reports,"town-map-runtime-diagnostics.txt"),lines);
    }
}
