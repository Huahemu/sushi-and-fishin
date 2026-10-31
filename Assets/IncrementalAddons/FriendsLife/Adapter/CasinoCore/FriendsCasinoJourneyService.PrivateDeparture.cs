using System;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.Casino;
using SushiAndFish.ResidentialIsland;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife.CasinoCore
{
    public sealed partial class CasinoJourney
    {
        public string bodyLeaseId,departureHomeId;
        public int departureBodyInstance;
        public bool privateDepartureRequired,privateDepartureStarted,privateDepartureComplete;
        public Vector3 commonPlanningOrigin,homeDepartureTarget;
        public PrivateCommonTransitSnapshot privateDepartureSnapshot;
    }
    public sealed partial class FriendsCasinoJourneyService
    {
        HousingHouse CurrentCommon(CasinoJourney j)=>FriendsEconomyService.Instance?.Plot(runtime.GetPerson(j.personId)?.currentHomeId)?.house;
        bool NeedsHomeDeparture(CasinoJourney j,ResidentController actor)
        {
            j.privateDepartureRequired=FriendsPrivateCommonTransit.IsInOwnPrivateRoom(runtime,j.personId);
            var home=CurrentCommon(j);
            if(AlreadyAtOriginalPublicOutside(j,actor,out var outside))
            {j.commonPlanningOrigin=actor.transform.position;j.homeDepartureTarget=outside;return false;}
            return j.privateDepartureRequired||home&&HousingPrivacyVolume.Of(home).Contains(actor.transform.position,.1f);
        }
        bool AlreadyAtOriginalPublicOutside(CasinoJourney j,ResidentController actor,out Vector3 outside)
        {
            outside=default;
            if(j.privateDepartureRequired||FriendsPrivateCommonTransit.IsInOwnPrivateRoom(runtime,j.personId)||!Continuous(actor))return false;
            var home=CurrentCommon(j);var agent=actor.GetComponent<NavMeshAgent>();
            if(!home||!HousingNavigation.TryResolveFriendHome(home,agent,out outside,out _)||Vector3.Distance(actor.transform.position,outside)>.15f)return false;
            if(!FriendsPrivateCommonTransit.TryPlanningOrigin(runtime,j.personId,j.id,out var origin,out bool viaPortal,out _)||viaPortal||
                Vector3.Distance(origin,actor.transform.position)>.15f||!FriendsPrivateCommonTransit.PlannedPath(actor,origin,outside,out var path))return false;
            // The original resolver/permissions confirm this actual body already
            // occupies its public outside waypoint. A one-corner complete path
            // needs no new home walk and certifies no private portal traversal.
            return path.corners.Length==1;
        }
        bool SameDepartureOwner(CasinoJourney j,ResidentController actor,out string reason)
        {
            reason=null;var leases=runtime.State.leases.Where(l=>l.personId==j.personId).ToArray();
            if(!actor||actor.GetInstanceID()!=j.departureBodyInstance||runtime.ActorOf(j.personId)!=actor||leases.Length!=1||
                leases[0].ownerEpisode!=j.id||leases[0].leaseId!=j.bodyLeaseId||leases[0].facilityId!="Casino"||runtime.GetPerson(j.personId)?.currentHomeId!=j.departureHomeId)
            {reason="Actual casino departure body/world/home/same lease changed; retain this journey without replacement";return false;}
            return true;
        }
        void SetCasinoLease(CasinoJourney j,string phase,Vector3 destination,bool safe)
        {
            var lease=runtime.State.leases.SingleOrDefault(l=>l.personId==j.personId&&l.ownerEpisode==j.id);
            if(lease==null||!string.IsNullOrEmpty(j.bodyLeaseId)&&lease.leaseId!=j.bodyLeaseId)return;
            lease.phase=phase;lease.destination=destination;lease.safeBoundary=safe;
        }
        void AdmitPublicJourney(CasinoJourney j,ResidentController actor)
        {
            if(FriendsPrivateCommonTransit.IsInOwnPrivateRoom(runtime,j.personId)||!Continuous(actor))
            {j.failure="Actual public body/agent departure has not been confirmed";return;}
            SetCasinoLease(j,"CasinoJourney",actor.transform.position,true);
            var passenger=new CasinoResidentPassenger(runtime,j);
            if(!NativeCasinoTransportTransaction.Register(passenger)){j.failure="Original passenger registration refused";return;}
            passengers[j.id]=passenger;Record(j,"LobbyApproach");
        }
        void PrepareCommonDoors(CasinoJourney j)
        {
            var home=CurrentCommon(j);var economy=FriendsEconomyService.Instance;
            if(!home||!economy||!economy.CanEnter(home,j.personId))return;
            foreach(var door in home.GetComponentsInChildren<HousingDoor>(true))if(door)FriendHousingDoor.OpenForResident(door);
        }
        bool SelectActualDepartureTarget(CasinoJourney j,ResidentController actor,out string reason)
        {
            reason=null;PrepareCommonDoors(j);
            if(!FriendsPrivateCommonTransit.TryPlanningOrigin(runtime,j.personId,j.id,out var origin,out bool viaPortal,out reason))return false;
            if(j.privateDepartureRequired&&!viaPortal){reason="Expected private source body is no longer at its actual room before original transit";return false;}
            j.commonPlanningOrigin=origin;
            // A zero-length endpoint has no ordinary final path. Use the same
            // existing public home waypoint used by normal resident home exit;
            // no invented offset, landing, room or source origin is introduced.
            var home=CurrentCommon(j);var agent=actor.GetComponent<NavMeshAgent>();
            if(home&&HousingNavigation.TryResolveFriendHome(home,agent,out var outside,out _)&&
                FriendsPrivateCommonTransit.PlannedPath(actor,origin,outside,out var outsidePath)&&outsidePath.corners.Length>1)
            {j.homeDepartureTarget=outside;return true;}
            reason="Original common landing/home outside waypoint has no complete permitted final path";return false;
        }
        void AdvanceHomeDeparture(CasinoJourney j,ResidentController actor,ParkCasinoIntegration casino)
        {
            if(!SameDepartureOwner(j,actor,out var reason)){Record(j,"AwaitInspection",reason);return;}
            if(j.phase=="HomeDeparturePlanning")
            {
                if(j.cancelRequested)
                {
                    if(Continuous(actor)&&FriendsBodyAuthority.Release(runtime,j.personId,j.id,true))Record(j,"Cancelled","原房间过渡尚未发送，身体已交还原日程。");return;
                }
                if(AlreadyAtOriginalPublicOutside(j,actor,out var outside))
                {
                    j.commonPlanningOrigin=actor.transform.position;j.homeDepartureTarget=outside;
                    SetCasinoLease(j,"CasinoJourney",actor.transform.position,true);
                    Record(j,"HomeDepartureReady","实际已在原公共住宅户外落点；无需新增住宅步行，未发送私人portal过渡。");return;
                }
                if(!SelectActualDepartureTarget(j,actor,out reason)){j.failure=reason;return;}
                if(!runtime.TryMoveWorldActor(j.personId,j.homeDepartureTarget,out var resolved,.05f))
                {j.failure=runtime.LastWorldMoveDiagnostic;return;}
                if(Vector3.Distance(resolved,j.homeDepartureTarget)>.15f)
                {Record(j,"AwaitInspection","Original private/common endpoint changed after acceptance; retain same request");return;}
                if(j.privateDepartureRequired)
                {
                    j.privateDepartureStarted=true;
                    Record(j,"HomeDepartureTransit","同一旅程/租约已受理原房门步行与原portal，等待实际公共区回读。");
                }
                else
                {
                    SetCasinoLease(j,"CasinoHomeDeparture",resolved,false);
                    Record(j,"HomeDepartureWalk","沿原公共住宅门路前往既有户外落点。");
                }
                return;
            }
            if(j.phase=="HomeDepartureTransit")
            {
                var view=FriendsPrivateCommonTransit.Instance?.Observe(j.personId);j.privateDepartureSnapshot=view;
                if(view==null||view.worldId!=runtime.State.worldId||view.personId!=j.personId||view.ownerEpisode!=j.id||
                    view.leaseId!=j.bodyLeaseId||view.homeId!=j.departureHomeId||Vector3.Distance(view.requested,j.homeDepartureTarget)>.15f)
                {Record(j,"AwaitInspection","Existing original private/common transit identity or final endpoint is missing/mismatched");return;}
                if(view.phase=="Failed")
                {
                    j.failure=view.reason;
                    if(j.cancelRequested&&Continuous(actor)&&FriendsBodyAuthority.Release(runtime,j.personId,j.id,true))Record(j,"Cancelled",view.reason);
                    return;
                }
                if(view.phase!="Completed"||!view.privateDoorArrived||!view.portalUsed||!view.publicArrived||view.recoveredAtCommon)
                {j.failure=view.reason??"等待真实门前到达、原portal及公共区落地完成。";return;}
                if(Vector3.Distance(view.landing,j.commonPlanningOrigin)>.15f||Vector3.Distance(view.actualPosition,actor.transform.position)>.2f||!AtDeparture(actor,j.homeDepartureTarget))
                {j.failure="Original completed transit has not confirmed the same actual supported body/agent endpoint";return;}
                j.privateDepartureComplete=true;SetCasinoLease(j,"CasinoJourney",actor.transform.position,true);
                Record(j,"HomeDepartureReady","已真实通过原房门与portal到公共区，并到达原住宅户外落点。");return;
            }
            if(j.phase=="HomeDepartureWalk")
            {
                if(!AtDeparture(actor,j.homeDepartureTarget)){j.failure="等待实际走出公共住宅门路。";return;}
                SetCasinoLease(j,"CasinoJourney",actor.transform.position,true);Record(j,"HomeDepartureReady");return;
            }
            if(j.phase=="HomeDepartureReady")
            {
                if(j.cancelRequested)
                {
                    if(Continuous(actor)&&FriendsBodyAuthority.Release(runtime,j.personId,j.id,true))Record(j,"Cancelled","原房间过渡已完成，在真实公共落点安全交还日程。");return;
                }
                if(casino.CurrentFloor!=0){j.failure="已在真实公共落点，等待原电梯返回Ground。";return;}
                AdmitPublicJourney(j,actor);
            }
        }
        static bool AtDeparture(ResidentController actor,Vector3 point)
        {
            var agent=actor?actor.GetComponent<NavMeshAgent>():null;
            return Continuous(actor)&&agent&&!agent.pathPending&&agent.pathStatus==NavMeshPathStatus.PathComplete&&
                actor.Brain.Movement.HasArrived()&&Vector3.Distance(actor.transform.position,point)<=.2f&&
                Vector3.Distance(actor.Brain.Movement.Destination,point)<=.15f;
        }
    }
}
