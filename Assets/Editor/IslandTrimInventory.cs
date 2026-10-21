using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class IslandTrimInventory
{
    [Serializable] public class Item {public string scene,path,types;public Vector3 position,min,max;public int objects,renderers,colliders;public bool active;}
    [Serializable] public class Land {public string scene,path,data;public Vector3 position,size,min,max;public int aboveWaterSamples;public Vector3[] shorelineSamples;}
    [Serializable] public class Report {public string utc;public Item[] items;public Land[] terrains;}
    static readonly Regex Names=new Regex("tunnel|building|house|shop|market|restaurant|terrain|city|bridge|boss|gate|village",RegexOptions.IgnoreCase);
    static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
    static string Hierarchy(Transform t)=>t.parent?Hierarchy(t.parent)+"/"+t.name:t.name;
    static int Depth(Transform t){int n=0;while(t.parent){n++;t=t.parent;}return n;}
    public static void Run()
    {
        var items=new List<Item>();var terrains=new List<Land>();
        var scenes=new[]{IslandFrontEndBuilder.GameScene}.Concat(Directory.GetFiles(IslandFrontEndBuilder.DressingRoot,"*.unity").OrderBy(p=>p)).ToArray();
        foreach(var path in scenes)
        {
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            foreach(var t in scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true)))
            {
                if(t.GetComponentInParent<Canvas>())continue;
                bool important=Names.IsMatch(t.name)&&!t.name.Contains("LOD")&&t.childCount>0;
                if(Depth(t)>2&&!important)continue;
                var rs=t.GetComponentsInChildren<Renderer>(true);var bounds=new Bounds(t.position,Vector3.zero);bool first=true;
                foreach(var r in rs){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
                items.Add(new Item{scene=scene.name,path=Hierarchy(t),types=string.Join(",",t.GetComponents<Component>().Where(c=>c).Select(c=>c.GetType().Name)),position=t.position,min=bounds.min,max=bounds.max,objects=t.GetComponentsInChildren<Transform>(true).Length,renderers=rs.Length,colliders=t.GetComponentsInChildren<Collider>(true).Length,active=t.gameObject.activeSelf});
            }
            foreach(var terrain in scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Terrain>(true)))
            {
                var data=terrain.terrainData;var points=new List<Vector3>();var bounds=new Bounds();bool first=true;int count=0;
                int step=Math.Max(1,data.heightmapResolution/128);var heights=data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution);
                for(int z=0;z<data.heightmapResolution;z+=step)for(int x=0;x<data.heightmapResolution;x+=step)
                {
                    float y=terrain.transform.position.y+heights[z,x]*data.size.y;
                    if(y<-3.5f)continue;count++;
                    var p=terrain.transform.position+new Vector3(x*data.size.x/(data.heightmapResolution-1),heights[z,x]*data.size.y,z*data.size.z/(data.heightmapResolution-1));
                    if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                    if(y<1.5f)points.Add(p);
                }
                terrains.Add(new Land{scene=scene.name,path=Hierarchy(terrain.transform),data=AssetDatabase.GetAssetPath(data),position=terrain.transform.position,size=data.size,min=bounds.min,max=bounds.max,aboveWaterSamples=count,shorelineSamples=points.Where((p,i)=>i%8==0).ToArray()});
            }
            Debug.Log("ISLAND_TRIM_INVENTORY "+scene.name);
        }
        File.WriteAllText(Path.Combine(Root,"reports/island-trim-inventory.json"),JsonUtility.ToJson(new Report{utc=DateTime.UtcNow.ToString("o"),items=items.ToArray(),terrains=terrains.ToArray()},true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);EditorApplication.Exit(0);
    }
}
