using System;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AI;
using SushiAndFish.FriendsLife;
using PaperPixelGames.Framework.Enums;

namespace PaperPixelGames.Core.Ai.NPCs
{
    [Serializable] public sealed class OriginalActivitySnapshot
    {
        public string phase,resourceId,ownerKey,requestOwner,activity,reason,exitRequester,exitStack,blockedExitRequester,blockedExitStack;
        public bool requestHeld;
        public string planFootRawJson,planFootJson,arrivalFootFirstJson,arrivalFootJson,exitFootJson;
        public int exitRequestFrame=-1,blockedExitFrame=-1;
        public string animationParameter;public bool activityParameter;
        public Vector3 approach,pose,actualPosition;
        public bool safeBoundary,reserved,reservationsReleased,physicalArrival,sitParameter,modelVisible;
        public float bodyAgentDistance;
        public int actorInstance,frame;
    }
    // A command/observation port for the original activity states. No autonomous
    // choice, probability, relationship score, clock or body lease lives here.
    public static class OriginalNpcActivityPort
    {
        sealed class Last{public OriginalActivitySnapshot value;}
        static readonly ConditionalWeakTable<ResidentController,Last> latest=new ConditionalWeakTable<ResidentController,Last>();
        public static bool IsSeat(ResidentActivityType type)=>type==ResidentActivityType.SeasideBench||type==ResidentActivityType.ParkBench;
        public static bool IsAuthoredActivity(ResidentActivityType type)=>IsSeat(type)||type==ResidentActivityType.BeachSunbathe||
            type==ResidentActivityType.MarketVisit||type==ResidentActivityType.LighthouseIdle||type==ResidentActivityType.PierFishing||type==ResidentActivityType.Idle;
        internal static string AnimationParameter(ResidentActivityType type)=>IsSeat(type)?"Sit":type==ResidentActivityType.BeachSunbathe?"LayDown":null;
        public static string IdentityKey(ResidentController actor)
        {
            if(!actor)return null;
            if(actor.GetComponent<FriendWorldActor>())return FriendsBodyAuthority.IdentityKey(actor);
            return "original-resident/"+actor.gameObject.scene.handle+"/"+actor.ResidentId+"/"+actor.GetInstanceID();
        }
        public static bool CanAmbientOwn(ResidentController actor)
        {
            if(!actor||actor.Brain==null||actor.Runtime==null)return false;
            var state=actor.Runtime;
            if(state.IsSnapshotApplying||state.IsWorking||state.IsInRestaurant||state.HasRestaurantAssignment||state.IsInteracting||state.IsSwimming)return false;
            return !actor.GetComponent<FriendWorldActor>()||FriendsBodyAuthority.CanAmbientOwn(actor);
        }
        public static bool TryBeginSeat(ResidentController actor,ResidentActivityType activity,WayPointIndex index,string requestOwner,out string reason)
        {
            if(!IsSeat(activity)){reason="The requested original type is not a seat";return false;}
            return TryBeginActivity(actor,activity,index,requestOwner,out reason);
        }
        public static bool TryBeginActivity(ResidentController actor,ResidentActivityType activity,WayPointIndex index,string requestOwner,out string reason)
        {
            reason=null;if(!IsAuthoredActivity(activity)||!CanAmbientOwn(actor)||string.IsNullOrEmpty(requestOwner)){reason="Original activity requires the actual free activity owner";return false;}
            if(FriendsOriginalActivityTransit.TryRoute(actor,activity,index,WaypointDirectory.TryGetAnchor(activity,index,out var routeAnchor)?routeAnchor:null,requestOwner,out bool routed,out reason))return routed;
            if(actor.Brain.StateMachine.Current is ResidentSeatActivityState current)
            {
                if(current.RequestOwner==requestOwner&&current.Activity==activity&&current.Index==index)return true;
                if(!current.SafeBoundary||current.RequestHeld){current.TryRequestExit(false,requestOwner);reason="The current authored activity must exit first";return false;}
            }
            if(!(actor.Brain.StateMachine.Current is ResidentSeatActivityState)&&IsAuthoredActivity(actor.Runtime.CurrentActivity)&&actor.Runtime.HasArrived&&!CanYield(actor))
            {RequestSafeExit(actor,requestOwner);reason="The existing original authored pose must exit before a new activity";return false;}
            if(!WaypointDirectory.TryGetAnchor(activity,index,out var anchor)||!anchor){reason="The exact original activity/index has no authored anchor";return false;}
            var next=new ResidentSeatActivityState(actor.Brain,actor,activity,index,anchor,requestOwner);
            if(!actor.Brain.StateMachine.ChangeState(next,ResidentRequestPriority.ScheduledActivity)){reason="The original current state rejected this activity";return false;}
            return true;
        }
        internal static void Remember(ResidentController actor,OriginalActivitySnapshot value)
        {if(actor)latest.GetOrCreateValue(actor).value=value;}
        public static OriginalActivitySnapshot Observe(ResidentController actor)
        {
            if(!actor||actor.Brain==null)return null;
            var transit=FriendsOriginalActivityTransit.Observe(actor);if(transit!=null)return transit;
            if(actor.Brain.StateMachine.Current is ResidentSeatActivityState seat)return seat.Snapshot();
            OriginalActivitySnapshot value=null;if(latest.TryGetValue(actor,out var old))value=old.value;
            var agent=actor.GetComponent<NavMeshAgent>();
            var current=actor.Brain.StateMachine.Current;
            bool boundary=value?.phase=="SafeBoundary"&&actor.Runtime.CurrentActivity==ResidentActivityType.Idle&&!actor.Runtime.HasArrived;
            string phase=boundary?"SafeBoundary":actor.Runtime.IsWorking?"Working":actor.Runtime.CurrentActivity==ResidentActivityType.Home?"Home":
                actor.Runtime.HasArrived?"Active":actor.Runtime.HasValidTarget?"Moving":"Free";
            if(current?.GetDebugName()?.Contains("WaitingForOriginalPath")==true)phase="WaitingPath";
            return new OriginalActivitySnapshot{phase=phase,resourceId=value?.resourceId,requestOwner=value?.requestOwner,
                reason=value?.reason,ownerKey=IdentityKey(actor),activity=actor.Runtime.CurrentActivity.ToString(),actualPosition=actor.transform.position,approach=value?.approach??actor.transform.position,pose=value?.pose??actor.transform.position,
                safeBoundary=CanAmbientOwn(actor)&&CanYield(actor),reserved=false,reservationsReleased=true,physicalArrival=value?.physicalArrival??false,modelVisible=Visible(actor),actorInstance=actor.GetInstanceID(),frame=Time.frameCount,
                bodyAgentDistance=agent&&agent.enabled&&agent.isOnNavMesh?Vector3.Distance(actor.transform.position,agent.nextPosition):float.MaxValue};
        }
        public static bool RequestSafeExit(ResidentController actor,string claimantKey)
        {
            if(!actor||actor.Brain==null||string.IsNullOrEmpty(claimantKey)||!CanAmbientOwn(actor))return false;
            if(actor.Brain.StateMachine.Current is ResidentSeatActivityState seat)return seat.TryRequestExit(false,claimantKey);
            if(IsAuthoredActivity(actor.Runtime.CurrentActivity)&&actor.Runtime.HasArrived)
            {
                if(!WaypointDirectory.TryGetAnchor(actor.Runtime.CurrentActivity,actor.Runtime.CurrentWaypointIndex,out var anchor)||
                    !CanAdoptExistingPose(actor,anchor,out _,out _,out _))return false;
                return actor.Brain.StateMachine.ChangeState(new ResidentSeatActivityState(actor.Brain,actor,actor.Runtime.CurrentActivity,
                    actor.Runtime.CurrentWaypointIndex,anchor,"existing-exit/"+claimantKey,true),ResidentRequestPriority.ScheduledActivity);
            }
            var agent=actor.GetComponent<NavMeshAgent>();
            return agent&&agent.enabled&&agent.isOnNavMesh&&Vector3.Distance(actor.transform.position,agent.nextPosition)<=.25f;
        }
        public static bool ResumeSchedule(ResidentController actor,string requestOwner)
        {
            if(!actor||actor.Brain==null||!CanAmbientOwn(actor))return false;
            if(actor.Brain.StateMachine.Current is ResidentSeatActivityState seat)
            {if(!seat.ReleaseRequest(requestOwner))return false;}
            if(!CanYield(actor))return false;
            actor.Brain.RequestScheduleChange();return true;
        }
        public static bool ResumeWhenSafe(ResidentController actor,string claimantKey)
        {
            if(!actor||actor.Brain==null||string.IsNullOrEmpty(claimantKey)||!CanAmbientOwn(actor))return false;
            if(actor.Brain.StateMachine.Current is ResidentSeatActivityState seat)return seat.TryRequestExit(true,claimantKey);
            if(!CanYield(actor))
            {
                if(!RequestSafeExit(actor,claimantKey))return false;
                if(actor.Brain.StateMachine.Current is ResidentSeatActivityState existing)return existing.TryRequestExit(true,claimantKey);
                return false;
            }
            actor.Brain.RequestScheduleChange();return true;
        }
        public static bool CanYield(ResidentController actor)
        {
            if(!actor||actor.Brain==null||actor.Brain.StateMachine.Current!=null&&!actor.Brain.StateMachine.Current.CanInterrupt(ResidentRequestPriority.PlayerInteraction))return false;
            if(actor.Brain.StateMachine.Current is ResidentSeatActivityState seat&&!seat.SafeBoundary)return false;
            var agent=actor.GetComponent<NavMeshAgent>();
            return agent&&agent.enabled&&agent.isOnNavMesh&&agent.updatePosition&&Vector3.Distance(actor.transform.position,agent.nextPosition)<=.25f;
        }
        internal static bool Visible(ResidentController actor)
            =>actor&&actor.VisualAnimator&&actor.VisualAnimator.gameObject.activeInHierarchy&&actor.VisualAnimator.GetComponentsInChildren<Renderer>(true).Any(r=>r.enabled&&r.gameObject.activeInHierarchy);
        internal static bool HasSitParameter(Animator animator)=>HasBoolParameter(animator,"Sit");
        internal static bool HasBoolParameter(Animator animator,string parameter)
            =>animator&&animator.runtimeAnimatorController&&animator.parameters.Any(p=>p.name==parameter&&p.type==AnimatorControllerParameterType.Bool);
        internal static bool At(ResidentController actor,Vector3 target)
        {
            var agent=actor.GetComponent<NavMeshAgent>();if(!agent||!agent.enabled||!agent.isOnNavMesh||agent.pathPending)return false;
            float tolerance=Mathf.Max(.15f,agent.stoppingDistance+.05f);
            return (!agent.hasPath||agent.pathStatus==NavMeshPathStatus.PathComplete)&&actor.Brain.Movement.HasArrived()&&
                Vector3.Distance(actor.transform.position,target)<=tolerance&&Vector3.Distance(agent.nextPosition,target)<=tolerance&&Vector3.Distance(actor.Brain.Movement.Destination,target)<=tolerance;
        }
        internal static bool AtRestoredApproach(ResidentController actor,Vector3 capturedApproach)
        {
            var agent=actor?actor.GetComponent<NavMeshAgent>():null;
            if(!agent||!agent.enabled||!agent.isOnNavMesh||agent.pathPending||!agent.isStopped||agent.velocity.sqrMagnitude>.0025f)return false;
            return (!agent.hasPath||agent.pathStatus==NavMeshPathStatus.PathComplete)&&actor.Brain.Movement.HasMoveGoal&&
                Vector3.Distance(actor.Brain.Movement.RequestedMoveGoal,capturedApproach)<=.05f&&
                Vector3.Distance(actor.transform.position,capturedApproach)<=.15f&&Vector3.Distance(agent.nextPosition,capturedApproach)<=.15f;
        }
        internal static string ResourceKey(WaypointAnchor anchor,EmoteArea area)
        {
            Transform t=area?area.transform:anchor.transform;string path=t.name;for(var p=t.parent;p;p=p.parent)path=p.name+"/"+path;
            return t.gameObject.scene.path+"/"+(area?"emote/":"anchor/")+path;
        }
        // Called from every 0.25 s WaitingResource retry: use the live EmoteArea
        // registry instead of a 3-7 ms scene-wide FindObjectsByType. A seat binds
        // to the area whose player target sits on the same authored pose.
        internal static EmoteArea AreaFor(Vector3 pose)
        {
            EmoteArea best=null;float bestDistance=float.MaxValue;var areas=EmoteArea.ActiveAreas;
            for(int i=0;i<areas.Count;i++)
            {
                var a=areas[i];if(!a||!a.isActiveAndEnabled)continue;var delta=a.ActivityTargetPosition-pose;
                if(Mathf.Abs(delta.y)>.6f)continue;float flat=new Vector2(delta.x,delta.z).magnitude;
                if(flat<=.45f&&flat<bestDistance){best=a;bestDistance=flat;}
            }
            return best;
        }
        internal static bool CanAdoptExistingPose(ResidentController actor,WaypointAnchor anchor,out Vector3 pose,out Vector3 approach,out string reason)
        {
            pose=actor?actor.transform.position:default;approach=default;reason=null;
            var agent=actor?actor.GetComponent<NavMeshAgent>():null;
            if(!actor||!anchor||actor.Brain==null||actor.Brain.StateMachine.IsSuspended||!CanAmbientOwn(actor)||!agent||!agent.enabled||!agent.isOnNavMesh||
                !anchor.TryGetActivityApproach(agent,out _,out var authored)||Vector3.Distance(pose,authored)>.15f)
            {reason="The actual body is not the known original authored activity pose";return false;}
            approach=agent.nextPosition;float domain=anchor.ActivitySampleRadius+Mathf.Abs(anchor.ActivityVerticalOffset)+.05f;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            if(Vector3.Distance(pose,approach)>domain||!NavMesh.SamplePosition(approach,out var exact,.05f,filter)||
                Vector3.Distance(exact.position,approach)>.05f||!FootFits(actor,approach))
            {reason="The retained original local exit is not a supported free same-type foot position";return false;}
            return true;
        }
        internal static bool ResolveApproach(ResidentController actor,WaypointAnchor anchor,out Vector3 pose,out Vector3 approach,out string reason)
            =>ResolveApproachFrom(actor,anchor,null,out pose,out approach,out reason);
        internal static bool ResolveApproachFrom(ResidentController actor,WaypointAnchor anchor,Vector3? prospectiveOrigin,out Vector3 pose,out Vector3 approach,out string reason)
        {
            reason=null;pose=anchor.transform.position;approach=default;var agent=actor.GetComponent<NavMeshAgent>();
            if(!agent||!agent.enabled||!agent.isOnNavMesh||Vector3.Distance(actor.transform.position,agent.nextPosition)>.25f){reason="Actual actor and same-type navigation are not continuous";return false;}
            if(!anchor.TryGetActivityApproach(agent,out var seed,out pose)){reason="Original anchor has no same-type nearby navigation";return false;}
            float radius=anchor.ActivitySampleRadius;var right=Vector3.ProjectOnPlane(anchor.transform.right,Vector3.up).normalized;var forward=Vector3.ProjectOnPlane(anchor.transform.forward,Vector3.up).normalized;
            // The baked agent radius may be smaller than the actual body.
            // Plan inside the ORIGINAL anchor domain with the real capsule's
            // world envelope; every candidate still passes the same physical
            // support, penetration, typed navigation and complete-path gates.
            var capsule=actor.GetComponent<CapsuleCollider>();
            if(!capsule||capsule.direction!=1){reason="The actual original body has no vertical capsule";return false;}
            var scale=capsule.transform.lossyScale;
            float bodyRadius=capsule.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));
            var candidates=new System.Collections.Generic.List<Vector3>{seed};
            var seen=new System.Collections.Generic.HashSet<Vector3>{seed};
            var directions=new[]{-forward,right,-right,forward,(-forward+right).normalized,(-forward-right).normalized,(forward+right).normalized,(forward-right).normalized};
            foreach(float distance in new[]{Mathf.Min(radius,bodyRadius+.05f),Mathf.Min(radius,bodyRadius*2+.05f),radius})
                foreach(var direction in directions)
                {var candidate=seed+direction*distance;if(seen.Add(candidate))candidates.Add(candidate);}
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            foreach(var requested in candidates)
            {
                if(!NavMesh.SamplePosition(requested,out var nav,.05f,filter)||Vector3.Distance(nav.position,seed)>radius+.05f||
                    Vector3.Distance(nav.position,pose)>radius+Mathf.Abs(anchor.ActivityVerticalOffset)+.05f||!FootFits(actor,nav.position))continue;
                var path=new NavMeshPath();bool complete=prospectiveOrigin.HasValue?NavMesh.CalculatePath(prospectiveOrigin.Value,nav.position,filter,path):agent.CalculatePath(nav.position,path);
                Vector3 start=prospectiveOrigin??actor.transform.position;
                if(!complete||path.status!=NavMeshPathStatus.PathComplete||path.corners.Length==0||
                    Vector3.Distance(path.corners[0],start)>.25f||Vector3.Distance(path.corners[path.corners.Length-1],nav.position)>.15f)continue;
                approach=nav.position;return true;
            }
            reason="No physically supported complete approach was found among the bounded original anchor candidates";return false;
        }
        static float[] FootDebugVector(Vector3 value)=>new[]{value.x,value.y,value.z};
        static string FootDebugPath(Transform value)
        {string path=value.name;for(var parent=value.parent;parent;parent=parent.parent)path=parent.name+"/"+path;return path;}
        internal static string CaptureFootEvidence(ResidentController actor,Vector3 point)
        {
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
            return null;
#else
            try
            {
                if(!actor)return "{\"error\":\"Actual actor absent\"}";
                var capsule=actor.GetComponent<CapsuleCollider>();var agent=actor.GetComponent<NavMeshAgent>();
                if(!capsule||!agent)return "{\"error\":\"Actual capsule or agent absent\"}";
                // Read the unmodified production predicate before metadata or
                // any caller-requested Transform synchronization can affect it.
                bool fits=FootFits(actor,point);var scale=capsule.transform.lossyScale;
                float radius=capsule.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z)),height=Mathf.Max(radius*2,capsule.height*Mathf.Abs(scale.y)),half=height*.5f-radius;
                var center=point+actor.transform.rotation*Vector3.Scale(capsule.center,scale);
                var at=capsule.transform.position+(point-actor.transform.position)+Vector3.up*.025f;
                var overlaps=Physics.OverlapCapsule(center+Vector3.up*(half+.025f),center-Vector3.up*(half-.025f),radius,~0,QueryTriggerInteraction.Ignore).Select(other=>
                {
                    bool own=other&&other.transform.IsChildOf(actor.transform);Vector3 normal=default;float depth=0;
                    bool penetration=other&&!own&&Physics.ComputePenetration(capsule,at,capsule.transform.rotation,other,other.transform.position,other.transform.rotation,out normal,out depth);
                    return new{path=other?FootDebugPath(other.transform):null,type=other?other.GetType().Name:null,own,penetration,depth,normal=FootDebugVector(normal),
                        blocking=penetration&&(Vector3.Dot(normal,Vector3.up)<.5f||depth>capsule.contactOffset)};
                }).ToArray();
                var boxes=UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .Where(box=>box&&!box.transform.IsChildOf(actor.transform)&&Vector3.Distance(box.transform.TransformPoint(box.center),point)<=3f+box.transform.TransformVector(box.size).magnitude*.5f)
                    .OrderBy(box=>Vector3.Distance(box.transform.TransformPoint(box.center),point)).Take(12).Select(box=>
                    {
                        var group=box.GetComponentInParent<PaperPixelGames.Core.DressingSceneSystem.DressingActivationGroup>(true);
                        return new{path=FootDebugPath(box.transform),box.enabled,active=box.gameObject.activeInHierarchy,position=FootDebugVector(box.transform.position),
                            rotation=FootDebugVector(box.transform.eulerAngles),scale=FootDebugVector(box.transform.lossyScale),center=FootDebugVector(box.center),size=FootDebugVector(box.size),
                            dressingGroup=group?FootDebugPath(group.transform):null,groupActive=group&&group.IsActivated};
                    }).ToArray();
                var dressing=UnityEngine.Object.FindFirstObjectByType<PaperPixelGames.Core.Streaming.DressingSceneManager>();
                return Newtonsoft.Json.JsonConvert.SerializeObject(new{frame=Time.frameCount,point=FootDebugVector(point),body=FootDebugVector(actor.transform.position),
                    agentNext=FootDebugVector(agent.nextPosition),actorRotation=FootDebugVector(actor.transform.eulerAngles),actorUp=FootDebugVector(actor.transform.up),
                    capsulePosition=FootDebugVector(capsule.transform.position),capsuleRotation=FootDebugVector(capsule.transform.eulerAngles),capsuleScale=FootDebugVector(scale),
                    capsuleCenter=FootDebugVector(capsule.center),capsule.radius,capsule.height,capsule.direction,capsule.enabled,capsule.isTrigger,capsule.contactOffset,
                    worldRadius=radius,worldHeight=height,bodyQueryCenter=FootDebugVector(center),computePose=FootDebugVector(at),autoSync=Physics.autoSyncTransforms,
                    streamingBusy=dressing&&dressing.IsStreamingBusy,appearanceApplying=FriendResidentBindings.IsApplyingAppearance(actor),customizationApplying=actor.IsCustomizationApplying,
                    animatorApplyRootMotion=actor.VisualAnimator&&actor.VisualAnimator.applyRootMotion,animatorPath=actor.VisualAnimator?FootDebugPath(actor.VisualAnimator.transform):null,footFits=fits,overlaps,nearbyAuthoredBoxes=boxes});
            }
            catch(Exception error){return Newtonsoft.Json.JsonConvert.SerializeObject(new{error=error.ToString()});}
#endif
        }
        internal static bool FootFits(ResidentController actor,Vector3 point)
        {
            var agent=actor.GetComponent<NavMeshAgent>();var capsule=actor.GetComponent<CapsuleCollider>();if(!agent||!capsule||capsule.direction!=1)return false;
            var settings=NavMesh.GetSettingsByID(agent.agentTypeID);var ground=Physics.RaycastAll(point+Vector3.up,Vector3.down,2f,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>h.collider&&!h.collider.transform.IsChildOf(actor.transform)&&!h.collider.GetComponentInParent<ResidentController>()&&h.normal.y>=Mathf.Cos(settings.agentSlope*Mathf.Deg2Rad))
                .OrderBy(h=>Mathf.Abs(h.point.y-point.y)).FirstOrDefault();if(!ground.collider||Mathf.Abs(ground.point.y-point.y)>.12f)return false;
            var scale=capsule.transform.lossyScale;float radius=capsule.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));float height=Mathf.Max(radius*2,capsule.height*Mathf.Abs(scale.y));float half=height*.5f-radius;
            var center=point+actor.transform.rotation*Vector3.Scale(capsule.center,scale);
            foreach(var other in Physics.OverlapCapsule(center+Vector3.up*(half+.025f),center-Vector3.up*(half-.025f),radius,~0,QueryTriggerInteraction.Ignore))
            {
                if(!other||other.transform.IsChildOf(actor.transform))continue;var at=capsule.transform.position+(point-actor.transform.position)+Vector3.up*.025f;
                if(Physics.ComputePenetration(capsule,at,capsule.transform.rotation,other,other.transform.position,other.transform.rotation,out var normal,out var depth)&&
                    (Vector3.Dot(normal,Vector3.up)<.5f||depth>capsule.contactOffset))return false;
            }
            return true;
        }
    }
}
