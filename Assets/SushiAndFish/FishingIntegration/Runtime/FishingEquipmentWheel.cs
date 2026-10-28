using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Wheel;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Core.Player;
using UnityEngine;
namespace SushiAndFish.FishingIntegration {
 public sealed class FishingEquipmentWheel:MonoBehaviour {
  FishingActor actor;PPG_WheelManager manager;PPG_Wheel page;readonly List<ScriptableObject> slices=new List<ScriptableObject>();int shownMask=-1;Coroutine opening,choosing;bool choiceQueued;
  public bool Bind(FishingActor owner){actor=owner;manager=Singleton<PPG_WheelManager>.Instance;if(!manager||!manager.ToolWheel||!manager.ToolWheel.ElementTemplate)return false;
   var source=manager.ToolWheel;page=Instantiate(source,source.transform.parent);page.name="Fishing equipment page";page.SelectSlice=null;page.ClearRuntimePage();page.SelectSlice+=OnChoice;Rebuild();page.Hide(false);return true;
  }
  public void Open(){if(!manager||!page)return;if(opening!=null)StopCoroutine(opening);if(choosing!=null)StopCoroutine(choosing);choiceQueued=false;manager.DeactiveWheel(false);opening=StartCoroutine(OpenPage());}
  void Add(string title,Sprite icon,int index){var slice=ScriptableObject.CreateInstance<FishingEquipmentSlice>();slice.title=title;slice.icon=icon;slice.index=index;slices.Add(slice);}
  void Rebuild(){int mask=actor.world.SaveData.ownedWeapons;if(shownMask==mask)return;shownMask=mask;page.ClearRuntimePage();foreach(var slice in slices)if(slice)Destroy(slice);slices.Clear();var c=actor.world.Config;Add("原版钓竿",actor.player.FishingRod.Icon,-2);Add("海钓竿",c.rodIcon,-1);foreach(var offer in c.weapons.OrderBy(w=>w.index))if(actor.world.OwnsWeapon(offer.index))Add(offer.title,offer.icon,offer.index);Add("收起装备",c.stowIcon,5);page.PopulateRuntimeElements(manager.ToolWheel.ElementTemplate,slices);IsolateIcons(page.gameObject);}
  void IsolateIcons(GameObject root){foreach(var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))if(FishingIconGeometry.IsOutlined(image.sprite)){image.useSpriteMesh=true;image.preserveAspect=true;FishingIconGeometry.Attach(image);}}
  IEnumerator OpenPage(){yield return null;if(manager&&page){Rebuild();manager.ActivateWheel(page,0,false);}opening=null;}
  void OnChoice(IWheelSlice data){if(choiceQueued||!(data is FishingEquipmentSlice slice))return;choiceQueued=true;manager.DeactiveWheel(false);choosing=StartCoroutine(ChooseAfterClose(slice.index));}
  IEnumerator ChooseAfterClose(int index){
   yield return null;float until=Time.unscaledTime+2;float retryAt=0;
   while(actor.player&&Time.unscaledTime<until){
    if(actor.player.IsTabletActive)break;
    if(Time.unscaledTime>=retryAt&&actor.player.CanStartControlAction(PlayerControlAction.ActivateTool)){
     retryAt=Time.unscaledTime+.05f;
     if(index==5){if(actor.player.TryDisableActiveToolForModeChange()){actor.Send("stow",0);choosing=null;yield break;}}
     else if(index==-2){if(actor.player.TryEquipFishingBackend(false)){choosing=null;yield break;}}
     else if(actor.world.OwnsWeapon(index)&&actor.player.TryEquipMigratedFishing()){
      actor.Send("weapon",index);
      if(actor.world.Server||(actor.world.ActorSnapshots.TryGetValue(actor.player.OwnerClientId,out var state)&&state.equipped&&state.equipment==index)){choosing=null;yield break;}
     }
    }
    yield return null;
   }
   if(actor.player)actor.world.Notify(actor.player.OwnerClientId,"暂时无法切换装备 · 请先放下手中物品或结束当前交互");choosing=null;
  }
  void LateUpdate(){if(manager&&page&&manager.IsWheelOpen(page)&&shownMask!=actor.world.SaveData.ownedWeapons)Rebuild();}
  void OnDestroy(){if(manager&&page&&manager.IsWheelOpen(page))manager.DeactiveWheel(false);if(page)Destroy(page.gameObject);foreach(var slice in slices)if(slice)Destroy(slice);}
 }
}
