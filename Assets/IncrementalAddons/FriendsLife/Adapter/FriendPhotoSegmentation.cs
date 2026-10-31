using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SushiAndFish.FriendsLife.PhotoProcessing;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    public sealed class FriendPhotoRegionResult
    {public byte[] png;public int width,height,covered,observed,inferred;public float seconds;}
    public static class FriendPhotoSegmentation
    {
        public const string Version="quad-head-384-v1";
        // One model/session at a time, one ORT thread. No background framework
        // settings or global worker limits are changed for the rest of the game.
        internal static readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
        static byte[] modelBytes;
        public static async Task<FriendPhotoRegionResult> Analyze(FriendGeneratedMesh surface,CancellationToken cancellation)
        {
            await gate.WaitAsync(cancellation);
            Texture2D texture=null,mask=null;
            try
            {
                if(Application.platform!=RuntimePlatform.WindowsEditor&&Application.platform!=RuntimePlatform.WindowsPlayer)throw new PlatformNotSupportedException("照片外观分析需要64位Windows客户端");
                if(IntPtr.Size!=8)throw new PlatformNotSupportedException("照片外观分析需要64位Windows客户端");
                if(surface==null||surface.texture==null||surface.vertices==null||surface.vertices.Length>100000||surface.triangles==null||surface.triangles.Length>600000||surface.triangles.Length%3!=0||surface.uv==null||surface.uv.Length!=surface.vertices.Length||surface.normals==null||surface.normals.Length!=surface.vertices.Length)
                    throw new InvalidDataException("照片模型缺少完整外观，请重试或更换照片");
                if(modelBytes==null)
                {
                    var asset=Resources.Load<TextAsset>("FriendsLife/Photo/SegformerClothes");if(!asset)throw new InvalidOperationException("照片外观分析组件尚未安装");
                    modelBytes=asset.bytes;Resources.UnloadAsset(asset);
                }
                texture=new Texture2D(2,2,TextureFormat.RGBA32,false){hideFlags=HideFlags.DontSave};
                if(!texture.LoadImage(surface.texture)||texture.width>2048||texture.height>2048)throw new InvalidDataException("照片模型材质无法读取");
                var colors=texture.GetPixels32();var data=new PhotoSurfaceInput{textureWidth=texture.width,textureHeight=texture.height,textureRgb=new byte[colors.Length*3],positions=new float[surface.vertices.Length*3],normals=new float[surface.vertices.Length*3],uv=new float[surface.vertices.Length*2],triangles=surface.triangles};
                for(int i=0;i<colors.Length;i++){data.textureRgb[i*3]=colors[i].r;data.textureRgb[i*3+1]=colors[i].g;data.textureRgb[i*3+2]=colors[i].b;}
                for(int i=0;i<surface.vertices.Length;i++)
                {
                    var p=surface.vertices[i];var n=surface.normals[i];var uv=surface.uv[i];
                    if(!Finite(p.x)||!Finite(p.y)||!Finite(p.z)||!Finite(n.x)||!Finite(n.y)||!Finite(n.z)||!Finite(uv.x)||!Finite(uv.y))throw new InvalidDataException("照片模型包含无效坐标");
                    data.positions[i*3]=p.x;data.positions[i*3+1]=p.y;data.positions[i*3+2]=p.z;data.normals[i*3]=n.x;data.normals[i*3+1]=n.y;data.normals[i*3+2]=n.z;data.uv[i*2]=uv.x;data.uv[i*2+1]=uv.y;
                }
                foreach(int index in surface.triangles)if(index<0||index>=surface.vertices.Length)throw new InvalidDataException("照片模型面数据无效");
                string library=LibraryPath();var result=await Task.Run(()=>PhotoRegionCpu.Generate(data,modelBytes,library,cancellation),cancellation);cancellation.ThrowIfCancellationRequested();
                mask=new Texture2D(result.width,result.height,TextureFormat.RGB24,false,true){hideFlags=HideFlags.DontSave,filterMode=FilterMode.Point};mask.LoadRawTextureData(result.rgb);mask.Apply(false,false);
                return new FriendPhotoRegionResult{png=mask.EncodeToPNG(),width=result.width,height=result.height,covered=result.covered,observed=result.observed,inferred=result.inferred,seconds=result.seconds};
            }
            catch(OperationCanceledException){throw;}
            catch(Exception error){if(cancellation.IsCancellationRequested)throw new OperationCanceledException(cancellation);Debug.LogWarning("[FriendsLife] Photo region analysis: "+error);throw new InvalidOperationException("照片外观分析未完成，请重试。",error);}
            finally
            {Release(texture);Release(mask);gate.Release();}
        }
        static void Release(UnityEngine.Object item){if(!item)return;if(Application.isPlaying)UnityEngine.Object.Destroy(item);else UnityEngine.Object.DestroyImmediate(item);}
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        internal static string LibraryPath()
        {
            string file=Application.isEditor?Path.Combine(Application.dataPath,"IncrementalAddons","FriendsLife","PhotoRuntime","Plugins","x86_64","onnxruntime.dll"):Path.Combine(Application.dataPath,"Plugins","x86_64","onnxruntime.dll");
            if(!File.Exists(file)&&!Application.isEditor)file=Path.Combine(Application.dataPath,"Plugins","onnxruntime.dll");
            return file;
        }
    }
}
