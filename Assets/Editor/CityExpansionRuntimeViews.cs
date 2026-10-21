using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PaperPixelGames.Core.Tablet;
using SushiAndFish.Casino;
using SushiAndFish.Casino.Original;
using Object=UnityEngine.Object;

public static partial class CityExpansionPlayAudit
{
    static async UniTask CaptureTabletView(TabletController tablet,string name)
    {
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
        var source=tablet.GetComponent<Canvas>().worldCamera;
        var go=new GameObject("CityAuditNativeTabletCamera");var copy=go.AddComponent<Camera>();copy.CopyFrom(source);copy.enabled=false;
        copy.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
        copy.GetUniversalAdditionalCameraData().SetRenderer(new SerializedObject(source.GetUniversalAdditionalCameraData()).FindProperty("m_RendererIndex").intValue);
        var rt=RenderTexture.GetTemporary(1440,900,24,RenderTextureFormat.ARGB32);
        try
        {
            OriginalNativeShaderRestoration.ApplyPending();RecoveryShaderSupport.BeginRendering();Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(copy,new RenderPipeline.StandardRequest{destination=rt});DumpTexture(rt,name);
            city.captures.Add(name);
            city.diagnostics.Add("TABLET_CAPTURE uses an independent Base camera with the unchanged native ViewModelCam pose, projection, renderer and layer mask. The host offscreen Overlay stack omits WorldSpace Canvas; its grey-screen capture is excluded. This image verifies the real native tablet UI and live map, without claiming a desktop Game view screenshot.");
        }
        finally{RenderTexture.ReleaseTemporary(rt);Object.Destroy(go);RecoveryShaderSupport.EndRendering();}
    }
    static async UniTask RuntimeCityViews(ParkCasinoIntegration casino)
    {
        var player=casino.HostPlayer;
        foreach(var view in new[]{
            ("city-restaurant-exterior.png",new Vector3(64.3f,3.06f,-13),new Vector3(72,4.5f,-13)),
            ("city-cinema-exterior.png",new Vector3(98,3.06f,-52.5f),new Vector3(88,4.8f,-52.5f)),
            ("city-perimeter-east-stairs.png",new Vector3(111,3.06f,-17),new Vector3(121,-1,-14.25f)),
            ("city-perimeter-south.png",new Vector3(48,3.06f,-74),new Vector3(48,-1,-88)),
            ("city-coastal-road-end.png",new Vector3(81,3.06f,-67),new Vector3(106,2,-71)),
            ("city-casino-exterior-final.png",new Vector3(-101,3.06f,-72),new Vector3(-101,9,-41))})
        {
            player.TeleportPlayer(view.Item2,true);player.FirstPersonCharacter.SetVelocity(Vector3.zero);await Pause(.3f);await Aim(casino,view.Item3);await Snapshot(view.Item1);
        }
        var live=player.GetComponentInChildren<MapApp>(true).GetComponent<LiveSceneTabletMap>();
        if(!live||!live.Texture)throw new Exception("The actual live-map overview is unavailable.");
        DumpTexture(live.Texture,"city-overview-final.png");city.captures.Add("city-overview-final.png");
        city.diagnostics.Add("OVERVIEW uses the unchanged output of the actual live-map camera from the preceding map check. Ground-level exterior and perimeter pictures use the actual host player camera. No scene geometry or materials are changed for these captures.");
    }
}
