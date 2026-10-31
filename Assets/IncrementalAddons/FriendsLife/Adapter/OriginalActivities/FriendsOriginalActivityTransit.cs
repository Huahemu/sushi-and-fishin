using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Framework.Enums;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    // The physical private->public leg of an existing original schedule request.
    // It borrows root BodyAuthority and the existing actual portal controller.
    [DefaultExecutionOrder(2130)]
    public sealed class FriendsOriginalActivityTransit:MonoBehaviour
    {
        const string Facility="OriginalActivityTransit|";
        static FriendsOriginalActivityTransit instance;
        static readonly HashSet<int> releasing=new HashSet<int>();
        sealed class Pending
        {
            public FriendsLifeSave state;public ResidentController actor;public ActivityLease lease;
            public string personId,requestOwner,ownerId,reason,phase="PlanningPublicRoute";public float started=Time.unscaledTime,nextPlan;
            public ResidentActivityType activity;public WayPointIndex index;public WaypointAnchor anchor;public Vector3 target,authoredTarget,rotation;public int patrolIndex=-1;
        }
        readonly Dictionary<int,Pending> pending=new Dictionary<int,Pending>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install(){if(!instance)new GameObject("Original activity private-public physical transfer").AddComponent<FriendsOriginalActivityTransit>();}
        void Awake(){if(instance&&instance!=this){Destroy(gameObject);return;}instance=this;DontDestroyOnLoad(gameObject);}
        public static bool SuppressScheduleDuringRelease(ResidentController actor)=>actor&&releasing.Contains(actor.GetInstanceID());
        public static bool TryRoute(ResidentController actor,ResidentActivityType activity,WayPointIndex index,WaypointAnchor anchor,string requestOwner,out bool accepted,out string reason)
        {
            var runtime=FriendsLifeRuntime.Instance;
            var goal=actor?.Runtime?.TargetDestination??default;var rotation=actor?.Runtime?.TargetRotation??default;
            if(anchor){goal=anchor.transform.position;rotation=anchor.transform.eulerAngles;}
            return TryRouteCore(actor,activity,index,anchor,goal,rotation,requestOwner,out accepted,out reason);
        }
        public static bool TryRouteResolvedSchedule(ResidentController actor,ResidentActivityType activity,WayPointIndex index,string requestOwner,out bool accepted,out string reason)
            =>TryRouteCore(actor,activity,index,null,actor.Runtime.TargetDestination,actor.Runtime.TargetRotation,requestOwner,out accepted,out reason);
        static string Number(float value)=>value.ToString("R",CultureInfo.InvariantCulture);
        static bool TryRouteCore(ResidentController actor,ResidentActivityType activity,WayPointIndex index,WaypointAnchor anchor,Vector3 authored,Vector3 rotation,string requestOwner,out bool accepted,out string reason)
        {
            accepted=false;reason=null;var runtime=FriendsLifeRuntime.Instance;var identity=actor?actor.GetComponent<FriendWorldActor>():null;
            if(!actor||!identity||!runtime||!runtime.Ready)return false;
            if(!instance)Install();
            if(instance.pending.TryGetValue(actor.GetInstanceID(),out var old)&&ReferenceEquals(old.state,runtime.State))
            {accepted=old.requestOwner==requestOwner&&old.activity==activity&&old.index==index;reason=accepted?old.reason:"Another original activity request still owns this physical transfer";return true;}
            if(!FriendsPrivateCommonTransit.IsInOwnPrivateRoom(runtime,identity.personId))return false;
            var agent=actor.GetComponent<NavMeshAgent>();
            Vector3 target=authored;
            if(!agent||anchor&&!anchor.TryGetActivityApproach(agent,out target,out _)||!anchor&&!actor.Brain.Movement.TryResolveDestination(authored,actor.Brain.GetDestinationSampleRadius(activity),out target))
            {reason="The original public activity has no actual same-type approach";return true;}
            int patrol=actor.Runtime.CurrentPatrolEntryPointIndex;
            string facility=Facility+(int)activity+"|"+(int)index+"|"+Uri.EscapeDataString(requestOwner)+"|"+
                Number(authored.x)+"|"+Number(authored.y)+"|"+Number(authored.z)+"|"+Number(rotation.x)+"|"+Number(rotation.y)+"|"+Number(rotation.z)+"|"+patrol;
            string owner="original-activity-transit/"+runtime.State.worldId+"/"+identity.personId+"/"+Uri.EscapeDataString(requestOwner);
            if(!FriendsBodyAuthority.TryAcquire(runtime,identity.personId,owner,facility,out var lease,out reason))return true;
            var row=new Pending{state=runtime.State,actor=actor,personId=identity.personId,lease=lease,ownerId=owner,requestOwner=requestOwner,activity=activity,index=index,anchor=anchor,target=target,authoredTarget=authored,rotation=rotation,patrolIndex=patrol};
            instance.pending[actor.GetInstanceID()]=row;
            lease.destination=target;lease.phase="OriginalActivityTransitPlanning";accepted=true;return true;
        }
        static void PrepareActualCommonDoors(FriendsLifeRuntime runtime,Pending row)
        {
            var economy=FriendsEconomyService.Instance;var home=economy?.Plot(runtime.GetPerson(row.personId)?.currentHomeId)?.house;
            if(!home||!economy.CanEnter(home,row.personId))return;
            foreach(var door in home.GetComponentsInChildren<SushiAndFish.ResidentialIsland.HousingDoor>(true))if(door)FriendHousingDoor.OpenForResident(door);
        }
        public static OriginalActivitySnapshot Observe(ResidentController actor)
        {
            if(!instance||!actor||!instance.pending.TryGetValue(actor.GetInstanceID(),out var row))return null;
            var runtime=FriendsLifeRuntime.Instance;if(!runtime||!ReferenceEquals(row.state,runtime.State))return null;
            var actual=FriendsPrivateCommonTransit.Instance?.Observe(row.personId);var agent=actor.GetComponent<NavMeshAgent>();
            return new OriginalActivitySnapshot{phase=row.phase=="Failed"?"Failed":"Approaching",activity=row.activity.ToString(),requestOwner=row.requestOwner,ownerKey=FriendsBodyAuthority.IdentityKey(actor),
                reason=row.reason??actual?.reason??"正在从自己的房间前往公共活动",approach=row.target,pose=row.anchor?row.anchor.transform.position:row.target,actualPosition=actor.transform.position,
                safeBoundary=false,reserved=false,reservationsReleased=true,actorInstance=actor.GetInstanceID(),frame=Time.frameCount,modelVisible=actor.VisualAnimator&&actor.VisualAnimator.gameObject.activeInHierarchy,
                bodyAgentDistance=agent&&agent.enabled&&agent.isOnNavMesh?Vector3.Distance(actor.transform.position,agent.nextPosition):float.MaxValue};
        }
        void Update()
        {
            var runtime=FriendsLifeRuntime.Instance;if(!runtime||!runtime.Ready||!runtime.IsAuthority||!runtime.IsWorldRunning)return;
            foreach(var lease in runtime.State.leases.Where(l=>l.facilityId?.StartsWith(Facility,StringComparison.Ordinal)==true).ToArray())
            {
                var actor=runtime.ActorOf(lease.personId);if(!actor||pending.ContainsKey(actor.GetInstanceID()))continue;
                var bits=lease.facilityId.Split('|');
                if((bits.Length!=4&&bits.Length!=11)||!int.TryParse(bits[1],out int a)||!int.TryParse(bits[2],out int i)||
                    !Enum.IsDefined(typeof(ResidentActivityType),a)||(ResidentActivityType)a==ResidentActivityType.Home)continue;
                WaypointDirectory.TryGetAnchor((ResidentActivityType)a,(WayPointIndex)i,out var anchor);
                Vector3 authored=anchor?anchor.transform.position:lease.destination,rotation=anchor?anchor.transform.eulerAngles:Vector3.zero;int patrol=-1;
                if(bits.Length==11)
                {
                    var numbers=new float[6];bool valid=true;for(int n=0;n<6;n++)valid&=float.TryParse(bits[n+4],NumberStyles.Float,CultureInfo.InvariantCulture,out numbers[n])&&!float.IsNaN(numbers[n])&&!float.IsInfinity(numbers[n]);
                    if(!valid||!int.TryParse(bits[10],out patrol))continue;authored=new Vector3(numbers[0],numbers[1],numbers[2]);rotation=new Vector3(numbers[3],numbers[4],numbers[5]);
                }
                pending[actor.GetInstanceID()]=new Pending{state=runtime.State,actor=actor,personId=lease.personId,lease=lease,ownerId=lease.ownerEpisode,requestOwner=Uri.UnescapeDataString(bits[3]),
                    activity=(ResidentActivityType)a,index=(WayPointIndex)i,anchor=anchor,target=lease.destination,authoredTarget=authored,rotation=rotation,patrolIndex=patrol,phase=lease.phase=="OriginalActivityTransitFailed"?"Failed":lease.phase=="OriginalActivityTransitPlanning"||lease.phase=="Reserved"?"PlanningPublicRoute":"PrivateTransit"};
            }
            foreach(var row in pending.Values.ToArray())
            {
                if(!ReferenceEquals(row.state,runtime.State)||!row.actor||runtime.ActorOf(row.personId)!=row.actor||!runtime.State.leases.Contains(row.lease)||row.lease.ownerEpisode!=row.ownerId)
                {if(row.actor)pending.Remove(row.actor.GetInstanceID());continue;}
                if(row.phase=="PlanningPublicRoute")
                {
                    // A natural source minute may advance while the same body
                    // is paused for planning. No physical private leg has begun:
                    // safely release this obsolete request before asking the
                    // ORIGINAL Brain to evaluate its actual latest time.
                    var activeTransfer=FriendsPrivateCommonTransit.Instance?.Observe(row.personId);
                    bool transferOwnsBody=activeTransfer!=null&&activeTransfer.ownerEpisode==row.ownerId&&activeTransfer.leaseId==row.lease.leaseId&&
                        activeTransfer.phase!="Completed"&&activeTransfer.phase!="Failed";
                    if(row.requestOwner.StartsWith("schedule/",StringComparison.Ordinal)&&!transferOwnsBody&&
                        !row.actor.Brain.IsOriginalScheduleRequestCurrent(row.activity,row.index,row.requestOwner))
                    {
                        if(Time.unscaledTime<row.nextPlan)continue;
                        row.nextPlan=Time.unscaledTime+.25f;
                        int staleKey=row.actor.GetInstanceID();bool staleReleased;
                        releasing.Add(staleKey);
                        try{staleReleased=FriendsBodyAuthority.Release(runtime,row.personId,row.ownerId,true);}finally{releasing.Remove(staleKey);}
                        if(staleReleased)
                        {
                            pending.Remove(staleKey);
                            row.actor.Brain.RequestScheduleChange();
                            continue;
                        }
                        row.reason="The obsolete natural request is waiting for its actual original safe release";
                        if(Time.unscaledTime-row.started<=90f)continue;
                    }
                    if(Time.unscaledTime-row.started>90f){row.phase="Failed";row.reason="The original public route did not become physically ready within the existing host travel boundary";row.lease.phase="OriginalActivityTransitFailed";}
                    else if(Time.unscaledTime>=row.nextPlan)
                    {
                        row.nextPlan=Time.unscaledTime+.25f;PrepareActualCommonDoors(runtime,row);
                        if(FriendsPrivateCommonTransit.TryPlanningOrigin(runtime,row.personId,row.ownerId,out var origin,out _,out var planFailure)&&
                            ResolvePublicApproach(row,origin,out var target,out planFailure))
                        {
                            row.target=target;row.lease.destination=target;
                            if(runtime.TryMoveWorldActor(row.personId,target,out var resolved,.05f))
                            {row.target=resolved;row.lease.destination=resolved;row.phase="PrivateTransit";row.reason=null;}
                            else row.reason=runtime.LastWorldMoveDiagnostic;
                        }
                        else row.reason=planFailure;
                    }
                }
                var view=FriendsPrivateCommonTransit.Instance?.Observe(row.personId);
                bool failed=row.phase=="Failed"||row.lease.phase=="OriginalActivityTransitFailed"||view?.phase=="Failed";
                bool arrived=row.phase=="PrivateTransit"&&!FriendsPrivateCommonTransit.IsInOwnPrivateRoom(runtime,row.personId)&&OriginalNpcActivityPort.At(row.actor,row.target)&&(view==null||view.publicArrived&&view.phase=="Completed");
                if(!failed&&!arrived)continue;
                row.phase=failed?"Failed":"PublicReady";row.reason=row.reason??view?.reason;
                // Mark completion BEFORE root Release synchronously resumes the
                // original Brain; avoid recursively reserving the same leg.
                int key=row.actor.GetInstanceID();releasing.Add(key);bool released;
                try{released=FriendsBodyAuthority.Release(runtime,row.personId,row.ownerId,true);}finally{releasing.Remove(key);}
                if(!released)continue;
                pending.Remove(key);
                if(failed)
                {
                    OriginalNpcActivityPort.Remember(row.actor,new OriginalActivitySnapshot{phase="Failed",activity=row.activity.ToString(),requestOwner=row.requestOwner,
                        ownerKey=FriendsBodyAuthority.IdentityKey(row.actor),reason=row.reason??"Original private/public physical transfer failed",approach=row.target,pose=row.anchor?row.anchor.transform.position:row.target,
                        actualPosition=row.actor.transform.position,safeBoundary=true,reservationsReleased=true,frame=Time.frameCount,actorInstance=key});
                }
                else row.actor.Brain.ContinueResolvedScheduleAfterPublicTransit(row.activity,row.index,row.requestOwner,row.authoredTarget,row.rotation,row.patrolIndex);
            }
        }
        static bool ResolvePublicApproach(Pending row,Vector3 origin,out Vector3 target,out string failure)
        {
            if(row.anchor)return OriginalNpcActivityPort.ResolveApproachFrom(row.actor,row.anchor,origin,out _,out target,out failure);
            target=row.authoredTarget;failure=null;var agent=row.actor.GetComponent<NavMeshAgent>();
            if(!agent||!row.actor.Brain.Movement.TryResolveDestination(row.authoredTarget,row.actor.Brain.GetDestinationSampleRadius(row.activity),out target)||!OriginalNpcActivityPort.FootFits(row.actor,target))
            {failure="The actual original patrol/work destination has no physically supported same-type approach";return false;}
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};var path=new NavMeshPath();
            if(!NavMesh.CalculatePath(origin,target,filter,path)||path.status!=NavMeshPathStatus.PathComplete||path.corners.Length==0||
                Vector3.Distance(path.corners[0],origin)>.25f||Vector3.Distance(path.corners[path.corners.Length-1],target)>.15f)
            {failure="The original patrol/work target is not fully reachable from this actual public portal";return false;}
            return true;
        }
        void OnDestroy(){if(instance==this)instance=null;}
    }
}
