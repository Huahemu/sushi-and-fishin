using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class IslandFrontEndVisualDiagnostic
{
    [Serializable] class Row {public string path,kind,details;public bool enabled;public Vector3 position,size;}
    [Serializable] class Report {public Row[] rows;}
    public static void Run()
    {
        var rows=new List<Row>();
        var game=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene,OpenSceneMode.Single);
        foreach(var r in game.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<MeshRenderer>(true)).Where(r=>r.bounds.size.magnitude>100))
            rows.Add(new Row{path=IslandFrontEndInventory.PathOf(r.transform),kind="large original renderer",enabled=r.enabled,position=r.bounds.center,size=r.bounds.size,details=string.Join(";",r.sharedMaterials.Select(m=>m?m.name+" : "+(m.shader?m.shader.name:"null"):"null"))});
        var menu=EditorSceneManager.OpenScene(IslandFrontEndBuilder.MenuScene,OpenSceneMode.Single);
        foreach(var b in menu.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<MonoBehaviour>(true)).Where(b=>b && b.GetType().Name.Contains("Water")))
        {
            var so=new SerializedObject(b);var it=so.GetIterator();var details=new List<string>();
            while(it.NextVisible(true))if(it.propertyType==SerializedPropertyType.ObjectReference)details.Add(it.name+"="+(it.objectReferenceValue?it.objectReferenceValue.name:"null"));else if(it.propertyType==SerializedPropertyType.Float)details.Add(it.name+"="+it.floatValue);
            rows.Add(new Row{path=IslandFrontEndInventory.PathOf(b.transform),kind=b.GetType().FullName,enabled=b.isActiveAndEnabled,position=b.transform.position,details=string.Join(";",details)});
        }
        File.WriteAllText(Path.Combine(IslandFrontEndInventory.Reports,"island-front-end-visual-diagnostic.json"),JsonUtility.ToJson(new Report{rows=rows.ToArray()},true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);
    }
}
