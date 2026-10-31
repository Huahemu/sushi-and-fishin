using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.ResidentialIsland;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    [Serializable]
    public sealed class HomeStandingMoveSnapshot
    {
        public string actionKey,ownerEpisode,personId,homeId,anchorId,phase="Queued",reason;
        public bool arrived,safeBoundary,privateDoorArrived,portalUsed;
        public Vector3 destination;
    }
    // Navigation only. Original Family Care/Grow guards, inputs and immediate
    // results remain in the original kernel; this actuator never calls them.
    [DefaultExecutionOrder(2100)]
    public sealed class FriendsHomeStandingNavigation : MonoBehaviour
    {
        public static FriendsHomeStandingNavigation Instance{get;private set;}
        sealed class Move
        {
            public HomeStandingMoveSnapshot view;
            public int slot;
            public ResidentController actor;
            public HousingPrivateRoom room;
            public HousingRoomPortal portal;
            public HousingHouse common;
            public ActivityLease lease;
            public bool ownLease,paused;
            public string previousPhase;public Vector3 previousDestination;public bool previousSafe;
            public float travelElapsed,retryAt;
        }
        // A refused route is re-evaluated at this interval, not every frame:
        // each candidate samples its full capsule path (hundreds of casts).
        const float RetrySeconds=.25f;
        readonly Dictionary<string,Move> moves=new Dictionary<string,Move>(StringComparer.Ordinal);
        static bool Terminal(Move m)=>m.view.phase=="Completed"||m.view.phase=="Failed";
        void Awake(){Instance=this;}
        public bool TryBegin(string personId,string actionKey,string ownerEpisode,string homeId,int slotIndex=0)
        {
            var runtime=FriendsLifeRuntime.Instance;
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||string.IsNullOrEmpty(actionKey)||string.IsNullOrEmpty(ownerEpisode)||
                runtime.GetPerson(personId)?.controllerKind!=PersonControllerKind.Npc||string.IsNullOrEmpty(homeId))return false;
            if(moves.TryGetValue(actionKey,out var old))return old.view.personId==personId&&old.view.ownerEpisode==ownerEpisode&&old.view.homeId==homeId&&old.slot==slotIndex;
            if(FriendsHomeWorldActions.Controls(personId))return false;
            if(moves.Values.Any(m=>m.view.personId==personId&&!Terminal(m)))return false;
            moves[actionKey]=new Move{slot=slotIndex,view=new HomeStandingMoveSnapshot{actionKey=actionKey,ownerEpisode=ownerEpisode,personId=personId,homeId=homeId}};return true;
        }
        public HomeStandingMoveSnapshot Observe(string key)
        {
            if(!moves.TryGetValue(key??"",out var move))return null;var s=move.view;
            return new HomeStandingMoveSnapshot{actionKey=s.actionKey,ownerEpisode=s.ownerEpisode,personId=s.personId,homeId=s.homeId,anchorId=s.anchorId,phase=s.phase,
                reason=s.reason,arrived=s.arrived,safeBoundary=s.safeBoundary,privateDoorArrived=s.privateDoorArrived,portalUsed=s.portalUsed,destination=s.destination};
        }
        public bool RequestExit(string key,string owner)
        {if(!moves.TryGetValue(key??"",out var move)||move.view.ownerEpisode!=owner)return false;if(!Terminal(move))Finish(move,null);return true;}
        public bool RequestOwnerExit(string owner)
        {foreach(var m in moves.Values.Where(m=>m.view.ownerEpisode==owner&&!Terminal(m)).ToArray())Finish(m,null);return true;}
        public bool OwnerAtSafeBoundary(string owner)=>moves.Values.Where(m=>m.view.ownerEpisode==owner).All(m=>Terminal(m)&&m.view.safeBoundary);
        public bool Forget(string key,string owner)=>moves.TryGetValue(key??"",out var m)&&m.view.ownerEpisode==owner&&Terminal(m)&&moves.Remove(key);
        public bool Controls(string personId)=>moves.Values.Any(m=>m.view.personId==personId&&!Terminal(m));
        void Update()
        {
            var runtime=FriendsLifeRuntime.Instance;if(!runtime||!runtime.Ready||!runtime.IsAuthority||!runtime.IsWorldRunning)return;
            foreach(var move in moves.Values.Where(m=>!Terminal(m)).ToArray())try{Advance(runtime,move);}catch(Exception error){Finish(move,error.GetBaseException().Message);}
        }
        static bool Inside(HousingHouse house,Vector3 point)
        {
            var p=house.transform.InverseTransformPoint(point);return p.y>-2&&p.y<5&&Mathf.Abs(p.x)<house.definition.footprint.x*.5f+.6f&&Mathf.Abs(p.z)<house.definition.footprint.y*.5f+.6f;
        }
        void Advance(FriendsLifeRuntime runtime,Move move)
        {
            var s=move.view;var economy=FriendsEconomyService.Instance;if(!economy)return;
            if(s.phase=="Queued")
            {
                // Continue a pre-existing owner, but defer any new claim.
                if(runtime.HasActiveInPlaceTalk(s.personId)&&!runtime.State.leases.Any(l=>l.personId==s.personId&&l.ownerEpisode==s.ownerEpisode&&l.phase!="JobHandover"))return;
                var rest=runtime.GetComponent<FriendsNpcRestController>();
                if(rest&&rest.IsResting(s.personId)){s.reason="Waiting for the actual original rest safe exit";return;}
                if(FriendsHomeWorldActions.Controls(s.personId)||economy.HasActiveJob(s.personId)||runtime.State.leases.Any(l=>l.personId==s.personId&&l.ownerEpisode!=s.ownerEpisode))return;
                var actor=runtime.ActorOf(s.personId);if(!actor||!FriendResidentBindings.IsOwned(actor)||actor.Brain==null||actor.Runtime==null||actor.Runtime.IsWorking||actor.Runtime.IsInteracting||actor.Runtime.IsInRestaurant)return;
                var common=economy.Plot(s.homeId)?.house;if(!common||!common.entry||common.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone!=true)return;
                move.actor=actor;move.common=common;
                if(move.lease==null)
                {
                    string ownId="home-standing/"+s.actionKey;move.lease=runtime.State.leases.FirstOrDefault(l=>l.personId==s.personId&&l.ownerEpisode==s.ownerEpisode);
                    move.ownLease=move.lease==null||move.lease.leaseId==ownId;
                    if(move.lease==null){move.lease=new ActivityLease{leaseId=ownId,personId=s.personId,ownerEpisode=s.ownerEpisode,facilityId=s.homeId};runtime.State.leases.Add(move.lease);}
                    move.previousPhase=move.lease.phase;move.previousDestination=move.lease.destination;move.previousSafe=move.lease.safeBoundary;
                }
                if(!actor.Runtime.IsSnapshotApplying){actor.Brain.Pause();move.paused=true;}actor.Brain.SetModelVisible(true);
                var room=FriendsHomeInteriors.Instance?.RoomFor(s.personId);
                if(room&&room.house&&Inside(room.house,actor.transform.position))
                {
                    move.room=room;
                    if(Time.unscaledTime<move.retryAt)return;
                    if(!FriendsPrivateRoomNavigation.Ensure(room,actor,out var failure)||!FriendsPrivateRoomNavigation.AlignActualBody(actor,out failure))
                    {s.reason=failure;move.retryAt=Time.unscaledTime+RetrySeconds;return;}
                    move.portal=FriendInstances<HousingRoomPortal>.Active().FirstOrDefault(p=>p.allowedPersonId==s.personId&&p.leavingPrivateRoom&&p.GetComponentInParent<HousingHouse>()==room.house&&p.destination);
                    if(!move.portal){Finish(move,"The exact actual private return portal is unavailable");return;}
                    var inward=Vector3.ProjectOnPlane(room.house.transform.position-move.portal.transform.position,Vector3.up);
                    if(inward.sqrMagnitude<.0001f)inward=-room.house.entry.forward;inward.Normalize();
                    var capsule=actor.GetComponent<CapsuleCollider>();float approachDistance=capsule?capsule.radius*2+.1f:.9f;
                    if(!StartWalk(runtime,move,move.portal.transform.position+inward*approachDistance,"PrivateDoorTravel")){move.retryAt=Time.unscaledTime+RetrySeconds;return;}
                }
                else s.phase="PlanCommon";
            }
            if(!move.actor||move.actor.Brain==null||!move.common||move.lease==null||!runtime.State.leases.Contains(move.lease)||
                move.lease.ownerEpisode!=s.ownerEpisode||runtime.State.leases.Any(l=>l.personId==s.personId&&l.ownerEpisode!=s.ownerEpisode))
            {Finish(move,"The actual standing movement owner or room changed");return;}
            move.actor.Brain.SetModelVisible(true);
            if(s.phase=="CommonTravel"||s.phase=="PrivateDoorTravel")
            {
                move.travelElapsed+=Time.unscaledDeltaTime;
                if(move.travelElapsed>90){Finish(move,"The actual standing movement did not reach its endpoint within the existing host travel boundary");return;}
            }
            if(s.phase=="PrivateDoorTravel")
            {
                if(!Arrived(move.actor,s.destination))return;
                if(Vector3.Distance(move.actor.transform.position,move.portal.transform.position)>1.25f){s.reason="Actual portal range rejected the private-door endpoint";return;}
                s.privateDoorArrived=true;s.phase="PrivatePortalTransfer";move.actor.Brain.Movement.Stop();
            }
            if(s.phase=="PrivatePortalTransfer")
            {
                // This is the existing authored portal destination and can be
                // used only after real arrival at that actual portal.
                if(!move.portal||!move.portal.destination||!s.privateDoorArrived){Finish(move,"The actual private portal changed before transfer");return;}
                if(!ResolveFoot(move.actor,move.portal.destination.position,out var actual)){s.reason="Waiting for the actual common return landing";return;}
                if(!move.actor.Brain.Movement.Warp(actual,Quaternion.Euler(0,move.portal.destination.eulerAngles.y,0)))return;
                var agent=move.actor.GetComponent<NavMeshAgent>();move.actor.Brain.Movement.MatchAgentPositionAtHeight(agent.nextPosition.y);
                if(Vector3.Distance(move.actor.transform.position,actual)>.15f||Vector3.Distance(move.actor.transform.position,agent.nextPosition)>.15f)
                {s.reason="The original portal transfer has not confirmed its actual endpoint";return;}
                s.portalUsed=true;s.phase="PlanCommon";move.room=null;
            }
            if(s.phase=="PlanCommon")
            {
                if(Time.unscaledTime<move.retryAt)return;
                var outward=-Vector3.ProjectOnPlane(move.common.entry.forward,Vector3.up);if(outward.sqrMagnitude<.0001f)outward=move.common.transform.forward;outward.Normalize();
                var right=Vector3.Cross(Vector3.up,outward);float preferred=move.slot%3==0?-.6f:move.slot%3==1?.6f:0;
                bool started=false;
                foreach(float distance in new[]{move.slot%3==2?2.4f:1.5f,1.9f,2.3f})
                {
                    foreach(float side in new[]{preferred,preferred-.5f,preferred+.5f})
                    {
                        var wanted=move.common.entry.position+outward*distance+right*side;
                        if(!ResolveFoot(move.actor,wanted,out var target))continue;
                        if(moves.Values.Any(other=>other!=move&&!Terminal(other)&&other.view.phase!="Queued"&&Vector3.Distance(other.view.destination,target)<.9f))continue;
                        if(StartWalk(runtime,move,target,"CommonTravel")){s.anchorId="common-entry/"+s.homeId+"/"+FriendHousingIsland.HomeIdentity(move.common)+"/"+move.slot;started=true;break;}
                    }
                    if(started)break;
                }
                if(!started){s.reason="The actual common entry has no complete permitted standing route";move.retryAt=Time.unscaledTime+RetrySeconds;return;}
            }
            if(s.phase=="CommonTravel"&&Arrived(move.actor,s.destination))
            {
                move.actor.Brain.Movement.StopAtRotation(Quaternion.Euler(0,move.common.entry.eulerAngles.y,0));
                s.phase="Arrived";s.arrived=true;s.safeBoundary=true;s.reason=null;move.lease.phase="HomeStanding";move.lease.safeBoundary=true;runtime.NotifyChanged();
            }
        }
        bool StartWalk(FriendsLifeRuntime runtime,Move move,Vector3 requested,string phase)
        {
            HousingNavigation.PrepareDoorRoute(move.actor,requested);
            if(!move.actor.Brain.Movement.TryResolveDestination(requested,.25f,out var prepared)||!PhysicalPath(move.actor,prepared))
            {move.view.reason="The actual capsule/support or complete standing path rejected this endpoint";return false;}
            if(!runtime.TryMoveWorldActor(move.view.personId,requested,out var actual,.25f)){move.view.reason=runtime.LastWorldMoveDiagnostic;return false;}
            move.view.destination=actual;move.view.phase=phase;move.view.safeBoundary=false;move.lease.destination=actual;move.lease.phase=phase;move.lease.safeBoundary=false;move.travelElapsed=0;runtime.NotifyChanged();return true;
        }
        static bool Arrived(ResidentController actor,Vector3 destination)
        {
            var agent=actor.GetComponent<NavMeshAgent>();return agent&&agent.enabled&&agent.isOnNavMesh&&!agent.pathPending&&agent.pathStatus==NavMeshPathStatus.PathComplete&&
                actor.Brain.Movement.HasArrived()&&Vector3.Distance(actor.transform.position,destination)<=.2f&&Vector3.Distance(actor.Brain.Movement.Destination,destination)<=.15f;
        }
        static bool ResolveFoot(ResidentController actor,Vector3 requested,out Vector3 point)
        {
            point=default;var agent=actor.GetComponent<NavMeshAgent>();var capsule=actor.GetComponent<CapsuleCollider>();if(!agent||!capsule||capsule.direction!=1)return false;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            if(!NavMesh.SamplePosition(requested,out var nav,.25f,filter))return false;
            var settings=NavMesh.GetSettingsByID(agent.agentTypeID);
            var ground=Physics.RaycastAll(nav.position+Vector3.up,Vector3.down,2,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>h.collider&&!h.collider.GetComponentInParent<ResidentController>()&&h.normal.y>=Mathf.Cos(settings.agentSlope*Mathf.Deg2Rad))
                .OrderBy(h=>Mathf.Abs(h.point.y-nav.position.y)).FirstOrDefault();
            if(!ground.collider||Mathf.Abs(ground.point.y-nav.position.y)>.12f)return false;
            var scale=capsule.transform.lossyScale;float radius=capsule.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));float height=Mathf.Max(radius*2,capsule.height*Mathf.Abs(scale.y));
            var center=nav.position+actor.transform.rotation*Vector3.Scale(capsule.center,scale);float half=height*.5f-radius;
            foreach(var c in Physics.OverlapCapsule(center+Vector3.up*(half+.025f),center-Vector3.up*(half-.025f),radius,~0,QueryTriggerInteraction.Ignore))
            {
                if(!c||c.transform.IsChildOf(actor.transform)||c.GetComponentInParent<EmoteArea>())continue;
                var at=capsule.transform.position+(nav.position-actor.transform.position)+Vector3.up*.025f;
                if(Physics.ComputePenetration(capsule,at,capsule.transform.rotation,c,c.transform.position,c.transform.rotation,out var normal,out var depth)&&
                    (Vector3.Dot(normal,Vector3.up)<.5f||depth>capsule.contactOffset))return false;
            }
            point=nav.position;return true;
        }
        static bool PhysicalPath(ResidentController actor,Vector3 destination)
        {
            var agent=actor.GetComponent<NavMeshAgent>();var path=new NavMeshPath();if(!agent||!agent.enabled||!agent.isOnNavMesh||!agent.CalculatePath(destination,path)||path.status!=NavMeshPathStatus.PathComplete||path.corners.Length==0)return false;
            if(Vector3.Distance(path.corners[0],actor.transform.position)>.25f)return false;
            foreach(var pair in path.corners.Zip(path.corners.Skip(1),(a,b)=>(a,b)))
            {
                int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(pair.a,pair.b)/.2f));
                for(int i=0;i<=count;i++)if(!ResolveFoot(actor,Vector3.Lerp(pair.a,pair.b,(float)i/count),out _))return false;
            }
            return true;
        }
        void Finish(Move move,string failure)
        {
            var runtime=FriendsLifeRuntime.Instance;var s=move.view;if(Terminal(move))return;
            bool current=runtime&&move.actor&&runtime.ActorOf(s.personId)==move.actor&&move.lease!=null&&runtime.State.leases.Contains(move.lease)&&move.lease.ownerEpisode==s.ownerEpisode;
            if(current)
            {
                move.actor.Brain?.Movement.Stop();
                if(move.ownLease)runtime.State.leases.Remove(move.lease);
                else{move.lease.phase=move.previousPhase;move.lease.destination=move.previousDestination;move.lease.safeBoundary=move.previousSafe;}
                if(move.paused&&!runtime.State.leases.Any(l=>l.personId==s.personId)&&FriendsEconomyService.Instance?.HasActiveJob(s.personId)!=true)
                {move.actor.Brain.Resume();move.actor.Brain.RequestScheduleChange();}
            }
            s.phase=failure==null?"Completed":"Failed";s.reason=failure;s.safeBoundary=true;if(runtime&&runtime.Ready)runtime.NotifyChanged();
        }
        void OnDestroy(){foreach(var m in moves.Values.Where(m=>!Terminal(m)).ToArray())Finish(m,"The standing movement projection was unloaded");if(Instance==this)Instance=null;}
    }
}
