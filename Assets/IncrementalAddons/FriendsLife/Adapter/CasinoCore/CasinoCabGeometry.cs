using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.Casino.Original;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife.CasinoCore
{
    public static class CasinoCabGeometry
    {
        // The original door opening, carving and source support define both
        // endpoints. No connector, obstacle edit or profile fallback is added.
        public static bool TryResolve(ResidentController actor,ElevatorManager elevator,out Vector3 gate,out Vector3 cab,out string reason)
        {
            gate=cab=default;reason=null;
            var obstacle=elevator?elevator.GetComponent<NavMeshObstacle>():null;
            if(!actor||!elevator||!elevator.playerSpawnPosition||!elevator.NativeLeftDoor||!elevator.NativeRightDoor||
                !obstacle||!obstacle.enabled||!obstacle.carving||obstacle.shape!=NavMeshObstacleShape.Box)
            {reason="Original door, box carving or passenger body missing";return false;}
            FriendGroundRoutePhysics shape;
            try{shape=new FriendGroundRoutePhysics(actor,elevator.playerSpawnPosition);}catch(System.Exception e){reason=e.Message;return false;}
            var bounds=elevator.NativeCabBounds;
            var door=(elevator.NativeLeftDoor.position+elevator.NativeRightDoor.position)*.5f;
            var forward=Vector3.ProjectOnPlane(door-bounds.center,Vector3.up).normalized;
            if(forward.sqrMagnitude<.9f){reason="Original door opening has no distinct outward direction";return false;}
            var right=Vector3.Cross(Vector3.up,forward);var scale=obstacle.transform.lossyScale;
            float half=Mathf.Abs(Vector3.Dot(forward,obstacle.transform.right))*obstacle.size.x*Mathf.Abs(scale.x)*.5f+
                Mathf.Abs(Vector3.Dot(forward,obstacle.transform.forward))*obstacle.size.z*Mathf.Abs(scale.z)*.5f;
            var center=obstacle.transform.TransformPoint(obstacle.center);
            var authored=elevator.playerSpawnPosition.position;
            if(!shape.Foot(authored,authored.y+.12f,out var support)){reason="Original cab passenger point has no physical support";return false;}
            var front=center+forward*(half+shape.Radius+.15f);front.y=support.point.y;
            var floor=SushiAndFish.Casino.ParkCasinoIntegration.Instance?.CurrentFloor??0;
            var extensions=floor==0?new[]{0f}:new[]{0f,.5f,1f,1.5f,2f,2.5f,3f,3.5f,4f,4.5f,5f};
            foreach(float extension in extensions)foreach(float side in new[]{0f,.25f,-.25f,.5f,-.5f})
            {
                var outside=front+forward*extension+right*side;
                if(!shape.Foot(outside,support.point.y+shape.Climb,out var foot)||!shape.SamplePhysical(foot.point,.15f,out var sample))continue;
                foreach(float insideSide in new[]{0f,.3f,-.3f})
                {
                    var wanted=support.point+right*insideSide;
                    if(!shape.Foot(wanted,support.point.y+.12f,out var inside)||!elevator.IsInElevator(inside.point+Vector3.up*.1f)||!shape.Clear(inside.point))continue;
                    if(!CasinoCabinWalker.ValidateSegment(actor,elevator.playerSpawnPosition,sample,inside.point,out reason))continue;
                    gate=sample;cab=inside.point;return true;
                }
            }
            if(string.IsNullOrEmpty(reason))reason="Original cab opening has no fully supported capsule-clear traversal";
            return false;
        }
    }
}
