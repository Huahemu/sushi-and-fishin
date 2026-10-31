using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.ResidentialIsland;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    // A finite, walking-only repair for an original Work route intersecting an
    // added private home. Each actual leg must remain complete and public.
    public static class FriendWorkHousingRoute
    {
        const float PrivacyMargin=.55f;
        static HousingPrivacyVolume[] Volumes(ResidentController actor)
        {
            var island=HousingIsland.Instance;
            var privacy=island?island.GetComponent<FriendHousingPrivacy>():null;
            return privacy&&privacy.isActiveAndEnabled?privacy.ExcludedVolumesForWork(actor):Array.Empty<HousingPrivacyVolume>();
        }
        public static bool HasPrivateVolumes(ResidentController actor)=>Volumes(actor).Length>0;
        // A detached/off-mesh body waits for normal source recovery. This
        // predicate does not move the body or alter the agent's internal foot.
        public static bool HasCurrentBodyFoot(ResidentController actor)
        {
            var agent=actor?actor.GetComponent<NavMeshAgent>():null;
            return agent&&agent.enabled&&agent.isOnNavMesh&&Vector3.Distance(actor.transform.position,agent.nextPosition)<=.25f;
        }
        static bool Complete(Vector3 from,Vector3 target,NavMeshQueryFilter filter,out NavMeshPath path)
        {
            path=new NavMeshPath();
            if(!NavMesh.CalculatePath(from,target,filter,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
            var corners=path.corners;
            return corners.Length>0&&Vector3.Distance(corners[0],from)<=.15f&&Vector3.Distance(corners[corners.Length-1],target)<=.15f;
        }
        static bool Crosses(HousingPrivacyVolume volume,NavMeshPath path)
        {
            var bounds=new Bounds(new Vector3(0,2.5f,0),new Vector3(volume.size.x+PrivacyMargin*2,11,volume.size.y+PrivacyMargin*2));
            var corners=path.corners;if(corners.Length==0)return true;
            var previous=volume.Local(corners[0]);if(bounds.Contains(previous))return true;
            for(int i=1;i<corners.Length;i++)
            {
                var point=volume.Local(corners[i]);var delta=point-previous;float length=delta.magnitude;
                if(bounds.Contains(point)||length>.0001f&&bounds.IntersectRay(new Ray(previous,delta/length),out var hit)&&hit<=length+.0001f)return true;
                previous=point;
            }
            return false;
        }
        static bool Public(HousingPrivacyVolume[] volumes,NavMeshPath path)
            =>!volumes.Any(v=>Crosses(v,path));
        static bool Sample(Vector3 requested,NavMeshQueryFilter filter,out Vector3 point)
        {
            point=default;
            if(!NavMesh.SamplePosition(requested,out var hit,.5f,filter)||Mathf.Abs(hit.position.y-requested.y)>.3f||
                Vector2.Distance(new Vector2(hit.position.x,hit.position.z),new Vector2(requested.x,requested.z))>.2f)return false;
            point=hit.position;return true;
        }
        static float Length(Vector3 start,Vector3[] targets)
        {float length=0;foreach(var point in targets){length+=Vector3.Distance(start,point);start=point;}return length;}
        // guarded means active private volumes exist, INCLUDING query failures.
        // Only the no-privacy case may use the old partial-route behaviour.
        public static bool TryPlan(ResidentController actor,Vector3 target,out Vector3[] targets,out bool guarded)
        {
            targets=null;var volumes=Volumes(actor);guarded=volumes.Length>0;
            if(!guarded)return true;
            var agent=actor?actor.GetComponent<NavMeshAgent>():null;
            if(!agent||!agent.enabled||!agent.isOnNavMesh)return false;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};var start=agent.nextPosition;
            if(!Complete(start,target,filter,out var direct))return false;
            int blocked=Array.FindIndex(volumes,v=>Crosses(v,direct));
            if(blocked<0){targets=new[]{target};return true;}
            var volume=volumes[blocked];
            // The current defect intersects one added home's volume. All legs
            // are checked against every home; a second unrouteable home waits.
            var proposals=new List<Vector3[]>();float pad=Mathf.Max(.8f,agent.radius+.65f);
            foreach(float extra in new[]{0f,.75f,1.5f})
            {
                float x=volume.size.x*.5f+pad+extra,z=volume.size.y*.5f+pad+extra;
                var requested=new[]{new Vector3(-x,0,-z),new Vector3(x,0,-z),new Vector3(x,0,z),new Vector3(-x,0,z)};
                var corners=new Vector3[4];var valid=new bool[4];
                for(int i=0;i<4;i++)valid[i]=Sample(volume.center+volume.rotation*requested[i],filter,out corners[i]);
                for(int i=0;i<4;i++)if(valid[i])
                {
                    proposals.Add(new[]{corners[i],target});
                    foreach(int step in new[]{1,3})if(valid[(i+step)%4])proposals.Add(new[]{corners[i],corners[(i+step)%4],target});
                }
            }
            // At most36 proposals/108 complete-path queries, only on a new plan.
            foreach(var proposal in proposals.OrderBy(p=>Length(start,p)))
            {
                bool pass=true;var prior=start;
                foreach(var point in proposal)
                {
                    if(!Complete(prior,point,filter,out var leg)||!Public(volumes,leg)){pass=false;break;}
                    prior=point;
                }
                if(pass){targets=proposal;return true;}
            }
            return false;
        }
        public static bool LegStillPublic(ResidentController actor,Vector3 target)
        {
            var agent=actor?actor.GetComponent<NavMeshAgent>():null;
            if(!agent||!agent.enabled||!agent.isOnNavMesh)return false;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            return Complete(agent.nextPosition,target,filter,out var path)&&Public(Volumes(actor),path);
        }
    }
}
