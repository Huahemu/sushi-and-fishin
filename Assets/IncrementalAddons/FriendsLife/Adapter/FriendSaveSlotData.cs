using System;
using System.Runtime.CompilerServices;
using System.Threading;
using PaperPixelGames.Core.SaveSystem;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // ES3's existing derived-type writer includes this payload in the original
    // slot. No baseline serializer or world collector is replaced.
    [Serializable]
    public sealed class FriendSaveSlotData : SaveSlotData, ISerializationCallbackReceiver
    {
        public int FriendsLifeModuleVersion = 1;
        public string FriendsLifeJson = "";
        public long FriendsLifeRevision;

        public void OnBeforeSerialize() => FriendSaveSlotBridge.CaptureForSerialization(this);
        public void OnAfterDeserialize() { }
    }

    public struct FriendSlotCapture
    {
        public string json;
        public long revision;
    }

    public static class FriendSaveSlotBridge
    {
        sealed class Binding
        {
            public int thread;
            public bool capturing;
            public Func<bool> shouldCapture;
            public Func<FriendSlotCapture> capture;
        }
        static readonly ConditionalWeakTable<FriendSaveSlotData, Binding> bindings = new ConditionalWeakTable<FriendSaveSlotData, Binding>();

        public static FriendSaveSlotData EnsureCurrent(SaveManager manager)
        {
            if (!manager || !manager.HasValidCurrentSlot) return null;
            FriendSaveSlotWireType.EnsureRegistered();
            var source = manager.CurrentSaveSlotData;
            if (source is FriendSaveSlotData current) return current;
            // Preserve every public baseline section and its reference. The
            // original collector will populate those sections immediately
            // before its original ES3 write.
            var result = new FriendSaveSlotData
            {
                SlotID = source.SlotID, HostSteamID = source.HostSteamID,
                Version = source.Version, BossGlitchResetApplied = source.BossGlitchResetApplied,
                TemporaryStaffStackRevision = source.TemporaryStaffStackRevision,
                PlayerData = source.PlayerData, RestaurantData = source.RestaurantData,
                GameProgress = source.GameProgress, DailyRecord = source.DailyRecord, TabletData = source.TabletData
            };
            manager.SaveSlotsData[manager.CurrentSaveSlotIndex] = result;
            return result;
        }

        public static void Bind(FriendSaveSlotData slot, Func<bool> shouldCapture, Func<FriendSlotCapture> capture)
        {
            if (slot == null || shouldCapture == null || capture == null) throw new ArgumentNullException(nameof(slot));
            bindings.Remove(slot);
            bindings.Add(slot, new Binding { thread = Thread.CurrentThread.ManagedThreadId, shouldCapture = shouldCapture, capture = capture });
        }

        public static void CaptureForSerialization(FriendSaveSlotData slot)
        {
            if (!bindings.TryGetValue(slot, out var binding)) return;
            // Unity may invoke this interface from a worker thread while
            // serializing inspector state. Only the bound main-thread original
            // save operation is permitted to capture the live world.
            if (binding.thread != Thread.CurrentThread.ManagedThreadId || !binding.shouldCapture()) return;
            if (binding.capturing) throw new InvalidOperationException("Reentrant FriendsLife slot serialization");
            binding.capturing = true;
            try
            {
                var snapshot = binding.capture();
                if (string.IsNullOrEmpty(snapshot.json)) throw new InvalidOperationException("FriendsLife snapshot was empty during host slot serialization");
                slot.FriendsLifeModuleVersion = 1;
                slot.FriendsLifeJson = snapshot.json;
                slot.FriendsLifeRevision = snapshot.revision;
            }
            finally { binding.capturing = false; }
        }
    }
}
