using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RecoveryEditabilityAudit
{
    [Serializable] private class Report
    {
        public string utc, sourceMaterial, copiedMaterial, copiedScene;
        public bool transformEditPersisted, materialEditPersisted, materialShaderReferencePersisted;
        public bool newNativeMaterialEditPersisted,newNativeMaterialShaderReferencePersisted;
        public bool newNativeMaterialOverrideTagPersisted;
        public int originalCompiledShaders, appliedMaterials;
        public string[] unsupportedRendererShaders;
    }
    private static string Reports => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../reports"));
    public static void Run()
    {
        const string folder = "Assets/RecoveryVerification";
        if(!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "RecoveryVerification");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/MainMenuState.unity", OpenSceneMode.Single);
        RestoredRenderPipelineSupport.RegisterOriginalAssets();
        OriginalNativeShaderRestoration.Apply();
        var unsupported = new HashSet<string>();
        Material source=null;
        foreach(var root in scene.GetRootGameObjects())
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
        foreach(var mat in renderer.sharedMaterials)
        {
            if(!mat || !mat.shader) continue;
            if(source==null && AssetDatabase.Contains(mat)) source=mat;
            if(!mat.shader.isSupported || mat.shader.name == "Hidden/InternalErrorShader") unsupported.Add(mat.name+" | "+mat.shader.name);
        }
        var report=new Report { utc=DateTime.UtcNow.ToString("o"), copiedScene=folder+"/MenuEditSaveCheck.unity",
            copiedMaterial=folder+"/MaterialEditSaveCheck.mat", originalCompiledShaders=OriginalNativeShaderRestoration.LoadedShaderCount,
            appliedMaterials=OriginalNativeShaderRestoration.AppliedMaterials, unsupportedRendererShaders=new List<string>(unsupported).ToArray() };
        if(!source) throw new InvalidOperationException("No material from the original menu could be loaded");
        report.sourceMaterial=AssetDatabase.GetAssetPath(source);
        OriginalNativeShaderRestoration.RestoreSerializedReferences();
        if(!AssetDatabase.CopyAsset(report.sourceMaterial,report.copiedMaterial)) throw new IOException("Could not make verification material copy");
        var copied=AssetDatabase.LoadAssetAtPath<Material>(report.copiedMaterial);
        Shader originalShader=copied.shader;
        copied.renderQueue=2451;
        EditorUtility.SetDirty(copied);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(report.copiedMaterial,ImportAssetOptions.ForceUpdate);
        copied=AssetDatabase.LoadAssetAtPath<Material>(report.copiedMaterial);
        report.materialEditPersisted=copied.renderQueue==2451;
        report.materialShaderReferencePersisted=copied.shader==originalShader && AssetDatabase.Contains(copied.shader);
        OriginalNativeShaderRestoration.Apply();
        var created=new Material(source){name="New native-shader material save verification",renderQueue=2452};
        created.SetOverrideTag("RenderType","RecoveryVerificationTag");
        string createdPath=folder+"/NewNativeMaterialSaveCheck.mat";
        AssetDatabase.CreateAsset(created,createdPath);
        EditorUtility.SetDirty(created);AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(createdPath,ImportAssetOptions.ForceUpdate);
        created=AssetDatabase.LoadAssetAtPath<Material>(createdPath);
        report.newNativeMaterialEditPersisted=created.renderQueue==2452;
        report.newNativeMaterialShaderReferencePersisted=created.shader && AssetDatabase.Contains(created.shader);
        report.newNativeMaterialOverrideTagPersisted=created.GetTag("RenderType",false,"")=="RecoveryVerificationTag";
        var marker=new GameObject("Recovery verification - persisted transform edit");
        marker.transform.position=new Vector3(11.25f,3.5f,-7.75f);
        EditorSceneManager.SaveScene(scene,report.copiedScene,true);
        UnityEngine.Object.DestroyImmediate(marker);
        var saved=EditorSceneManager.OpenScene(report.copiedScene,OpenSceneMode.Single);
        foreach(var root in saved.GetRootGameObjects())
            if(root.name=="Recovery verification - persisted transform edit") report.transformEditPersisted=root.transform.position==new Vector3(11.25f,3.5f,-7.75f);
        File.WriteAllText(Path.Combine(Reports,"unity-edit-save-reopen-audit.json"),JsonUtility.ToJson(report,true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);
        EditorApplication.Exit(report.transformEditPersisted && report.materialEditPersisted && report.materialShaderReferencePersisted && report.newNativeMaterialEditPersisted && report.newNativeMaterialShaderReferencePersisted && report.newNativeMaterialOverrideTagPersisted ? 0:2);
    }
}
