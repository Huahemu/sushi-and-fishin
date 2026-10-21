using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DistantLands.Cozy;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.AI;
using PaperPixelGames.Core;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.States.Game;
using PaperPixelGames.Framework.States.MainMenu;
using SaveManager=PaperPixelGames.Core.SaveSystem.SaveManager;
using SushiAndFish.CityExpansion;
using SushiAndFish.Casino;
using SushiAndFish.Casino.Original;
using SushiAndFish.Casino.Original.Extensions;
using Object=UnityEngine.Object;

public static partial class CityExpansionPlayAudit
{
    [Serializable]public class WorldPlayReceipt
    {
        public string utc,stage,error;public bool passed,restaurantWalk,cinemaWalk,auditoriumWalk,doorSafety,portals,mapCasino,nativeShops,nativeShopsAfterElevator,groupPair,groupTrio,sushiPriority,ledgerRoundTrip,returnedToMenu,reloadedLedger;
        public int runtimePerimeterObjects,population,eligiblePopulation;public List<ChairCase> chairs=new List<ChairCase>();public List<ResidentSeatCase> residents=new List<ResidentSeatCase>();public List<string> diagnostics=new List<string>();public List<string> captures=new List<string>();public List<string> failures=new List<string>();
    }
    [Serializable]public class ChairCase {public int seat;public bool spawned,actualE,seated,animation,occupied,exit,walkAfterExit,avatarVisible;public float poseError;public string playerTarget;}
    [Serializable]public class ResidentSeatCase {public int seat,resident;public bool reserved,walked,seated,animation,released,visibleSitPose;public float poseError,thighAngle,kneeAngle;public Vector3 start,end;}
    static WorldPlayReceipt city;
    static string CityPath=>Path.Combine(Reports,"city-world-play-audit.json");
    static async UniTask CityCase(string name,Func<UniTask> action)
    {
        CityStage(name);
        try{await action();}
        catch(Exception e){city.failures.Add(name+": "+e);CityStage(name+"_FAILED");}
    }
    static void CityStage(string stage){city.stage=stage;File.WriteAllText(CityPath,JsonUtility.ToJson(city,true));Stage("WORLD_"+stage);}
    static Transform CityObject(string name)=>Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Single(t=>t.name==name);
    static async UniTask Snapshot(string name)
    {
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
        var camera=CasinoRuntimeContext.PlayerCamera;
        var overlays=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.enabled&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var modes=overlays.Select(c=>(c.renderMode,c.worldCamera,c.planeDistance)).ToArray();
        var rt=RenderTexture.GetTemporary(1440,900,24,RenderTextureFormat.ARGB32);
        var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);var old=camera.targetTexture;var active=RenderTexture.active;
        try
        {
            foreach(var c in overlays){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=camera.nearClipPlane+.2f;}
            OriginalNativeShaderRestoration.ApplyPending();RecoveryShaderSupport.BeginRendering();
            Canvas.ForceUpdateCanvases();
            var request=new RenderPipeline.StandardRequest{destination=rt};
            if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new Exception("The host URP does not support full camera stack capture.");
            RenderPipeline.SubmitRenderRequest(camera,request);
            RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1440,900),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(Reports,name),texture.EncodeToPNG());
        }
        finally
        {
            for(int i=0;i<overlays.Length;i++)if(overlays[i]){overlays[i].renderMode=modes[i].renderMode;overlays[i].worldCamera=modes[i].worldCamera;overlays[i].planeDistance=modes[i].planeDistance;}
            camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(texture);RecoveryShaderSupport.EndRendering();
        }
        city.captures.Add(name);
    }
    static async UniTask MoveToward(ParkCasinoIntegration casino,Vector3 destination,float seconds)
    {
        var player=casino.HostPlayer;await Aim(casino,destination+Vector3.up*1.55f);
        float until=Time.realtimeSinceStartup+seconds;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.W));
        while(Time.realtimeSinceStartup<until&&Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),new Vector2(destination.x,destination.z))>.25f)await UniTask.Yield();
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Pause(.15f);player.FirstPersonCharacterInput.InputMove=Vector2.zero;player.FirstPersonCharacter.SetVelocity(Vector3.zero);
    }
    static async UniTask<bool> DoorWalk(ParkCasinoIntegration casino,Transform building,string picture)
    {
        var door=building.GetComponent<CityAutomaticDoor>();var player=casino.HostPlayer;
        Vector3 center=door.doorway.position;Vector3 front=-building.forward;
        player.TeleportPlayer(center+front*3.3f+Vector3.up*.15f,true);player.FirstPersonCharacter.SetVelocity(Vector3.zero);await Pause(.4f);
        await MoveToward(casino,center-front*1.45f,2.5f);
        bool entered=building.InverseTransformPoint(player.transform.position).z>building.InverseTransformPoint(center).z+1;
        await Pause(.9f);city.doorSafety|=door.IsOpen;
        await Snapshot(picture);
        await MoveToward(casino,center+front*4.2f,3.2f);await Pause(1.9f);
        var nearMethod=typeof(CityAutomaticDoor).GetMethod("Near",BindingFlags.Instance|BindingFlags.NonPublic);
        var nearby=Singleton<ResidentManager>.Instance.Residents.Where(r=>r&&r.gameObject.activeInHierarchy&&(bool)nearMethod.Invoke(door,new object[]{r.transform.position})).ToArray();
        var closeDeadline=Time.unscaledTime+6;
        while(door.Openness>=.02f&&!(nearby.Length>0&&door.IsOpen)&&Time.unscaledTime<closeDeadline){await Pause(.10f);nearby=Singleton<ResidentManager>.Instance.Residents.Where(r=>r&&r.gameObject.activeInHierarchy&&(bool)nearMethod.Invoke(door,new object[]{r.transform.position})).ToArray();}
        city.diagnostics.Add(building.name+" entered="+entered+" rootFeet="+building.InverseTransformPoint(player.transform.position)+" openAfterAway="+door.Openness+" nearbyNPCs="+string.Join(",",nearby.Select(r=>r.ResidentName)));
        return entered&&(door.Openness<.02f||nearby.Length>0&&door.IsOpen);
    }
    static async UniTask AimBasic(ParkCasinoIntegration casino,Vector3 point)
    {
        await Aim(casino,point);await Pause(.18f);
    }
    static async UniTask PlayerChairs(ParkCasinoIntegration casino,CityDiningSeat[] seats)
    {
        var player=casino.HostPlayer;
        foreach(var seat in seats)
        {
            CityStage("PLAYER_SEAT_"+seat.seatId);
            var row=new ChairCase{seat=seat.seatId,spawned=seat.Area&&seat.Area.IsSpawned};city.chairs.Add(row);
            player.TeleportPlayer(seat.approach.position+Vector3.up*.05f,true);player.FirstPersonCharacter.SetVelocity(Vector3.zero);await Pause(.3f);
            var sphere=seat.GetComponent<SphereCollider>();await AimBasic(casino,sphere.transform.TransformPoint(sphere.center));
            var target=CityExpansionInspection.Field<IBasicInteractable>(player.Interactor,"_currentBasicInteractable");
            row.playerTarget=target?.GetType().Name;row.actualE=ReferenceEquals(target,seat.Area);
            await KeyE();await Pause(.5f);
            row.seated=player.IsUsingEmoteArea&&player.CurrentEmoteArea==seat.Area;row.occupied=seat.Area.IsOccupied;
            row.poseError=Vector3.Distance(player.transform.position,seat.seatedPose.position);
            var animator=CityExpansionInspection.Field<Animator>(player,"_tpsAnimator");row.animation=animator&&animator.GetBool(PPG_Player.IS_EMOTE_STATIC);
            row.avatarVisible=animator&&animator.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.enabled&&!r.forceRenderingOff&&(CasinoRuntimeContext.PlayerCamera.cullingMask&(1<<r.gameObject.layer))!=0);
            if(seat.seatId==0||seat.seatId==11)await Snapshot("city-player-seat-"+seat.seatId+".png");
            player.EndStaticEmote();await Pause(.8f);
            row.exit=!player.IsUsingEmoteArea&&!seat.Area.IsOccupied&&Vector3.Distance(player.transform.position,seat.approach.position)<.35f;
            var before=player.transform.position;await MoveToward(casino,CityObject("AR_Restaurant").TransformPoint(new Vector3(0,.05f,-3.3f)),1);
            row.walkAfterExit=Vector3.Distance(before,player.transform.position)>.25f;
            CityStage(city.stage);
            if(!row.actualE||!row.seated||!row.exit)throw new Exception("Native player chair interaction failed on "+seat.seatId);
        }
    }
    static ResidentController[] Eligible(TownMealScheduler scheduler)=>Singleton<ResidentManager>.Instance.Residents.Where(scheduler.IsEligible).OrderBy(r=>Vector3.Distance(r.transform.position,CityObject("AR_Restaurant").position)).ToArray();
    static async UniTask ResidentChairs(TownMealScheduler scheduler,CityDiningSeat[] seats)
    {
        var pool=Eligible(scheduler);city.eligiblePopulation=pool.Length;
        if(pool.Length==0)throw new Exception("No eligible town resident at the meal time.");
        var npc=pool[0];
        foreach(var seat in seats)
        {
            CityStage("NPC_SEAT_"+seat.seatId);
            npc.Brain.RequestScheduleChange();npc.Brain.Movement.Warp(new Vector3(66,3.1f,-13),Quaternion.Euler(0,90,0));npc.Runtime.IsAvailable=true;
            var row=new ResidentSeatCase{seat=seat.seatId,resident=npc.ResidentId,start=npc.transform.position};city.residents.Add(row);
            row.reserved=seat.TryReserve(npc);
            if(!row.reserved)throw new Exception("Native seat reservation failed.");
            var state=new ResidentTownMealState(npc,seat,scheduler,scheduler.CurrentDay,25);
            npc.Brain.StateMachine.ChangeState(state,ResidentRequestPriority.ScheduledActivity);
            await Wait(()=>state.Seated,40);await Pause(.5f);
            row.seated=ReferenceEquals(npc.Brain.StateMachine.Current,state)&&npc.Runtime.HasArrived;
            row.end=npc.transform.position;row.walked=Vector3.Distance(row.start,row.end)>2;row.poseError=Vector3.Distance(row.end,seat.seatedPose.position);
            var animator=CityExpansionInspection.Field<Animator>(npc,"_animator");row.animation=animator&&animator.GetBool(Animator.StringToHash("Sit"));
            if(animator&&animator.isHuman)
            {
                var upper=animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);var lower=animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);var foot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                row.thighAngle=Vector3.Angle(lower.position-upper.position,Vector3.down);row.kneeAngle=Vector3.Angle(upper.position-lower.position,foot.position-lower.position);
                row.visibleSitPose=row.thighAngle>45&&row.kneeAngle<140;
            }
            if(seat.seatId==0||seat.seatId==11){await ObserveMeal(ParkCasinoIntegration.Instance,new[]{seat});await Snapshot("city-npc-seat-"+seat.seatId+".png");}
            npc.Brain.RequestScheduleChange();await Pause(.15f);row.released=!seat.Area.IsOccupied&&!seat.Resident;
            if(!row.seated||!row.released)throw new Exception("Resident seating lifecycle failed on "+seat.seatId);
            CityStage(city.stage);
        }
    }
    static async UniTask ObserveMeal(ParkCasinoIntegration casino,CityDiningSeat[] seats)
    {
        var restaurant=CityObject("AR_Restaurant");
        var point=seats.Aggregate(Vector3.zero,(sum,s)=>sum+s.seatedPose.position)/seats.Length;
        var local=restaurant.InverseTransformPoint(point);
        var stand=restaurant.TransformPoint(new Vector3(0,0,seats.All(s=>s.barStool)?-2.3f:Mathf.Clamp(local.z,-2.5f,-.5f)));
        casino.HostPlayer.TeleportPlayer(stand+Vector3.up*.04f,true);casino.HostPlayer.FirstPersonCharacter.SetVelocity(Vector3.zero);await Pause(.25f);await Aim(casino,point+Vector3.up*1.1f);
    }
    static async UniTask GroupsAndSushi(TownMealScheduler scheduler,CityDiningSeat[] seats)
    {
        CityStage("PAIRS_AND_TRIOS");
        var manager=Singleton<ResidentManager>.Instance;var clock=Singleton<GameProgressManager>.Instance.CozyWeatherManager;
        foreach(int size in new[]{2,3})
        {
            TownMealScheduler.RestoreLedger(JsonUtility.ToJson(new TownMealScheduler.Ledger{day=clock.CurrentDay}));
            var party=Eligible(scheduler).Take(size).ToArray();
            if(party.Length!=size)throw new Exception("Insufficient eligible residents for the group test: "+size);
            CityDiningSeat[] chosen=size==2?seats.Where(s=>!s.barStool).GroupBy(s=>s.tableId).First(g=>g.All(s=>s.IsFree)).ToArray():seats.Where(s=>s.barStool&&s.IsFree).Take(3).ToArray();
            if(!scheduler.TryStartParty(party,chosen,25))throw new Exception("Production party dispatch failed: "+size);
            await Wait(()=>party.All(n=>n.Brain.StateMachine.Current is ResidentTownMealState m&&m.Seated),70);
            if(size==2)city.groupPair=true;else city.groupTrio=true;
            await ObserveMeal(ParkCasinoIntegration.Instance,chosen);await Snapshot("city-meal-group-"+size+".png");
            if(size==3)
            {
                // Opening the original sushi shop must free every optional
                // diner before the original customer allocator next runs.
                var queue=CityExpansionInspection.Field<HashSet<ResidentController>>(manager,"_restaurantQueuedResidents");
                var before=queue.OrderBy(r=>r.ResidentId).ToArray();
                scheduler.sushiRestaurant.IsRestaurantOpen=true;
                int parties=scheduler.StartedParties;
                scheduler.ObserveTime(clock.CurrentDay,Math.Max(scheduler.Minute,1000));
                var after=queue.OrderBy(r=>r.ResidentId).ToArray();
                city.sushiPriority=scheduler.ActiveVisits.Count==0&&party.All(n=>!(n.Brain.StateMachine.Current is ResidentTownMealState))&&before.SequenceEqual(after)&&scheduler.StartedParties==parties&&chosen.All(s=>!s.Area.IsOccupied);
                scheduler.sushiRestaurant.IsRestaurantOpen=false;
                if(!city.sushiPriority)throw new Exception("The optional dining activity interfered with sushi priority.");
            }
            scheduler.CancelAllVisits();await Pause(.3f);
        }
        var saved=Singleton<GameProgressManager>.Instance.Save();string ledger=TownMealScheduler.CaptureLedger();
        var restored=JsonUtility.FromJson<GameProgressData>(JsonUtility.ToJson(saved));TownMealScheduler.RestoreLedger(restored.TownDiningLedger);
        city.ledgerRoundTrip=ledger==TownMealScheduler.CaptureLedger()&&!string.IsNullOrEmpty(ledger);
        city.diagnostics.Add("Meal ledger "+ledger);
    }
    static async UniTask Portals(ParkCasinoIntegration casino)
    {
        var player=casino.HostPlayer;var portals=Object.FindObjectsByType<CityCinemaPortal>(FindObjectsSortMode.None);
        var enter=portals.Single(p=>p.name=="EnterAuditorium");var back=portals.Single(p=>p.name=="ReturnToCinemaLobby");
        var lobbyArrival=enter.transform.position-CityObject("AR_CinemaLobby").forward*1.2f-Vector3.up*1.15f;
        player.TeleportPlayer(lobbyArrival+Vector3.up*.05f,true);await Pause(.3f);await AimBasic(casino,enter.transform.position);await KeyE();await Pause(.4f);
        bool inside=Vector3.Distance(player.transform.position,enter.destination.position)<.2f;
        city.diagnostics.Add("AUDITORIUM enteredPosition="+player.transform.position+" expected="+enter.destination.position+" basic="+CityExpansionInspection.Field<IBasicInteractable>(player.Interactor,"_currentBasicInteractable")?.GetType().Name);
        await Snapshot("city-cinema-auditorium.png");
        if(inside)
        {
            var start=player.transform.position;await MoveToward(casino,start+Vector3.back*10,4.5f);
            city.auditoriumWalk=start.z-player.transform.position.z>7&&Physics.RaycastAll(player.transform.position+Vector3.up*.4f,Vector3.down,.8f,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.transform.IsChildOf(CityObject("AR_CinemaAuditorium")));
            await Snapshot("city-cinema-aisle-walk.png");
            player.TeleportPlayer(enter.destination.position,enter.destination.rotation,true);player.FirstPersonCharacter.SetVelocity(Vector3.zero);await Pause(.4f);
        }
        await AimBasic(casino,back.transform.position);await KeyE();await Pause(.4f);
        bool returned=Vector3.Distance(player.transform.position,back.destination.position)<.2f;
        city.diagnostics.Add("AUDITORIUM returnPosition="+player.transform.position+" expected="+back.destination.position+" basic="+CityExpansionInspection.Field<IBasicInteractable>(player.Interactor,"_currentBasicInteractable")?.GetType().Name);
        city.portals=inside&&returned;city.diagnostics.Add("Cinema portal enter="+inside+" return="+returned);
        if(!city.portals)throw new Exception("Cinema inner hall portal failed.");
    }
    static async UniTask MapAndShops(ParkCasinoIntegration casino)
    {
        var player=casino.HostPlayer;var tablet=player.GetComponentInChildren<TabletController>(true);
        var app=player.GetComponentInChildren<MapApp>(true);
        city.diagnostics.Add("Map app "+app?.name+" tablet="+tablet?.name);
        // Exercise the real MapApp through its normal Show lifecycle.
        var action=Singleton<PaperPixelGames.Framework.Input.InputManager>.Instance.InputHandler.InputMap.asset.FindAction("Map",true);
        var binding=action.bindings.First(b=>b.effectivePath.StartsWith("<Keyboard>/")).effectivePath.Substring(11);
        var key=(UnityEngine.InputSystem.Key)Enum.Parse(typeof(UnityEngine.InputSystem.Key),binding,true);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));await Pause(.09f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Wait(()=>tablet.IsTabletActive,8);await Pause(.7f);
        var live=app.GetComponent<LiveSceneTabletMap>();
        live.MapCamera.Render();await Pause(.15f);
        var labels=app.GetComponentsInChildren<TMPro.TMP_Text>(true);
        city.mapCasino=live&&live.RenderedFrames>0&&labels.Any(t=>t.name=="Caption"&&t.text=="CASINO"&&t.gameObject.activeInHierarchy)&&!labels.Any(t=>t.gameObject.activeInHierarchy&&string.Equals(t.text.Trim(),"Park",StringComparison.OrdinalIgnoreCase));
        city.diagnostics.Add("MAP visible text: "+string.Join(" | ",labels.Where(t=>t.gameObject.activeInHierarchy).Select(t=>t.text)));
        await CaptureTabletView(tablet,"city-live-map-casino.png");
        DumpTexture(live.Texture,"city-live-map-world-raw.png");city.captures.Add("city-live-map-world-raw.png");
        tablet.CloseTablet();await Pause(.25f);
        city.nativeShops=await NativeShops(casino,false);
    }
    static async UniTask<bool> NativeShops(ParkCasinoIntegration casino,bool afterElevator)
    {
        var player=casino.HostPlayer;
        if(afterElevator)
        {
            var clock=Singleton<GameProgressManager>.Instance.CozyWeatherManager;
            clock.ClientPauseTime();clock.ClientSetTime(12,0);clock.TriggerBlackBoardTimeEvents(true);await Pause(1.2f);
        }
        var panel=Singleton<GameUIManager>.Instance.DialoguePanelController;bool both=true;int shopCount=0;
        foreach(var npc in Singleton<ResidentManager>.Instance.Residents.Where(r=>r&&r.ResidentData&&!string.IsNullOrEmpty(r.ResidentData.Name)&&(r.ResidentData.Name.Contains("Marina")||r.ResidentData.Name.Contains("Gabe"))))
        {
            shopCount++;
            CityStage("NATIVE_SHOP_"+npc.ResidentData.Name);
            // Use the resident's own interaction, billboard and database, and
            // confirm custom elevator buttons never enter the shop menu.
            player.TeleportPlayer(npc.transform.position+npc.transform.forward*1.5f,true);await Pause(.3f);await AimBasic(casino,npc.transform.position+Vector3.up*1.2f);
            await KeyE();await Pause(.7f);
            bool shown=panel.IsVisible&&!panel.HasCustomConversation&&panel.ConversationButtons.All(b=>!b.gameObject.activeSelf);
            both&=shown;city.diagnostics.Add("SHOP "+npc.ResidentData.Name+" nativePanel="+shown+" runtime="+npc.Brain.StateMachine.CurrentName);
            if(shown){await Snapshot("city-native-shop-"+npc.ResidentData.NpcID+(afterElevator?"-after-elevator":"")+".png");panel.Hide();}
        }
        return both&&shopCount==2;
    }
    static async UniTask World(ParkCasinoIntegration casino)
    {
        city=new WorldPlayReceipt{utc=DateTime.UtcNow.ToString("o")};
        var scheduler=Object.FindFirstObjectByType<TownMealScheduler>();
        var clock=Singleton<GameProgressManager>.Instance.CozyWeatherManager;
        var weather=CityExpansionInspection.Field<CozyWeather>(clock,"_cozyWeather");
        bool pausedBefore=weather.timeModule.perennialProfile.pauseTime;
        bool sushiBefore=scheduler&&scheduler.sushiRestaurant.IsRestaurantOpen;
        try
        {
            if(!scheduler)throw new Exception("Saved city scheduler missing.");scheduler.enabled=false;
            scheduler.sushiRestaurant.IsRestaurantOpen=false;
            clock.ClientPauseTime();clock.ClientSetTime(12,0);clock.TriggerBlackBoardTimeEvents(true);
            await Pause(2);
            var perimeter=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Single(t=>t.name=="RestoredIslandPerimeter_20261021");city.runtimePerimeterObjects=perimeter.childCount;
            city.population=Singleton<ResidentManager>.Instance.Residents.Where(r=>r&&r.IsSpawned).Select(r=>r.ResidentId).Distinct().Count();
            var restaurant=CityObject("AR_Restaurant");var cinema=CityObject("AR_CinemaLobby");
            await CityCase("RESTAURANT_DOOR_WALK",async()=>{city.restaurantWalk=await DoorWalk(casino,restaurant,"city-restaurant-entry.png");});
            await CityCase("CINEMA_DOOR_WALK",async()=>{city.cinemaWalk=await DoorWalk(casino,cinema,"city-cinema-entry.png");});
            await CityCase("CINEMA_PORTALS",()=>Portals(casino));
            var seats=Object.FindObjectsByType<CityDiningSeat>(FindObjectsSortMode.None).OrderBy(s=>s.seatId).ToArray();
            if(seats.Length!=12||seats.Any(s=>!s.Area.IsSpawned))throw new Exception("All 12 native seat NetworkObjects must spawn.");
            await CityCase("PLAYER_CHAIRS",()=>PlayerChairs(casino,seats));
            if(casino.HostPlayer.IsUsingEmoteArea){casino.HostPlayer.EndStaticEmote();await Pause(.8f);}
            if(Singleton<GameUIManager>.Instance.DialoguePanelController.IsVisible)Singleton<GameUIManager>.Instance.DialoguePanelController.Hide();
            await CityCase("NPC_CHAIRS",()=>ResidentChairs(scheduler,seats));
            await CityCase("GROUPS_AND_SUSHI",()=>GroupsAndSushi(scheduler,seats));
            await CityCase("MAP_AND_SHOPS",()=>MapAndShops(casino));
            await CityCase("RUNTIME_CITY_VIEWS",()=>RuntimeCityViews(casino));
            city.passed=city.restaurantWalk&&city.cinemaWalk&&city.auditoriumWalk&&city.doorSafety&&city.portals&&city.mapCasino&&city.nativeShops&&city.groupPair&&city.groupTrio&&city.sushiPriority&&city.ledgerRoundTrip&&city.runtimePerimeterObjects==565&&city.chairs.Count==12&&city.chairs.All(s=>s.spawned&&s.actualE&&s.seated&&s.animation&&s.occupied&&s.exit&&s.walkAfterExit&&s.avatarVisible&&s.poseError<.15f)&&city.residents.Count==12&&city.residents.All(s=>s.reserved&&s.walked&&s.seated&&s.animation&&s.released&&s.visibleSitPose&&s.poseError<.08f);
            CityStage(city.passed?"WORLD_PASS":"WORLD_FAILED");
            if(!city.passed)throw new Exception("One or more city runtime gates failed: "+string.Join(" | ",city.failures));
        }
        catch(Exception e){city.error=e.ToString();city.passed=false;CityStage("FAILED");throw;}
        finally
        {
            if(scheduler){scheduler.CancelAllVisits();scheduler.sushiRestaurant.IsRestaurantOpen=sushiBefore;}
            if(pausedBefore)clock.ClientPauseTime();else clock.ClientResumeTime();
            File.WriteAllText(CityPath,JsonUtility.ToJson(city,true));
        }
    }
    static async UniTask CityRoundTrip()
    {
        var save=Singleton<SaveManager>.Instance;int slot=save.CurrentSaveSlotIndex;string ledger=TownMealScheduler.CaptureLedger();
        save.SaveHostSession();
        await CleanupHost();city.returnedToMenu=true;
        SushiAndFish.FrontEnd.IslandFrontEndFlow.Active.ShowLoadGames();await Pause(1);
        Resources.FindObjectsOfTypeAll<SaveSlot>().Single(s=>s&&s.gameObject.scene.name=="MainMenuState"&&s.ContainsSave&&s.SlotIndex==slot).LaunchOriginalSlot();
        await Wait(()=>(bool)Prior("Ready"),240);await Wait(()=>ParkCasinoIntegration.Instance&&ParkCasinoIntegration.Instance.Ready,180);
        city.reloadedLedger=TownMealScheduler.Instance&&TownMealScheduler.CaptureLedger()==ledger&&Object.FindObjectsByType<CityDiningSeat>(FindObjectsSortMode.None).All(s=>s.Area.IsSpawned);
        city.passed&=city.reloadedLedger;
        File.WriteAllText(CityPath,JsonUtility.ToJson(city,true));
        if(!city.reloadedLedger)throw new Exception("Town dining ledger did not survive a real save/menu/reload.");
    }
    static async UniTask CleanupHost()
    {
        if(ParkCasinoIntegration.Instance&&Singleton<GameSessionLifecycle>.TryGetInstance(out var lifecycle))
        {
            lifecycle.ReturnToMainMenu(ReturnToMainMenuRequest.UserRequested());
            await Wait(()=>SushiAndFish.FrontEnd.IslandFrontEndFlow.Active&&SushiAndFish.FrontEnd.IslandFrontEndFlow.Active.BackdropLoaded&&!lifecycle.ReturnToMenuInProgress,120);
            await Pause(1);
        }
    }
}
