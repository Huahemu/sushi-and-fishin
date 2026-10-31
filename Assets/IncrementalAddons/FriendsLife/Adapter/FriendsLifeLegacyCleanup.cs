using System;
using System.Linq;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // C009 M0 save cleanup. Runs once for each loaded authority world (State is
    // replaced on every load). Uncommitted episodes that can never resolve, or
    // whose family action is gated off in FriendsLifeFeatureFlags, are kept as
    // history and closed with committed=true, status="Cancelled". Their adapter
    // leases and unanswered NPC offers are released. Nothing is refunded,
    // replayed or re-sent. Casino journeys, casino rounds and their leases are
    // never touched. Episodes that are moving, running or already answered are
    // left to their own flow.
    public sealed partial class FriendsLifeRuntime
    {
        static readonly string[] CleanupWaitingStates = { "Queued", "Suspended", "AwaitSourceInput", "BindingFault", "AwaitHuman" };
        static bool legacyCleanupInstalled;
        public int LegacyCleanupCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BootstrapLegacyCleanup()
        {
            TomodachiLife.Simulation.LifeSimulationRuntime.IslandAccidentsEnabled = FriendsLifeFeatureFlags.IslandExtras;
            if (legacyCleanupInstalled) return;
            legacyCleanupInstalled = true;
            var go = new GameObject("Friends Life Legacy Cleanup"); DontDestroyOnLoad(go);
            go.AddComponent<FriendsLifeLegacyCleanupDriver>();
        }

        /// <summary>Closes stuck or gated episodes. Idempotent: a closed episode is committed and never visited again.</summary>
        public int RunLegacyCleanup()
        {
            if (!Ready || !IsAuthority || committing) return 0;
            int closed = 0;
            foreach (var episode in State.episodes.Where(e => e != null && !e.committed).ToArray())
            {
                string reason;
                try { reason = LegacyCleanupReason(episode); }
                catch (Exception error) { Debug.LogWarning("[FriendsLife] Legacy cleanup skipped " + episode.episodeId + ": " + error); continue; }
                if (reason == null || TouchesCasino(episode) || !OffersReleasable(episode)) continue;
                foreach (var offer in State.worldTalks.Where(r => r.npcInitiated && r.sourceEpisodeId == episode.episodeId && !WorldTalkRules.Terminal(r)))
                { offer.phase = "Done"; offer.ignored = true; }
                episode.committed = true; episode.status = "Cancelled"; episode.fault = "";
                ReleaseEpisodeLeases(episode.episodeId);
                Debug.Log("[FriendsLife] Legacy cleanup cancelled " + episode.sourceKind + " " + episode.episodeId + " (" + reason + ")");
                closed++;
            }
            if (closed > 0) { LegacyCleanupCount += closed; NotifyChanged(); SaveNow(); }
            return closed;
        }

        string LegacyCleanupReason(SourceEpisode episode)
        {
            if (episode.inputAccepted || !CleanupWaitingStates.Contains(episode.status)) return null;
            if (episode.sourceKind == "MiiEvent")
            {
                if (episode.status == "AwaitHuman") return null;
                if (!FriendsLifeFeatureFlags.NativePairEvents) return "native pair events off";
                bool permanent = episode.nativePending == null || episode.branchIndex < 0 || episode.nativePending.usesReconstructedEligibility;
                return episode.status == "BindingFault" && permanent ? "native context unresolved" : null;
            }
            if (episode.sourceKind != "Family") return null;
            if (episode.familyChoice == null) return episode.status == "BindingFault" ? "family choice missing" : null;
            // Care and Grow run their own home-stage flow with home leases; leave them.
            if (UsesFamilyHomeStage(episode)) return null;
            bool human = HasHumanParticipant(episode);
            if (episode.status == "AwaitHuman" && !human) return null;
            string action = episode.familyChoice.action;
            if (!FriendsLifeFeatureFlags.AutoRomance && (action == "Confess" || action == "Date" || action == "Propose" || action == "Wedding" || action == "Breakup" || action == "Reunite")) return "auto romance off";
            if (!FriendsLifeFeatureFlags.Children && (action == "Baby" || action == "Birth")) return "children off";
            if (!FriendsLifeFeatureFlags.Cohabit && action == "Cohabit") return "cohabit off";
            // An NPC-only choice with no source input waits in AwaitSourceInput forever (Fight, Breakup, Reunite today).
            if (!human && episode.status == "AwaitSourceInput" && OriginalAutoInput == null && CompatibleFamilyInput(episode) == null) return "no source input";
            return null;
        }

        bool TouchesCasino(SourceEpisode episode)
        {
            if (Casinoish(episode.episodeId) || Casinoish(episode.sourceId) || Casinoish(episode.opId)) return true;
            return State.leases.Any(l => l.ownerEpisode == episode.episodeId && (l.facilityId == "Casino" || Casinoish(l.leaseId) || Casinoish(l.phase)));
        }
        static bool Casinoish(string value) => !string.IsNullOrEmpty(value) &&
            (value.IndexOf("casino", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("0e8177537fee46889d3afa3b0c06ab0a", StringComparison.OrdinalIgnoreCase) >= 0);
        // Only offers that were never answered and hold no body may be closed. An Unknown result is never replayed.
        bool OffersReleasable(SourceEpisode episode) => State.worldTalks.Where(r => r.sourceEpisodeId == episode.episodeId && !WorldTalkRules.Terminal(r))
            .All(r => r.npcInitiated && !r.inputDispatched && (r.phase == "InvitePending" || r.phase == "Offered"));

        // Debug hook for Root (editor eval): FriendsLifeRuntime.Instance.DebugRunLegacyCleanup()
        public string DebugRunLegacyCleanup() => "closed " + RunLegacyCleanup() + ", total " + LegacyCleanupCount;
        // Debug hook: number of uncommitted episodes the cleanup would close now.
        public int DebugLegacyCleanupCandidates() => !Ready ? 0 : State.episodes.Count(e => e != null && !e.committed && LegacyCleanupReason(e) != null && !TouchesCasino(e) && OffersReleasable(e));
    }

    // One reference compare per frame. No scene search.
    [DefaultExecutionOrder(-121)]
    public sealed class FriendsLifeLegacyCleanupDriver : MonoBehaviour
    {
        FriendsLifeSave cleaned;
        void Update()
        {
            var runtime = FriendsLifeRuntime.Instance;
            if (!runtime || !runtime.Ready || !runtime.IsAuthority || ReferenceEquals(cleaned, runtime.State)) return;
            cleaned = runtime.State;
            try { runtime.RunLegacyCleanup(); }
            catch (Exception error) { Debug.LogWarning("[FriendsLife] Legacy cleanup failed: " + error); }
        }
    }
}
