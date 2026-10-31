using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TomodachiLife;
using TomodachiLife.Simulation;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendTabletPresenter
    {
        readonly Dictionary<string,int> catalogPages=new Dictionary<string,int>();readonly Dictionary<string,string> catalogFilters=new Dictionary<string,string>();int clothColor;
        string renderedGameControls;
        static string SourceGameControlsKey(LifeMinigameSession session)=>session==null?null:session.id+"|"+session.definitionId+"|"+session.round+"|"+session.phase+"|"+session.completed+"|"+(session.definitionId=="CoinTurn"?session.answer:0)+"|"+string.Join("\u001f",session.options);
        bool SourceGameControlsUnchanged()=>page=="Source:games"&&FriendsLifeRuntime.Instance?.Core?.State.activities?.minigame is LifeMinigameSession game&&renderedGameControls==SourceGameControlsKey(game);
        bool RenderSourceContent(string category,Transform paper,RectTransform content,ref float y)
        {
            if(RenderSourceSocialPage(category,content,ref y))return true;
            var runtime=FriendsLifeRuntime.Instance;var person=runtime?.GetPerson(personId);if(person==null)return false;var core=runtime.Core;
            if(RenderAdditionalSourceContent(category,paper,content,ref y))return true;
            if(category=="family-housing"){RenderFamilyHomeSelection(content,ref y);return true;}
            if(category=="activities")
            {
                foreach(var item in ActivitiesMenu())Menu(content,ref y,item[0],item[1]);return true;
            }
            if(category=="food")
            {foreach(var item in CatalogPage(category,paper,core.Rules.Data.foods,i=>NativeLifeLocalization.Food(i.name)+i.name)){string id=item.name;long price=item.priceRaw;SourceRow(content,ref y,NativeLifeLocalization.Food(id)+"   "+(price/100m).ToString("0.00"),()=>ConfirmCommand(NativeLifeLocalization.Food(id),price,"food:"+id));}return true;}
            if(category=="goods")
            {foreach(var item in CatalogPage(category,paper,core.Rules.Data.goods.Where(i=>i.sourceUnlocked),i=>NativeLifeLocalization.Goods(i.name)+i.name)){string id=item.name;long price=item.buyingPriceRaw;SourceRow(content,ref y,NativeLifeLocalization.Goods(id)+"   "+(price/100m).ToString("0.00"),()=>ConfirmCommand(NativeLifeLocalization.Goods(id),price,"gift:"+id));}return true;}
            if(category=="cloth")
            {
                var personal=core.State.creative.residents.FirstOrDefault(r=>r.residentId==person.nativeResidentId);
                foreach(var item in CatalogPage(category,paper,core.CreativeCatalog.Data.clothes,i=>i.name+i.id,true))
                {string id=item.id;int color=clothColor%Math.Max(1,item.colors);bool owned=personal?.wardrobe.Any(c=>c.clothId==id&&c.color==color)==true;long price=item.price;
                    var b=SourceRow(content,ref y,item.name+" · "+(color+1)+"   "+(owned?"已拥有":(price/100m).ToString("0.00")),()=>{if(owned)Command("cloth-equip:"+id,color.ToString());else ConfirmCommand(item.name,price,"cloth:"+id,color.ToString());});b.interactable=owned||item.forSale;}
                return true;
            }
            if(category=="personal")
            {foreach(var item in CatalogPage(category,paper,core.CreativeCatalog.Data.habits,i=>i.name+i.id)){string id=item.id;long price=item.price;SourceRow(content,ref y,item.name,()=>ConfirmCommand(item.name,price,"habit:"+id));}return true;}
            if(category=="phrases")
            {
                var phrase=FriendUi.Input("PlayerPhrase",content,0,0,1120,64,"","输入词句");y=86;
                SourceRow(content,ref y,"口头禅",()=>Command("phrase:"+phrase.text));SourceRow(content,ref y,"话题",()=>Command("topic:"+phrase.text));return true;
            }
            if(category=="pet")
            {SourceRow(content,ref y,"喂食",()=>Command("pet-feed"));SourceRow(content,ref y,"玩耍",()=>Command("pet-play"));return true;}
            if(category=="family")
            {
                RenderPendingHumanFamilyChoices(content,ref y);
                Menu(content,ref y,"孩子","family-children");Menu(content,ref y,"家庭回忆","family-history");
                if(FriendsLifeFeatureFlags.Cohabit&&HasFamilyHomeSelection())Menu(content,ref y,"选择同住住宅","family-housing");
                if(FriendsLifeFeatureFlags.HousingUpgrade&&FriendsEconomyService.Instance?.SplitHomeMembers(personId).Length>0)SourceRow(content,ref y,"分住",()=>{if(!FriendHousingUI.BeginSplit(personId))Status("住宅暂不可调整，请稍后再试。");});
                foreach(var pair in FamilyActions()){string action=pair[1];SourceRow(content,ref y,pair[0],()=>StartSelectedFamilyAction(action,person.personId));}return true;
            }
            if(category.StartsWith("family-target/"))
            {
                string target=category.Substring("family-target/".Length);
                foreach(var pair in FamilyActions())
                {string action=pair[1];SourceRow(content,ref y,pair[0],()=>Command("family-start:"+action,new JObject{["targetPersonId"]=target}.ToString(Newtonsoft.Json.Formatting.None)));}return true;
            }
            if(category.StartsWith("family-choose/",StringComparison.Ordinal))
            {
                string action=category.Substring("family-choose/".Length);
                foreach(var target in runtime.People.Where(x=>x.personId!=person.personId))
                {
                    string id=target.personId;string reason=core.KernelFamilyActionReason(person.nativeResidentId,target.nativeResidentId,action);
                    var button=SourceRow(content,ref y,target.displayName,()=>Command("family-start:"+action,new JObject{["targetPersonId"]=id}.ToString(Newtonsoft.Json.Formatting.None)));
                    button.interactable=reason==null;
                    if(reason!=null)FriendUi.Flow(content,ref y,"FamilyReason_"+id,reason,22);
                }
                return true;
            }
            if(category=="games")
            {
                var session=core.State.activities?.minigame;
                renderedGameControls=SourceGameControlsKey(session);
                if(session!=null&&session.residentId==person.nativeResidentId)
                {
                    if(session.completed){FriendUi.Flow(content,ref y,"SourceGameResult",session.message,26);SourceRow(content,ref y,"返回",()=>Command("minigame-leave"));return true;}
                    FriendUi.Flow(content,ref y,"SourcePrompt",session.prompt,24);
                    FriendMinigameVisuals.Append(content,ref y,session);
                    RenderMinigameControls(session,content,ref y);
                    SourceRow(content,ref y,"离开",()=>Command("minigame-leave"));return true;
                }
                return false;
            }
            if(category=="travel")
            {
                var sessions=core.State.activities?.travels?.Where(t=>t.residentId==person.nativeResidentId&&!t.returned).ToArray();if(sessions==null||sessions.Length==0)return false;
                foreach(var trip in sessions){FriendUi.Flow(content,ref y,"TravelMessage_"+trip.id,trip.message+(!trip.awaitingChoice?"\n约 "+Math.Max(0,trip.readyAt-core.State.gameMinutes).ToString("0")+" 分钟后到下一站":""),24);if(trip.awaitingChoice)for(int i=0;i<trip.choices.Count;i++){string id=trip.id;int option=i;SourceRow(content,ref y,trip.choices[i],()=>Command("travel-choice:"+id,option.ToString()));}string tripId=trip.id;SourceRow(content,ref y,"提前回来（没有礼物）",()=>Command("travel-return:"+tripId));}return true;
            }
            if(category=="dream")
            {
                var session=core.State.activities?.dreams?.FirstOrDefault(d=>d.residentId==person.nativeResidentId&&!d.woken);
                if(session!=null){FriendUi.Flow(content,ref y,"DreamMessage_"+session.id,session.message,24);if(session.awaitingChoice)for(int i=0;i<session.choices.Count;i++){int option=i;SourceRow(content,ref y,session.choices[i],()=>Command("dream-choice:"+session.id,option.ToString()));}SourceRow(content,ref y,"醒来",()=>Command("wake:"+session.id));}
                else foreach(var definition in LifeActivitiesCatalog.Dreams){string id=definition.id;SourceRow(content,ref y,definition.title,()=>Command("dream:"+id));}return true;
            }
            if(category=="facilities")
            {foreach(var item in LifeActivitiesCatalog.Facilities){string id=item.id;SourceRow(content,ref y,item.title,()=>Command("facility:"+id));}return true;}
            if(category=="market")
            {foreach(var offer in core.State.activities.market){string id=offer.id;SourceRow(content,ref y,NativeLifeLocalization.Text(offer.itemName)+"   "+(offer.unitPrice/100m).ToString("0.00"),()=>ConfirmCommand(NativeLifeLocalization.Text(offer.itemName),offer.unitPrice,"market:"+id));}return true;}
            if(category=="recycle")
            {foreach(var stack in person.inventory.Where(s=>s.category=="Goods"&&s.quantity>0)){string id=stack.category+"/"+stack.itemName;SourceRow(content,ref y,NativeLifeLocalization.Goods(stack.itemName)+" ×"+stack.quantity,()=>Command("sell:"+id,"1"));}return true;}
            if(category=="donate")
            {var amount=FriendUi.Input("Amount",content,0,0,1120,64,"1","");y=86;SourceRow(content,ref y,"捐款",()=>{if(decimal.TryParse(amount.text,out var price)&&price>0)ConfirmCommand("捐款",(long)(price*100),"donate:"+(long)(price*100));});SourceRow(content,ref y,"收取",()=>Command("donations-collect"));return true;}
            if(category=="workshop")
            {
                foreach(var design in core.State.creative.designs){string id=design.id;SourceRow(content,ref y,design.title,()=>OpenSourcePage(personId,"design/"+id));}
                var title=FriendUi.Input("DesignName",content,0,y,1120,64,"","作品名称");y+=86;foreach(string kind in new[]{"Food","Clothing","Pet","Decor","Building"}){string value=kind;SourceRow(content,ref y,DesignLabel(kind),()=>Command("design-create:"+title.text,value));}return true;
            }
            if(category.StartsWith("design/"))
            {
                string id=category.Substring(7);var design=core.State.creative.designs.FirstOrDefault(d=>d.id==id);if(design==null)return true;
                var canvas=FriendUi.Rect("SourceDrawing",content,0,0,500,400);var drawing=canvas.gameObject.AddComponent<FriendSourceDrawing>();drawing.Bind(personId,design);y=420;
                string[] tools={"Pen","Heart","Star","Face","Circle"};for(int i=0;i<tools.Length;i++){string tool=tools[i];FriendUi.Button("Tool_"+tool,content,540+(i%3)*190,(i/3)*68,180,56,tool=="Pen"?"画笔":tool=="Heart"?"心":tool=="Star"?"星":tool=="Face"?"脸":"圆",()=>drawing.tool=tool);}
                for(int i=0;i<FriendSourceDrawing.Palette.Length;i++){var color=FriendSourceDrawing.Palette[i];var b=FriendUi.Button("Colour_"+i,content,540+(i%6)*94,142+(i/6)*62,84,52,"●",()=>drawing.rgba=FriendSourceDrawing.Packed(color));var text=b.GetComponentInChildren<TMPro.TMP_Text>();text.color=color;}
                for(int i=0;i<3;i++){int radius=i;FriendUi.Button("Brush_"+i,content,540+i*190,272,180,56,(i*2+1)+"像素",()=>drawing.radius=radius);}
                var titleInput=FriendUi.Input("Title",content,540,344,580,56,design.title,"作品名称");var textInput=FriendUi.Input("Text",content,540,420,580,56,design.text,"作品上的文字");
                y=500;SourceRow(content,ref y,"保存作品",()=>Command("design-text:"+id,new JObject{["title"]=titleInput.text,["text"]=textInput.text}.ToString(Newtonsoft.Json.Formatting.None)));
                SourceRow(content,ref y,"清空画布",()=>Command("design-clear:"+id,FriendSourceDrawing.Packed(FriendSourceDrawing.Palette[0]).ToString()));
                FriendUi.Flow(content,ref y,"DesignStock","现有份数："+design.stock,24);
                SourceRow(content,ref y,"再制作一份",()=>Command("design-craft:"+id));SourceRow(content,ref y,"使用",()=>Command("design-use:"+id));
                if(design.kind=="Decor")SourceRow(content,ref y,"把作品铺成壁纸/地板",()=>Command("design-room:"+id));
                Menu(content,ref y,"房间摆设","decorations");return true;
            }
            if(category=="memories")
            {foreach(var photo in core.State.activities.photos.Where(p=>p.residentId==person.nativeResidentId))FriendUi.Flow(content,ref y,"Photo_"+photo.id,photo.label,24);return true;}
            return false;
        }
        IEnumerable<T> CatalogPage<T>(string category,Transform paper,IEnumerable<T> entries,Func<T,string> searchable,bool colors=false)
        {
            catalogFilters.TryGetValue(category,out var filter);filter=filter??"";
            // The filter bar takes the top of the body; the list moves below it.
            var bar=FriendUi.Band("CatalogBar",current.body,new Vector2(0,1),Vector2.one,new Vector2(0,-FriendUi.ToolbarH),Vector2.zero);FriendUi.Row(bar,16,TextAnchor.MiddleLeft);
            ((RectTransform)scroll.transform).offsetMax=new Vector2(0,-(FriendUi.ToolbarH+FriendUi.Gap));
            var input=FriendUi.Flexible(FriendUi.Input("Filter",bar,0,0,600,FriendUi.ToolbarH,filter,"搜索"));input.onEndEdit.AddListener(value=>{catalogFilters[category]=value;catalogPages[category]=0;Render();});
            if(colors)
            {
                var less=FriendUi.Button("ColorPrevious",bar,0,0,150,FriendUi.ToolbarH,"颜色 −",()=>{clothColor=(clothColor+7)%8;Render();});FriendUi.FitCaption(less);FriendUi.Flexible(less,150,0);
                var color=FriendUi.Text("Color",bar,0,0,80,FriendUi.ToolbarH,(clothColor+1).ToString(),24);color.alignment=TMPro.TextAlignmentOptions.Center;FriendUi.Flexible(color,80,0);
                var more=FriendUi.Button("ColorNext",bar,0,0,150,FriendUi.ToolbarH,"颜色 +",()=>{clothColor=(clothColor+1)%8;Render();});FriendUi.FitCaption(more);FriendUi.Flexible(more,150,0);
            }
            var list=entries.Where(i=>string.IsNullOrEmpty(filter)||searchable(i).IndexOf(filter,StringComparison.OrdinalIgnoreCase)>=0).ToArray();int count=Math.Max(1,(list.Length+4)/5);catalogPages.TryGetValue(category,out int pageIndex);pageIndex=Mathf.Clamp(pageIndex,0,count-1);catalogPages[category]=pageIndex;
            var previous=FriendUi.FooterButton(current,"上一页",()=>{catalogPages[category]=pageIndex-1;Render();},200);previous.interactable=pageIndex>0;var next=FriendUi.FooterButton(current,"下一页",()=>{catalogPages[category]=pageIndex+1;Render();},200);next.interactable=pageIndex<count-1;
            var label=FriendUi.Text("Page",current.footer,0,0,120,FriendUi.FooterH,(pageIndex+1)+" / "+count,24);label.alignment=TMPro.TextAlignmentOptions.Center;FriendUi.Flexible(label,120,0);return list.Skip(pageIndex*5).Take(5);
        }
        // Tablet entries for side features. Each one is listed only while its
        // FriendsLifeFeatureFlags gate is on; saved data is never touched.
        static readonly string[][] ActivityEntries={new[]{"食品","food"},new[]{"送礼","goods"},new[]{"使用物品","goods-use"},new[]{"服装","cloth"},new[]{"衣柜","wardrobe"},new[]{"房间","room"},new[]{"性格","traits"},new[]{"微个性","personal"},new[]{"口头禅与话题","phrases"},new[]{"分享话题","culture"},new[]{"人际关系","family"},new[]{"小游戏","games"},new[]{"旅行","travel"},new[]{"梦","dream"},new[]{"摄影与回忆","memories"},new[]{"原创作品","workshop"},new[]{"宠物","pet"},new[]{"设施","facilities"},new[]{"新闻","news"},new[]{"近况","accidents"},new[]{"喷泉","fountain"},new[]{"市场","market"},new[]{"回收","recycle"},new[]{"捐款","donate"}};
        static string[][] ActivitiesMenu()=>ActivityEntries.Where(e=>SourceCategoryEnabled(e[1])).ToArray();
        static bool SourceCategoryEnabled(string category)
        {
            switch(category)
            {
                case "social":case "jobs":return true;
                case "activities":return ActivitiesMenu().Length>0;
                case "food":case "goods":case "goods-use":case "cloth":case "wardrobe":case "room":case "traits":case "personal":case "phrases":case "culture":case "pet":case "workshop":case "decorations":case "items":return FriendsLifeFeatureFlags.Creative;
                // 人际关系 is the player-commanded 16-action page (no Tomodachi equivalent); it returns only with romance.
                case "family":case "family-history":return FriendsLifeFeatureFlags.AutoRomance;
                case "family-children":return FriendsLifeFeatureFlags.Children;
                case "family-housing":return FriendsLifeFeatureFlags.Cohabit;
                case "games":return FriendsLifeFeatureFlags.Minigames;
                case "travel":return FriendsLifeFeatureFlags.Travel;
                case "dream":return FriendsLifeFeatureFlags.Dreams;
                case "photography":case "memories":return FriendsLifeFeatureFlags.Photography;
                case "facilities":case "news":case "accidents":case "fountain":case "market":case "recycle":case "donate":return FriendsLifeFeatureFlags.IslandExtras;
            }
            if(category.StartsWith("family-target/",StringComparison.Ordinal)||category.StartsWith("family-choose/",StringComparison.Ordinal))return FriendsLifeFeatureFlags.AutoRomance;
            if(category.StartsWith("design/",StringComparison.Ordinal))return FriendsLifeFeatureFlags.Creative;
            return false;
        }
        static string[][] FamilyActions()=>new[]{new[]{"交谈","Befriend"},new[]{"约定","BestFriends"},new[]{"发生争执","Fight"},new[]{"道歉","Reconcile"},new[]{"表白","Confess"},new[]{"约会","Date"},new[]{"求婚","Propose"},new[]{"婚礼","Wedding"},new[]{"分开","Breakup"},new[]{"复合","Reunite"},new[]{"孩子","Baby"},new[]{"迎接","Birth"},new[]{"照顾","Care"},new[]{"成长","Grow"},new[]{"安慰","Comfort"},new[]{"同住","Cohabit"}};
        string DesignLabel(string kind)=>kind=="Food"?"食品":kind=="Clothing"?"服装":kind=="Pet"?"宠物":kind=="Building"?"建筑":"摆设";
        string MinigameInputLabel(string action)=>action=="tap"?"点击":action=="shoot"?"发射":action=="stop"?"停止":action=="left"?"左":action=="right"?"右":action=="jump"?"跳跃":action=="catch"?"接住":"投掷";
        void RenderMinigameControls(LifeMinigameSession session,Transform content,ref float y)
        {
            switch(session.definitionId)
            {
                case "Bowling":
                    var aim=FriendUi.Input("Aim",content,0,y,500,56,"0","方向 -1～1");var power=FriendUi.Input("Power",content,580,y,500,56,"0.8","力度 0～1");y+=76;
                    SourceRow(content,ref y,"投球",()=>{float.TryParse(aim.text,out var a);float.TryParse(power.text,out var p);Command("minigame-input:throw",new JObject{["option"]=0,["value"]=p,["secondary"]=a}.ToString(Newtonsoft.Json.Formatting.None));});break;
                case "CoinTurn":
                    if(session.phase==0){SourceRow(content,ref y,"正面",()=>Command("minigame-input:predict","0"));SourceRow(content,ref y,"反面",()=>Command("minigame-input:predict","1"));
                        var spin=FriendUi.Input("Power",content,0,y,1120,56,"0.5","力度 0～1");y+=76;SourceRow(content,ref y,"转动",()=>{float.TryParse(spin.text,out var p);Command("minigame-input:spin",new JObject{["value"]=p}.ToString(Newtonsoft.Json.Formatting.None));});}
                    else if(session.phase==2)SourceRow(content,ref y,"下一轮",()=>Command("minigame-input:next"));break;
                case "FerrisWheelPush":for(int step=1;step<=3;step++){int option=step;SourceRow(content,ref y,"推进 "+step,()=>Command("minigame-input:push",option.ToString()));}break;
                case "FindTheOnly":
                    for(int i=0;i<9;i++){int option=i;float x=(i%3)*360,row=(i/3)*100;var b=FriendUi.Button("Grid_"+i,content,x,y+row,330,80,"",()=>Command("minigame-input:answer",option.ToString()));FriendMinigameVisuals.OddPortrait(b.transform,session,i);}
                    y+=320;break;
                case "StatuesGame":FriendUi.Text("Signal",content,0,y,1120,60,session.phase==0?"绿灯":"红灯",30);y+=80;SourceRow(content,ref y,"前进",()=>Command("minigame-input:move")).interactable=session.phase==0;break;
                case "ThreeShellGame":for(int i=0;i<3;i++){int option=i;var b=SourceRow(content,ref y,"杯子 "+(i+1),()=>Command("minigame-input:answer",option.ToString()));b.interactable=session.phase==1;}break;
                case "Dummy":SourceRow(content,ref y,"点击",()=>Command("minigame-input:tap"));break;
                case "ViewFromFerrisWheel":SourceRow(content,ref y,"左转",()=>Command("minigame-input:rotate","-1"));SourceRow(content,ref y,"右转",()=>Command("minigame-input:rotate","1"));SourceRow(content,ref y,"拍照",()=>Command("minigame-input:shutter"));break;
                default:for(int i=0;i<session.options.Count;i++){int option=i;string key=session.options[i];string label=key.StartsWith("F:")?NativeLifeLocalization.Food(key.Substring(2)):key.StartsWith("G:")?NativeLifeLocalization.Goods(key.Substring(2)):FriendsLifeRuntime.Instance.PersonFromNative(key)?.displayName??string.Join("＋",key.Split('+').Select(FriendMinigameVisuals.ObjectLabel));string input=session.definitionId=="EastAndWest"||session.definitionId=="EastAndWestGroup"?"choose":"answer";SourceRow(content,ref y,label,()=>Command("minigame-input:"+input,option.ToString()));}break;
            }
        }
        void ConfirmCommand(string title,long cents,string action,string option=null)
        {
            var old=view.Find("Status");if(old)Destroy(old.gameObject);var shield=FriendUi.Rect("Status",view,0,0,1200,720);var hit=shield.gameObject.AddComponent<UnityEngine.UI.Image>();hit.color=new Color(0,0,0,.001f);hit.raycastTarget=true;
            var p=FriendUi.Rect("T05_Confirm",shield,260,200,680,320);var skin=FriendUiSkin.Load();FriendUi.Image("Paper",p,0,0,680,320,skin?skin.paper:null);
            FriendUi.Text("Title",p,40,30,600,44,title,30);FriendUi.Text("Body",p,40,84,600,128,(cents/100m).ToString("0.00")+" 金币",24);
            FriendUi.Button("Confirm",p,360,226,264,54,"确认",()=>{Destroy(shield.gameObject);Command(action,option);},true);FriendUi.Button("Back",p,56,226,264,54,"返回",()=>Destroy(shield.gameObject));
        }
    }
    public sealed class FriendSourceDrawing:MonoBehaviour,UnityEngine.EventSystems.IPointerDownHandler,UnityEngine.EventSystems.IDragHandler
    {
        // Exact NativeResidentWardrobe.LifeCreativeTextures palette.
        public static readonly Color32[] Palette={new Color32(255,244,206,255),new Color32(24,56,67,255),new Color32(235,86,93,255),new Color32(255,166,67,255),new Color32(255,215,71,255),new Color32(111,192,103,255),new Color32(72,171,178,255),new Color32(78,133,213,255),new Color32(159,113,203,255),new Color32(237,148,189,255),new Color32(141,97,68,255),new Color32(255,255,255,255)};
        public static int Packed(Color32 c)=>unchecked((int)((uint)c.r<<24|(uint)c.g<<16|(uint)c.b<<8|c.a));
        public string tool="Pen";public int rgba=Packed(Palette[1]),radius;
        string personId;LifeCreativeDesign design;Texture2D texture;UnityEngine.UI.RawImage image;Vector2Int previous;
        public void Bind(string person,LifeCreativeDesign source)
        {personId=person;design=source;image=gameObject.AddComponent<UnityEngine.UI.RawImage>();FriendUi.UseTabletGraphic(image);texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);texture.filterMode=FilterMode.Point;var colors=new Color32[source.width*source.height];for(int i=0;i<colors.Length;i++){int c=source.pixels!=null&&i<source.pixels.Length?source.pixels[i]:-1;colors[i]=new Color32((byte)(c>>24),(byte)(c>>16),(byte)(c>>8),(byte)c);}texture.SetPixels32(colors);texture.Apply();image.texture=texture;}
        void OnEnable(){if(FriendsLifeRuntime.Instance)FriendsLifeRuntime.Instance.Changed+=Refresh;}
        void OnDisable(){if(FriendsLifeRuntime.Instance)FriendsLifeRuntime.Instance.Changed-=Refresh;}
        void Refresh()
        {if(design==null||!image)return;var updated=FriendsLifeRuntime.Instance?.Core?.State.creative.designs.FirstOrDefault(d=>d.id==design.id);if(updated==null||updated.pixels==null)return;design=updated;var colors=updated.pixels.Select(c=>new Color32((byte)((uint)c>>24),(byte)((uint)c>>16),(byte)((uint)c>>8),(byte)c)).ToArray();texture.SetPixels32(colors);texture.Apply();}
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e){previous=Point(e);if(tool=="Pen")Draw(previous);else{var data=new JObject{["stamp"]=tool,["x"]=previous.x,["y"]=previous.y,["rgba"]=rgba};FriendsLifeRuntime.Instance.SubmitSourceAction(personId,"design-stamp:"+design.id,data.ToString(Newtonsoft.Json.Formatting.None));Refresh();}}
        public void OnDrag(UnityEngine.EventSystems.PointerEventData e){if(tool=="Pen")Draw(Point(e));}
        Vector2Int Point(UnityEngine.EventSystems.PointerEventData e){RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,e.position,e.pressEventCamera,out var p);var r=((RectTransform)transform).rect;return new Vector2Int(Mathf.Clamp((int)((p.x-r.x)/r.width*design.width),0,design.width-1),Mathf.Clamp((int)((p.y-r.y)/r.height*design.height),0,design.height-1));}
        void Draw(Vector2Int p){var data=new JObject{["x0"]=previous.x,["y0"]=previous.y,["x1"]=p.x,["y1"]=p.y,["rgba"]=rgba,["radius"]=radius};FriendsLifeRuntime.Instance.SubmitSourceAction(personId,"design-paint:"+design.id,data.ToString(Newtonsoft.Json.Formatting.None));Refresh();previous=p;}
        void OnDestroy(){if(texture)Destroy(texture);}
    }
}
