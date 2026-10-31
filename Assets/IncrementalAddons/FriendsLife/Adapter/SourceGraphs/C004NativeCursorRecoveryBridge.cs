using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    public sealed partial class OriginalGraphBridge
    {
        internal bool CanRecoverSameNativeCursor(FriendsLifeRuntime owner,SourceEpisode episode,out string failure)
        {
            failure=null;
            if(!owner||!owner.Ready||!owner.IsAuthority||!ReferenceEquals(owner,FriendsLifeRuntime.Instance)||
               !owner.State.episodes.Any(e=>ReferenceEquals(e,episode))||episode.sourceKind!="MiiEvent"||episode.committed)
            {failure="Same current authoritative source episode required for cursor recovery.";return false;}
            if(string.IsNullOrEmpty(episode.runnerJson)||string.IsNullOrEmpty(episode.graph)||string.IsNullOrEmpty(episode.command))
            {failure="There is no persisted source cursor to recover; the original start path must handle a never-started episode.";return false;}
            JObject raw;try{raw=JObject.Parse(episode.runnerJson);}catch(Exception error){failure="Persisted original cursor is malformed: "+error.Message;return false;}
            if(raw["schema"]?.Type!=JTokenType.Integer||(int)raw["schema"]!=1||(string)raw["episodeId"]!=episode.episodeId||raw["finished"]?.Type!=JTokenType.Boolean||raw["frames"]?.Type!=JTokenType.Array||raw["scopes"]?.Type!=JTokenType.Array)
            {failure="Persisted original cursor schema/episode owner is unavailable.";return false;}
            if((bool)raw["finished"]){failure="Finished original results require their existing result/commit path, not replay.";return false;}
            if(running.TryGetValue(episode.episodeId,out var job)&&(!ReferenceEquals(job.episode,episode)||job.runner.State.episodeId!=episode.episodeId))
            {failure="The active original cursor is owned by a different episode object.";return false;}
            var binding=Library.Event(episode.eventName);
            if(binding.graph!=episode.graph||binding.command!=episode.command)
            {failure="The selected original event route changed; cursor recovery cannot substitute it.";return false;}
            return true;
        }

        internal bool ResumeRecoveredNativeCursor(FriendsLifeRuntime owner,SourceEpisode episode,out string failure)
        {
            if(!CanRecoverSameNativeCursor(owner,episode,out failure))return false;
            if(!episode.worldArrivalVerified||episode.status!="SourceRecoveryTravel")
            {failure="The same source episode has not actually completed recovery travel.";return false;}
            if(runtime!=owner)
            {
                if(runtime)runtime.SourceEpisodeRequested-=StartEpisode;
                runtime=owner;runtime.SourceEpisodeRequested+=StartEpisode;
            }
            // SourceRunning is established only after the runtime's real
            // EpisodeParticipantsArrived check. Restoring a lost bridge job uses
            // the SAME persisted JSON and selected branch, without ticking it.
            owner.UpdateEpisodeStatus(episode.episodeId,"SourceRunning");
            if(!running.TryGetValue(episode.episodeId,out var job))
            {
                StartEpisode(episode);
                if(!running.TryGetValue(episode.episodeId,out job)||episode.status!="SourceRunning")
                {failure=string.IsNullOrEmpty(episode.fault)?"The same persisted source runner could not be rebound.":episode.fault;return false;}
            }
            job.runner.RetryAfterBindingChange();episode.runnerJson=job.runner.Save();owner.MarkChanged();return true;
        }
    }
}
