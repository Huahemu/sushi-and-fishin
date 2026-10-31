using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SushiAndFish.FriendsLife.PhotoProcessing;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    [Serializable] public sealed class FriendPhotoPoseData
    {public string method;public Vector3[] joints;public float[] rms,confidence;public float seconds;}
    public static class FriendPhotoPose
    {
        public const string Version="quad-mediapipe-native-bind-v1";
        static byte[] modelBytes;
        public static async Task<FriendPhotoPoseData> Analyze(FriendGeneratedMesh surface,CancellationToken cancellation)
        {
            await FriendPhotoSegmentation.gate.WaitAsync(cancellation);Texture2D image=null;
            try
            {
                if(IntPtr.Size!=8||(Application.platform!=RuntimePlatform.WindowsEditor&&Application.platform!=RuntimePlatform.WindowsPlayer))throw new PlatformNotSupportedException("照片姿势适配需要64位Windows客户端");
                if(surface==null||surface.texture==null||surface.vertices==null||surface.vertices.Length>100000||surface.normals?.Length!=surface.vertices.Length||surface.uv?.Length!=surface.vertices.Length||surface.triangles==null||surface.triangles.Length>600000)throw new InvalidDataException("照片模型缺少完整姿势表面");
                if(modelBytes==null){var resource=Resources.Load<TextAsset>("FriendsLife/Photo/MediaPipePose");if(!resource)throw new InvalidOperationException("照片姿势适配组件尚未安装");modelBytes=resource.bytes;Resources.UnloadAsset(resource);}
                image=new Texture2D(2,2,TextureFormat.RGBA32,false){hideFlags=HideFlags.DontSave};if(!image.LoadImage(surface.texture)||image.width>2048||image.height>2048)throw new InvalidDataException("照片材质无法读取");
                var colors=image.GetPixels32();var input=new PhotoSurfaceInput{positions=new float[surface.vertices.Length*3],normals=new float[surface.vertices.Length*3],uv=new float[surface.vertices.Length*2],triangles=surface.triangles,textureWidth=image.width,textureHeight=image.height,textureRgb=new byte[colors.Length*3]};
                for(int i=0;i<colors.Length;i++){input.textureRgb[i*3]=colors[i].r;input.textureRgb[i*3+1]=colors[i].g;input.textureRgb[i*3+2]=colors[i].b;}
                for(int i=0;i<surface.vertices.Length;i++)
                {
                    var p=surface.vertices[i];var n=surface.normals[i];var uv=surface.uv[i];
                    if(!Finite(p.x)||!Finite(p.y)||!Finite(p.z)||!Finite(n.x)||!Finite(n.y)||!Finite(n.z)||!Finite(uv.x)||!Finite(uv.y))throw new InvalidDataException("照片姿势包含无效坐标");
                    input.positions[i*3]=p.x;input.positions[i*3+1]=p.y;input.positions[i*3+2]=p.z;input.normals[i*3]=n.x;input.normals[i*3+1]=n.y;input.normals[i*3+2]=n.z;input.uv[i*2]=uv.x;input.uv[i*2+1]=uv.y;
                }
                foreach(int index in input.triangles)if(index<0||index>=surface.vertices.Length)throw new InvalidDataException("照片姿势面数据无效");
                string library=FriendPhotoSegmentation.LibraryPath();var result=await Task.Run(()=>PhotoRegionCpu.EstimatePose(input,modelBytes,library,cancellation),cancellation);cancellation.ThrowIfCancellationRequested();
                var joints=new Vector3[33];for(int i=0;i<33;i++)joints[i]=new Vector3(result.joints[i*3],result.joints[i*3+1],result.joints[i*3+2]);
                return new FriendPhotoPoseData{method=Version,joints=joints,rms=result.rms,confidence=result.confidence,seconds=result.seconds};
            }
            finally
            {if(image){if(Application.isPlaying)UnityEngine.Object.Destroy(image);else UnityEngine.Object.DestroyImmediate(image);}FriendPhotoSegmentation.gate.Release();}
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
