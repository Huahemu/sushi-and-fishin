using System.Collections;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Wheel;
using UnityEngine;

namespace SushiAndFish.FishingIntegration
{
    // Restores access to the existing HarpoonTool and SeaCreature system.
    // No fish are spawned here, and no replacement catch/weapon is created.
    public sealed class NativeSpearfishingAccess : MonoBehaviour
    {
        Coroutine choosing;

        public static ToolDataNet RestoreBaseKit(ToolDataNet tools)
        {
            if (!tools.HarpoonUnlocked || !tools.DivingEquipmentUnlocked || tools.DivingEquipmentLevel < 1)
            {
                tools.HarpoonUnlocked = true;
                tools.DivingEquipmentUnlocked = true;
                if (tools.DivingEquipmentLevel < 1) tools.DivingEquipmentLevel = 1;
                tools.SyncRevision++;
            }
            return tools;
        }

        public static void UnlockOnServer(FishingActor actor)
        {
            if (actor == null || !actor.world.Server || !actor.player || !actor.player.IsSpawned) return;
            var relay = Singleton<GameProgressManager>.Instance?.RpcRelay;
            if (!relay || !relay.IsSpawned || !relay.IsServer) return;
            actor.SetEquipped(false);
            var before = relay.ToolDataNV.Value;
            var restored = RestoreBaseKit(before);
            if (!before.Equals(restored)) relay.ToolDataNV.Value = restored;
        }

        public void Open(FishingActor actor)
        {
            Cancel();
            Singleton<PPG_WheelManager>.Instance?.DeactiveWheel(false);
            choosing = StartCoroutine(SelectAfterWheelCloses(actor));
        }

        IEnumerator SelectAfterWheelCloses(FishingActor actor)
        {
            yield return null;
            float deadline = Time.unscaledTime + 4;
            bool requested = false;
            while (actor.player && actor.player.IsSpawned && actor.world.Local == actor && Time.unscaledTime < deadline)
            {
                if (actor.player.IsTabletActive) break;
                if (actor.player.CanStartControlAction(PaperPixelGames.Core.Player.PlayerControlAction.ActivateTool))
                {
                    if (!requested)
                    {
                        if (!actor.player.TryDisableActiveToolForModeChange()) break;
                        actor.Send("native-harpoon", 1);
                        requested = true;
                    }
                    if (actor.player.TryEquipNativeHarpoon()) { choosing = null; yield break; }
                }
                yield return null;
            }
            if (actor.player) Singleton<NotifyManager>.Instance?.ShowToast("暂时无法装备水下鱼枪，请先放下物品或结束当前交互。");
            choosing = null;
        }

        public void Cancel()
        {
            if (choosing != null) StopCoroutine(choosing);
            choosing = null;
        }
        void OnDisable() => Cancel();
    }
}
