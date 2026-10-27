using System;
using System.IO;
using HowToFish;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;
using StylizedWater3;

namespace SushiAndFish.FishingIntegration {
    // Per local player and host/slot/player identity, independent of ES3 schemas.
    // Runs after native movement, boat passenger movement and viewmodel updates.
    [DefaultExecutionOrder(15000)]
    public sealed class PlayerResumeController:MonoBehaviour {
        static PlayerResumeController local;
        PPG_Player player;PlayerResumeData pose,pending;string key,path,epoch,host;int slot;ulong steam;
        bool initialised,ready,protectedFile,closing,dirty,startedProbe;float nextWrite,nextProbe,restoreDeadline;Vector3 initialSpawn;
        static string Root=>Path.Combine(Application.persistentDataPath,"PlayerPositions");
        static ArchipelagoWorld Islands=>FishingMigration.Instance?FishingMigration.Instance.Archipelago:null;
        static float Sea=>FishingMigration.Instance?FishingMigration.Instance.SeaHeight:OceanFollowBehaviour.Instance?OceanFollowBehaviour.Instance.transform.position.y:-4;
        public static void Ensure(PPG_Player player){if(!Application.isPlaying||!player||!player.IsOwner)return;var controller=player.GetComponent<PlayerResumeController>();if(!controller)controller=player.gameObject.AddComponent<PlayerResumeController>();controller.player=player;local=controller;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetStatics(){local=null;}
        bool Context(out string owner,out int index,out ulong identity){
            owner=null;index=-1;identity=player?player.SteamID.Value:0;var manager=Singleton<SaveManager>.Instance;if(!manager)return false;
            if(NetworkManager.Singleton&&NetworkManager.Singleton.IsServer&&manager.HasValidCurrentSlot){owner=manager.CurrentSaveSlotHostSteamID;index=manager.CurrentSaveSlotIndex;}
            else if(!manager.TryResolveCurrentClientProfileContext(out owner,out index))return false;
            return PlayerResumeFiles.ValidIdentity(owner,index,identity);
        }
        void LateUpdate(){
            if(closing||!player||!player.IsOwner||!player.IsSpawned||!player.IsLocalGameplayPreparationReady||!Context(out var owner,out var index,out var identity))return;
            var current=PlayerResumeFiles.Key(owner,index,identity);
            if(!initialised||current!=key){Flush();key=current;host=owner;slot=index;steam=identity;path=PlayerResumeFiles.PathFor(Root,host,slot,steam);initialSpawn=player.transform.position;restoreDeadline=Time.unscaledTime+12;nextProbe=0;
                try{epoch=PlayerResumeFiles.Epoch(Root,host,slot);pending=PlayerResumeFiles.Read(Root,host,slot,steam,out protectedFile);}catch(Exception error){protectedFile=true;Debug.LogWarning("Player resume context could not be read: "+error.Message);}
                pose=null;initialised=true;ready=false;dirty=false;startedProbe=false;
            }
            if(protectedFile)return;
            if(!ready){
                if(FishingMigration.Enabled){var settings=ArchipelagoSettings.Load();if(settings&&settings.enabled&&!Islands)return;}
                if(player.IsTrapped||player.FirstPersonCharacter.HasActiveMotion||player.FirstPersonCharacter.IsPaused())return;
                if(!startedProbe){initialSpawn=player.transform.position;restoreDeadline=Time.unscaledTime+12;startedProbe=true;}
                if(Time.unscaledTime<nextProbe)return;nextProbe=Time.unscaledTime+.2f;
                if(pending!=null){
                    Physics.SyncTransforms();var character=player.FirstPersonCharacter;var movement=character.characterMovement;
                    bool moved=(player.transform.position-initialSpawn).sqrMagnitude>1;
                    if(!moved&&TryDestination(pending,Islands?Islands.Boat:null,Sea,movement.radius,movement.height,player.transform,out var destination)){
                        player.TeleportPlayer(destination,pending.rotation,true);character.SetVelocity(Vector3.zero);character.PitchInstant(pending.pitch);
                        if(pending.swimming&&destination.y<Sea+.5f)character.SetMovementMode(ECM2.Character.MovementMode.Swimming);
                        FishingMigration.Instance?.RefreshWalkContacts();
                    }else if(!moved&&Time.unscaledTime<restoreDeadline)return;
                    else Debug.LogWarning("Previous player location is unavailable; keeping the native safe spawn/current position.");
                }
                pending=null;ready=true;
            }
            Capture();if(Time.unscaledTime>=nextWrite){nextWrite=Time.unscaledTime+2;Flush();}
        }
        void Capture(){
            if(!ready||protectedFile||!player||!player.IsSpawned||!player.IsLocalGameplayPreparationReady||player.DivingEquipment==null||player.DivingEquipment.IsKnocked)return;
            var character=player.FirstPersonCharacter;bool aboard=Islands&&Islands.Boat&&(Islands.IsDriver(player)||Islands.OnDeck(player));
            if((player.IsTrapped&&!aboard)||character.HasActiveMotion||!PlayerResumeFiles.Finite(player.transform.position))return;
            if(pose==null)pose=new PlayerResumeData{host=host,slot=slot,steam=steam,epoch=epoch,valid=true};
            var position=player.transform.position;var rotation=Quaternion.Euler(0,player.transform.eulerAngles.y,0);float pitch=character.CameraPitch;var offset=aboard?Islands.Boat.transform.InverseTransformPoint(position):Vector3.zero;
            bool swimming=character.IsSwimming()||character.IsSeaScooter();dirty|=pose.position!=position||pose.rotation!=rotation||pose.pitch!=pitch||pose.onBoat!=aboard||pose.swimming!=swimming||pose.boatLocalPosition!=offset||pose.updatedUtc==null;
            pose.position=position;pose.rotation=rotation;pose.pitch=pitch;pose.swimming=swimming;pose.onBoat=aboard;pose.boatLocalPosition=offset;
        }
        void Flush(){
            if(!dirty||protectedFile||pose==null||string.IsNullOrEmpty(path))return;
            try{if((epoch??"")!=PlayerResumeFiles.Epoch(Root,host,slot))return;pose.updatedUtc=DateTime.UtcNow.ToString("O");PlayerResumeFiles.WriteAtomic(path,JsonUtility.ToJson(pose));dirty=false;}
            catch(Exception error){Debug.LogWarning("Player position save failed; previous file retained: "+error.Message);}
        }
        public static void SaveLocalNow(){if(!local)return;if(!local.closing)local.Capture();local.Flush();}
        public static void EndLocalSession(PPG_Player expected=null){if(!local||expected&&local.player!=expected)return;if(!local.closing)local.Capture();local.closing=true;local.Flush();}
        public static void InvalidateSlot(string host,int slot){
            if(!PlayerResumeFiles.ValidIdentity(host,slot,1))return;
            if(local&&local.host==host&&local.slot==slot){local.closing=true;local.dirty=false;}
            try{PlayerResumeFiles.WriteAtomic(PlayerResumeFiles.EpochPath(Root,host,slot),Guid.NewGuid().ToString("N"));}catch(Exception error){Debug.LogWarning("Player resume slot invalidation failed: "+error.Message);}
        }
        void OnApplicationPause(bool paused){if(paused){if(!closing)Capture();Flush();}}
        void OnApplicationQuit(){if(!closing)Capture();closing=true;Flush();}
        void OnDisable(){Flush();if(local==this)local=null;}

        static bool Ignore(Collider c,Transform player)=>!c||c.isTrigger||(player&&c.transform.IsChildOf(player))||c.GetComponentInParent<PPG_Player>()||c.GetComponentInParent<FishingItem>()||c.GetComponentInParent<DeadFish>();
        public static bool Clear(Vector3 point,float radius,float height,Transform player){
            float r=Mathf.Max(.1f,radius-.03f);var lower=point+Vector3.up*(r+.06f);var upper=point+Vector3.up*Mathf.Max(r+.06f,height-r);
            foreach(var collider in Physics.OverlapCapsule(lower,upper,r,~0,QueryTriggerInteraction.Ignore))if(!Ignore(collider,player))return false;return true;
        }
        static bool Supported(Vector3 point,Transform player){foreach(var hit in Physics.RaycastAll(point+Vector3.up*.15f,Vector3.down,12,~0,QueryTriggerInteraction.Ignore))if(!Ignore(hit.collider,player)&&hit.normal.y>.45f)return true;return false;}
        public static bool TryDestination(PlayerResumeData data,OriginalBoatRig boat,float sea,float radius,float height,Transform player,out Vector3 destination){
            destination=data!=null?data.position:Vector3.zero;if(data==null||!PlayerResumeFiles.Finite(destination))return false;
            bool water=data.swimming&&destination.y<sea+.5f&&destination.y>sea-299;
            if(Clear(destination,radius,height,player)&&(water||Supported(destination,player)))return true;
            if(data.onBoat&&boat&&PlayerResumeFiles.Finite(data.boatLocalPosition)&&data.boatLocalPosition.sqrMagnitude<225){var aboard=boat.transform.TransformPoint(data.boatLocalPosition);if(Clear(aboard,radius,height,player)&&Supported(aboard,player)){destination=aboard;return true;}}
            // Recover only an obstructed/unavailable point; ordinary saved
            // positions are returned unchanged, without snapping to a spawn.
            foreach(float distance in new[]{.5f,1f,1.5f})for(int i=0;i<8;i++){var p=data.position+Quaternion.Euler(0,i*45,0)*Vector3.forward*distance;
                foreach(float lift in new[]{0f,.5f,1f}){var candidate=p+Vector3.up*lift;if(Clear(candidate,radius,height,player)&&(water||Supported(candidate,player))){destination=candidate;return true;}}}
            return false;
        }
    }
}
