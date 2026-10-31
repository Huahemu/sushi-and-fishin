using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife.CasinoCore
{
    // A return may see a different first collider than the outbound funnel.
    // Reuse the existing finite planner in its proved outbound direction, then
    // accept only a freshly verified physical path in the return direction.
    public static class CasinoReturnRoute
    {
        public static bool TryReverseOutbound(FriendGroundRoutePhysics shape,Vector3 body,Vector3 gate,Bounds region,out Vector3[] targets,out string reason)
        {
            targets=Array.Empty<Vector3>();reason=null;
            var outbound=new NavMeshPath();
            if(!NavMesh.CalculatePath(gate,body,shape.Filter,outbound)||outbound.status!=NavMeshPathStatus.PathComplete||
                outbound.corners.Length==0||Vector3.Distance(outbound.corners[0],gate)>.15f||
                Vector3.Distance(outbound.corners[outbound.corners.Length-1],body)>.1f)
            {reason="Actual gate-to-body outbound path/endpoint unavailable";return false;}
            if(!FriendGroundSurfacePath.TryPlan(shape,outbound,region,default,default,false,out var forward,out var forwardReason)||
                forward.Length==0||Vector3.Distance(forward[forward.Length-1],body)>.1f)
            {reason="Actual outbound planner rejected / "+forwardReason;return false;}
            var reverse=forward.Take(forward.Length-1).Reverse().Concat(new[]{gate}).ToArray();
            var privacy=FriendHousingIsland.Instance?.GetComponent<FriendHousingPrivacy>();
            var prior=body;
            for(int i=0;i<reverse.Length;i++)
            {
                var target=reverse[i];var leg=new NavMeshPath();string physicalReason=null;
                if(!NavMesh.CalculatePath(prior,target,shape.Filter,leg)||leg.status!=NavMeshPathStatus.PathComplete||
                    leg.corners.Length==0||Vector3.Distance(leg.corners[0],prior)>.15f||
                    Vector3.Distance(leg.corners[leg.corners.Length-1],target)>.1f||
                    !FriendGroundSurfacePath.Validate(shape,leg,region,default,default,false,out physicalReason)||
                    privacy&&privacy.ExcludesPathFor(shape.Actor,leg))
                {reason="Actual reverse outbound leg "+(i+1)+" rejected / "+physicalReason;return false;}
                prior=target;
            }
            targets=reverse;reason="actual outbound route reversed and every original return leg reverified / legs="+targets.Length+" / outbound="+forwardReason;return true;
        }
    }
}
