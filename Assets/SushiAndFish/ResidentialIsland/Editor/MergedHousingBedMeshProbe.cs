using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Independent audit: compare baked avatar vertices with actual bed MeshCollider
// ray hits, rather than the runtime support-envelope calculation.
public sealed class MergedHousingBedMeshProbe : IDisposable
{
    sealed class Skin{public SkinnedMeshRenderer source;public readonly Mesh mesh=new Mesh();public readonly List<Vector3> vertices=new List<Vector3>();}
    readonly Skin[] skins;
    readonly Collider[] colliders;
    public MergedHousingBedMeshProbe(GameObject avatar,SushiAndFish.ResidentialIsland.HousingRestInteractable rest)
    {
        skins=avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.sharedMesh).Select(r=>new Skin{source=r}).ToArray();
        colliders=rest.bedSurfaces.Select(r=>r.GetComponent<Collider>()).Where(c=>c&&!c.isTrigger).ToArray();
    }
    public MergedHousingAudit.BedContactRow Measure(string home,SushiAndFish.ResidentialIsland.HousingRestSession session)
    {
        var row=new MergedHousingAudit.BedContactRow{home=home,phase=session.Phase,elapsed=session.Elapsed,minimumClearance=999,supportLift=session.BedSupportLift};
        foreach(var skin in skins)
        {
            skin.source.BakeMesh(skin.mesh);skin.mesh.GetVertices(skin.vertices);
            var matrix=skin.source.transform.localToWorldMatrix;
            for(int i=0;i<skin.vertices.Count;i+=3)
            {
                var point=matrix.MultiplyPoint3x4(skin.vertices[i]);
                foreach(var collider in colliders)
                {
                    var bounds=collider.bounds;
                    if(point.x<bounds.min.x||point.x>bounds.max.x||point.z<bounds.min.z||point.z>bounds.max.z)continue;
                    if(!collider.Raycast(new Ray(new Vector3(point.x,bounds.max.y+.5f,point.z),Vector3.down),out var hit,2))continue;
                    float gap=point.y-hit.point.y;if(gap<row.minimumClearance)
                    {
                        row.minimumClearance=gap;row.point=point;var weights=skin.source.sharedMesh.boneWeights;
                        if(i<weights.Length){var w=weights[i];int index=w.boneIndex0;float maximum=w.weight0;if(w.weight1>maximum){index=w.boneIndex1;maximum=w.weight1;}if(w.weight2>maximum){index=w.boneIndex2;maximum=w.weight2;}if(w.weight3>maximum)index=w.boneIndex3;row.region=skin.source.bones[index].name;}
                    }row.checkedPoints++;
                    if(gap<-.005f)row.penetratingPoints++;
                }
            }
        }
        return row;
    }
    public void Dispose(){foreach(var skin in skins)UnityEngine.Object.Destroy(skin.mesh);}
}
