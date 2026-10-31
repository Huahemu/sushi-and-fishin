using System;using System.Linq;using TMPro;using TomodachiLife;using TomodachiLife.Simulation;using UnityEngine;
namespace SushiAndFish.FriendsLife
{
 public sealed partial class FriendTabletPresenter
 {
  int activityPhotoPose,activityPhotoAngle,activityPhotoFraming;
  // These pages only expose the existing source HUD inputs. All rules,
  // ownership, prices and effects stay in the original source commands.
  bool RenderAdditionalSourceContent(string category,Transform paper,RectTransform content,ref float y)
  {
   var r=FriendsLifeRuntime.Instance;var p=r?.GetPerson(personId);if(p==null)return false;var core=r.Core;var personal=core.State.creative.residents.FirstOrDefault(x=>x.residentId==p.nativeResidentId);
   if(RenderSourceFamilyDetails(category,content,ref y))return true;
   if(category=="photography")
   {
    var labels=new[]{"待机","帅气","坐姿","生气","低落"};
    for(int index=0;index<5;index++){int pose=index;FriendUi.Button("PhotoPose_"+index,content,index*224,y,208,56,(activityPhotoPose==index?"● ":"")+labels[index],()=>{activityPhotoPose=pose;Render();});}y+=80;
    var framingLabels=new[]{"全身","近景","远景"};
    for(int index=0;index<3;index++){int framing=index;FriendUi.Button("PhotoFraming_"+index,content,index*374,y,352,56,(activityPhotoFraming==index?"● ":"")+framingLabels[index],()=>{activityPhotoFraming=framing;Render();});}y+=80;
    FriendUi.Button("PhotoAngleLeft",content,0,y,352,56,"左侧构图",()=>{activityPhotoAngle=(activityPhotoAngle+7)%8;Render();});FriendUi.Text("PhotoAngle",content,374,y,352,56,"角度 "+activityPhotoAngle,24);FriendUi.Button("PhotoAngleRight",content,748,y,352,56,"右侧构图",()=>{activityPhotoAngle=(activityPhotoAngle+1)%8;Render();});y+=80;
    foreach(var definition in CatalogPage(category,paper,LifeActivitiesCatalog.Photos,x=>x.title+x.id))
    {
     string id=definition.id;bool unlocked=core.IsPhotoUnlocked(id);
     var b=SourceRow(content,ref y,definition.title+(unlocked?" · 拍摄":" · 尚未解锁"),()=>Command("photo-capture",new Newtonsoft.Json.Linq.JObject{["presetId"]=id,["pose"]=activityPhotoPose,["angle"]=activityPhotoAngle,["framing"]=activityPhotoFraming}.ToString(Newtonsoft.Json.Formatting.None)));
     b.interactable=unlocked&&!core.IsResidentAway(p.nativeResidentId);
    }
    Menu(content,ref y,"相册","memories");return true;
   }
   if(category=="memories")
   {
    Menu(content,ref y,"拍摄照片","photography");
    foreach(var photo in core.State.activities.photos.Where(x=>x.residentId==p.nativeResidentId).Reverse().Take(3))
    {
     FriendUi.Flow(content,ref y,"Photo_"+photo.id,photo.label,24);
     var rect=FriendUi.Rect("SavedPhoto_"+photo.id,content,0,y,800,500);var graphic=rect.gameObject.AddComponent<UnityEngine.UI.RawImage>();FriendUi.UseTabletGraphic(graphic);graphic.raycastTarget=false;graphic.color=Color.clear;
     rect.gameObject.AddComponent<FriendSavedPhotoImage>().Bind(photo.path);
     // Keep the photo's own aspect ratio inside the 800x500 box.
     float boxW=800,boxH=500;var shot=graphic.texture;if(shot&&shot.width>0&&shot.height>0){float aspect=(float)shot.width/shot.height;if(aspect>boxW/boxH)boxH=boxW/aspect;else boxW=boxH*aspect;}
     rect.sizeDelta=new Vector2(boxW,boxH);y+=boxH+20;
    }
    return true;
   }
   if(category=="goods-use")
   {
    foreach(var item in CatalogPage(category,paper,core.CreativeCatalog.Data.goods,x=>x.name+x.id))
    {string id=item.id;long price=item.price;SourceRow(content,ref y,item.name+"   "+(price/100m).ToString("0.00")+" 金币",()=>ConfirmCommand(item.name,price,"goods:"+id));}return true;
   }
   if(category=="room")
   {
    Menu(content,ref y,"房间摆设","decorations");
    foreach(var item in CatalogPage(category,paper,core.CreativeCatalog.Data.rooms,x=>x.name+x.id))
    {string id=item.id;bool owned=personal?.ownedRoomStyles.Contains(id)==true;long price=owned?0:item.price;SourceRow(content,ref y,item.name+"   "+(owned?"已拥有":(price/100m).ToString("0.00")+" 金币"),()=>{if(owned)Command("room:"+id);else ConfirmCommand(item.name,price,"room:"+id);});}return true;
   }
   if(category=="wardrobe")
   {
    if(personal!=null)foreach(var garment in personal.garments.ToArray())
    {string slot=garment.slot;var definition=core.CreativeCatalog.Data.clothes.FirstOrDefault(x=>x.id==garment.clothId);string label=definition?.name??garment.clothId??slot;SourceRow(content,ref y,label+" · "+(garment.color+1)+"  取下",()=>Command("cloth-remove:"+slot));}
    Menu(content,ref y,"选择服装","cloth");return true;
   }
   if(category=="decorations")
   {
    if(personal!=null)foreach(var item in personal.decorations.ToArray())
    {
     string id=item.id;var design=core.CreativeDesign(item.designId);FriendUi.Flow(content,ref y,"Decor_"+id,(design?.title??item.goodsId)+" · 格 "+item.x+","+item.z,24);
     var labels=new[]{"左","右","近","远","转","收起"};
     for(int index=0;index<labels.Length;index++)
     {int option=index;FriendUi.Button("DecorMove_"+id+"_"+index,content,index*186,y,170,56,labels[index],()=>
      {if(option==5)Command("design-remove:"+id);else Command("design-move:"+id,new Newtonsoft.Json.Linq.JObject{["dx"]=option==0?-1:option==1?1:0,["dz"]=option==2?-1:option==3?1:0,["rotate"]=option==4}.ToString(Newtonsoft.Json.Formatting.None));});}
     y+=80;
    }
    Menu(content,ref y,"去绘制壁纸、摆设和建筑","workshop");return true;
   }
   if(category=="traits")
   {
    if(personal==null){FriendUi.Flow(content,ref y,"EmptyTraits","暂无资料",24);return true;}
    foreach(var row in new[]{new[]{"Energy","活力",personal.energy.ToString()},new[]{"Social","社交",personal.sociability.ToString()},new[]{"Patience","耐心",personal.patience.ToString()},new[]{"Expression","表现",personal.expression.ToString()},new[]{"Curiosity","好奇",personal.curiosity.ToString()}})
    {
     string axis=row[0];int value=int.Parse(row[2]);FriendUi.Text("Trait_"+axis,content,0,y,640,56,row[1]+"   "+value,24);
     var lower=FriendUi.Button("TraitDown_"+axis,content,690,y,180,56,"−",()=>Command("personality:"+axis,(value-1).ToString()));lower.interactable=value>1;
     var upper=FriendUi.Button("TraitUp_"+axis,content,900,y,180,56,"＋",()=>Command("personality:"+axis,(value+1).ToString()));upper.interactable=value<5;y+=80;
    }return true;
   }
   if(category=="culture")
   {foreach(var target in r.People.Where(x=>x.personId!=personId)){string targetId=target.personId;SourceRow(content,ref y,target.displayName,()=>Command("share-culture",new Newtonsoft.Json.Linq.JObject{["targetPersonId"]=targetId}.ToString(Newtonsoft.Json.Formatting.None)));}return true;}
   if(category=="facilities")
   {
    foreach(var item in LifeActivitiesCatalog.Facilities)
    {
     string id=item.id;var facility=core.State.activities.facilities.FirstOrDefault(x=>x.id==id);bool unlocked=facility?.unlocked==true;
     FriendUi.Flow(content,ref y,"Facility_"+id,item.title+"\n"+item.description+(unlocked?"\n去过 "+facility.visits+" 次":"\n费用 "+(item.cost/100m).ToString("0.00")+" 金币 · 需要居民 "+item.requiredResidents+" 位 · 捐款 "+(item.requiredDonations/100m).ToString("0.00")+" 金币"),24);
     if(unlocked)SourceRow(content,ref y,"一起去",()=>Command("facility:"+id));else SourceRow(content,ref y,"建设",()=>ConfirmCommand(item.title,item.cost,"unlock-facility:"+id));
    }
    Menu(content,ref y,"愿望喷泉","fountain");return true;
   }
   if(category=="news")
   {
    foreach(var news in core.State.activities.news.OrderByDescending(x=>x.gameMinutes).ToArray())
    {string id=news.id;SourceRow(content,ref y,(news.watched?"已读 · ":"新 · ")+news.title,()=>Command("news-read:"+id));FriendUi.Flow(content,ref y,"NewsBody_"+id,news.body,24,1120,24);}return true;
   }
   if(category=="accidents")
   {
    foreach(var accident in core.State.activities.accidents.Where(x=>!x.resolved&&x.residentId==p.nativeResidentId).ToArray())
    {string id=accident.id;FriendUi.Flow(content,ref y,"Accident_"+id,accident.message,24);SourceRow(content,ref y,"一起想办法",()=>Command("accident:"+id,"0"));SourceRow(content,ref y,"给点帮助（0.50 金币）",()=>ConfirmCommand("给点帮助",50,"accident:"+id,"1"));SourceRow(content,ref y,"让他自己处理",()=>Command("accident:"+id,"2"));}
    return true;
   }
   if(category=="fountain")
   {
    FriendUi.Flow(content,ref y,"FountainPoints","愿望点："+core.State.activities.fountainPoints,24);
    var contribute=SourceRow(content,ref y,"投入这位朋友的心情球",()=>Command("fountain-balls"));contribute.interactable=core.State.emotionBalls.Any(x=>x.residentId==p.nativeResidentId);
    foreach(var row in new[]{new[]{"Travel","许愿旅行券"},new[]{"Photo","许愿新摄影场景"},new[]{"Goods","许愿宝物"}}){string wish=row[0];var b=SourceRow(content,ref y,row[1]+" · 500点",()=>Command("fountain",new Newtonsoft.Json.Linq.JObject{["wish"]=wish}.ToString(Newtonsoft.Json.Formatting.None)));b.interactable=core.State.activities.fountainPoints>=500;}return true;
   }
   return false;
  }
 }
}
