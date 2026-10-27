using PaperPixelGames.Core.Economy;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using Unity.Netcode;
using UnityEngine;

namespace SushiAndFish.FishingIntegration
{
    // Session-local switch. Only its explicit UI action changes the native balance;
    // opening settings, loading a save or buying an item never grants currency.
    [DisallowMultipleComponent]
    public sealed class TestingCheatMode : MonoBehaviour
    {
        public const int DisplayBalance = 999999;
        public const int StoredBalance = DisplayBalance * 100;
        public bool Enabled { get; private set; }

        public static bool TryGetHostEconomy(out EconomyHandler economy)
        {
            economy = null;
            var network = NetworkManager.Singleton;
            if (!network || !network.IsListening || !network.IsHost || network.ShutdownInProgress) return false;
            if (!Singleton<SaveManager>.TryGetInstance(out var saves) || !saves.HasValidCurrentSlot) return false;
            if (!Singleton<GameProgressManager>.TryGetInstance(out var progress)) return false;
            economy = progress.EconomyHandler;
            return economy && economy.IsSpawned && economy.IsServer;
        }

        public static bool SetForCurrentSession(bool enabled)
        {
            if (!TryGetHostEconomy(out var economy)) return false;
            var mode = economy.GetComponent<TestingCheatMode>();
            if (!mode && !enabled) return true;
            if (!mode) mode = economy.gameObject.AddComponent<TestingCheatMode>();
            if (mode.Enabled == enabled) return true;
            if (enabled)
            {
                // SetBalance also updates the casino's exact-gold storage, both
                // replicated native balances, their HUDs and quest requirements.
                economy.SetBalance(StoredBalance, StoredBalance);
            }
            mode.Enabled = enabled;
            if (enabled)
            {
                if (Singleton<SaveManager>.TryGetInstance(out var saves)) saves.RequestHourlySave(true);
                if (Singleton<NotifyManager>.TryGetInstance(out var notify))
                    notify.ShowToast("作弊模式：金币和零件已各设为 999999");
            }
            return true;
        }

        private void OnDisable() { Enabled = false; }
    }
}
