using System;
using System.Linq;
using TomodachiLife.Simulation;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendsLifeRuntime
    {
        private static bool UsesFamilyHomeStage(SourceEpisode episode)
            => episode.familyChoice?.action == "Care" || episode.familyChoice?.action == "Grow";

        private void AdvanceFamilyHomeEpisode(SourceEpisode episode)
        {
            var navigation = FriendsHomeStandingNavigation.Instance;
            var economy = FriendsEconomyService.Instance;
            var q = episode.familyChoice;
            if (!navigation || !economy || q == null) return;
            if (State.receipts.Any(r => r.opId == episode.opId && r.success))
            {
                episode.committed = true; episode.status = "HomeSourceRelease";
                navigation.RequestOwnerExit(episode.episodeId); NotifyChanged(); return;
            }
            var child = GetPerson(episode.targetPersonId);
            if (child == null || string.IsNullOrEmpty(child.currentHomeId)) return;
            var home = economy.Plot(child.currentHomeId)?.house;
            if (!home || !home.entry) return;
            bool human = HasHumanParticipant(episode);
            if (human)
            {
                if (!episode.inputAccepted)
                {
                    if (episode.status != "AwaitHuman") { episode.status = "AwaitHuman"; PendingInputRequested?.Invoke(episode); NotifyChanged(); }
                    return;
                }
                if (!HumanParticipantsReadyForStage(episode)) return;
                // A real human walks under their existing controller. A remote
                // clicked choice does not teleport them or reserve a waiting child.
                foreach (string id in new[] { episode.focusPersonId, episode.targetPersonId }.Where(id => GetPerson(id)?.controllerKind == PersonControllerKind.Human))
                {
                    var body = TransformOf(id);
                    if (!body || Vector3.Distance(body.position, home.entry.position) > 3f) return;
                }
            }
            else if (OriginalAutoInput == null && CompatibleFamilyInput(episode) == null) return;
            if (episode.status == "SourceRejected")
            {
                string current = FamilyInputContext(episode);
                if (episode.rejectedContext == current) return;
            }
            if (!PrepareEcologyWorkBoundary(episode) || !TryAcquirePairLease(episode)) return;
            int slot = 0;
            bool allArrived = true;
            bool navigationFailed = false;
            foreach (string id in new[] { episode.focusPersonId, episode.targetPersonId })
            {
                if (GetPerson(id)?.controllerKind != PersonControllerKind.Npc) { slot++; continue; }
                string key = episode.episodeId + "/HomeStanding/" + id + "/" + episode.inputCursor;
                if (!navigation.TryBegin(id, key, episode.episodeId, child.currentHomeId, slot++)) { allArrived = false; continue; }
                var actual = navigation.Observe(key);
                if (actual == null || actual.phase != "Arrived" || !actual.arrived)
                {
                    if (actual?.phase == "Failed") { navigationFailed = true; episode.fault = actual.reason; }
                    else if (!navigationFailed) episode.status = "HomeTravel";
                    allArrived = false;
                }
            }
            if (navigationFailed)
            {
                episode.status = "BindingFault"; navigation.RequestOwnerExit(episode.episodeId);
                if (navigation.OwnerAtSafeBoundary(episode.episodeId)) ReleaseEpisodeLeases(episode.episodeId);
                NotifyChanged(); SaveNow(); return;
            }
            if (!allArrived) return;
            var focusBody = TransformOf(episode.focusPersonId); var targetBody = TransformOf(episode.targetPersonId);
            if (!focusBody || !targetBody || Vector3.Distance(focusBody.position, targetBody.position) > 3f) return;
            string option = human || episode.inputAccepted ? episode.optionId : OriginalAutoInput?.Invoke(episode) ?? CompatibleFamilyInput(episode);
            if (option == null || !q.options.Any(o => o.id == option && o.enabled)) return;
            if (!human && !episode.inputAccepted)
            {
                episode.optionId = option; episode.inputOwnerPersonId = episode.focusPersonId; episode.inputAccepted = true;
                NotifyChanged(); SaveNow();
            }
            foreach (string id in new[] { episode.focusPersonId, episode.targetPersonId }.Where(id => GetPerson(id)?.controllerKind == PersonControllerKind.Npc))
            {
                var actor = ActorOf(id); var other = TransformOf(id == episode.focusPersonId ? episode.targetPersonId : episode.focusPersonId);
                if (actor?.Brain == null || !other) continue;
                var direction = Vector3.ProjectOnPlane(other.position - actor.transform.position, Vector3.up);
                if (direction.sqrMagnitude > .001f) actor.Brain.Movement.StopAtRotation(Quaternion.LookRotation(direction));
            }
            string payer = human ? episode.inputOwnerPersonId : episode.focusPersonId;
            var receipt = ExecuteKernel(episode.opId, payer, scope =>
            {
                if (!scope.EnsureFamilyState().choices.Any(choice => choice.id == q.id)) scope.State.family.choices.Add(q);
                return scope.ResolveFamilyChoice(episode.sourceId, option, episode.inputChildName).success;
            }, episode.episodeId);
            if (!receipt.success)
            {
                episode.status = "SourceRejected"; episode.fault = receipt.message; episode.rejectedContext = FamilyInputContext(episode);
                navigation.RequestOwnerExit(episode.episodeId);
                if (navigation.OwnerAtSafeBoundary(episode.episodeId)) ReleaseEpisodeLeases(episode.episodeId);
                episode.inputCursor++; NotifyChanged(); SaveNow(); return;
            }
            episode.optionId = option; episode.worldArrivalVerified = true;
            episode.focusCommitPosition = focusBody.position; episode.targetCommitPosition = targetBody.position;
            episode.participantDistanceAtCommit = Vector3.Distance(focusBody.position, targetBody.position);
            episode.committed = true; episode.status = "HomeSourceRelease"; episode.fault = "";
            SynchronizeSourceChildren(); navigation.RequestOwnerExit(episode.episodeId);
            NotifyChanged(); SaveNow();
        }

        private void FinishFamilyHomeEpisodes()
        {
            var navigation = FriendsHomeStandingNavigation.Instance;
            if (!navigation) return;
            foreach (var episode in State.episodes.Where(e => e.committed && e.sourceKind == "Family" && e.status == "HomeSourceRelease").ToArray())
            {
                navigation.RequestOwnerExit(episode.episodeId);
                if (navigation.OwnerAtSafeBoundary(episode.episodeId)) CompleteEpisode(episode);
            }
        }

        public bool RetryFamilyHomeAfterBindingChange(string episodeId)
        {
            if (!Ready || !IsAuthority) return false;
            var episode = State.episodes.FirstOrDefault(e => e.episodeId == episodeId && e.sourceKind == "Family" && !e.committed && UsesFamilyHomeStage(e));
            var navigation = FriendsHomeStandingNavigation.Instance;
            if (episode == null || !navigation || episode.status != "BindingFault" || State.receipts.Any(r => r.opId == episode.opId && r.success)) return false;
            navigation.RequestOwnerExit(episodeId);
            if (!navigation.OwnerAtSafeBoundary(episodeId)) return false;
            ReleaseEpisodeLeases(episodeId);
            episode.inputCursor++; episode.status = "Queued"; episode.fault = "";
            NotifyChanged(); SaveNow(); return true;
        }

        // R-compatible follow-up of the source child's growth prerequisite:
        // supply only its still-missing original care count, at the original
        // Care qualification/cooldown, then wait for original readyToGrow.
        // It creates no pregnancy, new rate, threshold, time or extra reward.
        private void AdvanceOriginalChildGrowthInputs()
        {
            var family = Core.State.family;
            if (family?.configuration?.autonomousSuggestions != true || !FriendsHomeStandingNavigation.Instance) return;
            foreach (var sourceChild in family.children.Where(c => c.stage != "Adult").ToArray())
            {
                if (!sourceChild.readyToGrow && sourceChild.careActions - sourceChild.growthCareBaseline >= family.configuration.careActionsPerGrowth) continue;
                var child = PersonFromNative(sourceChild.residentId);
                if (child?.controllerKind != PersonControllerKind.Npc || string.IsNullOrEmpty(child.currentHomeId) || ActorOf(child.personId) == null) continue;
                if (State.episodes.Any(e => !e.committed && e.sourceKind == "Family" && UsesFamilyHomeStage(e) && e.targetPersonId == child.personId && !HasHumanParticipant(e))) continue;
                string action = sourceChild.readyToGrow ? "Grow" : "Care";
                foreach (string parentId in new[] { sourceChild.parentAId, sourceChild.parentBId }.Where(id => !string.IsNullOrEmpty(id)).Distinct())
                {
                    var parent = PersonFromNative(parentId);
                    if (parent?.controllerKind != PersonControllerKind.Npc || ActorOf(parent.personId) == null ||
                        Core.KernelFamilyActionReason(parentId, sourceChild.residentId, action) != null) continue;
                    string originalStage = sourceChild.stage + "/" + sourceChild.lastCareGameMinutes.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                    var receipt = ExecuteKernel("source-child-input/" + sourceChild.residentId + "/" + action + "/" + originalStage,
                        parent.personId, scope => StartExistingFamilySource(scope, parentId, sourceChild.residentId, action).success, sourceChild.residentId);
                    if (receipt.success) { CaptureSourceMailbox(); break; }
                }
            }
        }
    }
}
