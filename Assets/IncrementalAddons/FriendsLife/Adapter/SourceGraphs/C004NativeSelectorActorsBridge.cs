using System;
using System.Linq;
namespace SushiAndFish.FriendsLife.SourceGraphs
{
    public sealed partial class OriginalGraphBridge
    {
        bool? SourceSelectorActorValidity(OriginalGraphRunner runner,SourceEpisode episode,string id)
        {
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!runtime.State.episodes.Any(e=>ReferenceEquals(e,episode)))return null;
            if(IsSourceCameraToken(id))
            {
                if(id!=C004NativeEpisodeCameras.Token(episode.episodeId))throw new OriginalBindingException("Source selector camera handle belongs to another episode");
                var job=running.TryGetValue(episode.episodeId,out var actual)?actual:null;
                if(job==null)return null;nativeCameras.Register(runner,episode,job.definition);return nativeCameras.CameraOf(runner)!=null;
            }
            var person=runtime.GetPerson(id);if(person==null||string.IsNullOrEmpty(person.nativeResidentId))return null;
            var matches=NativeSourceContextFacade.AnimationActors(runtime).Where(a=>a.nativeResidentId==person.nativeResidentId).ToArray();
            if(matches.Length!=1)return null;
            // The recovered actor registry owns token/index retirement. A
            // temporarily missing renderer is not a fabricated invalid handle.
            return matches[0].registered;
        }
    }
}
