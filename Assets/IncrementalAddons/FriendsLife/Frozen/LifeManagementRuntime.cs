using System;
using System.Linq;

namespace TomodachiLife.Simulation
{
    public sealed partial class LifeSimulationRuntime
    {
        public LifeSimulationResolution SellInventoryItem(string category,string name,int quantity=1)
        {
            var stack=State.inventory.FirstOrDefault(s=>s.category==category&&s.itemName==name&&s.quantity>0);
            if(stack==null||quantity<1||quantity>stack.quantity)return Fail("库存不足，无法出售。");
            int unitPrice=category=="Goods"?Rules.FindGoods(name)?.sellingPriceRaw??0:0;
            if(unitPrice<=0)return Fail("回收店暂时不收这种物品。");
            int value=(int)Math.Min(99999999L,(long)unitPrice*quantity);var result=new LifeSimulationResolution{walletBefore=State.walletRaw,provenance="Source GoodsParam selling price; reconstructed inventory sale transaction"};
            stack.quantity-=quantity;State.inventory.RemoveAll(s=>s.quantity==0);ChangeWallet(value,"pawn-shop-sale",result);result.success=true;result.walletAfter=State.walletRaw;result.message="卖出了 "+quantity+" 件物品，收入 "+value+"。";
            Journal("sale",result.message,result.provenance);ActivityChanged();return result;
        }
        public LifeSimulationResolution RemoveResidentFromIsland(string residentId)
        {
            var resident=FindResident(residentId);if(resident==null)return Fail("居民已经离岛了。");
            if(Activities.minigame!=null&&!Activities.minigame.completed&&Activities.minigame.participantIds.Contains(residentId))LeaveMinigame();
            foreach(var trip in Activities.travels.Where(t=>!t.returned&&(t.residentId==residentId||t.partnerId==residentId)).ToArray())ReturnFromTravel(trip.id);
            foreach(var dream in Activities.dreams.Where(d=>d.residentId==residentId&&!d.woken).ToArray())WakeFromDream(dream.id);
            foreach(var house in State.households)house.residentIds.Remove(residentId);
            State.relationships.RemoveAll(r=>r.fromId==residentId||r.toId==residentId);State.pendingEvents.RemoveAll(e=>e.focusId==residentId||e.targetId==residentId||e.anotherResidentId==residentId);State.emotionBalls.RemoveAll(b=>b.residentId==residentId);
            FamilyState.couples.RemoveAll(c=>c.parentAId==residentId||c.parentBId==residentId);FamilyState.pregnancies.RemoveAll(p=>p.parentAId==residentId||p.parentBId==residentId);
            FamilyState.children.RemoveAll(c=>c.residentId==residentId);FamilyState.choices.RemoveAll(q=>q.focusId==residentId||q.targetId==residentId);
            foreach(var suggestion in FamilyState.housingSuggestions)suggestion.residentIds=suggestion.residentIds.Where(id=>id!=residentId).ToArray();FamilyState.housingSuggestions.RemoveAll(s=>s.residentIds.Length==0);FamilyState.revision++;
            // Keep old family profiles as ancestry records for surviving children.
            NormalizeCreativeState().residents.RemoveAll(r=>r.residentId==residentId);State.creative.trips.RemoveAll(t=>t.residentId==residentId);State.creative.revision++;
            DailyState.requests.RemoveAll(r=>r.residentId==residentId||r.targetId==residentId);DailyState.moods.RemoveAll(m=>m.residentId==residentId);DailyState.cooldowns.RemoveAll(c=>c.residentId==residentId);DailyState.revision++;
            State.residents.Remove(resident);string message="向 "+resident.displayName+" 道别了。";Journal("resident-departure",message,Reconstruction,residentId);Changed();return new LifeSimulationResolution{success=true,message=message};
        }
    }
}
