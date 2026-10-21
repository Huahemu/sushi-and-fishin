using System;
using UnityEngine;

namespace SushiAndFish.ResidentialIsland
{
    [Serializable]
    public sealed class HousingType
    {
        public string id, title, type, source, readiness;
        public int level;
        public GameObject prefab;
        public Vector2 footprint;
    }

    // This is the only installed asset entry point. The installer never alters a
    // host scene, existing prefab, controller, save schema, or project setting.
    public sealed class HousingCatalog : ScriptableObject
    {
        public HousingType[] homes;
        public GameObject nativeSeatPrefab;
        public Sprite panelSprite, buttonSprite, upSprite, downSprite, closeSprite;
        public TMPro.TMP_FontAsset font;
        public Shader floorPlanShader;
        public Material graphicMaterial;
        public Shader textShader;
        public AnimationClip restIdle,restSit,restLay,restTuck,restBrace,restRecline;
        public Color panelColor=new Color(1f,.92f,.67f);
    }

    [Serializable]
    public sealed class HousingPlacement
    {
        public string plot, type;
        public float yaw;
    }

    [Serializable]
    public sealed class HousingSave
    {
        public int version=1;
        public HousingPlacement[] homes=Array.Empty<HousingPlacement>();
    }

    public sealed class HousingPlot
    {
        public string id;
        public Rect rect;
        public Transform source;
        public Renderer[] boundary;
        float boundaryWorldWidth;
        public float BoundaryWorldWidth
        {
            get
            {
                if(boundaryWorldWidth>0)return boundaryWorldWidth;
                foreach(var renderer in boundary)
                {
                    var mesh=renderer?renderer.GetComponent<MeshFilter>()?.sharedMesh:null;
                    if(!mesh||!mesh.isReadable)continue;
                    var vertices=mesh.vertices;var indices=mesh.triangles;if(indices.Length<3)continue;
                    float shortest=float.MaxValue;
                    for(int i=0;i<3;i++)
                    {
                        var delta=renderer.transform.TransformPoint(vertices[indices[i]])-renderer.transform.TransformPoint(vertices[indices[(i+1)%3]]);delta.y=0;
                        if(delta.sqrMagnitude>.000001f)shortest=Mathf.Min(shortest,delta.magnitude);
                    }
                    if(shortest<float.MaxValue)return boundaryWorldWidth=shortest;
                }
                return boundaryWorldWidth=.38f;
            }
        }
        public HousingHouse house;
        public bool Occupied=>house;
        public Vector3 Center=>new Vector3(rect.center.x,source.position.y,rect.center.y);
    }

    public enum HousingRestKind { Bed, Toilet }

}
