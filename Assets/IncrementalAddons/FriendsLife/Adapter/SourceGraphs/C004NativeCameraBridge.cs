using System;
using UnityEngine;
namespace SushiAndFish.FriendsLife.SourceGraphs
{
    public sealed partial class OriginalGraphBridge
    {
        C004NativeEpisodeCameras nativeCameras;
        void RegisterC004NativeCameras()
        {
            nativeCameras=gameObject.AddComponent<C004NativeEpisodeCameras>();nativeCameras.Initialize(()=>runtime);
            Registry.Action("SetCameraLookAtAndPosAction",(context,seconds)=>
            {
                var job=Job(context);nativeCameras.Register(job.runner,job.episode,job.definition);return nativeCameras.SetPose(context,job.episode);
            },C004NativeCameraPoseBody.Source+"; actual episode-exclusive camera/render target, never the player's MainCamera");
        }
        Camera SourceCamera(OriginalNodeContext context)
        {
            var job=Job(context);nativeCameras.Register(job.runner,job.episode,job.definition);return nativeCameras.CameraOf(context.Runner);
        }
        OriginalValue SourceCameraActor(OriginalNodeContext context){SourceCamera(context);return OriginalValue.Actor(C004NativeEpisodeCameras.Token(context.Runner.State.episodeId));}
        bool IsSourceCameraToken(string id)=>id!=null&&id.StartsWith("@source-camera/",StringComparison.Ordinal);
        bool SourceCameraTokenValid(OriginalNodeContext context,string id)
        {return id==C004NativeEpisodeCameras.Token(context.Runner.State.episodeId)&&SourceCamera(context);}
        OriginalValue SourceCameraPoint(OriginalNodeContext context,string output)
        {
            var camera=SourceCamera(context);
            if(output=="CameraPosition")return OriginalValue.Vector(nativeCameras.PositionOf(context.Runner));
            if(output=="LookAtPosition")return OriginalValue.Vector(nativeCameras.LookAtOf(context.Runner));
            throw new OriginalBindingException(context.Address+": original camera output name is absent: "+output);
        }
        public bool ObserveSourceEpisode(string episodeId,bool observed,out RenderTexture texture)
        {texture=null;return nativeCameras&&nativeCameras.Observe(episodeId,observed,out texture);}
    }
}
