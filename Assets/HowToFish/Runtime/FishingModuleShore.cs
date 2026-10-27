using UnityEngine;

namespace HowToFish {
    public static class FishingLandingRules {
        public static bool ShouldProbeRestingSupport(bool sleeping,Vector3 velocity)=>
            Mathf.Abs(velocity.y)<=.15f&&(sleeping||velocity.sqrMagnitude<=.04f);
        // A side wall, a tail brushing an edge, or a fish still rising cannot
        // confirm that the main body has been set down on a walkable surface.
        public static bool IsSupportedTopContact(bool mainBody,Vector3 normal,Vector3 contact,Vector3 bodyCenter,float verticalVelocity,float seaHeight)=>
            mainBody&&normal.y>=.65f&&contact.y>seaHeight+.03f&&bodyCenter.y>=contact.y+.02f&&verticalVelocity<=1.5f;
    }

    public sealed class EscapeWaterSearch {internal Vector3 origin;internal int ring,slice;internal bool started;}

    public sealed partial class FishingModule {
        static readonly float[] escapeSearchRadii={1.5f,3,5,8,12,18,26,40,64,96,128};
        readonly RaycastHit[] shoreHits=new RaycastHit[32];
        public bool HasShoreSupport(Vector3 center,float distance){
            int count=Physics.RaycastNonAlloc(center+Vector3.up*.12f,Vector3.down,shoreHits,Mathf.Max(.3f,distance+.24f),~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++){
                var hit=shoreHits[i];var collider=hit.collider;
                if(collider is CharacterController||collider.GetComponentInParent<FishingItem>()||collider.attachedRigidbody==hookBody)continue;
                if(hit.normal.y>=.65f&&hit.point.y>SurfaceHeight+.03f&&center.y>=hit.point.y+.02f)return true;
            }
            return false;
        }
        public bool TryFindEscapeWater(Vector3 origin,EscapeWaterSearch search,out Vector3 target){
            // Continue across hops. A single catch may spend at most 12 map rays
            // here, rather than blocking a frame on every radius/direction.
            if(!search.started||(origin-search.origin).sqrMagnitude>36){search.origin=origin;search.ring=search.slice=0;search.started=true;}
            var preferred=waterTarget?Vector3.ProjectOnPlane(waterTarget.position-search.origin,Vector3.up):Vector3.forward;
            if(preferred.sqrMagnitude<.01f)preferred=Vector3.forward;
            float baseYaw=Mathf.Atan2(preferred.x,preferred.z)*Mathf.Rad2Deg;
            for(int budget=0;budget<12&&search.ring<escapeSearchRadii.Length;budget++){
                float radius=escapeSearchRadii[search.ring];float angle=(baseYaw+search.slice*30)*Mathf.Deg2Rad;
                var sample=search.origin+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*radius;sample.y=SurfaceHeight-.25f;
                search.slice++;if(search.slice>=12){search.slice=0;search.ring++;}
                if(IsWaterAt(sample,out _)){target=sample;search.started=false;return true;}
            }
            if(search.ring>=escapeSearchRadii.Length)search.started=false;
            target=origin;return false;
        }
    }
}
