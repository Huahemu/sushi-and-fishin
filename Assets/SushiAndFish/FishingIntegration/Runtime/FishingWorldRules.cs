using UnityEngine;

namespace SushiAndFish.FishingIntegration {
    public static class FishingWorldRules {
        public static bool ValidRestorePosition(Vector3 p,float sea,float cameraY,bool reachedShore,float maxLine)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z)&&p.y>=sea-Mathf.Max(100,maxLine+20)&&p.y<sea+500&&(reachedShore||p.y<cameraY+25);
    }
}
