using System;
using System.Linq;
using PaperPixelGames.Framework.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SushiAndFish.FriendsLife
{
    [CreateAssetMenu(menuName = "Sushi & Fish/Friends UI Skin")]
    public sealed class FriendUiSkin : ScriptableObject
    {
        public Sprite paper, primary, secondary, close, toolSlot, userIcon;
        public TMP_FontAsset font;
        public Material graphicMaterial, textMaterial;
        public static FriendUiSkin Load() => Resources.Load<FriendUiSkin>("FriendsLife/FriendUiSkin");
    }

    // All coordinates are the design's 1200 x 720 content coordinates. The tablet's
    // existing frame and Canvas keep ownership of placement, camera and input.
    public static class FriendUi
    {
        public static readonly Color Ink = new Color32(61,46,26,255);
        static Material clippedGraphics;
        static Material GraphicMaterial(FriendUiSkin skin)
        {
            if(!skin||!skin.graphicMaterial)return null;
            if(clippedGraphics)return clippedGraphics;
            var shader=Resources.Load<Shader>("FriendsLife/FriendTabletGraphic");
            if(!shader)return skin.graphicMaterial;
            clippedGraphics=new Material(skin.graphicMaterial){name="Friends tablet graphics",shader=shader,hideFlags=HideFlags.DontSave};
            return clippedGraphics;
        }
        public static void UseTabletGraphic(UnityEngine.UI.Graphic target)
        {
            var skin=FriendUiSkin.Load();
            if(target&&skin&&skin.graphicMaterial)target.material=GraphicMaterial(skin);
        }
        public static RectTransform Rect(string name, Transform parent, float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform)); go.layer=parent.gameObject.layer;
            var r=(RectTransform)go.transform;r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);
            r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
        }
        public static void Stretch(RectTransform rect)
        {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        public static Image Image(string name,Transform parent,float x,float y,float w,float h,Sprite sprite, bool sliced=true)
        {
            var r=Rect(name,parent,x,y,w,h);var image=r.gameObject.AddComponent<Image>();
            image.sprite=sprite;image.color=Color.white;image.raycastTarget=false;
            if(sprite&&sliced&&sprite.border.sqrMagnitude>0)
            {
                image.type=UnityEngine.UI.Image.Type.Sliced;
                float corner=Mathf.Max(.2f,Mathf.Min(w,h)*.07f);
                float border=Mathf.Max(sprite.border.x,sprite.border.y,sprite.border.z,sprite.border.w);
                image.pixelsPerUnitMultiplier=border/(corner*Mathf.Max(.00001f,image.pixelsPerUnit));
            }
            var skin=FriendUiSkin.Load();if(skin&&skin.graphicMaterial)image.material=GraphicMaterial(skin);return image;
        }
        public static TextMeshProUGUI Text(string name,Transform parent,float x,float y,float w,float h,string text,float size=24)
        {
            var r=Rect(name,parent,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();
            var skin=FriendUiSkin.Load();t.font=skin&&skin.font?skin.font:TMP_Settings.defaultFontAsset;
            if(skin&&skin.textMaterial)t.fontSharedMaterial=skin.textMaterial;
            t.text=text??"";t.fontSize=size;t.color=Ink;t.raycastTarget=false;
            // Long text is cut with an ellipsis instead of spilling over its
            // neighbours. A one-line rect keeps its label vertically centred.
            t.enableAutoSizing=false;t.textWrappingMode=TextWrappingModes.Normal;
            t.overflowMode=TextOverflowModes.Ellipsis;t.alignment=h>0&&h<size*2f?TextAlignmentOptions.MidlineLeft:TextAlignmentOptions.TopLeft;return t;
        }
        public static BaseButton Button(string name,Transform parent,float x,float y,float w,float h,string caption,Action click,bool primary=false)
        {
            var skin=FriendUiSkin.Load();var image=Image(name,parent,x,y,w,h,skin?(primary?skin.primary:skin.secondary):null);
            image.raycastTarget=true;var b=image.gameObject.AddComponent<BaseButton>();b.targetGraphic=image;
            var colors=b.colors;colors.disabledColor=new Color(.7843f,.7843f,.7843f,.502f);b.colors=colors;
            var t=Text("Caption",image.transform,12,2,w-24,h-4,caption);t.alignment=TextAlignmentOptions.Center;t.color=new Color32(255,240,184,255);
            t.enableAutoSizing=true;t.fontSizeMin=18;t.fontSizeMax=24;t.overflowMode=TextOverflowModes.Ellipsis;
            if(click!=null)b.Clicked+=click;return b;
        }
        public static TMP_InputField Input(string name,Transform parent,float x,float y,float w,float h,string value,string placeholder="")
        {
            var skin=FriendUiSkin.Load();var background=Image(name,parent,x,y,w,h,skin?skin.paper:null);background.raycastTarget=true;
            var input=background.gameObject.AddComponent<TMP_InputField>();var viewport=Rect("Text Area",background.transform,14,3,w-28,h-6);
            var mask=viewport.gameObject.AddComponent<RectMask2D>();var text=Text("Text",viewport,0,0,w-28,h-6,value);
            var hint=Text("Placeholder",viewport,0,0,w-28,h-6,placeholder);hint.color=new Color(.4f,.3f,.2f,.6f);
            text.overflowMode=hint.overflowMode=TextOverflowModes.Overflow;text.alignment=hint.alignment=TextAlignmentOptions.MidlineLeft;
            input.textViewport=viewport;input.textComponent=text;input.placeholder=hint;input.text=value??"";return input;
        }
        public static ScrollRect Scroll(string name,Transform parent,float x,float y,float w,float h,out RectTransform content)
        {
            var r=Rect(name,parent,x,y,w,h);var scroll=r.gameObject.AddComponent<ScrollRect>();
            var viewport=Rect("Viewport",r,0,0,w,h);viewport.gameObject.AddComponent<RectMask2D>();
            var hit=viewport.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
            content=Rect("Content",viewport,0,0,w,0);scroll.viewport=viewport;scroll.content=content;
            scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=40;return scroll;
        }
        // ---- C009 page frame -------------------------------------------------
        // Safe area inside the 1200x720 paper: L,R,T,B. It keeps every row clear
        // of the paper's stitching (about 25 units in) and the tablet edge.
        public static readonly RectOffset PageInset=new RectOffset(56,56,36,48);
        public const float HeaderH=48f,ToolbarH=50f,FooterH=50f,Gap=16f,ScrollbarW=12f;
        public sealed class FriendPage{public RectTransform paper,safe,header,toolbar,body,footer;public TextMeshProUGUI title,status;}
        // Bands (design units): safe 56..1144 x 36..672, header 36..84,
        // toolbar 100..150, body 100|166..606, footer 622..672.
        public static FriendPage Page(Transform view,string template,string title,bool toolbar=false)
        {
            var page=new FriendPage{paper=Paper(template,view)};
            page.safe=Band("Safe",page.paper,Vector2.zero,Vector2.one,new Vector2(PageInset.left,PageInset.bottom),new Vector2(-PageInset.right,-PageInset.top));
            page.header=Band("Header",page.safe,new Vector2(0,1),Vector2.one,new Vector2(0,-HeaderH),Vector2.zero);
            page.title=Text("Title",page.header,0,0,0,0,title,30);Fill(page.title.rectTransform,0,0,240,0);
            page.title.alignment=TextAlignmentOptions.MidlineLeft;page.title.textWrappingMode=TextWrappingModes.NoWrap;
            float top=HeaderH+Gap;
            if(toolbar){page.toolbar=Band("Toolbar",page.safe,new Vector2(0,1),Vector2.one,new Vector2(0,-(top+ToolbarH)),new Vector2(0,-top));Row(page.toolbar,16,TextAnchor.MiddleLeft);top+=ToolbarH+Gap;}
            page.footer=Band("Footer",page.safe,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,FooterH));Row(page.footer,12,TextAnchor.MiddleRight);
            page.status=Text("FooterStatus",page.footer,0,0,0,0,"",20);page.status.alignment=TextAlignmentOptions.MidlineLeft;page.status.textWrappingMode=TextWrappingModes.NoWrap;
            var slot=page.status.gameObject.AddComponent<LayoutElement>();slot.flexibleWidth=1;slot.minWidth=0;slot.preferredWidth=0;
            page.body=Band("Body",page.safe,Vector2.zero,Vector2.one,new Vector2(0,FooterH+Gap),new Vector2(0,-top));
            return page;
        }
        // Footer buttons sit right of the status line, left to right in call order.
        public static BaseButton FooterButton(FriendPage page,string caption,Action click,float width=220,bool primary=false)
        {
            var b=Button(caption,page.footer,0,0,width,FooterH,caption,click,primary);FitCaption(b);
            var e=b.gameObject.AddComponent<LayoutElement>();e.preferredWidth=width;e.minWidth=width*.6f;e.minHeight=FooterH;return b;
        }
        // One button in the header's reserved right slot (e.g. 返回 on the photo page).
        public static BaseButton HeaderButton(FriendPage page,string caption,Action click,float width=220)
        {
            var b=Button(caption,page.header,0,0,width,HeaderH,caption,click);var r=(RectTransform)b.transform;
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(1,.5f);r.anchoredPosition=Vector2.zero;FitCaption(b);return b;
        }
        // Width rules for a toolbar or footer child (search box, page label).
        public static T Flexible<T>(T item,float preferredWidth=0,float flexible=1) where T:Component
        {var e=item.gameObject.AddComponent<LayoutElement>();e.preferredWidth=preferredWidth;e.minWidth=0;e.flexibleWidth=flexible;return item;}
        // Self-sizing scroll list: rows come from ListRow/ListButton/Paragraph.
        public static ScrollRect List(RectTransform host,out RectTransform content,float spacing=12)
        {
            var scroll=ScrollFrame("List",host,out var viewport);
            content=Band("Content",viewport,new Vector2(0,1),Vector2.one,Vector2.zero,Vector2.zero);content.pivot=new Vector2(.5f,1);
            var v=content.gameObject.AddComponent<VerticalLayoutGroup>();v.padding=new RectOffset(4,4,4,24);v.spacing=spacing;
            v.childControlWidth=true;v.childControlHeight=true;v.childForceExpandWidth=true;v.childForceExpandHeight=false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.content=content;return scroll;
        }
        // Transitional scroll for hand-placed rows (x,y,w,h). Rows authored for
        // designWidth are scaled down to the viewport, so older 1120-wide pages
        // keep their margins. designWidth 0 = viewport width (no scaling).
        public static ScrollRect FitScroll(string name,RectTransform host,float designWidth,out RectTransform content)
        {
            var scroll=ScrollFrame(name,host,out var viewport);float width=Mathf.Max(1,viewport.rect.width);if(designWidth<=0)designWidth=width;
            content=Rect("Content",viewport,0,0,designWidth,0);content.localScale=Vector3.one*Mathf.Min(1,width/designWidth);
            scroll.content=content;return scroll;
        }
        // Content height from the last y, plus bottom padding (no blank scroll).
        public static void FitContent(RectTransform content,float y){content.sizeDelta=new Vector2(content.sizeDelta.x,y+24);}
        public static TextMeshProUGUI Paragraph(Transform list,string text,float size=24)
        {var t=Text("Paragraph",list,0,0,0,0,text,size);t.alignment=TextAlignmentOptions.TopLeft;t.gameObject.AddComponent<LayoutElement>().minHeight=size*1.4f;return t;}
        public static BaseButton ListButton(Transform list,string caption,Action click,bool primary=false,float minHeight=64)
        {var b=Button("Row",list,0,0,1000,minHeight,caption,click,primary);FitCaption(b);var e=b.gameObject.AddComponent<LayoutElement>();e.minHeight=e.preferredHeight=minHeight;return b;}
        public static RectTransform ListRow(Transform list,float height=56)
        {var r=Rect("Row",list,0,0,1000,height);var e=r.gameObject.AddComponent<LayoutElement>();e.minHeight=e.preferredHeight=height;return r;}
        // Measured text for hand-placed pages: the height follows the text and y moves past it.
        public static TextMeshProUGUI Flow(Transform content,ref float y,string name,string text,float size=24,float width=1120,float gap=16,float x=0)
        {
            var t=Text(name,content,x,y,width,10,text,size);t.alignment=TextAlignmentOptions.TopLeft;
            float h=Mathf.Ceil(t.GetPreferredValues(text??"",width,Mathf.Infinity).y)+4;t.rectTransform.sizeDelta=new Vector2(width,h);y+=h+gap;return t;
        }
        public static RectTransform Band(string name,Transform parent,Vector2 anchorMin,Vector2 anchorMax,Vector2 offsetMin,Vector2 offsetMax)
        {var r=Rect(name,parent,0,0,0,0);r.anchorMin=anchorMin;r.anchorMax=anchorMax;r.pivot=new Vector2(.5f,.5f);r.offsetMin=offsetMin;r.offsetMax=offsetMax;return r;}
        public static void Fill(RectTransform rect,float left,float bottom,float right,float top)
        {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,.5f);rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(-right,-top);}
        public static void FitCaption(BaseButton button){var caption=button?button.GetComponentInChildren<TMP_Text>():null;if(caption)Fill(caption.rectTransform,12,2,12,2);}
        public static HorizontalLayoutGroup Row(RectTransform rect,float spacing,TextAnchor alignment)
        {
            var h=rect.gameObject.AddComponent<HorizontalLayoutGroup>();h.spacing=spacing;h.childAlignment=alignment;
            h.childControlWidth=true;h.childControlHeight=true;h.childForceExpandWidth=false;h.childForceExpandHeight=true;return h;
        }
        static ScrollRect ScrollFrame(string name,RectTransform host,out RectTransform viewport)
        {
            var r=Band(name,host,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);var scroll=r.gameObject.AddComponent<ScrollRect>();
            viewport=Band("Viewport",r,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(-(ScrollbarW+4),0));viewport.gameObject.AddComponent<RectMask2D>();
            var hit=viewport.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
            scroll.viewport=viewport;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=40;
            scroll.verticalScrollbar=VerticalScrollbar(r);scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;return scroll;
        }
        static Scrollbar VerticalScrollbar(RectTransform host)
        {
            var track=Band("Scrollbar",host,new Vector2(1,0),Vector2.one,new Vector2(-ScrollbarW,0),Vector2.zero);
            var back=track.gameObject.AddComponent<Image>();back.color=new Color(Ink.r,Ink.g,Ink.b,.15f);UseTabletGraphic(back);
            var area=Band("SlidingArea",track,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var handle=Band("Handle",area,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);var grip=handle.gameObject.AddComponent<Image>();grip.color=new Color(Ink.r,Ink.g,Ink.b,.55f);UseTabletGraphic(grip);
            var bar=track.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle;bar.targetGraphic=grip;bar.direction=Scrollbar.Direction.BottomToTop;
            var navigation=bar.navigation;navigation.mode=Navigation.Mode.None;bar.navigation=navigation;return bar;
        }
        // Player text guard: curated short Chinese passes. Raw exception text,
        // English and system words are logged and replaced by the fallback.
        static readonly string[] SystemWords={"原生","原服务","原机","原电梯","原操作","原会话","原事件","原选择","原输入","原计划","原同住","原入住","原旅行","原结果","原参与者","原家庭","原居民","原角色","原捏人","原公共","原房门","原有道路","原舱","原场地","源参数","源规则","源标志","投影","租约","宿主","回读","交接","主机"};
        public static string PlayerText(string message,string fallback)
        {
            if(string.IsNullOrEmpty(message))return fallback;
            int longest=0,run=0;foreach(char c in message){if(c<128&&char.IsLetter(c)){run++;longest=Mathf.Max(longest,run);}else run=0;}
            bool plain=message.Length<=60&&(longest<3||message.Contains("Windows"))&&!SystemWords.Any(w=>message.IndexOf(w,StringComparison.Ordinal)>=0)&&message.Any(c=>c>=0x4e00&&c<=0x9fff);
            if(plain)return message;
            Debug.LogWarning("[FriendsLife] Player message replaced: "+message);return fallback;
        }
        public static RectTransform Paper(string template,Transform parent)
        {
            var prefab=Resources.Load<GameObject>("FriendsLife/Templates/"+template);
            if(prefab)
            {
                var root=(RectTransform)UnityEngine.Object.Instantiate(prefab,parent,false).transform;root.name=template;root.anchorMin=root.anchorMax=new Vector2(0,1);root.pivot=new Vector2(0,1);root.anchoredPosition=Vector2.zero;
                var paper=root.Find("Paper") as RectTransform;
                if(paper)
                {
                    DestroyChildren(paper);var graphic=paper.GetComponent<Image>();
                    if(graphic&&graphic.sprite)
                    {
                        float border=Mathf.Max(graphic.sprite.border.x,graphic.sprite.border.y,graphic.sprite.border.z,graphic.sprite.border.w);
                        float corner=Mathf.Max(.2f,Mathf.Min(paper.rect.width,paper.rect.height)*.07f);
                        graphic.pixelsPerUnitMultiplier=border/(corner*Mathf.Max(.00001f,graphic.pixelsPerUnit));
                        graphic.material=GraphicMaterial(FriendUiSkin.Load());
                    }
                    return paper;
                }
            }
            var skin=FriendUiSkin.Load();return (RectTransform)Image(template,parent,0,0,1200,720,skin?skin.paper:null).transform;
        }
        public static void DestroyChildren(Transform parent)
        {for(int i=parent.childCount-1;i>=0;i--){var go=parent.GetChild(i).gameObject;if(Application.isPlaying){go.SetActive(false);UnityEngine.Object.Destroy(go);}else UnityEngine.Object.DestroyImmediate(go);}}
        public static RawImage Portrait(string name,Transform parent,float x,float y,float w,float h)
        {
            var skin=FriendUiSkin.Load();var frame=Image(name+"Frame",parent,x,y,w,h,skin?skin.toolSlot:null).rectTransform;
            float padding=Mathf.Max(6,Mathf.Min(w,h)*.08f),side=Mathf.Min(w,h)-padding*2;
            var raw=Rect(name,frame,(w-side)*.5f,(h-side)*.5f,side,side).gameObject.AddComponent<RawImage>();raw.raycastTarget=false;UseTabletGraphic(raw);
            var placeholder=Image("Placeholder",frame,(w-side*.70f)*.5f,(h-side*.70f)*.5f,side*.70f,side*.70f,skin?skin.userIcon:null,false);
            placeholder.preserveAspect=true;placeholder.color=Ink;
            var visibility=frame.gameObject.AddComponent<FriendPortraitPlaceholder>();visibility.Bind(raw,placeholder);return raw;
        }
    }
    public sealed class FriendPortraitPlaceholder:MonoBehaviour
    {
        RawImage portrait;Image placeholder;
        public void Bind(RawImage image,Image fallback){portrait=image;placeholder=fallback;Refresh();}
        void LateUpdate()=>Refresh();
        void Refresh(){if(!portrait||!placeholder)return;bool ready=portrait.texture!=null;portrait.enabled=ready;placeholder.enabled=!ready;}
    }
    public sealed class FriendAppSlotVisual:MonoBehaviour
    {
        Transform root;float next;
        public void Bind(Transform owner){root=owner;enabled=true;next=0;}
        void LateUpdate()
        {
            if(!root||Time.unscaledTime<next)return;next=Time.unscaledTime+.25f;
            var slot=root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t=>t.name=="T00_TabletApp_Friends");if(!slot||!slot.parent)return;
            // The existing staff app sprite already contains the game's frame
            // and a two-person pictogram. Keep that exact original artwork on
            // the Friends clone rather than replacing the whole tile with a
            // white, unframed alpha icon.
            var original=slot.parent.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>!i.transform.IsChildOf(slot)&&i.sprite&&i.sprite.name=="UI_StaffApp");
            if(!original)return;var sourceRoot=original.transform;while(sourceRoot.parent&&sourceRoot.parent!=slot.parent)sourceRoot=sourceRoot.parent;
            foreach(var image in sourceRoot.GetComponentsInChildren<Image>(true))
            {
                string path="";for(var at=image.transform;at!=sourceRoot;at=at.parent)path=string.IsNullOrEmpty(path)?at.name:at.name+"/"+path;
                var target=string.IsNullOrEmpty(path)?slot:slot.Find(path);var copy=target?target.GetComponent<Image>():null;if(!copy)continue;
                copy.sprite=image.sprite;copy.type=image.type;copy.preserveAspect=image.preserveAspect;copy.color=image.color;copy.material=image.material;
            }
            enabled=false;
        }
    }
}
