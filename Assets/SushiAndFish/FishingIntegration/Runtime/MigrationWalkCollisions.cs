using System.Collections.Generic;
using System.Linq;
using HowToFish;
using PaperPixelGames.Core.Ocean;
using SDQQ1234;
using UnityEngine;

namespace SushiAndFish.FishingIntegration {
    public sealed partial class FishingMigration {
        sealed class WalkGuard {public FishingActor actor;public FishingWalkCollisionGuard guard;}
        readonly Dictionary<ulong,WalkGuard> walkGuards=new Dictionary<ulong,WalkGuard>();
        readonly Dictionary<GameObject,Collider[]> walkBodies=new Dictionary<GameObject,Collider[]>();
        float walkScanAt;
        internal void RefreshWalkContacts()=>TickWalkCollisions();
        internal void ProtectWalkBody(GameObject root,Collider[] known=null,bool refresh=false){
            if(!root)return;bool changed=false;
            if(known!=null){if(!walkBodies.TryGetValue(root,out var cached)||!ReferenceEquals(cached,known)){walkBodies[root]=known;changed=true;}}
            else if(refresh||!walkBodies.TryGetValue(root,out var old)||old.Length==0){walkBodies[root]=root.GetComponentsInChildren<Collider>(true);changed=true;}
            if(changed)foreach(var entry in walkGuards.Values)entry.guard.Protect(walkBodies[root]);
        }
        void TickWalkCollisions(){
            foreach(var key in walkGuards.Keys.ToArray())if(!actors.TryGetValue(key,out var actor)||!actor.player||actor!=walkGuards[key].actor){walkGuards[key].guard.Dispose();walkGuards.Remove(key);}
            foreach(var actor in actors.Values)if(actor.player&&!walkGuards.ContainsKey(actor.player.OwnerClientId)&&actor.player.FirstPersonCharacter&&actor.player.FirstPersonCharacter.characterMovement&&actor.player.FirstPersonCharacter.characterMovement.collider){
                // The original five-metre fish-school avoidance sphere is data
                // for CS_BoidsVFX, not the player's physical body. Keep its
                // radius and obstacle registration, but never let it push props.
                foreach(var obstacle in actor.player.GetComponentsInChildren<BoidsCollider>(true)){var collider=obstacle.GetComponent<Collider>();if(collider)collider.isTrigger=true;}
                walkGuards.Add(actor.player.OwnerClientId,new WalkGuard{actor=actor,guard=new FishingWalkCollisionGuard(actor.player.FirstPersonCharacter.characterMovement)});
            }
            if(Archipelago&&Archipelago.Boat)ProtectWalkBody(Archipelago.Boat.gameObject,Archipelago.Boat.hull);
            foreach(var fish in catches.Values)if(fish&&fish.IsAvailable&&fish.IsDead)ProtectWalkBody(fish.gameObject);
            foreach(var proxy in proxies)if(proxy.Value&&FishSnapshots.TryGetValue(proxy.Key,out var state)&&state.health<=0)ProtectWalkBody(proxy.Value);
            if(Time.unscaledTime>=walkScanAt){walkScanAt=Time.unscaledTime+.5f;
                foreach(var fish in FindObjectsByType<DeadFish>(FindObjectsSortMode.None))ProtectWalkBody(fish.gameObject,refresh:true);
                foreach(var key in walkBodies.Keys.ToArray())if(!key)walkBodies.Remove(key);
                foreach(var guard in walkGuards.Values)guard.guard.Prune();
            }
            // Re-apply pairs after collider disable/enable (held/dropped fish),
            // and include players that connected after a corpse was spawned.
            foreach(var body in walkBodies.Values)foreach(var guard in walkGuards.Values)guard.guard.Protect(body);
        }
        void ClearWalkCollisions(){foreach(var entry in walkGuards.Values)entry.guard.Dispose();walkGuards.Clear();walkBodies.Clear();walkScanAt=0;}
    }
}
