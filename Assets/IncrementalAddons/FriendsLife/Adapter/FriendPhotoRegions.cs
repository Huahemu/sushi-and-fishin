using System;
using System.IO;
using Bozo.ModularCharacters;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // Region PNGs are measured offline in the generated texture's UV space.
    // They carry no executable model, native library or automatic inference.
    // IDs: 0 unchanged, 1 hair, 2 top, 3 bottom, 4 body skin, 5 shoes, 6 face.
    public static class FriendPhotoRegions
    {
        public static void ValidatePng(byte[] png)
        {
            if(png==null||png.Length<33||png.Length>8*1024*1024)throw new InvalidDataException("颜色分区图片大小无效");
            byte[] signature={137,80,78,71,13,10,26,10};for(int i=0;i<signature.Length;i++)if(png[i]!=signature[i])throw new InvalidDataException("颜色分区必须是PNG");
            int width=ReadInt(png,16),height=ReadInt(png,20);
            if(width<16||height<16||width>4096||height>4096)throw new InvalidDataException("颜色分区尺寸必须在16到4096之间");
        }
        static int ReadInt(byte[] bytes,int offset)=>bytes[offset]<<24|bytes[offset+1]<<16|bytes[offset+2]<<8|bytes[offset+3];
        public static void ValidateMask(byte[] png)
        {var texture=LoadMask(png);if(Application.isPlaying)UnityEngine.Object.Destroy(texture);else UnityEngine.Object.DestroyImmediate(texture);}
        public static Texture2D LoadMask(byte[] png)
        {
            ValidatePng(png);var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,true){name="Friend semantic regions",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            try
            {
                if(!texture.LoadImage(png))throw new InvalidDataException("颜色分区图片无法读取");
                foreach(var pixel in texture.GetPixels32())if(pixel.r>6||pixel.g!=pixel.r||pixel.b!=pixel.r)throw new InvalidDataException("颜色分区像素必须是0到6的相同RGB值");
                return texture;
            }
            catch{if(Application.isPlaying)UnityEngine.Object.Destroy(texture);else UnityEngine.Object.DestroyImmediate(texture);throw;}
        }
        public static FriendGeneratedPackage CreateVariant(string sourcePackageId,byte[] regions,CharacterData appearance=null)
        {
            ValidatePng(regions);
            // Import validates the complete decoded mask before persisting or
            // registering any resource. The working source stays immutable.
            var source=JsonUtility.FromJson<FriendGeneratedPackage>(FriendGeneratedModels.ExportPackage(sourcePackageId));
            FriendGeneratedModels.Validate(source);if(source.schema<2)throw new InvalidOperationException("颜色分区需要带原始UV的照片模型");
            string nextId=Guid.NewGuid().ToString("N"),oldPath="/FriendsGenerated/"+sourcePackageId+"/",nextPath="/FriendsGenerated/"+nextId+"/";
            if(appearance!=null)source.characterData=JsonUtility.FromJson<CharacterData>(JsonUtility.ToJson(appearance));
            foreach(var part in source.parts)part.path=part.path.Replace(oldPath,nextPath);
            for(int i=0;i<source.characterData.outfitDatas.Count;i++)
            {var outfit=source.characterData.outfitDatas[i];if(!string.IsNullOrEmpty(outfit.outfit)){outfit.outfit=outfit.outfit.Replace(oldPath,nextPath);source.characterData.outfitDatas[i]=outfit;}}
            source.packageId=nextId;source.schema=Math.Max(source.schema,3);source.regionSchema=1;source.regionMaskPng=Convert.ToBase64String(regions);source.revision++;source.createdUtc=DateTime.UtcNow.ToString("O");
            FriendGeneratedModels.ImportPackage(nextId,JsonUtility.ToJson(source));return source;
        }
    }
}
