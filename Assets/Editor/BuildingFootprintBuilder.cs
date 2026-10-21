using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public static class BuildingFootprintBuilder
{
    const string Root="Assets/SushiAndFish/Modules/OriginalIsland/FootprintMarkers";
    const string SourceScene="Assets/Editor/FootprintSourceBeforeTrim.unity";
    const string Markers="RemovedBuildingFootprints";
    const float LineWidth=.35f,DashLength=1.3f,GapLength=.7f,Elevation=.045f,Margin=.2f;
    [Serializable] class Removal {public string scene,path,reason;}
    [Serializable] class RemovalReport {public Removal[] removed;}
    [Serializable] class Baseline {public string backup;}
    [Serializable] public class Site {public string id;public string[] sources;public Vector3 centre,right,forward;public float width,length;public bool attachment;}
    [Serializable] class Report {public string utc,stage,sourceBackup;public float lineWidth,dashLength,gapLength,elevation;public Color color;public Site[] sites;public int colliders,missingScripts;}
    static string Reports=>IslandFrontEndInventory.Reports;
    static readonly Color Yellow=new Color(246/255f,215/255f,122/255f,.94f);
    static IEnumerable<Transform> All(Scene s)=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true));
    static string PathOf(Transform t)=>IslandFrontEndInventory.PathOf(t);
    static IEnumerable<Vector3> Corners(Transform t)
    {
        foreach(var f in t.GetComponentsInChildren<MeshFilter>(true))
        {
            if(!f.sharedMesh)continue;var b=f.sharedMesh.bounds;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                yield return f.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));
        }
    }
    static Site Measure(Transform t,string path)
    {
        var forward=t.forward;forward.y=0;if(forward.sqrMagnitude<.01f)forward=Vector3.forward;forward.Normalize();
        var right=new Vector3(forward.z,0,-forward.x);float minX=float.MaxValue,maxX=float.MinValue,minZ=float.MaxValue,maxZ=float.MinValue;
        foreach(var p in Corners(t)){float x=Vector3.Dot(p,right),z=Vector3.Dot(p,forward);minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minZ=Mathf.Min(minZ,z);maxZ=Mathf.Max(maxZ,z);}
        if(minX==float.MaxValue)throw new InvalidOperationException("Source building has no mesh: "+path);
        return new Site{sources=new[]{path},right=right,forward=forward,centre=right*((minX+maxX)*.5f)+forward*((minZ+maxZ)*.5f),width=maxX-minX,length=maxZ-minZ,attachment=t.name.Contains("attachment")};
    }
    static void Union(Site target,Site other)
    {
        float cx=Vector3.Dot(target.centre,target.right),cz=Vector3.Dot(target.centre,target.forward);
        float minX=cx-target.width/2,maxX=cx+target.width/2,minZ=cz-target.length/2,maxZ=cz+target.length/2;
        foreach(float a in new[]{-1f,1f})foreach(float b in new[]{-1f,1f})
        {var p=other.centre+other.right*other.width*.5f*a+other.forward*other.length*.5f*b;float x=Vector3.Dot(p,target.right),z=Vector3.Dot(p,target.forward);minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minZ=Mathf.Min(minZ,z);maxZ=Mathf.Max(maxZ,z);}
        target.centre=target.right*(.5f*(minX+maxX))+target.forward*(.5f*(minZ+maxZ));target.width=maxX-minX;target.length=maxZ-minZ;target.sources=target.sources.Concat(other.sources).Distinct().ToArray();
    }
    static List<Site> Extract(string backup)
    {
        var receipt=JsonUtility.FromJson<RemovalReport>(File.ReadAllText(Path.Combine(Reports,"island-trim-edit-audit.json")));
        var source=Path.Combine(backup,IslandFrontEndBuilder.DressingRoot,"100_Dressing_City_01.unity");
        if(File.Exists(SourceScene))
        {
            using(var sha=System.Security.Cryptography.SHA256.Create())
            using(var a=File.OpenRead(source))using(var b=File.OpenRead(SourceScene))
                if(!sha.ComputeHash(a).SequenceEqual(sha.ComputeHash(b)))throw new InvalidOperationException("Temporary source differs from the verified backup");
        }
        else File.Copy(source,SourceScene);
        AssetDatabase.ImportAsset(SourceScene,ImportAssetOptions.ForceSynchronousImport);
        var scene=EditorSceneManager.OpenScene(SourceScene,OpenSceneMode.Single);var all=All(scene).GroupBy(PathOf).ToDictionary(g=>g.Key,g=>new Queue<Transform>(g));
        var sites=new List<Site>();
        foreach(var row in receipt.removed.Where(r=>r.scene=="100_Dressing_City_01"&&(r.reason=="decorative_building"||r.reason=="decorative_gazebo")))
        {
            if(!all.TryGetValue(row.path,out var matches)||matches.Count==0)throw new InvalidOperationException("Missing backed up building: "+row.path);
            var t=matches.Dequeue();
            sites.Add(Measure(t,row.path));
        }
        foreach(var a in sites.Where(s=>s.attachment).ToArray())
        {
            string parent=a.sources[0].Substring(0,a.sources[0].LastIndexOf('/'));
            var body=sites.Where(s=>!s.attachment&&s.sources.Any(p=>p.StartsWith(parent+"/",StringComparison.Ordinal))).OrderBy(s=>Vector3.Distance(s.centre,a.centre)).FirstOrDefault();
            if(body!=null){Union(body,a);sites.Remove(a);}
        }
        return sites;
    }
    static float Ground(float x,float z,Terrain[] terrains)
    {
        float y=float.NegativeInfinity;
        foreach(var t in terrains){var p=t.transform.position;var s=t.terrainData.size;if(x>=p.x&&x<=p.x+s.x&&z>=p.z&&z<=p.z+s.z)y=Mathf.Max(y,t.SampleHeight(new Vector3(x,0,z))+p.y);}
        if(float.IsNegativeInfinity(y))throw new InvalidOperationException("Footprint outside terrain at "+x+","+z);
        foreach(var hit in Physics.RaycastAll(new Vector3(x,100,z),Vector3.down,160,~0,QueryTriggerInteraction.Ignore))
        {
            string name=hit.collider.name.ToLowerInvariant();
            bool ground=hit.collider is TerrainCollider||new[]{"street","sidewalk","platform","ground","floor","pavement","foundation"}.Any(name.Contains);
            if(ground&&hit.normal.y>.6f&&hit.point.y>=y-.05f&&hit.point.y<y+3f)y=Mathf.Max(y,hit.point.y);
        }
        return y+Elevation;
    }
    static Mesh Geometry(Site site,Terrain[] terrains)
    {
        var v=new List<Vector3>();var indices=new List<int>();float hx=site.width*.5f+Margin,hz=site.length*.5f+Margin;
        var corners=new[]{site.centre-site.right*hx-site.forward*hz,site.centre+site.right*hx-site.forward*hz,site.centre+site.right*hx+site.forward*hz,site.centre-site.right*hx+site.forward*hz};
        for(int edge=0;edge<4;edge++)
        {
            var a=corners[edge];var b=corners[(edge+1)%4];float length=Vector3.Distance(a,b);var direction=(b-a).normalized;var side=new Vector3(-direction.z,0,direction.x)*LineWidth*.5f;
            int count=Mathf.Max(1,Mathf.RoundToInt(length/(DashLength+GapLength)));float step=length/count;float dash=Mathf.Min(DashLength,step*.72f);
            for(int i=0;i<count;i++)
            {
                float start=(i+.5f)*step-dash*.5f;int segments=Mathf.Max(1,Mathf.CeilToInt(dash/.25f));
                for(int j=0;j<segments;j++)
                {
                    var c=a+direction*(start+dash*j/segments);var d=a+direction*(start+dash*(j+1)/segments);int offset=v.Count;
                    foreach(var p in new[]{c-side,c+side,d+side,d-side})v.Add(new Vector3(p.x,Ground(p.x,p.z,terrains),p.z));
                    indices.AddRange(new[]{offset,offset+2,offset+1,offset,offset+3,offset+2});
                }
            }
        }
        var mesh=new Mesh{name=site.id};mesh.SetVertices(v);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play before authoring footprint markers");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Unsaved scene: "+SceneManager.GetSceneAt(i).path);
        string backup=JsonUtility.FromJson<Baseline>(File.ReadAllText(Path.Combine(Reports,"island-city-trim-baseline.json"))).backup;
        var sites=Extract(backup);var game=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene,OpenSceneMode.Single);
        if(game.GetRootGameObjects().Any(g=>g.name==Markers))throw new InvalidOperationException("Markers already exist; inspect before rebuilding");
        var city=EditorSceneManager.OpenScene(IslandFrontEndBuilder.DressingRoot+"/100_Dressing_City_01.unity",OpenSceneMode.Additive);
        var groups=city.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PaperPixelGames.Core.DressingSceneSystem.DressingActivationGroup>(true)).ToArray();var active=groups.Select(g=>g.gameObject.activeSelf).ToArray();foreach(var g in groups)g.SetActivated(true);
        Physics.SyncTransforms();Directory.CreateDirectory(Root+"/Meshes");AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/SoftYellowDashedOutline.shader");if(!shader||!shader.isSupported)throw new InvalidOperationException("Footprint shader unsupported");
        var material=new Material(shader){name="Soft Yellow Footprint Dashes"};material.SetColor("_BaseColor",Yellow);AssetDatabase.CreateAsset(material,Root+"/SoftYellowDashes.mat");
        var markerRoot=new GameObject(Markers);SceneManager.MoveGameObjectToScene(markerRoot,game);var terrains=game.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>(true)).ToArray();int id=0;
        foreach(var site in sites)
        {
            site.id="RemovedBuildingFootprint_"+(++id).ToString("00");site.centre.y=Ground(site.centre.x,site.centre.z,terrains)-Elevation;
            if(site.centre.y<-3.5f)throw new InvalidOperationException("Source footprint no longer on island: "+site.sources[0]);
            var mesh=Geometry(site,terrains);AssetDatabase.CreateAsset(mesh,Root+"/Meshes/"+site.id+".asset");
            var go=new GameObject(site.id);go.transform.SetParent(markerRoot.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            AssetDatabase.SaveAssetIfDirty(mesh);
        }
        for(int i=0;i<groups.Length;i++)if(groups[i])groups[i].SetActivated(active[i]);
        AssetDatabase.SaveAssetIfDirty(material);EditorSceneManager.SaveScene(game);
        typeof(IslandFrontEndBuilder).GetMethod("BuildBackdrop",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,null);
        game=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene,OpenSceneMode.Single);var saved=game.GetRootGameObjects().Single(g=>g.name==Markers);
        int missing=saved.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        var report=new Report{utc=DateTime.UtcNow.ToString("o"),stage="SAVED_AND_REOPENED",sourceBackup=backup,lineWidth=LineWidth,dashLength=DashLength,gapLength=GapLength,elevation=Elevation,color=Yellow,sites=sites.ToArray(),colliders=saved.GetComponentsInChildren<Collider>(true).Length,missingScripts=missing};
        File.WriteAllText(Path.Combine(Reports,"building-footprint-markers.json"),JsonUtility.ToJson(report,true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);
        var destination=Path.GetFullPath(Path.Combine(Reports,"../backup/footprint-source-staging"));Directory.CreateDirectory(destination);
        File.Move(SourceScene,Path.Combine(destination,"FootprintSourceBeforeTrim.unity"));if(File.Exists(SourceScene+".meta"))File.Move(SourceScene+".meta",Path.Combine(destination,"FootprintSourceBeforeTrim.unity.meta"));AssetDatabase.Refresh();
        if(report.colliders!=0||missing!=0)throw new InvalidOperationException("Footprint saved-scene verification failed");
    }
}
