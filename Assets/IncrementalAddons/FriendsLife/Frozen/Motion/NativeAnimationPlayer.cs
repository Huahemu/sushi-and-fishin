using System;
using System.Collections.Generic;
using UnityEngine;
using TomodachiPortData;

namespace TomodachiNative
{
    public sealed class NativeAnimationPlayer : MonoBehaviour
    {
        public NativeRig rig;
        public NativeAnimationAsset bank;
        public string clipName;
        public float framesPerSecond = 60, speed = 1;
        public bool playing = true, forceLoop;
        public float currentFrame;
        private NativeSkeletalClip clip;
        private readonly Dictionary<string, NativeBonePose> lookup = new Dictionary<string, NativeBonePose>();
        private ResolvedBlend[] blend;
        private NativeRig boundRig;
        private NativeModelAsset boundModel;
        private NativeBonePose[] boundBones;
        private ResolvedBlend[] singleFrameBlend;
        private readonly Dictionary<string, Pose> bindPose = new Dictionary<string, Pose>();
        private int bindingRevision;
        public string LastError { get; private set; }

        private struct Pose
        {
            public Vector3 translation, scale;
            public Vector4 rotation;
            public bool euler, segmentScaleCompensate;
        }
        internal sealed class ResolvedBlend
        {
            public NativeSkeletalClip clip;
            public Dictionary<string, NativeBoneTrack> tracks;
            // Effective flags include authored bind SSC for bones without a track.
            // Prepared handles retain this snapshot rather than choosing a clip
            // at sample time if flags differ between positive-weight sources.
            public Dictionary<string, bool> segmentScaleCompensate;
            public float frame, weight;
        }
        public sealed class PreparedBlend
        {
            internal NativeAnimationPlayer owner;
            internal NativeRig rig;
            internal NativeBonePose[] bones;
            internal NativeModelAsset model;
            internal ResolvedBlend[] entries;
            internal int revision;
            public int SampleCount => entries == null ? 0 : entries.Length;
        }

        private void Awake() { if (rig == null) rig = GetComponent<NativeRig>(); Rebind(); }
        public void Rebind()
        {
            bindingRevision++;
            singleFrameBlend = null;
            lookup.Clear();
            if (rig != null && rig.bones != null) foreach (var bone in rig.bones) lookup[bone.sourceName] = bone;
            clip = bank == null || bank.data?.clips == null ? null : Array.Find(bank.data.clips, c => c.name == clipName);
            if (boundRig != rig || boundModel != rig?.source || !ReferenceEquals(boundBones, rig?.bones) ||
                bindPose.Count == 0 || rig?.source?.data?.bones != null) CaptureBindPose();
        }
        public bool Play(NativeAnimationAsset source, string name, bool loop = false)
        {
            blend = null; bank = source; clipName = name; forceLoop = loop; currentFrame = 0; playing = true; Rebind();
            return TrySampleFrame(0, out _);
        }
        private void Update()
        {
            if (playing && blend != null)
            {
                currentFrame += Time.deltaTime * framesPerSecond * speed;
                SampleResolvedBlend(blend, currentFrame, forceLoop);
                if (!forceLoop)
                {
                    bool finished = true;
                    foreach (var entry in blend) if (entry.clip.loop || currentFrame + entry.frame < entry.clip.frames) { finished = false; break; }
                    if (finished) playing = false;
                }
                return;
            }
            if (!playing || clip == null) return;
            currentFrame += Time.deltaTime * framesPerSecond * speed;
            if (clip.frames > 0 && (forceLoop || clip.loop)) currentFrame = Mathf.Repeat(currentFrame, clip.frames);
            else if (currentFrame > clip.frames) { currentFrame = clip.frames; playing = false; }
            if (!TrySampleFrame(currentFrame, out _)) playing = false;
        }
        public void SampleFrame(float frame)
        {
            TrySampleFrame(frame, out _);
        }

        public bool TrySampleFrame(float frame, out string error)
        {
            error = null;
            if (!Finite(frame)) { LastError = error = "Nonfinite native sample frame."; return false; }
            if (clip == null) Rebind();
            if (clip == null || rig == null) { LastError = error = "Native clip/rig not bound."; return false; }
            if (singleFrameBlend == null && !ResolveBlend(new[] { new NativeAnimationBlendSample { bank = bank, clipName = clipName } }, out singleFrameBlend, out error))
            { LastError = error; return false; }
            var entry = singleFrameBlend[0];
            foreach (var pair in lookup)
            {
                var pose = bindPose[pair.Key];
                if (entry.tracks.TryGetValue(pair.Key, out var track)) pose = SamplePose(track, entry.clip, frame, pose);
                var bone = pair.Value;
                bone.translation = pose.translation; bone.scale = pose.scale;
                bone.rotation = pose.rotation; bone.euler = pose.euler;
                bone.segmentScaleCompensate = entry.segmentScaleCompensate[pair.Key];
            }
            rig.Apply(); LastError = null; return true;
        }

        // Validates the whole request before replacing playback or changing any bone.
        // All clips share the caller's frame clock; ASB frame controllers are not implied.
        public bool TrySetBlend(IReadOnlyList<NativeAnimationBlendSample> samples, bool loop, out string error)
        {
            if (!ResolveBlend(samples, out var resolved, out error)) return false;
            blend = resolved; forceLoop = loop; currentFrame = 0; playing = true;
            SampleResolvedBlend(blend, 0, loop); return true;
        }

        // A deterministic pose-only sample. It does not replace the current playback state.
        public bool TrySampleBlend(IReadOnlyList<NativeAnimationBlendSample> samples, out string error)
        {
            if (!ResolveBlend(samples, out var resolved, out error)) return false;
            SampleResolvedBlend(resolved, 0, false, false); return true;
        }

        // Prepared source clips must remain immutable. Rebind/rig replacement
        // invalidates the handle; source validation and track indexing happen once.
        public bool TryPrepareBlend(IReadOnlyList<NativeAnimationBlendSample> samples, out PreparedBlend prepared, out string error)
        {
            prepared = null;
            if (!ResolveBlend(samples, out var resolved, out error)) return false;
            prepared = new PreparedBlend { owner = this, rig = rig, bones = rig.bones, model = rig.source, entries = resolved, revision = bindingRevision };
            return true;
        }

        public bool IsPreparedBlendCurrent(PreparedBlend prepared)
        {
            return prepared != null && prepared.owner == this && rig != null && prepared.rig == rig &&
                ReferenceEquals(prepared.bones, rig.bones) && prepared.model == rig.source && prepared.revision == bindingRevision;
        }

        public bool TrySamplePreparedBlend(PreparedBlend prepared, IReadOnlyList<float> frames, out string error)
        {
            error = null;
            if (!IsPreparedBlendCurrent(prepared)) { error = "Prepared native blend is stale; prepare again after rig changes or Rebind."; return false; }
            if (frames == null || frames.Count != prepared.entries.Length) { error = "Prepared native frame dimensions differ."; return false; }
            for (int i = 0; i < frames.Count; i++) if (!Finite(frames[i])) { error = "Nonfinite prepared native frame."; return false; }
            for (int i = 0; i < frames.Count; i++) prepared.entries[i].frame = frames[i];
            SampleResolvedBlend(prepared.entries, 0, false, false); return true;
        }

        private void CaptureBindPose()
        {
            bindPose.Clear(); boundRig = rig; boundModel = rig?.source; boundBones = rig?.bones;
            if (rig?.bones == null) return;
            foreach (var bone in rig.bones)
                bindPose[bone.sourceName] = new Pose { translation = bone.translation, scale = bone.scale, rotation = bone.rotation,
                    euler = bone.euler, segmentScaleCompensate = bone.segmentScaleCompensate };
            // Prefer authored source transforms, independent of the currently sampled pose.
            if (rig.source?.data?.bones != null) foreach (var data in rig.source.data.bones)
                bindPose[data.name] = new Pose { translation = NativeMath.Vector(data.translation), scale = NativeMath.Vector(data.scale),
                    rotation = new Vector4(data.rotation[0], data.rotation[1], data.rotation[2], data.rotation[3]), euler = data.rotationMode == "EulerXYZ",
                    segmentScaleCompensate = (data.flags & NativeBonePose.SegmentScaleCompensateFlag) != 0 };
        }

        private bool ResolveBlend(IReadOnlyList<NativeAnimationBlendSample> samples, out ResolvedBlend[] resolved, out string error)
        {
            resolved = null; error = null;
            if (rig == null) rig = GetComponent<NativeRig>();
            if (rig?.bones == null) { error = "Native rig/bones not bound."; return false; }
            if (lookup.Count == 0 || boundRig != rig || boundModel != rig.source || !ReferenceEquals(boundBones, rig.bones)) Rebind();
            if (samples == null || samples.Count == 0) { error = "No blend samples."; return false; }
            var entries = new List<ResolvedBlend>(); double total = 0;
            foreach (var sample in samples)
            {
                if (sample == null || !Finite(sample.weight) || sample.weight < 0 || !Finite(sample.frame)) { error = "Invalid blend weight/frame."; return false; }
                if (sample.weight == 0) continue;
                var clips = sample.bank?.data?.clips;
                var selected = clips == null ? null : Array.Find(clips, c => c.name == sample.clipName);
                if (selected == null) { error = "Native clip missing: " + sample.clipName; return false; }
                if (selected.frames < 0) { error = "Negative native clip duration: " + sample.clipName; return false; }
                if (selected.rotationMode != "EulerXYZ" && selected.rotationMode != "Quaternion") { error = "Unsupported clip rotation mode: " + selected.rotationMode; return false; }
                var tracks = new Dictionary<string, NativeBoneTrack>();
                if (selected.tracks == null) { error = "Native clip has no track array: " + sample.clipName; return false; }
                foreach (var track in selected.tracks)
                {
                    if (track == null || string.IsNullOrEmpty(track.name) || tracks.ContainsKey(track.name)) { error = "Null/duplicate bone track: " + sample.clipName; return false; }
                    if (!ValidTrack(track)) { error = "Malformed native track: " + sample.clipName + "/" + track.name; return false; }
                    tracks.Add(track.name, track);
                }
                var ssc = new Dictionary<string, bool>();
                foreach (var pair in lookup)
                    ssc[pair.Key] = tracks.TryGetValue(pair.Key, out var track)
                        ? (track.transformFlags & NativeBonePose.SegmentScaleCompensateFlag) != 0
                        : bindPose[pair.Key].segmentScaleCompensate;
                entries.Add(new ResolvedBlend { clip = selected, tracks = tracks, segmentScaleCompensate = ssc, frame = sample.frame, weight = sample.weight }); total += sample.weight;
            }
            if (entries.Count == 0 || total <= 0 || double.IsInfinity(total)) { error = "Blend has no positive finite total weight."; return false; }
            foreach (var pair in lookup)
                for (int i = 1; i < entries.Count; i++)
                    if (entries[i].segmentScaleCompensate[pair.Key] != entries[0].segmentScaleCompensate[pair.Key])
                    { error = "Unsupported native SSC flag conflict for bone: " + pair.Key; return false; }
            foreach (var entry in entries) entry.weight = (float)(entry.weight / total);
            resolved = entries.ToArray(); return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool ValidTrack(NativeBoneTrack track)
        {
            if (((track.baseFlags & 8) != 0 && !ValidValues(track.scale, 3)) || ((track.baseFlags & 16) != 0 && !ValidValues(track.rotation, 4)) ||
                ((track.baseFlags & 32) != 0 && !ValidValues(track.translation, 3))) return false;
            if (track.curves == null) return true;
            foreach (var curve in track.curves)
            {
                if (curve == null || curve.frames == null || curve.keys == null || !Finite(curve.scale) || !Finite(curve.offset)) return false;
                if (curve.kind != "Cubic" && curve.kind != "Linear" && curve.kind != "StepInt" && curve.kind != "StepBool") return false;
                int needed = curve.kind == "Cubic" ? 4 : curve.kind == "Linear" ? 2 : 1;
                if (curve.elements < needed || curve.keys.Length < (long)curve.frames.Length * curve.elements) return false;
                int target = curve.targetOffset;
                if (target % 4 != 0 || !((target >= 4 && target <= 12) || (target >= 16 && target <= 24) || (target >= 32 && target <= 44))) return false;
                for (int i = 0; i < curve.frames.Length; i++) if (!Finite(curve.frames[i]) || (i > 0 && curve.frames[i] < curve.frames[i - 1])) return false;
                foreach (float key in curve.keys) if (!Finite(key)) return false;
            }
            return true;
        }
        private static bool ValidValues(float[] values, int count)
        {
            if (values == null || values.Length < count) return false;
            for (int i = 0; i < count; i++) if (!Finite(values[i])) return false;
            return true;
        }

        private void SampleResolvedBlend(ResolvedBlend[] entries, float elapsed, bool loop, bool respectClipLoop = true)
        {
            foreach (var pair in lookup)
            {
                var baseline = bindPose[pair.Key];
                Vector3 translation = Vector3.zero, scale = Vector3.zero;
                Vector4 rotation = Vector4.zero; Quaternion reference = Quaternion.identity; bool first = true;
                foreach (var entry in entries)
                {
                    var pose = baseline;
                    float frame = entry.frame + elapsed;
                    if (entry.clip.frames > 0 && (loop || (respectClipLoop && entry.clip.loop))) frame = Mathf.Repeat(frame, entry.clip.frames);
                    else frame = Mathf.Clamp(frame, 0, entry.clip.frames);
                    if (entry.tracks.TryGetValue(pair.Key, out var track)) pose = SamplePose(track, entry.clip, frame, baseline);
                    translation += pose.translation * entry.weight; scale += pose.scale * entry.weight;
                    // NativeMath.Rotation applies X reflection. Undo its quaternion reflection
                    // to accumulate in source coordinates; NativeBonePose.Apply reflects once.
                    Quaternion reflected = NativeMath.Rotation(pose.rotation, pose.euler);
                    Quaternion q = new Quaternion(reflected.x, -reflected.y, -reflected.z, reflected.w);
                    if (first) { reference = q; first = false; }
                    float sign = Quaternion.Dot(reference, q) < 0 ? -1 : 1;
                    rotation += new Vector4(q.x, q.y, q.z, q.w) * (entry.weight * sign);
                }
                float length = rotation.magnitude;
                var bone = pair.Value; bone.translation = translation; bone.scale = scale;
                bone.rotation = length > 1e-8f ? rotation / length : new Vector4(0, 0, 0, 1); bone.euler = false;
                bone.segmentScaleCompensate = entries[0].segmentScaleCompensate[pair.Key];
            }
            rig.Apply();
        }

        private static Pose SamplePose(NativeBoneTrack track, NativeSkeletalClip selected, float frame, Pose baseline)
        {
            var pose = baseline;
            pose.segmentScaleCompensate = (track.transformFlags & NativeBonePose.SegmentScaleCompensateFlag) != 0;
            if ((track.baseFlags & 8) != 0) pose.scale = NativeMath.Vector(track.scale);
            if ((track.baseFlags & 16) != 0) pose.rotation = new Vector4(track.rotation[0], track.rotation[1], track.rotation[2], track.rotation[3]);
            if ((track.baseFlags & 32) != 0) pose.translation = NativeMath.Vector(track.translation);
            // If the clip supplies no rotation data, preserve the bind pose encoding.
            bool hasRotation = (track.baseFlags & 16) != 0;
            if (track.curves != null) foreach (var curve in track.curves)
            {
                float value = Evaluate(curve, frame); int offset = curve.targetOffset;
                if (offset >= 4 && offset <= 12) { var v = pose.scale; v[(offset - 4) / 4] = value; pose.scale = v; }
                else if (offset >= 16 && offset <= 24) { var v = pose.translation; v[(offset - 16) / 4] = value; pose.translation = v; }
                else if (offset >= 32 && offset <= 44) { var v = pose.rotation; v[(offset - 32) / 4] = value; pose.rotation = v; hasRotation = true; }
            }
            if (hasRotation) pose.euler = selected.rotationMode == "EulerXYZ";
            return pose;
        }
        public static float Evaluate(NativeCurve curve, float frame)
        {
            int count = curve.frames.Length;
            if (count == 0) return curve.offset;
            int index = Array.BinarySearch(curve.frames, frame);
            if (index < 0) index = Mathf.Clamp(~index - 1, 0, count - 1);
            int offset = index * curve.elements;
            float u = index + 1 < count && curve.frames[index + 1] > curve.frames[index]
                ? Mathf.Clamp01((frame - curve.frames[index]) / (curve.frames[index + 1] - curve.frames[index])) : 0;
            float value;
            if (curve.kind == "Cubic") value = ((curve.keys[offset + 3] * u + curve.keys[offset + 2]) * u + curve.keys[offset + 1]) * u + curve.keys[offset];
            else if (curve.kind == "Linear") value = curve.keys[offset] + curve.keys[offset + 1] * u;
            else value = curve.keys[offset];
            return value * curve.scale + curve.offset;
        }
    }
}
