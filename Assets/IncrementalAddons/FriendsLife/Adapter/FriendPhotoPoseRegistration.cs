using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bozo.ModularCharacters;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // All geometry/weight computation uses captured arrays on a single worker.
    // Resource and Texture access is restricted to Capture on the main thread.
    public static class FriendPhotoPoseRegistration
    {
        public const string Version="pose-canonical-complete-photo-body-v4b";
        internal sealed class Template
        {
            public string key,type;public Vector2[] uv;public Vector3[] vertices,normals;public int[] triangles;public string[] bones;public Matrix4x4[] bindposes;public BoneWeight[] weights;public FriendGeneratedShape[] shapes;
        }
        sealed class Field
        {public Vector3[] points;public float[,] weights;public Dictionary<string,Vector3[]> shapes;public Tree tree;}
        sealed class Neighbors
        {public int[,] ids;public float[,] blend;}
        internal sealed class Supplement
        {public Vector3[] vertices,normals;public Vector2[] uv;public int[] triangles,coverage;public float[,] weights;public Dictionary<string,Vector3[]> shapes;public int inferred;}
        internal sealed class Attachment
        {public bool[] protectedVertices;public bool[] broken=new bool[2];public Vector3[] start=new Vector3[2],end=new Vector3[2];public int[][] startIds=new int[2][],endIds=new int[2][];}
        public sealed class Prepared
        {
            public int regionWidth,regionHeight;public bool[] artifactFaces;public bool completeBody;public byte[] bodyAtlasRgb;public string bodyTexturePng,bodyRegionPng,headRegionPng,footRegionPng;public Vector3[] vertices,normals,surfaceJointOffsets;public int inferredAnatomyTriangles,refinedRegionTexels,repairedHandComponents;public float[] poseRms;public byte[] regionPng,regionRgb;
            internal Dictionary<string,Template> templates;internal string[] bones;internal Vector3[] centers;internal float[,] weights;internal Dictionary<string,Vector3[]> bodyShapes,headShapes,sharedShapes;
            internal Dictionary<string,Supplement> supplements;
            internal bool[] brokenArms;
            internal Attachment attachment;
            public byte RegionAt(Vector2 uv)
            {if(regionRgb==null||regionWidth<=0||regionHeight<=0)return 0;int x=Mathf.Clamp(Mathf.RoundToInt(uv.x*(regionWidth-1)),0,regionWidth-1),y=Mathf.Clamp(Mathf.RoundToInt(uv.y*(regionHeight-1)),0,regionHeight-1);return regionRgb[(y*regionWidth+x)*3];}
            public BoneWeight[] Weights(string source,string renderer)
            {
                var template=templates[Key(source,renderer)];var result=new BoneWeight[vertices.Length];for(int i=0;i<result.Length;i++)result[i]=Map(weights,i,template,bones,centers);return result;
            }
            public Vector3[] Shape(string source,string renderer,string name)
            {
                var template=templates[Key(source,renderer)];var field=template.type=="Head"||template.type.StartsWith("Hair",StringComparison.Ordinal)?headShapes:bodyShapes;string key=Suffix(name);
                if(sharedShapes.TryGetValue(key,out var shared))return shared;return field.TryGetValue(key,out var value)?value:new Vector3[vertices.Length];
            }
            public void AppendBody(FriendGeneratedPart part,Vector2 skinUv)
            {
                string key=Key(part.sourceOutfit,part.rendererName);if(!supplements.TryGetValue(key,out var add)||add.triangles.Length==0)return;
                if(completeBody)
                {
                    var native=templates[key];part.vertices=add.vertices;part.normals=add.normals;part.uv=add.uv;part.triangles=add.triangles;part.bodyCoverage=add.coverage;part.sourceFaceIndices=Enumerable.Repeat(-1,part.triangles.Length/3).ToArray();part.texturePng=bodyTexturePng;part.regionMaskPng=bodyRegionPng;
                    part.weights=Enumerable.Range(0,add.vertices.Length).Select(i=>FriendGeneratedWeight.From(Map(add.weights,i,native,bones,centers))).ToArray();
                    foreach(var shape in part.shapes)shape.delta=add.shapes.TryGetValue(Suffix(shape.name),out var value)?value:new Vector3[add.vertices.Length];return;
                }
                int offset=part.vertices.Length;part.bodyCoverage=new int[part.triangles.Length/3].Concat(add.coverage).ToArray();part.vertices=part.vertices.Concat(add.vertices).ToArray();part.normals=part.normals.Concat(add.normals).ToArray();part.uv=part.uv.Concat(Enumerable.Repeat(skinUv,add.vertices.Length)).ToArray();
                var template=templates[key];var mapped=new FriendGeneratedWeight[add.vertices.Length];for(int i=0;i<mapped.Length;i++)mapped[i]=FriendGeneratedWeight.From(Map(add.weights,i,template,bones,centers));part.weights=part.weights.Concat(mapped).ToArray();part.triangles=part.triangles.Concat(add.triangles.Select(i=>i+offset)).ToArray();
                foreach(var shape in part.shapes){add.shapes.TryGetValue(Suffix(shape.name),out var value);shape.delta=shape.delta.Concat(value??new Vector3[add.vertices.Length]).ToArray();}
            }
        }
        static string Key(string source,string renderer)=>source+"\n"+renderer;
        static string Suffix(string name){int dot=name.LastIndexOf('.');return dot<0?name:name.Substring(dot+1);}
        static Template[] Capture()
        {
            var preset=Resources.Load<CharacterObject>("Default");if(!preset)throw new InvalidOperationException("缺少BoZo姿势模板");
            var paths=preset.GetCharacterData().outfitDatas.Select(o=>o.outfit).ToList();
            foreach(string resource in new[]{"HairFront/BSMC_HairFront_Fohawk","HairBack/BSMC_HairBack_Scrappy","Top/BSMC_Top_Shirt","Bottom/BSMC_Bottom_Jeans","Feet/BSMC_Feet_ComfyShoes"})if(!paths.Any(p=>p.StartsWith(resource.Split('/')[0]+"/",StringComparison.Ordinal)))paths.Add(resource);
            var output=new List<Template>();
            foreach(string path in paths)
            {
                var outfit=Resources.Load<Outfit>(path);if(!outfit)continue;
                foreach(var renderer in outfit.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mesh=renderer.sharedMesh;if(!mesh)continue;var shapes=new FriendGeneratedShape[mesh.blendShapeCount];
                    for(int si=0;si<shapes.Length;si++){var delta=new Vector3[mesh.vertexCount];mesh.GetBlendShapeFrameVertices(si,mesh.GetBlendShapeFrameCount(si)-1,delta,null,null);shapes[si]=new FriendGeneratedShape{name=mesh.GetBlendShapeName(si),delta=delta};}
                    output.Add(new Template{key=Key(path,renderer.name),type=outfit.Type.name,uv=mesh.uv,vertices=mesh.vertices,normals=mesh.normals,triangles=mesh.triangles,bones=renderer.bones.Select(b=>b?b.name:"").ToArray(),bindposes=mesh.bindposes,weights=mesh.boneWeights,shapes=shapes});
                }
            }
            return output.ToArray();
        }
        public static async Task<Prepared> Prepare(FriendGeneratedMesh surface,byte[] regions,FriendPhotoPoseData pose,CancellationToken cancellation)
        {
            if(pose?.method!=FriendPhotoPose.Version||pose.joints?.Length!=33)throw new InvalidDataException("照片姿势资料与当前模型不匹配");
            var templates=Capture();byte[] labels=Labels(surface,regions,out var atlas,out int width,out int height);var copy=new FriendGeneratedMesh{vertices=(Vector3[])surface.vertices.Clone(),normals=(Vector3[])surface.normals.Clone(),uv=surface.uv,triangles=surface.triangles,texture=surface.texture};
            Color32[] diffuse=null;int diffuseWidth=0,diffuseHeight=0;
            if(surface.texture!=null){var source=new Texture2D(2,2,TextureFormat.RGBA32,false);try{if(!source.LoadImage(surface.texture))throw new InvalidDataException("Photo diffuse cannot be decoded");diffuse=source.GetPixels32();diffuseWidth=source.width;diffuseHeight=source.height;}finally{if(Application.isPlaying)UnityEngine.Object.Destroy(source);else UnityEngine.Object.DestroyImmediate(source);}}
            var result=await Task.Run(()=>{var prepared=Compute(copy,labels,pose,templates,cancellation);Refine(copy.uv,copy.triangles,labels,atlas,width,height,prepared,cancellation);if(diffuse!=null){prepared.artifactFaces=ArtifactFaces(copy,prepared,diffuse,diffuseWidth,diffuseHeight,width,height,cancellation);CompleteBody(copy,prepared,diffuse,diffuseWidth,diffuseHeight,width,height,cancellation);}return prepared;},cancellation);cancellation.ThrowIfCancellationRequested();
            result.regionWidth=width;result.regionHeight=height;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false,true);
            try{image.LoadRawTextureData(result.regionRgb);image.Apply(false,false);result.regionPng=image.EncodeToPNG();}
            finally{if(Application.isPlaying)UnityEngine.Object.Destroy(image);else UnityEngine.Object.DestroyImmediate(image);}
            if(result.completeBody){var skin=new Texture2D(1024,1024,TextureFormat.RGB24,false);try{skin.LoadRawTextureData(result.bodyAtlasRgb);skin.Apply(false,false);result.bodyTexturePng=Convert.ToBase64String(skin.EncodeToPNG());}finally{if(Application.isPlaying)UnityEngine.Object.Destroy(skin);else UnityEngine.Object.DestroyImmediate(skin);}result.bodyRegionPng=ConstantRegion(4);result.headRegionPng=ConstantRegion(6);result.footRegionPng=ConstantRegion(5);result.bodyAtlasRgb=null;}
            return result;
        }
        static byte[] Labels(FriendGeneratedMesh surface,byte[] regions,out byte[] atlas,out int width,out int height)
        {
            var image=FriendPhotoRegions.LoadMask(regions);
            try{var pixels=image.GetPixels32();width=image.width;height=image.height;atlas=pixels.Select(p=>p.r).ToArray();var result=new byte[surface.vertices.Length];for(int i=0;i<result.Length;i++){var uv=surface.uv[i];int x=Mathf.Clamp(Mathf.RoundToInt(uv.x*(image.width-1)),0,image.width-1),y=Mathf.Clamp(Mathf.RoundToInt(uv.y*(image.height-1)),0,image.height-1);result[i]=pixels[y*image.width+x].r;}return result;}
            finally{if(Application.isPlaying)UnityEngine.Object.Destroy(image);else UnityEngine.Object.DestroyImmediate(image);}
        }
        static Prepared Compute(FriendGeneratedMesh surface,byte[] labels,FriendPhotoPoseData pose,Template[] templates,CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();var all=templates.SelectMany(t=>t.vertices).ToArray();Bounds native=BoundsOf(all),original=BoundsOf(surface.vertices);float scale=native.size.y/original.size.y;
            var points=surface.vertices.Select(v=>(v-original.center)*scale+native.center).ToArray();var joints=pose.joints.Select(v=>(v-original.center)*scale+native.center).ToArray();var body=templates.Where(t=>t.type=="Body").ToArray();var head=templates.Where(t=>t.type=="Head").ToArray();
            if(body.Length==0||head.Length==0)throw new InvalidDataException("照片姿势缺少身体或头部模板");var boneNames=body[0].bones;var boneIndex=boneNames.Select((n,i)=>new{n,i}).ToDictionary(x=>x.n,x=>x.i);var centers=body[0].bindposes.Select(m=>Inverse(m).MultiplyPoint3x4(Vector3.zero)).ToArray();
            var q=boneNames.Select((n,i)=>new{n,p=centers[i]}).ToDictionary(x=>x.n,x=>x.p);var source=new Dictionary<string,Vector3>();
            for(int side=0;side<2;side++){string suffix=side==0?"_l":"_r";int[] ids=side==0?new[]{12,14,16,24,26,28,32}:new[]{11,13,15,23,25,27,31};string[] names={"upperarm","lowerarm","hand","thigh","calf","foot","ball"};for(int i=0;i<ids.Length;i++)source[names[i]+suffix]=joints[ids[i]];}
            // Four-view image landmarks are a prior. At elbows/wrists with
            // actual observed skin, bounded surface-normal fitting locates the
            // internal joint rather than leaving it above the visible limb.
            var jointOffsets=new Vector3[33];var skinIds=Enumerable.Range(0,labels.Length).Where(i=>labels[i]==4).ToArray();var nativeBody=body.SelectMany(t=>t.vertices).ToArray();
            foreach(var entry in new[]{new{key="lowerarm_l",id=14},new{key="hand_l",id=16},new{key="lowerarm_r",id=13},new{key="hand_r",id=15}})
            {
                var guess=source[entry.key];var nearest=skinIds.OrderBy(i=>(points[i]-guess).sqrMagnitude).Take(12).ToArray();if(nearest.Length<6||(points[nearest[0]]-guess).magnitude>native.size.y*.04f)continue;
                var radii=nativeBody.Select(p=>(p-q[entry.key]).magnitude).OrderBy(v=>v).Take(12).ToArray();float radius=Mathf.Clamp(radii[radii.Length/2],native.size.y*.006f,native.size.y*.03f);Vector3 center=Vector3.zero;float total=0;
                foreach(int i in nearest){float weight=1/Math.Max((points[i]-guess).sqrMagnitude,.003f*.003f);center+=(points[i]-surface.normals[i].normalized*radius)*weight;total+=weight;}center/=total;Vector3 shift=center-guess;float limit=native.size.y*.035f;if(shift.magnitude>limit)shift=shift.normalized*limit;source[entry.key]=guess+shift*.8f;jointOffsets[entry.id]=shift*.8f;
            }
            source["pelvis"]=(source["thigh_l"]+source["thigh_r"])*.5f+q["pelvis"]-(q["thigh_l"]+q["thigh_r"])*.5f;
            source["spine_05"]=(source["upperarm_l"]+source["upperarm_r"])*.5f+q["spine_05"]-(q["upperarm_l"]+q["upperarm_r"])*.5f;
            var torso=Affine(q["pelvis"],q["spine_05"],source["pelvis"],source["spine_05"]);var transform=boneNames.ToDictionary(n=>n,n=>torso);
            foreach(string side in new[]{"l","r"})
            {
                string tail="_"+side;var major=new Dictionary<string,Matrix4x4>();
                foreach(var segment in new[]{new[]{"upperarm","lowerarm"},new[]{"lowerarm","hand"},new[]{"thigh","calf"},new[]{"calf","foot"},new[]{"foot","ball"}})major[segment[0]]=Affine(q[segment[0]+tail],q[segment[1]+tail],source[segment[0]+tail],source[segment[1]+tail]);
                foreach(string name in boneNames)
                {
                    if(!name.EndsWith(tail,StringComparison.Ordinal)&&!name.EndsWith(tail+"1",StringComparison.Ordinal))continue;string kind=name.Split('_')[0];
                    if(major.TryGetValue(kind,out var matrix))transform[name]=matrix;
                    else if(new[]{"hand","middle","index","ring","pinky","thumb"}.Contains(kind))transform[name]=major["lowerarm"];
                    else if(kind=="ball")transform[name]=major["foot"];else if(kind=="shoulder")transform[name]=major["upperarm"];
                }
            }
            var headObserved=points.Where((p,i)=>labels[i]==1||labels[i]==6).ToArray();if(headObserved.Length<10)throw new InvalidDataException("照片姿势没有可用头部");var hb=BoundsOf(headObserved);var tb=BoundsOf(head.SelectMany(t=>t.vertices).ToArray());float hs=Mathf.Clamp(hb.size.y/tb.size.y,.5f,1.5f);var headTransform=Matrix4x4.identity;headTransform[0,0]=headTransform[1,1]=headTransform[2,2]=hs;SetTranslation(ref headTransform,hb.center-tb.center*hs);
            foreach(string name in boneNames)if(name=="head"||name.StartsWith("neck",StringComparison.Ordinal))transform[name]=headTransform;
            var matrices=boneNames.Select(n=>transform[n]).ToArray();var bodyField=MakeField(body,boneNames,boneIndex);var headField=MakeField(head,boneNames,boneIndex);
            var posedBody=Skin(bodyField.points,bodyField.weights,matrices);var posedHead=Skin(headField.points,headField.weights,matrices);var bt=new Tree(posedBody);var ht=new Tree(posedHead);var weights=new float[points.Length,boneNames.Length];
            var armFields=new Field[2];var armTrees=new Tree[2];var armStrength=new float[points.Length];var armSeed=new float[points.Length,boneNames.Length];
            for(int side=0;side<2;side++)
            {
                string tail=side==0?"_l":"_r";var included=new List<int>();for(int b=0;b<boneNames.Length;b++)if(boneNames[b].EndsWith(tail,StringComparison.Ordinal)&&new[]{"upperarm","lowerarm","hand","thumb","middle","index","ring","pinky"}.Contains(boneNames[b].Split('_')[0]))included.Add(b);
                var ids=Enumerable.Range(0,bodyField.points.Length).Where(i=>included.Sum(b=>bodyField.weights[i,b])>.5f).ToArray();var values=new float[ids.Length,boneNames.Length];for(int i=0;i<ids.Length;i++)for(int b=0;b<boneNames.Length;b++)values[i,b]=bodyField.weights[ids[i],b];armFields[side]=new Field{points=ids.Select(i=>bodyField.points[i]).ToArray(),weights=values};armTrees[side]=new Tree(ids.Select(i=>posedBody[i]).ToArray());
            }
            for(int i=0;i<points.Length;i++)
            {
                if((i&127)==0)cancel.ThrowIfCancellationRequested();bool isHead=labels[i]==1||labels[i]==6;var field=isHead?headField:bodyField;SampleWeights(isHead?ht:bt,field.weights,points[i],weights,i);if(isHead)continue;
                float torsoDistance=SegmentDistance(points[i],source["pelvis"],source["spine_05"]);int nearestSide=0;float armDistance=float.MaxValue;
                for(int side=0;side<2;side++){string tail=side==0?"_l":"_r";float distance=Math.Min(SegmentDistance(points[i],source["upperarm"+tail],source["lowerarm"+tail]),SegmentDistance(points[i],source["lowerarm"+tail],source["hand"+tail]));if(distance<armDistance){armDistance=distance;nearestSide=side;}}
                if(points[i].y<Math.Min(source["hand_l"].y,source["hand_r"].y)-native.size.y*.05f||armDistance>native.size.y*.065f||armDistance>torsoDistance*.75f)continue;
                float strength=Mathf.Clamp01((torsoDistance*.8f-armDistance)/(native.size.y*.035f));if(armDistance<native.size.y*.025f)strength=1;armStrength[i]=strength;SampleWeights(armTrees[nearestSide],armFields[nearestSide].weights,points[i],armSeed,i);for(int b=0;b<boneNames.Length;b++)weights[i,b]=weights[i,b]*(1-strength)+armSeed[i,b]*strength;
            }
            var graph=new Graph(points,surface.triangles);weights=graph.Smooth(weights,6,.45f,cancel);for(int i=0;i<points.Length;i++)if(armStrength[i]>.8f)for(int b=0;b<boneNames.Length;b++)weights[i,b]=weights[i,b]*.25f+armSeed[i,b]*.75f;Limit(weights);
            var inverse=new Matrix4x4[points.Length];var target=new Vector3[points.Length];
            for(int i=0;i<points.Length;i++){var blend=new Matrix4x4();for(int b=0;b<boneNames.Length;b++)if(weights[i,b]>0)for(int row=0;row<4;row++)for(int col=0;col<4;col++)blend[row,col]+=matrices[b][row,col]*weights[i,b];inverse[i]=Inverse(blend);target[i]=inverse[i].MultiplyPoint3x4(points[i]);}
            var canonical=graph.Integrate(points,target,inverse,cancel);var attachment=graph.AttachDisconnectedHands(canonical,weights,boneNames,q,native.size.y,labels);var brokenArms=attachment.broken;var bodyNeighbors=Find(bodyField.tree,canonical);var headNeighbors=Find(headField.tree,canonical);
            // Canonical correspondence must keep the same anatomical domain as
            // the inverse pose; otherwise a folded scan forearm can be matched
            // back to the nearest waist after the first registration stage.
            for(int side=0;side<2;side++)
            {
                string tail=side==0?"_l":"_r";var included=new List<int>();for(int b=0;b<boneNames.Length;b++)if(boneNames[b].EndsWith(tail,StringComparison.Ordinal)&&new[]{"upperarm","lowerarm","hand","thumb","middle","index","ring","pinky"}.Contains(boneNames[b].Split('_')[0]))included.Add(b);var domain=Enumerable.Range(0,bodyField.points.Length).Where(i=>included.Sum(b=>bodyField.weights[i,b])>.5f).ToArray();var tree=new Tree(domain.Select(i=>bodyField.points[i]).ToArray());var ids=new int[8];var blend=new float[8];
                for(int i=0;i<canonical.Length;i++)if(armStrength[i]>.8f){float influence=0;foreach(int b in included)influence+=armSeed[i,b];if(influence<.5f)continue;tree.Query(canonical[i],ids,blend);for(int k=0;k<8;k++){bodyNeighbors.ids[i,k]=domain[ids[k]];bodyNeighbors.blend[i,k]=blend[k];}}
            }
            var final=new float[points.Length,boneNames.Length];for(int i=0;i<points.Length;i++){var field=labels[i]==1||labels[i]==6?headField:bodyField;var nearby=labels[i]==1||labels[i]==6?headNeighbors:bodyNeighbors;for(int j=0;j<8;j++)for(int b=0;b<boneNames.Length;b++)final[i,b]+=field.weights[nearby.ids[i,j],b]*nearby.blend[i,j];}
            final=graph.Smooth(final,5,.35f,cancel);Limit(final);var bodyShapes=SampleShapes(bodyField,bodyNeighbors,graph,cancel);var headShapes=SampleShapes(headField,headNeighbors,graph,cancel);
            var sharedShapes=new Dictionary<string,Vector3[]>();float neckLow=q["neck_01"].y-native.size.y*.01f,neckHigh=q["head"].y+native.size.y*.01f;
            foreach(string name in bodyShapes.Keys.Intersect(headShapes.Keys))
            {var value=new Vector3[canonical.Length];for(int i=0;i<value.Length;i++){float t=Mathf.Clamp01((canonical[i].y-neckLow)/(neckHigh-neckLow));t=t*t*(3-2*t);value[i]=Vector3.Lerp(bodyShapes[name][i],headShapes[name][i],t);}sharedShapes[name]=value;}
            var normals=new Vector3[points.Length];for(int i=0;i<surface.triangles.Length;i+=3){int a=surface.triangles[i],b=surface.triangles[i+1],c=surface.triangles[i+2];var normal=Vector3.Cross(canonical[b]-canonical[a],canonical[c]-canonical[a]);normals[a]+=normal;normals[b]+=normal;normals[c]+=normal;}for(int i=0;i<normals.Length;i++)normals[i]=normals[i].normalized;
            var result=new Prepared{vertices=canonical,normals=normals,surfaceJointOffsets=jointOffsets,bones=boneNames,centers=centers,weights=final,templates=templates.ToDictionary(t=>t.key),bodyShapes=bodyShapes,headShapes=headShapes,sharedShapes=sharedShapes,poseRms=pose.rms.Select(v=>v*scale).ToArray(),repairedHandComponents=brokenArms.Count(v=>v),brokenArms=brokenArms,attachment=attachment};
            result.supplements=Supplements(body,bodyField,canonical,labels,native.size.y,result,ref result.inferredAnatomyTriangles,cancel);return result;
        }
        static Field MakeField(Template[] parts,string[] names,Dictionary<string,int> global)
        {
            int count=parts.Sum(p=>p.vertices.Length);var points=new Vector3[count];var weights=new float[count,names.Length];var shapes=parts.SelectMany(p=>p.shapes).Select(s=>Suffix(s.name)).Distinct().ToDictionary(n=>n,n=>new Vector3[count]);int offset=0;
            foreach(var part in parts)
            {
                Array.Copy(part.vertices,0,points,offset,part.vertices.Length);
                for(int i=0;i<part.vertices.Length;i++)
                {
                    var w=part.weights[i];int[] indices={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};float[] values={w.weight0,w.weight1,w.weight2,w.weight3};float sum=0;
                    for(int k=0;k<4;k++)if(indices[k]>=0&&indices[k]<part.bones.Length&&global.TryGetValue(part.bones[indices[k]],out int index)){weights[offset+i,index]+=values[k];sum+=values[k];}
                    if(sum<=1e-8f){weights[offset+i,global["head"]]=1;}else for(int b=0;b<names.Length;b++)weights[offset+i,b]/=sum;
                }
                foreach(var shape in part.shapes)Array.Copy(shape.delta,0,shapes[Suffix(shape.name)],offset,part.vertices.Length);offset+=part.vertices.Length;
            }
            return new Field{points=points,weights=weights,shapes=shapes,tree=new Tree(points)};
        }
        static Vector3[] Skin(Vector3[] vertices,float[,] weights,Matrix4x4[] transforms)
        {var output=new Vector3[vertices.Length];for(int i=0;i<output.Length;i++)for(int b=0;b<transforms.Length;b++)if(weights[i,b]>0)output[i]+=transforms[b].MultiplyPoint3x4(vertices[i])*weights[i,b];return output;}
        static float SegmentDistance(Vector3 point,Vector3 a,Vector3 b)
        {var delta=b-a;float t=Mathf.Clamp01(Vector3.Dot(point-a,delta)/Math.Max(delta.sqrMagnitude,1e-10f));return (point-a-delta*t).magnitude;}
        static Neighbors Find(Tree tree,Vector3[] points)
        {var output=new Neighbors{ids=new int[points.Length,8],blend=new float[points.Length,8]};var ids=new int[8];var blend=new float[8];for(int i=0;i<points.Length;i++){tree.Query(points[i],ids,blend);for(int k=0;k<8;k++){output.ids[i,k]=ids[k];output.blend[i,k]=blend[k];}}return output;}
        static void SampleWeights(Tree tree,float[,] field,Vector3 point,float[,] result,int row)
        {var ids=new int[8];var blend=new float[8];tree.Query(point,ids,blend);for(int k=0;k<8;k++)for(int b=0;b<field.GetLength(1);b++)result[row,b]+=field[ids[k],b]*blend[k];}
        static Dictionary<string,Vector3[]> SampleShapes(Field field,Neighbors nearby,Graph graph,CancellationToken cancel)
        {
            var output=new Dictionary<string,Vector3[]>();foreach(var item in field.shapes){cancel.ThrowIfCancellationRequested();var value=new Vector3[nearby.ids.GetLength(0)];for(int i=0;i<value.Length;i++)for(int k=0;k<8;k++)value[i]+=item.Value[nearby.ids[i,k]]*nearby.blend[i,k];output[item.Key]=graph.Smooth(value,4,.35f,cancel);}return output;
        }
        static void Refine(Vector2[] uv,int[] triangles,byte[] labels,byte[] atlas,int width,int height,Prepared prepared,CancellationToken cancel)
        {
            var refined=(byte[])atlas.Clone();int count=uv.Length;
            foreach(string side in new[]{"l","r"})
            {
                var handBones=new List<int>();var armBones=new List<int>();for(int b=0;b<prepared.bones.Length;b++){string name=prepared.bones[b];string kind=name.Split('_')[0];if(!name.EndsWith("_"+side,StringComparison.Ordinal))continue;if(kind=="lowerarm")armBones.Add(b);else if(new[]{"hand","thumb","middle","index","ring","pinky"}.Contains(kind))handBones.Add(b);}
                var handProbability=new float[count];var armProbability=new float[count];var probability=new float[count];int skin=0,top=0,handSkin=0,handTop=0,armSkin=0,armTop=0;
                for(int i=0;i<count;i++)
                {
                    foreach(int b in handBones)handProbability[i]+=prepared.weights[i,b];foreach(int b in armBones)armProbability[i]+=prepared.weights[i,b];probability[i]=handProbability[i]+armProbability[i];
                    if(probability[i]>.7f){if(labels[i]==4)skin++;else if(labels[i]==2)top++;}if(handProbability[i]>.7f){if(labels[i]==4)handSkin++;else if(labels[i]==2)handTop++;}if(armProbability[i]>.7f){if(labels[i]==4)armSkin++;else if(labels[i]==2)armTop++;}
                }
                // A sleeve/glove dominated zone does not qualify. This is a
                // bounded same-surface cleanup, not a generic bare-arm rule.
                if(skin<20||skin/(float)Math.Max(skin+top,1)<.7f)continue;
                // Protect a locally sleeve-dominated forearm and a locally
                // glove-dominated hand even when the other zone is bare skin.
                for(int i=0;i<count;i++)probability[i]=(handSkin>handTop?handProbability[i]:0)+(armSkin>armTop?armProbability[i]:0);
                for(int face=0;face<triangles.Length;face+=3)
                {
                    if((face&255)==0)cancel.ThrowIfCancellationRequested();int a=triangles[face],b=triangles[face+1],c=triangles[face+2];if(Math.Max(probability[a],Math.Max(probability[b],probability[c]))<.7f)continue;
                    var p=new Vector2(uv[a].x*(width-1),uv[a].y*(height-1));var q=new Vector2(uv[b].x*(width-1),uv[b].y*(height-1));var r=new Vector2(uv[c].x*(width-1),uv[c].y*(height-1));float den=(q.y-r.y)*(p.x-r.x)+(r.x-q.x)*(p.y-r.y);if(Math.Abs(den)<1e-10f)continue;
                    int x0=Mathf.Clamp((int)Math.Floor(Math.Min(p.x,Math.Min(q.x,r.x))),0,width-1),x1=Mathf.Clamp((int)Math.Ceiling(Math.Max(p.x,Math.Max(q.x,r.x))),0,width-1),y0=Mathf.Clamp((int)Math.Floor(Math.Min(p.y,Math.Min(q.y,r.y))),0,height-1),y1=Mathf.Clamp((int)Math.Ceiling(Math.Max(p.y,Math.Max(q.y,r.y))),0,height-1);
                    for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                    {
                        int pixel=y*width+x;if(refined[pixel]!=2)continue;float u=((q.y-r.y)*(x+.5f-r.x)+(r.x-q.x)*(y+.5f-r.y))/den,v=((r.y-p.y)*(x+.5f-r.x)+(p.x-r.x)*(y+.5f-r.y))/den,w=1-u-v;if(u<0||v<0||w<0)continue;if(u*probability[a]+v*probability[b]+w*probability[c]>.7f)refined[pixel]=4;
                    }
                }
                // The original atlas has two-pixel island padding. Include
                // strong known hand seam vertices to avoid a one-pixel red rim.
                for(int i=0;i<count;i++)if(probability[i]>.9f)
                {
                    int cx=Mathf.RoundToInt(uv[i].x*(width-1)),cy=Mathf.RoundToInt(uv[i].y*(height-1));for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int x=cx+dx,y=cy+dy;if(x>=0&&y>=0&&x<width&&y<height&&refined[y*width+x]==2)refined[y*width+x]=4;}
                }
            }
            var rgb=new byte[atlas.Length*3];for(int i=0;i<atlas.Length;i++){if(refined[i]!=atlas[i])prepared.refinedRegionTexels++;rgb[i*3]=rgb[i*3+1]=rgb[i*3+2]=refined[i];}prepared.regionRgb=rgb;
            // Coverage is re-evaluated from the same refined atlas that will
            // drive material colours and semantic partitioning in Conform.
            var corrected=new byte[count];for(int i=0;i<count;i++){int x=Mathf.Clamp(Mathf.RoundToInt(uv[i].x*(width-1)),0,width-1),y=Mathf.Clamp(Mathf.RoundToInt(uv[i].y*(height-1)),0,height-1);corrected[i]=refined[y*width+x];}
            var templates=prepared.templates.Values.ToArray();var body=templates.Where(t=>t.type=="Body").ToArray();var index=prepared.bones.Select((n,i)=>new{n,i}).ToDictionary(x=>x.n,x=>x.i);var field=MakeField(body,prepared.bones,index);prepared.inferredAnatomyTriangles=0;
            prepared.supplements=Supplements(body,field,prepared.vertices,corrected,BoundsOf(templates.SelectMany(t=>t.vertices).ToArray()).size.y,prepared,ref prepared.inferredAnatomyTriangles,cancel);
        }
        static Dictionary<string,Supplement> Supplements(Template[] templates,Field body,Vector3[] surface,byte[] labels,float height,Prepared prepared,ref int inferred,CancellationToken cancel)
        {
            var result=new Dictionary<string,Supplement>();var tree=new Tree(surface);int offset=0;var elbow=new Vector3[2];var wrist=new Vector3[2];
            for(int side=0;side<2;side++){string tail=side==0?"_l":"_r";elbow[side]=prepared.centers[Array.IndexOf(prepared.bones,"lowerarm"+tail)];wrist[side]=prepared.centers[Array.IndexOf(prepared.bones,"hand"+tail)];}
            foreach(var template in templates)
            {
                cancel.ThrowIfCancellationRequested();var fitted=new Vector3[template.vertices.Length];
                string renderer=template.key.Substring(template.key.IndexOf('\n')+1);bool forearm=renderer=="lowerarms"||renderer=="wrists",arm=forearm;var fittedNormals=(Vector3[])template.normals.Clone();var bridgeMatrices=new Matrix4x4[2];
                for(int side=0;side<2;side++)if(prepared.brokenArms[side]){var direction=(prepared.attachment.end[side]-prepared.attachment.start[side]).normalized;float overlap=height*.01f;bridgeMatrices[side]=Affine(elbow[side],wrist[side],prepared.attachment.start[side]-direction*overlap,prepared.attachment.end[side]+direction*overlap);}
                for(int i=0;i<fitted.Length;i++)
                {
                    var point=template.vertices[i];int side=point.x<0?0:1;bool bridge=forearm&&prepared.brokenArms[side];
                    if(bridge){fitted[i]=bridgeMatrices[side].MultiplyPoint3x4(point);var inverse=Inverse(bridgeMatrices[side]);var normal=template.normals[i];fittedNormals[i]=new Vector3(inverse[0,0]*normal.x+inverse[1,0]*normal.y+inverse[2,0]*normal.z,inverse[0,1]*normal.x+inverse[1,1]*normal.y+inverse[2,1]*normal.z,inverse[0,2]*normal.x+inverse[1,2]*normal.y+inverse[2,2]*normal.z).normalized;continue;}
                    int closest=tree.Closest(point);float distance=(surface[closest]-point).magnitude;float inward=distance>height*.035f?0:Mathf.Clamp(Vector3.Dot(surface[closest]-point,template.normals[i])-height*.003f,-height*.1f,0);fitted[i]=point+template.normals[i]*inward;
                }
                var selected=new List<int>();var coverage=new List<int>();int missing=0;
                for(int i=0;i<template.triangles.Length;i+=3)
                {
                    var center=(fitted[template.triangles[i]]+fitted[template.triangles[i+1]]+fitted[template.triangles[i+2]])/3;int nearest=tree.Closest(center);byte label=labels[nearest];bool covered=label==2||label==3||label==5;
                    var originalCenter=(template.vertices[template.triangles[i]]+template.vertices[template.triangles[i+1]]+template.vertices[template.triangles[i+2]])/3;int side=originalCenter.x<0?0:1;bool bridge=arm&&prepared.brokenArms[side],absent=forearm&&(surface[nearest]-center).magnitude>height*.022f;int cover=covered?(label==2?1:label==3?2:3):0;
                    if(bridge){covered=false;absent=true;cover=0;}
                    if(!covered&&!absent&&!bridge)continue;if(absent)missing++;coverage.Add(absent?0:cover);selected.Add(template.triangles[i]);selected.Add(template.triangles[i+1]);selected.Add(template.triangles[i+2]);
                }
                if(selected.Count==0){offset+=template.vertices.Length;continue;}var ids=selected.Distinct().ToArray();var map=ids.Select((id,index)=>new{id,index}).ToDictionary(x=>x.id,x=>x.index);var vertices=ids.Select(i=>fitted[i]).ToArray();var nearby=Find(body.tree,vertices);var w=new float[ids.Length,body.weights.GetLength(1)];
                for(int i=0;i<ids.Length;i++)
                {
                    int side=template.vertices[ids[i]].x<0?0:1;
                    if(forearm&&prepared.brokenArms[side]){var axis=wrist[side]-elbow[side];float t=Mathf.Clamp01(Vector3.Dot(template.vertices[ids[i]]-elbow[side],axis)/axis.sqrMagnitude);var start=prepared.attachment.startIds[side];var end=prepared.attachment.endIds[side];for(int b=0;b<w.GetLength(1);b++){float a=0,z=0;foreach(int id in start)a+=prepared.weights[id,b]/start.Length;foreach(int id in end)z+=prepared.weights[id,b]/end.Length;w[i,b]=a*(1-t)+z*t;}}
                    else for(int k=0;k<8;k++)for(int b=0;b<w.GetLength(1);b++)w[i,b]+=body.weights[nearby.ids[i,k],b]*nearby.blend[i,k];
                }
                Limit(w);
                var shapes=new Dictionary<string,Vector3[]>();foreach(var item in body.shapes){var value=new Vector3[ids.Length];for(int i=0;i<value.Length;i++)for(int k=0;k<8;k++)value[i]+=item.Value[nearby.ids[i,k]]*nearby.blend[i,k];shapes[item.Key]=value;}
                foreach(var item in shapes)for(int i=0;i<ids.Length;i++)
                {
                    int side=template.vertices[ids[i]].x<0?0:1;if(!forearm||!prepared.brokenArms[side])continue;var axis=wrist[side]-elbow[side];float t=Mathf.Clamp01(Vector3.Dot(template.vertices[ids[i]]-elbow[side],axis)/axis.sqrMagnitude);var field=prepared.sharedShapes.TryGetValue(item.Key,out var shared)?shared:prepared.bodyShapes[item.Key];Vector3 start=Vector3.zero,end=Vector3.zero;foreach(int id in prepared.attachment.startIds[side])start+=field[id]/prepared.attachment.startIds[side].Length;foreach(int id in prepared.attachment.endIds[side])end+=field[id]/prepared.attachment.endIds[side].Length;item.Value[i]=Vector3.Lerp(start,end,t);
                }
                result[template.key]=new Supplement{vertices=vertices,normals=ids.Select(i=>fittedNormals[i]).ToArray(),triangles=selected.Select(i=>map[i]).ToArray(),coverage=coverage.ToArray(),weights=w,shapes=shapes,inferred=missing};inferred+=missing;offset+=template.vertices.Length;
            }
            return result;
        }
        // Restrict deletion to the independently observed broken-arm case:
        // largest source component, strong same-side arm weights, below elbow,
        // far outside the native arm axis, and skin/skin-like photo evidence.
        // A detached measured hand and the other intact arm remain protected.
        static bool[] ArtifactFaces(FriendGeneratedMesh surface,Prepared prepared,Color32[] pixels,int textureWidth,int textureHeight,int width,int height,CancellationToken cancel)
        {
            var output=new bool[surface.triangles.Length/3];if(!prepared.brokenArms.Any(b=>b))return output;
            var skinA=new int[256];var skinB=new int[256];var clothA=new int[256];var clothB=new int[256];int skinCount=0,clothCount=0;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int label=prepared.regionRgb[(y*width+x)*3];if(label!=2&&label!=4&&label!=6)continue;var color=pixels[Mathf.Clamp(Mathf.RoundToInt(y*(textureHeight-1f)/Math.Max(1,height-1)),0,textureHeight-1)*textureWidth+Mathf.Clamp(Mathf.RoundToInt(x*(textureWidth-1f)/Math.Max(1,width-1)),0,textureWidth-1)];var ab=LabAB(color);int a=Mathf.Clamp(Mathf.RoundToInt(ab.x),0,255),b=Mathf.Clamp(Mathf.RoundToInt(ab.y),0,255);if(label==2){clothA[a]++;clothB[b]++;clothCount++;}else{skinA[a]++;skinB[b]++;skinCount++;}
            }
            if(skinCount<20||clothCount<20)return output;var skin=new Vector2(Median(skinA,skinCount),Median(skinB,skinCount));var cloth=new Vector2(Median(clothA,clothCount),Median(clothB,clothCount));float nativeHeight=BoundsOf(prepared.templates.Values.SelectMany(t=>t.vertices).ToArray()).size.y;
            for(int face=0;face<output.Length;face++)
            {
                if((face&255)==0)cancel.ThrowIfCancellationRequested();int a=surface.triangles[face*3],b=surface.triangles[face*3+1],c=surface.triangles[face*3+2];if(prepared.attachment.protectedVertices!=null&&(prepared.attachment.protectedVertices[a]||prepared.attachment.protectedVertices[b]||prepared.attachment.protectedVertices[c]))continue;
                var center=(prepared.vertices[a]+prepared.vertices[b]+prepared.vertices[c])/3;var uv=(surface.uv[a]+surface.uv[b]+surface.uv[c])/3;int x=Mathf.Clamp(Mathf.RoundToInt(uv.x*(width-1)),0,width-1),y=Mathf.Clamp(Mathf.RoundToInt(uv.y*(height-1)),0,height-1);int label=prepared.regionRgb[(y*width+x)*3];if(label!=2&&label!=4)continue;var color=pixels[Mathf.Clamp(Mathf.RoundToInt(uv.y*(textureHeight-1)),0,textureHeight-1)*textureWidth+Mathf.Clamp(Mathf.RoundToInt(uv.x*(textureWidth-1)),0,textureWidth-1)];var ab=LabAB(color);bool skinLike=(ab-skin).sqrMagnitude<(ab-cloth).sqrMagnitude;if(label!=4&&!skinLike)continue;
                for(int side=0;side<2;side++)
                {
                    if(!prepared.brokenArms[side])continue;string tail=side==0?"_l":"_r";int upper=Array.IndexOf(prepared.bones,"upperarm"+tail),elbow=Array.IndexOf(prepared.bones,"lowerarm"+tail),wrist=Array.IndexOf(prepared.bones,"hand"+tail);if(upper<0||elbow<0||wrist<0)continue;float influence=0;for(int bone=0;bone<prepared.bones.Length;bone++)if(prepared.bones[bone].EndsWith(tail,StringComparison.Ordinal)&&new[]{"upperarm","lowerarm","hand","thumb","middle","index","ring","pinky"}.Contains(prepared.bones[bone].Split('_')[0]))influence+=(prepared.weights[a,bone]+prepared.weights[b,bone]+prepared.weights[c,bone])/3;
                    if(influence<=.55f||center.y>=prepared.centers[elbow].y+nativeHeight*.07f||center.y<=prepared.centers[wrist].y-nativeHeight*.06f)continue;float distance=Math.Min(SegmentDistance(center,prepared.centers[upper],prepared.centers[elbow]),SegmentDistance(center,prepared.centers[elbow],prepared.centers[wrist]));if(distance>nativeHeight*.025f)output[face]=true;
                }
            }
            return output;
        }
        static int Median(int[] histogram,int count)
        {int sum=0;for(int i=0;i<histogram.Length;i++){sum+=histogram[i];if(sum>=count/2)return i;}return 128;}
        static Vector2 LabAB(Color32 c)
        {
            double r=Linear(c.r/255.0),g=Linear(c.g/255.0),b=Linear(c.b/255.0);double x=LabF((r*.4124564+g*.3575761+b*.1804375)/.95047),y=LabF(r*.2126729+g*.7151522+b*.0721750),z=LabF((r*.0193339+g*.1191920+b*.9503041)/1.08883);return new Vector2((float)(500*(x-y)+128),(float)(200*(y-z)+128));
        }
        static double Linear(double value)=>value<=.04045?value/12.92:Math.Pow((value+.055)/1.055,2.4);
        static double LabF(double value)=>value>216.0/24389?Math.Pow(value,1.0/3):value*(24389.0/27)/116+16.0/116;
        static string ConstantRegion(byte id)
        {
            var image=new Texture2D(16,16,TextureFormat.RGB24,false,true);try{var bytes=Enumerable.Repeat(id,16*16*3).ToArray();image.LoadRawTextureData(bytes);image.Apply(false,false);return Convert.ToBase64String(image.EncodeToPNG());}finally{if(Application.isPlaying)UnityEngine.Object.Destroy(image);else UnityEngine.Object.DestroyImmediate(image);}
        }
        // Complete fitted native body is explicit inferred retopology. It is
        // computed jointly before splitting at native renderer boundaries, so
        // original coincident seams share one bounded displacement and colour.
        static void CompleteBody(FriendGeneratedMesh surface,Prepared prepared,Color32[] pixels,int textureWidth,int textureHeight,int regionWidth,int regionHeight,CancellationToken cancel)
        {
            var templates=prepared.templates.Values.Where(t=>t.type=="Body").ToArray();var positions=templates.SelectMany(t=>t.vertices).ToArray();var originalNormals=templates.SelectMany(t=>t.normals).ToArray();var triangles=new List<int>();int start=0;
            foreach(var template in templates){triangles.AddRange(template.triangles.Select(i=>i+start));start+=template.vertices.Length;}
            var topology=triangles.ToArray();var graph=new Graph(positions,topology);float height=BoundsOf(prepared.templates.Values.SelectMany(t=>t.vertices).ToArray()).size.y;var tree=new Tree(prepared.vertices);
            var ids=new int[8];var blend=new float[8];var displacement=new Vector3[positions.Length];var radii=new float[positions.Length];start=0;
            foreach(var template in templates)
            {
                for(int i=0;i<template.vertices.Length;i++)
                {
                    if((i&127)==0)cancel.ThrowIfCancellationRequested();int index=start+i;tree.Query(positions[index],ids,blend);Vector3 target=Vector3.zero;for(int k=0;k<ids.Length;k++)target+=prepared.vertices[ids[k]]*blend[k];
                    var native=template.weights[i];int[] bone={native.boneIndex0,native.boneIndex1,native.boneIndex2,native.boneIndex3};float[] weight={native.weight0,native.weight1,native.weight2,native.weight3};Vector3 axis=Vector3.zero;
                    for(int k=0;k<4;k++)if(bone[k]>=0&&bone[k]<template.bindposes.Length)axis+=Inverse(template.bindposes[bone[k]]).MultiplyPoint3x4(Vector3.zero)*weight[k];
                    float radius=Math.Max((positions[index]-axis).magnitude,height*.007f);radii[index]=radius;float amount=Mathf.Clamp(Vector3.Dot(target-positions[index],originalNormals[index])-height*.003f,-radius*.3f,radius*.1f);displacement[index]=originalNormals[index]*amount;
                }
                start+=template.vertices.Length;
            }
            displacement=graph.Smooth(displacement,4,.6f,cancel);var fitted=new Vector3[positions.Length];for(int i=0;i<fitted.Length;i++){float length=displacement[i].magnitude;if(length>radii[i]*.3f)displacement[i]*=radii[i]*.3f/length;fitted[i]=positions[i]+displacement[i];}
            int headBone=Array.IndexOf(prepared.bones,"head");if(headBone<0)throw new InvalidDataException("Native body head anchor missing");float neckTarget=prepared.centers[headBone].y+height*.015f;
            var neckBlend=new float[positions.Length];start=0;
            foreach(var template in templates)
            {
                string renderer=template.key.Substring(template.key.IndexOf('\n')+1);
                if(renderer=="neck")
                {
                    float low=template.vertices.Min(v=>v.y),range=Math.Max(template.vertices.Max(v=>v.y)-low,1e-8f);float extra=Math.Max(0,neckTarget-fitted.Skip(start).Take(template.vertices.Length).Max(v=>v.y));
                    for(int i=0;i<template.vertices.Length;i++){float t=Mathf.Clamp01(((template.vertices[i].y-low)/range-.35f)/.65f);t=t*t*(3-2*t);neckBlend[start+i]=t;var p=fitted[start+i];p.y+=t*extra;p.x*=1-.25f*t;p.z*=1-.25f*t;fitted[start+i]=p;}
                }
                start+=template.vertices.Length;
            }
            // Recompute welded normals after fitting and neck completion.
            var normals=new Vector3[fitted.Length];for(int i=0;i<topology.Length;i+=3){int a=topology[i],b=topology[i+1],c=topology[i+2];var n=Vector3.Cross(fitted[b]-fitted[a],fitted[c]-fitted[a]);normals[a]+=n;normals[b]+=n;normals[c]+=n;}
            foreach(var seam in Enumerable.Range(0,positions.Length).GroupBy(i=>Vector3Int.RoundToInt(positions[i]*100000))){Vector3 n=Vector3.zero;foreach(int i in seam)n+=normals[i];n=n.normalized;foreach(int i in seam)normals[i]=n;}
            var labels=new byte[surface.vertices.Length];for(int i=0;i<labels.Length;i++){int x=Mathf.Clamp(Mathf.RoundToInt(surface.uv[i].x*(regionWidth-1)),0,regionWidth-1),y=Mathf.Clamp(Mathf.RoundToInt(surface.uv[i].y*(regionHeight-1)),0,regionHeight-1);labels[i]=prepared.regionRgb[(y*regionWidth+x)*3];}
            var skinIds=Enumerable.Range(0,labels.Length).Where(i=>labels[i]==4||labels[i]==6).ToArray();if(skinIds.Length<4)throw new InvalidDataException("Photo skin support is insufficient for complete body retopology");
            var skinTree=new Tree(skinIds.Select(i=>prepared.vertices[i]).ToArray());var skinColours=skinIds.Select(i=>SampleColour(pixels,textureWidth,textureHeight,surface.uv[i])).ToArray();var median=new Vector3();
            for(int channel=0;channel<3;channel++){var values=skinColours.Select(v=>v[channel]).OrderBy(v=>v).ToArray();median[channel]=values[values.Length/2];}
            var colors=new Vector3[fitted.Length];var near=new int[4];var strengths=new float[4];
            for(int i=0;i<colors.Length;i++)
            {
                if((i&127)==0)cancel.ThrowIfCancellationRequested();skinTree.Query(fitted[i],near,strengths);Vector3 colour=Vector3.zero;for(int k=0;k<4;k++)colour+=skinColours[near[k]]*strengths[k];float distance=(fitted[i]-prepared.vertices[skinIds[near[0]]]).magnitude;colors[i]=Vector3.Lerp(median,colour,Mathf.Clamp01(1-distance/(height*.06f)));
            }
            colors=graph.Smooth(colors,2,.35f,cancel);const int atlasSize=1024;var atlas=new byte[atlasSize*atlasSize*3];for(int i=0;i<atlasSize*atlasSize;i++)WriteColour(atlas,i,median);
            var complete=new Dictionary<string,Supplement>();start=0;var photoHeadIds=Enumerable.Range(0,labels.Length).Where(i=>labels[i]==6).ToArray();var photoHead=photoHeadIds.Length>0?new Tree(photoHeadIds.Select(i=>prepared.vertices[i]).ToArray()):null;
            foreach(var template in templates)
            {
                cancel.ThrowIfCancellationRequested();int count=template.vertices.Length;string renderer=template.key.Substring(template.key.IndexOf('\n')+1);int cover=new[]{"back","chest","shoulders","waist"}.Contains(renderer)?1:new[]{"hips","upperlegs","lowerlegs"}.Contains(renderer)?2:renderer=="feet"?3:0;
                var nativeWeights=new float[count,prepared.bones.Length];for(int i=0;i<count;i++)
                {
                    var w=template.weights[i];int[] bone={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};float[] value={w.weight0,w.weight1,w.weight2,w.weight3};float t=neckBlend[start+i];for(int k=0;k<4;k++){int global=Array.IndexOf(prepared.bones,template.bones[bone[k]]);if(global>=0)nativeWeights[i,global]+=value[k]*(1-t);}nativeWeights[i,headBone]+=t;
                }
                Limit(nativeWeights);var shapes=new Dictionary<string,Vector3[]>();foreach(var source in template.shapes)
                {
                    string suffix=Suffix(source.name);var values=(Vector3[])source.delta.Clone();
                    if(photoHead!=null&&prepared.headShapes.TryGetValue(suffix,out var headDelta))for(int i=0;i<count;i++)if(neckBlend[start+i]>0){photoHead.Query(fitted[start+i],ids,blend);Vector3 delta=Vector3.zero;for(int k=0;k<ids.Length;k++)delta+=headDelta[photoHeadIds[ids[k]]]*blend[k];values[i]=Vector3.Lerp(values[i],delta,neckBlend[start+i]);}
                    shapes[suffix]=values;
                }
                var uv=template.uv;if(uv==null||uv.Length!=count)throw new InvalidDataException("Native body UV is missing");for(int face=0;face<template.triangles.Length;face+=3)
                {
                    int a=template.triangles[face],b=template.triangles[face+1],c=template.triangles[face+2];Raster(atlas,atlasSize,uv[a],uv[b],uv[c],colors[start+a],colors[start+b],colors[start+c]);
                }
                complete[template.key]=new Supplement{vertices=fitted.Skip(start).Take(count).ToArray(),normals=normals.Skip(start).Take(count).ToArray(),uv=(Vector2[])uv.Clone(),triangles=(int[])template.triangles.Clone(),coverage=Enumerable.Repeat(cover,template.triangles.Length/3).ToArray(),weights=nativeWeights,shapes=shapes,inferred=template.triangles.Length/3};start+=count;
            }
            prepared.supplements=complete;prepared.inferredAnatomyTriangles=topology.Length/3;prepared.bodyAtlasRgb=atlas;prepared.completeBody=true;
        }
        static Vector3 SampleColour(Color32[] colors,int width,int height,Vector2 uv)
        {var c=colors[Mathf.Clamp(Mathf.RoundToInt(uv.y*(height-1)),0,height-1)*width+Mathf.Clamp(Mathf.RoundToInt(uv.x*(width-1)),0,width-1)];return new Vector3(c.r,c.g,c.b);}
        static void WriteColour(byte[] atlas,int pixel,Vector3 color)
        {atlas[pixel*3]=(byte)Mathf.Clamp(Mathf.RoundToInt(color.x),0,255);atlas[pixel*3+1]=(byte)Mathf.Clamp(Mathf.RoundToInt(color.y),0,255);atlas[pixel*3+2]=(byte)Mathf.Clamp(Mathf.RoundToInt(color.z),0,255);}
        static void Raster(byte[] atlas,int size,Vector2 a,Vector2 b,Vector2 c,Vector3 ca,Vector3 cb,Vector3 cc)
        {
            a*=size-1;b*=size-1;c*=size-1;float denominator=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Math.Abs(denominator)<1e-10f)return;
            int x0=Mathf.Clamp((int)Math.Floor(Math.Min(a.x,Math.Min(b.x,c.x))),0,size-1),x1=Mathf.Clamp((int)Math.Ceiling(Math.Max(a.x,Math.Max(b.x,c.x))),0,size-1),y0=Mathf.Clamp((int)Math.Floor(Math.Min(a.y,Math.Min(b.y,c.y))),0,size-1),y1=Mathf.Clamp((int)Math.Ceiling(Math.Max(a.y,Math.Max(b.y,c.y))),0,size-1);
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){float u=((b.y-c.y)*(x+.5f-c.x)+(c.x-b.x)*(y+.5f-c.y))/denominator,v=((c.y-a.y)*(x+.5f-c.x)+(a.x-c.x)*(y+.5f-c.y))/denominator,w=1-u-v;if(u>=0&&v>=0&&w>=0)WriteColour(atlas,y*size+x,ca*u+cb*v+cc*w);}
        }
        static BoneWeight Map(float[,] weights,int row,Template template,string[] names,Vector3[] centers)
        {
            var indices=new int[4];var values=new float[4];float sum=0;var expected=Vector3.zero;
            for(int b=0;b<names.Length;b++)if(weights[row,b]>0)
            {
                expected+=centers[b]*weights[row,b];int local=Array.IndexOf(template.bones,names[b]);if(local<0)continue;float value=weights[row,b];for(int k=0;k<4;k++)if(value>values[k]){for(int j=3;j>k;j--){values[j]=values[j-1];indices[j]=indices[j-1];}values[k]=value;indices[k]=local;break;}
            }
            sum=values.Sum();if(sum<1e-8f)
            {
                int anchor=0;float distance=float.MaxValue;for(int b=0;b<template.bindposes.Length;b++){float d=(Inverse(template.bindposes[b]).MultiplyPoint3x4(Vector3.zero)-expected).sqrMagnitude;if(d<distance){distance=d;anchor=b;}}indices[0]=anchor;values[0]=sum=1;
            }
            return new BoneWeight{boneIndex0=indices[0],boneIndex1=indices[1],boneIndex2=indices[2],boneIndex3=indices[3],weight0=values[0]/sum,weight1=values[1]/sum,weight2=values[2]/sum,weight3=values[3]/sum};
        }
        static void Limit(float[,] weights)
        {
            int width=weights.GetLength(1);for(int i=0;i<weights.GetLength(0);i++){var indices=new int[4];var values=new float[4];for(int b=0;b<width;b++){float value=Math.Max(weights[i,b],0);for(int k=0;k<4;k++)if(value>values[k]){for(int j=3;j>k;j--){values[j]=values[j-1];indices[j]=indices[j-1];}values[k]=value;indices[k]=b;break;}}float sum=values.Sum();if(sum<=1e-8f)throw new InvalidDataException("Photo registration has an unbound vertex");for(int b=0;b<width;b++)weights[i,b]=0;for(int k=0;k<4;k++)weights[i,indices[k]]+=values[k]/sum;}
        }
        static Bounds BoundsOf(Vector3[] points){var b=new Bounds(points[0],Vector3.zero);for(int i=1;i<points.Length;i++)b.Encapsulate(points[i]);return b;}
        static void SetTranslation(ref Matrix4x4 m,Vector3 v){m[0,3]=v.x;m[1,3]=v.y;m[2,3]=v.z;}
        static Matrix4x4 Affine(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            var old=b-a;var current=d-c;float ratio=current.magnitude/Math.Max(old.magnitude,1e-8f);var axis=old.normalized;var result=Matrix4x4.identity;
            for(int column=0;column<3;column++){var v=Vector3.zero;v[column]=1;v+=axis*((ratio-1)*axis[column]);v=Rotate(v,old,current);result[0,column]=v.x;result[1,column]=v.y;result[2,column]=v.z;}SetTranslation(ref result,c-result.MultiplyVector(a));return result;
        }
        static Vector3 Rotate(Vector3 v,Vector3 from,Vector3 to)
        {
            var a=from.normalized;var b=to.normalized;float cosine=Mathf.Clamp(Vector3.Dot(a,b),-1,1);var cross=Vector3.Cross(a,b);float sine=cross.magnitude;
            if(sine<1e-7f){if(cosine>0)return v;var axis=Vector3.Cross(a,Math.Abs(a.x)<.8f?Vector3.right:Vector3.forward).normalized;return axis*(2*Vector3.Dot(axis,v))-v;}
            var n=cross/sine;return v*cosine+Vector3.Cross(n,v)*sine+n*(Vector3.Dot(n,v)*(1-cosine));
        }
        static Matrix4x4 Inverse(Matrix4x4 source)
        {
            var a=new double[4,8];for(int i=0;i<4;i++){for(int j=0;j<4;j++)a[i,j]=source[i,j];a[i,i+4]=1;}
            for(int col=0;col<4;col++){int pivot=col;for(int row=col+1;row<4;row++)if(Math.Abs(a[row,col])>Math.Abs(a[pivot,col]))pivot=row;if(Math.Abs(a[pivot,col])<1e-12)throw new InvalidDataException("Photo pose has a singular bone transform");if(pivot!=col)for(int j=0;j<8;j++){double tmp=a[col,j];a[col,j]=a[pivot,j];a[pivot,j]=tmp;}double divisor=a[col,col];for(int j=0;j<8;j++)a[col,j]/=divisor;for(int row=0;row<4;row++)if(row!=col){double factor=a[row,col];for(int j=0;j<8;j++)a[row,j]-=factor*a[col,j];}}
            var output=new Matrix4x4();for(int i=0;i<4;i++)for(int j=0;j<4;j++)output[i,j]=(float)a[i,j+4];return output;
        }
        sealed class Tree
        {
            sealed class Node{public int point,axis;public Node left,right;}
            readonly Vector3[] points;readonly Node root;
            public Tree(Vector3[] points){this.points=points;var ids=Enumerable.Range(0,points.Length).ToArray();root=Build(ids,0,ids.Length,0);}
            Node Build(int[] ids,int start,int length,int depth){if(length<=0)return null;int axis=depth%3;Array.Sort(ids,start,length,Comparer<int>.Create((a,b)=>points[a][axis].CompareTo(points[b][axis])));int middle=start+length/2;return new Node{point=ids[middle],axis=axis,left=Build(ids,start,middle-start,depth+1),right=Build(ids,middle+1,start+length-middle-1,depth+1)};}
            public int Closest(Vector3 point){var ids=new int[1];var weights=new float[1];Query(point,ids,weights);return ids[0];}
            public void Query(Vector3 point,int[] ids,float[] blend){var distance=Enumerable.Repeat(float.MaxValue,ids.Length).ToArray();Search(root,point,ids,distance);float sum=0;for(int i=0;i<ids.Length;i++){blend[i]=distance[i]==float.MaxValue?0:1/Math.Max(distance[i],.003f*.003f);sum+=blend[i];}for(int i=0;i<blend.Length;i++)blend[i]/=sum;}
            void Search(Node node,Vector3 point,int[] ids,float[] distance)
            {
                if(node==null)return;float d=(points[node.point]-point).sqrMagnitude;
                for(int i=0;i<distance.Length;i++)if(d<distance[i]){for(int j=distance.Length-1;j>i;j--){distance[j]=distance[j-1];ids[j]=ids[j-1];}distance[i]=d;ids[i]=node.point;break;}
                float delta=point[node.axis]-points[node.point][node.axis];Search(delta<0?node.left:node.right,point,ids,distance);if(delta*delta<distance[distance.Length-1])Search(delta<0?node.right:node.left,point,ids,distance);
            }
        }
        sealed class Graph
        {
            readonly int[] map,count;readonly int[][] neighbors;readonly int[] edgeA,edgeB;readonly int nodes;
            public Graph(Vector3[] points,int[] triangles)
            {
                var lookup=new Dictionary<Vector3Int,int>();map=new int[points.Length];var sizes=new List<int>();for(int i=0;i<map.Length;i++){var key=Vector3Int.RoundToInt(points[i]*100000);if(!lookup.TryGetValue(key,out int index)){index=sizes.Count;lookup[key]=index;sizes.Add(0);}map[i]=index;sizes[index]++;}count=sizes.ToArray();nodes=count.Length;
                var edges=new HashSet<long>();for(int i=0;i<triangles.Length;i+=3)for(int k=0;k<3;k++){int a=map[triangles[i+k]],b=map[triangles[i+(k+1)%3]];if(a==b)continue;int lo=Math.Min(a,b),hi=Math.Max(a,b);edges.Add(((long)lo<<32)|(uint)hi);}edgeA=edges.Select(e=>(int)(e>>32)).ToArray();edgeB=edges.Select(e=>(int)e).ToArray();var list=Enumerable.Range(0,nodes).Select(_=>new List<int>()).ToArray();for(int i=0;i<edgeA.Length;i++){list[edgeA[i]].Add(edgeB[i]);list[edgeB[i]].Add(edgeA[i]);}neighbors=list.Select(l=>l.ToArray()).ToArray();
            }
            public float[,] Smooth(float[,] source,int steps,float amount,CancellationToken cancel)
            {
                int width=source.GetLength(1);var current=new float[nodes,width];for(int i=0;i<map.Length;i++)for(int k=0;k<width;k++)current[map[i],k]+=source[i,k]/count[map[i]];
                for(int pass=0;pass<steps;pass++){cancel.ThrowIfCancellationRequested();var next=new float[nodes,width];for(int i=0;i<nodes;i++)for(int k=0;k<width;k++){float sum=0;foreach(int adjacent in neighbors[i])sum+=current[adjacent,k];next[i,k]=neighbors[i].Length==0?current[i,k]:current[i,k]*(1-amount)+sum/neighbors[i].Length*amount;}current=next;}
                var result=new float[map.Length,width];for(int i=0;i<map.Length;i++)for(int k=0;k<width;k++)result[i,k]=current[map[i],k];return result;
            }
            public Vector3[] Smooth(Vector3[] source,int steps,float amount,CancellationToken cancel)
            {var data=new float[source.Length,3];for(int i=0;i<source.Length;i++){data[i,0]=source[i].x;data[i,1]=source[i].y;data[i,2]=source[i].z;}var result=Smooth(data,steps,amount,cancel);var output=new Vector3[source.Length];for(int i=0;i<output.Length;i++)output[i]=new Vector3(result[i,0],result[i,1],result[i,2]);return output;}
            public Attachment AttachDisconnectedHands(Vector3[] canonical,float[,] weights,string[] bones,Dictionary<string,Vector3> joints,float height,byte[] labels)
            {
                var component=Enumerable.Repeat(-1,nodes).ToArray();int components=0;var queue=new Queue<int>();
                for(int seed=0;seed<nodes;seed++)if(component[seed]<0){component[seed]=components;queue.Enqueue(seed);while(queue.Count>0){int current=queue.Dequeue();foreach(int next in neighbors[current])if(component[next]<0){component[next]=components;queue.Enqueue(next);}}components++;}
                var sizes=new int[components];for(int i=0;i<map.Length;i++)sizes[component[map[i]]]++;int largest=Array.IndexOf(sizes,sizes.Max());var attachment=new Attachment{protectedVertices=map.Select(node=>component[node]!=largest).ToArray()};
                for(int side=0;side<2;side++)
                {
                    string tail=side==0?"_l":"_r";var indices=new List<int>();for(int b=0;b<bones.Length;b++)if(bones[b].EndsWith(tail,StringComparison.Ordinal)&&new[]{"hand","thumb","middle","index","ring","pinky"}.Contains(bones[b].Split('_')[0]))indices.Add(b);
                    var mass=new float[components];float total=0;for(int i=0;i<map.Length;i++){float value=0;foreach(int b in indices)value+=weights[i,b];if(value<.5f)continue;mass[component[map[i]]]+=value;total+=value;}
                    int detached=-1;float maximum=0;for(int c=0;c<components;c++)if(c!=largest&&sizes[c]<map.Length*.15f&&mass[c]>maximum){maximum=mass[c];detached=c;}if(detached<0||maximum<total*.55f)continue;
                    var ids=Enumerable.Range(0,map.Length).Where(i=>component[map[i]]==detached).ToArray();var wrist=joints["hand"+tail];var nearest=ids.OrderBy(i=>(canonical[i]-wrist).sqrMagnitude).Take(Math.Min(12,ids.Length)).ToArray();Vector3 anchor=Vector3.zero;float sum=0;foreach(int i in nearest){float w=1/Math.Max((canonical[i]-wrist).sqrMagnitude,.00001f);anchor+=canonical[i]*w;sum+=w;}anchor/=sum;
                    Vector3 shift=wrist-anchor;float maximumShift=height*.04f;if(shift.magnitude>maximumShift)shift=shift.normalized*maximumShift;foreach(int i in ids)canonical[i]+=shift;
                    var armBones=new List<int>();for(int b=0;b<bones.Length;b++)if(bones[b].EndsWith(tail,StringComparison.Ordinal)&&new[]{"upperarm","lowerarm","hand","thumb","middle","index","ring","pinky"}.Contains(bones[b].Split('_')[0]))armBones.Add(b);
                    var candidates=Enumerable.Range(0,map.Length).Where(i=>component[map[i]]==largest&&labels[i]==4&&armBones.Sum(b=>weights[i,b])>.35f).ToArray();if(candidates.Length<4)continue;
                    var handTree=new Tree(ids.Select(i=>canonical[i]).ToArray());var pairs=candidates.Select(i=>new{main=i,hand=ids[handTree.Closest(canonical[i])]}).Select(p=>new{p.main,p.hand,distance=(canonical[p.main]-canonical[p.hand]).magnitude}).OrderBy(p=>p.distance).ToArray();float gap=pairs[0].distance;var selected=pairs.Where(p=>p.distance<=gap+height*.02f).Take(24).ToArray();var startIds=selected.Select(p=>p.main).Distinct().ToArray();var endIds=selected.Select(p=>p.hand).Distinct().ToArray();Vector3 start=Vector3.zero,end=Vector3.zero;foreach(int i in startIds)start+=canonical[i]/startIds.Length;foreach(int i in endIds)end+=canonical[i]/endIds.Length;
                    attachment.broken[side]=true;attachment.start[side]=start;attachment.end[side]=end;attachment.startIds[side]=startIds;attachment.endIds[side]=endIds;
                }
                return attachment;
            }
            public Vector3[] Integrate(Vector3[] original,Vector3[] target,Matrix4x4[] inverse,CancellationToken cancel)
            {
                var points=new Vector3[nodes];var anchors=new Vector3[nodes];var local=new Matrix4x4[nodes];
                for(int i=0;i<map.Length;i++){int node=map[i];points[node]+=original[i]/count[node];anchors[node]+=target[i]/count[node];for(int row=0;row<3;row++)for(int col=0;col<3;col++)local[node][row,col]+=inverse[i][row,col]/count[node];}
                var weight=new double[edgeA.Length];var difference=new Vector3[edgeA.Length];var degree=new double[nodes];
                for(int i=0;i<edgeA.Length;i++){int a=edgeA[i],b=edgeB[i];var delta=points[a]-points[b];weight[i]=1/Math.Max(delta.sqrMagnitude,.003*.003);degree[a]+=weight[i];degree[b]+=weight[i];difference[i]=(local[a].MultiplyVector(delta)+local[b].MultiplyVector(delta))*.5f;}
                var sorted=(double[])weight.Clone();Array.Sort(sorted);double constraint=sorted[sorted.Length/2]*.03;var solved=new double[3][];
                for(int axis=0;axis<3;axis++)
                {
                    var rhs=new double[nodes];var initial=new double[nodes];for(int i=0;i<nodes;i++){rhs[i]=anchors[i][axis]*constraint;initial[i]=anchors[i][axis];}
                    for(int i=0;i<edgeA.Length;i++){double value=difference[i][axis]*weight[i];rhs[edgeA[i]]+=value;rhs[edgeB[i]]-=value;}
                    solved[axis]=Solve(rhs,initial,degree,weight,constraint,cancel);
                }
                var result=new Vector3[map.Length];for(int i=0;i<result.Length;i++)result[i]=new Vector3((float)solved[0][map[i]],(float)solved[1][map[i]],(float)solved[2][map[i]]);return result;
            }
            double[] Solve(double[] rhs,double[] x,double[] degree,double[] weight,double constraint,CancellationToken cancel)
            {
                var r=new double[nodes];var z=new double[nodes];var p=new double[nodes];var ap=new double[nodes];Multiply(x,ap,degree,weight,constraint);double rz=0,initial=0;
                for(int i=0;i<nodes;i++){r[i]=rhs[i]-ap[i];z[i]=r[i]/(degree[i]+constraint);p[i]=z[i];rz+=r[i]*z[i];initial+=r[i]*r[i];}double tolerance=Math.Max(initial*1e-10,1e-20);
                for(int iteration=0;iteration<600;iteration++)
                {
                    if((iteration&15)==0)cancel.ThrowIfCancellationRequested();Multiply(p,ap,degree,weight,constraint);double pap=0;for(int i=0;i<nodes;i++)pap+=p[i]*ap[i];if(Math.Abs(pap)<1e-30)break;double alpha=rz/pap,residual=0;
                    for(int i=0;i<nodes;i++){x[i]+=alpha*p[i];r[i]-=alpha*ap[i];residual+=r[i]*r[i];}if(residual<tolerance)break;double next=0;for(int i=0;i<nodes;i++){z[i]=r[i]/(degree[i]+constraint);next+=r[i]*z[i];}double beta=next/Math.Max(rz,1e-30);for(int i=0;i<nodes;i++)p[i]=z[i]+beta*p[i];rz=next;
                }
                if(x.Any(v=>double.IsNaN(v)||double.IsInfinity(v)))throw new InvalidDataException("Photo pose edge integration did not converge to finite geometry");return x;
            }
            void Multiply(double[] x,double[] output,double[] degree,double[] weight,double constraint)
            {for(int i=0;i<nodes;i++)output[i]=x[i]*(degree[i]+constraint);for(int i=0;i<edgeA.Length;i++){output[edgeA[i]]-=weight[i]*x[edgeB[i]];output[edgeB[i]]-=weight[i]*x[edgeA[i]];}}
        }
    }
}
