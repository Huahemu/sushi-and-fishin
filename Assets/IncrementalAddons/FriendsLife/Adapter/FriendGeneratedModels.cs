using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bozo.ModularCharacters;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace SushiAndFish.FriendsLife
{
    public sealed class FriendGeneratedOutfitIdentity:MonoBehaviour {public string resourcePath;}
    [Serializable] public sealed class FriendGeneratedShape{public string name;public Vector3[] delta;}
    [Serializable] public sealed class FriendGeneratedWeight{public int a,b,c,d;public float wa,wb,wc,wd;public BoneWeight Unity()=>new BoneWeight{boneIndex0=a,boneIndex1=b,boneIndex2=c,boneIndex3=d,weight0=wa,weight1=wb,weight2=wc,weight3=wd};public static FriendGeneratedWeight From(BoneWeight w)=>new FriendGeneratedWeight{a=w.boneIndex0,b=w.boneIndex1,c=w.boneIndex2,d=w.boneIndex3,wa=w.weight0,wb=w.weight1,wc=w.weight2,wd=w.weight3};}
    [Serializable] public sealed class FriendGeneratedPart
    {public string path,sourceOutfit,rendererName,boneRoot;public string[] boneNames;public Vector3[] vertices,normals;public Vector2[] uv;public int[] triangles,bodyCoverage;public Matrix4x4[] bindposes;public FriendGeneratedWeight[] weights;public FriendGeneratedShape[] shapes;public string texturePng,regionMaskPng;public int[] sourceFaceIndices;public string[] suppressedInheritedTags;}
    [Serializable] public sealed class FriendGeneratedPackage
    {public int schema=1,revision=1,regionSchema,sourceTriangles,underBodyTriangles,inferredAnatomyTriangles,refinedRegionTexels,repairedHandComponents;public int retainedSourceTriangles,removedSourceTriangles;public int[] removedSourceFaceIndices,removedArtifactSourceFaceIndices,preservedClothSourceFaceIndices;public string sourceFaceIndexSpace;public string packageId,sourcePhoto,sourceMask,sourcePhotoPng,sourceMaskPng,createdUtc,regionMaskPng,conformMethod,poseMethod;public float[] poseRms;public Vector3[] surfaceJointOffsets;public CharacterData characterData;public FriendGeneratedPart[] parts;}
    public sealed class FriendGeneratedMesh
    {public Vector3[] vertices,normals;public Vector2[] uv;public int[] triangles;public byte[] texture;}

    public static class FriendGeneratedModels
    {
        public const int MaximumPackageBytes=128*1024*1024;
        static readonly Dictionary<string,Outfit> registered=new Dictionary<string,Outfit>(StringComparer.Ordinal);
        static GameObject cacheRoot;
        static string Root=>PhotoFriendPaths.Folder("GeneratedModels");
        public static bool TryLoadOutfit(string path,out Outfit outfit)
        {
            outfit=null;if(string.IsNullOrEmpty(path)||path.IndexOf("/FriendsGenerated/",StringComparison.Ordinal)<0)return false;
            if(registered.TryGetValue(path,out outfit)&&outfit)return true;var pieces=path.Split('/');if(pieces.Length!=4||pieces[1]!="FriendsGenerated"||!Guid.TryParseExact(pieces[2],"N",out _))return false;
            string file=Path.Combine(Root,pieces[2],"package.json");if(!File.Exists(file)){FriendPhotoPackageNetwork.RequestPackage(pieces[2]);return false;}Register(JsonUtility.FromJson<FriendGeneratedPackage>(File.ReadAllText(file)));return registered.TryGetValue(path,out outfit)&&outfit;
        }
        public static string ExportPackage(string id){if(!Guid.TryParseExact(id,"N",out _))throw new InvalidDataException("Invalid photo package ID");return File.ReadAllText(Path.Combine(Root,id,"package.json"));}
        public static void ImportPackage(string id,string json)
        {if(!Guid.TryParseExact(id,"N",out _))throw new InvalidDataException("Invalid photo package ID");string dir=Path.Combine(Root,id),file=Path.Combine(dir,"package.json");if(File.Exists(file)){Register(JsonUtility.FromJson<FriendGeneratedPackage>(File.ReadAllText(file)));return;}if(System.Text.Encoding.UTF8.GetByteCount(json)>MaximumPackageBytes)throw new InvalidDataException("照片资源包超出网络容量");var package=JsonUtility.FromJson<FriendGeneratedPackage>(json);if(package?.packageId!=id)throw new InvalidDataException("Photo package identity mismatch");Validate(package);Directory.CreateDirectory(dir);string temp=file+".writing";File.WriteAllText(temp,json);File.Move(temp,file);if(!string.IsNullOrEmpty(package.sourcePhotoPng))File.WriteAllBytes(Path.Combine(dir,"source.png"),Convert.FromBase64String(package.sourcePhotoPng));if(!string.IsNullOrEmpty(package.sourceMaskPng))File.WriteAllBytes(Path.Combine(dir,"mask.png"),Convert.FromBase64String(package.sourceMaskPng));Register(package);}
        public static IEnumerable<Outfit> RegisteredOutfits=>registered.Values.Where(o=>o);
        public static void Register(FriendGeneratedPackage package)
        {
            if(!cacheRoot){cacheRoot=new GameObject("Generated Friend Outfit Cache"){hideFlags=HideFlags.HideAndDontSave};cacheRoot.SetActive(false);if(Application.isPlaying)UnityEngine.Object.DontDestroyOnLoad(cacheRoot);}
            var textures=new Dictionary<string,Texture2D>(StringComparer.Ordinal);var regionTextures=new Dictionary<string,Texture2D>(StringComparer.Ordinal);var materials=new Dictionary<string,Material>();
            foreach(var group in package.parts.GroupBy(p=>p.path))
            {
                if(registered.TryGetValue(group.Key,out var cached)&&cached)continue;registered.Remove(group.Key);var first=group.First();var source=Resources.Load<Outfit>(first.sourceOutfit);if(!source)throw new InvalidOperationException("模型缺少原服饰部件："+first.sourceOutfit);
                // Inactive parent delays Awake until the actual BMAC instance is
                // created, so its renderer/shape caches see the generated meshes.
                var root=UnityEngine.Object.Instantiate(source.gameObject,cacheRoot.transform,false);root.name=group.Key.Substring(group.Key.IndexOf('/')+1);root.SetActive(true);
                var outfit=root.GetComponent<Outfit>();var renderers=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var bodySections=new List<FriendPhotoBodyCoverage.Section>();
                if(package.schema>=2)
                {
                    // Scan partitions already describe the complete surface.
                    // Stock garment masks refer to a different topology and
                    // would otherwise remove arbitrary scan triangles.
                    foreach(var mask in root.GetComponentsInChildren<OutfitHideByTag>(true))if(package.schema<5||source.Type.name!="Body")UnityEngine.Object.DestroyImmediate(mask);
                    outfit.HideTypes=Array.Empty<OutfitType>();if(package.schema<5)outfit.tags=Array.Empty<string>();
                    if(package.schema>=6&&first.suppressedInheritedTags!=null)outfit.tags=(outfit.tags??Array.Empty<string>()).Except(first.suppressedInheritedTags).ToArray();
                }
                foreach(var renderer in renderers)
                {
                    var part=group.FirstOrDefault(p=>p.rendererName==renderer.name);if(part==null){if(package.schema>=2)UnityEngine.Object.DestroyImmediate(renderer);else renderer.enabled=false;continue;}
                    if(package.schema>=2)renderer.gameObject.SetActive(true);
                    var mesh=new Mesh{name="PhotoGenerated_"+part.path+"_"+part.rendererName,indexFormat=IndexFormat.UInt32};mesh.vertices=part.vertices;mesh.triangles=part.triangles;mesh.uv=part.uv;mesh.normals=part.normals;mesh.bindposes=part.bindposes;mesh.boneWeights=part.weights.Select(w=>w.Unity()).ToArray();
                    foreach(var shape in part.shapes)mesh.AddBlendShapeFrame(shape.name,100,shape.delta,new Vector3[part.vertices.Length],new Vector3[part.vertices.Length]);mesh.RecalculateBounds();renderer.sharedMesh=mesh;
                    if(package.schema>=5&&source.Type.name=="Body"&&part.bodyCoverage!=null&&part.bodyCoverage.Length==part.triangles.Length/3)bodySections.Add(new FriendPhotoBodyCoverage.Section{renderer=renderer,triangles=part.triangles,coverage=part.bodyCoverage});
                    if(!string.IsNullOrEmpty(part.texturePng))
                    {
                        if(!textures.TryGetValue(part.texturePng,out var partTexture)){partTexture=new Texture2D(2,2,TextureFormat.RGBA32,false){hideFlags=HideFlags.DontSave};if(!partTexture.LoadImage(Convert.FromBase64String(part.texturePng)))throw new InvalidOperationException("生成模型材质无法读取");partTexture.wrapMode=TextureWrapMode.Clamp;textures[part.texturePng]=partTexture;}
                        string key=group.Key;if(!materials.TryGetValue(key,out var material)){var shader=Resources.Load<Shader>("FriendsLife/FriendPhotoSurface");if(!shader)throw new InvalidOperationException("照片角色材质尚未载入");material=new Material(shader){name=key,hideFlags=HideFlags.DontSave};material.SetOverrideTag("FriendsPhotoPart",key);material.mainTexture=partTexture;material.SetColor("_Color_1",Color.white);
                            string mask=string.IsNullOrEmpty(part.regionMaskPng)?package.regionMaskPng:part.regionMaskPng;if(package.regionSchema==1&&!string.IsNullOrEmpty(mask)){if(!regionTextures.TryGetValue(mask,out var regionTexture)){regionTexture=FriendPhotoRegions.LoadMask(Convert.FromBase64String(mask));regionTextures[mask]=regionTexture;}material.SetTexture("_RegionMask",regionTexture);material.SetFloat("_UseRegions",1);}
                            materials[key]=material;}
                        renderer.sharedMaterial=material;outfit.material=material;outfit.mergeTexture=false;outfit.customShader=true;
                    }
                }
                if(bodySections.Count>0){var coverage=root.AddComponent<FriendPhotoBodyCoverage>();coverage.packageId=package.packageId;coverage.sections=bodySections.ToArray();}
                outfit.mergeMesh=true;outfit.ColorChannels=new[]{"Base"};outfit.colors=Enumerable.Repeat(Color.white,9).ToArray();outfit.defaultColors=Enumerable.Repeat(Color.white,9).ToArray();outfit.PackName="FriendsGenerated";outfit.OutfitName=PartLabel(source.Type.name);root.AddComponent<FriendGeneratedOutfitIdentity>().resourcePath=group.Key;registered[group.Key]=outfit;
                var lod=root.GetComponent<LODGroup>();if(!lod)lod=root.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.05f,root.GetComponentsInChildren<SkinnedMeshRenderer>(true))});lod.RecalculateBounds();
            }
        }
        static string PartLabel(string type)
        {switch(type){case "Body":return "照片身体";case "Head":return "照片头部";case "HairFront":return "照片前发";case "HairBack":return "照片后发";case "Top":return "照片上装";case "Bottom":return "照片下装";case "Feet":return "照片鞋履";default:return "照片部件";}}
        sealed class Sample{public Vector3 p;public int vertex,part;}
        sealed class PointTree
        {
            sealed class Node{public int point,axis;public Node left,right;}
            readonly Vector3[] positions;readonly Node root;
            public PointTree(Vector3[] points){positions=points;var indices=Enumerable.Range(0,points.Length).ToArray();root=Build(indices,0,indices.Length,0);}
            Node Build(int[] indices,int start,int length,int depth)
            {if(length<=0)return null;int axis=depth%3;Array.Sort(indices,start,length,Comparer<int>.Create((a,b)=>positions[a][axis].CompareTo(positions[b][axis])));int middle=start+length/2;return new Node{point=indices[middle],axis=axis,left=Build(indices,start,middle-start,depth+1),right=Build(indices,middle+1,start+length-middle-1,depth+1)};}
            public int Nearest(Vector3 target){int best=0;float distance=float.MaxValue;Find(root,target,ref best,ref distance);return best;}
            void Find(Node node,Vector3 target,ref int best,ref float distance)
            {if(node==null)return;var p=positions[node.point];float d=(p-target).sqrMagnitude;if(d<distance){distance=d;best=node.point;}float delta=target[node.axis]-p[node.axis];Find(delta<0?node.left:node.right,target,ref best,ref distance);if(delta*delta<distance)Find(delta<0?node.right:node.left,target,ref best,ref distance);}
        }
        public static async Task<FriendGeneratedPackage> ConformAsync(string packageId,FriendGeneratedMesh generated,string photo,string mask,byte[] regions,FriendPhotoPoseData pose,CancellationToken cancellation)
        {
            generated=Simplify(generated,20000);var registration=await FriendPhotoPoseRegistration.Prepare(generated,regions,pose,cancellation);cancellation.ThrowIfCancellationRequested();
            return Conform(packageId,generated,photo,mask,registration.regionPng??regions,pose,registration,cancellation);
        }
        public static FriendGeneratedPackage Conform(string packageId,FriendGeneratedMesh generated,string photo,string mask,byte[] regions=null,FriendPhotoPoseData pose=null,FriendPhotoPoseRegistration.Prepared registration=null,CancellationToken cancellation=default(CancellationToken))
        {
            if(!Guid.TryParseExact(packageId,"N",out _))throw new InvalidDataException("Invalid photo package ID");
            string existing=Path.Combine(Root,packageId,"package.json");if(File.Exists(existing)){var complete=JsonUtility.FromJson<FriendGeneratedPackage>(File.ReadAllText(existing));Validate(complete);if((pose!=null&&complete.conformMethod!=FriendPhotoPoseRegistration.Version)||(regions!=null&&complete.schema<4))return Conform(Guid.NewGuid().ToString("N"),generated,photo,mask,regions,pose,registration,cancellation);if(regions!=null&&!Convert.FromBase64String(complete.regionMaskPng).SequenceEqual(regions)){if(pose!=null)return Conform(Guid.NewGuid().ToString("N"),generated,photo,mask,regions,pose,registration,cancellation);return FriendPhotoRegions.CreateVariant(packageId,regions,complete.characterData);}Register(complete);return complete;}
            if(generated==null||generated.vertices==null||generated.vertices.Length<100||generated.triangles.Length<300)throw new InvalidOperationException("生成网格不完整");
            bool texturedGeometry=generated.texture!=null&&generated.uv!=null&&generated.uv.Length==generated.vertices.Length&&generated.uv.Any(uv=>uv.sqrMagnitude>.00001f);
            generated=Simplify(generated,20000);
            var preset=Resources.Load<CharacterObject>("Default");if(!preset)throw new InvalidOperationException("缺少BoZo原始模板");var data=preset.GetCharacterData();data=JsonUtility.FromJson<CharacterData>(JsonUtility.ToJson(data));
            // Hair and outer clothing need independent editable geometry. Their
            // source parts provide topology/rig conventions, never visible stock
            // geometry in the generated result.
            foreach(string resource in new[]{"HairFront/BSMC_HairFront_Fohawk","HairBack/BSMC_HairBack_Scrappy","Top/BSMC_Top_Shirt","Bottom/BSMC_Bottom_Jeans","Feet/BSMC_Feet_ComfyShoes"})if(!data.outfitDatas.Any(o=>o.outfit.StartsWith(resource.Split('/')[0]+"/",StringComparison.Ordinal)))
            {
                var source=Resources.Load<Outfit>(resource);
                if(source)data.outfitDatas.Add(new OutfitData{outfit=resource,colors=Enumerable.Repeat(Color.white,9).ToList(),pattern="",patternColors=Enumerable.Repeat(Color.white,3).ToList(),color=Color.white});
            }
            var outfitSources=data.outfitDatas.Select(d=>Resources.Load<Outfit>(d.outfit)).ToArray();
            var sources=new List<Outfit>();var sourceRenderers=new List<SkinnedMeshRenderer>();var originalOutfitIndices=new List<int>();
            for(int i=0;i<outfitSources.Length;i++)if(outfitSources[i])foreach(var renderer in outfitSources[i].GetComponentsInChildren<SkinnedMeshRenderer>(true))if(renderer.sharedMesh){sources.Add(outfitSources[i]);sourceRenderers.Add(renderer);originalOutfitIndices.Add(i);}
            var samples=new List<Sample>();var bounds=new Bounds();bool first=true;
            for(int part=0;part<sources.Count;part++)
            {
                if(!sourceRenderers[part]||!sourceRenderers[part].sharedMesh)continue;var vertices=sourceRenderers[part].sharedMesh.vertices;
                for(int i=0;i<vertices.Length;i++){samples.Add(new Sample{p=vertices[i],vertex=i,part=part});if(first){bounds=new Bounds(vertices[i],Vector3.zero);first=false;}else bounds.Encapsulate(vertices[i]);}
            }
            if(samples.Count==0||bounds.size.y<=0)throw new InvalidOperationException("BoZo骨架模板无法读取");
            var generatedBounds=new Bounds(generated.vertices[0],Vector3.zero);foreach(var v in generated.vertices)generatedBounds.Encapsulate(v);if(generatedBounds.size.y<.0001f)throw new InvalidOperationException("生成模型没有有效高度");
            // Match source bind-space; each actual generated vertex retains its
            // geometry while weights and all original deformers are transferred.
            float scale=bounds.size.y/generatedBounds.size.y;var aligned=generated.vertices.Select(v=>(v-generatedBounds.center)*scale+bounds.center).ToArray();
            Vector4 textureRect=new Vector4(0,0,1,1);
            if(generated.texture==null)
            {
                // The working public shape endpoint emits geometry only. Use the
                // user's confirmed cutout as a real front projection, preserving
                // its pixels; no stock face or fixed avatar is substituted.
                string cutout=Path.Combine(Path.GetDirectoryName(photo)??"","cutout.png");generated.texture=OpaquePhoto(File.ReadAllBytes(File.Exists(cutout)?cutout:photo),out textureRect);
            }
            var sourceTree=new PointTree(samples.Select(s=>s.p).ToArray());var nearest=new Sample[aligned.Length];
            byte[] semantic=null;Vector2 skinUv=Vector2.zero;
            if(regions!=null)
            {
                var labels=FriendPhotoRegions.LoadMask(regions);
                try
                {
                    var pixels=labels.GetPixels32();int width=labels.width,height=labels.height;semantic=new byte[generated.vertices.Length];
                    for(int i=0;i<semantic.Length;i++){var uv=generated.uv[i];int x=Mathf.Clamp(Mathf.RoundToInt(uv.x*(width-1)),0,width-1),y=Mathf.Clamp(Mathf.RoundToInt(uv.y*(height-1)),0,height-1);semantic[i]=pixels[y*width+x].r;}
                    skinUv=SkinAnchor(pixels,width,height,generated.texture);
                }
                finally{if(Application.isPlaying)UnityEngine.Object.Destroy(labels);else UnityEngine.Object.DestroyImmediate(labels);}
            }
            if(pose!=null&&registration==null)throw new InvalidOperationException("Pose registration must be prepared through the cancellable local pipeline");
            if(registration!=null){if(registration.vertices.Length!=aligned.Length)throw new InvalidDataException("Photo pose geometry no longer matches the current surface");aligned=registration.vertices;generated.normals=registration.normals;}
            var generatedTree=new PointTree(aligned);
            var semanticSamples=new Dictionary<byte,Sample[]>();var semanticTrees=new Dictionary<byte,PointTree>();
            if(semantic!=null)for(byte label=1;label<=6;label++)
            {
                byte captured=label;var set=samples.Where(s=>SemanticType(captured,sources[s.part].Type.name)).ToArray();
                if(set.Length>0){semanticSamples[label]=set;semanticTrees[label]=new PointTree(set.Select(s=>s.p).ToArray());}
            }
            for(int i=0;i<aligned.Length;i++)nearest[i]=semantic!=null&&semanticTrees.TryGetValue(semantic[i],out var tree)?semanticSamples[semantic[i]][tree.Nearest(aligned[i])]:samples[sourceTree.Nearest(aligned[i])];
            var trianglesByPart=new Dictionary<int,List<int>>();var facesByPart=new Dictionary<int,List<int>>();var artifactRemoved=new List<int>();var preservedCloth=new List<int>();
            int bottomSource=sources.FindIndex(source=>source.Type.name=="Bottom"),footSource=sources.FindIndex(source=>source.Type.name=="Feet");float footHeight=registration==null?float.NegativeInfinity:registration.centers.Where((p,index)=>registration.bones[index]=="foot_l"||registration.bones[index]=="foot_r").Select(p=>p.y).DefaultIfEmpty(float.NegativeInfinity).Max();
            for(int i=0;i<generated.triangles.Length;i+=3)
            {
                int a=generated.triangles[i],b=generated.triangles[i+1],c=generated.triangles[i+2];int part=nearest[a].part;if(nearest[b].part==nearest[c].part)part=nearest[b].part;
                // A face may cross a UV-region boundary even when its corner
                // votes select skin. Preserve an observed lower-body garment
                // face when its actual atlas centre and anatomy both say pants.
                if(registration?.completeBody==true&&bottomSource>=0&&sources[part].Type.name=="Body"&&registration.RegionAt((generated.uv[a]+generated.uv[b]+generated.uv[c])/3)==3)
                {
                    float lowerBody=0;for(int bone=0;bone<registration.bones.Length;bone++)if(registration.bones[bone]=="pelvis"||new[]{"thigh","calf","foot","ball"}.Contains(registration.bones[bone].Split('_')[0]))lowerBody+=(registration.weights[a,bone]+registration.weights[b,bone]+registration.weights[c,bone])/3;
                    if(lowerBody>.6f){part=bottomSource;preservedCloth.Add(i/3);}
                }
                if(registration?.completeBody==true&&sources[part].Type.name=="Top"&&registration.artifactFaces!=null&&registration.artifactFaces[i/3]){artifactRemoved.Add(i/3);continue;}
                // Low observed shoe faces retain their original geometry/UVs,
                // even when a scan partition originally called them trousers.
                if(registration?.completeBody==true&&footSource>=0&&(aligned[a].y+aligned[b].y+aligned[c].y)/3<footHeight+bounds.size.y*.05f)
                {
                    float influence=0;for(int bone=0;bone<registration.bones.Length;bone++)if(registration.bones[bone].StartsWith("foot_",StringComparison.Ordinal)||registration.bones[bone].StartsWith("ball_",StringComparison.Ordinal))influence+=(registration.weights[a,bone]+registration.weights[b,bone]+registration.weights[c,bone])/3;
                    if(influence>.65f)part=footSource;
                }
                if(!trianglesByPart.TryGetValue(part,out var list))trianglesByPart[part]=list=new List<int>();if(!facesByPart.TryGetValue(part,out var faces))facesByPart[part]=faces=new List<int>();list.Add(a);list.Add(b);list.Add(c);faces.Add(i/3);
            }
            var parts=new List<FriendGeneratedPart>();var newOutfits=new List<OutfitData>();
            // Full core sections remain complete when hair/clothes are removed.
            // Their original topology is registered to actual generated geometry;
            // every other part keeps the newly generated triangles.
            for(int i=0;i<sources.Count;i++)if(!trianglesByPart.ContainsKey(i))trianglesByPart[i]=new List<int>();
            var projectionMin=(generatedBounds.min-generatedBounds.center)*scale+bounds.center;var projectionSize=generatedBounds.size*scale;
            foreach(var item in trianglesByPart.OrderBy(p=>p.Key))
            {
                cancellation.ThrowIfCancellationRequested();
                bool underBody=semantic!=null&&sources[item.Key].Type.name=="Body";
                if(texturedGeometry&&item.Value.Count<3&&!underBody)continue;
                int sourceIndex=item.Key;var renderer=sourceRenderers[sourceIndex];var sourceMesh=renderer.sharedMesh;int[] oldIndices=item.Value.Distinct().ToArray();var remap=new Dictionary<int,int>();for(int i=0;i<oldIndices.Length;i++)remap[oldIndices[i]]=i;
                var sourceVertices=sourceMesh.vertices;var sourceNormals=sourceMesh.normals;var sourceBindposes=sourceMesh.bindposes;var boneCenters=sourceBindposes.Select(p=>p.inverse.MultiplyPoint3x4(Vector3.zero)).ToArray();var weights=sourceMesh.boneWeights;if(weights.Length!=sourceVertices.Length||sourceBindposes.Length==0)throw new InvalidOperationException("BoZo源部件骨骼权重缺失");
                // A textured reconstruction keeps its actual generated triangles
                // and UVs. The vendor mesh contributes rig/deformer conventions.
                // Shape-only legacy packages retain their explicit registration path.
                bool core=!texturedGeometry;
                Vector3[] coreVertices=null;
                if(core)
                {
                    coreVertices=new Vector3[sourceVertices.Length];
                    for(int i=0;i<sourceVertices.Length;i++)
                    {
                        var point=sourceVertices[i];int target=generatedTree.Nearest(point);var delta=aligned[target]-point;var normal=sourceNormals[i].normalized;
                        var weight=weights[i];Vector3 center=boneCenters[weight.boneIndex0]*weight.weight0+boneCenters[weight.boneIndex1]*weight.weight1+boneCenters[weight.boneIndex2]*weight.weight2+boneCenters[weight.boneIndex3]*weight.weight3;
                        float radius=Mathf.Max(.015f,(point-center).magnitude);float maximum=Mathf.Min(bounds.size.y*.08f,radius*.55f);
                        float distance=Mathf.Clamp(Vector3.Dot(delta,normal),-Mathf.Min(maximum,radius*.35f),maximum);coreVertices[i]=point+normal*distance*.85f;
                    }
                    // Nearest surface samples can alternate at adjacent source
                    // vertices. Smooth only the registration displacement; the
                    // original topology, bind poses and morph deltas stay exact.
                    var displacement=coreVertices.Select((v,index)=>v-sourceVertices[index]).ToArray();var topology=sourceMesh.triangles;
                    for(int pass=0;pass<4;pass++)
                    {
                        var sum=new Vector3[displacement.Length];var count=new int[displacement.Length];
                        for(int edge=0;edge<topology.Length;edge+=3)for(int side=0;side<3;side++){int a=topology[edge+side],b=topology[edge+(side+1)%3];sum[a]+=displacement[b];count[a]++;sum[b]+=displacement[a];count[b]++;}
                        for(int vertex=0;vertex<displacement.Length;vertex++)if(count[vertex]>0)displacement[vertex]=Vector3.Lerp(displacement[vertex],sum[vertex]/count[vertex],.75f);
                    }
                    var seam=Enumerable.Range(0,sourceVertices.Length).GroupBy(index=>Vector3Int.RoundToInt(sourceVertices[index]*100000));
                    foreach(var group in seam){Vector3 delta=Vector3.zero;int count=0;foreach(int vertex in group){delta+=displacement[vertex];count++;}delta/=count;foreach(int vertex in group)coreVertices[vertex]=sourceVertices[vertex]+delta;}
                }
                var shapes=new List<FriendGeneratedShape>();
                for(int si=0;si<sourceMesh.blendShapeCount;si++)
                {
                    var delta=new Vector3[sourceVertices.Length];sourceMesh.GetBlendShapeFrameVertices(si,sourceMesh.GetBlendShapeFrameCount(si)-1,delta,null,null);
                    var transferred=core?delta:new Vector3[oldIndices.Length];if(!core&&registration!=null){var field=registration.Shape(data.outfitDatas[originalOutfitIndices[sourceIndex]].outfit,renderer.name,sourceMesh.GetBlendShapeName(si));for(int i=0;i<oldIndices.Length;i++)transferred[i]=field[oldIndices[i]];}else if(!core)for(int i=0;i<oldIndices.Length;i++){int original=oldIndices[i];var sample=nearest[original];if(sample.part==sourceIndex)transferred[i]=delta[sample.vertex];else transferred[i]=delta[Closest(sourceVertices,aligned[original])];}
                    shapes.Add(new FriendGeneratedShape{name=sourceMesh.GetBlendShapeName(si),delta=transferred});
                }
                var transferredWeights=core?weights.Select(FriendGeneratedWeight.From).ToArray():new FriendGeneratedWeight[oldIndices.Length];if(!core&&registration!=null){var field=registration.Weights(data.outfitDatas[originalOutfitIndices[sourceIndex]].outfit,renderer.name);for(int i=0;i<oldIndices.Length;i++)transferredWeights[i]=FriendGeneratedWeight.From(field[oldIndices[i]]);}else if(!core)for(int i=0;i<oldIndices.Length;i++){var sample=nearest[oldIndices[i]];int v=sample.part==sourceIndex?sample.vertex:Closest(sourceVertices,aligned[oldIndices[i]]);transferredWeights[i]=FriendGeneratedWeight.From(weights[v]);}
                int originalOutfitIndex=originalOutfitIndices[sourceIndex];string path=sources[sourceIndex].Type.name+"/FriendsGenerated/"+packageId+"/"+sources[sourceIndex].Type.name;var part=new FriendGeneratedPart{path=path,sourceOutfit=data.outfitDatas[originalOutfitIndex].outfit,rendererName=renderer.name,
                    vertices=core?coreVertices:oldIndices.Select(i=>aligned[i]).ToArray(),normals=core?sourceNormals:oldIndices.Select(i=>generated.normals[i]).ToArray(),uv=core?coreVertices.Select(v=>new Vector2(textureRect.x+(1-(v.x-projectionMin.x)/Mathf.Max(.0001f,projectionSize.x))*textureRect.z,textureRect.y+(v.y-projectionMin.y)/projectionSize.y*textureRect.w)).ToArray():oldIndices.Select(i=>generated.uv[i]).ToArray(),triangles=core?sourceMesh.triangles:item.Value.Select(i=>remap[i]).ToArray(),
                    weights=transferredWeights,bindposes=sourceBindposes,boneNames=renderer.bones.Select(b=>b?b.name:"").ToArray(),boneRoot=renderer.rootBone?renderer.rootBone.name:"",shapes=shapes.ToArray(),texturePng=generated.texture==null?null:Convert.ToBase64String(generated.texture)};
                part.sourceFaceIndices=facesByPart.TryGetValue(sourceIndex,out var sourceFaces)?sourceFaces.ToArray():Array.Empty<int>();
                if(registration?.completeBody==true&&sources[sourceIndex].Type.name=="Head")part.regionMaskPng=registration.headRegionPng;
                if(registration?.completeBody==true&&sources[sourceIndex].Type.name=="Feet"){part.regionMaskPng=registration.footRegionPng;if(part.vertices.Length>0&&part.vertices.Max(v=>v.y)<footHeight+bounds.size.y*.05f)part.suppressedInheritedTags=new[]{"CoverAnkles"};}
                if(underBody){if(registration!=null)registration.AppendBody(part,skinUv);else AppendUnderBody(part,sourceMesh,generatedTree,aligned,semantic,skinUv,bounds.size.y);}
                if(part.triangles.Length<3)continue;
                parts.Add(part);if(!newOutfits.Any(o=>o.outfit==path)){var outfit=data.outfitDatas[originalOutfitIndex];outfit.outfit=path;outfit.colors=Enumerable.Repeat(Color.white,9).ToList();outfit.color=Color.white;if(texturedGeometry)outfit.partVisibility=null;newOutfits.Add(outfit);}
            }
            if(!parts.Any(p=>p.path.EndsWith("/Head"))||!parts.Any(p=>p.path.EndsWith("/Body")))throw new InvalidOperationException("生成模型缺少可独立编辑的头部或身体，请使用完整正面人物照片");
            // Symbolic vendor outfits (iris/pupil and underwear masks) generate
            // their visuals during Attach and must remain in CharacterData for
            // the original eye/colour/wardrobe controls to keep working.
            for(int i=0;i<outfitSources.Length;i++)if(outfitSources[i]&&outfitSources[i].GetComponentsInChildren<SkinnedMeshRenderer>(true).Length==0)newOutfits.Add(data.outfitDatas[i]);
            data.outfitDatas=newOutfits;var package=new FriendGeneratedPackage{schema=texturedGeometry?(registration?.completeBody==true?6:registration!=null?5:semantic!=null?4:2):1,packageId=packageId,sourcePhoto="source.png",sourceMask="mask.png",sourcePhotoPng=Convert.ToBase64String(File.ReadAllBytes(photo)),sourceMaskPng=Convert.ToBase64String(File.ReadAllBytes(mask)),createdUtc=DateTime.UtcNow.ToString("O"),characterData=data,parts=parts.ToArray(),sourceTriangles=generated.triangles.Length/3,underBodyTriangles=parts.Sum(p=>p.triangles.Length)/3-generated.triangles.Length/3,inferredAnatomyTriangles=registration?.inferredAnatomyTriangles??0,refinedRegionTexels=registration?.refinedRegionTexels??0,repairedHandComponents=registration?.repairedHandComponents??0,poseMethod=pose?.method,poseRms=registration?.poseRms,surfaceJointOffsets=registration?.surfaceJointOffsets,conformMethod=registration!=null?FriendPhotoPoseRegistration.Version:semantic!=null?"semantic-parts-native-underbody-v1":null};
            if(registration?.completeBody==true)
            {
                var retained=parts.SelectMany(p=>p.sourceFaceIndices??Array.Empty<int>()).Where(i=>i>=0).ToArray();if(retained.Distinct().Count()!=retained.Length)throw new InvalidOperationException("Observed photo triangles were duplicated across parts");var retainedSet=new HashSet<int>(retained);package.removedSourceFaceIndices=Enumerable.Range(0,package.sourceTriangles).Where(i=>!retainedSet.Contains(i)).ToArray();package.retainedSourceTriangles=retained.Length;package.removedSourceTriangles=package.removedSourceFaceIndices.Length;package.sourceFaceIndexSpace="ReadGlbWorkingAfterSimplify";package.removedArtifactSourceFaceIndices=artifactRemoved.ToArray();package.preservedClothSourceFaceIndices=preservedCloth.ToArray();package.underBodyTriangles=package.inferredAnatomyTriangles=parts.Sum(p=>p.sourceFaceIndices.Count(i=>i<0));
            }
            if(regions!=null){FriendPhotoRegions.ValidateMask(regions);package.regionSchema=1;package.regionMaskPng=Convert.ToBase64String(regions);}
            Validate(package);string serialized=JsonUtility.ToJson(package);if(System.Text.Encoding.UTF8.GetByteCount(serialized)>MaximumPackageBytes)throw new InvalidOperationException("照片模型资源过大，请使用较小的照片");string directory=Path.Combine(Root,packageId);Directory.CreateDirectory(directory);File.Copy(photo,Path.Combine(directory,"source.png"),true);File.Copy(mask,Path.Combine(directory,"mask.png"),true);string packageFile=Path.Combine(directory,"package.json"),writing=packageFile+".writing";File.WriteAllText(writing,serialized);File.Move(writing,packageFile);Register(package);return package;
        }
        static bool SemanticType(byte label,string type)
        {switch(label){case 1:return type=="HairFront"||type=="HairBack";case 2:return type=="Top";case 3:return type=="Bottom";case 4:return type=="Body";case 5:return type=="Feet";case 6:return type=="Head";default:return false;}}
        static Vector2 SkinAnchor(Color32[] labels,int width,int height,byte[] texture)
        {
            var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!image.LoadImage(texture))throw new InvalidOperationException("照片肤色材质无法读取");var colors=image.GetPixels32();byte kind=labels.Any(p=>p.r==4)?(byte)4:(byte)6;
                var hist=new int[3,256];int count=0;
                Func<int,Color32> sample=i=>colors[Mathf.Clamp((int)((i/width+.5f)/height*image.height),0,image.height-1)*image.width+Mathf.Clamp((int)((i%width+.5f)/width*image.width),0,image.width-1)];
                for(int i=0;i<labels.Length;i++)if(labels[i].r==kind){var color=sample(i);hist[0,color.r]++;hist[1,color.g]++;hist[2,color.b]++;count++;}
                if(count==0)throw new InvalidOperationException("照片没有可用的肤色表面，请使用清晰人物照片");
                var median=new int[3];for(int channel=0;channel<3;channel++){int sum=0;for(int v=0;v<256;v++){sum+=hist[channel,v];if(sum>=count/2){median[channel]=v;break;}}}
                int best=-1,distance=int.MaxValue;for(int i=0;i<labels.Length;i++)if(labels[i].r==kind){var color=sample(i);int d=Math.Abs(color.r-median[0])+Math.Abs(color.g-median[1])+Math.Abs(color.b-median[2]);if(d<distance){best=i;distance=d;}}
                return new Vector2((best%width+.5f)/width,(best/width+.5f)/height);
            }
            finally{if(Application.isPlaying)UnityEngine.Object.Destroy(image);else UnityEngine.Object.DestroyImmediate(image);}
        }
        static void AppendUnderBody(FriendGeneratedPart part,Mesh source,PointTree generatedTree,Vector3[] generated,byte[] labels,Vector2 skinUv,float height)
        {
            // The photo surface has no unseen torso/legs under its clothes.
            // Supply only those covered sections from the native editable body,
            // shrink them inside the measured 3D garment, and retain its rig and
            // body deformers. Visible skin and all generated surfaces stay real.
            var vertices=source.vertices;var normals=source.normals;var weights=source.boneWeights;var selected=new List<int>();var topology=source.triangles;
            for(int i=0;i<topology.Length;i+=3)
            {
                var center=(vertices[topology[i]]+vertices[topology[i+1]]+vertices[topology[i+2]])/3;
                byte label=labels[generatedTree.Nearest(center)];if(label!=2&&label!=3&&label!=5)continue;
                selected.Add(topology[i]);selected.Add(topology[i+1]);selected.Add(topology[i+2]);
            }
            if(selected.Count==0)return;int offset=part.vertices.Length;var ids=selected.Distinct().ToArray();var map=new Dictionary<int,int>();for(int i=0;i<ids.Length;i++)map[ids[i]]=offset+i;
            var fitted=new Vector3[ids.Length];
            for(int i=0;i<ids.Length;i++)
            {
                int id=ids[i];var point=vertices[id];var normal=normals[id].normalized;var target=generated[generatedTree.Nearest(point)];
                float inward=Mathf.Clamp(Vector3.Dot(target-point,normal)-height*.004f,-height*.06f,0);fitted[i]=point+normal*inward;
            }
            part.vertices=part.vertices.Concat(fitted).ToArray();part.normals=part.normals.Concat(ids.Select(i=>normals[i])).ToArray();part.uv=part.uv.Concat(Enumerable.Repeat(skinUv,ids.Length)).ToArray();part.weights=part.weights.Concat(ids.Select(i=>FriendGeneratedWeight.From(weights[i]))).ToArray();part.triangles=part.triangles.Concat(selected.Select(i=>map[i])).ToArray();
            foreach(var shape in part.shapes)
            {int index=source.GetBlendShapeIndex(shape.name);var delta=new Vector3[vertices.Length];source.GetBlendShapeFrameVertices(index,source.GetBlendShapeFrameCount(index)-1,delta,null,null);shape.delta=shape.delta.Concat(ids.Select(i=>delta[i])).ToArray();}
        }
        static int Closest(Vector3[] vertices,Vector3 target){int best=0;float distance=float.MaxValue;for(int i=0;i<vertices.Length;i++){float d=(vertices[i]-target).sqrMagnitude;if(d<distance){distance=d;best=i;}}return best;}
        static byte[] OpaquePhoto(byte[] png,out Vector4 rectangle)
        {
            var original=new Texture2D(2,2,TextureFormat.RGBA32,false);if(!original.LoadImage(png))throw new InvalidOperationException("人物贴图无法读取");var source=original.GetPixels32();float scale=Mathf.Min(1,1024f/Mathf.Max(original.width,original.height));int width=Mathf.Max(1,(int)(original.width*scale)),height=Mathf.Max(1,(int)(original.height*scale));var colors=new Color32[width*height];var queue=new Queue<int>();int minX=width,minY=height,maxX=0,maxY=0;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++){int index=y*width+x;colors[index]=source[Mathf.Min(original.height-1,(int)(y/scale))*original.width+Mathf.Min(original.width-1,(int)(x/scale))];if(colors[index].a>16){colors[index].a=255;queue.Enqueue(index);minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}else colors[index].a=0;}
            if(queue.Count==0){UnityEngine.Object.Destroy(original);throw new InvalidOperationException("人物轮廓为空");}rectangle=new Vector4(minX/(float)width,minY/(float)height,(maxX-minX+1)/(float)width,(maxY-minY+1)/(float)height);
            while(queue.Count>0){int index=queue.Dequeue(),x=index%width,y=index/width;foreach(int next in new[]{x>0?index-1:-1,x<width-1?index+1:-1,y>0?index-width:-1,y<height-1?index+width:-1})if(next>=0&&colors[next].a==0){colors[next]=colors[index];queue.Enqueue(next);}}
            var result=new Texture2D(width,height,TextureFormat.RGBA32,false);result.SetPixels32(colors);result.Apply();byte[] bytes=result.EncodeToPNG();UnityEngine.Object.Destroy(original);UnityEngine.Object.Destroy(result);return bytes;
        }
        public static FriendGeneratedMesh Simplify(FriendGeneratedMesh mesh,int maximum)
        {
            if(mesh.vertices.Length<=maximum)return mesh;var bounds=new Bounds(mesh.vertices[0],Vector3.zero);foreach(var v in mesh.vertices)bounds.Encapsulate(v);
            float cell=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z))/160;Dictionary<Vector3Int,int> bins=null;int[] map=null;List<Vector3> vertices=null,normals=null;List<Vector2> uv=null;List<int> counts=null;
            do
            {
                bins=new Dictionary<Vector3Int,int>();map=new int[mesh.vertices.Length];vertices=new List<Vector3>();normals=new List<Vector3>();uv=new List<Vector2>();counts=new List<int>();
                for(int i=0;i<mesh.vertices.Length;i++){var key=Vector3Int.FloorToInt((mesh.vertices[i]-bounds.min)/cell);if(!bins.TryGetValue(key,out var target)){target=vertices.Count;bins[key]=target;vertices.Add(Vector3.zero);normals.Add(Vector3.zero);uv.Add(Vector2.zero);counts.Add(0);}map[i]=target;vertices[target]+=mesh.vertices[i];normals[target]+=mesh.normals[i];uv[target]+=mesh.uv[i];counts[target]++;}
                cell*=1.2f;
            }while(vertices.Count>maximum);
            for(int i=0;i<vertices.Count;i++){vertices[i]/=counts[i];normals[i]=normals[i].normalized;uv[i]/=counts[i];}
            var triangles=new List<int>();for(int i=0;i<mesh.triangles.Length;i+=3){int a=map[mesh.triangles[i]],b=map[mesh.triangles[i+1]],c=map[mesh.triangles[i+2]];if(a==b||b==c||a==c)continue;triangles.Add(a);triangles.Add(b);triangles.Add(c);}
            return new FriendGeneratedMesh{vertices=vertices.ToArray(),normals=normals.ToArray(),uv=uv.ToArray(),triangles=triangles.ToArray(),texture=mesh.texture};
        }
        public static void Validate(FriendGeneratedPackage package)
        {
            if(package?.characterData==null||package.parts==null||package.parts.Length<2)throw new InvalidOperationException("模型包缺少可编辑角色资料");
            if(package.regionSchema!=0&&package.regionSchema!=1)throw new InvalidOperationException("照片颜色分区版本无法读取");
            if(package.schema>=3&&package.regionSchema!=1)throw new InvalidOperationException("照片模型包缺少颜色分区");
            if(package.regionSchema==1)FriendPhotoRegions.ValidateMask(Convert.FromBase64String(package.regionMaskPng??""));
            if(package.parts.Length>64||package.parts.Sum(p=>(long)(p.vertices?.Length??0))>500000)throw new InvalidOperationException("模型包超出运行容量");
            if(package.schema>=6)foreach(var group in package.parts.GroupBy(p=>p.path))if(group.Select(p=>p.texturePng).Distinct().Count()>1||group.Select(p=>p.regionMaskPng).Distinct().Count()>1)throw new InvalidOperationException("One generated outfit group cannot declare conflicting surface textures");
            if(package.sourceFaceIndexSpace=="ReadGlbWorkingAfterSimplify")
            {
                if(package.parts.Any(p=>p.sourceFaceIndices==null||p.sourceFaceIndices.Length!=p.triangles.Length/3))throw new InvalidOperationException("Photo source-face accounting is incomplete");var source=package.parts.SelectMany(p=>p.sourceFaceIndices).Where(i=>i>=0).ToArray();var removed=package.removedSourceFaceIndices??Array.Empty<int>();int inferred=package.parts.Sum(p=>p.sourceFaceIndices.Count(i=>i<0));
                if(source.Any(i=>i>=package.sourceTriangles)||removed.Any(i=>i<0||i>=package.sourceTriangles)||source.Distinct().Count()!=source.Length||removed.Distinct().Count()!=removed.Length||source.Intersect(removed).Any()||source.Length!=package.retainedSourceTriangles||removed.Length!=package.removedSourceTriangles||source.Length+removed.Length!=package.sourceTriangles||inferred!=package.inferredAnatomyTriangles||inferred!=package.underBodyTriangles||package.parts.Sum(p=>p.triangles.Length)/3!=source.Length+inferred)throw new InvalidOperationException("Photo observed/removed/inferred triangle accounting failed");
            }
            foreach(var p in package.parts)
            {
                if(p.vertices==null||p.vertices.Length<3||p.vertices.Length>200000||p.weights==null||p.normals==null||p.uv==null||p.bindposes==null||p.boneNames==null||p.shapes==null||p.triangles==null||p.shapes.Length>256||p.triangles.Length>3000000)throw new InvalidOperationException("模型包网格数据不完整");
                if(p.vertices.Length!=p.weights.Length||p.normals.Length!=p.vertices.Length||p.uv.Length!=p.vertices.Length||p.bindposes.Length==0||p.boneNames.Length!=p.bindposes.Length)throw new InvalidOperationException("模型包骨架或材质数据不完整");
                var source=Resources.Load<Outfit>(p.sourceOutfit);var renderer=source?source.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r=>r.name==p.rendererName):null;
                if(!renderer||!renderer.sharedMesh||!renderer.bones.Select(b=>b?b.name:"").SequenceEqual(p.boneNames))throw new InvalidOperationException("模型骨架与原部件不兼容");
                if(p.path!=source.Type.name+"/FriendsGenerated/"+package.packageId+"/"+source.Type.name)throw new InvalidOperationException("模型资源路径无效");
                foreach(var w in p.weights)if(w==null||!Finite(w.wa)||!Finite(w.wb)||!Finite(w.wc)||!Finite(w.wd)||Mathf.Min(w.wa,w.wb,w.wc,w.wd)<0||Mathf.Abs(w.wa+w.wb+w.wc+w.wd-1)>.02f||Mathf.Min(w.a,w.b,w.c,w.d)<0||Mathf.Max(w.a,w.b,w.c,w.d)>=p.bindposes.Length)throw new InvalidOperationException("模型骨骼权重无效");
                foreach(var s in p.shapes)if(s.delta.Length!=p.vertices.Length)throw new InvalidOperationException("模型形态键不完整");
                for(int i=0;i<renderer.sharedMesh.blendShapeCount;i++)if(!p.shapes.Any(s=>s.name==renderer.sharedMesh.GetBlendShapeName(i)))throw new InvalidOperationException("模型缺少原形态键");
                if(p.vertices.Concat(p.normals).Concat(p.shapes.SelectMany(s=>s.delta)).Any(v=>!Finite(v.x)||!Finite(v.y)||!Finite(v.z)))throw new InvalidOperationException("模型包含无效坐标");
                if(p.triangles.Any(i=>i<0||i>=p.vertices.Length))throw new InvalidOperationException("模型面索引无效");
                if(!string.IsNullOrEmpty(p.regionMaskPng))FriendPhotoRegions.ValidateMask(Convert.FromBase64String(p.regionMaskPng));
                if(package.schema>=6&&p.sourceFaceIndices!=null&&p.sourceFaceIndices.Length>0&&p.sourceFaceIndices.Length!=p.triangles.Length/3)throw new InvalidOperationException("照片来源面索引资料无效");
                if(p.bodyCoverage!=null&&p.bodyCoverage.Length>0&&(p.bodyCoverage.Length!=p.triangles.Length/3||p.bodyCoverage.Any(v=>v<0||v>3)))throw new InvalidOperationException("照片补体覆盖资料无效");
            }
        }
        static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        public static FriendGeneratedMesh ReadGlb(byte[] bytes,bool faceNegativeZ=false)
        {
            using(var stream=new MemoryStream(bytes))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadUInt32()!=0x46546c67||reader.ReadUInt32()!=2)throw new InvalidOperationException("生成模型不是GLB2");reader.ReadUInt32();JObject json=null;byte[] binary=null;
                while(stream.Position<stream.Length){int length=reader.ReadInt32();uint type=reader.ReadUInt32();byte[] chunk=reader.ReadBytes(length);if(type==0x4e4f534a)json=JObject.Parse(System.Text.Encoding.UTF8.GetString(chunk));else if(type==0x004e4942)binary=chunk;}
                if(json==null||binary==null)throw new InvalidOperationException("GLB模型数据缺失");var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();byte[] texture=null;
                foreach(var mesh in json["meshes"]??new JArray())foreach(var primitive in mesh["primitives"]??new JArray())
                {
                    if(primitive["extensions"]?["KHR_draco_mesh_compression"]!=null)throw new InvalidOperationException("模型使用了尚未启用的Draco压缩");
                    int start=vertices.Count;var pos=ReadAccessor(json,binary,(int)primitive["attributes"]["POSITION"]);var normal=primitive["attributes"]?["NORMAL"]==null?null:ReadAccessor(json,binary,(int)primitive["attributes"]["NORMAL"]);var tex=primitive["attributes"]?["TEXCOORD_0"]==null?null:ReadAccessor(json,binary,(int)primitive["attributes"]["TEXCOORD_0"]);
                    // SF3D's conditioning camera observes the model from -Z.
                    // Its explicit adapter turn faces the native character's +Z;
                    // other providers retain the existing glTF handedness conversion.
                    for(int i=0;i<pos.Length;i+=3){vertices.Add(new Vector3(faceNegativeZ?pos[i]:-pos[i],pos[i+1],faceNegativeZ?-pos[i+2]:pos[i+2]));normals.Add(normal==null?Vector3.up:new Vector3(faceNegativeZ?normal[i]:-normal[i],normal[i+1],faceNegativeZ?-normal[i+2]:normal[i+2]));int n=i/3;uv.Add(tex==null?Vector2.zero:new Vector2(tex[n*2],faceNegativeZ?1-tex[n*2+1]:tex[n*2+1]));}
                    var indices=ReadAccessor(json,binary,(int)primitive["indices"]);for(int i=0;i<indices.Length;i+=3){triangles.Add(start+(int)indices[i]);triangles.Add(start+(int)indices[i+2]);triangles.Add(start+(int)indices[i+1]);}
                    int? material=(int?)primitive["material"];int? texIndex=material==null?null:(int?)json["materials"]?[material.Value]?["pbrMetallicRoughness"]?["baseColorTexture"]?["index"];int? imageIndex=texIndex==null?null:(int?)json["textures"]?[texIndex.Value]?["source"];
                    if(imageIndex!=null&&texture==null){var image=json["images"]?[imageIndex.Value];if(image?["bufferView"]!=null){var view=json["bufferViews"][(int)image["bufferView"]];int offset=(int?)view["byteOffset"]??0;texture=new byte[(int)view["byteLength"]];Array.Copy(binary,offset,texture,0,texture.Length);}}
                }
                if(vertices.Count==0)throw new InvalidOperationException("生成模型没有几何体");return new FriendGeneratedMesh{vertices=vertices.ToArray(),normals=normals.ToArray(),uv=uv.ToArray(),triangles=triangles.ToArray(),texture=texture};
            }
        }
        static float[] ReadAccessor(JObject json,byte[] binary,int index)
        {
            var accessor=json["accessors"][index];if(accessor["sparse"]!=null)throw new InvalidOperationException("暂不支持稀疏GLB数据");var view=json["bufferViews"][(int)accessor["bufferView"]];int type=(int)accessor["componentType"],components=(string)accessor["type"]=="VEC3"?3:(string)accessor["type"]=="VEC2"?2:1,count=(int)accessor["count"];
            int size=type==5126||type==5125?4:type==5123?2:1;int stride=(int?)view["byteStride"]??size*components;int offset=((int?)view["byteOffset"]??0)+((int?)accessor["byteOffset"]??0);var result=new float[count*components];
            for(int i=0;i<count;i++)for(int c=0;c<components;c++){int p=offset+i*stride+c*size;result[i*components+c]=type==5126?BitConverter.ToSingle(binary,p):type==5125?BitConverter.ToUInt32(binary,p):type==5123?BitConverter.ToUInt16(binary,p):binary[p];}return result;
        }
    }
}
