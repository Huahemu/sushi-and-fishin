using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // Observes committed source/host state only. It never invokes a rule getter,
    // a gameplay command, movement, time advancement or a relationship write.
    public sealed class FriendRelationJournal : MonoBehaviour
    {
        public static FriendRelationJournal Instance {get;private set;}
        public string CurrentPath {get;private set;}
        public string LastError {get;private set;}
        public long WrittenRows {get;private set;}
        readonly Dictionary<string,string> relations=new Dictionary<string,string>(StringComparer.Ordinal);
        readonly Dictionary<string,string> episodes=new Dictionary<string,string>(StringComparer.Ordinal);
        readonly HashSet<string> sourceEntries=new HashSet<string>(StringComparer.Ordinal);
        FriendsLifeRuntime runtime;string world;float next;bool baseline;
        const long FileLimit=4*1024*1024;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){Instance=null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {if(Instance)return;var go=new GameObject("Friends relation journal");DontDestroyOnLoad(go);Instance=go.AddComponent<FriendRelationJournal>();}
        void Awake(){if(Instance&&Instance!=this){Destroy(gameObject);return;}Instance=this;}
        void OnDestroy(){if(Instance==this)Instance=null;}
        long observedRevision=long.MinValue;
        void Update()
        {
            // The journal diffs whole states, so a 5 s cadence that also skips
            // unchanged revisions loses no event; it only coalesces them.
            if(Time.unscaledTime<next)return;next=Time.unscaledTime+5;
            var r=FriendsLifeRuntime.Instance;if(!r||!r.Ready||!r.IsAuthority)return;
            if(world==r.State.worldId&&observedRevision==r.State.revision)return;observedRevision=r.State.revision;
            try
            {
                runtime=r;
                if(world!=r.State.worldId)
                {
                    world=r.State.worldId;if(!Guid.TryParseExact(world,"N",out _))throw new InvalidDataException("Relation log world identity invalid");
                    string folder=PhotoFriendPaths.Folder("RelationJournal");Directory.CreateDirectory(folder);CurrentPath=Path.Combine(folder,world+".jsonl");
                    relations.Clear();episodes.Clear();sourceEntries.Clear();baseline=false;
                    Write(new JObject{["kind"]="ObservationBaseline",["personCount"]=r.People.Count,["scope"]="Committed source state observed; baseline is not a newly caused relationship event"});
                }
                var present=new HashSet<string>(StringComparer.Ordinal);
                foreach(var relation in r.Core.State.relationships)
                {
                    string key=relation.fromResidentId+"/"+relation.toResidentId;present.Add(key);string json=JsonUtility.ToJson(relation);relations.TryGetValue(key,out var before);
                    if(baseline&&json!=before)
                    {
                        var from=r.PersonFromNative(relation.fromResidentId);var to=r.PersonFromNative(relation.toResidentId);
                        Write(new JObject{["kind"]=before==null?"RelationCreated":"RelationChanged",["direction"]=key,["before"]=before==null?null:JObject.Parse(before),["after"]=JObject.Parse(json),["from"]=Person(from?.personId),["to"]=Person(to?.personId),["sourceJournalTail"]=new JArray(r.Core.State.journal.Skip(Math.Max(0,r.Core.State.journal.Count-4)).Select(x=>JObject.Parse(JsonUtility.ToJson(x)))),["sourceRandom"]=JObject.Parse(JsonUtility.ToJson(r.Core.State.eventRandom))});
                    }
                    relations[key]=json;
                }
                foreach(string removed in relations.Keys.Where(k=>!present.Contains(k)).ToArray())
                {if(baseline)Write(new JObject{["kind"]="RelationRemoved",["direction"]=removed,["before"]=JObject.Parse(relations[removed])});relations.Remove(removed);}
                foreach(var e in r.State.episodes)
                {
                    var current=new JObject{["episodeId"]=e.episodeId,["sourceKind"]=e.sourceKind,["sourceId"]=e.sourceId,["graph"]=e.graph,["command"]=e.command,["branchIndex"]=e.branchIndex,["demoResult"]=e.demoResult,["status"]=e.status,["committed"]=e.committed,["inputAccepted"]=e.inputAccepted,["optionId"]=e.optionId,["fault"]=e.fault,["opId"]=e.opId,["sourceMinute"]=e.sourceMinute,["worldArrivalVerified"]=e.worldArrivalVerified};
                    string text=current.ToString(Newtonsoft.Json.Formatting.None);episodes.TryGetValue(e.episodeId,out var old);
                    if(baseline&&text!=old)Write(new JObject{["kind"]="EpisodeTransition",["before"]=old==null?null:JObject.Parse(old),["after"]=current,["focus"]=Person(e.focusPersonId),["target"]=Person(e.targetPersonId),["another"]=Person(e.anotherPersonId)});
                    episodes[e.episodeId]=text;
                }
                foreach(var entry in r.Core.State.journal)
                {if(sourceEntries.Add(entry.id)&&baseline)Write(new JObject{["kind"]="OriginalJournal",["source"]=JObject.Parse(JsonUtility.ToJson(entry)),["focus"]=Person(r.PersonFromNative(entry.focusResidentId)?.personId),["target"]=Person(r.PersonFromNative(entry.targetResidentId)?.personId)});}
                if(sourceEntries.Count>2048){sourceEntries.Clear();foreach(var j in r.Core.State.journal)sourceEntries.Add(j.id);}
                var liveEpisodes=new HashSet<string>(r.State.episodes.Select(e=>e.episodeId));foreach(var id in episodes.Keys.Where(x=>!liveEpisodes.Contains(x)).ToArray())episodes.Remove(id);
                baseline=true;LastError=null;
            }
            catch(Exception error){LastError=error.GetBaseException().Message;Debug.LogWarning("[FriendsLife] Relation observation: "+LastError);next=Time.unscaledTime+10;}
        }
        JObject Person(string id)
        {
            if(string.IsNullOrEmpty(id))return null;var p=runtime.GetPerson(id);if(p==null)return new JObject{["personId"]=id,["present"]=false};
            var root=runtime.TransformOf(p.personId);var animator=root?root.GetComponentInChildren<Animator>(true):null;Transform hip=null;
            if(animator&&animator.isHuman&&animator.avatar&&animator.avatar.isValid)hip=animator.GetBoneTransform(HumanBodyBones.Hips);
            var leases=runtime.State.leases.Where(x=>x.personId==p.personId).Select(x=>new JObject{["ownerEpisode"]=x.ownerEpisode,["facilityId"]=x.facilityId,["phase"]=x.phase,["safeBoundary"]=x.safeBoundary,["destination"]=Vector(x.destination)});
            return new JObject{["personId"]=p.personId,["nativeResidentId"]=p.nativeResidentId,["name"]=p.displayName,["controller"]=p.controllerKind.ToString(),["homeId"]=p.currentHomeId,["actorPresent"]=(bool)root,["actorActive"]=root&&root.gameObject.activeInHierarchy,["position"]=root?Vector(root.position):null,["hipPosition"]=hip?Vector(hip.position):null,["leases"]=new JArray(leases)};
        }
        static JArray Vector(Vector3 v)=>new JArray(v.x,v.y,v.z);
        void Write(JObject row)
        {
            row["utc"]=DateTime.UtcNow.ToString("O");row["worldId"]=world;row["worldRevision"]=runtime.State.revision;row["hostGameMinute"]=runtime.HostGameMinute;row["activeMicros"]=runtime.ActiveMicros;
            if(File.Exists(CurrentPath)&&new FileInfo(CurrentPath).Length>=FileLimit)
            {
                string oldest=CurrentPath+".3";if(File.Exists(oldest))File.Delete(oldest);
                for(int n=2;n>=1;n--){string from=CurrentPath+"."+n;if(File.Exists(from))File.Move(from,CurrentPath+"."+(n+1));}
                File.Move(CurrentPath,CurrentPath+".1");
            }
            File.AppendAllText(CurrentPath,row.ToString(Newtonsoft.Json.Formatting.None)+Environment.NewLine,new UTF8Encoding(false));WrittenRows++;
        }
    }
}
