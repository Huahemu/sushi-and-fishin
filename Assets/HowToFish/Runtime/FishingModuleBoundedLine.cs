using UnityEngine;

namespace HowToFish {
    public sealed partial class FishingModule {
        Vector3 boundedVelocity;
        bool haulRaised,boundedFlightBlocked,catchReeledIn;
        readonly RaycastHit[] boundedHits=new RaycastHit[32];

        void BeginBoundedCast(Vector3 originalLaunch){
            boundedFlightBlocked=false;
            // A downward-looking rod tip may be underwater before release.
            // Start above the surface so water drag cannot cancel the cast.
            var start=hookBody.position;start.y=Mathf.Max(start.y,SurfaceHeight+.4f);hookBody.position=start;
            boundedVelocity=FishingCastMath.Launch(originalLaunch,PlannedCastDistance,FishingCastMath.FlightSeconds(profile,charge),start.y,SurfaceHeight,Physics.gravity.y);
            hookBody.isKinematic=true;hookBody.useGravity=false;
            hookBody.GetComponent<Collider>().enabled=false;SetRope(false);
        }
        void FixedBoundedLine(){
            if(State==FishingState.Ready||State==FishingState.Charging)return;
            float dt=Time.fixedDeltaTime;
            if(hooked){StepBoundedHaul(dt);return;}
            // Holding reel during flight must not cancel a fully charged cast.
            // Apply the held input once the bait reaches water or hits a wall.
            if(payingOut&&reeling>.01f&&(castTouchedWater||boundedFlightBlocked))StartReelInput();
            bool wasUnder=hookBody.position.y<SurfaceHeight&&IsWaterAt(hookBody.position,out _);
            if(!payingOut&&reeling>.01f){
                float speed=12*reeling;lineSteps=Mathf.Max(0,lineSteps-speed*dt/Mathf.Max(.01f,profile.reelStep));
                float distance=MinimumLineLength+lineSteps*profile.reelStep;
                boundedVelocity=Vector3.zero;
                var target=WorldRodTip+Vector3.ClampMagnitude(hookBody.position-WorldRodTip,distance);
                hookBody.position=Vector3.MoveTowards(hookBody.position,target,speed*dt);
            }else{
                boundedVelocity+=Physics.gravity*dt;
                if(wasUnder){boundedVelocity*=Mathf.Exp(-profile.underwaterDrag*dt);boundedVelocity.y=Mathf.Min(-1.3f,boundedVelocity.y);}
                hookBody.position=SweepBoundedLure(hookBody.position,hookBody.position+boundedVelocity*dt);
                var offset=hookBody.position-WorldRodTip;
                if(offset.sqrMagnitude>profile.maxLine*profile.maxLine){hookBody.position=WorldRodTip+offset.normalized*profile.maxLine;boundedVelocity-=offset.normalized*Mathf.Max(0,Vector3.Dot(boundedVelocity,offset.normalized));}
            }
            lineLength=Vector3.Distance(WorldRodTip,hookBody.position);
            bool wet=hookBody.position.y<SurfaceHeight&&IsWaterAt(hookBody.position,out _);
            if(wet&&!wasWet)Splash();if(wet&&State==FishingState.Casting)State=FishingState.Floating;
            TickCastBite(dt,wet);wasWet=wet;
            if(!hooked&&!payingOut&&lineSteps<=0&&lineLength<.8f&&!EnsureCatchBeforeRecall())ResetHook();
        }
        Vector3 SweepBoundedLure(Vector3 from,Vector3 to){
            var delta=to-from;float distance=delta.magnitude;if(distance<.0001f)return to;
            int count=Physics.SphereCastNonAlloc(from,.025f,delta/distance,boundedHits,distance,~0,QueryTriggerInteraction.Ignore);
            float closest=distance;bool blocked=false;
            for(int i=0;i<count;i++){var hit=boundedHits[i];if(hit.collider.GetComponentInParent<FishingItem>()||(combat&&combat.IgnoreCollider!=null&&combat.IgnoreCollider(hit.collider))||hit.collider is CharacterController)continue;if(hit.distance<closest){closest=hit.distance;blocked=true;}}
            if(!blocked)return to;boundedFlightBlocked=true;boundedVelocity=Vector3.zero;return from+delta/distance*Mathf.Max(0,closest-.015f);
        }
        void StepBoundedHaul(float dt){
            var fish=hooked;
            if(reeling>.01f||RequireManualCatchRelease&&catchReeledIn){
                var target=WorldRodTip;
                float belowHook=Mathf.Max(0,(fish.hookAnchor?fish.hookAnchor.position.y:fish.body.position.y)-fish.BottomY);
                float clearance=Mathf.Max(rig.view.transform.position.y-.3f,SurfaceHeight+.5f)+belowHook;
                float shoreDistance=Vector3.ProjectOnPlane(hookBody.position-target,Vector3.up).magnitude;
                // Reel through the water first. Lift only over the final metres,
                // keeping the attached body clear of the platform edge.
                if(!haulRaised&&shoreDistance>profile.hookedLiftDistance)target.y=SurfaceHeight-.3f;
                else if(!haulRaised){target=new Vector3(hookBody.position.x,clearance,hookBody.position.z);if(hookBody.position.y>=clearance-.1f)haulRaised=true;}
                if(haulRaised){target=WorldRodTip;target.y=clearance;}
                if(haulRaised&&RequireManualCatchRelease){
                    float feet=rig.locomotionRoot?rig.locomotionRoot.position.y:rig.view.transform.position.y-1.6f;
                    target=FishingHaulMath.HeldTarget(WorldRodTip,Mathf.Max(SurfaceHeight,feet)+.12f,belowHook,profile.hookedMinLine);
                }
                float speed=profile.HookedReelSpeedAtDistance(Vector3.Distance(hookBody.position,WorldRodTip))*Mathf.Max(reeling,catchReeledIn?1:0);
                hookBody.position=Vector3.MoveTowards(hookBody.position,target,Mathf.Max(3.5f,speed)*dt);
                if(haulRaised&&RequireManualCatchRelease&&(hookBody.position-target).sqrMagnitude<.01f)catchReeledIn=true;
            }
            fish.MoveOnLine(hookBody.position);
            lineLength=Vector3.Distance(WorldRodTip,hookBody.position);lineSteps=Mathf.Max(0,(lineLength-MinimumLineLength)/Mathf.Max(.01f,profile.reelStep));
            if(!RequireManualCatchRelease&&reeling>.01f&&haulRaised&&Vector3.ProjectOnPlane(hookBody.position-WorldRodTip,Vector3.up).magnitude<=profile.hookedLandingDistance&&fish.BottomY>SurfaceHeight+.05f&&TryPlaceOnDryShore(fish)){
                // TryBeginDryLanding owns the catch until it visibly reaches the ground.
            }
        }
        bool TryPlaceOnDryShore(FishingCatch fish)=>TryBeginDryLanding(fish);
    }
}
