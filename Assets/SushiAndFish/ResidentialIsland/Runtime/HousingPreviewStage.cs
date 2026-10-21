using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SushiAndFish.ResidentialIsland
{
    public sealed class HousingPreviewStage : IDisposable
    {
        static int next;
        readonly GameObject root;
        readonly Camera camera;
        readonly List<Material> ownedMaterials=new List<Material>();
        readonly List<Mesh> ownedMeshes=new List<Mesh>();
        public RenderTexture Texture {get;}
        public int MeshCount {get;private set;}
        public Vector2 Size {get;private set;}
        public Vector3 Center {get;private set;}
        public Rect VisibleViewportRect {get;private set;}
        public Vector2 WorldRectSize=>new Vector2(camera.orthographicSize*2*camera.aspect,camera.orthographicSize*2);
        static Bounds MeshBounds(MeshRenderer renderer)
        {
            // Inactive casino floors retain stale Renderer world bounds after
            // their parent map is moved/scaled. Prefab assets also have no live
            // renderer bounds. Derive both from the mesh and current transform.
            var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var local=mesh.bounds;var matrix=renderer.transform.localToWorldMatrix;
            var bounds=new Bounds(matrix.MultiplyPoint3x4(local.center),Vector3.zero);
            for(int mask=0;mask<8;mask++)bounds.Encapsulate(matrix.MultiplyPoint3x4(local.center+Vector3.Scale(local.extents,new Vector3((mask&1)==0?-1:1,(mask&2)==0?-1:1,(mask&4)==0?-1:1))));
            return bounds;
        }
        public HousingPreviewStage(int width=768,int height=512)
        {
            root=new GameObject("HousingPreviewStage"){hideFlags=HideFlags.DontSave};
            root.transform.position=new Vector3(2000+(next++%30)*150,2500,2000);
            Texture=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){name="HousingFloorPlan",antiAliasing=1};Texture.Create();
            camera=new GameObject("FloorPlanCamera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);
            camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
            camera.nearClipPlane=.05f;camera.farClipPlane=120;camera.targetTexture=Texture;camera.aspect=(float)width/height;
            camera.transform.rotation=Quaternion.Euler(90,0,0);camera.depth=-85;
            var data=camera.GetUniversalAdditionalCameraData();data.SetRenderer(0);data.renderPostProcessing=false;data.renderShadows=false;data.volumeLayerMask=0;
        }
        public void SetBuilding(IEnumerable<Transform> sources,bool transparentRoof=false,bool normalizeVisible=false)
        {
            foreach(Transform child in root.transform)if(child!=camera.transform){child.gameObject.SetActive(false);UnityEngine.Object.Destroy(child.gameObject);}
            foreach(var m in ownedMaterials)if(m)UnityEngine.Object.Destroy(m);ownedMaterials.Clear();MeshCount=0;
            var renderers=sources.Where(t=>t).SelectMany(t=>t.GetComponentsInChildren<MeshRenderer>(true)).Distinct().Where(r=>{var mesh=r.GetComponent<MeshFilter>();return mesh&&mesh.sharedMesh;}).ToArray();
            if(renderers.Length==0)return;
            var bounds=MeshBounds(renderers[0]);foreach(var r in renderers)bounds.Encapsulate(MeshBounds(r));
            Center=bounds.center;Size=new Vector2(bounds.size.x,bounds.size.z);
            float floor=renderers.Where(r=>Regex.IsMatch(r.name,@"Floor|Foundation|Ground",RegexOptions.IgnoreCase)).Select(r=>MeshBounds(r).max.y).DefaultIfEmpty(bounds.min.y+.25f).Min();
            // Native casino floor 3 contains walkable terraces at +1.64m and
            // +3.64m. A cut measured only from the lowest floor removes them.
            float highestFloor=renderers.Where(r=>Regex.IsMatch(r.name,@"Floor|Foundation|Ground",RegexOptions.IgnoreCase)&&!Regex.IsMatch(r.name,@"Roof|Ceiling",RegexOptions.IgnoreCase))
                .Select(MeshBounds).Where(b=>b.size.y<.6f&&b.size.x*b.size.z>3).Select(b=>b.max.y).DefaultIfEmpty(floor).Max();
            float cutHeight=highestFloor-floor+1.45f;
            var shader=HousingIsland.Instance?.Catalog.floorPlanShader;
            if(!shader)shader=Shader.Find("Housing/FloorPlan");
            var materials=new Dictionary<(Material,bool),Material>();
            foreach(var source in renderers)
            {
                bool roof=Regex.IsMatch(source.name,@"Roof|Ceiling|UpperFloor|UpperStorey|RV_.*(Gable|Ridge)",RegexOptions.IgnoreCase);
                if(roof&&!transparentRoof)continue;
                var go=new GameObject(source.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root.transform,false);
                go.transform.position=root.transform.position+source.transform.position-new Vector3(bounds.center.x,floor,bounds.center.z);
                go.transform.rotation=source.transform.rotation;go.transform.localScale=source.transform.lossyScale;
                go.GetComponent<MeshFilter>().sharedMesh=source.GetComponent<MeshFilter>().sharedMesh;
                var mr=go.GetComponent<MeshRenderer>();mr.shadowCastingMode=ShadowCastingMode.Off;mr.receiveShadows=false;
                mr.sharedMaterials=source.sharedMaterials.Select(original=>
                {
                    var key=(original,roof);if(materials.TryGetValue(key,out var existing))return existing;
                    var mat=new Material(shader);
                    Texture tex=null;Color color=Color.white;
                    if(original)
                    {
                        if(original.HasProperty("_BaseMap"))tex=original.GetTexture("_BaseMap");else if(original.HasProperty("_MainTex"))tex=original.GetTexture("_MainTex");
                        if(original.HasProperty("_BaseColor"))color=original.GetColor("_BaseColor");else if(original.HasProperty("_Color"))color=original.GetColor("_Color");
                    }
                    color.a=roof?.24f:1f;mat.SetColor("_BaseColor",color);if(tex)mat.SetTexture("_BaseMap",tex);
                    // Opaque casino materials also use texture alpha for
                    // decoration. Treating it as opacity punches false holes
                    // into the floor. Preserve alpha only when authored as such.
                    bool alphaClip=original&&((original.HasProperty("_AlphaClip")&&original.GetFloat("_AlphaClip")>.5f)||original.IsKeywordEnabled("_ALPHATEST_ON"));
                    bool alphaBlend=original&&((original.HasProperty("_Surface")&&original.GetFloat("_Surface")>.5f)||(original.HasProperty("_Mode")&&original.GetFloat("_Mode")>=2));
                    mat.SetFloat("_TextureAlpha",alphaBlend?1:0);mat.SetFloat("_AlphaClip",alphaClip?1:0);
                    if(alphaClip&&original.HasProperty("_Cutoff"))mat.SetFloat("_Cutoff",original.GetFloat("_Cutoff"));
                    mat.SetFloat("_CutHeight",root.transform.position.y+(roof?50:cutHeight));
                    mat.renderQueue=roof?3100:3000;materials[key]=mat;ownedMaterials.Add(mat);return mat;
                }).ToArray();MeshCount++;
            }
            camera.orthographicSize=Mathf.Max(Size.y/2,Size.x/(2*camera.aspect))*1.06f;
            camera.transform.position=root.transform.position+Vector3.up*55;
            if(normalizeVisible)FitVisibleContent();
        }
        Rect ReadVisibleRect()
        {
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=Texture;
            var image=new Texture2D(Texture.width,Texture.height,TextureFormat.RGBA32,false);
            try
            {
                image.ReadPixels(new Rect(0,0,Texture.width,Texture.height),0,0);image.Apply();
                var pixels=image.GetPixels32();int left=Texture.width,right=-1,bottom=Texture.height,top=-1;
                for(int y=0;y<Texture.height;y++)for(int x=0;x<Texture.width;x++)
                    if(pixels[y*Texture.width+x].a>24){left=Mathf.Min(left,x);right=Mathf.Max(right,x);bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);}
                return right<left?Rect.zero:new Rect((float)left/Texture.width,(float)bottom/Texture.height,(float)(right-left+1)/Texture.width,(float)(top-bottom+1)/Texture.height);
            }
            finally{RenderTexture.active=previous;UnityEngine.Object.Destroy(image);}
        }
        void FitVisibleContent()
        {
            // Frame the pixels left by ceiling/material clipping, rather than
            // invisible roof or off-floor geometry in the source bounds.
            var visible=ReadVisibleRect();if(visible.width<=0||visible.height<=0){VisibleViewportRect=visible;return;}
            var offset=visible.center-new Vector2(.5f,.5f);
            camera.transform.position+=camera.transform.right*(offset.x*WorldRectSize.x)+camera.transform.up*(offset.y*WorldRectSize.y);
            camera.orthographicSize*=Mathf.Max(visible.width,visible.height)/.94f;
            VisibleViewportRect=ReadVisibleRect();
        }
        public void SetPortrait(GameObject avatar)
        {
            foreach(Transform child in root.transform)if(child!=camera.transform)UnityEngine.Object.Destroy(child.gameObject);
            foreach(var mesh in ownedMeshes)if(mesh)UnityEngine.Object.Destroy(mesh);ownedMeshes.Clear();
            var skins=avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.enabled&&r.sharedMesh).ToArray();
            var animator=avatar.GetComponent<Animator>();var head=animator&&animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Head):null;
            var center=head?head.position+Vector3.up*.08f:avatar.transform.position+Vector3.up*1.75f;
            foreach(var source in skins)
            {
                var mesh=new Mesh();source.BakeMesh(mesh);ownedMeshes.Add(mesh);
                var go=new GameObject(source.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root.transform,false);
                go.transform.SetPositionAndRotation(root.transform.position+source.transform.position-center,source.transform.rotation);
                go.transform.localScale=source.transform.lossyScale;go.GetComponent<MeshFilter>().sharedMesh=mesh;
                go.GetComponent<MeshRenderer>().sharedMaterials=source.sharedMaterials;
            }
            camera.transform.position=root.transform.position+avatar.transform.forward*1.5f+Vector3.up*.04f;
            camera.transform.rotation=Quaternion.LookRotation(root.transform.position-camera.transform.position,Vector3.up);
            camera.orthographicSize=.26f;camera.backgroundColor=new Color(.96f,.87f,.61f,1);
            var light=new GameObject("PortraitKey").AddComponent<Light>();light.transform.SetParent(root.transform,false);light.type=LightType.Point;light.range=4;light.intensity=3;light.transform.localPosition=new Vector3(-.5f,.6f,1);
        }
        public void Dispose()
        {
            foreach(var m in ownedMaterials)if(m)UnityEngine.Object.Destroy(m);
            foreach(var mesh in ownedMeshes)if(mesh)UnityEngine.Object.Destroy(mesh);
            if(root){root.SetActive(false);UnityEngine.Object.Destroy(root);}if(Texture){Texture.Release();UnityEngine.Object.Destroy(Texture);}
        }
    }
}
