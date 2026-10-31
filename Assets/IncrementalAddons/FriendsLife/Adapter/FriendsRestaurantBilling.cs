using System;
using System.Runtime.CompilerServices;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // Manual delivery, staff delivery and the conveyor all resolve the actual
    // seated customer. Host EarnMoney remains the only restaurant revenue path.
    public static class FriendsRestaurantBilling
    {
        sealed class ServiceSession { public readonly string id=Guid.NewGuid().ToString("N"); }
        static readonly ConditionalWeakTable<MonoBehaviour,ServiceSession> sessions=new ConditionalWeakTable<MonoBehaviour,ServiceSession>();
        public static string SourceBill(MonoBehaviour source,string sourceItem)=>"sushi/"+sessions.GetValue(source,_=>new ServiceSession()).id+"/"+sourceItem;
        public static bool Prepare(int tableId,ResidentRestaurantTargetType kind,int seat,string billId,int menuGold)
        {
            var economy=FriendsEconomyService.Instance;var manager=Singleton<ResidentManager>.Instance;
            if(!economy||!manager||!manager.TryGetRestaurantResident(tableId,kind,seat,out var actor)||!economy.IsFriend(actor))return true;
            return economy.QuoteAndReserve(economy.PersonId(actor),billId,"SushiRestaurant",Mathf.Max(0,menuGold)*100L,"Sushi");
        }
        public static void Delivered(string billId)
        {
            var economy=FriendsEconomyService.Instance;
            if(economy&&economy.State.bills.Exists(b=>b.billId==billId)&&!economy.DeliverBill(billId))Debug.LogError("Friend restaurant delivery has an unpaid reserved bill: "+billId);
        }
        public static void Cancel(string billId)=>FriendsEconomyService.Instance?.CancelBill(billId);
    }
}
