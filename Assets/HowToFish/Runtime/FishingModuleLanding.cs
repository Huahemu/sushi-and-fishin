using UnityEngine;

namespace HowToFish {
    public static class FishingShoreRules {
        public static bool CanEscape(bool released,bool attached,bool inOpenWater,float centerY,float seaY,float depth)=>released&&!attached&&inOpenWater&&centerY<seaY-depth;
        public static bool CanFloatCorpse(bool openWater,float centerY,float seaY)=>openWater&&centerY<seaY+.15f;
        public static Vector3 CorpseVelocity(Vector3 velocity)=>Vector3.ClampMagnitude(new Vector3(velocity.x,Mathf.Min(0,velocity.y),velocity.z),4);
    }
    public sealed partial class FishingModule {
        FishingCatch landingCatch;
        Vector3 landingStart,landingEnd;
        float landingProgress,landingDuration,nextLandingSearch;
        Vector3 lastLandingSearchPosition;
        readonly RaycastHit[] landingHits=new RaycastHit[32];
        public bool LandingInProgress=>landingCatch&&landingCatch.IsAvailable;
        public bool IsLanding(FishingCatch fish)=>fish&&landingCatch==fish;
        public Vector3 SavedCatchPosition(FishingCatch fish)=>IsLanding(fish)?landingEnd:fish.transform.position;
        void StopLandingOnDeath(FishingCatch fish){if(!IsLanding(fish))return;landingCatch=null;fish.ResumeFromLine();fish.MarkLanded();}

        public bool PrepareWeaponSwitch(){
            if(!BoundedLineMotion||!hooked)return true;
            if(RequireManualCatchRelease){rig.Notify("先按右键把鱼放到岸上，再切换鱼枪");return false;}
            if(!hooked.ArchiveLifted||Vector3.Distance(hooked.body.worldCenterOfMass,rig.view.transform.position)>6){rig.Notify("先把鱼收近岸边，再切换鱼枪");return false;}
            if(TryBeginDryLanding(hooked))return true;
            rig.Notify("附近没有干燥落点 · 先向岸上移动，鱼仍挂在线上");return false;
        }
        bool TryBeginDryLanding(FishingCatch fish){
            if(LandingInProgress)return landingCatch==fish;
            var playerPosition=rig.locomotionRoot?rig.locomotionRoot.position:rig.view.transform.position;
            if(Time.time<nextLandingSearch&&(playerPosition-lastLandingSearchPosition).sqrMagnitude<.04f)return false;
            nextLandingSearch=Time.time+.35f;lastLandingSearchPosition=playerPosition;
            if(!TryFindDryLanding(fish,out var surface))return false;
            landingCatch=fish;landingStart=fish.transform.position;
            var bodyOffset=fish.body.worldCenterOfMass-fish.transform.position;
            landingEnd=new Vector3(surface.x-bodyOffset.x,surface.y+.06f-(fish.BottomY-fish.transform.position.y),surface.z-bodyOffset.z);
            landingProgress=0;landingDuration=FishingMotionMath.LandingSeconds(landingStart,landingEnd,profile.landGravity);
            ClearJoint();if(hooked==fish)hooked=null;
            // The suspended fish belongs to this visible landing motion now.
            // Returning the rod must not release it at its old water position.
            ResetHook();fish.EnableLandingHitboxes();FocusCatch(fish);
            rig.Notify("鱼正在落到岸边 · 切枪后可直接射击");return true;
        }
        void TickLandingMotion(){
            if(!LandingInProgress){landingCatch=null;return;}
            var fish=landingCatch;landingProgress=Mathf.Min(1,landingProgress+Time.fixedDeltaTime/landingDuration);
            float t=landingProgress;
            var position=FishingMotionMath.LandingPosition(landingStart,landingEnd,t,landingDuration,profile.landGravity);
            var hook=fish.hookAnchor?fish.hookAnchor.position:fish.body.position;fish.MoveOnLine(hook+position-fish.transform.position);
            if(t<1)return;
            landingCatch=null;fish.ResumeFromLine();fish.MarkLanded();fish.ConfirmShoreLanding();fish.SettleOnShore();
            rig.Notify(fish.IsDead?"鱼已落岸 · E 拾取":"鱼已落岸 · 用鱼枪击败后拾取");
        }
        bool TryFindDryLanding(FishingCatch fish,out Vector3 surface){
            var feet=rig.locomotionRoot?rig.locomotionRoot.position:landingPoint.position;
            var forward=Vector3.ProjectOnPlane(rig.view.transform.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.01f)forward=Vector3.forward;
            var right=Vector3.Cross(Vector3.up,forward);
            // Forward candidates keep the catch visible; side/inland candidates
            // avoid dropping it back into the sea when standing on a shoreline.
            var offsets=new[]{forward*1.1f,forward*.55f,right*.85f+forward*.3f,-right*.85f+forward*.3f,right*.9f,-right*.9f,right*1.2f-forward*.6f,-right*1.2f-forward*.6f,-forward*.9f,-forward*1.6f,right*1.6f-forward*.8f,-right*1.6f-forward*.8f,-forward*2.4f};
            foreach(var offset in offsets){
                if(!DrySurface(feet+offset,feet.y,out var point))continue;
                // Require support on both sides as well as the centre, so a
                // narrow dry edge cannot roll a newly delivered fish into water.
                if(!DrySurface(point+right*.35f,feet.y,out var left)||!DrySurface(point-right*.35f,feet.y,out var other))continue;
                if(Mathf.Abs(left.y-point.y)>.35f||Mathf.Abs(other.y-point.y)>.35f)continue;
                surface=point;return true;
            }
            surface=default;return false;
        }
        bool DrySurface(Vector3 at,float feetY,out Vector3 surface){
            float top=Mathf.Max(feetY,rig.view.transform.position.y)+2;
            int count=Physics.RaycastNonAlloc(new Vector3(at.x,top,at.z),Vector3.down,landingHits,Mathf.Max(1,top-SurfaceHeight+.2f),~0,QueryTriggerInteraction.Ignore);
            float height=float.NegativeInfinity;bool found=false;surface=default;
            for(int i=0;i<count;i++){var h=landingHits[i];if(h.collider.GetComponentInParent<FishingItem>()||h.collider is CharacterController||combat&&combat.IgnoreCollider!=null&&combat.IgnoreCollider(h.collider))continue;
                if(h.normal.y<.65f||h.point.y<=SurfaceHeight+.03f||h.point.y>feetY+.9f||h.point.y<=height)continue;height=h.point.y;surface=h.point;found=true;
            }
            return found;
        }
    }
}
