using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.Casino;
using SushiAndFish.Casino.Original;
using UnityEngine;
using UnityEngine.AI;
namespace SushiAndFish.FriendsLife.CasinoCore
{
    public static class CasinoMachineApproach
    {
        public static bool TryResolve(ResidentController actor,GameBase machine,out Vector3 goal,out string reason)
        {
            goal=default;reason=null;var agent=actor?actor.GetComponent<NavMeshAgent>():null;var capsule=actor?actor.GetComponent<CapsuleCollider>():null;
            if(!actor||!machine||!machine.keypadSpawnPoint||!agent||!capsule){reason="Original machine input anchor or actual body unavailable";return false;}
            var shape=new FriendGroundRoutePhysics(actor,machine.transform);
            var anchor=machine.keypadSpawnPoint;var requested=anchor.position;requested.y=machine.transform.position.y;
            // The original hand anchor may already be in front of a machine.
            if(TryFeet(shape,requested,anchor.position,out goal))return true;
            var forward=Vector3.ProjectOnPlane(anchor.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.99f){reason="Original input has no horizontal facing direction";return false;}
            var colliders=machine.GetComponentsInChildren<Collider>(true).Where(x=>x&&x.enabled&&x.gameObject.activeInHierarchy&&!x.isTrigger).ToArray();
            if(colliders.Length==0){reason="Original machine has no actual physical footprint";return false;}
            float front=0;
            foreach(var collider in colliders)
            {
                var bounds=collider.bounds;var delta=bounds.center-anchor.position;
                front=Mathf.Max(front,Vector3.Dot(delta,forward)+Mathf.Abs(forward.x)*bounds.extents.x+Mathf.Abs(forward.z)*bounds.extents.z);
            }
            // One feet proposal follows the authored keypad facing and the
            // current real machine boundary, plus the unchanged body radius.
            requested=anchor.position+forward*(front+shape.Radius+.04f);requested.y=machine.transform.position.y;
            if(!TryFeet(shape,requested,anchor.position,out goal)){reason="Original facing footprint has no supported capsule-clear interaction target";return false;}
            return true;
        }
        static bool TryFeet(FriendGroundRoutePhysics shape,Vector3 requested,Vector3 hand,out Vector3 goal)
        {
            goal=default;
            // Resolve the original typed navigation feet first. Machine root Y
            // is an authored placement hint, not the physical standing surface.
            if(!NavMesh.SamplePosition(requested,out var nav,.25f,shape.Filter)||
                !shape.Foot(nav.position,nav.position.y+.12f,out var floor)||
                Vector3.ProjectOnPlane(nav.position-requested,Vector3.up).magnitude>.05f||Mathf.Abs(nav.position.y-floor.point.y)>.12f||!shape.Clear(nav.position))return false;
            var input=ParkCasinoIntegration.Instance?ParkCasinoIntegration.Instance.LogicInteractor:null;
            // Both body-axis endpoints remain within the actual original input
            // range; this does not invent a camera or claim a raycast occurred.
            if(!input||input.raycastDistance<=0||Vector3.Distance(nav.position,hand)>input.raycastDistance||
                Vector3.Distance(nav.position+Vector3.up*shape.Height,hand)>input.raycastDistance)return false;
            goal=nav.position;return true;
        }
    }
}
