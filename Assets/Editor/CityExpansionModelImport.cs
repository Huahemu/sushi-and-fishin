using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class CityExpansionModelImport
{
    const string Root="Assets/SushiAndFish/CityExpansion";
    static readonly string[] Names={"AR_Restaurant","AR_CinemaLobby","AR_CinemaAuditorium"};
    [Serializable]public class MaterialData {public string asset,name,image,alpha;public float[] color,emission;public float metallic,roughness;public bool doubleSided;}
    [Serializable]public class MaterialList {public MaterialData[] materials;}
    [Serializable]public class Node {public string name,path;public Vector3 position,rotation,scale,min,max;}
    [Serializable]public class Model {public string name,prefab;public Vector3 min,max;public int renderers,colliders,materials;public List<Node> anchors=new List<Node>();}
    [Serializable]public class Receipt {public string utc,error;public List<Model> models=new List<Model>();}
    public static void ImportAndInspect()
    {
        var receipt=new Receipt{utc=DateTime.UtcNow.ToString("o")};
        try
        {
            Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Prefabs");AssetDatabase.Refresh();
            var entries=JsonUtility.FromJson<MaterialList>(File.ReadAllText(Root+"/SourceMaterials.json"));
            var materials=new Dictionary<string,Material>();
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)throw new Exception("Host URP/Lit shader unavailable.");
            foreach(var entry in entries.materials)
            {
                var path=Root+"/Materials/"+entry.name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}mat.shader=shader;
                Color color=new Color(entry.color[0],entry.color[1],entry.color[2],entry.color[3]);
                // glTF factors are linear; the inspector color property follows
                // Unity's normal sRGB material workflow.
                color=new Color(color.gamma.r,color.gamma.g,color.gamma.b,color.a);
                mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",entry.metallic);mat.SetFloat("_Smoothness",1-entry.roughness);
                if(!string.IsNullOrEmpty(entry.image))
                {
                    var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Models/"+entry.asset+"/Textures/"+entry.image+".png");
                    if(!texture)throw new Exception("Source base color texture missing: "+entry.image);
                    mat.SetTexture("_BaseMap",texture);
                }
                bool glass=entry.name.EndsWith("_glass",StringComparison.OrdinalIgnoreCase);
                if(glass||entry.alpha=="BLEND")
                {
                    color.a=glass?.18f:color.a;mat.SetColor("_BaseColor",color);mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",0);mat.SetFloat("_ZWrite",0);
                    mat.SetFloat("_SrcBlend",(int)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
                    mat.SetOverrideTag("RenderType","Transparent");mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=(int)RenderQueue.Transparent;
                }
                else {mat.SetFloat("_Surface",0);mat.SetFloat("_ZWrite",1);mat.renderQueue=-1;mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");}
                mat.SetFloat("_Cull",entry.doubleSided||glass?0:2);
                if(entry.emission!=null&&entry.emission.Any(x=>x>0)){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",new Color(entry.emission[0],entry.emission[1],entry.emission[2]));}
                EditorUtility.SetDirty(mat);materials.Add(entry.name,mat);
            }
            AssetDatabase.SaveAssets();
            foreach(string name in Names)
            {
                string source=Root+"/Models/"+name+"/"+name+".fbx";
                var importer=(ModelImporter)AssetImporter.GetAtPath(source);
                importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=true;
                importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;importer.addCollider=false;
                importer.importNormals=ModelImporterNormals.Import;importer.meshCompression=ModelImporterMeshCompression.Off;
                importer.SaveAndReimport();
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(source);if(!asset)throw new Exception("FBX did not import: "+source);
                var go=new GameObject(name);
                // Keep the FBX root's importer axis conversion under a neutral
                // metre-scale placement root. Resetting its rotation loses the
                // authored Z-up to Unity Y-up conversion.
                UnityEngine.Object.Instantiate(asset,go.transform,false);
                foreach(var animator in go.GetComponentsInChildren<Animator>(true))UnityEngine.Object.DestroyImmediate(animator);
                foreach(var animation in go.GetComponentsInChildren<Animation>(true))UnityEngine.Object.DestroyImmediate(animation);
                var row=new Model{name=name,prefab=Root+"/Prefabs/"+name+".prefab"};
                foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>{
                        string key=m.name.Replace(" (Instance)","");
                        if(!materials.TryGetValue(key,out var replacement))throw new Exception("FBX material was not mapped: "+key);
                        return replacement;
                    }).ToArray();
                    renderer.shadowCastingMode=ShadowCastingMode.On;
                    var mesh=renderer.GetComponent<MeshFilter>();
                    // Keep the shell and substantial furniture solid. Tiny
                    // dishes, signs, plants and decorative trim stay visual.
                    if(mesh&&mesh.sharedMesh&&Regex.IsMatch(renderer.name,@"Foundation|Floor|Wall|Glass|Glazing|Post|RoofSlab|DoorHeader|FrontHeader|Threshold|Approach|TableTop|TableBase|Chair.*_(Seat|Back)$|StoolSeat|SushiBar(Base|Top)|Kitchen.*(Base|Counter|Work)|TicketCounter|PopcornCounter|ConcessionCounter(Body|Top)|Auditorium.*(Step|Riser)|Tier_|HalfStep|RearCorridor|ScreenFrame",RegexOptions.IgnoreCase))
                    {
                        var collider=renderer.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh.sharedMesh;
                    }
                }
                var bounds=IslandComprehensiveRepair.BoundsOf(go.transform);row.min=bounds.min;row.max=bounds.max;
                if(bounds.size.x<8||bounds.size.x>25||bounds.size.y<2.7f||bounds.size.y>12||bounds.size.z<7||bounds.size.z>30)throw new Exception("Unexpected metre scale: "+name+" "+bounds);
                foreach(var t in go.GetComponentsInChildren<Transform>(true).Where(t=>Regex.IsMatch(t.name,@"Entrance_[LR]$|Entrance_Trigger$|Arrival|Return|Portal|Chair.*_Seat$|StoolSeat|Entry|Lobby",RegexOptions.IgnoreCase)))
                {
                    var b=IslandComprehensiveRepair.BoundsOf(t);
                    row.anchors.Add(new Node{name=t.name,path=CityExpansionInspection.PathOf(t),position=t.position,rotation=t.eulerAngles,scale=t.lossyScale,min=b.min,max=b.max});
                }
                row.renderers=go.GetComponentsInChildren<Renderer>(true).Length;row.colliders=go.GetComponentsInChildren<Collider>(true).Length;row.materials=go.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Distinct().Count();
                PrefabUtility.SaveAsPrefabAsset(go,row.prefab);UnityEngine.Object.DestroyImmediate(go);receipt.models.Add(row);
            }
            AssetDatabase.SaveAssets();Debug.Log("CITY_BUILDING_ASSETS_IMPORTED "+receipt.models.Count);
        }
        catch(Exception e){receipt.error=e.ToString();throw;}
        finally{File.WriteAllText(Path.Combine(CityExpansionInspection.Reports,"city-building-import-inspection.json"),JsonUtility.ToJson(receipt,true));}
    }
}
