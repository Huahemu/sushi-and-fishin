using System;
using PaperPixelGames.Core.SaveSystem;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendsLifeRuntime
    {
        void BindHostSlotSave(SaveManager manager, FriendSaveSlotData slot)
        {
            FriendSaveSlotBridge.Bind(slot,
                () => manager && manager.SaveFlag && ReferenceEquals(manager.CurrentSaveSlotData, slot),
                () => CaptureHostSlot(slot));
        }

        FriendSlotCapture CaptureHostSlot(FriendSaveSlotData slot)
        {
            if (!Ready || !IsAuthority || !ReferenceEquals(loadedSlotData, slot))
                throw new InvalidOperationException("Host slot is not bound to the active FriendsLife world");
            if (committing) throw new InvalidOperationException("A FriendsLife transaction is still preparing during the host save");
            // Called by the unmodified ES3 writer, after the unmodified world
            // collector has captured the wallet, day and all baseline sections.
            // Main-thread capture cannot interleave another simulation tick.
            string json = ExportSnapshot();
            return new FriendSlotCapture { json = json, revision = State.revision };
        }
    }
}
