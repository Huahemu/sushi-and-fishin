using System.Collections.Generic;
using TomodachiNative;

namespace TomodachiLife
{
    public static class NativeAnimationStateGraphPlayback
    {
        // The caller supplies the bank set and frame explicitly. Ambiguous clip names fail;
        // no bank-name guess or fabricated replacement clip is selected.
        public static bool TryCreateSamples(NativeAnimationSelection selection, IReadOnlyList<NativeAnimationAsset> banks,
            float frame, out NativeAnimationBlendSample[] samples, out string error)
        {
            samples = null; error = null;
            if (selection == null || !selection.selectionSupported || selection.clips == null || selection.clips.Count == 0)
            { error = "State graph selection is unsupported or empty."; return false; }
            if (banks == null) { error = "No animation banks supplied."; return false; }
            var result = new List<NativeAnimationBlendSample>();
            foreach (var chosen in selection.clips)
            {
                NativeAnimationAsset bank = null;
                foreach (var candidate in banks)
                {
                    if (candidate?.data?.clips == null) continue;
                    foreach (var clip in candidate.data.clips)
                    {
                        if (clip.name != chosen.animation) continue;
                        if (bank != null) { error = "Ambiguous native animation: " + chosen.animation; return false; }
                        bank = candidate;
                    }
                }
                if (bank == null) { error = "Native animation not found in supplied banks: " + chosen.animation; return false; }
                result.Add(new NativeAnimationBlendSample { bank = bank, clipName = chosen.animation, weight = chosen.weight, frame = frame });
            }
            samples = result.ToArray(); return true;
        }

        public static bool TryCreateTimedSamples(NativeAnimationStateGraph graph, NativeAnimationSelection selection,
            NativeStateGraphParameters parameters, IReadOnlyList<NativeAnimationAsset> banks, NativeStateGraphSampleClock clock,
            out NativeAnimationBlendSample[] samples, out NativeStateGraphStaticSamplePlan plan, out string error)
        {
            samples = null; plan = null;
            if (!TryCreateSamples(selection, banks, 0, out var resolved, out error)) return false;
            var metadata = new List<NativeStateGraphClipTimingInfo>();
            var names = new HashSet<string>();
            foreach (var sample in resolved)
            {
                if (!names.Add(sample.clipName)) continue;
                foreach (var clip in sample.bank.data.clips) if (clip.name == sample.clipName)
                    metadata.Add(new NativeStateGraphClipTimingInfo { animation = clip.name, frames = clip.frames, loop = clip.loop });
            }
            if (!new NativeStateGraphStaticTiming(graph).TryPlan(selection, parameters, metadata, clock, out plan))
            {
                error = plan.issues.Count == 0 ? "Static timing plan unsupported." : plan.issues[plan.issues.Count - 1].code + ": " + plan.issues[plan.issues.Count - 1].detail;
                return false;
            }
            for (int i = 0; i < resolved.Length; i++) resolved[i].frame = plan.samples[i].frame;
            samples = resolved; return true;
        }

        public static bool TryCreateRealtimeSession(NativeAnimationStateGraph graph, NativeAnimationSelection selection,
            NativeStateGraphParameters parameters, IReadOnlyList<NativeAnimationAsset> banks, NativeStateGraphRealtimeOptions options,
            out NativeStateGraphRealtimeSession session, out NativeAnimationBlendSample[] initialSamples,
            out NativeStateGraphStaticSamplePlan plan, out string error)
        {
            session = null; initialSamples = null; plan = null;
            if (!TryCreateSamples(selection, banks, 0, out var resolved, out error)) return false;
            var metadata = new List<NativeStateGraphClipTimingInfo>(); var names = new HashSet<string>();
            foreach (var sample in resolved)
            {
                if (!names.Add(sample.clipName)) continue;
                foreach (var clip in sample.bank.data.clips) if (clip.name == sample.clipName)
                    metadata.Add(new NativeStateGraphClipTimingInfo { animation = clip.name, frames = clip.frames, loop = clip.loop });
            }
            if (!NativeStateGraphRealtimeSession.TryCreate(graph, selection, parameters, metadata, options, out session, out plan, out error)) return false;
            for (int i = 0; i < resolved.Length; i++) resolved[i].frame = plan.samples[i].frame;
            initialSamples = resolved; return true;
        }
    }
}
