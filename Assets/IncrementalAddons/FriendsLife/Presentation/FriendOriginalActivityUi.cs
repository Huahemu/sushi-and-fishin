using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Managers;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // UI navigation/status labels only, not invented NPC dialogue. The caller
    // remains the existing DBS DialoguePanelController and its authored styles.
    public static class FriendOriginalActivityUi
    {
        public static string Header(string personId)
        {
            var runtime=FriendsLifeRuntime.Instance;var actor=runtime?.ActorOf(personId);if(!actor||actor.Runtime==null)return "";
            var view=OriginalNpcActivityPort.Observe(actor);var type=actor.Runtime.CurrentActivity;
            if(view!=null&&System.Enum.TryParse(view.activity,out ResidentActivityType observed))type=observed;
            bool seat=OriginalNpcActivityPort.IsSeat(type);string activity=Name(type);
            bool returningHome=actor.Brain?.StateMachine?.Current is ResidentReturnHomeState;
            if(returningHome)activity="回家";
            if(view==null)return "当前活动："+activity;
            string status;
            switch(view.phase)
            {
                case "Queued":status=seat?"准备前往座位":"准备前往活动地点";break;
                case "WaitingResource":status=seat?"等候空位":"等候活动位置";break;
                case "Approaching":status=seat?"正在前往座位":"正在前往活动地点";break;
                case "EnteringSeat":status="正在坐下";break;
                case "EnteringActivity":status=type==ResidentActivityType.BeachSunbathe?"正在躺下":"准备开始活动";break;
                case "Active":status=activity;break;
                case "Working":status="正在工作";break;
                case "Moving":status=returningHome?"正在返回住宅":"正在前往活动地点";break;
                case "WaitingPath":status="正在等候可通行的路线";break;
                case "Seated":status="正在休息";break;
                case "StandingUp":case "ReturningToApproach":status="正在起身，请稍等";break;
                case "SafeBoundary":status=view.requestHeld?"已起身，正在交还当前活动":seat?"已起身，可以继续互动":"活动已结束，可以继续互动";break;
                case "Failed":status=seat?"暂时无法到达座位":"暂时无法到达活动地点";break;
                default:status=returningHome?"正在返回住宅":activity;break;
            }
            return "当前活动："+activity+"\n"+status;
        }
        public static bool CanCancelCurrent(string personId)
        {
            var runtime=FriendsLifeRuntime.Instance;var actor=runtime?.ActorOf(personId);
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!runtime.IsWorldRunning||!actor||!OriginalNpcActivityPort.CanAmbientOwn(actor)||
                !(actor.Brain?.StateMachine?.Current is ResidentSeatActivityState state))return false;
            var view=state.Snapshot();
            return !view.safeBoundary&&view.phase!="StandingUp"&&view.phase!="ReturningToApproach"&&view.phase!="ExitBlocked";
        }
        public static bool TryCancelCurrent(string personId,out string reason)
        {
            reason=null;var runtime=FriendsLifeRuntime.Instance;var actor=runtime?.ActorOf(personId);
            if(!CanCancelCurrent(personId)){reason="当前活动尚不能交还，请等待本次工作、用餐或社交完成。";return false;}
            var state=(ResidentSeatActivityState)actor.Brain.StateMachine.Current;
            if(!OriginalNpcActivityPort.ResumeWhenSafe(actor,state.RequestOwner)){reason="当前活动正在等待安全交接。";return false;}
            return true;
        }
        public static void Track(DialoguePanelController panel,ResidentBillboardText billboard,string personId)
        {
            if(!panel||!billboard)return;
            var observer=panel.GetComponent<FriendOriginalActivityHeaderWatch>();
            if(!observer)observer=panel.gameObject.AddComponent<FriendOriginalActivityHeaderWatch>();
            observer.Bind(panel,billboard,personId);
        }
        static string Name(ResidentActivityType type)
        {
            switch(type)
            {
                case ResidentActivityType.Home:return "在家";
                case ResidentActivityType.SeasideBench:return "海边长椅";
                case ResidentActivityType.ParkBench:return "公园长椅";
                case ResidentActivityType.BeachSunbathe:return "海滩休息";
                case ResidentActivityType.SeaSwim:return "游泳";
                case ResidentActivityType.CityPatrolNear:return "散步";
                case ResidentActivityType.MarketVisit:return "逛市场";
                case ResidentActivityType.ParkPatrol:return "逛公园";
                case ResidentActivityType.LighthouseIdle:return "灯塔休息";
                case ResidentActivityType.PierFishing:return "码头活动";
                case ResidentActivityType.Restaurant:return "用餐";
                case ResidentActivityType.Work:return "工作";
                default:return "自由活动";
            }
        }
    }
    public sealed class FriendOriginalActivityHeaderWatch:MonoBehaviour
    {
        DialoguePanelController panel;ResidentBillboardText billboard;string personId,lastText;
        public void Bind(DialoguePanelController value,ResidentBillboardText body,string person)
        {panel=value;billboard=body;personId=person;lastText=value.ConversationText;}
        void LateUpdate()
        {
            if(string.IsNullOrEmpty(personId)||!panel||!billboard)return;
            string current=FriendOriginalActivityUi.Header(personId);
            if(!panel.TryRefreshActivityStatus(billboard,lastText,current)){personId=null;return;}
            lastText=current;
        }
    }

}
