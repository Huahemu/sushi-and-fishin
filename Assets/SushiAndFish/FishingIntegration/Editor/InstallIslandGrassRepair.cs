using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SushiAndFish.FishingIntegration;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Offline, deterministic ground cover. The result is regular chunk meshes and
// renderers, without grass colliders, per-blade objects or runtime spawning.
public static class InstallIslandGrassRepair {
    const string Root="Assets/SushiAndFish/FishingIntegration/Archipelago/";
    const string Veg=Root+"Vegetation/", Cover=Veg+"GroundCover/";
    const string Out="D:/UnityProjects/_CodexWork/GrassDensityHalf-20261027/";
    const string GrassShader="SushiAndFish/Original Island Grass", Group="How to Fish 全岛草地";
    const float Spacing=.85f,ChunkSize=10;
    const int DensityDivisor=2;
    [Serializable] class Mat {public string key,name,texture;public Color color,topColor;public bool grass;}
    [Serializable] class Catalog {public Mat[] materials;}
    sealed class Chunk {public List<Vector3> v=new List<Vector3>(),n=new List<Vector3>();public List<Vector2> uv=new List<Vector2>();public List<int> t=new List<int>();public int clumps;}
    static readonly List<string> checks=new List<string>();
    static void Check(bool ok,string title){checks.Add((ok?"PASS ":"FAIL ")+title);Directory.CreateDirectory(Out);File.WriteAllLines(Out+"Checks.txt",checks);if(!ok)throw new Exception(title);}
    static T Save<T>(T value,string path)where T:Object{var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old){EditorUtility.CopySerialized(value,old);Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(value,path);return value;}
    static Catalog Source()=>JsonUtility.FromJson<Catalog>(File.ReadAllText(Veg+"Source/VegetationCatalog.json"));
    static Material Grass(int island)=>AssetDatabase.LoadAssetAtPath<Material>(Veg+"Materials/sharedassets0_"+(island==1?"76":"78")+".mat");
    [MenuItem("Tools/Fishing/Fix grass material and cover both islands")]
    public static void BuildAndVerify(){ApplyAssets();Verify();}
    public static void ApplyAssets(){
        Check(!EditorApplication.isPlaying,"No Play, game simulation, input replay or physics stepping");
        Directory.CreateDirectory(Cover);AssetDatabase.Refresh();
        var importer=(TextureImporter)AssetImporter.GetAtPath(Veg+"Source/sharedassets0_156.png");
        importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
        importer.mipmapEnabled=false;importer.isReadable=true;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        foreach(var d in Source().materials.Where(m=>m.grass)){
            var m=new Material(Shader.Find(GrassShader)){name="How to Fish "+d.name,enableInstancing=true};m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Veg+"Source/"+d.texture+".png"));
            m.SetColor("_BaseColor",d.color);m.SetColor("_TopColor",d.topColor);m.SetFloat("_AlphaClip",1);m.SetFloat("_Cutoff",.5f);m.SetFloat("_Cull",0);m.EnableKeyword("_ALPHATEST_ON");m.SetOverrideTag("RenderType","TransparentCutout");m.renderQueue=2450;
            Save(m,Veg+"Materials/"+d.key+".mat");
        }
        foreach(var name in new[]{"Grass","BeachGrass"}){
            string path=Veg+"Prefabs/"+name+".prefab";var prefab=PrefabUtility.LoadPrefabContents(path);
            try{foreach(var r in prefab.GetComponentsInChildren<MeshRenderer>())r.sharedMaterial=Grass(1);PrefabUtility.SaveAsPrefabAsset(prefab,path);}finally{PrefabUtility.UnloadPrefabContents(prefab);}
        }
        AssetDatabase.SaveAssets();foreach(int index in new[]{1,2})BuildCover(index);AssetDatabase.SaveAssets();
    }
    static bool OverWood(Vector3 local,int index,BoxCollider bridge){var p=bridge.transform.InverseTransformPoint(local+ArchipelagoSettings.Load().regions[index].center);return Mathf.Abs(p.x)<bridge.size.x/2+.3f&&Mathf.Abs(p.z)<bridge.size.z/2+.3f;}
    static void BuildCover(int index){
        var settings=ArchipelagoSettings.Load();string path=Root+"Prefabs/HowToFishIsland"+index+".prefab";
        var island=PrefabUtility.LoadPrefabContents(path);var access=PrefabUtility.LoadPrefabContents(Root+"Prefabs/HowToFishAccessDocks.prefab");
        try{
            var old=island.transform.Find(Group);if(old)Object.DestroyImmediate(old.gameObject);
            var group=new GameObject(Group).transform;group.SetParent(island.transform,false);
            var scatteredRoot=island.transform.Find("How to Fish 原生植被");int scatteredTotal=0,scatteredActive=0;
            if(scatteredRoot)foreach(Transform item in scatteredRoot){
                if(!item.name.StartsWith("Grass ",StringComparison.Ordinal)&&!item.name.StartsWith("BeachGrass ",StringComparison.Ordinal))continue;
                bool keep=scatteredTotal++%DensityDivisor==0;item.gameObject.SetActive(keep);if(keep)scatteredActive++;
            }
            var terrain=island.GetComponentsInChildren<MeshCollider>().Single(c=>c.name=="Island1");
            var bridge=access.GetComponentsInChildren<BoxCollider>().Single(c=>c.name==settings.regions[index].title+" · 靠岸栈桥");
            foreach(var r in island.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Grass"||r.name=="BeachGrass"))r.sharedMaterial=Grass(index);
            var sourceNames=new[]{"BeachGrass","Grass"};var sources=sourceNames.Select(name=>AssetDatabase.LoadAssetAtPath<GameObject>(Veg+"Prefabs/"+name+".prefab").GetComponentInChildren<MeshFilter>()).ToArray();
            var sourceV=sources.Select(m=>m.sharedMesh.vertices).ToArray();var sourceN=sources.Select(m=>m.sharedMesh.normals).ToArray();var sourceUV=sources.Select(m=>m.sharedMesh.uv).ToArray();var sourceT=sources.Select(m=>m.sharedMesh.triangles).ToArray();
            var chunks=new Dictionary<Vector2Int,Chunk>();var rng=new System.Random(271020+index);var densityRng=new System.Random(121020+index);var bounds=terrain.bounds;int dry=0,count=0,fullDensityClumps=0,keepInGroup=0;var sites=new List<Vector3>();
            var treeBases=island.GetComponentsInChildren<Collider>().Where(c=>c!=terrain).Select(c=>c.bounds).ToArray();
            for(float x=bounds.min.x;x<=bounds.max.x;x+=Spacing)for(float z=bounds.min.z;z<=bounds.max.z;z+=Spacing){
                var p=new Vector3(x+(float)(rng.NextDouble()-.5)*.3f,40,z+(float)(rng.NextDouble()-.5)*.3f);
                if(!terrain.Raycast(new Ray(p,Vector3.down),out var hit,80)||hit.point.y<.2f||hit.normal.y<.78f)continue;dry++;
                if(OverWood(hit.point,index,bridge)||treeBases.Any(b=>new Vector2(hit.point.x-b.center.x,hit.point.z-b.center.z).sqrMagnitude<.25f))continue;
                int species=rng.Next(5)==0?1:0;float scale=.85f+(float)rng.NextDouble()*.65f;float yaw=(float)rng.NextDouble()*360;
                // Keep one randomly chosen member of each pair. A separate RNG
                // preserves every retained clump's previous position and shape;
                // rebuilding applies the same density instead of halving again.
                int ordinal=fullDensityClumps++;if(ordinal%DensityDivisor==0)keepInGroup=densityRng.Next(DensityDivisor);if(ordinal%DensityDivisor!=keepInGroup)continue;
                var matrix=Matrix4x4.TRS(hit.point+Vector3.up*.018f,Quaternion.Euler(0,yaw,0),Vector3.one*scale)*sources[species].transform.localToWorldMatrix;
                var key=new Vector2Int(Mathf.FloorToInt(hit.point.x/ChunkSize),Mathf.FloorToInt(hit.point.z/ChunkSize));if(!chunks.TryGetValue(key,out var chunk)){chunk=new Chunk();chunks.Add(key,chunk);}int first=chunk.v.Count;
                for(int i=0;i<sourceV[species].Length;i++){chunk.v.Add(matrix.MultiplyPoint3x4(sourceV[species][i]));chunk.n.Add(matrix.MultiplyVector(sourceN[species][i]).normalized);chunk.uv.Add(sourceUV[species][i]);}
                chunk.t.AddRange(sourceT[species].Select(i=>i+first));chunk.clumps++;count++;sites.Add(hit.point);
            }
            foreach(var pair in chunks){
                var d=pair.Value;var mesh=new Mesh{name="Island "+index+" grass "+pair.Key,indexFormat=d.v.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(d.v);mesh.SetNormals(d.n);mesh.SetUVs(0,d.uv);mesh.SetTriangles(d.t,0);mesh.RecalculateBounds();
                var saved=Save(mesh,Cover+"Island"+index+"_"+pair.Key.x+"_"+pair.Key.y+".asset");var go=new GameObject("原生草地分块 "+pair.Key,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(group,false);go.GetComponent<MeshFilter>().sharedMesh=saved;go.GetComponent<MeshRenderer>().sharedMaterial=Grass(index);
            }
            BuildGroundTint(terrain,group,index);
            PrefabUtility.SaveAsPrefabAsset(island,path);
            File.WriteAllText(Out+"Coverage"+index+".json",JsonUtility.ToJson(new Coverage{island=index,clumps=count,chunks=chunks.Count,eligible=dry,fullDensityClumps=fullDensityClumps,scatteredTotal=scatteredTotal,scatteredActive=scatteredActive,sites=sites.ToArray()},false));
        }finally{PrefabUtility.UnloadPrefabContents(access);PrefabUtility.UnloadPrefabContents(island);}
    }
    [Serializable]sealed class Coverage{public int island,clumps,chunks,eligible,fullDensityClumps,scatteredTotal,scatteredActive;public Vector3[] sites;}
    static void BuildGroundTint(MeshCollider terrain,Transform group,int index){
        var original=terrain.sharedMesh;var v=original.vertices;var t=original.triangles;var matrix=terrain.transform.localToWorldMatrix;var d=new Chunk();
        for(int i=0;i<t.Length;i+=3){var a=matrix.MultiplyPoint3x4(v[t[i]]);var b=matrix.MultiplyPoint3x4(v[t[i+1]]);var c=matrix.MultiplyPoint3x4(v[t[i+2]]);var n=Vector3.Cross(b-a,c-a).normalized;
            if(n.y<.78f||Mathf.Min(a.y,b.y,c.y)<.12f)continue;int first=d.v.Count;d.v.AddRange(new[]{a+Vector3.up*.012f,b+Vector3.up*.012f,c+Vector3.up*.012f});d.t.AddRange(new[]{first,first+1,first+2});
        }
        var mesh=new Mesh{name="Island "+index+" grass ground tint"};mesh.SetVertices(d.v);mesh.SetTriangles(d.t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();var saved=Save(mesh,Cover+"Island"+index+"_Ground.asset");
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="How to Fish island "+index+" original grass ground"};mat.SetColor("_BaseColor",Source().materials.Single(m=>m.key==(index==1?"sharedassets0_76":"sharedassets0_78")).color);mat.SetFloat("_Smoothness",0);mat=Save(mat,Cover+"Island"+index+"_Ground.mat");
        var go=new GameObject("原版草色地被底层",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(group,false);go.GetComponent<MeshFilter>().sharedMesh=saved;go.GetComponent<MeshRenderer>().sharedMaterial=mat;
    }
    public static void Verify(){
        Check(!EditorApplication.isPlaying,"Static verification only; no gameplay or physical stepping");
        var shader=Shader.Find(GrassShader);Check(shader&&shader.isSupported&&!ShaderUtil.GetShaderMessages(shader).Any(m=>m.severity==ShaderCompilerMessageSeverity.Error),"Grass shader compiles without shader errors");
        var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Veg+"Source/sharedassets0_156.png");var pixels=tex.GetPixels32();Check(tex.width==16&&tex.height==16&&pixels.All(p=>p.r==255&&p.g==255&&p.b==255)&&pixels.Any(p=>p.a==0)&&pixels.Any(p=>p.a==255),"Original grass texture is a white RGB leaf mask with transparent cutouts, not a colour palette");
        foreach(int index in new[]{1,2}){
            var material=Grass(index);var source=Source().materials.Single(m=>m.key==(index==1?"sharedassets0_76":"sharedassets0_78"));
            Check(material.shader==shader&&material.GetTexture("_BaseMap")==tex&&material.GetFloat("_AlphaClip")==1&&material.IsKeywordEnabled("_ALPHATEST_ON")&&material.GetFloat("_Cull")==0,"Island "+index+": original leaf mask, alpha clipping, depth and double-sided leaf rendering assigned");
            Check(material.GetColor("_BaseColor")==source.color&&material.GetColor("_TopColor")==source.topColor,"Island "+index+": exact original root and tip grass colours restored");
            var prefab=ArchipelagoSettings.Load().regions[index].prefab;var group=prefab.transform.Find(Group);var cover=JsonUtility.FromJson<Coverage>(File.ReadAllText(Out+"Coverage"+index+".json"));
            Check(group&&cover.clumps>2000&&cover.chunks>30&&cover.fullDensityClumps>cover.eligible*.9f&&Mathf.Abs(cover.clumps*DensityDivisor-cover.fullDensityClumps)<DensityDivisor&&cover.scatteredActive==(cover.scatteredTotal+DensityDivisor-1)/DensityDivisor,"Island "+index+": whole-island and original scattered grass are both reduced to half density");
            Check(cover.sites.Any(p=>Mathf.Abs(p.x)<3&&Mathf.Abs(p.z)<3)&&cover.sites.Any(p=>p.x< -25)&&cover.sites.Any(p=>p.x>25)&&cover.sites.Any(p=>p.z< -20)&&cover.sites.Any(p=>p.z>20),"Island "+index+": centre and all four outer sides are grassed");
            Check(!group.GetComponentsInChildren<Collider>().Any()&&!group.GetComponentsInChildren<Rigidbody>().Any()&&!group.GetComponentsInChildren<MonoBehaviour>().Any(),"Island "+index+": grass is passable and adds no rigidbodies or per-blade scripts");
            Check(group.childCount==cover.chunks+1&&group.GetComponentsInChildren<MeshFilter>().All(m=>m.sharedMesh&&m.sharedMesh.vertexCount<65536),"Island "+index+": grass uses bounded chunk meshes and a ground layer instead of thousands of scene objects");
            var chunks=group.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("原生草地分块"));Check(chunks.All(r=>r.sharedMaterial==material),"Island "+index+": every grass chunk uses its correct regional grass material");
        }
        RenderCloseup();ExpandRockIsland.VerifyIsland(1,false);ExpandRockIsland.VerifyIsland(2,true);
        File.WriteAllLines(Out+"Checks.txt",checks);Debug.Log("ISLAND_GRASS_REPAIR_VERIFIED checks="+checks.Count+" play=false simulation=false savesWritten=false");
    }
    static void RenderCloseup(){
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Static grass material camera",typeof(Camera)).GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.24f,.24f,.24f);camera.nearClipPlane=.01f;camera.farClipPlane=100;
        var light=new GameObject("Static grass light",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(45,-25,0);RenderSettings.ambientLight=Color.gray;RenderSettings.fog=false;
        var roots=new List<GameObject>();for(int i=0;i<2;i++){var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Veg+"Prefabs/"+(i==0?"Grass":"BeachGrass")+".prefab"));go.transform.position=new Vector3((i-.5f)*.65f,0,0);go.transform.localScale=Vector3.one*1.5f;roots.Add(go);}
        camera.transform.position=new Vector3(0,.65f,-1.8f);camera.transform.LookAt(new Vector3(0,.22f,0));var target=new RenderTexture(1200,800,24);camera.targetTexture=target;camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1200,800,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1200,800),0,0);image.Apply();File.WriteAllBytes(Out+"grass-closeup.png",image.EncodeToPNG());
        var pixels=image.GetPixels32();int coloured=pixels.Count(p=>Mathf.Max(p.r,p.g,p.b)-Mathf.Min(p.r,p.g,p.b)>20);int green=pixels.Count(p=>p.g>p.r&&p.g>p.b*1.25f);int bad=pixels.Count(p=>p.b>p.g*1.25f&&p.b>80||p.r>p.g*1.5f&&p.r>100);
        File.WriteAllText(Out+"RenderDiagnostic.txt","coloured="+coloured+" green="+green+" bad="+bad+"\n"+string.Join("\n",roots.SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)).Select(r=>r.name+" enabled="+r.enabled+" active="+r.gameObject.activeInHierarchy+" bounds="+r.bounds+" shader="+r.sharedMaterial.shader.name+" color="+r.sharedMaterial.GetColor("_BaseColor")+" texture="+r.sharedMaterial.GetTexture("_BaseMap").name+" scale="+r.sharedMaterial.GetTextureScale("_BaseMap"))));
        if(coloured<1000){
            var control=new Material(Shader.Find("Universal Render Pipeline/Lit"));control.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Veg+"Source/sharedassets0_156.png"));control.SetColor("_BaseColor",new Color(.3f,.6f,.15f));control.SetFloat("_AlphaClip",1);control.SetFloat("_Cull",0);control.EnableKeyword("_ALPHATEST_ON");
            foreach(var r in roots.SelectMany(g=>g.GetComponentsInChildren<Renderer>()))r.sharedMaterial=control;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1200,800),0,0);image.Apply();File.WriteAllBytes(Out+"grass-control.png",image.EncodeToPNG());Object.DestroyImmediate(control);
        }
        Check(coloured>1000&&green>coloured*.92f&&bad<10,"Close-up render contains green leaf silhouettes with no rainbow / blue / magenta strips");RenderTexture.active=old;camera.targetTexture=null;foreach(var go in roots)Object.DestroyImmediate(go);Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(light.gameObject);Object.DestroyImmediate(image);Object.DestroyImmediate(target);
    }
}
