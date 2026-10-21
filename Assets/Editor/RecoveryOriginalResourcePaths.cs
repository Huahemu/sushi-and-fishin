using System;
using System.IO;
using UnityEditor;
using UnityEngine;
public static class RecoveryOriginalResourcePaths
{
    public static void RestoreOutlineMesh()
    {
        const string oldPrefab="Assets/Resources/easy performant outline/models/Rounded box.prefab";
        const string prefab="Assets/GameObject/Rounded box.prefab";
        const string resource="Assets/Resources/easy performant outline/models/Rounded box.asset";
        string mesh=AssetDatabase.GUIDToAssetPath("fce7a0aebe560bc4dbd39d324e1060f6");
        var before=AssetDatabase.LoadAssetAtPath<Mesh>(mesh);
        if(!before)throw new InvalidOperationException("Original outline mesh is absent");
        string first=AssetDatabase.MoveAsset(oldPrefab,prefab);
        if(first!="")throw new IOException(first);
        string second=AssetDatabase.MoveAsset(mesh,resource);
        if(second!="")throw new IOException(second);
        AssetDatabase.Refresh();
        var restored=Resources.Load<Mesh>("Easy performant outline/Models/Rounded box");
        if(!restored || restored!=before)throw new InvalidOperationException("Original typed mesh lookup did not resolve");
        var method=typeof(EPOOutline.BlitUtility).GetMethod("CheckModel",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        method.Invoke(null,null);
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../reports/original-outline-resource-path-restoration.txt")),
            "PASS: Original Mesh GUID fce7a0aebe560bc4dbd39d324e1060f6 resolves at its original runtime Resources.Load<Mesh> path. Original model prefab retained at "+prefab+". Vertices="+before.vertexCount);
        EditorApplication.Exit(0);
    }
}
