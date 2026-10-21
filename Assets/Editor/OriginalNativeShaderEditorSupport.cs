using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class OriginalNativeShaderEditorSupport
{
    static OriginalNativeShaderEditorSupport()
    {
        EditorApplication.delayCall += Initialize;
        EditorApplication.hierarchyChanged += HierarchyChanged;
        EditorApplication.projectChanged += OriginalNativeShaderRestoration.RequestRefresh;
        EditorApplication.playModeStateChanged += _ => OriginalNativeShaderRestoration.RequestRefresh();
        Undo.undoRedoPerformed += OriginalNativeShaderRestoration.RequestRefresh;
        ObjectChangeEvents.changesPublished += ObjectsChanged;
        AssemblyReloadEvents.beforeAssemblyReload += OriginalNativeShaderRestoration.RestoreSerializedReferences;
    }
    private static void Initialize()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess()) return;
        if (EditorApplication.isUpdating || EditorApplication.isCompiling)
        {
            EditorApplication.delayCall += Initialize;
            return;
        }
        OriginalNativeShaderRestoration.InstallHooks();
        OriginalNativeShaderRestoration.Apply();
    }
    private static void HierarchyChanged()
    {
        // Runtime scene loads and async outfit bindings have their own hooks.
        // NPC motion/spawning must not turn an Editor notification into a scan.
        if (!EditorApplication.isPlaying) OriginalNativeShaderRestoration.RequestRefresh();
    }
    private static void ObjectsChanged(ref ObjectChangeEventStream changes)
    {
        if (!EditorApplication.isPlaying && changes.length > 0)
            OriginalNativeShaderRestoration.RequestRefresh();
    }
    [MenuItem("Tools/Recovered original game/Reload original compiled shader programs")]
    public static void Apply() { OriginalNativeShaderRestoration.Apply(); SceneView.RepaintAll(); }
}

public sealed class OriginalNativeMaterialSaveBoundary : AssetModificationProcessor
{
    private static void OnWillCreateAsset(string path)
    {
        if(!path.EndsWith(".mat",System.StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".mat.meta",System.StringComparison.OrdinalIgnoreCase))return;
        OriginalNativeShaderRestoration.RestoreSerializedReferences();
        EditorApplication.delayCall += OriginalNativeShaderRestoration.ApplyPending;
    }
    private static string[] OnWillSaveAssets(string[] paths)
    {
        SushiAndFish.Casino.Original.RecoveryShaderSupport.RestoreAuthoringReferences();
        OriginalNativeShaderRestoration.RestoreSerializedReferences();
        EditorApplication.delayCall += OriginalNativeShaderRestoration.ApplyPending;
        return paths;
    }
}
