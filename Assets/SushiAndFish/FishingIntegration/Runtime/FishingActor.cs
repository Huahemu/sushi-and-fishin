using System;
using System.Linq;
using HowToFish;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.PlayerTools;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Core.Managers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SushiAndFish.FishingIntegration {
    public sealed partial class FishingActor {
        internal readonly FishingMigration world;
        public readonly PPG_Player player;
        public FishingModule module;
        public FishingRig rig;
        public int equipment=-1;
        int pendingEquipment=int.MinValue;
        public bool equipped;
        FishingEquipmentWheel equipmentWheel;
        public bool inputActive=true;bool lastAllowed=true;
        public bool ControlsAllowed=>player&&!IsDefeated&&player.IsLocalGameplayPreparationReady&&!player.IsTabletActive&&player.CanStartControlAction(PlayerControlAction.OpenToolWheel);
        public string message;public float messageUntil;
        public Camera Camera=>player&&player.IsOwner?player.GameplayCamera:virtualCamera;
        Camera virtualCamera;
        GameObject root,rigRoot,visualRoot;
        LineRenderer replicaLine;
        bool replicaReloading;
        Vector3 aimPosition,aimForward;
        float nextPose;
        bool left,previousLeft,right,previousRight;
        public FishingActor(FishingMigration service,PPG_Player actor){world=service;player=actor;if(world.Server)CreateServerModule();}
        void CreateServerModule(){
            rigRoot=new GameObject("Fishing actor "+player.OwnerClientId);rigRoot.SetActive(false);rigRoot.transform.SetParent(world.transform,false);
            rig=rigRoot.AddComponent<FishingRig>();rig.hands=new FishingHand[2];rig.locomotionRoot=player.transform;rig.interfaceFont=world.Config.legacyFont;
            var view=new GameObject("Authoritative cast origin");view.transform.SetParent(rigRoot.transform,false);virtualCamera=view.AddComponent<Camera>();virtualCamera.enabled=false;rig.view=player.IsOwner?player.GameplayCamera:virtualCamera;
            for(int i=0;i<2;i++){var go=new GameObject(i==0?"Left fishing grip":"Right fishing grip");go.transform.SetParent(rigRoot.transform,false);rig.hands[i]=go.AddComponent<FishingHand>();}
            RefreshGrips();rigRoot.SetActive(true);
            root=UnityEngine.Object.Instantiate(world.Config.systemPrefab,world.transform);root.name="Fishing system "+player.OwnerClientId;root.transform.position=player.transform.position;
            module=root.GetComponent<FishingModule>();module.inventory.persistence=false;module.inventory.UseSharedData(world.SaveData.archive);module.BindRig(rig);
            module.BoundedLineMotion=true;module.RequireManualCatchRelease=true;if(module.rope){UnityEngine.Object.Destroy(module.rope);module.rope=null;}module.WaterQuery=world.IsWater;var sea=new GameObject("Sea query height");sea.transform.SetParent(root.transform);sea.transform.position=Vector3.up*world.SeaHeight;module.waterSurface=sea.transform;
            if(module.worldEvents)module.worldEvents.enabled=false;var weather=root.GetComponent<FishingWeather>();if(weather)weather.enabled=false;
            foreach(var board in root.GetComponentsInChildren<TextMesh>(true))board.gameObject.SetActive(false);
            foreach(var helper in root.GetComponentsInChildren<FishingInteractable>(true))helper.gameObject.SetActive(false);
            // The portable prefab also contains demo racks and interaction cubes.
            // Native wheel/tablet replaces them, so none should occupy the island.
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))if(!renderer.GetComponentInParent<FishingItem>()&&renderer!=module.line&&!renderer.GetComponentInParent<ParticleSystem>()&&renderer.transform!=module.hookBody.transform)renderer.enabled=false;
            foreach(var collider in root.GetComponentsInChildren<Collider>(true))if(!collider.GetComponentInParent<FishingItem>()&&collider.attachedRigidbody!=module.hookBody)collider.enabled=false;
            CacheToolGraphics();module.profile=UnityEngine.Object.Instantiate(world.Config.profile);module.profile.catchVertexBudget=int.MaxValue;module.profile.catchTriangleBudget=int.MaxValue;module.MaxLooseCatches=0;
            module.HookedFish+=fish=>world.RegisterCatch(this,fish);rig.Notified+=text=>world.Notify(player.OwnerClientId,text);
            module.KilledFish+=fish=>world.ProtectWalkBody(fish.gameObject);
            module.combat.IgnoreCollider=c=>c.GetComponentInParent<PPG_Player>()!=null;module.combat.AffectOtherBodies=false;
            if(player.IsOwner){module.ToolToWorld=ToolWorldPoint;module.WorldToTool=WorldToolPoint;module.combat.ProjectileOrigin=ToolWorldPoint;}
            module.TryPlayerContact=world.HandleFishPlayerContact;world.BindShotEvents(this);
            module.vitals.Died+=BeginNativeDeath;
            aimPosition=player.transform.position+Vector3.up*1.6f;aimForward=player.transform.forward;
        }
        public void SetEquipped(bool active){active=active&&!IsDefeated&&FishingMigration.UsesMigratedFishing(player);equipped=active;if(!active)pendingEquipment=int.MinValue;if(player.IsOwner)Send(active?"equip":"stow",active?equipment:0);if(!active&&module){module.SetReeling(0);module.Abort();ReleaseTools();}}
        public void Send(string action,float value,string target=null){world.SendCommand(this,action,value,target);}
        public void OpenEquipmentPage(){
            if(!equipmentWheel){var candidate=world.gameObject.AddComponent<FishingEquipmentWheel>();if(!candidate.Bind(this)){UnityEngine.Object.Destroy(candidate);world.Notify(player.OwnerClientId,"装备界面尚未准备完成");return;}equipmentWheel=candidate;}
            equipmentWheel.Open();
        }
        public void Aim(Vector3 position,Vector3 direction){
            if(!Finite(position)||!Finite(direction)||(position-player.transform.position).sqrMagnitude>25)return;
            aimPosition=position;aimForward=direction.sqrMagnitude>.5f?direction.normalized:player.transform.forward;
        }
        static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
        public void Tick(){
            TickNativeDeath();
            if(world.Server&&module){
                if(module.waterSurface)module.waterSurface.position=Vector3.up*world.SeaHeight;
                if(!player.IsOwner)virtualCamera.transform.SetPositionAndRotation(aimPosition,Quaternion.LookRotation(aimForward));RefreshGrips();
                if(equipped&&pendingEquipment!=int.MinValue&&module.rig.Items.Count>=6){int requested=pendingEquipment;pendingEquipment=int.MinValue;Equip(requested);}
                if(equipped&&module.rig.Items.Count>=6&&module.rig.ActiveHand.Held==null)Equip(equipment);
                if(equipped&&equipment>=0){var gun=module.combat.guns[equipment];gun.Input(left,previousLeft,false);previousLeft=left;}
                RefreshToolGraphics();
                if(module.waterTarget&&module.hookBody.position.y<=module.SurfaceHeight&&world.IsWater(module.hookBody.position))module.waterTarget.position=new Vector3(module.hookBody.position.x,module.SurfaceHeight,module.hookBody.position.z);
                if(module.landingPoint)module.landingPoint.position=player.transform.position+player.transform.forward*1.5f;
            }
            if(!player.IsOwner){if(!world.Server)UpdateLocalVisual();return;}
            world.TickFreezerInput(this);
            bool allowed=ControlsAllowed;
            if(allowed!=lastAllowed){lastAllowed=allowed;Send("control",allowed?1:0);}
            if(!allowed){if(!world.Server)UpdateLocalVisual();return;}
            if(Time.unscaledTime>=nextPose&&Camera){nextPose=Time.unscaledTime+.05f;world.SendAim(this,Camera.transform.position,Camera.transform.forward);}
            if(equipped&&Keyboard.current!=null&&Keyboard.current.bKey.wasPressedThisFrame)Send("bait",1);
            if(!world.Server)UpdateLocalVisual();
        }
        public void Command(string action,float value,string target){
            if(!world.Server||!module)return;
            if(!float.IsFinite(value))return;
            if(IsDefeated&&action!="control"&&action!="stow"&&action!="native-recovered")return;
            if((action=="equip"||action=="weapon")&&(!FishingMigration.UsesMigratedFishing(player)||!player.ToolsData.FishingRodUnlocked||module.vitals.Down))return;
            if((!inputActive||!FishingMigration.UsesMigratedFishing(player))&&(action=="left"||action=="right"||action=="reload"||action=="bait"))return;
            if((action=="equip"||action=="weapon")&&value>=0&&!world.OwnsWeapon((int)value)){world.Notify(player.OwnerClientId,"先在平板 · 升级 · 枪械中购买这把枪");return;}
            switch(action){
                case "native-recovered":CompleteNativeDeath(target);break;
                case "native-harpoon":NativeSpearfishingAccess.UnlockOnServer(this);break;
                case "buy-weapon":world.BuyWeapon(this,(int)value);break;
                case "control":inputActive=value>.5f;if(!inputActive){left=previousLeft=right=previousRight=false;module.SetReeling(0);if(module.State==FishingState.Charging)module.Abort();if(equipment>=0)module.combat.guns[equipment].SetAim(false);}break;
                case "equip":equipped=true;RequestEquipment(Mathf.Clamp((int)value,-1,4));break;
                case "stow":equipped=false;pendingEquipment=int.MinValue;module.SetReeling(0);module.Abort();ReleaseTools();break;
                case "weapon":equipped=true;RequestEquipment(Mathf.Clamp((int)value,-1,4));break;
                case "left":left=value>.5f;if(equipment<0)module.SetReeling(left?1:0);break;
                case "right":right=value>.5f;if(equipment<0){if(right&&!previousRight){if(module.Hooked)module.ReleaseCatch(module.Hooked);else module.BeginCast();}if(!right&&previousRight)module.ReleaseCast(false);}else module.combat.guns[equipment].SetAim(right);previousRight=right;break;
                case "reload":if(equipment>=0)module.combat.guns[equipment].Reload();break;
                case "bait":module.CycleBait();break;
                case "restock":module.inventory.RestockBait();world.Notify(player.OwnerClientId,"鱼饵已补充 · 每种至少 8 份");break;
                case "harvest":world.Harvest(this,target);break;
                case "deposit":world.DepositHeld(this);break;
                case "freezer-deposit":if(world.FreezerNear(this))world.DepositHeld(this);break;
                case "freezer-withdraw":if(world.FreezerNear(this))world.Withdraw(this,target);break;
                case "withdraw":world.Withdraw(this,target);break;
            }
        }
        bool Equip(int index){if(module.rig.Items.Count<6)return false;if(index>=0&&!world.OwnsWeapon(index))index=-1;if(index>=0&&!module.PrepareWeaponSwitch())return false;module.combat.Equip(index);equipment=index;module.SetReeling(0);left=previousLeft=right=previousRight=false;return true;}
        void RequestEquipment(int index){if(module.rig.Items.Count<6){pendingEquipment=index;return;}pendingEquipment=int.MinValue;Equip(index);}
        void ReleaseTools(){foreach(var hand in rig.hands)if(hand.Held&&hand.Held.kind!=FishingItemKind.Catch)hand.Release();}
        void UpdateLocalVisual(){
            // Remote/own client views are graphics only; the server owns physics.
            if(!equipped){if(visualRoot)visualRoot.SetActive(false);if(replicaLine)replicaLine.enabled=false;return;}
            if(!visualRoot||visualRoot.name!="Equipment "+equipment){if(visualRoot)UnityEngine.Object.Destroy(visualRoot);var template=equipment<0?world.Config.systemPrefab.GetComponent<FishingModule>().rod.gameObject:world.Config.systemPrefab.GetComponent<FishCombat>().guns[equipment].gameObject;visualRoot=UnityEngine.Object.Instantiate(template);visualRoot.name="Equipment "+equipment;replicaFingers=null;foreach(var script in visualRoot.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;foreach(var joint in visualRoot.GetComponentsInChildren<Joint>(true))UnityEngine.Object.Destroy(joint);foreach(var body in visualRoot.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.useGravity=false;}foreach(var col in visualRoot.GetComponentsInChildren<Collider>(true))col.enabled=false;foreach(var part in visualRoot.GetComponentsInChildren<Transform>(true))part.gameObject.layer=player.IsOwner?Mathf.Max(0,LayerMask.NameToLayer("ViewModel")):0;var parent=player.IsOwner&&player.MainCamera?player.MainCamera.transform:player.RigBones?.TpsRig?.RightHand;visualRoot.transform.SetParent(parent?parent:player.transform,false);visualRoot.transform.localPosition=player.IsOwner?GripLocal(1):Vector3.zero;visualRoot.transform.localRotation=Quaternion.identity;}
            visualRoot.SetActive(true);
            if(world.ActorSnapshots.TryGetValue(player.OwnerClientId,out var state)){
                if(equipment<0){var rod=visualRoot.GetComponent<HowToFish.FishingRod>();visualRoot.transform.localRotation=state.rodRotation;if(rod&&state.rodTurns!=null)for(int i=0;i<rod.segments.Length&&i<state.rodTurns.Length;i++)rod.segments[i].localRotation=state.rodTurns[i];
                    if(!replicaLine){var go=new GameObject("Replicated fishing line");replicaLine=go.AddComponent<LineRenderer>();var source=world.Config.systemPrefab.GetComponent<FishingModule>().line;replicaLine.sharedMaterial=source.sharedMaterial;replicaLine.widthMultiplier=source.widthMultiplier;replicaLine.positionCount=2;replicaLine.useWorldSpace=true;}
                    replicaLine.enabled=state.lineEnabled;replicaLine.SetPosition(0,rod?ToolWorldPoint(rod.tip.position):state.rodTip);replicaLine.SetPosition(1,state.hook);
                }else{if(replicaLine)replicaLine.enabled=false;var gun=visualRoot.GetComponent<FishWeapon>();if(gun&&gun.animationPlayer&&state.reloading&&!replicaReloading&&gun.animationPlayer["Reload"]!=null)gun.animationPlayer.Play("Reload");replicaReloading=state.reloading;visualRoot.transform.localPosition=Vector3.Lerp(visualRoot.transform.localPosition,player.IsOwner?(state.aiming?new Vector3(0,-.08f,.5f):GripLocal(1)):Vector3.zero,Time.deltaTime*14);}
            }
        }
        // Cache once: querying hundreds of weapon/hand renderers every frame allocates and stalls the editor.
        Renderer[][] toolGraphics;Collider[][] toolColliders;bool[][] authoredGraphics;bool[] toolVisible;bool initialToolCollidersCleared;
        void CacheToolGraphics(){var tools=new[]{module.rod.item}.Concat(module.combat.guns.Select(g=>g.item)).ToArray();toolGraphics=tools.Select(t=>t.GetComponentsInChildren<Renderer>(true)).ToArray();authoredGraphics=toolGraphics.Select(rs=>rs.Select(r=>r.enabled).ToArray()).ToArray();toolColliders=tools.Select(t=>t.GetComponentsInChildren<Collider>(true)).ToArray();toolVisible=new bool[tools.Length];int layer=player.IsOwner?Mathf.Max(0,LayerMask.NameToLayer("ViewModel")):0;for(int i=0;i<tools.Length;i++){tools[i].ExternalGripPose=true;foreach(var c in toolColliders[i])c.enabled=false;foreach(var r in toolGraphics[i]){r.enabled=false;r.gameObject.layer=layer;}}}
        void RefreshToolGraphics(){if(toolGraphics==null)return;if(!initialToolCollidersCleared&&module.rig.Items.Count>=6){foreach(var set in toolColliders)foreach(var c in set)if(c)c.enabled=false;initialToolCollidersCleared=true;}for(int i=0;i<toolGraphics.Length;i++){var item=i==0?module.rod.item:module.combat.guns[i-1].item;bool visible=item.owner;if(visible==toolVisible[i])continue;toolVisible[i]=visible;for(int j=0;j<toolGraphics[i].Length;j++)if(toolGraphics[i][j])toolGraphics[i][j].enabled=visible&&authoredGraphics[i][j];foreach(var c in toolColliders[i])if(c)c.enabled=false;}}
        Transform[][] nativeFingers,sourceFingers;Transform[] gripWrists;
        Transform[] replicaFingers;
        void CacheGrips(){
            if(nativeFingers==null&&player.RigBones){var bones=player.IsOwner?player.RigBones.FpsRig:player.RigBones.TpsRig;gripWrists=new[]{bones.LeftHand,bones.RightHand};nativeFingers=gripWrists.Select(FishingGripPose.NativeFingers).ToArray();}
            if(module&&sourceFingers==null)sourceFingers=new[]{module.rod.transform}.Concat(module.combat.guns.Select(g=>g.transform)).Select(FishingGripPose.AuthoredFingers).ToArray();
        }
        Vector3 ToolWorldPoint(Vector3 point)=>player.IsOwner?FishingGripPose.ProjectToolPoint(player.MainCamera,Camera,point):point;
        Vector3 WorldToolPoint(Vector3 point)=>player.IsOwner?FishingGripPose.ProjectToolPoint(Camera,player.MainCamera,point):point;
        Vector3 GripLocal(int index){
            var fallback=index==0?new Vector3(-.2f,-.19f,.55f):new Vector3(.2f,-.19f,.55f);CacheGrips();
            var view=player.IsOwner&&player.MainCamera?player.MainCamera:Camera;
            if(!view||gripWrists==null||!gripWrists[index])return fallback;
            return view.transform.InverseTransformPoint(FishingGripPose.Palm(gripWrists[index],nativeFingers[index]));
        }
        void RefreshGrips(){if(!rig||rig.hands==null||!rig.view)return;CacheGrips();for(int i=0;i<rig.hands.Length;i++){var hand=rig.hands[i];if(!hand)continue;var pos=rig.view.transform.TransformPoint(GripLocal(i));if(gripWrists!=null&&gripWrists[i])pos=FishingGripPose.Palm(gripWrists[i],nativeFingers[i]);hand.transform.SetPositionAndRotation(pos,rig.view.transform.rotation);}}
        void PoseHeldTool(Transform tool,FishingItem item,Transform[] fingers,Quaternion localRotation,Vector3 target){
            var grip=FishingGripPose.AuthoredGrip(tool,fingers);var view=player.IsOwner&&player.MainCamera?player.MainCamera:Camera;var rotation=(view?view.transform.rotation:player.transform.rotation)*localRotation;
            tool.SetPositionAndRotation(FishingGripPose.RootPosition(target,rotation,grip,tool.lossyScale),rotation);if(item)item.SyncBodies();
        }
        public void LateTick(){
            if(!equipped||!FishingMigration.UsesMigratedFishing(player))return;
            if(world.Server&&rig){RefreshGrips();foreach(var hand in rig.hands){if(!hand||!hand.Held)continue;var item=hand.Held;if(item.kind==FishingItemKind.Catch){item.SyncBodies();continue;}int index=item.kind==FishingItemKind.Rod?0:Array.FindIndex(module.combat.guns,g=>g.item==item)+1;var fingers=sourceFingers!=null&&index>=0&&index<sourceFingers.Length?sourceFingers[index]:null;PoseHeldTool(item.transform,item,fingers,item.kind==FishingItemKind.Rod?module.rod.HandRotation:Quaternion.identity,hand.transform.position);}}
            else if(player.IsOwner&&visualRoot&&Camera){if(replicaFingers==null)replicaFingers=FishingGripPose.AuthoredFingers(visualRoot.transform);var rotation=world.ActorSnapshots.TryGetValue(player.OwnerClientId,out var state)&&equipment<0?state.rodRotation:Quaternion.identity;var view=player.MainCamera?player.MainCamera:Camera;PoseHeldTool(visualRoot.transform,null,replicaFingers,rotation,view.transform.TransformPoint(GripLocal(1)));}
        }
        public void PlayShot(){if(!visualRoot)return;var gun=visualRoot.GetComponent<FishWeapon>();if(gun&&gun.animationPlayer&&gun.animationPlayer["Fire"]!=null)gun.animationPlayer.Play("Fire");}
        public void Dispose(){ClearNativeDeath();if(equipmentWheel)UnityEngine.Object.Destroy(equipmentWheel);if(module)module.Abort();if(root)UnityEngine.Object.Destroy(root);if(rigRoot)UnityEngine.Object.Destroy(rigRoot);if(visualRoot)UnityEngine.Object.Destroy(visualRoot);if(replicaLine)UnityEngine.Object.Destroy(replicaLine.gameObject);}
    }
}
