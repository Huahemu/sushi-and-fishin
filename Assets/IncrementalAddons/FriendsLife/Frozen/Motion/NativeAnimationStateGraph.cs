using System;
using System.Collections.Generic;

namespace TomodachiLife
{
    // Schema 2 is a JsonUtility-compatible projection of read_native_stategraphs.py.
    // Selection offsets/ids are retained; remaining records stay in sourceJson.
    [Serializable] public sealed class NativeAnimationStateGraph
    {
        public int schema;
        public string source, sourceJson, sourceSha256, version, name;
        public bool runtimeParity;
        public NativeStateGraphCommand[] commands;
        public NativeStateGraphNode[] nodes;
        public NativeStateGraphValue[] blackboard;
        public NativeStateGraphFloatPropertyRecord[] floatProperties;
        public string[] remaining;
    }

    [Serializable] public sealed class NativeStateGraphValue
    {
        public string name, kind, source, text;
        public int offset, index, component;
        public int extraIndex = -1;
        public uint flags;
        public long integer;
        public float number;
        public bool boolean;
        public float[] vector;
        public string[] tags;
    }

    [Serializable] public sealed class NativeStateGraphCommand
    {
        public int index, offset, node;
        public string name, guid;
        public string[] tags;
        public NativeStateGraphValue frameCount, interpolation;
    }

    [Serializable] public sealed class NativeStateGraphLink
    {
        public int offset, node;
        public NativeStateGraphValue value, min, max;
    }

    [Serializable] public sealed class NativeStateGraphPoint
    {
        public NativeStateGraphValue x, y;
        public int child;
    }

    [Serializable] public sealed class NativeStateGraphFloatPropertyRecord
    {
        public int index, offset;
        public uint[] rawWords;
        public uint parameterWord, curveType;
        public float valueAt4, minimum, maximum;
        public float[] curveData;
        public bool runtimeSemanticsVerified;
    }

    [Serializable] public sealed class NativeStateGraphTriangle { public int a, b, c, reserved; }

    [Serializable] public sealed class NativeStateGraphBlendSpace
    {
        public int triangleOffset, pointOffset;
        public bool childMappingComplete;
        public NativeStateGraphTriangle[] triangles;
        public NativeStateGraphPoint[] points;
    }

    [Serializable] public sealed class NativeStateGraphNode
    {
        public int index, offset, bodyOffset, typeId, attachmentCount, floatCount, floatFlags, noStateEnd;
        public string type, guid;
        public string[] tags;
        public NativeStateGraphValue[] properties;
        public NativeStateGraphLink[] children, frameControllers, events;
        public int stateCount, unknownPlugCount, expressionCount;
        public NativeStateGraphBlendSpace blendSpace;
        public NativeStateGraphValue Property(string name)
        {
            if (properties != null) foreach (var value in properties) if (value.name == name) return value;
            return null;
        }
    }

    public sealed class NativeStateGraphParameters
    {
        private readonly Dictionary<string, NativeStateGraphValue> values = new Dictionary<string, NativeStateGraphValue>(StringComparer.Ordinal);
        public void SetInt(string name, int value) => values[name] = new NativeStateGraphValue { kind = "int", integer = value };
        public void SetFloat(string name, float value) => values[name] = new NativeStateGraphValue { kind = "float", number = value };
        public void SetBool(string name, bool value) => values[name] = new NativeStateGraphValue { kind = "bool", boolean = value };
        public void SetString(string name, string value) => values[name] = new NativeStateGraphValue { kind = "string", text = value };
        public void SetVector(string name, float x, float y, float z) => values[name] = new NativeStateGraphValue { kind = "vec3", vector = new[] { x, y, z } };
        internal bool TryGet(string name, out NativeStateGraphValue value) => values.TryGetValue(name, out value);
        public NativeStateGraphParameters Snapshot()
        {
            var copy = new NativeStateGraphParameters();
            foreach (var pair in values)
            {
                var value = pair.Value;
                copy.values.Add(pair.Key, new NativeStateGraphValue { name = value.name, kind = value.kind, source = value.source, text = value.text,
                    offset = value.offset, index = value.index, component = value.component, extraIndex = value.extraIndex, flags = value.flags,
                    integer = value.integer, number = value.number, boolean = value.boolean,
                    vector = value.vector == null ? null : (float[])value.vector.Clone(), tags = value.tags == null ? null : (string[])value.tags.Clone() });
            }
            return copy;
        }
    }

    [Serializable] public sealed class NativeStateGraphIssue
    {
        public string code, scope, detail;
        public int node, offset;
    }

    [Serializable] public sealed class NativeStateGraphClip
    {
        public string animation;
        public int node, offset;
        public float weight;
        // Native controller ids encountered along this selected path, outer to inner.
        public int[] frameControllers;
    }

    [Serializable] public sealed class NativeAnimationSelection
    {
        public bool selectionSupported;
        // Deliberately always false: events, transitions, timing and partials are not implemented.
        public bool runtimeParity;
        public bool controllerPathsRecorded;
        public string source, command;
        public int commandIndex = -1, rootNode = -1;
        public List<NativeStateGraphClip> clips = new List<NativeStateGraphClip>();
        public List<NativeStateGraphIssue> issues = new List<NativeStateGraphIssue>();
        public List<int> visitedNodes = new List<int>();
    }

    // Stateless selection only. The caller owns time and explicitly requests pose sampling.
    public sealed class NativeAnimationStateGraphEvaluator
    {
        private readonly NativeAnimationStateGraph graph;
        private readonly Dictionary<int, NativeStateGraphNode> nodes = new Dictionary<int, NativeStateGraphNode>();
        private readonly Dictionary<string, NativeStateGraphValue> defaults = new Dictionary<string, NativeStateGraphValue>(StringComparer.Ordinal);
        private readonly string invalidGraph;
        private NativeStateGraphParameters parameters;
        private NativeAnimationSelection result;
        private readonly HashSet<int> active = new HashSet<int>();
        private readonly List<int> controllerPath = new List<int>();
        private const float Epsilon = 0.00001f;

        public NativeAnimationStateGraphEvaluator(NativeAnimationStateGraph source)
        {
            graph = source;
            if (source == null || source.schema != 2 || source.version != "0x41B") { invalidGraph = "Expected runtime schema 2 / ASB 0x41B."; return; }
            if (source.nodes != null) foreach (var node in source.nodes)
            {
                if (node == null || nodes.ContainsKey(node.index)) { invalidGraph = "Null or duplicate native node id."; return; }
                nodes.Add(node.index, node);
            }
            if (source.blackboard != null) foreach (var value in source.blackboard)
                defaults[value.kind + ":" + value.index] = value;
        }

        public bool TrySelectCommand(string command, NativeStateGraphParameters values, out NativeAnimationSelection selection)
        {
            Begin(values);
            result.command = command;
            if (invalidGraph != null) Fail(null, "InvalidGraph", invalidGraph);
            else
            {
                NativeStateGraphCommand found = null;
                if (graph.commands != null) foreach (var entry in graph.commands)
                    if (entry.name == command) { if (found != null) { Fail(null, "AmbiguousCommand", command); break; } found = entry; }
                if (found == null) Fail(null, "UnknownCommand", command);
                else if (!HasBlocker())
                {
                    result.commandIndex = found.index; result.rootNode = found.node;
                    Visit(found.node, 1, 0);
                }
            }
            return Finish(out selection);
        }

        // Useful for inspecting a native body blend node without inventing a command.
        public bool TrySelectNode(int node, NativeStateGraphParameters values, out NativeAnimationSelection selection)
        {
            Begin(values); result.rootNode = node;
            if (invalidGraph != null) Fail(null, "InvalidGraph", invalidGraph); else Visit(node, 1, 0);
            return Finish(out selection);
        }

        private void Begin(NativeStateGraphParameters values)
        {
            parameters = values ?? new NativeStateGraphParameters(); active.Clear(); controllerPath.Clear();
            result = new NativeAnimationSelection { source = graph == null ? null : graph.source, controllerPathsRecorded = true };
        }

        private bool Finish(out NativeAnimationSelection selection)
        {
            result.selectionSupported = !HasBlocker() && result.clips.Count > 0;
            if (!result.selectionSupported) result.clips.Clear();
            selection = result; return result.selectionSupported;
        }

        private bool HasBlocker() => result.issues.Exists(i => i.scope == "selection");
        private void Fail(NativeStateGraphNode node, string code, string detail) => Issue(node, code, detail, "selection");
        private void Issue(NativeStateGraphNode node, string code, string detail, string scope = "runtime")
        {
            result.issues.Add(new NativeStateGraphIssue { node = node == null ? -1 : node.index, offset = node == null ? -1 : node.bodyOffset, code = code, detail = detail, scope = scope });
        }

        private void Visit(int id, float weight, int depth)
        {
            if (weight <= 0 || HasBlocker()) return;
            if (!nodes.TryGetValue(id, out var node)) { Fail(null, "MissingNode", id.ToString()); return; }
            if (depth > 128 || !active.Add(id)) { Fail(node, "CycleOrDepth", "Selection is cyclic or exceeds 128 nodes."); return; }
            result.visitedNodes.Add(id);
            int controllerBase = controllerPath.Count;
            try
            {
                if (node.expressionCount > 0 || node.floatCount > 0) { Fail(node, "UnsupportedExpression", "EXB / Float Property Ex execution is unresolved."); return; }
                if (node.attachmentCount > 0) Issue(node, "UnsupportedAttachments", "Attachment synchronization is not executed.");
                if (node.stateCount > 0 || node.noStateEnd == 0) Issue(node, "UnsupportedTransitions", "State/end transitions are not executed.");
                if (node.unknownPlugCount > 0) { Fail(node, "UnknownPlug", "Unknown plug semantics may affect selection."); return; }
                if (node.events != null && node.events.Length > 0) Issue(node, "UnsupportedEvents", "BAEV/event nodes are retained but not dispatched.");
                if (node.frameControllers != null && node.frameControllers.Length > 0)
                {
                    Issue(node, "UnsupportedFrameControllers", "Selection preserves controller ids; use the separate static timing planner for its supported subset. Stateful synchronization is not executed.");
                    foreach (var link in node.frameControllers) controllerPath.Add(link.node);
                }
                switch (node.typeId)
                {
                    case 3: SelectClip(node, weight); break;
                    case 8: SelectDiscrete(node, weight, depth, "int"); break;
                    case 2: SelectDiscrete(node, weight, depth, "string"); break;
                    case 21: SelectBoolean(node, weight, depth); break;
                    case 1: SelectFloatInterior(node, weight, depth); break;
                    case 6: SelectLinearPair(node, weight, depth); break;
                    case 26: SelectTwoDimensional(node, weight, depth); break;
                    default: Fail(node, "UnsupportedNode", node.type + " is outside the selection subset."); break;
                }
            }
            finally { controllerPath.RemoveRange(controllerBase, controllerPath.Count - controllerBase); active.Remove(id); }
        }

        private bool Resolve(NativeStateGraphNode node, NativeStateGraphValue property, string kind, out NativeStateGraphValue value)
        {
            value = null;
            if (property == null || property.kind != kind) { Fail(node, "MissingProperty", "Expected property kind " + kind); return false; }
            if (property.source == "constant") { value = property; return ValidateNumber(node, value); }
            if (property.source != "blackboard") { Fail(node, "UnsupportedValueSource", property.source + " at native offset " + property.offset); return false; }
            string keyKind = kind == "float" && property.component != 0 ? "vec3" : kind;
            if (!defaults.TryGetValue(keyKind + ":" + property.index, out var entry)) { Fail(node, "MissingBlackboard", keyKind + ":" + property.index); return false; }
            if (entry.extraIndex >= 0) { Fail(node, "UnsupportedExternalBlackboard", entry.name + " extra info index " + entry.extraIndex); return false; }
            if (!parameters.TryGet(entry.name, out value)) value = entry;
            if (value.kind != keyKind) { Fail(node, "ParameterTypeMismatch", entry.name + " expects " + keyKind); return false; }
            if (keyKind == "vec3")
            {
                if (property.component < 1 || property.component > 3 || value.vector == null || value.vector.Length != 3) { Fail(node, "InvalidVector", entry.name); return false; }
                value = new NativeStateGraphValue { kind = "float", number = value.vector[property.component - 1] };
            }
            return ValidateNumber(node, value);
        }

        private bool ValidateNumber(NativeStateGraphNode node, NativeStateGraphValue value)
        {
            if (value.kind == "float" && (float.IsNaN(value.number) || float.IsInfinity(value.number))) { Fail(node, "NonFiniteValue", value.name); return false; }
            return true;
        }

        private void SelectorTiming(NativeStateGraphNode node)
        {
            Issue(node, "StatelessSelector", "Selector child/default rules are evaluated for this call; native selectTiming and slotSync persistence are not executed.");
        }

        private void SelectDiscrete(NativeStateGraphNode node, float weight, int depth, string kind)
        {
            if (!Resolve(node, node.Property("value"), kind, out var selected)) return;
            var children = node.children;
            if (children == null || children.Length == 0) { Fail(node, "MissingChildren", node.type); return; }
            SelectorTiming(node);
            // The last entry is the default, not another equality test. Its stored
            // value often duplicates the first case (e.g. Mii 106: 0 -> WaitCool / default Wait).
            for (int i = 0; i < children.Length - 1; i++)
            {
                if (!Resolve(node, children[i].value, kind, out var candidate)) return;
                bool match = kind == "int" ? selected.integer == candidate.integer : string.Equals(selected.text, candidate.text, StringComparison.Ordinal);
                if (match) { Visit(children[i].node, weight, depth + 1); return; }
            }
            Visit(children[children.Length - 1].node, weight, depth + 1);
        }

        private void SelectBoolean(NativeStateGraphNode node, float weight, int depth)
        {
            if (!Resolve(node, node.Property("value"), "bool", out var value)) return;
            if (node.children == null || node.children.Length != 2) { Fail(node, "BooleanArity", "Expected true child followed by false child."); return; }
            SelectorTiming(node); Visit(node.children[value.boolean ? 0 : 1].node, weight, depth + 1);
        }

        private void SelectClip(NativeStateGraphNode node, float weight)
        {
            if (!Resolve(node, node.Property("animation"), "string", out var animation)) return;
            if (string.IsNullOrEmpty(animation.text)) { Fail(node, "EmptyAnimation", "No skeletal animation name."); return; }
            if (!Resolve(node, node.Property("mirror"), "bool", out var mirror)) return;
            if (mirror.boolean) { Fail(node, "UnsupportedMirror", "Mirrored animation basis is unresolved."); return; }
            if (!Resolve(node, node.Property("mirrorOffset"), "float", out var mirrorOffset)) return;
            if (Math.Abs(mirrorOffset.number) > Epsilon) { Fail(node, "UnsupportedMirrorOffset", "Nonzero mirror offset is unresolved."); return; }
            if (node.children != null && node.children.Length != 0) { Fail(node, "SkeletalChildren", "SkeletalAnimation with children is outside the subset."); return; }
            result.clips.Add(new NativeStateGraphClip { animation = animation.text, node = node.index, offset = node.bodyOffset, weight = weight, frameControllers = controllerPath.ToArray() });
        }

        private void SelectFloatInterior(NativeStateGraphNode node, float weight, int depth)
        {
            if (!Resolve(node, node.Property("value"), "float", out var selected)) return;
            var children = node.children;
            if (children == null || children.Length == 0) { Fail(node, "MissingChildren", node.type); return; }
            int match = -1;
            for (int i = 0; i < children.Length - 1; i++)
            {
                if (!Resolve(node, children[i].min, "float", out var min) || !Resolve(node, children[i].max, "float", out var max)) return;
                if (min.number > max.number) { Fail(node, "InvalidFloatRange", "Min exceeds max at child " + i); return; }
                // The 0x41B structure documents min/max, not comparison tie rules.
                // Refuse exact boundaries rather than guessing inclusive/exclusive priority.
                if (selected.number == min.number || selected.number == max.number) { Fail(node, "UnverifiedFloatBoundary", "Native FloatSelector endpoint rule is unverified."); return; }
                if (selected.number > min.number && selected.number < max.number)
                {
                    if (match >= 0) { Fail(node, "OverlappingFloatRanges", "Native overlapping-range priority is unverified."); return; }
                    match = i;
                }
            }
            SelectorTiming(node);
            Visit(children[match < 0 ? children.Length - 1 : match].node, weight, depth + 1);
        }

        private void SelectLinearPair(NativeStateGraphNode node, float weight, int depth)
        {
            var children = node.children;
            if (children == null || children.Length != 2) { Fail(node, "UnsupportedOneDimensionalTopology", "Only two children with the same authored interval are supported; multi-point basis rules remain unresolved."); return; }
            if (!Resolve(node, node.Property("easing"), "u32", out var easing)) return;
            if (easing.integer != 0) { Fail(node, "UnsupportedOneDimensionalEasing", "Only the authored no-easing mode 0 is supported."); return; }
            if (!Resolve(node, node.Property("weight"), "float", out var value) ||
                !Resolve(node, children[0].min, "float", out var low0) || !Resolve(node, children[0].max, "float", out var high0) ||
                !Resolve(node, children[1].min, "float", out var low1) || !Resolve(node, children[1].max, "float", out var high1)) return;
            if (low0.number != low1.number || high0.number != high1.number || low0.number >= high0.number)
            { Fail(node, "UnsupportedOneDimensionalTopology", "Asymmetric/degenerate child intervals are outside the confirmed linear-pair subset."); return; }
            if (value.number < low0.number || value.number > high0.number)
            { Fail(node, "OutsideBlendInterval", "No unverified clamping/extrapolation is applied."); return; }
            float ratio = (float)(((double)value.number - low0.number) / ((double)high0.number - low0.number));
            Issue(node, "LinearPairSubset", "Authored equal-range two-child blender, easing=0, evaluated as normalized linear weights; native state/update/synchronization parity remains unverified.", "provenance");
            Visit(children[0].node, weight * (1 - ratio), depth + 1);
            Visit(children[1].node, weight * ratio, depth + 1);
        }

        private void SelectTwoDimensional(NativeStateGraphNode node, float weight, int depth)
        {
            var space = node.blendSpace;
            if (space == null || space.points == null || space.triangles == null || node.children == null) { Fail(node, "MissingBlendSpace", "No native triangle/point mapping."); return; }
            if (!space.childMappingComplete) { Fail(node, "UnresolvedBlendChild", "The native blend space contains an unmapped child (including 255); no center animation is invented."); return; }
            if (!Resolve(node, node.Property("weightX"), "float", out var x) || !Resolve(node, node.Property("weightY"), "float", out var y)) return;
            var px = new float[space.points.Length]; var py = new float[space.points.Length];
            for (int i = 0; i < space.points.Length; i++)
            {
                var point = space.points[i];
                if (point.child < 0 || point.child >= node.children.Length) { Fail(node, "UnresolvedBlendChild", "Point " + i + " child " + point.child); return; }
                if (!Resolve(node, point.x, "float", out var vx) || !Resolve(node, point.y, "float", out var vy)) return;
                px[i] = vx.number; py[i] = vy.number;
            }
            foreach (var t in space.triangles)
            {
                if (t.a < 0 || t.b < 0 || t.c < 0 || t.a >= px.Length || t.b >= px.Length || t.c >= px.Length || t.reserved != 0) { Fail(node, "InvalidTriangle", "Triangle index/reserved byte invalid."); return; }
                double denominator = ((double)py[t.b] - py[t.c]) * (px[t.a] - px[t.c]) + ((double)px[t.c] - px[t.b]) * (py[t.a] - py[t.c]);
                if (Math.Abs(denominator) < 1e-12) { Fail(node, "DegenerateTriangle", "Native blend triangle has zero area."); return; }
                double a = (((double)py[t.b] - py[t.c]) * (x.number - px[t.c]) + ((double)px[t.c] - px[t.b]) * (y.number - py[t.c])) / denominator;
                double b = (((double)py[t.c] - py[t.a]) * (x.number - px[t.c]) + ((double)px[t.a] - px[t.c]) * (y.number - py[t.c])) / denominator;
                double c = 1 - a - b;
                if (a < -Epsilon || b < -Epsilon || c < -Epsilon) continue;
                float wa = (float)Math.Max(0, a), wb = (float)Math.Max(0, b), wc = (float)Math.Max(0, c), sum = wa + wb + wc;
                Visit(node.children[space.points[t.a].child].node, weight * wa / sum, depth + 1);
                Visit(node.children[space.points[t.b].child].node, weight * wb / sum, depth + 1);
                Visit(node.children[space.points[t.c].child].node, weight * wc / sum, depth + 1);
                return;
            }
            Fail(node, "OutsideBlendSpace", "Input is outside all authored triangles; extrapolation/clamping is not assumed.");
        }
    }
}
