using System;
using System.Collections.Generic;
using HowToFish;
using PaperPixelGames.Core.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SushiAndFish.FishingIntegration {
    [Serializable] sealed class ShotPresentation {public Vector3 position,velocity,normal;public ulong actor;public int weapon,kind;public bool impact,segment;}
    public sealed partial class FishingMigration {
        const string ShotChannel="sushi.fishing.shots.v1";
        FishingShotEffects remoteEffects;
        readonly Dictionary<string,float> nextFishContact=new Dictionary<string,float>();
        internal void BindShotEvents(FishingActor actor){
            actor.module.combat.ProjectileFired+=(gun,position,velocity)=>{if(!Server)return;Broadcast(ShotChannel,JsonUtility.ToJson(new ShotPresentation{actor=actor.player.OwnerClientId,weapon=Array.IndexOf(actor.module.combat.guns,gun),position=position,velocity=velocity}));};
            actor.module.combat.ProjectileSegment+=(start,end)=>{if(Server)Broadcast(ShotChannel,JsonUtility.ToJson(new ShotPresentation{position=start,velocity=end,segment=true}));};
            actor.module.combat.ProjectileImpact+=(position,normal,kind)=>{if(Server)Broadcast(ShotChannel,JsonUtility.ToJson(new ShotPresentation{position=position,normal=normal,kind=kind,impact=true}));};
        }
        void ReceiveShot(ulong sender,FastBufferReader reader){
            if(sender!=NetworkManager.ServerClientId||Server)return;reader.ReadValueSafe(out string json);var shot=JsonUtility.FromJson<ShotPresentation>(json);if(shot==null)return;
            if(!remoteEffects){var go=new GameObject("Fishing shot presentation");go.transform.SetParent(transform);remoteEffects=go.AddComponent<FishingShotEffects>();var original=Config.systemPrefab.GetComponent<FishingShotEffects>();JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(original),remoteEffects);}
            if(shot.segment){remoteEffects.TraceSegment(shot.position,shot.velocity);return;}
            if(shot.impact)remoteEffects.Impact(shot.position,shot.normal,(FishingShotEffects.HitKind)shot.kind,null);
            else{
                if(actors.TryGetValue(shot.actor,out var actor))actor.PlayShot();
                remoteEffects.TraceSegment(shot.position,shot.position+shot.velocity*.025f);
                var guns=Config.systemPrefab.GetComponent<FishCombat>().guns;int index=Mathf.Clamp(shot.weapon,0,guns.Length-1);var clips=guns[index].fireSounds;
                if(clips!=null&&clips.Length>0)AudioSource.PlayClipAtPoint(clips[UnityEngine.Random.Range(0,clips.Length)],shot.position,.45f);
            }
        }
        internal bool HandleFishPlayerContact(FishingCatch fish,Collider collider){
            var player=collider.GetComponentInParent<PPG_Player>();if(!player)return false;
            if(collider.isTrigger||collider.GetComponent<SDQQ1234.BoidsCollider>())return true;
            if(!Server||!fish.IsAttackingPlayer||fish.IsDead||!fish.definition.ShoreAttackEligible||!actors.TryGetValue(player.OwnerClientId,out var victim)||!victim.module||victim.IsDefeated)return true;
            var identity=fish.GetComponent<MigratedCatchIdentity>();string key=(identity?identity.token:fish.GetInstanceID().ToString())+"_"+player.OwnerClientId;
            if(nextFishContact.TryGetValue(key,out var time)&&Time.time<time)return true;
            nextFishContact[key]=Time.time+fish.definition.shore.contactCooldown;victim.module.vitals.Hit(fish.definition.shore.attackDamage,fish.definition.displayName);return true;
        }
    }
}
