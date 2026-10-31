using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.CityExpansion;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    public static class FriendFacilities
    {
        public static bool IsEntrance(CityCinemaPortal portal)
            => portal && portal.destination && portal.destination.position.y > portal.transform.position.y + 1;

        public static bool SellTicket(CityCinemaPortal portal, string personId, string billId)
        {
            var economy = FriendsEconomyService.Instance; if (!portal || !economy) return false;
            bool Sell()
            {
                if (!economy.QuoteAndReserve(personId, billId, "Cinema", 2000) || !economy.DeliverBill(billId))
                { economy.CancelBill(billId); return false; }
                return true;
            }
            var station = portal.GetComponent<FriendJobStation>(); return station ? station.ExecuteService(Sell) : Sell();
        }

        public static bool AdmitFriend(CityCinemaPortal portal, string personId, string billId)
        {
            var runtime = FriendsLifeRuntime.Instance; var actor = runtime ? runtime.ActorOf(personId) : null;
            var economy = FriendsEconomyService.Instance;
            if (!portal || !portal.destination || !runtime || !runtime.IsAuthority || !actor || actor.Brain == null || !economy) return false;
            bool Transfer()
            {
                bool charge = IsEntrance(portal);
                if (charge && !economy.QuoteAndReserve(personId, billId, "Cinema", 2000)) return false;
                var previousPosition = actor.transform.position; var previousRotation = actor.transform.rotation;
                actor.Brain.Movement.Stop();
                if (!actor.Brain.Movement.Warp(portal.destination.position, portal.destination.rotation))
                { if (charge) economy.ReleaseUnstartedBill(billId); return false; }
                if (charge && !economy.DeliverBill(billId))
                { actor.Brain.Movement.Warp(previousPosition, previousRotation); economy.ReleaseUnstartedBill(billId); return false; }
                return true;
            }
            using (economy.DeferExternalCommit())
            { var station = portal.GetComponent<FriendJobStation>(); return station ? station.ExecuteService(Transfer) : Transfer(); }
        }

        public static bool TryStartFriendVisit(TownMealScheduler scheduler, ResidentController actor, string billId, int sourceStayMinutes)
        {
            var economy = FriendsEconomyService.Instance;
            if (!scheduler || scheduler.SushiOpen || !actor || actor.Brain == null || !actor.IsServer || actor.Runtime.IsInteracting ||
                actor.Brain.StateMachine.Current is ResidentTownMealState || !economy || economy.HasActiveJob(economy.PersonId(actor))) return false;
            var seat = scheduler.seats?.Where(s => s && s.IsFree && s.approach).OrderBy(s => s.seatId).FirstOrDefault();
            if (!seat) return false;
            var agent = actor.GetComponent<NavMeshAgent>(); var path = new NavMeshPath();
            if (!agent || !agent.enabled || !agent.isOnNavMesh || !agent.CalculatePath(seat.approach.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            var privacy = FriendHousingIsland.Instance ? FriendHousingIsland.Instance.GetComponent<FriendHousingPrivacy>() : null;
            if (privacy && privacy.ExcludesPathFor(actor, path)) return false;
            if (!economy.QuoteTownMealAndReserve(economy.PersonId(actor), billId, "Sushi")) return false;
            if (!seat.TryReserve(actor)) { economy.ReleaseUnstartedBill(billId); return false; }
            // Reuse the original walking, seating, animation, interruption and
            // exit state. Payment is an additive observer around its public
            // Seated/Seat state, so that original implementation stays intact.
            if(sourceStayMinutes<=0)
            {
                // R VisitActivityFacility has no duration field. Its host meal
                // uses the existing town service's own planned stay, rather
                // than inventing a new default life timer in this adapter.
                int window=scheduler.Minute<TownMealScheduler.PlannedMinute(scheduler.CurrentDay,1)?0:1;
                TownMealScheduler.PlannedEvent(scheduler.CurrentDay,window,out _,out sourceStayMinutes);
            }
            var state = new ResidentTownMealState(actor, seat, scheduler, scheduler.CurrentDay, sourceStayMinutes);
            if (!actor.Brain.StateMachine.ChangeState(state, ResidentRequestPriority.ScheduledActivity))
            { seat.Release(actor); economy.ReleaseUnstartedBill(billId); return false; }
            FriendTownMealBillingObserver.Bind(actor, state, billId);
            // Record this actual explicit visit through the original public
            // ledger API, preventing a second autonomous booking that day.
            var ledger = JsonUtility.FromJson<TownMealScheduler.Ledger>(TownMealScheduler.CaptureLedger());
            if (ledger != null) { if (ledger.visitors == null) ledger.visitors = new List<byte>(); if (!ledger.visitors.Contains(actor.ResidentId)) ledger.visitors.Add(actor.ResidentId); TownMealScheduler.RestoreLedger(JsonUtility.ToJson(ledger)); }
            return true;
        }
    }

    // Runs after original TownMealScheduler(-40) and before ordinary resident
    // ticks. Autonomous parties retain their original trial, candidates, seats,
    // random choices, ledger and state; this component owns only friend bills.
    [DefaultExecutionOrder(-20)]
    public sealed class FriendTownMealBillingObserver : MonoBehaviour
    {
        sealed class Visit { public ResidentController actor; public ResidentTownMealState state; public string billId; public bool delivered; }
        static readonly Dictionary<int, Visit> visits = new Dictionary<int, Visit>();
        public static void Bind(ResidentController actor, ResidentTownMealState state, string billId)
        { visits[actor.GetInstanceID()] = new Visit { actor = actor, state = state, billId = billId }; }
        void Update()
        {
            var runtime = FriendsLifeRuntime.Instance; var economy = FriendsEconomyService.Instance; var scheduler = TownMealScheduler.Instance;
            if (!runtime || !runtime.Ready || !runtime.IsAuthority || !runtime.IsWorldRunning || !economy || !scheduler) return;
            foreach (var person in runtime.People.Where(p => p.controllerKind == PersonControllerKind.Npc))
            {
                var actor = runtime.ActorOf(person.personId);
                if (!actor || actor.Brain == null || !(actor.Brain.StateMachine.Current is ResidentTownMealState state)) continue;
                int id = actor.GetInstanceID();
                if (!visits.TryGetValue(id, out var visit) || visit.state != state)
                {
                    string bill = "town-meal/" + scheduler.CurrentDay + "/" + person.personId;
                    if (!economy.QuoteTownMealAndReserve(person.personId, bill, "Sushi")) { actor.Brain.RequestScheduleChange(); continue; }
                    Bind(actor, state, bill); visit = visits[id];
                }
                if (visit.delivered || !state.Seat || !state.Seat.approach) continue;
                if (!state.Seated || !state.Seat.seatedPose || Vector3.Distance(actor.transform.position,state.Seat.seatedPose.position)>.15f) continue;
                bool Deliver() => economy.DeliverBill(visit.billId);
                var station = scheduler.GetComponent<FriendJobStation>();
                if (!(station ? station.ExecuteService(Deliver) : Deliver())) { actor.Brain.RequestScheduleChange(); continue; }
                visit.delivered = true; runtime.NotifyChanged();
            }
            foreach (var pair in visits.ToArray())
            {
                if (pair.Value.actor && pair.Value.actor.Brain != null && ReferenceEquals(pair.Value.actor.Brain.StateMachine.Current, pair.Value.state)) continue;
                if (!pair.Value.delivered) economy.CancelBill(pair.Value.billId);
                visits.Remove(pair.Key);
            }
            // The original scheduler does not persist its live walking state.
            // A loaded autonomous visitor must therefore release an undelivered
            // reservation, while retaining the original attempted-day ledger;
            // it must not create a replay or a second quote after loading.
            foreach (var bill in economy.State.bills.Where(b => !b.delivered && !b.cancelled &&
                b.billId != null && b.billId.StartsWith("town-meal/", StringComparison.Ordinal)).ToArray())
                if (!visits.Values.Any(v => v.billId == bill.billId)) economy.CancelBill(bill.billId);
        }
        void OnDestroy() { visits.Clear(); }
    }
}
