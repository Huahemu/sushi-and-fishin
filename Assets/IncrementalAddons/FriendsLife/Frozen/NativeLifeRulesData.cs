using System;

namespace TomodachiLife.Simulation
{
    [Serializable] public sealed class NativeLifeRulesData
    {
        public int schema = 1;
        public string sourceVersion, provenance;
        public NativeLifeEventRule[] events;
        public NativeLifeConditionRule[] conditions;
        public NativeLifeFoodRule[] foods;
        public NativeLifeGoodsRule[] goods;
        public NativeLifeGiftTasteRule[] giftTastes;
        public NativeLifeEmotionBallRules emotionBalls;
        public NativeLifeRelationRules relation;
        public int foodPriceDiv, foodSatisfyMax;
        public float foodHabitStomachBigFactor, foodHabitStomachSmallFactor;
    }
    [Serializable] public sealed class NativeLifeEmotionBallRules
    {
        public int addMoneyPerSatisfyMeter,priceMini,priceSmall,priceMedium,priceBig;
        public string provenance;
    }
    [Serializable] public sealed class NativeLifeRelationRules
    {
        public int meterInit,meterMax;
        public int[] rankMeterMax;
        public string provenance;
    }
    [Serializable] public sealed class NativeLifeEventRule
    {
        public string name, rowId, simulationLabel;
        public bool enabled;
        public uint conditionId;
        public int generateRateRaw, firstGenerateRateRaw;
        public NativeLifeBranchRule[] branches;
    }
    [Serializable] public sealed class NativeLifeBranchRule
    {
        public int index, rateRaw;
        public string forkStr, simulationLabel;
        public uint conditionId;
        public bool changeOnesideLove01;
        public NativeLifeResultRule[] results;
    }
    [Serializable] public sealed class NativeLifeResultRule
    {
        public int index, rewardMoney0, rewardMoney1, rewardMoney2;
        public uint rewardHash0, rewardHash1, rewardHash2, feeling0, feeling1, historyId, trouble01Id, action0Id, action1Id;
        public string demoResult;
        public bool skipResult, becomeFriend01, becomeOnesideLove01, becomeOnesideLove02, lostOnesideLove01, fightNormal01, fightRivalConfession021;
        public NativeLifeRelationEffect[] relations;
    }
    [Serializable] public sealed class NativeLifeRelationEffect
    {
        public string field, reactionName;
        public int fromIndex, toIndex, delta;
        public uint reactionId;
        public bool noChangeRank;
    }
    [Serializable] public sealed class NativeLifeConditionRule
    {
        public string name, rowId, rawJson;
        public uint id;
        public bool needTargetMii, needAnotherMii, focusNotSleeping, targetNotSleeping, focusHasNoTrouble, targetHasNoTrouble;
        public bool focusNotHasFight, targetNotHasFight;
        public int targetMin, targetMax;
    }
    [Serializable] public sealed class NativeLifeFoodRule
    {
        public string name, rowId;
        public int priceRaw;
        public uint quantityHash;
        public float quantityFactor;
        public NativeLifeFoodTaste[] tastes;
    }
    [Serializable] public sealed class NativeLifeFoodTaste
    {
        public string column;
        public uint hash;
        public int baseSatisfy, rankValue;
        public bool hasSatisfyValue;
    }
    [Serializable] public sealed class NativeLifeGoodsRule
    {
        public string name, rowId, iconName;
        public int buyingPriceRaw, sellingPriceRaw;
        public uint typeHash;
        public bool sourceUnlocked;
    }
    [Serializable] public sealed class NativeLifeGiftTasteRule { public uint hash; public int satisfy; }
}
