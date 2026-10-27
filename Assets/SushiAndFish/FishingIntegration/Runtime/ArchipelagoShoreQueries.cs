using System;
using HowToFish;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Player;
using UnityEngine;
namespace SushiAndFish.FishingIntegration {
    // Geometry queries only. Used by runtime traversal and static editor checks.
    public static class ArchipelagoShoreQueries {
        static readonly Collider[] overlaps=new Collider[128];
        static bool Ignore(Collider c,Transform player)=>!c||c.isTrigger||(player&&c.transform.IsChildOf(player))||c.GetComponentInParent<FishingItem>()||c.GetComponentInParent<DeadFish>();
        static Vector3 Bottom(Vector3 feet,float radius)=>feet+Vector3.up*radius;
        static Vector3 Top(Vector3 feet,float radius,float height)=>feet+Vector3.up*Mathf.Max(radius,height-radius);
        public static bool Clear(Vector3 feet,float radius,float height,Transform player){int count=Physics.OverlapCapsuleNonAlloc(Bottom(feet,radius)+Vector3.up*.025f,Top(feet,radius,height),radius*.96f,overlaps,~0,QueryTriggerInteraction.Ignore);if(count==overlaps.Length)return false;for(int i=0;i<count;i++)if(!Ignore(overlaps[i],player))return false;return true;}
        public static bool ClearPath(Vector3 from,Vector3 to,float radius,float height,Transform player){var offset=to-from;if(offset.sqrMagnitude<.0001f)return true;foreach(var hit in Physics.CapsuleCastAll(Bottom(from,radius),Top(from,radius,height),radius*.94f,offset.normalized,offset.magnitude,~0,QueryTriggerInteraction.Ignore))if(!Ignore(hit.collider,player)&&hit.distance>.02f)return false;return true;}
        public static bool TryFind(Vector3 feet,Vector3 forward,float radius,float height,Transform player,out Vector3 destination,out Transform support){
            destination=default;support=null;forward=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;if(forward.sqrMagnitude<.1f)return false;
            foreach(float angle in new[]{0f,-22,22}){var direction=Quaternion.Euler(0,angle,0)*forward;var right=Vector3.Cross(Vector3.up,direction);
                foreach(float distance in new[]{.65f,1f,1.4f,1.8f,2.2f}){var origin=feet+direction*distance+Vector3.up*2.15f;
                    if(!Physics.Raycast(origin,Vector3.down,out var hit,2.1f,~0,QueryTriggerInteraction.Ignore)||Ignore(hit.collider,player)||hit.normal.y<.65f)continue;
                    float rise=hit.point.y-feet.y;if(rise<.12f||rise>2)continue;var end=hit.point+Vector3.up*.055f;
                    bool stable=true;foreach(float side in new[]{-1f,1f}){var p=hit.point+right*(radius*.85f*side)+Vector3.up*.2f;if(!Physics.Raycast(p,Vector3.down,out var edge,.45f,~0,QueryTriggerInteraction.Ignore)||edge.normal.y<.65f||Mathf.Abs(edge.point.y-hit.point.y)>.2f)stable=false;}
                    if(!stable||!Clear(end,radius,height,player))continue;var lift=new Vector3(feet.x,end.y+.16f,feet.z);
                    if(!ClearPath(feet,lift,radius,height,player)||!ClearPath(lift,end,radius,height,player))continue;
                    destination=end;support=hit.collider.transform;return true;
                }
            }return false;
        }
    }
}
