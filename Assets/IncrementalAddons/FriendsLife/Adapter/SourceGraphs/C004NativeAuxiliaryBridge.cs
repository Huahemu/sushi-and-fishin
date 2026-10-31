using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    // This entry point executes authored O graph structure against explicit host
    // ports. Daily/Creative records remain R records; a graph result is never a
    // Give/Use/Answer/NativeMiiEvent rule settlement.
    public sealed partial class OriginalGraphBridge
    {
        readonly Dictionary<string,Action<SourceEpisode,OriginalGraphRunState,OriginalNodeStatus>> auxiliaryCallbacks=new Dictionary<string,Action<SourceEpisode,OriginalGraphRunState,OriginalNodeStatus>>(StringComparer.Ordinal);
        public Func<SourceEpisode,bool> AuxiliarySafeBoundaryPort;
        static bool IsAuxiliary(SourceEpisode e)=>e!=null&&(e.sourceKind=="DailyRequest"||e.sourceKind=="CreativeActivity");
        static string[] AuxiliaryPeople(SourceEpisode e)=>new[]{e.focusPersonId,e.targetPersonId,e.anotherPersonId}.Where(x=>!string.IsNullOrEmpty(x)).ToArray();

        void RequireAuxiliaryOwner(SourceEpisode episode)
        {
            var actual=FriendsLifeRuntime.Instance;
            if(actual!=runtime){if(runtime)runtime.SourceEpisodeRequested-=StartEpisode;runtime=actual;if(runtime)runtime.SourceEpisodeRequested+=StartEpisode;}
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!runtime.IsWorldRunning||!IsAuxiliary(episode)||episode.committed||
               !runtime.State.episodes.Any(e=>ReferenceEquals(e,episode)))
                throw new OriginalBindingException("Auxiliary source requires its exact current authoritative DailyRequest/CreativeActivity record");
            var people=AuxiliaryPeople(episode);
            if(people.Length==0||people.Distinct(StringComparer.Ordinal).Count()!=people.Length)
                throw new OriginalBindingException("Auxiliary source participants are missing or repeated");
            foreach(string id in people)
            {
                var person=runtime.GetPerson(id);
                if(person==null||!runtime.TransformOf(id))throw new OriginalBindingException("Auxiliary actual actor unavailable: "+id);
                if(person.controllerKind==PersonControllerKind.Npc&&!runtime.State.leases.Any(l=>l.personId==id&&l.ownerEpisode==episode.episodeId))
                    throw new OriginalBindingException("Auxiliary NPC has no same-episode resource lease: "+id);
            }
        }

        public void StartAuxiliaryEpisode(SourceEpisode episode,string graph,string command,Action<SourceEpisode,OriginalGraphRunState,OriginalNodeStatus> completion)
        {
            RequireAuxiliaryOwner(episode);
            if(string.IsNullOrEmpty(graph)||string.IsNullOrEmpty(command))throw new OriginalBindingException("Exact auxiliary source graph and command required");
            Library.Graph(graph).Entry(command);
            if(!string.IsNullOrEmpty(episode.graph)&&(episode.graph!=graph||episode.command!=command))
                throw new OriginalBindingException("Cannot replace a persisted auxiliary graph/command");
            if(completion!=null)auxiliaryCallbacks[episode.episodeId]=completion;
            if(running.TryGetValue(episode.episodeId,out var existing))
            {
                if(episode.status=="SourceResultReady"||episode.status=="BindingFault")DeliverAuxiliaryResult(existing);
                return;
            }
            var restored=string.IsNullOrEmpty(episode.runnerJson)?null:JsonUtility.FromJson<OriginalGraphRunState>(episode.runnerJson);
            var people=AuxiliaryPeople(episode);
            var runner=new OriginalGraphRunner(Library,Registry,episode.episodeId,graph,command,people,Array.Empty<string>(),restored);
            runner.ActorHandleValidity=id=>SourceSelectorActorValidity(runner,episode,id);
            BindC004NativeFrameConsumer(runner,episode);
            var actualDefinitions=(Library.demoDefinitions??Array.Empty<OriginalDemoDefinition>()).Where(d=>d.graph==graph).ToArray();
            if(actualDefinitions.Length>1)throw new OriginalBindingException("Ambiguous authored auxiliary DemoParam: "+graph);
            var definition=actualDefinitions.SingleOrDefault();
            var job=new Running{episode=episode,runner=runner,definition=definition,original=definition==null?null:JObject.Parse(definition.rawJson),hostStage=Matrix4x4.identity};
            // Coordinates require an authored stage owner. Identity here is not
            // used as a fabricated stage: the coordinate ports below refuse it.
            running.Add(episode.episodeId,job);episode.graph=graph;episode.command=command;
            runner.SetSystemValue("MainCamera",OriginalValue.Actor(C004NativeEpisodeCameras.Token(episode.episodeId)));
            runner.SetSystemValue("MiiMaleList",OriginalValue.Actors(people.Where(p=>OriginalGender(p)=="Male")));
            runner.SetSystemValue("MiiFemaleList",OriginalValue.Actors(people.Where(p=>OriginalGender(p)=="Female")));
            if(episode.status=="SourceResultAcknowledged")return;
            if(episode.status=="SourceResultReady"||episode.status=="BindingFault"){DeliverAuxiliaryResult(job);return;}
            runtime.UpdateEpisodeStatus(episode.episodeId,"SourceRunning");
        }

        void RestoreAuxiliaryEpisodes()
        {
            foreach(var stale in running.Values.Where(j=>IsAuxiliary(j.episode)&&!runtime.State.episodes.Any(e=>ReferenceEquals(e,j.episode))).ToArray())
            {auxiliaryCallbacks.Remove(stale.episode.episodeId);running.Remove(stale.episode.episodeId);}
            foreach(var e in runtime.State.episodes.Where(e=>IsAuxiliary(e)&&!e.committed&&!running.ContainsKey(e.episodeId)&&!string.IsNullOrEmpty(e.graph)&&
                    (e.status=="SourceRunning"||e.status=="SourceResultReady"||e.status=="BindingFault"||e.status=="SourceResultAcknowledged")).ToArray())
            {
                try{StartAuxiliaryEpisode(e,e.graph,e.command,null);}
                catch(Exception error){e.fault=error.Message;runtime.UpdateEpisodeStatus(e.episodeId,"BindingFault",error.Message);}
            }
        }
        void CompleteAuxiliary(Running job,OriginalNodeStatus status)
        {
            job.episode.runnerJson=job.runner.Save();
            runtime.UpdateEpisodeStatus(job.episode.episodeId,status==OriginalNodeStatus.Success?"SourceResultReady":"BindingFault",job.runner.State.error);
            DeliverAuxiliaryResult(job);
        }
        void DeliverAuxiliaryResult(Running job)
        {
            if(!auxiliaryCallbacks.TryGetValue(job.episode.episodeId,out var callback))return;
            // Remove the volatile callback before invoking. Durable result stays
            // available for explicit rebind until the consumer acknowledges it.
            auxiliaryCallbacks.Remove(job.episode.episodeId);
            callback(job.episode,job.runner.State,job.episode.status=="SourceResultReady"?OriginalNodeStatus.Success:OriginalNodeStatus.Failure);
        }
        public void AcknowledgeAuxiliaryResult(SourceEpisode episode)
        {
            RequireAuxiliaryOwner(episode);
            if(episode.status!="SourceResultReady"||!running.TryGetValue(episode.episodeId,out var job)||!job.runner.State.finished)
                throw new OriginalBindingException("Auxiliary result is not durably ready");
            job.episode.runnerJson=job.runner.Save();runtime.UpdateEpisodeStatus(episode.episodeId,"SourceResultAcknowledged");
        }
        public bool CanReleaseAuxiliaryEpisode(SourceEpisode episode)
        {
            if(!IsAuxiliary(episode)||episode.status!="SourceResultAcknowledged"||!running.TryGetValue(episode.episodeId,out var job)||!job.runner.State.finished)return false;
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!runtime.State.episodes.Any(e=>ReferenceEquals(e,episode)))return false;
            foreach(string id in AuxiliaryPeople(episode))
                if(runtime.GetPerson(id)?.controllerKind==PersonControllerKind.Npc&&!runtime.State.leases.Any(l=>l.personId==id&&l.ownerEpisode==episode.episodeId))return false;
            if(AuxiliarySafeBoundaryPort==null||!AuxiliarySafeBoundaryPort(episode))return false;
            // Restore the same source motion owner BEFORE the consumer releases
            // its lease and resumes the NPC brain, not one frame after commit.
            StopMotions(episode);job.episode.runnerJson=job.runner.Save();return true;
        }
        static string RequiredAuxiliaryArchive(Running job,OriginalNodeContext c)
        {
            if(job.definition==null||string.IsNullOrEmpty(job.definition.defaultArchive))throw new OriginalBindingException(c.Address+": authored AS archive owner is absent; no inferred auxiliary archive");
            return job.definition.defaultArchive;
        }
        Matrix4x4 RequiredHostStage(OriginalNodeContext c)
        {
            var job=Job(c);
            if(IsAuxiliary(job.episode))throw new OriginalBindingException(c.Address+": auxiliary graph has no authored HostStage binding");
            return job.hostStage;
        }
    }
}
