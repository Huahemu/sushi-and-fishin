using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class IslandRevision20261018
{
    [Serializable] public class DensityCell {public int x,z,count;}
    [Serializable] public class DetailLayerArchive {public string terrain,terrainData,prototype,texture,prefab,heightHash;public int layer,width,height,instances;public float minWidth,maxWidth,minHeight,maxHeight;public Color healthy,dry;public DensityCell[] density;}
    [Serializable] public class TerrainDetailArchive {public string utc,stage;public DetailLayerArchive[] layers;}
    static string HeightHash(TerrainData data) {
        var heights=data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution);var bytes=new byte[heights.Length*4];Buffer.BlockCopy(heights,0,bytes,0,bytes.Length);
        using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(bytes).Select(b=>b.ToString("x2")));
    }
    public static void ExportTerrainDetails() {
        var scenes=LoadIsland();var layers=new List<DetailLayerArchive>();
        const string folder=LibraryRoot+"/TerrainDetails";
        if(AssetDatabase.IsValidFolder(LibraryRoot+"/TerrainGrass")&&!AssetDatabase.IsValidFolder(folder)) {
            string moveError=AssetDatabase.MoveAsset(LibraryRoot+"/TerrainGrass",folder);if(moveError!="")throw new IOException(moveError);
        }
        Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        foreach(var terrain in scenes.SelectMany(All).Select(t=>t.GetComponent<Terrain>()).Where(t=>t)) {
            var data=terrain.terrainData;var prototypes=data.detailPrototypes;
            for(int layer=0;layer<prototypes.Length;layer++) {
                var density=data.GetDetailLayer(0,0,data.detailWidth,data.detailHeight,layer);var cells=new List<DensityCell>();int count=0;
                for(int z=0;z<density.GetLength(0);z++)for(int x=0;x<density.GetLength(1);x++)if(density[z,x]>0){cells.Add(new DensityCell{x=x,z=z,count=density[z,x]});count+=density[z,x];}
                if(count==0)continue;var proto=prototypes[layer];string key=Sha(AssetDatabase.GetAssetPath(proto.prototype)+AssetDatabase.GetAssetPath(proto.prototypeTexture)+proto.minWidth+proto.maxWidth+proto.minHeight+proto.maxHeight).Substring(0,12);
                string path=folder+"/TerrainGrass_"+key+".prefab";
                if(!File.Exists(path)) {
                    GameObject model;
                    if(proto.prototype)model=UnityEngine.Object.Instantiate(proto.prototype);
                    else {
                        if(!proto.prototypeTexture)throw new Exception("Terrain detail prototype has neither a model nor a texture");
                        model=new GameObject("TerrainGrass_"+key);float w=(proto.minWidth+proto.maxWidth)*.5f,h=(proto.minHeight+proto.maxHeight)*.5f;
                        var mesh=new Mesh{name="TerrainGrass_CrossedCards_"+key};
                        mesh.vertices=new[]{new Vector3(-w*.5f,0,0),new Vector3(w*.5f,0,0),new Vector3(-w*.5f,h,0),new Vector3(w*.5f,h,0),new Vector3(0,0,-w*.5f),new Vector3(0,0,w*.5f),new Vector3(0,h,-w*.5f),new Vector3(0,h,w*.5f)};
                        mesh.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(0,1),new Vector2(1,1),new Vector2(0,0),new Vector2(1,0),new Vector2(0,1),new Vector2(1,1)};
                        mesh.triangles=new[]{0,2,1,1,2,3,4,6,5,5,6,7};mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();
                        AssetDatabase.CreateAsset(mesh,folder+"/TerrainGrass_"+key+".asset");model.AddComponent<MeshFilter>().sharedMesh=mesh;
                        var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="TerrainGrass_"+key};
                        material.SetTexture("_BaseMap",proto.prototypeTexture);material.SetColor("_BaseColor",proto.healthyColor);material.SetFloat("_AlphaClip",1);material.EnableKeyword("_ALPHATEST_ON");material.SetFloat("_Cutoff",.35f);material.SetFloat("_Cull",0);material.SetFloat("_Smoothness",0);material.renderQueue=2450;
                        AssetDatabase.CreateAsset(material,folder+"/TerrainGrass_"+key+".mat");model.AddComponent<MeshRenderer>().sharedMaterial=material;
                    }
                    model.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);model.SetActive(true);foreach(var b in model.GetComponentsInChildren<MonoBehaviour>(true).Reverse())if(b)UnityEngine.Object.DestroyImmediate(b);
                    PrefabUtility.SaveAsPrefabAsset(model,path,out bool success);UnityEngine.Object.DestroyImmediate(model);if(!success)throw new Exception("Terrain grass prefab export failed");
                }
                layers.Add(new DetailLayerArchive{terrain=terrain.name,terrainData=AssetDatabase.GetAssetPath(data),prototype=AssetDatabase.GetAssetPath(proto.prototype),texture=AssetDatabase.GetAssetPath(proto.prototypeTexture),prefab=path,heightHash=HeightHash(data),layer=layer,width=data.detailWidth,height=data.detailHeight,instances=count,minWidth=proto.minWidth,maxWidth=proto.maxWidth,minHeight=proto.minHeight,maxHeight=proto.maxHeight,healthy=proto.healthyColor,dry=proto.dryColor,density=cells.ToArray()});
            }
        }
        AssetDatabase.SaveAssets();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(var layer in layers)if(!AssetDatabase.LoadAssetAtPath<GameObject>(layer.prefab))throw new Exception("Terrain grass resource could not be reopened");
        File.WriteAllText(Path.Combine(Reports,"island-terrain-details-export.json"),JsonUtility.ToJson(new TerrainDetailArchive{utc=DateTime.UtcNow.ToString("o"),stage="EXPORTED_AND_VERIFIED",layers=layers.ToArray()},true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);Debug.Log("ISLAND_TERRAIN_DETAILS_ARCHIVED "+layers.Sum(l=>l.instances));
    }
}
