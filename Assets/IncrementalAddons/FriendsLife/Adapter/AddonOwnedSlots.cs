using SushiAndFish.Casino.Original;
namespace SushiAndFish.FriendsLife
{
    public sealed class AddonOwnedSlots:IncrementalAddons.Casino.SourceReuse.Slots
    {
        public FriendsCasinoCloneDriver Driver;
        public override void TryStartGame(PlayerInteract input){if(!Driver||!Driver.Owned||input!=Driver.Input||CasinoInvocation.Current?.owner!=Driver)return;base.TryStartGame(input);}
        protected override void OnDisable(){if(Driver&&Driver.Suspended)return;if(Driver&&Driver.Owned){using(CasinoInvocation.EnterOwned(Driver,"disable"))base.OnDisable();}else base.OnDisable();}
    }
}
