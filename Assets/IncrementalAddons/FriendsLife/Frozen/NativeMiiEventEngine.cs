using System;
using System.Collections.Generic;
using System.Linq;

namespace TomodachiLife.Simulation
{
    /// <summary>Explicit snapshots of predicate inputs. Null means unavailable;
    /// absence of an original state component is never silently treated as true.</summary>
    public sealed class NativeMiiEventActorSnapshot
    {
        public string id,homeId;
        public uint? feelingHash,aiEventFlags;
        public bool? isGroup,walking,sleeping,accident,hasFight,hasOneSideLove,hasTrouble,hasOtherInSamePlace,cooldownReady,stayNone,onIsland;
        public int? housemateCount,groupSize,groupId;
        public float? worldX,worldZ;
        public uint[] nativeGoodsIds;
    }
    public sealed class NativeMiiEventQuery
    {
        public string kind,sourceFunction,actorId,otherId;
        public uint id,id2;
        public int arg0,arg1;
    }
    public sealed class NativeMiiEventContext
    {
        public NativeMiiEventActorSnapshot Focus;
        // These are explicitly chosen participants, not an unfiltered candidate
        // pool. Native discovery/shuffling is a separate caller boundary.
        public NativeMiiEventActorSnapshot[] Targets=Array.Empty<NativeMiiEventActorSnapshot>(),Anothers=Array.Empty<NativeMiiEventActorSnapshot>();
        public int? population,timePeriod;
        public bool? firstHistoryAvailable;
        public Func<uint,bool?> firstHistoryAllows;
        public Func<uint,uint?> statistics;
        public Func<NativeMiiEventQuery,bool?> resolve;
        public Func<string,string,NativeMiiEventRelationSnapshot> relation;
        public Func<NativeMiiEvent,NativeMiiEventContext,int?> relationBoostRate;
        public Func<uint,NativeMiiEventContext,NativeMiiEventContext> reselectBranchParticipants;
    }

    /// <summary>Source compiler 004d3234 and its seven predicate builders,
    /// and AND/inversion evaluation from 01d471b4/47544/477f4/488cc/48d1c/48df4.
    /// Complex live predicates remain explicit nullable callbacks, not inferred
    /// from table names or a simulator's presentation text.</summary>
    public sealed class NativeMiiEventEngine
    {
        public const string CompilerSource="main 004d3234,004d3dd4,004d34f8,004d3a18,004d36fc,004d435c,004d4684,004d44e0";
        public const string EvaluationSource="main 01d471b4,01d47544,01d477f4,01d488cc,01d48d1c,01d48df4";
        public const uint NothingHash=2848093597;
        private readonly NativeMiiEventDatabase database;
        private readonly Dictionary<uint,NativeMiiEventCondition> conditions;
        private readonly Dictionary<uint,NativeMiiEventFeeling> feelings;
        private readonly Dictionary<uint,NativeMiiEventPlace> places;
        private readonly Dictionary<uint,NativeMiiEventTime> times;
        private readonly NativeMiiEventRelations relationEngine;
        public NativeMiiEvent[] Events=>database.Data.events;
        public NativeMiiEventDatabase Database=>database;
        private sealed class Predicate
        {
            public string kind,source;
            public bool expected=true;
            public uint id,id2;
            public int arg0,arg1;
            public NativeMiiEventFloat2 box0,box1;
        }
        private static Predicate P(string kind,string source,bool expected=true,uint id=0,int arg0=0,int arg1=0,uint id2=0)
            =>new Predicate{kind=kind,source=source,expected=expected,id=id,id2=id2,arg0=arg0,arg1=arg1};
        public NativeMiiEventEngine(NativeMiiEventDatabase database)
        {
            this.database=database??throw new ArgumentNullException(nameof(database));
            conditions=(database.Data.conditions??Array.Empty<NativeMiiEventCondition>()).ToDictionary(v=>v.id);
            feelings=(database.Data.feelings??Array.Empty<NativeMiiEventFeeling>()).ToDictionary(v=>v.id);
            places=(database.Data.places??Array.Empty<NativeMiiEventPlace>()).ToDictionary(v=>v.id);
            times=(database.Data.times??Array.Empty<NativeMiiEventTime>()).ToDictionary(v=>v.id);
            relationEngine=new NativeMiiEventRelations(database);
        }
        public static NativeMiiEventEngine Load()=>new NativeMiiEventEngine(NativeMiiEventDatabase.Load());
        public NativeMiiEvent FindEvent(string name)=>Events?.FirstOrDefault(v=>v.name==name);
        public NativeMiiEvent FindEvent(uint id)=>Events?.FirstOrDefault(v=>v.id==id);
        public NativeMiiEventCondition FindCondition(uint id)=>conditions.TryGetValue(id,out var value)?value:null;
        private static NativeMiiEventDecision Unknown(string reason,string source)=>NativeMiiEventDecision.Unsupported(reason,source);
        private static NativeMiiEventDecision Known(bool pass,string reason,string source)=>pass?NativeMiiEventDecision.Pass(source):NativeMiiEventDecision.Fail(reason,source);
        private static bool Valid(NativeMiiEventActorSnapshot actor)=>actor!=null&&!string.IsNullOrEmpty(actor.id);

        public NativeMiiEventDecision EvaluateCondition(uint id,NativeMiiEventContext context)
        {
            if(id==0)return NativeMiiEventDecision.Pass("No branch condition, main 01d4bc24");
            if(context==null||!Valid(context.Focus))return NativeMiiEventDecision.Fail("Missing focus participant",EvaluationSource);
            if(!conditions.TryGetValue(id,out var condition))return Unknown("Condition row is not present: "+id,CompilerSource);
            NativeMiiEventDecision unresolved=null;
            Func<NativeMiiEventDecision,bool> keep=result=>
            {
                if(result.outcome==NativeMiiEventOutcome.Unsupported&&unresolved==null)unresolved=result;
                return result.outcome!=NativeMiiEventOutcome.Fail;
            };
            // Special cached predicates are evaluated before the normal unary
            // list by 01d47544; their expectation byte is preserved here.
            if(!keep(Check(P("Group","00288ab4",condition.FocusIsGroup),context,context.Focus)))return lastFailure;
            if(condition.FocusIsNotSleeping&&!keep(Check(P("Sleeping","0164a390",false),context,context.Focus)))return lastFailure;
            if(condition.FocusFeelingConditionId!=0&&!keep(Check(P("Feeling","004b2ab4/01648164",id:condition.FocusFeelingConditionId),context,context.Focus)))return lastFailure;
            foreach(var predicate in FocusPredicates(condition).Take(11))if(!keep(Check(predicate,context,context.Focus)))return lastFailure;
            foreach(var predicate in GlobalPredicates(condition).Take(5))if(!keep(Check(predicate,context,null)))return lastFailure;
            if(!condition.IsNeedTargetMii)return unresolved??NativeMiiEventDecision.Pass(EvaluationSource);
            var targets=context.Targets??Array.Empty<NativeMiiEventActorSnapshot>();
            if(targets.Length<condition.TargetMiiMinNum||targets.Length>condition.TargetMiiMaxNum)return NativeMiiEventDecision.Fail("Selected target count is outside native minimum/maximum", "01d47d78");
            foreach(var target in targets)
            {
                if(!Valid(target)||target.id==context.Focus.id)return NativeMiiEventDecision.Fail("Invalid or duplicate target participant","01d477f4");
                foreach(var predicate in TargetSpecial(condition))if(!keep(Check(predicate,context,target,context.Focus)))return lastFailure;
                foreach(var predicate in TargetPredicates(condition).Take(6))if(!keep(Check(predicate,context,target)))return lastFailure;
                foreach(var predicate in FocusTargetPredicates(condition).Take(7))if(!keep(Check(predicate,context,context.Focus,target)))return lastFailure;
            }
            if(condition.IsNeedAnotherMii)
            {
                var others=context.Anothers??Array.Empty<NativeMiiEventActorSnapshot>();
                if(others.Length<condition.AnotherMiiMinNum||others.Length>condition.AnotherMiiMaxNum)return NativeMiiEventDecision.Fail("Selected another count is outside native minimum/maximum","01d47d78");
                foreach(var other in others)
                {
                    if(!Valid(other)||other.id==context.Focus.id||targets.Any(t=>t.id==other.id))return NativeMiiEventDecision.Fail("Invalid or duplicate third participant","01d488cc");
                    if(condition.AnotherIsNotAccident&&!keep(Check(P("Accident","01d497a0",false),context,other)))return lastFailure;
                    if(condition.AnotherIsNotSleeping&&!keep(Check(P("Sleeping","0164a390",false),context,other)))return lastFailure;
                    if(condition.AnotherFeelingConditionId!=0&&!keep(Check(P("Feeling","004b2ab4/01648164",id:condition.AnotherFeelingConditionId),context,other)))return lastFailure;
                    if(condition.FcsToAnoIsAwareOf&&!keep(Check(P("AwareOf","01d497d8"),context,context.Focus,other)))return lastFailure;
                    if(condition.FcsToAnoRelationConditionId!=0&&condition.FcsToAnoRelationConditionId!=NothingHash&&!keep(Check(P("Relation","00263f48/0005146c",id:condition.FcsToAnoRelationConditionId),context,context.Focus,other)))return lastFailure;
                    foreach(var predicate in AnotherPredicates(condition).Take(2))if(!keep(Check(predicate,context,other)))return lastFailure;
                    foreach(var predicate in FocusAnotherPredicates(condition).Take(3))if(!keep(Check(predicate,context,context.Focus,other)))return lastFailure;
                    foreach(var target in targets)foreach(var predicate in TargetAnotherPredicates(condition).Take(4))if(!keep(Check(predicate,context,target,other)))return lastFailure;
                }
            }
            return unresolved??NativeMiiEventDecision.Pass(EvaluationSource);
        }
        private NativeMiiEventDecision lastFailure;
        private NativeMiiEventDecision Check(Predicate predicate,NativeMiiEventContext context,NativeMiiEventActorSnapshot actor,NativeMiiEventActorSnapshot other=null)
        {
            bool? value=Resolve(predicate,context,actor,other);
            if(!value.HasValue)return Unknown("Native input unavailable: "+predicate.kind+(actor==null?"":" for "+actor.id),predicate.source);
            var result=Known(value.Value==predicate.expected,"Native predicate rejected: "+predicate.kind,predicate.source);
            if(result.outcome==NativeMiiEventOutcome.Fail)lastFailure=result;
            return result;
        }
        private bool? Resolve(Predicate p,NativeMiiEventContext c,NativeMiiEventActorSnapshot a,NativeMiiEventActorSnapshot b)
        {
            bool? value=null;
            switch(p.kind)
            {
                case "Group":value=a?.isGroup;break;
                case "Sleeping":value=a?.sleeping;break;
                case "Accident":value=a?.accident;break;
                case "Walking":value=a?.walking;break;
                case "Fight":value=a?.hasFight;break;
                case "OneSideLove":value=a?.hasOneSideLove;break;
                case "Trouble":value=a?.hasTrouble;break;
                case "OtherInSamePlace":value=a?.hasOtherInSamePlace;break;
                case "Cooldown":value=a?.cooldownReady;break;
                case "StayNone":value=a?.stayNone;break;
                case "Population":if(c.population.HasValue)value=p.arg0<=c.population.Value;break;
                case "HousemateCount":if(a?.housemateCount!=null)value=p.arg0<=a.housemateCount.Value;break;
                case "GroupSize":if(a?.groupSize!=null)value=p.arg0==a.groupSize.Value;break;
                case "GoldPhase":if(p.arg0==0)value=true;break;
                case "Goods":if(a?.nativeGoodsIds!=null)value=a.nativeGoodsIds.Contains(p.id);break;
                case "Feeling":
                    if(a?.feelingHash!=null&&feelings.TryGetValue(p.id,out var feeling))
                    {
                        int index=Array.IndexOf(database.Data.feelingHashes,a.feelingHash.Value);
                        bool[] mask={feeling.IsExecutableNormal,feeling.IsExecutableGood,feeling.IsExecutableIrritate,feeling.IsExecutableDeject,feeling.IsExecutableCareless,feeling.IsExecutableAngry,feeling.IsExecutableDepress,feeling.IsExecutableWorry};
                        value=index>=0&&index<mask.Length&&mask[index];
                    }
                    break;
                case "AIEventFlag":
                    int bit=Array.IndexOf(database.Data.aiEventTypeHashes,p.id);
                    if(a?.aiEventFlags!=null&&bit>=0)value=(a.aiEventFlags.Value&(1U<<(bit&31)))!=0;
                    break;
                case "Box":
                    if(a?.worldX!=null&&a.worldZ.HasValue&&p.box0!=null&&p.box1!=null)
                        value=Math.Min(p.box0.X,p.box1.X)<=a.worldX.Value&&a.worldX.Value<=Math.Max(p.box0.X,p.box1.X)&&Math.Min(p.box0.Y,p.box1.Y)<=a.worldZ.Value&&a.worldZ.Value<=Math.Max(p.box0.Y,p.box1.Y);
                    break;
                case "Statistics":
                    uint? count=c.statistics?.Invoke(p.id);
                    if(count.HasValue)value=(p.arg0<0||(uint)p.arg0<=count.Value)&&(p.arg1<0||count.Value<=(uint)p.arg1);
                    break;
                case "Time":
                    if(c.timePeriod.HasValue&&times.TryGetValue(p.id,out var time))
                    {
                        uint[] periods={time.ConditionInEarlyMorning,time.ConditionInMorning,time.ConditionInAfternoon,time.ConditionInEvening,time.ConditionInNight,time.ConditionInMidnight};
                        int index=c.timePeriod.Value;value=index>=0&&index<6&&periods[index]==660592555U;
                    }
                    break;
                case "Place":
                    if(a?.onIsland==true&&places.TryGetValue(p.id,out var place))value=place.IsOnIsland;
                    break;
                case "GroupTogether":if(a?.groupId!=null&&b?.groupId!=null)value=a.groupId.Value!=-1&&a.groupId.Value==b.groupId.Value;break;
                case "Relation":
                case "ReverseRelation":
                    if(a!=null&&b!=null)
                    {
                        var snapshot=p.kind=="ReverseRelation"?c.relation?.Invoke(b.id,a.id):c.relation?.Invoke(a.id,b.id);
                        var result=relationEngine.Evaluate(p.id,snapshot);
                        if(result.outcome!=NativeMiiEventOutcome.Unsupported)value=result.IsPass;
                    }
                    break;
            }
            if(value.HasValue)return value;
            return c.resolve?.Invoke(new NativeMiiEventQuery{kind=p.kind,sourceFunction=p.source,actorId=a?.id,otherId=b?.id,id=p.id,id2=p.id2,arg0=p.arg0,arg1=p.arg1});
        }
        private IEnumerable<Predicate> FocusPredicates(NativeMiiEventCondition r)
        {
            if(r.FocusPlaceConditionId!=0)yield return P("Place","003ef214",id:r.FocusPlaceConditionId);
            if(r.FocusHasGoodsId!=0)yield return P("Goods","01648194",id:r.FocusHasGoodsId);
            if(r.FocusHasGoodsRandomRule)yield return P("GoodsRandomRule","01d4900c",id:r.FocusGoodsRandomRulePreset,id2:r.FocusGoodsRandomRuleUgc);
            if(r.FocusIsAIMiiEventFlag)yield return P("AIEventFlag","01649c80",id:r.FocusIsAIMiiEventType);
            else if(r.FocusIsNotAIMiiEventFlag)yield return P("AIEventFlag","01649c80",false,r.FocusIsAIMiiEventType);
            yield return P("HousemateCount","01d49174",arg0:r.FocusHousemateNumOrMore);
            // This gate is unconditional when FocusIsGroup is false in the
            // original compiler; it is not represented by a table boolean.
            if(!r.FocusIsGroup)yield return P("Cooldown","01d491a8");
            if(r.FocusIsWalking)yield return P("Walking","01d49218");
            if(r.FocusNotHasFight)yield return P("Fight","01d49250",false);
            int love=Array.IndexOf(database.Data.oneSideLoveHashes,r.FocusOneSideLove);
            if(love==1||love==2)yield return P("OneSideLove","01d49280",love==1);
            if(r.FocusHasNoTrouble)yield return P("Trouble","01d492b0",false);
            if(r.FocusIsNoneInSamePlace)yield return P("OtherInSamePlace","01d492f0",false);
            if(r.FocusIsInWorldBBArea)yield return new Predicate{kind="Box",source="01d49434",box0=r.FocusInWorldBBArea0,box1=r.FocusInWorldBBArea1};
        }
        private IEnumerable<Predicate> GlobalPredicates(NativeMiiEventCondition r)
        {
            if(r.GameTimeConditionId!=0)yield return P("Time","00290cf0/00290dc0",id:r.GameTimeConditionId);
            yield return P("Population","01d49494",arg0:r.AllMiiNumOrMore);
            yield return P("GoldPhase","007c75c4",id:r.GoldSeriesUnlockPhaseCondition,arg0:Array.IndexOf(database.Data.goldPhaseHashes,r.GoldSeriesUnlockPhaseCondition));
            if(r.IsCheckStatisticsCount)yield return P("Statistics","01d494c4",id:r.StatisticsCountType,arg0:r.StatisticsCountOrMore,arg1:r.StatisticsCountOrLess);
            if(r.ForegroundMapHouseToiletEmpty)yield return P("ForegroundHouseToiletEmpty","01d49540");
        }
        private IEnumerable<Predicate> TargetSpecial(NativeMiiEventCondition r)
        {
            if(r.FcsToTgtIsAwareOf)yield return P("ReverseAwareOf","01d497d8");
            if(r.TargetIsNotAccident)yield return P("Accident","01d497a0",false);
            if(r.TargetIsGroup)yield return P("Group","00288ab4");
            if(r.TargetIsNotGroup)yield return P("Group","00288ab4",false);
            if(r.TargetIsNotSleeping)yield return P("Sleeping","0164a390",false);
            if(r.TargetFeelingConditionId!=0)yield return P("Feeling","004b2ab4/01648164",id:r.TargetFeelingConditionId);
            if(r.FcsToTgtRelationConditionId!=0&&r.FcsToTgtRelationConditionId!=NothingHash)yield return P("ReverseRelation","00263f48",id:r.FcsToTgtRelationConditionId);
        }
        private static IEnumerable<Predicate> TargetPredicates(NativeMiiEventCondition r)
        {
            if(r.TargetHasGoodsId!=0)yield return P("Goods","01648194",id:r.TargetHasGoodsId);
            if(r.TargetIsAIMiiEventFlag)yield return P("AIEventFlag","01649c80",id:r.TargetIsAIMiiEventType);
            if(r.TargetIsGroup&&r.TargetGroupNum>1)yield return P("GroupSize","01d4975c",arg0:r.TargetGroupNum);
            if(r.TargetIsWalking)yield return P("Walking","01d49218");
            if(r.TargetNotHasFight)yield return P("Fight","01d49250",false);
            if(r.TargetHasNoTrouble)yield return P("Trouble","01d492b0",false);
            if(r.TargetIsStayNone)yield return P("StayNone","01649d74");
        }
        private static IEnumerable<Predicate> FocusTargetPredicates(NativeMiiEventCondition r)
        {
            if(r.FcsToTgtIsRoommate)yield return P("Roommate","01649f64");
            if(r.FcsToTgtGroupTogether)yield return P("GroupTogether","01d49800");
            if(r.FcsToTgtIsInPrivateRoom)yield return P("PrivateRoom","01d49840");
            if(r.FcsToTgtIsGroupWalking2)yield return P("GroupWalking","01d49850",arg0:2);
            if(r.FcsToTgtIsInSameHouse)yield return P("SameHouse","01d49950");
            if(r.FcsToTgtCanLove)yield return P("CanLove","01d499b8");
            if(r.FcsToTgtCanFight)yield return P("CanFight","01d499d4");
        }
        private static IEnumerable<Predicate> AnotherPredicates(NativeMiiEventCondition r)
        {
            if(r.AnotherIsWalking)yield return P("Walking","01d49218");
            if(r.AnotherHasNoTrouble)yield return P("Trouble","01d492b0",false);
        }
        private static IEnumerable<Predicate> FocusAnotherPredicates(NativeMiiEventCondition r)
        {
            if(r.FcsToAnoIsNotInSamePlace)yield return P("SamePlace","01d499e4",false);
            if(r.FcsToAnoCanLove)yield return P("CanLove","01d499b8");
            if(r.FcsToAnoCanFight)yield return P("CanFight","01d499d4");
        }
        private static IEnumerable<Predicate> TargetAnotherPredicates(NativeMiiEventCondition r)
        {
            if(r.TgtToAnoRelationConditionId!=0&&r.TgtToAnoRelationConditionId!=NothingHash)yield return P("Relation","00263f48",id:r.TgtToAnoRelationConditionId);
            if(r.TgtToAnoGroupTogether)yield return P("GroupTogether","01d49800");
            if(r.TgtToAnoCanLove)yield return P("CanLove","01d499b8");
            if(r.TgtToAnoCanLoveGenderAgeBlood)yield return P("CanLoveGenderAgeBlood","01d499b8",arg0:1);
        }

        public NativeMiiEventDecision CheckOccurrence(NativeMiiEvent record,NativeMiiEventContext context,ref NativeMiiEventRandomState random,out int appliedRate)
        {
            appliedRate=0;if(record==null)return NativeMiiEventDecision.Fail("Event row is missing","01d4abe4");
            if(!record.IsEnable)return NativeMiiEventDecision.Fail("Source event is disabled","MiiEventParam.IsEnable");
            var eligibility=EvaluateCondition(record.ConditionId,context);if(!eligibility.IsPass)return eligibility;
            int rate=record.GenerateRate;
            if(record.IsUseFirstGenerateRate)
            {
                if(!context.firstHistoryAvailable.HasValue)return Unknown("Native first-generation history availability is unknown","01d4abe4");
                if(context.firstHistoryAvailable.Value)
                {
                    bool? allows=context.firstHistoryAllows?.Invoke(record.id);
                    if(!allows.HasValue)return Unknown("Native per-event first-generation history is unknown","01d4abe4");
                    if(allows.Value)rate=record.FirstGenerateRate;
                }
            }
            if((context.Targets?.Length??0)>0&&(record.GenerateRateOnRelationBoost??Array.Empty<NativeMiiEventBoost>()).Any(v=>v.rate>=0))
            {
                int? boost=context.relationBoostRate?.Invoke(record,context);
                if(!boost.HasValue)return Unknown("Native relation-boost gate/rank is unavailable","01d4c038");
                if(boost.Value>=0)rate=boost.Value;
            }
            appliedRate=rate;return Known(NativeMiiEventRandom.TestRate(rate,ref random),"Native generation-rate draw rejected","01d4abe4/01d03800");
        }
        public NativeMiiEventDecision SelectBranch(NativeMiiEvent record,NativeMiiEventContext context,ref NativeMiiEventRandomState localRandom,out int branchIndex)
        {
            branchIndex=-1;if(record==null)return NativeMiiEventDecision.Fail("Event row missing","01d4b004");
            var branches=record.BranchParam??Array.Empty<NativeMiiEventBranch>();
            if(branches.Length<=1){branchIndex=0;return NativeMiiEventDecision.Pass("01d4b004 single-branch fast path");}
            var rates=new int[branches.Length];var eligible=new bool[branches.Length];NativeMiiEventDecision unknown=null;
            for(int i=0;i<branches.Length;i++)
            {
                rates[i]=branches[i].Rate;var branchContext=context;
                if(branches[i].ConditionId!=0&&branches[i].IsConditionOtherReselect)
                {
                    branchContext=context.reselectBranchParticipants?.Invoke(branches[i].ConditionId,context);
                    if(branchContext==null){unknown=Unknown("Native branch participant reselection is unavailable","01d4bc24");continue;}
                }
                var result=EvaluateCondition(branches[i].ConditionId,branchContext);
                if(result.outcome==NativeMiiEventOutcome.Unsupported&&rates[i]!=0)unknown=result;
                eligible[i]=result.IsPass;
            }
            if(unknown!=null)return unknown;
            branchIndex=NativeMiiEventRandom.SelectWeightedBranch(rates,eligible,ref localRandom);
            return Known(branchIndex>=0,"No source branch has a passing nonzero-rate candidate","01d4b004");
        }
        // Adapter utility for an explicitly supplied order. The outer loop is
        // not a recovered native selector: only each CheckOccurrence gate is a
        // native port. Discovery, ordering and the occurrence clock remain
        // caller seams, and no fixed rotation is installed by this engine.
        public NativeMiiEventDecision CheckOrderedCandidates(IEnumerable<NativeMiiEvent> nativeCallerOrder,NativeMiiEventContext context,ref NativeMiiEventRandomState random,out NativeMiiEvent selected)
        {
            selected=null;if(nativeCallerOrder==null)return Unknown("Native candidate enumeration/order is unavailable","Native selector caller seam; 01d49b38 not fully ported");
            foreach(var candidate in nativeCallerOrder)
            {
                var result=CheckOccurrence(candidate,context,ref random,out _);
                if(result.outcome==NativeMiiEventOutcome.Unsupported)return result;
                if(result.IsPass){selected=candidate;return result;}
            }
            return NativeMiiEventDecision.Fail("No supplied candidate passed its condition and occurrence draw","Adapter outer loop; native per-event gate 01d4abe4");
        }
    }
}
