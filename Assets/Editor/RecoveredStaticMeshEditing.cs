using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Reconstruct independent local meshes from the published static-batch
// submesh ranges, retaining topology, attributes and world-space geometry.
public static class RecoveredStaticMeshEditing
{
    private sealed class SourceData
    {
        public Vector3[] vertices,normals;
        public Vector4[] tangents;
        public Color[] colors;
        public List<Vector4>[] uv=new List<Vector4>[8];
        public SourceData(Mesh mesh)
        {
            if(mesh.blendShapeCount!=0 || mesh.HasVertexAttribute(VertexAttribute.BlendIndices) || mesh.HasVertexAttribute(VertexAttribute.BlendWeight))
                throw new InvalidOperationException("Static source has skin/morph attributes: "+mesh.name);
            vertices=mesh.vertices; normals=mesh.normals; tangents=mesh.tangents; colors=mesh.colors;
            for(int channel=0;channel<8;channel++) { uv[channel]=new List<Vector4>(); mesh.GetUVs(channel,uv[channel]); }
        }
    }
    [Serializable] private class SceneRow { public string scene; public int converted,vertices; public double maxWorldPositionError; public string error; }
    [Serializable] private class Report { public string utc; public List<SceneRow> scenes=new List<SceneRow>(); public int totalConverted,totalVertices; public bool allPassed; }
    private static readonly Dictionary<Mesh,SourceData> cache=new Dictionary<Mesh,SourceData>();
    private static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
    private static int createdVertices;
    private const string MeshFolder="Assets/RecoveredIndependentMeshes";

    private static int Convert(MeshRenderer renderer,out double maximumError)
    {
        maximumError=0;
        if(!renderer.isPartOfStaticBatch) return 0;
        var filter=renderer.GetComponent<MeshFilter>();
        if(!filter || !filter.sharedMesh) throw new InvalidOperationException("Static renderer has no mesh: "+renderer.name);
        if(renderer.additionalVertexStreams) throw new InvalidOperationException("Additional vertex stream needs separate reconstruction: "+renderer.name);
        var source=filter.sharedMesh;
        var serialized=new SerializedObject(renderer);
        var batch=serialized.FindProperty("m_StaticBatchInfo");
        int first=batch.FindPropertyRelative("firstSubMesh").intValue;
        int count=batch.FindPropertyRelative("subMeshCount").intValue;
        if(count<=0 || first+count>source.subMeshCount) throw new InvalidOperationException("Invalid original submesh range: "+renderer.name);
        var batchRoot=serialized.FindProperty("m_StaticBatchRoot").objectReferenceValue as Transform;
        var rootMatrix=batchRoot?batchRoot.localToWorldMatrix:Matrix4x4.identity;
        var toLocal=filter.transform.worldToLocalMatrix*rootMatrix;
        var normalMatrix=toLocal.inverse.transpose;
        if(!cache.TryGetValue(source,out var data)) { data=new SourceData(source); cache.Add(source,data); }
        var indices=new int[count][]; var mapping=new Dictionary<int,int>(); var originalVertices=new List<int>();
        for(int sub=0;sub<count;sub++)
        {
            indices[sub]=source.GetIndices(first+sub);
            for(int i=0;i<indices[sub].Length;i++)
            {
                int index=indices[sub][i];
                if(!mapping.TryGetValue(index,out int converted)) { converted=originalVertices.Count; mapping.Add(index,converted); originalVertices.Add(index); }
                indices[sub][i]=converted;
            }
        }
        createdVertices+=originalVertices.Count;
        if(createdVertices>10000000) throw new InvalidOperationException("Independent mesh recovery exceeded the ten-million vertex budget");
        var vertices=new List<Vector3>(); var normals=new List<Vector3>(); var tangents=new List<Vector4>(); var colors=new List<Color>();
        var uv=new List<Vector4>[8]; for(int c=0;c<8;c++) uv[c]=new List<Vector4>();
        foreach(int index in originalVertices)
        {
            var vertex=toLocal.MultiplyPoint3x4(data.vertices[index]); vertices.Add(vertex);
            double error=Vector3.Distance(rootMatrix.MultiplyPoint3x4(data.vertices[index]),filter.transform.localToWorldMatrix.MultiplyPoint3x4(vertex));
            maximumError=Math.Max(maximumError,error);
            if(data.normals.Length>0) normals.Add(normalMatrix.MultiplyVector(data.normals[index]).normalized);
            if(data.tangents.Length>0)
            {
                var original=data.tangents[index]; var tangent=toLocal.MultiplyVector(new Vector3(original.x,original.y,original.z)).normalized;
                tangents.Add(new Vector4(tangent.x,tangent.y,tangent.z,original.w*Mathf.Sign(toLocal.determinant)));
            }
            if(data.colors.Length>0) colors.Add(data.colors[index]);
            for(int c=0;c<8;c++) if(data.uv[c].Count>0) uv[c].Add(data.uv[c][index]);
        }
        if(maximumError>0.002) throw new InvalidOperationException("World geometry round-trip error too large: "+maximumError+" for "+renderer.name);
        var mesh=new Mesh { name=renderer.name+" - independent recovered mesh",indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16 };
        mesh.SetVertices(vertices);
        if(normals.Count>0) mesh.SetNormals(normals);
        if(tangents.Count>0) mesh.SetTangents(tangents);
        if(colors.Count>0) mesh.SetColors(colors);
        for(int c=0;c<8;c++) if(uv[c].Count>0) mesh.SetUVs(c,uv[c]);
        mesh.subMeshCount=count;
        for(int sub=0;sub<count;sub++) mesh.SetIndices(indices[sub],source.GetTopology(first+sub),sub,false);
        mesh.RecalculateBounds();
        if(!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder("Assets","RecoveredIndependentMeshes");
        string name=renderer.name; foreach(char c in Path.GetInvalidFileNameChars()) name=name.Replace(c,'_'); name=name.Replace('/','_').Replace('\\','_');
        if(name.Length>80) name=name.Substring(0,80);
        // Thousands of repeated rock names make GenerateUniqueAssetPath scan
        // growing numeric suffix lists. Give each recovered mesh a unique ID.
        string assetPath=MeshFolder+"/"+name+"_"+Guid.NewGuid().ToString("N")+".asset";
        AssetDatabase.CreateAsset(mesh,assetPath);
        Undo.RecordObjects(new UnityEngine.Object[]{filter,renderer},"Recover independent mesh from static batch");
        filter.sharedMesh=mesh;
        batch.FindPropertyRelative("firstSubMesh").intValue=0;
        batch.FindPropertyRelative("subMeshCount").intValue=0;
        serialized.FindProperty("m_StaticBatchRoot").objectReferenceValue=null;
        serialized.ApplyModifiedProperties();
        if(renderer.lightmapIndex>=0 && renderer.lightmapIndex<LightmapSettings.lightmaps.Length) renderer.lightmapScaleOffset=new Vector4(1,1,0,0);
        EditorUtility.SetDirty(filter); EditorUtility.SetDirty(renderer);
        if(renderer.isPartOfStaticBatch) throw new InvalidOperationException("Static batch flag was not cleared: "+renderer.name);
        return vertices.Count;
    }

    [MenuItem("Tools/Recovered original game/Make selected static-batched objects independently editable")]
    public static void ConvertSelection()
    {
        cache.Clear(); createdVertices=0;
        foreach(var root in Selection.gameObjects)
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) if(renderer.isPartOfStaticBatch) Convert(renderer,out _);
        cache.Clear(); SceneView.RepaintAll();
    }
    public static void RecoverAllOriginalScenes()
    {
        var report=new Report { utc=DateTime.UtcNow.ToString("o"),allPassed=true };
        foreach(var item in EditorBuildSettings.scenes)
        {
            if(!item.enabled) continue;
            var row=new SceneRow { scene=item.path }; report.scenes.Add(row); cache.Clear();
            try
            {
                var scene=EditorSceneManager.OpenScene(item.path,OpenSceneMode.Single);
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach(var root in scene.GetRootGameObjects())
                    foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if(!renderer.isPartOfStaticBatch) continue;
                        row.vertices+=Convert(renderer,out double error); row.converted++; row.maxWorldPositionError=Math.Max(row.maxWorldPositionError,error);
                    }
                }
                finally { AssetDatabase.StopAssetEditing(); }
                if(row.converted>0)
                {
                    var backup=Path.Combine(Root,"backup/scenes-before-independent-mesh-recovery",item.path);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    if(File.Exists(backup)) throw new IOException("Scene before-copy already exists: "+backup);
                    File.Copy(item.path,backup); File.Copy(item.path+".meta",backup+".meta");
                    OriginalNativeShaderRestoration.RestoreSerializedReferences();
                    EditorGraphicsModeCompatibility.Restore();
                    EditorSceneManager.SaveScene(scene);
                }
                report.totalConverted+=row.converted; report.totalVertices+=row.vertices;
            }
            catch(Exception e) { row.error=e.ToString(); report.allPassed=false; }
            Debug.Log("RECOVERY_INDEPENDENT_MESH_SCENE "+JsonUtility.ToJson(row));
            File.WriteAllText(Path.Combine(Root,"reports/independent-mesh-recovery.json"),JsonUtility.ToJson(report,true));
            cache.Clear(); GC.Collect();
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine(Root,"reports/independent-mesh-recovery.json"),JsonUtility.ToJson(report,true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);
        EditorApplication.Exit(report.allPassed?0:2);
    }
}
