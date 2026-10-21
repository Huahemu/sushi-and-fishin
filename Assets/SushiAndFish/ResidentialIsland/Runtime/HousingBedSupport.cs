using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SushiAndFish.ResidentialIsland
{
    // Contact uses the visible skinned surface, including the customized outfit;
    // the hip bone alone cannot establish clearance from a duvet or pillow.
    public sealed class HousingBedSupport : IDisposable
    {
        sealed class Skin
        {
            public SkinnedMeshRenderer source;
            public readonly Mesh baked=new Mesh();
            public readonly List<Vector3> vertices=new List<Vector3>();
        }
        readonly Transform frame;
        readonly Bounds[] surfaces;
        readonly Skin[] skins;
        public HousingBedSupport(HousingRestInteractable rest,GameObject avatar)
        {
            frame=rest.transform;
            surfaces=(rest.bedSurfaces??Array.Empty<Renderer>()).Where(r=>r).Select(r=>
            {
                var mesh=r.GetComponent<MeshFilter>().sharedMesh;var local=mesh.bounds;
                var matrix=frame.worldToLocalMatrix*r.transform.localToWorldMatrix;
                var bounds=new Bounds(matrix.MultiplyPoint3x4(local.center),Vector3.zero);
                for(int mask=0;mask<8;mask++)bounds.Encapsulate(matrix.MultiplyPoint3x4(local.center+Vector3.Scale(local.extents,new Vector3((mask&1)==0?-1:1,(mask&2)==0?-1:1,(mask&4)==0?-1:1))));
                return bounds;
            }).ToArray();
            skins=avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.sharedMesh).Select(r=>new Skin{source=r}).ToArray();
        }
        public float RequiredLift()
        {
            float lift=0;
            foreach(var skin in skins)
            {
                if(!skin.source||!skin.source.enabled)continue;
                skin.source.BakeMesh(skin.baked);skin.baked.GetVertices(skin.vertices);
                var matrix=frame.worldToLocalMatrix*skin.source.transform.localToWorldMatrix;
                foreach(var vertex in skin.vertices)
                {
                    var p=matrix.MultiplyPoint3x4(vertex);
                    foreach(var surface in surfaces)
                        if(p.x>=surface.min.x&&p.x<=surface.max.x&&p.z>=surface.min.z&&p.z<=surface.max.z)
                            lift=Mathf.Max(lift,surface.max.y+.015f-p.y);
                }
            }
            return lift;
        }
        public void Dispose(){foreach(var skin in skins)if(skin.baked)UnityEngine.Object.Destroy(skin.baked);}
    }
}
