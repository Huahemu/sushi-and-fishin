using UnityEngine;

namespace HowToFish {
    public sealed partial class FishingModule {
        public FishingItem AssistedCatch(Camera camera){
            var eye=camera.transform.position;var forward=camera.transform.forward;
            bool nearMat=Vector3.Distance(eye,landingPoint.position)<2.8f;
            FishingItem best=null;float bestScore=float.PositiveInfinity;
            foreach(var fish in loose){
                if(!fish||!fish.CanGrab||fish.item.Retired||fish.item.owner)continue;
                Vector3 delta=fish.transform.position-eye;float distance=delta.magnitude;if(distance>3.2f)continue;
                float along=Vector3.Dot(delta,forward);float sideways=(delta-forward*along).magnitude;
                bool aimed=along>0&&sideways<.30f+distance*.035f;
                bool onMat=nearMat&&Vector3.Distance(fish.transform.position,landingPoint.position)<1.5f;
                if((!aimed&&!onMat)||!ClearCatchSight(eye,fish))continue;
                float score=aimed?sideways+distance*.05f:1+distance*.1f;
                if(score<bestScore){bestScore=score;best=fish.item;}
            }
            return best;
        }
        public FishingItem NearestLandedCatch(Vector3 hand,float radius){
            FishingItem best=null;
            foreach(var fish in loose){
                if(!fish||!fish.CanGrab||fish.item.Retired||fish.item.owner)continue;
                float distance=Vector3.Distance(hand,fish.transform.position);
                if(distance<radius&&ClearCatchSight(hand,fish)){radius=distance;best=fish.item;}
            }
            return best;
        }
        bool ClearCatchSight(Vector3 origin,FishingCatch fish){
            var delta=fish.transform.position+Vector3.up*.05f-origin;
            foreach(var hit in Physics.RaycastAll(origin,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                if(!hit.collider.GetComponentInParent<FishingItem>())return false;
            return true;
        }
    }
}
