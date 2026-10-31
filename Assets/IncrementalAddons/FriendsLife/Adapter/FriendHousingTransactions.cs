using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using SushiAndFish.CityExpansion;
using SushiAndFish.ResidentialIsland;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendHousingIsland
    {
        sealed class PreparedSeat
        {public GameObject go;public EmoteArea area;public Transform pose,approach;public HousingRestInteractable rest;}
        readonly Dictionary<HousingHouse,List<PreparedSeat>> preparedFurniture=new Dictionary<HousingHouse,List<PreparedSeat>>();
        public static string HomeIdentity(HousingHouse house)
        {
            if(!house)return null;var metadata=FriendHouseMetadata.For(house);
            if(metadata.friendsOwnedClone&&string.IsNullOrEmpty(metadata.instanceId))metadata.instanceId=HousingVacancyRules.Generation(house.plot,null);
            return metadata.instanceId;
        }
        public bool IsEmptyForNewHousing(string plotId)
        {
            var plot=Plots.FirstOrDefault(p=>p.id==plotId);var runtime=FriendsLifeRuntime.Instance;
            return plot!=null&&!plot.Occupied&&runtime&&runtime.Ready&&
                !runtime.People.Any(p=>p.ownedHomeId==plotId||p.currentHomeId==plotId);
        }
        public HousingHouse PrepareEmptyHome(HousingPlot plot,HousingType type,float yaw,FriendHousingPlacement template,out string error)
        {
            error=null;if(!CanBuild||plot==null||!IsEmptyForNewHousing(plot.id)){error="所选地块已被使用";return null;}
            if(!Fits(plot,type,yaw,out error))return null;
            HousingHouse house=null;
            try
            {
                house=Construct(plot,type,yaw);if(!house){error=LastFailure??"住宅资源尚未准备完成";return null;}house.gameObject.SetActive(false);
                // Template doors are restored, but the explicit target plot and
                // orientation are authoritative. The old plot is not reserved.
                foreach(var door in house.GetComponentsInChildren<HousingDoor>(true))
                {var state=template?.doors?.FirstOrDefault(d=>d.path==RelativePath(door.transform,house.transform));if(state!=null)FriendHousingDoor.SetOpenInstant(door,state.open);}
                if(!PrepareFurniture(house,out error)){DisposeOwnedProjection(house);return null;}
                return house;
            }
            catch(Exception exception){if(house)DisposeOwnedProjection(house);Debug.LogWarning("[FriendsLife] Housing prepare failed: "+exception);error="住宅操作失败，已恢复原样。请重试。";return null;}
        }
        bool PrepareFurniture(HousingHouse house,out string error)
        {
            error=null;if(!CanBuild)return true;if(preparedFurniture.ContainsKey(house))return true;
            var network=NetworkManager.Singleton;var prefab=Catalog?Catalog.nativeSeatPrefab:null;
            if(!network||!network.IsServer||!network.IsListening||!prefab||!prefab.GetComponent<NetworkObject>()||
               !prefab.GetComponent<EmoteArea>()||!prefab.GetComponent<CityDiningSeat>())
            {error="住宅家具资源尚未准备完成";return false;}
            var seats=new List<PreparedSeat>();preparedFurniture[house]=seats;
            try
            {
                void Add(Transform pose,Transform approach,HousingRestInteractable rest=null)
                {
                    if(!pose)throw new InvalidOperationException("住宅家具站位缺失");
                    var go=Instantiate(prefab,pose.position,pose.rotation);house.spawnedSeats.Add(go);go.SetActive(false);go.name="PreparedResidenceSeat_"+house.plot;
                    SceneManager.MoveGameObjectToScene(go,house.gameObject.scene);
                    var seat=new PreparedSeat{go=go,area=go.GetComponent<EmoteArea>(),pose=pose,approach=approach,rest=rest};
                    seats.Add(seat);SetField(seat.area,"_targetPos",pose.position);SetField(seat.area,"_targetRot",pose.eulerAngles);
                    var dining=go.GetComponent<CityDiningSeat>();dining.seatedPose=pose;dining.approach=approach;
                    if(rest){rest.area=seat.area;ConfigureRestSeat(rest);}
                }
                foreach(var anchor in house.chairs??Array.Empty<HousingSeatAnchor>())if(anchor)Add(anchor.pose,anchor.exit);
                foreach(var rest in house.GetComponentsInChildren<HousingRestInteractable>(true))Add(rest.exit,rest.exit,rest);
                return true;
            }
            catch(Exception exception){Debug.LogWarning("[FriendsLife] Housing furniture failed: "+exception);error="住宅操作失败，已恢复原样。请重试。";DisposeOwnedProjection(house);return false;}
        }
        void ActivatePreparedFurniture(HousingHouse house)
        {
            if(!preparedFurniture.TryGetValue(house,out var seats))return;
            foreach(var seat in seats)
            {
                if(!seat.go||!seat.pose)throw new InvalidOperationException("准备中的家具已不可用");
                seat.go.transform.SetPositionAndRotation(seat.pose.position,seat.pose.rotation);
                SetField(seat.area,"_targetPos",seat.pose.position);SetField(seat.area,"_targetRot",seat.pose.eulerAngles);
                seat.go.SetActive(true);var net=seat.go.GetComponent<NetworkObject>();if(!net.IsSpawned)net.Spawn(destroyWithScene:true);
                if(seat.rest)ConfigureRestSeat(seat.rest);
            }
            preparedFurniture.Remove(house);
        }
        public void DisposeOwnedProjection(HousingHouse house)
        {
            if(!house||house.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone!=true)return;
            preparedFurniture.Remove(house);
            foreach(var seat in house.spawnedSeats.ToArray())if(seat)
            {
                var net=seat.GetComponent<NetworkObject>();
                if(net&&net.IsSpawned){if(NetworkManager.Singleton&&NetworkManager.Singleton.IsServer)net.Despawn(true);}
                else Destroy(seat);
            }
            house.spawnedSeats.Clear();house.gameObject.SetActive(false);Destroy(house.gameObject);
        }
        public sealed class DetachedHome
        {
            internal HousingPlot plot;internal HousingHouse house;internal bool active;
            internal readonly List<(GameObject go,bool active)> seats=new List<(GameObject,bool)>();
        }
        public DetachedHome DetachOwnedHome(HousingPlot plot)
        {
            if(plot?.house==null||plot.house.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone!=true)
                throw new InvalidOperationException("只能空置新增住宅");
            var detached=new DetachedHome{plot=plot,house=plot.house,active=plot.house.gameObject.activeSelf};
            foreach(var seat in detached.house.spawnedSeats)if(seat){detached.seats.Add((seat,seat.activeSelf));seat.SetActive(false);}
            detached.house.gameObject.SetActive(false);plot.house=null;return detached;
        }
        public void RestoreDetachedHome(DetachedHome detached)
        {
            if(detached?.house==null)return;
            if(detached.plot.house&&detached.plot.house!=detached.house)throw new InvalidOperationException("这块地已经有房子了");
            detached.plot.house=detached.house;detached.house.gameObject.SetActive(detached.active);
            foreach(var seat in detached.seats)if(seat.go)seat.go.SetActive(seat.active);
        }
        public void FinalizeDetachedHome(DetachedHome detached){if(detached?.house)DisposeOwnedProjection(detached.house);}
        public bool TryRemoveRetiredProjection(HousingPlot plot,out string error)
        {
            error=null;var house=plot?.house;if(!house||house.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone!=true)return true;
            if(!CanBuild)
            {
                var volume=HousingPrivacyVolume.Of(house);
                if(FindObjectsByType<PPG_Player>(FindObjectsSortMode.None).Any(p=>volume.Contains(p.transform.position,.6f))||
                   FindObjectsByType<ResidentController>(FindObjectsSortMode.None).Any(p=>volume.Contains(p.transform.position,.6f)))
                {error="等待住户离开旧住宅";return false;}
                var old=DetachOwnedHome(plot);FinalizeDetachedHome(old);return true;
            }
            if(!TryPrepareTransitionExits(new[]{house},Array.Empty<HousingHouse>(),out var exits,out error)||!exits.Apply(out error))return false;
            var detached=DetachOwnedHome(plot);FinalizeDetachedHome(detached);exits.Commit();return true;
        }

        public sealed class ExitPlan
        {
            internal sealed class PlayerMove{public PPG_Player actor;public Vector3 before,after,velocity;public Quaternion rotation;}
            internal sealed class NpcMove{public ResidentController actor;public Vector3 before,after,velocity;public Quaternion rotation;public NavMeshAgent agent;public NavMeshPath path;public bool hadPath,stopped,updatePosition,updateRotation;public float speed;public int agentType,areaMask;}
            sealed class Step{public Func<bool> ready,apply,inverse;}
            internal readonly List<PlayerMove> players=new List<PlayerMove>();internal readonly List<NpcMove> npcs=new List<NpcMove>();
            readonly List<Step> attempts=new List<Step>();bool committed;
            public string LastRollbackFailure{get;private set;}
            public bool Apply(out string error)
            {
                var steps=new List<Step>();
                foreach(var move in players)steps.Add(new Step{
                    ready=()=>move.actor&&move.actor.IsOwner&&!move.actor.IsUsingEmoteArea&&Vector3.Distance(move.actor.transform.position,move.before)<=.05f,
                    apply=()=>{move.actor.TeleportPlayer(move.after,move.rotation,true);return Vector3.Distance(move.actor.transform.position,move.after)<=.2f;},
                    inverse=()=>{if(!move.actor)return false;move.actor.TeleportPlayer(move.before,move.rotation,true);if(move.actor.FirstPersonCharacter)move.actor.FirstPersonCharacter.SetVelocity(move.velocity);return Vector3.Distance(move.actor.transform.position,move.before)<=.2f;}});
                foreach(var move in npcs)steps.Add(new Step{
                    ready=()=>move.actor&&move.actor.Brain!=null&&move.agent&&move.agent.enabled&&move.agent.isOnNavMesh&&move.agent.agentTypeID==move.agentType&&move.agent.areaMask==move.areaMask&&Vector3.Distance(move.actor.transform.position,move.before)<=.05f,
                    apply=()=>{move.actor.Brain.Movement.Stop();return move.actor.Brain.Movement.Warp(move.after,move.rotation)&&Vector3.Distance(move.actor.transform.position,move.after)<=.15f;},
                    inverse=()=>{
                        if(!move.actor||move.actor.Brain==null||!move.agent)return false;
                        if(!move.actor.Brain.Movement.Warp(move.before,move.rotation)||Vector3.Distance(move.actor.transform.position,move.before)>.15f||!move.agent.enabled||!move.agent.isOnNavMesh)return false;
                        bool restored=!move.hadPath||move.path!=null&&move.agent.SetPath(move.path);if(!move.hadPath)move.agent.ResetPath();
                        move.agent.speed=move.speed;move.agent.isStopped=move.stopped;move.agent.updatePosition=move.updatePosition;move.agent.updateRotation=move.updateRotation;move.agent.velocity=move.velocity;return restored;}});
                bool accepted=HousingAtomicBatch.TryApply(steps,x=>x.ready(),x=>x.apply(),x=>x.inverse(),attempts,out error);
                LastRollbackFailure=accepted||attempts.Count==0?null:error;return accepted;
            }
            public bool Rollback()
            {bool restored=HousingAtomicBatch.Rollback(attempts,x=>x.inverse(),out var error);LastRollbackFailure=error;return restored;}
            public void Commit()
            {
                if(committed)return;committed=true;attempts.Clear();
                // The original brain re-evaluates its unchanged schedule after
                // an authorized placement displacement. No source state-machine
                // resolver, activity record or original NPC model is replaced.
                foreach(var move in npcs)if(move.actor&&move.actor.Brain!=null){try{move.actor.Brain.RequestScheduleChange();}catch(Exception notification){Debug.LogWarning(notification);}}
            }
        }
        public bool TryPrepareTransitionExits(IEnumerable<HousingHouse> changing,IEnumerable<HousingHouse> incoming,out ExitPlan plan,out string error)
        {
            plan=null;error=null;if(!CanBuild){error="等待房主准备住宅";return false;}
            var changed=changing.Where(h=>h).Distinct().ToArray();var added=incoming.Where(h=>h).Distinct().ToArray();
            if(changed.Concat(added).Any(h=>h.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone!=true))
            {error="原住宅保持不变";return false;}
            foreach(var house in changed)
            {
                if(FindObjectsByType<HousingRestSession>(FindObjectsSortMode.None).Any(s=>s.Active&&s.Target&&s.Target.GetComponentInParent<HousingHouse>()==house)||
                   house.GetComponentsInChildren<HousingRestInteractable>(true).Any(r=>r.area&&r.area.IsOccupied||!string.IsNullOrEmpty(r.GetComponent<FriendRestOccupancy>()?.friendOccupantPersonId))||
                   house.spawnedSeats.Any(go=>go&&go.GetComponent<EmoteArea>()?.IsOccupied==true))
                {error="请等待住户起身后再调整住宅";return false;}
            }
            var volumes=Plots.Where(p=>p.house).Select(p=>HousingPrivacyVolume.Of(p.house)).Concat(added.Select(HousingPrivacyVolume.Of)).ToList();
            var exits=new ExitPlan();var reserved=new List<Vector3>();
            HousingHouse Affected(Vector3 position)=>changed.Concat(added).FirstOrDefault(h=>HousingPrivacyVolume.Of(h).Contains(position,.6f));
            Vector3 Origin(HousingHouse affected,Vector3 actual)
            {
                if(string.IsNullOrEmpty(affected.GetComponent<FriendHouseMetadata>()?.privatePersonId))return actual;
                var common=Plots.FirstOrDefault(p=>p.id==affected.plot)?.house;return common?(common.exit?common.exit.position:common.entry?common.entry.position:common.transform.position):actual;
            }
            foreach(var player in FindObjectsByType<PPG_Player>(FindObjectsSortMode.None).OrderBy(p=>p.OwnerClientId))
            {
                var affected=Affected(player.transform.position);if(!affected)continue;
                // A remote body requires its own actual safe-exit acknowledgement.
                // No local timeout/fake ACK permits deleting its occupied house.
                if(!player.IsOwner||player.IsUsingEmoteArea||!player.FirstPersonCharacter||!player.FirstPersonCharacter.characterMovement||player.FirstPersonCharacter.HasActiveMotion||player.FirstPersonCharacter.characterMovement.velocity.sqrMagnitude>.01f)
                {error="请先让住户离开住宅";return false;}
                var motor=player.FirstPersonCharacter.characterMovement;var capsule=motor.collider as CapsuleCollider;
                float offset=player.transform.position.y-motor.groundPoint.y;
                Vector3? Prepare(Vector3 point)
                {
                    var target=point+Vector3.up*offset;
                    if(!capsule||!CapsuleClear(capsule,player.transform,target-player.transform.position,motor.collisionLayers))return null;
                    motor.FindGround(motor.position+(target-player.transform.position),out var ground);return ground.isWalkableGround?(Vector3?)target:null;
                }
                if(!FriendHousingSafeLanding.Nearest(Origin(affected,player.transform.position),volumes,null,reserved,Prepare,out var target))
                {error="门口被挡住了，请换个位置";return false;}
                exits.players.Add(new ExitPlan.PlayerMove{actor=player,before=player.transform.position,after=target,rotation=player.transform.rotation,velocity=motor.velocity});reserved.Add(target);
            }
            foreach(var actor in FindObjectsByType<ResidentController>(FindObjectsSortMode.None).OrderBy(a=>a.ResidentId))
            {
                var affected=Affected(actor.transform.position);if(!affected)continue;
                var agent=actor.GetComponent<NavMeshAgent>();
                if(actor.Brain==null||!agent||!agent.enabled||!agent.isOnNavMesh||agent.pathPending||agent.isOnOffMeshLink||actor.Runtime!=null&&(actor.Runtime.IsWorking||actor.Runtime.IsInteracting||actor.Runtime.IsSnapshotApplying))
                {error="请稍等";return false;}
                var capsule=actor.GetComponentInChildren<CapsuleCollider>();
                Vector3? Prepare(Vector3 point)
                {
                    bool blocked=capsule?!CapsuleClear(capsule,actor.transform,point-actor.transform.position,~0):
                        Physics.OverlapCapsule(point+Vector3.up*(agent.radius+.025f),point+Vector3.up*Mathf.Max(agent.radius+.025f,agent.height-agent.radius),agent.radius,~0,QueryTriggerInteraction.Ignore).Any(c=>!c.transform.IsChildOf(actor.transform));
                    return blocked?null:(Vector3?)point;
                }
                if(!FriendHousingSafeLanding.Nearest(Origin(affected,actor.transform.position),volumes,agent,reserved,Prepare,out var point))
                {error="门口被挡住了，请换个位置";return false;}
                exits.npcs.Add(new ExitPlan.NpcMove{actor=actor,before=actor.transform.position,after=point,rotation=actor.transform.rotation,agent=agent,
                    path=agent.hasPath?agent.path:null,hadPath=agent.hasPath,stopped=agent.isStopped,updatePosition=agent.updatePosition,updateRotation=agent.updateRotation,
                    speed=agent.speed,velocity=agent.velocity,agentType=agent.agentTypeID,areaMask=agent.areaMask});reserved.Add(point);
            }
            plan=exits;return true;
        }
        static bool CapsuleClear(CapsuleCollider capsule,Transform actor,Vector3 delta,int layers)
        {
            var scale=capsule.transform.lossyScale;if(capsule.direction!=1||Vector3.Dot(capsule.transform.up,Vector3.up)<.999f)return false;
            float radius=capsule.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));float half=Mathf.Max(0,capsule.height*Mathf.Abs(scale.y)/2-radius);
            var center=capsule.transform.TransformPoint(capsule.center)+delta;var up=capsule.transform.up;
            foreach(var other in Physics.OverlapCapsule(center+up*half,center-up*half,radius,layers,QueryTriggerInteraction.Ignore))
            {
                if(other.transform.IsChildOf(actor))continue;
                if(Physics.ComputePenetration(capsule,capsule.transform.position+delta,capsule.transform.rotation,other,other.transform.position,other.transform.rotation,out var normal,out var depth)&&
                   (Vector3.Dot(normal,Vector3.up)<.5f||depth>capsule.contactOffset+.024f))return false;
            }
            return true;
        }
    }
}
