using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static partial class IslandRevision20261018
{
    [Serializable] public class PavingPhysicsSample {public string owner,colliders,scale;public bool supported;public Vector3 point;public string[] hits;}
    [Serializable] public class PavingPhysicsReport {public string utc;public int samples,unsupported;public PavingPhysicsSample[] rows;public string[] fishWalkObstacles,publicFishPathBlockers,publicGreenPathBlockers;}
    static string[] PublicPathBlockers(Vector3 a,Vector3 b){a.y=b.y=3.169f;var direction=(b-a).normalized;return Physics.CapsuleCastAll(a+Vector3.up*.35f,a+Vector3.up*1.45f,.35f,direction,Vector3.Distance(a,b),~0,QueryTriggerInteraction.Ignore).Where(h=>!(h.collider is TerrainCollider)).Select(h=>P(h.collider.transform)+" distance="+h.distance).Distinct().ToArray();}
    public static void DiagnoseUnifiedPavingPhysics() {
        var scenes=LoadIsland();var all=scenes.SelectMany(All).ToArray();var data=JsonUtility.FromJson<UnifiedGeometry>(File.ReadAllText(Path.Combine(Reports,"town-unified-road-geometry.json")));Physics.SyncTransforms();var rows=new List<PavingPhysicsSample>();
        foreach(var surface in data.surfaces.Where(s=>!string.IsNullOrEmpty(s.nativeOwner))){var owner=all.Single(t=>P(t)==surface.nativeOwner);var mesh=owner.GetComponent<MeshFilter>().sharedMesh;var vs=mesh.vertices;var ts=mesh.triangles;for(int i=0;i<ts.Length;i+=3*Mathf.Max(1,ts.Length/24)){var point=owner.TransformPoint((vs[ts[i]]+vs[ts[i+1]]+vs[ts[i+2]])/3);var hits=Physics.RaycastAll(point+Vector3.up*.3f,Vector3.down,.6f,~0,QueryTriggerInteraction.Ignore);rows.Add(new PavingPhysicsSample{owner=P(owner),point=point,scale=owner.lossyScale.ToString("R"),colliders=string.Join(" | ",owner.GetComponents<Collider>().Select(c=>c.GetType().Name+" enabled="+c.enabled+" active="+c.gameObject.activeInHierarchy+" trigger="+c.isTrigger+(c is MeshCollider m?" mesh="+AssetDatabase.GetAssetPath(m.sharedMesh)+" convex="+m.convex+" cooking="+m.cookingOptions:""))),supported=hits.Any(h=>h.collider.transform==owner&&Mathf.Abs(h.point.y-3.159f)<.001f&&h.normal.y>.9f),hits=hits.Select(h=>P(h.collider.transform)+" point="+h.point.ToString("R")+" normal="+h.normal.ToString("R")).ToArray()});}}
        var obstacles=Physics.OverlapCapsule(new Vector3(-10.13f,3.55f,-2.16f),new Vector3(-10.13f,4.6f,-2.16f),.4f,~0,QueryTriggerInteraction.Ignore).Where(c=>!(c is TerrainCollider)).Select(c=>P(c.transform)+" "+c.GetType().Name+" bounds="+c.bounds).ToArray();File.WriteAllText(Path.Combine(Reports,"town-unified-paving-physics-diagnostic.json"),JsonUtility.ToJson(new PavingPhysicsReport{utc=DateTime.UtcNow.ToString("o"),samples=rows.Count,unsupported=rows.Count(r=>!r.supported),rows=rows.ToArray(),fishWalkObstacles=obstacles,publicFishPathBlockers=PublicPathBlockers(new Vector3(-17,0,-1.7f),new Vector3(-1,0,-1.7f)),publicGreenPathBlockers=PublicPathBlockers(new Vector3(.55f,0,-31),new Vector3(.55f,0,-10))},true));EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");Debug.Log("UNIFIED_PAVING_DIAG "+rows.Count(r=>!r.supported)+"/"+rows.Count);
    }
}
