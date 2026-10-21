using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PaperPixelGames.Framework.States.MainMenu;

public static class WorldIssueFollowup
{
    [Serializable] public class Row {public string scene,path,components,json;public Vector2 anchorMin,anchorMax,pivot,size,position;}
    [Serializable] public class Report {public string utc;public Row[] rows;}
    [Serializable] public class ShaderRow {public string name,path,bundle;public int instance;public bool supported;}
    [Serializable] public class ShaderReport {public string utc;public int restoredShaderCount;public string[] bundles;public ShaderRow[] shaders;}
    public static void InspectShaders()
    {
        OriginalNativeShaderRestoration.Apply();
        var rows=new List<ShaderRow>();
        foreach(var bundle in AssetBundle.GetAllLoadedAssetBundles())
        foreach(var shader in bundle.LoadAllAssets<Shader>())
            rows.Add(new ShaderRow{name=shader.name,bundle=bundle.name,instance=shader.GetInstanceID(),supported=shader.isSupported});
        foreach(var guid in AssetDatabase.FindAssets("t:Shader"))
        {
            var path=AssetDatabase.GUIDToAssetPath(guid);var shader=AssetDatabase.LoadAssetAtPath<Shader>(path);
            if(shader&&(shader.name.Contains("Impostor")||shader.name.Contains("Stylized Lit URP"))) rows.Add(new ShaderRow{name=shader.name,path=path,instance=shader.GetInstanceID(),supported=shader.isSupported});
        }
        File.WriteAllText(Path.Combine(IslandWorldIssues.Reports,"world-issues-shader-state.json"),JsonUtility.ToJson(new ShaderReport{utc=DateTime.UtcNow.ToString("o"),restoredShaderCount=OriginalNativeShaderRestoration.LoadedShaderCount,bundles=AssetBundle.GetAllLoadedAssetBundles().Select(b=>b.name+" assets="+b.GetAllAssetNames().Length).ToArray(),shaders=rows.ToArray()},true));
    }
    public static void Inspect()
    {
        var rows=new List<Row>();
        foreach(var path in new[]{IslandFrontEndBuilder.MenuScene,IslandFrontEndBuilder.GameScene})
        {
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            foreach(var screen in IslandWorldIssues.All(scene).Select(t=>t.GetComponent<SettingsScreen>()).Where(s=>s))
            {
                var root=new SerializedObject(screen).FindProperty("_controlsContainer").objectReferenceValue as RectTransform;
                if(!root)continue;
                foreach(var rect in root.GetComponentsInChildren<RectTransform>(true))
                    rows.Add(new Row{scene=path,path=IslandWorldIssues.PathOf(rect),anchorMin=rect.anchorMin,anchorMax=rect.anchorMax,pivot=rect.pivot,size=rect.sizeDelta,position=rect.anchoredPosition,components=string.Join(",",rect.GetComponents<Component>().Where(c=>c).Select(c=>c.GetType().Name)),json=string.Join("\n",rect.GetComponents<Component>().Where(c=>c&&(c is LayoutGroup||c is ContentSizeFitter||c is LayoutElement)).Select(c=>JsonUtility.ToJson(c)))});
            }
        }
        File.WriteAllText(Path.Combine(IslandWorldIssues.Reports,"world-issues-followup-inventory.json"),JsonUtility.ToJson(new Report{utc=DateTime.UtcNow.ToString("o"),rows=rows.ToArray()},true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
    }
}
