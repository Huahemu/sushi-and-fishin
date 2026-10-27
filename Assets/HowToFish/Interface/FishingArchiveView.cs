using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace HowToFish.Interface {
    public sealed class FishingArchiveView:MonoBehaviour {
        public FishingModule module;
        public FishingCatalog catalog;
        public Font font;
        const int PageSize=15;
        Canvas canvas;CanvasScaler scaler;RectTransform panel;
        Text title,progress,pageText,detailTitle,detailState,detailStats,empty;
        RawImage detailImage;
        Button appearanceButton,collectionTab,warehouseTab;
        Text appearanceLabel;
        readonly Button[] cells=new Button[PageSize];
        readonly RawImage[] portraits=new RawImage[PageSize];
        readonly Text[] names=new Text[PageSize],states=new Text[PageSize];
        FishCatalogEntry[] visible=Array.Empty<FishCatalogEntry>();
        FishingArchiveData snapshot;
        int page,selected,lastSelectedId=-1;
        bool warehouse,showShiny,open,vr,lastDefaultShiny;
        CursorLockMode previousLock;bool previousCursor;
        static readonly Color Mint=new Color(.40f,.91f,.78f),Muted=new Color(.66f,.77f,.79f);
        public Canvas Canvas=>canvas;
        IReadOnlyList<FishCollectionRecord> Discoveries=>snapshot!=null?snapshot.collection:module.inventory.Collection;
        IReadOnlyList<FishingInventory.Record> Stock=>snapshot!=null?snapshot.fish:module.inventory.Records;
        FishCollectionRecord Discovery(int id)=>Discoveries.FirstOrDefault(r=>r.id==id);
        FishingInventory.Record Stored(int id)=>Stock.FirstOrDefault(r=>r.id==id);
        void OnEnable(){if(module&&module.inventory){module.inventory.ArchiveRequested+=Command;module.inventory.Changed+=Refresh;}}
        void OnDisable(){if(module&&module.inventory){module.inventory.ArchiveRequested-=Command;module.inventory.Changed-=Refresh;}Close();}
        public void Command(FishingArchiveCommand command){
            if(command==FishingArchiveCommand.Collection){if(open&&!warehouse)Close();else Open(false);return;}
            if(command==FishingArchiveCommand.Warehouse){if(open&&warehouse)Close();else Open(true);return;}
            if(command==FishingArchiveCommand.Close){Close();return;}
            if(!open)return;
            if(command==FishingArchiveCommand.NextPage)ChangePage(1);
            else if(command==FishingArchiveCommand.PreviousPage)ChangePage(-1);
            else if(command==FishingArchiveCommand.NextEntry)Select(selected+1);
            else if(command==FishingArchiveCommand.PreviousEntry)Select(selected-1);
            else if(command==FishingArchiveCommand.ToggleAppearance)ToggleAppearance();
        }
        public void Open(bool storage){
            if(!module||!module.rig||!catalog)return;snapshot=null;Build();
            if(!open){previousLock=Cursor.lockState;previousCursor=Cursor.visible;open=true;}
            warehouse=storage;page=selected=0;showShiny=false;lastSelectedId=-1;
            module.inventory.ArchiveOpen=true;module.inventory.ArchiveIsWarehouse=storage;module.SetReeling(0);
            if(module.combat.HeldGun)module.combat.HeldGun.SetAim(false);
            if(!module.rig.Tracked){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
            canvas.gameObject.SetActive(true);Layout();Refresh();
        }
        public void Close(){
            if(!open)return;open=false;if(canvas)canvas.gameObject.SetActive(false);
            if(module&&module.inventory)module.inventory.ArchiveOpen=false;
            if(module&&module.rig&&!module.rig.Tracked){Cursor.lockState=previousLock;Cursor.visible=previousCursor;}
        }
        void LateUpdate(){if(open)Layout();}
        void Layout(){
            bool tracked=module&&module.rig&&module.rig.Tracked;
            if(vr==tracked&&canvas.renderMode==(tracked?RenderMode.WorldSpace:RenderMode.ScreenSpaceOverlay))return;
            vr=tracked;var r=(RectTransform)canvas.transform;scaler.enabled=!tracked;
            if(tracked){r.SetParent(module.rig.view.transform,false);canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=module.rig.view;r.sizeDelta=new Vector2(1440,900);r.localPosition=new Vector3(0,0,1.15f);r.localRotation=Quaternion.identity;r.localScale=Vector3.one*.0009f;}
            else{r.SetParent(transform,false);r.localScale=Vector3.one;r.localRotation=Quaternion.identity;canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;}
        }
        void Build(){
            if(canvas)return;
            var root=new GameObject("Fish collection and unlimited warehouse",typeof(RectTransform));root.transform.SetParent(transform,false);
            canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=140;root.AddComponent<GraphicRaycaster>();
            scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1440,900);scaler.matchWidthOrHeight=.5f;
            panel=Box(root.transform,"Archive panel",0,0,1180,760,new Color(.025f,.065f,.08f,.985f));panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);panel.anchoredPosition=Vector2.zero;
            title=Label(panel,28,18,430,50,"海岛鱼类图鉴",27,Color.white);
            progress=Label(panel,28,76,1124,27,"收获 0 / 60 · 见过 0 / 60",17,Mint);
            collectionTab=Button(panel,720,22,135,38,"鱼类图鉴",()=>Open(false));warehouseTab=Button(panel,867,22,135,38,"无限仓库",()=>Open(true));Button(panel,1014,22,138,38,"关闭  Esc",Close);
            Label(panel,28,110,1124,24,"黑影：未遇见     灰度：已钓起 / 曾逃走     彩色：已成功收获     C：切换已收获样式",14,Muted);
            for(int i=0;i<PageSize;i++){
                int index=i;int col=i%5,row=i/5;
                cells[i]=Button(panel,28+col*150,150+row*164,142,154,"",()=>Select(page*PageSize+index));
                portraits[i]=Portrait(cells[i].transform,8,7,126,94.5f);
                names[i]=Label(cells[i].transform,8,106,126,30,"???",13,Color.white);names[i].alignment=TextAnchor.MiddleCenter;
                names[i].resizeTextForBestFit=true;names[i].resizeTextMinSize=10;names[i].resizeTextMaxSize=13;
                states[i]=Label(cells[i].transform,8,136,126,15,"未遇见",11,Muted);states[i].alignment=TextAnchor.MiddleCenter;
            }
            empty=Label(panel,70,300,650,100,"仓库还没有鱼\n击败活鱼、拾取，再放入冷藏箱",22,Muted);empty.alignment=TextAnchor.MiddleCenter;
            Button(panel,28,663,145,38,"上一页  PgUp",()=>ChangePage(-1));pageText=Label(panel,185,669,435,28,"1 / 4",17,Color.white);pageText.alignment=TextAnchor.MiddleCenter;Button(panel,632,663,138,38,"下一页  PgDn",()=>ChangePage(1));
            var detail=Box(panel,"Selected species",800,150,352,551,new Color(.075f,.13f,.15f));
            detailImage=Portrait(detail,16,16,320,240);detailTitle=Label(detail,20,273,312,50,"???",23,Color.white);detailTitle.resizeTextForBestFit=true;detailTitle.resizeTextMinSize=16;detailTitle.resizeTextMaxSize=23;detailState=Label(detail,20,329,312,32,"未遇见",17,Mint);detailStats=Label(detail,20,369,312,103,"",16,Muted);
            appearanceButton=Button(detail,20,490,312,39,"异色尚未收获",ToggleAppearance);appearanceLabel=appearanceButton.GetComponentInChildren<Text>();
            Label(panel,28,719,1124,23,"Tab 图鉴    I 仓库    方向键选择    PgUp / PgDn 翻页    C 切换样式    Esc 返回",14,Muted);
        }
        void Refresh(){if(!canvas||(!open&&snapshot==null))return;Paint();}
        void Paint(){
            visible=catalog.entries.Where(e=>!warehouse||(Stored(e.definition.originalId)?.count??0)>0).ToArray();selected=Mathf.Clamp(selected,0,Mathf.Max(0,visible.Length-1));
            int pages=Mathf.Max(1,Mathf.CeilToInt(visible.Length/(float)PageSize));page=Mathf.Clamp(page,0,pages-1);
            if(selected/PageSize!=page)selected=page*PageSize;
            title.text=warehouse?"无限仓库":"海岛鱼类图鉴";
            long total=0;foreach(var r in Stock)total+=r.count;
            progress.text=warehouse?"已存 "+total.ToString("N0")+" 条 · "+visible.Length+" 种 · 容量不限":"收获 "+Discoveries.Count(r=>r.Acquired)+" / "+catalog.entries.Length+" · 见过 "+Discoveries.Count(r=>r.Seen)+" / "+catalog.entries.Length+" · 异色 "+Discoveries.Count(r=>r.shinyCaptured>0)+" 种";
            collectionTab.image.color=!warehouse?new Color(.15f,.36f,.32f):new Color(.11f,.20f,.23f);warehouseTab.image.color=warehouse?new Color(.15f,.36f,.32f):new Color(.11f,.20f,.23f);
            empty.gameObject.SetActive(visible.Length==0);pageText.text=(page+1)+" / "+pages+"    ·    "+visible.Length+" 个条目";
            for(int i=0;i<PageSize;i++){
                int index=page*PageSize+i;bool active=index<visible.Length;cells[i].gameObject.SetActive(active);if(!active)continue;
                var e=visible[index];var record=Discovery(e.definition.originalId);var appearance=record?.Appearance()??FishArchiveAppearance.Silhouette;
                Display(portraits[i],e,appearance);names[i].text=record!=null&&record.Seen?e.Title:"???";
                states[i].text=warehouse?"库存 "+(Stored(e.definition.originalId)?.count??0).ToString("N0"):State(record,appearance);
                cells[i].image.color=index==selected?new Color(.18f,.39f,.37f):new Color(.14f,.22f,.25f);
            }
            detailImage.gameObject.SetActive(visible.Length>0);appearanceButton.gameObject.SetActive(visible.Length>0);
            if(visible.Length==0){detailTitle.text="仓库空空如也";detailState.text="去海边钓一条鱼吧";detailStats.text="成功收获会永久记入图鉴，\n入库数量不限。";return;}
            var entry=visible[selected];var discovery=Discovery(entry.definition.originalId);bool defaultShiny=discovery!=null&&discovery.originalCaptured==0&&discovery.shinyCaptured>0;
            if(lastSelectedId!=entry.definition.originalId||lastDefaultShiny&&!defaultShiny)showShiny=false;
            lastSelectedId=entry.definition.originalId;lastDefaultShiny=defaultShiny;
            var style=discovery?.Appearance(showShiny)??FishArchiveAppearance.Silhouette;Display(detailImage,entry,style);
            detailTitle.text=discovery!=null&&discovery.Seen?entry.Title:"尚未遇见的鱼";detailState.text=State(discovery,style);
            if(warehouse){var stored=Stored(entry.definition.originalId);detailStats.text="库存总数  "+stored.count.ToString("N0")+"\n原色  "+stored.originalCount.ToString("N0")+"    异色  "+stored.shinyCount.ToString("N0")+"\n最大重量  "+stored.bestWeight.ToString("0.00")+" kg";}
            else detailStats.text=discovery==null||!discovery.Seen?"先将鱼钓出水面，发现它的身影。\n击败并拾取后，解锁完整颜色。":entry.definition.RarityLabel+"  ·  钓起 "+discovery.lifted+" 次\n收获原色 "+discovery.originalCaptured+" · 异色 "+discovery.shinyCaptured+"\n逃走 "+discovery.escaped+" 次 · 纪录 "+discovery.bestWeight.ToString("0.00")+" kg";
            bool toggle=entry.HasShiny&&discovery!=null&&discovery.originalCaptured>0&&discovery.shinyCaptured>0;appearanceButton.interactable=toggle;
            appearanceLabel.text=!entry.HasShiny?"此物种没有异色样式":toggle?(style==FishArchiveAppearance.Shiny?"C  查看原色":"C  查看已收获的异色"):defaultShiny?"当前异色 · 原色尚未收获":"异色尚未收获";
        }
        static string State(FishCollectionRecord record,FishArchiveAppearance appearance){
            if(appearance==FishArchiveAppearance.Silhouette)return "未遇见 · 黑色剪影";
            if(appearance==FishArchiveAppearance.Grayscale)return record!=null&&record.escaped>0?"曾经逃走 · 灰度记录":"已钓起 · 尚未收获";
            return appearance==FishArchiveAppearance.Shiny?"已收获 · 异色样式":"已收获 · 原色样式";
        }
        void Display(RawImage image,FishCatalogEntry entry,FishArchiveAppearance appearance){image.texture=appearance==FishArchiveAppearance.Shiny&&entry.HasShiny?entry.shiny:entry.original;image.color=Color.white;image.material=appearance==FishArchiveAppearance.Silhouette?catalog.silhouetteMaterial:appearance==FishArchiveAppearance.Grayscale?catalog.grayscaleMaterial:null;}
        public void Select(int index){if(visible.Length==0)return;selected=(index%visible.Length+visible.Length)%visible.Length;page=selected/PageSize;showShiny=false;lastSelectedId=-1;Paint();}
        public void ChangePage(int direction){int pages=Mathf.Max(1,Mathf.CeilToInt(visible.Length/(float)PageSize));page=(page+direction+pages)%pages;selected=page*PageSize;showShiny=false;lastSelectedId=-1;Paint();}
        public void ToggleAppearance(){if(visible.Length==0)return;var entry=visible[selected];var record=Discovery(entry.definition.originalId);if(!entry.HasShiny||record==null||record.originalCaptured<=0||record.shinyCaptured<=0)return;showShiny=!showShiny;Paint();}
        public void ShowStatic(FishingArchiveData authoredData,bool storage,int selectedId,bool shiny=false){
            snapshot=authoredData;warehouse=storage;showShiny=false;Build();visible=catalog.entries.Where(e=>!storage||(Stored(e.definition.originalId)?.count??0)>0).ToArray();selected=Mathf.Max(0,Array.FindIndex(visible,e=>e.definition.originalId==selectedId));page=selected/PageSize;lastSelectedId=-1;canvas.gameObject.SetActive(true);Paint();if(shiny){showShiny=true;Paint();}
        }
        RectTransform Box(Transform parent,string name,float x,float y,float w,float h,Color color){var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);var image=g.AddComponent<Image>();image.color=color;image.raycastTarget=false;Place(image.rectTransform,x,y,w,h);return image.rectTransform;}
        Button Button(Transform parent,float x,float y,float w,float h,string text,UnityEngine.Events.UnityAction click){var r=Box(parent,text,x,y,w,h,new Color(.11f,.20f,.23f));var button=r.gameObject.AddComponent<Button>();button.navigation=new Navigation{mode=Navigation.Mode.None};button.targetGraphic=r.GetComponent<Image>();button.targetGraphic.raycastTarget=true;button.onClick.AddListener(click);var label=Label(r,4,2,w-8,h-4,text,15,Color.white);label.alignment=TextAnchor.MiddleCenter;return button;}
        RawImage Portrait(Transform parent,float x,float y,float w,float h){var g=new GameObject("Species portrait",typeof(RectTransform));g.transform.SetParent(parent,false);var image=g.AddComponent<RawImage>();image.raycastTarget=false;Place(image.rectTransform,x,y,w,h);return image;}
        Text Label(Transform parent,float x,float y,float w,float h,string value,int size,Color color){var g=new GameObject("Archive label",typeof(RectTransform));g.transform.SetParent(parent,false);var text=g.AddComponent<Text>();text.font=font?font:module.rig.interfaceFont;text.fontSize=size;text.text=value;text.color=color;text.raycastTarget=false;text.supportRichText=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;Place(text.rectTransform,x,y,w,h);return text;}
        static void Place(RectTransform rect,float x,float y,float w,float h){rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);}
    }
}
