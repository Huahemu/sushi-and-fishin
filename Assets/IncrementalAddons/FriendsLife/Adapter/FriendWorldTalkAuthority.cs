using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Staff;
using UnityEngine;
using Bozo.ModularCharacters;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    // This adapter waits for the existing activity owner to release the actor.
    // A safeBoundary data flag alone is not a source-graph suspension API.
    [DefaultExecutionOrder(-110)]
    public sealed partial class FriendWorldTalkAuthority : MonoBehaviour
    {
        static FriendWorldTalkAuthority instance;
        FriendsLifeSave observed;
        readonly Dictionary<string, float> contact = new Dictionary<string, float>();
        readonly Dictionary<string, float> waitSince = new Dictionary<string, float>();
        // Ephemeral only; never serialized and never used as a source receipt.
        sealed class TalkHold
        {
            public FriendsLifeRuntime runtime; public FriendsLifeSave world; public FriendWorldTalkRequest row;
            public ResidentController actor; public ResidentBrain brain; public object token;
            public string nativeId,activity; public int cursor; public double deadline; public bool sourceChanged;
        }
        readonly Dictionary<string,TalkHold> talkHolds = new Dictionary<string,TalkHold>();
        // Only token-owned, bounded post-talk original-goal handoffs.
        readonly Dictionary<string,TalkHold> talkGoals = new Dictionary<string,TalkHold>();
        bool BeginConversationHold(FriendsLifeRuntime runtime,FriendWorldTalkRequest row,out string reason)
        {
            reason=null; var person=runtime.GetPerson(row.personId);var actor=runtime.ActorOf(row.personId);var brain=actor?actor.Brain:null;
            var resident=person==null?null:runtime.Core?.FindResident(person.nativeResidentId);
            if(person==null||!actor||brain==null||resident==null||talkHolds.ContainsKey(row.requestId))
            {reason="ConversationHoldIdentityUnavailable";return false;}
            var token=new object();
            if(!brain.TryHoldConversation(token,out reason))return false;
            try
            {
                talkHolds.Add(row.requestId,new TalkHold{runtime=runtime,world=runtime.State,row=row,actor=actor,brain=brain,token=token,
                    nativeId=resident.id,activity=resident.activity,cursor=resident.activityCursor,deadline=resident.activityUntilGameMinutes});
                // A repeated talk may have atomically inherited the same goal.
                talkGoals.Remove(row.personId);
                return true;
            }
            catch
            {
                brain.ReleaseConversationHold(token);throw;
            }
        }
        bool HoldStillValid(FriendsLifeRuntime runtime,FriendWorldTalkRequest row)
        {
            if(!talkHolds.TryGetValue(row.requestId,out var h))return false;
            return h.runtime==runtime&&ReferenceEquals(h.world,runtime.State)&&ReferenceEquals(h.row,row)&&!h.sourceChanged&&
                h.actor&&runtime.ActorOf(row.personId)==h.actor&&ReferenceEquals(h.actor.Brain,h.brain)&&h.brain.HasConversationHold(h.token);
        }
        void ReleaseConversationHold(FriendWorldTalkRequest row)
        {
            if(row==null||!talkHolds.TryGetValue(row.requestId,out var h)||!ReferenceEquals(h.row,row))return;
            talkHolds.Remove(row.requestId);
            // Token API alone decides whether this exact actor/state is still owned.
            if(h.actor&&ReferenceEquals(h.actor.Brain,h.brain))
            {
                bool released=h.brain.ReleaseConversationHold(h.token);
                if(released&&h.brain.ConversationGoalStatus=="awaiting-original-goal")talkGoals[row.personId]=h;
            }
        }
        void ClearConversationGoals()
        {
            foreach(var h in talkGoals.Values.ToArray())
                if(h.actor&&ReferenceEquals(h.actor.Brain,h.brain))h.brain.CancelConversationGoalHandoff(h.token);
            talkGoals.Clear();
        }
        void ObserveConversationGoals(FriendsLifeRuntime runtime)
        {
            foreach(var entry in talkGoals.ToArray())
            {
                var h=entry.Value;
                if(!h.actor||!ReferenceEquals(h.actor.Brain,h.brain)||h.brain.ConversationGoalStatus!="awaiting-original-goal")
                {talkGoals.Remove(entry.Key);continue;}
                var person=runtime.GetPerson(entry.Key);var source=person==null?null:runtime.Core?.FindResident(person.nativeResidentId);
                bool changed=!ReferenceEquals(h.runtime,runtime)||!ReferenceEquals(h.world,runtime.State)||
                    runtime.ActorOf(entry.Key)!=h.actor||person==null||person.nativeResidentId!=h.nativeId||source==null||
                    source.activity!=h.activity||source.activityCursor!=h.cursor||source.activityUntilGameMinutes!=h.deadline;
                if(changed){h.brain.CancelConversationGoalHandoff(h.token);talkGoals.Remove(entry.Key);}
            }
        }
        void ReleaseAllConversationHolds()
        {
            foreach(var h in talkHolds.Values.ToArray())ReleaseConversationHold(h.row);
        }
        // Called only by the scoped Core.Advance hook. Existing source changes
        // win: never overwrite a changed activity, cursor or deadline.
        public static bool AdvanceHeldActivityClock(TomodachiLife.Simulation.LifeSimulationResident resident,double minutes)
        {
            var owner=instance;if(!owner||resident==null||double.IsNaN(minutes)||double.IsInfinity(minutes)||minutes<=0)return false;
            try
            {
                var candidates=owner.talkHolds.Values.Where(h=>h.nativeId==resident.id).ToArray();
                if(candidates.Length!=1)return false;var h=candidates[0];var runtime=FriendsLifeRuntime.Instance;
                if(!runtime||!runtime.Ready||!ReferenceEquals(runtime,h.runtime)||!ReferenceEquals(runtime.State,h.world)||
                   h.row.phase!="Active"||!h.row.inPlace||h.row.inputDispatched||!string.IsNullOrEmpty(h.row.completedAction)||
                   !owner.HoldStillValid(runtime,h.row))return false;
                if(runtime.Core==null||!ReferenceEquals(runtime.Core.FindResident(h.nativeId),resident)||
                   runtime.State.leases.Any(l=>l.personId==h.row.personId&&l.facilityId!="WorldTalk")||
                   FriendsEconomyService.Instance?.HasActiveJob(h.row.personId)==true)
                {h.sourceChanged=true;return false;}
                if(resident.activity!=h.activity||resident.activityCursor!=h.cursor||resident.activityUntilGameMinutes!=h.deadline||
                   double.IsNaN(h.deadline)||double.IsInfinity(h.deadline)||double.IsInfinity(h.deadline+minutes))
                {h.sourceChanged=true;return false;}
                resident.activityUntilGameMinutes+=minutes;h.deadline=resident.activityUntilGameMinutes;return true;
            }
            catch(Exception error)
            {
                foreach(var h in owner.talkHolds.Values.Where(h=>h.nativeId==resident.id).ToArray())h.sourceChanged=true;
                Debug.LogWarning("[FriendsLife] Conversation source clock hold failed: "+error.GetType().Name);return false;
            }
        }
        public static FriendWorldTalkAuthority Instance
        {
            get
            {
                if (!instance)
                {
                    var go = new GameObject("Friend world talk authority"); DontDestroyOnLoad(go);
                    instance = go.AddComponent<FriendWorldTalkAuthority>();
                }
                return instance;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { instance = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install() { _ = Instance; }

        public static Transform VisibleBody(FriendsLifeRuntime runtime, string id)
        {
            var actor = runtime?.ActorOf(id);
            if (!actor || !FriendResidentBindings.IsOwned(actor)) return null;
            var economy = FriendsEconomyService.Instance;
            // A staff body cannot map to this person without a current job row.
            // Read the live ledger now; retain the original lookup and visibility
            // predicate whenever such a mapping can exist. No frame/state cache.
            // The staff body is the NPC of this person's live employee contract.
            var contract = economy && id != null ? economy.State.jobs.FirstOrDefault(j => j.personId == id && j.state != FriendJobPhase.Done && j.employeeId != 0) : null;
            var npc = contract != null && economy.PersonForEmployee(contract.employeeId) == id ? economy.StaffNpcFor(contract.employeeId, false) : null;
            var staff = npc && npc.gameObject.activeInHierarchy && npc.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled && r.gameObject.activeInHierarchy) ? npc : null;
            if (staff) return staff.transform;
            bool mergedVisible=actor.GetComponentsInChildren<OutfitSystem>(true).Any(system=>
            {var body=system.GetCharacterBody();return body&&body.enabled&&body.gameObject.activeInHierarchy&&body.sharedMesh&&body.sharedMesh.vertexCount>0;});
            if(mergedVisible)return actor.transform;
            return actor.VisualAnimator && actor.VisualAnimator.gameObject.activeInHierarchy &&
                actor.VisualAnimator.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled && r.gameObject.activeInHierarchy)
                ? actor.transform : null;
        }
        public static bool WithinReach(FriendsLifeRuntime runtime, string human, string person)
        {
            var from = runtime?.TransformOf(human); var to = VisibleBody(runtime, person);
            // The native Interactor has already selected the E target locally.
            // Authority uses that player's authored range and the copied
            // capsule size, including a dormant donor whose bounds are empty.
            var player = from ? from.GetComponent<PPG_Player>() : null;
            var shape = runtime?.ActorOf(person)?.GetComponent<CapsuleCollider>();
            if (!player || !player.Interactor || !to || !shape) return false;
            float scale = Mathf.Max(Mathf.Abs(to.lossyScale.x), Mathf.Abs(to.lossyScale.y), Mathf.Abs(to.lossyScale.z));
            float radius = shape.radius * scale, half = Mathf.Max(shape.height * .5f, shape.radius) * scale;
            float extent = new Vector3(radius, half, radius).magnitude;
            return Vector3.Distance(from.position, to.position) <= player.Interactor.InteractRange + extent;
        }
        static bool Safe(FriendsLifeRuntime runtime, string id)
        {
            var actor = runtime.ActorOf(id); var state = actor?.Runtime;
            var person = runtime.GetPerson(id);
            string activity = person == null ? null : runtime.Core.FindResident(person.nativeResidentId)?.activity;
            return WorldTalkRules.CanSuspend(actor && actor.Brain != null && state != null && VisibleBody(runtime, id) == actor.transform,
                runtime.State.leases.Any(l => l.personId == id), FriendsEconomyService.Instance?.HasActiveJob(id) == true,
                person == null || activity == "睡觉" || activity == "梦中" || runtime.Core.IsResidentInActivity(person.nativeResidentId) ||
                    runtime.GetEpisodes(id).Any(e => !e.committed && (e.status == "Travel" || e.status == "SourceRunning" || e.status == "WorldPresentation" || e.status == "SourceActivity")),
                state?.IsWorking ?? true, state?.IsInRestaurant ?? true, state?.HasRestaurantAssignment ?? true,
                state?.IsInteracting ?? true, state?.IsSwimming ?? true, state?.IsSnapshotApplying ?? true,
                actor?.Brain?.StateMachine?.Current?.CanInterrupt(ResidentRequestPriority.PlayerInteraction) == true);
        }
        // Tomodachi: a Mii can always be spoken to. Only work, eating and sleep
        // keep it from talking, and then it says so in one line.
        public static string BusyLine(FriendsLifeRuntime runtime, string id)
        {
            var actor = runtime?.ActorOf(id); var state = actor ? actor.Runtime : null; var person = runtime?.GetPerson(id);
            if (person == null || !actor || state == null) return "现在不方便，等下再聊吧。";
            if (FriendsEconomyService.Instance?.HasActiveJob(id) == true || state.IsWorking) return "正在工作，等下再聊吧。";
            if (state.IsInRestaurant || state.HasRestaurantAssignment) return "正在吃饭，等下再聊吧。";
            if (FriendLifeStatus.Describe(id) == "在家睡觉") return "困了，明天再聊吧。";
            if (state.IsSwimming) return "正在游泳，等下再聊吧。";
            if (runtime.State.leases.Any(l => l.personId == id && l.facilityId != "WorldTalk")) return "现在有事，等下再聊吧。";
            return null;
        }
        // A seated, lying or walking friend answers where it is. The seat and
        // its original schedule keep their owner; no body lease is taken.
        static bool CanTalkInPlace(FriendsLifeRuntime runtime, string id)
        {
            var actor = runtime.ActorOf(id);
            return actor && actor.Runtime != null && !actor.Runtime.IsSnapshotApplying && VisibleBody(runtime, id) == actor.transform;
        }
        public static FriendWorldTalkRequest Find(FriendsLifeRuntime runtime, string request)
            => runtime?.State?.worldTalks?.FirstOrDefault(r => r.requestId == request);
        static SourceEpisode HumanChoice(FriendsLifeRuntime runtime, FriendWorldTalkRequest row)
            => runtime.GetEpisodes(row.personId).FirstOrDefault(e => !e.committed && !e.inputAccepted && e.status == "AwaitHuman" && e.familyChoice != null &&
                (row.sourceChoiceId == null || e.sourceId == row.sourceChoiceId) &&
                runtime.CanSubmitHumanAction(row.humanId, row.personId, "family:" + e.sourceId));
        public bool Execute(FriendsLifeRuntime runtime, string human, FriendsWorldCommand command, out string message)
        {
            message = "现在不能交谈。";
            if (!runtime || !runtime.Ready || !runtime.IsAuthority || command == null || string.IsNullOrEmpty(command.optionId) || command.optionId.Length > 64) return false;
            if (runtime.GetPerson(human)?.controllerKind != PersonControllerKind.Human || runtime.GetPerson(command.targetPersonId)?.controllerKind != PersonControllerKind.Npc) return false;
            Observe(runtime);
            var row = Find(runtime, command.optionId);
            if (row != null && !WorldTalkRules.SameOwner(row, human, command.targetPersonId)) return false;
            if (command.actionId == "begin")
            {
                if (row != null)
                {
                    if (row.phase == "Done" && row.busyLine != null) { message = row.busyLine; return false; }
                    message = "操作已受理。"; return true;
                }
                if (!WithinReach(runtime, human, command.targetPersonId)) { message = "再走近一点吧。"; return false; }
                if (!WorldTalkRules.CanBegin(runtime.State.worldTalks, human, command.targetPersonId, command.optionId)) { message = "请稍等，上一段对话还没结束。"; return false; }
                Prune(runtime);
                row = new FriendWorldTalkRequest { requestId = command.optionId, humanId = human, personId = command.targetPersonId };
                runtime.State.worldTalks.Add(row); contact[row.requestId] = Time.unscaledTime;
                // Answer at once: the menu opens (Active / in place / a family
                // choice) or the busy friend says one line. Never silence.
                Step(runtime, row, false);
                runtime.NotifyChanged(); runtime.SaveNow();
                if (row.phase == "Done") { message = row.busyLine ?? "现在没空，等下再聊吧。"; return false; }
                message = "操作已受理。"; return true;
            }
            if (row == null) return false;
            if (row.npcInitiated) return ExecuteNpcOffer(runtime,human,row,command,out message);
            ReconcileInput(runtime, row);
            if (row.phase == "Unknown") { message = "暂未收到操作结果。"; return false; }
            if (command.actionId == "end") { Finish(runtime, row); message = "操作已结束。"; return true; }
            if (command.actionId == "pulse")
            { if (!WorldTalkRules.Terminal(row)) contact[row.requestId] = Time.unscaledTime; message = "操作已受理。"; return true; }
            string action = command.actionId == "job" ? "job:" + command.displayName : command.displayName;
            if (row.phase == "Done")
            {
                bool same = WorldTalkRules.SameCompletion(row, command.commandId, action, command.nativeProfileJson);
                message = same ? row.resultMessage : "该交互已经结束。"; return same && row.actionAccepted;
            }
            if (command.actionId != "source" && command.actionId != "job") return false;
            if (row.phase != "Active" && row.phase != "SourceChoice") { message = "请稍等一下。"; return false; }
            if (!WithinReach(runtime, human, row.personId)) { message = "再走近一点吧。"; return false; }
            if (command.actionId == "source" && !runtime.CanSubmitHumanAction(human, row.personId, action)) { message = "现在不能这样做。"; return false; }
            if (row.phase == "SourceChoice" && (command.actionId != "source" || action != "family:" + row.sourceChoiceId || HumanChoice(runtime, row) == null)) { message = "请先回应这件事。"; return false; }
            if (row.phase == "Active" && !row.inPlace && !FriendsBodyAuthority.IsOwned(runtime,row.personId,LeaseId(row))) { message = "请稍等一下。"; return false; }
            // Record the exact command before releasing the execution lease.
            // A retry cannot submit the source input or job invitation twice.
            row.completedCommandId = command.commandId; row.completedAction = action; row.completedOption = command.nativeProfileJson;
            try
            {
                Finish(runtime, row, false);
                if(row.phase!="Done") { message="请稍等一下，再试一次。";row.resultMessage=message;row.actionAccepted=false;runtime.SaveNow();return false; }
                row.inputDispatched=true;row.phase="Unknown";runtime.NotifyChanged();runtime.SaveNow();
                if (command.actionId == "job")
                {
                    row.actionAccepted = Enum.TryParse(command.displayName, out StaffRole role) && Enum.IsDefined(typeof(StaffRole), role) &&
                        FriendsEconomyService.Instance && FriendsEconomyService.Instance.InviteToWork(row.personId, role);
                    var economy = FriendsEconomyService.Instance;
                    message = row.actionAccepted ? "已录用，开店后去上班。" : FriendUi.PlayerText(economy ? economy.LastFailure : null, "现在无法安排工作，请稍后再试。");
                }
                else
                {
                    message = DispatchSourceInput(runtime,row);
                    row.actionAccepted = InputRecorded(runtime, row);
                    if(HeldGiftRules.IsHeld(action)&&row.heldGiftConsumptionRequested&&!row.actionAccepted)
                    {row.phase="Unknown";row.resultMessage=null;runtime.NotifyChanged();runtime.SaveNow();message="礼物还在处理中，请稍等。";return false;}
                }
                row.resultMessage = message;row.phase="Done"; runtime.NotifyChanged(); runtime.SaveNow(); return row.actionAccepted;
            }
            catch (Exception error)
            {
                row.phase = "Unknown"; row.resultMessage = null; runtime.NotifyChanged(); runtime.SaveNow();
                message = "暂未收到操作结果。"; Debug.LogWarning("[FriendsLife] World talk input outcome unresolved: " + error.Message); return false;
            }
        }
        static bool InputRecorded(FriendsLifeRuntime runtime, FriendWorldTalkRequest row)
        {
            if (row.completedAction?.StartsWith("job:", StringComparison.Ordinal) == true)
                return false; // A pre-existing authorization is not this command's ACK.
            string op = "world-talk/" + row.requestId + "/input";
            bool receipt=runtime.State.receipts.Any(r => r.opId == op && r.success);
            if(HeldGiftRules.IsHeld(row.completedAction))return HeldGiftRules.Recorded(row.heldGiftConsumptionObserved,row.heldGiftCompletionSaved,receipt);
            return receipt ||
                row.completedAction?.StartsWith("family:", StringComparison.Ordinal) == true &&
                runtime.State.episodes.Any(e => e.sourceId == row.completedAction.Substring(7) && (e.committed || e.inputAccepted) &&
                    e.optionId == row.completedOption && e.inputOwnerPersonId == row.humanId);
        }
        static void ReconcileInput(FriendsLifeRuntime runtime, FriendWorldTalkRequest row)
        {
            if (string.IsNullOrEmpty(row.completedCommandId) || row.resultMessage != null || !InputRecorded(runtime, row)) return;
            row.actionAccepted = true; row.resultMessage = "操作已提交。"; row.phase = "Done"; runtime.NotifyChanged(); runtime.SaveNow();
        }
        void Observe(FriendsLifeRuntime runtime)
        {
            if (observed == runtime.State) return;
            ReleaseAllConversationHolds(); ClearConversationGoals();
            contact.Clear(); waitSince.Clear(); observed = runtime.State;
            observed.worldTalks = observed.worldTalks ?? new List<FriendWorldTalkRequest>();
            bool changed=false;
            foreach(var row in observed.worldTalks)
            {
                // UI does not survive a reload. Preserve the exact input/op;
                // release only this claim through the shared body boundary.
                if(row.npcInitiated)
                {
                    row.approachAttempted=true;
                    if(row.phase=="Approaching"||row.phase=="ApproachReleasing")row.phase="ApproachReleasing";
                    else if(row.phase!="Done"&&row.phase!="Unknown")row.phase="Offered";
                }
                else if(row.phase=="Unknown"||!string.IsNullOrEmpty(row.completedCommandId)&&row.resultMessage==null)
                    row.phase="Unknown"; // legacy saves have no inputDispatched field: never infer a negative ACK.
                else if(!WorldTalkRules.Terminal(row))row.phase="Closing";
                TryReleaseTalkBody(runtime,row);
                ReconcileInput(runtime,row);contact[row.requestId]=Time.unscaledTime;changed=true;
            }
            if (changed) { runtime.NotifyChanged(); runtime.SaveNow(); }
        }
        static string LeaseId(FriendWorldTalkRequest row) => "world-talk/" + row.requestId;
        void Update()
        {
            var runtime = FriendsLifeRuntime.Instance; MaintainHeldGiftSession(runtime);
            if (!runtime || !runtime.Ready || !runtime.IsAuthority) { ReleaseAllConversationHolds(); ClearConversationGoals(); return; }
            Observe(runtime); ObserveConversationGoals(runtime); FriendPlayerInteractions.Bind(runtime); AdvanceHeldGifts(runtime);
            foreach(var h in talkHolds.Values.ToArray())
            {
                bool sameWorld=ReferenceEquals(h.world,runtime.State);
                bool present=sameWorld&&runtime.State.worldTalks.Contains(h.row);
                // UNKNOWN or recorded input belongs to reconciliation. Never
                // convert it to Done as a side effect of local hold cleanup.
                if(!present||h.row.phase!="Active"||h.row.inputDispatched||!string.IsNullOrEmpty(h.row.completedAction))
                {ReleaseConversationHold(h.row);continue;}
                if(!HoldStillValid(runtime,h.row))Finish(runtime,h.row);
            }
            foreach (var row in runtime.State.worldTalks.Where(r => !WorldTalkRules.Terminal(r)).ToArray())
            {
                if(row.npcInitiated){AdvanceNpcOffer(runtime,row);continue;}
                if(row.phase=="Closing"){Finish(runtime,row);continue;}
                if (row.phase == "Unknown") { TryReleaseTalkBody(runtime,row);ReconcileInput(runtime, row); continue; }
                if (!WithinReach(runtime, row.humanId, row.personId) || !contact.TryGetValue(row.requestId, out var seen) || Time.unscaledTime - seen > 20f)
                { Finish(runtime, row); continue; }
                if (row.phase == "Active")
                {
                    if (!row.inPlace) { if (HasOtherOwner(runtime, row.personId, LeaseId(row))) Finish(runtime, row); }
                    else { string busy = BusyLine(runtime, row.personId); if (busy != null) FinishBusy(runtime, row, busy); }
                    continue;
                }
                if (row.phase == "SourceChoice")
                { if (HumanChoice(runtime, row) == null) Finish(runtime, row); continue; }
                Step(runtime, row);
            }
        }
        void Step(FriendsLifeRuntime runtime, FriendWorldTalkRequest row, bool persist = true)
        {
            var choice = HumanChoice(runtime, row);
            if (choice != null)
            {
                // A source-owned pending human input needs no second world
                // episode or suspension. Keep its lease, seat and bill.
                row.sourceChoiceId = choice.sourceId; row.phase = "SourceChoice"; runtime.NotifyChanged(); if(persist)runtime.SaveNow(); return;
            }
            string busy = BusyLine(runtime, row.personId);
            if (busy != null) { FinishBusy(runtime, row, busy, persist); return; }
            // Both walking and stable seated conversations use a temporary
            // local hold; do not take a WorldTalk body lease or reset its path.
            if (CanTalkInPlace(runtime,row.personId) && BeginConversationHold(runtime,row,out _))
            {
                row.inPlace = true; row.phase = "Active";
                try { runtime.NotifyChanged(); if(persist)runtime.SaveNow(); return; }
                catch { ReleaseConversationHold(row); row.phase="Closing"; throw; }
            }
            if (row.phase != "Waiting") { row.phase = "Waiting"; waitSince[row.requestId] = Time.unscaledTime; runtime.NotifyChanged(); }
            else if (!waitSince.TryGetValue(row.requestId, out var since) || Time.unscaledTime - since > 6f)
                FinishBusy(runtime, row, "现在走不开，等下再聊吧。", persist);
        }
        void FinishBusy(FriendsLifeRuntime runtime, FriendWorldTalkRequest row, string line, bool persist = true)
        { row.busyLine = line; row.resultMessage = line; row.actionAccepted = false; Finish(runtime, row, persist); }
        // Finished player requests are only UI history. Keep the newest few;
        // NPC offers and social results stay for the 社交消息 page.
        static void Prune(FriendsLifeRuntime runtime)
        {
            var done = runtime.State.worldTalks.Where(r => r.phase == "Done" && !r.npcInitiated && !r.sourceResult).ToArray();
            for (int i = 0; i < done.Length - 30; i++) runtime.State.worldTalks.Remove(done[i]);
        }
        bool TryReleaseTalkBody(FriendsLifeRuntime runtime,FriendWorldTalkRequest row)
        {
            ReleaseConversationHold(row);
            string owner=row.npcInitiated?row.sourceEpisodeId:LeaseId(row);
            var claims=runtime.State.leases.Where(l=>l.personId==row.personId&&l.ownerEpisode==owner).ToArray();
            if(claims.Length==0)return true;
            // The source episode reuses its ID for the later accepted pair.
            // A completed UI offer must never release that later family body.
            if(row.npcInitiated&&claims.Any(l=>l.facilityId!="SocialApproach"))return true;
            var actor=runtime.ActorOf(row.personId);var prior=actor?.Brain?.StateMachine.Current;
            bool released=FriendsBodyAuthority.Release(runtime,row.personId,owner,true);
            // The shared release already restores a new original schedule.
            // Only an unchanged native exit state still needs its cancel/resume
            // signal; do not cancel a newly entered ambient seat a second time.
            if(released&&actor&&ReferenceEquals(prior,actor.Brain.StateMachine.Current)&&OriginalNpcActivityPort.CanAmbientOwn(actor))
                OriginalNpcActivityPort.ResumeWhenSafe(actor,owner);
            return released;
        }
        void Finish(FriendsLifeRuntime runtime, FriendWorldTalkRequest row, bool persist = true)
        {
            if(WorldTalkRules.Terminal(row))return;
            if(!TryReleaseTalkBody(runtime,row)){row.phase="Closing";runtime.NotifyChanged();return;}
            contact.Remove(row.requestId);waitSince.Remove(row.requestId);row.phase="Done";runtime.NotifyChanged();if(persist)runtime.SaveNow();
        }
        static bool HasOtherOwner(FriendsLifeRuntime runtime, string person, string ownLease)
            => runtime && runtime.State != null && runtime.State.leases.Any(l => l.personId == person && l.ownerEpisode != ownLease) ||
                FriendsEconomyService.Instance?.HasActiveJob(person) == true;
        void OnDestroy()
        {
            DisposeHeldGiftObservers();
            ReleaseAllConversationHolds(); ClearConversationGoals();
            var runtime=FriendsLifeRuntime.Instance;
            if(runtime&&runtime.Ready&&observed==runtime.State)foreach(var row in runtime.State.worldTalks.Where(r=>!WorldTalkRules.Terminal(r)))TryReleaseTalkBody(runtime,row);
            if(instance==this)instance=null;
        }
    }
}
