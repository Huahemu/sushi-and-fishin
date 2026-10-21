using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class CityCasinoSceneUnbatch
{
    const string ScenePath="Assets/SushiAndFish/Casino/Imported/Scenes/CasinoScene.unity";
    const string MeshRoot="Assets/SushiAndFish/Casino/RelocatableMeshes";
    [Serializable]public class Receipt {public string utc,error;public int restored,vertices,flagsCleared;public float maxVertexError;public List<string> rows=new List<string>();}
    public static void ApplyAndProbe(){Apply();CityExpansionPlayAudit.VisualProbe();}
    public static void Apply()
    {
        var receipt=new Receipt{utc=DateTime.UtcNow.ToString("o")};
        try
        {
            var scene=EditorSceneManager.OpenScene(ScenePath);Directory.CreateDirectory(MeshRoot);AssetDatabase.Refresh();
            var nodes=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach(var t in nodes)
            {
                var flags=GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                if((flags&StaticEditorFlags.BatchingStatic)!=0){GameObjectUtility.SetStaticEditorFlags(t.gameObject,flags&~StaticEditorFlags.BatchingStatic);receipt.flagsCleared++;}
                var renderer=t.GetComponent<MeshRenderer>();var filter=t.GetComponent<MeshFilter>();if(!renderer||!filter||!filter.sharedMesh)continue;
                var data=new SerializedObject(renderer);var batch=data.FindProperty("m_StaticBatchInfo");if(batch==null)continue;
                int first=batch.FindPropertyRelative("firstSubMesh").intValue,count=batch.FindPropertyRelative("subMeshCount").intValue;if(count==0)continue;
                var source=filter.sharedMesh;if(first+count>source.subMeshCount)throw new Exception("Invalid source static submesh range: "+t.name);
                var sourceVertices=source.vertices;var sourceNormals=source.normals;var sourceTangents=source.tangents;var sourceColors=source.colors32;
                var sourceUv=new List<Vector4>[8];for(int channel=0;channel<8;channel++){sourceUv[channel]=new List<Vector4>();source.GetUVs(channel,sourceUv[channel]);}
                var map=new Dictionary<int,int>();var ids=new List<int>();var triangles=new List<int[]>();
                for(int sub=0;sub<count;sub++)
                {
                    var indices=source.GetTriangles(first+sub);var converted=new int[indices.Length];
                    for(int i=0;i<indices.Length;i++){int old=indices[i];if(!map.TryGetValue(old,out int next)){next=ids.Count;map.Add(old,next);ids.Add(old);}converted[i]=next;}
                    triangles.Add(converted);
                }
                Matrix4x4 inverse=t.worldToLocalMatrix,normalInverse=t.localToWorldMatrix.transpose;
                var vertices=ids.Select(i=>inverse.MultiplyPoint3x4(sourceVertices[i])).ToArray();
                for(int i=0;i<ids.Count;i++)receipt.maxVertexError=Mathf.Max(receipt.maxVertexError,Vector3.Distance(t.TransformPoint(vertices[i]),sourceVertices[ids[i]]));
                if(receipt.maxVertexError>.005f)throw new Exception("Static batch inverse transform lost source world coordinates.");
                var mesh=new Mesh{name=t.name+"_Relocatable",indexFormat=IndexFormat.UInt32};mesh.vertices=vertices;mesh.subMeshCount=count;
                for(int sub=0;sub<count;sub++)mesh.SetTriangles(triangles[sub],sub);
                if(sourceNormals.Length==sourceVertices.Length)mesh.normals=ids.Select(i=>normalInverse.MultiplyVector(sourceNormals[i]).normalized).ToArray();else mesh.RecalculateNormals();
                if(sourceTangents.Length==sourceVertices.Length)mesh.tangents=ids.Select(i=>{var v=sourceTangents[i];var d=inverse.MultiplyVector(new Vector3(v.x,v.y,v.z)).normalized;return new Vector4(d.x,d.y,d.z,v.w);}).ToArray();
                if(sourceColors.Length==sourceVertices.Length)mesh.colors32=ids.Select(i=>sourceColors[i]).ToArray();
                for(int channel=0;channel<8;channel++)if(sourceUv[channel].Count==sourceVertices.Length)mesh.SetUVs(channel,ids.Select(i=>sourceUv[channel][i]).ToList());
                mesh.RecalculateBounds();
                string name="Object_"+receipt.restored.ToString("0000")+".asset";AssetDatabase.CreateAsset(mesh,MeshRoot+"/"+name);
                batch.FindPropertyRelative("firstSubMesh").intValue=0;batch.FindPropertyRelative("subMeshCount").intValue=0;
                var root=data.FindProperty("m_StaticBatchRoot");if(root!=null)root.objectReferenceValue=null;
                data.ApplyModifiedPropertiesWithoutUndo();filter.sharedMesh=mesh;renderer.ResetBounds();EditorUtility.SetDirty(filter);EditorUtility.SetDirty(renderer);
                receipt.rows.Add(CityExpansionInspection.PathOf(t)+" source="+AssetDatabase.GetAssetPath(source)+" sourceSubmesh="+first+"+"+count+" vertices="+ids.Count);
                receipt.vertices+=ids.Count;receipt.restored++;
            }
            if(receipt.restored<100)throw new Exception("Expected the recovered scene's world-space static batches.");
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        }
        catch(Exception e){receipt.error=e.ToString();throw;}
        finally{File.WriteAllText(Path.Combine(CityExpansionInspection.Reports,"city-casino-relocatable-meshes.json"),JsonUtility.ToJson(receipt,true));}
    }
}
