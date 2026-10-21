using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SushiAndFish.Casino.Original;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CasinoDoorGeometryProbe
{
    [Serializable]public class Row{public float scale;public bool clear;public string blocker;public Vector3 hit,normal;public float distance;public string[] initialOverlap;}
    [Serializable]public class Report{public string utc,error;public Vector3 leftDoor,rightDoor;public List<Row> rows=new List<Row>();}
    public static void Run()
    {
        var report=new Report{utc=DateTime.UtcNow.ToString("o")};
        try
        {
            var scene=EditorSceneManager.OpenScene("Assets/SushiAndFish/Casino/Imported/Scenes/CasinoScene.unity");
            var originalRoots=scene.GetRootGameObjects();var root=new GameObject("TemporaryGeometryProbeRoot");
            foreach(var obj in originalRoots)obj.transform.SetParent(root.transform,true);
            foreach(var floor in root.GetComponentsInChildren<CasinoFloor>(true))floor.gameObject.SetActive(floor.floorIndex==0);
            var elevator=root.GetComponentInChildren<ElevatorManager>(true);var serialized=new SerializedObject(elevator);
            report.leftDoor=((Transform)serialized.FindProperty("leftDoor").objectReferenceValue).localPosition;
            report.rightDoor=((Transform)serialized.FindProperty("rightDoor").objectReferenceValue).localPosition;
            foreach(float scale in new[]{.35f,.4f,.45f,.47f,.48f,.49f,.5f,1f})
            {
                root.transform.localScale=Vector3.one*scale;Physics.SyncTransforms();
                Vector3 feet=root.transform.TransformPoint(new Vector3(0,.3f,48));
                Vector3 bottom=feet+Vector3.up*.4f,top=feet+Vector3.up*1.45f;
                var row=new Row{scale=scale};
                row.initialOverlap=Physics.OverlapCapsule(bottom,top,.399f,~0,QueryTriggerInteraction.Ignore).Select(c=>PathOf(c.transform)).Take(8).ToArray();
                var hits=Physics.CapsuleCastAll(bottom,top,.4f,Vector3.forward,16*scale,~0,QueryTriggerInteraction.Ignore).Where(h=>h.normal.z<-.2f).OrderBy(h=>h.distance).ToArray();
                row.clear=hits.Length==0;
                if(hits.Length>0){var h=hits[0];row.blocker=PathOf(h.collider.transform);row.hit=root.transform.InverseTransformPoint(h.point);row.normal=h.normal;row.distance=h.distance;}
                report.rows.Add(row);
            }
        }
        catch(Exception e){report.error=e.ToString();}
        File.WriteAllText(System.IO.Path.Combine(IslandRevision20261018.Reports,"casino-door-geometry-probe.json"),JsonUtility.ToJson(report,true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");EditorApplication.Exit(string.IsNullOrEmpty(report.error)?0:1);
    }
    static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
}
