using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class IslandTrimNavigationProbe
{
    [Serializable] public class Row {public string path;public Bounds bounds;}
    [Serializable] public class Sample {public string name;public Vector3 wanted,hit;public bool navigable;}
    [Serializable] public class Report {public Row[] surfaces;public Sample[] samples;}
    public static void Run()
    {
        var scene=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene,OpenSceneMode.Single);
        var rows=new List<Row>();
        foreach(var b in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)).Where(b=>b&&b.GetType().Name=="NavMeshSurface"))
        {var p=new SerializedObject(b).FindProperty("m_NavMeshData");if(p!=null&&p.objectReferenceValue is NavMeshData d)rows.Add(new Row{path=IslandFrontEndInventory.PathOf(b.transform),bounds=d.sourceBounds});}
        var points=new[]{new Sample{name="former_tunnel",wanted=new Vector3(6,3,-136)},new Sample{name="former_lighthouse",wanted=new Vector3(183,3,-89)},new Sample{name="shops",wanted=new Vector3(-5,3,-10)},new Sample{name="rear_mountain",wanted=new Vector3(-270,30,-150)}};
        foreach(var s in points){s.navigable=NavMesh.SamplePosition(s.wanted,out var hit,5,NavMesh.AllAreas);s.hit=hit.position;}
        File.WriteAllText(Path.Combine(IslandFrontEndInventory.Reports,"island-trim-navmesh-probe.json"),JsonUtility.ToJson(new Report{surfaces=rows.ToArray(),samples=points},true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);
    }
}
