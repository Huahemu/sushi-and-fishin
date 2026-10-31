using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.ResidentialIsland;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    // Keep the existing ground NavMesh continuous through the real doorway.
    // Solid walls/furniture and moving door leaves carve their actual geometry;
    // the privacy guard still rejects people without a home/episode permit.
    public sealed class HousingNavigation : MonoBehaviour
    {
        HousingHouse house;bool built;HousingDoor[] doors;float nextDoorCheck,homePoseReadyAt;
        public bool HomeNavigationReady=>built&&Time.unscaledTime>=homePoseReadyAt;
        IEnumerator Start(){yield return null;Build();}
        void Build()
        {
            house=GetComponent<HousingHouse>();if(built||!house||house.definition==null)return;built=true;doors=GetComponentsInChildren<HousingDoor>(true);
            float floor=house.entry?house.entry.position.y:transform.position.y;
            foreach(var collider in GetComponentsInChildren<Collider>(true))
            {
                if(!collider.enabled||collider.isTrigger||collider.GetComponentInParent<global::EmoteArea>())continue;
                var bounds=collider.bounds;if(bounds.max.y<floor+.28f||bounds.min.y>floor+1.75f||bounds.size.x<.015f||bounds.size.z<.015f)continue;
                // A combined wall mesh surrounds empty rooms. Its bounding box
                // is not solid: query the actual PhysX mesh at walking height.
                if(collider is MeshCollider&&bounds.size.x>2&&bounds.size.z>2&&bounds.size.x*bounds.size.z>8)
                {BuildMeshFootprint(collider,bounds,floor);continue;}
                bounds.SetMinMax(new Vector3(bounds.min.x,floor-.1f,bounds.min.z),new Vector3(bounds.max.x,floor+2,bounds.max.z));
                var door=collider.GetComponentInParent<HousingDoor>();var parent=door?(door.pivot?door.pivot:door.transform):transform;
                AddSolid(bounds,parent,"NavigationSolid_"+collider.name,!door);
            }
            // NavMesh carving is applied after the obstacle creation frame.
            homePoseReadyAt=Time.unscaledTime+.25f;
        }
        public static bool TryResolveFriendHome(HousingHouse home,NavMeshAgent agent,out Vector3 position,out Quaternion rotation,bool forSpawn=false)
        {
            position=default;rotation=Quaternion.identity;
            if(!home||!home.entry||!agent)return false;
            var navigation=home.GetComponent<HousingNavigation>();
            if(!navigation||!navigation.HomeNavigationReady)return false;
            // WakeExit is a bed-animation pose, including its imported downward
            // orientation. A friend's home waypoint belongs outside the entry.
            var outward=-Vector3.ProjectOnPlane(home.entry.forward,Vector3.up);
            if(outward.sqrMagnitude<.0001f)outward=Vector3.ProjectOnPlane(home.transform.forward,Vector3.up);
            if(outward.sqrMagnitude<.0001f)return false;
            outward.Normalize();var sideways=Vector3.Cross(Vector3.up,outward);
            rotation=Quaternion.LookRotation(outward,Vector3.up);
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            var settings=NavMesh.GetSettingsByID(agent.agentTypeID);
            float radius=Mathf.Max(.05f,agent.radius),height=Mathf.Max(radius*2,agent.height);
            foreach(float distance in new[]{1.5f,1.9f,2.3f})foreach(float side in new[]{0f,.45f,-.45f})
            {
                var candidate=home.entry.position+outward*distance+sideways*side;
                if(!NavMesh.SamplePosition(candidate,out var nav,.25f,filter))continue;
                var ground=Physics.RaycastAll(candidate+Vector3.up,Vector3.down,3,~0,QueryTriggerInteraction.Ignore)
                    .Where(h=>h.normal.y>=Mathf.Cos(settings.agentSlope*Mathf.Deg2Rad)&&!h.collider.GetComponentInParent<ResidentController>()&&!h.collider.GetComponentInParent<PaperPixelGames.Core.Player.PPG_Player>())
                    .OrderBy(h=>Mathf.Abs(h.point.y-nav.position.y)).FirstOrDefault();
                if(!ground.collider||Mathf.Abs(ground.point.y-nav.position.y)>Mathf.Max(.15f,settings.agentClimb*.5f))continue;
                var feet=nav.position;feet.y=Mathf.Max(feet.y,ground.point.y)+.04f;
                bool blocked=Physics.OverlapCapsule(feet+Vector3.up*(radius+.02f),feet+Vector3.up*(height-radius),radius,~0,QueryTriggerInteraction.Ignore)
                    .Any(c=>forSpawn||(!c.GetComponentInParent<ResidentController>()&&!c.GetComponentInParent<PaperPixelGames.Core.Player.PPG_Player>()));
                if(blocked)continue;
                position=nav.position;return true;
            }
            return false;
        }
        void BuildMeshFootprint(Collider source,Bounds bounds,float floor)
        {
            const float cell=.15f;var hits=new Collider[64];var rectangles=new List<Bounds>();
            int nx=Mathf.CeilToInt(bounds.size.x/cell),nz=Mathf.CeilToInt(bounds.size.z/cell);
            var previous=new Dictionary<(int,int),int>();
            for(int z=0;z<nz;z++)
            {
                var current=new Dictionary<(int,int),int>();int start=-1;
                for(int x=0;x<=nx;x++)
                {
                    bool occupied=false;
                    if(x<nx)
                    {
                        var point=new Vector3(bounds.min.x+(x+.5f)*cell,floor+1.015f,bounds.min.z+(z+.5f)*cell);
                        int count=Physics.OverlapBoxNonAlloc(point,new Vector3(cell*.5f,.735f,cell*.5f),hits,Quaternion.identity,1<<source.gameObject.layer,QueryTriggerInteraction.Ignore);
                        for(int k=0;k<count;k++)if(hits[k]==source){occupied=true;break;}
                    }
                    if(occupied){if(start<0)start=x;continue;}if(start<0)continue;
                    var key=(start,x-start);int index;
                    if(previous.TryGetValue(key,out index))
                    {var rectangle=rectangles[index];rectangle.Encapsulate(new Vector3(rectangle.center.x,rectangle.center.y,bounds.min.z+(z+1)*cell));rectangles[index]=rectangle;}
                    else
                    {index=rectangles.Count;rectangles.Add(new Bounds(new Vector3(bounds.min.x+(start+(x-start)*.5f)*cell,floor+.95f,bounds.min.z+(z+.5f)*cell),new Vector3((x-start)*cell,2.1f,cell)));}
                    current[key]=index;start=-1;
                }
                previous=current;
            }
            foreach(var rectangle in rectangles)AddSolid(rectangle,transform,"NavigationWall_"+source.name,true);
        }
        static void AddSolid(Bounds bounds,Transform parent,string solidName,bool stationary)
        {
            var local=new Bounds(parent.InverseTransformPoint(bounds.center),Vector3.zero);
            foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})local.Encapsulate(parent.InverseTransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z))));
            var solid=new GameObject(solidName);solid.transform.SetParent(parent,false);
            var obstacle=solid.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=local.center;obstacle.size=local.size;
            obstacle.carving=true;obstacle.carveOnlyStationary=stationary;obstacle.carvingTimeToStationary=.05f;obstacle.carvingMoveThreshold=.04f;
        }
        public static void PrepareDoorRoute(ResidentController actor,Vector3 destination)
        {
            var runtime=FriendsLifeRuntime.Instance;var economy=FriendsEconomyService.Instance;
            if(!actor||!runtime||!runtime.IsAuthority||!economy)return;string person=economy.PersonId(actor);
            foreach(var navigation in FindObjectsByType<HousingNavigation>(FindObjectsSortMode.None))
            {
                var home=navigation.house?navigation.house:navigation.GetComponent<HousingHouse>();if(!home||!economy.CanEnter(home,person))continue;
                var volume=HousingPrivacyVolume.Of(home);
                if(!volume.Contains(actor.transform.position,1.5f)&&!volume.Contains(destination,.5f))continue;
                foreach(var door in navigation.doors??home.GetComponentsInChildren<HousingDoor>(true))if(door)FriendHousingDoor.OpenForResident(door);
            }
        }
        void Update()
        {
            if(!house||!built)return;var runtime=FriendsLifeRuntime.Instance;var economy=FriendsEconomyService.Instance;
            if(!runtime||!runtime.IsAuthority||!runtime.IsWorldRunning||!economy)return;
            if(Time.unscaledTime<nextDoorCheck)return;nextDoorCheck=Time.unscaledTime+.1f;
            foreach(var person in runtime.People.Where(p=>p.controllerKind==PersonControllerKind.Npc&&p.currentHomeId==house.plot))
            {
                var actor=runtime.ActorOf(person.personId);if(!actor||!economy.CanEnter(house,person.personId))continue;
                foreach(var door in doors)if(door&&Vector3.Distance(actor.transform.position,door.transform.position)<3)FriendHousingDoor.OpenForResident(door);
            }
        }
    }
}
