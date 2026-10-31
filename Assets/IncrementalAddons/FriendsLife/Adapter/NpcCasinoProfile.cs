using SushiAndFish.Casino.Original;
namespace SushiAndFish.FriendsLife
{
    // A NEW server input/profile object, never a human or LocalManager player.
    public sealed class NpcCasinoProfile:PlayerProfile
    {
        public override void OnStartClient(){enabled=false;}
        public override void OnStopClient(){}
        void Start(){}
    }
}
