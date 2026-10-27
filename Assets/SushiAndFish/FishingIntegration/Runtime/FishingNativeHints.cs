using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace SushiAndFish.FishingIntegration {
    public sealed class FishingNativeHints : MonoBehaviour {
        sealed class Row {public RectTransform root;public TMP_Text label,icon;}
        readonly List<Row> rows=new List<Row>();TMP_FontAsset font;TMP_SpriteAsset sprites;Material spriteMaterial;
        public RectTransform[] Rects {get {var result=new RectTransform[rows.Count];for(int i=0;i<rows.Count;i++)result[i]=rows[i].root;return result;}}
        public void Build(MigrationConfig config){
            if(rows.Count>0||!config.nativeHintTemplate)return;
            if(config.nativeHintSprites){
                // Keep the original glyph metrics and keyboard artwork. The
                // extracted TMP sprite shader has no usable compiled variant.
                sprites=Instantiate(config.nativeHintSprites);
                spriteMaterial=new Material(Graphic.defaultGraphicMaterial){name="Native fishing key sprites",mainTexture=sprites.spriteSheet};
                sprites.material=spriteMaterial;sprites.UpdateLookupTables();
            }
            var staging=new GameObject("Inactive native hint cloning");staging.SetActive(false);
            for(int i=0;i<4;i++){
                var go=Instantiate(config.nativeHintTemplate,staging.transform);go.name="Native fishing hint "+i;
                var behavior=go.GetComponent<HintItem>();if(behavior){behavior.enabled=false;Remove(behavior);}
                foreach(var localizer in go.GetComponentsInChildren<LocalizeStringEvent>(true))localizer.enabled=false;
                var row=new Row{root=(RectTransform)go.transform,label=go.GetComponent<TMP_Text>(),icon=go.transform.Find("HintIconText").GetComponent<TMP_Text>()};
                if(!font){font=Instantiate(row.label.font);font.name=row.label.font.name+" (fishing native copy)";font.fallbackFontAssetTable=new List<TMP_FontAsset>(row.label.font.fallbackFontAssetTable??new List<TMP_FontAsset>());foreach(var fallback in config.font.fallbackFontAssetTable)if(fallback&&!font.fallbackFontAssetTable.Contains(fallback))font.fallbackFontAssetTable.Add(fallback);}
                row.label.font=font;row.icon.font=font;if(sprites)row.icon.spriteAsset=sprites;row.label.raycastTarget=false;row.icon.raycastTarget=false;row.label.textWrappingMode=TextWrappingModes.NoWrap;
                row.root.SetParent(transform,false);row.root.anchorMin=row.root.anchorMax=Vector2.zero;row.root.pivot=Vector2.zero;row.root.anchoredPosition=new Vector2(28,34+i*54);row.root.localScale=Vector3.one;
                go.SetActive(false);rows.Add(row);
            }
            Remove(staging);
        }
        public void Render(bool equipped,int equipment,int health,int ammo,int capacity,string weapon,string notice,bool charging=false,float power=0,float range=0,bool hooked=false){
            if(rows.Count<4)return;
            Set(0,equipped,equipment<0?"key1_leftbutton":"key1_r",equipment<0?"收线 · B 更换拟饵":"换弹 · 长按 R 装备");
            Set(1,equipped,equipment<0?"key1_rightbutton":"key1_leftbutton",equipment<0?(hooked?"放鱼到岸上 · 然后切枪":"按住蓄力 · 松开抛出"):"开火 · 右键瞄准");
            var rodStatus=charging?"蓄力 "+Mathf.RoundToInt(Mathf.Clamp01(power)*100)+"% · 预计 "+Mathf.RoundToInt(range)+" 米":"海钓竿 · 生命 "+health+" / 100";
            Set(2,equipped,null,equipment<0?rodStatus:weapon+"  "+ammo+" / "+capacity+" · 生命 "+health+" / 100");
            Set(3,!string.IsNullOrEmpty(notice),null,notice);
        }
        void Set(int index,bool visible,string key,string text){
            var row=rows[index];row.root.gameObject.SetActive(visible);if(!visible)return;
            row.label.text=text;row.icon.gameObject.SetActive(!string.IsNullOrEmpty(key));row.icon.text=string.IsNullOrEmpty(key)?"":"<sprite name="+key+">";
        }
        public void RenderSailing(bool driving,bool motorReady,float speed,string route,string notice){if(rows.Count<4)return;
            Set(0,true,driving?null:"key1_e",driving?"W/S 前进倒车 · A/D 转向":notice);Set(1,driving,"key1_e","退出驾驶 · G 同样可用");Set(2,driving,null,motorReady?"航速 "+(speed*3.6f).ToString("F0")+" km/h":"发动机启动中");Set(3,driving,null,route);
        }
        public void RenderWaterAccess(string action,string notice){if(rows.Count<4)return;Set(0,true,null,action);Set(1,false,null,null);Set(2,false,null,null);Set(3,!string.IsNullOrEmpty(notice),null,notice);}
        void OnDestroy(){if(font)Remove(font);if(sprites)Remove(sprites);if(spriteMaterial)Remove(spriteMaterial);}
        static void Remove(Object item){if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}
    }
}
