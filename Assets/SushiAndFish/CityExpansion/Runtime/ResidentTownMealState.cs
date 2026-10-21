using PaperPixelGames.Core.Ai.NPCs;
using UnityEngine;

namespace SushiAndFish.CityExpansion
{
    // This optional activity never enters the sushi assignment or customer
    // queue. A native work, interaction or sushi request can interrupt it.
    public sealed class ResidentTownMealState : ResidentBaseState
    {
        readonly ResidentController resident;
        readonly CityDiningSeat seat;
        readonly TownMealScheduler scheduler;
        readonly int day,stayMinutes;
        int seatedUntil;
        float arrivalDeadline;
        Animator visualAnimator;
        AnimatorCullingMode previousCulling;
        bool seated,failed;
        public bool Seated=>seated;
        public CityDiningSeat Seat=>seat;
        public ResidentTownMealState(ResidentController resident,CityDiningSeat seat,TownMealScheduler scheduler,int day,int stayMinutes):base(resident.Brain)
        {this.resident=resident;this.seat=seat;this.scheduler=scheduler;this.day=day;this.stayMinutes=stayMinutes;}
        public override void Enter()
        {
            Runtime.SetActivity(ResidentActivityType.ParkBench);
            Runtime.HasArrived=false;Runtime.IsWorking=false;Runtime.IsSwimming=false;Runtime.IsAvailable=true;
            Runtime.TargetDestination=seat.approach.position;Runtime.TargetRotation=seat.seatedPose.eulerAngles;Runtime.HasValidTarget=true;
            Brain.SetModelVisible(true);
            visualAnimator=resident.VisualAnimator;
            if(visualAnimator){previousCulling=visualAnimator.cullingMode;visualAnimator.cullingMode=AnimatorCullingMode.AlwaysAnimate;}
            failed=!Brain.Movement.MoveTo(seat.approach.position,Brain.EffectiveMoveSpeed,.6f,true);
            arrivalDeadline=Time.time+90;
        }
        public override void Tick(float deltaTime)
        {
            if(failed||!seat||!scheduler||scheduler.CurrentDay!=day||seated&&scheduler.Minute>=seatedUntil||scheduler.SushiOpen||Time.time>arrivalDeadline&&!seated)
            {Brain.RequestScheduleChange();return;}
            if(!seated&&Brain.Movement.HasArrived())
            {
                Brain.Movement.StopAtPose(seat.seatedPose.position,seat.seatedPose.rotation);
                Runtime.HasArrived=true;
                resident.PlaySeatedIdleAnimation();
                seated=true;
                seatedUntil=scheduler.Minute+stayMinutes;
            }
        }
        public override void Exit()
        {
            if(visualAnimator)visualAnimator.cullingMode=previousCulling;
            if(seat)
            {
                seat.Release(resident);
                if(!Runtime.IsSnapshotApplying)Brain.Movement.Warp(seat.approach.position,seat.approach.rotation);
            }
            Runtime.HasArrived=false;
        }
        public override bool CanInterrupt(ResidentRequestPriority priority)=>true;
        public override string GetDebugName()=>seated?"Town meal (seated)":"Town meal (walking)";
    }
}
