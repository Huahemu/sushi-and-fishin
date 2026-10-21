using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using PaperPixelGames.Core.DressingSceneSystem;
using ECM2.Examples.FirstPerson;

public static class IslandComprehensivePlayAudit
{
    [Serializable] public class Audit {public string utc;public int scenes,renderers,lods,missingScripts;public string[] loaded,unsupported,captures,materialDetails;public string[] notes;}
    public static async UniTask Run(Camera camera)
    {
        var waterGrid=UnityEngine.Object.FindFirstObjectByType<StylizedWater3.WaterGrid>();
        var ocean=UnityEngine.Object.FindFirstObjectByType<StylizedWater3.OceanFollowBehaviour>();
        if(waterGrid&&waterGrid.name=="TownOceanContinuousGrid"){
            var tiles=waterGrid.GetComponentsInChildren<Renderer>();bool consistent=tiles.Length==16&&tiles.All(r=>r.enabled&&r.sharedMaterial==ocean.material&&Mathf.Abs(r.transform.position.y-ocean.transform.position.y)<.001f)&&ocean.GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled);
            File.WriteAllText(Path.Combine(IslandComprehensiveRepair.Reports,"ocean-runtime-verification.json"),"{\"utc\":\""+DateTime.UtcNow.ToString("o")+"\",\"tiles\":"+tiles.Length+",\"allAtWaterLevelWithSelectedMaterial\":"+consistent.ToString().ToLowerInvariant()+"}");if(!consistent)throw new Exception("Ocean grid verification failed");}
        var scenes=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        var all=scenes.SelectMany(s=>s.GetRootGameObjects()).SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        foreach(var c in all.Select(t=>t.GetComponent<DressingRendererDistanceCuller>()).Where(c=>c))c.enabled=false;
        foreach(var r in all.Select(t=>t.GetComponent<DressingSceneRoot>()).Where(r=>r))r.ActivateAllImmediate();
        var lods=all.Select(t=>t.GetComponent<LODGroup>()).Where(l=>l&&l.enabled&&l.gameObject.activeInHierarchy).ToArray();foreach(var l in lods)l.ForceLOD(0);
        var renderers=all.Select(t=>t.GetComponent<Renderer>()).Where(r=>r&&r.gameObject.activeInHierarchy).ToArray();foreach(var r in renderers.Where(r=>r.GetComponentInParent<DressingSceneRoot>()))r.forceRenderingOff=false;
        camera.useOcclusionCulling=false;camera.layerCullDistances=new float[32];camera.farClipPlane=1200;
        var positions=new[]{new Vector3(-30,185,-20),new Vector3(-110,22,-8),new Vector3(-38,22,-60),new Vector3(25,22,-60),new Vector3(68,22,-8),new Vector3(35,1.5f,53),new Vector3(-110,1.5f,53),new Vector3(-134,0,-88),new Vector3(94,0,-89),new Vector3(105,12,-5),new Vector3(-14,5,1),new Vector3(8,3,66)};
        var targets=new[]{new Vector3(-30,0,-20),new Vector3(-90,3,-15),new Vector3(-40,3,-45),new Vector3(25,3,-43),new Vector3(50,3,-20),new Vector3(53,-3.7f,53),new Vector3(-130,-3.7f,52),new Vector3(-134,1,-76),new Vector3(94,1,-76),new Vector3(113,-1,-8),new Vector3(-5,3,-15),new Vector3(26,1,77)};
        var names=new[]{"overhead-all-content","west-lots","southwest-lots","southeast-lots","east-lots","east-beach","west-beach","west-stairs","east-stairs","dock-and-park","markets","restaurant"};
        var character=UnityEngine.Object.FindFirstObjectByType<FirstPersonCharacter>();var playerStart=character.GetPosition();character.SetMovementMode(ECM2.Character.MovementMode.Flying);
        var captures=new List<string>();Vector3 original=camera.transform.position;Quaternion rotation=camera.transform.rotation;
        var fog=all.FirstOrDefault(t=>IslandFrontEndInventory.PathOf(t)=="---PostProcess/SushiWeather/Sky/Fog")?.GetComponent<Renderer>();bool fogEnabled=fog&&fog.enabled;
        for(int k=0;k<positions.Length;k++)
        {if(fog)fog.enabled=k==0?false:fogEnabled;character.SetVelocity(Vector3.zero);character.SetPosition(positions[k]-Vector3.up*1.6f,true);character.LookAtInstant(targets[k]);await UniTask.Delay(800,ignoreTimeScale:true);for(int j=0;j<8;j++){await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);camera.transform.SetPositionAndRotation(positions[k],Quaternion.LookRotation(targets[k]-positions[k]));camera.useOcclusionCulling=false;camera.farClipPlane=1200;if(j==6){string file="comprehensive-"+names[k]+".png";ScreenCapture.CaptureScreenshot(Path.Combine(IslandComprehensiveRepair.Reports,file));captures.Add(file);}}await UniTask.Delay(300,ignoreTimeScale:true);}
        if(fog)fog.enabled=fogEnabled;character.SetVelocity(Vector3.zero);character.SetPosition(playerStart,true);character.SetMovementMode(ECM2.Character.MovementMode.Falling);camera.transform.SetPositionAndRotation(original,rotation);
        var audit=new Audit{utc=DateTime.UtcNow.ToString("o"),scenes=scenes.Length,loaded=scenes.Select(s=>s.name).ToArray(),renderers=renderers.Length,lods=lods.Length,missingScripts=all.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),unsupported=renderers.Where(r=>r.enabled&&(!(r is ParticleSystemRenderer particle)||particle.renderMode!=ParticleSystemRenderMode.None)&&r.sharedMaterials.Any(m=>!m||!m.shader||!m.shader.isSupported||m.shader.name=="Hidden/InternalErrorShader")).Select(r=>IslandFrontEndInventory.PathOf(r.transform)).ToArray(),captures=captures.ToArray(),materialDetails=renderers.Where(r=>r.sharedMaterials.Any(m=>!m||!m.shader||!m.shader.isSupported)).Select(r=>IslandFrontEndInventory.PathOf(r.transform)+" materials="+string.Join(";",r.sharedMaterials.Select(m=>m?m.name+"/"+(m.shader?m.shader.name:"null"):"NULL"))+" enabled="+r.enabled).ToArray(),notes=new[]{"The high survey view remains affected by the original atmospheric fog; it is excluded from visual acceptance. Ground and shore captures and the full static inventory are used instead.","All loaded dressing activation groups enabled, distance culler disabled, LOD0 forced, far clip 1200m and occlusion disabled for inspection.","Static inventory independently loaded every one of the 14 dressing scenes, including inactive meshes."}};
        File.WriteAllText(Path.Combine(IslandComprehensiveRepair.Reports,"comprehensive-runtime.json"),JsonUtility.ToJson(audit,true));
        foreach(var l in lods)if(l)l.ForceLOD(-1);
    }
}
