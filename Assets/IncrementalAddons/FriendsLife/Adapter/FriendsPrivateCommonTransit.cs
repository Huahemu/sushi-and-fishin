using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.ResidentialIsland;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    [Serializable] public sealed class PrivateCommonTransitSnapshot
    {
        public string worldId,personId,ownerEpisode,leaseId,homeId,phase,reason;
        public Vector3 requested,doorEndpoint,portalPoint,landing,actualPosition;
        public bool privateDoorArrived,portalUsed,publicArrived,recoveredAtCommon;
        public float portalDistance,bodyAgentDistance;
    }
    // Physical host actuator only. It borrows an existing source episode lease;
    // no core choices, elapsed activity, rewards, RNG or source result is made.
    [DefaultExecutionOrder(2120)]
    public sealed class FriendsPrivateCommonTransit:MonoBehaviour
    {
        const string TransitPhase="PrivateCommonTransit";
        public static FriendsPrivateCommonTransit Instance{get;private set;}
        sealed class Move
        {
            public FriendsLifeSave state;public ActivityLease lease;public SourceEpisode episode;
            public ResidentController actor;public HousingPrivateRoom room;public HousingRoomPortal portal;
            public PrivateCommonTransitSnapshot view;public float elapsed,retryAt;
        }
        // A refused physical step is re-evaluated at this interval, not every
        // frame: each attempt samples the full capsule path (hundreds of casts).
        const float RetrySeconds=.25f;
        readonly Dictionary<string,Move> moves=new Dictionary<string,Move>(StringComparer.Ordinal);
        static int bypass;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap(){if(!Instance)new GameObject("Friends owned private to common transit").AddComponent<FriendsPrivateCommonTransit>();}
        void Awake(){if(Instance&&Instance!=this){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);}
        static bool Inside(HousingHouse house,Vector3 point)
        {if(!house||house.definition==null)return false;var p=house.transform.InverseTransformPoint(point);return p.y>-2&&p.y<5&&Mathf.Abs(p.x)<house.definition.footprint.x*.5f+.6f&&Mathf.Abs(p.z)<house.definition.footprint.y*.5f+.6f;}
        public static bool IsInOwnPrivateRoom(FriendsLifeRuntime runtime,string personId)
        {var actor=runtime?.ActorOf(personId);var room=FriendsHomeInteriors.Instance?.RoomFor(personId);return actor&&room&&Inside(room.house,actor.transform.position);}
        static bool PrivateTarget(Vector3 point)=>FriendInstances<HousingPrivateRoom>.Active().Any(r=>r&&Inside(r.house,point));
        static bool Owner(FriendsLifeRuntime runtime,string personId,string episodeId,out ResidentController actor,out ActivityLease lease,out SourceEpisode episode,out string failure)
        {
            failure=null;actor=runtime?.ActorOf(personId);lease=null;episode=null;
            var person=runtime?.GetPerson(personId);
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||person?.controllerKind!=PersonControllerKind.Npc||!actor||!FriendResidentBindings.IsOwned(actor)||actor.Brain==null||actor.Runtime==null||runtime.Core.IsResidentAway(person.nativeResidentId))
            {failure="An actual authoritative owned NPC body is required";return false;}
            var leases=runtime.State.leases.Where(l=>l.personId==personId).ToArray();
            if(leases.Length!=1||string.IsNullOrEmpty(leases[0].ownerEpisode)||episodeId!=null&&leases[0].ownerEpisode!=episodeId)
            {failure="Private transit requires exactly the same existing episode lease";return false;}
            lease=leases[0];string ownerId=lease.ownerEpisode;episode=runtime.State.episodes.SingleOrDefault(e=>e.episodeId==ownerId);
            bool originalActivity=episode==null&&lease.facilityId?.StartsWith("OriginalActivityTransit|",StringComparison.Ordinal)==true&&FriendsBodyAuthority.IsOwned(runtime,personId,ownerId);
            var economy=FriendsEconomyService.Instance;
            bool staff=episode==null&&lease.facilityId=="SushiWork"&&economy&&economy.State.staffEntries.Any(e=>e.personId==personId&&e.ownerId==ownerId&&!e.sourceBodyObserved&&e.phase!="Done"&&e.phase!="Cancelled")&&economy.State.sushiAuthorizations.Any(a=>a.personId==personId);
            bool casino=episode==null&&lease.facilityId=="Casino"&&economy&&economy.State.originalCasinoJourneys.Any(j=>j.personId==personId&&j.id==ownerId&&!CasinoCore.FriendsCasinoJourneyService.Terminal(j));
            if(!originalActivity&&!staff&&!casino&&(episode==null||episode.committed||!new[]{episode.focusPersonId,episode.targetPersonId,episode.anotherPersonId}.Contains(personId)))
            {failure="Private transit episode/participant ownership is unavailable";return false;}
            return true;
        }
        static bool Portal(FriendsLifeRuntime runtime,string personId,ResidentController actor,out HousingPrivateRoom room,out HousingRoomPortal portal,out string failure)
        {
            failure=null;room=FriendsHomeInteriors.Instance?.RoomFor(personId);portal=null;var person=runtime.GetPerson(personId);var economy=FriendsEconomyService.Instance;
            var common=economy?.Plot(person.currentHomeId)?.house;
            if(!room||!room.house||room.personId!=personId||room.homeId!=person.currentHomeId||!common||!economy.CanEnter(room.house,personId)||!economy.CanEnter(common,personId)||room.house.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone!=true)
            {failure="Actual private room/common home generation or access changed";return false;}
            var ownedRoom=room;var candidates=FriendInstances<HousingRoomPortal>.Active().Where(p=>p&&p.isActiveAndEnabled&&p.allowedPersonId==personId&&p.homeId==person.currentHomeId&&p.leavingPrivateRoom&&p.GetComponentInParent<HousingHouse>()==ownedRoom.house&&p.destination&&p.destination==ownedRoom.returnToCommon&&p.destination.GetComponentInParent<HousingHouse>()==common).ToArray();
            if(candidates.Length!=1){failure="The exact actual private return portal is absent or ambiguous";return false;}portal=candidates[0];return true;
        }
        // Read-only prospective origin. The actual body remains in its room;
        // this is NOT an arrival, transfer, reservation or NavMesh rebuild.
        public static bool TryPlanningOrigin(FriendsLifeRuntime runtime,string personId,string ownerEpisode,out Vector3 origin,out bool viaPortal,out string failure)
        {
            origin=default;viaPortal=false;failure=null;var actor=runtime?.ActorOf(personId);if(!actor||actor.Brain==null)return false;origin=actor.transform.position;
            if(!IsInOwnPrivateRoom(runtime,personId))return true;
            if(!Owner(runtime,personId,ownerEpisode,out actor,out _,out _,out failure)||!Portal(runtime,personId,actor,out _,out var portal,out failure)||!ResolvePortalLanding(actor,portal.destination.position,out origin))
            {if(failure==null)failure="The actual common portal landing has no supported same-type endpoint";return false;}
            viaPortal=true;return true;
        }
        public static bool PlannedPath(ResidentController actor,Vector3 origin,Vector3 destination,out NavMeshPath path)
        {
            path=new NavMeshPath();var agent=actor?actor.GetComponent<NavMeshAgent>():null;if(!agent)return false;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            return NavMesh.CalculatePath(origin,destination,filter,path)&&path.status==NavMeshPathStatus.PathComplete&&path.corners.Length>0&&Vector3.Distance(path.corners[0],origin)<=.25f&&Vector3.Distance(path.corners[path.corners.Length-1],destination)<=.25f&&PermittedPath(actor,path,origin);
        }
        // Same NEW home privacy bounds as FriendHousingPrivacy, using the
        // prospective portal origin rather than a fictitious ray from22300.
        static bool PermittedPath(ResidentController actor,NavMeshPath path,Vector3 origin)
        {
            var economy=FriendsEconomyService.Instance;var person=actor.GetComponent<FriendWorldActor>()?.personId;if(!economy||string.IsNullOrEmpty(person))return false;
            foreach(var home in FriendInstances<FriendHouseMetadata>.Active().Where(m=>m.friendsOwnedClone).Select(m=>m.GetComponent<HousingHouse>()).Where(h=>h&&h.definition!=null))
            {
                if(economy.CanEnter(home,person))continue;var size=home.definition.footprint;var bounds=new Bounds(new Vector3(0,2.5f,0),new Vector3(size.x+1.1f,11,size.y+1.1f));var prior=home.transform.InverseTransformPoint(origin);if(bounds.Contains(prior))return false;
                foreach(var corner in path.corners){var p=home.transform.InverseTransformPoint(corner);var d=p-prior;float length=d.magnitude;if(bounds.Contains(p)||length>.0001f&&bounds.IntersectRay(new Ray(prior,d/length),out var hit)&&hit<=length+.0001f)return false;prior=p;}
            }
            return true;
        }
        public static bool TryHandle(FriendsLifeRuntime runtime,string personId,Vector3 requested,float sampleRadius,out bool accepted,out Vector3 resolved,out string failure)
        {
            accepted=false;resolved=requested;failure=null;if(bypass>0||!runtime||!runtime.Ready||!runtime.IsAuthority)return false;
            if(!Instance)Bootstrap();
            if(Instance.moves.TryGetValue(personId,out var current)&&ReferenceEquals(current.state,runtime.State)&&current.view.phase!="Completed"&&current.view.phase!="Failed")
            {
                if(Vector3.Distance(requested,current.view.requested)>.15f){failure="The same private-transit lease cannot replace its final target mid-transfer";return true;}
                resolved=current.view.requested;accepted=true;return true;
            }
            if(!IsInOwnPrivateRoom(runtime,personId))return false;
            var room=FriendsHomeInteriors.Instance.RoomFor(personId);if(Inside(room.house,requested))return false;
            if(PrivateTarget(requested)){failure="A common transit cannot jump into another private room";return true;}
            if(!Owner(runtime,personId,null,out var actor,out var lease,out var episode,out failure)||!Portal(runtime,personId,actor,out room,out var portal,out failure))return true;
            if(episode?.status=="SourceRunning")
            {failure="The source Walk leaf's arrival protocol does not yet bind asynchronous private transit; preserve its cursor instead of declaring its intermediate leg complete";return true;}
            if(!runtime.IsWorldRunning||FriendsHomeWorldActions.Controls(personId)||FriendsHomeStandingNavigation.Instance&&FriendsHomeStandingNavigation.Instance.Controls(personId)||FriendsEconomyService.Instance.HasActiveJob(personId)||actor.Runtime.IsWorking||actor.Runtime.IsInteracting||actor.Runtime.IsInRestaurant||runtime.GetComponent<FriendsNpcRestController>()?.IsResting(personId)==true)
            {failure="The same NPC still has an actual work/rest/furniture/standing owner";return true;}
            if(!ResolveFoot(actor,requested,out resolved)||PrivateTarget(resolved)||!TryPlanningOrigin(runtime,personId,lease.ownerEpisode,out var origin,out _,out failure)||!PlannedPath(actor,origin,resolved,out _))
            {if(failure==null)failure="Portal landing to requested public target has no complete permitted same-type route";return true;}
            Instance.StartTransit(runtime,actor,lease,episode,room,portal,resolved);accepted=true;return true;
        }
        void StartTransit(FriendsLifeRuntime runtime,ResidentController actor,ActivityLease lease,SourceEpisode episode,HousingPrivateRoom room,HousingRoomPortal portal,Vector3 target)
        {
            var move=new Move{state=runtime.State,actor=actor,lease=lease,episode=episode,room=room,portal=portal,view=new PrivateCommonTransitSnapshot{worldId=runtime.State.worldId,personId=lease.personId,ownerEpisode=lease.ownerEpisode,leaseId=lease.leaseId,homeId=runtime.GetPerson(lease.personId).currentHomeId,requested=target,phase=room&&Inside(room.house,actor.transform.position)?"Queued":"PlanPublic",recoveredAtCommon=!(room&&Inside(room.house,actor.transform.position))}};
            moves[lease.personId]=move;lease.destination=target;lease.phase=TransitPhase;lease.safeBoundary=false;runtime.NotifyChanged();
        }
        public PrivateCommonTransitSnapshot Observe(string personId)
        {if(!moves.TryGetValue(personId??"",out var m))return null;return JsonUtility.FromJson<PrivateCommonTransitSnapshot>(JsonUtility.ToJson(m.view));}
        void Update()
        {
            var runtime=FriendsLifeRuntime.Instance;if(!runtime||!runtime.Ready||!runtime.IsAuthority||!runtime.IsWorldRunning)return;
            // Existing lease.phase and FINAL destination are the durable host
            // continuation; a cold load resumes from the actual body location.
            foreach(var lease in runtime.State.leases.Where(l=>l.phase==TransitPhase).ToArray())
            {
                if(moves.TryGetValue(lease.personId,out var old)&&ReferenceEquals(old.state,runtime.State))continue;
                if(Owner(runtime,lease.personId,lease.ownerEpisode,out var actor,out _,out var episode,out _)&&Portal(runtime,lease.personId,actor,out var room,out var portal,out _))StartTransit(runtime,actor,lease,episode,room,portal,lease.destination);
            }
            foreach(var move in moves.Values.Where(m=>m.view.phase!="Completed"&&m.view.phase!="Failed").ToArray())try{Advance(runtime,move);}catch(Exception error){Fail(runtime,move,error.GetBaseException().Message);}
        }
        void Advance(FriendsLifeRuntime runtime,Move move)
        {
            var s=move.view;
            string failure=null;ResidentController actor=null;ActivityLease lease=null;SourceEpisode episode=null;
            if(!ReferenceEquals(move.state,runtime.State)||!Owner(runtime,s.personId,s.ownerEpisode,out actor,out lease,out episode,out failure)||actor!=move.actor||!ReferenceEquals(lease,move.lease)||!ReferenceEquals(episode,move.episode)||runtime.GetPerson(s.personId).currentHomeId!=s.homeId)
            {s.phase="Failed";s.reason="The original transit body/state/lease/home owner changed: "+failure;return;}
            if(FriendsHomeWorldActions.Controls(s.personId)||FriendsHomeStandingNavigation.Instance&&FriendsHomeStandingNavigation.Instance.Controls(s.personId)){Fail(runtime,move,"Another actual furniture/standing actuator acquired this body");return;}
            move.elapsed+=Time.unscaledDeltaTime;if(move.elapsed>90){Fail(runtime,move,"The existing bounded host travel window expired before actual public arrival");return;}
            actor.Brain.Pause();actor.Brain.SetModelVisible(true);var agent=actor.GetComponent<NavMeshAgent>();s.actualPosition=actor.transform.position;s.bodyAgentDistance=agent&&agent.enabled&&agent.isOnNavMesh?Vector3.Distance(actor.transform.position,agent.nextPosition):float.MaxValue;
            if(s.phase=="Queued")
            {
                if(!Portal(runtime,s.personId,actor,out var currentRoom,out var currentPortal,out failure)||currentRoom!=move.room||currentPortal!=move.portal){Fail(runtime,move,failure??"Actual private portal generation changed");return;}
                if(Time.unscaledTime<move.retryAt)return;
                if(!FriendsPrivateRoomNavigation.Ensure(move.room,actor,out failure)||!FriendsPrivateRoomNavigation.AlignActualBody(actor,out failure)){s.reason=failure;move.retryAt=Time.unscaledTime+RetrySeconds;return;}
                var inward=Vector3.ProjectOnPlane(move.room.house.transform.position-move.portal.transform.position,Vector3.up);if(inward.sqrMagnitude<.0001f)inward=-move.room.house.entry.forward;inward.Normalize();var capsule=actor.GetComponent<CapsuleCollider>();float distance=capsule?capsule.radius*2+.1f:.9f;
                HousingNavigation.PrepareDoorRoute(actor,move.portal.transform.position);
                if(!ResolveFoot(actor,move.portal.transform.position+inward*distance,out var target)||!PhysicalPath(actor,target)||!actor.Brain.Movement.MoveTo(target,actor.Brain.EffectiveMoveSpeed,.05f,true)){s.reason="The actual private capsule/complete path rejected the door approach";move.retryAt=Time.unscaledTime+RetrySeconds;return;}
                s.doorEndpoint=target;s.portalPoint=move.portal.transform.position;s.phase="PrivateDoorTravel";s.reason=null;
            }
            if(s.phase=="PrivateDoorTravel")
            {
                if(!Arrived(actor,s.doorEndpoint))return;s.portalDistance=Vector3.Distance(actor.transform.position,move.portal.transform.position);if(s.portalDistance>1.25f){Fail(runtime,move,"Actual body did not enter the original portal's1.25m interaction range");return;}
                s.privateDoorArrived=true;actor.Brain.Movement.Stop();s.phase="PortalTransfer";
            }
            if(s.phase=="PortalTransfer")
            {
                if(!Portal(runtime,s.personId,actor,out var room,out var portal,out failure)||room!=move.room||portal!=move.portal||!s.privateDoorArrived||Vector3.Distance(actor.transform.position,portal.transform.position)>1.25f){Fail(runtime,move,failure??"The exact reached portal changed before transfer");return;}
                if(Time.unscaledTime<move.retryAt)return;
                if(!ResolvePortalLanding(actor,portal.destination.position,out var landing)){s.reason="The actual common landing is not physically available";move.retryAt=Time.unscaledTime+RetrySeconds;return;}
                if(!actor.Brain.Movement.Warp(landing,Quaternion.Euler(0,portal.destination.eulerAngles.y,0))){s.reason="Original portal Warp rejected the supported common landing";move.retryAt=Time.unscaledTime+RetrySeconds;return;}
                actor.Brain.Movement.MatchAgentPositionAtHeight(agent.nextPosition.y);
                if(Vector3.Distance(actor.transform.position,landing)>.15f||Vector3.Distance(actor.transform.position,agent.nextPosition)>.15f){Fail(runtime,move,"Original portal Warp did not confirm the actual body/agent landing");return;}
                s.landing=landing;s.portalUsed=true;s.phase="PlanPublic";s.reason=null;
            }
            if(s.phase=="PlanPublic")
            {
                if(IsInOwnPrivateRoom(runtime,s.personId)||PrivateTarget(actor.transform.position)){Fail(runtime,move,"The public leg still starts in a private room");return;}
                if(Time.unscaledTime<move.retryAt)return;
                HousingNavigation.PrepareDoorRoute(actor,s.requested);
                if(!PhysicalPath(actor,s.requested)){s.reason="The actual common-to-public capsule path is not complete";move.retryAt=Time.unscaledTime+RetrySeconds;return;}
                bool accepted;Vector3 resolved;try{bypass++;accepted=runtime.TryMoveWorldActor(s.personId,s.requested,out resolved,.05f);}finally{bypass--;}
                if(!accepted){s.reason=runtime.LastWorldMoveDiagnostic;move.retryAt=Time.unscaledTime+RetrySeconds;return;}
                if(Vector3.Distance(resolved,s.requested)>.15f){Fail(runtime,move,"The final public endpoint changed after the original movement request");return;}
                s.phase="PublicTravel";s.reason=null;lease.destination=resolved;lease.phase="Travel";lease.safeBoundary=false;runtime.NotifyChanged();
            }
            if(s.phase=="PublicTravel"&&Arrived(actor,s.requested))
            {s.publicArrived=true;s.actualPosition=actor.transform.position;s.bodyAgentDistance=Vector3.Distance(actor.transform.position,agent.nextPosition);s.phase="Completed";s.reason=null;runtime.NotifyChanged();}
        }
        void Fail(FriendsLifeRuntime runtime,Move move,string reason)
        {
            move.view.phase="Failed";move.view.reason=reason;
            if(!runtime||!ReferenceEquals(runtime.State,move.state)||!runtime.State.leases.Contains(move.lease)||move.lease.ownerEpisode!=move.view.ownerEpisode)return;
            if(runtime.ActorOf(move.view.personId)==move.actor)move.actor?.Brain?.Movement.Stop();
            if(runtime.State.episodes.Contains(move.episode)&&!move.episode.committed){move.episode.status="Suspended";move.episode.fault="Private/common physical transit: "+reason;runtime.ReleaseEpisodeLeases(move.episode.episodeId);runtime.NotifyChanged();}
            else if(move.episode==null&&move.lease.facilityId?.StartsWith("OriginalActivityTransit|",StringComparison.Ordinal)==true)
            {move.lease.phase="OriginalActivityTransitFailed";move.lease.safeBoundary=true;runtime.NotifyChanged();}
        }
        // Verbatim physical gates from current housing11v2 Standing adapter.
        static bool Arrived(ResidentController actor,Vector3 destination)
        {var agent=actor.GetComponent<NavMeshAgent>();return agent&&agent.enabled&&agent.isOnNavMesh&&!agent.pathPending&&agent.pathStatus==NavMeshPathStatus.PathComplete&&actor.Brain.Movement.HasArrived()&&Vector3.Distance(actor.transform.position,destination)<=.2f&&Vector3.Distance(actor.Brain.Movement.Destination,destination)<=.15f&&Vector3.Distance(actor.transform.position,agent.nextPosition)<=.15f;}
        // A generated common-room return marker may lie exactly over a
        // decorative floor seam. Keep the existing .25m typed sampling domain
        // and unchanged support/penetration predicate; try only its immediate
        // five-centimetre neighbours before reporting the same real refusal.
        static bool ResolvePortalLanding(ResidentController actor,Vector3 requested,out Vector3 point)
        {
            if(ResolveFoot(actor,requested,out point)&&Vector3.Distance(point,requested)<=.25f)return true;
            var directions=new[]{Vector3.forward,-Vector3.forward,Vector3.right,-Vector3.right,
                (Vector3.forward+Vector3.right).normalized,(Vector3.forward-Vector3.right).normalized,
                (-Vector3.forward+Vector3.right).normalized,(-Vector3.forward-Vector3.right).normalized};
            foreach(var direction in directions)
                if(ResolveFoot(actor,requested+direction*.05f,out var candidate)&&Vector3.Distance(candidate,requested)<=.25f)
                {point=candidate;return true;}
            point=default;return false;
        }
        static bool ResolveFoot(ResidentController actor,Vector3 requested,out Vector3 point)
        {
            point=default;var agent=actor.GetComponent<NavMeshAgent>();var capsule=actor.GetComponent<CapsuleCollider>();if(!agent||!capsule||capsule.direction!=1)return false;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};if(!NavMesh.SamplePosition(requested,out var nav,.25f,filter))return false;var settings=NavMesh.GetSettingsByID(agent.agentTypeID);
            var ground=Physics.RaycastAll(nav.position+Vector3.up,Vector3.down,2,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider&&!h.collider.GetComponentInParent<ResidentController>()&&h.normal.y>=Mathf.Cos(settings.agentSlope*Mathf.Deg2Rad)).OrderBy(h=>Mathf.Abs(h.point.y-nav.position.y)).FirstOrDefault();if(!ground.collider||Mathf.Abs(ground.point.y-nav.position.y)>.12f)return false;
            var scale=capsule.transform.lossyScale;float radius=capsule.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));float height=Mathf.Max(radius*2,capsule.height*Mathf.Abs(scale.y));var center=nav.position+actor.transform.rotation*Vector3.Scale(capsule.center,scale);float half=height*.5f-radius;
            foreach(var c in Physics.OverlapCapsule(center+Vector3.up*(half+.025f),center-Vector3.up*(half-.025f),radius,~0,QueryTriggerInteraction.Ignore))
            {if(!c||c.transform.IsChildOf(actor.transform)||c.GetComponentInParent<EmoteArea>())continue;var at=capsule.transform.position+(nav.position-actor.transform.position)+Vector3.up*.025f;if(Physics.ComputePenetration(capsule,at,capsule.transform.rotation,c,c.transform.position,c.transform.rotation,out var normal,out var depth)&&(Vector3.Dot(normal,Vector3.up)<.5f||depth>capsule.contactOffset))return false;}
            point=nav.position;return true;
        }
        static bool PhysicalPath(ResidentController actor,Vector3 destination)
        {
            var agent=actor.GetComponent<NavMeshAgent>();var path=new NavMeshPath();if(!agent||!agent.enabled||!agent.isOnNavMesh||!agent.CalculatePath(destination,path)||path.status!=NavMeshPathStatus.PathComplete||path.corners.Length==0||Vector3.Distance(path.corners[0],actor.transform.position)>.25f||Vector3.Distance(path.corners[path.corners.Length-1],destination)>.25f)return false;
            if(!PermittedPath(actor,path,actor.transform.position))return false;
            foreach(var pair in path.corners.Zip(path.corners.Skip(1),(a,b)=>(a,b))){int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(pair.a,pair.b)/.2f));for(int i=0;i<=count;i++)if(!ResolveFoot(actor,Vector3.Lerp(pair.a,pair.b,(float)i/count),out _))return false;}return true;
        }
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
