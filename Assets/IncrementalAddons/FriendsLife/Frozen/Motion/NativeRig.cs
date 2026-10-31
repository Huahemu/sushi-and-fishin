using UnityEngine;
namespace TomodachiNative
{
    public sealed class NativeRig : MonoBehaviour
    {
        public NativeModelAsset source;
        public NativeBonePose[] bones;
        public string[] materialNames;
        private void LateUpdate() => Apply();
        public void Apply() { if (bones != null) foreach (var bone in bones) bone.Apply(); }
        public void ResetToBindPose()
        {
            for (int i = 0; i < bones.Length; i++)
            {
                var data = source.data.bones[i]; var pose = bones[i];
                pose.translation = NativeMath.Vector(data.translation); pose.scale = NativeMath.Vector(data.scale);
                pose.rotation = new Vector4(data.rotation[0], data.rotation[1], data.rotation[2], data.rotation[3]);
                pose.euler = data.rotationMode == "EulerXYZ";
                pose.segmentScaleCompensate = (data.flags & NativeBonePose.SegmentScaleCompensateFlag) != 0;
            }
            Apply();
        }
    }
}
