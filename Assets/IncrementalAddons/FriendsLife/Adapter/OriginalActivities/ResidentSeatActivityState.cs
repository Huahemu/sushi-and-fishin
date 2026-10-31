using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using PaperPixelGames.Framework.Enums;

namespace PaperPixelGames.Core.Ai.NPCs
{
    // Original authored activities, with separate real approach and authored
    // pose/resource lifecycle. Selection, schedule and Animator remain original.
    public sealed class ResidentSeatActivityState:ResidentBaseState
    {
        readonly ResidentController actor;
        // anchor = seat actually used; requestedAnchor = the schedule's exact
        // index. A busy requested seat falls back to a free same-type seat.
        WaypointAnchor anchor;
        readonly WaypointAnchor requestedAnchor;
        readonly string requestOwner;
        readonly ResidentActivityType activity;
        readonly WayPointIndex index;
        readonly bool exitExistingPose;
        ResidentActivityResourceReservations.Claim claim;
        EmoteArea area;
        Vector3 approach,pose,rotation;
        string resourceId,ownerKey,areaToken,reason;
        string phase="Queued";
        bool exitRequested,resumeSchedule,physicalArrival,exitRestored,requestReleased;
        string exitRequester,exitStack,blockedExitRequester,blockedExitStack;int exitRequestFrame=-1,blockedExitFrame=-1;
        public bool RequestHeld=>!exitExistingPose&&!requestReleased&&!requestOwner.StartsWith("schedule/",StringComparison.Ordinal);
        public bool ReleaseRequest(string owner)
        {if(!SafeBoundary||owner!=requestOwner)return false;requestReleased=true;return true;}
        public bool TryRequestExit(bool thenResumeSchedule,string claimant)
        {
            if(RequestHeld&&claimant!=requestOwner&&claimant!=OriginalNpcActivityPort.IdentityKey(actor))
            {
                if(string.IsNullOrEmpty(blockedExitRequester))
                {
                    blockedExitRequester=claimant;blockedExitFrame=Time.frameCount;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    blockedExitStack=Environment.StackTrace;
#endif
                }
                return false;
            }
            if(string.IsNullOrEmpty(exitRequester)){exitRequester=claimant;exitRequestFrame=Time.frameCount;CaptureExitStack();}
            RequestExit(thenResumeSchedule);return true;
        }
        void CaptureExitStack()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            exitStack=Environment.StackTrace;
#endif
        }
        int phaseFrame;
        float nextPreparation;
        string planFootRawJson,planFootJson,arrivalFootFirstJson,arrivalFootJson,exitFootJson;
        Vector3 recordedPlanPoint;bool hasPlanEvidence;
        string Animation=>OriginalNpcActivityPort.AnimationParameter(activity);
        bool HasAnimation=>!string.IsNullOrEmpty(Animation);
        bool AnimationReady(Animator animator)=>!HasAnimation||OriginalNpcActivityPort.HasBoolParameter(animator,Animation);
        bool AnimationActive(Animator animator)=>HasAnimation&&AnimationReady(animator)&&animator.GetBool(Animation);
        string EnteringPhase=>OriginalNpcActivityPort.IsSeat(activity)?"EnteringSeat":"EnteringActivity";
        string ActivePhase=>OriginalNpcActivityPort.IsSeat(activity)?"Seated":"Active";
        public string RequestOwner=>requestOwner;
        public ResidentActivityType Activity=>activity;
        public WayPointIndex Index=>index;
        public bool SafeBoundary=>phase=="SafeBoundary"||phase=="Failed"&&Released;
        bool Released=>!ResidentActivityResourceReservations.IsOwned(claim)&&(!area||!area.ResidentReservationMatches(areaToken));
        public ResidentSeatActivityState(ResidentBrain brain,ResidentController actor,ResidentActivityType activity,WayPointIndex index,WaypointAnchor anchor,string requestOwner,bool exitExistingPose=false):base(brain)
        {this.actor=actor;this.activity=activity;this.index=index;this.anchor=anchor;this.requestedAnchor=anchor;this.requestOwner=requestOwner;this.exitExistingPose=exitExistingPose;browseAfter=UnityEngine.Random.Range(25f,40f);}
        public string Phase=>phase;
        // Read-only: no acquisition or release of the original activity owner.
        bool ConversationOwnsActivity => !exitRequested && actor && actor.Brain != null &&
            ReferenceEquals(actor.Brain.StateMachine?.Current,this) && !string.IsNullOrEmpty(ownerKey) &&
            OriginalNpcActivityPort.IdentityKey(actor)==ownerKey && ResidentActivityResourceReservations.IsOwned(claim) &&
            (!area || area.ResidentReservationMatches(areaToken));
        public bool ConversationStableRest => ConversationOwnsActivity && phase==ActivePhase && Resting &&
            actor.VisualAnimator && AnimationReady(actor.VisualAnimator) && AnimationActive(actor.VisualAnimator);
        public bool ConversationStableSeat => phase=="Seated" && ConversationStableRest;
        public bool ConversationStableApproach => ConversationOwnsActivity && phase=="Approaching" && !physicalArrival;
        // A pathless pre-pose approach can still own its original reservation
        // and a queued exit. A temporary talk preserves both; it clears neither.
        public bool ConversationOwnedWaitingApproach => phase=="Approaching" && !physicalArrival && actor && actor.Brain != null &&
            ReferenceEquals(actor.Brain.StateMachine?.Current,this) && !string.IsNullOrEmpty(ownerKey) &&
            OriginalNpcActivityPort.IdentityKey(actor)==ownerKey && ResidentActivityResourceReservations.IsOwned(claim) &&
            (!area || area.ResidentReservationMatches(areaToken));
        public bool ConversationStableStanding => ConversationOwnsActivity && phase=="Active" && !HasAnimation && physicalArrival;
        // Sitting or lying in the authored pose: a resting body, not a stuck one.
        public bool Resting=>HasAnimation&&physicalArrival&&(phase==EnteringPhase||phase==ActivePhase);
        public override void Enter()
        {
            ownerKey=OriginalNpcActivityPort.IdentityKey(actor);
            if(exitExistingPose){AdoptExistingPoseForExit();Publish();return;}
            Runtime.SetActivity(activity);Runtime.CurrentWaypointIndex=index;Runtime.IsWorking=false;Runtime.IsSwimming=false;Runtime.IsAvailable=true;Runtime.HasArrived=false;
            Brain.SetModelVisible(true);Prepare();Publish();
        }
        void AdoptExistingPoseForExit()
        {
            // This narrow migration exits a real old original authored pose.
            // Its retained actual agent point is the only permitted exit; no
            // destination search, body placement or other owner's area release.
            if(!OriginalNpcActivityPort.CanAdoptExistingPose(actor,anchor,out pose,out approach,out reason))
            {SetPhase("ExitBlocked");nextPreparation=Brain.TalkActivityTime+.25f;return;}
            rotation=actor.transform.eulerAngles;resourceId=OriginalNpcActivityPort.ResourceKey(anchor,null);
            physicalArrival=true;exitRequested=true;BeginStand();
        }
        // A schedule-owned request that cannot obtain/plan its authored resource
        // must not freeze the body for the whole schedule slot.
        const float ScheduleWaitLimitSeconds=20f;
        // An approach that keeps failing at arrival re-plans every 0.25 s and
        // never reaches the wait limit (Approaching resets it). Count re-plans.
        const int MaxReplans=6;
        int replans;
        float waitingSince=-1f,phaseTime;
        // S06's owned, schedule-driven bench exit depends on Animator
        // transition completion even while that body is outside the camera.
        Animator exitProgressAnimator;
        AnimatorCullingMode exitProgressOriginalCulling;
        void RestoreExitAnimationCulling()
        {
            var held=exitProgressAnimator;exitProgressAnimator=null;
            // Do not overwrite a later mode chosen by another owner/system.
            if(held&&held.cullingMode==AnimatorCullingMode.CullUpdateTransforms)
                held.cullingMode=exitProgressOriginalCulling;
        }
        void KeepExitAnimationProgress()
        {
            var animator=actor?actor.VisualAnimator:null;
            if(exitProgressAnimator&&exitProgressAnimator!=animator)RestoreExitAnimationCulling();
            bool ownsExit=phase=="StandingUp"&&activity==ResidentActivityType.ParkBench&&physicalArrival&&exitRequested&&
                !string.IsNullOrEmpty(requestOwner)&&requestOwner.StartsWith("schedule/",StringComparison.Ordinal)&&
                !string.IsNullOrEmpty(ownerKey)&&ownerKey.StartsWith("original-resident/",StringComparison.Ordinal)&&
                actor&&ReferenceEquals(actor.Brain?.StateMachine?.Current,this)&&animator&&animator.enabled&&animator.gameObject.activeInHierarchy&&
                ResidentActivityResourceReservations.IsOwned(claim)&&(!area||area.ResidentReservationMatches(areaToken));
            if(!ownsExit){RestoreExitAnimationCulling();return;}
            if(exitProgressAnimator)
            {
                if(exitProgressAnimator.cullingMode!=AnimatorCullingMode.CullUpdateTransforms)exitProgressAnimator=null;
                return;
            }
            if(animator.cullingMode!=AnimatorCullingMode.CullCompletely)return;
            exitProgressOriginalCulling=animator.cullingMode;exitProgressAnimator=animator;
            // Keep state/transition evaluation; off-screen Transform/IK
            // culling remains. No clip, speed, pose or resource is forced.
            animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
        }
        bool PreApproachWait=>(phase=="Queued"||phase=="WaitingResource")&&!physicalArrival&&!exitRequested&&!ResidentActivityResourceReservations.IsOwned(claim);
        void SetPhase(string value)
        {
            if(value!="StandingUp")RestoreExitAnimationCulling();
            phase=value;phaseFrame=Time.frameCount;phaseTime=Brain.TalkActivityTime;
            if(value=="Queued"||value=="WaitingResource"){if(waitingSince<0)waitingSince=Brain.TalkActivityTime;}
            else waitingSince=-1f;
        }
        bool GiveUpScheduleWait()
        {
            if(!PreApproachWait||!requestOwner.StartsWith("schedule/",StringComparison.Ordinal)||
                (waitingSince<0||Brain.TalkActivityTime-waitingSince<ScheduleWaitLimitSeconds)&&replans<MaxReplans)return false;
            string waited=reason;ReleaseResource();Brain.Movement.Stop();Runtime.HasArrived=false;
            reason="Schedule wait limit reached ("+waited+"); original ambient patrol continues this slot";
            SetPhase("Failed");Publish();
            Brain.BeginAuthoredActivityFallback(activity,reason);
            return true;
        }
        void Prepare()
        {
            if(exitRequested){CompleteExit();return;}
            if(!anchor||!actor||!OriginalNpcActivityPort.CanAmbientOwn(actor)||OriginalNpcActivityPort.IdentityKey(actor)!=ownerKey)
            {Fail("Original activity body/identity owner changed before entry");return;}
            var animator=actor.VisualAnimator;if(!AnimationReady(animator))
            {Fail("The actual original Animator has no "+Animation+" Bool binding");return;}
            if(string.IsNullOrEmpty(planFootRawJson)&&anchor.TryGetActivityApproach(actor.GetComponent<NavMeshAgent>(),out var rawSeed,out _))
                planFootRawJson=OriginalNpcActivityPort.CaptureFootEvidence(actor,rawSeed);
            // Source streaming/appearance can change real collider Transforms
            // after the previous physics step. Query the current real poses.
            Physics.SyncTransforms();
            string waitReason=null,queueReason=null;bool acquired=false;
            foreach(var candidate in SeatCandidates())
            {
                // Approach planning is path + physics work; a spot that just
                // failed it is not planned again on every 0.25 s retry.
                if(unreachable!=null&&unreachable.TryGetValue(candidate,out float retryAt)&&Brain.TalkActivityTime<retryAt)
                {queueReason=queueReason??"No reachable approach to this authored spot yet";continue;}
                if(!OriginalNpcActivityPort.ResolveApproach(actor,candidate,out var candidatePose,out var candidateApproach,out var resolveReason))
                {if(unreachable==null)unreachable=new Dictionary<WaypointAnchor,float>();unreachable[candidate]=Brain.TalkActivityTime+3f;queueReason=queueReason??resolveReason;continue;}
                var candidateArea=OriginalNpcActivityPort.AreaFor(candidatePose);var candidateResource=OriginalNpcActivityPort.ResourceKey(candidate,candidateArea);
                if(candidateArea&&candidateArea.IsOccupied){waitReason=waitReason??"The original player/area already owns this seat";continue;}
                if(!ResidentActivityResourceReservations.TryAcquire(candidateResource,ownerKey,actor,candidateArea?(UnityEngine.Object)candidateArea:candidate,candidatePose,out var candidateClaim))
                {waitReason=waitReason??"Another actual actor reserved this authored seat";continue;}
                string token=ownerKey+"/seat/"+candidateClaim.epoch;
                if(candidateArea&&!candidateArea.TryReserveForResident(token))
                {ResidentActivityResourceReservations.Release(candidateClaim);waitReason=waitReason??"The authoritative original area rejected this resource owner";continue;}
                anchor=candidate;pose=candidatePose;approach=candidateApproach;area=candidateArea;resourceId=candidateResource;claim=candidateClaim;areaToken=token;acquired=true;break;
            }
            if(!acquired)
            {
                if(waitReason!=null){SetPhase("WaitingResource");reason=waitReason;}else{SetPhase("Queued");reason=queueReason;}
                nextPreparation=Brain.TalkActivityTime+.25f;return;
            }
            if(!hasPlanEvidence||Vector3.Distance(recordedPlanPoint,approach)>.001f)
            {planFootJson=OriginalNpcActivityPort.CaptureFootEvidence(actor,approach);recordedPlanPoint=approach;hasPlanEvidence=true;}
            rotation=anchor.transform.eulerAngles;Runtime.TargetDestination=approach;Runtime.TargetRotation=rotation;Runtime.HasValidTarget=true;Runtime.HasArrived=false;
            if(!Brain.Movement.MoveTo(approach,Brain.EffectiveMoveSpeed,.05f,true))
            {ReleaseResource();SetPhase("Queued");reason="The original movement rejected the complete approach";nextPreparation=Brain.TalkActivityTime+.25f;return;}
            reason=null;SetPhase("Approaching");
        }
        // Requested seat first, then every other registered seat of the same
        // activity (WaypointDirectory is keyed by type+index: 7 dictionary
        // lookups, no scene search), nearest first. Seats visibly taken by a
        // player/NPC are skipped before the costly approach planning.
        // Every authored type (market, pier, lighthouse spots too) uses the same
        // free same-type choice; a browsing body skips the spots it just used.
        const int MaxSeatIndex=16;
        List<WaypointAnchor> SeatCandidates()
        {
            var result=new List<WaypointAnchor>(7);if(requestedAnchor&&!Visited(requestedAnchor))result.Add(requestedAnchor);
            var others=new List<WaypointAnchor>(6);
            // Indices past EntryPoint_6 are extra authored seats (second seat of a bench).
            for(int i=0;i<MaxSeatIndex;i++)
            {
                if(!WaypointDirectory.TryGetAnchor(activity,(WayPointIndex)i,out var other)||!other||other==requestedAnchor||Visited(other))continue;
                var at=other.transform.position;var otherArea=OriginalNpcActivityPort.AreaFor(at);
                if(otherArea&&otherArea.IsOccupied&&!otherArea.ResidentReservationMatches(areaToken))continue;
                if(ResidentActivityResourceReservations.IsPoseReservedByOther(at,actor))continue;
                others.Add(other);
            }
            var from=actor.transform.position;others.Sort((a,b)=>(a.transform.position-from).sqrMagnitude.CompareTo((b.transform.position-from).sqrMagnitude));
            result.AddRange(others);
            if(result.Count==0&&visited!=null&&visited.Count>0){visited.Clear();return SeatCandidates();}
            return result;
        }
        // Spots without a sit/lie pose (market, pier, lighthouse) are not a
        // place to stand for a whole schedule slot (1 game hour = 1 real
        // minute). After a short stay the body walks on to another free spot
        // of the same type, like browsing. Seats and sunbeds keep their pose.
        HashSet<WaypointAnchor> visited;
        Dictionary<WaypointAnchor,float> unreachable;
        readonly float browseAfter;
        bool browse;
        bool Browses=>!HasAnimation&&requestOwner.StartsWith("schedule/",StringComparison.Ordinal);
        bool Visited(WaypointAnchor value)=>visited!=null&&visited.Contains(value);
        void Relocate()
        {
            browse=false;if(visited==null)visited=new HashSet<WaypointAnchor>();visited.Add(anchor);
            ReleaseResource();Brain.Movement.Stop();
            physicalArrival=false;exitRestored=false;hasPlanEvidence=false;replans=0;
            Runtime.HasArrived=false;Runtime.IsAvailable=true;
            reason=null;SetPhase("Queued");nextPreparation=Brain.TalkActivityTime;Publish();
        }
        public override void Tick(float deltaTime)
        {
            if(!actor||!OriginalNpcActivityPort.CanAmbientOwn(actor)||OriginalNpcActivityPort.IdentityKey(actor)!=ownerKey)
            {RestoreExitAnimationCulling();Publish();return;} // Never move a body already transferred to another owner.
            KeepExitAnimationProgress();
            if(phase=="ExitBlocked")
            {if(Brain.TalkActivityTime>=nextPreparation)AdoptExistingPoseForExit();Publish();return;}
            if(phase=="Failed"||phase=="SafeBoundary")
            {
                Publish();
                if(resumeSchedule&&Time.frameCount>phaseFrame){resumeSchedule=false;requestReleased=true;Brain.RequestScheduleChange();}
                // A failed schedule-owned activity is a free boundary; do not
                // stand in place for the rest of the slot. A schedule seat that
                // was asked to stand up (e.g. for a talk) and then not taken
                // over re-enters its schedule instead of standing forever.
                else if(!resumeSchedule&&Released&&requestOwner.StartsWith("schedule/",StringComparison.Ordinal)&&
                    (phase=="Failed"||Brain.TalkActivityTime-phaseTime>=15f)&&Brain.TalkActivityTime>=nextPreparation)
                {
                    nextPreparation=Brain.TalkActivityTime+5f;
                    if(phase=="Failed")Brain.BeginAuthoredActivityFallback(activity,reason);else Brain.RequestScheduleChange();
                }
                return;
            }
            if(phase=="Queued"||phase=="WaitingResource")
            {
                if(exitRequested)CompleteExit();else if(Brain.TalkActivityTime>=nextPreparation)Prepare();
                if(GiveUpScheduleWait())return;
                Publish();return;
            }
            if(phase!="StandingUp"&&phase!="ReturningToApproach"&&(!ResidentActivityResourceReservations.IsOwned(claim)||area&&!area.ResidentReservationMatches(areaToken)))
            {Fail("The original seat resource ownership changed during use");Publish();return;}
            if(phase=="Approaching")
            {
                if(exitRequested){Brain.Movement.Stop();CompleteExit();Publish();return;}
                var agent=actor.GetComponent<NavMeshAgent>();
                if(!agent||!agent.enabled||!agent.isOnNavMesh||Vector3.Distance(actor.transform.position,agent.nextPosition)>.25f)
                {Fail("Actual body/navigation continuity was lost on approach");Publish();return;}
                if(!agent.pathPending&&agent.hasPath&&agent.pathStatus!=NavMeshPathStatus.PathComplete)
                {Brain.Movement.Stop();ReleaseResource();replans++;SetPhase("Queued");reason="The approach became partial; no authored pose entered";nextPreparation=Brain.TalkActivityTime+.25f;Publish();return;}
                if(!OriginalNpcActivityPort.At(actor,approach)){Publish();return;}
                Physics.SyncTransforms();
                arrivalFootJson=OriginalNpcActivityPort.CaptureFootEvidence(actor,approach);
                if(string.IsNullOrEmpty(arrivalFootFirstJson))arrivalFootFirstJson=arrivalFootJson;
                if(!OriginalNpcActivityPort.FootFits(actor,approach))
                {
                    Brain.Movement.Stop();ReleaseResource();replans++;SetPhase("Queued");
                    reason="The actual arrival foot changed before the original pose; re-plan inside the same authored domain";
                    nextPreparation=Brain.TalkActivityTime+.25f;Publish();return;
                }
                float authoredDomain=anchor.ActivitySampleRadius+Mathf.Abs(anchor.ActivityVerticalOffset)+.05f;
                if(Vector3.Distance(actor.transform.position,pose)>authoredDomain)
                {Fail("The authored pose is outside its original local anchor sampling domain");Publish();return;}
                // The existing original authored alignment is permitted only
                // after THIS body reached THIS reserved local approach.
                physicalArrival=true;replans=0;
                if(HasAnimation||anchor.UseAuthoredPose)Brain.Movement.StopAtPose(pose,Quaternion.Euler(rotation));
                else {pose=actor.transform.position;Brain.Movement.StopAtRotation(Quaternion.Euler(rotation));}
                Runtime.TargetDestination=pose;Runtime.TargetRotation=rotation;Runtime.HasArrived=true;SetPhase(EnteringPhase);
            }
            else if(phase==EnteringPhase)
            {
                if(exitRequested){BeginStand();Publish();return;}
                var animator=actor.VisualAnimator;
                if(Time.frameCount>phaseFrame&&AnimationReady(animator)&&(!HasAnimation||AnimationActive(animator))&&OriginalNpcActivityPort.Visible(actor))SetPhase(ActivePhase);
            }
            else if(phase==ActivePhase)
            {
                if(exitRequested)BeginStand();
                else if(Browses&&Brain.TalkActivityTime-phaseTime>=browseAfter){browse=true;BeginStand();}
            }
            else if(phase=="StandingUp")
            {
                var animator=actor.VisualAnimator;
                if(Time.frameCount<=phaseFrame||!AnimationReady(animator)||AnimationActive(animator))
                {Publish();return;}
                bool transitioning=false;if(HasAnimation)for(int layer=0;layer<animator.layerCount;layer++)transitioning|=animator.IsInTransition(layer);
                if(transitioning){Publish();return;}
                SetPhase("ReturningToApproach");
            }
            else if(phase=="ReturningToApproach")
            {
                float domain=anchor.ActivitySampleRadius+Mathf.Abs(anchor.ActivityVerticalOffset)+.05f;
                // A body standing on the stand-up spot (often another waiting
                // resident) must not keep this one in place forever.
                if(!OriginalNpcActivityPort.FootFits(actor,approach)&&Brain.TalkActivityTime-phaseTime<8f)
                {if(string.IsNullOrEmpty(exitFootJson))exitFootJson=OriginalNpcActivityPort.CaptureFootEvidence(actor,approach);reason="The recorded stand-up approach is currently physically occupied";Publish();return;}
                if(!exitRestored)
                {
                    if(!Brain.Movement.TryRestoreFromActivityPose(approach,Quaternion.Euler(rotation),domain))
                    {reason="The original local activity exit could not restore this exact same-type approach";Publish();return;}
                    exitRestored=true;
                }
                if(!OriginalNpcActivityPort.AtRestoredApproach(actor,approach)){Publish();return;}
                CompleteExit();
            }
            Publish();
        }
        void BeginStand()
        {
            Runtime.HasArrived=false;Runtime.IsAvailable=false;
            // The original Controller clears Sit from HasArrived; do not play
            // another invented clip or run a second pose graph.
            SetPhase("StandingUp");
        }
        public void RequestExit(bool thenResumeSchedule)
        {
            if(string.IsNullOrEmpty(exitRequester)){exitRequester="original-brain";exitRequestFrame=Time.frameCount;CaptureExitStack();}
            exitRequested=true;resumeSchedule|=thenResumeSchedule;if(phase==ActivePhase||phase==EnteringPhase)BeginStand();
        }
        public override bool CanInterrupt(ResidentRequestPriority priority)
        {
            if(SafeBoundary&&!RequestHeld)return true;
            // Before any approach/claim nothing is held: a player talk may pause
            // this waiting body (it resumes the same request afterwards).
            if(priority==ResidentRequestPriority.PlayerInteraction&&PreApproachWait)return true;
            if(priority==ResidentRequestPriority.SnapshotApplying||priority==ResidentRequestPriority.HardReset)return true;
            return false;
        }
        void ReleaseResource()
        {if(area)area.ReleaseResidentReservation(areaToken);ResidentActivityResourceReservations.Release(claim);claim=null;}
        void CompleteExit()
        {
            if(browse&&!exitRequested){Relocate();return;}
            Brain.Movement.Stop();
            Runtime.HasArrived=false;Runtime.IsAvailable=true;Runtime.SetActivity(ResidentActivityType.Idle);
            Runtime.TargetDestination=actor?actor.transform.position:approach;Runtime.HasValidTarget=true;
            ReleaseResource();reason=null;SetPhase("SafeBoundary");Publish();
        }
        void Fail(string value)
        {
            RestoreExitAnimationCulling();
            reason=value;
            // Failure before any authored alignment is a normal free boundary.
            // A seated body retains its claim until the real stand-up completes.
            if(physicalArrival&&(phase==ActivePhase||phase==EnteringPhase||phase=="StandingUp"||phase=="ReturningToApproach"))
            {exitRequested=true;BeginStand();return;}
            Brain.Movement.Stop();Runtime.HasArrived=false;ReleaseResource();SetPhase("Failed");Publish();
        }
        public override void Exit()
        {
            RestoreExitAnimationCulling();
            // A normal transfer must have reached SafeBoundary. HardReset and
            // Snapshot use their existing authoritative restore, no local Warp.
            ReleaseResource();Publish();
        }
        public OriginalActivitySnapshot Snapshot()
        {
            var agent=actor?actor.GetComponent<NavMeshAgent>():null;var animator=actor?actor.VisualAnimator:null;
            return new OriginalActivitySnapshot{phase=phase,resourceId=resourceId,requestOwner=requestOwner,ownerKey=ownerKey,activity=activity.ToString(),reason=reason,
                requestHeld=RequestHeld,exitRequester=exitRequester,exitStack=exitStack,exitRequestFrame=exitRequestFrame,
                blockedExitRequester=blockedExitRequester,blockedExitFrame=blockedExitFrame,blockedExitStack=blockedExitStack,
                planFootRawJson=planFootRawJson,planFootJson=planFootJson,arrivalFootFirstJson=arrivalFootFirstJson,arrivalFootJson=arrivalFootJson,exitFootJson=exitFootJson,
                approach=approach,pose=pose,actualPosition=actor?actor.transform.position:default,safeBoundary=SafeBoundary,reserved=ResidentActivityResourceReservations.IsOwned(claim),
                reservationsReleased=Released,physicalArrival=physicalArrival,sitParameter=OriginalNpcActivityPort.HasSitParameter(animator)&&animator.GetBool("Sit"),modelVisible=OriginalNpcActivityPort.Visible(actor),
                animationParameter=Animation,activityParameter=AnimationActive(animator),
                actorInstance=actor?actor.GetInstanceID():0,frame=Time.frameCount,bodyAgentDistance=agent&&agent.enabled&&agent.isOnNavMesh?Vector3.Distance(actor.transform.position,agent.nextPosition):float.MaxValue};
        }
        void Publish(){if(actor)OriginalNpcActivityPort.Remember(actor,Snapshot());}
        public override string GetDebugName()=>"OriginalActivity:"+activity+"/"+phase;
    }
}
