using UnityEngine;
namespace TomodachiNative
{
    public sealed class NativeBonePose : MonoBehaviour
    {
        public const uint SegmentScaleCompensateFlag = 1u << 23;
        public string sourceName;
        public Vector3 translation, scale = Vector3.one;
        public Vector4 rotation = new Vector4(0, 0, 0, 1);
        public bool euler = true, segmentScaleCompensate;
        public NativeBonePose nativeParent;
        public Transform compensation;
        public void Apply()
        {
            Vector3 position = NativeMath.Reflect(translation);
            // Animation may enable SSC although the authored bind bone did not.
            // Keep the native bone Transform stable for skinning and attachments.
            if (segmentScaleCompensate && nativeParent != null && compensation == null)
            {
                var pivot = new GameObject("__ssc_" + sourceName).transform;
                pivot.SetParent(transform.parent, false);
                transform.SetParent(pivot, false);
                compensation = pivot;
            }
            if (compensation != null)
            {
                compensation.localPosition = position;
                compensation.localRotation = Quaternion.identity;
                Vector3 parentScale = nativeParent == null ? Vector3.one : nativeParent.scale;
                // T * inverseParentScale * R * S: the translated offset remains
                // affected by the parent's scale. Disabling SSC keeps the pivot.
                compensation.localScale = segmentScaleCompensate && nativeParent != null
                    ? new Vector3(Reciprocal(parentScale.x), Reciprocal(parentScale.y), Reciprocal(parentScale.z))
                    : Vector3.one;
                transform.localPosition = Vector3.zero;
            }
            else transform.localPosition = position;
            transform.localRotation = NativeMath.Rotation(rotation, euler); transform.localScale = scale;
        }
        private static float Reciprocal(float value) => Mathf.Abs(value) > 1e-8f ? 1 / value : 1;
    }
}
