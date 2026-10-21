using System;using System.IO;using System.Linq;using System.Collections.Generic;
using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.SceneManagement;
using PaperPixelGames.Core.DressingSceneSystem;using Object=UnityEngine.Object;

public static class IslandFoundationFinalization
{
    const string Output="Assets/SushiAndFish/CityExpansion/SolidFoundation20261021";
    const string CornerRoot="SourceFoundationCompletions_20261021";
    const string FoundationRoot="NativeContourSolidFoundations_20261021";
    static string Reports=>IslandFrontEndInventory.Reports;
    static Transform[] All(Scene s)=>IslandFoundationDiagnosis.All(s);
    static string P(Transform t)=>IslandFoundationDiagnosis.P(t);
    [Serializable]public class CornerProof{public string utc,texture,sourceTexture;public int restored;public float maxWorldVertexError;public bool materialSupported;public List<string>objects=new List<string>();}
    public static void RefineAndRestore()
    {
        var receipt=new CornerProof{utc=DateTime.UtcNow.ToString("o"),sourceTexture="Assets/Texture2D/M_walls_japan_poarch_BaseMap_01.png"};
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(receipt.sourceTexture);
        var rt=RenderTexture.GetTemporary(256,128,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var prior=RenderTexture.active;
        Graphics.Blit(texture,rt,new Vector2(.25f,.125f),new Vector2(.05f,.80f));RenderTexture.active=rt;
        var crop=new Texture2D(256,128,TextureFormat.RGB24,false,false);crop.ReadPixels(new Rect(0,0,256,128),0,0);crop.Apply();
        receipt.texture=Output+"/NativeGrayStoneTile.png";File.WriteAllBytes(receipt.texture,crop.EncodeToPNG());RenderTexture.active=prior;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(crop);
        AssetDatabase.ImportAsset(receipt.texture,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(receipt.texture);importer.wrapMode=TextureWrapMode.Mirror;importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.SaveAndReimport();
        var material=AssetDatabase.LoadAssetAtPath<Material>(Output+"/FoundationStone.mat");
        foreach(var property in new[]{"_BumpMap","_MetallicGlossMap","_SpecGlossMap","_OcclusionMap","_EmissionMap"})if(material.HasProperty(property))material.SetTexture(property,null);
        foreach(var keyword in new[]{"_NORMALMAP","_METALLICGLOSSMAP","_METALLICSPECGLOSSMAP","_SPECGLOSSMAP","_EMISSION"})material.DisableKeyword(keyword);
        foreach(var property in new[]{"_BaseMap","_MainTex"})if(material.HasProperty(property)){material.SetTexture(property,AssetDatabase.LoadAssetAtPath<Texture2D>(receipt.texture));material.SetTextureScale(property,Vector2.one);material.SetTextureOffset(property,Vector2.zero);}
        if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",Color.white);if(material.HasProperty("_Color"))material.SetColor("_Color",Color.white);material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.12f);EditorUtility.SetDirty(material);receipt.materialSupported=material.shader&&material.shader.isSupported;
        var gs=EditorSceneManager.OpenScene(ScreenshotFollowupRepair.GS);var ds=EditorSceneManager.OpenScene(ScreenshotFollowupRepair.DS,OpenSceneMode.Additive);
        var root=ds.GetRootGameObjects().FirstOrDefault(g=>g.name==CornerRoot);if(!root){root=new GameObject(CornerRoot);SceneManager.MoveGameObjectToScene(root,ds);}
        var source=EditorSceneManager.OpenScene(ScreenshotFollowupRepair.Source,OpenSceneMode.Additive);
        foreach(var t in All(source).Where(t=>P(t)=="Root/Comps/CompOtopark/ParkCornerWalls"||P(t)=="Root/Comps/CompOtopark/ParkCornerWalls (1)"))
        {
            var target=root.transform.Find(t.name);if(!target){var g=new GameObject(t.name);target=g.transform;target.SetParent(root.transform,false);target.SetPositionAndRotation(t.position,t.rotation);target.localScale=t.lossyScale;g.AddComponent<MeshFilter>().sharedMesh=t.GetComponent<MeshFilter>().sharedMesh;g.AddComponent<MeshRenderer>().sharedMaterials=t.GetComponent<MeshRenderer>().sharedMaterials;g.AddComponent<MeshCollider>().sharedMesh=g.GetComponent<MeshFilter>().sharedMesh;g.isStatic=true;receipt.restored++;}
            var vertices=t.GetComponent<MeshFilter>().sharedMesh.vertices;foreach(var v in vertices)receipt.maxWorldVertexError=Mathf.Max(receipt.maxWorldVertexError,Vector3.Distance(t.TransformPoint(v),target.TransformPoint(v)));receipt.objects.Add(P(t));
        }
        if(receipt.objects.Count!=2||receipt.maxWorldVertexError>.001f||!receipt.materialSupported)throw new Exception("Original corner assembly/material verification failed");
        EditorSceneManager.CloseScene(source,true);EditorSceneManager.MarkSceneDirty(ds);EditorSceneManager.SaveScene(ds);AssetDatabase.SaveAssets();
        // This was our diagnostic copy with incompatible raw-export GUIDs; it is never a valid source scene.
        AssetDatabase.DeleteAsset("Assets/SushiAndFish/RevisionReference_20261021/OriginalGameStateNative.unity");
        File.WriteAllText(Path.Combine(Reports,"island-foundation-corner-material.json"),JsonUtility.ToJson(receipt,true));EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
    }
    [Serializable]public class Validation
    {public string utc,stage,error;public bool passed;public int generatedParts,eligibleObjects,alreadyGrounded,prisms,topChecks,bottomChecks,sideChecks,sourceNodes,sourceMeshes,cornerParts,primaryCapChecks;public float minEmbed=float.PositiveInfinity,maxTopError,maxPrimaryCapArea;public List<string>primaryCaps=new List<string>();public List<string>failures=new List<string>();public List<string>categories=new List<string>();}
    static string Signature(Transform t)
    {
        var f=t.GetComponent<MeshFilter>();var r=t.GetComponent<Renderer>();
        return t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R")+t.gameObject.activeSelf+"|"+string.Join(";",t.GetComponents<Component>().Select(c=>c?c.GetType().FullName:"MISSING"))+"|"+(f?AssetDatabase.GetAssetPath(f.sharedMesh):"")+"|"+(r?string.Join(";",r.sharedMaterials.Select(AssetDatabase.GetAssetPath))+"|"+r.enabled:"");
    }
    static bool Added(Transform t)=>P(t).StartsWith(FoundationRoot)||P(t).StartsWith(CornerRoot);
    static string StableKey(Transform t)=>t.parent?StableKey(t.parent)+"/"+t.name+"#"+t.GetSiblingIndex():t.name+"#"+t.GetSiblingIndex();
    static Dictionary<string,string> Baseline(string filename,string sceneName)
    {
        string path="Assets/SushiAndFish/RevisionReference_20261021/FoundationBaseline_"+sceneName+".unity";
        if(!File.Exists(path)){File.Copy(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../backup/island-foundation-20261021",filename)),path);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);}
        var scene=EditorSceneManager.OpenScene(path);return All(scene).Where(t=>!Added(t)).ToDictionary(t=>sceneName+"/"+StableKey(t),Signature);
    }
    static float Ground(Terrain[] lands,Vector3 p)=>lands.Where(t=>{var q=p-t.transform.position;return q.x>=0&&q.z>=0&&q.x<=t.terrainData.size.x&&q.z<=t.terrainData.size.z;}).Max(t=>t.SampleHeight(p)+t.transform.position.y);
    public static void ValidateSaved()
    {
        var result=new Validation{utc=DateTime.UtcNow.ToString("o"),stage="CHECKING"};
        try
        {
            var old=Baseline("GameState.unity.before","Game");foreach(var p in Baseline("100_Dressing_City_01.unity.before","City"))old.Add(p.Key,p.Value);
            var gs=EditorSceneManager.OpenScene(ScreenshotFollowupRepair.GS);var ds=EditorSceneManager.OpenScene(ScreenshotFollowupRepair.DS,OpenSceneMode.Additive);
            var nodes=All(gs).Concat(All(ds)).ToArray();var current=All(gs).Where(t=>!Added(t)).ToDictionary(t=>"Game/"+StableKey(t),Signature);foreach(var t in All(ds).Where(t=>!Added(t)))current.Add("City/"+StableKey(t),Signature(t));
            foreach(var p in old){result.sourceNodes++;if(!current.TryGetValue(p.Key,out var value)||value!=p.Value)result.failures.Add("Existing object changed: "+p.Key);}foreach(var p in current)if(!old.ContainsKey(p.Key))result.failures.Add("Unexpected existing-root addition: "+p.Key);
            var activation=nodes.ToDictionary(t=>t.gameObject,t=>t.gameObject.activeSelf);foreach(var d in nodes.Select(t=>t.GetComponent<DressingSceneRoot>()).Where(d=>d))d.ActivateAllImmediate();Physics.SyncTransforms();
            var lands=nodes.Select(t=>t.GetComponent<Terrain>()).Where(t=>t).ToArray();var generated=JsonUtility.FromJson<IslandSolidFoundation.Result>(File.ReadAllText(Path.Combine(Reports,"island-foundation-complete.json")));
            var generatedSources=new HashSet<string>(generated.parts.Select(p=>p.source));var eligible=nodes.Select(t=>t.GetComponent<MeshFilter>()).Where(IslandSolidFoundation.Eligible).ToArray();result.eligibleObjects=eligible.Length;
            foreach(var f in eligible.Where(f=>!generatedSources.Contains(P(f.transform))))
            {
                bool unsupported=false;var v=f.sharedMesh.vertices;var tr=f.sharedMesh.triangles;bool mirror=f.transform.localToWorldMatrix.determinant<0;
                for(int i=0;i<tr.Length;i+=3){var a=f.transform.TransformPoint(v[tr[i]]);var b=f.transform.TransformPoint(v[tr[i+1]]);var c=f.transform.TransformPoint(v[tr[i+2]]);var n=Vector3.Cross(b-a,c-a).normalized;if((mirror?-n.y:n.y)<.45f)continue;foreach(var p in new[]{a,b,c,(a+b+c)/3})if(p.y-Ground(lands,p)>.15f)unsupported=true;}
                if(unsupported)result.failures.Add("Eligible source has no solid foundation: "+P(f.transform));else result.alreadyGrounded++;
            }
            foreach(var part in generated.parts)
            {
                result.generatedParts++;var f=nodes.Select(t=>t.GetComponent<MeshFilter>()).Single(t=>t&&AssetDatabase.GetAssetPath(t.sharedMesh)==part.mesh);var collider=f.GetComponent<MeshCollider>();var v=f.sharedMesh.vertices;var tr=f.sharedMesh.triangles;
                if(v.Length!=part.prisms*24||tr.Length!=part.prisms*24)result.failures.Add("Closed prism topology mismatch "+part.source);
                if(!f.GetComponent<Renderer>().sharedMaterial.shader.isSupported)result.failures.Add("Unsupported material "+part.source);
                for(int i=0;i<v.Length;i+=24)
                {
                    result.prisms++;var a=v[i];var b=v[i+1];var c=v[i+2];var floor=v[i+3].y;var center=(a+b+c)/3;
                    foreach(var p in new[]{center,(a+b)*.5f,(b+c)*.5f,(c+a)*.5f}){result.bottomChecks++;float embed=Ground(lands,p)-floor;result.minEmbed=Mathf.Min(result.minEmbed,embed);if(embed<.015f)result.failures.Add("Bottom above ground "+part.source+" "+p);}
                    // Every stored prism must have all eight faces and hit its cap after save/reload.
                    if((center.y-floor)>.08f)
                    {
                        result.topChecks++;
                        if(!collider.Raycast(new Ray(center+Vector3.up*.01f,Vector3.down),out var hit,.05f))
                        {
                            // The inset foundation is below the retained primary pavement. Check that
                            // primary cap independently when PhysX cooks a narrow generated sliver away.
                            float area=Vector3.Cross(b-a,c-a).magnitude*.5f;var owner=nodes.First(t=>P(t)==part.source);var primary=owner.GetComponent<MeshCollider>();
                            bool supported=primary&&primary.enabled&&primary.Raycast(new Ray(center+Vector3.up*.1f,Vector3.down),out var top,.2f)&&Mathf.Abs(top.point.y-(center.y+.025f))<.002f;
                            result.primaryCapChecks++;result.maxPrimaryCapArea=Mathf.Max(result.maxPrimaryCapArea,area);result.primaryCaps.Add(part.source+" point="+center.ToString("R")+" triangleArea="+area.ToString("R")+" primaryCapHit="+supported);
                            if(!supported||area>.001f)result.failures.Add("Saved cap miss "+part.source+" area="+area);
                        }
                        else{float err=Mathf.Abs(hit.point.y-center.y);result.maxTopError=Mathf.Max(result.maxTopError,err);}
                    }
                }
                var p0=part.point;float g=Ground(lands,p0);var middle=new Vector3(p0.x,(p0.y+g)*.5f,p0.z);float w=collider.bounds.size.x+3,d=collider.bounds.size.z+3;
                result.sideChecks+=2;if(!collider.Raycast(new Ray(middle+Vector3.right*w,Vector3.left),out _,w*2)||!collider.Raycast(new Ray(middle+Vector3.forward*d,Vector3.back),out _,d*2))result.failures.Add("Saved side cross-section miss "+part.source);
            }
            result.cornerParts=nodes.Count(t=>P(t).StartsWith(CornerRoot+"/")&&t.GetComponent<MeshFilter>());result.sourceMeshes=nodes.Count(t=>!Added(t)&&t.GetComponent<MeshFilter>());result.categories=generated.parts.GroupBy(p=>p.category).Select(g=>g.Key+"="+g.Count()).ToList();
            if(result.cornerParts!=2)result.failures.Add("Native corner assembly not complete");
            foreach(var p in activation)if(p.Key)p.Key.SetActive(p.Value);
            result.passed=result.generatedParts>7&&result.failures.Count==0;result.stage=result.passed?"PASS":"FAILED";
        }
        catch(Exception e){result.error=e.ToString();result.stage="FAILED";Debug.LogException(e);}
        finally{File.WriteAllText(Path.Combine(Reports,"island-foundation-saved-validation.json"),JsonUtility.ToJson(result,true));EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");}
    }
    public static void CompleteAndValidate(){IslandSolidFoundation.Complete();ValidateSaved();}
    [Serializable]public class Coverage{public string utc,error;public bool passed;public int authoringEligible,runtimeEligible,foundationOwners,groundedWithoutFoundation,supportSamples;public float maxUnfilledGap;public List<string>failures=new List<string>();}
    static void SampleUnfilled(Vector3 a,Vector3 b,Vector3 c,Terrain[] lands,Coverage r,int depth)
    {
        float ab=Vector2.SqrMagnitude(new Vector2(a.x-b.x,a.z-b.z)),bc=Vector2.SqrMagnitude(new Vector2(b.x-c.x,b.z-c.z)),ca=Vector2.SqrMagnitude(new Vector2(c.x-a.x,c.z-a.z));
        if(depth<16&&Mathf.Max(ab,bc,ca)>1){if(ab>=bc&&ab>=ca){var m=(a+b)*.5f;SampleUnfilled(a,m,c,lands,r,depth+1);SampleUnfilled(m,b,c,lands,r,depth+1);}else if(bc>=ca){var m=(b+c)*.5f;SampleUnfilled(a,b,m,lands,r,depth+1);SampleUnfilled(a,m,c,lands,r,depth+1);}else{var m=(c+a)*.5f;SampleUnfilled(a,b,m,lands,r,depth+1);SampleUnfilled(m,b,c,lands,r,depth+1);}return;}
        foreach(var p in new[]{a,b,c,(a+b)*.5f,(b+c)*.5f,(c+a)*.5f,(a+b+c)/3}){r.supportSamples++;r.maxUnfilledGap=Mathf.Max(r.maxUnfilledGap,p.y-Ground(lands,p));}
    }
    public static void ValidateCoverageIdentity()
    {
        var r=new Coverage{utc=DateTime.UtcNow.ToString("o")};
        try
        {
            var gs=EditorSceneManager.OpenScene(ScreenshotFollowupRepair.GS);var ds=EditorSceneManager.OpenScene(ScreenshotFollowupRepair.DS,OpenSceneMode.Additive);var nodes=All(gs).Concat(All(ds)).ToArray();
            var authored=nodes.Select(t=>t.GetComponent<MeshFilter>()).Where(IslandSolidFoundation.Eligible).ToArray();r.authoringEligible=authored.Length;
            var result=JsonUtility.FromJson<IslandSolidFoundation.Result>(File.ReadAllText(Path.Combine(Reports,"island-foundation-complete.json")));var owners=new HashSet<MeshFilter>();
            foreach(var part in result.parts)
            {
                var foundation=nodes.Select(t=>t.GetComponent<MeshFilter>()).Single(t=>t&&AssetDatabase.GetAssetPath(t.sharedMesh)==part.mesh);int index=int.Parse(foundation.name.Split('_')[1]);
                if(index>=authored.Length||P(authored[index].transform)!=part.source)throw new Exception("Authoring target order changed: "+part.mesh);
                var owner=authored[index];var b=owner.GetComponent<Renderer>().bounds;b.Expand(.004f);if(!b.Contains(part.point))throw new Exception("Source identity/geometry mismatch: "+part.mesh);
                part.sourceId=GlobalObjectId.GetGlobalObjectIdSlow(owner).ToString();if(!owners.Add(owner))throw new Exception("Two foundations assigned to the same source object");
            }
            r.foundationOwners=owners.Count;foreach(var root in nodes.Select(t=>t.GetComponent<DressingSceneRoot>()).Where(d=>d))root.ActivateAllImmediate();Physics.SyncTransforms();var lands=nodes.Select(t=>t.GetComponent<Terrain>()).Where(t=>t).ToArray();var eligible=nodes.Select(t=>t.GetComponent<MeshFilter>()).Where(IslandSolidFoundation.Eligible).ToArray();r.runtimeEligible=eligible.Length;
            foreach(var f in eligible.Where(f=>!owners.Contains(f)))
            {
                var local=new Coverage();var v=f.sharedMesh.vertices;var triangles=f.sharedMesh.triangles;bool mirror=f.transform.localToWorldMatrix.determinant<0;
                for(int i=0;i<triangles.Length;i+=3){var a=f.transform.TransformPoint(v[triangles[i]]);var b=f.transform.TransformPoint(v[triangles[i+1]]);var c=f.transform.TransformPoint(v[triangles[i+2]]);var n=Vector3.Cross(b-a,c-a).normalized;if((mirror?-n.y:n.y)<.45f)continue;SampleUnfilled(a,b,c,lands,local,0);}
                r.supportSamples+=local.supportSamples;r.maxUnfilledGap=Mathf.Max(r.maxUnfilledGap,local.maxUnfilledGap);if(local.maxUnfilledGap>.15f)r.failures.Add("Unfilled source "+GlobalObjectId.GetGlobalObjectIdSlow(f)+" "+P(f.transform)+" gap="+local.maxUnfilledGap);else r.groundedWithoutFoundation++;
            }
            r.passed=r.foundationOwners==result.parts.Count&&r.foundationOwners+r.groundedWithoutFoundation==r.runtimeEligible&&r.failures.Count==0;
            if(r.passed)File.WriteAllText(Path.Combine(Reports,"island-foundation-complete.json"),JsonUtility.ToJson(result,true));
        }
        catch(Exception e){r.error=e.ToString();Debug.LogException(e);}
        finally{File.WriteAllText(Path.Combine(Reports,"island-foundation-coverage-identity.json"),JsonUtility.ToJson(r,true));EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");}
    }
    public static void NativeSurfacePriority()
    {
        // Derived from the project's matching SDK Lit source. Only the new foundation
        // material gets a positive raster depth bias, preserving the native skin.
        var source=File.ReadAllText("Assets/SushiAndFish/Casino/SDKShaders/Lit.shader");
        var derived=source.Replace("Shader \"Universal Render Pipeline/Lit\"","Shader \"SushiAndFish/FoundationNativeSurfacePriority\"").Replace("LOD 300","LOD 300\n        Offset 1, 1");
        string path=Output+"/FoundationNativeSurfacePriority.shader";File.WriteAllText(path,derived);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var shader=AssetDatabase.LoadAssetAtPath<Shader>(path);if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Foundation depth-bias shader did not compile");
        var material=AssetDatabase.LoadAssetAtPath<Material>(Output+"/FoundationStone.mat");material.shader=shader;EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine(Reports,"island-foundation-native-surface-priority.json"),"{\"shader\":\""+path+"\",\"offsetFactor\":1,\"offsetUnits\":1,\"supported\":true,\"compilerErrors\":false,\"reference\":\"https://docs.unity3d.com/cn/current/Manual/SL-Offset.html\",\"geometryChanged\":false}");
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
    }
    public static void RefreshAndRecheck(){IslandFrontEndBuilder.RefreshMenuPresentation();ScreenshotFollowupRepair.VerifySaved();ScreenshotFollowupRepair.InspectContacts();EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");}
}
