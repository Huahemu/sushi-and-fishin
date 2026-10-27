using UnityEngine;

namespace HowToFish {
    // Closed-form launch parameters, independent of any player or physics loop.
    public static class FishingCastMath {
        public static float Distance(FishingProfile profile,float power)=>Mathf.Min(Mathf.Lerp(profile.boundedCastNearDistance,Mathf.Max(profile.boundedCastNearDistance,profile.boundedCastFarDistance),Mathf.Clamp01(power)),Mathf.Max(1,profile.maxLine-10));
        public static float FlightSeconds(FishingProfile profile,float power)=>Mathf.Max(.1f,Mathf.Lerp(profile.boundedCastNearSeconds,Mathf.Max(profile.boundedCastNearSeconds,profile.boundedCastFarSeconds),Mathf.Clamp01(power)));
        public static Vector3 Launch(Vector3 direction,float distance,float seconds,float originY,float seaY,float gravityY){
            var forward=Vector3.ProjectOnPlane(direction,Vector3.up);if(forward.sqrMagnitude<.001f)forward=Vector3.forward;forward.Normalize();
            seconds=Mathf.Max(.1f,seconds);
            return forward*(distance/seconds)+Vector3.up*((seaY-originY)/seconds-.5f*gravityY*seconds);
        }
        public static bool CanStartReeling(bool bounded,bool payingOut,bool touchedWater,bool hitSolid)=>!bounded||!payingOut||touchedWater||hitSolid;
    }
}
