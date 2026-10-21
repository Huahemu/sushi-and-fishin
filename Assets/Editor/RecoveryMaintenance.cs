using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

public static class RecoveryMaintenance
{
    [Serializable] public class ScriptRow { public string type, name, guid; public long fileID; }
    [Serializable] public class Rows { public List<ScriptRow> scripts = new List<ScriptRow>(); }
    private static AddRequest request;
    private static AddAndRemoveRequest collectionsRequest;
    private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
    public static void InstallMatchingInputSystem()
    {
        var rows = new Rows();
        string path = "Assets/Plugins/Unity.InputSystem.dll";
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (!(asset is MonoScript script)) continue;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string guid, out long fileID);
            rows.scripts.Add(new ScriptRow { type = script.GetClass()?.FullName, name = script.name, guid = guid, fileID = fileID });
        }
        File.WriteAllText(Path.Combine(Root, "reports/input-system-original-monoscripts.json"), JsonUtility.ToJson(rows, true));
        var backup = Path.Combine(Root, "export/original-player-input-system");
        Directory.CreateDirectory(backup);
        File.Move(path, Path.Combine(backup, "Unity.InputSystem.dll"));
        File.Move(path + ".meta", Path.Combine(backup, "Unity.InputSystem.dll.meta"));
        request = Client.Add("com.unity.inputsystem@1.18.0");
        EditorApplication.update += Poll;
    }
    private static void Poll()
    {
        if (request == null || !request.IsCompleted) return;
        EditorApplication.update -= Poll;
        File.WriteAllText(Path.Combine(Root, "reports/input-system-package-install.json"),
            request.Status == StatusCode.Success ? JsonUtility.ToJson(request.Result, true) : request.Error.message);
        EditorApplication.Exit(request.Status == StatusCode.Success ? 0 : 2);
    }
    public static void InstallEditorCollections()
    {
        var backup=Path.Combine(Root,"export/original-player-collections-assemblies");
        Directory.CreateDirectory(backup);
        foreach(var name in new[]{"Unity.Collections.dll","Unity.Collections.LowLevel.ILSupport.dll","Unity.Burst.dll","Unity.Burst.Unsafe.dll","Unity.Mathematics.dll"})
        {
            var path="Assets/Plugins/"+name;
            File.Move(path,Path.Combine(backup,name));
            File.Move(path+".meta",Path.Combine(backup,name+".meta"));
        }
        collectionsRequest=Client.AddAndRemove(new[]{"com.unity.collections@2.4.3","com.unity.burst@1.8.14","com.unity.mathematics@1.3.2"});
        EditorApplication.update+=PollCollections;
    }
    private static void PollCollections()
    {
        if(collectionsRequest==null || !collectionsRequest.IsCompleted)return;
        EditorApplication.update-=PollCollections;
        var success=collectionsRequest.Status==StatusCode.Success;
        File.WriteAllText(Path.Combine(Root,"reports/editor-collections-package-install.txt"),success?"Installed Collections 2.4.3, Burst 1.8.14, Mathematics 1.3.2, matching the installed SRP SDK dependency declarations.":collectionsRequest.Error.message);
        EditorApplication.Exit(success?0:2);
    }
    public static void DiagnoseShaderAssets()
    {
        var lines = new List<string>();
        foreach (var path in Directory.GetFiles("Packages", "*.shader", SearchOption.AllDirectories))
        {
            string assetPath = path.Replace('\\', '/');
            lines.Add(assetPath + " | " + AssetImporter.GetAtPath(assetPath)?.GetType().FullName + " | " + AssetDatabase.GetMainAssetTypeAtPath(assetPath));
        }
        File.WriteAllLines(Path.Combine(Root, "reports/shader-importer-types.txt"), lines);
        InstallMatchingInputSystem();
    }
}
