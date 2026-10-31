using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TomodachiLife.Simulation
{
    public sealed partial class LifeSimulationRuntime
    {
        public NativeLifeRulesDatabase Rules { get; private set; }
        public LifeSimulationConfiguration Configuration { get; private set; }
        public LifeSimulationSave State { get; private set; }
        public NativeMiiEventEngine NativeEventEngine { get; private set; }
        public Func<string,string,string,NativeMiiEventContext> NativeEventContextProvider;
        // Optional scoped port: caller owns lifetime, identity checks, exception
        // containment and cleanup. Null preserves the original autonomous loop.
        public Func<LifeSimulationResident,double,bool> BeforeResidentActivitySelection;
        public string LastError { get; private set; }
        public event Action StateChanged;
        public string[] JournalMessages=>State.journal.Select(entry=>entry.message).ToArray();
        public const string Reconstruction = "Reconstructed playable scheduling/state application; editable LifeRules/reconstruction-config";
        public const string NativeArithmetic = "Original 01d6d404 wallet / 01d08870 resolved-food arithmetic; source table values";
        private NativeMiiEventDecision lastNativeCondition;
        private bool lastConditionUsesFallback;
        private LifeSimulationRuntime(NativeLifeRulesDatabase rules,LifeSimulationConfiguration config)
        {
            Rules=rules ?? throw new ArgumentNullException(nameof(rules));
            Configuration=config ?? NativeLifeRulesDatabase.LoadConfiguration();
            if(Configuration.minutesPerDay<=0 || Configuration.gameMinutesPerRealSecond<0)
                throw new ArgumentException("Invalid editable simulation configuration.");
#if !LIFE_SIMULATION_STANDALONE
            NativeEventEngine=NativeMiiEventEngine.Load();
#endif
        }
        public static LifeSimulationRuntime CreateNew(NativeLifeRulesDatabase rules,LifeSimulationConfiguration config=null)
        {
            var simulation=new LifeSimulationRuntime(rules,config);
            simulation.State=new LifeSimulationSave { walletRaw=Math.Max(0,Math.Min(99999999,simulation.Configuration.initialWalletRaw)),
                gameMinutes=simulation.Configuration.clockStartMinutes,
                nextPairEventGameMinutes=simulation.Configuration.clockStartMinutes+simulation.Configuration.pairEventIntervalGameMinutes };
            simulation.State.eventSeedTick=unchecked((uint)Environment.TickCount);
            simulation.State.eventRandom=NativeMiiEventRandom.FromSystemTick(simulation.State.eventSeedTick);
            simulation.State.eventRandomProvenance="Original0005021c seed expansion with explicit host Environment.TickCount low32; original platform tick acquisition/frequency is a caller boundary; main01d03800 transition persisted";
            if(config==null)simulation.Configuration.scheduledEventNames=rules.Data.events.Where(e=>e.enabled).Select(e=>e.name).ToArray();
            return simulation;
        }
        public void BindNativeEventEngine(NativeMiiEventEngine engine){NativeEventEngine=engine??throw new ArgumentNullException(nameof(engine));}
        public LifeSimulationResident FindResident(string id) => State.residents.FirstOrDefault(r=>r.id==id);
        public LifeSimulationResident CreateResident(string displayName,string avatarJson="")
        {
            if(State.residents.Count>=Configuration.maximumResidents){LastError="居民数量已到当前配置上限。";return null;}
            var resident=new LifeSimulationResident { id=Guid.NewGuid().ToString("N"),displayName=string.IsNullOrWhiteSpace(displayName)?"居民"+(State.residents.Count+1):displayName.Trim(),
                avatarJson=avatarJson ?? "",hunger=Configuration.initialNeed,social=Configuration.initialNeed,energy=Configuration.initialNeed,
                activityUntilGameMinutes=State.gameMinutes };
            State.residents.Add(resident);SeedGiftTaste(resident.id);Journal("resident","欢迎 "+resident.displayName+" 来到岛上。",Reconstruction,resident.id);Changed();return resident;
        }
        public void Advance(double realSeconds)
        {
            if(double.IsNaN(realSeconds)||double.IsInfinity(realSeconds)||realSeconds<0)throw new ArgumentOutOfRangeException(nameof(realSeconds));
            double minutes=realSeconds*Configuration.gameMinutesPerRealSecond;
            if(double.IsNaN(minutes)||double.IsInfinity(minutes)||double.IsInfinity(State.gameMinutes+minutes))throw new ArgumentOutOfRangeException(nameof(realSeconds));
            if(minutes==0)return;
            State.gameMinutes+=minutes;
            AdvanceFamily(minutes);
            AdvanceCreative(minutes);
            AdvanceActivities(minutes);
            AdvanceDaily(minutes);
            foreach(var resident in State.residents)
            {
                float hours=(float)(minutes/60);
                resident.hunger=Need(resident.hunger-Configuration.hungerDecayPerHour*hours);
                resident.social=Need(resident.social-Configuration.socialDecayPerHour*hours);
                resident.energy=Need(resident.energy-Configuration.energyDecayPerHour*hours);
                bool deferActivity = BeforeResidentActivitySelection?.Invoke(resident,minutes) == true;
                if(!deferActivity && State.gameMinutes>=resident.activityUntilGameMinutes)ChooseActivity(resident);
            }
            if(Configuration.schedulePairEvents && State.gameMinutes>=State.nextPairEventGameMinutes)
            {
                State.nextPairEventGameMinutes=State.gameMinutes+Math.Max(1,Configuration.pairEventIntervalGameMinutes);
                var available=State.residents.Where(r=>r.activity!="睡觉").ToArray();
                if(available.Length>=2 && State.pendingEvents.Count<Configuration.maximumPendingEvents)
                {
                    int first=PositiveMod(State.pairCursor++,available.Length),second=(first+1)%available.Length;
                    TriggerPairEvent(available[first].id,available[second].id);
                }
            }
            Changed();
        }
        private void ChooseActivity(LifeSimulationResident resident)
        {
            if(IsResidentInActivity(resident.id))return;
            int hour=(int)((State.gameMinutes%Configuration.minutesPerDay)/60);
            bool night=Configuration.sleepStartHour>Configuration.sleepEndHour
                ? hour>=Configuration.sleepStartHour || hour<Configuration.sleepEndHour
                : hour>=Configuration.sleepStartHour && hour<Configuration.sleepEndHour;
            if(night || resident.energy<Configuration.restActivityThreshold)
            { resident.activity="睡觉";resident.energy=Need(resident.energy+Configuration.restNeedRecovery); }
            else if(resident.hunger<Configuration.hungerActivityThreshold)
            { resident.activity="吃饭";resident.hunger=Need(resident.hunger+Configuration.eatNeedRecovery); }
            else if(resident.social<Configuration.socialActivityThreshold && State.residents.Count>1)
            { resident.activity="找朋友聊天";resident.social=Need(resident.social+Configuration.socialNeedRecovery); }
            else
            {
                var choices=Configuration.freeActivities ?? Array.Empty<string>();
                resident.activity=choices.Length==0?"散步":choices[PositiveMod(resident.activityCursor++,choices.Length)];
            }
            resident.activityUntilGameMinutes=State.gameMinutes+Math.Max(1,Configuration.activityDurationGameMinutes);
        }
        private NativeMiiEventContext NativeContext(LifeSimulationResident focus,LifeSimulationResident target,LifeSimulationResident another)
        {
            if(NativeEventContextProvider!=null)return NativeEventContextProvider(focus?.id,target?.id,another?.id);
            Func<LifeSimulationResident,NativeMiiEventActorSnapshot> snapshot=r=>r==null?null:new NativeMiiEventActorSnapshot {
                id=r.id,homeId=r.homeId,feelingHash=r.feelingHash,sleeping=r.activity=="睡觉",hasTrouble=r.activeTroubleHash!=0,
                hasFight=State.relationships.Any(v=>v.fromId==r.id&&v.fighting),hasOneSideLove=State.relationships.Any(v=>v.fromId==r.id&&v.oneSideLove) };
            // These are explicit simulator snapshots. Native group/cooldown,
            // discovery, world positions, statistics and first-history inputs
            // remain nullable; none is silently assumed to pass.
            return new NativeMiiEventContext { Focus=snapshot(focus),Targets=target==null?Array.Empty<NativeMiiEventActorSnapshot>():new[]{snapshot(target)},
                Anothers=another==null?Array.Empty<NativeMiiEventActorSnapshot>():new[]{snapshot(another)},population=State.residents.Count,
                relation=(fromId,toId)=>
                {
                    var forward=State.relationships.FirstOrDefault(v=>v.fromId==fromId&&v.toId==toId);
                    var reverse=State.relationships.FirstOrDefault(v=>v.fromId==toId&&v.toId==fromId);
                    if(forward==null)return null;
                    return new NativeMiiEventRelationSnapshot { nativeType=forward.kind=="Friend"?(int?)2:null,
                        meter=forward.nativeMeterApplied?(int?)forward.meter:null,reverseMeter=reverse!=null&&reverse.nativeMeterApplied?(int?)reverse.meter:null,
                        oneSideLove=forward.oneSideLove,reverseOneSideLove=reverse?.oneSideLove,fighting=forward.fighting };
                } };
        }
        public LifeSimulationPendingEvent TriggerPairEvent(string focusId,string targetId,string eventName=null,string anotherId=null)
        {
            LastError=null;
            var focus=FindResident(focusId);var target=FindResident(targetId);var another=FindResident(anotherId);
            if(eventName==null&&another==null){another=State.residents.FirstOrDefault(r=>r.id!=focusId&&r.id!=targetId&&r.activity!="睡觉");anotherId=another?.id;}
            if(focus==null||target==null||focus==target){LastError="请选择两位不同的岛民。";return null;}
            if(another!=null&&(another==focus||another==target)){LastError="第三位参与者需要是另一位岛民。";return null;}
            if(State.pendingEvents.Count>=Configuration.maximumPendingEvents){LastError="请先处理正在发生的事件。";return null;}
            NativeLifeEventRule rule=null;string occurrenceSupport=eventName==null?"Reconstructed configured candidate order/clock":"Explicit named event: caller/manual occurrence, no native random-generation claim";
            if(eventName!=null)rule=Rules.FindEvent(eventName);
            else
            {
                var schedule=Configuration.scheduledEventNames ?? Array.Empty<string>();
                for(int attempt=0;attempt<schedule.Length;attempt++)
                {
                    var candidate=Rules.FindEvent(schedule[PositiveMod(State.eventCursor++,schedule.Length)]);
                    if(candidate==null||!candidate.enabled)continue;
                    if(!CanScheduleGameplayEvent(candidate,focus,target,another))continue;
                    if(NativeEventEngine!=null)
                    {
                        var source=NativeEventEngine.FindEvent(candidate.name);
                        var decision=NativeEventEngine.CheckOccurrence(source,NativeContext(focus,target,another),ref State.eventRandom,out int appliedRate);
                        if(decision.outcome==NativeMiiEventOutcome.Fail)continue;
                        if(decision.IsPass){rule=candidate;occurrenceSupport="Original01d4abe4/01d03800 rate draw="+appliedRate+"; candidate order/clock supplied by reconstructed scheduler";break;}
                        occurrenceSupport="Unsupported native occurrence: "+decision.reason+"; explicit reconstructed eligibility/occurrence fallback";
                        if(!Configuration.allowReconstructedEligibility)continue;
                    }
                    if(PartialCondition(candidate.conditionId,focus,target,another,out _)){rule=candidate;break;}
                }
            }
            if(rule==null||!rule.enabled){LastError="当前没有可用的事件配置。";return null;}
            if(!PartialCondition(rule.conditionId,focus,target,another,out var reason)){LastError=reason;return null;}
            var pending=new LifeSimulationPendingEvent { id=Guid.NewGuid().ToString("N"),eventName=rule.name,focusResidentId=focusId,
                targetResidentId=targetId,anotherResidentId=anotherId ?? "",createdGameMinutes=State.gameMinutes,manualBranchOverride=eventName!=null,
                usesReconstructedEligibility=lastConditionUsesFallback,nativeEligibilitySupport=lastNativeCondition?.outcome+": "+lastNativeCondition?.reason,
                nativeOccurrenceSupport=occurrenceSupport };
            if(!pending.manualBranchOverride)
            {
                pending.branchSeedTick=unchecked((uint)Environment.TickCount);pending.branchRandom=NativeMiiEventRandom.FromSystemTick(pending.branchSeedTick);
                var decision=NativeEventEngine==null?NativeMiiEventDecision.Unsupported("Native event database is not bound","Runtime adapter"):
                    NativeEventEngine.SelectBranch(NativeEventEngine.FindEvent(rule.name),NativeContext(focus,target,another),ref pending.branchRandom,out pending.nativeSelectedBranch);
                pending.nativeBranchSupport=decision.outcome+": "+decision.reason+"; local seed uses explicit host TickCount boundary";
                if(decision.outcome==NativeMiiEventOutcome.Fail){LastError=decision.reason;return null;}
                if(!decision.IsPass)
                {
                    if(!Configuration.allowReconstructedEligibility){LastError=decision.reason;return null;}
                    var rates=rule.branches.Select(b=>b.rateRaw).ToArray();var eligible=rule.branches.Select(b=>PartialCondition(b.conditionId,focus,target,another,out _)).ToArray();
                    pending.nativeSelectedBranch=NativeMiiEventRandom.SelectWeightedBranch(rates,eligible,ref pending.branchRandom);
                    pending.usesReconstructedEligibility=true;pending.nativeBranchSupport+="; original weighted reservoir over explicitly reconstructed passing participants";
                }
                if(pending.nativeSelectedBranch<0){LastError="没有满足条件的原始事件分支。";return null;}
                pending.nativeBranchSelectionApplied=true;
            }
            else pending.nativeBranchSupport="Explicit named-event/manual branch override; no native branch selection claim";
            pending.provenance="Source native condition/rate/reservoir engine with explicit snapshot/input boundaries; "+pending.nativeOccurrenceSupport+"; "+pending.nativeBranchSupport;
            FillChoices(pending,rule,focus,target,another);
            State.pendingEvents.Add(pending);focus.activity="和 "+target.displayName+" 交谈";target.activity="和 "+focus.displayName+" 交谈";
            Journal("event",focus.displayName+" 和 "+target.displayName+" 遇到了一件事。",pending.provenance,focusId,targetId,rule.name);Changed();return pending;
        }
        private void FillChoices(LifeSimulationPendingEvent pending,NativeLifeEventRule rule,LifeSimulationResident focus,LifeSimulationResident target,LifeSimulationResident another)
        {
            pending.title=GameplayEventTitle(rule);
            pending.branchLabels=rule.branches.Select(branch=>branch.simulationLabel ?? branch.forkStr).ToArray();
            pending.choices=new List<LifeSimulationEventChoice>();
            foreach(var branch in rule.branches)
            {
                if(pending.nativeBranchSelectionApplied&&branch.index!=pending.nativeSelectedBranch)continue;
                string reason=null;bool enabled=pending.nativeBranchSelectionApplied||PartialCondition(branch.conditionId,focus,target,another,out reason);
                pending.usesReconstructedEligibility|=lastConditionUsesFallback;
                foreach(var result in branch.results)
                    pending.choices.Add(new LifeSimulationEventChoice { branchIndex=branch.index,resultIndex=result.index,enabled=enabled,disabledReason=reason,
                        label=GameplayEventChoice(branch.forkStr,result.demoResult) });
            }
        }
        private bool PartialCondition(uint id,LifeSimulationResident focus,LifeSimulationResident target,LifeSimulationResident another,out string reason)
        {
            reason=null;lastConditionUsesFallback=false;
            lastNativeCondition=NativeEventEngine==null?NativeMiiEventDecision.Unsupported("Native event database is not bound","Runtime adapter"):
                NativeEventEngine.EvaluateCondition(id,NativeContext(focus,target,another));
            if(id==0)return true;
            if(lastNativeCondition.IsPass)return true;
            if(lastNativeCondition.outcome==NativeMiiEventOutcome.Fail){reason=lastNativeCondition.reason;return false;}
            var c=Rules.FindCondition(id);
            if(!Configuration.allowReconstructedEligibility){reason="Unsupported native condition: "+lastNativeCondition.reason;return false;}
            lastConditionUsesFallback=true;
            if(c==null)return true;
            if(c.needAnotherMii && another==null){reason="这件事需要第三位岛民参与。";return false;}
            if((c.focusNotSleeping && focus.activity=="睡觉")||(c.targetNotSleeping && target.activity=="睡觉")){reason="先等岛民醒来吧。";return false;}
            if((c.focusHasNoTrouble && focus.activeTroubleHash!=0)||(c.targetHasNoTrouble && target.activeTroubleHash!=0)){reason="岛民还有一件烦恼没处理。";return false;}
            return true;
        }
        public LifeSimulationResolution ChooseBranch(string pendingEventId,int branchIndex,int resultIndex=0)
        {
            var pending=State.pendingEvents.FirstOrDefault(e=>e.id==pendingEventId);
            if(pending==null)return Fail("事件已经结束或不存在。");
            var rule=Rules.FindEvent(pending.eventName);
            if(rule==null||branchIndex<0||branchIndex>=rule.branches.Length)return Fail("该分支不存在。");
            if(pending.nativeBranchSelectionApplied&&branchIndex!=pending.nativeSelectedBranch)return Fail("该结果不属于原生算法选中的事件分支。");
            var branch=rule.branches[branchIndex];
            if(resultIndex<0||resultIndex>=branch.results.Length)return Fail("该演出结果不存在。");
            var actors=new[]{FindResident(pending.focusResidentId),FindResident(pending.targetResidentId),FindResident(pending.anotherResidentId)};
            if(actors[0]==null||actors[1]==null)return Fail("事件的参与居民已经不在岛上。");
            if(!pending.nativeBranchSelectionApplied&&!PartialCondition(branch.conditionId,actors[0],actors[1],actors[2],out var reason))return Fail(reason);
            var source=branch.results[resultIndex];var result=StartResult(actors[0],"event");
            result.nativeEventConditionApplied=!pending.usesReconstructedEligibility;result.nativeEventBranchApplied=pending.nativeBranchSelectionApplied;
            result.usesReconstructedEventInputs=pending.usesReconstructedEligibility;result.nativeEventSupport=pending.provenance+"; demo result supplied explicitly by caller";
            result.provenance="Source event flags/reaction deltas/reward lookups + native wallet arithmetic; reconstructed occurrence/participants/state application";
            if(!source.skipResult)
            {
                foreach(int amount in new[]{source.rewardMoney0,source.rewardMoney1,source.rewardMoney2})
                    if(amount!=0){ChangeWallet(amount,"event-reward",result);result.sourceRewardRaw=unchecked(result.sourceRewardRaw+amount);}
                foreach(var effect in source.relations ?? Array.Empty<NativeLifeRelationEffect>())
                {
                    var from=actors[effect.fromIndex];var to=actors[effect.toIndex];
                    if(from==null||to==null){result.details.Add("保留未就绪参与者的关系反应："+effect.field);continue;}
                    var relationship=Relationship(from.id,to.id);
                    if(Rules.Data.relation==null){result.details.Add("Unsupported native relation reaction: source relation parameters missing");continue;}
                    relationship.meter=NativeLifeRulesCalculations.ApplyRelationReaction(relationship.meter,effect.delta,effect.noChangeRank,Rules.Data.relation);
                    relationship.nativeRank=NativeLifeRulesCalculations.RelationRank(relationship.meter,Rules.Data.relation);relationship.nativeMeterApplied=true;
                    relationship.meterProvenance="Original 01d1b970 reaction clamp/NoChangeRank interval and 0039b3a0 inclusive rank; source RelationRoot parameters";
                    result.details.Add(effect.reactionName+": "+effect.delta+(effect.noChangeRank?"（源标志：不变更等级）":""));
                }
                if(source.becomeFriend01){foreach(var pair in new[]{new[]{actors[0].id,actors[1].id},new[]{actors[1].id,actors[0].id}}){var relation=Relationship(pair[0],pair[1]);if(relation.kind=="Known"||relation.kind=="Friend"||string.IsNullOrEmpty(relation.kind))relation.kind="Friend";}}
                if((source.becomeOnesideLove01||branch.changeOnesideLove01)&&CanFamilyRomance(actors[0].id,actors[1].id,out _))Relationship(actors[0].id,actors[1].id).oneSideLove=true;
                if(source.lostOnesideLove01)Relationship(actors[0].id,actors[1].id).oneSideLove=false;
                if(source.becomeOnesideLove02&&actors[2]!=null&&CanFamilyRomance(actors[0].id,actors[2].id,out _))Relationship(actors[0].id,actors[2].id).oneSideLove=true;
                if(source.fightNormal01){Relationship(actors[0].id,actors[1].id).fighting=true;Relationship(actors[1].id,actors[0].id).fighting=true;}
                if(source.feeling0!=3069107721)actors[0].feelingHash=source.feeling0;
                if(source.feeling1!=3069107721)actors[1].feelingHash=source.feeling1;
                if(source.trouble01Id!=0)actors[0].activeTroubleHash=source.trouble01Id;
                if(source.action0Id!=0||source.action1Id!=0)result.details.Add("原生后续动作引用已保留在源规则中；当前日常调度继续执行。");
                AddHappiness(actors[0],Configuration.eventHappinessGain,result);
            }
            State.pendingEvents.Remove(pending);actors[0].social=Need(actors[0].social+Configuration.socialNeedRecovery);actors[1].social=Need(actors[1].social+Configuration.socialNeedRecovery);
            FinishResult(result,actors[0],"事件已结算："+(string.IsNullOrEmpty(branch.forkStr)?"继续":branch.forkStr));
            Journal("result",result.message,result.provenance,actors[0].id,actors[1].id,rule.name,source.historyId);RecordDailyAction(actors[0].id,"Relation",rule.name,actors[1].id);RecordDailyAction(actors[1].id,"Relation",rule.name,actors[0].id);Changed();return result;
        }
        public LifeSimulationResolution GiveFood(string residentId,string foodName,string tasteColumn=null)
        {
            var resident=FindResident(residentId);var food=Rules.FindFood(foodName);
            if(resident==null||food==null)return Fail("居民或食物不存在。");
            if(resident.nativeMoodDeadlineRaw!=0&&!State.nativeMoodClockResolved)return Fail("现在不能吃东西。");
            var preference=resident.foodPreferences.FirstOrDefault(p=>p.foodName==foodName);
            string column=tasteColumn ?? preference?.tasteColumn ?? Configuration.defaultFoodTasteColumn;
            var taste=(food.tastes ?? Array.Empty<NativeLifeFoodTaste>()).FirstOrDefault(t=>t.column==column);
            if(taste==null||!taste.hasSatisfyValue)return Fail("这个食物现在不能用。");
            var result=StartResult(resident,"food");
            if(!AcquireAndConsume("Food",foodName,Math.Max(0,food.priceRaw),result))return Fail("钱不够购买这份食物。");
            uint reaction=preference?.reactionClass ?? Configuration.defaultFoodReactionClass;
            int amount=NativeLifeRulesCalculations.CalculateResolvedFood(reaction,taste.baseSatisfy,food.quantityFactor,food.priceRaw,
                resident.stomachBig,resident.stomachSmall,Rules.Data.foodPriceDiv,Rules.Data.foodHabitStomachBigFactor,Rules.Data.foodHabitStomachSmallFactor,Rules.Data.foodSatisfyMax);
            result.nativeReactionClass=reaction;
            result.provenance=NativeArithmetic+"; source "+food.rowId+" "+column+"; explicit reconstructed taste/reaction selection; native satisfaction/recovery core selected separately";
            result.details.Add("源基础满足度 "+taste.baseSatisfy+"，份量系数 "+food.quantityFactor+"，本次满足度 "+amount);
            AddHappiness(resident,amount,result,true,true);resident.hunger=Need(resident.hunger+Configuration.eatNeedRecovery);resident.activity="吃 "+foodName;
            resident.activityUntilGameMinutes=State.gameMinutes+Configuration.activityDurationGameMinutes;
            FinishResult(result,resident,result.nativeMoodApplied?resident.displayName+" 收到了 "+foodName+"，心情好了一些。":resident.displayName+" 收到了 "+foodName+"，满足度 "+(amount>=0?"+":"")+amount+"。");
            Journal("food",result.message,result.provenance,residentId);RecordDailyAction(residentId,"Food",foodName);Changed();return result;
        }
        public LifeSimulationResolution GiveGift(string residentId,string goodsName,uint tasteHash=0)
        {
            var resident=FindResident(residentId);var goods=Rules.FindGoods(goodsName);
            if(resident==null||goods==null)return Fail("居民或礼物不存在。");
            if(resident.nativeMoodDeadlineRaw!=0)return Fail("现在不能收礼物。");
            if(tasteHash==0)tasteHash=Configuration.defaultGiftTasteHash;
            if(!Rules.TryGiftSatisfy(tasteHash,out int amount))return Fail("这个礼物现在不能送。");
            var result=StartResult(resident,"gift");
            if(!AcquireAndConsume("Goods",goodsName,Math.Max(0,goods.buyingPriceRaw),result))return Fail("钱不够购买这份礼物。");
            AddHappiness(resident,amount,result,true);resident.social=Need(resident.social+Configuration.socialNeedRecovery);
            result.provenance="Source GoodsSatisfy lookup and native wallet/satisfaction/emotion-ball arithmetic; reconstructed taste assignment/gift application";
            FinishResult(result,resident,resident.displayName+" 收到了礼物 "+goodsName+"。");Journal("gift",result.message,result.provenance,residentId);RecordDailyAction(residentId,"Goods",goodsName);Changed();return result;
        }
        // In-person gifts are Deep Blue Sushi things the player holds or buys.
        // Each resident has one favourite and one disliked kind (Tomodachi).
        public static readonly string[] PlayerGiftKinds={"Sushi","Fish","Soup","Dessert","Drink","Side","Snack"};
        public static string PlayerGiftName(string kind)
        {
            switch(kind){case"Sushi":return"寿司";case"Fish":return"鱼";case"Soup":return"汤";case"Dessert":return"甜点";
                case"Drink":return"饮料";case"Side":return"小菜";case"Snack":return"小吃";default:return"礼物";}
        }
        public LifeDailyTaste GiftTaste(string residentId)
        {
            var stored=DailyState.tastes.FirstOrDefault(t=>t.residentId==residentId);if(stored!=null)return stored;
            // Residents from older saves get a stable taste from their id.
            uint h=StableGameplayHash(residentId);int n=PlayerGiftKinds.Length;int liked=(int)(h%(uint)n);
            return new LifeDailyTaste{residentId=residentId,liked=PlayerGiftKinds[liked],disliked=PlayerGiftKinds[(liked+1+(int)(h/(uint)n%(uint)(n-1)))%n]};
        }
        private void SeedGiftTaste(string residentId){if(DailyState.tastes.All(t=>t.residentId!=residentId))DailyState.tastes.Add(GiftTaste(residentId));}
        public bool SetGiftTaste(string residentId,string liked,string disliked)
        {
            if(FindResident(residentId)==null||liked==disliked||Array.IndexOf(PlayerGiftKinds,liked)<0||Array.IndexOf(PlayerGiftKinds,disliked)<0)return false;
            DailyState.tastes.RemoveAll(t=>t.residentId==residentId);DailyState.tastes.Add(new LifeDailyTaste{residentId=residentId,liked=liked,disliked=disliked});Changed();return true;
        }
        static bool FoodGiftFits(string sourceId,string kind)
        {
            switch(sourceId)
            {
                case"HungryFish":return kind=="Sushi"||kind=="Fish";case"HungryRice":return kind=="Sushi";
                case"HungrySweet":return kind=="Dessert"||kind=="Snack";case"HungryDrink":case"HungryCool":return kind=="Drink";
                case"HungryHot":return kind=="Soup"||kind=="Side";default:return sourceId!=null&&sourceId.StartsWith("Hungry",StringComparison.Ordinal);
            }
        }
        public LifeSimulationResolution GivePlayerGift(string residentId,string giverId,string kind,int priceRaw=0)
        {
            var resident=FindResident(residentId);
            if(resident==null||Array.IndexOf(PlayerGiftKinds,kind)<0||priceRaw<0)return Fail("这个礼物送不了。");
            var taste=GiftTaste(residentId);int reaction=kind==taste.liked?2:kind==taste.disliked?0:1;
            var result=StartResult(resident,"gift");
            if(priceRaw>0){if(State.walletRaw<priceRaw)return Fail("钱不够了。");ChangeWallet(-priceRaw,"gift",result);}
            // Source GoodsSatisfy rows: 30 favourite, 12 normal, 0 disliked.
            Rules.TryGiftSatisfy(reaction==2?3547444722u:reaction==1?1699633274u:195135385u,out int amount);
            if(amount>0)AddHappiness(resident,amount,result,true);
            resident.hunger=Need(resident.hunger+(reaction==0?5:Configuration.eatNeedRecovery/2));
            if(reaction>0)resident.social=Need(resident.social+Configuration.socialNeedRecovery/2);
            // Affinity and gift effects commit together under the installed M3
            // compatibility/level rules, with no second adapter transaction.
            if(!string.IsNullOrEmpty(giverId)&&giverId!=residentId&&FindResident(giverId)!=null)AdjustFamilyBond(giverId,residentId,reaction==2?6:reaction==1?3:-2,"gift",true);
            result.nativeReactionClass=(uint)reaction;string name=PlayerGiftName(kind);
            FinishResult(result,resident,reaction==2?"哇，是"+name+"！这是我最喜欢的！":reaction==1?"谢谢你的"+name+"！":"呃……我不太喜欢"+name+"。");
            Journal("gift",resident.displayName+" 收到了"+name+"。",Reconstruction,residentId,giverId??"");
            if(reaction>0)
            {
                foreach(var request in DailyState.requests.Where(r=>r.status=="Pending"&&r.residentId==residentId&&r.category=="Food"&&FoodGiftFits(r.sourceId,kind)).ToArray())
                    EndDailyRequest(request,true,"吃饱了，谢谢你！");
                RecordDailyAction(residentId,"Goods",kind);
            }
            Changed();return result;
        }
        public void SetFoodPreference(string residentId,string foodName,string tasteColumn,uint reactionClass,string provenance="Player/configured preference")
        {
            var resident=FindResident(residentId)??throw new ArgumentException("Unknown resident.");
            if(Rules.FindFood(foodName)==null)throw new ArgumentException("Unknown source food.");
            var item=resident.foodPreferences.FirstOrDefault(p=>p.foodName==foodName);
            if(item==null){item=new LifeSimulationFoodPreference { foodName=foodName };resident.foodPreferences.Add(item);}
            item.tasteColumn=tasteColumn;item.reactionClass=reactionClass;item.provenance=provenance;Changed();
        }
        public LifeSimulationRelationship Relationship(string fromId,string toId)
        {
            var r=State.relationships.FirstOrDefault(v=>v.fromResidentId==fromId&&v.toResidentId==toId);
            if(r==null)
            {
                r=new LifeSimulationRelationship { fromResidentId=fromId,toResidentId=toId };
                if(Rules.Data.relation!=null){r.meter=Rules.Data.relation.meterInit;r.nativeRank=NativeLifeRulesCalculations.RelationRank(r.meter,Rules.Data.relation);r.nativeMeterApplied=true;r.meterProvenance="Source RelationRoot.MeterInit via original01745a08; rank0039b3a0";}
                State.relationships.Add(r);
            }
            return r;
        }
        public LifeSimulationHousehold CreateHousehold(string id,int capacity,string mapSlotId="")
        {
            LastError=null;
            if(string.IsNullOrWhiteSpace(id)||capacity<1||capacity>Configuration.maximumHouseCapacity){LastError="住宅容量必须在当前配置范围内。";return null;}
            var existing=State.households.FirstOrDefault(h=>h.id==id);
            if(existing!=null)
            {
                if(existing.residentIds.Count>capacity){LastError="新容量小于当前住户数。";return null;}
                existing.capacity=capacity;existing.mapSlotId=mapSlotId ?? "";Changed();return existing;
            }
            var house=new LifeSimulationHousehold { id=id,capacity=capacity,mapSlotId=mapSlotId ?? "" };State.households.Add(house);Changed();return house;
        }
        public bool AssignHouse(string residentId,string houseId)
        {
            LastError=null;
            var resident=FindResident(residentId);var house=State.households.FirstOrDefault(h=>h.id==houseId);
            if(resident==null||house==null){LastError="居民或住宅不存在。";return false;}
            if(house.residentIds.Contains(residentId)){resident.homeId=houseId;return true;}
            if(house.residentIds.Count>=house.capacity){LastError="这栋住宅已经住满了。";return false;}
            foreach(var old in State.households)old.residentIds.Remove(residentId);
            house.residentIds.Add(residentId);resident.homeId=houseId;Journal("housing",resident.displayName+" 搬进了新家。",Reconstruction,residentId);Changed();return true;
        }
        public void UnassignHouse(string residentId)
        {
            foreach(var house in State.households)house.residentIds.Remove(residentId);
            var resident=FindResident(residentId);if(resident!=null)resident.homeId="";Changed();
        }
        public bool UpdateResidentProfile(string residentId,string displayName,string avatarJson,string catchphrase=null,string hobby=null)
        {
            var resident=FindResident(residentId);
            if(resident==null){LastError="居民不存在。";return false;}
            if(!string.IsNullOrWhiteSpace(displayName))resident.displayName=displayName.Trim();
            if(avatarJson!=null)resident.avatarJson=avatarJson;
            if(catchphrase!=null)resident.catchphrase=catchphrase;
            if(hobby!=null)resident.hobby=hobby;
            LastError=null;Changed();return true;
        }
        public void ResolveTrouble(string residentId)
        {
            var resident=FindResident(residentId);if(resident==null)return;
            if(resident.nativeMoodDeadlineRaw!=0)
            {LastError="Unsupported native comfort action/presentation: use a resolved native food/recovery route; the timer was preserved.";Journal("unsupported",LastError,"Native recovery arithmetic is separate from unresolved comfort/cause transitions",residentId);Changed();return;}
            resident.activeTroubleHash=0;resident.troubleResolutionReconstructed=true;
            Journal("trouble",resident.displayName+" 的烦恼已由玩家处理。",Reconstruction+"; generic trouble clearing is explicitly reconstructed, not native mood-timer recovery",residentId);Changed();
        }
        private bool AcquireAndConsume(string category,string name,int price,LifeSimulationResolution result)
        {
            var item=State.inventory.FirstOrDefault(v=>v.category==category&&v.itemName==name);
            if(item!=null&&item.quantity>0){item.quantity--;return true;}
            if(State.walletRaw<price)return false;
            if(price!=0)ChangeWallet(-price,"purchase",result);return true;
        }
        // A native snapshot/clock provider may supply this state. Neither the
        // feeling hash nor reconstructed gameMinutes is inferred to be a timer.
        public void SetResolvedNativeMoodSnapshot(string residentId,long deadlineRaw,long clockRaw)
        {
            var resident=FindResident(residentId);if(resident==null)throw new ArgumentException("Unknown resident.");
            resident.nativeMoodDeadlineRaw=deadlineRaw;State.nativeMoodClockRaw=clockRaw;State.nativeMoodClockResolved=true;
            resident.nativeMoodProvenance="Resolved native Satisfy timer snapshot; unit/cause provided by caller, not inferred from reconstructed gameplay";Changed();
        }
        public void SetResolvedNativeMoodClock(long clockRaw){State.nativeMoodClockRaw=clockRaw;State.nativeMoodClockResolved=true;Changed();}
        private void AddHappiness(LifeSimulationResident resident,int amount,LifeSimulationResolution result,bool createEmotionBalls=false,bool nativeFood=false)
        {
            if(resident.nativeMoodDeadlineRaw!=0)
            {
                if(!State.nativeMoodClockResolved||!nativeFood)
                {result.nativeSettlementUnsupported=true;result.satisfactionProvenance="Unsupported native mood settlement for unresolved clock/item route";return;}
                var recovery=NativeLifeRulesCalculations.PlanNativeMoodGift(resident.nativeMoodDeadlineRaw,State.nativeMoodClockRaw,amount,(int)result.nativeReactionClass,true);
                resident.nativeMoodDeadlineRaw=recovery.deadlineRaw;result.nativeMoodApplied=true;result.nativeMoodRecovered=recovery.recovered;
                result.satisfactionProvenance="Original01d0a340 timer route ->01f9d7d8 food-item7 recovery ->017175a0/01717574; native recovery presentation/feeling transition remains separate";
                result.levelRewardSupport="Native recovery arithmetic supported for explicit raw clock; automatic timer cause/clock conversion and post-recovery presentation unsupported";
                if(createEmotionBalls)QueueEmotionBalls(resident,recovery.effectiveAmount,false,result);
                result.details.Add("原生计时恢复已结算，剩余值 "+recovery.gauge+(recovery.recovered?"；原生后续恢复演出待接入。":"。"));return;
            }
            var plan=NativeLifeRulesCalculations.PlanSatisfaction(resident.level,resident.happinessMeter,amount);
            resident.level=plan.level;resident.happinessMeter=plan.meter;
            result.nativeSatisfactionApplied=true;result.nativeLevelThresholdCrossed=plan.crossedThreshold;
            result.satisfactionProvenance="Original ARM64 0171749c normal-mood satisfaction accumulation, fixed100 carry and zero-based level cap998; routing of reconstructed resident mood remains separate";
            result.levelRewardSupport="Native emotion-ball value/split and explicit collection supported; level-up gift choice/presentation remains unsupported";
            if(createEmotionBalls)QueueEmotionBalls(resident,amount,plan.crossedThreshold,result);
            if(plan.crossedThreshold)result.details.Add(createEmotionBalls?"原生满足度跨越100并结算等级；本次心情球价值按原生标志翻倍。":"原生满足度跨越100并结算等级。");
        }
        private void QueueEmotionBalls(LifeSimulationResident resident,int amount,bool crossed,LifeSimulationResolution result)
        {
            var prices=Rules.Data.emotionBalls;
            if(prices==null){result.emotionBallProvenance="Unsupported: source EmotionBall parameters are absent";return;}
            int value=NativeLifeRulesCalculations.EmotionBallValue(amount,crossed,prices.addMoneyPerSatisfyMeter);
            var split=NativeLifeRulesCalculations.SplitEmotionBalls(value,prices);
            result.emotionBallValueRaw=value;
            result.emotionBallProvenance="Original 01ff1620 -> 01d3b250 source amount*rate and threshold doubling -> 01d3a84c denominations; physical spawn/collection timing is separate";
            if(value<1)return;
            var batch=new LifeSimulationEmotionBallBatch { id=Guid.NewGuid().ToString("N"),residentId=resident.id,satisfactionAmount=amount,valueRaw=value,
                doubledForLevelUp=crossed,mini=split.mini,small=split.small,medium=split.medium,big=split.big,provenance=result.emotionBallProvenance };
            State.emotionBalls.Add(batch);result.emotionBallBatchId=batch.id;
            result.details.Add("待领取心情球价值 "+value+"；领取后才计入钱包。");
        }
        // Each collected source denomination reaches the native wallet core
        // separately, preserving its cap and per-ball statistics semantics.
        public LifeSimulationResolution CollectEmotionBalls(string residentId)
        {
            var resident=FindResident(residentId);var prices=Rules.Data.emotionBalls;
            if(resident==null||prices==null)return Fail("居民或原生心情球参数不存在。");
            var result=StartResult(resident,"emotion-ball-collection");
            foreach(var batch in State.emotionBalls.Where(b=>b.residentId==residentId).ToArray())
            {
                foreach(var pair in new[]{new[]{batch.mini,prices.priceMini},new[]{batch.small,prices.priceSmall},new[]{batch.medium,prices.priceMedium},new[]{batch.big,prices.priceBig}})
                    for(int i=0;i<pair[0];i++)ChangeWallet(pair[1],"native-emotion-ball",result);
                result.emotionBallValueRaw=unchecked(result.emotionBallValueRaw+batch.valueRaw);State.emotionBalls.Remove(batch);
            }
            result.provenance="Original 01d3b2dc -> 01d3b194 source denomination -> 01d6d404 wallet; explicit player/API collection, physical ball presentation not asserted";
            FinishResult(result,resident,"已领取心情球。");Journal("emotion-balls",result.message,result.provenance,residentId);Changed();return result;
        }
        private void ChangeWallet(int amount,string kind,LifeSimulationResolution result)
        {
            var plan=NativeLifeRulesCalculations.PlanWalletDelta(State.walletRaw,amount);State.walletRaw=plan.balance;
            result.details.Add(kind+": "+amount+"，统计类型 "+plan.statisticsTypeRaw+"，统计原值 "+plan.statisticsAmountRawU32);
        }
        private LifeSimulationResolution StartResult(LifeSimulationResident resident,string kind) => new LifeSimulationResolution {
            walletBefore=State.walletRaw,happinessBefore=resident.happinessMeter,levelBefore=resident.level,provenance=kind
        };
        private void FinishResult(LifeSimulationResolution result,LifeSimulationResident resident,string message)
        {
            result.success=true;result.message=message;result.walletAfter=State.walletRaw;result.happinessAfter=resident.happinessMeter;result.levelAfter=resident.level;LastError=null;
        }
        private LifeSimulationResolution Fail(string reason){LastError=reason;return new LifeSimulationResolution { success=false,message=reason,walletBefore=State.walletRaw,walletAfter=State.walletRaw };}
        private void Journal(string kind,string message,string provenance,string focus="",string target="",string sourceEvent="",uint history=0)
        {
            State.journal.Add(new LifeSimulationJournalEntry { id=Guid.NewGuid().ToString("N"),kind=kind,message=message,provenance=provenance,
                focusResidentId=focus,targetResidentId=target,sourceEventName=sourceEvent,sourceHistoryHash=history,gameMinutes=State.gameMinutes });
            while(State.journal.Count>Math.Max(1,Configuration.maximumJournalEntries))State.journal.RemoveAt(0);
        }
        public string ExportJson(bool pretty=true){State.savedUtc=DateTime.UtcNow.ToString("O");return LifeSimulationJson.Write(State,pretty);}
        public bool TryImportJson(string json,out string error)
        {
            error=null;
            try
            {
                var value=LifeSimulationJson.Read<LifeSimulationSave>(json);
                if(value==null||value.schema!=1||value.residents==null||value.relationships==null||value.households==null||value.inventory==null||value.pendingEvents==null||value.journal==null)
                    throw new InvalidDataException("存档格式不完整。");
                if(double.IsNaN(value.gameMinutes)||double.IsInfinity(value.gameMinutes)||value.gameMinutes<0)throw new InvalidDataException("存档时间无效。");
                var ids=new HashSet<string>();
                if(value.emotionBalls==null)value.emotionBalls=new List<LifeSimulationEmotionBallBatch>();
                value.family=value.family??new LifeFamilyState();value.creative=value.creative??new LifeCreativeState();value.activities=value.activities??new LifeActivitiesState();value.daily=value.daily??new LifeDailyState();
                foreach(var r in value.residents)
                {
                    if(r==null||string.IsNullOrEmpty(r.id)||!ids.Add(r.id))throw new InvalidDataException("存档居民标识重复或为空。");
                    if(r.foodPreferences==null)r.foodPreferences=new List<LifeSimulationFoodPreference>();
                    r.hunger=Need(r.hunger);r.social=Need(r.social);r.energy=Need(r.energy);
                }
                if(value.households.Any(h=>h==null||h.residentIds==null||h.capacity<1||h.residentIds.Count>h.capacity||h.residentIds.Any(id=>!ids.Contains(id))))
                    throw new InvalidDataException("住宅住户记录无效。");
                if(value.pendingEvents.Any(e=>!ids.Contains(e.focusResidentId)||!ids.Contains(e.targetResidentId)))throw new InvalidDataException("待处理事件的居民不存在。");
                foreach(var pending in value.pendingEvents)
                {
                    var rule=Rules.FindEvent(pending.eventName);
                    if(rule!=null && (pending.choices==null||pending.branchLabels==null))
                        FillChoices(pending,rule,value.residents.First(r=>r.id==pending.focusResidentId),value.residents.First(r=>r.id==pending.targetResidentId),value.residents.FirstOrDefault(r=>r.id==pending.anotherResidentId));
                }
                State=value;LastError=null;Changed();return true;
            }
            catch(Exception exception){error=LastError=exception.Message;return false;}
        }
        public void SaveToPath(string path)
        {
            string folder=Path.GetDirectoryName(Path.GetFullPath(path));if(!Directory.Exists(folder))Directory.CreateDirectory(folder);
            File.WriteAllText(path,ExportJson(true));
        }
        public bool LoadFromPath(string path,out string error)
        {
            if(!File.Exists(path)){error=LastError="尚无生活存档。";return false;}
            return TryImportJson(File.ReadAllText(path),out error);
        }
        private static float Need(float value)=>float.IsNaN(value)?0:Math.Max(0,Math.Min(100,value));
        private static int PositiveMod(int value,int count)=>(value%count+count)%count;
        private void Changed()=>StateChanged?.Invoke();
    }
}
