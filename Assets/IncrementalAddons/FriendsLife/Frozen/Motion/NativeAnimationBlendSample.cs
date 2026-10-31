using System;

namespace TomodachiNative
{
    // A caller-authored sampling request, not an assertion that ASB timing ran.
    [Serializable] public sealed class NativeAnimationBlendSample
    {
        public NativeAnimationAsset bank;
        public string clipName;
        public float weight = 1, frame;
    }
}
