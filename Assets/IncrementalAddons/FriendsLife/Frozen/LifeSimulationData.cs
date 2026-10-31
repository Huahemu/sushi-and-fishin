using System;
using System.Collections.Generic;

namespace TomodachiLife.Simulation
{
    [Serializable] public sealed class LifeSimulationConfiguration
    {
        public int schema = 1;
        public string provenance = "Editable reconstructed gameplay policy; native scheduling/eligibility/level settlement not recovered";
        public int initialWalletRaw = 5000, maximumResidents = 100, maximumHouseCapacity = 8;
        public double clockStartMinutes = 540, gameMinutesPerRealSecond = 1, minutesPerDay = 1440;
        public double activityDurationGameMinutes = 15, pairEventIntervalGameMinutes = 30;
        public float initialNeed = 100, hungerDecayPerHour = 5, socialDecayPerHour = 4, energyDecayPerHour = 3;
        public float hungerActivityThreshold = 30, socialActivityThreshold = 35, restActivityThreshold = 25;
        public float eatNeedRecovery = 60, socialNeedRecovery = 35, restNeedRecovery = 70;
        public int sleepStartHour = 22, sleepEndHour = 7;
        public int happinessMeterPerLevel = 100, reconstructedLevelUpMoneyRaw = 0;
        public bool allowLevelDecrease = false, schedulePairEvents = true, allowReconstructedEligibility = true;
        public string defaultFoodTasteColumn = "TasteA";
        public uint defaultFoodReactionClass = 4, defaultGiftTasteHash = 1699633274;
        public int eventHappinessGain = 0, maximumPendingEvents = 1, maximumJournalEntries = 200;
        public string[] scheduledEventNames = { "Event0001FL", "Event0004CH", "Event0022CH", "Event0010CH" };
        public string[] freeActivities = { "散步", "发呆", "看风景", "在家休息" };
    }
    [Serializable] public sealed class LifeSimulationFoodPreference
    {
        public string foodName, tasteColumn;
        public uint reactionClass;
        public string provenance;
    }
    [Serializable] public sealed class LifeSimulationResident
    {
        public string id, displayName, avatarJson, homeId = "", activity = "散步";
        public string catchphrase = "", hobby = "";
        public float hunger = 100, social = 100, energy = 100;
        public int happinessMeter, level, activityCursor;
        public uint feelingHash = 957022509, activeTroubleHash;
        public bool stomachBig, stomachSmall;
        public long nativeMoodDeadlineRaw;
        public string nativeMoodProvenance;
        public bool troubleResolutionReconstructed;
        public double activityUntilGameMinutes;
        public List<LifeSimulationFoodPreference> foodPreferences = new List<LifeSimulationFoodPreference>();
        public int happiness { get=>happinessMeter;set=>happinessMeter=value; }
        public int displayedLevel=>NativeLifeRulesCalculations.DisplaySatisfyLevel(level);
        public string emotion => feelingHash==957022509?"平静":feelingHash==1645779707?"开心":"心情有变化";
    }
    [Serializable] public sealed class LifeSimulationRelationship
    {
        public string fromResidentId, toResidentId, kind = "Known";
        public int meter;
        public int nativeRank;
        public bool nativeMeterApplied;
        public string meterProvenance;
        public bool oneSideLove, fighting;
        public string provenance = "Source reaction deltas/flags, reconstructed directional state application";
        public string fromId { get=>fromResidentId;set=>fromResidentId=value; }
        public string toId { get=>toResidentId;set=>toResidentId=value; }
        public int value { get=>meter;set=>meter=value; }
        public string status { get=>kind;set=>kind=value; }
    }
    [Serializable] public sealed class LifeSimulationHousehold
    {
        public string id, mapSlotId;
        public int capacity;
        public List<string> residentIds = new List<string>();
    }
    [Serializable] public sealed class LifeSimulationInventoryStack
    {
        public string category, itemName;
        public int quantity;
    }
    [Serializable] public sealed class LifeSimulationEmotionBallBatch
    {
        public string id,residentId,provenance;
        public int satisfactionAmount,valueRaw,mini,small,medium,big;
        public bool doubledForLevelUp;
    }
    [Serializable] public sealed class LifeSimulationPendingEvent
    {
        public string id, eventName, focusResidentId, targetResidentId, anotherResidentId;
        public string title;
        public string[] branchLabels;
        public List<LifeSimulationEventChoice> choices = new List<LifeSimulationEventChoice>();
        public double createdGameMinutes;
        public bool usesReconstructedEligibility,nativeBranchSelectionApplied,manualBranchOverride;
        public int nativeSelectedBranch=-1;
        public uint branchSeedTick;
        public NativeMiiEventRandomState branchRandom;
        public string nativeEligibilitySupport,nativeOccurrenceSupport,nativeBranchSupport;
        public string provenance = "Source event/branches; reconstructed occurrence and participant eligibility";
        public string focusId { get=>focusResidentId;set=>focusResidentId=value; }
        public string targetId { get=>targetResidentId;set=>targetResidentId=value; }
    }
    [Serializable] public sealed class LifeSimulationEventChoice
    {
        public int branchIndex,resultIndex;
        public string label,disabledReason;
        public bool enabled;
    }
    [Serializable] public sealed class LifeSimulationJournalEntry
    {
        public string id, kind, message, provenance, focusResidentId, targetResidentId, sourceEventName;
        public double gameMinutes;
        public uint sourceHistoryHash;
        public override string ToString()=>message;
    }
    [Serializable] public sealed class LifeSimulationSave
    {
        public LifeFamilyState family = new LifeFamilyState();
        public LifeCreativeState creative = new LifeCreativeState();
        public LifeActivitiesState activities = new LifeActivitiesState();
        public LifeDailyState daily = new LifeDailyState();
        public int schema = 1, walletRaw, eventCursor, pairCursor;
        public string savedUtc, rulesVersion = "base-1.0.0-life-runtime-1";
        public NativeMiiEventRandomState eventRandom=new NativeMiiEventRandomState(123456789,362436069,521288629,88675123);
        public uint eventSeedTick;
        public string eventRandomProvenance="Reconstructed fixed nonzero startup seed; native main01d03800 transition/rate algorithm; native startup entropy source unresolved";
        public bool nativeMoodClockResolved;
        public long nativeMoodClockRaw;
        public double gameMinutes, nextPairEventGameMinutes;
        public List<LifeSimulationResident> residents = new List<LifeSimulationResident>();
        public List<LifeSimulationRelationship> relationships = new List<LifeSimulationRelationship>();
        public List<LifeSimulationHousehold> households = new List<LifeSimulationHousehold>();
        public List<LifeSimulationInventoryStack> inventory = new List<LifeSimulationInventoryStack>();
        public List<LifeSimulationEmotionBallBatch> emotionBalls = new List<LifeSimulationEmotionBallBatch>();
        public List<LifeSimulationPendingEvent> pendingEvents = new List<LifeSimulationPendingEvent>();
        public List<LifeSimulationJournalEntry> journal = new List<LifeSimulationJournalEntry>();
    }
    [Serializable] public sealed class LifeSimulationResolution
    {
        public bool success;
        public bool nativeSatisfactionApplied,nativeLevelThresholdCrossed;
        public string satisfactionProvenance,levelRewardSupport;
        public int emotionBallValueRaw;
        public bool nativeMoodApplied,nativeMoodRecovered,nativeSettlementUnsupported;
        public bool nativeEventConditionApplied,nativeEventBranchApplied,usesReconstructedEventInputs;
        public string nativeEventSupport;
        public string emotionBallBatchId,emotionBallProvenance;
        public string message, provenance;
        public int walletBefore, walletAfter, happinessBefore, happinessAfter, levelBefore, levelAfter, sourceRewardRaw;
        public uint nativeReactionClass, modifierFlag;
        public List<string> details = new List<string>();
    }
}
