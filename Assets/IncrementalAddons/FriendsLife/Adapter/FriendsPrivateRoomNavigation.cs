using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.ResidentialIsland;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    // Runtime navigation for a NEW offstage private-room clone only. The source
    // world's surfaces, agent profile, original geometry and portal are intact.
    public sealed class FriendsPrivateRoomNavigation : MonoBehaviour
    {
        NavMeshData data;NavMeshDataInstance instance;int agentType;Vector3 position,scale;Quaternion rotation;
        public string LastFailure{get;private set;}
        public int SourceCount{get;private set;}
        public static bool Ensure(HousingPrivateRoom room,ResidentController actor,out string failure)
        {
            failure=null;var runtime=FriendsLifeRuntime.Instance;var economy=FriendsEconomyService.Instance;
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!room||!room.house||!actor||!FriendResidentBindings.IsOwned(actor)||
                room.house.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone!=true||!economy||!economy.CanEnter(room.house,actor.GetComponent<FriendWorldActor>()?.personId))
            {failure="An authorized actual NEW private room and owned NPC are required";return false;}
            var self=room.house.GetComponent<FriendsPrivateRoomNavigation>();if(!self)self=room.house.gameObject.AddComponent<FriendsPrivateRoomNavigation>();
            return self.Prepare(room.house,actor,out failure);
        }
        bool Prepare(HousingHouse house,ResidentController actor,out string failure)
        {
            failure=null;var agent=actor.GetComponent<NavMeshAgent>();var capsule=actor.GetComponent<CapsuleCollider>();
            if(!agent||!capsule||capsule.direction!=1){failure=LastFailure="The actual upright navigation/capsule binding is unavailable";return false;}
            if(instance.valid&&agentType==agent.agentTypeID&&position==transform.position&&scale==transform.lossyScale&&Quaternion.Angle(rotation,transform.rotation)<.01f)return true;
            Release();
            var colliders=house.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger&&!c.GetComponentInParent<EmoteArea>()).ToArray();
            if(colliders.Length==0){failure=LastFailure="The private room has no actual floor/collision sources";return false;}
            var bounds=colliders[0].bounds;foreach(var c in colliders.Skip(1))bounds.Encapsulate(c.bounds);bounds.Expand(new Vector3(.5f,.5f,.5f));
            var sources=new List<NavMeshBuildSource>();NavMeshBuilder.CollectSources(bounds,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            sources.RemoveAll(s=>!s.component||!(s.component is Collider c)||!c.enabled||!c.gameObject.activeInHierarchy||c.isTrigger||
                c.GetComponentInParent<HousingHouse>()!=house||c.GetComponentInParent<EmoteArea>()||c.GetComponentInParent<ResidentController>());
            SourceCount=sources.Count;if(SourceCount==0){failure=LastFailure="The owned private floor projection collected no physical geometry";return false;}
            var bodyScale=capsule.transform.lossyScale;float radius=capsule.radius*Mathf.Max(Mathf.Abs(bodyScale.x),Mathf.Abs(bodyScale.z));
            float height=Mathf.Max(radius*2,capsule.height*Mathf.Abs(bodyScale.y));
            var settings=NavMesh.GetSettingsByID(agent.agentTypeID);settings.agentRadius=Mathf.Max(settings.agentRadius,radius);settings.agentHeight=Mathf.Max(settings.agentHeight,height);
            settings.overrideVoxelSize=true;settings.voxelSize=Mathf.Min(.04f,NavMesh.GetSettingsByID(agent.agentTypeID).agentRadius/3);
            // Keep small physically valid room fragments; a complete path and
            // the actual body are still required by the movement actuator.
            settings.minRegionArea=0;
            data=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
            if(!data){failure=LastFailure="The actual private sources produced no navigation data";return false;}
            data.name="Owned private room navigation "+house.plot;instance=NavMesh.AddNavMeshData(data);
            if(!instance.valid){Release();failure=LastFailure="The owned private navigation projection could not register";return false;}
            agentType=agent.agentTypeID;position=transform.position;rotation=transform.rotation;scale=transform.lossyScale;LastFailure=null;return true;
        }
        public static bool AlignActualBody(ResidentController actor,out string failure)
        {
            failure=null;var agent=actor?actor.GetComponent<NavMeshAgent>():null;if(!actor||actor.Brain==null||!agent)return false;
            if(agent.enabled&&agent.isOnNavMesh&&Vector3.Distance(actor.transform.position,agent.nextPosition)<=.15f)return true;
            var before=actor.transform.position;var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            if(!NavMesh.SamplePosition(before,out var nav,.25f,filter)||Mathf.Abs(nav.position.y-before.y)>.15f)
            {failure="The actual private body has no same-type navigation immediately below it";return false;}
            if(!actor.Brain.Movement.Warp(nav.position,Quaternion.Euler(0,actor.transform.eulerAngles.y,0)))
            {failure="The original movement rejected local private-room navigation alignment";return false;}
            actor.Brain.Movement.MatchAgentPositionAtHeight(agent.nextPosition.y);
            if(Vector3.Distance(actor.transform.position,before)>.25f||Vector3.Distance(actor.transform.position,agent.nextPosition)>.15f)
            {failure="Local private-room alignment exceeded its actual endpoint";return false;}
            return true;
        }
        void Release(){if(instance.valid)instance.Remove();if(data)Destroy(data);data=null;}
        void OnDestroy(){Release();}
    }
}
