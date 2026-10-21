using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Published shader programs are immutable compiled data. Retain their exact
// programs in a bundle and resolve recovered shader references by original
// name while authoring/playing, keeping all material property values intact.
public static class OriginalNativeShaderRestoration
{
    private static AssetBundle bundle;
    private static AssetBundle waterTransitionBundle;
    private static ComputeShader originalHeightSampler;
    private static readonly Dictionary<StylizedWater3.StylizedWaterRenderFeature, ComputeShader> serializedHeightSamplers = new Dictionary<StylizedWater3.StylizedWaterRenderFeature, ComputeShader>();
    private static Dictionary<string, Shader> shaders;
#if UNITY_EDITOR
    private static readonly Dictionary<string,Shader> authoringShaders=new Dictionary<string,Shader>(StringComparer.Ordinal);
#endif
    private static readonly Dictionary<Material, Shader> serializedShaders = new Dictionary<Material, Shader>();
    private static bool hooksInstalled;
    public static long ScanCount { get; private set; }
    public static double TotalScanMilliseconds { get; private set; }
    public static double LastScanMilliseconds { get; private set; }
    public static int LastRendererCount { get; private set; }
    public static int LastMaterialCount { get; private set; }
    public static int LoadedShaderCount => shaders == null ? 0 : shaders.Count;
    public static int AppliedMaterials { get; private set; }
    private static bool refreshPending = true;
    public static bool RefreshPending => refreshPending;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void BeginPlay()
    {
        InstallHooks();
        RequestRefresh();
        ApplyPending();
    }
    // A render context only drains pending work. The Editor and Player share
    // this single named subscription, including when domain reload is disabled.
    public static void InstallHooks()
    {
        if (hooksInstalled) return;
        RenderPipelineManager.beginContextRendering += BeginContext;
        SceneManager.sceneLoaded += SceneLoaded;
        SceneManager.activeSceneChanged += ActiveSceneChanged;
        hooksInstalled = true;
    }
    private static void BeginContext(ScriptableRenderContext context, List<Camera> cameras) => ApplyPending();
    private static void SceneLoaded(Scene scene, LoadSceneMode mode) => RequestRefresh();
    private static void ActiveSceneChanged(Scene previous, Scene current) => RequestRefresh();
    public static void RequestRefresh() => refreshPending = true;
    public static void ApplyPending()
    {
        if (refreshPending) Apply();
    }
    // Async character resources can arrive after sceneLoaded. Bind only their
    // new materials, without making a global scan part of NPC spawning.
    public static bool RestoreMaterial(Material material)
    {
        if (SushiAndFish.Casino.CasinoShaderOwnership.Owns(material) && material.shader && SushiAndFish.Casino.Original.RecoveryShaderSupport.IsOriginalCustomShader(material.shader.name)) return false;
        EnsureLoaded();
        if (!material || !material.shader || !shaders.TryGetValue(material.shader.name, out var restored)
            || !restored || !restored.isSupported) return false;
        if (material.shader != restored)
        {
            if (!serializedShaders.ContainsKey(material)) serializedShaders.Add(material, material.shader);
            SetShaderRetainingMaterialState(material, restored);
        }
        return true;
    }
    public static void RestoreRendererMaterials(GameObject root)
    {
        if (!root) return;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials) RestoreMaterial(material);
    }
    public static void EnsureLoaded()
    {
        if (shaders != null) return;
#if UNITY_EDITOR
        // Load GUID-bearing editor assets first. Loading native YAML proxies
        // after the published shader objects can overwrite their native cache.
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Shader"))
        {
            var shader=UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if(shader)authoringShaders[shader.name]=shader;
        }
        var path = Path.Combine(Application.dataPath, "OriginalRuntimeShaders/OriginalNativeShaderData.bytes");
        var namesPath = Path.Combine(Application.dataPath, "OriginalRuntimeShaders/OriginalNativeShaderNames.txt");
#else
        var path = Path.Combine(Application.streamingAssetsPath, "OriginalNativeShaders/OriginalNativeShaderData.bundle");
        var namesPath = Path.Combine(Application.streamingAssetsPath, "OriginalNativeShaders/OriginalNativeShaderNames.txt");
#endif
        // Native bundles survive an editor domain reload. Reuse the existing
        // original container when entering Play instead of loading it twice.
        foreach(var loaded in AssetBundle.GetAllLoadedAssetBundles())
            if(loaded.name == "sushi-recovery-original-native-shaders") { bundle=loaded; break; }
        if(!bundle) bundle = AssetBundle.LoadFromFile(path);
        if (!bundle) throw new InvalidOperationException("Original compiled shader data could not be loaded: " + path);
        shaders = new Dictionary<string, Shader>(StringComparer.Ordinal);
        foreach (var shader in bundle.LoadAllAssets<Shader>())
        {
            shaders[shader.name] = shader;
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.RegisterShader(shader);
#endif
        }
        // These programs are used only during surface crossing and GPU height
        // queries. They were absent from the original island shader subset.
        foreach(var loaded in AssetBundle.GetAllLoadedAssetBundles())
            if(loaded.name=="sushi-water-transition-programs") { waterTransitionBundle=loaded; break; }
        if(!waterTransitionBundle)
        {
#if UNITY_EDITOR
            var transitionPath=Path.Combine(Application.dataPath,"OriginalRuntimeShaders/OriginalWaterTransitionPrograms.bytes");
#else
            var transitionPath=Path.Combine(Application.streamingAssetsPath,"OriginalNativeShaders/OriginalWaterTransitionPrograms.bundle");
#endif
            waterTransitionBundle=AssetBundle.LoadFromFile(transitionPath);
        }
        if(!waterTransitionBundle)throw new InvalidOperationException("Original water transition programs could not be loaded");
        foreach(var shader in waterTransitionBundle.LoadAllAssets<Shader>())
        {
            shaders[shader.name]=shader;
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.RegisterShader(shader);
#endif
        }
        originalHeightSampler=waterTransitionBundle.LoadAllAssets<ComputeShader>().SingleOrDefault();
    }
    public static void Apply()
    {
        EnsureLoaded();
        refreshPending = false;
        long scanStart = System.Diagnostics.Stopwatch.GetTimestamp();
        ScanCount++;
        int applied = 0;
        // Serialized renderer slots load their material assets lazily. Force
        // those handles to resolve before enumerating loaded materials.
        var renderers = Resources.FindObjectsOfTypeAll<Renderer>();
        LastRendererCount = renderers.Length;
        foreach (var renderer in renderers)
            if (renderer) { var materials = renderer.sharedMaterials; }
        var loadedMaterials = Resources.FindObjectsOfTypeAll<Material>();
        LastMaterialCount = loadedMaterials.Length;
        foreach (var material in loadedMaterials)
            if (RestoreMaterial(material)) applied++;
        AppliedMaterials = applied;
        if(originalHeightSampler)
        foreach(var feature in Resources.FindObjectsOfTypeAll<StylizedWater3.StylizedWaterRenderFeature>())
        {
            if(feature.heightReadbackCS==originalHeightSampler)continue;
            if(!serializedHeightSamplers.ContainsKey(feature))serializedHeightSamplers.Add(feature,feature.heightReadbackCS);
            feature.heightReadbackCS=originalHeightSampler;
        }
        LastScanMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - scanStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        TotalScanMilliseconds += LastScanMilliseconds;
    }
    // A serialization boundary, not a material-value reset. Inspector edits to
    // color/texture/etc. remain intact when saving; only the Shader pointer is
    // restored to its original GUID-bearing authoring asset.
    public static void RestoreSerializedReferences()
    {
        RequestRefresh();
        foreach(var pair in serializedHeightSamplers)
            if(pair.Key)pair.Key.heightReadbackCS=pair.Value;
        foreach (var pair in serializedShaders)
            if (pair.Key && pair.Value) SetShaderRetainingMaterialState(pair.Key,pair.Value);
#if UNITY_EDITOR
        // New material instances may already start with a loaded native Shader
        // handle and therefore have no entry in serializedShaders yet. Save
        // those through the matching original GUID-bearing authoring proxy.
        if(shaders!=null)
        foreach(var material in Resources.FindObjectsOfTypeAll<Material>())
            if(material && material.shader && shaders.TryGetValue(material.shader.name,out var native) && material.shader==native && authoringShaders.TryGetValue(native.name,out var authoring))
            {
                SetShaderRetainingMaterialState(material,authoring);
                if(!serializedShaders.ContainsKey(material))serializedShaders.Add(material,authoring);
            }
#endif
    }
    private static void SetShaderRetainingMaterialState(Material material,Shader shader)
    {
        if(material.shader==shader)return;
        int queue=material.rawRenderQueue;
        var keywords=new HashSet<string>(material.shader?material.shaderKeywords:Array.Empty<string>(),StringComparer.Ordinal);
        var tags=new Dictionary<string,string>(StringComparer.Ordinal);
#if UNITY_EDITOR
        var state=new UnityEditor.SerializedObject(material);
        foreach(var field in new[]{"m_ValidKeywords","m_InvalidKeywords"})
        {
            var values=state.FindProperty(field);
            if(values!=null && values.isArray)for(int i=0;i<values.arraySize;i++)keywords.Add(values.GetArrayElementAtIndex(i).stringValue);
        }
        var tagMap=state.FindProperty("stringTagMap");
        if(tagMap!=null && tagMap.isArray)
            for(int i=0;i<tagMap.arraySize;i++)
            {
                var pair=tagMap.GetArrayElementAtIndex(i);var key=pair.FindPropertyRelative("first");var value=pair.FindPropertyRelative("second");
                if(key!=null && value!=null)tags[key.stringValue]=value.stringValue;
            }
#else
        var renderType=material.GetTag("RenderType",false,"");
        if(renderType!="")tags["RenderType"]=renderType;
#endif
        material.shader=shader;
        material.renderQueue=queue;
        var names=new string[keywords.Count];keywords.CopyTo(names);material.shaderKeywords=names;
        foreach(var pair in tags)material.SetOverrideTag(pair.Key,pair.Value);
    }
}
