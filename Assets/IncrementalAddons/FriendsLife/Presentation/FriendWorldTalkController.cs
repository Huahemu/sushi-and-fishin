using System;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // Local T07 navigation. UNKNOWN keeps the same request/command identity;
    // only an authority response or its snapshot can close that uncertainty.
    public sealed class FriendWorldTalkController : MonoBehaviour
    {
        static FriendWorldTalkController instance;
        FriendsLifeNetwork network;
        string world, human, person, request, pendingCommand, pendingVerb, desiredPage;
        bool retainQueue, submitted, showing, detached, waitingForPage,awaitingHeldGift;
        int pulseSequence;
        float nextPulse;
        ResidentBillboardText ownedBillboard;
        Transform billboardBody;
        public string RequestId => request;
        // The friend whose E menu is open (its billboard belongs to the menu).
        public static string ActivePersonId => instance && !instance.detached ? instance.person ?? instance.linePerson : null;
        string linePerson;
        public string PersonId => person;
        public string Phase => FriendWorldTalkAuthority.Find(FriendsLifeRuntime.Instance, request)?.phase ?? (request == null ? "None" : "Unknown");
        bool ReadyForInput => Phase == "Active" || Phase == "SourceChoice";
        public static FriendWorldTalkController Instance
        {
            get
            {
                if (!instance) { var go = new GameObject("Friend world talk navigation"); DontDestroyOnLoad(go); instance = go.AddComponent<FriendWorldTalkController>(); }
                return instance;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() { instance = null; }
        public static void Open(string id)
        {
            var current=Instance;
            if(current.request!=null&&current.Phase=="Unknown")
            {
                if(current.person==id){current.ShowUnresolvedOnly();return;}
                current.DetachUnresolvedPresentation();instance=null;
            }
            Instance.Begin(id);
        }
        void DetachUnresolvedPresentation()
        {
            // Keep request/command identity and only observe the authority ACK.
            // This detached instance cannot pulse, End or submit another input.
            detached=true;retainQueue=true;HideOwned();retainQueue=false;
            if(ownedBillboard)Destroy(ownedBillboard.gameObject);ownedBillboard=null;billboardBody=null;
        }
        void ShowUnresolvedOnly(string line="暂未收到这次操作的结果。")
        {
            var panel=Panel;var billboard=Billboard();if(!panel||!billboard||panel.HasCustomConversation&&!showing)return;
            showing=true;retainQueue=true;
            panel.ShowCustomConversation(billboard,line,()=>{showing=false;if(retainQueue)return;DetachUnresolvedPresentation();if(instance==this)instance=null;},
                new[]{new DialoguePanelController.ConversationChoice("返回",()=>{DetachUnresolvedPresentation();if(instance==this)instance=null;})});
        }
        void BindNetwork()
        {
            var current = FriendsLifeNetwork.Instance; if (network == current) return;
            if (network) network.ResponseReceived -= Response; network = current;
            if (network) network.ResponseReceived += Response;
        }
        void Begin(string id)
        {
            var runtime = FriendsLifeRuntime.Instance; if (!runtime || !runtime.Ready || runtime.LocalHuman() == null) return;
            BindNetwork();
            if (request != null)
            {
                if (world != runtime.State.worldId) { Clear(); }
                else if (person != id) { End(); return; }
                else { if (!submitted && pendingVerb != "end" && pendingVerb != "begin") ShowRoot(); return; }
            }
            var target = runtime.GetPerson(id);
            if (target?.controllerKind != PersonControllerKind.Npc || target.existingHostResident || !FriendWorldTalkAuthority.WithinReach(runtime, runtime.LocalHuman().personId, id)) return;
            world = runtime.State.worldId; human = runtime.LocalHuman().personId; person = id; request = Guid.NewGuid().ToString("N");
            // The menu opens only after the authority answers begin: either the
            // friend can talk (menu) or it says one busy line (never silence).
            submitted = false; desiredPage = null; Send("begin", request + "/begin");
        }
        void Send(string verb, string commandId, string action = null, string option = null)
        {
            if (request == null) return;
            BindNetwork(); var runtime = FriendsLifeRuntime.Instance;
            var command = new FriendsWorldCommand { kind = "world-talk", actorPersonId = human, targetPersonId = person,
                commandId = commandId, optionId = request, actionId = verb, displayName = action, nativeProfileJson = option, worldId = world };
            if (verb != "pulse") { pendingCommand = commandId; pendingVerb = verb; }
            if (network) network.Submit(command);
            else if (runtime && runtime.IsAuthority)
            {
                bool accepted = FriendWorldTalkAuthority.Instance.Execute(runtime, human, command, out var result);
                Response(new FriendsWorldResponse { commandId = commandId, accepted = accepted, unresolved = FriendWorldTalkAuthority.Find(runtime, command.optionId)?.phase == "Unknown", message = result });
            }
            // A missing client transport is unresolved. No local success, new
            // nonce or repeated source input is inferred from the return string.
        }
        void Response(FriendsWorldResponse response)
        {
            if (request == null || response.commandId != pendingCommand) return;
            if(response.unresolved){if(awaitingHeldGift&&!showing)ShowUnresolvedOnly("礼物还在处理中，请稍等。");return;}
            string verb = pendingVerb; pendingCommand = null; pendingVerb = null;
            if (verb == "begin")
            {
                if (response.accepted) { if (!showing) ShowRoot(); return; }
                CloseWithLine(string.IsNullOrEmpty(response.message) ? "现在没空，等下再聊吧。" : response.message, false); return;
            }
            if (verb == "source" || verb == "job")
            {
                // Every action shows its result: the friend's line, reaction or
                // a short reason. 继续 opens the menu again.
                CloseWithLine(string.IsNullOrEmpty(response.message) ? (response.accepted ? "好的！" : "现在不行，等下再试吧。") : response.message, true); return;
            }
            if (verb == "end") { submitted = true; HideOwned(); Clear(); }
        }
        // The friend says one line on its billboard; the menu is closed.
        void CloseWithLine(string line, bool allowContinue)
        {
            var noticeBillboard = Billboard(); var noticePanel = Panel; string friend = person;
            ResidentBillboardText owned = null;
            if (noticeBillboard && noticeBillboard == ownedBillboard) { owned = ownedBillboard; ownedBillboard = null; billboardBody = null; }
            submitted = true; retainQueue = true; HideOwned(); Clear();
            if (!noticeBillboard || !noticePanel || noticePanel.HasCustomConversation) { if (owned) Destroy(owned.gameObject); return; }
            linePerson = friend;
            Action closed = () => { if (linePerson == friend) linePerson = null; if (owned) Destroy(owned.gameObject); };
            var leave = new DialoguePanelController.ConversationChoice("离开", () => noticePanel.Hide());
            if (!allowContinue) { noticePanel.ShowCustomConversation(noticeBillboard, line, closed, leave); return; }
            noticePanel.ShowCustomConversation(noticeBillboard, line, closed,
                new DialoguePanelController.ConversationChoice("继续", () => { noticePanel.Hide(); FriendDialoguePresenter.ShowWorldInteraction(friend); }), leave);
        }
        DialoguePanelController Panel => Singleton<GameUIManager>.TryGetInstance(out var ui) ? ui.DialoguePanelController : null;
        ResidentBillboardText Billboard()
        {
            var runtime = FriendsLifeRuntime.Instance; var actor = runtime?.ActorOf(person);
            var original = actor ? actor.GetComponentInChildren<ResidentBillboardText>(true) : null;
            var body = FriendWorldTalkAuthority.VisibleBody(runtime, person);
            if (!original || !body) return null;
            if (body == actor.transform && original.isActiveAndEnabled) return original;
            if (ownedBillboard && billboardBody == body) return ownedBillboard;
            if (ownedBillboard) Destroy(ownedBillboard.gameObject);
            // Reuse only the authored billboard subtree on a NEW owned person.
            // Never clone a resident, network object or inactive donor actor.
            if (original.transform == actor.transform || original.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true).Length != 0) return null;
            ownedBillboard = Instantiate(original, body, false); billboardBody = body;
            ownedBillboard.transform.localPosition = actor.transform.InverseTransformPoint(original.transform.position);
            ownedBillboard.gameObject.SetActive(true); return ownedBillboard;
        }
        void ShowRoot()
        {
            var panel = Panel; var billboard = Billboard(); if (!panel || !billboard || panel.HasCustomConversation && !showing) return;
            showing = true; waitingForPage = false; FriendDialoguePresenter.ShowWorldInteractionMenu(person, billboard, Closed, ChoosePage, Phase == "SourceChoice");
        }
        void ChoosePage(string page)
        {
            if (request == null || submitted) return;
            if (page == "leave") { End(); HideOwned(); return; }
            if (!ReadyForInput)
            {
                // The body is still owned by its current activity. Keep the
                // selection queued, but show that it is queued instead of
                // silently closing the menu (it looked like a dead button).
                desiredPage = page;
                var waitPanel = Panel; var waitBillboard = Billboard();
                if (waitPanel && waitBillboard)
                {
                    showing = true; waitingForPage = true;
                    string header = FriendOriginalActivityUi.Header(person);
                    waitPanel.ShowCustomConversation(waitBillboard, (string.IsNullOrEmpty(header) ? "" : header + "\n") + "请稍等，TA结束当前动作后马上回应你。", Closed,
                        new DialoguePanelController.ConversationChoice("离开", () => { End(); HideOwned(); }));
                    return;
                }
                retainQueue = true; HideOwned(); retainQueue = false; return;
            }
            if (Phase == "SourceChoice" && page != "talk" && page != "activities") { ShowRoot(); return; }
            // Only a choice that waits for THIS player replaces the chat line;
            // an NPC-only episode used to leave a page holding just 返回.
            if (page == "talk" && !FriendsLifeRuntime.Instance.GetEpisodes(person).Any(e => e.familyChoice != null && !e.committed && !e.inputAccepted &&
                e.status == "AwaitHuman" && (e.focusPersonId == human || e.targetPersonId == human || e.anotherPersonId == human)))
            { SubmitSource("talk"); return; }
            var panel = Panel; var billboard = Billboard(); if (!panel || !billboard) { End(); return; }
            showing = true; waitingForPage = false;
            FriendDialoguePresenter.ShowWorldInteractionSource(person, page, billboard, Closed, ShowRoot, SubmitSource, SubmitJob, ChoosePage);
        }
        void SubmitSource(string action, string option = null)
        {
            if (request == null || submitted || !ReadyForInput) return; submitted = true;
            awaitingHeldGift=HeldGiftRules.IsHeld(action);
            if (action != null && action.StartsWith(FriendPlayerInteractions.HeldPrefix, StringComparison.Ordinal))
            {
                var held = HeldItem();
                option = held && held.NetworkObject && held.NetworkObject.IsSpawned ? held.NetworkObjectId.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
            }
            HideOwned();Send("source", request + "/input", action, option);
            if(request!=null&&awaitingHeldGift&&!showing)ShowUnresolvedOnly("正在送出礼物，请稍等。");
        }
        public static PaperPixelGames.Core.Interaction.BaseHoldable HeldItem()
            => Singleton<PaperPixelGames.Core.Player.QuickSlot.QuickSlotHandler>.TryGetInstance(out var slots) ? slots.SelectedItem : null;
        void SubmitJob(StaffRole role)
        { if (request == null || submitted || Phase != "Active") return; submitted = true; Send("job", request + "/input", role.ToString()); HideOwned(); }
        void Closed()
        { showing = false; if (!retainQueue && !submitted) End(); }
        void End()
        {
            if (request == null || submitted || pendingVerb == "end") return;
            desiredPage = null; Send("end", request + "/end");
        }
        void HideOwned() { var panel = Panel; waitingForPage = false; if (showing && panel && panel.HasCustomConversation) panel.Hide(); showing = false; }
        void Clear()
        {
            world = human = person = request = pendingCommand = pendingVerb = desiredPage = null;
            submitted = retainQueue = showing = waitingForPage = awaitingHeldGift = false;
            if (ownedBillboard) Destroy(ownedBillboard.gameObject); ownedBillboard = null; billboardBody = null;
        }
        void Update()
        {
            BindNetwork();
            if(detached)
            {
                var current=FriendsLifeRuntime.Instance;
                if(request==null||current&&current.Ready&&(current.State.worldId!=world||FriendWorldTalkAuthority.Find(current,request)?.phase=="Done"))
                {Clear();Destroy(gameObject);}return;
            }
            if (request == null) return;
            var runtime = FriendsLifeRuntime.Instance; if (!runtime || !runtime.Ready) return;
            if (world != runtime.State.worldId) { retainQueue = true; HideOwned(); retainQueue = false; Clear(); return; }
            var row = FriendWorldTalkAuthority.Find(runtime, request);
            if (row?.phase == "Done")
            {
                // The snapshot can close the row before the response arrives
                // (client) or while the friend became busy: show its line.
                bool action = pendingVerb == "source" || pendingVerb == "job";
                string line = action ? row.resultMessage : !submitted && pendingVerb != "end" ? row.busyLine : null;
                if (!string.IsNullOrEmpty(line)) CloseWithLine(line, action);
                else { retainQueue = true; HideOwned(); retainQueue = false; Clear(); }
                return;
            }
            if (!FriendWorldTalkAuthority.WithinReach(runtime, human, person)) { End(); HideOwned(); }
            if (request == null) return;
            if (Time.unscaledTime >= nextPulse)
            { nextPulse = Time.unscaledTime + 3f; Send("pulse", request + "/p" + (++pulseSequence)); }
            if (ReadyForInput && desiredPage != null && !submitted && pendingVerb != "end")
            {
                var panel = Panel;
                // Our own queued-wait page is showing: continue in place.
                if (waitingForPage && showing && panel && panel.HasCustomConversation)
                { string page = desiredPage; desiredPage = null; waitingForPage = false; ChoosePage(page); }
                else if (panel && !panel.IsVisible && Singleton<GameUIManager>.TryGetInstance(out var ui) && !ui.IsShopUIActive &&
                    !(ui.PauseScreen && ui.PauseScreen.IsVisible) && Singleton<PlayersManager>.TryGetInstance(out var players) && players.LocalPlayer &&
                    players.LocalPlayer.CanStartControlAction(PlayerControlAction.EnterUsable))
                { string page = desiredPage; desiredPage = null; ChoosePage(page); }
            }
            if (showing && (!Panel || !Panel.HasCustomConversation)) Closed();
        }
        void OnDestroy()
        { if (network) network.ResponseReceived -= Response; End(); if (ownedBillboard) Destroy(ownedBillboard.gameObject); if (instance == this) instance = null; }
    }
}
