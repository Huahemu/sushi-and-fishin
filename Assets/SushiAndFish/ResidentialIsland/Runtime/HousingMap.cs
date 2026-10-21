using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Tablet;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SushiAndFish.ResidentialIsland
{
    [DefaultExecutionOrder(30000)]
    public sealed class HousingMap : MonoBehaviour
    {
        public HousingPlot PreviewPlot {get;private set;}
        public float PreviewYaw {get;private set;}
        public HousingType PreviewType=>types!=null&&types.Length>0?types[typeIndex]:null;
        public RectTransform Overlay=>overlay;
        public HousingInfoWindow Window {get;private set;}
        public RenderTexture PreviewTexture=>previewStage?.Texture;
        public Vector2 PreviewSize=>previewStage?.Size??Vector2.zero;
        public RectTransform PreviewRect=>ghost?ghost.rectTransform:null;
        public RectTransform EntranceRect=>door?door.rectTransform:null;
        LiveSceneTabletMap map;
        RectTransform overlay,view,hud;
        RectTransform placementBar;
        TextMeshProUGUI placementText;
        RawImage ghost,hoverImage;
        Image door;
        HousingPreviewStage previewStage,hoverStage;
        HousingType[] types;
        int typeIndex;
        float nextRefresh;
        readonly List<(Rect rect,RectTransform button)> cells=new List<(Rect,RectTransform)>();
        readonly List<GameObject> buttons=new List<GameObject>();
        readonly HousingInputGate gate=new HousingInputGate();
        bool releasePending;
        string parcelSignature;
        public bool IsVisible=>map&&map.MapCamera&&map.MapCamera.enabled&&!map.IsUnderwater&&view&&view.gameObject.activeInHierarchy;
        void Awake(){map=GetComponent<LiveSceneTabletMap>();}
        void Start(){if(HousingIsland.Instance)HousingIsland.Instance.Changed+=Rebuild;}
        void Configure()
        {
            view=map.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t=>t.name=="LiveSceneView");if(!view)return;
            overlay=HousingUI.Rect("InteractiveBuildingParcels",view,Vector2.zero);overlay.anchorMin=Vector2.zero;overlay.anchorMax=Vector2.one;overlay.offsetMin=overlay.offsetMax=Vector2.zero;
            var raw=view.GetComponent<RawImage>();
            // Reuse the actual tablet graphic material, including its world UI
            // depth policy. Every child inherits the tablet layer.
            var graphic=overlay.gameObject.AddComponent<Image>();graphic.color=Color.clear;graphic.raycastTarget=false;graphic.material=HousingIsland.Instance.Catalog.graphicMaterial;
            var scroll=view.GetComponentInParent<ScrollRect>();var viewport=scroll&&scroll.viewport?scroll.viewport:view.parent as RectTransform;
            hud=HousingUI.Rect("HousingMapHUD",viewport,Vector2.zero);hud.anchorMin=Vector2.zero;hud.anchorMax=Vector2.one;hud.offsetMin=hud.offsetMax=Vector2.zero;
            var hudGraphic=hud.gameObject.AddComponent<Image>();hudGraphic.color=Color.clear;hudGraphic.raycastTarget=false;hudGraphic.material=graphic.material;
            hoverImage=HousingUI.Rect("HoveredFloorPlan",overlay,Vector2.zero).gameObject.AddComponent<RawImage>();hoverImage.raycastTarget=false;hoverImage.material=graphic.material;
            ghost=HousingUI.Rect("ResidenceGhost",overlay,Vector2.zero).gameObject.AddComponent<RawImage>();ghost.raycastTarget=false;ghost.material=graphic.material;ghost.color=new Color(1,1,1,.72f);
            door=HousingUI.Image("EntranceDirection",overlay,new Vector2(20,7),new Color(.14f,1,.25f));door.raycastTarget=false;
            ghost.gameObject.SetActive(false);door.gameObject.SetActive(false);hoverImage.gameObject.SetActive(false);
            var barSize=new Vector2(hud.rect.width*.72f,hud.rect.height*.12f);
            placementBar=HousingUI.Image("PlacementControls",hud,barSize,HousingIsland.Instance.Catalog.panelColor,HousingIsland.Instance.Catalog.panelSprite).rectTransform;
            placementBar.anchorMin=placementBar.anchorMax=new Vector2(.5f,0);placementBar.pivot=new Vector2(.5f,0);placementBar.anchoredPosition=new Vector2(0,hud.rect.height*.015f);
            placementText=HousingUI.Text("PlacementText",placementBar,new Vector2(barSize.x-barSize.y*1.4f,barSize.y*.9f),"",barSize.y*.23f);
            float side=barSize.y*.55f;
            var previous=HousingUI.Button("PreviousHome",placementBar,new Vector2(side,side),"◀",()=>CycleType(-1));((RectTransform)previous.transform).anchoredPosition=new Vector2(-barSize.x/2+side*.7f,0);
            var next=HousingUI.Button("NextHome",placementBar,new Vector2(side,side),"▶",()=>CycleType(1));((RectTransform)next.transform).anchoredPosition=new Vector2(barSize.x/2-side*.7f,0);
            placementBar.gameObject.SetActive(false);
            types=HousingIsland.Instance.Catalog.homes.Where(t=>t.prefab).OrderBy(t=>t.level).ThenBy(t=>t.id).ToArray();
            Rebuild();
        }
        void Update()
        {
            if(!HousingIsland.Instance||!map)return;
            if(!overlay)Configure();if(!overlay)return;
            if(!IsVisible)
            {
                if(overlay.gameObject.activeSelf){CancelPreview();ClearHover();if(Window)Window.Close();overlay.gameObject.SetActive(false);}hud.gameObject.SetActive(false);return;
            }
            overlay.gameObject.SetActive(true);
            hud.gameObject.SetActive(true);
            if(Time.unscaledTime>nextRefresh){nextRefresh=Time.unscaledTime+2;Rebuild();}
            foreach(var cell in cells)Layout(cell.button,cell.rect);
            if(PreviewPlot!=null)
            {
                var key=Keyboard.current;
                float previousYaw=PreviewYaw;
                if(key?.wKey.wasPressedThisFrame==true)PreviewYaw=180;
                if(key?.sKey.wasPressedThisFrame==true)PreviewYaw=0;
                if(key?.aKey.wasPressedThisFrame==true)PreviewYaw=90;
                if(key?.dKey.wasPressedThisFrame==true)PreviewYaw=270;
                if(PreviewYaw!=previousYaw)HousingIsland.Instance.ClearFailure();
                if(key?.escapeKey.wasPressedThisFrame==true||Mouse.current?.rightButton.wasPressedThisFrame==true)CancelPreview();
                else UpdatePreview();
            }
            if(releasePending&&gate.Release())releasePending=false;
        }
        public void Rebuild()
        {
            if(!overlay)return;
            string signature=string.Join(";",HousingIsland.Instance.Plots.Select(p=>p.id+":"+(p.house?p.house.definition.id:"")))+":"+(SushiAndFish.Casino.ParkCasinoIntegration.Instance?.Ready==true);
            if(signature==parcelSignature)return;parcelSignature=signature;
            foreach(var button in buttons)if(button)Destroy(button);buttons.Clear();cells.Clear();
            foreach(var plot in HousingIsland.Instance.Plots)
            {
                var copy=plot;
                AddCell(plot.id,plot.rect,inside=>
                {
                    if(PreviewPlot!=null)return;
                    HousingIsland.Instance.Highlight(inside?copy:null);
                    if(inside&&copy.house)ShowHover(copy.house.transform);else ClearHover();
                },()=>SelectPlot(copy));
            }
            foreach(var info in HousingBuildingInfo.Collect().Where(i=>!i.residence))
            {
                var copy=info;AddCell(info.id,info.rect,inside=>
                {
                    if(PreviewPlot!=null)return;
                    if(inside)ShowHoverAt(copy.rect,copy.floors[0].roots);else ClearHover();
                },()=>OpenInfo(copy));
            }
            hoverImage.transform.SetAsLastSibling();ghost.transform.SetAsLastSibling();door.transform.SetAsLastSibling();placementBar.SetAsLastSibling();if(Window?.Panel)Window.Panel.SetAsLastSibling();
        }
        void AddCell(string id,Rect rect,Action<bool> hover,Action click)
        {
            var image=HousingUI.Image("Parcel_"+id,overlay,Vector2.zero,Color.clear);var button=image.gameObject.AddComponent<HousingParcelButton>();
            button.targetGraphic=image;button.hover=inside=>{image.color=inside?new Color(1,1,.2f,.13f):Color.clear;hover(inside);};button.click=click;
            buttons.Add(image.gameObject);cells.Add((rect,image.rectTransform));Layout(image.rectTransform,rect);
        }
        public void SelectPlot(HousingPlot plot)
        {
            if(PreviewPlot!=null)
            {
                if(plot==PreviewPlot && HousingIsland.Instance.Place(plot,PreviewType,PreviewYaw))CancelPreview();return;
            }
            if(plot.house){OpenInfo(HousingBuildingInfo.Collect().First(i=>i.house==plot.house));return;}
            PreviewPlot=plot;PreviewYaw=180;typeIndex=0;HousingIsland.Instance.ClearFailure();gate.Acquire(true);ClearHover();HousingIsland.Instance.Highlight(plot);CreatePreview();
        }
        public void CycleType(int delta)
        {
            if(PreviewPlot==null||types.Length==0)return;HousingIsland.Instance.ClearFailure();typeIndex=(typeIndex+delta+types.Length)%types.Length;CreatePreview();
        }
        void CreatePreview()
        {
            previewStage?.Dispose();previewStage=new HousingPreviewStage(768,768);previewStage.SetBuilding(new[]{PreviewType.prefab.transform},true);
            ghost.texture=previewStage.Texture;ghost.gameObject.SetActive(true);door.gameObject.SetActive(true);placementBar.gameObject.SetActive(true);UpdatePreview();
        }
        void UpdatePreview()
        {
            if(PreviewPlot==null||previewStage==null)return;
            var yaw=Quaternion.Euler(0,PreviewYaw,0);var center=PreviewPlot.Center+yaw*new Vector3(previewStage.Center.x,0,previewStage.Center.z);
            LayoutAt(ghost.rectTransform,center,previewStage.WorldRectSize);ghost.rectTransform.localRotation=Quaternion.Euler(0,0,-PreviewYaw);
            var house=PreviewType.prefab.GetComponent<HousingHouse>();
            var entry=house.entry?house.entry.position:new Vector3(0,0,-PreviewType.footprint.y/2+.3f);
            var point=PreviewPlot.Center+yaw*new Vector3(entry.x,0,entry.z);
            LayoutAt(door.rectTransform,point,new Vector2(1.4f,PreviewPlot.BoundaryWorldWidth));
            door.rectTransform.localRotation=Quaternion.Euler(0,0,-PreviewYaw);
            bool fits=HousingIsland.Instance.Fits(PreviewPlot,PreviewType,PreviewYaw,out var failure);
            ghost.color=fits?new Color(1,1,1,.72f):new Color(1,.5f,.5f,.65f);
            placementText.text=PreviewType.title+"  L"+PreviewType.level+"\n"+(HousingIsland.Instance.LastFailure??(fits?"WASD 朝向 · 左键放置 · 右键/Esc 取消":failure));
        }
        public void CancelPreview()
        {
            PreviewPlot=null;previewStage?.Dispose();previewStage=null;
            if(ghost)ghost.gameObject.SetActive(false);if(door)door.gameObject.SetActive(false);if(placementBar)placementBar.gameObject.SetActive(false);
            HousingIsland.Instance?.Highlight(null);releasePending=!gate.Release();
        }
        public void OpenInfo(HousingBuildingInfo info)
        {
            CancelPreview();ClearHover();if(Window)Window.Close();Window=hud.gameObject.AddComponent<HousingInfoWindow>();Window.Show(info,hud);
        }
        void ShowHover(params Transform[] sources)
        {
            ClearHover();hoverStage=new HousingPreviewStage();hoverStage.SetBuilding(sources);
            hoverImage.texture=hoverStage.Texture;hoverImage.gameObject.SetActive(true);
            LayoutAt(hoverImage.rectTransform,hoverStage.Center,hoverStage.WorldRectSize);
        }
        void ShowHoverAt(Rect site,Transform[] sources)
        {
            ShowHover(sources);
            var size=hoverStage.WorldRectSize;float scale=Mathf.Min(site.width/size.x,site.height/size.y);
            LayoutAt(hoverImage.rectTransform,new Vector3(site.center.x,0,site.center.y),size*scale);
        }
        void ClearHover(){hoverStage?.Dispose();hoverStage=null;if(hoverImage)hoverImage.gameObject.SetActive(false);}
        void Layout(RectTransform rect,Rect world)
        {
            var b=map.WorldBounds;rect.anchorMin=new Vector2((world.xMin-b.xMin)/b.width,(world.yMin-b.yMin)/b.height);
            rect.anchorMax=new Vector2((world.xMax-b.xMin)/b.width,(world.yMax-b.yMin)/b.height);rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
        void LayoutAt(RectTransform rect,Vector3 point,Vector2 size)
        {
            var b=map.WorldBounds;rect.anchorMin=rect.anchorMax=new Vector2((point.x-b.xMin)/b.width,(point.z-b.yMin)/b.height);rect.anchoredPosition=Vector2.zero;
            rect.sizeDelta=new Vector2(size.x/b.width*overlay.rect.width,size.y/b.height*overlay.rect.height);
        }
        void OnDisable(){CancelPreview();ClearHover();if(Window)Window.Close();if(overlay)overlay.gameObject.SetActive(false);if(hud)hud.gameObject.SetActive(false);}
        void OnDestroy()
        {
            if(HousingIsland.Instance)HousingIsland.Instance.Changed-=Rebuild;gate.Release(true);previewStage?.Dispose();hoverStage?.Dispose();if(overlay)Destroy(overlay.gameObject);if(hud)Destroy(hud.gameObject);
        }
    }
}
