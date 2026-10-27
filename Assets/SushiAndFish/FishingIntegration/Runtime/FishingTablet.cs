using System;
using HowToFish;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace SushiAndFish.FishingIntegration {
 public static class FishingTablet {
  public static FishCollectionRecord Record(FishSO fish){var world=FishingMigration.Instance;if(!world||!fish)return null;var binding=world.Config.Find(fish.Id);return binding==null?null:world.SaveData.archive.collection.Find(r=>r.id==binding.definition.originalId);}
  public static bool Known(FishSO fish,bool fallback)=>Record(fish)?.Seen==true||fallback;
  public static bool Acquired(FishSO fish,bool fallback)=>Record(fish)?.Acquired==true||fallback;
  public static bool Paint(FishSO fish,Image image,TMP_Text title,bool showShiny=false,Color? locked=null,Color? unlocked=null,bool nativeAcquired=false){
   var world=FishingMigration.Instance;if(!world||!fish)return false;var binding=world.Config.Find(fish.Id);if(binding==null)return false;
   var record=Record(fish);bool acquired=record?.Acquired==true||nativeAcquired;
   bool shiny=acquired&&binding.shiny&&record!=null&&record.Appearance(showShiny)==FishArchiveAppearance.Shiny;
   ApplyBookIcon(image,shiny?binding.shiny.Sprite:binding.original.Sprite,acquired,shiny?binding.shinyIconMaterial:null,locked??new Color(0,0,0,.35f),unlocked??Color.white);
   if(title&&acquired)title.text=binding.original.LocalizedName;
   return shiny;
  }
  public static void ApplyBookIcon(Image image,Sprite sprite,bool acquired,Material alternate,Color locked,Color unlocked){if(!image)return;image.sprite=sprite;FishingIconGeometry.Attach(image);image.type=Image.Type.Simple;image.preserveAspect=true;image.useSpriteMesh=true;image.material=acquired?alternate:null;image.color=acquired?unlocked:locked;}
  public static void Detail(FishBookDetailPage page,FishSO fish,Image image,TMP_Text title,TMP_Text total,Color locked,Color unlocked,bool nativeAcquired){
   var world=FishingMigration.Instance;if(!world)return;var controller=page.GetComponent<FishingBookDetail>();
   if(!fish||world.Config.Find(fish.Id)==null){if(controller)controller.Hide();return;}
   if(!controller)controller=page.gameObject.AddComponent<FishingBookDetail>();controller.Bind(fish,image,title,total,locked,unlocked,nativeAcquired);
  }
  public static void AttachInventory(InventoryApp app,InventoryItemUI template,Transform container){if(!FishingMigration.Enabled||app.GetComponent<FishingWarehousePage>())return;app.gameObject.AddComponent<FishingWarehousePage>().Bind(template,container);}
  public static void PaintInventory(ItemSO item,Image image,MigrationConfig configuration=null){
   if(!image)return;image.material=null;image.useSpriteMesh=false;if(!item)return;
   var c=configuration?configuration:FishingMigration.Enabled?FishingMigration.Instance.Config:null;if(!c)return;var binding=c.Find(item.Id);if(binding==null)return;
   bool shiny=c.IsShiny(item.Id);image.sprite=binding.Item(shiny).Sprite;FishingIconGeometry.Attach(image);image.type=Image.Type.Simple;image.preserveAspect=true;image.useSpriteMesh=true;image.color=Color.white;image.material=shiny?binding.shinyIconMaterial:null;
  }
  public static void PaintHeld(BaseHoldable held,Image image){
   if(!image)return;image.material=null;image.useSpriteMesh=false;if(!FishingMigration.Enabled||!(held is DeadFish fish))return;
   var c=FishingMigration.Instance.Config;var binding=c.Find(fish.FishId.Value.ToString());if(binding==null)return;
   PaintInventory(binding.Item(c.IsShiny(fish.FishId.Value.ToString())),image);
  }
  public static TMP_Text Text(Transform parent,string text,Vector2 anchor,Vector2 position,Vector2 size,float fontSize=5.5f){
   var go=new GameObject("Fishing label",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var label=go.GetComponent<TextMeshProUGUI>();var config=MigrationConfig.Load();if(config&&config.font)label.font=config.font;label.text=text;label.fontSize=fontSize;label.color=new Color(.2f,.13f,.08f);label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;label.textWrappingMode=TextWrappingModes.NoWrap;var rect=label.rectTransform;rect.anchorMin=rect.anchorMax=anchor;rect.anchoredPosition=position;rect.sizeDelta=size;return label;
  }
  public static BaseButton Button(Transform parent,string title,Vector2 anchor,Vector2 position,Vector2 size,Action clicked){
   var config=MigrationConfig.Load();if(!config||!config.nativeButtonTemplate)return null;
   var button=UnityEngine.Object.Instantiate(config.nativeButtonTemplate,parent);button.gameObject.name="Fishing "+title;
   foreach(var localizer in button.GetComponentsInChildren<UnityEngine.Localization.Components.LocalizeStringEvent>(true))localizer.enabled=false;
   var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;rect.localScale=Vector3.one;
   if(!button.GetComponentInChildren<TMP_Text>(true))Text(button.transform,title,new Vector2(.5f,.5f),Vector2.zero,size-Vector2.one*2);
   foreach(var label in button.GetComponentsInChildren<TMP_Text>(true)){if(config.font)label.font=config.font;label.fontSize=5;label.enableAutoSizing=true;label.fontSizeMin=3.5f;label.fontSizeMax=5.5f;label.raycastTarget=false;}
   button.SetText(title);if(clicked!=null)button.Clicked+=clicked;button.gameObject.SetActive(true);return button;
  }
 }
}
