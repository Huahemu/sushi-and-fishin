using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// A bounded runtime-loading probe. It never saves any scene/material changes.
// The bundle retains the original published Shader object bytes, not a visual
// substitute. Temporary material copies test those programs against the scene.
public static class RecoveryNativeShaderRuntimeProbe
{
    [Serializable] public class ShaderRow { public string name; public bool supported; public int passes; }
    [Serializable] public class Report
    {
        public string utc, editor, error;
        public int loadedShaders, replacedMaterialSlots;
        public ShaderRow[] shaders;
        public string[] errors;
        public double mean, variance;
        public bool visible;
    }
    public static void Run()
    {
        var root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        var directory = Path.Combine(root, "reports");
        var report = new Report { utc = DateTime.UtcNow.ToString("o"), editor = Application.unityVersion };
        var errors = new List<string>();
        Application.LogCallback collect = (message, trace, severity) =>
        {
            if (severity == LogType.Error || severity == LogType.Exception || severity == LogType.Assert)
                errors.Add(message + "\n" + trace);
        };
        Application.logMessageReceived += collect;
        AssetBundle bundle = null;
        var temporaryMaterials = new List<Material>();
        try
        {
            RestoredRenderPipelineSupport.RegisterOriginalAssets();
            bundle = AssetBundle.LoadFromFile(Path.Combine(root, "export/original-native-shaders-runtime.bundle"));
            if (!bundle) throw new InvalidOperationException("Original shader byte container failed to load");
            var shaders = bundle.LoadAllAssets<Shader>();
            report.loadedShaders = shaders.Length;
            var byName = new Dictionary<string, Shader>(StringComparer.Ordinal);
            var rows = new List<ShaderRow>();
            foreach (var shader in shaders)
            {
                byName[shader.name] = shader;
                var probe = new Material(shader);
                rows.Add(new ShaderRow { name = shader.name, supported = shader.isSupported, passes = probe.passCount });
                UnityEngine.Object.DestroyImmediate(probe);
            }
            report.shaders = rows.ToArray();
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenuState.unity", OpenSceneMode.Single);
            var replacements = new Dictionary<Material, Material>();
            foreach (var sceneRoot in scene.GetRootGameObjects())
            foreach (var renderer in sceneRoot.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    var original = materials[index];
                    if (!original || !original.shader || !byName.TryGetValue(original.shader.name, out var shader)) continue;
                    if (!replacements.TryGetValue(original, out var replacement))
                    {
                        replacement = new Material(original) { shader = shader, hideFlags = HideFlags.HideAndDontSave };
                        replacements.Add(original, replacement);
                        temporaryMaterials.Add(replacement);
                    }
                    materials[index] = replacement;
                    report.replacedMaterialSlots++;
                }
                renderer.sharedMaterials = materials;
            }
            typeof(UnityEngine.Rendering.RenderPipelineManager).GetMethod("TryPrepareRenderPipeline", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline });
            Camera selected = null;
            foreach (var sceneRoot in scene.GetRootGameObjects())
            foreach (var camera in sceneRoot.GetComponentsInChildren<Camera>(true))
                if (camera.enabled && camera.gameObject.activeInHierarchy && camera.cameraType == CameraType.Game) selected = camera;
            if (!selected) throw new InvalidOperationException("Original menu camera is absent");
            var texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            texture.Create();
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(selected,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = texture });
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;
            double sum = 0, squares = 0;
            var samples = pixels.GetPixels32();
            foreach (var sample in samples)
            {
                double luminance = (sample.r + sample.g + sample.b) / 3.0;
                sum += luminance;
                squares += luminance * luminance;
            }
            report.mean = sum / samples.Length;
            report.variance = squares / samples.Length - report.mean * report.mean;
            report.visible = report.mean > 1 && report.variance > 1;
            File.WriteAllBytes(Path.Combine(directory, "native-shader-runtime-probe.png"), pixels.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(pixels);
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
        }
        catch (Exception exception) { report.error = exception.ToString(); }
        report.errors = errors.ToArray();
        Application.logMessageReceived -= collect;
        File.WriteAllText(Path.Combine(directory, "native-shader-runtime-probe.json"), JsonUtility.ToJson(report, true));
        // No authoring asset or scene is saved in this probe.
        foreach (var material in temporaryMaterials) UnityEngine.Object.DestroyImmediate(material);
        if (bundle) bundle.Unload(false);
        EditorApplication.Exit(report.error == null && report.visible && report.errors.Length == 0 ? 0 : 2);
    }
}
