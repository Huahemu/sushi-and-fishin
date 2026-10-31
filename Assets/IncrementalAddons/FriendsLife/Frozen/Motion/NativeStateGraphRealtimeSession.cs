using System;
using System.Collections.Generic;

namespace TomodachiLife
{
    public sealed class NativeStateGraphRealtimeOptions
    {
        // Explicit application clock: no native frames-per-second default is inferred.
        public double sourceFramesPerSecond;
        public string clockProvenance;
        public NativeStateGraphSampleClock initialClock;
    }

    // Advances only the already supported static timing subset. Graph selection,
    // parameters and historical-frame overrides are snapshots until explicit restart.
    // It does not execute native state transitions, EXB, random or selector update rules.
    public sealed class NativeStateGraphRealtimeSession
    {
        private readonly NativeStateGraphTimedSample[] bases;
        private readonly double[] duration;
        private readonly float[] frames;
        private readonly NativeStateGraphIssue[] issues;
        private readonly bool callerOverrides;
        public double SourceFramesPerSecond { get; }
        public string ClockProvenance { get; }
        public double ElapsedSourceFrames { get; private set; }
        public bool IsPlaying { get; private set; }
        // This means selected clip ranges ended, not native ASB StateEnd execution.
        public bool ClipRangesFinished { get; private set; }
        public bool NativeExecutionVerified => false;
        public bool NativeFrameClockVerified => false;
        public int SampleCount => frames.Length;

        private NativeStateGraphRealtimeSession(NativeStateGraphStaticSamplePlan initial,
            IReadOnlyList<NativeStateGraphClipTimingInfo> metadata, NativeStateGraphRealtimeOptions options)
        {
            SourceFramesPerSecond = options.sourceFramesPerSecond; ClockProvenance = options.clockProvenance;
            callerOverrides = initial.callerOverrides; issues = initial.issues.ToArray();
            bases = initial.samples.ToArray(); duration = new double[bases.Length]; frames = new float[bases.Length];
            for (int i = 0; i < bases.Length; i++) foreach (var clip in metadata) if (clip.animation == bases[i].animation) duration[i] = clip.frames;
            IsPlaying = true;
        }

        public static bool TryCreate(NativeAnimationStateGraph graph, NativeAnimationSelection selection,
            NativeStateGraphParameters parameters, IReadOnlyList<NativeStateGraphClipTimingInfo> clips,
            NativeStateGraphRealtimeOptions options, out NativeStateGraphRealtimeSession session,
            out NativeStateGraphStaticSamplePlan plan, out string error)
        {
            session = null; plan = null; error = null;
            if (options == null || !Finite(options.sourceFramesPerSecond) || options.sourceFramesPerSecond <= 0 || string.IsNullOrWhiteSpace(options.clockProvenance))
            { error = "A positive explicit source-frame rate and clock provenance are required."; return false; }
            var clock = options.initialClock?.Snapshot() ?? new NativeStateGraphSampleClock();
            double start = clock.elapsedFrames;
            if (!Finite(start) || start < 0) { error = "Initial source-frame clock must be finite and nonnegative."; return false; }
            clock.elapsedFrames = 0;
            if (!new NativeStateGraphStaticTiming(graph).TryPlan(selection, parameters?.Snapshot(), clips, clock, out plan))
            { error = LastIssue(plan); return false; }
            var prepared = new NativeStateGraphRealtimeSession(plan, clips, options);
            if (!prepared.TrySeekSourceFrames(start, out error)) return false;
            session = prepared; plan = prepared.CreatePlanSnapshot(); return true;
        }

        public bool TryAdvanceSeconds(double deltaSeconds, out string error)
        {
            error = null;
            if (!Finite(deltaSeconds) || deltaSeconds < 0) { error = "Preview delta must be finite and nonnegative."; return false; }
            if (!IsPlaying) return true;
            return TrySeekSourceFrames(ElapsedSourceFrames + deltaSeconds * SourceFramesPerSecond, out error);
        }

        public bool TrySeekSourceFrames(double elapsedFrames, out string error)
        {
            error = null;
            if (!Finite(elapsedFrames) || elapsedFrames < 0) { error = "Source-frame clock must be finite and nonnegative."; return false; }
            // Validate all samples before committing the clock or any frame.
            for (int i = 0; i < bases.Length; i++)
                if (!Finite((double)bases[i].frame + elapsedFrames * bases[i].playRate)) { error = "Source-frame/rate product overflow."; return false; }
            bool finished = true;
            for (int i = 0; i < bases.Length; i++)
            {
                double frame = bases[i].frame + elapsedFrames * bases[i].playRate;
                if (bases[i].sourceLoop && duration[i] > 0) { frame %= duration[i]; finished = false; }
                else { if (frame < duration[i]) finished = false; frame = Math.Min(frame, duration[i]); }
                frames[i] = (float)frame;
            }
            ElapsedSourceFrames = elapsedFrames; ClipRangesFinished = finished;
            if (finished) IsPlaying = false;
            return true;
        }

        public void Pause() { IsPlaying = false; }
        public bool TryResume(out string error)
        {
            error = null;
            if (ClipRangesFinished) { error = "Selected nonlooping clip ranges ended; seek/restart explicitly before resuming."; return false; }
            IsPlaying = true; return true;
        }

        public bool TryCopyFrames(float[] destination, out string error)
        {
            error = null;
            if (destination == null || destination.Length != frames.Length) { error = "Prepared sample-frame buffer dimensions differ."; return false; }
            Array.Copy(frames, destination, frames.Length); return true;
        }

        public NativeStateGraphStaticSamplePlan CreatePlanSnapshot()
        {
            var result = new NativeStateGraphStaticSamplePlan { supported = true, callerOverrides = callerOverrides };
            foreach (var issue in issues) result.issues.Add(new NativeStateGraphIssue { code = issue.code, scope = issue.scope, detail = issue.detail, node = issue.node, offset = issue.offset });
            for (int i = 0; i < bases.Length; i++)
            {
                var source = bases[i]; result.samples.Add(new NativeStateGraphTimedSample { animation = source.animation, node = source.node, weight = source.weight,
                    frame = frames[i], playRate = source.playRate, sourceLoop = source.sourceLoop, frameController = source.frameController,
                    initialFrame = source.initialFrame, callerInitialFrame = source.callerInitialFrame, initialFrameProvenance = source.initialFrameProvenance });
            }
            return result;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static string LastIssue(NativeStateGraphStaticSamplePlan plan) => plan.issues.Count == 0 ? "Unsupported timing plan." : plan.issues[plan.issues.Count - 1].code + ": " + plan.issues[plan.issues.Count - 1].detail;
    }
}
