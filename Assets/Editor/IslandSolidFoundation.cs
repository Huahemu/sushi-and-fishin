using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Text.RegularExpressions;
using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.SceneManagement;using UnityEngine.Rendering;
using PaperPixelGames.Core.DressingSceneSystem;using Object=UnityEngine.Object;

public static class IslandSolidFoundation
{
    const string Output="Assets/SushiAndFish/CityExpansion/SolidFoundation20261021";
    const string RootName="NativeContourSolidFoundations_20261021";
    public static string Reports=>IslandFrontEndInventory.Reports;
    [Serializable]public class Result{public string utc,stage,error;public bool prototype,saved,passed;public List<Part>parts=new List<Part>();}
    [Serializable]public class Part
    {public string source,sourceId,category,mesh;public int prisms,vertices,triangles,checks,failures;public float maxGroundGap,minEmbed,topOffset;public Vector3 point,min,max,lowStep,highStep;}
    static Terrain[] lands;
    static string P(Transform t)=>IslandFrontEndInventory.PathOf(t);
    public static string Category(MeshFilter f)
    {if(f.name.StartsWith("SM_stairs_wide"))return "WideSteps";if(Regex.IsMatch(f.name,"stairs",RegexOptions.IgnoreCase))return "Steps";if(f.name=="SideWalkTerrainEdge")return "CurvedPlatform";if(f.name=="SideWalkWalls"||f.name.StartsWith("ParkCornerWalls"))return "CurvedWall";if(Regex.IsMatch(f.name,"pillar",RegexOptions.IgnoreCase))return "StonePier";if(Regex.IsMatch(f.name,"wall",RegexOptions.IgnoreCase))return "RetainingWall";return "StraightPlatform";}
    static Material StoneMaterial()
    {
        var path=Output+"/FoundationStone.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/M_walls_japan 1.mat"));material.name="FoundationStone";material.SetTexture("_BumpMap",null);material.DisableKeyword("_NORMALMAP");material.SetFloat("_Smoothness",.12f);AssetDatabase.CreateAsset(material,path);}return material;
    }
    public static bool Eligible(MeshFilter f)
    {
        if(!f||!f.sharedMesh||!f.GetComponent<MeshRenderer>()||!f.GetComponent<MeshRenderer>().enabled)return false;
        var p=P(f.transform);if(p.StartsWith(RootName)||p.StartsWith("ParkCasino")||p.StartsWith("---Restaurant/")||p.Contains("CityBuildings")||p.Contains("GabeMarket")||p.Contains("MarinaMarket")||p.Contains("DockSide/")||p.Contains("WaterTile"))return false;
        if(!Regex.IsMatch(f.name,@"wall|pillar|stairs|sidewalk|street|curb",RegexOptions.IgnoreCase)||Regex.IsMatch(f.name,@"flower|decal",RegexOptions.IgnoreCase))return false;
        var b=f.GetComponent<MeshRenderer>().bounds;
        return b.min.x<150&&b.max.x>-180&&b.min.z<95&&b.max.z>-145&&b.max.y<6&&b.min.y>-12;
    }
    public static float Ground(Vector3 p)
    {float value=-1000;foreach(var t in lands){var q=p-t.transform.position;var d=t.terrainData;var size=d.size;if(q.x<0||q.z<0||q.x>size.x||q.z>size.z)continue;int h=d.holesResolution;if(h>0&&d.IsHole(Mathf.Clamp((int)(q.x/size.x*h),0,h-1),Mathf.Clamp((int)(q.z/size.z*h),0,h-1)))continue;value=Mathf.Max(value,t.SampleHeight(p)+t.transform.position.y);}return value;}
    static float FloorMinimum(Vector3 a,Vector3 b,Vector3 c)
    {
        float value=new[]{a,b,c,(a+b)*.5f,(b+c)*.5f,(c+a)*.5f,(a+b+c)/3}.Min(Ground);
        var p=(a+b+c)/3;var t=lands.Where(t=>{var q=p-t.transform.position;return q.x>=0&&q.z>=0&&q.x<=t.terrainData.size.x&&q.z<=t.terrainData.size.z;}).OrderByDescending(t=>t.SampleHeight(p)+t.transform.position.y).First();
        var d=t.terrainData;var o=t.transform.position;float sx=d.size.x/(d.heightmapResolution-1),sz=d.size.z/(d.heightmapResolution-1);
        int x0=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x,b.x,c.x)-o.x)/sx),0,d.heightmapResolution-1),x1=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x,b.x,c.x)-o.x)/sx),0,d.heightmapResolution-1);
        int z0=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.z,b.z,c.z)-o.z)/sz),0,d.heightmapResolution-1),z1=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.z,b.z,c.z)-o.z)/sz),0,d.heightmapResolution-1);
        for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)value=Mathf.Min(value,Ground(new Vector3(o.x+x*sx,0,o.z+z*sz)));
        return value-.3f;
    }
    static void Add(List<Vector3>v,List<Vector2>uv,List<int>tri,Vector3 a,Vector3 b,Vector3 c)
    {int k=v.Count;v.Add(a);v.Add(b);v.Add(c);var n=Vector3.Cross(b-a,c-a).normalized;bool horizontal=Mathf.Abs(n.y)>.5f;foreach(var p in new[]{a,b,c})uv.Add(horizontal?new Vector2(p.x,p.z)*.25f:Mathf.Abs(n.x)>Mathf.Abs(n.z)?new Vector2(p.z,p.y)*.5f:new Vector2(p.x,p.y)*.5f);tri.Add(k);tri.Add(k+1);tri.Add(k+2);}
    static void Prism(List<Vector3>v,List<Vector2>uv,List<int>tri,Vector3 a,Vector3 b,Vector3 c,Part row)
    {
        a.y-=.025f;b.y-=.025f;c.y-=.025f;
        float ga=Ground(a),gb=Ground(b),gc=Ground(c);float gap=Mathf.Max(a.y-ga,b.y-gb,c.y-gc);
        foreach(var p in new[]{(a+b+c)/3,(a+b)*.5f,(b+c)*.5f,(c+a)*.5f})gap=Mathf.Max(gap,p.y-Ground(p));
        if(gap<.12f)return;
        var aa=a;var bb=b;var cc=c;float bottom=FloorMinimum(a,b,c);aa.y=Mathf.Min(bottom,a.y-.08f);bb.y=Mathf.Min(bottom,b.y-.08f);cc.y=Mathf.Min(bottom,c.y-.08f);
        if(gap>row.maxGroundGap)row.point=(a+b+c)/3+Vector3.up*.025f;
        row.maxGroundGap=Mathf.Max(row.maxGroundGap,gap);row.prisms++;
        Add(v,uv,tri,a,b,c);Add(v,uv,tri,aa,cc,bb);
        Add(v,uv,tri,a,aa,bb);Add(v,uv,tri,a,bb,b);Add(v,uv,tri,b,bb,cc);Add(v,uv,tri,b,cc,c);Add(v,uv,tri,c,cc,aa);Add(v,uv,tri,c,aa,a);
        foreach(var p in new[]{(a+b+c)/3,(a+b)*.5f,(b+c)*.5f,(c+a)*.5f})
        {
            var bary=Coordinates(p,a,b,c);float sampledBottom=aa.y*bary.x+bb.y*bary.y+cc.y*bary.z;
            float embed=Ground(p)-sampledBottom;row.minEmbed=Mathf.Min(row.minEmbed,embed);row.checks++;if(embed<.015f)row.failures++;
        }
    }
    static Vector3 Coordinates(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
    {float d=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(d)<1e-12f)return new Vector3(1,0,0);float x=((b.z-c.z)*(p.x-c.x)+(c.x-b.x)*(p.z-c.z))/d;float y=((c.z-a.z)*(p.x-c.x)+(a.x-c.x)*(p.z-c.z))/d;return new Vector3(x,y,1-x-y);}
    static void Subdivide(List<Vector3>v,List<Vector2>uv,List<int>tri,Vector3 a,Vector3 b,Vector3 c,Part row,int depth)
    {
        float ab=new Vector2(a.x-b.x,a.z-b.z).sqrMagnitude,bc=new Vector2(b.x-c.x,b.z-c.z).sqrMagnitude,ca=new Vector2(c.x-a.x,c.z-a.z).sqrMagnitude;
        if(depth<16&&Mathf.Max(ab,bc,ca)>1f)
        {
            if(ab>=bc&&ab>=ca){var m=(a+b)*.5f;Subdivide(v,uv,tri,a,m,c,row,depth+1);Subdivide(v,uv,tri,m,b,c,row,depth+1);}
            else if(bc>=ca){var m=(b+c)*.5f;Subdivide(v,uv,tri,a,b,m,row,depth+1);Subdivide(v,uv,tri,a,m,c,row,depth+1);}
            else{var m=(c+a)*.5f;Subdivide(v,uv,tri,a,b,m,row,depth+1);Subdivide(v,uv,tri,m,b,c,row,depth+1);}return;
        }
        Prism(v,uv,tri,a,b,c,row);
    }
    static Part Build(MeshFilter source,Transform root,int index)
    {
        var row=new Part{source=P(source.transform),sourceId=GlobalObjectId.GetGlobalObjectIdSlow(source).ToString(),category=Category(source),minEmbed=float.PositiveInfinity,topOffset=.025f};
        var mesh=source.sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;var v=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();bool mirrored=source.transform.localToWorldMatrix.determinant<0;
        float low=float.PositiveInfinity,high=float.NegativeInfinity;
        for(int i=0;i<triangles.Length;i+=3)
        {
            var a=source.transform.TransformPoint(vertices[triangles[i]]);var b=source.transform.TransformPoint(vertices[triangles[i+1]]);var c=source.transform.TransformPoint(vertices[triangles[i+2]]);
            if(mirrored){var swap=b;b=c;c=swap;}var n=Vector3.Cross(b-a,c-a);if(n.sqrMagnitude<1e-10f||n.normalized.y<.45f)continue;
            var center=(a+b+c)/3;if(center.y<low){low=center.y;row.lowStep=center;}if(center.y>high){high=center.y;row.highStep=center;}
            Subdivide(v,uv,indices,a,b,c,row,0);
        }
        if(v.Count==0)return row;
        var generated=new Mesh{name="NativeContour_"+index+"_"+row.category,indexFormat=IndexFormat.UInt32};generated.SetVertices(v);generated.SetUVs(0,uv);generated.SetTriangles(indices,0);generated.RecalculateNormals();generated.RecalculateTangents();generated.RecalculateBounds();
        row.mesh=AssetDatabase.GenerateUniqueAssetPath(Output+"/"+generated.name+".asset");AssetDatabase.CreateAsset(generated,row.mesh);row.vertices=v.Count;row.triangles=indices.Count/3;row.min=generated.bounds.min;row.max=generated.bounds.max;
        var go=new GameObject("Foundation_"+index+"_"+source.name);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=generated;
        var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=StoneMaterial();r.shadowCastingMode=ShadowCastingMode.On;
        go.AddComponent<MeshCollider>().sharedMesh=generated;go.isStatic=true;
        return row;
    }
    public static void Prototype()=>Apply(true);
    public static void Complete()=>Apply(false);
    static void Apply(bool prototype)
    {
        var result=new Result{utc=DateTime.UtcNow.ToString("o"),prototype=prototype,stage="BUILDING"};
        try
        {
            if(!prototype)
            {var proof=JsonUtility.FromJson<IslandFoundationPlayAudit.Receipt>(File.ReadAllText(Path.Combine(Reports,"island-foundation-prototype-runtime.json")));if(!proof.passed)throw new Exception("Prototype runtime proof has not passed");}
            Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            var gs=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene);var ds=EditorSceneManager.OpenScene(IslandFrontEndBuilder.DressingRoot+"/100_Dressing_City_01.unity",OpenSceneMode.Additive);
            var nodes=IslandFoundationDiagnosis.All(gs).Concat(IslandFoundationDiagnosis.All(ds)).ToArray();lands=nodes.Select(t=>t.GetComponent<Terrain>()).Where(t=>t).ToArray();
            var prior=ds.GetRootGameObjects().FirstOrDefault(g=>g.name==RootName);if(prior)Object.DestroyImmediate(prior);
            var root=new GameObject(RootName);SceneManager.MoveGameObjectToScene(root,ds);
            var targets=nodes.Where(t=>t).Select(t=>t.GetComponent<MeshFilter>()).Where(Eligible).ToArray();
            if(prototype)
            {
                var selected=new List<MeshFilter>();
                foreach(var category in new[]{"StraightPlatform","CurvedPlatform","Steps","WideSteps","RetainingWall","CurvedWall","StonePier"})
                {
                    var choices=targets.Where(t=>Category(t)==category);
                    if(category=="StraightPlatform")choices=choices.Where(t=>t.name.StartsWith("SM_sidewalk_long")&&t.GetComponent<Renderer>().bounds.center.z>25);
                    var destination=category=="Steps"?new Vector3(89,0,32):category=="RetainingWall"?new Vector3(80,0,30):new Vector3(76,0,29);
                    var chosen=category=="WideSteps"?choices.OrderByDescending(t=>t.GetComponent<Renderer>().bounds.max.y-Ground(t.GetComponent<Renderer>().bounds.center)).First():choices.OrderBy(t=>Vector3.Distance(t.GetComponent<Renderer>().bounds.center,destination)).First();selected.Add(chosen);
                }
                targets=selected.ToArray();
            }
            for(int i=0;i<targets.Length;i++){var part=Build(targets[i],root.transform,i);if(part.prisms>0)result.parts.Add(part);}
            result.passed=result.parts.Count>0&&result.parts.All(p=>p.failures==0)&&(!prototype||result.parts.Select(p=>p.category).Distinct().Count()==7);
            if(!result.passed)throw new Exception("Foundation bottom/interior samples failed; do not save or propagate");
            EditorSceneManager.MarkSceneDirty(ds);EditorSceneManager.SaveScene(ds);AssetDatabase.SaveAssets();result.saved=true;result.stage="SAVED";
        }
        catch(Exception e){result.error=e.ToString();result.stage="FAILED";Debug.LogException(e);}
        finally{File.WriteAllText(Path.Combine(Reports,prototype?"island-foundation-prototype.json":"island-foundation-complete.json"),JsonUtility.ToJson(result,true));EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");}
    }
}
