using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Patterns;
using SushiAndFish.CityExpansion;
using SushiAndFish.ResidentialIsland;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using OriginalIsland = SushiAndFish.ResidentialIsland.HousingIsland;

namespace SushiAndFish.FriendsLife
{
    // One facade over the original island. Plot discovery, its original watcher,
    // original UI and legacy houses remain owned by OriginalIsland.
    public sealed partial class FriendHousingIsland : MonoBehaviour
    {
        static FriendHousingIsland instance;
        OriginalIsland original;
        int observedFrame;
        string failure;
        public static FriendHousingIsland Instance
        {
            get
            {
                if(instance)return instance;
                var source=OriginalIsland.Instance;if(!source)return null;
                instance=source.GetComponent<FriendHousingIsland>();
                if(!instance)instance=source.gameObject.AddComponent<FriendHousingIsland>();
                return instance;
            }
        }
        public HousingCatalog Catalog=>original?original.Catalog:null;
        public List<HousingPlot> Plots=>original.Plots;
        public PPG_Player Player=>original?original.Player:null;
        public bool CanBuild=>original&&original.CanBuild;
        public bool CanProject=>original&&NetworkManager.Singleton&&Player&&Player.IsSpawned;
        public bool InitialHousingLoaded=>CanProject&&Plots.Count>0&&Time.frameCount>observedFrame;
        public string LastFailure=>failure??(original?original.LastFailure:null);
        public string ArrivalPersonId{get;private set;}
        public bool HasArrivalTask=>!string.IsNullOrEmpty(ArrivalPersonId);
        public event Action Changed;
        void Awake(){instance=this;original=GetComponent<OriginalIsland>();observedFrame=Time.frameCount;original.Changed+=OriginalChanged;if(!GetComponent<FriendHousingPrivacy>())gameObject.AddComponent<FriendHousingPrivacy>();}
        void OriginalChanged(){observedFrame=Time.frameCount;Changed?.Invoke();}
        void OnDestroy(){if(original)original.Changed-=OriginalChanged;if(instance==this)instance=null;}
        public void BeginArrival(string personId){ArrivalPersonId=personId;Changed?.Invoke();}
        public void FinishArrival(){ArrivalPersonId=null;Changed?.Invoke();}
        public void SetFailure(string message){failure=message;}
        public void ClearFailure(){failure=null;original?.ClearFailure();}
        public void Highlight(HousingPlot selected)=>original?.Highlight(selected);
        public bool Fits(HousingPlot plot,HousingType type,float yaw,out string error)=>original.Fits(plot,type,yaw,out error);
        public void NotifyChanged(){FriendsLifeRuntime.Instance?.NotifyChanged();Changed?.Invoke();}
        public void Save(){FriendsLifeRuntime.Instance?.NotifyChanged();FriendsLifeRuntime.Instance?.SaveNow();}
        // The existing public configuration helper is used only on freshly
        // instantiated native seat clones, never on baseline live actors.
        public static void SetField(object owner,string field,object value)=>OriginalIsland.SetField(owner,field,value);

        public bool Place(HousingPlot plot,HousingType type,float yaw,bool persist=true,string arrivalPersonId=null)
        {
            if(persist)
            {
                string arriving=arrivalPersonId??ArrivalPersonId;var economy=FriendsEconomyService.Instance;
                if(!economy||!economy.TryPlaceArrivalHome(arriving,plot,type,yaw)){SetFailure(economy?.LastFailure??"入住资料尚未准备完成");return false;}
                if(ArrivalPersonId==arriving)FinishArrival();failure=null;Changed?.Invoke();return true;
            }
            if(!CanProject||plot==null||plot.Occupied){SetFailure("没有空地");return false;}
            var service=FriendsEconomyService.Instance;
            var row=service?.State.placements.FirstOrDefault(r=>r.plot==plot.id&&r.type==type?.id&&!HousingVacancyRules.IsRetired(service.State.vacatedHomes,r.plot,r.instanceId));
            if(row==null){SetFailure("住宅加载失败，请重试");return false;}
            if(!Fits(plot,type,yaw,out var error)){SetFailure(error);return false;}
            var house=Construct(plot,type,yaw);if(!house)return false;house.gameObject.SetActive(false);
            if(row.hasTransform)house.transform.SetPositionAndRotation(row.position,row.rotation);
            FriendHouseMetadata.For(house).legacyPlayerOwned=row.legacyPlayerOwned;FriendHouseMetadata.For(house).instanceId=HousingVacancyRules.Generation(row.plot,row.instanceId);
            foreach(var door in house.GetComponentsInChildren<HousingDoor>(true)){var state=row.doors?.FirstOrDefault(d=>d.path==RelativePath(door.transform,house.transform));if(state!=null)FriendHousingDoor.SetOpenInstant(door,state.open);}
            ExitPlan exits=null;
            if(CanBuild)
            {
                if(!PrepareFurniture(house,out error)||!TryPrepareTransitionExits(Array.Empty<HousingHouse>(),new[]{house},out exits,out error))
                {DisposeOwnedProjection(house);SetFailure(error);return false;}
            }
            bool realized=false;
            try
            {
                if(exits!=null&&!exits.Apply(out error))throw new InvalidOperationException(error);
                plot.house=house;ActivatePrivateInterior(house);Physics.SyncTransforms();realized=true;exits?.Commit();failure=null;Changed?.Invoke();return true;
            }
            catch(Exception exception)
            {
                if(realized){SetFailure(null);Debug.LogWarning(exception);return true;}
                if(plot.house==house)plot.house=null;DisposeOwnedProjection(house);exits?.Rollback();Debug.LogWarning("[FriendsLife] Housing place failed: "+exception);SetFailure("住宅操作失败，已恢复原样。请重试。");return false;
            }
        }
        static bool InsidePlacement(Vector3 position,HousingPlot plot,HousingType type,float yaw)
        {var local=Quaternion.Inverse(Quaternion.Euler(0,yaw,0))*(position-plot.Center);return local.y>-2&&local.y<5&&Mathf.Abs(local.x)<type.footprint.x/2+.6f&&Mathf.Abs(local.z)<type.footprint.y/2+.6f;}
        HousingHouse Construct(HousingPlot plot,HousingType type,float yaw)
        {
            GameObject go=null;
            try
            {
                go=Instantiate(type.prefab);go.name="FriendResidence_"+plot.id+"_"+type.id;
                SceneManager.MoveGameObjectToScene(go,plot.source.gameObject.scene);
                var center=plot.Center;var terrain=Terrain.activeTerrains.FirstOrDefault(t=>center.x>=t.transform.position.x&&center.x<=t.transform.position.x+t.terrainData.size.x&&center.z>=t.transform.position.z&&center.z<=t.transform.position.z+t.terrainData.size.z);
                if(terrain)center.y=terrain.SampleHeight(center)+terrain.transform.position.y;
                go.transform.SetPositionAndRotation(center+Vector3.up*.035f,Quaternion.Euler(0,yaw,0));
                var house=go.GetComponent<HousingHouse>();if(!house)throw new InvalidOperationException("住宅资源缺少房屋组件");
                house.definition=type;house.plot=plot.id;FriendHouseMetadata.MarkNew(house);go.AddComponent<HousingNavigation>();return house;
            }
            catch(Exception exception){if(go)Destroy(go);Debug.LogWarning("[FriendsLife] Housing construct failed: "+exception);SetFailure("住宅操作失败，已恢复原样。请重试。");return null;}
        }
        HousingHouse ReplaceCanonicalProjection(HousingPlot plot,FriendHousingPlacement row)
        {
            // A non-legacy row is the saved ownership evidence required to
            // replace an original sidecar projection with a new owned clone.
            if(row.legacyPlayerOwned&&!row.friendsOwnedClone||!CanProject)return null;
            var type=Catalog.homes.FirstOrDefault(t=>t.id==row.type);if(type==null)return null;
            var old=plot.house;var house=Construct(plot,type,row.yaw);if(!house)return null;
            plot.house=house;ActivatePrivateInterior(house);if(old)Destroy(old.gameObject);return house;
        }
        public HousingHouse PrepareReplacement(HousingPlot plot,HousingType type,out string failure)
        {
            failure=null;if(!CanProject||plot?.house==null){failure="住宅不可用";return null;}
            var old=plot.house;if(!FriendHouseMetadata.For(old).friendsOwnedClone){failure="原住宅保持不变";return null;}float yaw=old.transform.eulerAngles.y;
            if(!Fits(plot,type,yaw,out failure))return null;
            var go=Instantiate(type.prefab);go.SetActive(false);go.name="PreparedResidence_"+plot.id+"_"+type.id;
            SceneManager.MoveGameObjectToScene(go,plot.source.gameObject.scene);
            go.transform.SetPositionAndRotation(old.transform.position,old.transform.rotation);
            var house=go.GetComponent<HousingHouse>();house.definition=type;house.plot=plot.id;FriendHouseMetadata.MarkNew(house);FriendHouseMetadata.For(house).legacyPlayerOwned=FriendHouseMetadata.For(old).legacyPlayerOwned;FriendHouseMetadata.For(house).instanceId=HomeIdentity(old);
            if(old.entry&&house.entry)go.transform.position+=old.entry.position-house.entry.position;
            var inv=Quaternion.Inverse(go.transform.rotation);var center=inv*(plot.Center-go.transform.position);
            float halfW=type.footprint.x/2,halfD=type.footprint.y/2;
            foreach(var corner in new[]{new Vector3(-halfW,0,-halfD),new Vector3(-halfW,0,halfD),new Vector3(halfW,0,-halfD),new Vector3(halfW,0,halfD)})
            {var world=go.transform.TransformPoint(corner);if(!plot.rect.Contains(new Vector2(world.x,world.z))){failure="保留门口后住宅超出地块";Destroy(go);return null;}}
            go.AddComponent<HousingNavigation>();
            if(!PrepareFurniture(house,out failure)){DisposeOwnedProjection(house);return null;}
            return house;
        }

        public void ActivatePrepared(HousingPlot plot,HousingHouse prepared)
        {
            if(plot==null||!prepared)throw new ArgumentException("Prepared residence is missing");
            if(!FriendHouseMetadata.For(prepared).friendsOwnedClone||plot.house&&!FriendHouseMetadata.For(plot.house).friendsOwnedClone)
                throw new InvalidOperationException("Only an owned new residence may be upgraded");
            var old=plot.house;plot.house=prepared;prepared.name="Residence_"+plot.id+"_"+prepared.definition.id;
            try{ActivatePrivateInterior(prepared);if(old)DisposeOwnedProjection(old);Physics.SyncTransforms();}
            catch{plot.house=old;DisposeOwnedProjection(prepared);throw;}
        }

        public void DisposePrepared(HousingHouse prepared)=>DisposeOwnedProjection(prepared);

        public bool PrepareUpgradeExit(HousingPlot plot,HousingType next)
        {
            if(!CanBuild||plot?.house==null)return false;var old=plot.house;
            if(!FriendHouseMetadata.For(old).friendsOwnedClone){failure="原住宅保持不变";return false;}
            foreach(var session in FindObjectsByType<HousingRestSession>(FindObjectsSortMode.None))
                if(session.Active&&session.Target&&session.Target.GetComponentInParent<HousingHouse>()==old){session.RequestExit();failure="等待住户起身后升级";return false;}
            var moving=new List<(PPG_Player player,Vector3 position)>();var reserved=new List<Vector3>();
            var volumes=Plots.Where(p=>p.house).Select(p=>HousingPrivacyVolume.Of(p.house)).ToList();volumes.Add(new HousingPrivacyVolume{center=plot.Center,rotation=old.transform.rotation,size=next.footprint});
            foreach(var player in FindObjectsByType<PPG_Player>(FindObjectsSortMode.None))
            {
                var point=old.transform.InverseTransformPoint(player.transform.position);if(Mathf.Abs(point.x)>old.definition.footprint.x/2+.5f||Mathf.Abs(point.z)>old.definition.footprint.y/2+.5f||Mathf.Abs(point.y)>5)continue;
                if(player.IsUsingEmoteArea)player.EndStaticEmote();
                if(!HousingNpcLanding.Nearest(player.transform.position,volumes,null,reserved,out var safe)){failure="未找到住户可用的安全出口";return false;}
                moving.Add((player,safe));reserved.Add(safe);
            }
            var privacy=GetComponent<FriendHousingPrivacy>();if(privacy&&!privacy.RelocateForPlacement(plot,next,old.transform.eulerAngles.y)){failure="未找到住户可用的安全出口";return false;}
            foreach(var move in moving)move.player.TeleportPlayer(move.position,move.player.transform.rotation,true);return true;
        }

        public HousingHouse CreatePrivateInterior(HousingType type,string homeId,string personId,Vector3 position,bool prepareOnly=false)
        {
            if(!CanProject||type?.prefab==null)return null;
            if(!Singleton<ResidentManager>.TryGetInstance(out var residents))return null;
            var residentAgent=residents.Residents.Where(r=>r).Select(r=>r.GetComponent<NavMeshAgent>()).FirstOrDefault(a=>a);
            if(!residentAgent)return null;GameObject go=null;HousingHouse house=null;
            try
            {
                go=Instantiate(type.prefab);go.name="PrivateRoom_"+homeId+"_"+personId;go.transform.SetPositionAndRotation(position,Quaternion.identity);
                var plot=Plots.FirstOrDefault(p=>p.id==homeId);if(plot?.source)SceneManager.MoveGameObjectToScene(go,plot.source.gameObject.scene);
                house=go.GetComponent<HousingHouse>();if(!house)throw new InvalidOperationException("住宅资源缺少房屋组件");
                house.definition=type;house.plot=homeId;FriendHouseMetadata.MarkNew(house);FriendHouseMetadata.For(house).privatePersonId=personId;
                foreach(var door in go.GetComponentsInChildren<HousingDoor>(true))FriendHousingDoor.SetOpenInstant(door,true);
                var surface=go.AddComponent<Unity.AI.Navigation.NavMeshSurface>();surface.agentTypeID=residentAgent.agentTypeID;surface.collectObjects=Unity.AI.Navigation.CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();
                if(prepareOnly){go.SetActive(false);if(!PrepareFurniture(house,out failure)){DisposeOwnedProjection(house);return null;}return house;}
                ActivatePrivateInterior(house);return house;
            }
            catch(Exception exception){if(house&&house.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone==true)DisposeOwnedProjection(house);else if(go)Destroy(go);Debug.LogWarning("[FriendsLife] Private interior failed: "+exception);failure="住宅操作失败，已恢复原样。请重试。";return null;}
        }

        public void ActivatePrivateInterior(HousingHouse house)
        {
            if(!house||!FriendHouseMetadata.For(house).friendsOwnedClone)return;house.gameObject.SetActive(true);
            if(!house.GetComponent<FriendsChairApproachProjection>())house.gameObject.AddComponent<FriendsChairApproachProjection>();
            if(CanBuild&&preparedFurniture.ContainsKey(house))ActivatePreparedFurniture(house);
            else if(CanBuild&&house.spawnedSeats.Count==0)
            {
                foreach(var a in house.chairs)SpawnNativeSeat(house,a);
                foreach(var rest in house.GetComponentsInChildren<HousingRestInteractable>(true))
                {var anchor=rest.GetComponent<HousingSeatAnchor>();if(!anchor)anchor=rest.gameObject.AddComponent<HousingSeatAnchor>();anchor.pose=rest.exit;anchor.exit=rest.exit;rest.area=SpawnNativeSeat(house,anchor);ConfigureRestSeat(rest);}
            }
            if(!CanBuild)BindClientFurniture(house);
            var privacy=GetComponent<FriendHousingPrivacy>();if(!privacy)privacy=gameObject.AddComponent<FriendHousingPrivacy>();privacy.Register(house);
        }

        static void ConfigureRestSeat(HousingRestInteractable rest)
        {
            if(!rest.area)return;foreach(var c in rest.area.GetComponentsInChildren<Collider>())c.enabled=false;
            rest.area.ConfigureCustomPlayerInput(rest.GetComponentsInChildren<Collider>(true));
            var dining=rest.area.GetComponent<CityDiningSeat>();if(dining)dining.enabled=false;if(rest.area.WorldUIAnchor){rest.area.WorldUIAnchor.SetVisible(false);rest.area.WorldUIAnchor.enabled=false;}
        }

        public void BindClientFurniture(HousingHouse house)
        {
            if(CanBuild||!house||!FriendHouseMetadata.For(house).friendsOwnedClone)return;var areas=FindObjectsByType<EmoteArea>(FindObjectsSortMode.None).Where(a=>a.IsSpawned&&a.GetComponent<CityDiningSeat>()).ToArray();
            EmoteArea Near(Transform pose)=>pose?areas.OrderBy(a=>Vector3.Distance(a.transform.position,pose.position)).FirstOrDefault(a=>Vector3.Distance(a.transform.position,pose.position)<.6f):null;
            void Bind(EmoteArea area,Transform pose,Transform approach)
            {if(!area||!pose)return;SetField(area,"_targetPos",pose.position);SetField(area,"_targetRot",pose.eulerAngles);var dining=area.GetComponent<CityDiningSeat>();dining.seatedPose=pose;dining.approach=approach;}
            foreach(var anchor in house.chairs)Bind(Near(anchor.pose),anchor.pose,anchor.exit);
            foreach(var rest in house.GetComponentsInChildren<HousingRestInteractable>(true)){if(!rest.area)rest.area=Near(rest.exit);Bind(rest.area,rest.exit,rest.exit);ConfigureRestSeat(rest);}
        }

        EmoteArea SpawnNativeSeat(HousingHouse house,HousingSeatAnchor anchor)
        {
            var go=Instantiate(Catalog.nativeSeatPrefab,anchor.pose.position,anchor.pose.rotation);
            go.name="NativeResidenceSeat_"+house.plot;SceneManager.MoveGameObjectToScene(go,house.gameObject.scene);
            var area=go.GetComponent<EmoteArea>();SetField(area,"_targetPos",anchor.pose.position);SetField(area,"_targetRot",anchor.pose.eulerAngles);
            var dining=go.GetComponent<CityDiningSeat>();dining.seatedPose=anchor.pose;dining.approach=anchor.exit;
            var net=go.GetComponent<NetworkObject>();net.Spawn(destroyWithScene:true);house.spawnedSeats.Add(go);
            return area;
        }

        static string RelativePath(Transform item,Transform root)
        {string value=item.name;for(var parent=item.parent;parent&&parent!=root;parent=parent.parent)value=parent.name+"/"+value;return value;}

        public static FriendHousingPlacement CapturePlacement(HousingHouse house)=>new FriendHousingPlacement
        {
            plot=house.plot,type=house.definition.id,instanceId=HomeIdentity(house),yaw=house.transform.eulerAngles.y,hasTransform=true,position=house.transform.position,rotation=house.transform.rotation,legacyPlayerOwned=FriendHouseMetadata.For(house).legacyPlayerOwned,friendsOwnedClone=FriendHouseMetadata.For(house).friendsOwnedClone,
            doors=house.GetComponentsInChildren<HousingDoor>(true).Select(d=>new FriendHousingDoorState{path=RelativePath(d.transform,house.transform),open=d.IsOpen}).ToArray()
        };

        public void ApplyPlacementProjection(HousingPlot plot,FriendHousingPlacement row)
        {
            if(plot?.house==null)return;var house=plot.house;if(!FriendHouseMetadata.For(house).friendsOwnedClone){if(row.legacyPlayerOwned&&!row.friendsOwnedClone)return;house=ReplaceCanonicalProjection(plot,row);if(!house)return;}FriendHouseMetadata.For(house).legacyPlayerOwned=row.legacyPlayerOwned;FriendHouseMetadata.For(house).instanceId=HousingVacancyRules.Generation(row.plot,row.instanceId);
            if(row.hasTransform)
            {
                house.transform.SetPositionAndRotation(row.position,row.rotation);int index=0;
                foreach(var anchor in house.chairs)if(index<house.spawnedSeats.Count)UpdateSeatProjection(house.spawnedSeats[index++],anchor.pose);
                foreach(var rest in house.GetComponentsInChildren<HousingRestInteractable>(true))if(index<house.spawnedSeats.Count)UpdateSeatProjection(house.spawnedSeats[index++],rest.exit);
            }
            foreach(var door in house.GetComponentsInChildren<HousingDoor>(true))
            {var state=row.doors?.FirstOrDefault(d=>d.path==RelativePath(door.transform,house.transform));if(state!=null)FriendHousingDoor.SetOpenInstant(door,state.open);}
            Physics.SyncTransforms();
        }

        static void UpdateSeatProjection(GameObject seat,Transform pose)
        {if(!seat||!pose)return;seat.transform.SetPositionAndRotation(pose.position,pose.rotation);var area=seat.GetComponent<EmoteArea>();if(area){SetField(area,"_targetPos",pose.position);SetField(area,"_targetRot",pose.eulerAngles);}}
    }
}

