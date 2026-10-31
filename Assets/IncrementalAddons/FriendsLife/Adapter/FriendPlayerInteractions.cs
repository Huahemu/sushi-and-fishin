using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using TomodachiLife.Simulation;
using UnityEngine;
using Unity.Netcode;

namespace SushiAndFish.FriendsLife
{
    // In-person E actions (Tomodachi Life): 交谈 gives a short line, 帮忙
    // answers the friend's problem, 送礼 gives a held or bought Deep Blue
    // Sushi item. They reach the authority through the existing
    // SourceCommandOverride hook of SubmitSourceActionAs, so the world-talk
    // request, its receipt and its result message stay the single path.
    // Existing M3 pair moments supply both bubbles; no second pair scheduler.
    public static class FriendPlayerInteractions
    {
        static readonly Func<string, string, string> handler = Override;
        static Func<string, string, string> previous;
        static FriendsLifeRuntime bound;
        static float nextSync;
        static string boundWorld;
        public const string BuyPrefix = "buy-present:", HeldPrefix = "present:", HelpPrefix = "help:";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { bound = null; boundWorld = null; previous = null; nextSync = 0; }

        // Called by the world-talk authority every frame (cheap checks only).
        public static void Bind(FriendsLifeRuntime runtime)
        {
            if (!runtime || !runtime.Ready) return;
            if (bound != runtime || boundWorld != runtime.State.worldId)
            { nextSync = 0; boundWorld = runtime.State.worldId; }
            if (bound != runtime || runtime.SourceCommandOverride != handler)
            {
                if (runtime.SourceCommandOverride != handler) previous = runtime.SourceCommandOverride;
                runtime.SourceCommandOverride = handler; bound = runtime;
            }
            if (!runtime.IsAuthority || Time.unscaledTime < nextSync) return;
            nextSync = Time.unscaledTime + 1f;
            SyncPlayerResidents(runtime);
        }
        // Player residents never get problems of their own; they help.
        static void SyncPlayerResidents(FriendsLifeRuntime runtime)
        {
            var list = runtime.Core.DailyState.playerResidents;
            var humans = runtime.People.Where(p => p.controllerKind == PersonControllerKind.Human && !string.IsNullOrEmpty(p.nativeResidentId)).Select(p => p.nativeResidentId).ToArray();
            if (list.Count == humans.Length && humans.All(list.Contains)) return;
            list.Clear(); list.AddRange(humans); runtime.NotifyChanged();
        }
        public static bool Handles(string action) => action == "talk" || action != null &&
            (action.StartsWith(HelpPrefix, StringComparison.Ordinal) || action.StartsWith(HeldPrefix, StringComparison.Ordinal) || action.StartsWith(BuyPrefix, StringComparison.Ordinal));
        static string Override(string personId, string actionId)
        {
            if (!Handles(actionId)) return previous?.Invoke(personId, actionId);
            var runtime = FriendsLifeRuntime.Instance; var friend = runtime?.GetPerson(personId);
            if (!runtime || !runtime.Ready || friend?.controllerKind != PersonControllerKind.Npc) return previous?.Invoke(personId, actionId);
            if (!FriendWorldTalkAuthority.TryCurrentDispatch(runtime, friend.personId, out var humanId, out var option, out var op))
                return "请走到朋友身边再交谈。";
            var human = runtime.GetPerson(humanId);
            if (human?.controllerKind != PersonControllerKind.Human) return "现在不能交谈。";
            try
            {
                if (actionId == "talk") return Talk(runtime, friend, human, op);
                if (actionId.StartsWith(HelpPrefix, StringComparison.Ordinal)) return Help(runtime, friend, human, actionId.Substring(HelpPrefix.Length), option, op);
                bool buy = actionId.StartsWith(BuyPrefix, StringComparison.Ordinal);
                return Present(runtime, friend, human, actionId.Substring(buy ? BuyPrefix.Length : HeldPrefix.Length), buy, option, op);
            }
            catch (Exception error)
            {
                // A committed kernel receipt with an uncertain held-item exit
                // stays Unknown at the talk boundary; never infer a retry.
                if (runtime.State.receipts.Any(r => r.opId == op && r.success)) throw;
                Debug.LogWarning("[FriendsLife] In-person action failed: " + error); return "没能完成，请稍后再试。";
            }
        }

        static string Talk(FriendsLifeRuntime runtime, PersonBinding friend, PersonBinding human, string op)
        {
            bool closer = false;
            var receipt = runtime.ExecuteKernel(op, human.personId, scope => scope.RecordPlayerChat(friend.nativeResidentId, human.nativeResidentId, out closer));
            if (!receipt.success) return "现在不想聊天。";
            return TalkLine(runtime, friend, human);
        }
        // One short line about the friend's problem, mood, job, relations or
        // taste. Each chat shows the next topic, so three chats differ.
        public static string TalkLine(FriendsLifeRuntime runtime, PersonBinding friend, PersonBinding human)
        {
            var core = runtime.Core; var me = core.FindResident(friend.nativeResidentId); if (me == null) return "你好！";
            var problem = Problem(runtime, friend.personId);
            if (problem != null) return problem.description;
            var lines = new List<string>();
            var fight = core.State.relationships.FirstOrDefault(r => r.fromId == me.id && r.fighting && r.toId != human.nativeResidentId);
            if (fight != null) lines.Add("我和" + Name(core, fight.toId) + "吵架了，心里不太舒服。");
            var economy = FriendsEconomyService.Instance;
            var job = economy ? economy.State.sushiAuthorizations.FirstOrDefault(j => j.personId == friend.personId) : null;
            if (job != null)
            {
                string role = job.role == PaperPixelGames.Core.Staff.StaffRole.SushiChef ? "寿司师傅" : job.role == PaperPixelGames.Core.Staff.StaffRole.PrepCook ? "备料员" : job.role == PaperPixelGames.Core.Staff.StaffRole.Waitress ? "服务员" : "补货员";
                lines.Add("我在寿司店当" + role + "。");
            }
            if (me.hunger < 40) lines.Add("肚子有点饿了……");
            if (me.social < 40) lines.Add("最近有点无聊，多来找我玩吧。");
            lines.Add("我最喜欢" + LifeSimulationRuntime.PlayerGiftName(core.GiftTaste(me.id).liked) + "了！");
            var close = core.State.relationships.Where(r => r.fromId == me.id && r.toId != human.nativeResidentId && !r.fighting && (r.kind == "BestFriend" || r.kind == "Friend"))
                .OrderByDescending(r => r.meter).FirstOrDefault();
            if (close != null && core.FindResident(close.toId) != null) lines.Add("我和" + Name(core, close.toId) + "是" + core.FamilyRelationshipLabel(me.id, close.toId) + "！");
            var chat = core.State.journal.LastOrDefault(j => j.kind == "chat" && (j.focusResidentId == me.id || j.targetResidentId == me.id));
            if (chat != null) lines.Add("刚才和" + Name(core, chat.focusResidentId == me.id ? chat.targetResidentId : chat.focusResidentId) + "聊得很开心。");
            string label = core.FamilyRelationshipLabel(me.id, human.nativeResidentId);
            lines.Add(label == "挚友" ? "你是我最好的朋友！" : label == "朋友" ? "和你聊天真开心！" : "很高兴认识你！");
            int turn = core.DailyState.actions.Count(a => a.residentId == me.id && a.category == "PlayerChat");
            return lines[turn % lines.Count];
        }
        static string Name(LifeSimulationRuntime core, string residentId) => core.FindResident(residentId)?.displayName ?? "朋友";

        public static LifeDailyRequest Problem(FriendsLifeRuntime runtime, string personId)
        {
            var person = runtime?.GetPerson(personId); var daily = runtime?.Core?.State.daily;
            return person == null || daily?.requests == null ? null : daily.requests.LastOrDefault(r => r.status == "Pending" && r.residentId == person.nativeResidentId);
        }
        // Answers shown under 帮忙: (label, answer). A null answer opens 送礼.
        public static List<KeyValuePair<string, string>> HelpChoices(FriendsLifeRuntime runtime, LifeDailyRequest request)
        {
            var list = new List<KeyValuePair<string, string>>();
            void Add(string label, string answer) => list.Add(new KeyValuePair<string, string>(label, answer));
            switch (request.category)
            {
                case "Food": Add("送TA吃的", null); break;
                case "Goods": Add("送TA一份礼物", null); break;
                case "Question": Add("多认识些朋友吧", "1"); Add("慢慢来就好", "3"); break;
                case "Relation":
                    string other = runtime.PersonFromNative(request.targetId)?.displayName;
                    Add(string.IsNullOrEmpty(other) ? "去交个新朋友吧" : "去找" + other + "聊聊吧", "1"); break;
                case "Fight": Add("主动去和好吧", "1"); break;
                case "Depress": Add("陪TA聊聊", "1"); break;
            }
            Add("等一下再说", "2");
            if (request.category != "Question" && request.category != "Depress") Add("现在帮不了", "0");
            return list;
        }
        static string Help(FriendsLifeRuntime runtime, PersonBinding friend, PersonBinding human, string requestId, string option, string op)
        {
            if (!int.TryParse(option, out int answer) || answer < 0 || answer > 3) return "请选一个回答。";
            var request = runtime.Core.State.daily?.requests.FirstOrDefault(r => r.id == requestId && r.status == "Pending" && r.residentId == friend.nativeResidentId);
            if (request == null) return "这件事已经过去了。";
            LifeSimulationResolution resolution = null;
            var receipt = runtime.ExecuteKernel(op, human.personId, scope => (resolution = scope.AnswerDailyRequest(requestId, answer, human.nativeResidentId)).success, requestId);
            if (!receipt.success) return resolution?.message ?? "现在帮不上忙。";
            bool solved = runtime.Core.State.daily.requests.Any(r => r.id == requestId && r.status == "Completed");
            var actor = runtime.ActorOf(friend.personId);
            if (solved && actor) { actor.ServerPlayPurchaseExpression(); actor.ServerPlayPurchaseWave(); }
            string message = resolution?.message ?? "";
            return solved ? (string.IsNullOrEmpty(message) ? "" : message + "\n") + "谢谢你，心情好多了！" : message;
        }
        // Short "buy a gift" list (Deep Blue Sushi items, player's wallet).
        public static readonly string[] BuyKinds = { "Drink", "Dessert", "Sushi" };
        public static int BuyPriceRaw(string kind) => kind == "Drink" ? 500 : kind == "Dessert" ? 800 : kind == "Sushi" ? 1200 : 0;
        static string Present(FriendsLifeRuntime runtime, PersonBinding friend, PersonBinding human, string kind, bool buy, string itemId, string op)
        {
            if (Array.IndexOf(LifeSimulationRuntime.PlayerGiftKinds, kind) < 0 || buy && BuyPriceRaw(kind) <= 0) return "这个礼物送不了。";
            if (!buy) return FriendWorldTalkAuthority.Instance.BeginHeldGift(runtime, friend, human, kind, itemId, op);
            return CommitGift(runtime, friend, human, kind, BuyPriceRaw(kind), op, out _);
        }
        // The held-item observer calls this only after its durable positive
        // source witness. Bought gifts have no original inventory to consume.
        internal static string CommitGift(FriendsLifeRuntime runtime, PersonBinding friend, PersonBinding human, string kind, int priceRaw, string op, out bool accepted)
        {
            LifeSimulationResolution resolution = null;
            var receipt = runtime.ExecuteKernel(op, human.personId, scope =>
                (resolution = scope.GivePlayerGift(friend.nativeResidentId, human.nativeResidentId, kind, priceRaw)).success);
            accepted = receipt.success;
            if (!accepted) return resolution?.message ?? "没能送出。";
            // Presentation is separate from the already committed result.
            try
            {
                var actor = runtime.ActorOf(friend.personId);
                if (actor && resolution?.nativeReactionClass == 2) { actor.ServerPlayPurchaseExpression(); actor.ServerPlayPurchaseWave(); }
                else if (actor && resolution?.nativeReactionClass == 1) actor.ServerPlayPurchaseExpression();
            }
            catch (Exception error) { Debug.LogWarning("[FriendsLife] Gift reaction presentation skipped: " + error); }
            return resolution?.message ?? "已收到礼物，谢谢你！";
        }
        // Gift kind of a held Deep Blue Sushi item, or null when not giftable.
        public static string HeldGiftKind(PaperPixelGames.Core.Interaction.BaseHoldable held)
        {
            if (!held) return null;
            if (held is PaperPixelGames.Core.Ocean.DeadFish) return "Fish";
            if (held is PaperPixelGames.Core.ServedDishes.Dango || held is PaperPixelGames.Core.ServedDishes.Dorayaki ||
                held is PaperPixelGames.Core.ServedDishes.Taiyaki || held is PaperPixelGames.Core.ServedDishes.Tamagoyaki) return "Snack";
            if (!(held.ItemSO is PaperPixelGames.Core.Data.ServedDishSO dish)) return null;
            switch (dish.ServedDishType)
            {
                case PaperPixelGames.Core.Interaction.ServedDishType.Sushi: return "Sushi";
                case PaperPixelGames.Core.Interaction.ServedDishType.Soup: return "Soup";
                case PaperPixelGames.Core.Interaction.ServedDishType.Dessert: return "Dessert";
                case PaperPixelGames.Core.Interaction.ServedDishType.Beverage: return "Drink";
                case PaperPixelGames.Core.Interaction.ServedDishType.SideDish: return "Side";
                case PaperPixelGames.Core.Interaction.ServedDishType.Yaki3000: return "Snack";
                default: return null;
            }
        }

        // Second bubble of a resolved pair episode (FriendWorldTalkInvitations).
        public static string PairReply(string action)
        {
            switch (action)
            {
                case "Befriend": return "以后请多关照！"; case "BestFriends": return "我们是最好的朋友！";
                case "Fight": return "哼！"; case "Reconcile": return "嗯，和好吧。"; case "Comfort": return "谢谢你。";
                default: return "嗯嗯！";
            }
        }
    }
}
