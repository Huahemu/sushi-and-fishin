using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using TomodachiLife.Simulation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    [DefaultExecutionOrder(-80)]
    public sealed partial class OriginalGraphBridge:MonoBehaviour
    {
        sealed class Running
        {
            public SourceEpisode episode;
            public OriginalGraphRunner runner;
            public OriginalDemoDefinition definition;
            public JObject original;
            public Matrix4x4 hostStage;
            public double nextCheckpoint;
        }
        public static OriginalGraphBridge Instance{get;private set;}
        public OriginalGraphLibrary Library{get;private set;}
        public OriginalNodeRegistry Registry{get;private set;}
        public Func<SourceEpisode,OriginalNodeContext,string> OriginalAutomaticChoice;
        public Func<SourceEpisode,OriginalNodeContext,OriginalNodeStatus> OriginalActionPort;
        public Func<SourceEpisode,OriginalNodeContext,string,OriginalValue> OriginalQueryPort;
        readonly Dictionary<string,Running> running=new Dictionary<string,Running>(StringComparer.Ordinal);
        FriendsLifeRuntime runtime;
        NativeMiiExpressionCueRuntime nativeExpressions;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap(){if(Instance==null)new GameObject("Friends Original Graph Bridge").AddComponent<OriginalGraphBridge>();}
        void Awake()
        {
            if(Instance&&Instance!=this){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);
            var text=Resources.Load<TextAsset>("FriendsLife/OriginalGraphs/library");
            if(!text){Debug.LogError("Friends original graph data is missing");enabled=false;return;}
            Library=JsonUtility.FromJson<OriginalGraphLibrary>(text.text);C004NativeGraphLibrary.Extend(Library);Library.Index();Registry=new OriginalNodeRegistry();
            OriginalPrimitiveBindings.Register(Registry);RegisterHostBindings();RegisterC004NativeCameras();InitializeC004NativeFrameProducer();
        }
        void OnDestroy(){if(runtime)runtime.SourceEpisodeRequested-=StartEpisode;if(Instance==this)Instance=null;foreach(var item in running.Values)StopMotions(item.episode);}
        void OnApplicationPause(bool paused){if(paused)CheckpointAll();}
        void OnApplicationQuit()=>CheckpointAll();
        void CheckpointAll(){if(runtime)CaptureSourceRunnersForSerialization(runtime);if(runtime&&runtime.Ready&&runtime.IsAuthority)runtime.SaveNow();}
        // A normal world snapshot must capture current source cursors and raw
        // detached Cue state, without ticking, revision increments or SaveNow.
        public void CaptureSourceRunnersForSerialization(FriendsLifeRuntime owner)
        {
            if(!owner||!owner.Ready||!owner.IsAuthority||!ReferenceEquals(owner,runtime))return;
            nativeExpressions?.CaptureRawSourceState();CaptureC004NativeFrameClock();foreach(var job in running.Values)job.episode.runnerJson=job.runner.Save();
        }
        void Update()
        {
            var current=FriendsLifeRuntime.Instance;
            if(current!=runtime)
            {
                if(runtime)runtime.SourceEpisodeRequested-=StartEpisode;
                runtime=current;if(runtime)runtime.SourceEpisodeRequested+=StartEpisode;
            }
            if(!runtime||!runtime.IsWorldRunning)return;
            runtime.AdvanceC004NativeEpisodeRecovery();
            foreach(var episode in runtime.State.episodes.Where(e=>!e.committed&&e.sourceKind=="MiiEvent"&&e.status=="SourceRunning"&&!running.ContainsKey(e.episodeId)).ToArray())StartEpisode(episode);
            RestoreAuxiliaryEpisodes();
            if(running.Values.Any(j=>!j.episode.committed&&j.episode.status=="SourceRunning"))
            {
                if(!nativeFrameProducer.PublishedThisUnityFrame(runtime,out double sourceSeconds,out string frameFailure))
                {
                    if(frameFailure!=null)foreach(var pending in running.Values.Where(j=>!j.episode.committed&&j.episode.status=="SourceRunning").ToArray())
                    {pending.episode.runnerJson=pending.runner.Save();runtime.UpdateEpisodeStatus(pending.episode.episodeId,"BindingFault",frameFailure);StopMotions(pending.episode);}
                    return;
                }
            }
            foreach(var job in running.Values.ToArray())
            {
                if(job.episode.committed){nativeExpressions.EndCommittedSource(job.runner);StopMotions(job.episode);running.Remove(job.episode.episodeId);continue;}
                if(job.episode.status!="SourceRunning")continue;
                if(!nativeFrameProducer.PublishedThisUnityFrame(runtime,out double sourceDelta,out string sourceFailure))continue;
                var status=job.runner.Tick(sourceDelta);
                try{if(!IsAuxiliary(job.episode))nativeExpressions.AfterSourceGraphTick(job.runner);}catch(Exception e){job.runner.State.error="Native expression actor boundary: "+e.Message;status=OriginalNodeStatus.Failure;}
                if(IsAuxiliary(job.episode)&&status!=OriginalNodeStatus.Running)
                {CompleteAuxiliary(job,status);continue;}
                if(status==OriginalNodeStatus.Failure)
                {
                    job.episode.runnerJson=job.runner.Save();runtime.UpdateEpisodeStatus(job.episode.episodeId,"BindingFault",job.runner.State.error);StopMotions(job.episode);continue;
                }
                if(status==OriginalNodeStatus.Success)
                {
                    // Empty is the original demo result register's initial value, not result[0].
                    // CompleteNativeEpisode rejects it unless the selected source branch has one exact match.
                    job.episode.runnerJson=job.runner.Save();var receipt=runtime.CompleteNativeEpisode(job.episode.episodeId,job.runner.State.result);
                    if(receipt!=null&&receipt.success){nativeExpressions.EndCommittedSource(job.runner);StopMotions(job.episode);running.Remove(job.episode.episodeId);}continue;
                }
                if(Time.unscaledTimeAsDouble>=job.nextCheckpoint)
                {job.nextCheckpoint=Time.unscaledTimeAsDouble+2;job.episode.runnerJson=job.runner.Save();runtime.MarkChanged();}
            }
        }
        public void StartEpisode(SourceEpisode episode)
        {
            if(episode==null||episode.committed||running.ContainsKey(episode.episodeId))return;
            try
            {
                var binding=Library.Event(episode.eventName);
                var definition=(Library.demoDefinitions??Array.Empty<OriginalDemoDefinition>()).Single(d=>d.graph==binding.graph);
                var raw=JObject.Parse(definition.rawJson);
                var branch=runtime.Core.Rules.FindEvent(episode.eventName)?.branches.SingleOrDefault(b=>b.index==episode.branchIndex)??throw new OriginalBindingException("Selected original branch is absent: "+episode.eventName);
                var people=new[]{episode.focusPersonId,episode.targetPersonId,episode.anotherPersonId}.Where(p=>!string.IsNullOrEmpty(p)).ToArray();
                string Sub(string field)
                {
                    var values=(raw[field] as JArray)?.Select(v=>(string)v).Distinct().ToArray()??Array.Empty<string>();
                    if(values.Length>1)throw new OriginalBindingException(definition.rowId+": "+field+" requires the original fork selector");
                    return values.Length==1?values[0]:"";
                }
                OriginalGraphRunState restored=string.IsNullOrEmpty(episode.runnerJson)?null:JsonUtility.FromJson<OriginalGraphRunState>(episode.runnerJson);
                if(restored!=null)
                {
                    // UI coroutines are projections, not durable source effects. Reattach
                    // an unfinished original line/choice after loading the same node.
                    foreach(var frame in restored.frames.Where(f=>f.entered&&!f.complete))
                    {
                        var owner=restored.scopes.Single(s=>s.id==frame.scope);var sourceNode=Library.Graph(owner.graph).Node(frame.node);
                        if(sourceNode.name=="TalkMessageLabelDirection"||sourceNode.name=="TalkMessageLabelDetailDirection")
                            frame.values.RemoveAll(v=>v.key=="started"&& !frame.values.Any(x=>x.key=="finished"&&x.value.boolean));
                        if(sourceNode.name=="ScreenDemoSelectBtnState"&&!frame.values.Any(v=>v.key=="answer"))frame.values.RemoveAll(v=>v.key=="shown");
                    }
                }
                var graphRunner=new OriginalGraphRunner(Library,Registry,episode.episodeId,binding.graph,binding.command,people,new[]{branch.forkStr??"",Sub("DemoForkStrSub1Ary"),Sub("DemoForkStrSub2Ary")},restored);
                graphRunner.ActorHandleValidity=id=>SourceSelectorActorValidity(graphRunner,episode,id);
                BindC004NativeFrameConsumer(graphRunner,episode);
                var focus=runtime.TransformOf(episode.focusPersonId);var target=runtime.TransformOf(episode.targetPersonId);
                if(!focus||!target)throw new OriginalBindingException("Original graph participants have no actual host actors");
                var stage=graphRunner.NewBag("HostStage");var bag=graphRunner.Bag(stage);
                var pos=graphRunner.Get(bag.values,"position");var dir=graphRunner.Get(bag.values,"direction");
                if(pos==null)
                {
                    pos=OriginalValue.Vector((focus.position+target.position)*.5f);var v=target.position-focus.position;v.y=0;dir=OriginalValue.Vector(v.sqrMagnitude>0?v.normalized:focus.forward);
                    graphRunner.Set(bag.values,"position",pos);graphRunner.Set(bag.values,"direction",dir);
                }
                var job=new Running{episode=episode,runner=graphRunner,definition=definition,original=raw,hostStage=Matrix4x4.TRS(pos.AsVector(),Quaternion.LookRotation(dir.AsVector()),Vector3.one)};
                running.Add(episode.episodeId,job);nativeExpressions.TrackRestored(graphRunner);episode.graph=binding.graph;episode.command=binding.command;episode.demoParam=definition.rowId;
                graphRunner.SetSystemValue("MainCamera",OriginalValue.Actor(C004NativeEpisodeCameras.Token(episode.episodeId)));
                graphRunner.SetSystemValue("MiiMaleList",OriginalValue.Actors(people.Where(p=>OriginalGender(p)=="Male")));
                graphRunner.SetSystemValue("MiiFemaleList",OriginalValue.Actors(people.Where(p=>OriginalGender(p)=="Female")));
                runtime.UpdateEpisodeStatus(episode.episodeId,"SourceRunning");
            }
            catch(Exception e){runtime.UpdateEpisodeStatus(episode.episodeId,"BindingFault",e.Message);}
        }
        string OriginalGender(string person)
        {
            var p=runtime.GetPerson(person);string gender=runtime.Core.State.family.profiles.FirstOrDefault(v=>v.residentId==p?.nativeResidentId)?.gender??"";
            return gender=="Man"?"Male":gender=="Woman"?"Female":gender;
        }
        public void Retry(string episodeId)
        {
            var owner=FriendsLifeRuntime.Instance;if(owner)owner.RecoverNativeEpisodeAfterBindingChange(episodeId);
        }
        Running Job(OriginalNodeContext c)=>running.TryGetValue(c.Runner.State.episodeId,out var job)?job:throw new OriginalBindingException("Source episode owner is missing");
        Transform Actor(OriginalValue value)
        {
            if(value.IsNull)throw new OriginalBindingException("Required original actor is null");
            if(IsSourceCameraToken(value.text))return nativeCameras.Resolve(value.text);
            if(value.text=="@camera")throw new OriginalBindingException("Global player camera is not a source episode camera owner");
            var result=runtime.TransformOf(value.ActorIds().FirstOrDefault());return result?result:throw new OriginalBindingException("Actual host actor is missing for original actor slot "+value.text);
        }
        Transform[] Actors(OriginalNodeContext c)
        {
            string key=c.HasInput("SlaveActorHandleList")?"SlaveActorHandleList":c.HasInput("ActorHandleList")?"ActorHandleList":c.HasInput("ActorHandle")?"ActorHandle":null;
            if(key==null)throw new OriginalBindingException(c.Address+": actor input is not declared");
            return c.Input(key).ActorIds().Select(id=>Actor(OriginalValue.Actor(id))).ToArray();
        }
        Transform[] ControlledActors(OriginalNodeContext c)
        {
            var job=Job(c);
            if(!runtime||!runtime.IsAuthority||!runtime.IsWorldRunning||!ReferenceEquals(job.runner,c.Runner)||job.episode.committed||job.episode.status!="SourceRunning")
                throw new OriginalBindingException(c.Address+": source action does not own a running authoritative episode");
            var participants=new[]{job.episode.focusPersonId,job.episode.targetPersonId,job.episode.anotherPersonId}.Where(id=>!string.IsNullOrEmpty(id)).ToArray();
            if(c.Participants.Any(id=>!participants.Contains(id)))
                throw new OriginalBindingException(c.Address+": runner participants do not belong to this episode");
            // Validate the entire list before the first write. Camera handles and
            // human dialogue/choice participants remain readable, but a native
            // projection must never take over their root, view or renderers.
            var targets=Actors(c);
            foreach(var actor in targets)
            {
                var identity=actor.GetComponent<FriendWorldActor>();
                var person=identity?runtime.GetPerson(identity.personId):null;
                var resident=person!=null?runtime.ActorOf(person.personId):null;
                if(person==null||person.controllerKind!=PersonControllerKind.Npc||!participants.Contains(person.personId)||!c.Participants.Contains(person.personId)||
                    !resident||resident.transform!=actor||runtime.TransformOf(person.personId)!=actor||
                    !runtime.State.leases.Any(lease=>lease.personId==person.personId&&lease.ownerEpisode==job.episode.episodeId))
                    throw new OriginalBindingException(c.Address+": world action requires an episode-owned bound NPC; human/camera control requires an explicit host interaction binding");
            }
            return targets;
        }
        OriginalValue Pointer(OriginalNodeContext c,params string[] names)
        {
            foreach(string name in names)if(c.HasInput(name))return c.Input(name);
            throw new OriginalBindingException(c.Address+": required pointer input missing");
        }
        void RegisterHostBindings()
        {
            const string host="Host actor/world projection of the exact original node request";
            C004NativeHeadParameterBindings.Register(Registry);
            NativeRecoveredQueries.Register(Registry,c=>
            {
                string id=c.Input("ActorHandle").ActorIds().SingleOrDefault();var person=runtime?.GetPerson(id);
                var actor=person!=null?runtime.TransformOf(person.personId):null;
                var original=person!=null?runtime.Core.FindResident(person.nativeResidentId):null;
                if(!actor||string.IsNullOrEmpty(original?.avatarJson))throw new OriginalBindingException(c.Address+": original actor appearance owner is unavailable");
                var height=JObject.Parse(original.avatarJson)["appearance"]?["height"];
                if(height==null||height.Type!=JTokenType.Integer||height.Value<int>()<0||height.Value<int>()>127)
                    throw new OriginalBindingException(c.Address+": original raw profile height byte is unavailable");
                return (byte)height.Value<int>();
            });
            ScalarOrderedBindings.Register(Registry,c=>
            {
                string id=c.Input("ActorHandle").ActorIds().SingleOrDefault();
                var person=runtime?.GetPerson(id);
                var body=person!=null?runtime.TransformOf(person.personId):null;
                var original=person!=null?runtime.Core.FindResident(person.nativeResidentId):null;
                if(!body||string.IsNullOrEmpty(original?.avatarJson))throw new OriginalBindingException(c.Address+": original raw-build actor/profile owner is unavailable");
                // AvatarData.appearance.build is the copied native raw byte;
                // a host Bozo weight/morph is not that field.
                var build=JObject.Parse(original.avatarJson)["appearance"]?["build"];
                if(build==null||build.Type!=JTokenType.Integer||build.Value<int>()<0||build.Value<int>()>127)
                    throw new OriginalBindingException(c.Address+": original raw profile build byte is unavailable");
                return (byte)build.Value<int>();
            });
            NativeParabolaBindings.Register(Registry);
            nativeExpressions=gameObject.AddComponent<NativeMiiExpressionCueRuntime>();nativeExpressions.Initialize(Registry,()=>runtime);
            NativeWaitBgmBindings.Register(Registry,()=>runtime);
            Registry.Query("ActorToPositionQuery",(c,o)=>OriginalValue.Vector(Actor(c.Input("ActorHandle")).position),host);
            Registry.Query("ActorToDirZ",(c,o)=>OriginalValue.Vector(Actor(c.Input("ActorHandle")).forward),host);
            Registry.Query("ActorToGoalPositionQuery",(c,o)=>{var t=Actor(c.Input("ActorHandle"));var a=t.GetComponent<NavMeshAgent>();if(!a||!a.isOnNavMesh)throw new OriginalBindingException(c.Address+": host navigation agent is unavailable");return OriginalValue.Vector(a.destination);},host);
            Registry.Query("ActorToCenterPosByBoundBoxQuery",(c,o)=>{var t=Actor(c.Input("ActorHandle"));var rs=t.GetComponentsInChildren<Renderer>();if(rs.Length==0)throw new OriginalBindingException(c.Address+": host actor has no bounds");var bounds=rs[0].bounds;foreach(var r in rs.Skip(1))bounds.Encapsulate(r.bounds);return OriginalValue.Vector(bounds.center);},host);
            Registry.Query("IsValidActorQuery",(c,o)=>OriginalValue.Bool(c.Input("ActorHandle").ActorIds().Any(id=>IsSourceCameraToken(id)?SourceCameraTokenValid(c,id):id!="@camera"&&runtime.TransformOf(id)!=null)),host);
            Registry.Query("ConvertDemoTransToWorldPositionQuery",(c,o)=>OriginalValue.Vector(RequiredHostStage(c).MultiplyPoint3x4(c.Input("DemoTrans").AsVector())),host);
            Registry.Query("ConvertWorldPositionToDemoTransQuery",(c,o)=>OriginalValue.Vector(RequiredHostStage(c).inverse.MultiplyPoint3x4(c.Input("Position").AsVector())),host);
            Registry.Query("ConvertDemoDirToWorldDirQuery",(c,o)=>OriginalValue.Vector(RequiredHostStage(c).MultiplyVector(c.Input("DemoDir").AsVector())),host);
            Registry.Query("ConvertDemoVecToWorldVecQuery",(c,o)=>OriginalValue.Vector(RequiredHostStage(c).MultiplyVector(c.Input("DemoVec").AsVector())),host);
            Registry.Query("ConvertDemoYToWorldYQuery",(c,o)=>OriginalValue.Number(RequiredHostStage(c).MultiplyPoint3x4(new Vector3(0,(float)c.Input("DemoAxisY").AsNumber(),0)).y),host);
            Registry.Query("ConvertDemoDirRotToWorldDirQuery",(c,o)=>OriginalValue.Vector(RequiredHostStage(c).MultiplyVector(Quaternion.AngleAxis((float)c.Input("YawDegree").AsNumber(),Vector3.up)*Vector3.forward)),host);
            Registry.Query("GetPositionAndDirByBaseActorQuery",(c,o)=>{var actor=Actor(c.Input("BaseActor"));return o=="Direction"?OriginalValue.Vector(actor.forward):OriginalValue.Vector(actor.TransformPoint(c.Input("Offset").AsVector()));},host);
            Registry.Query("GetCameraActorQuery",(c,o)=>SourceCameraActor(c),host);
            Registry.Query("GetCameraLookAtAndPosQuery",(c,o)=>SourceCameraPoint(c,o),host);
            Registry.Query("GetActorBoneWorldPoisitionQuery",(c,o)=>
            {
                var actor=Actor(c.Input("ActorHandle"));string bone=c.Input("String").AsText();var exact=actor.GetComponentsInChildren<Transform>(true).Where(t=>t.name==bone).ToArray();
                if(exact.Length!=1)throw new OriginalBindingException(c.Address+": explicit native-to-host bone locator required: "+bone);
                return OriginalValue.Vector(exact[0].position);
            },host);
            Registry.Query("FaceHeightWithoutAnimPositionQuery",(c,o)=>
            {
                var actor=Actor(c.Input("ActorHandle"));var anim=actor.GetComponentsInChildren<Animator>().FirstOrDefault(a=>a.isHuman);var head=anim?anim.GetBoneTransform(HumanBodyBones.Head):null;
                if(!head)throw new OriginalBindingException(c.Address+": host humanoid head unavailable");return OriginalValue.Vector(head.position+Vector3.up*(float)c.Input("YDirOffset").AsNumber());
            },host);
            Registry.Query("RandomF32Query",(c,o)=>
            {
                var old=c.Read("random");if(old!=null)return old;
                float unit=NativeMiiEventRandom.NextUnitFloat(ref runtime.Core.State.eventRandom);
                float value=(float)c.Input("FloatMin").AsNumber()+unit*((float)c.Input("FloatMax").AsNumber()-(float)c.Input("FloatMin").AsNumber());
                var result=OriginalValue.Number(value);c.Write("random",result);runtime.MarkChanged();return result;
            },"Copied NativeMiiEventRandom.NextUnitFloat; same host-serialised original RNG state");
            foreach(string name in new[]{"TalkMessageLabelDirection","TalkMessageLabelDetailDirection"})Registry.Action(name,Talk,host);
            Registry.Action("ScreenDemoSelectBtnState",Choice,host);
            foreach(string name in new[]{"RequestActorWarpPosAction","RequestActorWarpPosNoResetClothAction","RequestActorWarpDirAction","RequestActorWarpPosDirAction","WarpPositionDirection","WarpDirDirection","WarpPositionAndDirDirection"})Registry.Action(name,Warp,host);
            foreach(string name in new[]{"WalkingDirection","WalkingAction","WalkingCue"})Registry.Action(name,Walk,host);
            foreach(string name in new[]{"TurningAction","TurningDirection","TurningCue","LookAtDirection","LookAtAction","LookAtCue"})Registry.Action(name,Turn,host);
            foreach(string name in new[]{"RequestStartAnimAction","StartAndStopAnimDirection","StartAndStopAnimSecCue"})Registry.Action(name,Animate,host);
            Registry.Action("StartAndStopAnimSecCueEnd",(c,dt)=>{foreach(var actor in ControlledActors(c)){var motion=actor.GetComponent<OriginalMotionRetargeter>();if(motion)motion.Stop();}return OriginalNodeStatus.Success;},host);
            Registry.Action("DemoSetActorVisibleAction",Visible,host);Registry.Action("DemoSetActorListVisibleAction",Visible,host);
            Registry.Action("RequestActorChangePosition",Warp,host);
            foreach(string name in new[]{"DemoSetupImpl000Action","DemoSetupImpl001State","DemoTeardownImpl000Action"})
                Registry.Action(name,(c,dt)=>{if(c.Participants.Any(id=>!runtime.TransformOf(id)))throw new OriginalBindingException(c.Address+": actual participant lost during source setup/teardown");return OriginalNodeStatus.Success;},"Adapter episode lease/setup/teardown; source actor identity remains the same");
        }
        OriginalNodeStatus Talk(OriginalNodeContext c,double dt)
        {
            if(c.Read("presentationError") is OriginalValue presentationError)throw new OriginalBindingException(c.Address+": "+presentationError.AsText());
            if(c.Read("finished")?.AsBool()==true)return OriginalNodeStatus.Success;
            if(c.Read("started")==null)
            {
                string file=c.Property("Prop_Mstxt").AsText();string label=c.Input("MessageLabel").AsText();
                string textRef=file+".msbt.yml#"+label;string speaker=Pointer(c,"SlaveActorHandleList","ActorHandleList","ActorHandle").ActorIds().FirstOrDefault();
                if(string.IsNullOrEmpty(speaker))throw new OriginalBindingException(c.Address+": original talk speaker is missing");
                var slots=OriginalMessageSlots.Build(c,speaker);
                slots["@SourceMotionArchive"]=RequiredAuxiliaryArchive(Job(c),c)??"";
                c.Write("started",OriginalValue.Bool(true));
                string executionKey=c.ExecutionKey;
                FriendDialoguePresenter.ShowSourceLine(c.Runner.State.episodeId,executionKey,textRef,speaker,slots,()=>{if(c.ExecutionKey==executionKey)c.Write("finished",OriginalValue.Bool(true));},error=>{if(c.ExecutionKey==executionKey)c.Write("presentationError",OriginalValue.String(error));});
            }
            return OriginalNodeStatus.Running;
        }
        OriginalNodeStatus Choice(OriginalNodeContext c,double dt)
        {
            var answer=c.Read("answer");
            if(answer!=null){c.Signal(answer.AsText());return OriginalNodeStatus.Success;}
            var job=Job(c);var options=c.Node.Transitions.Where(e=>e.transitionType==0).Select(e=>e.transitionName).Distinct().ToArray();
            string automatic=OriginalAutomaticChoice?.Invoke(job.episode,c);
            if(automatic!=null)
            {
                if(!options.Contains(automatic))throw new OriginalBindingException(c.Address+": automatic source input is not an authored output signal");
                c.Write("answer",OriginalValue.String(automatic));c.Signal(automatic);return OriginalNodeStatus.Success;
            }
            bool human=c.Participants.Any(id=>runtime.GetPerson(id)?.controllerKind==PersonControllerKind.Human);
            if(!human)throw new OriginalBindingException(c.Address+": this original screen requires a human decision; no original automatic owner/input binding exists");
            if(c.Read("shown")==null)
            {
                c.Write("shown",OriginalValue.Bool(true));
                string executionKey=c.ExecutionKey;
                FriendDialoguePresenter.ShowSourceChoice(job.episode.episodeId,executionKey,c.Property("Prop_Mstxt").AsText()+".msbt.yml#"+c.Input("MessageLabel").AsText(),job.episode.focusPersonId,options,a=>{if(c.ExecutionKey==executionKey)c.Write("answer",OriginalValue.String(a));});
            }
            return OriginalNodeStatus.Running;
        }
        OriginalNodeStatus Warp(OriginalNodeContext c,double dt)
        {
            var targets=ControlledActors(c);if(targets.Length==0)return OriginalNodeStatus.Success;
            string position=c.HasInput("Position")?"Position":c.HasInput("Pos")?"Pos":null;
            string direction=c.HasInput("Direction")?"Direction":c.HasInput("Dir")?"Dir":null;
            if(position==null&&direction==null)throw new OriginalBindingException(c.Address+": exact warp fields require a world binding");
            foreach(var actor in targets)
            {
                if(position!=null){var pos=c.Input(position).AsVector();var agent=actor.GetComponent<NavMeshAgent>();if(agent&&agent.isOnNavMesh){if(!agent.Warp(pos))return OriginalNodeStatus.Failure;}else actor.position=pos;}
                if(direction!=null){var dir=c.Input(direction).AsVector();if(dir.sqrMagnitude>0)actor.rotation=Quaternion.LookRotation(dir,Vector3.up);}
            }
            return OriginalNodeStatus.Success;
        }
        OriginalNodeStatus Walk(OriginalNodeContext c,double dt)
        {
            var value=c.Input("WalkingRequest");var request=c.Bag(value);var point=c.Runner.Get(request.values,"Position")??throw new OriginalBindingException(c.Address+": walking destination absent");
            var actors=ControlledActors(c);bool done=true;
            foreach(var actor in actors)
            {
                var agent=actor.GetComponent<NavMeshAgent>();if(!agent||!agent.isOnNavMesh)throw new OriginalBindingException(c.Address+": actual host navigation agent unavailable");
                if(c.Read("started")==null)
                {
                    var identity=actor.GetComponent<FriendWorldActor>();
                    if(!identity||!runtime.TryMoveWorldActor(identity.personId,point.AsVector(),out var resolved))return OriginalNodeStatus.Failure;
                }
                done&=!agent.pathPending&&agent.remainingDistance<=agent.stoppingDistance;
            }
            c.Write("started",OriginalValue.Bool(true));return done?OriginalNodeStatus.Success:OriginalNodeStatus.Running;
        }
        OriginalNodeStatus Turn(OriginalNodeContext c,double dt)
        {
            var request=c.Bag(Pointer(c,"TurningRequest","HeadRequest"));var direction=c.Runner.Get(request.values,"Direction");var point=c.Runner.Get(request.values,"Position");var target=c.Runner.Get(request.values,"ActorHandle");
            foreach(var actor in ControlledActors(c))
            {
                Vector3 delta=direction!=null?direction.AsVector():target!=null?Actor(target).position-actor.position:point!=null?point.AsVector()-actor.position:throw new OriginalBindingException(c.Address+": turn/look target not bound");
                delta.y=0;if(delta.sqrMagnitude>0)actor.rotation=Quaternion.LookRotation(delta,Vector3.up);
            }
            return OriginalNodeStatus.Success;
        }
        OriginalNodeStatus Animate(OriginalNodeContext c,double dt)
        {
            string command=c.Input("ASName").AsText();var actors=ControlledActors(c);bool done=true;
            foreach(var actor in actors)
            {
                var motion=actor.GetComponent<OriginalMotionRetargeter>();if(!motion)motion=actor.gameObject.AddComponent<OriginalMotionRetargeter>();
                if(!motion.PlaySource(command,c.ExecutionKey,c.Elapsed,RequiredAuxiliaryArchive(Job(c),c),out var error))throw new OriginalBindingException(c.Address+": "+error);
                if(!string.IsNullOrEmpty(motion.LastError))throw new OriginalBindingException(c.Address+": "+motion.LastError);
                done&=motion.Finished;
            }
            if(c.Node.name=="RequestStartAnimAction")return OriginalNodeStatus.Success;
            if(c.Node.name=="StartAndStopAnimSecCue")return c.Elapsed>=c.Input("Sec").AsNumber()?OriginalNodeStatus.Success:OriginalNodeStatus.Running;
            return done?OriginalNodeStatus.Success:OriginalNodeStatus.Running;
        }
        OriginalNodeStatus Visible(OriginalNodeContext c,double dt)
        {
            string input=c.HasInput("IsVisible")?"IsVisible":c.HasInput("Visible")?"Visible":throw new OriginalBindingException(c.Address+": exact visibility parameter is missing");
            foreach(var actor in ControlledActors(c))foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true))renderer.enabled=c.Input(input).AsBool();return OriginalNodeStatus.Success;
        }
        void StopMotions(SourceEpisode episode)
        {
            if(!runtime)return;foreach(string id in new[]{episode.focusPersonId,episode.targetPersonId,episode.anotherPersonId})
            {
                var person=runtime.GetPerson(id);var actor=person?.controllerKind==PersonControllerKind.Npc?runtime.ActorOf(id):null;
                if(actor&&actor.GetComponent<FriendWorldActor>()?.personId==id){var motion=actor.GetComponent<OriginalMotionRetargeter>();if(motion)motion.Stop();}
            }
        }
    }
}
