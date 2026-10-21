using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Random=System.Random;

namespace SushiAndFish.CityExpansion
{
    [DefaultExecutionOrder(-40)]
    public sealed class TownMealScheduler : MonoBehaviour
    {
        public const double EventProbability=.45;
        public static TownMealScheduler Instance {get;private set;}
        static string pendingLedger;
        [Serializable] public class Ledger {public int day=-1,attemptedWindows;public List<byte> visitors=new List<byte>();}
        public CityDiningSeat[] seats;
        public RestaurantManager sushiRestaurant;
        Ledger ledger=new Ledger();
        float nextTick;
        readonly List<ResidentController> visits=new List<ResidentController>();
        public int StartedParties {get;private set;}
        public int StartedVisitors {get;private set;}
        public int SkippedForSushi {get;private set;}
        public int EligiblePopulation {get;private set;}
        public int AttemptedWindows=>ledger.attemptedWindows;
        CozyWeatherManager Clock=>Singleton<GameProgressManager>.Instance?.CozyWeatherManager;
        public int CurrentDay=>Clock?Clock.CurrentDay:ledger.day;
        public int Minute=>Clock?Clock.CurrentTime.hours*60+Clock.CurrentTime.minutes:0;
        public bool SushiOpen=>sushiRestaurant&&sushiRestaurant.IsRestaurantOpen;
        public IReadOnlyList<ResidentController> ActiveVisits=>visits;
        void Awake(){Instance=this;if(pendingLedger!=null){ApplyLedger(pendingLedger);pendingLedger=null;}}
        void OnDestroy(){if(Instance==this)Instance=null;}
        public static string CaptureLedger()=>Instance?JsonUtility.ToJson(Instance.ledger):"";
        public static void RestoreLedger(string json)
        {
            if(Instance)Instance.ApplyLedger(json);else pendingLedger=json??"";
        }
        void ApplyLedger(string json)
        {
            ledger=string.IsNullOrWhiteSpace(json)?new Ledger():JsonUtility.FromJson<Ledger>(json)??new Ledger();
            if(ledger.visitors==null)ledger.visitors=new List<byte>();
        }
        void Update()
        {
            if(!NetworkManager.Singleton||!NetworkManager.Singleton.IsServer||!Clock||!Singleton<ResidentManager>.Instance)return;
            if(Time.unscaledTime<nextTick)return;
            nextTick=Time.unscaledTime+.25f;
            ObserveTime(CurrentDay,Minute);
        }
        public static int PlannedMinute(int day,int window)
        {
            var random=new Random(unchecked(day*73856093+window*19349663+1021));
            return (window==0?690:960)+random.Next(0,36);
        }
        public static bool PlannedEvent(int day,int window,out int partySize,out int stayMinutes)
        {
            var random=new Random(unchecked(day*83492791+window*297121507+20261021));
            bool occurs=random.NextDouble()<EventProbability;
            double group=random.NextDouble();partySize=group<.20?1:group<.75?2:3;
            stayMinutes=20+random.Next(16);
            return occurs;
        }
        public void ObserveTime(int day,int minute)
        {
            if(ledger.day!=day){CancelAllVisits();ledger=new Ledger{day=day};}
            visits.RemoveAll(r=>!r||!(r.Brain?.StateMachine.Current is ResidentTownMealState));
            if(SushiOpen)CancelAllVisits();
            for(int window=0;window<2;window++)
            {
                int bit=1<<window;if((ledger.attemptedWindows&bit)!=0||minute<PlannedMinute(day,window))continue;
                // One trial only, including when the player skips time, saves,
                // reloads, or keeps the restaurant occupied for the window.
                ledger.attemptedWindows|=bit;
                int end=window==0?810:1050;
                if(minute>=end)continue;
                if(SushiOpen){SkippedForSushi++;continue;}
                if(!PlannedEvent(day,window,out int size,out int stay))continue;
                var candidates=Singleton<ResidentManager>.Instance.Residents.Where(IsEligible).OrderBy(r=>r.ResidentId).ToList();
                EligiblePopulation=candidates.Count;
                if(candidates.Count==0)continue;
                size=Math.Min(size,candidates.Count);
                var available=ChooseSeats(size);
                if(available.Length!=size)continue;
                var random=new Random(unchecked(day*486187739+window*16777619));
                for(int i=candidates.Count-1;i>0;i--){int j=random.Next(i+1);var x=candidates[i];candidates[i]=candidates[j];candidates[j]=x;}
                TryStartParty(candidates.Take(size).ToArray(),available,stay);
            }
        }
        public bool IsEligible(ResidentController resident)
        {
            if(!resident||!resident.IsServer||!resident.IsSpawned||resident.Brain==null||!resident.Brain.CanAcceptRestaurantAssignment()||ledger.visitors.Contains(resident.ResidentId))return false;
            var questLinks=resident.GetComponent<ResidentQuestLinks>();
            if(questLinks&&questLinks.RelatedQuestIds!=null&&QuestManager.Instance&&questLinks.RelatedQuestIds.Any(id=>QuestManager.Instance.IsQuestOrGroupActive(id)))return false;
            var activity=resident.Runtime.CurrentActivity;
            if(activity!=ResidentActivityType.Idle&&activity!=ResidentActivityType.CityPatrolNear&&activity!=ResidentActivityType.ParkPatrol&&activity!=ResidentActivityType.ParkBench&&activity!=ResidentActivityType.SeasideBench)return false;
            // A meal cannot displace an imminent work shift.
            var entries=resident.ResidentData?.Schedule?.Entries;
            if(entries!=null)foreach(var entry in entries)if(entry.Activity==ResidentActivityType.Work)
            {
                int until=(entry.StartTime*60+entry.StartMinute-Minute+1440)%1440;
                if(until<90)return false;
            }
            return seats!=null&&seats.Length>0&&Vector3.Distance(resident.transform.position,seats[0].transform.position)<180;
        }
        CityDiningSeat[] ChooseSeats(int size)
        {
            var free=seats.Where(s=>s&&s.IsFree).OrderBy(s=>s.seatId).ToArray();
            if(size==3)
            {
                var bar=seats.Where(s=>s&&s.barStool).OrderBy(s=>s.seatId).ToArray();
                for(int i=0;i+2<bar.Length;i++){var group=bar.Skip(i).Take(3).ToArray();if(group.All(s=>s.IsFree))return group;}
                return Array.Empty<CityDiningSeat>();
            }
            if(size==2)
                foreach(var group in free.Where(s=>!s.barStool).GroupBy(s=>s.tableId))if(group.Count()>=2)return group.Take(2).ToArray();
            return free.Take(size).ToArray();
        }
        public bool TryStartParty(ResidentController[] party,CityDiningSeat[] chosen,int stayMinutes)
        {
            if(SushiOpen||party==null||chosen==null||party.Length!=chosen.Length||party.Length<1||party.Length>3||party.Distinct().Count()!=party.Length||chosen.Distinct().Count()!=chosen.Length)return false;
            for(int i=0;i<party.Length;i++)
            {
                if(!IsEligible(party[i])||!chosen[i].IsFree)return false;
                var agent=party[i].GetComponent<NavMeshAgent>();var path=new NavMeshPath();
                if(!agent||!agent.enabled||!agent.isOnNavMesh||!agent.CalculatePath(chosen[i].approach.position,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
            }
            var reserved=new List<(ResidentController,CityDiningSeat)>();
            for(int i=0;i<party.Length;i++)
            {
                if(!chosen[i].TryReserve(party[i])){foreach(var r in reserved)r.Item2.Release(r.Item1);return false;}
                reserved.Add((party[i],chosen[i]));
            }
            for(int i=0;i<party.Length;i++)
            {
                var state=new ResidentTownMealState(party[i],chosen[i],this,CurrentDay,Mathf.Clamp(stayMinutes,20,35));
                if(!party[i].Brain.StateMachine.ChangeState(state,ResidentRequestPriority.ScheduledActivity))
                {
                    foreach(var r in reserved)r.Item2.Release(r.Item1);
                    CancelAllVisits();
                    return false;
                }
                ledger.visitors.Add(party[i].ResidentId);visits.Add(party[i]);
            }
            StartedParties++;StartedVisitors+=party.Length;
            Debug.Log("TOWN_MEAL_PARTY day="+CurrentDay+" minute="+Minute+" size="+party.Length);
            return true;
        }
        public void CancelAllVisits()
        {
            foreach(var resident in visits.ToArray())
                if(resident&&resident.Brain?.StateMachine.Current is ResidentTownMealState)resident.Brain.RequestScheduleChange();
            visits.Clear();
        }
    }
}
