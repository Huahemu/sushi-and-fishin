using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// ResourcePath attributes in the original SRP runtime resolve SDK shader paths
// below Packages. Keep the recovered GUID while restoring the expected path.
public static class RecoverySDKResourceLayout
{
    [Serializable] public class Entry { public string from, to, error; }
    [Serializable] public class Report { public Entry[] moved; public string[] resourceShaderReferences; }
    public static void Run()
    {
        Restore(false);
    }
    public static void RunAndRender()
    {
        Restore(true);
    }
    private static void Restore(bool render)
    {
        var rows = new List<Entry>();
        var shaderGuids = AssetDatabase.FindAssets("t:Shader", new[] { "Assets/RecoveredSDKShaderSources" });
        foreach (var guid in shaderGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".shader", StringComparison.Ordinal))
                Directory.CreateDirectory(Path.GetDirectoryName(path.Replace("Assets/RecoveredSDKShaderSources/", "Packages/")));
        }
        // MoveAsset requires the target parent folder to have an imported GUID.
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (var guid in shaderGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".shader", StringComparison.Ordinal)) continue;
            var destination = path.Replace("Assets/RecoveredSDKShaderSources/", "Packages/");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var error = AssetDatabase.MoveAsset(path, destination);
            rows.Add(new Entry { from = path, to = destination, error = error });
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var settingsPath = "Assets/MonoBehaviour/UniversalRenderPipelineGlobalSettings.asset";
        AssetDatabase.ImportAsset(settingsPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        RestoredRenderPipelineSupport.RegisterOriginalAssets();
        var settings = AssetDatabase.LoadMainAssetAtPath(settingsPath);
        var serialized = new SerializedObject(settings);
        var property = serialized.GetIterator();
        var references = new List<string>();
        while (property.Next(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
            var value = property.objectReferenceValue;
            if (property.propertyPath.Contains("PS") || value is Shader)
                references.Add(property.propertyPath + " -> " + (value ? value.name + " @ " + AssetDatabase.GetAssetPath(value) : "NULL"));
        }
        var reports = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../reports"));
        File.WriteAllText(Path.Combine(reports, "sdk-resource-layout-restoration.json"), JsonUtility.ToJson(new Report { moved = rows.ToArray(), resourceShaderReferences = references.ToArray() }, true));
        if (rows.Exists(r => !string.IsNullOrEmpty(r.error))) EditorApplication.Exit(2);
        else if (render) RecoveryProjectAudit.RenderMenuScene();
        else EditorApplication.Exit(0);
    }
}
