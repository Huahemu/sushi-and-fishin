using System;
using System.Collections.Generic;

namespace TomodachiLife.Simulation
{
    [Serializable] public sealed class LifeFamilyState
    {
        public int schema=1,revision,birthSequence,suggestionCursor;
        public double nextSuggestionGameMinutes;
        public string provenance="Reconstructed playable family rules, choices and game-clock durations; native family probability and timing are not asserted";
        public LifeFamilyConfiguration configuration=new LifeFamilyConfiguration();
        public List<LifeFamilyProfile> profiles=new List<LifeFamilyProfile>();
        public List<LifeFamilyCouple> couples=new List<LifeFamilyCouple>();
        public List<LifeFamilyPregnancy> pregnancies=new List<LifeFamilyPregnancy>();
        public List<LifeFamilyChild> children=new List<LifeFamilyChild>();
        public List<LifeFamilyChoice> choices=new List<LifeFamilyChoice>();
        public List<LifeFamilyHousingSuggestion> housingSuggestions=new List<LifeFamilyHousingSuggestion>();
        public List<LifeFamilyMemory> memories=new List<LifeFamilyMemory>();
    }
    [Serializable] public sealed class LifeFamilyConfiguration
    {
        public int adultAge=18,friendThreshold=40,bestFriendThreshold=100,confessionThreshold=60;
        public int proposalLove=75,minimumDatesForProposal=2,chatGain=25,dateLoveGain=18;
        public double pregnancyGameMinutes=360,adoptionGameMinutes=180,babyGrowthGameMinutes=360,laterGrowthGameMinutes=720,careCooldownGameMinutes=20;
        public int careActionsPerGrowth=3,maximumChildrenPerCouple=8;
        public bool autonomousSuggestions=true;
        public double suggestionIntervalGameMinutes=180;
    }
    [Serializable] public sealed class LifeFamilyProfile
    {
        public string residentId,gender="Other",romancePreference="Anyone",parentAId="",parentBId="";
        public int ageYears=20;
        public bool adopted;
        public double sadUntilGameMinutes;
    }
    [Serializable] public sealed class LifeFamilyCouple
    {
        public string id,parentAId,parentBId,status="Dating",weddingStyle="",honeymoonTripId="";
        public int love=50,dates,breakups,reunions;
        public double startedGameMinutes,marriedGameMinutes,lastDateGameMinutes=-100000;
        public bool honeymoonSuggestion;
    }
    [Serializable] public sealed class LifeFamilyPregnancy
    {
        public string id,coupleId,parentAId,parentBId,carrierId,mode="Pregnancy",status="Expecting",childId;
        public double startedGameMinutes,dueGameMinutes;
    }
    [Serializable] public sealed class LifeFamilyChild
    {
        public string residentId,parentAId,parentBId,stage="Baby",pregnancyId;
        public int ageYears,careActions,growthCareBaseline,appearanceRevision;
        public uint birthSeed;
        public double bornGameMinutes,nextGrowthGameMinutes,lastCareGameMinutes=-100000;
        public bool adopted,readyToGrow;
    }
    [Serializable] public sealed class LifeFamilyChoice
    {
        public string id,action,focusId,targetId,title,relatedId;
        public double createdGameMinutes;
        public List<LifeFamilyOption> options=new List<LifeFamilyOption>();
    }
    [Serializable] public sealed class LifeFamilyOption {public string id,label,reason;public bool enabled=true;}
    [Serializable] public sealed class LifeFamilyHousingSuggestion
    {
        public string id,coupleId,houseId="",status="Pending",message="";
        public string[] residentIds=Array.Empty<string>();
    }
    [Serializable] public sealed class LifeFamilyMemory {public string id,kind,message,focusId,targetId;public double gameMinutes;}
}
