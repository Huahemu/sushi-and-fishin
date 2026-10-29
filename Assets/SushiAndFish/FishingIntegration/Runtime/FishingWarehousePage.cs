using System;
using System.Linq;
using HowToFish;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Core.Struct;
using PaperPixelGames.Framework.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SushiAndFish.FishingIntegration {
    public sealed class FishingWarehousePage:MonoBehaviour {
        InventoryItemUI template;Transform original;RectTransform content;ScrollRect scroll;BaseButton open,deposit;bool visible;float refresh;string drawn;
        public void Bind(InventoryItemUI row,Transform container){template=row;original=container;scroll=container.GetComponentInParent<ScrollRect>();FishingTablet.Button(transform,"召回船只",new Vector2(1,1),new Vector2(-280,-7),new Vector2(74,12),()=>FishingMigration.Instance.Local?.Send("boat-recall",1));open=FishingTablet.Button(transform,"海钓鱼仓",new Vector2(1,1),new Vector2(-40,-7),new Vector2(74,12),()=>Show(!visible));FishingTablet.Button(transform,"补充鱼饵",new Vector2(1,1),new Vector2(-200,-7),new Vector2(74,12),()=>FishingMigration.Instance.Local?.Send("restock",1));}
        void OnDisable(){Show(false);}
        void Update(){if(visible&&Time.unscaledTime>=refresh){refresh=Time.unscaledTime+1;Draw();}}
        public void Show(bool show){visible=show;if(original)original.gameObject.SetActive(!show);if(!show){if(content)content.gameObject.SetActive(false);if(deposit)deposit.gameObject.SetActive(false);if(scroll&&original)scroll.content=(RectTransform)original;return;}
            if(!content){var go=new GameObject("Infinite fish warehouse",typeof(RectTransform),typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));content=(RectTransform)go.transform;content.SetParent(original.parent,false);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;content.anchoredPosition=Vector2.zero;var layout=go.GetComponent<VerticalLayoutGroup>();layout.spacing=3;layout.childControlWidth=true;layout.childControlHeight=false;layout.childForceExpandHeight=false;go.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;deposit=FishingTablet.Button(transform,"存入手中鱼",new Vector2(1,1),new Vector2(-120,-7),new Vector2(74,12),()=>FishingMigration.Instance.Local?.Send("deposit",1));}
            content.gameObject.SetActive(true);deposit.gameObject.SetActive(true);if(scroll)scroll.content=content;drawn=null;Draw();LayoutRebuilder.ForceRebuildLayoutImmediate(content);if(scroll)scroll.verticalNormalizedPosition=1;
        }
        void Draw(){if(!content||!FishingMigration.Instance)return;var lines=FishingMigration.Instance.WarehouseLines;string signature=string.Join(";",lines.Select(l=>l.itemId+":"+l.count));if(drawn==signature)return;drawn=signature;foreach(Transform row in content)Destroy(row.gameObject);
            var world=FishingMigration.Instance;
            foreach(var record in lines.Where(r=>r.count>0)){
                var item=world.Config.database.FindById(record.itemId) as FishSO;if(!item)continue;
                var row=Instantiate(template,content);row.Initialize(item,(int)Math.Min(record.count,int.MaxValue),"海钓鱼仓");row.PrepareWarehouseRow();row.SetLongAmount(record.count);
                var rect=(RectTransform)row.transform;rect.sizeDelta=new Vector2(rect.sizeDelta.x,Mathf.Max(32,((RectTransform)template.transform).rect.height));
                var take=FishingTablet.Button(row.transform,"取出一条",new Vector2(1,.5f),new Vector2(-26,0),new Vector2(48,12),()=>world.Local?.Send("withdraw",1,item.Id));if(take)take.interactable=record.count>0;
            }
        }
    }
}
