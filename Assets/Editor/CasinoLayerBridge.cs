using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SushiAndFish.Casino;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CasinoLayerBridge
{
    const string Prefix="Assets/SushiAndFish/Casino/Imported/";
    static string Reports=>IslandRevision20261018.Reports;
    [Serializable]class Row{public string path;public int changes;}
    [Serializable]class Report{public int version=1;public string utc;public List<Row> assets=new List<Row>();public int[] mapping=CasinoLayers.Map;}
    public static void Apply()
    {
        string done=Path.Combine(Reports,"casino-layer-bridge.json");if(File.Exists(done))return;
        var report=new Report{utc=DateTime.UtcNow.ToString("o")};
        var tagManager=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers=tagManager.FindProperty("layers");
        foreach(var pair in new[]{(3,"CasinoGround"),(28,"CasinoPlinkoPuck"),(29,"CasinoDiceZone"),(30,"CasinoDice"),(31,"CasinoPlinkoWalls")})
        {
            var layer=layers.GetArrayElementAtIndex(pair.Item1);
            if(!string.IsNullOrEmpty(layer.stringValue)&&layer.stringValue!=pair.Item2)throw new Exception("Casino requires unused layer "+pair.Item1+" but found "+layer.stringValue);
            layer.stringValue=pair.Item2;
        }
        var tags=tagManager.FindProperty("tags");
        foreach(var tag in new[]{"Target","Missile","menu_hit","menu_play","menu_interact","menu_call","menu_quit","menu_settings","menu_link","menu_invite"})
        {
            bool found=false;for(int i=0;i<tags.arraySize;i++)found|=tags.GetArrayElementAtIndex(i).stringValue==tag;
            if(!found){tags.InsertArrayElementAtIndex(tags.arraySize);tags.GetArrayElementAtIndex(tags.arraySize-1).stringValue=tag;}
        }
        tagManager.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();
        var paths=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith(Prefix,StringComparison.Ordinal)).ToArray();
        foreach(string path in paths.Where(p=>p.EndsWith(".prefab")))
        {
            var root=PrefabUtility.LoadPrefabContents(path);int changes=0;
            try
            {
                foreach(var t in root.GetComponentsInChildren<Transform>(true))
                {
                    changes+=Patch(t.gameObject);
                    foreach(var c in t.GetComponents<Component>())if(c&&!(c is Transform))changes+=Patch(c);
                }
                if(changes>0){Backup(path);PrefabUtility.SaveAsPrefabAsset(root,path);}
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            if(changes>0)report.assets.Add(new Row{path=path,changes=changes});
        }
        foreach(string path in paths.Where(p=>p.EndsWith(".asset")))
        {
            int changes=0;
            foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if(asset is ScriptableObject){changes+=Patch(asset);if(changes>0)EditorUtility.SetDirty(asset);}
            if(changes>0){report.assets.Add(new Row{path=path,changes=changes});}
        }
        foreach(string path in paths.Where(p=>p.EndsWith(".unity")))
        {
            var scene=EditorSceneManager.OpenScene(path);int changes=0;
            foreach(var root in scene.GetRootGameObjects())
                foreach(var t in root.GetComponentsInChildren<Transform>(true))
                {changes+=Patch(t.gameObject);foreach(var c in t.GetComponents<Component>())if(c&&!(c is Transform))changes+=Patch(c);}
            if(changes>0){Backup(path);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);report.assets.Add(new Row{path=path,changes=changes});}
        }
        AssetDatabase.SaveAssets();File.WriteAllText(done,JsonUtility.ToJson(report,true));
    }
    static int Patch(UnityEngine.Object target)
    {
        var serialized=new SerializedObject(target);var p=serialized.GetIterator();int changed=0;bool instance=PrefabUtility.IsPartOfPrefabInstance(target);
        while(p.Next(true))
        {
            if(instance&&!p.prefabOverride)continue;
            if(p.propertyPath=="m_Layer")
            {int layer=p.intValue;if(layer>=0&&layer<32&&CasinoLayers.Map[layer]!=layer){p.intValue=CasinoLayers.Map[layer];changed++;}}
            else if(p.propertyType==SerializedPropertyType.LayerMask)
            {int old=p.intValue;int mapped=CasinoLayers.Mask(old);if(mapped!=old){p.intValue=mapped;changed++;}}
        }
        if(changed>0){string path=AssetDatabase.GetAssetPath(target);if(!string.IsNullOrEmpty(path))Backup(path);serialized.ApplyModifiedPropertiesWithoutUndo();}
        return changed;
    }
    static void Backup(string path)
    {
        var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../backup/casino-migration-20261020-200219/layer-bridge"));
        var destination=Path.Combine(folder,path.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(destination));if(!File.Exists(destination))File.Copy(path,destination);
    }
}
