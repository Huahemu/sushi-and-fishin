using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace SushiAndFish.ResidentialIsland
{
    public static class HousingUI
    {
        public static RectTransform Rect(string name,Transform parent,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));var canvas=parent.GetComponentInParent<Canvas>();
            go.layer=canvas?canvas.rootCanvas.gameObject.layer:parent.gameObject.layer;
            var r=(RectTransform)go.transform;r.SetParent(parent,false);r.sizeDelta=size;return r;
        }
        public static Image Image(string name,Transform parent,Vector2 size,Color color,Sprite sprite=null)
        {
            var r=Rect(name,parent,size);var image=r.gameObject.AddComponent<Image>();image.sprite=sprite;image.color=color;
            if(sprite&&sprite.border.sqrMagnitude>0)
            {
                image.type=UnityEngine.UI.Image.Type.Sliced;
                // The visible tablet viewport is only 288 logical units wide.
                // Scale the source's 75px corners to the widget's own size.
                float corner=Mathf.Max(.2f,Mathf.Min(size.x,size.y)*.07f);
                float border=Mathf.Max(sprite.border.x,sprite.border.y,sprite.border.z,sprite.border.w);
                image.pixelsPerUnitMultiplier=border/(corner*Mathf.Max(.00001f,image.pixelsPerUnit));
            }
            image.material=HousingIsland.Instance.Catalog.graphicMaterial;
            return image;
        }
        public static TextMeshProUGUI Text(string name,Transform parent,Vector2 size,string value,float fontSize=22)
        {
            var r=Rect(name,parent,size);var text=r.gameObject.AddComponent<TextMeshProUGUI>();
            text.font=HousingIsland.Instance.Catalog.font?HousingIsland.Instance.Catalog.font:TMP_Settings.defaultFontAsset;
            text.text=value;text.fontSize=fontSize;text.color=new Color(.24f,.18f,.1f);text.alignment=TextAlignmentOptions.Center;
            text.raycastTarget=false;text.enableAutoSizing=false;r.gameObject.AddComponent<HousingTextMaterial>();return text;
        }
        public static Button Button(string name,Transform parent,Vector2 size,string caption,Action action,Sprite icon=null)
        {
            var c=HousingIsland.Instance.Catalog;var image=Image(name,parent,size,icon?new Color(.45f,.3f,.16f):Color.white,icon?icon:c.buttonSprite);
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.highlightedColor=new Color(1,1,.7f);colors.pressedColor=new Color(.86f,.71f,.36f);button.colors=colors;
            button.onClick.AddListener(()=>action());if(!icon){var label=Text("Caption",image.transform,size,caption,size.y*.55f);label.color=new Color(1,.94f,.72f);}return button;
        }
    }
}
