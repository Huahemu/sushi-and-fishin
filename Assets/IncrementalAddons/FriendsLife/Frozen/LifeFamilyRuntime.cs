using System;
using System.Collections.Generic;
using System.Linq;

namespace TomodachiLife.Simulation
{
    public sealed partial class LifeSimulationRuntime
    {
        public const string FamilyProvenance="Reconstructed player-driven family lifecycle; existing directional relationship state, native source meter limit, and game clock";
        public LifeFamilyState FamilyState=>EnsureFamilyState();
        public int FamilyRevision=>EnsureFamilyState().revision;
        public LifeFamilyState EnsureFamilyState()
        {
            if(State.family==null)State.family=new LifeFamilyState();var f=State.family;
            if(f.configuration==null)f.configuration=new LifeFamilyConfiguration();
            if(f.profiles==null)f.profiles=new List<LifeFamilyProfile>();if(f.couples==null)f.couples=new List<LifeFamilyCouple>();
            if(f.pregnancies==null)f.pregnancies=new List<LifeFamilyPregnancy>();if(f.children==null)f.children=new List<LifeFamilyChild>();
            if(f.choices==null)f.choices=new List<LifeFamilyChoice>();if(f.housingSuggestions==null)f.housingSuggestions=new List<LifeFamilyHousingSuggestion>();
            if(f.memories==null)f.memories=new List<LifeFamilyMemory>();
            foreach(var r in State.residents)if(!f.profiles.Any(p=>p.residentId==r.id)){f.profiles.Add(new LifeFamilyProfile {residentId=r.id});f.revision++;}
            foreach(var q in f.choices)if(q.options==null)q.options=new List<LifeFamilyOption>();
            return f;
        }
        public LifeFamilyProfile FamilyProfile(string id)=>EnsureFamilyState().profiles.FirstOrDefault(p=>p.residentId==id);
        public LifeFamilyCouple FamilyCouple(string a,string b=null,bool includeBroken=false)
            =>EnsureFamilyState().couples.LastOrDefault(c=>(includeBroken||c.status!="Broken")&&(c.parentAId==a||c.parentBId==a)&&(b==null||c.parentAId==b||c.parentBId==b));
        public LifeSimulationResolution ConfigureFamilyProfile(string id,int ageYears,string gender,string preference)
        {
            var p=FamilyProfile(id);if(p==null)return FamilyFail("没有找到这位居民。");
            if(ageYears<0||ageYears>120)return FamilyFail("年龄需要在0到120岁之间。");
            if(EnsureFamilyState().children.Any(c=>c.residentId==id&&c.stage!="Adult"))return FamilyFail("孩子的年龄会随着成长阶段变化。");
            if(ageYears<18&&(FamilyCouple(id)!=null||EnsureFamilyState().pregnancies.Any(v=>v.status!="Born"&&(v.parentAId==id||v.parentBId==id))))return FamilyFail("这位居民正在成年伴侣或孕育阶段，不能调为未成年。");
            if(!new[]{"Man","Woman","Other"}.Contains(gender)||!new[]{"Anyone","Men","Women","Nobody"}.Contains(preference))return FamilyFail("请选择有效的性别和恋爱偏好。");
            p.ageYears=ageYears;p.gender=gender;p.romancePreference=preference;return FamilyFinish("已保存居民的年龄和恋爱偏好。",id);
        }
        public bool FamilyAreKin(string a,string b)
        {
            if(a==b)return true;EnsureFamilyState();var aa=FamilyAncestors(a);var bb=FamilyAncestors(b);
            return aa.Contains(b)||bb.Contains(a)||aa.Overlaps(bb);
        }
        private HashSet<string> FamilyAncestors(string id)
        {
            var set=new HashSet<string>();var queue=new Queue<KeyValuePair<string,int>>();queue.Enqueue(new KeyValuePair<string,int>(id,0));
            while(queue.Count>0){var node=queue.Dequeue();if(node.Value>=5)continue;var p=State.family.profiles.FirstOrDefault(v=>v.residentId==node.Key);if(p==null)continue;
                foreach(var parent in new[]{p.parentAId,p.parentBId})if(!string.IsNullOrEmpty(parent)&&set.Add(parent))queue.Enqueue(new KeyValuePair<string,int>(parent,node.Value+1));}
            return set;
        }
        public bool CanFamilyRomance(string a,string b,out string reason)
        {
            reason=null;var p=FamilyProfile(a);var q=FamilyProfile(b);
            if(p==null||q==null||a==b){reason="请选择两位不同的居民。";return false;}
            if(p.ageYears<18||q.ageYears<18){reason="恋爱和婚育仅向成年居民开放。";return false;}
            if(FamilyAreKin(a,b)){reason="有亲属关系的居民不能建立恋爱关系。";return false;}
            if(!FamilyAttracted(p,q)||!FamilyAttracted(q,p)){reason="两人的恋爱偏好不匹配。";return false;}
            var ca=FamilyCouple(a);var cb=FamilyCouple(b);
            if(ca!=null&&ca.parentAId!=b&&ca.parentBId!=b||cb!=null&&cb.parentAId!=a&&cb.parentBId!=a){reason="其中一位居民已有伴侣。";return false;}
            return true;
        }
        private static bool FamilyAttracted(LifeFamilyProfile a,LifeFamilyProfile b)=>a.romancePreference=="Anyone"||a.romancePreference=="Men"&&b.gender=="Man"||a.romancePreference=="Women"&&b.gender=="Woman";
        public int FamilyFriendship(string a,string b)
        {var x=State.relationships.FirstOrDefault(r=>r.fromId==a&&r.toId==b);var y=State.relationships.FirstOrDefault(r=>r.fromId==b&&r.toId==a);return ((x?.meter??0)+(y?.meter??0))/2;}
        public string FamilyRelationshipLabel(string a,string b)
        {
            var c=FamilyCouple(a,b,true);var r=State.relationships.FirstOrDefault(v=>v.fromId==a&&v.toId==b);
            if(r?.fighting==true)return "争吵中";if(c!=null&&c.status!="Broken")return c.status=="Married"?"已婚":c.status=="Engaged"?"已订婚":"恋人";
            if(FamilyAreKin(a,b))return "家人";return r?.kind=="BestFriend"?"挚友":r?.kind=="Friend"?(FamilyFriendship(a,b)>=FamilyGoodFriendMeter?"好朋友":"朋友"):c?.status=="Broken"?"曾经的伴侣":"认识";
        }
        // Tomodachi pair levels on the source meter (MeterInit 100, MeterMax 200):
        // 0 认识, 1 朋友 (kind Friend), 2 好朋友 (Friend at a high meter), 3 挚友 (kind BestFriend).
        public const int FamilyFriendMeter=120,FamilyGoodFriendMeter=160,FamilyBestFriendMeter=180;
        public const double FamilyReconcileMinutes=60,FamilySuggestionMinutes=45;
        public int FamilyRelationLevel(string a,string b)
        {
            var r=State.relationships.FirstOrDefault(v=>v.fromId==a&&v.toId==b);if(r==null)return 0;
            if(r.kind=="BestFriend")return 3;if(r.kind!="Friend"&&r.kind!="Lover"&&r.kind!="Spouse"&&r.kind!="Family")return 0;
            return FamilyFriendship(a,b)>=FamilyGoodFriendMeter?2:1;
        }
        // Compatibility 0..100 from the five creative personality axes. A resident
        // whose axes were never tuned (all 3) gets stable axes from the id instead.
        public int FamilyCompatibility(string a,string b)
        {var x=FamilyTraits(a);var y=FamilyTraits(b);int diff=0;for(int i=0;i<5;i++)diff+=Math.Abs(x[i]-y[i]);return Math.Max(0,100-diff*5);}
        private int[] FamilyTraits(string id)
        {
            var p=State.creative?.residents?.FirstOrDefault(v=>v.residentId==id);
            if(p!=null&&!(p.energy==3&&p.sociability==3&&p.patience==3&&p.expression==3&&p.curiosity==3))return new[]{p.energy,p.sociability,p.patience,p.expression,p.curiosity};
            uint seed=2166136261u;foreach(char ch in id??"")seed=unchecked((seed^ch)*16777619u);
            var traits=new int[5];for(int i=0;i<5;i++){traits[i]=1+(int)(seed%5);seed/=5;}return traits;
        }
        private int FamilyScaledGain(string a,string b,int delta)=>delta<=0?delta:Math.Max(1,(int)Math.Round(delta*(75+FamilyCompatibility(a,b)/2)/100.0));
        private int FamilyRoll(string a,string b,string salt)
        {uint seed=2166136261u;foreach(char ch in string.Concat(a,b,salt,((long)State.gameMinutes).ToString()))seed=unchecked((seed^ch)*16777619u);return (int)(seed%100);}
        public double FamilyLastFightMinutes(string a,string b)
            =>EnsureFamilyState().memories.LastOrDefault(m=>m.kind=="Fight"&&(m.focusId==a&&m.targetId==b||m.focusId==b&&m.targetId==a))?.gameMinutes??-100000;
        // Player chats, gifts and help (M2) and pair results share this one table.
        public LifeSimulationResolution AdjustFamilyBond(string a,string b,int delta,string reason,bool allowBestFriend)
        {
            if(a==b||FindResident(a)==null||FindResident(b)==null)return FamilyFail("请选择两位不同的居民。");
            FamilyBond(a,b,FamilyScaledGain(a,b,delta));var r=Relationship(a,b);string message=null;
            bool plain=FamilyCouple(a,b)==null&&!FamilyAreKin(a,b)&&!r.fighting;
            if(plain&&r.kind=="Known"&&FamilyFriendship(a,b)>=FamilyFriendMeter){FamilyBond(a,b,0,"Friend");message=FamilyName(a)+"和"+FamilyName(b)+"成为了朋友。";}
            else if(plain&&allowBestFriend&&r.kind=="Friend"&&FamilyFriendship(a,b)>=FamilyBestFriendMeter){FamilyBond(a,b,0,"BestFriend");message=FamilyName(a)+"和"+FamilyName(b)+"成为了挚友！";}
            if(message!=null)return FamilyFinish(message,a,b,"Relation");
            LastError=null;EnsureFamilyState().revision++;Changed();return FamilyResult(true,reason??"");
        }
        public LifeSimulationResolution ForceFamilyQuarrel(string a,string b)
        {
            if(a==b||FindResident(a)==null||FindResident(b)==null)return FamilyFail("请选择两位不同的居民。");
            FamilyBond(a,b,-25);FamilySetFight(a,b,true);return FamilyFinish(FamilyName(a)+"与"+FamilyName(b)+"吵架了，彼此都需要冷静一下。",a,b,"Fight");
        }
        public LifeSimulationResolution SetFamilyRelationLevel(string a,string b,int level)
        {
            if(a==b||FindResident(a)==null||FindResident(b)==null||level<0||level>3)return FamilyFail("请选择两位不同的居民。");
            int[] meters={100,FamilyFriendMeter+10,FamilyGoodFriendMeter+10,FamilyBestFriendMeter+10};string[] kinds={"Known","Friend","Friend","BestFriend"};
            foreach(var pair in new[]{new[]{a,b},new[]{b,a}}){var r=Relationship(pair[0],pair[1]);r.meter=meters[level];r.kind=kinds[level];}
            FamilyBond(a,b,0);return FamilyFinish(FamilyName(a)+"和"+FamilyName(b)+"现在是"+FamilyRelationshipLabel(a,b)+"。",a,b,"Relation");
        }
        public LifeSimulationResolution StartFamilyAction(string focusId,string targetId,string action)
        {
            EnsureFamilyState();var focus=FindResident(focusId);var target=FindResident(targetId);
            if(focus==null||target==null||focus==target)return FamilyFail("请选择两位不同的居民。");
            string reason=FamilyActionReason(focusId,targetId,action);if(reason!=null)return FamilyFail(reason);
            var existing=State.family.choices.FirstOrDefault(c=>c.focusId==focusId&&c.targetId==targetId&&c.action==action);
            if(existing!=null)return FamilyResult(true,"这件事正在等待你的选择。");
            var choice=new LifeFamilyChoice {id=Guid.NewGuid().ToString("N"),action=action,focusId=focusId,targetId=targetId,createdGameMinutes=State.gameMinutes};
            string pair=focus.displayName+"与"+target.displayName;
            switch(action)
            {
                case "Befriend":choice.title=pair+"想更了解彼此";FamilyOptions(choice,"chat","聊共同的爱好","listen","耐心听对方说话","wait","改天再聊");break;
                case "BestFriends":choice.title=pair+"能成为挚友吗？";FamilyOptions(choice,"promise","约定互相支持","wait","再相处一阵");break;
                case "Fight":choice.title=pair+"出现了分歧";FamilyOptions(choice,"argue","各自坚持意见","calm","先冷静聊聊");break;
                case "Reconcile":choice.title=pair+"该怎样和好？";FamilyOptions(choice,"apologize","认真道歉","listen","听听彼此的想法","wait","再冷静一阵");break;
                case "Confess":choice.title=focus.displayName+"想向"+target.displayName+"告白";FamilyOptions(choice,"confess","鼓起勇气表白","friends","还是先当朋友");break;
                case "Date":choice.title=pair+"准备约会";FamilyOptions(choice,"picnic","去海边野餐","walk","一起散步看风景","wait","另约时间");break;
                case "Propose":choice.title=focus.displayName+"想求婚";FamilyOptions(choice,"propose","认真表达心意","wait","先把感情培养好");break;
                case "Wedding":choice.title=pair+"准备举行婚礼";FamilyOptions(choice,"simple","办一场温馨婚礼","party","邀请朋友一起庆祝（100）","wait","晚点再决定");break;
                case "Breakup":choice.title=pair+"正在考虑未来";FamilyOptions(choice,"separate","决定分开","talk","再好好谈一次");break;
                case "Reunite":choice.title=pair+"能重新开始吗？";FamilyOptions(choice,"restart","认真重新交往","friends","保持朋友关系");break;
                case "Baby":choice.title=pair+"想迎接宝宝";FamilyOptions(choice,"pregnancy","准备怀孕迎接宝宝","adopt","申请领养宝宝","wait","以后再考虑");
                    var opt=choice.options[0];opt.enabled=FamilyProfile(focusId).gender=="Woman"||FamilyProfile(targetId).gender=="Woman";opt.reason=opt.enabled?null:"怀孕需要一位成年女性居民；也可以选择领养。";break;
                case "Care":choice.title=focus.displayName+"来照顾"+target.displayName;FamilyOptions(choice,"feed","喂饭并整理衣服","play","一起玩游戏","lullaby","讲故事哄睡");break;
                case "Grow":choice.title=target.displayName+"已经准备好成长";FamilyOptions(choice,"grow","庆祝新的成长阶段","wait","再陪伴一阵");break;
                case "Comfort":choice.title=focus.displayName+"想陪陪"+target.displayName;FamilyOptions(choice,"listen","耐心陪伴和倾听","walk","一起出去走走");break;
                case "Cohabit":choice.title=pair+"想住在一起";FamilyOptions(choice,"suggest","看看合适的住宅","wait","先保持现在的生活");break;
                default:return FamilyFail("没有这个家庭互动。");
            }
            State.family.choices.Add(choice);return FamilyFinish(choice.title+"，请选择接下来怎么做。",focusId,targetId,"choice");
        }
        private string FamilyActionReason(string a,string b,string action)
        {
            var f=EnsureFamilyState();var c=FamilyCouple(a,b,true);bool active=c!=null&&c.status!="Broken";
            bool fighting=State.relationships.Any(r=>(r.fromId==a&&r.toId==b||r.fromId==b&&r.toId==a)&&r.fighting);
            if(new[]{"Confess","Date","Propose","Wedding","Baby","Reunite"}.Contains(action)&&!CanFamilyRomance(a,b,out var reason))return reason;
            if(fighting&&action!="Reconcile"&&action!="Fight"&&action!="Comfort")return "两人还在争吵，先试着和好吧。";
            if(action=="Reconcile"&&!fighting)return "两人现在没有争吵。";
            if(action=="Confess"&&active)return "两人已经是伴侣了。";
            if(action=="Confess"&&c?.status=="Broken")return "两人曾经交往过，可以选择重新开始。";
            if(action=="Date"&&(!active||c.status=="Broken"))return "先成为恋人，再安排约会。";
            if(action=="Propose"&&(!active||c.status!="Dating"))return "求婚需要两人正在交往。";
            if(action=="Wedding"&&(!active||c.status!="Engaged"))return "两人需要先订婚。";
            if(action=="Breakup"&&!active)return "两人目前不是伴侣。";
            if(action=="Reunite"&&(c==null||c.status!="Broken"))return "只有曾经分开的伴侣可以重新交往。";
            if(action=="Baby")
            {if(!active||c.status!="Married")return "两人结婚后才能一起迎接宝宝。";if(f.pregnancies.Any(p=>p.coupleId==c.id&&p.status!="Born"))return "两人已经在等待宝宝了。";
                if(f.children.Count(v=>v.parentAId==c.parentAId&&v.parentBId==c.parentBId)>=f.configuration.maximumChildrenPerCouple)return "这对伴侣的孩子数量已到上限。";}
            if(action=="Care"||action=="Grow")
            {var child=f.children.FirstOrDefault(v=>v.residentId==b);if(child==null||child.stage=="Adult")return "请选择一位还在成长的孩子。";
                if(FamilyProfile(a).ageYears<18)return "需要成年居民来照顾孩子。";
                if(action=="Care"&&State.gameMinutes-child.lastCareGameMinutes<f.configuration.careCooldownGameMinutes)return "孩子刚刚得到照顾，过一会儿再来吧。";
                if(action=="Grow"&&(!child.readyToGrow||child.careActions-child.growthCareBaseline<f.configuration.careActionsPerGrowth))return "还需要更多陪伴和成长时间。";}
            if(action=="Comfort"&&FamilyProfile(b).sadUntilGameMinutes<=State.gameMinutes)return "对方现在没有因感情失落。";
            if(action=="Cohabit"&&!active&&FamilyFriendship(a,b)<f.configuration.friendThreshold)return "先成为朋友或伴侣，再考虑同住。";
            return null;
        }
        private static void FamilyOptions(LifeFamilyChoice c,params string[] values)
        {for(int i=0;i<values.Length;i+=2)c.options.Add(new LifeFamilyOption{id=values[i],label=values[i+1]});}
        private LifeSimulationResolution FamilyFail(string message){LastError=message;return FamilyResult(false,message);}
        private static LifeSimulationResolution FamilyResult(bool success,string message)=>new LifeSimulationResolution {success=success,message=message,provenance=FamilyProvenance};
        private LifeSimulationResolution FamilyFinish(string message,string focus="",string target="",string kind="family")
        {
            LastError=null;var f=EnsureFamilyState();f.revision++;f.memories.Add(new LifeFamilyMemory{id=Guid.NewGuid().ToString("N"),kind=kind,message=message,focusId=focus,targetId=target,gameMinutes=State.gameMinutes});
            while(f.memories.Count>100)f.memories.RemoveAt(0);Journal("family",message,FamilyProvenance,focus,target);Changed();return FamilyResult(true,message);
        }
        private void FamilyBond(string a,string b,int delta,string kind=null)
        {
            int maximum=Math.Max(100,Rules.Data.relation?.meterMax??200);
            foreach(var pair in new[]{new[]{a,b},new[]{b,a}}){var r=Relationship(pair[0],pair[1]);r.meter=Math.Max(0,Math.Min(maximum,r.meter+delta));if(Rules.Data.relation!=null)r.nativeRank=NativeLifeRulesCalculations.RelationRank(r.meter,Rules.Data.relation);r.nativeMeterApplied=false;r.meterProvenance=FamilyProvenance;r.provenance=FamilyProvenance;if(kind!=null)r.kind=kind;}
        }
        private void FamilySetFight(string a,string b,bool value){Relationship(a,b).fighting=value;Relationship(b,a).fighting=value;}
        private string FamilyName(string id)=>FindResident(id)?.displayName??"居民";
    }
}
