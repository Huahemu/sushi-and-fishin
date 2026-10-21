using System;
using Unity.Collections;
using Unity.Netcode;

namespace PaperPixelGames.Core.Economy
{
    public partial class EconomyHandler
    {
        [NonSerialized] public NetworkVariable<FixedString128Bytes> ExactGoldStorage = new NetworkVariable<FixedString128Bytes>(default);
        private bool projectingGold;
        public event Action ExactGoldChanged;

        public decimal ExactGoldBalance => GoldAmount.Decode(ExactGoldStorage.Value.ToString(), ShopBalance.Value, out _);
        public bool InfiniteGold
        {
            get { GoldAmount.Decode(ExactGoldStorage.Value.ToString(), ShopBalance.Value, out bool infinite); return infinite; }
        }
        public string SavedGoldStorage => GoldAmount.Encode(ExactGoldBalance, InfiniteGold);
        public long CasinoGoldCoins => GoldAmount.CasinoProjection(ExactGoldBalance, InfiniteGold);

        private void InitializeExactGold()
        {
            ExactGoldStorage.OnValueChanged += OnExactGoldChanged;
            if (IsServer && ExactGoldStorage.Value.Length == 0)
                SetExactGold(ShopBalance.Value == int.MaxValue ? 0 : ShopBalance.Value, ShopBalance.Value == int.MaxValue);
            UpdateExactGoldHud();
        }

        private void DisposeExactGold() => ExactGoldStorage.OnValueChanged -= OnExactGoldChanged;

        private void OnExactGoldChanged(FixedString128Bytes previous, FixedString128Bytes next)
        {
            UpdateExactGoldHud();
            ExactGoldChanged?.Invoke();
        }

        private void UpdateExactGoldHud() => _economyUI?.MoneyUpdateExact(ExactGoldBalance, InfiniteGold);

        private void SetExactGold(decimal storedCents, bool infinite = false)
        {
            string encoded = GoldAmount.Encode(storedCents, infinite);
            if (ExactGoldStorage.Value.ToString() != encoded)
                ExactGoldStorage.Value = new FixedString128Bytes(encoded);
            int legacy = GoldAmount.LegacyProjection(storedCents, infinite);
            projectingGold = true;
            try { if (ShopBalance.Value != legacy) ShopBalance.Value = legacy; }
            finally { projectingGold = false; }
            UpdateExactGoldHud();
        }

        private void AcceptLegacyGoldWrite(int next)
        {
            if (projectingGold || !IsServer) return;
            SetExactGold(next == int.MaxValue ? 0 : next, next == int.MaxValue);
        }

        public void SetSavedGoldBalance(int legacy, string exactStorage, int scrap)
        {
            decimal value = GoldAmount.Decode(exactStorage, legacy, out bool infinite);
            SetExactGold(value, infinite);
            ScrapBalance.Value = scrap;
        }

        public bool CanSpendStoredGold(decimal amount) => amount >= 0 && (InfiniteGold || ExactGoldBalance >= amount);

        public bool ServerTryChangeStoredGold(decimal delta)
        {
            if (!IsServer || !GoldAmount.TryChange(ExactGoldBalance, InfiniteGold, delta, out decimal next)) return false;
            SetExactGold(next, InfiniteGold);
            return true;
        }

        public bool ServerTryChangeCasinoCoins(long delta, out long balance)
        {
            bool accepted = ServerTryChangeStoredGold((decimal)delta * 100m);
            balance = CasinoGoldCoins;
            return accepted;
        }
    }
}
