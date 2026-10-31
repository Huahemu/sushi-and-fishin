using System;
using System.Collections.Generic;

namespace TomodachiLife
{
    [Serializable] public sealed class NativeStateGraphClipTimingInfo
    {
        public string animation;
        public int frames;
        public bool loop;
    }

    public sealed class NativeStateGraphSampleClock
    {
        // The caller defines units as source animation frames, not wall-clock seconds.
        public double elapsedFrames;
        internal readonly Dictionary<int, NativeStateGraphInitialFrameOverride> initialFrames = new Dictionary<int, NativeStateGraphInitialFrameOverride>();
        public void SetInitialFrame(int initialFrameNode, float frame, string provenance)
        { initialFrames[initialFrameNode] = new NativeStateGraphInitialFrameOverride { frame = frame, provenance = provenance }; }
        public NativeStateGraphSampleClock Snapshot()
        {
            var copy = new NativeStateGraphSampleClock { elapsedFrames = elapsedFrames };
            foreach (var pair in initialFrames) copy.SetInitialFrame(pair.Key, pair.Value.frame, pair.Value.provenance);
            return copy;
        }
    }
    internal sealed class NativeStateGraphInitialFrameOverride { public float frame; public string provenance; }

    [Serializable] public sealed class NativeStateGraphTimedSample
    {
        public string animation;
        public int node, frameController = -1, initialFrame = -1;
        public float weight, frame, playRate = 1;
        public bool sourceLoop, callerInitialFrame;
        public string initialFrameProvenance;
    }

    [Serializable] public sealed class NativeStateGraphStaticSamplePlan
    {
        public bool supported, callerOverrides;
        // A deterministic snapshot mapping is not proof of original runtime execution.
        public bool nativeExecutionVerified = false, runtimeParity = false;
        public List<NativeStateGraphTimedSample> samples = new List<NativeStateGraphTimedSample>();
        public List<NativeStateGraphIssue> issues = new List<NativeStateGraphIssue>();
    }

    // Deliberately narrow: default clip range, native loop flag, one play-rate
    // controller per path. History/random/seek/crop/reverse modes fail closed.
    public sealed class NativeStateGraphStaticTiming
    {
        private readonly NativeAnimationStateGraph graph;
        private readonly Dictionary<int, NativeStateGraphNode> nodes = new Dictionary<int, NativeStateGraphNode>();
        private readonly Dictionary<int, NativeStateGraphValue> floatDefaults = new Dictionary<int, NativeStateGraphValue>();

        public NativeStateGraphStaticTiming(NativeAnimationStateGraph graph)
        {
            this.graph = graph;
            if (graph?.nodes != null) foreach (var node in graph.nodes) if (node != null && !nodes.ContainsKey(node.index)) nodes.Add(node.index, node);
            if (graph?.blackboard != null) foreach (var value in graph.blackboard) if (value.kind == "float") floatDefaults[value.index] = value;
        }

        public bool TryPlan(NativeAnimationSelection selection, NativeStateGraphParameters parameters,
            IReadOnlyList<NativeStateGraphClipTimingInfo> clips, NativeStateGraphSampleClock clock,
            out NativeStateGraphStaticSamplePlan plan)
        {
            plan = new NativeStateGraphStaticSamplePlan();
            if (graph == null || graph.schema != 2 || graph.version != "0x41B") return Fail(plan, null, "InvalidGraph", "Expected schema 2 / ASB 0x41B.");
            if (selection == null || !selection.selectionSupported || !selection.controllerPathsRecorded || selection.source != graph.source || selection.clips == null || selection.clips.Count == 0)
                return Fail(plan, null, "InvalidSelection", "Selection must come from this graph and contain supported clips.");
            if (clock == null || !Finite(clock.elapsedFrames) || clock.elapsedFrames < 0)
                return Fail(plan, null, "InvalidSampleClock", "Supply a finite, nonnegative source-frame clock.");
            if (clips == null) return Fail(plan, null, "MissingClipTiming", "No source clip duration/loop metadata.");
            if (selection.commandIndex >= 0)
            {
                var command = graph.commands == null ? null : Array.Find(graph.commands, c => c.index == selection.commandIndex && c.name == selection.command);
                if (command == null || command.frameCount == null || command.frameCount.kind != "float" || command.frameCount.source != "constant" || command.frameCount.number != -1 ||
                    command.interpolation == null || command.interpolation.source != "constant" || command.interpolation.integer != 0)
                    return Fail(plan, null, "UnsupportedCommandTiming", "Only the default command duration/interpolation snapshot is supported.");
            }
            var definitions = new Dictionary<string, NativeStateGraphClipTimingInfo>(StringComparer.Ordinal);
            foreach (var clip in clips)
            {
                if (clip == null || string.IsNullOrEmpty(clip.animation) || clip.frames < 0 || definitions.ContainsKey(clip.animation))
                    return Fail(plan, null, "AmbiguousClipTiming", "Null, duplicate or negative-duration source clip metadata.");
                definitions.Add(clip.animation, clip);
            }
            foreach (var chosen in selection.clips)
            {
                if (!nodes.TryGetValue(chosen.node, out var leaf) || leaf.typeId != 3 || !definitions.TryGetValue(chosen.animation, out var definition))
                    return Fail(plan, null, "MissingClipTiming", chosen.animation);
                var sample = new NativeStateGraphTimedSample { node = chosen.node, animation = chosen.animation, weight = chosen.weight, sourceLoop = definition.loop };
                NativeStateGraphNode controller = null, initial = null;
                if (chosen.frameControllers != null) foreach (int id in chosen.frameControllers)
                {
                    if (!nodes.TryGetValue(id, out var node)) return Fail(plan, leaf, "MissingTimingNode", id.ToString());
                    if (node.typeId == 12 && controller == null) controller = node;
                    else if (node.typeId == 19 && initial == null) initial = node;
                    else return Fail(plan, node, "UnsupportedTimingComposition", "Multiple controllers/initializers or another timing-node type require native inheritance rules.");
                }
                if (controller != null)
                {
                    if (!PlainTimingNode(plan, controller) || !DefaultController(plan, controller)) return false;
                    if (!Float(plan, controller, "playRate", parameters, out var rate) || rate < 0)
                        return Fail(plan, controller, "UnsupportedPlayRate", "Only finite nonnegative play rate snapshots are supported.");
                    sample.playRate = rate; sample.frameController = controller.index;
                }
                float initialFrame = 0;
                if (initial != null)
                {
                    if (!PlainTimingNode(plan, initial) || !Constant(plan, initial, "mode", "u32", out var mode) ||
                        !Expected(plan, initial, "random", "bool", 0)) return false;
                    sample.initialFrame = initial.index;
                    if (mode.integer == 0) initialFrame = 0; // Start enum; no prior-state matching required.
                    else if (clock.initialFrames.TryGetValue(initial.index, out var supplied))
                    {
                        if (!Finite(supplied.frame) || supplied.frame < 0 || supplied.frame > definition.frames || string.IsNullOrWhiteSpace(supplied.provenance))
                            return Fail(plan, initial, "InvalidInitialFrameOverride", "An explicit frame inside the clip and a provenance note are required.");
                        initialFrame = supplied.frame; sample.callerInitialFrame = plan.callerOverrides = true;
                        sample.initialFrameProvenance = supplied.provenance;
                        Issue(plan, initial, "UnsupportedNativeInitialFrame", "Native mode " + mode.integer + " history/tag/random execution is unresolved. This sample uses the recorded caller frame override.", "native-timing");
                    }
                    else return Fail(plan, initial, "UnsupportedNativeInitialFrame", "Mode " + mode.integer + " requires native historical/tag/random state; supply an explicit recorded frame override for pose-only sampling.");
                }
                double frame = initialFrame + (double)clock.elapsedFrames * sample.playRate;
                if (definition.loop && definition.frames > 0) frame %= definition.frames;
                else frame = Math.Min(frame, definition.frames);
                if (double.IsNaN(frame) || double.IsInfinity(frame)) return Fail(plan, controller, "NonFiniteFrame", "Clock/rate overflow.");
                sample.frame = (float)frame; plan.samples.Add(sample);
            }
            plan.supported = true; return true;
        }

        private static bool PlainTimingNode(NativeStateGraphStaticSamplePlan plan, NativeStateGraphNode node)
        {
            if (node.attachmentCount != 0 || node.floatCount != 0 || node.expressionCount != 0 || node.stateCount != 0 || node.unknownPlugCount != 0 ||
                (node.children != null && node.children.Length != 0) || (node.events != null && node.events.Length != 0) ||
                (node.frameControllers != null && node.frameControllers.Length != 0))
                return Fail(plan, node, "UnsupportedTimingNodePlugs", "Timing-node attachments/expressions/transitions/children are outside the static subset.");
            return true;
        }

        private static bool DefaultController(NativeStateGraphStaticSamplePlan plan, NativeStateGraphNode node)
        {
            return Expected(plan, node, "startFrame", "float", 0) && Expected(plan, node, "frameCount", "float", -1) &&
                Expected(plan, node, "loopMode", "u32", 0) && Expected(plan, node, "killLoop", "bool", 0) &&
                Expected(plan, node, "reverse", "bool", 0) && Expected(plan, node, "loopCount", "int", -1) &&
                Expected(plan, node, "randomRange", "int", -1) &&
                Expected(plan, node, "seekPercent", "float", -1) && Expected(plan, node, "seekFrame", "float", -1) &&
                Expected(plan, node, "endFrame", "float", -1) && Expected(plan, node, "endFrameMode", "u32", 0) &&
                Expected(plan, node, "unknown60", "float", -1) && Expected(plan, node, "unknown68", "bool", 0) &&
                Expected(plan, node, "seekDebug", "bool", 0) && Constant(plan, node, "slotSyncRandom", "bool", out _);
        }

        private static bool Expected(NativeStateGraphStaticSamplePlan plan, NativeStateGraphNode node, string name, string kind, float expected)
        {
            if (!Constant(plan, node, name, kind, out var property)) return false;
            double actual = kind == "float" ? property.number : kind == "bool" ? (property.boolean ? 1 : 0) : property.integer;
            return actual == expected || Fail(plan, node, "UnsupportedTimingProperty", name + "=" + actual + " is outside the confirmed default-range snapshot subset.");
        }
        private static bool Constant(NativeStateGraphStaticSamplePlan plan, NativeStateGraphNode node, string name, string kind, out NativeStateGraphValue value)
        {
            value = node.Property(name);
            if (value == null || value.kind != kind || value.source != "constant")
                return Fail(plan, node, "UnsupportedTimingProperty", name + " must be an authored constant " + kind + ".");
            return true;
        }
        private bool Float(NativeStateGraphStaticSamplePlan plan, NativeStateGraphNode node, string name, NativeStateGraphParameters parameters, out float value)
        {
            value = 0; var property = node.Property(name);
            if (property == null || property.kind != "float") return Fail(plan, node, "MissingTimingProperty", name);
            NativeStateGraphValue resolved = property;
            if (property.source == "blackboard" && property.component == 0 && floatDefaults.TryGetValue(property.index, out var entry) && entry.extraIndex < 0)
            {
                resolved = entry;
                if (parameters != null && parameters.TryGet(entry.name, out var supplied)) resolved = supplied;
            }
            else if (property.source != "constant") return Fail(plan, node, "UnsupportedTimingValueSource", name + " source=" + property.source);
            if (resolved.kind != "float" || !Finite(resolved.number)) return Fail(plan, node, "InvalidTimingValue", name);
            value = resolved.number; return true;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool Fail(NativeStateGraphStaticSamplePlan plan, NativeStateGraphNode node, string code, string detail)
        { plan.supported = false; plan.samples.Clear(); Issue(plan, node, code, detail, "timing"); return false; }
        private static void Issue(NativeStateGraphStaticSamplePlan plan, NativeStateGraphNode node, string code, string detail, string scope)
        { plan.issues.Add(new NativeStateGraphIssue { node = node == null ? -1 : node.index, offset = node == null ? -1 : node.bodyOffset, code = code, detail = detail, scope = scope }); }
    }
}
