namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.IO;
using global::System.Reflection;
using global::UnityEngine;
using global::UnityEngine.Rendering;
using global::UnityEngine.SceneManagement;

// Compatibility support for authoring a recovered player. Original compiled
// programs are retained; this does not recreate unavailable ShaderGraph source.
public static class RecoveryShaderSupport
{
    static AssetBundle bundle;
    static readonly Dictionary<string,Shader> shaders = new Dictionary<string,Shader>();
    static readonly Dictionary<string,Shader> allShaders = new Dictionary<string,Shader>();
    static readonly Dictionary<Material,Shader> authoringMaterials = new Dictionary<Material,Shader>();
    static readonly Dictionary<string,Shader> authoringShaders = new Dictionary<string,Shader>();
    static readonly Dictionary<Type,FieldInfo[]> fields = new Dictionary<Type,FieldInfo[]>();
    static readonly List<Action> restoreFields = new List<Action>();
    static readonly HashSet<string> fieldKeys = new HashSet<string>();
    static int renderDepth;
    static bool runtimeRefreshRequired = true;
    public static double LastRenderScanMilliseconds;
    public static int RuntimeRefreshCount { get; private set; }
    sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public new bool Equals(object x, object y) => ReferenceEquals(x,y);
        public int GetHashCode(object value) => global::System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }
    public static void RequestRuntimeRefresh() { runtimeRefreshRequired=true; CasinoRuntimeContext.SharedShadersRefresh?.Invoke(); }
    public static bool IsOriginalCustomShader(string name){Load();return name!=null&&shaders.ContainsKey(name);}
    public static void InstallForHost()
    {
        Load();
        RequestRuntimeRefresh();
        RenderPipelineManager.beginContextRendering -= BeforeRendering;
        RenderPipelineManager.beginContextRendering += BeforeRendering;
        RenderPipelineManager.endContextRendering -= AfterRendering;
        RenderPipelineManager.endContextRendering += AfterRendering;
    }
    public static void UninstallForHost()
    {
        RenderPipelineManager.beginContextRendering -= BeforeRendering;
        RenderPipelineManager.endContextRendering -= AfterRendering;
        RestoreAuthoringReferences();
    }
    static void SceneLoaded(Scene scene,LoadSceneMode mode) { InstallForHost(); }
    public static int ShaderCount => shaders.Count;
    public static IEnumerable<Shader> Shaders => shaders.Values;
    public static Shader CompiledFor(Shader authoring) { return authoring!=null && shaders.TryGetValue(authoring.name,out var compiled)?compiled:null; }
    public static IEnumerable<Shader> CompatibilityShaders
    {
        get
        {
            foreach(var entry in allShaders)
                if(entry.Key.Contains("FallbackError") || entry.Key=="Universal Render Pipeline/Particles/Simple Lit" || entry.Key=="Universal Render Pipeline/Particles/Unlit")yield return entry.Value;
        }
    }
    public static void RegisterAuthoringShader(Shader shader) { if(shader!=null)authoringShaders[shader.name]=shader; }
    public static void BindMaterial(Material material,Shader authoring,string name)
    {
        if(authoring!=null)RegisterAuthoringShader(authoring);
        if(shaders.TryGetValue(name,out var compiled))material.shader=compiled;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        Load(); Apply();
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
        RequestRuntimeRefresh();
        RenderPipelineManager.beginContextRendering -= BeforeRendering;
        RenderPipelineManager.beginContextRendering += BeforeRendering;
        RenderPipelineManager.endContextRendering -= AfterRendering;
        RenderPipelineManager.endContextRendering += AfterRendering;
    }
    static void BeforeRendering(ScriptableRenderContext context,List<Camera> cameras) { BeginRendering(); }
    static void AfterRendering(ScriptableRenderContext context,List<Camera> cameras) { EndRendering(); }
    public static void Load()
    {
        if (bundle != null) return;
        var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if(pipeline!=null)Shader.globalRenderPipeline=pipeline.renderPipelineShaderTag;
        foreach(var loaded in AssetBundle.GetAllLoadedAssetBundles())
            if(loaded.name=="gwyf-107-original-compiled-shaders") { bundle=loaded;break; }
        if(bundle==null)bundle=AssetBundle.LoadFromFile(Path.Combine(SushiAndFish.Casino.Original.CasinoPaths.StreamingAssetsPath,"Recovery/OriginalCompiledShaders.bundle"));
        if (bundle==null) throw new InvalidOperationException("Cannot load original GWYF compiled shader container.");
        string sourceNames=Path.Combine(SushiAndFish.Casino.Original.CasinoPaths.StreamingAssetsPath,"Recovery/SdkShaderNames.txt");
        var sourceShaders=new HashSet<string>(File.Exists(sourceNames)?File.ReadAllLines(sourceNames):Array.Empty<string>());
        foreach(var shader in bundle.LoadAllAssets<Shader>())
        {
            allShaders[shader.name]=shader;
            if(!sourceShaders.Contains(shader.name))shaders[shader.name]=shader;
        }
#if UNITY_EDITOR
        // The source project's RecoveryEditorSupport registers these native
        // programs after the GUID-bearing YAML proxies have been imported.
        // Keep that same registration when embedding its runtime in the host.
        foreach(var shader in shaders.Values)UnityEditor.ShaderUtil.RegisterShader(shader);
        foreach(var shader in CompatibilityShaders)UnityEditor.ShaderUtil.RegisterShader(shader);
#endif
    }
    static FieldInfo[] Fields(Type type)
    {
        if (fields.TryGetValue(type,out var result)) return result;
        var all=new List<FieldInfo>();
        for(var t=type;t!=null && t!=typeof(UnityEngine.Object);t=t.BaseType)
            all.AddRange(t.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly));
        return fields[type]=all.ToArray();
    }
    static void PatchFields(object target,HashSet<object> visited,int depth)
    {
        if (target==null || depth>9 || !visited.Add(target)) return;
        if (target is IList list)
        {
            foreach(var child in list) PatchFields(child,visited,depth+1);
            return;
        }
        foreach(var field in Fields(target.GetType()))
        {
            var value=field.GetValue(target);
            if(!field.IsInitOnly && value is Shader original && original!=null && shaders.TryGetValue(original.name,out var compiled) && original!=compiled)
            {
                string key=global::System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(target)+":"+field.MetadataToken;
                if(Application.isEditor && fieldKeys.Add(key)) { var owner=target;var member=field;var old=original;restoreFields.Add(()=>member.SetValue(owner,old)); }
                field.SetValue(target,compiled);
            }
            else if(value!=null && !(value is UnityEngine.Object) && !(value is string) && !field.FieldType.IsValueType)
            {
                var ns=value.GetType().Namespace ?? "";
                if(value is IList || ns.StartsWith("UnityEngine.Rendering")) PatchFields(value,visited,depth+1);
            }
        }
    }
    public static int Apply()
    {
        Load();
        var visited=new HashSet<object>(new ReferenceComparer());
        foreach(var asset in SushiAndFish.Casino.Original.CasinoResources.FindObjectsOfTypeAll<ScriptableObject>())
            if(asset && (asset.GetType().Namespace ?? "").StartsWith("SushiAndFish.Casino.Original")) PatchFields(asset,visited,0);
        return 0;
    }
    public static void BeginRendering()
    {
        if(!Application.isEditor || Application.isPlaying)
        {
            LastRenderScanMilliseconds=0;
            if(!runtimeRefreshRequired)return;
            runtimeRefreshRequired=false;
            long started=global::System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                Apply();
                foreach(var material in SushiAndFish.Casino.Original.CasinoResources.FindObjectsOfTypeAll<Material>())
                    if(SushiAndFish.Casino.CasinoShaderOwnership.Owns(material) && material.shader!=null && shaders.TryGetValue(material.shader.name,out var compiled) && material.shader!=compiled)
                    {
                        if(Application.isEditor && !authoringMaterials.ContainsKey(material))authoringMaterials.Add(material,material.shader);
                        material.shader=compiled;
                    }
                RuntimeRefreshCount++;
            }
            finally { LastRenderScanMilliseconds=(global::System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/global::System.Diagnostics.Stopwatch.Frequency; }
            return;
        }
        if(renderDepth++>0)return;
        Apply();
        foreach(var material in SushiAndFish.Casino.Original.CasinoResources.FindObjectsOfTypeAll<Material>())
        {
            if(!SushiAndFish.Casino.CasinoShaderOwnership.Owns(material))continue;
            var original=material.shader;
            if(original==null || !shaders.TryGetValue(original.name,out var compiled)) continue;
            if(original==compiled)
            {
                if(!authoringMaterials.ContainsKey(material) && authoringShaders.TryGetValue(original.name,out var authoring))authoringMaterials.Add(material,authoring);
                continue;
            }
            if(!authoringMaterials.ContainsKey(material))authoringMaterials.Add(material,original);
            authoringMaterials[material]=original;
            material.shader=compiled;
        }
    }
    public static void EndRendering()
    {
        if(!Application.isEditor)return;
        if(renderDepth>0 && --renderDepth>0)return;
        if(Application.isPlaying)return;
        RestoreMaterials();
    }
    static void RestoreMaterials()
    {
        foreach(var item in authoringMaterials)if(item.Key!=null && item.Value!=null)item.Key.shader=item.Value;
        authoringMaterials.Clear();renderDepth=0;
    }
    public static void RestoreAuthoringReferences()
    {
        RestoreMaterials();
        foreach(var action in restoreFields) { try { action(); } catch(MissingReferenceException) { } }
        restoreFields.Clear();fieldKeys.Clear();
    }
}

}
