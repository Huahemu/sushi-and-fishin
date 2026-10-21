using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using GraphicsSettings=UnityEngine.Rendering.GraphicsSettings;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.Patterns;
using SushiAndFish.CityExpansion;
using SushiAndFish.Casino;
using SushiAndFish.Casino.Original;
using Object=UnityEngine.Object;

public static partial class CityExpansionInstallation
{
    public static void RenameAndProbe(){RenameCasinoRegion();CityExpansionPlayAudit.VisualProbe();}
}
public static partial class CityExpansionPlayAudit
{
    [Serializable]public class VisualProbeReceipt {public string utc;public List<string> details=new List<string>();public List<string> captures=new List<string>();}
    static VisualProbeReceipt probe;
    static void ProbeWrite()=>File.WriteAllText(Path.Combine(Reports,"city-visual-probe.json"),JsonUtility.ToJson(probe,true));
    static void DumpTexture(RenderTexture rt,string name)
    {
        var old=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);t.Apply();File.WriteAllBytes(Path.Combine(Reports,name),t.EncodeToPNG());Object.Destroy(t);RenderTexture.active=old;
    }
    static string Pose(Animator animator)
    {
        string value="enabled="+animator.enabled+" speed="+animator.speed+" cull="+animator.cullingMode+" avatar="+animator.avatar?.isValid+"/"+animator.avatar?.isHuman+" controller="+animator.runtimeAnimatorController?.name+" state="+animator.GetCurrentAnimatorStateInfo(0).fullPathHash+" clip="+string.Join(",",animator.GetCurrentAnimatorClipInfo(0).Select(c=>c.clip.name+"@"+c.weight));
        if(animator.isHuman)
        {
            var hip=animator.GetBoneTransform(HumanBodyBones.Hips);var upper=animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);var lower=animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);var foot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            value+=" hipY="+(hip.position.y-animator.transform.position.y)+" thighAngle="+Vector3.Angle(lower.position-upper.position,Vector3.down)+" kneeAngle="+Vector3.Angle(upper.position-lower.position,foot.position-lower.position);
        }
        return value;
    }
    static async UniTask ProbeVisuals(ParkCasinoIntegration casino)
    {
        probe=new VisualProbeReceipt{utc=DateTime.UtcNow.ToString("o")};city=new WorldPlayReceipt();
        var scheduler=Object.FindFirstObjectByType<TownMealScheduler>();scheduler.enabled=false;
        var clock=Singleton<GameProgressManager>.Instance.CozyWeatherManager;clock.ClientPauseTime();clock.ClientSetTime(12,0);clock.TriggerBlackBoardTimeEvents(true);
        await Pause(1);
        var seat=scheduler.seats.First(s=>s.barStool);var npc=Eligible(scheduler).First();
        var animator=CityExpansionInspection.Field<Animator>(npc,"_animator");var original=animator.runtimeAnimatorController;
        try
        {
            foreach(var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))probe.details.Add("CAMERA "+camera.name+" enabled="+camera.enabled+" mask="+camera.cullingMask+" texture="+camera.targetTexture?.name);
            npc.Brain.Movement.Warp(new Vector3(68,3.1f,-13),Quaternion.identity);seat.TryReserve(npc);
            var state=new ResidentTownMealState(npc,seat,scheduler,scheduler.CurrentDay,25);npc.Brain.StateMachine.ChangeState(state,ResidentRequestPriority.ScheduledActivity);await Wait(()=>state.Seated,30);await Pause(2);
            probe.details.Add("NORMAL "+Pose(animator));await ObserveMeal(casino,new[]{seat});await Snapshot("city-probe-npc-normal.png");ProbeWrite();
            animator.Play("Sit",0,0);animator.Update(0);await Pause(1);
            probe.details.Add("FORCED_NATIVE "+Pose(animator));await Snapshot("city-probe-npc-force-native.png");ProbeWrite();
            foreach(string clipPath in Array.Empty<string>())
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                probe.details.Add("ASSET "+clipPath+" humanMotion="+clip.humanMotion+" genericRoot="+clip.hasGenericRootTransform+" curves="+AnimationUtility.GetCurveBindings(clip).Length);
                var over=new AnimatorOverrideController(original);var entries=new List<KeyValuePair<AnimationClip,AnimationClip>>();over.GetOverrides(entries);
                probe.details.Add("OVERRIDES "+string.Join(",",entries.Select(e=>e.Key.name)));
                for(int i=0;i<entries.Count;i++)if(entries[i].Key.name=="Resident_Sit")entries[i]=new KeyValuePair<AnimationClip,AnimationClip>(entries[i].Key,clip);
                over.ApplyOverrides(entries);animator.runtimeAnimatorController=over;animator.Rebind();animator.SetBool("Sit",true);animator.Play("Sit",0,0);animator.Update(0);await Pause(1);
                probe.details.Add("CANDIDATE "+clip.name+" "+Pose(animator));await Snapshot(clipPath.Contains("HumanM")?"city-probe-npc-player-clip.png":"city-probe-npc-customer-clip.png");
                animator.runtimeAnimatorController=original;Object.Destroy(over);ProbeWrite();
            }
            npc.Brain.RequestScheduleChange();await Pause(.2f);
            await OpenNpc(casino);var panel=Singleton<GameUIManager>.Instance.DialoguePanelController;await Wait(()=>!CityExpansionInspection.Field<bool>(panel,"_isTyping"),10);
            var billboard=CityExpansionInspection.Field<PaperPixelGames.Core.Ai.NPCs.ResidentBillboardText>(casino.elevatorConversation,"billboard");
            var text=CityExpansionInspection.Field<TMPro.TextMeshProUGUI>(billboard,"_text");
            var elevatorNpc=casino.ModuleRoot.GetComponentInChildren<SushiAndFish.Casino.Original.ElevatorGuy>(true);
            foreach(var r in casino.ModuleRoot.GetComponentsInChildren<Renderer>(true))
            {r.ResetBounds();}
            foreach(var r in elevatorNpc.GetComponentsInChildren<Renderer>(true))
            {
                probe.details.Add("ELEVATOR_RENDERER "+r.name+" type="+r.GetType().Name+" transform="+r.transform.position+" enabled="+r.enabled+" forceOff="+r.forceRenderingOff+" layer="+r.gameObject.layer+" active="+r.gameObject.activeInHierarchy+" bounds="+r.bounds+" materials="+string.Join(",",r.sharedMaterials.Select(m=>m?.name+"="+m?.shader?.name)));
                if(r is SkinnedMeshRenderer skin)probe.details.Add("SKIN "+r.name+" root="+skin.rootBone?.name+" rootPosition="+skin.rootBone?.position+" boneCount="+skin.bones.Length+" outside="+skin.bones.Count(b=>b&&!b.IsChildOf(casino.ModuleRoot))+" persistent="+skin.bones.Count(b=>b&&EditorUtility.IsPersistent(b)));
            }
            foreach(var r in casino.ModuleRoot.GetComponentsInChildren<Renderer>(true).Where(r=>Vector3.Distance(r.bounds.center,elevatorNpc.transform.position)<3f*casino.CurrentScale))probe.details.Add("NEAR_ELEVATOR "+CityExpansionInspection.PathOf(r.transform)+" layer="+r.gameObject.layer+" active="+r.gameObject.activeInHierarchy+" enabled="+r.enabled+" bounds="+r.bounds);
            probe.details.Add("BILLBOARD pos="+billboard.transform.position+" scale="+billboard.transform.lossyScale+" textPos="+text.transform.position+" text="+text.text+" root="+text.gameObject.activeInHierarchy+" canvas="+text.GetComponentInParent<Canvas>().enabled+" layer="+text.gameObject.layer+" cameraMask="+CasinoRuntimeContext.PlayerCamera.cullingMask);
            await Snapshot("city-probe-elevator-caption.png");panel.Hide();ProbeWrite();
            var tablet=casino.HostPlayer.GetComponentInChildren<TabletController>(true);InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.M));await Pause(.1f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Wait(()=>tablet.IsTabletActive,10);await Pause(.5f);
            var live=tablet.MapApplication.GetComponent<LiveSceneTabletMap>();
            RenderPipeline.SubmitRenderRequest(live.MapCamera,new RenderPipeline.StandardRequest{destination=live.Texture});
            await Pause(1.5f);
            int priorFrames=live.RenderedFrames;await Pause(.5f);probe.details.Add("MAP_FRAMES automaticDelta="+(live.RenderedFrames-priorFrames));
            bool srp=GraphicsSettings.useScriptableRenderPipelineBatching;GraphicsSettings.useScriptableRenderPipelineBatching=false;
            RenderPipeline.SubmitRenderRequest(live.MapCamera,new RenderPipeline.StandardRequest{destination=live.Texture});DumpTexture(live.Texture,"city-probe-map-without-srp.png");GraphicsSettings.useScriptableRenderPipelineBatching=srp;
            int mapMask=live.MapCamera.cullingMask;
            foreach(var r in casino.permanentExterior.GetComponentsInChildren<Renderer>(true))live.MapCamera.cullingMask|=1<<r.gameObject.layer;
            RenderPipeline.SubmitRenderRequest(live.MapCamera,new RenderPipeline.StandardRequest{destination=live.Texture});DumpTexture(live.Texture,"city-probe-map-all-casino-layers.png");
            live.MapCamera.cullingMask=mapMask;
            var tabletCanvas=tablet.GetComponentInParent<Canvas>();
            var actualCanvas=tablet.GetComponent<Canvas>();if(actualCanvas)tabletCanvas=actualCanvas;
            probe.details.Add("TABLET_CANVAS "+tabletCanvas?.name+" rect="+(tabletCanvas?((RectTransform)tabletCanvas.transform).rect.ToString():"none")+" camera="+tabletCanvas?.worldCamera?.name);
            var vm=tabletCanvas.worldCamera;
            foreach(var g in tabletCanvas.GetComponentsInChildren<UnityEngine.UI.Graphic>().Where(g=>g.name=="LiveSceneView"||g is TMPro.TMP_Text&&g.name=="Caption"||g.name=="Header"||g.name=="Body"))
            {
                var corners=new Vector3[4];g.rectTransform.GetWorldCorners(corners);
                probe.details.Add("GRAPHIC "+g.name+" layer="+g.gameObject.layer+" cull="+g.canvasRenderer.cull+" alpha="+g.canvasRenderer.GetAlpha()+" inheritedAlpha="+g.canvasRenderer.GetInheritedAlpha()+" color="+g.color+" world="+g.transform.position+" view="+vm.WorldToViewportPoint(g.transform.position)+" shader="+g.material?.shader?.name+" rect="+g.rectTransform.rect);
            }
            foreach(var group in tabletCanvas.GetComponentsInChildren<CanvasGroup>(true))probe.details.Add("UI_GROUP "+CityExpansionInspection.PathOf(group.transform)+" active="+group.gameObject.activeInHierarchy+" alpha="+group.alpha);
            var layers=tabletCanvas.GetComponentsInChildren<Transform>(true).Select(t=>(t.gameObject,t.gameObject.layer)).ToArray();
            foreach(var p in layers)p.gameObject.layer=10;
            await Snapshot("city-probe-map-unified-ui-layers.png");
            foreach(var p in layers)p.gameObject.layer=p.layer;
            foreach(var group in tabletCanvas.GetComponentsInParent<CanvasGroup>(true))probe.details.Add("ANCESTOR_GROUP "+CityExpansionInspection.PathOf(group.transform)+" alpha="+group.alpha);
            var vmData=vm.GetUniversalAdditionalCameraData();int rendererIndex=new SerializedObject(vmData).FindProperty("m_RendererIndex").intValue;
            vmData.SetRenderer(0);await Snapshot("city-probe-map-main-renderer.png");vmData.SetRenderer(rendererIndex);
            var vmCopyObject=new GameObject("TemporaryStandaloneViewModelProbe");var vmCopy=vmCopyObject.AddComponent<Camera>();vmCopy.CopyFrom(vm);vmCopy.transform.SetPositionAndRotation(vm.transform.position,vm.transform.rotation);
            vmCopy.GetUniversalAdditionalCameraData().SetRenderer(rendererIndex);
            var vmTarget=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32);vmTarget.Create();
            RenderPipeline.SubmitRenderRequest(vmCopy,new RenderPipeline.StandardRequest{destination=vmTarget});DumpTexture(vmTarget,"city-probe-viewmodel-standalone.png");Object.Destroy(vmCopyObject);vmTarget.Release();Object.Destroy(vmTarget);
            var main=CasinoRuntimeContext.PlayerCamera;var mainData=main.GetUniversalAdditionalCameraData();
            var stackBefore=mainData.cameraStack.ToArray();var mainTarget=main.targetTexture;var overlayTarget=vm.targetTexture;
            var combinedTarget=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32);combinedTarget.Create();
            try
            {
                main.targetTexture=vm.targetTexture=combinedTarget;
                vmData.renderType=CameraRenderType.Base;Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(vm,new RenderPipeline.StandardRequest{destination=combinedTarget});
                vmData.renderType=CameraRenderType.Overlay;
                RenderPipeline.SubmitRenderRequest(main,new RenderPipeline.StandardRequest{destination=combinedTarget});DumpTexture(combinedTarget,"city-probe-map-overlay-prepass.png");
                mainData.cameraStack.Clear();
                RenderPipeline.SubmitRenderRequest(main,new RenderPipeline.StandardRequest{destination=combinedTarget});
                RenderPipeline.SubmitRenderRequest(vm,new UniversalRenderPipeline.SingleCameraRequest{destination=combinedTarget});DumpTexture(combinedTarget,"city-probe-map-single-overlay.png");
                RenderPipeline.SubmitRenderRequest(main,new RenderPipeline.StandardRequest{destination=combinedTarget});
                var clear=vm.clearFlags;vm.clearFlags=CameraClearFlags.Nothing;vmData.renderType=CameraRenderType.Base;
                RenderPipeline.SubmitRenderRequest(vm,new RenderPipeline.StandardRequest{destination=combinedTarget});DumpTexture(combinedTarget,"city-probe-map-sequential-base.png");
                vm.clearFlags=clear;
            }
            finally
            {
                vmData.renderType=CameraRenderType.Overlay;mainData.cameraStack.Clear();mainData.cameraStack.AddRange(stackBefore);
                main.targetTexture=mainTarget;vm.targetTexture=overlayTarget;combinedTarget.Release();Object.Destroy(combinedTarget);
            }
            var oldPosition=tabletCanvas.transform.position;tabletCanvas.transform.position-=vm.transform.forward*.025f;
            Canvas.ForceUpdateCanvases();await Snapshot("city-probe-map-ui-in-front.png");tabletCanvas.transform.position=oldPosition;
            var hardware=tablet.transform.parent.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled).ToArray();
            foreach(var r in hardware)probe.details.Add("TABLET_HARDWARE "+CityExpansionInspection.PathOf(r.transform)+" materials="+string.Join(",",r.sharedMaterials.Select(m=>m?.name+"="+m?.shader?.name+" q"+m?.renderQueue)));
            foreach(var r in hardware)r.enabled=false;await Snapshot("city-probe-map-ui-without-hardware.png");foreach(var r in hardware)r.enabled=true;
            foreach(var g in tabletCanvas.GetComponentsInChildren<UnityEngine.UI.Graphic>().Where(g=>g.name=="LiveSceneView"||g is TMPro.TMP_Text&&g.name=="Caption"))probe.details.Add("UI_MESH "+g.name+" vertices="+g.canvasRenderer.GetMesh()?.vertexCount+" root="+g.canvas.rootCanvas.name+" rootEnabled="+g.canvas.rootCanvas.enabled+" actualMat="+g.materialForRendering?.shader?.name+" depth="+g.canvasRenderer.absoluteDepth);
            var oldMode=tabletCanvas.renderMode;var oldCam=tabletCanvas.worldCamera;tabletCanvas.renderMode=RenderMode.ScreenSpaceOverlay;Canvas.ForceUpdateCanvases();await Snapshot("city-probe-map-flat-canvas.png");tabletCanvas.renderMode=oldMode;tabletCanvas.worldCamera=oldCam;
            foreach(var r in casino.ModuleRoot.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.IndexOf("Pillar",StringComparison.OrdinalIgnoreCase)>=0).Take(4))probe.details.Add("PILLAR "+CityExpansionInspection.PathOf(r.transform)+" staticBatch="+r.isPartOfStaticBatch+" pos="+r.transform.position+" bounds="+r.bounds+" localMesh="+r.GetComponent<MeshFilter>()?.sharedMesh?.bounds);
            var raw=tabletCanvas.GetComponentsInChildren<UnityEngine.UI.RawImage>().Single(r=>r.name=="LiveSceneView");
            var oldMaterial=raw.material;var testMaterial=new Material(Shader.Find("SushiAndFish/WorldTabletUI"));raw.material=testMaterial;await Snapshot("city-probe-map-editable-ui-shader.png");raw.material=oldMaterial;Object.Destroy(testMaterial);
            var pipeline=new SerializedObject(UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset);var renderers=pipeline.FindProperty("m_RendererDataList");
            for(int i=0;i<renderers.arraySize;i++){var renderer=renderers.GetArrayElementAtIndex(i).objectReferenceValue;if(!renderer)continue;var s=new SerializedObject(renderer);probe.details.Add("PIPE_RENDERER "+renderer.name+" transparent="+s.FindProperty("m_TransparentLayerMask")?.intValue+" opaque="+s.FindProperty("m_OpaqueLayerMask")?.intValue);}
            foreach(var r in casino.ModuleRoot.GetComponentsInChildren<Renderer>().Where(r=>r.bounds.size.magnitude>25).Take(15))probe.details.Add("LARGE_CASINO "+CityExpansionInspection.PathOf(r.transform)+" static="+r.isPartOfStaticBatch+" pos="+r.transform.position+" bounds="+r.bounds+" type="+r.GetType().Name);
            foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c.enabled))probe.details.Add("STACK "+c.name+" type="+c.GetUniversalAdditionalCameraData().renderType+" target="+c.targetTexture?.name+" cameras="+string.Join(",",c.GetUniversalAdditionalCameraData().cameraStack?.Where(x=>x).Select(x=>x.name)??Enumerable.Empty<string>()));
            foreach(var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))probe.details.Add("CANVAS "+CityExpansionInspection.PathOf(c.transform)+" mode="+c.renderMode+" enabled="+c.enabled+" camera="+c.worldCamera?.name+" layer="+c.gameObject.layer+" scale="+c.transform.lossyScale);
            probe.details.Add("MAP_CANVAS "+tablet.MapApplication.GetComponentInParent<Canvas>().name+" enabled="+tablet.MapApplication.GetComponentInParent<Canvas>().enabled+" path="+CityExpansionInspection.PathOf(tablet.MapApplication.transform));
            foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c.enabled&&c.targetTexture))probe.details.Add("RTCAM "+c.name+" renderer="+new SerializedObject(c.GetUniversalAdditionalCameraData()).FindProperty("m_RendererIndex").intValue+" type="+c.GetUniversalAdditionalCameraData().renderType+" mask="+c.cullingMask+" target="+c.targetTexture.name);
            await Snapshot("city-probe-map-composite.png");
            DumpTexture(live.Texture,"city-probe-map-raw.png");
            foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c.enabled&&c.targetTexture&&c.name=="GameUICamera"))DumpTexture(c.targetTexture,"city-probe-tablet-ui-raw.png");
            tablet.CloseTablet();await Pause(.8f);
            probe.captures.AddRange(city.captures);ProbeWrite();
        }
        finally{animator.runtimeAnimatorController=original;npc.Brain.RequestScheduleChange();clock.ClientResumeTime();ProbeWrite();}
    }
}
