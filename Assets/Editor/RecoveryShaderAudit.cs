using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class RecoveryShaderAudit
{
    [Serializable] public class Record
    {
        public string path, name;
        public bool supported;
        public int passCount, subshaderCount;
        public string[] passNames, messages;
    }
    [Serializable] public class Report { public string editor, device; public Record[] shaders; }
    public static void Run()
    {
        RestoredRenderPipelineSupport.RegisterOriginalAssets();
        var records = new List<Record>();
        foreach (var guid in AssetDatabase.FindAssets("t:Shader"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (!shader) continue;
            var material = new Material(shader);
            var names = new List<string>();
            for (int i = 0; i < material.passCount; i++) names.Add(material.GetPassName(i));
            var messages = new List<string>();
            foreach (var message in ShaderUtil.GetShaderMessages(shader)) messages.Add(message.severity + ": " + message.message);
            records.Add(new Record { path = path, name = shader.name, supported = shader.isSupported,
                passCount = material.passCount, subshaderCount = shader.subshaderCount,
                passNames = names.ToArray(), messages = messages.ToArray() });
            UnityEngine.Object.DestroyImmediate(material);
        }
        var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../reports"));
        File.WriteAllText(Path.Combine(directory, "unity-shader-diagnostic.json"),
            JsonUtility.ToJson(new Report { editor = Application.unityVersion, device = SystemInfo.graphicsDeviceType.ToString(), shaders = records.ToArray() }, true));
        EditorApplication.Exit(0);
    }
}
