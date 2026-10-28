using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Wheel;

namespace SushiAndFish.FishingIntegration {
    public sealed class FishingHud:MonoBehaviour {
        FishingActor actor;Canvas canvas;RectTransform panel,eventPanel;Image health,portrait;TMP_Text nameText,healthText,status,eventText;FishingNativeHints nativeHints;readonly BoatCompassDisplay boatCompass=new BoatCompassDisplay();string focused;
        public void Bind(FishingActor owner){actor=owner;Build();}
        void Build(){
            var root=new GameObject("Native-style fishing HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));root.transform.SetParent(transform);canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=50;var scale=root.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.matchWidthOrHeight=.5f;
            panel=Panel(canvas.transform,new Vector2(.5f,1),new Vector2(0,-172),new Vector2(530,98));portrait=Image(panel,new Vector2(0,.5f),new Vector2(48,0),new Vector2(76,70),Color.white);portrait.preserveAspect=true;portrait.useSpriteMesh=true;
            nameText=Label(panel,new Vector2(0,1),new Vector2(245,-22),new Vector2(300,30),22);healthText=Label(panel,new Vector2(1,1),new Vector2(-65,-22),new Vector2(120,28),18);
            var background=Image(panel,new Vector2(.5f,.5f),new Vector2(42,0),new Vector2(390,11),new Color(.22f,.16f,.12f));health=Image(background.transform,new Vector2(0,.5f),Vector2.zero,new Vector2(390,11),new Color(.78f,.21f,.16f));FishingHudPresentation.SetHealth(health,1,1);
            status=Label(panel,new Vector2(.5f,0),new Vector2(40,20),new Vector2(390,28),17);
            eventPanel=Panel(canvas.transform,new Vector2(.5f,1),new Vector2(0,-254),new Vector2(530,58));eventText=Label(eventPanel,new Vector2(.5f,.5f),Vector2.zero,new Vector2(490,48),18);
            nativeHints=root.AddComponent<FishingNativeHints>();nativeHints.Build(actor.world.Config);
        }
        void Update(){if(actor==null||!actor.player||!canvas)return;bool blocked=actor.player.IsTabletActive;canvas.enabled=!blocked;var world=actor.world;
            bool nativeHarpoon=actor.player.Harpoon&&actor.player.Harpoon.ToolStatus;
            FishingSnapshot fish=null;
            if(actor.module&&actor.module.DisplayCatch){var identity=actor.module.DisplayCatch.GetComponent<MigratedCatchIdentity>();if(identity)focused=identity.token;}
            if(world.ActorSnapshots.TryGetValue(actor.player.OwnerClientId,out var a)&&!string.IsNullOrEmpty(a.focus))focused=a.focus;
            if(actor.Camera&&Physics.Raycast(actor.Camera.transform.position,actor.Camera.transform.forward,out var hit,100,~0,QueryTriggerInteraction.Ignore)){var id=hit.collider.GetComponentInParent<MigratedCatchIdentity>();if(id)focused=id.token;}
            fish=FishingHudPresentation.LiveFocus(world.FishSnapshots,focused);
            if(fish==null)focused=null;
            panel.gameObject.SetActive(fish!=null&&!nativeHarpoon);if(fish!=null){var binding=world.Config.Find(fish.species);nameText.text=binding.definition.RarityLabel+" · "+binding.definition.displayName;healthText.text=fish.health+" / "+binding.definition.MaximumHealth;FishingHudPresentation.SetHealth(health,fish.health,binding.definition.MaximumHealth);portrait.material=fish.shiny?binding.shinyIconMaterial:null;portrait.sprite=fish.shiny&&binding.shiny?binding.shiny.Sprite:binding.original.Sprite;FishingIconGeometry.Attach(portrait);var offset=actor.Camera.transform.InverseTransformPoint(fish.position);string direction=offset.z<0?"身后":offset.x< -1?"左侧":offset.x>1?"右侧":"前方";status.text=(fish.landing?"正在落到岸边":fish.attached?"继续收线 · 右键放到岸上":"活鱼 · "+direction)+" · "+Vector3.Distance(actor.Camera.transform.position,fish.position).ToString("F1")+"米";}
            eventPanel.gameObject.SetActive(!nativeHarpoon&&!string.IsNullOrEmpty(world.EventTitle));eventText.text=world.EventTitle+" · "+Mathf.CeilToInt(world.EventRemaining)+"秒\n"+world.EventDescription;
            var notice=!string.IsNullOrEmpty(actor.message)&&Time.unscaledTime<actor.messageUntil?actor.message:null;
            if(notice!=null&&(notice.Contains("右键抛竿，左键收线")||notice.Contains("左键射击，右键瞄准")))notice=null;
            int index=a!=null?a.equipment:actor.equipment;var offer=world.Config.weapons?.FirstOrDefault(w=>w.index==index);
            var islands=world.Archipelago;bool aboard=islands&&islands.Boat&&(islands.IsDriver(actor.player)||islands.OnDeck(actor.player));
            boatCompass.Render(islands?islands.Boat:null,world.Config,aboard&&!blocked,islands?islands.Settings.regions:null);
            if(islands&&islands.RenderHints(nativeHints,actor))return;
            bool migrated=FishingMigration.UsesMigratedFishing(actor.player);
            nativeHints.Render(migrated&&(a!=null?a.equipped:actor.equipped),index,a!=null?a.health:100,a!=null?a.ammo:0,a!=null?a.capacity:0,offer?.title??"鱼枪",migrated?notice:null,a!=null&&a.charging,a!=null?a.castPower:0,a!=null?a.castRange:0,fish!=null&&fish.attached&&fish.fisher==actor.player.OwnerClientId);
        }
        RectTransform Panel(Transform parent,Vector2 anchor,Vector2 pos,Vector2 size){var image=Image(parent,anchor,pos,size,Color.white);image.sprite=actor.world.Config.panelSprite;image.type=UnityEngine.UI.Image.Type.Sliced;return image.rectTransform;}
        Image Image(Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Color color){var go=new GameObject("Fishing UI",typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();var rect=image.rectTransform;rect.anchorMin=rect.anchorMax=anchor;rect.anchoredPosition=pos;rect.sizeDelta=size;image.color=color;image.raycastTarget=false;return image;}
        TMP_Text Label(Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,float fontSize){var go=new GameObject("Fishing text",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var text=go.GetComponent<TextMeshProUGUI>();text.font=actor.world.Config.font;text.fontSize=fontSize;text.color=new Color(.2f,.13f,.08f);text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;var rect=text.rectTransform;rect.anchorMin=rect.anchorMax=anchor;rect.anchoredPosition=pos;rect.sizeDelta=size;return text;}
        void OnDisable(){boatCompass.Dispose();}
        void OnDestroy(){boatCompass.Dispose();if(canvas)Destroy(canvas.gameObject);}
    }
}
