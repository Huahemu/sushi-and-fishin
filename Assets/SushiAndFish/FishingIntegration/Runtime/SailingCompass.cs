using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SushiAndFish.FishingIntegration {
    public static class SailingCompassMath {
        public static Vector2 Direction(Vector3 origin,Vector3 target,float viewYaw){
            var delta=target-origin;if(delta.x*delta.x+delta.z*delta.z<4)return Vector2.zero;float angle=(Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg-viewYaw)*Mathf.Deg2Rad;return new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));
        }
    }
    // Original How to Fish radar circle/arrow/dot sprites, using the native
    // Sushi UI palette/font. One stable marker per island, no terrain minimap.
    public sealed class SailingCompass:MonoBehaviour {
        MigrationConfig config;Image[] dots=new Image[3];TMP_Text[] names=new TMP_Text[3],cardinals=new TMP_Text[4];readonly Vector2[] directions=new Vector2[3];bool built;
        static readonly Color ink=new Color(.25f,.2f,.15f),gold=new Color(.63f,.45f,.25f);
        static readonly Color[] colours={new Color(.72f,.29f,.19f),new Color(.12f,.42f,.44f),new Color(.44f,.43f,.19f)};
        public RectTransform Bounds=>(RectTransform)transform;
        public void Build(MigrationConfig value){if(built)return;built=true;config=value;
            var rect=Bounds;rect.anchorMin=rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(-166,-322);rect.sizeDelta=new Vector2(270,308);rect.localScale=Vector3.one;
            Picture("Original radar ring",config.compassCircle,Vector2.zero,new Vector2(248,248),gold);
            Picture("Native paper face",config.compassFace,Vector2.zero,new Vector2(226,226),new Color(.96f,.92f,.82f,.97f));
            var arrow=Picture("Original radar heading arrow",config.compassArrow,Vector2.zero,new Vector2(24,24),new Color(.2f,.34f,.35f));arrow.rectTransform.localEulerAngles=new Vector3(0,0,-90);
            string[] labels={"北","东","南","西"};for(int i=0;i<4;i++)cardinals[i]=Label(labels[i],Vector2.zero,new Vector2(28,26),18,ink);
            for(int i=0;i<3;i++){dots[i]=Picture("Island "+i+" original map dot",config.compassDot,Vector2.zero,new Vector2(12,12),colours[i]);names[i]=Label(i==0?"主岛":"小岛 "+i,new Vector2((i-1)*84,-142),new Vector2(78,24),17,colours[i]);}
            Show(false);
        }
        public void Show(bool show){if(gameObject.activeSelf!=show)gameObject.SetActive(show);}
        public void Render(Vector3 position,float yaw,FishingIslandRegion[] regions){if(!built||regions==null)return;
            for(int i=0;i<4;i++)cardinals[i].rectTransform.anchoredPosition=SailingCompassMath.Direction(Vector3.zero,Quaternion.Euler(0,i*90,0)*Vector3.forward*10,yaw)*101;
            for(int i=0;i<3;i++){bool valid=i<regions.Length&&regions[i]!=null;dots[i].gameObject.SetActive(valid);names[i].gameObject.SetActive(valid);if(!valid)continue;
                var direction=SailingCompassMath.Direction(position,regions[i].dock,yaw);directions[i]=direction;int cluster=0;for(int j=0;j<i;j++)if(Vector2.Distance(direction,directions[j])<.35f)cluster++;
                var point=direction*(76-cluster*17);dots[i].rectTransform.anchoredPosition=point;
            }
        }
        Image Picture(string name,Sprite sprite,Vector2 position,Vector2 size,Color colour){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(transform,false);var image=go.GetComponent<Image>();image.sprite=sprite;image.color=colour;image.preserveAspect=true;image.raycastTarget=false;var rect=image.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=size;rect.anchoredPosition=position;return image;}
        TMP_Text Label(string value,Vector2 position,Vector2 size,float fontSize,Color colour){var go=new GameObject("Compass "+value,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(transform,false);var text=go.GetComponent<TMP_Text>();text.font=config.font;text.text=value;text.fontSize=fontSize;text.color=colour;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.NoWrap;var rect=text.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=size;rect.anchoredPosition=position;return text;}
    }
}
