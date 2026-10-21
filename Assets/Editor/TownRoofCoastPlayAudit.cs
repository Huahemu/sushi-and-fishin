using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Reflection;using Cysharp.Threading.Tasks;using UnityEngine;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;using UnityEngine.EventSystems;using UnityEditor;using PaperPixelGames.Core.Managers;using PaperPixelGames.Core.Tablet;using PaperPixelGames.Framework.Patterns;using PaperPixelGames.Framework.Input;using SushiAndFish.Casino;using SushiAndFish.Casino.Original;using SushiAndFish.Casino.Original.CasinoLab;using SushiAndFish.Casino.Original.Extensions;using SushiAndFish.CityExpansion;using Object=UnityEngine.Object;
public static partial class CityExpansionPlayAudit {
 [Serializable]public class CoastWalkCase {public float upperX,lowerX;public bool down,up,grounded;public Vector3 downEnd,upEnd;}
 [Serializable]public class CoastRevisionReceipt {public string utc,error,stage;public bool passed,ready,restaurantDoor,cinemaDoor,counterBlocks,portals,auditoriumWalk,tabletOpened,tabletCursor,tabletMapClick,shoreFalls,lowBalanceMinValid,lowBalanceGameStarted;public float counterStartX,counterEndX,shoreStartY,shoreEndY;public List<CoastSupportPoint> shoreSamples=new List<CoastSupportPoint>();public int phantomSupports;public int runtimeAddedCoast;public long coins,minimum;public List<CoastWalkCase> stairs=new List<CoastWalkCase>();public List<string> diagnostics=new List<string>();public List<string> captures=new List<string>();}
 static CoastRevisionReceipt coast;
 static string CoastPath=>Path.Combine(Reports,"town-roof-coast-play-audit.json");
 public static void RoofsAndCoast(){SessionState.SetBool(Key+"detailed",false);SessionState.SetBool(Key+"fullInput",false);SessionState.SetBool(Key+"roofCoast",true);SessionState.SetBool(Key+"probe",false);StartMode(false);}
 static void CoastStage(string s){coast.stage=s;coast.captures=city?.captures??new List<string>();File.WriteAllText(CoastPath,JsonUtility.ToJson(coast,true));Stage("ROOF_COAST_"+s);}
 static async UniTask CoastCase(string name,Func<UniTask> run){CoastStage(name);try{await run();}catch(Exception e){coast.diagnostics.Add(name+": "+e);CoastStage(name+"_FAILED");}}
 static async UniTask CoastView(ParkCasinoIntegration c,string name,Vector3 stand,Vector3 look){c.HostPlayer.TeleportPlayer(stand,true);c.HostPlayer.FirstPersonCharacter.SetVelocity(Vector3.zero);await Pause(.3f);await Aim(c,look);await Snapshot("town-revision-"+name+".png");}
 static bool FeetSupport(ParkCasinoIntegration c)=>Physics.RaycastAll(c.HostPlayer.transform.position+Vector3.up*.2f,Vector3.down,.65f,~0,QueryTriggerInteraction.Ignore).Any(h=>!h.transform.IsChildOf(c.HostPlayer.transform)&&h.normal.y>.3f);
 static async UniTask CoastStair(ParkCasinoIntegration c,float upper,float lower,int index){
  var w=new CoastWalkCase{upperX=upper,lowerX=lower};coast.stairs.Add(w);c.HostPlayer.TeleportPlayer(new Vector3(upper,3.12f,7.6f),true);c.HostPlayer.FirstPersonCharacter.SetVelocity(Vector3.zero);await Pause(.3f);
  foreach(var point in new[]{new Vector3(upper,3.1f,10.8f),new Vector3(lower,3.1f,13.6f),new Vector3(lower,2.1f,19.6f),new Vector3(lower,-.6f,26.9f)})await MoveToward(c,point,3.1f);
  w.downEnd=c.HostPlayer.transform.position;w.down=w.downEnd.z>25.8f&&w.downEnd.y<1.2f;w.grounded=FeetSupport(c);
  await Aim(c,new Vector3(upper,3.2f,9));await Snapshot("town-revision-north-stairs-"+index+"-up-view.png");
  foreach(var point in new[]{new Vector3(lower,2.1f,19.4f),new Vector3(lower,3.1f,13.4f),new Vector3(upper,3.1f,10.7f),new Vector3(upper,3.1f,7.6f)})await MoveToward(c,point,3.1f);
  w.upEnd=c.HostPlayer.transform.position;w.up=w.upEnd.z<8.8f&&w.upEnd.y>2.8f&&w.upEnd.y<3.3f;w.grounded&=FeetSupport(c);await Aim(c,new Vector3(lower,1.2f,23));await Snapshot("town-revision-north-stairs-"+index+"-down-view.png");
 }
 static async UniTask RoofCoast(ParkCasinoIntegration c){
  coast=new CoastRevisionReceipt{utc=DateTime.UtcNow.ToString("o"),ready=true};city=new WorldPlayReceipt{utc=coast.utc};
  try{
   var clock=Singleton<GameProgressManager>.Instance.CozyWeatherManager;clock.ClientPauseTime();clock.ClientSetTime(12,0);clock.TriggerBlackBoardTimeEvents(true);var scheduler=Object.FindFirstObjectByType<TownMealScheduler>();if(scheduler)scheduler.enabled=false;await Pause(1);
   if(SessionState.GetBool(Key+"environment",false))await EnvironmentInspection(c);
   coast.runtimeAddedCoast=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Single(t=>t.name=="RestoredCoastConnections_20261021").childCount;
   await CoastCase("EXTERIOR_VIEWS",async()=>{
    await CoastView(c,"restaurant-roof",new Vector3(57.8f,3.10f,-20.0f),new Vector3(72,6.2f,-13));
    await CoastView(c,"casino-front",new Vector3(-45,3.1f,-27.5f),new Vector3(-90,7,-27.5f));
    await CoastView(c,"cinema-two-storeys",new Vector3(105.5f,3.10f,-63.0f),new Vector3(88,8.0f,-52.5f));
   });
   await CoastCase("RESTAURANT_DOOR",async()=>{coast.restaurantDoor=await DoorWalk(c,CityObject("AR_Restaurant"),"town-revision-restaurant-door.png");});
   await CoastCase("CINEMA_DOOR",async()=>{coast.cinemaDoor=await DoorWalk(c,CityObject("AR_CinemaLobby"),"town-revision-cinema-door.png");});
   await CoastCase("COUNTER_COLLISION",async()=>{
    c.HostPlayer.TeleportPlayer(new Vector3(87.5f,3.08f,-55.5f),true);c.HostPlayer.FirstPersonCharacter.SetVelocity(Vector3.zero);await Pause(.4f);coast.counterStartX=c.HostPlayer.transform.position.x;await MoveToward(c,new Vector3(84.4f,3.08f,-55.5f),2.1f);coast.counterEndX=c.HostPlayer.transform.position.x;coast.counterBlocks=coast.counterEndX>86.12f&&coast.counterEndX<coast.counterStartX-.15f;await Aim(c,new Vector3(85.7f,4,-55.5f));await Snapshot("town-revision-cinema-counter-block.png");
   });
   await CoastCase("CINEMA_INNER_HALL",async()=>{await Portals(c);coast.portals=city.portals;coast.auditoriumWalk=city.auditoriumWalk;});
   int i=0;foreach(var q in new[]{(19.1f,19.1f),(9.1f,7.59f),(-.9f,.78f),(-10.9f,-10.9f)}){int n=++i;await CoastCase("NORTH_STAIRS_"+n,()=>CoastStair(c,q.Item1,q.Item2,n));}
   await CoastCase("COAST_VIEWS",async()=>{
    await CoastView(c,"east-coast-connection",new Vector3(108.9f,3.16f,-33),new Vector3(114.4f,2.5f,-14.25f));
    await CoastView(c,"south-coast-rails",new Vector3(47.0f,3.15f,-74),new Vector3(25,2.5f,-80));
    await CoastView(c,"north-east-beach-steps",new Vector3(88.5f,3.16f,27.5f),new Vector3(83.0f,-1.5f,35.5f));
   });
   await CoastCase("SHORE_GRAVITY",()=>ShorePhysics(c));
   await CoastCase("TABLET_POINTER",async()=>{
    c.HostPlayer.TeleportPlayer(new Vector3(66,3.1f,-13),true);c.HostPlayer.FirstPersonCharacter.SetVelocity(Vector3.zero);InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.Tab));await Pause(.12f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Pause(.9f);var tab=c.HostPlayer.GetComponentInChildren<TabletController>(true);coast.tabletOpened=tab.IsTabletActive;coast.tabletCursor=Cursor.visible&&Cursor.lockState==CursorLockMode.None;
    if(coast.tabletOpened){var button=CityExpansionInspection.Field<PaperPixelGames.Framework.UI.BaseButton>(tab,"_mapAppButton");var rect=button.GetComponent<RectTransform>();var canvas=tab.GetComponent<Canvas>();var screen=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,rect.TransformPoint(rect.rect.center));InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});await Pause(.15f);InputSystem.QueueStateEvent(mouse,new MouseState{position=screen,buttons=1});await Pause(.12f);InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});await Pause(.5f);coast.tabletMapClick=CityExpansionInspection.Field<object>(tab,"_currentActiveApp") is MapApp;coast.diagnostics.Add("Tablet click screen="+screen+" camera="+canvas.worldCamera.name+" cursor="+Cursor.visible+" "+Cursor.lockState);await CaptureTabletView(tab,"town-revision-tablet-pointer.png");InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.Escape));await Pause(.12f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Pause(.7f);if(tab.IsTabletActive){InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.Escape));await Pause(.12f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Pause(.5f);}}
   });
   await CoastCase("LOW_BALANCE_MINIMUM",async()=>{
    c.HostEconomy.SetSavedGoldBalance(9000,"9000",c.HostEconomy.ScrapBalance.Value);coast.coins=c.WholeCoins;var elevator=NetworkSingleton<ElevatorManager>.Instance;var button=CasinoLabBootstrap.Field<List<Transform>>(elevator,"buttonList")[1].GetComponent<InteractableEventTrigger>();c.HostPlayer.TeleportPlayer(elevator.playerSpawnPosition.position-Vector3.up*(.4f*c.CurrentScale),true);await Pause(.2f);button.ServerOnInteract(c.LogicInteractor);await Pause(.1f);await Wait(()=>!elevator.isTeleporting&&c.Bootstrap.ready,30);
    var pad=c.ModuleRoot.GetComponentsInChildren<Keypad>(false).FirstOrDefault(k=>k.NetworkcasinoGame&&k.NetworkcasinoGame.MinBet<=90&&(k.NetworkcasinoGame is Slots||k.NetworkcasinoGame is Plinko||k.NetworkcasinoGame is Blackjack||k.NetworkcasinoGame is WheelOfFortune||k.NetworkcasinoGame is Crash));if(!pad)throw new Exception("No affordable simple keypad active on floor1");await StandAtPad(c,pad.NetworkcasinoGame);var minButton=pad.GetComponentsInChildren<InteractableEventTrigger>().First(b=>HasMethod(b,"SetBetMinimum"));await ReachControl(c,minButton);if(c.LogicInteractor.TargetInteractable!=minButton)throw new Exception("Low balance minimum button not targeted");await KeyE();coast.minimum=pad.GetCurrentInput();coast.lowBalanceMinValid=coast.minimum>0&&coast.minimum<=90&&string.IsNullOrEmpty(pad.Network_errorMessage);var playButton=GameButtons(pad.NetworkcasinoGame).First(b=>HasMethod(b,"TryStartGame"));await ReachControl(c,playButton);if(c.LogicInteractor.TargetInteractable!=playButton)throw new Exception("Low balance play button not targeted");await KeyE();coast.lowBalanceGameStarted=pad.NetworkcasinoGame.isPlaying||NetworkSingleton<MoneyManager>.Instance.balance==90-coast.minimum;coast.diagnostics.Add("90coin test through actual E: "+pad.NetworkcasinoGame.GameType+" min="+coast.minimum+" balance="+NetworkSingleton<MoneyManager>.Instance.balance);
   });
   coast.diagnostics.AddRange(city.diagnostics);coast.passed=coast.tabletOpened&&coast.tabletCursor&&coast.tabletMapClick&&coast.shoreFalls&&coast.lowBalanceMinValid&&coast.lowBalanceGameStarted&&coast.ready&&coast.runtimeAddedCoast>=365&&coast.restaurantDoor&&coast.cinemaDoor&&coast.counterBlocks&&coast.portals&&coast.auditoriumWalk&&coast.stairs.Count==4&&coast.stairs.All(s=>s.down&&s.up&&s.grounded);CoastStage(coast.passed?"PASS":"FAILED");
  }catch(Exception e){coast.error=e.ToString();CoastStage("FAILED");throw;}
 }
}
