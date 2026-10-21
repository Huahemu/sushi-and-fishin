using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Reflection;using Cysharp.Threading.Tasks;using UnityEngine;using UnityEditor;using UnityEngine.SceneManagement;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;using PaperPixelGames.Core.Managers;using PaperPixelGames.Core.SaveSystem;using PaperPixelGames.Framework.Patterns;using PaperPixelGames.Framework.Input;using SushiAndFish.Casino;using SushiAndFish.Casino.Original;using SushiAndFish.Casino.Original.CasinoLab;using SushiAndFish.Casino.Original.Extensions;using Object=UnityEngine.Object;
public static partial class CityExpansionPlayAudit {
 [Serializable]public class InputGameCase{public string name,type,error;public bool started,finished,result,wallet,grounded;public long before,after,bet,payout;public int pressed;}
 public static void DetailedAllGames(){DetailedInput();SessionState.SetBool(Key+"fullInput",true);}
 static bool HasMethod(InteractableEventTrigger b,string name)=>Enumerable.Range(0,b.serverOnInteractEvent.GetPersistentEventCount()).Any(i=>b.serverOnInteractEvent.GetPersistentMethodName(i)==name);
 static InteractableEventTrigger[] GameButtons(GameBase game)=>game.GetComponentsInChildren<InteractableEventTrigger>(true).Where(b=>!b.GetComponentInParent<Keypad>()).ToArray();
 static float NativeFloorHeight(ParkCasinoIntegration c,Vector3 point,float top){return Physics.RaycastAll(new Vector3(point.x,top,point.z),Vector3.down,16,~0,QueryTriggerInteraction.Ignore).Where(h=>h.normal.y>.65f&&!h.transform.IsChildOf(c.HostPlayer.transform)&&!h.collider.GetComponentInParent<GameBase>()&&!h.collider.GetComponentInParent<Item>()).Select(h=>h.point.y).DefaultIfEmpty(c.nativeMapOrigin.y).Max();}
 static async UniTask ReachControl(ParkCasinoIntegration c,InteractableBase b){
  var co=b.GetComponents<Collider>().FirstOrDefault(x=>x.enabled);var target=co?co.bounds.center:b.transform.position;await Aim(c,target);await Pause(.12f);if(c.LogicInteractor.TargetInteractable==b&&(!(b is Item)||Vector3.Distance(CasinoRuntimeContext.PlayerCamera.transform.position,target)<2.2f))return;
  var from=c.HostPlayer.transform.position-target;from.y=0;if(from.sqrMagnitude>.01f)from.Normalize();else from=Vector3.forward;
  var dirs=new[]{from,b.transform.forward,-b.transform.forward,b.transform.right,-b.transform.right,Vector3.forward,Vector3.back,Vector3.right,Vector3.left};
  foreach(float distance in new[]{1.3f,1.8f,2.3f})foreach(var d0 in dirs){var d=d0;d.y=0;if(d.sqrMagnitude<.1f)continue;d.Normalize();target=co?co.bounds.center:b.transform.position;var p=target+d*distance;p.y=NativeFloorHeight(c,p,target.y+.15f)+.06f;
   var overlaps=Physics.OverlapCapsule(p+Vector3.up*.4f,p+Vector3.up*1.45f,.27f,~0,QueryTriggerInteraction.Ignore);if(overlaps.Any(x=>!x.transform.IsChildOf(c.HostPlayer.transform)))continue;
   c.HostPlayer.TeleportPlayer(p,true);c.HostPlayer.FirstPersonCharacter.SetVelocity(Vector3.zero);await Pause(.12f);await Aim(c,target);await Pause(.1f);if(c.LogicInteractor.TargetInteractable==b&&(!(b is Item)||Vector3.Distance(CasinoRuntimeContext.PlayerCamera.transform.position,target)<2.2f))return;
  }
 }
 static async UniTask<InputCase> RequiredPress(ParkCasinoIntegration c,InteractableBase b,bool click=false){await ReachControl(c,b);var q=await PressActual(c,b,click);if(SessionState.GetBool(Key+"screenshot",false))await RecordScreenAfterControl(c,b);DetailedStage("PRESSED_"+b.name);if(!q.correct)throw new Exception("Input did not reach/call "+FullPath(b.transform)+" hovered="+q.hover);return q;}
 static async UniTask PressMethod(ParkCasinoIntegration c,GameBase g,string method){var b=GameButtons(g).FirstOrDefault(x=>HasMethod(x,method)&&x.gameObject.activeInHierarchy&&x.IsInteractable&&x.MeetRequirements);if(!b)throw new Exception("No available button for "+g.GetType().Name+"."+method);await RequiredPress(c,b);}
 static async UniTask StandAtPad(ParkCasinoIntegration c,GameBase g){var t=g.Keypad.transform;var front=t.forward;front.y=0;front.Normalize();var p=t.position+front*1.65f;p.y=NativeFloorHeight(c,p,t.position.y+.25f)+.06f;c.HostPlayer.TeleportPlayer(p,true);c.HostPlayer.FirstPersonCharacter.SetVelocity(Vector3.zero);await Pause(.35f);c.RegisterCasinoButtons();}
 static async UniTask VerifyPad(ParkCasinoIntegration c,Keypad pad){
  var bs=pad.GetComponentsInChildren<InteractableEventTrigger>(true);var clear=bs.First(b=>HasMethod(b,"ClearInput"));
  foreach(var b in bs.Where(b=>HasMethod(b,"AppendDigit"))){await RequiredPress(c,clear);var so=new SerializedObject(b);var calls=so.FindProperty("serverOnInteractEvent.m_PersistentCalls.m_Calls");string digit="";for(int i=0;i<calls.arraySize;i++){var e=calls.GetArrayElementAtIndex(i);if(e.FindPropertyRelative("m_MethodName").stringValue=="AppendDigit")digit=e.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;}
   string expected=digit=="000"?"0":digit;var q=await RequiredPress(c,b);q.before="0";q.after=pad.Network_currentInput;q.correct&=q.after==expected;if(!q.correct)throw new Exception("Digit "+digit+" produced "+q.after);
  }
  foreach(var b in bs.Where(b=>HasMethod(b,"SetPercentageBet"))){var q=await RequiredPress(c,b,true);q.after=pad.Network_currentInput;q.correct&=pad.GetCurrentInput()>0&&pad.GetCurrentInput()<=pad.NetworkcasinoGame.MaxBet;if(!q.correct)throw new Exception("Invalid percentage input");}
  await RequiredPress(c,bs.First(b=>HasMethod(b,"SetBetMinimum")));if(pad.GetCurrentInput()!=pad.NetworkcasinoGame.MinBet||!string.IsNullOrEmpty(pad.Network_errorMessage))throw new Exception("Minimum bet not accepted");
 }
 static async UniTask ThrowActualDice(ParkCasinoIntegration c,Craps cp){
  var dm=CasinoLabBootstrap.Field<DiceManager>(cp,"diceManager");var zone=CasinoLabBootstrap.Field<Transform>(dm,"diceZone");foreach(var d in CasinoLabBootstrap.Field<List<Dice>>(dm,"_diceList").Where(d=>d&&d.IsInteractable&&!CasinoLabBootstrap.Field<bool>(d,"_isRolling"))){
   await Pause(.4f);await ReachControl(c,d);var q=await PressActual(c,d);await Pause(.1f);q.invoked=c.LogicInteractor.GetComponent<PlayerInventory>().NetworkholdingItem==d;q.correct=q.reached&&q.invoked;if(!q.correct)throw new Exception("Dice pickup failed");await Aim(c,zone.position);InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});await Pause(.4f);InputSystem.QueueStateEvent(mouse,new MouseState());await Pause(.1f);var action=Singleton<InputManager>.Instance.InputHandler.InputMap.asset.FindAction("Player/Throw");var binding=action.bindings.First(b=>b.effectivePath.StartsWith("<Keyboard>/")).effectivePath.Substring(11);var key=(UnityEngine.InputSystem.Key)Enum.Parse(typeof(UnityEngine.InputSystem.Key),binding,true);InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));await Pause(.12f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Pause(.4f);
   if(c.LogicInteractor.GetComponent<PlayerInventory>().NetworkholdingItem)throw new Exception("Dice throw did not release inventory");
  }
 }
 static async UniTask FullInputRounds(ParkCasinoIntegration c){
  detailed.games=new List<InputGameCase>();detailed.diagnosis.Add("QA_SAVE="+Application.persistentDataPath+" save="+Singleton<PaperPixelGames.Core.SaveSystem.SaveManager>.Instance.SaveGame());
  var elev=NetworkSingleton<ElevatorManager>.Instance;c.HostPlayer.TeleportPlayer(elev.playerSpawnPosition.position-Vector3.up*(.4f*c.CurrentScale),true);await Pause(.2f);CasinoLabBootstrap.Field<List<Transform>>(elev,"buttonList")[4].GetComponent<InteractableEventTrigger>().ServerOnInteract(c.LogicInteractor);await Wait(()=>!elev.isTeleporting&&c.CurrentFloor==4,35);
  var outcomes=new List<(string type,long bet,long payout)>();NetworkSingleton<GameResultsManager>.Instance.OnResultRegistered+=(bet,payout,p,type,pos,a,b,d,data)=>outcomes.Add((type.ToString(),bet,payout));
  for(int i=0;i<16;i++){if(SessionState.GetBool(Key+"crapsOnly",false)&&i!=1&&i!=14)continue;
   var row=new InputGameCase{name=CasinoLabSelection.Names[i]};detailed.games.Add(row);int initial=detailed.buttons.Count;GameBase g=null;try{
    DetailedStage("ROUND_"+row.name);SceneManager.SetActiveScene(c.ModuleRoot.gameObject.scene);bool replaced=false;c.Bootstrap.StartCoroutine(ReplaceDetailed(i,()=>replaced=true));await Wait(()=>replaced,40);SceneManager.SetActiveScene(c.gameObject.scene);g=CasinoLabSelection.SelectedGame;if(!g)throw new Exception("Missing selected game");row.type=g.GameType.ToString();await StandAtPad(c,g);await VerifyPad(c,g.Keypad);
    foreach(var b in GameButtons(g))detailed.diagnosis.Add("CONTROL "+row.name+"/"+b.name+" "+Methods(b));
    if(g is Keno)foreach(var b in g.GetComponentsInChildren<KenoButton>().Take(3))await RequiredPress(c,b);
    if(g is Roulette)await RequiredPress(c,g.GetComponentsInChildren<RouletteButton>().First());
    if(g is MoneyWheel)await RequiredPress(c,g.GetComponentsInChildren<MoneyWheelButton>().First(b=>b.betOption!="Green"));
    if(g is DuckRace)await PressMethod(c,g,"ServerPlaceBet");
    if(g is HiLoGame)await PressMethod(c,g,"SetOver");
    row.before=c.WholeCoins;row.bet=g.currentBet;int outcomeCount=outcomes.Count;int turn=CasinoLabBootstrap.Field<int>(g,"gameTurn");
    if(g is Baccarat)await PressMethod(c,g,"PlaceBetOnPlayer");else await PressMethod(c,g,"TryStartGame");await Pause(.5f);row.started=g.isPlaying||CasinoLabBootstrap.Field<int>(g,"gameTurn")>turn||outcomes.Count>outcomeCount;if(!row.started)throw new Exception("Start button callback ran but game did not start; bet="+g.currentBet+" error="+g.Keypad.Network_errorMessage);
    var deadline=DateTime.UtcNow.AddSeconds(105);bool first=true;float next=0;
    while(g&&(g.isPlaying||outcomes.Count==outcomeCount)&&DateTime.UtcNow<deadline){if(Time.realtimeSinceStartup>=next){next=Time.realtimeSinceStartup+1;
      if(g is Blackjack)await PressMethod(c,g,"PlayerStand");
      if(g is Poker && CasinoLabBootstrap.Field<object>(g,"gameState").ToString()=="PlayerTurn")await PressMethod(c,g,"TryStartGame");
      if(g is Crash)await PressMethod(c,g,"Cashout");
      if(g is CrossyRoad){await PressMethod(c,g,first?"TakeStep":"CashOut");}
      if(g is DragonTower){if(first)await RequiredPress(c,g.GetComponentsInChildren<DragonTowerButton>().First(b=>CasinoLabBootstrap.Field<int>(b,"floorIndex")==0));else await PressMethod(c,g,"Cashout");}
      if(g is Minesweeper){if(first)await RequiredPress(c,g.GetComponentsInChildren<MinesweeperTile>().First());else await PressMethod(c,g,"CashOut");}
      if(g is Craps cp)await ThrowActualDice(c,cp);first=false;
    }await UniTask.Yield();}
    row.finished=!g.isPlaying;row.result=outcomes.Count>outcomeCount;if(row.result){var os=outcomes.Skip(outcomeCount).Where(x=>x.type==row.type).ToArray();row.bet=os.Sum(x=>x.bet);row.payout=os.Sum(x=>x.payout);}row.after=c.WholeCoins;row.wallet=row.result&&row.after==row.before-row.bet+row.payout&&NetworkSingleton<MoneyManager>.Instance.balance==row.after;float foot=c.ModuleRoot.InverseTransformPoint(c.HostPlayer.transform.position).y;row.grounded=foot>-.15f&&foot<1.5f;if(!row.finished||!row.result||!row.wallet||!row.grounded)throw new Exception("Round final gates failed");
   }catch(Exception e){row.error=e.ToString();if(g&&g.isPlaying){g.StopAllCoroutines();g.gameObject.SetActive(false);} }row.pressed=detailed.buttons.Count-initial;DetailedStage("DONE_"+row.name);
  }
 }
}
