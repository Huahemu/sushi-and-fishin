using System;
using System.IO;
using System.Linq;
using HowToFish;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;

namespace SushiAndFish.FishingIntegration {
    public sealed partial class FishingMigration {
        readonly System.Collections.Generic.HashSet<string> convertingCatches=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        string SavePath=>string.IsNullOrEmpty(slotKey)?null:Path.Combine(Application.persistentDataPath,"FishingMigration",slotKey+".json");
        void EnsureSlot(){
            var manager=Singleton<SaveManager>.Instance;if(!manager||!manager.HasValidCurrentSlot)return;
            string next=manager.CurrentSaveSlotHostSteamID+"_"+manager.CurrentSaveSlotIndex;
            if(slotKey==next){RestorePending();return;}
            if(slotKey!=null){SaveWorld();ClearArchipelago();foreach(var fish in catches.Values)if(fish)Destroy(fish.gameObject);catches.Clear();foreach(var actor in actors.Values)if(actor.module)actor.module.Abort();}slotKey=next;SaveData=new FishingWorldSave();
            if(File.Exists(SavePath)){try{SaveData=JsonUtility.FromJson<FishingWorldSave>(File.ReadAllText(SavePath))??new FishingWorldSave();if(SaveData.version>1)throw new InvalidDataException("Newer fishing save version");SaveData.archive=FishingArchiveData.Decode(JsonUtility.ToJson(SaveData.archive));SaveData.stock=SaveData.stock??new System.Collections.Generic.List<StoredFish>();SaveData.pending=SaveData.pending??new System.Collections.Generic.List<SavedCatch>();SaveData.acquired=SaveData.acquired??new System.Collections.Generic.List<string>();}catch(Exception error){Debug.LogError("Fishing save kept unchanged after read failure: "+error.Message);Config.enabled=false;return;}}
            FishingCaptureLedger.Normalize(SaveData);
            foreach(var actor in actors.Values)if(actor.module)actor.module.inventory.UseSharedData(SaveData.archive);
            restored=true;RestorePending();
        }
        public bool SaveWorld(){
            if(!Server||!restored||SavePath==null)return false;
            if(Archipelago&&Archipelago.Boat)SaveData.boat=new SavedBoat{valid=true,position=Archipelago.Boat.body.position,rotation=Archipelago.Boat.body.rotation};
            var live=new System.Collections.Generic.List<SavedCatch>();
            foreach(var pair in catches){if(!pair.Value||!pair.Value.IsAvailable)continue;var fish=pair.Value;var identity=fish.GetComponent<MigratedCatchIdentity>();live.Add(new SavedCatch{token=pair.Key,fisher=identity?identity.fisher:0,steam=identity?identity.steam:0,species=fish.definition.originalId,health=fish.Health,position=fish.fishing.SavedCatchPosition(fish),rotation=fish.transform.rotation,weight=fish.weight,shiny=fish.IsDrip,reachedShore=fish.HasReachedShore||fish.fishing.IsLanding(fish),seen=fish.ArchiveLifted});}
            // Serialize a separate snapshot. Replacing the restore queue with
            // live catches here resurrected every fish removed after an autosave.
            var snapshot=FishingCaptureLedger.Snapshot(SaveData,live);
            try{Directory.CreateDirectory(Path.GetDirectoryName(SavePath));string temp=SavePath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(snapshot));if(File.Exists(SavePath))File.Replace(temp,SavePath,SavePath+".bak");else File.Move(temp,SavePath);return true;}catch(Exception error){Debug.LogError("Fishing save failed; previous file retained: "+error.Message);return false;}
        }
        void RestorePending(){
            if(!Server||!restored||SaveData.pending.Count==0)return;
            var nativeTokens=new System.Collections.Generic.HashSet<string>(FindObjectsByType<DeadFish>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(f=>f.FishingCaptureId).Where(t=>!string.IsNullOrEmpty(t)),StringComparer.Ordinal);
            foreach(var saved in SaveData.pending.ToArray()){
                if(saved==null||FishingCaptureLedger.IsResolved(SaveData,saved.token)||nativeTokens.Contains(saved.token)){if(saved!=null)FishingCaptureLedger.Resolve(SaveData,saved.token);else SaveData.pending.Remove(saved);continue;}
                if(catches.ContainsKey(saved.token))continue;
                var actor=saved.steam!=0?actors.Values.FirstOrDefault(a=>a.module&&a.player.SteamID.Value==saved.steam):null;
                if(actor==null)actor=actors.Values.FirstOrDefault(a=>a.module&&a.player.IsOwner)??actors.Values.FirstOrDefault(a=>a.module);if(actor==null)continue;
                var binding=Config.Find(saved.species);if(binding==null)continue;
                var fish=actor.module.RestoreCatch(binding.definition,SafeRestorePosition(actor,saved),saved.rotation,saved.health,saved.shiny,saved.weight,saved.reachedShore,saved.seen);
                var id=fish.gameObject.AddComponent<MigratedCatchIdentity>();id.token=saved.token;id.fisher=actor.player.OwnerClientId;id.steam=saved.steam;catches[saved.token]=fish;SaveData.pending.Remove(saved);
            }
        }
        Vector3 SafeRestorePosition(FishingActor actor,SavedCatch saved){
            var p=saved.position;
            if(FishingWorldRules.ValidRestorePosition(p,SeaHeight,actor.Camera.transform.position.y,saved.reachedShore,Config.profile.maxLine))return p;
            // Retain species, health, identity and archive state while recovering
            // only physically impossible positions left by the previous spring.
            return actor.module.landingPoint?actor.module.landingPoint.position+Vector3.up*1.2f:actor.player.transform.position+Vector3.up*1.2f;
        }
        internal void Harvest(FishingActor actor,string token){
            if(!Server||string.IsNullOrEmpty(token)||!catches.TryGetValue(token,out var fish)||!fish||!fish.IsAvailable)return;
            if((fish.body.worldCenterOfMass-actor.player.transform.position).sqrMagnitude>25)return;
            if(!fish.IsDead){Notify(actor.player.OwnerClientId,"活鱼不能拾取 · 先用鱼枪击败它");return;}
            var binding=Config.Find(fish.definition.originalId);if(binding==null)return;
            if(!convertingCatches.Add(token))return;
            try{
                if(FishingCaptureLedger.IsResolved(SaveData,token)||FindObjectsByType<DeadFish>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(f=>f.FishingCaptureId==token)){
                    FishingCaptureLedger.Resolve(SaveData,token);fish.Stored=true;fish.item.Retire();catches.Remove(token);SaveWorld();return;
                }
                var item=binding.Item(fish.IsDrip);var native=SpawnNative(item,fish.weight,fish.body.position,fish.transform.rotation,token);if(!native)return;
                // Commit ownership before native hold callbacks can save or a
                // second input/network request can attempt the conversion.
                FishingCaptureLedger.Resolve(SaveData,token);fish.Stored=true;fish.item.Retire();catches.Remove(token);
                try{native.ServerLazyHold(item.Id,actor.player.OwnerClientId,new NetworkObjectReference(actor.player.Interactor.NetworkObject),true,true,true,actor.player.transform.position+actor.player.transform.forward,false);
                    if(native.OwnerClientId==actor.player.OwnerClientId&&!native.CanHold.Value)RegisterNativePickup(native,actor.player);
                }finally{SaveWorld();}
            }finally{convertingCatches.Remove(token);}
        }
        DeadFish SpawnNative(FishSO item,float weight,Vector3 position,Quaternion rotation,string token){
            if(!item||!item.DeadPrefab)return null;
            var obj=Instantiate(item.DeadPrefab,position,rotation);var native=obj.GetComponent<DeadFish>();
            if(!native){Destroy(obj.gameObject);return null;}
            native.FishId.Value=item.Id;native.Weight.Value=weight;native.FishingCaptureId=token;obj.Spawn();native.ServerSpawnGraphic(item.Id);ProtectWalkBody(native.gameObject);return native;
        }
        public static void NativePicked(DeadFish fish,PaperPixelGames.Core.Player.PPG_Player player){if(Enabled&&Instance.Server)Instance.RegisterNativePickup(fish,player);}
        void RegisterNativePickup(DeadFish fish,PaperPixelGames.Core.Player.PPG_Player player){
            var binding=Config.Find(fish.FishId.Value.ToString());if(binding==null||string.IsNullOrEmpty(fish.FishingCaptureId))return;
            FishingCaptureLedger.Resolve(SaveData,fish.FishingCaptureId);if(SaveData.acquired.Contains(fish.FishingCaptureId))return;
            var actor=actors.Values.FirstOrDefault(a=>a.player==player);if(actor==null||!actor.module)return;SaveData.acquired.Add(fish.FishingCaptureId);
            actor.module.inventory.RecordAcquired(binding.definition,Config.IsShiny(fish.FishId.Value.ToString()),fish.Weight.Value);
            SaveData.archive.captures++;SaveData.archive.best=Mathf.Max(SaveData.archive.best,fish.Weight.Value);
            TabletNetworkHandler.Instance?.ServerAddToFishBook(binding.original,fish.Weight.Value,player.SteamID.Value,player.OwnerClientId);
            fish.ServerMarkFishBookCatchRegistered();SaveWorld();
        }
        internal void DepositHeld(FishingActor actor){
            if(!Server)return;var fish=actor.player.Interactor.CurrentHeld as DeadFish;
            if(!fish){Notify(actor.player.OwnerClientId,"先拿起一条已击败的鱼，再存入鱼仓");return;}
            var binding=Config.Find(fish.FishId.Value.ToString());
            RegisterNativePickup(fish,actor.player);string token=string.IsNullOrEmpty(fish.FishingCaptureId)?Guid.NewGuid().ToString("N"):fish.FishingCaptureId;
            if(SaveData.stock.Any(s=>s.token==token))return;
            SaveData.stock.Add(new StoredFish{token=token,itemId=fish.FishId.Value.ToString(),weight=fish.Weight.Value});
            if(binding!=null){var record=SaveData.archive.fish.Find(r=>r.id==binding.definition.originalId);
                if(record==null){record=new FishingInventory.Record{id=binding.definition.originalId,name=binding.definition.displayName};SaveData.archive.fish.Add(record);}
                record.count++;if(Config.IsShiny(fish.FishId.Value.ToString()))record.shinyCount++;else record.originalCount++;record.bestWeight=Mathf.Max(record.bestWeight,fish.Weight.Value);}
            string title=fish.FishSO.LocalizedName;fish.ServerDeregisterInventoryStock();fish.NetworkObject.Despawn();Notify(actor.player.OwnerClientId,"已存入鱼仓 · "+title);SaveWorld();
        }
        internal void Withdraw(FishingActor actor,string itemId){
            if(!Server)return;var stored=SaveData.stock.FirstOrDefault(s=>s.itemId==itemId);if(stored==null)return;
            var binding=Config.Find(itemId);var data=Config.database.FindById(itemId) as FishSO;if(!data)return;
            var native=SpawnNative(data,stored.weight,actor.player.transform.position+actor.player.transform.forward*1.3f+Vector3.up*.7f,Quaternion.identity,stored.token);if(!native)return;
            native.ServerMarkFishBookCatchRegistered();SaveData.stock.Remove(stored);var r=binding==null?null:SaveData.archive.fish.Find(x=>x.id==binding.definition.originalId);if(r!=null){r.count=Math.Max(0,r.count-1);if(Config.IsShiny(itemId))r.shinyCount=Math.Max(0,r.shinyCount-1);else r.originalCount=Math.Max(0,r.originalCount-1);}
            native.ServerLazyHold(itemId,actor.player.OwnerClientId,new NetworkObjectReference(actor.player.Interactor.NetworkObject),true,true,true,native.transform.position,false);SaveWorld();
        }
    }
}
