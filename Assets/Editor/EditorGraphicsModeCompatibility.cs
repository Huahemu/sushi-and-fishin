using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Player GPU-resident drawer jobs lack the editor's NativeContainer safety
// metadata. Use URP's regular drawing path only in the editor. Serialized
// quality assets retain the game's original GPU resident drawer mode.
[InitializeOnLoad]
public static class EditorGraphicsModeCompatibility
{
    private static readonly Dictionary<UniversalRenderPipelineAsset, GPUResidentDrawerMode> originalModes = new Dictionary<UniversalRenderPipelineAsset, GPUResidentDrawerMode>();
    static EditorGraphicsModeCompatibility()
    {
        EditorApplication.delayCall += Apply;
        EditorApplication.playModeStateChanged += _ => Apply();
        AssemblyReloadEvents.beforeAssemblyReload += Restore;
        EditorApplication.quitting += Restore;
    }
    public static void Apply()
    {
        if(AssetDatabase.IsAssetImportWorkerProcess()) return;
        foreach(var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset",new[]{"Assets"}))
        {
            var asset=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if(!asset) continue;
            if(!originalModes.ContainsKey(asset)) originalModes.Add(asset,asset.gpuResidentDrawerMode);
            asset.gpuResidentDrawerMode=GPUResidentDrawerMode.Disabled;
        }
    }
    public static void Restore()
    {
        foreach(var pair in originalModes) if(pair.Key) pair.Key.gpuResidentDrawerMode=pair.Value;
    }
}

public sealed class OriginalGraphicsOptimizationSaveBoundary : AssetModificationProcessor
{
    private static string[] OnWillSaveAssets(string[] paths)
    {
        EditorGraphicsModeCompatibility.Restore();
        EditorApplication.delayCall += EditorGraphicsModeCompatibility.Apply;
        return paths;
    }
}
