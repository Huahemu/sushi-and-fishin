using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bozo.ModularCharacters;
using SushiAndFish.FriendsLife.PhotoProcessing;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    [Serializable] public sealed class NativePhotoFitRecord
    {public string version,sourcePath,cutoutPath;public int inputRevision;public NativePhotoFeatures features;public CharacterData character;public string[] adjusted,preserved,resources;}
    public static class NativePhotoParameterFit
    {
        public const string Version="native-photo-parameter-fit-v1";
        static byte[] segmentation,pose;static readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
        public static bool HasGenerated(CharacterData data)=>data?.outfitDatas?.Any(o=>(o.outfit??"").IndexOf("FriendsGenerated/",StringComparison.OrdinalIgnoreCase)>=0)==true;
        public static CharacterData Copy(CharacterData data)=>data==null?null:JsonUtility.FromJson<CharacterData>(JsonUtility.ToJson(data));
        // JsonUtility can restore a serialized null CharacterData as this empty v0 shell.
        // It contains no saved appearance choice; retain the complete native preset instead.
        static bool Empty<T>(ICollection<T> values)=>values==null||values.Count==0;
        static bool EmptyBodyData(OutfitData data)=>string.IsNullOrEmpty(data.outfit)&&Empty(data.colors)&&
            string.IsNullOrEmpty(data.decal)&&Empty(data.decalColors)&&data.decalScale.Equals(default(Vector4))&&
            string.IsNullOrEmpty(data.pattern)&&Empty(data.patternColors)&&data.patternScale.Equals(default(Vector4))&&
            Empty(data.partVisibility)&&data.color.Equals(default(Color))&&data.swatch==0&&Empty(data.decalDatas);
        static bool Uninitialized(CharacterData data)=>data!=null&&data.versionID==0&&
            string.IsNullOrEmpty(data.characterName)&&string.IsNullOrEmpty(data.material)&&data.stance==0&&
            Empty(data.bodyIDs)&&Empty(data.bodyShapes)&&Empty(data.faceIDs)&&Empty(data.faceShapes)&&
            Empty(data.bodyModsKeys)&&Empty(data.bodyMods)&&Empty(data.outfitDatas)&&EmptyBodyData(data.bodyData);
        public static CharacterData NativeBase(CharacterData preferred=null)
        {
            var resource=Resources.Load<CharacterObject>("Default");if(!resource)throw new InvalidOperationException("缺少原捏人预设");
            var baseline=Copy(resource.GetCharacterData());CharacterData data=null;
            if(preferred!=null&&!Uninitialized(preferred)){try{data=Copy(HasGenerated(preferred)?FriendAppearancePort.ToDonorData(preferred):preferred);}catch(Exception){data=null;}}
            data=data??baseline;
            for(int i=0;i<data.outfitDatas.Count;i++){var item=data.outfitDatas[i];if(item.colors==null||item.colors.Count==0){var stock=baseline.outfitDatas.FirstOrDefault(o=>string.Equals(TypeOf(o),TypeOf(item),StringComparison.OrdinalIgnoreCase));item.colors=stock.colors==null?new List<Color>():new List<Color>(stock.colors);data.outfitDatas[i]=item;}}
            if(data?.bodyIDs==null||data.faceIDs==null||data.outfitDatas==null)throw new InvalidDataException("原捏人预设资料不完整");return data;
        }
        public static void Validate(CharacterData data)
        {
            if(data==null||HasGenerated(data)||data.outfitDatas==null||data.bodyIDs?.Count!=data.bodyShapes?.Count||data.faceIDs?.Count!=data.faceShapes?.Count)throw new InvalidDataException("原生外貌参数无效");
            if(!data.outfitDatas.Any(o=>string.Equals(TypeOf(o),"Head",StringComparison.OrdinalIgnoreCase))||!data.outfitDatas.Any(o=>string.Equals(TypeOf(o),"Body",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("原生外貌缺少头部或身体");
            foreach(var outfit in data.outfitDatas)if(string.IsNullOrEmpty(outfit.outfit)||!Resources.Load<Outfit>(outfit.outfit))throw new InvalidDataException("原生外貌部件不存在："+outfit.outfit);
            foreach(float v in data.bodyShapes.Concat(data.faceShapes))if(float.IsNaN(v)||float.IsInfinity(v)||v<0||v>100)throw new InvalidDataException("原生外貌参数超出控件范围");
            if(!Resources.Load<MaterialPackage>(data.material))throw new InvalidDataException("原生外貌材质包不存在");
        }
        public static Task<NativePhotoFeatures> Analyze(Texture2D image,CancellationToken cancellation)
        {if(!image)throw new InvalidDataException("照片不存在");return Analyze(image.GetPixels32(),image.width,image.height,cancellation);}
        public static async Task<NativePhotoFeatures> Analyze(Color32[] colors,int width,int height,CancellationToken cancellation)
        {
            if(colors==null||width<=0||height<=0||colors.Length!=width*height)throw new InvalidDataException("照片不存在");
            if(segmentation==null){var resource=Resources.Load<TextAsset>("FriendsLife/Photo/SegformerClothes");if(!resource)throw new InvalidOperationException("本地人像分析组件尚未安装");segmentation=resource.bytes;Resources.UnloadAsset(resource);}
            if(pose==null){var resource=Resources.Load<TextAsset>("FriendsLife/Photo/MediaPipePose");if(!resource)throw new InvalidOperationException("本地人像特征组件尚未安装");pose=resource.bytes;Resources.UnloadAsset(resource);}
            var rgba=new byte[colors.Length*4];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++){var c=colors[(height-1-y)*width+x];int i=(y*width+x)*4;rgba[i]=c.r;rgba[i+1]=c.g;rgba[i+2]=c.b;rgba[i+3]=c.a;}
            string library=FriendPhotoSegmentation.LibraryPath();await gate.WaitAsync(cancellation);
            try{return Normalize(await Task.Run(()=>NativePhotoImageCpu.Analyze(rgba,width,height,segmentation,pose,library,cancellation),cancellation));}finally{gate.Release();}
        }
        // JsonUtility stores a null array as [] and reads it back as an empty array.
        // Every optional feature array is turned back into null unless it is complete.
        public static NativePhotoFeatures Normalize(NativePhotoFeatures f)
        {
            if(f==null)return null;
            float[] Full(float[] values,int count){if(values==null||values.Length<count)return null;for(int i=0;i<count;i++)if(float.IsNaN(values[i])||float.IsInfinity(values[i]))return null;return values;}
            f.crop=Full(f.crop,3);if(f.crop!=null&&f.crop[2]<=0)f.crop=null;
            f.face=Full(f.face,4);f.hair=Full(f.hair,4);f.upper=Full(f.upper,4);f.lower=Full(f.lower,4);f.landmarks=Full(f.landmarks,99);
            f.skinRgb=Full(f.skinRgb,3);f.hairRgb=Full(f.hairRgb,3);f.upperRgb=Full(f.upperRgb,3);f.lowerRgb=Full(f.lowerRgb,3);f.shoeRgb=Full(f.shoeRgb,3);
            if(f.personAlpha!=null&&f.personAlpha.Length<96*96)f.personAlpha=null;if(f.labels!=null&&f.labels.Length<96*96)f.labels=null;
            return f;
        }
        public static bool Usable(NativePhotoFeatures f)=>f!=null&&f.crop!=null&&f.personAlpha!=null;
        public static void ApplyMask(Texture2D source,Texture2D cutout,NativePhotoFeatures f)
        {
            if(!source||!cutout||!Usable(Normalize(f)))throw new InvalidDataException("自动抠图没完成");
            var pixels=source.GetPixels32();int w=source.width,h=source.height;if(cutout.width!=w||cutout.height!=h)cutout.Reinitialize(w,h);
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                float gx=(x-f.crop[0])/f.crop[2]*96-.5f,gy=(h-1-y-f.crop[1])/f.crop[2]*96-.5f;float alpha=0;
                if(gx>=0&&gy>=0&&gx<=95&&gy<=95){int x0=(int)gx,y0=(int)gy,x1=Math.Min(95,x0+1),y1=Math.Min(95,y0+1);float dx=gx-x0,dy=gy-y0;alpha=(f.personAlpha[y0*96+x0]*(1-dx)+f.personAlpha[y0*96+x1]*dx)*(1-dy)+(f.personAlpha[y1*96+x0]*(1-dx)+f.personAlpha[y1*96+x1]*dx)*dy;}
                alpha=Mathf.SmoothStep(0,1,Mathf.InverseLerp(64,192,alpha));// firmer edge, fewer half-transparent blotches
                int i=y*w+x;pixels[i].a=(byte)Mathf.RoundToInt(pixels[i].a*alpha);
            }
            cutout.SetPixels32(pixels);cutout.Apply();
        }
        static void Shape(CharacterData d,string key,float value,List<string> changed,bool shared=false)
        {
            bool applied=false;int i=d.bodyIDs.IndexOf(key);if(i>=0&&i<d.bodyShapes.Count){d.bodyShapes[i]=Mathf.Clamp(value,0,100);applied=true;}
            i=d.faceIDs.IndexOf(key);if(i>=0&&i<d.faceShapes.Count&&(shared||!applied)){d.faceShapes[i]=Mathf.Clamp(value,0,100);applied=true;}
            if(applied)changed.Add(key);
        }
        static void Scale(CharacterData d,string modifierName,float value,List<string> changed)
        {
            var rig=Resources.Load<OutfitSystem>("BSMC_CharacterMergedBase");if(!rig)return;var mod=rig.GetComponentsInChildren<BodyShapeModifier>(true).FirstOrDefault(m=>m.shapeName==modifierName&&m.useScale);if(!mod)return;
            int i=d.bodyModsKeys?.IndexOf(mod.name)??-1;if(i<0||d.bodyMods==null||i>=d.bodyMods.Count)return;var data=d.bodyMods[i];if(data==null)return;float v=Mathf.Clamp(value,mod.scaleRange.x,mod.scaleRange.y);data.scaleValue=v;data.scale=new Vector3(v,v,v);changed.Add(modifierName+" ("+mod.name+")");
        }
        static Color Colour(float[] rgb,Color previous)
        {return rgb==null||rgb.Length<3?previous:new Color(Mathf.Clamp(rgb[0],.02f,.98f),Mathf.Clamp(rgb[1],.02f,.98f),Mathf.Clamp(rgb[2],.02f,.98f),previous.a);}
        static string TypeOf(OutfitData data)=>(data.outfit??"").Split('/')[0];
        // Photo fitting follows CharacterCreator's data-family and current-tag rules.
        // It filters new wardrobe candidates; it never changes the core rig or material.
        enum PhotoFamily { Stylized, Anime }
        static bool FamilyMarker(string[] tags,string family)=>tags?.Any(t=>string.Equals(t,family,StringComparison.OrdinalIgnoreCase))==true;
        static bool TryOutfitFamily(Outfit outfit,out PhotoFamily family)
        {
            family=PhotoFamily.Stylized;if(!outfit)return false;
            if(FamilyMarker(outfit.tags,"Anime")||FamilyMarker(outfit.requiredTags,"Anime")||string.Equals(outfit.PackName,"Anime",StringComparison.OrdinalIgnoreCase)||(outfit.Type&&string.Equals(outfit.Type.name,"Body",StringComparison.Ordinal)&&outfit.name.IndexOf("Anime",StringComparison.OrdinalIgnoreCase)>=0)){family=PhotoFamily.Anime;return true;}
            return FamilyMarker(outfit.tags,"Stylized")||FamilyMarker(outfit.requiredTags,"Stylized")||!string.IsNullOrWhiteSpace(outfit.PackName)||(outfit.Type&&string.Equals(outfit.Type.name,"Body",StringComparison.Ordinal));
        }
        static PhotoFamily DataFamily(CharacterData data)
        {
            // CharacterCreator.TryGetCharacterFamily: Body, then Head, then material.
            foreach(string type in new[]{"Body","Head"})
            {
                var item=data.outfitDatas.FirstOrDefault(o=>string.Equals(TypeOf(o),type,StringComparison.Ordinal));
                var outfit=string.IsNullOrEmpty(item.outfit)?null:Resources.Load<Outfit>(item.outfit);
                if(TryOutfitFamily(outfit,out var family))return family;
            }
            return string.Equals(data.material,"BSMC_AnimeShader",StringComparison.OrdinalIgnoreCase)?PhotoFamily.Anime:PhotoFamily.Stylized;
        }
        static HashSet<string> CurrentTags(CharacterData data)
        {
            // Match native current wearing state, including the slot being replaced.
            // Native capture uses one outfit per Type; a repeated Type loads last-wins.
            var current=data.outfitDatas.Where(o=>!string.IsNullOrEmpty(o.outfit))
                .Select(o=>new{data=o,outfit=Resources.Load<Outfit>(o.outfit)}).Where(o=>o.outfit&&o.outfit.Type)
                .GroupBy(o=>o.outfit.Type.name,StringComparer.Ordinal).Select(g=>g.Last()).ToArray();
            var hidden=new HashSet<OutfitType>(current.SelectMany(o=>o.outfit.HideTypes??Array.Empty<OutfitType>()).Where(t=>t));
            if(current.Any(o=>string.Equals(o.outfit.Type.name,"Overall",StringComparison.Ordinal)))
                foreach(var item in current)if(item.outfit.Type.name=="UnderLower"||item.outfit.Type.name=="UnderUpper")hidden.Add(item.outfit.Type);
            var tags=new HashSet<string>(StringComparer.Ordinal);
            foreach(var item in current)
            {
                var outfit=item.outfit;if(hidden.Contains(outfit.Type))continue;
                tags.UnionWith(outfit.tags??Array.Empty<string>());
                foreach(var extra in outfit.GetComponentsInChildren<ApplyTags>(true))
                {
                    bool active=extra.gameObject.activeSelf;
                    if(extra.gameObject==outfit.gameObject&&outfit.mergeMesh)active=false;
                    // BMAC_SaveSystem restores optionalPieces before ordinary selection.
                    int count=Math.Min(item.data.partVisibility?.Length??0,outfit.optionalPieces?.Length??0);
                    for(int i=0;i<count;i++)if(outfit.optionalPieces[i]==extra.gameObject)active=item.data.partVisibility[i];
                    if(active)tags.UnionWith(extra.tags??Array.Empty<string>());
                }
            }
            return tags;
        }
        static string CompatiblePath(CharacterData data,string type,string[] paths)
        {
            if(paths.Length==0)return null;
            var family=DataFamily(data);var tags=CurrentTags(data);
            return paths.FirstOrDefault(path=>
            {
                var outfit=Resources.Load<Outfit>(path);if(!outfit||!outfit.Type||!string.Equals(outfit.Type.name,type,StringComparison.Ordinal))return false;
                // Unclassified outfits remain neutral, as in the original Creator.
                if(TryOutfitFamily(outfit,out var candidate)&&candidate!=family)return false;
                return outfit.requiredTags==null||outfit.requiredTags.All(tags.Contains);
            });
        }
        static void Pick(CharacterData d,string type,string[] paths,float[] rgb,List<string> adjusted,List<string> resources)
        {
            string chosen=CompatiblePath(d,type,paths);int at=d.outfitDatas.FindIndex(o=>string.Equals(TypeOf(o),type,StringComparison.OrdinalIgnoreCase));
            if(chosen==null){if(at>=0)chosen=d.outfitDatas[at].outfit;else return;}
            var item=at>=0?d.outfitDatas[at]:new OutfitData{colors=new List<Color>(),patternColors=new List<Color>(),decalColors=new List<Color>(),decalDatas=new List<DecalData>()};
            item.outfit=chosen;item.colors=item.colors??new List<Color>();while(item.colors.Count<9)item.colors.Add(Color.white);
            if(rgb!=null){int count=type=="Head"||type=="Body"?1:type.StartsWith("Hair",StringComparison.Ordinal)?2:9;for(int i=0;i<count;i++)item.colors[i]=Colour(rgb,item.colors[i]);item.color=Colour(rgb,item.color);}
            if(at>=0)d.outfitDatas[at]=item;else d.outfitDatas.Add(item);adjusted.Add(type+" selection/colour");resources.Add(chosen);
        }
        static float? MouthValue(CharacterData data,float ratio)
        {
            var item=data.outfitDatas.FirstOrDefault(o=>TypeOf(o)=="Head");var outfit=Resources.Load<Outfit>(item.outfit);var renderer=outfit?outfit.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r=>r.sharedMesh):null;if(!renderer)return null;
            var mesh=renderer.sharedMesh;int shape=-1;for(int i=0;i<mesh.blendShapeCount;i++)if(mesh.GetBlendShapeName(i).EndsWith("Shape_MouthWide",StringComparison.Ordinal))shape=i;if(shape<0)return null;
            var delta=new Vector3[mesh.vertexCount];mesh.GetBlendShapeFrameVertices(shape,mesh.GetBlendShapeFrameCount(shape)-1,delta,null,null);float max=delta.Max(v=>Math.Abs(v.x));if(max<1e-6f)return null;
            var ids=Enumerable.Range(0,delta.Length).Where(i=>Math.Abs(delta[i].x)>=max*.4f).ToArray();var points=mesh.vertices;float head=points.Max(v=>v.x)-points.Min(v=>v.x),a=ids.Max(i=>points[i].x)-ids.Min(i=>points[i].x),b=ids.Max(i=>points[i].x+delta[i].x)-ids.Min(i=>points[i].x+delta[i].x);if(Math.Abs(b-a)<1e-5f)return null;return Mathf.Clamp((ratio*head-a)/(b-a)*100,0,100);
        }
        public static NativePhotoFitRecord Fit(NativePhotoFeatures f,CharacterData previous,string source,string cutout,int revision)
        {
            f=Normalize(f)??throw new InvalidDataException("照片特征无效");var d=NativeBase(previous);var changed=new List<string>();var resources=new List<string>();
            // These are appearance/proportion estimates, not demographic/profile inference.
            // BodyType and invisible anatomical details retain the user's/native baseline.
            if(f.fullBody&&f.shoulderTorso>.2f&&f.shoulderTorso<2)
            {
                Scale(d,"Shoulder Width",Mathf.Clamp(f.shoulderTorso/.85f,.92f,1.15f),changed);
                if(f.headBody>.07f&&f.headBody<.23f)Scale(d,"Height",Mathf.Clamp(.135f/f.headBody,.9f,1.1f),changed);
            }
            if(f.landmarks!=null&&f.landmarks.Length>=99&&new[]{7,8,9,10}.All(i=>f.landmarks[i*3+2]>.45f)&&f.mouthEar>.1f&&f.mouthEar<.7f){var value=MouthValue(d,f.mouthEar);if(value.HasValue)Shape(d,"MouthWide",value.Value,changed);}
            Pick(d,"Head",new string[0],f.skinRgb,changed,resources);Pick(d,"Body",new string[0],f.skinRgb,changed,resources);
            string[] front=f.shortHair?(f.hairTexture>.015f?new[]{"hairfront/HairFront_TightCurls","hairfront/Outfit_HairFront_Puffy","HairFront/BSMC_HairFront_Fohawk"}:new[]{"hairfront/HairFront_ProperPart","hairfront/HairFront_SideSwept","HairFront/BSMC_HairFront_Fohawk"}):new[]{"hairfront/HairFront_HimeCut","hairfront/HairFront_UnkeptLoose"};
            string[] back=f.shortHair?new[]{"hairback/HairBack_ShortShag","hairback/Outfit_HairBack_Dude","HairBack/BSMC_HairBack_Scrappy"}:new[]{"hairback/Outfit_HairBack_LongCut","hairback/HairBack_TotalCurls"};
            Pick(d,"HairFront",front,f.hairRgb,changed,resources);Pick(d,"HairBack",back,f.hairRgb,changed,resources);
            string[] top=f.shortSleeves?(f.openNeckline?new[]{"top/Top_ShortSleeveDressShirt","Top/BSMC_Top_Shirt"}:new[]{"Top/BSMC_Top_Shirt"}):new[]{"top/Top_DressShirt","top/Top_SimpleSweater","Top/BSMC_Top_Shirt"};
            Pick(d,"Top",top,f.upperRgb,changed,resources);Pick(d,"Bottom",new string[0],f.lowerRgb,changed,resources);Pick(d,"Feet",new string[0],f.shoeRgb,changed,resources);
            if(f.glasses)Pick(d,"UpperFace",new[]{"upperface/UpperFace_RoundGlasses","upperface/BSMC_Face_SimpleGlasses"},null,changed,resources);
            Validate(d);
            return new NativePhotoFitRecord{version=Version,sourcePath=source,cutoutPath=cutout,inputRevision=revision,features=f,character=d,adjusted=changed.ToArray(),resources=resources.Distinct().ToArray(),preserved=new[]{"BodyType/user baseline","Unobserved muscle/body depth","Nose/ear/eyelid and other unsupported face details","Absolute real-world height is not inferred; Height is relative avatar proportion","Profile identity/age/birthday/voice/personality are not inferred"}};
        }
    }
}
