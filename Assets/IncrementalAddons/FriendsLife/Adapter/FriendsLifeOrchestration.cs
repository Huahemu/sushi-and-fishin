using System;
using System.Linq;
using TomodachiLife.Simulation;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendsLifeRuntime
    {
        public Func<SourceEpisode, string> OriginalAutoInput;
        public Func<LifeMinigameSession, MinigameSourceInput> NativeMinigameInput;
        public Func<string, string, string, string, string> RegisteredPhotoRecordCommand;
        public Func<string, string, string, string, string> RegisteredPhotoCaptureCommand;
        public event Action<string, string> BudgetNeeded;
        public event Action<string, string> ActivityPresentationRequested;
        private float nextAutomaticAction;
        private float minigameSeconds;
        [Serializable] public sealed class MinigameSourceInput
        {
            public string action;
            public int option;
            public float value, secondary;
        }
        [Serializable] public sealed class ProfileFieldInput { public int ageYears; public string gender, preference; }
        [Serializable] public sealed class DesignPaintInput { public int x0, y0, x1, y1, rgba, radius; }
        [Serializable] public sealed class DesignStampInput { public string stamp; public int x, y, rgba; }
        [Serializable] public sealed class DesignTextInput { public string title, text; }
        [Serializable] public sealed class DesignMoveInput { public int dx, dz; public bool rotate; }
        [Serializable] public sealed class CultureShareInput { public string targetPersonId; }
        [Serializable] public sealed class FamilyChoiceInput { public string optionId, childName; }
        [Serializable] public sealed class FountainWishInput { public string wish; }
        [Serializable] public sealed class AccidentChoiceInput { public int choice; }
        [Serializable] public sealed class HoneymoonInput { public string destinationId; public bool useTicket = true; }
        [Serializable] public sealed class TravelInput { public bool useTicket = true; public string partnerPersonId; }

        private void CaptureSourceMailbox()
        {
            var game=Core.State.activities?.minigame;
            bool closedInputs=false;
            foreach(var input in State.episodes.Where(e=>e.sourceKind=="MinigameInput"&&!e.committed&&(game==null||game.completed||e.sourceId!=game.id)))
            {
                // Close only the durable human-input handle. The source session
                // already owns its result/reward; never replay settlement here.
                input.committed=true;input.status="Done";input.fault="";closedInputs=true;
            }
            if(closedInputs)NotifyChanged();
            foreach (var pending in Core.State.pendingEvents.ToArray())
            {
                if (State.episodes.All(e => e.sourceKind != "MiiEvent" || e.sourceId != pending.id))
                {
                    var native = Core.NativeEventEngine.FindEvent(pending.eventName);
                    State.episodes.Add(new SourceEpisode { episodeId = "native/" + pending.id, sourceKind = "MiiEvent", sourceId = pending.id,
                        eventName = pending.eventName, branchIndex = pending.nativeSelectedBranch, nativePending = pending, sourceMinute = pending.createdGameMinutes,
                        focusPersonId = PersonFromNative(pending.focusResidentId)?.personId, targetPersonId = PersonFromNative(pending.targetResidentId)?.personId,
                        anotherPersonId = PersonFromNative(pending.anotherResidentId)?.personId, demoParam = native?.DemoParamId.ToString(), opId = "native-result/" + pending.id });
                }
                Core.State.pendingEvents.Remove(pending);
            }
            if (Core.State.family == null) return;
            foreach (var choice in Core.State.family.choices.ToArray())
            {
                bool duplicate = State.episodes.Any(e => !e.committed && e.sourceKind == "Family" && e.familyChoice != null &&
                    e.familyChoice.focusId == choice.focusId && e.familyChoice.targetId == choice.targetId && e.familyChoice.action == choice.action);
                if (!duplicate && State.episodes.All(e => e.sourceKind != "Family" || e.sourceId != choice.id))
                    State.episodes.Add(new SourceEpisode { episodeId = "family/" + choice.id, sourceKind = "Family", sourceId = choice.id,
                        sourceMinute = choice.createdGameMinutes, familyChoice = choice, opId = "family-result/" + choice.id,
                        focusPersonId = PersonFromNative(choice.focusId)?.personId, targetPersonId = PersonFromNative(choice.targetId)?.personId });
                Core.State.family.choices.Remove(choice);
            }
        }
        private void AdvanceOrchestration()
        {
            BindRelationPorts();
            ObserveRelationMeetings();
            CaptureSourceMailbox();
            SynchronizeSourceChildren();
            AdvanceWorldLeases();
            AdvanceSocialReturns();
            FinishFamilyHomeEpisodes();
            foreach (var episode in State.episodes.Where(e => !e.committed).ToArray())
            {
                if (episode.sourceKind == "Family" && FamilyEpisodeExpired(episode)) { CancelFamilyEpisode(episode); continue; }
                if (episode.status == "BindingFault") continue;
                if (string.IsNullOrEmpty(episode.focusPersonId) || GetPerson(episode.focusPersonId) == null)
                { episode.status = "Suspended"; episode.fault = "Source participant is not bound to this world."; continue; }
                if (episode.sourceKind == "Family") AdvanceFamilyEpisode(episode);
                else if (episode.sourceKind == "MiiEvent") AdvanceNativeEpisode(episode);
            }
            AdvanceEcologyFood();
            AdvanceEcologyRoomStyles();
            AdvanceEcologyCreativeActivities();
            if (Time.unscaledTime < nextAutomaticAction) return;
            nextAutomaticAction = Time.unscaledTime + 2f;
            AdvanceDailyRequests();
            AdvanceAutomaticActivities();
            if (FriendRelationFlags.FamilyHousing) AdvanceFamilyHousing();
            if (FriendRelationFlags.Children) AdvanceOriginalChildGrowthInputs();
        }
        private bool HasHumanParticipant(SourceEpisode episode)
        {
            return new[] { episode.focusPersonId, episode.targetPersonId, episode.anotherPersonId }
                .Any(id => GetPerson(id)?.controllerKind == PersonControllerKind.Human);
        }
        private void AdvanceNativeEpisode(SourceEpisode episode)
        {
            if (episode.nativePending?.usesReconstructedEligibility == true)
            {
                UpdateEpisodeStatus(episode.episodeId, "BindingFault", "Mandatory original native context input is unresolved: " + episode.nativePending.nativeEligibilitySupport + "; " + episode.nativePending.nativeBranchSupport);
                return;
            }
            if (episode.nativePending == null || episode.branchIndex < 0)
            { UpdateEpisodeStatus(episode.episodeId, "BindingFault", "Native selected branch or pending record is unavailable."); return; }
            if (episode.status == "Queued" || episode.status == "Suspended")
            {
                if (!TryAcquirePairLease(episode)) return;
                episode.status = "Travel";
                MoveEpisodeParticipantsTogether(episode);
            }
            if (episode.status != "Travel" || !EpisodeParticipantsArrived(episode)) return;
            episode.status = "SourceRunning";
            episode.presentationRequested = true;
            if (SourceEpisodeRequested == null)
            { UpdateEpisodeStatus(episode.episodeId, "BindingFault", "Original graph result provider is not installed."); ReleaseEpisodeLeases(episode.episodeId); return; }
            SourceEpisodeRequested.Invoke(episode);
        }
        public void UpdateEpisodeStatus(string episodeId, string status, string reason = null)
        {
            var episode = State?.episodes.FirstOrDefault(e => e.episodeId == episodeId);
            if (episode == null || episode.committed) return;
            episode.status = status; episode.fault = reason ?? "";
            if (status == "BindingFault") ReleaseEpisodeLeases(episodeId);
            NotifyChanged();
        }
        public void ParkBindingFault(string episodeId, string reason) => UpdateEpisodeStatus(episodeId, "BindingFault", reason);
        public bool RetryNativeEpisode(string episodeId)
            => RecoverNativeEpisodeAfterBindingChange(episodeId);
        public FriendsKernelReceipt CompleteNativeEpisode(string episodeId, string demoResult)
        {
            var episode = State?.episodes.FirstOrDefault(e => e.episodeId == episodeId && e.sourceKind == "MiiEvent");
            if (episode == null) return new FriendsKernelReceipt { success = false, message = "这件事已经结束了。" };
            if (episode.committed) return State.receipts.FirstOrDefault(r => r.opId == episode.opId);
            var rule = Core.Rules.FindEvent(episode.eventName);
            var branch = rule?.branches.FirstOrDefault(b => b.index == episode.branchIndex);
            var matches = branch?.results.Where(r => r.demoResult == demoResult).ToArray();
            if (matches == null || matches.Length != 1)
            {
                UpdateEpisodeStatus(episodeId, "BindingFault", "DemoResult does not uniquely match the selected original branch: " + demoResult);
                return new FriendsKernelReceipt { success = false, message = episode.fault };
            }
            episode.demoResult = demoResult;
            episode.status = "CommitQueued";
            var sourceResult = matches[0];
            string[] owners = { episode.focusPersonId, episode.targetPersonId, episode.anotherPersonId };
            int[] amounts = { sourceResult.rewardMoney0, sourceResult.rewardMoney1, sourceResult.rewardMoney2 };
            var allocations = new System.Collections.Generic.List<ActorWalletDelta>();
            var projectedBalances = new System.Collections.Generic.Dictionary<string, int>();
            if (!sourceResult.skipResult)
                for (int slot = 0; slot < owners.Length; slot++)
                {
                    if (amounts[slot] == 0) continue;
                    if (GetPerson(owners[slot]) == null)
                    { UpdateEpisodeStatus(episodeId, "BindingFault", "Source reward slot has no actual owner: " + slot); return new FriendsKernelReceipt { success = false, message = episode.fault }; }
                    int projected;
                    if (!projectedBalances.TryGetValue(owners[slot], out projected)) projected = (int)Math.Min(99999999L, Math.Max(0, ReadWalletCents?.Invoke(owners[slot]) ?? 0));
                    int after = NativeLifeRulesCalculations.PlanWalletDelta(projected, amounts[slot]).balance;
                    long delta = (long)after - projected; projectedBalances[owners[slot]] = after;
                    allocations.Add(new ActorWalletDelta { personId = owners[slot], deltaCents = delta, sourceSlot = slot, sourceAmountRaw = amounts[slot],
                        provenance = "Original branch RewardMoney slot " + slot + "; original PlanWalletDelta 01d6d404; participant-owned projection" });
                }
            var receipt = ExecuteKernel(episode.opId, episode.focusPersonId, scope =>
            {
                scope.State.pendingEvents.Add(episode.nativePending);
                return scope.ChooseBranch(episode.sourceId, episode.branchIndex, matches[0].index).success;
            }, episode.episodeId, true, allocations);
            if (!receipt.success) { UpdateEpisodeStatus(episodeId, "SourceRejected", receipt.message); return receipt; }
            var native = Core.NativeEventEngine.FindEvent(episode.eventName);
            State.nativeHistory.Add(new NativeCommittedHistory { episodeId = episodeId, eventName = episode.eventName, eventId = native?.id ?? 0,
                focusNativeId = episode.nativePending.focusResidentId, targetNativeId = episode.nativePending.targetResidentId, gameMinute = Core.State.gameMinutes });
            CompleteEpisode(episode);
            return receipt;
        }
        private LifeSimulationResolution StartExistingFamilySource(LifeSimulationRuntime scope, string focusNativeId, string targetNativeId, string action)
        {
            // Preserve the exact original F3 key after host mailbox extraction.
            // Original Start still performs its own F2 qualification before
            // returning its existing choice; no guard/result is bypassed.
            var pending = State.episodes.FirstOrDefault(e => !e.committed && e.sourceKind == "Family" && e.familyChoice != null &&
                e.familyChoice.focusId == focusNativeId && e.familyChoice.targetId == targetNativeId && e.familyChoice.action == action)?.familyChoice;
            if (pending != null)
            {
                var family = scope.EnsureFamilyState();
                if (!family.choices.Any(q => q.id == pending.id)) family.choices.Add(pending);
            }
            return scope.StartFamilyAction(focusNativeId, targetNativeId, action);
        }
        private void AdvanceFamilyEpisode(SourceEpisode episode)
        {
            var choice = episode.familyChoice;
            if (choice == null) { episode.status = "BindingFault"; episode.fault = "Original family choice is absent."; return; }
            if (UsesFamilyHomeStage(episode)) { AdvanceFamilyHomeEpisode(episode); return; }
            // Hidden kinds never start on their own; the row is kept as Cancelled.
            if (!FriendRelationFlags.AutoAllowed(choice.action) && GetPerson(episode.focusPersonId)?.controllerKind != PersonControllerKind.Human)
            { CancelFamilyEpisode(episode); return; }
            if (episode.status == "SourceRejected")
            {
                ReleaseEpisodeLeases(episode.episodeId);
                string context = FamilyInputContext(episode);
                if (string.IsNullOrEmpty(episode.rejectedContext)) { episode.rejectedContext = context; return; }
                if (episode.rejectedContext == context) return;
                episode.status = "Queued";
            }
            if (HasHumanParticipant(episode))
            {
                if (!episode.inputAccepted)
                {
                    WaitForHumanSocialInput(episode);
                    return;
                }
                if (FriendWorldTalkAuthority.HasNpcApproachBody(this,episode.episodeId)) return;
                if (!SocialStageMayAttempt(episode)) return;
                if (!HumanParticipantsReadyForStage(episode)) return;
                bool refreshStage = HumanStageNeedsRefresh(episode);
                if (!PrepareEcologyWorkBoundary(episode) || !TryAcquirePairLease(episode)) return;
                if (episode.status != "Travel" || refreshStage) { episode.status = "Travel"; MoveEpisodeParticipantsTogether(episode); RememberSocialStageFailure(episode); }
                if (episode.status != "Travel" || !EpisodeParticipantsArrived(episode)) return;
                ResolveFamilyEpisode(episode, episode.optionId, episode.inputChildName, episode.inputOwnerPersonId, true);
                return;
            }
            if (OriginalAutoInput == null && CompatibleFamilyInput(episode) == null)
            { episode.status = "AwaitSourceInput"; return; }
            if (!SocialStageMayAttempt(episode)) return;
            if (!PrepareEcologyWorkBoundary(episode) || !TryAcquirePairLease(episode)) return;
            if (episode.status == "Queued" || episode.status == "Suspended" || episode.status == "AwaitSourceInput")
            { episode.status = "Travel"; MoveEpisodeParticipantsTogether(episode); RememberSocialStageFailure(episode); }
            // A rejected navigation call changes Travel to Suspended. It must
            // never fall through to an original result merely because it is no
            // longer in Travel. Every retry requires real shared-stage facts.
            if ((episode.status != "Travel" && episode.status != "AwaitSourceInput") || !EpisodeParticipantsArrived(episode)) return;
            string option = OriginalAutoInput?.Invoke(episode) ?? CompatibleFamilyInput(episode);
            if (option == null) { episode.status = "AwaitSourceInput"; return; }
            ResolveFamilyEpisode(episode, option);
        }
        private string CompatibleFamilyInput(SourceEpisode episode)
        {
            var q = episode.familyChoice;
            var f = Core.State.family;
            string option;
            switch (q.action)
            {
                case "Befriend": option = "chat"; break;
                case "BestFriends": option = Core.FamilyFriendship(q.focusId, q.targetId) >= LifeSimulationRuntime.FamilyBestFriendMeter ? "promise" : "wait"; break;
                // Low compatibility makes a disagreement turn into a quarrel more often.
                case "Fight": option = FriendRelations.Roll(episode.episodeId) >= Core.FamilyCompatibility(q.focusId, q.targetId) / 2 ? "argue" : "calm"; break;
                case "Reconcile": option = "apologize"; break;
                // A confession is the player's advice problem; it is never answered here.
                case "Confess": return null;
                case "Breakup": option = "talk"; break;
                case "Reunite": option = "friends"; break;
                case "Date": option = "picnic"; break;
                case "Propose":
                    var couple = Core.State.family.couples.LastOrDefault(c => c.status != "Broken" && (c.parentAId == q.focusId && c.parentBId == q.targetId || c.parentAId == q.targetId && c.parentBId == q.focusId));
                    option = couple != null && couple.love >= f.configuration.proposalLove && couple.dates >= f.configuration.minimumDatesForProposal ? "propose" : "wait"; break;
                case "Wedding": option = "simple"; break;
                case "Baby": option = q.options.Any(o => o.id == "pregnancy" && o.enabled) ? "pregnancy" : "adopt"; break;
                case "Birth": option = "welcome"; break;
                case "Care":
                    var options = q.options.Where(o => o.enabled && (o.id == "feed" || o.id == "play" || o.id == "lullaby")).ToArray();
                    var child = f.children.FirstOrDefault(c => c.residentId == q.targetId);
                    option = options.Length == 0 ? null : options[(child?.careActions ?? episode.inputCursor) % options.Length].id; break;
                case "Grow": option = "grow"; break;
                case "Comfort": option = "listen"; break;
                case "Cohabit": option = "suggest"; break;
                default: return null;
            }
            return q.options.Any(o => o.id == option && o.enabled) ? option : null;
        }
        private FriendsKernelReceipt ResolveFamilyEpisode(SourceEpisode episode, string option, string childName = null, string payerPersonId = null, bool resourcesReady = false)
        {
            if (!episode.familyChoice.options.Any(o => o.id == option && o.enabled))
                return new FriendsKernelReceipt { success = false, message = "这个选项现在不能选。" };
            if (HasHumanParticipant(episode) && !resourcesReady)
            {
                episode.optionId = option; episode.inputChildName = childName; episode.inputOwnerPersonId = payerPersonId; episode.inputAccepted = true; episode.status = "HumanInputQueued";
                NotifyChanged(); SaveNow();
                return new FriendsKernelReceipt { success = false, domain = "QueuedSourceInput", causeEpisode = episode.episodeId, message = "已记下你的选择，等待参与者到场。" };
            }
            LifeSimulationResolution originalResolution=null;
            var receipt = ExecuteKernel(episode.opId, payerPersonId ?? episode.focusPersonId, scope =>
            {
                scope.State.family.choices.Add(episode.familyChoice);
                originalResolution=scope.ResolveFamilyChoice(episode.sourceId, option, childName);
                return originalResolution.success;
            }, episode.episodeId);
            if (!receipt.success)
            {
                episode.status = "SourceRejected"; episode.fault = receipt.message;
                episode.rejectedContext = FamilyInputContext(episode);
                ReleaseEpisodeLeases(episode.episodeId); NotifyChanged();
                return receipt;
            }
            episode.optionId = option;
            string originalMessage=originalResolution?.message??Core.State.family.memories.LastOrDefault(m=>m.kind==episode.familyChoice.action&&m.focusId==episode.familyChoice.focusId&&m.targetId==episode.familyChoice.targetId&&m.gameMinutes>=episode.sourceMinute)?.message;
            FriendWorldTalkAuthority.RecordSocialResult(this,episode,originalMessage);
            if (episode.familyChoice.action == "Date" && option != "wait")
            {
                foreach (var lease in State.leases.Where(l => l.ownerEpisode == episode.episodeId))
                { lease.phase = "Date"; lease.untilGameMinute = Core.FindResident(GetPerson(lease.personId)?.nativeResidentId)?.activityUntilGameMinutes ?? Core.State.gameMinutes; }
                episode.status = "SourceActivity";
                episode.committed = true;
            }
            else if (!BeginFamilyWorldPresentation(episode)) CompleteEpisode(episode);
            SynchronizeSourceChildren();
            return receipt;
        }
        private string FamilyInputContext(SourceEpisode episode)
        {
            var ids = new[] { episode.familyChoice.focusId, episode.familyChoice.targetId };
            var family = Core.State.family;
            return string.Join("|", Core.State.relationships.Where(r => ids.Contains(r.fromId) || ids.Contains(r.toId)).Select(r => JsonUtility.ToJson(r))) +
                "|" + string.Join("|", family.couples.Where(c => ids.Contains(c.parentAId) || ids.Contains(c.parentBId)).Select(c => JsonUtility.ToJson(c))) +
                "|" + string.Join("|", family.profiles.Where(p => ids.Contains(p.residentId)).Select(p => JsonUtility.ToJson(p))) +
                "|" + string.Join("|", family.children.Where(c => ids.Contains(c.residentId)).Select(c => JsonUtility.ToJson(c))) +
                "|" + string.Join("|", State.people.Where(p => ids.Contains(p.nativeResidentId)).Select(p => p.currentHomeId)) +
                "|guard=" + (episode.familyChoice.action == "Birth" ? "Birth" : Core.KernelFamilyActionReason(ids[0], ids[1], episode.familyChoice.action) ?? "Ready") +
                "|population=" + Core.State.residents.Count + "/" + configuration.maximumResidents +
                "|payer=" + (ReadWalletCents?.Invoke(episode.inputOwnerPersonId ?? episode.focusPersonId) ?? 0);
        }
        // Nothing waits forever: an unresolved family episode times out to Cancelled.
        private bool FamilyEpisodeExpired(SourceEpisode episode)
            => !UsesFamilyHomeStage(episode) && !FriendWorldTalkAuthority.HasNpcApproachBody(this, episode.episodeId) &&
               Core.State.gameMinutes - episode.sourceMinute >= FriendRelationFlags.EpisodeTimeoutMinutes * (episode.inputAccepted ? 2 : 1);
        private void CancelFamilyEpisode(SourceEpisode episode)
        {
            ReleaseEpisodeLeases(episode.episodeId);
            episode.committed = true; episode.status = "Cancelled"; episode.fault = "";
            NotifyChanged();
        }
        private void CompleteEpisode(SourceEpisode episode)
        {
            episode.committed = true; episode.fault = "";
            ReleaseEpisodeLeases(episode.episodeId);
            episode.status=episode.sourceKind=="Family"&&State.leases.Any(l=>l.ownerEpisode==episode.episodeId)?"Returning":"Done";
            NotifyChanged(); SaveNow();
        }
        private void AdvanceDailyRequests() => AdvanceEcologyDailyRequests();
        private void AdvanceAutomaticActivities()
        {
            var activities = Core.State.activities;
            if (activities == null) return;
            foreach (var trip in activities.travels.Where(t => t.awaitingChoice && !t.returned).ToArray())
            {
                var person = PersonFromNative(trip.residentId);
                if (person?.controllerKind != PersonControllerKind.Npc || PersonFromNative(trip.partnerId)?.controllerKind == PersonControllerKind.Human) continue;
                ExecuteKernel("travel/" + trip.id + "/stage/" + trip.stage, person.personId, scope => scope.ChooseTravelActivity(trip.id, 0).success, trip.id);
            }
            foreach (var dream in Core.State.activities.dreams.Where(d => d.awaitingChoice && !d.woken).ToArray())
            {
                var person = PersonFromNative(dream.residentId);
                if (person?.controllerKind != PersonControllerKind.Npc) continue;
                ExecuteKernel("dream/" + dream.id + "/stage/" + dream.stage, person.personId, scope => scope.ChooseDreamAction(dream.id, 1).success, dream.id);
            }
        }
        private void AdvanceFamilyHousing()
        {
            var economy = FriendsEconomyService.Instance;
            if (economy == null || Core.State.family == null || committing) return;
            foreach (var plan in Core.State.family.housingSuggestions.Where(s => s.status == "Pending" || s.status == "Ready").ToArray())
                economy.TryApplyAutomaticFamilyHousing(plan.id);
        }
        private void TickCurrentMinigame(float seconds)
        {
            var session = Core?.State.activities?.minigame;
            if (session == null || session.completed || seconds <= 0) { minigameSeconds = 0; return; }
            var person = PersonFromNative(session.residentId);
            if (person == null) return;
            string ownerId = State.episodes.FirstOrDefault(e => e.sourceKind == "MinigameInput" && e.sourceId == session.id)?.targetPersonId ?? person.personId;
            minigameSeconds += seconds;
            if (minigameSeconds < .1f) return;
            float delta = minigameSeconds; minigameSeconds = 0;
            string op = "minigame-tick/" + session.id + "/" + State.activeMicros;
            ExecuteKernel(op, ownerId, scope => { scope.TickMinigame(delta); return true; }, session.id, false);
            session = Core.State.activities.minigame;
            if (person.controllerKind != PersonControllerKind.Npc || session == null || session.completed || State.episodes.Any(e => e.sourceKind == "MinigameInput" && e.sourceId == session.id && GetPerson(e.targetPersonId)?.controllerKind == PersonControllerKind.Human)) return;
            // A registered source AI can supply actual inputs; no answer field is read to manufacture a win.
            var input = NativeMinigameInput?.Invoke(session);
            if (input != null) ExecuteKernel("minigame-input/" + session.id + "/" + session.inputs.Count, person.personId,
                scope => scope.SubmitMinigameInput(input.action, input.option, input.value, input.secondary).success, session.id);
        }
        private bool RouteCreativeActivity(LifeSimulationRuntime kernel, string nativeId, string action, System.Collections.Generic.List<SourceEpisode> preparedIntents = null)
        {
            var person = PersonFromNative(nativeId);
            if (person == null) throw new InvalidOperationException("Creative actor has no world binding.");
            // Original IslandLifeGameCreative.UpdateCreativeWorld: a Travel
            // goods request grants one real TravelTicket before its destination input.
            // This callback runs inside the already actor-owned KernelScope.
            if (preparedIntents == null) throw new InvalidOperationException("Creative goods require the actor-owned original scope.");
            if (action == "Travel")
            {
                var stack = kernel.State.inventory.FirstOrDefault(i => i.category == "TravelTicket" && i.itemName == "TravelTicket");
                if (stack == null) { stack = new LifeSimulationInventoryStack { category = "TravelTicket", itemName = "TravelTicket" }; kernel.State.inventory.Add(stack); }
                stack.quantity++;
            }
            var sourceCaller = CaptureCreativeActivity(kernel, person, action);
            // Routing is always bound. A pending actual activity never falls into legacy creative.trips.
            var episode = new SourceEpisode { episodeId = Guid.NewGuid().ToString("N"), sourceKind = "CreativeActivity", sourceId = action,
                focusPersonId = person.personId, sourceMinute = kernel.State.gameMinutes, status = "AwaitSourceInput", optionId = JsonUtility.ToJson(sourceCaller) };
            preparedIntents.Add(episode);
            return true;
        }
        private void RequestFacilityVisit(string nativeId, string facility, System.Collections.Generic.List<SourceEpisode> preparedIntents = null)
        {
            var person = PersonFromNative(nativeId);
            if (person == null) return;
            if (preparedIntents == null) ActivityPresentationRequested?.Invoke(person.personId, "facility:" + facility);
            else preparedIntents.Add(new SourceEpisode { episodeId = Guid.NewGuid().ToString("N"), sourceKind = "FacilityVisit", sourceId = facility,
                focusPersonId = person.personId, sourceMinute = Core.State.gameMinutes, status = "PendingWorld" });
        }
        public string SubmitSourceAction(string personId, string actionId, string optionId = null)
        {
            if (!IsAuthority) return FriendsLifeNetwork.Instance?.RequestAction(personId, actionId, optionId) ?? "正在连接，请稍后再试。";
            var human = LocalHuman();
            if (human == null) return "玩家资料尚未就绪。";
            return SubmitSourceActionAs(human.personId, personId, actionId, optionId);
        }
        public bool CanSubmitHumanAction(string actorPersonId, string targetPersonId, string actionId)
        {
            var actor = GetPerson(actorPersonId); var target = GetPerson(targetPersonId);
            if (actor?.controllerKind != PersonControllerKind.Human || target == null || string.IsNullOrEmpty(actionId)) return false;
            var request = actionId.Split(new[] { ':' }, 2);
            if (!SourceActionBelongsTo(Core, target.nativeResidentId, request[0], request.Length == 2 ? request[1] : "")) return false;
            if (actionId.StartsWith("family:", StringComparison.Ordinal))
            {
                string id = actionId.Substring(7);
                var episode = State.episodes.FirstOrDefault(e => e.sourceKind == "Family" && e.sourceId == id);
                if(FriendWorldTalkAuthority.FamilyInputUnresolved(this,actorPersonId,id)&&
                    !FriendWorldTalkAuthority.IsCurrentFamilyDispatch(this,actorPersonId,targetPersonId,actionId))return false;
                return episode != null && (episode.focusPersonId == actorPersonId || episode.targetPersonId == actorPersonId) &&
                    (episode.focusPersonId==targetPersonId||episode.targetPersonId==targetPersonId||episode.anotherPersonId==targetPersonId);
            }
            if (actionId.StartsWith("minigame-input:", StringComparison.Ordinal) || actionId == "minigame-leave")
            {
                var game = Core.State.activities?.minigame;
                return game != null && (game.residentId == actor.nativeResidentId || State.episodes.Any(e => e.sourceKind == "MinigameInput" && e.sourceId == game.id && e.targetPersonId == actorPersonId));
            }
            return target.controllerKind == PersonControllerKind.Npc || target.personId == actor.personId;
        }
        static bool SourceActionBelongsTo(LifeSimulationRuntime kernel, string nativeResidentId, string kind, string sessionId)
        {
            if (kernel == null || string.IsNullOrEmpty(nativeResidentId)) return false;
            switch (kind)
            {
                case "travel-choice": case "travel-return":
                    return kernel.State.activities?.travels.Any(t => t.id == sessionId && t.residentId == nativeResidentId) == true;
                case "dream-choice": case "wake":
                    return kernel.State.activities?.dreams.Any(d => d.id == sessionId && d.residentId == nativeResidentId) == true;
                case "minigame-input": case "minigame-leave":
                    return kernel.State.activities?.minigame?.residentId == nativeResidentId;
                case "daily":
                    // residentId owns the request. targetId is the original
                    // requested relation/action target, never the responder.
                    return kernel.State.daily?.requests.Any(r => r.id == sessionId && r.residentId == nativeResidentId) == true;
                case "accident":
                    return kernel.State.activities?.accidents.Any(a => a.id == sessionId && a.residentId == nativeResidentId) == true;
                default: return true;
            }
        }
        public string SubmitSourceActionAs(string actorPersonId, string personId, string actionId, string optionId = null, string operationId = null)
        {
            var person = GetPerson(personId);
            if (person == null || !IsAuthority) return "当前不能执行该朋友操作。";
            if (string.IsNullOrEmpty(actionId)) return "请选择一项操作。";
            var actor = GetPerson(actorPersonId);
            if (actor == null) return "角色还没准备好，请稍后再试。";
            if (actor.controllerKind == PersonControllerKind.Human && !CanSubmitHumanAction(actorPersonId, personId, actionId)) return "这不是你的操作。";
            if (SourceCommandOverride != null)
            {
                string overridden = SourceCommandOverride(personId, actionId);
                if (overridden != null) return overridden;
            }
            string[] parts = actionId.Split(new[] { ':' }, 2);
            string kind = parts[0], source = parts.Length == 2 ? parts[1] : "";
            if (!SourceActionBelongsTo(Core, person.nativeResidentId, kind, source)) return "这项活动不属于该朋友。";
            if (kind == "family")
            {
                var episode = State.episodes.FirstOrDefault(e => e.sourceKind == "Family" && !e.committed && e.sourceId == source && (e.focusPersonId == personId || e.targetPersonId == personId));
                if (episode == null) return "对话已经结束了。";
                FamilyChoiceInput familyInput;
                try
                {
                    familyInput = !string.IsNullOrEmpty(optionId) && optionId.TrimStart().StartsWith("{", StringComparison.Ordinal)
                        ? JsonUtility.FromJson<FamilyChoiceInput>(optionId) : new FamilyChoiceInput { optionId = optionId };
                }
                catch (Exception) { return "选项无效。"; }
                if(episode.inputAccepted)
                    return episode.inputOwnerPersonId==actorPersonId&&episode.optionId==familyInput?.optionId&&episode.inputChildName==familyInput?.childName
                        ? "已记下你的选择，等待参与者到场。" : "已经选过了，请稍等。";
                return ResolveFamilyEpisode(episode, familyInput?.optionId, familyInput?.childName, actorPersonId).message;
            }
            string op = operationId ?? "input/" + Guid.NewGuid().ToString("N");
            if (kind == "food") return QueueManualEcologyFood(actorPersonId, person, source, op);
            if (kind == "photo-capture")
            {
                if (RegisteredPhotoCaptureCommand == null) return "摄影尚未准备好。";
                return RegisteredPhotoCaptureCommand(actorPersonId, personId, optionId ?? "{}", op);
            }
            if (kind == "photo-record")
            {
                if (string.IsNullOrEmpty(source) || RegisteredPhotoRecordCommand == null) return "照片尚未准备好。";
                // A private host renderer receipt is the only accepted input.
                // No client filename or arbitrary path is passed to the source.
                return RegisteredPhotoRecordCommand(actorPersonId, personId, source, op);
            }
            if (kind == "honeymoon")
            {
                HoneymoonInput input;
                try { input = JsonUtility.FromJson<HoneymoonInput>(optionId ?? "{}"); }
                catch (Exception) { return "旅行选项无效。"; }
                var economy = FriendsEconomyService.Instance;
                if (input == null || string.IsNullOrEmpty(input.destinationId) || !economy) return "请选择旅行目的地。";
                // The domain port opens its own original KernelScope. Nesting it
                // inside ExecuteKernel would reject the source booking.
                bool accepted = economy.TryStartFamilyHoneymoon(actorPersonId, personId, source, input.destinationId, input.useTicket, op, out var travelSessionId);
                if (!accepted) return economy.LastFailure ?? "旅行尚未开始。";
                CaptureSourceMailbox(); NotifyChanged(); SaveNow(); return "操作已提交。";
            }
            bool manualStart=actor.controllerKind==PersonControllerKind.Human&&person.controllerKind==PersonControllerKind.Npc&&
                (kind=="minigame"||kind=="travel"||kind=="dream");
            bool priorOperation=State.receipts.Any(r=>r.opId==op);
            if (actor.controllerKind == PersonControllerKind.Human && person.personId == actorPersonId &&
                (kind == "minigame" || kind == "travel" || kind == "dream") && !priorOperation)
            {
                var ownBody = TransformOf(person.personId);
                var ownPlayer = ownBody ? ownBody.GetComponent<PaperPixelGames.Core.Player.PPG_Player>() : null;
                if (!ownPlayer || !ownPlayer.IsSpawned) return "本人角色尚未就绪。";
                if (State.leases.Any(l => l.personId == person.personId) || FriendsEconomyService.Instance?.HasActiveJob(person.personId) == true ||
                    ownPlayer.IsUsingEmoteArea || ownPlayer.FirstPersonCharacter && ownPlayer.FirstPersonCharacter.HasActiveMotion)
                    return "请先结束当前动作。";
            }
            if(manualStart&&!priorOperation)
            {
                var body=ActorOf(person.personId);
                if(body==null||body.GetComponent<FriendWorldActor>()==null)return "朋友还没有到场。";
                if(FriendsEconomyService.Instance?.HasActiveJob(person.personId)==true)return "朋友正在工作，稍后再试。";
                bool occupied=State.leases.Any(l=>l.personId==person.personId&&
                    !(l.facilityId=="Casino"&&l.phase=="Queued")&&
                    !(kind=="dream"&&l.ownerEpisode=="rest/"+person.personId&&l.phase=="Sleeping"));
                if(occupied||FriendsCasinoElevatorBridge.Instance?.CanYieldForManualActivity(person.personId)==false)return "朋友正在活动，稍后再试。";
            }
            LifeSimulationResolution resolution = null;
            LifeActivitiesResult activity = null;
            // These original actions consume/refund the selected resident's
            // own emotion balls, wish reward or previously owned decoration.
            string payer = kind == "collect-emotion" || kind == "fountain" || kind == "fountain-balls" || kind == "design-remove"
                ? personId : actorPersonId;
            var receipt = ExecuteKernel(op, payer, scope =>
            {
                if (!SourceActionBelongsTo(scope, person.nativeResidentId, kind, source)) return false;
                switch (kind)
                {
                    // The source HUD StartInteraction route is TriggerPairEvent.
                    // Pressing Talk must not implicitly select Befriend.
                    case "talk": return scope.TriggerPairEvent(actor.nativeResidentId, person.nativeResidentId) != null;
                    case "gift": resolution = scope.GiveGift(person.nativeResidentId, source); break;
                    case "collect-emotion": resolution = scope.CollectEmotionBalls(person.nativeResidentId); break;
                    case "daily-request": return scope.RequestDailyHelp(person.nativeResidentId, string.IsNullOrEmpty(source) ? null : source) != null;
                    case "daily": if (!int.TryParse(optionId, out var answer)) return false; resolution = scope.AnswerDailyRequest(source, answer); break;
                    case "minigame": activity = scope.StartMinigame(person.nativeResidentId, source); break;
                    case "minigame-input":
                        if (scope.State.activities?.minigame?.residentId != person.nativeResidentId) return false;
                        MinigameSourceInput input;
                        if (!string.IsNullOrEmpty(optionId) && optionId.TrimStart().StartsWith("{", StringComparison.Ordinal)) input = JsonUtility.FromJson<MinigameSourceInput>(optionId);
                        else { int.TryParse(optionId, out var selected); input = new MinigameSourceInput { option = selected }; }
                        activity = scope.SubmitMinigameInput(source, input.option, input.value, input.secondary); break;
                    case "minigame-leave": activity = scope.LeaveMinigame(); break;
                    case "travel-ticket": activity = scope.BuyTravelTicket(); break;
                    case "travel":
                        var travelInput = string.IsNullOrEmpty(optionId) ? new TravelInput() : JsonUtility.FromJson<TravelInput>(optionId);
                        var travelPartner = GetPerson(travelInput.partnerPersonId);
                        if (travelPartner?.controllerKind == PersonControllerKind.Human && travelPartner.personId != actorPersonId) return false;
                        activity = scope.StartTravel(person.nativeResidentId, source, travelInput.useTicket, travelPartner?.nativeResidentId); break;
                    case "travel-choice": if (!int.TryParse(optionId, out var travelChoice)) return false; activity = scope.ChooseTravelActivity(source, travelChoice); break;
                    case "travel-return": activity = scope.ReturnFromTravel(source); break;
                    case "dream": activity = scope.StartDream(person.nativeResidentId, source); break;
                    case "dream-choice": if (!int.TryParse(optionId, out var dreamChoice)) return false; activity = scope.ChooseDreamAction(source, dreamChoice); break;
                    case "wake": activity = scope.WakeFromDream(source); break;
                    case "cloth": int.TryParse(optionId, out var clothColor); resolution = scope.BuyCreativeCloth(person.nativeResidentId, source, clothColor); break;
                    case "cloth-equip": int.TryParse(optionId, out var equipColor); resolution = scope.EquipCreativeCloth(person.nativeResidentId, source, equipColor); break;
                    case "cloth-remove": resolution = scope.RemoveCreativeGarment(person.nativeResidentId, source); break;
                    case "room": resolution = scope.BuyCreativeRoom(person.nativeResidentId, source); break;
                    case "goods": resolution = scope.UseCreativeGoods(person.nativeResidentId, source); break;
                    case "habit": resolution = scope.GiveCreativeHabit(person.nativeResidentId, source); break;
                    case "phrase": resolution = scope.TeachCreativePhrase(person.nativeResidentId, source); break;
                    case "topic": resolution = scope.TeachCreativePhrase(person.nativeResidentId, source, true); break;
                    case "share-culture":
                        CultureShareInput share;
                        if (!string.IsNullOrEmpty(optionId) && optionId.TrimStart().StartsWith("{", StringComparison.Ordinal)) share = JsonUtility.FromJson<CultureShareInput>(optionId);
                        else share = new CultureShareInput { targetPersonId = optionId };
                        var cultureTarget = GetPerson(share?.targetPersonId);
                        if (cultureTarget == null || cultureTarget.controllerKind == PersonControllerKind.Human && cultureTarget.personId != actorPersonId) return false;
                        resolution = scope.ShareCreativeCulture(person.nativeResidentId, cultureTarget.nativeResidentId); break;
                    case "profile-fields":
                        var profile = JsonUtility.FromJson<ProfileFieldInput>(optionId ?? "{}");
                        resolution = scope.ConfigureFamilyProfile(person.nativeResidentId, profile.ageYears, profile.gender, profile.preference); break;
                    case "personality":
                        if (!int.TryParse(optionId, out var axisValue)) return false;
                        resolution = scope.TuneCreativePersonality(person.nativeResidentId, source, axisValue); break;
                    case "facility": activity = scope.VisitActivityFacility(source, person.nativeResidentId); break;
                    case "unlock-facility": activity = scope.UnlockFacility(source); break;
                    case "donate": if (!int.TryParse(source, out var donation)) return false; activity = scope.DonateToIsland(donation); break;
                    case "donations-collect": activity = scope.CollectDailyDonations(); break;
                    case "fountain-balls": activity = scope.ContributeEmotionBalls(person.nativeResidentId); break;
                    case "fountain":
                        var wish = !string.IsNullOrEmpty(optionId) && optionId.TrimStart().StartsWith("{", StringComparison.Ordinal)
                            ? JsonUtility.FromJson<FountainWishInput>(optionId)?.wish : optionId;
                        if (string.IsNullOrEmpty(wish)) return false;
                        activity = scope.MakeFountainWish(person.nativeResidentId, wish); break;
                    case "news-read": activity = scope.WatchActivityNews(source); break;
                    case "accident":
                        int accidentChoice;
                        if (!string.IsNullOrEmpty(optionId) && optionId.TrimStart().StartsWith("{", StringComparison.Ordinal)) accidentChoice = JsonUtility.FromJson<AccidentChoiceInput>(optionId).choice;
                        else if (!int.TryParse(optionId, out accidentChoice)) return false;
                        if (accidentChoice < 0 || accidentChoice > 2) return false;
                        activity = scope.ResolveActivityAccident(source, accidentChoice); break;
                    case "market": activity = scope.PurchaseMarketOffer(source); break;
                    case "sell":
                        var item = source.Split(new[] { '/' }, 2); if (item.Length != 2 || !int.TryParse(optionId, out var quantity)) return false;
                        resolution = scope.SellInventoryItem(item[0], item[1], quantity); break;
                    case "design-create":
                        var design = scope.CreateCreativeDesign(source, optionId ?? "Decor");
                        return design != null;
                    case "design-paint":
                        var paint = JsonUtility.FromJson<DesignPaintInput>(optionId ?? "{}");
                        if (scope.CreativeDesign(source) == null) return false;
                        scope.PaintCreativeDesign(source, paint.x0, paint.y0, paint.x1, paint.y1, paint.rgba, paint.radius); return true;
                    case "design-craft": resolution = scope.CraftCreativeDesign(person.nativeResidentId, source); break;
                    case "design-stamp":
                        var stamp = JsonUtility.FromJson<DesignStampInput>(optionId ?? "{}"); if (scope.CreativeDesign(source) == null) return false;
                        scope.StampCreativeDesign(source, stamp.stamp, stamp.x, stamp.y, stamp.rgba); return true;
                    case "design-text":
                        var text = JsonUtility.FromJson<DesignTextInput>(optionId ?? "{}"); if (scope.CreativeDesign(source) == null) return false;
                        scope.SetCreativeDesignText(source, text.title, text.text); return true;
                    case "design-clear":
                        if (!int.TryParse(optionId, out var clearColor) || scope.CreativeDesign(source) == null) return false;
                        scope.ClearCreativeDesign(source, clearColor); return true;
                    case "design-room": resolution = scope.UseCreativeRoomDesign(person.nativeResidentId, source); break;
                    case "design-move":
                        var move = JsonUtility.FromJson<DesignMoveInput>(optionId ?? "{}");
                        resolution = scope.MoveCreativeDecoration(person.nativeResidentId, source, move.dx, move.dz, move.rotate); break;
                    case "design-remove": resolution = scope.RemoveCreativeDecoration(person.nativeResidentId, source); break;
                    case "pet-feed": resolution = scope.CareCreativePet(person.nativeResidentId, true); break;
                    case "pet-play": resolution = scope.CareCreativePet(person.nativeResidentId, false); break;
                    case "design-use": resolution = scope.UseCreativeDesign(person.nativeResidentId, source); break;
                    case "family-start":
                        var selectedFamilyInput = !string.IsNullOrEmpty(optionId) && optionId.TrimStart().StartsWith("{", StringComparison.Ordinal)
                            ? JsonUtility.FromJson<CultureShareInput>(optionId) : new CultureShareInput { targetPersonId = optionId };
                        var selectedFamilyTarget = GetPerson(selectedFamilyInput?.targetPersonId);
                        if (selectedFamilyTarget == null || selectedFamilyTarget.controllerKind == PersonControllerKind.Human && selectedFamilyTarget.personId != actorPersonId) return false;
                        resolution = StartExistingFamilySource(scope, person.nativeResidentId, selectedFamilyTarget.nativeResidentId, source); break;
                    case "family-action":
                        var target = GetPerson(optionId) ?? person;
                        resolution = StartExistingFamilySource(scope, actor.nativeResidentId, target.nativeResidentId, source); break;
                    default: return false;
                }
                return resolution?.success ?? activity?.success ?? false;
            }, editableDraftNativeId: kind == "profile-fields" || kind == "personality" ? person.nativeResidentId : null);
            if (receipt.success)
            {
                // A rejected source command leaves the queued host plan intact.
                // Replaying an old receipt must not cancel a later unrelated visit.
                if(manualStart&&!priorOperation)FriendsCasinoElevatorBridge.Instance?.YieldQueuedForManualActivity(person.personId);
                receipt.message = resolution?.message ?? activity?.message ?? receipt.message;
                if (kind == "minigame" && actor.controllerKind == PersonControllerKind.Human && activity != null &&
                    State.episodes.All(e => e.sourceKind != "MinigameInput" || e.sourceId != activity.sessionId))
                    State.episodes.Add(new SourceEpisode { episodeId = "input/" + activity.sessionId, sourceKind = "MinigameInput", sourceId = activity.sessionId,
                        focusPersonId = personId, targetPersonId = actorPersonId, status = "AwaitHuman" });
                if (activity != null && activity.success)
                {
                    if (kind == "travel") LinkCreativeSourceActivity(actorPersonId, personId, "Travel", source, activity.sessionId, op);
                    else if (kind == "minigame") LinkCreativeSourceActivity(actorPersonId, personId, "Game", source, activity.sessionId, op);
                }
                AdvanceEcologyCreativeActivities();
                CaptureSourceMailbox(); NotifyChanged(); SaveNow();
            }
            return receipt.success ? resolution?.message ?? activity?.message ?? receipt.message : receipt.message;
        }
    }
}
