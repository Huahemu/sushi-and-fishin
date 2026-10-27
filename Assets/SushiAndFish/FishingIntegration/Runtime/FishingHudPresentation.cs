using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace SushiAndFish.FishingIntegration {
    public static class FishingHudPresentation {
        public static bool IsLive(FishingSnapshot fish)=>fish!=null&&fish.health>0;
        public static FishingSnapshot LiveFocus(IReadOnlyDictionary<string,FishingSnapshot> fish,string token)=>token!=null&&fish.TryGetValue(token,out var value)&&IsLive(value)?value:null;
        public static void SetHealth(Image bar,int current,int maximum){
            float ratio=maximum>0?Mathf.Clamp01(current/(float)maximum):0;
            // Image.Type.Filled ignores fillAmount when the Image has no sprite.
            // Stretch the solid native-colour rectangle to its actual fraction.
            bar.type=Image.Type.Simple;bar.enabled=ratio>0;
            var rect=bar.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=new Vector2(ratio,1);rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
    }
}
