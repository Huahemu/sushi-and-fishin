using System;
using System.Linq;
using System.Reflection;
using ECM2;
using PaperPixelGames.Core.Player;
using UnityEngine;
namespace SushiAndFish.FishingIntegration {
    public sealed class ArchipelagoWaterTraversal:IDisposable {
        readonly ArchipelagoWorld world;PhysicsVolume fallback;PhysicsVolume[] volumes=Array.Empty<PhysicsVolume>();PPG_Player player;
        Action<Collider> addVolume,removeVolume;Action consumeLook;float scanAt,entryUntil,jumpUntil,nextProbe;bool previousJump,climbing,oldTrapped,oldInput,disposed;
        Vector3 start,end;Transform support;Vector3 supportLocal;float climbAt;bool canClimb;
        public bool Climbing=>climbing;
        public ArchipelagoWaterTraversal(ArchipelagoWorld owner){world=owner;fallback=CreateWater(owner.Settings,owner.SeaHeight,owner.transform);}
        public static PhysicsVolume CreateWater(ArchipelagoSettings settings,float sea,Transform parent){var bounds=ArchipelagoWaterMath.WaterBounds(settings,sea);var go=new GameObject("Archipelago fallback swim volume");go.SetActive(false);go.transform.SetParent(parent,false);go.transform.position=bounds.center;go.layer=LayerMask.NameToLayer("Water");var box=go.AddComponent<BoxCollider>();box.size=bounds.size;box.isTrigger=true;var volume=go.AddComponent<PhysicsVolume>();volume.waterVolume=true;volume.priority=-100;volume.friction=1;volume.maxFallSpeed=2;go.SetActive(true);return volume;}
        void Bind(PPG_Player current){Unbind();player=current;if(!player)return;var character=player.FirstPersonCharacter;var type=typeof(Character);const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            addVolume=(Action<Collider>)type.GetMethod("AddPhysicsVolume",flags).CreateDelegate(typeof(Action<Collider>),character);removeVolume=(Action<Collider>)type.GetMethod("RemovePhysicsVolume",flags).CreateDelegate(typeof(Action<Collider>),character);consumeLook=(Action)type.GetMethod("ConsumeRotationInput",flags).CreateDelegate(typeof(Action),character);character.MovementModeChanged+=OnModeChanged;
        }
        void OnModeChanged(Character.MovementMode previous,int custom){if(!player||!player.FirstPersonCharacter.IsSwimming()||previous==Character.MovementMode.Swimming||previous==Character.MovementMode.SeaScooter)return;entryUntil=Time.time+.65f;var c=player.FirstPersonCharacter;c.SetVelocity(ArchipelagoWaterMath.EntryVelocity(c.GetVelocity(),-c.GetGravityDirection(),0,0,true));}
        public void Tick(){var current=world.Service.Local?.player;if(player!=current)Bind(current);if(!player)return;var c=player.FirstPersonCharacter;
            if(climbing){if(player.DivingEquipment.IsKnocked||!support)FinishClimb(false);return;}
            if(c.IsPaused()||c.HasActiveMotion||player.IsTrapped||world.IsDriver(player)||!player.IsLocalGameplayPreparationReady)return;
            if(Time.unscaledTime>=scanAt){scanAt=Time.unscaledTime+.5f;volumes=UnityEngine.Object.FindObjectsByType<PhysicsVolume>(FindObjectsSortMode.None);}
            // Child colliders, disabled boat-driving capsules and teleports can
            // unbalance trigger enter/exit callbacks. Reconcile exact centres,
            // preserving dry-volume priorities and all native water callbacks.
            PhysicsVolume selected=null;var center=c.characterMovement.worldCenter;
            foreach(var volume in volumes){if(!volume||!volume.isActiveAndEnabled)continue;bool inside=(volume.boxCollider.ClosestPoint(center)-center).sqrMagnitude<.000001f;if(inside){addVolume(volume.boxCollider);if(!selected||volume.priority>selected.priority)selected=volume;}else removeVolume(volume.boxCollider);}
            if(selected&&selected.waterVolume&&c.physicsVolume==selected&&c.IsFalling()&&c.GetVelocity().y<=0&&center.y<selected.boxCollider.bounds.max.y-.04f)c.SetMovementMode(Character.MovementMode.Swimming);
            bool jump=c.jumpInputPressed;if(jump&&!previousJump)jumpUntil=Time.time+.4f;previousJump=jump;
            bool water=c.IsSwimming()||c.IsInWaterPhysicsVolume();bool inputAllowed=!player.FirstPersonCharacterInput.IsInputPaused&&player.PlayerInput.currentActionMap!=null&&player.PlayerInput.currentActionMap.name=="Player"&&Cursor.lockState==CursorLockMode.Locked;bool allowed=water&&inputAllowed&&!player.IsTabletActive&&player.transform.position.y<world.SeaHeight+.4f&&center.y<world.SeaHeight+.7f;
            if(allowed&&Time.time>=nextProbe){nextProbe=Time.time+.08f;var camera=player.GameplayCamera;canClimb=camera&&ArchipelagoShoreQueries.TryFind(player.transform.position,camera.transform.forward,c.characterMovement.radius,c.characterMovement.height,player.transform,out end,out support);}
            if(!allowed)canClimb=false;
            if(canClimb&&(jump||Time.time<jumpUntil))BeginClimb();
        }
        void BeginClimb(){if(!support)return;climbing=true;canClimb=false;climbAt=Time.time;start=player.transform.position;supportLocal=support.InverseTransformPoint(end);oldTrapped=player.IsTrapped;oldInput=player.FirstPersonCharacterInput.IsInputPaused;player.FirstPersonCharacter.StopJumping();player.PauseMovement();player.PauseInputs();player.IsTrapped=true;player.SetLocomotionAnimationSuppressed(true);}
        public void LateTick(){if(!player)return;var c=player.FirstPersonCharacter;
            if(climbing){if(!support){FinishClimb(false);return;}end=support.TransformPoint(supportLocal);float t=Mathf.Clamp01((Time.time-climbAt)/.48f);var next=ArchipelagoWaterMath.ClimbPosition(start,end,t);if(!ArchipelagoShoreQueries.ClearPath(player.transform.position,next,c.characterMovement.radius,c.characterMovement.height,player.transform)){FinishClimb(false);return;}c.TeleportPosition(next,false,false);consumeLook?.Invoke();if(t>=1)FinishClimb(true);}
            else if(Time.time<entryUntil&&c.IsSwimming()&&!c.IsSeaScooter()&&!c.IsPaused()){float desired=Vector3.Dot(c.GetMovementDirection(),Vector3.up);c.SetVelocity(ArchipelagoWaterMath.EntryVelocity(c.GetVelocity(),Vector3.up,desired,Time.deltaTime,false));}
        }
        void FinishClimb(bool completed){if(!climbing)return;climbing=false;jumpUntil=0;previousJump=false;if(!player)return;var c=player.FirstPersonCharacter;player.IsTrapped=oldTrapped;if(!player.DivingEquipment.IsKnocked)player.FirstPersonCharacterInput.IsInputPaused=oldInput;player.SetLocomotionAnimationSuppressed(false);c.StopJumping();if(completed){c.TeleportPosition(end,false,true);c.SetMovementMode(Character.MovementMode.Walking);}player.TryRestoreCharacterControlAfterModeExit();if(!disposed&&c.characterMovement.collider.enabled)world.Service.RefreshWalkContacts();}
        public bool RenderHints(FishingNativeHints hints){if(climbing){hints.RenderWaterAccess("正在攀上岸边",null);return true;}if(canClimb){hints.RenderWaterAccess("按住空格 · 靠近边缘上岸",null);return true;}return false;}
        void Unbind(){FinishClimb(false);if(player&&player.FirstPersonCharacter){player.FirstPersonCharacter.MovementModeChanged-=OnModeChanged;if(fallback)removeVolume?.Invoke(fallback.boxCollider);}player=null;addVolume=removeVolume=null;consumeLook=null;}
        public void Dispose(){if(disposed)return;disposed=true;Unbind();if(fallback)UnityEngine.Object.Destroy(fallback.gameObject);}
    }
}
