using System;
using System.Linq;
using System.Collections.Generic;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Components;

namespace SushiAndFish.FishingIntegration {
 // References supplied by UpgradeApp: both native tools and firearms render in
 // the same controls, so their frames, typography and button states match.
 public sealed class FishingUpgradeView {
  public GameObject content;
  public BaseButton diving,harpoon,scooter,rod,action;
  public BaseButton[] choices;
  public Image preview,moneyIcon,scrapIcon;
  public TMP_Text title,actionLabel,price,scrapPrice,cost;
  public TMP_Text[] specs;
  public Color normal,insufficient;
 }

 public sealed class FishingWeaponShop:MonoBehaviour {
  sealed class TextState {
   readonly TMP_Text text;readonly string value;readonly TMP_FontAsset font;readonly float size,min,max;readonly bool auto;readonly TextWrappingModes wrapping;readonly Color color;
   public TextState(TMP_Text target){text=target;value=target.text;font=target.font;size=target.fontSize;min=target.fontSizeMin;max=target.fontSizeMax;auto=target.enableAutoSizing;wrapping=target.textWrappingMode;color=target.color;}
   public void Restore(){if(!text)return;text.text=value;text.font=font;text.enableAutoSizing=auto;text.fontSize=size;text.fontSizeMin=min;text.fontSizeMax=max;text.textWrappingMode=wrapping;text.color=color;}
  }
  sealed class ImageState {
   readonly Image image;readonly Sprite sprite;readonly Material material;readonly Color color;readonly Image.Type type;readonly bool aspect,mesh;
   public ImageState(Image target){image=target;sprite=target.sprite;material=target.material;color=target.color;type=target.type;aspect=target.preserveAspect;mesh=target.useSpriteMesh;}
   public void Restore(){if(!image)return;image.sprite=sprite;image.material=material;image.color=color;image.type=type;image.preserveAspect=aspect;image.useSpriteMesh=mesh;}
  }
  sealed class RectState {
   readonly RectTransform rect;readonly Transform parent;readonly int sibling;readonly Vector2 min,max,pivot,position,size;readonly Vector3 scale;
   public RectState(RectTransform target){rect=target;parent=target.parent;sibling=target.GetSiblingIndex();min=target.anchorMin;max=target.anchorMax;pivot=target.pivot;position=target.anchoredPosition;size=target.sizeDelta;scale=target.localScale;}
   public void Restore(){if(!rect)return;rect.SetParent(parent,false);rect.SetSiblingIndex(sibling);rect.anchorMin=min;rect.anchorMax=max;rect.pivot=pivot;rect.anchoredPosition=position;rect.sizeDelta=size;rect.localScale=scale;}
  }
  FishingUpgradeView view;MigrationConfig config;FishingWeaponOffer[] offers;BaseButton guns;ImageState nativePreview;bool showGuns;int selected,visibleCount=-1;float refreshAt;
  readonly List<TextState> texts=new List<TextState>();readonly List<RectState> rects=new List<RectState>();readonly Dictionary<LocalizeStringEvent,bool> localizers=new Dictionary<LocalizeStringEvent,bool>();readonly Dictionary<GameObject,bool> visibility=new Dictionary<GameObject,bool>();
  public bool ShowingFirearms=>showGuns;
  public FishingWeaponOffer SelectedOffer=>offers!=null&&offers.Length>0?offers[selected]:null;
  public BaseButton FirearmsButton=>guns;
  public static void Attach(UpgradeApp app,GameObject nativeContent){
   if(!FishingMigration.Enabled||!nativeContent)return;
   var shop=app.GetComponent<FishingWeaponShop>();if(!shop){shop=app.gameObject.AddComponent<FishingWeaponShop>();shop.Build(app.CreateFishingUpgradeView());}
   shop.RefreshToolButtons();shop.TryRefresh();
  }
  public void Build(FishingUpgradeView nativeView){
   if(view!=null)return;view=nativeView;config=MigrationConfig.Load();offers=config.weapons.OrderBy(w=>w.price).ToArray();nativePreview=new ImageState(view.preview);
   foreach(var label in view.choices.SelectMany(b=>b.GetComponentsInChildren<TMP_Text>(true)).Concat(new[]{view.title,view.actionLabel,view.price,view.scrapPrice,view.cost}).Concat(view.specs).Where(t=>t).Distinct()){
    texts.Add(new TextState(label));foreach(var localizer in label.GetComponents<LocalizeStringEvent>())localizers[localizer]=localizer.enabled;
   }
   foreach(var target in new Component[]{view.preview,view.cost,view.price,view.moneyIcon}.Where(t=>t))rects.Add(new RectState((RectTransform)target.transform));
   foreach(var target in new Component[]{view.cost,view.price,view.moneyIcon,view.scrapPrice,view.scrapIcon}.Where(t=>t))visibility[target.gameObject]=target.gameObject.activeSelf;
   guns=Instantiate(view.rod,view.rod.transform.parent);guns.name="Firearms";guns.transform.SetAsLastSibling();guns.gameObject.SetActive(true);
   var icon=guns.GetComponent<Image>();icon.sprite=config.gunIcon;icon.color=Color.white;icon.material=Resources.Load<Material>("FishingWhiteWeaponIcon");icon.type=Image.Type.Simple;icon.preserveAspect=true;icon.useSpriteMesh=true;FishingIconGeometry.Attach(icon);
   var shadow=guns.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.3f,.28f,.24f,.2f);shadow.effectDistance=new Vector2(.45f,-.45f);shadow.useGraphicAlpha=true;
   guns.Clicked+=OpenFirearms;SetSelected(guns,false);RefreshToolButtons();
  }
  void OpenFirearms(){Select(true);}
  public void Select(bool firearms){
   if(view==null)return;
   if(showGuns&&!firearms){foreach(var state in texts)state.Restore();foreach(var state in rects)state.Restore();foreach(var pair in localizers)if(pair.Key)pair.Key.enabled=pair.Value;foreach(var pair in visibility)if(pair.Key)pair.Key.SetActive(pair.Value);nativePreview.Restore();}
   showGuns=firearms;SetSelected(guns,firearms);
   if(firearms){foreach(var pair in localizers)if(pair.Key)pair.Key.enabled=false;RenderSelection();RefreshPurchase();}
  }
  public bool TrySelectWeapon(int slot){if(!showGuns)return false;if(slot>=0&&slot<offers.Length){selected=slot;RenderSelection();RefreshPurchase();}return true;}
  public bool TryBuySelected(){
   if(!showGuns)return false;
   var world=FishingMigration.Instance;if(world&&SelectedOffer!=null&&view.action.interactable)world.Local?.Send("buy-weapon",SelectedOffer.index);
   return true;
  }
  public bool TryRefresh(){if(!showGuns||view==null)return false;RenderSelection();RefreshPurchase();return true;}
  public void RefreshToolButtons(){
   if(view==null||!guns)return;
   int count=new[]{view.diving,view.harpoon,view.scooter,view.rod,guns}.Count(b=>b&&b.gameObject.activeSelf);if(count==visibleCount)return;visibleCount=count;
   var row=(RectTransform)guns.transform.parent;row.anchorMin=row.anchorMax=new Vector2(.5f,1);row.pivot=new Vector2(.5f,1);row.anchoredPosition=Vector2.zero;
   var layout=row.GetComponent<HorizontalLayoutGroup>();if(layout){layout.childAlignment=TextAnchor.MiddleCenter;layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;layout.childControlWidth=false;layout.childControlHeight=false;}
   var frame=(RectTransform)row.parent;frame.sizeDelta=new Vector2(count*24+Mathf.Max(0,count-1)*10+18,frame.sizeDelta.y);LayoutRebuilder.ForceRebuildLayoutImmediate(row);
  }
  void OnEnable(){if(view!=null){RefreshToolButtons();TryRefresh();}}
  void Update(){if(Time.unscaledTime>=refreshAt){refreshAt=Time.unscaledTime+.2f;RefreshToolButtons();if(showGuns)RefreshPurchase();}}
  void RenderSelection(){
   var offer=SelectedOffer;if(offer==null)return;var gun=config.systemPrefab.GetComponent<HowToFish.FishCombat>().guns[offer.index];
   foreach(var tool in new[]{view.diving,view.harpoon,view.scooter,view.rod})SetSelected(tool,false);SetSelected(guns,true);
   for(int i=0;i<view.choices.Length;i++){
    var button=view.choices[i];button.interactable=true;SetSelected(button,i==selected);button.SetText(offers[i].title);
    foreach(var label in button.GetComponentsInChildren<TMP_Text>(true)){label.font=config.font;label.enableAutoSizing=true;label.fontSizeMin=3.5f;label.fontSizeMax=6;label.textWrappingMode=TextWrappingModes.NoWrap;}
   }
   view.preview.sprite=offer.icon;view.preview.material=null;view.preview.color=Color.white;view.preview.type=Image.Type.Simple;view.preview.preserveAspect=true;view.preview.useSpriteMesh=true;FishingIconGeometry.Attach(view.preview);
   var picture=view.preview.rectTransform;picture.anchorMin=picture.anchorMax=picture.pivot=new Vector2(.5f,.5f);picture.anchoredPosition=new Vector2(0,-14);picture.sizeDelta=new Vector2(100,75);
   SetText(view.title,offer.title);SetText(view.specs[0],"伤害："+gun.damage+(gun.pellets>1?" × "+gun.pellets+" 弹丸":""));
   SetText(view.specs[1],"弹匣："+gun.magazine+" 发");SetText(view.specs[2],"射速："+(1f/Mathf.Max(.01f,gun.interval)).ToString("0.#")+" 发/秒 · "+(gun.fullAuto?"连发":"单发"));
   var detail=view.title.transform.parent;
   Place(view.cost.rectTransform,detail,new Vector2(0,23),new Vector2(40,12.5f));Place(view.moneyIcon.rectTransform,detail,new Vector2(44,23),new Vector2(12,12));Place(view.price.rectTransform,detail,new Vector2(60,23),new Vector2(66,12.5f));
   view.cost.gameObject.SetActive(true);view.moneyIcon.gameObject.SetActive(true);view.price.gameObject.SetActive(true);view.scrapPrice.gameObject.SetActive(false);view.scrapIcon.gameObject.SetActive(false);
   SetText(view.cost,"价格");SetText(view.price,offer.price.ToString("N0"));
  }
  static void Place(RectTransform rect,Transform parent,Vector2 position,Vector2 size){rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(0,0);rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;}
  void SetText(TMP_Text text,string value){if(!text)return;text.font=config.font;text.text=value;text.textWrappingMode=TextWrappingModes.NoWrap;}
  void RefreshPurchase(){
   if(view==null||SelectedOffer==null)return;
   var world=FishingMigration.Instance;var economy=Singleton<GameProgressManager>.Instance?.EconomyHandler;int balance=economy?economy.ShopBalance.Value:0;
   RenderPurchaseState(world&&world.OwnsWeapon(SelectedOffer.index),MoneyHelper.IsInfinite(balance)||balance>=SelectedOffer.price*100,world&&world.Local!=null);
  }
  // Presentation only; also used to render authored purchase states outside Play.
  public void RenderPurchaseState(bool owned,bool enough,bool ready){
   SetText(view.actionLabel,owned?"已拥有":enough?"购买":"余额不足");view.action.interactable=!owned&&enough&&ready;view.price.color=!owned&&!enough?view.insufficient:view.normal;view.cost.color=!owned&&!enough?view.insufficient:view.normal;
  }
  static void SetSelected(BaseButton button,bool value){if(!button)return;var selected=button.transform.Find("Selected");if(selected)selected.gameObject.SetActive(value);}
  void OnDestroy(){if(guns){guns.Clicked-=OpenFirearms;}}
 }
}
