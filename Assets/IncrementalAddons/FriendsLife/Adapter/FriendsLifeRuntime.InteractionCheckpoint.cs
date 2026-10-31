namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendsLifeRuntime
    {
        // Keep the existing slot/host/storage owner. SaveNow is void; its dirty
        // flag only clears after that owner's successful save. Never consume
        // an item merely because SaveNow returned without throwing.
        internal bool TrySaveInteractionCheckpoint()
        {
            if(!Ready||!IsAuthority||committing||State==null)return false;
            var state=State;string world=state.worldId;int slot=loadedSlot;string host=loadedHost;var slotData=loadedSlotData;
            try
            {
                NotifyChanged();SaveNow();
                return Ready&&IsAuthority&&!committing&&!dirty&&ReferenceEquals(State,state)&&State.worldId==world&&
                    loadedSlot==slot&&loadedHost==host&&ReferenceEquals(loadedSlotData,slotData);
            }
            catch(System.Exception error)
            {UnityEngine.Debug.LogWarning("[FriendsLife] Interaction checkpoint failed: "+error);return false;}
        }
    }
}
