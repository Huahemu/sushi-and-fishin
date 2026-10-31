using System;
using System.Linq;

namespace TomodachiLife.Simulation
{
    public sealed partial class LifeSimulationRuntime
    {
        public LifeActivitiesResult StartMinigame(string residentId,string definitionId)
        {
            var a=EnsureActivities();var resident=FindResident(residentId);var definition=LifeActivitiesCatalog.Minigames.FirstOrDefault(d=>d.id==definitionId);
            if(resident==null||definition==null)return ActivityResult(false,"请选择居民与小游戏。");if(a.minigame!=null&&!a.minigame.completed)return ActivityResult(false,"请先完成或退出当前小游戏。");
            if(IsResidentInActivity(residentId))return ActivityResult(false,"居民正在参加其他活动。");int required=Math.Max(definition.minimumResidents,definition.requiredPopulation);
            if(State.residents.Count<required)return ActivityResult(false,"这项小游戏需要岛上至少有 "+required+" 位居民。");
            var session=new LifeMinigameSession{id=Guid.NewGuid().ToString("N"),residentId=residentId,definitionId=definitionId,prompt=definition.explanation};
            session.participantIds.Add(residentId);if(definition.group)session.participantIds.AddRange(State.residents.Where(r=>r.id!=residentId&&!IsResidentInActivity(r.id)).Take(Math.Max(0,definition.maximumParticipants-1)).Select(r=>r.id));
            a.minigame=session;foreach(string id in session.participantIds){var r=FindResident(id);if(r!=null)r.activity="玩"+definition.title;}
            PrepareMinigameRound(session);ActivityChanged();return ActivityResult(true,"开始"+definition.title+"。",session.id);
        }
        private void PrepareMinigameRound(LifeMinigameSession g)
        {
            g.roundElapsed=0;g.message="";g.options.Clear();g.board.Clear();g.phase=0;g.swaps=0;
            switch(g.definitionId)
            {
                case "Bowling":g.prompt="调整方向与力度，三轮内尽可能击倒更多球瓶。";g.pins=10;g.shots=0;break;
                case "CoinTurn":g.prompt="预测正面或反面，再选择力度转动硬币。";g.answer=0;g.target=-1;break;
                case "EastAndWest":case "EastAndWestGroup":
                    g.prompt="在8秒内选一个还没有提及过的食物。";
                    for(int i=0;i<3;i++){string key;int tries=0;do{key=Rules.Data.foods[ActivityRandom(Rules.Data.foods.Length)].name;}while(g.options.Contains("F:"+key)&&++tries<20);g.options.Add("F:"+key);}
                    g.options.Insert(ActivityRandom(4),"G:"+Rules.Data.goods[ActivityRandom(Rules.Data.goods.Length)].name);
                    if(g.usedFoods.Count>0&&g.round%2==1)g.options[ActivityRandom(4)]="F:"+g.usedFoods[ActivityRandom(g.usedFoods.Count)];break;
                case "FerrisWheelPush":
                    g.prompt="只推三次，让标记吊舱停在箭头位置。每次可推进1、2或3格。";g.board.Add(0);g.target=(ActivityRandom(7)+3)%8;g.shots=0;break;
                case "FindTheOnly":
                    g.prompt="找出九个居民中唯一不同的那个。";g.answer=ActivityRandom(9);for(int i=0;i<9;i++)g.board.Add(i==g.answer?1:0);break;
                case "StatuesGame":g.prompt="绿灯时前进，红灯时停下；30秒内到达终点。";g.distance=0;break;
                case "ThreeShellGame":g.prompt="记住硬币的位置，追踪四次杯子交换。";g.board.AddRange(new[]{1,0,0});break;
                case "Dummy":g.prompt="等“现在点击”出现后再点，抢跑会失败。";g.target=2+ActivityRandom(4);break;
                case "ViewFromFerrisWheel":g.prompt="转到0、2、4号方向，分别记录一次风景。";g.phase=0;g.visitedViews.Clear();break;
                default:
                    if(g.definitionId=="CafeLatteGame"||g.definitionId=="SilhouetteGameMii")
                    {g.options.AddRange(State.residents.OrderBy(r=>r.id).Take(4).Select(r=>r.id));g.answer=ActivityRandom(g.options.Count);g.target=g.answer;g.prompt=g.definitionId=="CafeLatteGame"?"咖啡拉花的轮廓是谁？":"这是谁的剪影？";}
                    else
                    {
                        g.options.AddRange(new[]{"Apple","Guitar","BaseBall","Umbrella"});g.answer=ActivityRandom(4);g.target=g.answer;
                        if(g.definitionId=="SilhouetteGameDouble")
                        {g.options.Clear();g.options.AddRange(new[]{"Apple+Guitar","Apple+BaseBall","Guitar+Umbrella","BaseBall+Umbrella"});g.prompt="两件物品的剪影叠在一起，猜猜是哪一组。";}
                        else g.prompt=g.definitionId=="SilhouetteGameMosaic"?"辨认被打上马赛克的物品。":g.definitionId=="SilhouetteGameZoom"?"从特写辨认物品。":g.definitionId=="SilhouetteGameNoting"?"场景里没有出现的物品是哪一个？":"辨认这件物品的剪影。";
                    }
                    break;
            }
        }
        public void TickMinigame(float realSeconds)
        {
            var g=EnsureActivities().minigame;if(g==null||g.completed||float.IsNaN(realSeconds)||float.IsInfinity(realSeconds)||realSeconds<=0)return;
            g.elapsed+=realSeconds;g.roundElapsed+=realSeconds;
            if(g.definitionId=="CoinTurn"&&g.phase==1&&g.roundElapsed>=1.5f)
            {
                bool correct=g.answer==g.target;g.score+=correct?1:0;g.message="硬币停在"+(g.target==0?"正面":"反面")+"，"+(correct?"猜中了！":"这次没有猜中。");g.round++;g.phase=2;ActivityChanged();
                if(g.round>=3)FinishMinigame(g,g.score>=2);return;
            }
            if(g.definitionId=="StatuesGame")
            {int phase=(int)(g.elapsed/2)%2;if(phase!=g.phase){g.phase=phase;ActivityChanged();}if(g.elapsed>=30)FinishMinigame(g,g.distance>=1);return;}
            if(g.definitionId=="ThreeShellGame"&&g.phase==0)
            {
                while(g.swaps<4&&g.roundElapsed>=1.2f+g.swaps*.8f)
                {int left=ActivityRandom(3),right=(left+1+ActivityRandom(2))%3;int value=g.board[left];g.board[left]=g.board[right];g.board[right]=value;g.swaps++;g.message="杯子 "+(left+1)+" 与 "+(right+1)+" 交换位置。";ActivityChanged();}
                if(g.swaps==4&&g.roundElapsed>=4.5f){g.phase=1;g.message="现在猜硬币在哪个杯子下面？";ActivityChanged();}return;
            }
            if(g.definitionId=="Dummy"&&g.phase==0&&g.roundElapsed>=g.target){g.phase=1;g.message="现在点击！";ActivityChanged();}
            float limit=g.definitionId=="EastAndWest"||g.definitionId=="EastAndWestGroup"?8:g.definitionId=="Dummy"?g.target+1.2f:g.definitionId=="ViewFromFerrisWheel"?60:45;
            if(g.roundElapsed>limit)FinishMinigame(g,false);
        }
        public LifeActivitiesResult SubmitMinigameInput(string action,int option=0,float value=0,float secondary=0)
        {
            var g=EnsureActivities().minigame;if(g==null||g.completed)return ActivityResult(false,"当前没有正在进行的小游戏。");
            g.inputs.Add(action+":"+option+":"+value.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture));
            if(float.IsNaN(value)||float.IsInfinity(value)||float.IsNaN(secondary)||float.IsInfinity(secondary))return ActivityResult(false,"输入无效。");
            switch(g.definitionId)
            {
                case "Bowling":
                    if(action!="throw")return ActivityResult(false,"请调整方向与力度后投球。");
                    float power=Math.Max(0,Math.Min(1,value)),aim=Math.Max(-1,Math.Min(1,secondary));
                    int hit=Math.Abs(aim)>.92f||power<.15f?0:Math.Max(0,Math.Min(g.pins,(int)Math.Round(g.pins*(1-Math.Abs(aim)*.7f)*(1-Math.Abs(power-.8f)*.9f))-(ActivityRandom(5)==0?1:0)));
                    g.score+=hit;g.pins-=hit;g.shots++;g.message="击倒 "+hit+" 瓶，剩余 "+g.pins+" 瓶。";
                    if(g.pins==0||g.shots>=2){g.round++;if(g.round>=3)return FinishMinigame(g,g.score>=20);g.pins=10;g.shots=0;g.roundElapsed=0;}break;
                case "CoinTurn":
                    if(action=="predict"){if(g.phase!=0)return ActivityResult(false,"硬币开始转动后不能更改预测。");g.answer=option==0?0:1;g.message="预测"+(g.answer==0?"正面":"反面");break;}
                    if(action=="next"&&g.phase==2){PrepareMinigameRound(g);break;}
                    if(action!="spin"||g.phase!=0)return ActivityResult(false,"硬币正在旋转，或者需要先进入下一轮。");
                    g.target=(ActivityRandom(2)+(int)(Math.Max(0,Math.Min(1,value))*11))%2;g.phase=1;g.roundElapsed=0;g.message="硬币正在旋转……";break;
                case "EastAndWest":case "EastAndWestGroup":
                    if(option<0||option>=g.options.Count)return ActivityResult(false,"请选择一个选项。");string word=g.options[option];
                    if(!word.StartsWith("F:",StringComparison.Ordinal)||g.usedFoods.Contains(word.Substring(2)))return FinishMinigame(g,false);
                    g.usedFoods.Add(word.Substring(2));g.score++;g.round++;
                    if(g.definitionId=="EastAndWestGroup"&&g.round>1&&ActivityRandom(5)==0)return FinishMinigame(g,true);
                    if(g.round>=(g.definitionId=="EastAndWestGroup"?6:8))return FinishMinigame(g,true);
                    if(g.definitionId=="EastAndWestGroup"){var other=Rules.Data.foods[ActivityRandom(Rules.Data.foods.Length)].name;g.usedFoods.Add(other);}
                    PrepareMinigameRound(g);break;
                case "FerrisWheelPush":
                    if(action!="push")return ActivityResult(false,"请选择推进格数。");int step=Math.Max(1,Math.Min(3,option));g.board[0]=(g.board[0]+step)%8;g.shots++;g.message="当前标记在 "+g.board[0]+"，目标在 "+g.target+"。";g.score=8-Math.Abs(g.target-g.board[0]);
                    if(g.shots>=3)return FinishMinigame(g,g.board[0]==g.target);break;
                case "FindTheOnly":
                    if(option<0||option>=9)return ActivityResult(false,"请选择九宫格中的一个居民。");return ResolveQuizAnswer(g,option==g.answer,3,3);
                case "StatuesGame":
                    if(action!="move")return ActivityResult(false,"绿灯时点击前进。");if(g.phase==1)return FinishMinigame(g,false);g.distance=Math.Min(1,g.distance+.14f);g.score=(int)(g.distance*100);if(g.distance>=1)return FinishMinigame(g,true);break;
                case "ThreeShellGame":
                    if(g.phase!=1)return ActivityResult(false,"请先观察杯子交换。");if(option<0||option>=3)return ActivityResult(false,"请选择一个杯子。");return ResolveQuizAnswer(g,g.board[option]==1,3,2);
                case "Dummy":
                    if(action!="tap")return ActivityResult(false,"等待指示再点击。");if(g.phase==0)return FinishMinigame(g,false);
                    return ResolveQuizAnswer(g,g.roundElapsed-g.target<=.55f,3,2);
                case "ViewFromFerrisWheel":
                    if(action=="rotate"){g.phase=(g.phase+8+(option<0?-1:1))%8;g.message="当前方向 "+g.phase+"。";break;}
                    if(action!="shutter")return ActivityResult(false,"先转动角度，再记录景色。");if(g.phase!=0&&g.phase!=2&&g.phase!=4)return ActivityResult(false,"这里不是指定的观景方向。");
                    if(!g.visitedViews.Contains(g.phase))g.visitedViews.Add(g.phase);g.score=g.visitedViews.Count;if(g.score==3)return FinishMinigame(g,true);g.message="已经记录 "+g.score+" / 3 个方向。";break;
                default:
                    if(option<0||option>=g.options.Count)return ActivityResult(false,"请选择一个答案。");return ResolveQuizAnswer(g,option==g.answer,3,2);
            }
            ActivityChanged();return ActivityResult(true,g.message,g.id);
        }
        private LifeActivitiesResult ResolveQuizAnswer(LifeMinigameSession g,bool correct,int rounds,int scoreToWin)
        {
            if(correct)g.score++;else g.lives--;g.round++;g.message=correct?"答对了！":"这次没有答对。";
            if(g.round>=rounds||g.lives<=0)return FinishMinigame(g,g.score>=scoreToWin);PrepareMinigameRound(g);ActivityChanged();return ActivityResult(true,correct?"答对了，进入下一轮。":"再仔细观察一下。",g.id);
        }
        private LifeActivitiesResult FinishMinigame(LifeMinigameSession g,bool won)
        {
            if(g.completed)return ActivityResult(true,g.message,g.id,g.won,g.rewardRaw);g.completed=true;g.won=won;g.rewardClaimed=true;
            g.rewardRaw=g.definitionId=="Dummy"?0:won?200+Math.Min(100,g.score*10):30;
            if(g.rewardRaw>0)ActivityMoney(g.rewardRaw,"minigame-result");string prize="";
            if(won)
            {
                EnsureActivities().minigamesWon++;if(g.definitionId!="Dummy"){var item=Rules.Data.goods[ActivityRandom(Rules.Data.goods.Length)].name;ActivityInventory("Goods",item);prize="，还有一件宝物";}UnlockProgressPhoto();
            }
            foreach(string id in g.participantIds){var resident=FindResident(id);if(resident!=null){resident.activity="散步";resident.social=Need(resident.social+(won?12:5));}}
            var definition=LifeActivitiesCatalog.Minigames.First(d=>d.id==g.definitionId);g.message=(won?"挑战成功！":"这次挑战结束。")+"得分 "+g.score+"，获得 "+g.rewardRaw+prize+"。";
            ActivityRecord("Minigame",g.definitionId,g.residentId,definition.title,g.message,won,g.rewardRaw);ActivityChanged();return ActivityResult(true,g.message,g.id,won,g.rewardRaw);
        }
        public LifeActivitiesResult LeaveMinigame()
        {
            var g=EnsureActivities().minigame;if(g==null)return ActivityResult(false,"没有正在进行的小游戏。");
            if(!g.completed){foreach(string id in g.participantIds){var resident=FindResident(id);if(resident!=null)resident.activity="散步";}ActivityRecord("Minigame",g.definitionId,g.residentId,"退出小游戏","本次小游戏提前结束，没有发放奖励。");}
            EnsureActivities().minigame=null;ActivityChanged();return ActivityResult(true,"回到海岛生活。");
        }
    }
}
