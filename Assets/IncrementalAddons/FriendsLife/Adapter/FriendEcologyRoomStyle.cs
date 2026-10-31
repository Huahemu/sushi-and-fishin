using System;
using System.Linq;
using TomodachiLife.Simulation;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    [Serializable] public sealed class EcologyRoomStylePlan
    {
        public string ruleLayer = "RCompatibleSource", worldId, styleId, sourceReference;
        public bool sourceCommitted;
        public long sourceRevision;
    }

    public sealed partial class FriendsLifeRuntime
    {
        private void QueueEcologyRoomStyle(LifeDailyRequest request, PersonBinding person, string styleId)
        {
            if (State.episodes.Any(e => e.sourceKind == "RoomStyleInput" && e.sourceId == request.id)) return;
            var definition = Core.CreativeCatalog.Room(styleId);
            if (definition == null) return;
            var plan = new EcologyRoomStylePlan { worldId = State.worldId, styleId = styleId, sourceReference = definition.sourceReference };
            State.episodes.Add(new SourceEpisode { episodeId = "room-style/" + request.id, sourceKind = "RoomStyleInput", sourceId = request.id,
                eventName = request.sourceId, focusPersonId = person.personId, sourceMinute = request.createdMinutes,
                opId = "daily/" + request.id, optionId = JsonUtility.ToJson(plan) });
            NotifyChanged(); SaveNow();
        }

        private void AdvanceEcologyRoomStyles()
        {
            var homes = FriendsHomeInteriors.Instance;
            var actions = FriendsHomeWorldActions.Instance;
            if (!homes || !actions) return;
            foreach (var episode in State.episodes.Where(e => !e.committed && e.sourceKind == "RoomStyleInput").ToArray())
            {
                var person = GetPerson(episode.focusPersonId);
                var plan = JsonUtility.FromJson<EcologyRoomStylePlan>(episode.optionId ?? "{}");
                if (person?.controllerKind != PersonControllerKind.Npc || plan?.worldId != State.worldId)
                { episode.status = "BindingFault"; episode.fault = "Original room input has lost its actual person/world owner."; continue; }
                var request = Core.State.daily?.requests.FirstOrDefault(r => r.id == episode.sourceId);
                var definition = Core.CreativeCatalog.Room(plan.styleId);
                var prior = State.receipts.FirstOrDefault(r => r.opId == episode.opId && r.success);
                if (prior != null && !plan.sourceCommitted)
                {
                    // Recover a committed source input, including a save between
                    // the original receipt and its host projection checkpoint.
                    plan.sourceCommitted = true; plan.sourceRevision = prior.revision;
                    episode.optionId = JsonUtility.ToJson(plan);
                }
                if (definition == null || !plan.sourceCommitted && (request == null || !Core.KernelDailyActionMatches(request, plan.styleId, request.targetId, "RoomStyle")))
                { episode.status = "BindingFault"; episode.fault = "Original room item or daily request match is unavailable."; continue; }
                if (!plan.sourceCommitted && request.status != "Pending")
                {
                    actions.RequestOwnerExit(episode.episodeId);
                    if (actions.OwnerAtSafeBoundary(episode.episodeId)) CompleteEpisode(episode);
                    continue;
                }
                if (!PrepareEcologyWorkBoundary(episode) || !TryAcquireRoomStyleLease(episode)) continue;
                if (!plan.sourceCommitted)
                {
                    // This is actual owned-home/material readiness, not a timer
                    // or a successful source rule result fabricated by the host.
                    if (!homes.CanApplySourceRoomStyle(person.personId, plan.styleId, episode.episodeId)) continue;
                    var receipt = ExecuteKernel(episode.opId, person.personId,
                        scope => scope.BuyCreativeRoom(person.nativeResidentId, plan.styleId).success, episode.episodeId);
                    if (!receipt.success)
                    {
                        episode.status = "Queued"; episode.fault = receipt.message;
                        // No physical action was entered by this input. Release
                        // its idle lease so a real budget job can proceed.
                        actions.RequestOwnerExit(episode.episodeId);
                        if (actions.OwnerAtSafeBoundary(episode.episodeId)) ReleaseEpisodeLeases(episode.episodeId);
                        bool owned = Core.State.creative?.residents.FirstOrDefault(r => r.residentId == person.nativeResidentId)?.ownedRoomStyles.Contains(plan.styleId) == true;
                        HasSourceBudget(person, episode.sourceId, owned ? 0 : definition.price);
                        continue;
                    }
                    plan.sourceCommitted = true; plan.sourceRevision = receipt.revision;
                    episode.optionId = JsonUtility.ToJson(plan); episode.status = "AwaitHomeProjection"; episode.fault = "";
                    NotifyChanged(); SaveNow();
                }
                if (!homes.IsSourceRoomStyleProjected(person.personId, plan.styleId, episode.episodeId)) continue;
                actions.RequestOwnerExit(episode.episodeId);
                if (!actions.OwnerAtSafeBoundary(episode.episodeId)) continue;
                CompleteEpisode(episode);
            }
        }

        private bool TryAcquireRoomStyleLease(SourceEpisode episode)
        {
            var actor = ActorOf(episode.focusPersonId);
            if (actor?.Brain == null || actor.Runtime == null) return false;
            if (FriendsEconomyService.Instance?.HasActiveJob(episode.focusPersonId) == true) return false;
            var own = State.leases.FirstOrDefault(l => l.personId == episode.focusPersonId && l.ownerEpisode == episode.episodeId);
            if (own != null) { if (!actor.Runtime.IsSnapshotApplying) actor.Brain.Pause(); return true; }
            if (!IsWorldAvailable(episode.focusPersonId) || actor.Runtime.IsSnapshotApplying || actor.Runtime.IsSwimming ||
                actor.Runtime.HasRestaurantAssignment || FriendsHomeWorldActions.Controls(episode.focusPersonId) ||
                actor.Brain.StateMachine.Current?.CanInterrupt(PaperPixelGames.Core.Ai.NPCs.ResidentRequestPriority.PlayerInteraction) != true) return false;
            actor.Brain.Pause();
            State.leases.Add(new ActivityLease { leaseId = episode.episodeId + "/input", personId = episode.focusPersonId,
                ownerEpisode = episode.episodeId, facilityId = "OwnRoomStyle", phase = "SourceRoomInput", safeBoundary = true,
                destination = actor.transform.position, resumeToken = actor.Runtime.CurrentActivity.ToString() });
            return true;
        }
    }
}
