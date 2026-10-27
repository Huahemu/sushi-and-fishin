using UnityEngine;
namespace HowToFish {
    public static class FishingHaulMath {
        public static Vector3 HeldTarget(Vector3 tip,float minimumBottom,float belowHook,float shortLine){
            var target=tip+Vector3.down*Mathf.Clamp(shortLine,.08f,.28f);
            target.y=Mathf.Max(target.y,minimumBottom+Mathf.Max(0,belowHook));return target;
        }
    }
}
