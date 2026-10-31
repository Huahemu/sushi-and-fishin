using System;
using System.Collections.Generic;
using System.Linq;

namespace TomodachiLife.Simulation
{
    public sealed partial class LifeSimulationRuntime
    {
        public LifeSimulationResolution ResolveFamilyChoice(string choiceId,string optionId,string childName=null)
        {
            var f=EnsureFamilyState();var q=f.choices.FirstOrDefault(v=>v.id==choiceId);if(q==null)return FamilyFail("这件事已经处理过了。");
            var option=q.options.FirstOrDefault(v=>v.id==optionId);if(option==null||!option.enabled)return FamilyFail(option?.reason??"请选择可用的选项。");
            if(FindResident(q.focusId)==null||FindResident(q.targetId)==null)return FamilyFail("参与的居民已经不在岛上。");
            if(q.action!="Birth"){string reason=FamilyActionReason(q.focusId,q.targetId,q.action);if(reason!=null)return FamilyFail(reason);}
            string a=q.focusId,b=q.targetId,pair=FamilyName(a)+"与"+FamilyName(b),message="";var couple=FamilyCouple(a,b,true);string daily="Relation";
            if(optionId=="wait"||optionId=="later")daily=null;
            switch(q.action)
            {
                case "Befriend":
                    if(optionId=="wait"){message=pair+"约好下次再聊。";break;}
                    FamilyBond(a,b,FamilyScaledGain(a,b,optionId=="listen"?20:f.configuration.chatGain));
                    if(FamilyCouple(a,b)==null&&!FamilyAreKin(a,b)&&FamilyFriendship(a,b)>=FamilyFriendMeter)
                    {string kind=Relationship(a,b).kind=="BestFriend"?"BestFriend":"Friend";FamilyBond(a,b,0,kind);}
                    FindResident(a).social=Need(FindResident(a).social+20);FindResident(b).social=Need(FindResident(b).social+20);
                    message=pair+"聊得很投缘，关系更亲近了。";break;
                case "BestFriends":
                    if(optionId=="wait"){message=pair+"决定再多了解彼此。";break;}
                    if(FamilyFriendship(a,b)>=FamilyBestFriendMeter){FamilyBond(a,b,10,"BestFriend");message=pair+"约定互相支持，成为了挚友！";}
                    else{FamilyBond(a,b,FamilyScaledGain(a,b,15));message="对方很高兴，但还想再多相处一些。";}break;
                case "Fight":
                    if(optionId=="calm"){FamilyBond(a,b,5);message=pair+"平静地说开了，没有吵起来。";break;}
                    FamilyBond(a,b,-25);FamilySetFight(a,b,true);message=pair+"吵架了，彼此都需要冷静一下。";daily=null;break;
                case "Reconcile":
                    if(optionId=="wait"){message=pair+"决定先各自冷静。";daily=null;break;}
                    FamilySetFight(a,b,false);FamilyBond(a,b,optionId=="apologize"?30:20);message=pair+"说开了心事，重新和好了。";daily="Fight";break;
                case "Confess":
                    if(optionId=="friends"){Relationship(a,b).oneSideLove=false;FamilyBond(a,b,5);message=pair+"决定先珍惜朋友关系。";break;}
                    if(FamilyFriendship(a,b)<f.configuration.confessionThreshold)
                    {Relationship(a,b).oneSideLove=true;FamilyProfile(a).sadUntilGameMinutes=State.gameMinutes+180;message=FamilyName(b)+"还想再多了解对方，暂时没有答应告白。";break;}
                    couple=new LifeFamilyCouple{id=Guid.NewGuid().ToString("N"),parentAId=a,parentBId=b,status="Dating",love=50,startedGameMinutes=State.gameMinutes};f.couples.Add(couple);
                    FamilyBond(a,b,10,"Lover");Relationship(a,b).oneSideLove=Relationship(b,a).oneSideLove=false;message=pair+"互相表达心意，开始交往了！";break;
                case "Date":
                    if(optionId=="wait"){message=pair+"把约会约在了下次。";break;}
                    couple.love=Math.Min(100,couple.love+(optionId=="picnic"?f.configuration.dateLoveGain:14));couple.dates++;couple.lastDateGameMinutes=State.gameMinutes;FamilyBond(a,b,12);
                    foreach(var id in new[]{a,b}){var r=FindResident(id);r.activity=optionId=="picnic"?"和伴侣去野餐":"和伴侣散步";r.activityUntilGameMinutes=State.gameMinutes+60;r.social=Need(r.social+25);}
                    message=pair+"度过了一次开心的约会，感情更好了。";break;
                case "Propose":
                    if(optionId=="wait"){message="两人决定继续培养感情。";break;}
                    if(couple.love<f.configuration.proposalLove||couple.dates<f.configuration.minimumDatesForProposal){message="对方还没有准备好结婚，多安排一些约会吧。";break;}
                    couple.status="Engaged";message=pair+"订婚了，接下来可以筹备婚礼！";break;
                case "Wedding":
                    if(optionId=="wait"){message="两人决定稍后再筹备婚礼。";break;}
                    if(optionId=="party"&&State.walletRaw<100)return FamilyFail("庆祝派对需要100，或选择温馨婚礼。");
                    if(optionId=="party")State.walletRaw-=100;couple.status="Married";couple.weddingStyle=optionId;couple.marriedGameMinutes=State.gameMinutes;couple.love=Math.Max(85,couple.love);couple.honeymoonSuggestion=true;
                    FamilyBond(a,b,15,"Spouse");FamilySuggestHousing(a,b,couple.id);message=pair+"举行了婚礼，成为了一家人！可以安排同住、蜜月和迎接宝宝。";break;
                case "Breakup":
                    if(optionId=="talk"){couple.love=Math.Min(100,couple.love+5);FamilyBond(a,b,5);message="两人认真谈过后，决定再给彼此一次机会。";break;}
                    string old=couple.status;couple.status="Broken";couple.breakups++;couple.love=Math.Max(0,couple.love-25);FamilyBond(a,b,-15,old=="Married"?"ExSpouse":"ExLover");FamilySetFight(a,b,false);
                    foreach(var id in new[]{a,b}){FamilyProfile(id).sadUntilGameMinutes=State.gameMinutes+360;FindResident(id).activity="感情有点失落";}
                    message=pair+"决定分开。家人关系和已有孩子会保留，也可以在以后重新交往。";break;
                case "Reunite":
                    if(optionId=="friends"){FamilyBond(a,b,10,"Friend");message="两人决定以朋友的身份相处。";break;}
                    if(FamilyFriendship(a,b)<f.configuration.confessionThreshold){message="两人还需要重新建立信任，先多聊聊天吧。";break;}
                    couple.status="Dating";couple.reunions++;couple.love=50;couple.dates=0;FamilyBond(a,b,10,"Lover");FamilyProfile(a).sadUntilGameMinutes=FamilyProfile(b).sadUntilGameMinutes=0;message=pair+"决定重新开始交往。";break;
                case "Baby":
                    if(optionId=="wait"){message="两人决定准备充分之后，再迎接宝宝。";break;}
                    if(optionId=="pregnancy"&&FamilyProfile(a).gender!="Woman"&&FamilyProfile(b).gender!="Woman")return FamilyFail("两人可以申请领养；怀孕需要一位成年女性居民。");
                    var pregnancy=new LifeFamilyPregnancy{id=Guid.NewGuid().ToString("N"),coupleId=couple.id,parentAId=a,parentBId=b,mode=optionId=="adopt"?"Adoption":"Pregnancy",startedGameMinutes=State.gameMinutes,
                        dueGameMinutes=State.gameMinutes+(optionId=="adopt"?f.configuration.adoptionGameMinutes:f.configuration.pregnancyGameMinutes),carrierId=optionId=="adopt"?"":FamilyProfile(a).gender=="Woman"?a:b};
                    f.pregnancies.Add(pregnancy);message=pair+(optionId=="adopt"?"提交了领养申请，开始准备迎接宝宝。":"确认了怀孕的消息，开始期待宝宝出生。");break;
                case "Birth":
                    var birth=f.pregnancies.FirstOrDefault(v=>v.id==q.relatedId);if(birth==null||birth.status!="Ready")return FamilyFail("宝宝还没有准备好到来。");
                    if(optionId=="later"){birth.status="Expecting";birth.dueGameMinutes=State.gameMinutes+60;message="两人再做一会儿准备，很快就能迎接宝宝。";break;}
                    var child=FamilyCreateChild(birth,childName);if(child==null)return FamilyFail("岛上居民已满，请先安排空间再迎接宝宝。");
                    message=FamilyName(a)+"与"+FamilyName(b)+"迎来了宝宝“"+child.displayName+"”！";break;
                case "Care":
                    var cared=f.children.First(v=>v.residentId==b);cared.careActions++;cared.lastCareGameMinutes=State.gameMinutes;
                    var baby=FindResident(b);baby.hunger=Need(baby.hunger+30);baby.social=Need(baby.social+30);baby.energy=Need(baby.energy+30);
                    if(State.gameMinutes>=cared.nextGrowthGameMinutes&&cared.careActions-cared.growthCareBaseline>=f.configuration.careActionsPerGrowth)cared.readyToGrow=true;
                    FamilyBond(a,b,8);message=FamilyName(a)+(optionId=="feed"?"给孩子喂饭并整理了衣服。":optionId=="play"?"陪孩子玩得很开心。":"给孩子讲了故事，孩子安心地睡了。");daily="Other";break;
                case "Grow":
                    if(optionId=="wait"){message="决定再享受一阵现在的亲子时光。";break;}
                    var grown=f.children.First(v=>v.residentId==b);string[] stages={"Baby","Toddler","Child","Teen","Adult"};int index=Math.Min(4,Array.IndexOf(stages,grown.stage)+1);int[] ages={0,2,8,14,18};
                    grown.stage=stages[index];grown.ageYears=ages[index];grown.growthCareBaseline=grown.careActions;grown.nextGrowthGameMinutes=State.gameMinutes+f.configuration.laterGrowthGameMinutes;grown.readyToGrow=false;grown.appearanceRevision++;
                    FamilyProfile(b).ageYears=grown.ageYears;message=FamilyName(b)+"成长到了"+FamilyStageLabel(grown.stage)+"阶段！";daily="Other";break;
                case "Comfort":FamilyProfile(b).sadUntilGameMinutes=0;FindResident(b).social=Need(FindResident(b).social+35);FamilyBond(a,b,15);message=FamilyName(a)+"耐心陪伴了"+FamilyName(b)+"，对方感觉好多了。";daily="Depress";break;
                case "Cohabit":
                    if(optionId=="wait"){message="两人决定暂时保持现在的生活。";break;}
                    FamilySuggestHousing(a,b,couple?.status=="Broken"?null:couple?.id);message="已经记下同住计划，请在下面选择一栋有足够位置的住宅。";break;
                default:return FamilyFail("这项家庭选择尚不可用。");
            }
            f.choices.Remove(q);if(daily!=null){RecordDailyAction(a,daily,q.action,b);if(daily!="Other")RecordDailyAction(b,daily,q.action,a);}
            return FamilyFinish(message,a,b,q.action);
        }
        public void AdvanceFamily(double elapsedGameMinutes)
        {
            if(double.IsNaN(elapsedGameMinutes)||double.IsInfinity(elapsedGameMinutes)||elapsedGameMinutes<0)throw new ArgumentOutOfRangeException(nameof(elapsedGameMinutes));
            var f=EnsureFamilyState();bool changed=false;
            foreach(var p in f.profiles)if(p.sadUntilGameMinutes>0&&p.sadUntilGameMinutes<=State.gameMinutes){p.sadUntilGameMinutes=0;changed=true;}
            // Births wait while the host keeps children off; the pregnancy record is kept.
            foreach(var pregnancy in f.pregnancies.Where(p=>p.status=="Expecting"&&p.dueGameMinutes<=State.gameMinutes&&FamilyAutoAllowed?.Invoke("Birth")==true).ToArray())
            {
                pregnancy.status="Ready";var q=new LifeFamilyChoice{id=Guid.NewGuid().ToString("N"),action="Birth",focusId=pregnancy.parentAId,targetId=pregnancy.parentBId,relatedId=pregnancy.id,title="宝宝准备好来到岛上了",createdGameMinutes=State.gameMinutes};
                FamilyOptions(q,"welcome","迎接宝宝","later","再准备一会儿");f.choices.Add(q);Journal("family","宝宝快要到来了，请家人做出选择。",FamilyProvenance,q.focusId,q.targetId);changed=true;
            }
            foreach(var child in f.children)if(child.stage!="Adult"&&!child.readyToGrow&&State.gameMinutes>=child.nextGrowthGameMinutes&&child.careActions-child.growthCareBaseline>=f.configuration.careActionsPerGrowth){child.readyToGrow=true;changed=true;}
            // Autonomous pair moments only between residents the host saw together
            // (FamilyPairContact: -1 busy or the player, 0 free, 1 met today).
            if(f.configuration.autonomousSuggestions&&FamilyPairContact!=null&&State.gameMinutes>=f.nextSuggestionGameMinutes&&State.residents.Count>=2&&f.choices.Count<3)
            {
                f.nextSuggestionGameMinutes=State.gameMinutes+Math.Max(30,Math.Min(f.configuration.suggestionIntervalGameMinutes,FamilySuggestionMinutes));
                var adults=State.residents.Where(r=>FamilyProfile(r.id).ageYears>=18).ToArray();
                var couple=FamilyAutoAllowed?.Invoke("Propose")==true?f.couples.FirstOrDefault(c=>(c.status=="Engaged"||c.status=="Dating"&&c.love>=f.configuration.proposalLove&&c.dates>=f.configuration.minimumDatesForProposal)&&FamilyPairContact(c.parentAId,c.parentBId)>0):null;
                if(couple!=null)StartFamilyAction(couple.parentAId,couple.parentBId,couple.status=="Engaged"?"Wedding":"Propose");
                else
                {
                    var makeUp=new List<string[]>();var moments=new List<string[]>();
                    for(int i=0;i<adults.Length;i++)for(int j=i+1;j<adults.Length;j++)
                    {
                        string a=adults[i].id,b=adults[j].id;int contact=FamilyPairContact(a,b);if(contact<0)continue;
                        bool fight=State.relationships.Any(r=>(r.fromId==a&&r.toId==b||r.fromId==b&&r.toId==a)&&r.fighting);
                        if(fight){if(State.gameMinutes-FamilyLastFightMinutes(a,b)>=FamilyReconcileMinutes)makeUp.Add(new[]{a,b,"Reconcile"});continue;}
                        if(contact>0){var moment=FamilyAutoMoment(a,b);if(moment!=null&&FamilyActionReason(moment[0],moment[1],moment[2])==null)moments.Add(moment);}
                    }
                    var list=makeUp.Count>0?makeUp:moments;
                    if(list.Count>0){var pick=list[PositiveMod(f.suggestionCursor++,list.Count)];StartFamilyAction(pick[0],pick[1],pick[2]);}
                }
            }
            if(changed){f.revision++;Changed();}
        }
        public Func<string,string,int> FamilyPairContact;
        public Func<string,bool> FamilyAutoAllowed;
        // Tomodachi order: comfort a sad friend, sometimes quarrel (more often when
        // compatibility is low), propose best friends at a high meter, otherwise chat.
        private string[] FamilyAutoMoment(string a,string b)
        {
            if(FamilyProfile(b).sadUntilGameMinutes>State.gameMinutes)return new[]{a,b,"Comfort"};
            if(FamilyProfile(a).sadUntilGameMinutes>State.gameMinutes)return new[]{b,a,"Comfort"};
            int level=FamilyRelationLevel(a,b);int fightChance=Math.Max(5,Math.Min(25,25-FamilyCompatibility(a,b)/5));
            if(level>=1&&FamilyRoll(a,b,"Fight")<fightChance)return new[]{a,b,"Fight"};
            if(level==2&&State.relationships.Any(r=>r.fromId==a&&r.toId==b&&r.kind=="Friend")&&FamilyFriendship(a,b)>=FamilyBestFriendMeter)return new[]{a,b,"BestFriends"};
            if(FamilyAutoAllowed?.Invoke("Date")==true&&FamilyCouple(a,b)!=null)return new[]{a,b,"Date"};
            if(FamilyAutoAllowed?.Invoke("Confess")==true&&FamilyFriendship(a,b)>=FamilyGoodFriendMeter&&CanFamilyRomance(a,b,out _))return new[]{a,b,"Confess"};
            return new[]{a,b,"Befriend"};
        }
        private LifeSimulationResident FamilyCreateChild(LifeFamilyPregnancy p,string suppliedName)
        {
            if(State.residents.Count>=Configuration.maximumResidents)return null;var f=EnsureFamilyState();string id=Guid.NewGuid().ToString("N");string name=string.IsNullOrWhiteSpace(suppliedName)?"宝宝"+(f.children.Count+1):suppliedName.Trim();if(name.Length>24)name=name.Substring(0,24);
            var resident=new LifeSimulationResident{id=id,displayName=name,avatarJson="",activity="和家人在一起",hunger=100,social=100,energy=100,activityUntilGameMinutes=State.gameMinutes+60};
            var profile=new LifeFamilyProfile{residentId=id,ageYears=0,gender=f.children.Count%2==0?"Woman":"Man",romancePreference="Anyone",parentAId=p.parentAId,parentBId=p.parentBId,adopted=p.mode=="Adoption"};
            var child=new LifeFamilyChild{residentId=id,parentAId=p.parentAId,parentBId=p.parentBId,pregnancyId=p.id,adopted=profile.adopted,bornGameMinutes=State.gameMinutes,nextGrowthGameMinutes=State.gameMinutes+f.configuration.babyGrowthGameMinutes,birthSeed=unchecked((uint)++f.birthSequence*2654435761u)};
            f.profiles.Add(profile);f.children.Add(child);State.residents.Add(resident);p.status="Born";p.childId=id;
            FamilyBond(p.parentAId,id,25,"Family");FamilyBond(p.parentBId,id,25,"Family");FamilySuggestHousing(p.parentAId,p.parentBId,p.coupleId);
            return resident;
        }
        public static string FamilyStageLabel(string stage)=>stage=="Baby"?"婴儿":stage=="Toddler"?"幼儿":stage=="Child"?"儿童":stage=="Teen"?"少年":"成年";
        private LifeFamilyHousingSuggestion FamilySuggestHousing(string a,string b,string coupleId)
        {
            var f=EnsureFamilyState();var ids=new[]{a,b}.Concat(f.children.Where(c=>c.parentAId==a&&c.parentBId==b&&c.stage!="Adult"||c.parentAId==b&&c.parentBId==a&&c.stage!="Adult").Select(c=>c.residentId)).Distinct().ToArray();
            var item=f.housingSuggestions.FirstOrDefault(s=>s.status=="Pending"&&s.residentIds.Contains(a)&&s.residentIds.Contains(b));
            if(item!=null){item.residentIds=ids;return item;}
            item=new LifeFamilyHousingSuggestion{id=Guid.NewGuid().ToString("N"),coupleId=coupleId,residentIds=ids};f.housingSuggestions.Add(item);return item;
        }
        public LifeSimulationResolution ChooseFamilyHouse(string suggestionId,string houseId)
        {
            var s=EnsureFamilyState().housingSuggestions.FirstOrDefault(v=>v.id==suggestionId);var house=State.households.FirstOrDefault(h=>h.id==houseId);
            if(s==null||house==null)return FamilyFail("请选择有效的同住计划和住宅。");
            if(s.status=="Applied")return FamilyFail("这项同住计划已经完成。");
            int needed=s.residentIds.Count(id=>FindResident(id)!=null);int others=house.residentIds.Count(id=>!s.residentIds.Contains(id));
            if(others+needed>house.capacity)return FamilyFail("这栋住宅没有足够的位置容纳全家。");
            s.houseId=houseId;s.status="Ready";s.message="正在安排入住";return FamilyFinish("已选择住宅，正在安排家人一起入住。");
        }
        public void CompleteFamilyHousingSuggestion(string suggestionId,bool success,string error=null)
        {
            var s=EnsureFamilyState().housingSuggestions.FirstOrDefault(v=>v.id==suggestionId);if(s==null||s.status!="Ready")return;
            s.status=success?"Applied":"Failed";s.message=success?"家人已经住到一起了。":error??"暂时无法安排入住，请重新选择住宅。";FamilyFinish(s.message);
        }
        public LifeSimulationResolution RecordFamilyHoneymoon(string coupleId,string travelSessionId)
        {
            var c=EnsureFamilyState().couples.FirstOrDefault(v=>v.id==coupleId&&v.status=="Married");
            if(c==null||string.IsNullOrEmpty(travelSessionId))return FamilyFail("请先安排一趟伴侣旅行。");
            c.honeymoonTripId=travelSessionId;c.honeymoonSuggestion=false;return FamilyFinish(FamilyName(c.parentAId)+"与"+FamilyName(c.parentBId)+"一起出发去蜜月旅行了！",c.parentAId,c.parentBId,"honeymoon");
        }
    }
}
