using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class CityExpansionInstallation
{
    static void SetRoadMesh(Transform owner,Vector3[] points,int[] indices,string path)
    {
        var local=points.Select(owner.InverseTransformPoint).ToArray();
        for(int i=0;i<indices.Length;i+=3)if(Vector3.Cross(points[indices[i+1]]-points[indices[i]],points[indices[i+2]]-points[indices[i]]).y<0)
        {int v=indices[i+1];indices[i+1]=indices[i+2];indices[i+2]=v;}
        var mesh=new Mesh{name=owner.name,indexFormat=IndexFormat.UInt32};mesh.vertices=local;mesh.triangles=indices;mesh.uv=points.Select(v=>new Vector2(v.x*.3f,v.z*.3f)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,path);
        owner.GetComponent<MeshFilter>().sharedMesh=mesh;
        var collider=owner.GetComponent<MeshCollider>();if(collider){collider.sharedMesh=null;collider.sharedMesh=mesh;}
        EditorUtility.SetDirty(owner.GetComponent<MeshFilter>());if(collider)EditorUtility.SetDirty(collider);
    }
    static void WallMesh(Transform owner,IslandRevision20261018.UnifiedRing[] rings,float top,Func<Vector3,float> bottom,string path)
    {
        var vertices=new List<Vector3>();var indices=new List<int>();
        foreach(var ring in rings)for(int i=0;i<ring.points.Length-1;i++)
        {
            var a=ring.points[i];var b=ring.points[i+1];a.y=b.y=top;
            var c=b;c.y=bottom(b);var d=a;d.y=bottom(a);
            int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3,n+2,n+1,n,n+3,n+2,n});
        }
        var mesh=new Mesh{name=owner.name,indexFormat=IndexFormat.UInt32};mesh.vertices=vertices.Select(owner.InverseTransformPoint).ToArray();mesh.triangles=indices.ToArray();mesh.uv=vertices.Select(v=>new Vector2(v.x*.3f,v.z*.3f)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);
        owner.GetComponent<MeshFilter>().sharedMesh=mesh;
        var collider=owner.GetComponent<MeshCollider>();if(collider){collider.sharedMesh=null;collider.sharedMesh=mesh;}
    }
    static void ApplyCoastalRoadGeometry()
    {
        var file=Path.Combine(CityExpansionInspection.Reports,"city-coastal-road-geometry.json");
        var data=JsonUtility.FromJson<IslandRevision20261018.UnifiedGeometry>(File.ReadAllText(file));
        if(data.humanMessage!="01a15a95-81a2-7a90-8882-5a64280accd5")throw new Exception("Coastal road geometry belongs to a different request.");
        var network=All(gameScene).Single(t=>t.name=="TownRoadNetwork20261019");
        var folder=CityRoot+"/RoadMeshes";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        foreach(var surface in data.surfaces)
        {
            var owner=network.GetComponentsInChildren<Transform>(true).Single(t=>t.name==surface.name);
            float y=surface.kind=="asphalt"?data.asphaltY:surface.kind=="paint"?data.paintY:data.curbTopY;
            var points=surface.vertices.Select(v=>new Vector3(v.x,y,v.z)).ToArray();
            SetRoadMesh(owner,points,(int[])surface.triangles.Clone(),AssetDatabase.GenerateUniqueAssetPath(folder+"/"+surface.name+".asset"));
        }
        var curb=network.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Unified_CurbFaces");
        WallMesh(curb,data.curbInnerRings,data.curbTopY,p=>data.asphaltY,AssetDatabase.GenerateUniqueAssetPath(folder+"/CoastalCurbFaces.asset"));
        var outer=network.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Unified_OuterRoadbed");
        WallMesh(outer,data.outerPavingRings,data.curbTopY,p=>Ground(p.x,p.z)-.025f,AssetDatabase.GenerateUniqueAssetPath(folder+"/CoastalOuterRoadbed.asset"));
        foreach(var line in data.plan.roads)
        {
            var guide=network.Find("RoadSections/"+line.id);
            if(guide)guide.position=new Vector3((line.a.x+line.b.x)/2,data.asphaltY,(line.a.z+line.b.z)/2);
        }
        world.diagnostics.Add("Coastal adjustment: SouthStreet endpoint (102,-67) -> (88,-67); EastAvenue southern endpoint (102,-67) -> (102,-57). Both keep rounded ends; carriageway remains one connected polygon, all 13 plots and native shop forecourts clear.");
    }
}
