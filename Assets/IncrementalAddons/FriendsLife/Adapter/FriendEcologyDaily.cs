using System;
using System.Linq;
using TomodachiLife.Simulation;

namespace SushiAndFish.FriendsLife
{
    // Host input routing only. Frozen DailyActionMatches, source item functions,
    // FamilyActionReason and ResolveFamilyChoice remain the decision owners.
    public static class FriendEcologyInputContracts
    {
        public static string DailyActionCategory(LifeDailyRequest request)
        {
            if (request == null) return null;
            if (request.category != "Introduction") return request.category;
            // Same source-id tests and precedence as LifeDailyRuntime.cs:110-116.
            // Unknown introductions keep their real input instead of becoming Befriend.
            string id = request.sourceId ?? "";
            if (id.Contains("Hungry")) return "Food";
            if (id.Contains("Dress")) return "Cloth";
            if (id.Contains("Interior")) return "RoomStyle";
            if (id.Contains("Friend")) return "Relation";
            if (id.Contains("IslandEdit")) return "IslandEdit";
            return null;
        }
    }

    public sealed partial class FriendsLifeRuntime
    {
        // Ids of hungry requests already sent to the sushi shop (this session).
        private readonly System.Collections.Generic.HashSet<string> hungryFallbacks = new System.Collections.Generic.HashSet<string>();
        private void AdvanceEcologyDailyRequests()
        {
            var daily = Core.State.daily;
            if (daily == null) return;
            // Tomodachi: a problem waits for the player (E -> 帮忙, 送礼). The
            // friend no longer buys clothes, goods or rooms or answers its own
            // quarrel. Only a hungry friend nobody helped goes to the original
            // sushi customer queue by itself, 30 game minutes before the end.
            foreach (var current in daily.requests.Where(r => r.status == "Pending" && r.category == "Food" &&
                r.deadlineMinutes - Core.State.gameMinutes <= 30 && !hungryFallbacks.Contains(r.id)).ToArray())
            {
                var person = PersonFromNative(current.residentId);
                if (person == null || person.controllerKind == PersonControllerKind.Human || !IsWorldAvailable(person.personId)) continue;
                var customer = ActorOf(person.personId);
                var queue = PaperPixelGames.Framework.Patterns.Singleton<PaperPixelGames.Core.Ai.NPCs.ResidentManager>.Instance;
                if (!customer || !queue) continue;
                hungryFallbacks.Add(current.id); queue.AddQueue(customer);
            }
        }

        private static bool OwnsSourceInventory(PersonBinding person, string category, string item)
            => person.inventory.Any(i => i.category == category && i.itemName == item && i.quantity > 0);

        private bool HasSourceBudget(PersonBinding person, string requestId, int originalPrice)
        {
            if (originalPrice < 0) return false; // Same rejection as original CreativePay.
            if (originalPrice == 0) return true;
            if (ReadWalletCents == null) return false;
            if (ReadWalletCents(person.personId) >= originalPrice) return true;
            // QueueIncome uses an absolute target balance and already deduplicates
            // this intent. Raw and host cents are the design's fixed 1:1 projection.
            FriendsEconomyService.Instance?.QueueIncome(person.personId, "daily/" + requestId, originalPrice);
            BudgetNeeded?.Invoke(person.personId, requestId);
            return false;
        }

        private void AdvanceDailySourceRelationship(LifeDailyRequest request, PersonBinding person)
        {
            var target = PersonFromNative(request.targetId);
            if (target == null || !IsWorldAvailable(target.personId) ||
                !Core.KernelDailyActionMatches(request, "Befriend", target.nativeResidentId, "Relation")) return;
            // Mailbox extraction removes the source choice from Core. Check that
            // durable owner before invoking StartFamilyAction again.
            if (State.episodes.Any(e => !e.committed && e.sourceKind == "Family" && e.familyChoice != null &&
                e.familyChoice.focusId == person.nativeResidentId && e.familyChoice.targetId == target.nativeResidentId && e.familyChoice.action == "Befriend")) return;
            if (Core.KernelFamilyActionReason(person.nativeResidentId, target.nativeResidentId, "Befriend") != null) return;
            var receipt = ExecuteKernel("daily-family-intent/" + request.id, person.personId,
                scope => scope.StartFamilyAction(person.nativeResidentId, target.nativeResidentId, "Befriend").success, request.id);
            if (receipt.success) CaptureSourceMailbox();
        }
    }
}
