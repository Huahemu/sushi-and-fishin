using UnityEngine;
namespace HowToFish {
    public sealed partial class FishingModule {
        public float CastDepth{get;private set;}
        public float CastDistance{get;private set;}
        public bool CastHasCatch=>castCaught;
        public bool BiteReady=>castTouchedWater&&!castCaught&&wetTime>=wait;
        bool castActive,castCaught,castTouchedWater;
        Vector3 castOrigin,lastWaterPosition;
        bool HasRoomForCast()=>FishingCatchPool.SmallCatch(profile,BaitInfo,0,AvailableCatchVertices,AvailableCatchTriangles);
        void BeginWaterCast(){
            castActive=true;castCaught=castTouchedWater=false;CastDepth=CastDistance=wetTime=0;
            castOrigin=rig.view.transform.position;
        }
        void TickCastBite(float dt,bool wet){
            if(!castActive||castCaught||hooked)return;
            if(wet){
                if(!castTouchedWater){AnnounceWaterCast();wait=Random.Range(profile.biteWaitMin,Mathf.Max(profile.biteWaitMin,profile.biteWaitMax));castTouchedWater=true;}
                lastWaterPosition=hookBody.position;
                CastDepth=Mathf.Max(CastDepth,SurfaceHeight-hookBody.position.y);
                CastDistance=Mathf.Max(CastDistance,Vector3.ProjectOnPlane(hookBody.position-castOrigin,Vector3.up).magnitude);
                wetTime+=dt*(worldEvents?worldEvents.BiteRate:1);
                if(reeling>.01f&&(wetTime>=wait||lineLength<2.5f||Vector3.Distance(hookBody.position,WorldRodTip)<3))HookFish();
            }else if(castTouchedWater&&reeling>.01f)HookFish();
        }
        bool EnsureCatchBeforeRecall(){
            if(!castActive||castCaught)return false;
            if(!castTouchedWater){rig.Notify("鱼饵没有入水 · 请向海面抛竿");return false;}
            HookFish();return true;
        }
        void PrepareBitePosition(){
            if(hookBody.position.y>=SurfaceHeight||!IsWaterAt(hookBody.position,out _)){
                hookBody.position=lastWaterPosition;
                lineLength=Mathf.Clamp(Vector3.Distance(WorldRodTip,lastWaterPosition)+.25f,profile.minLine,profile.maxLine);
                lineSteps=Mathf.CeilToInt((lineLength-profile.minLine)/profile.reelStep);lineVelocity=0;payingOut=false;if(rope)rope.maxDistance=lineLength;
            }
        }
        void ClearWaterCast(){castActive=castCaught=castTouchedWater=false;CastDepth=CastDistance=0;}
    }
}
