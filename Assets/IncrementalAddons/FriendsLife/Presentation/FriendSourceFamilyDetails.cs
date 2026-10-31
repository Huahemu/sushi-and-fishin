using System;using System.Linq;using Newtonsoft.Json.Linq;using TomodachiLife.Simulation;using UnityEngine;
namespace SushiAndFish.FriendsLife
{
 public sealed partial class FriendTabletPresenter
 {
  // Source: IslandLifeHudFamily.BuildFamilyChildren/BuildFamilyHistory.
  // The caller, selected source subject and choice owner remain distinct.
  bool RenderSourceFamilyDetails(string category,Transform content,ref float y)
  {
   if(category!="family-children"&&category!="family-history")return false;
   var runtime=FriendsLifeRuntime.Instance;var selected=runtime?.GetPerson(personId);if(selected==null)return true;var core=runtime.Core;var family=core.State.family;
   if(category=="family-history")
   {foreach(var memory in family.memories.Where(x=>x.focusId==selected.nativeResidentId||x.targetId==selected.nativeResidentId).Reverse().Take(20)){FriendUi.Text("FamilyMemory_"+memory.id,content,0,y,1120,110,memory.message,24);y+=130;}return true;}
   string Name(string native)=>core.FindResident(native)?.displayName??"居民";
   var pregnancies=family.pregnancies.Where(x=>x.status!="Born"&&(x.parentAId==selected.nativeResidentId||x.parentBId==selected.nativeResidentId)).ToArray();
   foreach(var pregnancy in pregnancies)
   {
    FriendUi.Text("Pregnancy_"+pregnancy.id,content,0,y,1120,140,(pregnancy.mode=="Adoption"?"等待领养宝宝":"正在期待宝宝出生")+"\n家长："+Name(pregnancy.parentAId)+"、"+Name(pregnancy.parentBId)+"\n"+(pregnancy.status=="Ready"?"已经准备好迎接宝宝！":"预计再过 "+Math.Max(0,(int)Math.Ceiling((pregnancy.dueGameMinutes-core.State.gameMinutes)/60))+" 个游戏小时"),24);y+=160;
    if(pregnancy.status=="Ready")Menu(content,ref y,"前往迎接宝宝的选择","family");
   }
   var children=family.children.Where(x=>x.parentAId==selected.nativeResidentId||x.parentBId==selected.nativeResidentId||x.residentId==selected.nativeResidentId).ToArray();
   if(children.Length==0&&pregnancies.Length==0){FriendUi.Text("NoChildren",content,0,y,1120,140,"还没有孩子。结婚后可以生育或领养。",24);y+=160;}
   foreach(var child in children)
   {
    FriendUi.Text("Child_"+child.residentId,content,0,y,1120,150,Name(child.residentId)+" · "+LifeSimulationRuntime.FamilyStageLabel(child.stage)+"\n家长："+Name(child.parentAId)+"、"+Name(child.parentBId)+"\n"+child.ageYears+"岁 · 本阶段陪伴 "+(child.careActions-child.growthCareBaseline)+" / "+family.configuration.careActionsPerGrowth+"次"+(child.adopted?"\n领养家庭":""),24);y+=170;
    if(child.stage=="Adult"){FriendUi.Text("AdultChild",content,0,y,1120,70,"已成年。",24);y+=90;continue;}
    var profile=family.profiles.FirstOrDefault(x=>x.residentId==selected.nativeResidentId);string nativeCaregiver=profile!=null&&profile.ageYears>=18?selected.nativeResidentId:child.parentAId;
    var caregiver=runtime.PersonFromNative(nativeCaregiver);var target=runtime.PersonFromNative(child.residentId);
    foreach(var option in new[]{new[]{"Care","照顾孩子"},new[]{"Grow","庆祝成长"}})
    {
     string action=option[0],caregiverId=caregiver?.personId,targetId=target?.personId;string reason=core.KernelFamilyActionReason(nativeCaregiver,child.residentId,action);
     var button=SourceRow(content,ref y,option[1],()=>SendTrackedCommand("action","family-start:"+action,new JObject{["targetPersonId"]=targetId}.ToString(Newtonsoft.Json.Formatting.None),inline:true,targetPersonId:caregiverId));
     button.interactable=caregiver!=null&&target!=null&&reason==null;
     if(reason!=null){FriendUi.Text("ChildReason_"+action,content,0,y,1120,70,reason,22);y+=90;}
    }
    FriendUi.Text("Growth_"+child.residentId,content,0,y,1120,90,child.readyToGrow?"已经准备好进入下一个阶段。":"约 "+Math.Max(0,(int)Math.Ceiling((child.nextGrowthGameMinutes-core.State.gameMinutes)/60))+" 小时后可以长大。",22);y+=110;
   }
   return true;
  }
 }
}
