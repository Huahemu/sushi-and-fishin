using System;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // Proposed public port only. It has no runtime detour and is inert for all
    // original residents without a FriendWorldActor binding.
    public static class FriendsSushiDeliveryGate
    {
        public delegate bool BeforeDeliveryHandler(MonoBehaviour source,string personId,int tableId,ResidentRestaurantTargetType targetType,int seatIndex,int originalOrderId,ulong foodNetworkId,int exactMenuGold);
        static BeforeDeliveryHandler handler;
        public static string LastFailure{get;private set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){handler=null;LastFailure=null;}
        public static void Register(BeforeDeliveryHandler provider)
        {
            if(handler!=null&&handler!=provider)throw new InvalidOperationException("A sushi delivery authority is already registered");
            handler=provider;
        }
        public static void Unregister(BeforeDeliveryHandler provider){if(handler==provider)handler=null;}
        public static bool BeforeDelivery(MonoBehaviour source,int tableId,ResidentRestaurantTargetType targetType,int seatIndex,int originalOrderId,ulong foodNetworkId,int exactMenuGold)
            =>BeforeDelivery(source,tableId,targetType,seatIndex,originalOrderId,foodNetworkId,()=>exactMenuGold);
        public static bool BeforeDelivery(MonoBehaviour source,int tableId,ResidentRestaurantTargetType targetType,int seatIndex,int originalOrderId,ulong foodNetworkId,Func<int> exactSourcePrice)
        {
            var manager=Singleton<ResidentManager>.Instance;
            if(!manager||!manager.TryGetRestaurantResident(tableId,targetType,seatIndex,out var actor)||!actor||!actor.GetComponent<FriendWorldActor>())return true;
            var identity=actor.GetComponent<FriendWorldActor>();var runtime=FriendsLifeRuntime.Instance;var economy=FriendsEconomyService.Instance;
            // Only a positively bound friend takes the additive branch. All
            // source residents above return before price/provider evaluation.
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!economy||!economy.IsFriend(actor)||string.IsNullOrEmpty(identity.personId)||handler==null)
            {LastFailure="Bound friend delivery authority is not ready";return false;}
            try
            {
                int price=exactSourcePrice();if(price<0){LastFailure="The actual source menu price is invalid";return false;}
                bool accepted=handler(source,identity.personId,tableId,targetType,seatIndex,originalOrderId,foodNetworkId,price);
                LastFailure=accepted?null:"The friend could not reserve this actual order";return accepted;
            }
            catch(Exception error)
            {LastFailure=error.GetBaseException().Message;Debug.LogWarning("[FriendsLife] Bound sushi delivery refused: "+LastFailure);return false;}
        }
    }
}
