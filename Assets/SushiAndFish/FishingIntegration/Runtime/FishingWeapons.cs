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
   var economy=Singleton<GameProgressManager>.Instance?.EconomyHandler;
   if(!economy||!economy.ServerTrySpendStoredMoney(offer.price*100,actor.player.OwnerClientId)){Notify(actor.player.OwnerClientId,"余额不足 · "+offer.title+"需要 $"+offer.price.ToString("N0"));return;}
   SaveData.ownedWeapons=FishingWeaponRules.Add(SaveData.ownedWeapons,index);SaveWorld();archiveAt=0;PublishWorld();
   Singleton<SaveManager>.Instance?.RequestHourlySave(true);Notify(actor.player.OwnerClientId,"已购买 "+offer.title+" · 长按 R 从海钓装备中取出");
  }
 }
}
