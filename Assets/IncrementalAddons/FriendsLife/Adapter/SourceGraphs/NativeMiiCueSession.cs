using System;using System.Collections.Generic;using System.Linq;using Newtonsoft.Json.Linq;
namespace SushiAndFish.FriendsLife.SourceGraphs
{
    [Serializable]public sealed class NativeMiiCueEndActor
    {public string personId,nativeActorId;public int handleToken,handleIndex;}
    [Serializable]public sealed class NativeMiiCueEndData
    {public int schema=1;public string worldId,executionKey,phase="Captured";public List<NativeMiiCueEndActor> targets=new List<NativeMiiCueEndActor>();}
    [Serializable]public sealed class NativeMiiCueCaptureData
    {public int schema=1,rawExpression;public string worldId,executionKey,sourceAddress;public bool ignoreHabit;public List<NativeMiiCueEndActor> targets=new List<NativeMiiCueEndActor>();}
    [Serializable]public sealed class NativeMiiCueActor
    {public string personId,nativeActorId;public int handleToken,handleIndex;public NativeMiiFacialState state;public bool selectedResolved;public string bindingFault="";}
    [Serializable]public sealed class NativeMiiCueTarget
    {public string personId,nativeActorId,leafOwner,phase="Queued";public int handleToken,handleIndex;public bool leafCreated;public NativeMiiExpressionLeaf leaf=new NativeMiiExpressionLeaf{owner="",rawExpression=-1};}
    [Serializable]public sealed class NativeMiiCueRequest
    {public string requestId,sourceAddress,kind="MiiExpression";public int rawExpression;public bool ignoreHabit,isCue=true;public long sequence;public List<NativeMiiCueTarget> targets=new List<NativeMiiCueTarget>();}
    [Serializable]public sealed class NativeMiiCueSessionData
    {public int schema=1;public string worldId,episodeId;public long nextSequence=1;public List<NativeMiiCueActor> actors=new List<NativeMiiCueActor>();public List<NativeMiiCueRequest> requests=new List<NativeMiiCueRequest>();}
    public static class NativeMiiCueSession
    {
        public static void Validate(NativeMiiCueSessionData data,string world,string episode)
        {
            if(data==null||string.IsNullOrEmpty(world)||string.IsNullOrEmpty(episode)||data.schema!=1||data.worldId!=world||data.episodeId!=episode||data.nextSequence<1||data.actors==null||data.requests==null||data.actors.Any(a=>a==null||string.IsNullOrEmpty(a.personId)||string.IsNullOrEmpty(a.nativeActorId)||a.handleIndex<0||a.handleIndex>=70)||data.actors.Any(a=>a.bindingFault==null)||data.actors.GroupBy(a=>a.nativeActorId).Any(g=>g.Count()!=1)||data.actors.GroupBy(a=>a.personId).Any(g=>g.Count()!=1)||data.actors.GroupBy(a=>a.handleToken+"/"+a.handleIndex).Any(g=>g.Count()!=1)||data.requests.Any(r=>r==null||string.IsNullOrEmpty(r.requestId)||string.IsNullOrEmpty(r.sourceAddress)||r.sequence<1||r.sequence>=data.nextSequence||r.kind!="MiiExpression"||!r.isCue||r.rawExpression< -1||r.rawExpression>=75||r.targets==null)||data.requests.GroupBy(r=>r.requestId).Any(g=>g.Count()!=1)||data.requests.GroupBy(r=>r.sequence).Any(g=>g.Count()!=1))throw new InvalidOperationException("Malformed/world-mismatched native expression cue session");
            foreach(var a in data.actors){NativeMiiFacialOwner.Validate(a.state);if(a.state.worldId!=world||a.state.nativeActorId!=a.nativeActorId||a.state.slots.Any(slot=>slot.owner==null)||(a.state.slots[1].expression==-1)!=string.IsNullOrEmpty(a.state.slots[1].owner))throw new InvalidOperationException("Native face state owner mismatch");}
            var owners=new HashSet<string>(StringComparer.Ordinal);foreach(var r in data.requests)foreach(var t in r.targets){if(t==null||t.phase!="Queued"&&t.phase!="Applied"&&t.phase!="Ended")throw new InvalidOperationException("Unknown source cue target phase");var a=data.actors.SingleOrDefault(x=>x.nativeActorId==t.nativeActorId&&x.personId==t.personId&&x.handleToken==t.handleToken&&x.handleIndex==t.handleIndex);if(a==null||string.IsNullOrEmpty(t.leafOwner)||!owners.Add(t.leafOwner)||t.leaf==null||t.leaf.owner==null||t.phase=="Queued"&&t.leafCreated||t.phase=="Applied"&&!t.leafCreated||t.leafCreated&&(t.leaf.owner!=t.leafOwner||t.leaf.rawExpression!=r.rawExpression||t.leaf.ignoreHabit!=r.ignoreHabit||t.leaf.ended!=(t.phase=="Ended"))||!t.leafCreated&&(t.leaf.owner!=""||t.leaf.rawExpression!=-1||t.leaf.ignoreHabit||t.leaf.ended))throw new InvalidOperationException("Unknown/mismatched cached source cue target");}
            foreach(var a in data.actors)if(!string.IsNullOrEmpty(a.state.slots[1].owner)&&!data.requests.Any(r=>r.targets.Any(t=>t.nativeActorId==a.nativeActorId&&t.phase=="Applied"&&t.leafCreated&&t.leafOwner==a.state.slots[1].owner&&r.rawExpression==a.state.slots[1].expression&&r.ignoreHabit==a.state.slots[1].ignoreHabit)))throw new InvalidOperationException("Source Cue lane has no matching current leaf owner");
        }
        static void EndTarget(NativeMiiCueSessionData data,NativeMiiCueTarget target)
        {
            if(target.phase=="Ended")return;var actor=data.actors.Single(a=>a.nativeActorId==target.nativeActorId);if(target.leafCreated)NativeMiiFacialOwner.ClearOwnedCueLeafRaw(actor.state,target.leaf);target.phase="Ended";actor.selectedResolved=false;
        }
        static bool Same(NativeMiiCueActor a,NativeMiiCueActor b)=>a.personId==b.personId&&a.nativeActorId==b.nativeActorId&&a.handleToken==b.handleToken&&a.handleIndex==b.handleIndex;
        public static void ValidateQueue(NativeMiiCueSessionData data,string id,string address,int raw,IReadOnlyList<NativeMiiCueActor> actualOrderedActors)
        {
            Validate(data,data.worldId,data.episodeId);
            if(string.IsNullOrEmpty(id)||string.IsNullOrEmpty(address)||raw< -1||raw>=75||actualOrderedActors==null||actualOrderedActors.Any(a=>a==null||a.handleIndex<0||a.handleIndex>=70||string.IsNullOrEmpty(a.personId)||string.IsNullOrEmpty(a.nativeActorId)))throw new InvalidOperationException("Actual ordered source cue arguments unavailable");
            if(data.nextSequence==long.MaxValue)throw new InvalidOperationException("Native cue stable identity sequence exhausted");
            foreach(var actual in actualOrderedActors){var matches=data.actors.Where(a=>a.nativeActorId==actual.nativeActorId||a.personId==actual.personId||a.handleToken==actual.handleToken&&a.handleIndex==actual.handleIndex);if(matches.Any(a=>!Same(a,actual)))throw new InvalidOperationException("Source actor handle changed without an actual restored owner");}
            if(actualOrderedActors.GroupBy(a=>a.nativeActorId).Any(g=>g.Any(a=>!Same(a,g.First())))||actualOrderedActors.GroupBy(a=>a.personId).Any(g=>g.Any(a=>!Same(a,g.First())))||actualOrderedActors.GroupBy(a=>a.handleToken+"/"+a.handleIndex).Any(g=>g.Any(a=>!Same(a,g.First()))))throw new InvalidOperationException("Ambiguous original actor handle list");
        }
        public static NativeMiiCueRequest Queue(NativeMiiCueSessionData data,string id,string address,int raw,bool ignore,IReadOnlyList<NativeMiiCueActor> actualOrderedActors)
        {
            Validate(data,data.worldId,data.episodeId);var existing=data.requests.SingleOrDefault(r=>r.requestId==id);if(existing!=null)return existing;ValidateQueue(data,id,address,raw,actualOrderedActors);
            // Native708ef0/709008 removes matching existing actor+kind Cue
            // requests BEFORE adding the new group. Direction is not matched.
            foreach(var old in data.requests)foreach(var target in old.targets)if(actualOrderedActors.Any(a=>a.handleToken==target.handleToken&&a.handleIndex==target.handleIndex)&&old.kind=="MiiExpression"&&old.isCue)EndTarget(data,target);
            var request=new NativeMiiCueRequest{requestId=id,sourceAddress=address,rawExpression=raw,ignoreHabit=ignore,sequence=data.nextSequence++};int index=0;
            foreach(var actual in actualOrderedActors)
            {
                var saved=data.actors.SingleOrDefault(a=>a.nativeActorId==actual.nativeActorId);if(saved!=null&&(saved.personId!=actual.personId||saved.handleToken!=actual.handleToken||saved.handleIndex!=actual.handleIndex))throw new InvalidOperationException("Source actor handle changed without an actual restored owner");if(saved==null){saved=new NativeMiiCueActor{personId=actual.personId,nativeActorId=actual.nativeActorId,handleToken=actual.handleToken,handleIndex=actual.handleIndex,state=NativeMiiFacialOwner.Construct(data.worldId,actual.nativeActorId)};data.actors.Add(saved);}
                request.targets.Add(new NativeMiiCueTarget{personId=actual.personId,nativeActorId=actual.nativeActorId,handleToken=actual.handleToken,handleIndex=actual.handleIndex,leafOwner=id+"/actor-row:"+index++});
            }
            data.requests.Add(request);Validate(data,data.worldId,data.episodeId);return request;
        }
        public static void ApplyQueuedAtActorBoundary(NativeMiiCueSessionData data)
        {
            Validate(data,data.worldId,data.episodeId);foreach(var r in data.requests.OrderBy(r=>r.sequence))foreach(var t in r.targets.Where(t=>t.phase=="Queued")){var a=data.actors.Single(a=>a.nativeActorId==t.nativeActorId);t.leaf=NativeMiiFacialOwner.EnterCueLeaf(a.state,t.leafOwner,r.rawExpression,r.ignoreHabit);t.leafCreated=true;t.phase="Applied";a.selectedResolved=false;}
        }
        public static int End(NativeMiiCueSessionData data,IReadOnlyList<NativeMiiCueActor> actualTargets)
        {
            Validate(data,data.worldId,data.episodeId);if(actualTargets==null||actualTargets.Any(a=>a==null||string.IsNullOrEmpty(a.personId)||string.IsNullOrEmpty(a.nativeActorId)||a.handleIndex<0||a.handleIndex>=70))throw new InvalidOperationException("Actual CueEnd target handles required");int count=0;foreach(var r in data.requests.Where(r=>r.kind=="MiiExpression"&&r.isCue))foreach(var t in r.targets)if(t.phase!="Ended"&&actualTargets.Any(a=>NativeMiiFacialOwner.MatchesCueEnd(a.handleToken,a.handleIndex,1,t.handleToken,t.handleIndex,1,r.isCue))){EndTarget(data,t);count++;}return count;
        }
        public static void EndAllOwned(NativeMiiCueSessionData data){Validate(data,data.worldId,data.episodeId);foreach(var r in data.requests)foreach(var t in r.targets)EndTarget(data,t);}
        static JObject Object(JToken token){if(!(token is JObject result))throw new InvalidOperationException("Native Cue serialized object missing");return result;}
        static void Fields(JObject raw,JTokenType kind,params string[] names){foreach(string name in names)if(raw[name]?.Type!=kind)throw new InvalidOperationException("Native Cue mandatory raw field missing: "+name);}
        public static void ValidateEnd(NativeMiiCueEndData data,string world,string executionKey)
        {
            if(data==null||data.schema!=1||string.IsNullOrEmpty(world)||string.IsNullOrEmpty(executionKey)||data.worldId!=world||data.executionKey!=executionKey||data.phase!="Captured"&&data.phase!="Applied"||data.targets==null||data.targets.Any(a=>a==null||string.IsNullOrEmpty(a.personId)||string.IsNullOrEmpty(a.nativeActorId)||a.handleIndex<0||a.handleIndex>=70))throw new InvalidOperationException("Malformed/foreign source CueEnd continuation");
        }
        public static void ValidateEndRawEnvelope(string json)
        {
            var raw=JObject.Parse(json);Fields(raw,JTokenType.Integer,"schema");Fields(raw,JTokenType.String,"worldId","executionKey","phase");Fields(raw,JTokenType.Array,"targets");foreach(var item in raw["targets"]){var actor=Object(item);Fields(actor,JTokenType.String,"personId","nativeActorId");Fields(actor,JTokenType.Integer,"handleToken","handleIndex");}
        }
        public static void ValidateCapture(NativeMiiCueCaptureData data,string world,string executionKey,string sourceAddress)
        {
            if(data==null||data.schema!=1||string.IsNullOrEmpty(world)||string.IsNullOrEmpty(executionKey)||string.IsNullOrEmpty(sourceAddress)||data.worldId!=world||data.executionKey!=executionKey||data.sourceAddress!=sourceAddress||data.rawExpression< -1||data.rawExpression>=75||data.targets==null||data.targets.Any(a=>a==null||string.IsNullOrEmpty(a.personId)||string.IsNullOrEmpty(a.nativeActorId)||a.handleIndex<0||a.handleIndex>=70))throw new InvalidOperationException("Malformed/foreign raw source Cue capture");
        }
        public static void ValidateCaptureRawEnvelope(string json)
        {
            var raw=JObject.Parse(json);Fields(raw,JTokenType.Integer,"schema","rawExpression");Fields(raw,JTokenType.String,"worldId","executionKey","sourceAddress");Fields(raw,JTokenType.Boolean,"ignoreHabit");Fields(raw,JTokenType.Array,"targets");foreach(var item in raw["targets"]){var actor=Object(item);Fields(actor,JTokenType.String,"personId","nativeActorId");Fields(actor,JTokenType.Integer,"handleToken","handleIndex");}
        }
        public static void ValidateRawEnvelope(string json)
        {
            var raw=JObject.Parse(json);Fields(raw,JTokenType.Integer,"schema","nextSequence");Fields(raw,JTokenType.String,"worldId","episodeId");Fields(raw,JTokenType.Array,"actors","requests");if((int)raw["schema"]!=1)throw new InvalidOperationException("Native Cue unknown raw schema");
            foreach(var token in raw["actors"]){var a=Object(token);Fields(a,JTokenType.String,"personId","nativeActorId","bindingFault");Fields(a,JTokenType.Integer,"handleToken","handleIndex");Fields(a,JTokenType.Boolean,"selectedResolved");var state=Object(a["state"]);Fields(state,JTokenType.Integer,"schema","selectedExpression","selectedPriority");Fields(state,JTokenType.String,"worldId","nativeActorId");Fields(state,JTokenType.Array,"slots");foreach(var item in state["slots"]){var slot=Object(item);Fields(slot,JTokenType.Integer,"expression");Fields(slot,JTokenType.String,"owner");Fields(slot,JTokenType.Boolean,"ignoreHabit");}}
            foreach(var token in raw["requests"]){var request=Object(token);Fields(request,JTokenType.String,"requestId","sourceAddress","kind");Fields(request,JTokenType.Integer,"rawExpression","sequence");Fields(request,JTokenType.Boolean,"ignoreHabit","isCue");Fields(request,JTokenType.Array,"targets");foreach(var item in request["targets"]){var target=Object(item);Fields(target,JTokenType.String,"personId","nativeActorId","leafOwner","phase");Fields(target,JTokenType.Integer,"handleToken","handleIndex");Fields(target,JTokenType.Boolean,"leafCreated");var leaf=Object(target["leaf"]);Fields(leaf,JTokenType.String,"owner");Fields(leaf,JTokenType.Integer,"rawExpression");Fields(leaf,JTokenType.Boolean,"ignoreHabit","ended");}}
        }
    }
}
