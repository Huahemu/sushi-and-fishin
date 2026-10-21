using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class CasinoCanonicalLayerRepair
{
    [Serializable]class Change{public long fileId;public string name;public int sourceLayer,currentLayer,expectedLayer;}
    [Serializable]class Mask{public long fileId;public string field;public uint source,current,expected;}
    [Serializable]class Asset{public string path;public Change[] changes;public Mask[] fields;}
    [Serializable]class Input{public string owner;public Asset[] assets;}
    [Serializable]class Receipt{public string utc,error;public int assets,objects,masks,unusedDebugAssets;public bool passed;}
    public static void Run()
    {
        var result=new Receipt{utc=DateTime.UtcNow.ToString("o")};
        try
        {
            var data=JsonUtility.FromJson<Input>(File.ReadAllText(Path.Combine(IslandRevision20261018.Reports,"casino-canonical-layer-differences.json")));
            if(data.owner!="01a13e18-e061-7930-8a81-af528c1d37d5")throw new Exception("Wrong task owner.");
            foreach(var row in data.assets)
            {
                // SRDebugger autostart is disabled in the embedded module.
                // Its unused legacy popup is not a gameplay/rendering asset.
                if(row.path.Contains("/srdebugger/")){result.unusedDebugAssets++;continue;}
                if(!row.path.StartsWith("Assets/SushiAndFish/Casino/Imported/")||!row.path.EndsWith(".prefab"))throw new Exception("Unexpected repair scope: "+row.path);
                var root=AssetDatabase.LoadAssetAtPath<GameObject>(row.path);
                var objects=new Dictionary<long,Object>();
                foreach(var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    foreach(var obj in transform.GetComponents<Component>().Cast<Object>().Concat(new Object[]{transform.gameObject}).Where(o=>o))
                        if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out string guid,out long id))objects[id]=obj;
                }
                foreach(var change in row.changes)if(!objects.ContainsKey(change.fileId)||!(objects[change.fileId] is GameObject))throw new Exception("Cannot resolve object "+row.path+" "+change.fileId);
                foreach(var field in row.fields)if(!objects.ContainsKey(field.fileId)||new SerializedObject(objects[field.fileId]).FindProperty(field.field)==null)throw new Exception("Cannot resolve mask "+row.path+" "+field.field);
                var backup=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../backup/casino-migration-20261020-200219/canonical-layers",row.path));
                Directory.CreateDirectory(Path.GetDirectoryName(backup));if(!File.Exists(backup))File.Copy(row.path,backup);
                foreach(var change in row.changes){var go=(GameObject)objects[change.fileId];go.layer=change.expectedLayer;EditorUtility.SetDirty(go);result.objects++;}
                foreach(var field in row.fields)
                {
                    var serialized=new SerializedObject(objects[field.fileId]);var property=serialized.FindProperty(field.field);
                    if(property.propertyType==SerializedPropertyType.LayerMask||property.propertyType==SerializedPropertyType.Integer)property.intValue=unchecked((int)field.expected);
                    else{var bits=property.FindPropertyRelative("m_Bits");if(bits==null)throw new Exception("Unsupported mask "+field.field);bits.longValue=field.expected;}
                    serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(objects[field.fileId]);result.masks++;
                }
                PrefabUtility.SavePrefabAsset(root,out bool saved);if(!saved)throw new Exception("Prefab save failed: "+row.path);result.assets++;
            }
            AssetDatabase.SaveAssets();result.passed=true;
        }
        catch(Exception e){result.error=e.ToString();Debug.LogException(e);}
        File.WriteAllText(Path.Combine(IslandRevision20261018.Reports,"casino-canonical-layer-repair.json"),JsonUtility.ToJson(result,true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");EditorApplication.Exit(result.passed?0:1);
    }
}
