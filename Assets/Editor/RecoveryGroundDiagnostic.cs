using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class RecoveryGroundDiagnostic
{
    [Serializable] public class TerrainRecord
    {
        public string name,asset,material,shader;
        public Vector3 position,size;
        public bool enabled,drawHeightmap,drawInstanced,shaderSupported,materialInstancing;
        public int resolution,layers,holes,solid,alphamaps;
        public float minHeight,maxHeight,minAlphaSum,maxAlphaSum;
    }
    [Serializable] public class ObjectRecord {public string name;public Vector3 position;}
    [Serializable] public class Report
    {
        public string utc;public TerrainRecord[] terrains;public ObjectRecord[] beachObjects;
        public string[] errors;public Vector3 cameraTarget;
    }
    static string Reports=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../reports"));
    public static void Run(){RunInternal(false);}
    public static void Repair(){RunInternal(true);}
    static void RunInternal(bool repair)
    {
        var errors=new List<string>();
        Application.logMessageReceived+=(m,s,t)=>{if(t==LogType.Error || t==LogType.Exception)errors.Add(m);};
        RestoredRenderPipelineSupport.RegisterOriginalAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/GameState.unity",OpenSceneMode.Single);
        EditorSceneManager.OpenScene("Assets/Scenes/DressingScenes/100_Dressing_City_01.unity",OpenSceneMode.Additive);
        typeof(RenderPipelineManager).GetMethod("TryPrepareRenderPipeline",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{GraphicsSettings.currentRenderPipeline});
        OriginalNativeShaderRestoration.Apply();
        var terrains=Resources.FindObjectsOfTypeAll<Terrain>().Where(t=>t.gameObject.scene.IsValid()).ToArray();
        var rows=new List<TerrainRecord>();
        foreach(var t in terrains)
        {
            var d=t.terrainData;var mat=t.materialTemplate;
            var row=new TerrainRecord{name=t.name,asset=AssetDatabase.GetAssetPath(d),position=t.transform.position,size=d.size,
                enabled=t.enabled,drawHeightmap=t.drawHeightmap,drawInstanced=t.drawInstanced,material=mat?mat.name:"null",shader=mat&&mat.shader?mat.shader.name:"null",
                shaderSupported=mat&&mat.shader&&mat.shader.isSupported,materialInstancing=mat&&mat.enableInstancing,resolution=d.heightmapResolution,layers=d.terrainLayers.Length,alphamaps=d.alphamapTextureCount,
                minHeight=float.MaxValue,maxHeight=float.MinValue,minAlphaSum=float.MaxValue,maxAlphaSum=float.MinValue};
            var hs=d.GetHeights(0,0,d.heightmapResolution,d.heightmapResolution);foreach(float h in hs){row.minHeight=Mathf.Min(row.minHeight,h);row.maxHeight=Mathf.Max(row.maxHeight,h);}
            var holes=d.GetHoles(0,0,d.holesResolution,d.holesResolution);foreach(bool b in holes){if(b)row.solid++;else row.holes++;}
            var alpha=d.GetAlphamaps(0,0,d.alphamapWidth,d.alphamapHeight);
            for(int y=0;y<d.alphamapHeight;y++)for(int x=0;x<d.alphamapWidth;x++){float sum=0;for(int l=0;l<d.alphamapLayers;l++)sum+=alpha[y,x,l];row.minAlphaSum=Mathf.Min(row.minAlphaSum,sum);row.maxAlphaSum=Mathf.Max(row.maxAlphaSum,sum);}
            rows.Add(row);
        }
        var beach=Resources.FindObjectsOfTypeAll<Transform>().Where(t=>t.gameObject.scene.IsValid() && (t.name.IndexOf("chair",StringComparison.OrdinalIgnoreCase)>=0 || t.name.IndexOf("loung",StringComparison.OrdinalIgnoreCase)>=0 || t.name.IndexOf("Ocean",StringComparison.OrdinalIgnoreCase)>=0)).Select(t=>new ObjectRecord{name=t.name,position=t.position}).Take(40).ToArray();
        Vector3 target=Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t=>t.gameObject.scene.IsValid() && t.name.StartsWith("BeachChair",StringComparison.Ordinal))?.position ?? terrains[0].transform.position+terrains[0].terrainData.size*0.5f;
        var camera=new GameObject("Recovery ground diagnostic camera").AddComponent<Camera>();camera.nearClipPlane=0.1f;camera.farClipPlane=2000;camera.fieldOfView=55;
        camera.transform.position=target+new Vector3(0,25,-32);camera.transform.LookAt(target);
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        Capture(camera,"ground-before.png");
        var old=terrains.Select(t=>t.drawInstanced).ToArray();for(int i=0;i<terrains.Length;i++)terrains[i].drawInstanced=false;
        Capture(camera,"ground-without-instancing.png");for(int i=0;i<terrains.Length;i++)terrains[i].drawInstanced=old[i];
        var templates=terrains.Select(t=>t.materialTemplate).ToArray();
        var sourceShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/RecoveredTerrainShaders/TerrainLit.shader");
        Debug.Log("RECOVERY_TERRAIN_SOURCE_SHADER "+sourceShader+" supported="+(sourceShader && sourceShader.isSupported));
        foreach(var message in ShaderUtil.GetShaderMessages(sourceShader))Debug.Log("RECOVERY_TERRAIN_SHADER_MESSAGE "+message.message);
        for(int i=0;i<terrains.Length;i++){var material=new Material(templates[i]);material.shader=sourceShader;terrains[i].materialTemplate=material;terrains[i].Flush();}
        Capture(camera,"ground-sdk-source-instanced.png");
        for(int i=0;i<terrains.Length;i++)terrains[i].drawInstanced=false;
        Capture(camera,"ground-sdk-source-uninstanced.png");
        for(int i=0;i<terrains.Length;i++){terrains[i].drawInstanced=old[i];terrains[i].materialTemplate=templates[i];}
        if(repair)
        {
            OriginalNativeShaderRestoration.RestoreSerializedReferences();
            var path="Assets/Material/TerrainLit.mat";
            var backup=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../backup/ground-jump-before-repair-20261015/",path));
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if(!File.Exists(backup)){File.Copy(path,backup);File.Copy(path+".meta",backup+".meta");}
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            int queue=material.rawRenderQueue;var keywords=material.shaderKeywords;
            material.shader=sourceShader;material.renderQueue=queue;material.shaderKeywords=keywords;
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
            OriginalNativeShaderRestoration.Apply();
            foreach(var terrain in terrains)terrain.Flush();
            Capture(camera,"ground-repaired-beach.png");
            var shaderErrors=new List<string>();
            foreach(var guid in AssetDatabase.FindAssets("t:Shader",new[]{"Assets/RecoveredTerrainShaders"}))
            foreach(var message in ShaderUtil.GetShaderMessages(AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid))))shaderErrors.Add(message.message);
            File.WriteAllText(Path.Combine(Reports,"terrain-shader-compile-messages.txt"),string.Join("\n",shaderErrors));
            if(shaderErrors.Count>0)throw new InvalidOperationException("Recovered terrain shader has compile messages");
        }
        File.WriteAllText(Path.Combine(Reports,"ground-diagnostic.json"),JsonUtility.ToJson(new Report{utc=DateTime.UtcNow.ToString("o"),terrains=rows.ToArray(),beachObjects=beach,errors=errors.ToArray(),cameraTarget=target},true));
        Debug.Log("RECOVERY_GROUND_DIAGNOSTIC_COMPLETE");EditorApplication.Exit(0);
    }
    public static void Capture(Camera camera,string name)
    {
        var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);rt.Create();
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        var previous=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1280,720,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();RenderTexture.active=previous;
        File.WriteAllBytes(Path.Combine(Reports,name),tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
    }
}
