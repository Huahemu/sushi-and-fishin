using PaperPixelGames.Core.Tablet;
using TMPro;
using UnityEngine;

namespace SushiAndFish.CityExpansion
{
    public sealed class CityMapPlaces : MonoBehaviour
    {
        RectTransform container;
        readonly Vector2[] points={new Vector2(-101,-35),new Vector2(72,-13),new Vector2(88,-52.5f)};
        readonly string[] captions={"CASINO","RESTAURANT","CINEMA"};
        readonly RectTransform[] pins=new RectTransform[3];
        public void Configure(RectTransform image)
        {
            if(container)return;
            var root=new GameObject("CurrentTownPlaces",typeof(RectTransform));
            container=(RectTransform)root.transform;container.SetParent(image,false);container.anchorMin=Vector2.zero;container.anchorMax=Vector2.one;container.offsetMin=container.offsetMax=Vector2.zero;
            TMP_FontAsset font=TMP_Settings.defaultFontAsset;
            for(int i=0;i<pins.Length;i++)
            {
                var go=new GameObject(captions[i]+"Marker",typeof(RectTransform));var pin=(RectTransform)go.transform;pin.SetParent(container,false);pin.sizeDelta=new Vector2(220,60);pins[i]=pin;
                var icon=new GameObject("PlaceSymbol",typeof(RectTransform),typeof(UnityEngine.UI.Image));icon.transform.SetParent(pin,false);
                var ir=(RectTransform)icon.transform;ir.sizeDelta=new Vector2(14,14);ir.anchoredPosition=Vector2.zero;ir.localRotation=Quaternion.Euler(0,0,45);
                var img=icon.GetComponent<UnityEngine.UI.Image>();img.color=new Color(.98f,.78f,.28f);img.raycastTarget=false;
                var label=new GameObject("Caption",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(pin,false);
                var text=label.GetComponent<TextMeshProUGUI>();text.font=font;text.text=captions[i];text.fontSize=28;text.fontStyle=FontStyles.Bold;text.color=new Color(1,.92f,.66f);text.outlineColor=Color.black;text.outlineWidth=.3f;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
                var rect=(RectTransform)label.transform;rect.sizeDelta=new Vector2(220,42);rect.anchoredPosition=new Vector2(0,26);
            }
        }
        public void Refresh(LiveSceneTabletMap map)
        {
            if(!container)return;
            container.gameObject.SetActive(!map.IsUnderwater);
            Rect bounds=map.WorldBounds;
            for(int i=0;i<pins.Length;i++)
            {
                var uv=new Vector2((points[i].x-bounds.xMin)/bounds.width,(points[i].y-bounds.yMin)/bounds.height);
                pins[i].anchorMin=pins[i].anchorMax=uv;pins[i].anchoredPosition=Vector2.zero;
            }
        }
    }
}
