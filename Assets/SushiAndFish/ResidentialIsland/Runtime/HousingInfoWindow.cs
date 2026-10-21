using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace SushiAndFish.ResidentialIsland
{
    public sealed class HousingInfoWindow : MonoBehaviour
    {
        public HousingBuildingInfo Building {get;private set;}
        public int Floor {get;private set;}
        public RectTransform Panel {get;private set;}
        TextMeshProUGUI title,description;
        TextMeshProUGUI descriptionRight;
        public string FullDescription {get;private set;}
        Vector2 descriptionSize,descriptionPosition;
        RawImage picture;
        Button up,down;
        HousingPreviewStage stage;
        public Rect PreviewContentRect=>stage?.VisibleViewportRect??Rect.zero;
        HousingInputGate gate=new HousingInputGate();
        public void Show(HousingBuildingInfo building,RectTransform overlay)
        {
            Building=building;Floor=0;
            float w=overlay.rect.width*.80f,h=w/1.8f;
            var catalog=HousingIsland.Instance.Catalog;
            Panel=HousingUI.Image("BuildingInformation",overlay,new Vector2(w,h),catalog.panelColor,catalog.panelSprite).rectTransform;
            Panel.gameObject.AddComponent<HousingDraggable>();
            title=HousingUI.Text("BuildingName",Panel,new Vector2(w*.8f,h*.08f),building.title,w*.032f);title.rectTransform.anchoredPosition=new Vector2(0,h*.41f);
            var close=HousingUI.Button("Close",Panel,Vector2.one*w*.045f,"×",Close,catalog.closeSprite);((RectTransform)close.transform).anchoredPosition=new Vector2(w*.455f,h*.42f);
            var r=HousingUI.Rect("FloorPlan",Panel,Vector2.one*h*.56f);r.anchoredPosition=new Vector2(0,h*.06f);
            picture=r.gameObject.AddComponent<RawImage>();picture.raycastTarget=false;
            var inherited=Panel.GetComponent<Image>();picture.material=inherited.material;
            description=HousingUI.Text("Description",Panel,new Vector2(w*.9f,h*.22f),"",w*.022f);description.alignment=TextAlignmentOptions.TopLeft;
            description.rectTransform.anchoredPosition=new Vector2(0,-h*.36f);description.overflowMode=TextOverflowModes.Truncate;
            descriptionSize=description.rectTransform.sizeDelta;descriptionPosition=description.rectTransform.anchoredPosition;
            descriptionRight=HousingUI.Text("DescriptionRight",Panel,descriptionSize,"",w*.019f);descriptionRight.alignment=TextAlignmentOptions.TopLeft;descriptionRight.gameObject.SetActive(false);
            up=HousingUI.Button("FloorUp",Panel,Vector2.one*w*.055f,"▲",()=>SetFloor(Floor+1),catalog.upSprite);
            down=HousingUI.Button("FloorDown",Panel,Vector2.one*w*.055f,"▼",()=>SetFloor(Floor-1),catalog.downSprite);
            ((RectTransform)up.transform).anchoredPosition=new Vector2(-w*.42f,h*.15f);
            ((RectTransform)down.transform).anchoredPosition=new Vector2(-w*.42f,-h*.015f);
            if(building.residence)r.sizeDelta=Vector2.one*h*.5f;
            stage=new HousingPreviewStage(1024,1024);picture.texture=stage.Texture;SetFloor(0);gate.Acquire();
        }
        public void SetFloor(int index)
        {
            Floor=Mathf.Clamp(index,0,Building.floors.Count-1);
            var page=Building.floors[Floor];title.text=Building.residence?"户主":Building.title+" · "+page.title;
            bool multiple=Building.floors.Count>1;up.gameObject.SetActive(multiple);down.gameObject.SetActive(multiple);
            up.interactable=Floor<Building.floors.Count-1;down.interactable=Floor>0;
            description.rectTransform.sizeDelta=descriptionSize;description.rectTransform.anchoredPosition=descriptionPosition;description.fontSize=Panel.rect.width*.022f;description.enableAutoSizing=false;descriptionRight.gameObject.SetActive(false);description.alignment=Building.residence?TextAlignmentOptions.Center:TextAlignmentOptions.TopLeft;
            if(Building.residence)
            {
                var player=HousingIsland.Instance.Player;
                if(player){stage.SetPortrait(player.TPS_Character);description.text=string.IsNullOrWhiteSpace(player.PlayerName.Value.ToString())?"玩家":player.PlayerName.Value.ToString();FullDescription=description.text;}
            }
            else
            {
                stage.SetBuilding(page.roots,normalizeVisible:true);FullDescription=page.description();description.text=FullDescription;
                var lines=FullDescription.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries);
                if(Building.id=="casino"&&Floor>0&&Floor<5&&lines.Length>3)
                {
                    int half=(lines.Length+1)/2;float width=(descriptionSize.x-20)/2;
                    description.rectTransform.sizeDelta=new Vector2(width,descriptionSize.y);description.rectTransform.anchoredPosition=descriptionPosition+Vector2.left*(width+20)/2;
                    descriptionRight.rectTransform.sizeDelta=new Vector2(width,descriptionSize.y);descriptionRight.rectTransform.anchoredPosition=descriptionPosition+Vector2.right*(width+20)/2;
                    description.text=string.Join("\n",lines.Take(half));descriptionRight.text=string.Join("\n",lines.Skip(half));descriptionRight.gameObject.SetActive(true);
                    description.enableAutoSizing=descriptionRight.enableAutoSizing=true;description.fontSizeMax=descriptionRight.fontSizeMax=Panel.rect.width*.019f;description.fontSizeMin=descriptionRight.fontSizeMin=Panel.rect.width*.013f;
                }
            }
        }
        void Update(){if(KeyboardEscape())Close();}
        static bool KeyboardEscape()=>UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame==true;
        public void Close(){if(Panel)Destroy(Panel.gameObject);stage?.Dispose();stage=null;Destroy(this);}
        void OnDestroy(){gate.Release(true);stage?.Dispose();if(Panel)Destroy(Panel.gameObject);}
    }
}
