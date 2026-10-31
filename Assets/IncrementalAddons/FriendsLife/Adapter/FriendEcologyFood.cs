using System;
using System.Linq;
using TomodachiLife.Simulation;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    [Serializable] public sealed class EcologyFoodPlan
    {
        public string ruleLayer = "RCompatibleSource", worldId, foodId, sourceReference, actionKey, anchorId, payerPersonId;
        public bool sourceCommitted, presentationStarted, manualInput;
        public double sourceUntil;
        public long sourceRevision;
    }

    public sealed partial class FriendsLifeRuntime
    {
        private void QueueEcologyFood(LifeDailyRequest request, PersonBinding person, string foodId)
        {
            if (State.episodes.Any(e => e.sourceKind == "FoodInput" && e.sourceId == request.id)) return;
            var food = Core.Rules.FindFood(foodId);
            if (food == null) return;
            string id = "food-input/" + request.id;
            var plan = new EcologyFoodPlan { worldId = State.worldId, foodId = foodId, sourceReference = food.rowId, actionKey = id + "/Chair/0", payerPersonId = person.personId };
            State.episodes.Add(new SourceEpisode { episodeId = id, sourceKind = "FoodInput", sourceId = request.id,
                eventName = request.sourceId, focusPersonId = person.personId, sourceMinute = request.createdMinutes,
                opId = "daily/" + request.id, optionId = JsonUtility.ToJson(plan) });
            NotifyChanged(); SaveNow();
        }

        private string QueueManualEcologyFood(string actorId, PersonBinding person, string foodId, string op)
        {
            if (person.controllerKind != PersonControllerKind.Npc) return "本人食用功能尚未完成。";
            var payer = GetPerson(actorId); var food = Core.Rules.FindFood(foodId);
            if (payer == null || food == null) return "请选择原目录中的食物。";
            var prior = State.receipts.FirstOrDefault(r => r.opId == op);
            if (prior != null) return prior.success ? "原操作已提交。" : prior.message;
            var old = State.episodes.FirstOrDefault(e => e.sourceKind == "FoodInput" && e.opId == op);
            if (old != null)
            {
                var saved = JsonUtility.FromJson<EcologyFoodPlan>(old.optionId);
                return old.focusPersonId == person.personId && saved.foodId == foodId && saved.payerPersonId == actorId ? "食物已安排。" : "该操作已经提交了其他输入。";
            }
            if (!HasSourceBudget(payer, op, OwnsSourceInventory(payer, "Food", foodId) ? 0 : food.priceRaw)) return "本人余额或食物库存不足。";
            string id = "manual-food/" + op;
            var plan = new EcologyFoodPlan { worldId = State.worldId, foodId = foodId, sourceReference = food.rowId,
                actionKey = id + "/Chair/0", payerPersonId = actorId, manualInput = true };
            State.episodes.Add(new SourceEpisode { episodeId = id, sourceKind = "FoodInput", sourceId = null, eventName = foodId,
                focusPersonId = person.personId, inputOwnerPersonId = actorId, inputAccepted = true, sourceMinute = Core.State.gameMinutes,
                opId = op, optionId = JsonUtility.ToJson(plan) });
            NotifyChanged(); SaveNow(); return "食物已安排。";
        }

        private void AdvanceEcologyFood()
        {
            var actions = FriendsHomeWorldActions.Instance;
            if (!actions) return;
            foreach (var episode in State.episodes.Where(e => !e.committed && e.sourceKind == "FoodInput").ToArray())
            {
                var person = GetPerson(episode.focusPersonId);
                var plan = JsonUtility.FromJson<EcologyFoodPlan>(episode.optionId ?? "{}");
                if (person?.controllerKind != PersonControllerKind.Npc || plan?.worldId != State.worldId) continue;
                var prior = State.receipts.FirstOrDefault(r => r.opId == episode.opId && r.success);
                if (prior != null && !plan.sourceCommitted)
                {
                    plan.sourceCommitted = true; plan.sourceRevision = prior.revision;
                    plan.sourceUntil = Core.FindResident(person.nativeResidentId)?.activityUntilGameMinutes ?? Core.State.gameMinutes;
                    episode.optionId = JsonUtility.ToJson(plan);
                }
                var request = Core.State.daily?.requests.FirstOrDefault(r => r.id == episode.sourceId);
                var pose = actions.Observe(plan.actionKey);
                var payer = GetPerson(string.IsNullOrEmpty(plan.payerPersonId) ? person.personId : plan.payerPersonId);
                if (payer == null) { episode.status = "BindingFault"; episode.fault = "Original food input payer is absent."; continue; }
                if (request == null && !plan.sourceCommitted && !plan.manualInput)
                {
                    episode.status = "BindingFault"; episode.fault = "The original food request record is absent.";
                    actions.RequestOwnerExit(episode.episodeId);
                    if (CanReleaseEcologyFood(episode) && actions.OwnerAtSafeBoundary(episode.episodeId)) ReleaseEpisodeLeases(episode.episodeId);
                    continue;
                }
                bool expired = !plan.sourceCommitted && !plan.manualInput && request?.status != "Pending";
                bool sourceEnded = plan.sourceCommitted && Core.State.gameMinutes >= plan.sourceUntil;
                if (expired || sourceEnded)
                {
                    actions.RequestOwnerExit(episode.episodeId);
                    if (CanReleaseEcologyFood(episode) && actions.OwnerAtSafeBoundary(episode.episodeId)) CompleteEpisode(episode);
                    continue;
                }
                var food = Core.Rules.FindFood(plan.foodId);
                if (food == null || !plan.sourceCommitted && !plan.manualInput && !Core.KernelDailyActionMatches(request, plan.foodId, request.targetId, "Food"))
                { episode.status = "BindingFault"; episode.fault = "The exact original food/request binding is unavailable."; continue; }
                if (!PrepareEcologyWorkBoundary(episode) || State.leases.Any(l => l.personId == person.personId && l.ownerEpisode != episode.episodeId)) continue;
                var actor = ActorOf(person.personId);
                if (actor?.Brain == null || actor.Runtime == null) continue;
                if (pose == null)
                {
                    var sourceActor = Core.FindResident(person.nativeResidentId);
                    if (!plan.sourceCommitted && (sourceActor?.activity == "睡觉" || sourceActor?.activity == "梦中" || Core.IsResidentInActivity(person.nativeResidentId))) continue;
                    if (actor.Runtime.IsSnapshotApplying && !State.leases.Any(l => l.personId == person.personId && l.ownerEpisode == episode.episodeId)) continue;
                    if (!actions.TryBegin(person.personId, plan.actionKey, "Chair", episode.episodeId, plan.anchorId)) continue;
                    episode.status = "WorldTravel"; NotifyChanged();
                    continue;
                }
                if (pose.phase == "Failed")
                {
                    episode.status = "BindingFault"; episode.fault = pose.failure;
                    // Keep the failed action identity. Repeated polling must not
                    // manufacture another entry or hide an invalid physical pose.
                    continue;
                }
                if (pose.phase == "Completed")
                {
                    if (plan.sourceCommitted)
                    { if (CanReleaseEcologyFood(episode)) CompleteEpisode(episode); continue; }
                    // A known rejected original input can be retried after the
                    // real budget/native-mood context changes, with a new node entry.
                    string current = FoodInputContext(payer, person, plan.foodId);
                    if (episode.rejectedContext == current) continue;
                    plan.actionKey = episode.episodeId + "/Chair/" + (++episode.inputCursor);
                    plan.anchorId = null; episode.optionId = JsonUtility.ToJson(plan); episode.status = "Queued";
                    NotifyChanged(); SaveNow(); continue;
                }
                if (pose.phase != "Holding" || !pose.arrived || !pose.poseReady || !pose.supportVerified) continue;
                if (string.IsNullOrEmpty(plan.anchorId)) { plan.anchorId = pose.anchorId; episode.optionId = JsonUtility.ToJson(plan); }
                if (!plan.sourceCommitted)
                {
                    if (!actions.CanBeginSourceFoodUse(plan.actionKey, episode.episodeId)) continue;
                    var receipt = ExecuteKernel(episode.opId, payer.personId,
                        scope => scope.GiveFood(person.nativeResidentId, plan.foodId).success, episode.episodeId);
                    if (!receipt.success)
                    {
                        episode.status = "SourceRejected"; episode.fault = receipt.message;
                        episode.rejectedContext = FoodInputContext(payer, person, plan.foodId);
                        HasSourceBudget(payer, episode.sourceId ?? episode.opId, OwnsSourceInventory(payer, "Food", plan.foodId) ? 0 : food.priceRaw);
                        actions.RequestExit(plan.actionKey, episode.episodeId); NotifyChanged(); SaveNow(); continue;
                    }
                    plan.sourceCommitted = true; plan.sourceRevision = receipt.revision;
                    plan.sourceUntil = Core.FindResident(person.nativeResidentId).activityUntilGameMinutes;
                    episode.worldArrivalVerified = true; episode.focusCommitPosition = actor.transform.position;
                    episode.optionId = JsonUtility.ToJson(plan); episode.status = "SourceEating"; episode.fault = "";
                    NotifyChanged(); SaveNow();
                }
                // A source receipt already exists. A late pose/material failure
                // only retries presentation; it never consumes food/money twice.
                if (actions.TryBeginSourceFoodUse(plan.actionKey, episode.episodeId, plan.foodId) && !plan.presentationStarted)
                {
                    plan.presentationStarted = true; episode.optionId = JsonUtility.ToJson(plan); NotifyChanged();
                }
            }
        }

        private string FoodInputContext(PersonBinding person, PersonBinding target, string foodId)
        {
            var native = Core.FindResident(target.nativeResidentId);
            return (ReadWalletCents?.Invoke(person.personId) ?? 0) + "|" +
                string.Join(";", person.inventory.Where(i => i.category == "Food" && i.itemName == foodId).Select(i => i.quantity)) + "|" +
                native?.nativeMoodDeadlineRaw + "|" + Core.State.nativeMoodClockResolved + "|" + Core.State.nativeMoodClockRaw;
        }
        private bool CanReleaseEcologyFood(SourceEpisode episode)
            => FriendsEconomyService.Instance?.HasActiveJob(episode.focusPersonId) != true &&
                !State.leases.Any(l => l.personId == episode.focusPersonId && l.ownerEpisode != episode.episodeId) &&
                !FriendInstances<FriendsHomeFurnitureClaim>.Active()
                    .Any(c => c.Active && c.personId == episode.focusPersonId && c.ownerEpisode != episode.episodeId);
    }
}
