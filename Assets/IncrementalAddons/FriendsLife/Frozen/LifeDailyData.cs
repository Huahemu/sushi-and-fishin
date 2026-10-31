using System;
using System.Collections.Generic;
#if !LIFE_SIMULATION_STANDALONE
using UnityEngine;
#endif

namespace TomodachiLife.Simulation
{
    [Serializable] public sealed class LifeDailyState
    {
        public string islandName="我的海岛";
        public int revision, requestCursor, completedRequests, missedRequests;
        public double nextRequestMinutes, offlineMinutes;
        public bool realTimeClock, offlineLife = true;
        public bool musicEnabled=true,speechEnabled=true;
        public List<LifeDailyRequest> requests = new List<LifeDailyRequest>();
        public List<LifeDailyCooldown> cooldowns = new List<LifeDailyCooldown>();
        public List<LifeDailyMood> moods = new List<LifeDailyMood>();
        public List<LifeDailyAction> actions = new List<LifeDailyAction>();
        // Player residents never get problems; they are the ones who help.
        public List<string> playerResidents = new List<string>();
        // One favourite and one disliked in-person gift per resident.
        public List<LifeDailyTaste> tastes = new List<LifeDailyTaste>();
    }
    [Serializable] public sealed class LifeDailyTaste { public string residentId, liked, disliked; }
    [Serializable] public sealed class LifeDailyRequest
    {
        public string id, sourceId, residentId, targetId, category, title, description, status="Pending", result="";
        public double createdMinutes, deadlineMinutes;
    }
    [Serializable] public sealed class LifeDailyCooldown { public string residentId, sourceId; public double untilMinutes; }
    [Serializable] public sealed class LifeDailyMood { public string residentId, kind; public double untilMinutes; }
    [Serializable] public sealed class LifeDailyAction { public string residentId, category, detail; public double minutes; }
    [Serializable] public sealed class LifeDailySource
    {
        public int schema;
        public string provenance;
        public LifeDailyTroubleRule[] troubles=Array.Empty<LifeDailyTroubleRule>();
        public LifeEasyQuestion[] questions=Array.Empty<LifeEasyQuestion>();
        public LifeEasyAnswer[] answers=Array.Empty<LifeEasyAnswer>();
        public LifeDailyFood[] foods=Array.Empty<LifeDailyFood>();
        public static LifeDailySource Load()
        {
#if LIFE_SIMULATION_STANDALONE
            return new LifeDailySource();
#else
            var asset=Resources.Load<TextAsset>("Gameplay/daily-source");
            if(asset==null)throw new InvalidOperationException("Daily gameplay source is missing.");
            return JsonUtility.FromJson<LifeDailySource>(asset.text);
#endif
        }
    }
    [Serializable] public sealed class LifeDailyTroubleRule { public string id,category,source; public int rate,duration,cooldown; public string[] choices; }
    [Serializable] public sealed class LifeEasyQuestion { public string id; public uint type,nextDefault; public uint[] nextQuestions;public string[] answers; }
    [Serializable] public sealed class LifeEasyAnswer { public string id; public LifeEasyCandidate[] candidates; }
    [Serializable] public sealed class LifeEasyCandidate { public string part; public int param; public uint type,partType; }
    [Serializable] public sealed class LifeDailyFood {public string name;public string[] tags;}
}
