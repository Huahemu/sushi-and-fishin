using System;using System.Collections.Generic;using System.Linq;using System.Reflection;using Newtonsoft.Json.Linq;using UnityEngine;
namespace SushiAndFish.FriendsLife.SourceGraphs
{
    [DefaultExecutionOrder(10050)]
    public sealed class NativeMiiExpressionCueRuntime:MonoBehaviour
    {
        const string BagName="NativeMiiExpression/v1";
        sealed class Session{public OriginalGraphRunner runner;public OriginalBagState bag;public NativeMiiCueSessionData data;}
        sealed class Visual{public Transform body;public NativeHostExpressionSubset projection;}
        readonly Dictionary<OriginalGraphRunner,Session> sessions=new Dictionary<OriginalGraphRunner,Session>();
        readonly Dictionary<string,Visual> visuals=new Dictionary<string,Visual>(StringComparer.Ordinal);
        Func<FriendsLifeRuntime> runtime;
        public string LastProjectionError{get;private set;}
        public const string Domain="Copied native Cue/leaf/8-slot state; explicit ordinary host model/no equipped habit semantic subset0,2,4,6,8,10,44,47,57 with individually sourced HOST morph recipes; raw47 uses the closed-eye/closed-mouth UnspeakableCM counterpart. Native environmental/temporary face/AS and75-preset pixel mapping are separate unsupported counterpart modules.";
        public void Initialize(OriginalNodeRegistry registry,Func<FriendsLifeRuntime> actualRuntime)
        {
            if(runtime!=null)throw new InvalidOperationException("Native expression runtime already initialized");runtime=actualRuntime;
            registry.Action("MiiExpressionCue",Cue,NativeMiiFacialOwner.Source+"; native actor request boundary; "+Domain);
            registry.Action("MiiExpressionCueEnd",End,"Native007ac198/01799978 actor+cue-kind+isCue cancellation; "+Domain);
        }
        FriendsLifeRuntime Actual(OriginalGraphRunner runner)
        {
            var actual=runtime?.Invoke();if(!actual||!actual.Ready||!actual.IsAuthority||actual.State.episodes.All(e=>e.episodeId!=runner.State.episodeId||e.sourceKind!="MiiEvent"||e.committed||e.status!="SourceRunning"))throw new OriginalBindingException("Actual current authoritative running original source episode required for MiiExpressionCue");return actual;
        }
        Session Load(OriginalGraphRunner runner,bool create)
        {
            if(sessions.TryGetValue(runner,out var cached))return cached;var actual=Actual(runner);string id=runner.State.episodeId+"/"+BagName;var matches=runner.State.bags.Where(b=>b.id==id).ToArray();if(matches.Length>1)throw new OriginalBindingException("Ambiguous persisted native face source bag");
            if(matches.Length==0&&!create)return null;OriginalBagState bag;
            NativeMiiCueSessionData data;
            if(matches.Length==0){bag=runner.Bag(runner.NewBag(BagName));data=new NativeMiiCueSessionData{worldId=actual.State.worldId,episodeId=runner.State.episodeId};}
            else{bag=matches[0];var value=runner.Get(bag.values,"payload");if(value==null||value.kind!="String")throw new OriginalBindingException("Declared native expression bag has no raw payload");NativeMiiCueSession.ValidateRawEnvelope(value.text);data=JsonUtility.FromJson<NativeMiiCueSessionData>(value.text);NativeMiiCueSession.Validate(data,actual.State.worldId,runner.State.episodeId);ValidateHostDomain(data);}
            var result=new Session{runner=runner,bag=bag,data=data};sessions.Add(runner,result);return result;
        }
        static void ValidateHostDomain(NativeMiiCueSessionData data)
        {
            if(data.requests.Any(r=>r.targets.Count>0&&r.rawExpression!=-1&&!NativeHostExpressionSubset.SupportsCode(r.rawExpression)))throw new OriginalBindingException("Saved raw Mii expression is outside declared host subset");
            foreach(var actor in data.actors)
            {
                for(int i=2;i<8;i++)if(actor.state.slots[i].expression!=-1||!string.IsNullOrEmpty(actor.state.slots[i].owner))throw new OriginalBindingException("Unbound foreign source face priority in saved Cue-only module");
                if(actor.selectedResolved&&(actor.state.selectedPriority!=(actor.state.slots[1].expression==-1?0:1)||actor.state.selectedExpression!=(actor.state.slots[1].expression==-1?0:actor.state.slots[1].expression)))throw new OriginalBindingException("Saved derived selection is outside the declared ordinary/no-habit source domain");
            }
        }
        public void TrackRestored(OriginalGraphRunner runner){Load(runner,false);}
        void Persist(Session session,bool notify)
        {
            string json=JsonUtility.ToJson(session.data);var old=session.runner.Get(session.bag.values,"payload");bool changed=old==null||old.kind!="String"||old.text!=json;if(changed)session.runner.Set(session.bag.values,"payload",OriginalValue.String(json));var actual=runtime?.Invoke();if(actual&&actual.Ready&&actual.IsAuthority){var episode=actual.State.episodes.SingleOrDefault(e=>e.episodeId==session.runner.State.episodeId);if(episode!=null)episode.runnerJson=session.runner.Save();if(changed&&notify)actual.MarkChanged();}
        }
        List<NativeMiiCueActor> CaptureActualActors(FriendsLifeRuntime actual,OriginalNodeContext context,bool requireBody)
        {
            var ids=NativeMiiCueInputs.Actors(context);var result=new List<NativeMiiCueActor>();
            foreach(string id in ids){var person=actual.GetPerson(id);if(person==null||string.IsNullOrEmpty(person.nativeResidentId)||requireBody&&!actual.TransformOf(id))throw new OriginalBindingException(context.Address+": actual source actor/body unavailable");var native=NativeSourceContextFacade.AnimationActor(actual,person.nativeResidentId);if(native==null||!native.registered)throw new OriginalBindingException(context.Address+": actual registered source actor handle unavailable");result.Add(new NativeMiiCueActor{personId=id,nativeActorId=person.nativeResidentId,handleToken=native.handleToken,handleIndex=native.handleIndex});}return result;
        }
        bool TryOrdinaryInputs(FriendsLifeRuntime actual,NativeMiiCueActor actor,bool ignoreHabit,out NativeMiiFacialInputs inputs,out string reason)
        {
            inputs=null;reason=null;var body=actual.TransformOf(actor.personId);if(!body){reason="Actual source face body is temporarily absent";return false;}
            var nativeDate=body.GetComponent<NativeDateFacialHold>();var motion=body.GetComponent<OriginalMotionRetargeter>();if(nativeDate&&!string.IsNullOrEmpty(nativeDate.Owner)||motion&&motion.IsPlaying){reason="Active source AS/Date facial model owner is outside this ordinary host subset";return false;}
            var ui=body.GetComponent<FriendResidentExpressionProjection>();if(ui){var until=typeof(FriendResidentExpressionProjection).GetField("until",BindingFlags.Instance|BindingFlags.NonPublic);if(until!=null&&(float)until.GetValue(ui)>Time.time){reason="Active source message-expression owner is outside this Cue-only priority subset";return false;}}
            if(ignoreHabit){inputs=new NativeMiiFacialInputs{ordinaryModelGateResolved=true};return true;}
            var creative=actual.Core.State.creative;if(creative?.residents==null){reason="Canonical equipped native habit selection container absent";return false;}var personal=creative.residents.Where(r=>r.residentId==actor.nativeActorId).ToArray();if(personal.Length>1||personal.Length==1&&personal[0].equippedHabits==null){reason="Ambiguous/missing canonical equipped-habit selection";return false;}if(personal.Length==1&&personal[0].equippedHabits.Count!=0){reason="Equipped native HabitParam modifiers require their exact row/temporary-owner mapping";return false;}
            // Explicit input adapter: this addon owns the ordinary BMAC model
            // counterpart and its empty equipped selection. It does not alias
            // creative.expression/personality sliders or pretend that native
            // environmental/temporary model producers are implemented.
            inputs=new NativeMiiFacialInputs{ordinaryModelGateResolved=true,habitOwnerResolved=true,habitMode=0,temporaryHabitOwnerResolved=true};return true;
        }
        OriginalNodeStatus Cue(OriginalNodeContext context,double seconds)
        {
            var actual=Actual(context.Runner);var existing=Load(context.Runner,false);string requestId=context.ExecutionKey;
            // A saved source activation retains actual raw params and ordered
            // targets. Never re-run producer queries/RNG on resume.
            if(existing?.data.requests.Any(r=>r.requestId==requestId)==true)return OriginalNodeStatus.Success;
            var captured=NativeMiiCueInputs.CachedCue(context,actual.State.worldId);List<NativeMiiCueActor> actors;
            if(captured==null)
            {
                int expression=NativeMiiCueInputs.Expression(context);bool ignoreHabit=NativeMiiCueInputs.IgnoreHabit(context);actors=CaptureActualActors(actual,context,false);
                captured=new NativeMiiCueCaptureData{worldId=actual.State.worldId,executionKey=context.ExecutionKey,sourceAddress=context.Address,rawExpression=expression,ignoreHabit=ignoreHabit,targets=actors.Select(a=>new NativeMiiCueEndActor{personId=a.personId,nativeActorId=a.nativeActorId,handleToken=a.handleToken,handleIndex=a.handleIndex}).ToList()};NativeMiiCueInputs.CacheCue(context,captured);
            }
            else actors=captured.targets.Select(a=>new NativeMiiCueActor{personId=a.personId,nativeActorId=a.nativeActorId,handleToken=a.handleToken,handleIndex=a.handleIndex}).ToList();
            // A renderer/input binding fault retains the SAME authored capture.
            // After a real binding change, retry validates its actual handle but
            // never reruns expression/list producer queries or their RNG.
            foreach(var actor in actors){var person=actual.GetPerson(actor.personId);var native=person!=null&&person.nativeResidentId==actor.nativeActorId?NativeSourceContextFacade.AnimationActor(actual,actor.nativeActorId):null;if(native==null||!native.registered||native.handleToken!=actor.handleToken||native.handleIndex!=actor.handleIndex)throw new OriginalBindingException(context.Address+": cached source actor handle is no longer the actual registered owner");}
            int raw=captured.rawExpression;bool ignore=captured.ignoreHabit;
            if(raw!=-1&&actors.Count>0)
            {
                if(!NativeHostExpressionSubset.SupportsCode(raw))throw new OriginalBindingException(context.Address+": Unsupported actual host expression preset "+raw+"; no Smile fallback");
                foreach(var actor in actors){if(!TryOrdinaryInputs(actual,actor,ignore,out _,out var inputReason))throw new OriginalBindingException(context.Address+": "+inputReason);if(!NativeHostExpressionSubset.TryPrepare(actual.TransformOf(actor.personId),raw,out var prepared,out var reason))throw new OriginalBindingException(context.Address+": "+reason);prepared.Dispose();}
            }
            var session=existing??Load(context.Runner,true);NativeMiiCueSession.ValidateQueue(session.data,requestId,context.Address,raw,actors);
            // Source actor-kind preemption is global to tracked source sessions,
            // not confined to the episode that happens to enqueue the new Cue.
            foreach(var other in sessions.Values.Where(s=>s!=session&&s.data.worldId==actual.State.worldId).ToArray()){NativeMiiCueSession.Validate(other.data,actual.State.worldId,other.data.episodeId);}
            foreach(var other in sessions.Values.Where(s=>s!=session&&s.data.worldId==actual.State.worldId).ToArray())if(NativeMiiCueSession.End(other.data,actors)>0){ResolveAll(other,actual);Persist(other,false);}
            NativeMiiCueSession.Queue(session.data,requestId,context.Address,raw,ignore,actors);Persist(session,false);actual.MarkChanged();return OriginalNodeStatus.Success;
        }
        OriginalNodeStatus End(OriginalNodeContext context,double seconds)
        {
            var actual=Actual(context.Runner);var end=NativeMiiCueInputs.CachedEnd(context,actual.State.worldId);if(end?.phase=="Applied")return OriginalNodeStatus.Success;
            List<NativeMiiCueActor> actors;
            if(end==null)
            {
                actors=CaptureActualActors(actual,context,false);end=new NativeMiiCueEndData{worldId=actual.State.worldId,executionKey=context.ExecutionKey,targets=actors.Select(a=>new NativeMiiCueEndActor{personId=a.personId,nativeActorId=a.nativeActorId,handleToken=a.handleToken,handleIndex=a.handleIndex}).ToList()};NativeMiiCueInputs.CacheEnd(context,end);
            }
            else actors=end.targets.Select(a=>new NativeMiiCueActor{personId=a.personId,nativeActorId=a.nativeActorId,handleToken=a.handleToken,handleIndex=a.handleIndex}).ToList();
            // Actual End only cancels the corresponding Cue kind and actor
            // handles. It neither resets every face lane nor ends Direction.
            var targets=sessions.Values.Where(s=>s.data.worldId==actual.State.worldId).ToArray();foreach(var session in targets)NativeMiiCueSession.Validate(session.data,actual.State.worldId,session.data.episodeId);
            foreach(var session in targets)if(NativeMiiCueSession.End(session.data,actors)>0)ResolveAll(session,actual);
            end.phase="Applied";NativeMiiCueInputs.CacheEnd(context,end);
            // Persist ALL cancellation records and the original captured End
            // parameters before notifying observers that may take a snapshot.
            foreach(var session in targets)Persist(session,false);var own=Load(context.Runner,false);if(own!=null)Persist(own,false);actual.MarkChanged();return OriginalNodeStatus.Success;
        }
        void ResolveAll(Session session,FriendsLifeRuntime actual)
        {
            foreach(var actor in session.data.actors)
            {
                if(!TryOrdinaryInputs(actual,actor,actor.state.slots[1].expression!=-1&&actor.state.slots[1].ignoreHabit,out var inputs,out var reason)){actor.selectedResolved=false;actor.bindingFault=reason;continue;}
                try{NativeMiiFacialOwner.Resolve(actor.state,inputs);actor.selectedResolved=true;actor.bindingFault="";}catch(Exception e){actor.selectedResolved=false;actor.bindingFault=e.Message;}
            }
        }
        public void AfterSourceGraphTick(OriginalGraphRunner runner)
        {
            var session=Load(runner,false);if(session==null)return;var actual=Actual(runner);NativeMiiCueSession.ApplyQueuedAtActorBoundary(session.data);ResolveAll(session,actual);Persist(session,true);
        }
        public void EndCommittedSource(OriginalGraphRunner runner)
        {if(!sessions.TryGetValue(runner,out var session))return;NativeMiiCueSession.EndAllOwned(session.data);var actual=runtime?.Invoke();if(actual&&actual.Ready)ResolveAll(session,actual);Persist(session,true);}
        public void CaptureRawSourceState(){foreach(var session in sessions.Values.ToArray())Persist(session,false);}
        IEnumerable<NativeMiiCueSessionData> ClientSnapshots(FriendsLifeRuntime actual)
        {
            foreach(var episode in actual.State.episodes.Where(e=>e.sourceKind=="MiiEvent"&&!e.committed&&e.status=="SourceRunning"&&!string.IsNullOrEmpty(e.runnerJson)))
            {
                var state=JsonUtility.FromJson<OriginalGraphRunState>(episode.runnerJson);var bags=state?.bags?.Where(b=>b.id==state.episodeId+"/"+BagName).ToArray();if(bags==null||bags.Length==0)continue;if(bags.Length!=1)throw new OriginalBindingException("Ambiguous replicated source face bag");var value=bags[0].values.SingleOrDefault(v=>v.key=="payload")?.value;if(value?.kind!="String")throw new OriginalBindingException("Replicated source face payload absent");NativeMiiCueSession.ValidateRawEnvelope(value.text);var data=JsonUtility.FromJson<NativeMiiCueSessionData>(value.text);NativeMiiCueSession.Validate(data,actual.State.worldId,episode.episodeId);ValidateHostDomain(data);yield return data;
            }
        }
        void LateUpdate()
        {
            var actual=runtime?.Invoke();if(!actual||!actual.Ready){ReleaseVisuals();return;}var desired=new Dictionary<string,int>(StringComparer.Ordinal);
            try
            {
                var data=actual.IsAuthority?sessions.Values.Where(s=>s.data.worldId==actual.State.worldId&&actual.State.episodes.Any(e=>e.episodeId==s.data.episodeId&&!e.committed&&e.status=="SourceRunning")).Select(s=>s.data).ToArray():ClientSnapshots(actual).ToArray();
                foreach(var session in data)foreach(var actor in session.actors.Where(a=>a.selectedResolved&&a.state.slots[1].expression!=-1&&!string.IsNullOrEmpty(a.state.slots[1].owner)&&session.requests.Any(r=>r.targets.Any(t=>t.nativeActorId==a.nativeActorId&&t.phase=="Applied")))){if(desired.ContainsKey(actor.personId))throw new OriginalBindingException("Concurrent source face authorities for the same actual person");desired.Add(actor.personId,actor.state.selectedExpression);}
                foreach(string old in visuals.Keys.Where(k=>!desired.ContainsKey(k)).ToArray()){visuals[old].projection.Dispose();visuals.Remove(old);}
                foreach(var pair in desired)
                {
                    var body=actual.TransformOf(pair.Key);if(!body){LastProjectionError="Actual source face body unavailable";continue;}
                    if(!visuals.TryGetValue(pair.Key,out var visual)||visual.body!=body){if(visual!=null)visual.projection.Dispose();if(!NativeHostExpressionSubset.TryPrepare(body,pair.Value,out var projection,out var reason)){LastProjectionError=reason;continue;}visual=new Visual{body=body,projection=projection};visuals[pair.Key]=visual;}
                    if(!visual.projection.Apply(pair.Value,out var error)){LastProjectionError=error;visual.projection.Dispose();visuals.Remove(pair.Key);}
                }
            }catch(Exception e){LastProjectionError=e.Message;ReleaseVisuals();}
        }
        void ReleaseVisuals(){foreach(var visual in visuals.Values)visual.projection.Dispose();visuals.Clear();}
        void OnDisable()=>ReleaseVisuals();void OnDestroy()=>ReleaseVisuals();
    }
}
