using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Restore the authoring registration that is absent from a published player.
// All registered shader and pipeline data comes from the original game.
[InitializeOnLoad]
public static class RestoredRenderPipelineSupport
{
    static RestoredRenderPipelineSupport()
    {
        // Localized UI can enable while Unity restores its last open scene,
        // before delayCall. Activate the original preloaded settings first.
        if(!AssetDatabase.IsAssetImportWorkerProcess())
        {
            foreach(var asset in PlayerSettings.GetPreloadedAssets())
                if(asset is UnityEngine.Localization.Settings.LocalizationSettings localization)
                    UnityEngine.Localization.Settings.LocalizationSettings.Instance=localization;
        }
        EditorApplication.delayCall += RegisterWhenImportComplete;
    }

    private static void RegisterWhenImportComplete()
    {
        if (EditorApplication.isUpdating || EditorApplication.isCompiling)
        {
            EditorApplication.delayCall += RegisterWhenImportComplete;
            return;
        }
        RegisterOriginalAssets();
    }

    public static void RegisterOriginalAssets()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess()) return;
        EditorGraphicsModeCompatibility.Apply();
        // Player preloaded assets must also be active while authoring in the
        // Editor. Otherwise localized original UI throws before entering Play.
        foreach (var asset in PlayerSettings.GetPreloadedAssets())
        {
            var localization = asset as UnityEngine.Localization.Settings.LocalizationSettings;
            if (localization != null)
                UnityEngine.Localization.Settings.LocalizationSettings.Instance = localization;
        }
        var settings = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(
            "Assets/MonoBehaviour/UniversalRenderPipelineGlobalSettings.asset");
        if (settings != null && EditorGraphicsSettings.GetRenderPipelineGlobalSettingsAsset(typeof(UniversalRenderPipeline)) != settings)
        {
            EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>(settings);
            AssetDatabase.SaveAssets();
        }
        OriginalNativeShaderRestoration.Apply();
    }
}
