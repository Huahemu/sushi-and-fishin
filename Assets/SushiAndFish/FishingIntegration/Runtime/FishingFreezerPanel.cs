using System;
using System.Linq;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Tablet;
using SushiAndFish.ResidentialIsland;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SushiAndFish.FishingIntegration
{
    public sealed class FishingFreezerPanel:MonoBehaviour
    {
        static FishingFreezerPanel current;
        FishingActor actor;Transform station;RectTransform content;TMP_Text empty;string signature;float nextRefresh;bool closing,ownsInput;
        readonly HousingInputGate escape=new HousingInputGate();
        public static bool IsOpen=>current;
        public static void Open(FishingActor owner,Transform target)
        {
            if(owner==null||owner.IsDefeated||!owner.player||!owner.player.IsOwner)return;
            CloseCurrent();if(owner.player.IsToolActive&&!owner.player.TryDisableActiveToolForModeChange())return;
            var go=new GameObject("Shared fish freezer",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            current=go.AddComponent<FishingFreezerPanel>();current.actor=owner;current.station=target;current.Build(owner.world.Config);current.Draw();
            owner.player.AcquireUIInput(PlayerUIInputOwner.FishingFreezer);current.ownsInput=true;current.escape.Acquire();
        }
        public static void CloseCurrent(){if(current)current.ForceClose();}
        // Used only for edit-time native-UI rendering; acquires no gameplay input.
        public void BuildStaticPreview(MigrationConfig config,WarehouseLine[] rows){Build(config);DrawRows(config,rows);}
        void Build(MigrationConfig config)
        {
            var canvas=GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=80;
            var scale=GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.matchWidthOrHeight=.5f;
            var panel=new GameObject("Native freezer panel",typeof(RectTransform),typeof(Image));panel.transform.SetParent(transform,false);
            var rect=(RectTransform)panel.transform;rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;rect.sizeDelta=new Vector2(920,720);
            var bg=panel.GetComponent<Image>();bg.sprite=config.panelSprite;bg.type=Image.Type.Sliced;
            Label(rect,"共享鱼类冰柜",new Vector2(0,315),new Vector2(750,48),30);
            Label(rect,"寿司店 · 小岛1 · 小岛2 互通",new Vector2(0,270),new Vector2(760,35),20);
            Button(rect,"关闭",new Vector2(375,312),new Vector2(105,44),()=>closing=true);
            Button(rect,"存入手中鱼",new Vector2(-245,-290),new Vector2(240,52),()=>actor.Send("freezer-deposit",1));
            Button(rect,"召回船只",new Vector2(245,-290),new Vector2(240,52),()=>{var owner=actor;ForceClose();owner.Send("boat-recall",1);});
            Label(rect,"E 存鱼 · F 打开冰柜 · F5 召回船只 · Esc 关闭",new Vector2(0,-335),new Vector2(800,32),18);
            var viewport=new GameObject("Fish list viewport",typeof(RectTransform),typeof(Image),typeof(Mask),typeof(ScrollRect));viewport.transform.SetParent(rect,false);
            var vr=(RectTransform)viewport.transform;vr.anchorMin=vr.anchorMax=Vector2.one*.5f;vr.sizeDelta=new Vector2(820,480);vr.anchoredPosition=new Vector2(0,-5);
            viewport.GetComponent<Image>().color=new Color(1,1,1,.03f);viewport.GetComponent<Mask>().showMaskGraphic=false;
            var rows=new GameObject("Stored fish",typeof(RectTransform),typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));rows.transform.SetParent(vr,false);content=(RectTransform)rows.transform;
            content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
            var layout=rows.GetComponent<VerticalLayoutGroup>();layout.childControlWidth=true;layout.childControlHeight=false;layout.childForceExpandHeight=false;layout.spacing=8;
            rows.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=viewport.GetComponent<ScrollRect>();scroll.viewport=vr;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=45;
            empty=Label(rect,"冰柜里还没有鱼",Vector2.zero,new Vector2(700,70),26);
        }
        void Update()
        {
            if(actor==null||!actor.player||!actor.player.IsSpawned||actor.IsDefeated||!station||(actor.player.transform.position-station.position).sqrMagnitude>49){ForceClose();return;}
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true)closing=true;
            if(closing){if(escape.Release())ForceClose();return;}
            if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.3f;Draw();}
        }
        void Draw()
        {
            var world=actor.world;var rows=world.Server?world.SharedStock():world.WarehouseLines;
            DrawRows(world.Config,rows);
        }
        void DrawRows(MigrationConfig config,WarehouseLine[] rows)
        {
            string next=string.Join(";",rows.Select(r=>r.itemId+":"+r.count));if(signature==next)return;signature=next;
            foreach(Transform row in content)Destroy(row.gameObject);empty.gameObject.SetActive(rows.Length==0);
            foreach(var stock in rows)
            {
                var item=config.database.FindById(stock.itemId) as FishSO;if(!item)continue;
                var entry=new GameObject("Stored "+stock.itemId,typeof(RectTransform),typeof(LayoutElement));entry.transform.SetParent(content,false);entry.GetComponent<LayoutElement>().preferredHeight=88;
                if(config.nativeInventoryRow)
                {
                    var card=Instantiate(config.nativeInventoryRow,entry.transform);card.Initialize(item,(int)Math.Min(int.MaxValue,stock.count),"共享冰柜");card.PrepareWarehouseRow();card.SetLongAmount(stock.count);
                    var cr=(RectTransform)card.transform;float factor=Mathf.Min(610/Mathf.Max(1,cr.rect.width),78/Mathf.Max(1,cr.rect.height));cr.anchorMin=cr.anchorMax=cr.pivot=new Vector2(0,.5f);cr.anchoredPosition=new Vector2(4,0);cr.localScale=Vector3.one*factor;
                }
                else Label((RectTransform)entry.transform,item.LocalizedName+" × "+stock.count,Vector2.zero,new Vector2(600,70),22);
                var button=FishingTablet.Button(entry.transform,"取出一条",new Vector2(1,.5f),new Vector2(-85,0),new Vector2(150,44),()=>
                {var owner=actor;ForceClose();owner.Send("freezer-withdraw",1,item.Id);});StyleButton(button);
            }
        }
        TMP_Text Label(RectTransform parent,string title,Vector2 position,Vector2 size,float font)
        {return FishingTablet.Text(parent,title,Vector2.one*.5f,position,size,font);}
        void Button(RectTransform parent,string title,Vector2 position,Vector2 size,Action clicked){StyleButton(FishingTablet.Button(parent,title,Vector2.one*.5f,position,size,clicked));}
        static void StyleButton(PaperPixelGames.Framework.UI.BaseButton button)
        {if(!button)return;foreach(var label in button.GetComponentsInChildren<TMP_Text>(true)){label.fontSize=21;label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=22;}}
        void ReleaseInput(){escape.Release(true);if(ownsInput&&actor!=null&&actor.player)actor.player.ReleaseUIInput(PlayerUIInputOwner.FishingFreezer);ownsInput=false;}
        void ForceClose(){ReleaseInput();if(current==this)current=null;Destroy(gameObject);}
        void OnDestroy(){ReleaseInput();if(current==this)current=null;}
    }
}
