using System;
using System.Collections.Generic;
using System.Linq;

namespace TomodachiLife.Simulation
{
    public sealed partial class LifeSimulationRuntime
    {
        public bool SpendGameplayMoney(int amount,string reason)
        {
            if(amount<0||State.walletRaw<amount)return false;var result=new LifeSimulationResolution();ChangeWallet(-amount,reason,result);Changed();return true;
        }
        private LifeDailySource dailySource;
        public LifeDailySource DailySource=>dailySource??(dailySource=LifeDailySource.Load());
        public int DailyRevision=>DailyState.revision;
        public LifeDailyState DailyState
        {
            get
            {
                if(State.daily==null)State.daily=new LifeDailyState();
                State.daily.requests=State.daily.requests??new List<LifeDailyRequest>();
                State.daily.cooldowns=State.daily.cooldowns??new List<LifeDailyCooldown>();
                State.daily.moods=State.daily.moods??new List<LifeDailyMood>();
                State.daily.actions=State.daily.actions??new List<LifeDailyAction>();
                State.daily.playerResidents=State.daily.playerResidents??new List<string>();
                State.daily.tastes=State.daily.tastes??new List<LifeDailyTaste>();
                return State.daily;
            }
        }
        private void AdvanceDaily(double minutes)
        {
            var daily=DailyState;
            foreach(var mood in daily.moods.Where(m=>m.untilMinutes<=State.gameMinutes).ToArray())
            {
                var resident=FindResident(mood.residentId);
                if(resident!=null){resident.feelingHash=957022509;Journal("mood",resident.displayName+" 又打起精神了。",Reconstruction,resident.id);}
                daily.moods.Remove(mood);daily.revision++;
            }
            foreach(var request in daily.requests.Where(r=>r.status=="Pending"&&r.deadlineMinutes<=State.gameMinutes).ToArray())
                EndDailyRequest(request,false,"这次没赶上，改天再帮忙吧。");
            daily.cooldowns.RemoveAll(c=>c.untilMinutes<State.gameMinutes-1440);
            if(State.gameMinutes>=daily.nextRequestMinutes)
            {
                // A problem every two game hours; the player walks over to help.
                daily.nextRequestMinutes=State.gameMinutes+120;
                var resident=State.residents.Where(r=>r.activity!="睡觉"&&!daily.playerResidents.Contains(r.id)&&!daily.requests.Any(q=>q.status=="Pending"&&q.residentId==r.id)).OrderBy(r=>r.hunger+r.social+r.energy).FirstOrDefault();
                if(resident!=null)RequestDailyHelp(resident.id);
            }
            foreach(var resident in State.residents)
            {
                var mood=daily.moods.FirstOrDefault(m=>m.residentId==resident.id);
                if(mood!=null)resident.activity=mood.kind=="Depress"?"独自整理心情":resident.activity;
            }
        }
        public LifeDailyRequest RequestDailyHelp(string residentId,string sourceId=null)
        {
            var resident=FindResident(residentId);var daily=DailyState;
            if(resident==null){LastError="先选一位居民。";return null;}
            var pending=daily.requests.FirstOrDefault(r=>r.residentId==residentId&&r.status=="Pending");
            if(pending!=null)return pending;
            var options=DailySource.troubles.Where(t=>t.id!="Dummy"&&t.rate>0&&!daily.cooldowns.Any(c=>c.residentId==residentId&&c.sourceId==t.id&&c.untilMinutes>State.gameMinutes)).ToArray();
            LifeDailyTroubleRule rule=sourceId==null?null:DailySource.troubles.FirstOrDefault(t=>t.id==sourceId);
            if(sourceId!=null&&rule==null){LastError="找不到这件求助。";return null;}
            if(rule==null)
            {
                string desired=resident.hunger<50?"Food":State.relationships.Any(r=>r.fromId==residentId&&r.fighting)?"Fight":daily.moods.Any(m=>m.residentId==residentId)||resident.social<40?"Depress":null;
                // Quarrel and feeling down have source rate 0. They come from the
                // resident's own state, as in Tomodachi.
                if(desired=="Fight"||desired=="Depress")
                    rule=DailySource.troubles.FirstOrDefault(t=>t.id==(desired=="Fight"?"FightBase":"DepressBase")&&!daily.cooldowns.Any(c=>c.residentId==residentId&&c.sourceId==t.id&&c.untilMinutes>State.gameMinutes));
                var allowed=options.Where(t=>PlayerProblem(t)&&(State.residents.Count-daily.playerResidents.Count>1||t.category!="Relation")).ToArray();
                var candidates=allowed.Where(t=>desired==null||t.category==desired).ToArray();
                if(candidates.Length==0)candidates=allowed;
                if(rule==null&&candidates.Length==0){LastError="居民现在没有烦恼。";return null;}
                if(rule==null)rule=candidates[(int)(NativeMiiEventRandom.NextUInt(ref State.eventRandom)%(uint)candidates.Length)];
            }
            var others=State.residents.Where(r=>r.id!=residentId&&!daily.playerResidents.Contains(r.id)).OrderBy(r=>r.displayName).ToArray();
            var target=others.Skip(daily.requestCursor++%Math.Max(1,others.Length)).FirstOrDefault();
            // At least four game hours: time to walk over and help in person.
            var request=new LifeDailyRequest{id=Guid.NewGuid().ToString("N"),sourceId=rule.id,residentId=residentId,targetId=target?.id??"",category=rule.category,
                title=DailyRequestTitle(rule.category,rule.id),description=DailyRequestDescription(resident,rule,target),createdMinutes=State.gameMinutes,
                deadlineMinutes=State.gameMinutes+Math.Max(240,rule.duration)};
            daily.requests.Add(request);resident.activeTroubleHash=StableGameplayHash(rule.id);resident.troubleResolutionReconstructed=true;
            daily.revision++;Journal("request",resident.displayName+"："+request.description,Reconstruction,residentId,target?.id??"");Changed();return request;
        }
        // Problems the player can see and solve in person: hungry, wants a
        // gift, wants to make a friend, has a question (plus quarrel/down above).
        static readonly string[] PlayerFoodProblems={"Hungry","HungryAll1","HungryAll2","HungryFish","HungryRice","HungrySweet","HungryDrink","HungryHot","HungryCool"};
        static bool PlayerProblem(LifeDailyTroubleRule t)=>t.category=="Food"&&Array.IndexOf(PlayerFoodProblems,t.id)>=0||t.category=="Goods"||t.category=="Question"||t.category=="Relation"&&t.id=="FriendOther";
        // Debug and tests: replace the resident's open problem with this one.
        public LifeDailyRequest ForceDailyRequest(string residentId,string sourceId)
        {
            foreach(var pending in DailyState.requests.Where(r=>r.residentId==residentId&&r.status=="Pending").ToArray())EndDailyRequest(pending,false,"先不想这件事了。");
            DailyState.cooldowns.RemoveAll(c=>c.residentId==residentId&&c.sourceId==sourceId);
            return RequestDailyHelp(residentId,sourceId);
        }
        public static uint StableGameplayHash(string text)
        {unchecked{uint hash=2166136261;foreach(char c in text??""){hash^=c;hash*=16777619;}return hash==0?1:hash;}}
        public static string DailyCategoryName(string category)
        {
            switch(category){case"Food":return"想吃点东西";case"Cloth":return"想换身衣服";case"Goods":return"想要一件礼物";case"Relation":return"关系烦恼";
                case"Fight":return"争吵与和好";case"Depress":return"有些失落";case"IslandEdit":return"岛屿建设建议";case"RoomStyle":return"房间装修";
                case"Question":return"想问问你";case"Introduction":return"初来乍到";default:return"生活小愿望";}
        }
        private static string DailyRequestTitle(string category,string sourceId)=>DailyCategoryName(category);
        private static string DailyRequestDescription(LifeSimulationResident resident,LifeDailyTroubleRule rule,LifeSimulationResident target)
        {
            string other=target?.displayName??"新朋友";
            switch(rule.category)
            {
                case"Food":return FoodRequestDescription(rule.id);
                case"Cloth":return"想试试不一样的穿搭。帮我挑一件吧。";
                case"Goods":return"今天想找个新玩意儿玩一会儿。";
                case"RoomStyle":return"房间看腻了，想重新布置一下。";
                case"IslandEdit":return rule.id.Contains("Plant")?"这里种棵树应该不错，你觉得呢？":rule.id.Contains("Road")?"能不能给大家修一段好走的路？":"岛上还有点空，添个大家能用的东西吧。";
                case"Depress":return"今天有点提不起劲。能陪我聊聊吗？";
                case"Fight":return"和朋友闹别扭了……想和好，又不知道怎么开口。";
                case"Relation":return"我想和 "+other+" 好好谈谈，你能帮我出出主意吗？";
                case"Question":return"你觉得我应该主动认识更多朋友吗？";
                case"Introduction":return"刚来岛上，好多地方都没去过。陪我熟悉一下吧。";
                default:return rule.id=="AskUgcWord"?"教我一句有趣的话吧，我想说给大家听。":"想换个心情，做点平时没做过的事。";
            }
        }
        private static string FoodRequestDescription(string source)
        {
            string kind=source.StartsWith("Hungry")?source.Substring(6):"";string label=kind=="Bread"?"面包":kind=="Confectionery"?"点心":kind=="Drink"?"饮料":kind=="Fish"?"鱼":kind=="Fruit"?"水果":kind=="Meat"?"肉":kind=="Noodle"?"面条":kind=="Rice"?"米饭":kind=="Vegetable"?"蔬菜":kind=="Sweet"?"甜食":kind=="Salty"?"咸口食物":kind=="Sour"?"酸的食物":kind=="Spicy"?"辣的食物":kind=="Oily"?"油香的食物":kind=="Hot"?"热乎的食物":kind=="Cool"?"清凉的食物":kind=="Ugc"?"你亲手做的食物":null;
            return label==null?"肚子在叫了，能给我找点吃的吗？":"今天特别想吃点"+label+"，能帮我找找吗？";
        }
        private bool DailyActionMatches(LifeDailyRequest request,string detail,string targetId,string actionCategory)
        {
            if(request.category=="Introduction")
            {
                if(request.sourceId.Contains("Hungry"))return actionCategory=="Food";
                if(request.sourceId.Contains("Dress"))return actionCategory=="Cloth";
                if(request.sourceId.Contains("Interior"))return actionCategory=="RoomStyle"||detail=="EnterHouse";
                if(request.sourceId.Contains("Friend"))return actionCategory=="Relation";
                if(request.sourceId.Contains("IslandEdit"))return actionCategory=="IslandEdit";
            }
            if(request.sourceId=="AskUgcWord")return actionCategory=="Phrase"||actionCategory=="Topic";
            if(request.sourceId=="ChangeHair")return detail=="ChangeHair";
            if(request.category=="Relation"&&!string.IsNullOrEmpty(request.targetId)&&request.targetId!=targetId)return false;
            if(request.category=="IslandEdit")
            {
                if(request.sourceId.Contains("Plant"))return detail.Contains("Tree")||detail.Contains("Flower")||detail.Contains("Hedge")||detail.Contains("Weed");
                if(request.sourceId.Contains("Road"))return detail.EndsWith("_Road",StringComparison.Ordinal);
                if(request.sourceId.Contains("Streetscape"))return detail.StartsWith("Obj",StringComparison.Ordinal)&&!detail.Contains("Tree")&&!detail.Contains("Flower");
            }
            if(request.category=="Food"&&request.sourceId.StartsWith("Hungry",StringComparison.Ordinal))
            {
                string kind=request.sourceId.Substring(6);if(string.IsNullOrEmpty(kind)||kind.StartsWith("All"))return true;
                if(kind=="Ugc")return detail.StartsWith("ugc:",StringComparison.OrdinalIgnoreCase)||detail.Contains("自绘");
                return DailySource.foods.FirstOrDefault(f=>f.name==detail)?.tags?.Contains(kind)==true;
            }
            return true;
        }
        public void RecordDailyAction(string residentId,string category,string detail="",string targetId="")
        {
            var daily=DailyState;daily.actions.Add(new LifeDailyAction{residentId=residentId,category=category,detail=detail,minutes=State.gameMinutes});
            while(daily.actions.Count>100)daily.actions.RemoveAt(0);
            foreach(var request in daily.requests.Where(r=>r.status=="Pending"&&(r.residentId==residentId||category=="IslandEdit")&&(r.category==category||r.category=="Introduction"||r.sourceId=="AskUgcWord"&&(category=="Phrase"||category=="Topic"))&&DailyActionMatches(r,detail??"",targetId,category)).ToArray())
                EndDailyRequest(request,true,"心愿完成了，谢谢你！");
            daily.revision++;Changed();
        }
        public LifeSimulationResolution AnswerDailyRequest(string requestId,int answer)=>AnswerDailyRequest(requestId,answer,null);
        // helperId: the player resident who answered in person. A solved
        // problem also brings the two closer (Tomodachi: helping raises it).
        public LifeSimulationResolution AnswerDailyRequest(string requestId,int answer,string helperId)
        {
            var request=DailyState.requests.FirstOrDefault(r=>r.id==requestId&&r.status=="Pending");
            if(request==null)return Fail("这件事已经处理过了。");
            var result=AnswerPendingRequest(request,answer);
            // Use the installed M3 rules inside this same kernel transaction.
            if(result.success&&request.status=="Completed"&&!string.IsNullOrEmpty(helperId)&&helperId!=request.residentId&&FindResident(helperId)!=null)
                AdjustFamilyBond(helperId,request.residentId,4,"help",true);
            return result;
        }
        private LifeSimulationResolution AnswerPendingRequest(LifeDailyRequest request,int answer)
        {
            if(answer==2){request.deadlineMinutes+=120;DailyState.revision++;Changed();return new LifeSimulationResolution{success=true,message="好，那过一会儿再说。"};}
            if(answer==0){EndDailyRequest(request,false,"好吧，那下次再说。");return new LifeSimulationResolution{success=true,message=request.result};}
            if(request.category=="Question")
            {
                var resident=FindResident(request.residentId);resident.social=Need(resident.social+(answer==3?10:25));var personal=CreativeResident(resident.id);personal.sociability=Math.Max(0,Math.Min(5,personal.sociability+(answer==3?-1:1)));EndDailyRequest(request,true,answer==3?"嗯，我决定慢慢交朋友。":"好，我试着主动认识新朋友！");
                return new LifeSimulationResolution{success=true,message=request.result};
            }
            // Advice is not a meal. Hunger stays pending until a fitting food
            // gift or the existing restaurant-consumption path completes it.
            if(request.category=="Food"&&answer==1)return new LifeSimulationResolution{success=false,message="可以送我一点吃的吗？"};
            if(request.category=="Relation"&&answer==1)
            {
                var target=FindResident(request.targetId);
                if(target!=null&&target.id!=request.residentId)AdjustFamilyBond(request.residentId,target.id,8,"help",false);
                EndDailyRequest(request,true,target==null?"好，我去交个新朋友！":"好，我去找"+target.displayName+"聊聊！");
                return new LifeSimulationResolution{success=true,message=request.result};
            }
            if(request.category=="Depress")return ComfortResident(request.residentId);
            if(request.category=="Fight")
            {
                var fights=State.relationships.Where(r=>(r.fromId==request.residentId||r.toId==request.residentId)&&r.fighting).ToArray();
                if(fights.Length==0){EndDailyRequest(request,true,"原来是个误会，两人已经说开了。");return new LifeSimulationResolution{success=true,message=request.result};}
                foreach(var relation in fights){relation.fighting=false;relation.meter=Math.Min(199,relation.meter+20);}
                RecordDailyAction(request.residentId,"Fight","和好");return new LifeSimulationResolution{success=true,message="坐下来谈了谈，争吵结束了。"};
            }
            return new LifeSimulationResolution{success=false,message="这件事要换个办法帮忙。"};
        }
        // E -> 交谈. Once per game hour a chat raises the social need and
        // brings the friend and the player a little closer.
        public bool RecordPlayerChat(string residentId,string playerId,out bool closer)
        {
            closer=false;var resident=FindResident(residentId);if(resident==null||residentId==playerId||FindResident(playerId)==null)return false;var daily=DailyState;
            string cooldown="PlayerChat/"+playerId;
            closer=!daily.cooldowns.Any(c=>c.residentId==residentId&&c.sourceId==cooldown&&c.untilMinutes>State.gameMinutes);
            daily.actions.Add(new LifeDailyAction{residentId=residentId,category="PlayerChat",detail=playerId??"",minutes=State.gameMinutes});
            while(daily.actions.Count>100)daily.actions.RemoveAt(0);
            if(closer)
            {
                resident.social=Need(resident.social+5);
                daily.cooldowns.RemoveAll(c=>c.residentId==residentId&&c.sourceId==cooldown);
                daily.cooldowns.Add(new LifeDailyCooldown{residentId=residentId,sourceId=cooldown,untilMinutes=State.gameMinutes+60});
                AdjustFamilyBond(playerId,residentId,2,"chat",true);
            }
            daily.revision++;Changed();return true;
        }
        public LifeSimulationResolution ComfortResident(string residentId)
        {
            var resident=FindResident(residentId);if(resident==null)return Fail("居民不存在。");
            var result=StartResult(resident,"comfort");resident.social=Need(resident.social+30);resident.energy=Need(resident.energy+15);resident.feelingHash=957022509;
            DailyState.moods.RemoveAll(m=>m.residentId==residentId);resident.activeTroubleHash=0;
            RecordDailyAction(residentId,"Depress","陪伴安慰");FinishResult(result,resident,"陪 "+resident.displayName+" 坐了一会儿，心情好多了。");return result;
        }
        public void SetDailyMood(string residentId,string kind,double duration=180)
        {
            var resident=FindResident(residentId);if(resident==null)return;
            DailyState.moods.RemoveAll(m=>m.residentId==residentId);DailyState.moods.Add(new LifeDailyMood{residentId=residentId,kind=kind,untilMinutes=State.gameMinutes+Math.Max(1,duration)});
            resident.feelingHash=kind=="Happy"?1645779707u:StableGameplayHash(kind);DailyState.revision++;Changed();
        }
        private void EndDailyRequest(LifeDailyRequest request,bool success,string message)
        {
            if(request.status!="Pending")return;var resident=FindResident(request.residentId);var daily=DailyState;
            request.status=success?"Completed":"Closed";request.result=message;var rule=DailySource.troubles.FirstOrDefault(r=>r.id==request.sourceId);
            daily.cooldowns.Add(new LifeDailyCooldown{residentId=request.residentId,sourceId=request.sourceId,untilMinutes=State.gameMinutes+(rule?.cooldown??60)});
            if(resident!=null)
            {
                resident.activeTroubleHash=0;
                if(success){daily.completedRequests++;var result=StartResult(resident,"request");AddHappiness(resident,15,result,true);resident.social=Need(resident.social+10);}
                else{daily.missedRequests++;resident.social=Need(resident.social-3);}
            }
            Journal("request-result",(resident?.displayName??"居民")+"："+message,Reconstruction,request.residentId,request.targetId);daily.revision++;
            while(daily.requests.Count>100){var old=daily.requests.FirstOrDefault(r=>r.status!="Pending");if(old==null)break;daily.requests.Remove(old);}Changed();
        }
        public string ResumeOfflineLife(DateTime utcNow)
        {
            if(!DailyState.offlineLife||!DateTime.TryParse(State.savedUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out var saved))return"";
            double minutes=Math.Max(0,Math.Min(10080,(utcNow-saved.ToUniversalTime()).TotalMinutes));
            if(minutes<1)return"";double rate=Configuration.gameMinutesPerRealSecond;if(rate<=0)return"";
            for(double left=minutes;left>0;left-=15)Advance(Math.Min(15,left)/rate);
            DailyState.offlineMinutes+=minutes;State.savedUtc=utcNow.ToString("O");DailyState.revision++;
            string message="离开期间，岛上又生活了 "+(int)minutes+" 分钟。";Journal("offline",message,Reconstruction);Changed();return message;
        }
    }
}
