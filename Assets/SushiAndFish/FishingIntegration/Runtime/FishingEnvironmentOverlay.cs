using System;
using System.Linq;
using HowToFish;
using UnityEngine;

namespace SushiAndFish.FishingIntegration {
    [DefaultExecutionOrder(900)]
    public sealed class FishingEnvironmentOverlay:MonoBehaviour {
        bool active,savedFog;FogMode savedMode;Color savedColor;float savedStart,savedEnd,savedDensity,intensity;FishingEventKind kind;
        public void Apply(FishingEventKind weather,float amount){
            if(weather!=FishingEventKind.RainSquall&&weather!=FishingEventKind.SeaMist||amount<=0){Restore();return;}
            if(!active){savedFog=RenderSettings.fog;savedMode=RenderSettings.fogMode;savedColor=RenderSettings.fogColor;savedStart=RenderSettings.fogStartDistance;savedEnd=RenderSettings.fogEndDistance;savedDensity=RenderSettings.fogDensity;active=true;}
            kind=weather;intensity=Mathf.Clamp01(amount);
        }
        void LateUpdate(){if(!active)return;bool mist=kind==FishingEventKind.SeaMist;RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=Color.Lerp(savedColor,mist?new Color(.63f,.74f,.75f):new Color(.54f,.65f,.68f),intensity);RenderSettings.fogStartDistance=Mathf.Lerp(savedFog?savedStart:120,mist?12:38,intensity);RenderSettings.fogEndDistance=Mathf.Lerp(savedFog?savedEnd:260,mist?55:125,intensity);}
        void Restore(){if(!active)return;active=false;RenderSettings.fog=savedFog;RenderSettings.fogMode=savedMode;RenderSettings.fogColor=savedColor;RenderSettings.fogStartDistance=savedStart;RenderSettings.fogEndDistance=savedEnd;RenderSettings.fogDensity=savedDensity;}
        void OnDisable(){Restore();}
    }
}
