using System;
using System.Linq;
using System.Collections.Generic;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.Casino;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendCasinoGroundNavigation
    {
        public Vector3 ExteriorStage{get;private set;}
        public Bounds PhysicalRegion{get;private set;}
        public static bool TryReadPhysicalRegion(FriendGroundRoutePhysics shape,out Bounds bounds,out string reason)
        {
            bounds=default;reason=null;var casino=ParkCasinoIntegration.Instance;
            if(shape==null||!casino||!casino.Ready||!casino.ModuleRoot){reason="Actual original source module required";return false;}
            var colliders=casino.ModuleRoot.GetComponentsInChildren<Collider>(true).Where(c=>c&&c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger&&!(c is TerrainCollider)&&!shape.IsCharacter(c)).ToArray();
            if(colliders.Length==0){reason="Actual source module has no physical bounds";return false;}
            bounds=colliders[0].bounds;foreach(var c in colliders.Skip(1))bounds.Encapsulate(c.bounds);
            bounds.Expand(new Vector3(shape.Radius*2+.1f,shape.Height*2,shape.Radius*2+.1f));return true;
        }
        public static bool TryPlanExplicitApproach(ResidentController actor,Transform original,Vector3 outer,Vector3 inner,Vector3 goal,bool physicalCabExit,
            out Vector3 stage,out Vector3[] targets,out Bounds region,out string reason)
        {
            stage=default;targets=Array.Empty<Vector3>();reason=null;var shape=new FriendGroundRoutePhysics(actor,original);
            if(!TryReadPhysicalRegion(shape,out region,out reason))return false;
            var result=new List<Vector3>();var from=physicalCabExit?outer:actor.transform.position;
            if(!physicalCabExit)
            {
                if(!shape.Agent.enabled||!shape.Agent.isOnNavMesh||Vector3.Distance(shape.Agent.nextPosition,actor.transform.position)>.15f)
                {reason="Actual public NPC agent/body is not continuously bound";return false;}
                // The actual module bounds, not a fixed world coordinate, define
                // an east/north original-ground staging point outside the source.
                var wanted=new Vector3(region.max.x+shape.Radius+.25f,outer.y,region.max.z+shape.Radius+.25f);
                if(!shape.Foot(wanted,wanted.y+.12f,out var floor)||!NavMesh.SamplePosition(floor.point+Vector3.up*.04f,out var nav,.15f,shape.Filter)||
                    Vector3.ProjectOnPlane(nav.position-wanted,Vector3.up).magnitude>.05f||Mathf.Abs(nav.position.y-floor.point.y)>.12f||!shape.Clear(nav.position))
                {reason="Derived exterior staging original support/type/body rejected";return false;}
                stage=nav.position;
                if(!PlanLeg(shape,actor,from,stage,region,outer,inner,"original-public-to-staging",result,out reason))return false;from=stage;
                if(!PlanLeg(shape,actor,from,outer,region,outer,inner,"staging-to-verified-outer",result,out reason))return false;
            }
            if(!PlanLeg(shape,actor,outer,inner,region,outer,inner,"original-short-source-link",result,out reason))return false;
            if(!PlanLeg(shape,actor,inner,goal,region,outer,inner,"strict-inner-detour",result,out reason))return false;
            targets=result.ToArray();return targets.Length>0;
        }
        static bool PlanLeg(FriendGroundRoutePhysics shape,ResidentController actor,Vector3 from,Vector3 to,Bounds region,Vector3 outer,Vector3 inner,string label,List<Vector3> output,out string reason)
        {
            reason=null;var path=new NavMeshPath();var corners=Array.Empty<Vector3>();
            if(!NavMesh.CalculatePath(from,to,shape.Filter,path)||path.status!=NavMeshPathStatus.PathComplete||(corners=path.corners).Length==0||
                Vector3.Distance(corners[0],from)>.15f||Vector3.Distance(corners[corners.Length-1],to)>.1f)
            {reason=label+" / original complete path or endpoints rejected / "+path.status;return false;}
            var privacy=FriendHousingIsland.Instance?.GetComponent<FriendHousingPrivacy>();
            if(privacy&&privacy.ExcludesPathFor(actor,path)){reason=label+" / original housing privacy rejected";return false;}
            if(!FriendGroundSurfacePath.TryPlan(shape,path,region,outer,inner,true,out var targets,out reason)){reason=label+" / "+reason;return false;}
            foreach(var target in targets)if(output.Count==0||Vector3.Distance(output[output.Count-1],target)>.01f)output.Add(target);
            return true;
        }
    }
}
