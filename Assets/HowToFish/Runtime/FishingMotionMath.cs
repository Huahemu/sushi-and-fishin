using UnityEngine;

namespace HowToFish {
    public static class FishingMotionMath {
        public static float ShoreGravity(FishingProfile profile,bool bounded,float worldGravityY)=>Mathf.Max(.1f,bounded?profile.landGravity:-worldGravityY);
        public static float HopSpeed(float gravity,float height)=>Mathf.Sqrt(2*Mathf.Max(.1f,gravity)*Mathf.Max(0,height));
        public static float HopSeconds(float gravity,float height)=>2*HopSpeed(gravity,height)/Mathf.Max(.1f,gravity);
        public static float LandingSeconds(Vector3 start,Vector3 end,float gravity)=>Mathf.Clamp(Mathf.Sqrt(2*Mathf.Abs(start.y-end.y)/Mathf.Max(.1f,gravity)),.2f,.46f);
        public static Vector3 LandingPosition(Vector3 start,Vector3 end,float progress,float seconds,float gravity){
            float t=Mathf.Clamp01(progress);if(t>=1)return end;seconds=Mathf.Max(.01f,seconds);float elapsed=t*seconds;
            var point=Vector3.Lerp(start,end,t);float vertical=(end.y-start.y)/seconds+.5f*gravity*seconds;
            point.y=start.y+vertical*elapsed-.5f*gravity*elapsed*elapsed;return point;
        }
        public static Quaternion LineWriggle(Quaternion rest,float time)=>Quaternion.Euler(Mathf.Sin(time*18)*7,Mathf.Sin(time*14)*9,Mathf.Sin(time*21)*6)*rest;
    }
}
