using System;
using System.Collections.Generic;

namespace TomodachiLife.Simulation
{
    [Serializable] public sealed class LifeActivitiesResult
    {
        public bool success,won;
        public string message,sessionId;
        public int rewardRaw;
    }
    [Serializable] public sealed class LifeActivitiesState
    {
        public int schema=1,revision,lastDay=-1,marketDay=-1,marketPeriod=-1,donationClaimDay=-1;
        public uint randomState=0x6d2b79f5;
        public int donationTotal,fountainPoints,minigamesWon,travelsCompleted,dreamsCompleted;
        public double nextAccidentAt=660;
        public LifeMinigameSession minigame;
        public List<LifeTravelSession> travels=new List<LifeTravelSession>();
        public List<LifeDreamSession> dreams=new List<LifeDreamSession>();
        public List<LifePhotoRecord> photos=new List<LifePhotoRecord>();
        public List<LifeActivityRecord> history=new List<LifeActivityRecord>();
        public List<LifeNewsBulletin> news=new List<LifeNewsBulletin>();
        public List<LifeAccident> accidents=new List<LifeAccident>();
        public List<LifeFacilityState> facilities=new List<LifeFacilityState>();
        public List<LifeMarketOffer> market=new List<LifeMarketOffer>();
        public List<string> unlockedPhotos=new List<string>(),dreamsSeen=new List<string>(),wishes=new List<string>();
    }
    [Serializable] public sealed class LifeMinigameSession
    {
        public string id,residentId,definitionId,prompt,message;
        public List<string> participantIds=new List<string>(),options=new List<string>(),usedFoods=new List<string>(),inputs=new List<string>();
        public List<int> board=new List<int>(),visitedViews=new List<int>();
        public int round,score,lives=3,answer,target,shots,pins=10,phase,swaps;
        public float elapsed,roundElapsed,distance;
        public bool completed,won,rewardClaimed;
        public int rewardRaw;
    }
    [Serializable] public sealed class LifeTravelSession
    {
        public string id,residentId,partnerId,destinationId,message;
        public int stage,exploration;
        public double readyAt;
        public bool awaitingChoice,completed,returned;
        public List<string> choices=new List<string>();
    }
    [Serializable] public sealed class LifeDreamSession
    {
        public string id,residentId,dreamId,message;
        public int stage,lucidity;
        public double readyAt;
        public bool awaitingChoice,completed,woken;
        public List<string> choices=new List<string>();
    }
    [Serializable] public sealed class LifePhotoRecord
    {
        public string id,residentId,partnerId,presetId,label,path;
        public double gameMinutes;
        public int pose,angle;
    }
    [Serializable] public sealed class LifeActivityRecord
    {
        public string id,category,definitionId,residentId,title,result;
        public bool won;
        public int rewardRaw;
        public double gameMinutes;
    }
    [Serializable] public sealed class LifeNewsBulletin
    {
        public string id,title,body,residentId;
        public double gameMinutes;
        public bool watched;
    }
    [Serializable] public sealed class LifeAccident
    {
        public string id,residentId,kind,message;
        public double expiresAt;
        public bool resolved;
        public string resolution;
    }
    [Serializable] public sealed class LifeFacilityState
    {public string id;public bool unlocked;public int visits;}
    [Serializable] public sealed class LifeMarketOffer
    {public string id,category,itemName;public int unitPrice,remaining,originalPrice;}
    public sealed class LifeMinigameDefinition
    {public string id,title,explanation,sourceRow;public int minimumResidents,maximumParticipants,requiredPopulation;public bool group;}
    public sealed class LifeTravelDefinition
    {public string id,title,sourceRow;public bool canWedding;public string[] foodRewards,goodsRewards,coordinateRewards;}
    public sealed class LifeDreamDefinition
    {public string id,title,sourceRow,rewardCategory,rewardItem;}
    public sealed class LifePhotoDefinition
    {public string id,title,sourceRow,background;public uint sourceUnlockId;public int index;}
    public sealed class LifeFacilityDefinition
    {public string id,title,description;public int cost,requiredResidents,requiredDonations;}
}
