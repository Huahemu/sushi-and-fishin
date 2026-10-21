namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;
using global::UnityEngine.Rendering;

namespace CasinoLab
{
    // Keep the existing controller, hand bones, press animations and item holders.
    // Serialized references contain only the avatar's original renderers, never held items.
    [DefaultExecutionOrder(31000)]
    public sealed class CasinoLabHandsOnly:MonoBehaviour
    {
        public SkinnedMeshRenderer hands;
        public GameObject handsRoot;
        public Renderer[] hiddenRenderers;
        void Awake(){Apply();}
        void LateUpdate(){Apply();}
        void Apply()
        {
            // Inventory swaps the idle pair for an item's own grip rig while holding it.
            // Preserve that swap so holding an item never produces an extra pair of hands.
            if(hands)hands.forceRenderingOff=false;
            if(hiddenRenderers==null)return;
            foreach(var renderer in hiddenRenderers)
            {
                if(!renderer)continue;
                renderer.enabled=false;renderer.forceRenderingOff=true;renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
        }
    }
}

}
