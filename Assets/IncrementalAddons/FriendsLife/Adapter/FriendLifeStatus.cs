using System;
using System.Linq;
using System.Text;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // Tomodachi: you can always see where a Mii is and what it is doing. The
    // line is read from the actual body (lease, original state, authored spot),
    // never from the kernel's resident.activity text.
    public static class FriendLifeStatus
    {
        // Always a short fixed Chinese line (or "" for a non-friend); never
        // English, ids or exception text. UI callers: M5 list/detail, M2 talk.
        public static string Describe(string personId)
        {
            try{return DescribeBody(personId);}
            catch(Exception error){Debug.LogWarning("[FriendsLife] Status line unavailable: "+error.Message);return "";}
        }
        static string DescribeBody(string personId)
        {
            var runtime=FriendsLifeRuntime.Instance;var person=runtime&&runtime.Ready?runtime.GetPerson(personId):null;
            if(person==null||person.controllerKind!=PersonControllerKind.Npc)return "";
            if(runtime.Core.IsResidentAway(person.nativeResidentId))return "外出中";
            var lease=runtime.State.leases.FirstOrDefault(l=>l.personId==person.personId);
            if(lease!=null)return LeaseText(lease);
            if(FriendsEconomyService.Instance?.HasActiveJob(person.personId)==true)return "在工作";
            var actor=runtime.ActorOf(person.personId);
            if(!actor||actor.Brain==null||actor.Runtime==null)return string.IsNullOrEmpty(person.currentHomeId)?"还没有搬来":"在家";
            var state=actor.Runtime;var current=actor.Brain.StateMachine.Current;
            if(state.IsInteracting)return "正在和你说话";
            if(state.IsInRestaurant)return "在寿司店吃饭";
            if(state.HasRestaurantAssignment||state.CurrentActivity==ResidentActivityType.Restaurant)return "正在去寿司店吃饭";
            if(state.IsWorking||current is ResidentWorkState)return "在工作";
            if(current is ResidentHomeState)return Night(runtime)?"在家睡觉":"在家";
            if(current is ResidentReturnHomeState)return "正在回家";
            if(current is ResidentSeatActivityState seat)
            {
                string place=Place(seat.Activity);
                switch(seat.Phase)
                {
                    case "EnteringSeat":case "Seated":case "EnteringActivity":case "Active":return Doing(seat.Activity);
                    case "Queued":case "Approaching":return "正在去"+place;
                    case "WaitingResource":return OriginalNpcActivityPort.IsSeat(seat.Activity)?"在找空长椅":"正在去"+place;
                    default:return "在"+place+"附近";
                }
            }
            if(current is ResidentMoveToScheduleTargetState)
            {
                switch(state.CurrentActivity)
                {
                    case ResidentActivityType.CityPatrolNear:return "在街上散步";
                    case ResidentActivityType.ParkPatrol:return "在公园散步";
                    case ResidentActivityType.SeaSwim:return state.IsSwimming?"在海里游泳":"正在去海边游泳";
                    case ResidentActivityType.Work:return "正在去上班";
                    default:return "正在去"+Place(state.CurrentActivity);
                }
            }
            return "在附近休息";
        }
        static bool Night(FriendsLifeRuntime runtime)
        {double minute=(runtime.HostGameMinute%1440+1440)%1440;return minute>=21*60||minute<7*60;}
        static string LeaseText(ActivityLease lease)
        {
            string facility=lease.facilityId??"";
            if(facility.StartsWith("OriginalActivityTransit",StringComparison.Ordinal))return "正在出门";
            if(facility.StartsWith("bed/",StringComparison.Ordinal))return "在家睡觉";
            switch(facility)
            {
                case "WorldTalk":return "正在和你说话";
                case "SocialApproach":return "正在来找你";
                case "SourceEpisode":return "正在和朋友说话";
                case "SushiWork":return "在寿司店工作";
                case "TownRestaurant":return "在餐厅吃饭";
                case "Cinema":return "在看电影";
                case "Casino":return "在娱乐场";
                case "OriginalRTravel":return "外出中";
                case "SourceWorkBoundary":case "SourceRecovery":return "正在下班";
                default:return lease.phase=="JobHandover"?"正在下班":"在忙";
            }
        }
        static string Place(ResidentActivityType type)
        {
            switch(type)
            {
                case ResidentActivityType.SeasideBench:return "海边长椅";
                case ResidentActivityType.ParkBench:return "公园长椅";
                case ResidentActivityType.BeachSunbathe:return "海滩";
                case ResidentActivityType.SeaSwim:return "海边";
                case ResidentActivityType.MarketVisit:return "市场";
                case ResidentActivityType.ParkPatrol:return "公园";
                case ResidentActivityType.LighthouseIdle:return "灯塔";
                case ResidentActivityType.PierFishing:return "码头";
                case ResidentActivityType.Restaurant:return "寿司店";
                case ResidentActivityType.Home:return "家";
                default:return "街上";
            }
        }
        static string Doing(ResidentActivityType type)
        {
            switch(type)
            {
                case ResidentActivityType.SeasideBench:return "在海边长椅休息";
                case ResidentActivityType.ParkBench:return "在公园长椅休息";
                case ResidentActivityType.BeachSunbathe:return "在海滩晒太阳";
                case ResidentActivityType.MarketVisit:return "在市场逛街";
                case ResidentActivityType.LighthouseIdle:return "在灯塔边看海";
                case ResidentActivityType.PierFishing:return "在码头闲逛";
                default:return "在附近休息";
            }
        }
    }

    // Editor-eval probe for play-tests. Read-only except SetTime.
    public static class FriendLifeDebug
    {
        // One line per projected friend: status, original state, place,
        // target, seconds standing still, lease, schedule and places reached.
        public static string Snapshot()
        {
            var runtime=FriendsLifeRuntime.Instance;
            if(!runtime||!runtime.Ready)return "FriendsLife not ready";
            var text=new StringBuilder();
            double minute=(runtime.HostGameMinute%1440+1440)%1440;
            text.Append("time ").Append(((int)minute/60).ToString("00")).Append(':').Append(((int)minute%60).ToString("00")).Append('\n');
            var seats=new System.Collections.Generic.List<(string name,Vector3 pose)>();
            foreach(var person in runtime.People.Where(p=>p.controllerKind==PersonControllerKind.Npc))
            {
                var actor=runtime.ActorOf(person.personId);
                text.Append(person.displayName).Append(" | ").Append(FriendLifeStatus.Describe(person.personId));
                if(!actor||actor.Brain==null||actor.Runtime==null){text.Append(" | no body\n");continue;}
                var state=actor.Runtime;var guard=actor.GetComponent<FriendOwnedAmbientPathGuardian>();
                var lease=runtime.State.leases.FirstOrDefault(l=>l.personId==person.personId);
                text.Append(" | state=").Append(actor.Brain.StateMachine.CurrentName)
                    .Append(" activity=").Append(state.CurrentActivity)
                    .Append(" pos=").Append(actor.transform.position.ToString("F1"))
                    .Append(" target=").Append(state.HasValidTarget?state.TargetDestination.ToString("F1"):"-")
                    .Append(" still=").Append(guard?guard.StillSeconds.ToString("F0"):"?").Append("s")
                    .Append(" visible=").Append(OriginalNpcActivityPort.Visible(actor))
                    .Append(" lease=").Append(lease==null?"-":lease.facilityId+"/"+lease.phase)
                    .Append(" schedule=").Append(actor.ResidentData&&actor.ResidentData.Schedule?actor.ResidentData.Schedule.name:"-")
                    .Append(" places=").Append(guard?string.Join(",",guard.PlacesReached):"?")
                    .Append(" outfitFill=").Append(FriendResidentBindings.FilledSlots(actor) is string fill&&fill.Length>0?fill:"-");
                if(actor.Brain.StateMachine.Current is ResidentSeatActivityState seat)
                {
                    var view=seat.Snapshot();
                    text.Append(" spot=").Append(view.resourceId).Append(" reason=").Append(view.reason);
                    if(seat.Resting)seats.Add((person.displayName,view.pose));
                }
                text.Append('\n');
            }
            for(int a=0;a<seats.Count;a++)for(int b=a+1;b<seats.Count;b++)
                if(Vector3.Distance(seats[a].pose,seats[b].pose)<.35f)text.Append("SHARED SEAT: ").Append(seats[a].name).Append(" + ").Append(seats[b].name).Append('\n');
            return text.ToString();
        }
        // Re-applies the friend's own appearance once (missing hair/top/bottom/
        // shoes filled from an original resident outfit) and re-bakes the rig.
        public static string RefreshAppearance(string personId)
        {
            var runtime=FriendsLifeRuntime.Instance;var person=runtime&&runtime.Ready?runtime.GetPerson(personId):null;
            var actor=person==null?null:runtime.ActorOf(person.personId);
            if(!actor||!FriendResidentBindings.IsOwned(actor))return "no owned friend body";
            var appearance=FriendResidentBindings.DesiredAppearance(actor);
            if(!appearance)return "no appearance";
            FriendResidentBindings.ApplyAppearance(actor,appearance);
            string fill=FriendResidentBindings.FilledSlots(actor);
            return person.displayName+" reloading; filled "+(string.IsNullOrEmpty(fill)?"nothing":fill);
        }
        // Moves the original Cozy clock (same calls as the existing editor
        // audits) and re-dispatches the original resident schedule minute.
        public static string SetTime(int hour,int minute)
        {
            if(!Singleton<GameProgressManager>.TryGetInstance(out var progress)||progress.CozyWeatherManager==null)return "no clock";
            var clock=progress.CozyWeatherManager;clock.ClientSetTime(Mathf.Clamp(hour,0,23),Mathf.Clamp(minute,0,59));clock.TriggerBlackBoardTimeEvents(true);
            return "time set";
        }
    }
}
