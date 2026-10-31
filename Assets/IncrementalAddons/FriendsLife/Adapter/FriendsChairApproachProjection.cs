using System;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.ResidentialIsland;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    // Repairs only NEW cloned chair Exit transforms. The authored seated ROOT,
    // real chair/table meshes, colliders and navigation acceptance stay intact.
    public sealed class FriendsChairApproachProjection:MonoBehaviour
    {
        float next;
        void Update()
        {
            var r=FriendsLifeRuntime.Instance;if(!r||!r.Ready||!r.IsAuthority||Time.unscaledTime<next)return;next=Time.unscaledTime+.5f;
            var home=GetComponent<HousingHouse>();if(!home)return;
            var id=home.GetComponent<FriendHouseMetadata>()?.privatePersonId;
            var actor=!string.IsNullOrEmpty(id)?r.ActorOf(id):r.People.Where(p=>p.currentHomeId==home.plot&&p.controllerKind==PersonControllerKind.Npc).Select(p=>r.ActorOf(p.personId)).FirstOrDefault(a=>a);
            if(actor)Apply(home,actor);
        }
        public static int Apply(HousingHouse home,ResidentController actor)
        {
            if(!home||home.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone!=true||!actor||!FriendResidentBindings.IsOwned(actor))return 0;
            var navigation=home.GetComponent<HousingNavigation>();if(!navigation||!navigation.HomeNavigationReady)return 0;
            var agent=actor.GetComponent<NavMeshAgent>();var capsule=actor.GetComponent<CapsuleCollider>();if(!agent||!capsule||capsule.direction!=1)return 0;
            var scale=capsule.transform.lossyScale;float radius=capsule.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));
            float height=Mathf.Max(radius*2,capsule.height*Mathf.Abs(scale.y));var profile=NavMesh.GetSettingsByID(agent.agentTypeID);
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};int moved=0;
            bool Permitted(Vector3 wanted,out Vector3 actual)
            {
                actual=default;
                var floor=Physics.RaycastAll(wanted+Vector3.up,Vector3.down,2,~0,QueryTriggerInteraction.Ignore)
                    .Where(h=>h.collider&&h.collider.transform.IsChildOf(home.transform)&&h.normal.y>=Mathf.Cos(profile.agentSlope*Mathf.Deg2Rad)&&Mathf.Abs(h.point.y-wanted.y)<=.12f)
                    .OrderBy(h=>Mathf.Abs(h.point.y-wanted.y)).FirstOrDefault();
                if(!floor.collider||!NavMesh.SamplePosition(floor.point,out var nav,.25f,filter)||Mathf.Abs(nav.position.y-floor.point.y)>.12f||Vector2.Distance(new Vector2(nav.position.x,nav.position.z),new Vector2(floor.point.x,floor.point.z))>.15f)return false;
                var center=floor.point+Vector3.up*(height*.5f+.025f);float half=height*.5f-radius;
                if(Physics.OverlapCapsule(center+Vector3.up*half,center-Vector3.up*half,radius,~0,QueryTriggerInteraction.Ignore)
                    .Any(c=>c&&!c.transform.IsChildOf(actor.transform)&&!c.GetComponentInParent<EmoteArea>()))return false;
                actual=floor.point;return true;
            }
            foreach(var chair in (home.chairs??Array.Empty<HousingSeatAnchor>()).Where(c=>c&&c.pose&&c.exit&&c.name.StartsWith("NativeChair_",StringComparison.Ordinal)))
            {
                if(chair.GetComponent<FriendsHomeFurnitureClaim>()?.Active==true)continue;
                if(Permitted(chair.exit.position,out _))continue;
                var seat=home.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled&&!c.isTrigger&&c.name.Contains("DiningChair")&&c.name.EndsWith("_Seat",StringComparison.Ordinal))
                    .OrderBy(c=>Vector2.Distance(new Vector2(c.bounds.center.x,c.bounds.center.z),new Vector2(chair.pose.position.x,chair.pose.position.z))).FirstOrDefault();
                if(!seat||Vector2.Distance(new Vector2(seat.bounds.center.x,seat.bounds.center.z),new Vector2(chair.pose.position.x,chair.pose.position.z))>.35f)continue;
                var right=Vector3.ProjectOnPlane(chair.pose.right,Vector3.up).normalized;var forward=Vector3.ProjectOnPlane(chair.pose.forward,Vector3.up).normalized;
                float halfWidth=Mathf.Abs(right.x)*seat.bounds.extents.x+Mathf.Abs(right.z)*seat.bounds.extents.z;
                bool chosen=false;
                foreach(float clearance in new[]{.1f,.2f,.3f})
                {
                    foreach(float side in new[]{1f,-1f})
                    {
                        var wanted=chair.pose.position+right*side*(halfWidth+radius+clearance);
                        if(!Permitted(wanted,out var actual))continue;
                        chair.exit.position=actual;chair.exit.rotation=Quaternion.LookRotation(-right*side,Vector3.up);moved++;chosen=true;break;
                    }
                    if(chosen)break;
                }
                if(chosen)continue;
                float originalDistance=Vector3.ProjectOnPlane(chair.exit.position-chair.pose.position,Vector3.up).magnitude;
                if(originalDistance>.05f&&Permitted(chair.pose.position-forward*originalDistance,out var back))
                {chair.exit.position=back;chair.exit.rotation=Quaternion.LookRotation(forward,Vector3.up);moved++;}
            }
            return moved;
        }
    }
}
