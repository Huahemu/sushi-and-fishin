using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using SushiAndFish.FriendsLife.SourceGraphs;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendsLifeRuntime
    {
        // R commits its original result immediately. Its source has no TextRef
        // here, so the world projects only the host's existing chat motion.
        // Finishing this motion never calls ResolveFamilyChoice a second time.
        sealed class FamilyMotion
        {
            public PlayableGraph graph;
            public AnimationClipPlayable clip;
            public Animator animator;
            public ResidentController actor;
            public bool rootMotion;
            public AnimatorCullingMode culling;
        }
        readonly Dictionary<string, FamilyMotion> familyMotions = new Dictionary<string, FamilyMotion>();
        long familyPresentationMicros;
        static readonly HashSet<string> TalkActions = new HashSet<string>
        { "Befriend", "BestFriends", "Reconcile", "Confess", "Propose", "Comfort", "Cohabit" };
        static AnimationClip HostChatClip(Animator animator) => animator?.runtimeAnimatorController?.animationClips
            .FirstOrDefault(c => c.name == "beforemeal_chat_f_295467");

        static bool HasFamilyChairPose(SourceEpisode episode, string personId)
        {
            var actions = FriendsHomeWorldActions.Instance;
            if (!actions) return false;
            foreach (var claim in FriendInstances<FriendsHomeFurnitureClaim>.Active())
            {
                if (!claim.Active || claim.personId != personId || claim.ownerEpisode != episode.episodeId) continue;
                var pose = actions.Observe(claim.actionKey);
                if (pose != null && pose.personId == personId && pose.ownerEpisode == episode.episodeId && pose.sourceAction == "Chair" &&
                    pose.phase == "Holding" && pose.arrived && pose.poseReady && pose.supportVerified) return true;
            }
            return false;
        }

        bool BeginFamilyWorldPresentation(SourceEpisode episode)
        {
            if (!TalkActions.Contains(episode.familyChoice.action)) return false;
            var leases = State.leases.Where(l => l.ownerEpisode == episode.episodeId).ToArray();
            // The authored restaurant chat is seated. A standing public stage
            // keeps its real facing/arrival and immediate original R result.
            if (leases.Length == 0 || !leases.Any(l => HasFamilyChairPose(episode, l.personId) && HostChatClip(ActorOf(l.personId)?.VisualAnimator) != null)) return false;
            foreach (var lease in leases) { lease.phase = "FamilyTalk"; lease.visualElapsed = 0; }
            episode.committed = true; episode.status = "WorldPresentation"; episode.fault = "";
            familyPresentationMicros = State.activeMicros;
            NotifyChanged(); SaveNow();
            return true;
        }
        void AdvanceFamilyWorldPresentations()
        {
            float elapsed = familyPresentationMicros == 0 ? 0 : (float)((State.activeMicros - familyPresentationMicros) / 1000000d);
            familyPresentationMicros = State.activeMicros;
            AdvanceNativeDatePresentations(elapsed);
            foreach (var episode in State.episodes.Where(e => e.committed && e.status == "WorldPresentation").ToArray())
            {
                var leases = State.leases.Where(l => l.ownerEpisode == episode.episodeId).ToArray();
                bool pending = false;
                foreach (var lease in leases)
                {
                    var actor = ActorOf(lease.personId); var animator = actor?.VisualAnimator;
                    if (actor == null && GetPerson(lease.personId)?.controllerKind == PersonControllerKind.Npc) { pending = true; continue; }
                    var source = HasFamilyChairPose(episode, lease.personId) ? HostChatClip(animator) : null;
                    if (source == null) continue;
                    lease.visualElapsed += elapsed;
                    if (lease.visualElapsed >= source.length) continue;
                    pending = true;
                    string key = episode.episodeId + "/" + lease.personId;
                    if (!familyMotions.TryGetValue(key, out var motion) || motion.animator != animator)
                    {
                        StopFamilyMotion(key);
                        motion = new FamilyMotion { actor = actor, animator = animator, rootMotion = animator.applyRootMotion, culling = animator.cullingMode };
                        actor.Brain.BeginModelVisibilityHold();
                        animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        motion.graph = PlayableGraph.Create("Friends committed R chat"); motion.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                        motion.clip = AnimationClipPlayable.Create(motion.graph, source); motion.clip.SetSpeed(0); motion.clip.SetApplyFootIK(false);
                        AnimationPlayableOutput.Create(motion.graph, "Existing host chat", animator).SetSourcePlayable(motion.clip);
                        motion.graph.Play(); familyMotions[key] = motion;
                    }
                    motion.clip.SetTime(lease.visualElapsed); motion.graph.Evaluate(0);
                }
                if (pending) continue;
                foreach (var key in familyMotions.Keys.Where(k => k.StartsWith(episode.episodeId + "/", System.StringComparison.Ordinal)).ToArray()) StopFamilyMotion(key);
                ReleaseEpisodeLeases(episode.episodeId);
                episode.status = State.leases.Any(l => l.ownerEpisode == episode.episodeId) ? "Returning" : "Done";
                NotifyChanged(); SaveNow();
            }
        }
        void StopFamilyMotion(string key)
        {
            if (!familyMotions.TryGetValue(key, out var motion)) return;
            if (motion.graph.IsValid()) motion.graph.Destroy();
            if (motion.animator != null) { motion.animator.applyRootMotion = motion.rootMotion; motion.animator.cullingMode = motion.culling; }
            motion.actor?.Brain?.ReleaseModelVisibilityHold();
            familyMotions.Remove(key);
        }
        void StopFamilyMotions()
        { foreach (string key in familyMotions.Keys.ToArray()) StopFamilyMotion(key);foreach(string episode in nativeDateMotions.Keys.Select(k=>k.Substring(0,k.IndexOf('|'))).Distinct().ToArray())StopDatePresentation(episode); }

        void StopDatePresentation(string episodeId)=>StopNativeDatePresentation(episodeId);
    }
}
