using System;
using System.Collections.Generic;
using System.Linq;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    [Serializable] public sealed class NativeSourceActorRecord
    {
        public string nativeResidentId;
        public int slot,handleToken,handleIndex;
        public bool registered=true;
        public NativeCooldownOwner cooldown=new NativeCooldownOwner();
        public int asModuleVersion;
        public NativeDateActorAsState animation;
    }
    [Serializable] public sealed class NativeSourceTaskParticipant
    {
        public string nativeResidentId;
        public int slot,handleToken,handleIndex;
    }
    [Serializable] public sealed class NativeSourceTaskRecord
    {
        public string taskId,sourceKind,sourceId;
        public bool ended;
        public long startedUserPosixSeconds,endedUserPosixSeconds;
        public bool startClockAvailable,endClockAvailable;
        public List<string> ownerNativeIds=new List<string>();
        public List<NativeSourceTaskParticipant> participants=new List<NativeSourceTaskParticipant>();
    }
    [Serializable] public sealed class NativeSourceContextData
    {
        public int schema=1,nextHandleToken=1;
        public int moduleVersion;
        public string moduleId,worldId;
        public List<NativeSourceActorRecord> actors=new List<NativeSourceActorRecord>();
        public List<NativeSourceTaskRecord> tasks=new List<NativeSourceTaskRecord>();
    }
    // Adapter identity/task receipts only. NativeCooldownContext retains the
    // original arithmetic and70-slot owner. No event, RNG or clock is advanced.
    public sealed class NativeSourceContextLedger
    {
        public NativeSourceContextData Data{get;private set;}
        public NativeSourceContextLedger(string worldId,NativeSourceContextData restored=null)
        {
            if(string.IsNullOrEmpty(worldId))throw new OriginalBindingException("Native source context requires an actual world identity");
            Data=restored??new NativeSourceContextData{worldId=worldId,moduleVersion=1,moduleId="NativeGroupCooldown"};
            if(Data.schema!=1||Data.moduleVersion!=1||Data.moduleId!="NativeGroupCooldown"||Data.worldId!=worldId||Data.actors==null||Data.tasks==null)throw new OriginalBindingException("Native source context belongs to another world/module/schema");
            if(Data.actors.Any(a=>a==null||string.IsNullOrEmpty(a.nativeResidentId)||a.slot<0||a.slot>=NativeCooldownContext.SlotCount)||
                Data.actors.GroupBy(a=>a.nativeResidentId,StringComparer.Ordinal).Any(g=>g.Count()!=1)||
                Data.actors.Where(a=>a.registered).GroupBy(a=>a.slot).Any(g=>g.Count()!=1)||
                Data.tasks.Any(t=>t==null||string.IsNullOrEmpty(t.taskId)||t.ownerNativeIds==null||t.participants==null)||
                Data.tasks.GroupBy(t=>t.taskId,StringComparer.Ordinal).Any(g=>g.Count()!=1))throw new OriginalBindingException("Native source context contains ambiguous actor/task identities");
            foreach(var actor in Data.actors)
            {
                NativeCooldownContext.Validate(actor.cooldown);
                if(actor.asModuleVersion==0){if(actor.animation!=null)throw new OriginalBindingException("Actor AS data has no declared module version");}
                else if(actor.asModuleVersion==1)NativeDateAsRuntime.Validate(actor.animation);
                else throw new OriginalBindingException("Unknown saved actor AS module version");
            }
            foreach(var task in Data.tasks)
            {
                if(task.ownerNativeIds.Count<1||task.ownerNativeIds.Count>11||task.ownerNativeIds.Distinct(StringComparer.Ordinal).Count()!=task.ownerNativeIds.Count||task.participants.Count<1||task.participants.Count>10||
                    task.ownerNativeIds.Any(id=>!Data.actors.Any(a=>a.nativeResidentId==id&&(task.ended||a.registered)))||
                    task.participants.Any(p=>p==null||!Data.actors.Any(a=>a.nativeResidentId==p.nativeResidentId&&a.slot==p.slot&&a.handleIndex==p.handleIndex&&a.handleToken==p.handleToken&&(task.ended||a.registered))))
                    throw new OriginalBindingException("Native source task receipt does not match its original bound actor identities");
            }
        }
        public NativeSourceActorRecord Actor(string nativeId)=>Data.actors.FirstOrDefault(a=>a.nativeResidentId==nativeId&&a.registered);
        public bool Register(string nativeId)
        {
            if(string.IsNullOrEmpty(nativeId))throw new OriginalBindingException("Native actor identity is absent");
            var old=Data.actors.FirstOrDefault(a=>a.nativeResidentId==nativeId);
            if(old!=null){if(!old.registered)throw new OriginalBindingException("A removed native identity cannot be silently revived");return false;}
            int slot=Enumerable.Range(0,NativeCooldownContext.SlotCount).FirstOrDefault(i=>Data.actors.All(a=>!a.registered||a.slot!=i));
            if(Data.actors.Any(a=>a.registered&&a.slot==slot))throw new OriginalBindingException("Original70 Mii actor slots are full");
            int token=Data.nextHandleToken;Data.nextHandleToken=unchecked(token+1);if(Data.nextHandleToken==0)Data.nextHandleToken=1;
            Data.actors.Add(new NativeSourceActorRecord{nativeResidentId=nativeId,slot=slot,handleIndex=slot,handleToken=token});return true;
        }
        public bool Begin(string taskId,string sourceKind,string sourceId,IReadOnlyList<string> acceptedOwnerNativeIds,IReadOnlyList<string> originalParticipantNativeIds,long? currentUserPosixSeconds)
        {
            if(string.IsNullOrEmpty(taskId)||acceptedOwnerNativeIds==null||originalParticipantNativeIds==null)throw new OriginalBindingException("Original source task identity/participants are absent");
            // Original TaskDef/record capacity10; wrappers are unique owners.
            // Preserve duplicates and order inside P, including the owner itself.
            var participants=originalParticipantNativeIds.Take(10).ToArray();var owners=acceptedOwnerNativeIds.Distinct(StringComparer.Ordinal).ToArray();
            if(participants.Length==0||owners.Length==0||owners.Length>11)throw new OriginalBindingException("Source task has no accepted owner/participants or exceeds its wrapper capacity");
            var existing=Data.tasks.FirstOrDefault(t=>t.taskId==taskId);
            if(existing!=null)
            {
                if(existing.sourceKind!=sourceKind||existing.sourceId!=sourceId||!existing.ownerNativeIds.SequenceEqual(owners)||!existing.participants.Select(p=>p.nativeResidentId).SequenceEqual(participants))
                    throw new OriginalBindingException("Source task replay changed its owners/participant definition: "+taskId);
                return false; // Already active or ended; never acquire twice.
            }
            foreach(string id in participants.Concat(owners))if(Actor(id)==null)throw new OriginalBindingException("Source task actor is not registered: "+id);
            var record=new NativeSourceTaskRecord{taskId=taskId,sourceKind=sourceKind,sourceId=sourceId,startedUserPosixSeconds=currentUserPosixSeconds??0,startClockAvailable=currentUserPosixSeconds.HasValue,ownerNativeIds=owners.ToList(),participants=participants.Select(id=>
            {var actor=Actor(id);return new NativeSourceTaskParticipant{nativeResidentId=id,slot=actor.slot,handleIndex=actor.handleIndex,handleToken=actor.handleToken};}).ToList()};
            foreach(string id in owners)foreach(var participant in record.participants)
                if(!NativeCooldownContext.Acquire(Actor(id).cooldown,(uint)participant.slot,participant.handleToken,participant.handleIndex,currentUserPosixSeconds))throw new OriginalBindingException("Validated source actor slot was rejected");
            Data.tasks.Add(record);return true;
        }
        public bool End(string taskId,long? currentUserPosixSeconds)
        {
            var record=Data.tasks.FirstOrDefault(t=>t.taskId==taskId);if(record==null||record.ended)return false;
            foreach(string id in record.ownerNativeIds)
            {
                var owner=Data.actors.FirstOrDefault(a=>a.nativeResidentId==id);if(owner==null)throw new OriginalBindingException("Source task owner disappeared before its real end: "+id);
                foreach(var participant in record.participants)
                    if(!NativeCooldownContext.Release(owner.cooldown,(uint)participant.slot,participant.handleToken,participant.handleIndex,currentUserPosixSeconds))throw new OriginalBindingException("Stored original source slot was rejected");
            }
            record.ended=true;record.endedUserPosixSeconds=currentUserPosixSeconds??0;record.endClockAvailable=currentUserPosixSeconds.HasValue;return true;
        }
        public bool? Ready(string nativeId,bool nativeEventSystemAvailable,long? currentUserPosixSeconds)
        {var actor=Actor(nativeId);return actor==null?(bool?)null:NativeCooldownContext.Ready(actor.cooldown,nativeEventSystemAvailable,currentUserPosixSeconds);}
        public bool Retire(string nativeId)
        {
            var actor=Actor(nativeId);if(actor==null)return false;
            if(Data.tasks.Any(t=>!t.ended&&(t.ownerNativeIds.Contains(nativeId)||t.participants.Any(p=>p.nativeResidentId==nativeId))))throw new OriginalBindingException("End the actual source task before retiring/reusing its actor slot");
            actor.registered=false;return true;
        }
    }
}
