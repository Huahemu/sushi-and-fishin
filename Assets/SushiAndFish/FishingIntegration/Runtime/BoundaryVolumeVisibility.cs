using UnityEngine.Rendering;

namespace SushiAndFish.FishingIntegration
{
    // Per-client override for the native boundary's local post-processing only.
    // Leave the shared profile, weather, underwater and oxygen effects untouched.
    public sealed class BoundaryVolumeVisibility
    {
        Volume target;
        float originalWeight;
        bool suppressed;

        public void Apply(Volume volume, bool authorisedArea)
        {
            if (target != volume) Release();
            if (!authorisedArea || !volume || volume.isGlobal)
            {
                Release();
                return;
            }
            if (!suppressed)
            {
                target = volume;
                originalWeight = volume.weight;
                suppressed = true;
            }
            volume.weight = 0f;
        }

        public void Release()
        {
            if (suppressed && target) target.weight = originalWeight;
            target = null;
            suppressed = false;
        }
    }
}
