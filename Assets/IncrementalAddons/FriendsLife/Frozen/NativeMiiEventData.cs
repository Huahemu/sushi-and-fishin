using System;
using System.Linq;
using System.Collections.Generic;
namespace TomodachiLife.Simulation
{
    [Serializable] public sealed class NativeMiiEventCondition
    {
        public uint id; public string name; public int sourceRowIndex;
        public int AllMiiNumOrMore;
        public uint AnotherFeelingConditionId;
        public bool AnotherHasNoTrouble;
        public bool AnotherIsNotAccident;
        public bool AnotherIsNotSleeping;
        public bool AnotherIsWalking;
        public int AnotherMiiMaxNum;
        public int AnotherMiiMinNum;
        public bool FcsToAnoCanFight;
        public bool FcsToAnoCanLove;
        public bool FcsToAnoIsAwareOf;
        public bool FcsToAnoIsNotInSamePlace;
        public uint FcsToAnoRelationConditionId;
        public bool FcsToTgtCanFight;
        public bool FcsToTgtCanLove;
        public bool FcsToTgtGroupTogether;
        public bool FcsToTgtIsAwareOf;
        public bool FcsToTgtIsGroupWalking2;
        public bool FcsToTgtIsInPrivateRoom;
        public bool FcsToTgtIsInSameHouse;
        public bool FcsToTgtIsRoommate;
        public uint FcsToTgtRelationConditionId;
        public uint FocusFeelingConditionId;
        public uint FocusGoodsRandomRulePreset;
        public uint FocusGoodsRandomRuleUgc;
        public uint FocusHasGoodsId;
        public bool FocusHasGoodsRandomRule;
        public bool FocusHasNoTrouble;
        public int FocusHousemateNumOrMore;
        public NativeMiiEventFloat2 FocusInWorldBBArea0;
        public NativeMiiEventFloat2 FocusInWorldBBArea1;
        public bool FocusIsAIMiiEventFlag;
        public uint FocusIsAIMiiEventType;
        public bool FocusIsGroup;
        public bool FocusIsInWorldBBArea;
        public bool FocusIsNoneInSamePlace;
        public bool FocusIsNotAIMiiEventFlag;
        public bool FocusIsNotSleeping;
        public bool FocusIsWalking;
        public bool FocusNotHasFight;
        public uint FocusOneSideLove;
        public uint FocusPlaceConditionId;
        public bool ForegroundMapHouseToiletEmpty;
        public uint GameTimeConditionId;
        public uint GoldSeriesUnlockPhaseCondition;
        public bool IsCheckStatisticsCount;
        public bool IsNeedAnotherMii;
        public bool IsNeedTargetMii;
        public bool IsUseAllowableTargetMiiNum;
        public int StatisticsCountOrLess;
        public int StatisticsCountOrMore;
        public uint StatisticsCountType;
        public uint TargetFeelingConditionId;
        public int TargetGroupNum;
        public uint TargetHasGoodsId;
        public bool TargetHasNoTrouble;
        public bool TargetIsAIMiiEventFlag;
        public uint TargetIsAIMiiEventType;
        public bool TargetIsGroup;
        public bool TargetIsNotAccident;
        public bool TargetIsNotGroup;
        public bool TargetIsNotSleeping;
        public bool TargetIsStayNone;
        public bool TargetIsWalking;
        public int TargetMiiMaxNum;
        public int TargetMiiMinNum;
        public bool TargetNotHasFight;
        public bool TgtToAnoCanLove;
        public bool TgtToAnoCanLoveGenderAgeBlood;
        public bool TgtToAnoGroupTogether;
        public uint TgtToAnoRelationConditionId;
        public string __RowId;
    }
    [Serializable] public sealed class NativeMiiEventFeeling
    {
        public uint id; public string name; public int sourceRowIndex;
        public bool IsExecutableAngry;
        public bool IsExecutableCareless;
        public bool IsExecutableDeject;
        public bool IsExecutableDepress;
        public bool IsExecutableGood;
        public bool IsExecutableIrritate;
        public bool IsExecutableNormal;
        public bool IsExecutableWorry;
        public string __RowId;
    }
    [Serializable] public sealed class NativeMiiEventPlace
    {
        public uint id; public string name; public int sourceRowIndex;
        public bool IsFacilityAll;
        public bool IsHouseLDK;
        public bool IsHouseLDKMine;
        public bool IsHousePrivateRoom;
        public bool IsHousePrivateRoomMine;
        public bool IsHouseToilet;
        public bool IsHouseToiletMine;
        public bool IsOnIsland;
        public uint[] MapObjectAIAffordanceHash;
        public string __RowId;
    }
    [Serializable] public sealed class NativeMiiEventTime
    {
        public uint id; public string name; public int sourceRowIndex;
        public uint ConditionInAfternoon;
        public uint ConditionInEarlyMorning;
        public uint ConditionInEvening;
        public uint ConditionInMidnight;
        public uint ConditionInMorning;
        public uint ConditionInNight;
        public string __RowId;
    }
    [Serializable] public sealed class NativeMiiEventRelation
    {
        public uint id; public string name; public int sourceRowIndex;
        public bool IsFight;
        public bool IsFightNot;
        public bool IsOnesideLove;
        public bool IsOnesideLoveNot;
        public bool IsRefarence;
        public bool IsTypeBrotherSisterOlder;
        public bool IsTypeBrotherSisterYounger;
        public bool IsTypeChild;
        public bool IsTypeCouple;
        public bool IsTypeDivorce;
        public bool IsTypeExFriend;
        public bool IsTypeExLover;
        public bool IsTypeFriend;
        public bool IsTypeGrandChild;
        public bool IsTypeGrandParent;
        public bool IsTypeKnow;
        public bool IsTypeLover;
        public bool IsTypeOther;
        public bool IsTypeParent;
        public bool IsTypeRelative;
        public uint MeterOrHigherId;
        public string MeterOrLower;
        public uint MeterOrLowerId;
        public uint[] RefarenceArrayId;
        public bool ReverseIsOnesideLove;
        public bool ReverseIsOnesideLoveNot;
        public uint ReverseMeterOrHigherId;
        public string ReverseMeterOrLower;
        public uint ReverseMeterOrLowerId;
        public string __RowId;
    }
    [Serializable] public sealed class NativeMiiEventMeter
    {
        public uint id; public string name; public int sourceRowIndex;
        public int Meter;
        public string __RowId;
    }
    [Serializable] public sealed class NativeMiiEvent
    {
        public uint id; public string name; public int sourceRowIndex;
        public NativeMiiEventBranch[] BranchParam;
        public uint ConditionId;
        public uint DefaultMiiHistoryDataId;
        public uint DemoParamId;
        public int FirstGenerateRate;
        public int GenerateRate;
        public NativeMiiEventBoost[] GenerateRateOnRelationBoost;
        public uint GenerateType;
        public NativeMiiEventSeconds GeneratedCoolingTimeSpan;
        public uint GroupId;
        public bool IsChangeOtherToKnow;
        public bool IsChangeOtherToKnowAnother;
        public bool IsEnable;
        public bool IsFindDemoStageByMii1;
        public bool IsIncludeAnotherInResult;
        public bool IsIncludeTargetInAll;
        public bool IsIncludeTargetInResult;
        public bool IsReplaceCheckTargetGroupMember;
        public bool IsRevertClothExceptMii0;
        public bool IsShowTelop;
        public bool IsUseAfSourceToilet;
        public bool IsUseFirstGenerateRate;
        public bool IsUseMii0BelongItemInfo;
        public bool IsUseMiiEventItemInfo;
        public bool IsUseParentActorWhenSubActor;
        public bool IsUseTargetForDemoStage;
        public uint MapObjectMiiEventTargetType;
        public uint MiiHistoryDataId;
        public uint NoGroupId;
        public uint NoSittingSubActorSetId;
        public uint RandomUseDemoStageType;
        public string __RowId;
    }
    [Serializable] public sealed class NativeMiiEventBranch
    {
        public uint ConditionId;
        public string ForkStr;
        public bool IsChangeOnesideLove01;
        public bool IsConditionOtherReselect;
        public int Rate;
        public NativeMiiEventResult[] ResultParam;
    }
    [Serializable] public sealed class NativeMiiEventResult
    {
        public uint AdditionalAction0Id;
        public uint AdditionalAction1Id;
        public uint AdditionalRewardType0;
        public uint AdditionalRewardType1;
        public uint AdditionalRewardType2;
        public string DemoResult;
        public uint Feeling0;
        public uint Feeling1;
        public bool IsBecomeFriend01;
        public bool IsBecomeOnesideLove01;
        public bool IsBecomeOnesideLove02;
        public bool IsFightNormal01;
        public bool IsFightRivalConfession021;
        public bool IsLostOnesideLove01;
        public bool IsSkipResult;
        public uint MiiHistoryDataId;
        public uint RelationMeter01Id;
        public int RelationMeter02Id;
        public uint RelationMeter10Id;
        public int RelationMeter20Id;
        public int Trouble01Id;
    }
    [Serializable] public sealed class NativeMiiEventFloat2 { public float X,Y; }
    [Serializable] public sealed class NativeMiiEventSeconds { public int AllSecond; }
    [Serializable] public sealed class NativeMiiEventBoost { public uint hash; public int rate; }
    [Serializable] public sealed class NativeMiiEventTables
    {
        public int schema; public string source; public bool originalFullGameplayParity;
        public NativeMiiEventCondition[] conditions;
        public NativeMiiEventFeeling[] feelings;
        public NativeMiiEventPlace[] places;
        public NativeMiiEventTime[] times;
        public NativeMiiEventRelation[] relations;
        public NativeMiiEventMeter[] meters;
        public NativeMiiEvent[] events;
        public uint[] goldPhaseHashes,oneSideLoveHashes,aiEventTypeHashes,generateTypeHashes,feelingHashes;
    }
    public sealed class NativeMiiEventDatabase
    {
        public NativeMiiEventTables Data { get; private set; }
        public NativeMiiEventDatabase(NativeMiiEventTables data){Data=data??throw new ArgumentNullException(nameof(data));}
        public static NativeMiiEventDatabase FromJson(string json)=>new NativeMiiEventDatabase(LifeSimulationJson.Read<NativeMiiEventTables>(json));
        public static NativeMiiEventDatabase Load()
        {
#if LIFE_SIMULATION_STANDALONE
            throw new InvalidOperationException("Standalone caller must supply the source JSON.");
#else
            var asset=UnityEngine.Resources.Load<UnityEngine.TextAsset>("NativeEvent/native-event-tables");
            if(asset==null)throw new InvalidOperationException("NativeEvent/native-event-tables is missing.");return FromJson(asset.text);
#endif
        }
    }
}
