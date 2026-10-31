using System;
using System.Linq;
using SushiAndFish.FriendsLife.CasinoCore;
using SushiAndFish.Casino.Original;
using SushiAndFish.Casino.Original.Extensions;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendTabletPresenter
    {
        string casinoSelection,casinoBetText="0";int casinoFloor=1;
        public void OpenCasino(string id)
        {if(!FriendsLifeFeatureFlags.Casino&&!HasCasinoJourney(id))return;Push();personId=id;page="OriginalCasino";Render();}
        // A journey that was already running when the casino was hidden stays visible (read only).
        static bool HasCasinoJourney(string id){var service=FriendsCasinoJourneyService.Instance;return service&&service.ActiveFor(id)!=null;}
        void RenderOriginalCasino()
        {
            // Hidden while the casino flag is off; an old navigation frame lands on the friend instead.
            bool casinoOpen=FriendsLifeFeatureFlags.Casino;
            if(!casinoOpen&&!HasCasinoJourney(personId)){page=FriendsLifeRuntime.Instance?.GetPerson(personId)!=null?"Detail":"List";if(page=="Detail")RenderDetail();else RenderList();return;}
            var p=Page("T_CasinoJourney","赌场",true);
            var runtime=FriendsLifeRuntime.Instance;var person=runtime?.GetPerson(personId);var service=FriendsCasinoJourneyService.Instance;
            var balance=FriendsEconomyService.Instance?(FriendsEconomyService.Instance.BalanceCents(personId)/100m).ToString("0.00"):"—";
            FriendUi.Flexible(FriendUi.Text("Actor",p.toolbar,0,0,1088,FriendUi.ToolbarH,(person?.displayName??"朋友")+" · 余额 "+balance+" 金币",28));
            scroll=FriendUi.FitScroll("OriginalCasinoRows",p.body,1120,out var c);float y=0;
            var active=service?.ActiveFor(personId);
            if(active!=null)
            {
                FriendUi.Flow(c,ref y,"Selection",active.targetFloor+" 层 · "+GameLabel(active.type)+(active.roundTripOnly?" · 只去看看":" · 下注 "+active.bet+" 金币"),23,1120,12);
                FriendUi.Flow(c,ref y,"Activity",PhaseLabel(active.phase),27,1120,10);
                FriendUi.Flow(c,ref y,"Progress",ProgressLabel(active),23,1120,12);
                if(!string.IsNullOrEmpty(active.failure))FriendUi.Flow(c,ref y,"Waiting",PlayerReason(active.failure),22,1120,12);
                string id=active.id;
                // Flag off: status only. No button may resume, re-send or replay a hidden journey.
                if(casinoOpen){
                FriendUi.Button("Return",c,0,y,350,58,"回来",()=>{service.Return(id);Render();});
                FriendUi.Button("Cancel",c,375,y,350,58,"取消",()=>{service.Cancel(id);Render();});
                FriendUi.Button("Resume",c,750,y,350,58,"继续",()=>{if(!service.Resume(id,out var reason))Status(PlayerReason(reason));else Render();});y+=82;}
                foreach(var r in active.receipts.Skip(Math.Max(0,active.receipts.Count-8)))
                {FriendUi.Text("Receipt_"+y,c,0,y,1120,40,PhaseLabel(r.phase)+" · "+r.floor+" 层",21);y+=48;}
            }
            else
            {
                FriendUi.Flow(c,ref y,"Intro","选好楼层和机台，朋友会坐电梯过去。",24);
                for(int floor=1;floor<=4;floor++)
                {
                    int selected=floor;bool open=NetworkSingleton<ElevatorManager>.TryGetInstance(out var original)&&original.NativeFloorButtonAvailable(floor);
                    var button=FriendUi.Button("Floor_"+floor,c,(floor-1)*280,y,264,54,floor+" 层"+(open?"":" · 未开放"),()=>{casinoFloor=selected;casinoSelection=null;Render();});
                    button.interactable=open;button.SetSelected(floor==casinoFloor);
                }y+=76;
                var rows=NativeCasinoTargetDirectory.Observe().Where(t=>t.floor==casinoFloor).ToArray();
                if(rows.Length==0)FriendUi.Flow(c,ref y,"NoLayout","这层的机台还没准备好。",24);
                int number=0;foreach(var target in rows)
                {
                    string id=target.id;
                    number++;var button=FriendUi.Button("Target_"+y,c,0,y,1120,74,GameLabel(target.type)+" · "+number+" 号机台\n"+(target.instantiated?target.available?"空闲":"有人在用":"暂未开放"),()=>{casinoSelection=id;Render();});
                    button.SetSelected(casinoSelection==id);y+=90;
                }
                if(!string.IsNullOrEmpty(casinoSelection))
                {
                    var start=FriendUi.Button("RoundTrip",c,0,y,530,64,"去看看（不下注）",()=>
                    {string reason=null;if(!service||!service.TryStartRoundTrip(personId,casinoSelection,"tablet/"+Guid.NewGuid().ToString("N"),out _,out reason))Status(service?PlayerReason(reason):"赌场还没准备好，请稍后再试。");else Render();},true);
                    start.interactable=service;y+=84;
                    if(NativeCasinoTargetDirectory.TryGet(casinoSelection,out var chosen)&&chosen.type==CasinoGameType.SlotMachine.ToString())
                    {
                        FriendUi.Text("BetLabel",c,0,y,640,58,"下注金币（填 0 为最低注）",24);
                        var amount=FriendUi.Input("CasinoBet",c,660,y,440,58,casinoBetText,"金币");
                        amount.onEndEdit.AddListener(v=>casinoBetText=v);y+=76;
                        var play=FriendUi.Button("OriginalSlotsRound",c,0,y,1100,64,"玩一局老虎机",()=>
                        {
                            if(!long.TryParse(casinoBetText,out var bet)||bet<0){Status("请输入 0 或更大的整数。");return;}
                            string reason=null;
                            if(!service||!service.TryStart(personId,casinoSelection,"tablet/"+Guid.NewGuid().ToString("N"),bet,false,out _,out reason))Status(service?PlayerReason(reason):"赌场还没准备好，请稍后再试。");else Render();
                        },true);play.interactable=service;y+=86;
                    }
                }
                var previous=service?.Journeys.LastOrDefault(j=>j.personId==personId&&FriendsCasinoJourneyService.Terminal(j));
                if(previous!=null)FriendUi.Flow(c,ref y,"LastResult","上次："+PhaseLabel(previous.phase),23);
            }
            FriendUi.FitContent(c,y);BackButton();
        }
        // About ten short labels instead of one sentence per internal phase.
        static string PhaseLabel(string phase)
        {
            switch(phase)
            {
                case "HomeDeparturePlanning":case "HomeDepartureTransit":case "HomeDepartureWalk":case "HomeDepartureReady":case "Queued":return "出门中";
                case "LobbyApproach":case "LobbyCabEntry":return "去电梯";
                case "OutboundButton":case "OriginalTransfer":case "FloorCabExit":return "坐电梯";
                case "SourceStartPending":case "MachineApproach":return "去机台";
                case "MachineReady":case "OriginalRound":return "正在玩";
                case "ReturnApproach":case "ReturnCabEntry":case "ReturnButton":case "ReturnCabExit":return "返回中";
                case "Done":case "SettledRestoredGround":return "已完成";
                case "Cancelled":return "已取消";
                default:return "请稍等";
            }
        }
        static string GameLabel(string type)
        {
            switch(type){case "SlotMachine":case "Slots":return "老虎机";case "Blackjack":return "二十一点";case "Roulette":return "轮盘";case "Craps":return "双骰";
                case "WheelOfFortune":return "幸运转轮";case "DuckRace":return "赛鸭";case "HiLo":return "高低牌";case "Keno":return "基诺";
                case "CrossyRoad":return "过马路";case "MoneyWheel":return "金币转轮";case "Plinko":return "弹珠落盘";case "Crash":return "爆点";
                case "Minesweeper":return "扫雷";case "DragonTower":return "龙之塔";case "Poker":return "扑克";case "Baccarat":return "百家乐";default:return "机台";}
        }
        // Journey reasons are internal log text (English or system words). Only a
        // few cases get their own short line; every other reason, including any
        // exception text, maps to one generic sentence. The casino service is not changed.
        static string PlayerReason(string reason)
        {
            if(string.IsNullOrEmpty(reason))return "";
            if(reason.Contains("原有路径未闭合"))return "路不通，请稍后再试。";
            if(reason.IndexOf("physical",StringComparison.OrdinalIgnoreCase)>=0||reason.Contains("capsule")||reason.Contains("source support"))return "电梯口被挡住了，请稍后再试。";
            if(reason.Contains("Human priority")||reason.Contains("shared-floor"))return "等其他人离开电梯口。";
            if(reason.Contains("服务其他人")||reason.Contains("原玩家优先"))return "机台有人在用。";
            if(reason.Contains("余额不足")||reason.Contains("下注范围"))return "余额不足或超出下注范围。";
            if(reason.Contains("重新选定机台")||reason.Contains("Select an actual planned"))return "请重新选择机台。";
            if(reason.Contains("尚未开放"))return "这层还没开放。";
            if(reason.Contains("An available bound friend"))return "朋友或赌场还没准备好。";
            if(reason.Contains("input adapter is not installed"))return "这台机器暂不支持。";
            if(reason.Contains("Journey is not active"))return "活动已结束。";
            Debug.Log("[FriendsLife] Casino reason shown as generic: "+reason);
            return "请稍等，稍后再试。";
        }
        static string ProgressLabel(CasinoJourney j)
        {
            bool entry=j.receipts.Any(r=>r.phase=="OutboundButton");bool arrived=j.receipts.Any(r=>r.phase=="MachineReady");
            bool returning=j.receipts.Any(r=>r.phase=="ReturnApproach");
            return (entry?"✓":"○")+" 进电梯 → "+(arrived?"✓":"○")+" 到机台 → "+(returning?"✓":"○")+" 返回 → "+(FriendsCasinoJourneyService.Terminal(j)?"✓":"○")+" 完成";
        }
    }
}
