using System;
using System.Linq;
using HowToFish;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;
namespace SushiAndFish.FishingIntegration {
 [Serializable] public sealed class FishingWeaponOffer {public int index,price;public string title;public Sprite icon;}
 public static class FishingWeaponRules {
  public static bool Owned(int mask,int index)=>index>=0&&index<5&&(mask&(1<<index))!=0;
  public static int Add(int mask,int index)=>index>=0&&index<5?mask|(1<<index):mask;
 }
 public sealed partial class FishingMigration {
  public bool OwnsWeapon(int index)=>index<0||FishingWeaponRules.Owned(SaveData.ownedWeapons,index);
  internal void BuyWeapon(FishingActor actor,int index){
   if(!Server||!restored||!actor.player)return;
   var offer=Config.weapons?.FirstOrDefault(w=>w.index==index);if(offer==null||offer.price<=0||offer.price>int.MaxValue/100)return;
   if(OwnsWeapon(index)){Notify(actor.player.OwnerClientId,"已经购买这把枪，无需重复付费");return;}
   // RequestHourlySave silently skips a request while a previous save is active.
   // Do not spend money and persist the separate weapon file in that window.
   var saves=Singleton<SaveManager>.Instance;
   if(!saves||saves.SaveFlag){Notify(actor.player.OwnerClientId,"存档尚未准备好或正在保存，请稍后购买");return;}
   var economy=Singleton<GameProgressManager>.Instance?.EconomyHandler;
   int cost=offer.price*100;
   if(!economy||!economy.IsServer||!economy.CanSpendStoredGold(cost)){Notify(actor.player.OwnerClientId,"余额不足 · "+offer.title+"需要 $"+offer.price.ToString("N0"));return;}
   // Legacy saves need one committed native wallet+ownership baseline before buying.
   if(!EnsureNativeWeaponAnchor(saves,economy)){Notify(actor.player.OwnerClientId,"原生存档尚未完成，未扣款，请稍后再试");return;}
   int previous=SaveData.ownedWeapons;SaveData.ownedWeapons=FishingWeaponRules.Add(previous,index);
   // A failed sidecar write must not charge the player for an unsaved weapon.
   if(!SaveWorld()){SaveData.ownedWeapons=previous;Notify(actor.player.OwnerClientId,"钓鱼存档写入失败，未扣款");return;}
   if(!economy.ServerTrySpendStoredMoney(cost,actor.player.OwnerClientId)){
    SaveData.ownedWeapons=previous;
    bool rolledBack=SaveWorld();if(!rolledBack)Debug.LogError("Fishing weapon purchase rollback failed; sidecar ownership may need recovery.");
    Notify(actor.player.OwnerClientId,rolledBack?"扣款未完成，购买已取消":"扣款未完成且存档回退失败，请检查钓鱼存档");return;
   }
   archiveAt=0;PublishWorld();
   saves.RequestHourlySave(true);Notify(actor.player.OwnerClientId,"已购买 "+offer.title+" · 长按 R 从海钓装备中取出");
  }
 }
}
