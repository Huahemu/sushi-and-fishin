using System;
using System.Collections.Generic;
using System.Linq;
using HowToFish;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.PlayerTools;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using StylizedWater3;

namespace SushiAndFish.FishingIntegration {
    // Added to the existing world at runtime; no replacement player or map.
    [DefaultExecutionOrder(10000)]
    public sealed partial class FishingMigration:MonoBehaviour {
        public static FishingMigration Instance{get;private set;}
        public MigrationConfig Config{get;private set;}
        public FishingWorldSave SaveData{get;private set;}=new FishingWorldSave();
        internal readonly Dictionary<ulong,FishingActor> actors=new Dictionary<ulong,FishingActor>();
        internal readonly Dictionary<string,FishingCatch> catches=new Dictionary<string,FishingCatch>();
        internal readonly Dictionary<string,GameObject> proxies=new Dictionary<string,GameObject>();
        NetworkManager network;
        float scanAt,snapshotAt,saveAt;
        string slotKey;
        bool wireReady,restored;
        public bool Server=>network&&network.IsListening&&network.IsServer;
        public float SeaHeight {get {var ocean=OceanFollowBehaviour.Instance;float height=ocean?ocean.transform.position.y:Config.seaHeight;return float.IsFinite(height)?height:Config.seaHeight;}}
        public FishingActor Local=>actors.Values.FirstOrDefault(a=>a.player&&a.player.IsOwner);
        public static bool Enabled=>Instance&&Instance.Config&&Instance.Config.enabled;
        public static bool UsesMigratedFishing(PPG_Player player)=>Enabled&&player&&player.FishingRod&&player.FishingRod.ToolStatus;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(Instance)return;var config=MigrationConfig.Load();if(!config||!config.enabled)return;var go=new GameObject("Sushi fishing integration");DontDestroyOnLoad(go);go.AddComponent<FishingMigration>().Config=config;}
        void Awake(){Instance=this;}
        void OnApplicationQuit(){SaveWorld();}
        void OnDestroy(){SaveWorld();ClearWalkCollisions();UnbindWire();if(Instance==this)Instance=null;}
        void Update(){
            var current=NetworkManager.Singleton;
            if(!current||!current.IsListening){if(network){SaveWorld();ResetWorld();}return;}
            if(network!=current){ResetWorld();network=current;BindWire();}
            if(Time.unscaledTime>=scanAt){scanAt=Time.unscaledTime+.5f;DiscoverPlayers();}
            foreach(var actor in actors.Values)if(actor.player)actor.Tick();
            if(Server){
                EnsureSlot();TickArchipelago();TickEvents();
                if(Time.unscaledTime>=snapshotAt){snapshotAt=Time.unscaledTime+.1f;PublishWorld();}
                if(Time.unscaledTime>=saveAt){saveAt=Time.unscaledTime+10;SaveWorld();}
            }else TickArchipelago();
            TickWalkCollisions();
        }
        void LateUpdate(){if(Archipelago)Archipelago.LateTick();foreach(var actor in actors.Values)if(actor.player)actor.LateTick();}
        void DiscoverPlayers(){
            foreach(var player in FindObjectsByType<PPG_Player>(FindObjectsSortMode.None)){
                if(player.IsSpawned&&player.IsOwner)PlayerResumeController.Ensure(player);
                if(!player.IsSpawned||actors.ContainsKey(player.OwnerClientId)||player.IsOwner&&(!player.IsLocalGameplayPreparationReady||!player.GameplayCamera))continue;
                var actor=new FishingActor(this,player);actors.Add(player.OwnerClientId,actor);
                if(player.IsOwner){Singleton<PaperPixelGames.Wheel.PPG_WheelManager>.Instance?.UnlockHarpoon(player.ToolsData.HarpoonUnlocked);gameObject.AddComponent<FishingHud>().Bind(actor);}
            }
            foreach(var id in actors.Keys.ToArray())if(!actors[id].player){SaveWorld();actors[id].Dispose();actors.Remove(id);}
        }
        void ResetWorld(){
            var nativeSelection=GetComponent<NativeSpearfishingAccess>();if(nativeSelection)nativeSelection.Cancel();
            ClearWalkCollisions();
            ClearArchipelago();
            UnbindWire();foreach(var actor in actors.Values)actor.Dispose();actors.Clear();
            foreach(var proxy in proxies.Values)if(proxy)Destroy(proxy);proxies.Clear();catches.Clear();
            if(eventDirector)Destroy(eventDirector.gameObject);eventDirector=null;
            if(replicaEvents)Destroy(replicaEvents.gameObject);replicaEvents=null;if(replicaContext)Destroy(replicaContext.gameObject);replicaContext=null;
            if(remoteEffects)Destroy(remoteEffects.gameObject);remoteEffects=null;if(environment)environment.Apply(FishingEventKind.None,0);
            FishSnapshots.Clear();ActorSnapshots.Clear();nextFishContact.Clear();nextCommand.Clear();WarehouseLines=Array.Empty<WarehouseLine>();EventTitle=EventDescription="";EventRemaining=0;
            foreach(var hud in GetComponents<FishingHud>())Destroy(hud);
            network=null;slotKey=null;restored=false;SaveData=new FishingWorldSave();
        }
        public static void ToolStatus(PPG_Player player,bool active){if(!Enabled||!player)return;Instance.DiscoverPlayers();if(Instance.actors.TryGetValue(player.OwnerClientId,out var actor))actor.SetEquipped(active);}
        public static bool OpenFishingEquipment(PPG_Player player,IWheelSlice data){
            if(!Enabled||!(data is PaperPixelGames.Core.Data.Tools.FishingRodData)||!player||!player.IsOwner)return false;
            Instance.DiscoverPlayers();var actor=Instance.Local;if(actor==null||actor.player!=player)return false;
            actor.OpenEquipmentPage();return true;
        }
        public static bool OpenNativeHarpoon(PPG_Player player,IWheelSlice data){
            if(!Enabled||!(data is PaperPixelGames.Core.Data.Tools.HarpoonData)||!player||!player.IsOwner)return false;
            Instance.DiscoverPlayers();var actor=Instance.Local;if(actor==null||actor.player!=player)return false;
            var selection=Instance.GetComponent<NativeSpearfishingAccess>();if(!selection)selection=Instance.gameObject.AddComponent<NativeSpearfishingAccess>();
            selection.Open(actor);return true;
        }
        public static bool ToolInput(PPG_Player player,string action,InputActionPhase phase){
            if(!UsesMigratedFishing(player)||!player.IsOwner)return false;
            if(!Instance.actors.TryGetValue(player.OwnerClientId,out var actor)||!actor.equipped)return true;
            if(!actor.ControlsAllowed){actor.Send("control",0);return true;}
            actor.Send(action,phase==InputActionPhase.Canceled?0:1);return true;
        }
        public static bool Interact(PPG_Player player,InputActionPhase phase){
            if(Enabled&&Instance.Archipelago&&Instance.Archipelago.Interact(player,phase))return true;
            if(!Enabled||!player||!player.IsOwner||phase!=InputActionPhase.Started)return false;
            if(player.ActiveTool&&player.ActiveTool.CapturesInteractInput)return false;
            var actor=Instance.Local;if(actor==null)return false;
            var camera=actor.Camera;if(!camera)return false;
            if(Physics.Raycast(camera.transform.position,camera.transform.forward,out var hit,4,~0,QueryTriggerInteraction.Ignore)){
                var identity=hit.collider.GetComponentInParent<MigratedCatchIdentity>();
                if(identity){actor.Send("harvest",1,identity.token);return true;}
            }
            return false;
        }
        public static bool ReloadTap(PPG_Player player,InputActionPhase phase,bool hold){
            if(!Enabled||hold||!player||!player.IsOwner)return false;
            var actor=Instance.Local;if(actor==null||!actor.equipped||actor.equipment<0)return false;
            if(phase==InputActionPhase.Performed)actor.Send("reload",1);return true;
        }
        public void Notify(ulong player,string message){if(Local!=null&&Local.player.OwnerClientId==player){Local.message=message;Local.messageUntil=Time.unscaledTime+5;}SendNotice(player,message);}
        internal void RegisterCatch(FishingActor actor,FishingCatch fish){
            var identity=fish.gameObject.AddComponent<MigratedCatchIdentity>();identity.token=Guid.NewGuid().ToString("N");identity.fisher=actor.player.OwnerClientId;identity.steam=actor.player.SteamID.Value;catches[identity.token]=fish;
        }
        readonly RaycastHit[] waterHits=new RaycastHit[64];
        public bool IsWater(Vector3 point){
            // Above-water terrain and solid structures exclude otherwise open sea.
            float sea=SeaHeight;if(point.y>sea+.3f)return false;
            int count=Physics.RaycastNonAlloc(new Vector3(point.x,sea+80,point.z),Vector3.down,waterHits,160,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++){var hit=waterHits[i];
                if(hit.collider.GetComponentInParent<FishingItem>()||hit.collider.GetComponentInParent<PPG_Player>())continue;
                if(hit.point.y>=sea-.05f&&(hit.collider is TerrainCollider||hit.normal.y>.55f))return false;
            }
            return true;
        }
    }
}
