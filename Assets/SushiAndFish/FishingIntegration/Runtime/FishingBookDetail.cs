using HowToFish;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Framework.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace SushiAndFish.FishingIntegration {
 public sealed class FishingBookDetail:MonoBehaviour {
  FishSO fish;Image image,badge;TMP_Text title,total;BaseButton toggle;bool alternate,nativeAcquired,layoutSaved;Vector2 nativeSize,nativePosition;Color locked,unlocked;float refreshAt;
  public void Bind(FishSO data,Image picture,TMP_Text name,TMP_Text count,Color lockedColor,Color unlockedColor,bool nativeUnlocked){
   if(fish!=data)alternate=false;fish=data;image=picture;title=name;total=count;locked=lockedColor;unlocked=unlockedColor;nativeAcquired=nativeUnlocked;
   if(picture){if(!layoutSaved){nativeSize=picture.rectTransform.sizeDelta;nativePosition=picture.rectTransform.anchoredPosition;layoutSaved=true;}picture.rectTransform.sizeDelta=new Vector2(nativeSize.x,nativeSize.y*.575f);picture.rectTransform.anchoredPosition=nativePosition+Vector2.down;}
   if(!toggle)toggle=FishingTablet.Button(transform,"C 异色",new Vector2(1,1),new Vector2(-15,-11.25f),new Vector2(25,10),()=>{alternate=!alternate;Refresh();});
   if(!badge&&picture){var go=new GameObject("Acquired shiny badge",typeof(RectTransform),typeof(Image));go.transform.SetParent(picture.transform,false);badge=go.GetComponent<Image>();badge.rectTransform.anchorMin=badge.rectTransform.anchorMax=Vector2.one;badge.rectTransform.pivot=Vector2.one;badge.rectTransform.anchoredPosition=new Vector2(-2,0);badge.rectTransform.sizeDelta=new Vector2(22,8);badge.sprite=MigrationConfig.Load().panelSprite;badge.type=Image.Type.Sliced;badge.color=new Color(1,.85f,.47f,1);badge.raycastTarget=false;FishingTablet.Text(go.transform,"异色",new Vector2(.5f,.5f),Vector2.zero,new Vector2(20,7),4.5f);}
   Refresh();
  }
  void OnEnable(){Refresh();}
  void Update(){if(!fish)return;if(toggle&&toggle.gameObject.activeSelf&&toggle.interactable&&UnityEngine.InputSystem.Keyboard.current?.cKey.wasPressedThisFrame==true){alternate=!alternate;Refresh();}else if(Time.unscaledTime>=refreshAt){refreshAt=Time.unscaledTime+.25f;Refresh();}}
  public void Hide(){fish=null;if(image&&layoutSaved){image.rectTransform.sizeDelta=nativeSize;image.rectTransform.anchoredPosition=nativePosition;}if(toggle)toggle.gameObject.SetActive(false);if(badge)badge.gameObject.SetActive(false);}
  void Refresh(){
   if(!fish||!FishingMigration.Instance)return;var record=FishingTablet.Record(fish);var binding=FishingMigration.Instance.Config.Find(fish.Id);if(binding==null){Hide();return;}
   bool showingShiny=FishingTablet.Paint(fish,image,title,alternate,locked,unlocked,nativeAcquired);
   if(total&&record?.Acquired==true)total.text=(record.originalCaptured+record.shinyCaptured).ToString("N0");
   if(badge)badge.gameObject.SetActive(showingShiny);
   bool canToggle=binding.shiny&&record!=null&&record.originalCaptured>0&&record.shinyCaptured>0;
   if(toggle){toggle.gameObject.SetActive(canToggle);toggle.interactable=canToggle;toggle.SetText(alternate?"C 原色":"C 异色");}
  }
 }
}
