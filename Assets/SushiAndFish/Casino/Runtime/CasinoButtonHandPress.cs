using DG.Tweening;
using PaperPixelGames.Core.Player;
using UnityEngine;

namespace SushiAndFish.Casino
{
    // Match the original random hand, 0.15s OutCirc reach and 0.15s
    // InOutCirc return, applied after the host's own animation evaluation.
    [DefaultExecutionOrder(31000)]
    public sealed class CasinoButtonHandPress : MonoBehaviour
    {
        public PPG_Player player;
        public int CompletedPresses { get; private set; }
        Transform target;
        int handIndex;
        float started;
        bool pressing;
        Vector3 startLocalPosition;
        Quaternion startLocalRotation;
        Transform activeHand;

        public void Press(Transform button, int hand)
        {
            if (!button || !player || player.RigBones == null) return;
            var rig = player.RigBones.FpsRig;
            activeHand = hand == 0 ? rig.RightHand : rig.LeftHand;
            if (!activeHand) return;
            target = button;
            handIndex = hand;
            startLocalPosition = activeHand.localPosition;
            startLocalRotation = activeHand.localRotation;
            started = Time.unscaledTime;
            pressing = true;
        }
        void LateUpdate()
        {
            if (!pressing || !activeHand || !target) return;
            float elapsed = Time.unscaledTime - started;
            if (elapsed >= .3f)
            {
                activeHand.localPosition = startLocalPosition;
                activeHand.localRotation = startLocalRotation;
                pressing = false; CompletedPresses++; return;
            }
            float amount = elapsed <= .15f ? DOVirtual.EasedValue(0, 1, elapsed / .15f, Ease.OutCirc) :
                DOVirtual.EasedValue(1, 0, (elapsed - .15f) / .15f, Ease.InOutCirc);
            var naturalPosition = activeHand.parent.TransformPoint(startLocalPosition);
            var naturalRotation = activeHand.parent.rotation * startLocalRotation;
            float sign = handIndex == 0 ? 1 : -1;
            float scale = ParkCasinoIntegration.Instance ? ParkCasinoIntegration.Instance.CurrentScale : 1f;
            var point = target.position + target.forward * (.25f * scale);
            var rotation = target.rotation * Quaternion.Euler(-60, 35 * sign, 0);
            activeHand.SetPositionAndRotation(Vector3.Lerp(naturalPosition, point, amount), Quaternion.Slerp(naturalRotation, rotation, amount));
        }
    }
}
