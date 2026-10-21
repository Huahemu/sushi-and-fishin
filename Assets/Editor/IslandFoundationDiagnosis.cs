using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Text.RegularExpressions;
using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.SceneManagement;
using PaperPixelGames.Core.DressingSceneSystem;using Object=UnityEngine.Object;

public static class IslandFoundationDiagnosis
{
    public static string Reports=>IslandFrontEndInventory.Reports;
    public static string P(Transform t)=>IslandFrontEndInventory.PathOf(t);
    public static Transform[] All(Scene s)=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
    [Serializable]public class Node
    {public string path,mesh,shader;public string[] materials;public Vector3 position,rotation,scale,min,max;public bool visible,active,collider;public int vertices,triangles;}
    [Serializable]public class Body{public Node node;public Vector3[] vertices;public int[] triangles;}
    [Serializable]public class Land{public string path,current,original;public Vector3 origin,size;public int changed,total;public float maxDifference;}
    [Serializable]public class Report
    {public string utc,error,stage;public List<Node> current=new List<Node>(),source=new List<Node>();public List<Body>bodies=new List<Body>();public List<Land>lands=new List<Land>();}
    static Node Describe(MeshRenderer r)
    {
        var f=r.GetComponent<MeshFilter>();var m=f?f.sharedMesh:null;var c=r.GetComponent<Collider>();
        return new Node{path=P(r.transform),mesh=AssetDatabase.GetAssetPath(m),materials=r.sharedMaterials.Select(x=>AssetDatabase.GetAssetPath(x)).ToArray(),shader=string.Join(";",r.sharedMaterials.Where(x=>x).Select(x=>x.shader?x.shader.name:"null")),position=r.transform.position,rotation=r.transform.eulerAngles,scale=r.transform.lossyScale,min=r.bounds.min,max=r.bounds.max,visible=r.enabled,active=r.gameObject.activeInHierarchy,collider=c&&c.enabled,vertices=m?m.vertexCount:0,triangles=m?m.triangles.Length/3:0};
    }
    static bool Candidate(MeshRenderer r)
    {var p=P(r.transform);return Regex.IsMatch(r.name,@"wall|stairs|sidewalk|street|fence|railing|curb|^zen$|island|ground|WorldCollider|foundation|cliff",RegexOptions.IgnoreCase)||r.bounds.size.x>60&&r.bounds.size.z>40;}
    [Serializable]public class Gap{public string path;public Vector3 point;public float current,native;public bool hole,nativeHole;}
    [Serializable]public class Gaps{public string utc;public List<Gap>points=new List<Gap>();public List<string>holes=new List<string>();}
    [Serializable]public class HeightProof{public string utc,error;public List<string>rows=new List<string>();}
    public static void ProbeNativeHeights()
    {
        var result=new HeightProof{utc=DateTime.UtcNow.ToString("o")};
        try
        {
            const string folder="Assets/SushiAndFish/RevisionReference_20261021/NativeTerrainData";Directory.CreateDirectory(folder);
            var gs=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene);
            var points=new[]{new Vector3(80,0,30),new Vector3(115,0,-23),new Vector3(115,0,-15),new Vector3(116.45f,0,57.67f),new Vector3(-100,0,-80),new Vector3(60,0,-90)};
            foreach(var t in All(gs).Select(x=>x.GetComponent<Terrain>()).Where(t=>t&&t.name.StartsWith("Terrain_")))
            {
                var raw=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../export/DeepBlueSushi-v0.3.6-raw/ExportedProject/Assets/TerrainData/"+t.name+".asset"));
                var path=folder+"/"+t.name+".asset";if(!File.Exists(path)){File.Copy(raw,path);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);}
                var native=AssetDatabase.LoadAssetAtPath<TerrainData>(path);var prior=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/TerrainData/"+t.name+".asset");
                int n=native.heightmapResolution;var a=native.GetHeights(0,0,n,n);var b=prior.GetHeights(0,0,n,n);float diff=0;int changed=0;
                for(int z=0;z<n;z++)for(int x=0;x<n;x++){float delta=Mathf.Abs(a[z,x]-b[z,x])*native.size.y;diff=Mathf.Max(diff,delta);if(delta>.001f)changed++;}
                result.rows.Add(t.name+" rawVsProjectOriginal changed="+changed+" max="+diff+" nativeSize="+native.size+" projectSize="+prior.size);
                foreach(var p in points)
                {
                    var local=p-t.transform.position;float x=local.x/t.terrainData.size.x,z=local.z/t.terrainData.size.z;
                    if(x<0||x>1||z<0||z>1)continue;
                    result.rows.Add(p+" RAW="+(native.GetInterpolatedHeight(x,z)+t.transform.position.y)+" baseline="+(prior.GetInterpolatedHeight(x,z)+t.transform.position.y)+" current="+(t.terrainData.GetInterpolatedHeight(x,z)+t.transform.position.y));
                }
            }
        }
        catch(Exception e){result.error=e.ToString();Debug.LogException(e);}
        finally{File.WriteAllText(Path.Combine(Reports,"island-foundation-native-heights.json"),JsonUtility.ToJson(result,true));EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");}
    }
    [Serializable]public class DeepReport{public string utc,error;public List<Node>nativeGame=new List<Node>(),otherSource=new List<Node>();public List<string>comparisons=new List<string>();public List<Body>geometry=new List<Body>();}
    public static void InspectDeep()
    {
        var result=new DeepReport{utc=DateTime.UtcNow.ToString("o")};
        try
        {
            const string reference="Assets/SushiAndFish/RevisionReference_20261021/OriginalGameStateBeforeTrim.unity";
            if(!File.Exists(reference))
            {File.Copy(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../backup/island-before-city-trim-20261016-095412/Assets/SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity")),reference);AssetDatabase.ImportAsset(reference,ImportAssetOptions.ForceSynchronousImport);}
            var originalGame=EditorSceneManager.OpenScene(reference);
            foreach(var r in All(originalGame).Select(t=>t.GetComponent<MeshRenderer>()).Where(r=>r))result.nativeGame.Add(Describe(r));
            var gs=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene);var ds=EditorSceneManager.OpenScene(IslandFrontEndBuilder.DressingRoot+"/100_Dressing_City_01.unity",OpenSceneMode.Additive);
            var current=All(gs).Concat(All(ds)).Select(t=>t.GetComponent<MeshFilter>()).Where(f=>f&&f.sharedMesh).ToArray();
            var original=EditorSceneManager.OpenScene(ScreenshotFollowupRepair.Source,OpenSceneMode.Additive);
            foreach(var r in All(original).Select(t=>t.GetComponent<MeshRenderer>()).Where(r=>r&&!Candidate(r)))
            {
                var b=r.bounds;
                if(b.min.y<5&&b.max.y>-8&&((b.min.x<125&&b.max.x>45&&b.min.z<40&&b.max.z>20)||(b.min.x<130&&b.max.x>105&&b.min.z<20&&b.max.z>-60)))result.otherSource.Add(Describe(r));
            }
            foreach(var f in All(original).Select(t=>t.GetComponent<MeshFilter>()).Where(f=>f&&f.sharedMesh&&f.GetComponent<MeshRenderer>()&&Regex.IsMatch(f.name,"wall|stairs|sidewalk|street|curb|railing",RegexOptions.IgnoreCase)))
            {
                var match=current.FirstOrDefault(c=>c.sharedMesh==f.sharedMesh&&Vector3.Distance(c.transform.position,f.transform.position)<.002f);if(!match)continue;
                float worst=0;var v=f.sharedMesh.vertices;for(int i=0;i<v.Length;i++)worst=Mathf.Max(worst,Vector3.Distance(f.transform.TransformPoint(v[i]),match.transform.TransformPoint(v[i])));
                if(worst>.002f)result.comparisons.Add(P(f.transform)+" -> "+P(match.transform)+" worldVertexError="+worst+" determinant="+f.transform.localToWorldMatrix.determinant+"/"+match.transform.localToWorldMatrix.determinant);
                if(f.name=="SideWalkWalls"||f.name=="SideWalkTerrainEdge"||Regex.IsMatch(P(f.transform),@"Comp9 \(17\)/SM_sidewalk_long"))
                    result.geometry.Add(new Body{node=Describe(f.GetComponent<MeshRenderer>()),vertices=v.Select(f.transform.TransformPoint).ToArray(),triangles=f.sharedMesh.triangles});
            }
        }
        catch(Exception e){result.error=e.ToString();Debug.LogException(e);}
        finally{File.WriteAllText(Path.Combine(Reports,"island-foundation-deep.json"),JsonUtility.ToJson(result,false));EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");}
    }
    public static void InspectGaps()
    {
        var result=new Gaps{utc=DateTime.UtcNow.ToString("o")};
        var gs=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene);var ds=EditorSceneManager.OpenScene(IslandFrontEndBuilder.DressingRoot+"/100_Dressing_City_01.unity",OpenSceneMode.Additive);
        var nodes=All(gs).Concat(All(ds)).ToArray();var lands=nodes.Select(t=>t.GetComponent<Terrain>()).Where(t=>t).ToArray();
        var native=lands.ToDictionary(t=>t,t=>AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/TerrainData/"+t.name+".asset"));
        foreach(var land in lands.Where(t=>native[t]))
        {var d=land.terrainData;var h=d.GetHoles(0,0,d.holesResolution,d.holesResolution);var s=native[land].GetHoles(0,0,native[land].holesResolution,native[land].holesResolution);result.holes.Add(land.name+" current="+h.Cast<bool>().Count(x=>!x)+" source="+s.Cast<bool>().Count(x=>!x));}
        foreach(var f in nodes.Select(t=>t.GetComponent<MeshFilter>()).Where(f=>f&&f.sharedMesh&&Regex.IsMatch(f.name,@"stairs|sidewalk|street|curb",RegexOptions.IgnoreCase)))
        {
            var mesh=f.sharedMesh;var v=mesh.vertices;var triangles=mesh.triangles;var seen=new HashSet<Vector3>();
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=f.transform.TransformPoint(v[triangles[i]]);var b=f.transform.TransformPoint(v[triangles[i+1]]);var c=f.transform.TransformPoint(v[triangles[i+2]]);
                if(Vector3.Cross(b-a,c-a).normalized.y<.5f)continue;var p=(a+b+c)/3;
                if(p.y<-.5f||p.y>12||!seen.Add(p))continue;
                var t=lands.FirstOrDefault(t=>native[t]&&p.x>=t.transform.position.x&&p.x<=t.transform.position.x+t.terrainData.size.x&&p.z>=t.transform.position.z&&p.z<=t.transform.position.z+t.terrainData.size.z);
                if(!t)continue;var o=t.transform.position;var d=t.terrainData;float x=(p.x-o.x)/d.size.x,z=(p.z-o.z)/d.size.z;
                result.points.Add(new Gap{path=P(f.transform),point=p,current=d.GetInterpolatedHeight(x,z)+o.y,native=native[t].GetInterpolatedHeight(x,z)+o.y,hole=t.terrainData.IsHole(Mathf.Clamp((int)(x*d.holesResolution),0,d.holesResolution-1),Mathf.Clamp((int)(z*d.holesResolution),0,d.holesResolution-1)),nativeHole=native[t].IsHole(Mathf.Clamp((int)(x*native[t].holesResolution),0,native[t].holesResolution-1),Mathf.Clamp((int)(z*native[t].holesResolution),0,native[t].holesResolution-1))});
            }
        }
        File.WriteAllText(Path.Combine(Reports,"island-foundation-gaps.json"),JsonUtility.ToJson(result,false));EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
    }
    public static void Inspect()
    {
        var result=new Report{utc=DateTime.UtcNow.ToString("o"),stage="DIAGNOSIS"};
        try
        {
            var gs=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene);
            var ds=EditorSceneManager.OpenScene(IslandFrontEndBuilder.DressingRoot+"/100_Dressing_City_01.unity",OpenSceneMode.Additive);
            var nodes=All(gs).Concat(All(ds)).ToArray();
            foreach(var d in nodes.Select(t=>t.GetComponent<DressingSceneRoot>()).Where(d=>d))d.ActivateAllImmediate();
            foreach(var r in nodes.Select(t=>t.GetComponent<MeshRenderer>()).Where(r=>r&&Candidate(r)))result.current.Add(Describe(r));
            foreach(var t in nodes.Select(x=>x.GetComponent<Terrain>()).Where(t=>t))
            {
                var source=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/TerrainData/"+t.name+".asset");if(!source)continue;
                var a=t.terrainData;int n=a.heightmapResolution;var h=a.GetHeights(0,0,n,n);var s=source.GetHeights(0,0,n,n);
                var row=new Land{path=P(t.transform),current=AssetDatabase.GetAssetPath(a),original=AssetDatabase.GetAssetPath(source),origin=t.transform.position,size=a.size,total=n*n};
                for(int z=0;z<n;z++)for(int x=0;x<n;x++){float delta=Mathf.Abs(h[z,x]-s[z,x])*a.size.y;if(delta>.001f)row.changed++;row.maxDifference=Mathf.Max(row.maxDifference,delta);}
                result.lands.Add(row);
            }
            var original=EditorSceneManager.OpenScene(ScreenshotFollowupRepair.Source,OpenSceneMode.Additive);
            foreach(var r in All(original).Select(t=>t.GetComponent<MeshRenderer>()).Where(r=>r&&Candidate(r)))
            {
                var row=Describe(r);result.source.Add(row);
                if(r.name=="WorldCollider"||r.name=="zen")
                {var mesh=r.GetComponent<MeshFilter>().sharedMesh;result.bodies.Add(new Body{node=row,vertices=mesh.vertices.Select(r.transform.TransformPoint).ToArray(),triangles=mesh.triangles});}
            }
            result.stage="INSPECTED";
        }
        catch(Exception e){result.error=e.ToString();result.stage="FAILED";Debug.LogException(e);}
        finally
        {File.WriteAllText(Path.Combine(Reports,"island-foundation-diagnosis.json"),JsonUtility.ToJson(result,false));EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");}
    }
}
