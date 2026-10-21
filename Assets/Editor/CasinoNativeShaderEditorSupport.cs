using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using SourceShaders=SushiAndFish.Casino.Original.RecoveryShaderSupport;

// The source editor's compiled-program bridge, restricted to imported casino
// ownership. Runtime rendering continues to use ParkCasinoIntegration's hook.
[InitializeOnLoad]
public static class CasinoNativeShaderEditorSupport
{
    static CasinoNativeShaderEditorSupport()
    {
        EditorApplication.delayCall+=Initialize;
        RenderPipelineManager.beginContextRendering+=BeginContext;
        RenderPipelineManager.endContextRendering+=EndContext;
        AssemblyReloadEvents.beforeAssemblyReload+=SourceShaders.RestoreAuthoringReferences;
    }
    static void Initialize()
    {
        if(AssetDatabase.IsAssetImportWorkerProcess())return;
        if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Initialize;return;}
        OriginalNativeShaderRestoration.EnsureLoaded();
        SourceShaders.Load();SourceShaders.RequestRuntimeRefresh();
    }
    static void BeginContext(ScriptableRenderContext context,List<Camera> cameras){if(!Application.isPlaying)Begin();}
    static void EndContext(ScriptableRenderContext context,List<Camera> cameras){if(!Application.isPlaying)End();}
    public static void Begin(){OriginalNativeShaderRestoration.ApplyPending();SourceShaders.BeginRendering();}
    public static void End(){SourceShaders.EndRendering();}
}
