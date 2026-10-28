using System;
using System.Linq;
using System.Collections.Generic;
using HowToFish;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SushiAndFish.FishingIntegration {
    [Serializable] sealed class FishingCommand {public string action,target;public float value;public Vector3 position,forward;}
    [Serializable] public sealed class FishingSnapshot {
        public string token;public int species,health;public float weight;public bool shiny,reachedShore,attached,landing;
        public ulong fisher;public Vector3 position;public Quaternion rotation;public Vector3[] parts;public Quaternion[] turns;
    }
    [Serializable] public sealed class ActorSnapshot {public ulong player;public int equipment,health,ammo,capacity;public bool equipped,lineEnabled,aiming,reloading,charging;public float castPower,castRange;public string bait,focus;public Vector3 rodTip,hook;public Quaternion rodRotation;public Quaternion[] rodTurns;}
    [Serializable] sealed class WorldSnapshot {public SailingSnapshot boat;public FishingSnapshot[] fish;public ActorSnapshot[] actors;public string eventTitle,eventDescription;public float remaining,duration;public int eventKind;}
    [Serializable] sealed class ArchiveSnapshot {public FishingArchiveData archive;public WarehouseLine[] stock;public int weapons;}
    public sealed partial class FishingMigration {
        const string CommandChannel="sushi.fishing.command.v1",StateChannel="sushi.fishing.state.v1",ArchiveChannel="sushi.fishing.archive.v1",NoticeChannel="sushi.fishing.notice.v1";
        readonly Dictionary<ulong,float> nextCommand=new Dictionary<ulong,float>();
        public readonly Dictionary<string,FishingSnapshot> FishSnapshots=new Dictionary<string,FishingSnapshot>();
        public readonly Dictionary<ulong,ActorSnapshot> ActorSnapshots=new Dictionary<ulong,ActorSnapshot>();
        public WarehouseLine[] WarehouseLines{get;private set;}=Array.Empty<WarehouseLine>();
        public string EventTitle{get;private set;}
        public string EventDescription{get;private set;}
        public float EventRemaining{get;private set;}
        float archiveAt;
        void BindWire(){if(wireReady||!network)return;var wire=network.CustomMessagingManager;wire.RegisterNamedMessageHandler(CommandChannel,ReceiveCommand);wire.RegisterNamedMessageHandler(StateChannel,ReceiveState);wire.RegisterNamedMessageHandler(ArchiveChannel,ReceiveArchive);wire.RegisterNamedMessageHandler(NoticeChannel,ReceiveNotice);wire.RegisterNamedMessageHandler(ShotChannel,ReceiveShot);wire.RegisterNamedMessageHandler(NativeDeathChannel,ReceiveNativeDeath);wireReady=true;}
        void UnbindWire(){if(!wireReady||!network)return;var wire=network.CustomMessagingManager;if(wire!=null){wire.UnregisterNamedMessageHandler(CommandChannel);wire.UnregisterNamedMessageHandler(StateChannel);wire.UnregisterNamedMessageHandler(ArchiveChannel);wire.UnregisterNamedMessageHandler(NoticeChannel);wire.UnregisterNamedMessageHandler(ShotChannel);wire.UnregisterNamedMessageHandler(NativeDeathChannel);}wireReady=false;}
        void SendJson(string channel,ulong recipient,string json){if(!network||!network.IsListening)return;using(var writer=new FastBufferWriter(Mathf.Max(1024,json.Length*4+32),Allocator.Temp)){writer.WriteValueSafe(json);network.CustomMessagingManager.SendNamedMessage(channel,recipient,writer,NetworkDelivery.ReliableFragmentedSequenced);}}
        void Broadcast(string channel,string json){if(!Server)return;foreach(ulong client in network.ConnectedClientsIds)if(client!=network.LocalClientId)SendJson(channel,client,json);}
        public void SendCommand(FishingActor actor,string action,float value,string target){var command=new FishingCommand{action=action,value=value,target=target};if(Server)HandleCommand(actor,command);else SendJson(CommandChannel,NetworkManager.ServerClientId,JsonUtility.ToJson(command));}
        internal void SendAim(FishingActor actor,Vector3 position,Vector3 direction){var command=new FishingCommand{action="aim",position=position,forward=direction};if(Server)actor.Aim(position,direction);else SendJson(CommandChannel,NetworkManager.ServerClientId,JsonUtility.ToJson(command));}
        void ReceiveCommand(ulong sender,FastBufferReader reader){if(!Server||!actors.TryGetValue(sender,out var actor))return;reader.ReadValueSafe(out string json);if(json.Length>4096)return;try{HandleCommand(actor,JsonUtility.FromJson<FishingCommand>(json));}catch(Exception error){Debug.LogWarning("Fishing command rejected: "+error.Message);}}
        void HandleCommand(FishingActor actor,FishingCommand command){if(command==null)return;if(command.action!=null&&command.action.StartsWith("boat-",StringComparison.Ordinal)){if(actor.IsDefeated)return;if(Archipelago)Archipelago.Command(actor,command.action,command.position);return;}if(command.action=="aim"){actor.Aim(command.position,command.forward);return;}if(command.action!="control"&&command.action!="equip"&&command.action!="stow"&&command.action!="withdraw"&&command.action!="deposit"&&command.action!="restock"&&command.action!="buy-weapon"&&command.action!="native-harpoon"&&command.action!="native-recovered"&&command.action!="harvest"&&!actor.equipped)return;actor.Command(command.action,command.value,command.target);}
        void SendNotice(ulong player,string text){if(Server&&player!=network.LocalClientId)SendJson(NoticeChannel,player,text);}
        void ReceiveNotice(ulong sender,FastBufferReader reader){if(sender!=NetworkManager.ServerClientId)return;reader.ReadValueSafe(out string text);if(Local!=null){Local.message=text;Local.messageUntil=Time.unscaledTime+5;}}
        void PublishWorld(){
            foreach(var key in catches.Keys.ToArray())if(!catches[key]||!catches[key].IsAvailable)catches.Remove(key);
            var state=new WorldSnapshot{boat=Archipelago?Archipelago.Snapshot():null,fish=catches.Select(pair=>Snapshot(pair.Key,pair.Value)).ToArray(),actors=actors.Values.Where(a=>a.module).Select(ActorState).ToArray(),eventTitle=eventDirector?eventDirector.Title:"",eventDescription=eventDirector?eventDirector.Description:"",remaining=eventDirector?eventDirector.Remaining:0,duration=eventDirector?eventDirector.Duration:0,eventKind=eventDirector?(int)eventDirector.Current:0};
            ApplySnapshot(state);Broadcast(StateChannel,JsonUtility.ToJson(state));
            if(Time.unscaledTime>=archiveAt){archiveAt=Time.unscaledTime+1;WarehouseLines=SaveData.stock.GroupBy(s=>s.itemId).Select(g=>new WarehouseLine{itemId=g.Key,count=g.LongCount()}).ToArray();Broadcast(ArchiveChannel,JsonUtility.ToJson(new ArchiveSnapshot{archive=SaveData.archive,stock=WarehouseLines,weapons=SaveData.ownedWeapons}));}
        }
        ActorSnapshot ActorState(FishingActor actor){var module=actor.module;var gun=actor.equipment>=0?module.combat.guns[actor.equipment]:null;return new ActorSnapshot{player=actor.player.OwnerClientId,equipment=actor.equipment,equipped=actor.equipped,health=module.vitals.Health,ammo=gun?gun.Ammo:0,capacity=gun?gun.magazine:0,bait=module.BaitName,focus=module.DisplayCatch?module.DisplayCatch.GetComponent<MigratedCatchIdentity>()?.token:null,lineEnabled=module.line&&module.line.enabled&&actor.equipment<0,rodTip=module.WorldRodTip,hook=module.hookBody.position,rodRotation=module.rod.transform.localRotation,rodTurns=module.rod.segments.Select(s=>s.localRotation).ToArray(),aiming=gun&&gun.Aiming,reloading=gun&&gun.Reloading,charging=module.State==FishingState.Charging,castPower=module.CastPower,castRange=module.PlannedCastDistance};}
        FishingSnapshot Snapshot(string token,FishingCatch fish){var id=fish.GetComponent<MigratedCatchIdentity>();var bodies=fish.GetComponentsInChildren<Rigidbody>(true);return new FishingSnapshot{token=token,species=fish.definition.originalId,health=fish.Health,weight=fish.weight,shiny=fish.IsDrip,reachedShore=fish.HasReachedShore,attached=fish.Attached,landing=fish.fishing&&fish.fishing.IsLanding(fish),fisher=id?id.fisher:0,position=fish.transform.position,rotation=fish.transform.rotation,parts=bodies.Select(b=>b.transform.localPosition).ToArray(),turns=bodies.Select(b=>b.transform.localRotation).ToArray()};}
        void ReceiveState(ulong sender,FastBufferReader reader){if(sender!=NetworkManager.ServerClientId||Server)return;reader.ReadValueSafe(out string json);ApplySnapshot(JsonUtility.FromJson<WorldSnapshot>(json));}
        void ReceiveArchive(ulong sender,FastBufferReader reader){if(sender!=NetworkManager.ServerClientId||Server)return;reader.ReadValueSafe(out string json);var snapshot=JsonUtility.FromJson<ArchiveSnapshot>(json);if(snapshot?.archive!=null){SaveData.archive=snapshot.archive;SaveData.ownedWeapons=snapshot.weapons;WarehouseLines=snapshot.stock??Array.Empty<WarehouseLine>();}}
        void ApplySnapshot(WorldSnapshot state){
            if(state==null)return;pendingSailing=state.boat;if(Archipelago)Archipelago.ApplySnapshot(state.boat);FishSnapshots.Clear();ActorSnapshots.Clear();EventTitle=state.eventTitle;EventDescription=state.eventDescription;EventRemaining=state.remaining;ApplyReplicaEvent(state.eventKind,state.remaining,state.duration);
            foreach(var actor in state.actors??Array.Empty<ActorSnapshot>()){ActorSnapshots[actor.player]=actor;if(actors.TryGetValue(actor.player,out var view)){view.equipment=actor.equipment;view.equipped=actor.equipped&&!view.IsDefeated&&FishingMigration.UsesMigratedFishing(view.player);}}
            foreach(var fish in state.fish??Array.Empty<FishingSnapshot>()){
                FishSnapshots[fish.token]=fish;if(Server)continue;
                if(!proxies.TryGetValue(fish.token,out var go)){
                    var binding=Config.Find(fish.species);if(binding==null)continue;
                    go=Instantiate(binding.definition.catchPrefab.gameObject);go.name="Fishing catch replica "+fish.token;
                    foreach(var script in go.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
                    foreach(var joint in go.GetComponentsInChildren<Joint>(true))Destroy(joint);
                    foreach(var body in go.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.useGravity=false;}
                    var visual=go.GetComponent<FishingCatch>();visual.SetDrip(fish.shiny);
                    var identity=go.AddComponent<MigratedCatchIdentity>();identity.token=fish.token;identity.fisher=fish.fisher;proxies.Add(fish.token,go);
                }
                foreach(var collider in go.GetComponentsInChildren<Collider>(true))collider.enabled=!fish.attached||fish.landing;
                go.transform.SetPositionAndRotation(fish.position,fish.rotation);var parts=go.GetComponentsInChildren<Rigidbody>(true);
                for(int i=0;i<parts.Length&&i<fish.parts.Length;i++)if(parts[i].transform!=go.transform){parts[i].transform.localPosition=fish.parts[i];parts[i].transform.localRotation=fish.turns[i];}
            }
            foreach(var token in proxies.Keys.ToArray())if(!FishSnapshots.ContainsKey(token)){Destroy(proxies[token]);proxies.Remove(token);}
        }
    }
}
