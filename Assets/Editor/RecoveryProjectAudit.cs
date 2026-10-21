using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Verification utilities for this recovered project; does not save its scenes.
public static class RecoveryProjectAudit
{
    [Serializable] public class ShaderRecord { public string path; public string name; public bool supported; }
    [Serializable] public class SceneRecord
    {
        public string path;
        public int gameObjects;
        public int missingScripts;
        public int renderers;
        public int missingMeshes;
        public int missingMaterials;
        public int staticBatchedRenderers;
        public string error;
    }
    [Serializable] public class Report
    {
        public string utc;
        public string editorVersion;
        public string companyName;
        public string productName;
        public string bundleVersion;
        public string colorSpace;
        public string graphicsDevice;
        public string configuredRenderPipeline;
        public string qualityRenderPipeline;
        public string instantiatedRenderPipeline;
        public string renderPipelineInitializationError;
        public int monoScripts;
        public int materials;
        public int meshes;
        public string[] buildScenes;
        public ShaderRecord[] shaders;
        public SceneRecord[] scenes;
        public string[] renderErrors;
        public bool renderProducedVisiblePixels;
        public double renderMeanLuminance;
        public double renderLuminanceVariance;
    }

    private static string ReportsDirectory => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../reports"));

    public static void Import()
    {
        Report report = CreateReport();
        Write("unity-import-audit.json", report);
        Debug.Log("RECOVERY_IMPORT_AUDIT_COMPLETE " + report.editorVersion);
        EditorApplication.Exit(0);
    }

    public static void ProbeRenderPipeline()
    {
        RestoredRenderPipelineSupport.RegisterOriginalAssets();
        string error = null;
        try
        {
            MethodInfo prepare = typeof(UnityEngine.Rendering.RenderPipelineManager).GetMethod(
                "TryPrepareRenderPipeline", BindingFlags.NonPublic | BindingFlags.Static);
            if (prepare == null) throw new MissingMethodException("RenderPipelineManager.TryPrepareRenderPipeline");
            prepare.Invoke(null, new object[] { UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline });
        }
        catch (Exception exception) { error = exception.ToString(); }
        Report report = CreateReport();
        report.renderPipelineInitializationError = error;
        Write("unity-render-pipeline-probe.json", report);
        Debug.Log("RECOVERY_RENDER_PIPELINE_PROBE_COMPLETE " + report.instantiatedRenderPipeline + " " + error);
        EditorApplication.Exit(error == null && report.instantiatedRenderPipeline != null ? 0 : 2);
    }

    public static void RenderMenuScene()
    {
        var renderErrors = new List<string>();
        Application.LogCallback collect = (message, trace, severity) =>
        {
            if (severity == LogType.Error || severity == LogType.Exception || severity == LogType.Assert)
                renderErrors.Add(message + "\n" + trace);
        };
        Application.logMessageReceived += collect;
        RestoredRenderPipelineSupport.RegisterOriginalAssets();
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenuState.unity", OpenSceneMode.Single);
        typeof(UnityEngine.Rendering.RenderPipelineManager).GetMethod("TryPrepareRenderPipeline", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline });
        var cameras = new List<Camera>();
        foreach (GameObject root in scene.GetRootGameObjects()) cameras.AddRange(root.GetComponentsInChildren<Camera>(true));
        foreach (Camera camera in cameras)
        {
            Debug.Log("RECOVERY_CAMERA " + camera.name + " enabled=" + camera.enabled + " active=" + camera.gameObject.activeInHierarchy + " position=" + camera.transform.position);
        }
        Camera selected = cameras.Find(c => c.enabled && c.gameObject.activeInHierarchy && c.cameraType == CameraType.Game);
        if (selected == null) selected = cameras.Find(c => c.cameraType == CameraType.Game);
        if (selected == null) throw new InvalidOperationException("No original menu camera found");
        OriginalNativeShaderRestoration.Apply();
        var texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        texture.Create();
        var request = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = texture };
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(selected, request);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = texture;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
        pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        pixels.Apply();
        RenderTexture.active = previous;
        double sum = 0, squares = 0;
        Color32[] samples = pixels.GetPixels32();
        foreach (var sample in samples)
        {
            double luminance = (sample.r + sample.g + sample.b) / 3.0;
            sum += luminance;
            squares += luminance * luminance;
        }
        double mean = sum / samples.Length;
        double variance = squares / samples.Length - mean * mean;
        var report = CreateReport();
        report.renderErrors = renderErrors.ToArray();
        report.renderMeanLuminance = mean;
        report.renderLuminanceVariance = variance;
        report.renderProducedVisiblePixels = mean > 1 && variance > 1;
        Directory.CreateDirectory(ReportsDirectory);
        File.WriteAllBytes(Path.Combine(ReportsDirectory, "original-menu-camera-render.png"), pixels.EncodeToPNG());
        Write("unity-menu-render-audit.json", report);
        Application.logMessageReceived -= collect;
        Debug.Log("RECOVERY_MENU_RENDER_COMPLETE " + selected.name + " visible=" + report.renderProducedVisiblePixels + " errors=" + report.renderErrors.Length);
        UnityEngine.Object.DestroyImmediate(pixels);
        texture.Release();
        UnityEngine.Object.DestroyImmediate(texture);
        EditorApplication.Exit(report.renderProducedVisiblePixels && report.renderErrors.Length == 0 ? 0 : 2);
    }

    public static void AuditScenes()
    {
        Report report = CreateReport();
        var records = new List<SceneRecord>();
        foreach (string path in report.buildScenes)
        {
            var item = new SceneRecord { path = path };
            try
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        item.gameObjects++;
                        item.missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                    }
                    foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                        if (filter.sharedMesh == null) item.missingMeshes++;
                    foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        item.renderers++;
                        if (renderer.isPartOfStaticBatch) item.staticBatchedRenderers++;
                        foreach (Material material in renderer.sharedMaterials)
                            if (material == null || material.shader == null) item.missingMaterials++;
                    }
                }
            }
            catch (Exception exception) { item.error = exception.ToString(); }
            records.Add(item);
            Debug.Log("RECOVERY_SCENE_AUDIT " + JsonUtility.ToJson(item));
        }
        report.scenes = records.ToArray();
        Write("unity-scenes-audit.json", report);
        EditorApplication.Exit(records.Exists(s => s.error != null || s.missingScripts > 0) ? 2 : 0);
    }

    public static void ReimportSDKShaders()
    {
        foreach (var path in Directory.GetFiles("Packages", "*.shader", SearchOption.AllDirectories))
            AssetDatabase.ImportAsset(path.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
        RenderMenuScene();
    }

    private static Report CreateReport()
    {
        var report = new Report
        {
            utc = DateTime.UtcNow.ToString("o"), editorVersion = Application.unityVersion,
            companyName = PlayerSettings.companyName, productName = PlayerSettings.productName,
            bundleVersion = PlayerSettings.bundleVersion, colorSpace = PlayerSettings.colorSpace.ToString(),
            graphicsDevice = SystemInfo.graphicsDeviceType.ToString(),
            configuredRenderPipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline == null ? null : UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.name,
            qualityRenderPipeline = QualitySettings.renderPipeline == null ? null : QualitySettings.renderPipeline.name,
            instantiatedRenderPipeline = UnityEngine.Rendering.RenderPipelineManager.currentPipeline == null ? null : UnityEngine.Rendering.RenderPipelineManager.currentPipeline.GetType().FullName,
            monoScripts = AssetDatabase.FindAssets("t:MonoScript").Length,
            materials = AssetDatabase.FindAssets("t:Material").Length,
            meshes = AssetDatabase.FindAssets("t:Mesh").Length
        };
        var scenes = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.enabled) scenes.Add(scene.path);
        report.buildScenes = scenes.ToArray();
        var shaders = new List<ShaderRecord>();
        foreach (string guid in AssetDatabase.FindAssets("t:Shader"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            shaders.Add(new ShaderRecord { path = path, name = shader == null ? null : shader.name,
                                          supported = shader != null && shader.isSupported });
        }
        report.shaders = shaders.ToArray();
        return report;
    }

    private static void Write(string name, Report report)
    {
        Directory.CreateDirectory(ReportsDirectory);
        File.WriteAllText(Path.Combine(ReportsDirectory, name), JsonUtility.ToJson(report, true));
    }
}
