using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using HowToFish;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Input;
using UnityEngine;
using UnityEngine.InputSystem;
namespace SushiAndFish.FishingIntegration {
    [Serializable] public sealed class SailingSnapshot {public Vector3 position,velocity;public Quaternion rotation;public ulong driver=ulong.MaxValue;public Vector2 input;public int relocation;}
    [Serializable] public sealed class SavedBoat {public bool valid;public Vector3 position;public Quaternion rotation;}
    public sealed class ArchipelagoWorld:MonoBehaviour {
        FishingMigration world;public ArchipelagoSettings Settings{get;private set;}public OriginalBoatRig Boat{get;private set;}public bool Server=>world&&world.Server;public float SeaHeight=>world.SeaHeight;
        internal FishingMigration Service=>world;ArchipelagoWaterTraversal traversal;float recoveryHold=-1,nextRecoveryProbe,recoveryCooldown;bool needsRecovery;int remoteRelocation;
        PPG_Player localDriver;bool oldTrapped,oldInputPause,oldMotorEnabled;Vector3 boardStart,lastBoatPosition;Quaternion lastBoatRotation;float boardAt,nextInput;Vector2 input;int lastRegion=-1;Action consumeNativeLook;
        sealed class Subscription {public FishingModule module;public Action<FishingModule,Vector3> entered;}
        readonly Dictionary<ulong,Subscription> profiles=new Dictionary<ulong,Subscription>();readonly List<GameObject> islands=new List<GameObject>();
        readonly Collider[] cargo=new Collider[128];readonly HashSet<Rigidbody> carried=new HashSet<Rigidbody>();readonly HashSet<FishingCatch> carriedFish=new HashSet<FishingCatch>();bool disposed;
        public void Bind(FishingMigration owner,ArchipelagoSettings settings){world=owner;Settings=settings;
            foreach(var region in settings.regions.Skip(1)){var island=Instantiate(region.prefab,region.center,Quaternion.identity,transform);island.name=region.title;islands.Add(island);}
            if(settings.accessPrefab)Instantiate(settings.accessPrefab,Vector3.zero,Quaternion.identity,transform);
            var saved=world.SaveData.boat;bool valid=saved!=null&&saved.valid&&ValidPosition(saved.position,settings.boatSpawn)&&ValidRotation(saved.rotation);
            var position=valid?saved.position:settings.boatSpawn;var rotation=valid?saved.rotation:settings.boatRotation;
            if(Server&&valid){Physics.SyncTransforms();if((!ArchipelagoBoatSafety.SavedPoseUsable(position,rotation,SeaHeight)||!ArchipelagoBoatSafety.ClearWater(position,rotation,SeaHeight))&&ArchipelagoBoatSafety.TryFind(settings,position,rotation.eulerAngles.y,SeaHeight,null,out var recovered,out var upright)){position=recovered;rotation=upright;}}
            Boat=Instantiate(settings.boatPrefab,position,rotation,transform);Boat.name="How to Fish boat";Boat.Bind(this,Server);lastBoatPosition=Boat.transform.position;lastBoatRotation=Boat.transform.rotation;traversal=new ArchipelagoWaterTraversal(this);
            var handler=Singleton<InputManager>.Instance?.InputHandler;if(handler!=null){handler.Move+=OnMove;handler.Throw+=OnDrop;}
        }
        public static bool ValidPosition(Vector3 p,Vector3 origin)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z)&&Vector3.Distance(p,origin)<2500&&Mathf.Abs(p.y-origin.y)<30;
        public static bool ValidRotation(Quaternion q)=>float.IsFinite(q.x)&&float.IsFinite(q.y)&&float.IsFinite(q.z)&&float.IsFinite(q.w)&&Mathf.Abs(Quaternion.Dot(q,q)-1)<.05f;
        void OnMove(InputActionPhase phase,Vector2 value){input=phase==InputActionPhase.Canceled?Vector2.zero:value;}
        void OnDrop(InputActionPhase phase){if(phase==InputActionPhase.Started&&localDriver&&world.Local!=null)world.Local.Send("boat-exit",0);}
        public bool IsDriver(PPG_Player player)=>Boat&&player&&Boat.driver==player.OwnerClientId;
        public bool AnyPlayerNear(Vector3 p,float distance)=>world.actors.Values.Any(a=>a.player&&(a.player.transform.position-p).sqrMagnitude<distance*distance);
        public Vector3 NearestDock(Vector3 p)=>Settings.regions.OrderBy(r=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2(r.dock.x,r.dock.z))).First().dock;
        bool AboveHull(Vector3 point,float tolerance){if(!Boat)return false;var p=Boat.transform.InverseTransformPoint(point);if(p.x< -2.3f||p.x>2.3f||p.z< -4.9f||p.z>4.5f||p.y<-.3f||p.y>3.5f)return false;
            var ray=new Ray(point+Vector3.up*.3f,Vector3.down);foreach(var collider in Boat.hull)if(collider.Raycast(ray,out var hit,tolerance+.3f)&&hit.normal.y>.45f)return true;return false;
        }
        public bool OnDeck(PPG_Player player)=>player&&AboveHull(player.transform.position,1.3f);
        bool LookingAtHelm(FishingActor actor){if(actor==null||!actor.Camera||!OnDeck(actor.player))return false;var offset=Boat.steering.position-actor.Camera.transform.position;return offset.sqrMagnitude<12.25f&&Vector3.Dot(actor.Camera.transform.forward,offset.normalized)>.86f;}
        public bool Interact(PPG_Player player,InputActionPhase phase){if(!player||!player.IsOwner)return false;
            if(phase==InputActionPhase.Canceled&&recoveryHold>=0){recoveryHold=-1;return true;}
            if(IsDriver(player)){if(phase==InputActionPhase.Started)world.Local?.Send("boat-exit",0);return true;}
            if(needsRecovery&&world.Local!=null&&LookingAtBoat(world.Local)){if(phase==InputActionPhase.Started){if(player.Interactor.CurrentHeld||player.IsToolActive)world.Notify(player.OwnerClientId,"先收起手中物品和工具，再长按 E 让船脱困");else recoveryHold=Time.unscaledTime;}return true;}
            var actor=world.Local;if(actor==null||actor.player!=player||!LookingAtHelm(actor))return false;
            if(phase==InputActionPhase.Started){if(player.Interactor.CurrentHeld||player.IsToolActive)world.Notify(player.OwnerClientId,"先收起手中物品和工具，再驾驶船只");else if(!player.CanStartControlAction(PlayerControlAction.ActivateTool))return true;else actor.Send("boat-drive",1);}return true;
        }
        internal void Command(FishingActor actor,string action,Vector3 axes){if(!Server||!actor.player||!Boat)return;
            if(action=="boat-recover"){if(Time.unscaledTime<recoveryCooldown||Boat.driver!=ulong.MaxValue||Vector3.Distance(actor.player.transform.position,Boat.transform.position)>12||actor.equipped||actor.player.IsToolActive||actor.player.Interactor.CurrentHeld||!actor.player.CanStartControlAction(PlayerControlAction.ActivateTool))return;
                recoveryCooldown=Time.unscaledTime+3;if(!ArchipelagoBoatSafety.NeedsRecovery(Boat,SeaHeight))return;
                if(ArchipelagoBoatSafety.TryFind(Settings,Boat.transform.position,Boat.transform.eulerAngles.y,SeaHeight,Boat.transform,out var p,out var q)){RelocateBoat(p,q);world.Notify(actor.player.OwnerClientId,"船已扶正并回到附近开阔水面");world.SaveWorld();}else world.Notify(actor.player.OwnerClientId,"附近没有足够宽敞的水面，请移开船旁障碍");return;
            }
            if(action=="boat-drive"){
                if(Boat.driver!=ulong.MaxValue||!OnDeck(actor.player)||(actor.player.transform.position-Boat.steering.position).sqrMagnitude>16||actor.player.Interactor.CurrentHeld||actor.equipped||actor.player.IsToolActive||!actor.player.CanStartControlAction(PlayerControlAction.ActivateTool))return;
                Boat.SetDriver(actor.player.OwnerClientId);world.Notify(actor.player.OwnerClientId,"发动机启动中 · W/S 前进倒车，A/D 转向，E/G 退出驾驶");
            }else if(Boat.driver==actor.player.OwnerClientId){if(action=="boat-exit")Boat.SetDriver(ulong.MaxValue);else if(action=="boat-input"&&float.IsFinite(axes.x)&&float.IsFinite(axes.y)&&Mathf.Abs(axes.x)<=1.01f&&Mathf.Abs(axes.y)<=1.01f)Boat.SetInput(new Vector2(axes.x,axes.y));}
        }
        public void ApplyRegion(FishingActor actor,Vector3 cast){if(!actor.module)return;int i=Settings.ApplyTo(actor.module.profile,cast);world.Notify(actor.player.OwnerClientId,Settings.regions[i].title+" · 本杆鱼池已锁定");}
        public void Tick(){if(!Boat)return;
            foreach(var key in profiles.Keys.ToArray())if(!world.actors.TryGetValue(key,out var actor)||profiles[key].module!=actor.module){var old=profiles[key];if(old.module)old.module.WaterCastEntered-=old.entered;profiles.Remove(key);}
            foreach(var actor in world.actors.Values)if(actor.module&&!profiles.ContainsKey(actor.player.OwnerClientId)){var subscription=new Subscription{module=actor.module,entered=(module,position)=>ApplyRegion(actor,position)};profiles.Add(actor.player.OwnerClientId,subscription);actor.module.WaterCastEntered+=subscription.entered;ApplyRegion(actor,actor.player.transform.position);}
            if(Server&&Boat.driver!=ulong.MaxValue&&(!world.actors.TryGetValue(Boat.driver,out var captain)||!captain.player||captain.player.DivingEquipment.IsKnocked))Boat.SetDriver(ulong.MaxValue);
            var local=world.Local;if(local==null||!local.player)return;bool driving=IsDriver(local.player);
            if(driving&&localDriver!=local.player)Enter(local.player);else if(!driving&&localDriver)Exit();
            traversal?.Tick();if(Time.unscaledTime>=nextRecoveryProbe){nextRecoveryProbe=Time.unscaledTime+.4f;needsRecovery=ArchipelagoBoatSafety.NeedsRecovery(Boat,SeaHeight);}
            if(recoveryHold>=0){bool allowed=local.player.PlayerInput.currentActionMap!=null&&local.player.PlayerInput.currentActionMap.name=="Player"&&Cursor.lockState==CursorLockMode.Locked&&local.player.CanStartControlAction(PlayerControlAction.ActivateTool);if(driving||!needsRecovery||!LookingAtBoat(local)||local.player.IsTabletActive||!allowed){recoveryHold=-1;}else if(Time.unscaledTime-recoveryHold>=2){recoveryHold=-1;local.Send("boat-recover",0);}}
            if(driving&&Time.unscaledTime>=nextInput){nextInput=Time.unscaledTime+.05f;bool allowed=local.player.PlayerInput.currentActionMap!=null&&local.player.PlayerInput.currentActionMap.name=="Player"&&!local.player.IsTabletActive&&Cursor.lockState==CursorLockMode.Locked;var axes=allowed?input:Vector2.zero;
                if(allowed&&Gamepad.current!=null&&local.player.PlayerInput.currentControlScheme!=null&&local.player.PlayerInput.currentControlScheme.IndexOf("Gamepad",StringComparison.OrdinalIgnoreCase)>=0)axes.y=Gamepad.current.rightTrigger.ReadValue()>.1f?1:Gamepad.current.leftTrigger.ReadValue()>.1f?-1:0;
                world.SendSailingInput(local,axes);
            }
            int region=Settings.RegionAt(local.player.transform.position);if(region!=lastRegion){lastRegion=region;world.Notify(local.player.OwnerClientId,"抵达 "+Settings.regions[region].title);}
        }
        void Enter(PPG_Player player){localDriver=player;oldTrapped=player.IsTrapped;oldInputPause=player.FirstPersonCharacterInput.IsInputPaused;oldMotorEnabled=player.FirstPersonCharacter.characterMovement.enabled;boardStart=player.transform.position;boardAt=Time.time;
            // Keep the original look input, sensitivity, inversion and sway.
            // ECM2 normally consumes yaw inside its movement simulation, which
            // is paused while driving. Bind only that rotation consumer here.
            consumeNativeLook=(Action)typeof(ECM2.Character).GetMethod("ConsumeRotationInput",BindingFlags.Instance|BindingFlags.NonPublic).CreateDelegate(typeof(Action),player.FirstPersonCharacter);
            player.PauseMovement();player.PauseInputs();player.IsTrapped=true;player.FirstPersonCharacter.characterMovement.enabled=false;player.SetLocomotionAnimationSuppressed(true);player.PauseConstraints();input=Vector2.zero;lastBoatPosition=Boat.transform.position;lastBoatRotation=Boat.transform.rotation;
        }
        void Exit(){var player=localDriver;localDriver=null;consumeNativeLook=null;if(!player)return;player.IsTrapped=oldTrapped;player.FirstPersonCharacter.characterMovement.enabled=oldMotorEnabled;player.FirstPersonCharacterInput.IsInputPaused=oldInputPause;player.SetLocomotionAnimationSuppressed(false);player.TryRestoreCharacterControlAfterModeExit();input=Vector2.zero;}
        public void LateTick(){if(!Boat)return;var player=world.Local?.player;var delta=Boat.transform.rotation*Quaternion.Inverse(lastBoatRotation);float yaw=Mathf.DeltaAngle(lastBoatRotation.eulerAngles.y,Boat.transform.eulerAngles.y);
            if(localDriver){var target=Boat.DriverFeet;float t=Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.time-boardAt)/.25f));localDriver.FirstPersonCharacter.TeleportPosition(Vector3.Lerp(boardStart,target,t),false,false);localDriver.FirstPersonCharacter.AddControlYawInput(yaw);consumeNativeLook?.Invoke();}
            else if(player&&OnDeck(player)&&!player.IsTrapped){var movement=player.FirstPersonCharacter.characterMovement;
                // ECM2 already carries grounded passengers on this Rigidbody.
                // Only use an explicit delta if that native option is disabled.
                if(!movement.impartPlatformMovement){var p=Boat.transform.position+delta*(player.transform.position-lastBoatPosition);player.FirstPersonCharacter.TeleportPosition(p,false,false);}
                if(!movement.impartPlatformRotation)player.FirstPersonCharacter.AddControlYawInput(yaw);
            }
            if(Server)CarryCargo(delta);
            traversal?.LateTick();
            lastBoatPosition=Boat.transform.position;lastBoatRotation=Boat.transform.rotation;
        }
        void CarryCargo(Quaternion delta,Vector3? destination=null,Vector3? origin=null){var to=destination??Boat.transform.position;var from=origin??lastBoatPosition;carried.Clear();carriedFish.Clear();int count=Physics.OverlapBoxNonAlloc(Boat.transform.TransformPoint(new Vector3(0,1,0)),new Vector3(2.1f,2,4.8f),cargo,Boat.transform.rotation,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++){var collider=cargo[i];var body=collider.attachedRigidbody;if(!body||body==Boat.body||collider.GetComponentInParent<PPG_Player>()||body.isKinematic||!AboveHull(collider.bounds.center,collider.bounds.extents.y+.6f)||world.actors.Values.Any(a=>a.player&&a.player.Interactor.CurrentHeld&&collider.transform.IsChildOf(a.player.Interactor.CurrentHeld.transform)))continue;
                var fish=collider.GetComponentInParent<FishingCatch>();if(fish){if(carriedFish.Add(fish))fish.CarryOnBoat(from,to,delta);continue;}
                if(carried.Add(body)){body.position=to+delta*(body.position-from);body.rotation=delta*body.rotation;}
            }
        }
        void RelocateBoat(Vector3 position,Quaternion rotation){var old=Boat.transform.position;var turn=rotation*Quaternion.Inverse(Boat.transform.rotation);bool riding=world.Local!=null&&OnDeck(world.Local.player);CarryCargo(turn,position,old);Boat.Relocate(position,rotation);if(riding)MovePassenger(old,position,turn);lastBoatPosition=position;lastBoatRotation=rotation;needsRecovery=false;}
        internal bool ReturnBoatHome(){
            if(!Server||!Boat)return false;var target=Settings.boatSpawn;target.y=SeaHeight+.25f;var rotation=Settings.boatRotation;
            if(!ArchipelagoBoatSafety.ClearWater(target,rotation,SeaHeight,Boat.transform)&&!ArchipelagoBoatSafety.TryFind(Settings,target,rotation.eulerAngles.y,SeaHeight,Boat.transform,out target,out rotation))return false;
            Boat.SetDriver(ulong.MaxValue);RelocateBoat(target,rotation);return true;
        }
        void MovePassenger(Vector3 from,Vector3 to,Quaternion delta){var player=world.Local?.player;if(!player)return;player.FirstPersonCharacter.TeleportPosition(to+delta*(player.transform.position-from),false,false);player.FirstPersonCharacter.SetVelocity(Vector3.zero);}
        public SailingSnapshot Snapshot()=>Boat?new SailingSnapshot{position=Boat.body.position,rotation=Boat.body.rotation,driver=Boat.driver,input=Boat.Input,velocity=Boat.body.linearVelocity,relocation=Boat.RelocationRevision}:null;
        public void ApplySnapshot(SailingSnapshot snapshot){if(!Server&&Boat&&snapshot!=null&&ValidPosition(snapshot.position,Settings.boatSpawn)&&ValidRotation(snapshot.rotation)){bool warp=snapshot.relocation!=remoteRelocation;bool riding=warp&&world.Local!=null&&OnDeck(world.Local.player);var old=Boat.transform.position;var delta=snapshot.rotation*Quaternion.Inverse(Boat.transform.rotation);Boat.ApplyRemote(snapshot.position,snapshot.rotation,snapshot.driver,snapshot.input,snapshot.velocity,warp);if(warp){if(riding)MovePassenger(old,snapshot.position,delta);lastBoatPosition=snapshot.position;lastBoatRotation=snapshot.rotation;}remoteRelocation=snapshot.relocation;}}
        public bool RenderHints(FishingNativeHints hints,FishingActor actor){if(!Boat)return false;if(IsDriver(actor.player)){hints.RenderSailing(true,Boat.MotorReady,Boat.Speed,null,null);return true;}
            if(needsRecovery&&LookingAtBoat(actor)){hints.RenderWaterAccess("长按 E 2秒 · 扶正并推船下水",recoveryHold>=0?"脱困 "+Mathf.RoundToInt(Mathf.Clamp01((Time.unscaledTime-recoveryHold)/2)*100)+"%":null);return true;}
            if(traversal!=null&&traversal.RenderHints(hints))return true;
            if(LookingAtHelm(actor)&&!actor.equipped){hints.RenderSailing(false,true,0,null,Boat.driver==ulong.MaxValue?"空手按 E 驾驶船只":"船只正在被其他玩家驾驶");return true;}return false;}
        bool LookingAtBoat(FishingActor actor){return actor!=null&&actor.Camera&&Boat&&Physics.Raycast(actor.Camera.transform.position,actor.Camera.transform.forward,out var hit,12,~0,QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<OriginalBoatRig>()==Boat;}
        public void Dispose(){if(disposed)return;disposed=true;Exit();traversal?.Dispose();traversal=null;var handler=Singleton<InputManager>.Instance?.InputHandler;if(handler!=null){handler.Move-=OnMove;handler.Throw-=OnDrop;}foreach(var p in profiles.Values)if(p.module)p.module.WaterCastEntered-=p.entered;profiles.Clear();}
        void OnDestroy(){Dispose();}
    }
}
