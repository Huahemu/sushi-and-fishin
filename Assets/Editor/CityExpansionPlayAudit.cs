using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.States.MainMenu;
using PaperPixelGames.Core.SaveSystem;
using PlayerSettings=UnityEditor.PlayerSettings;
using SushiAndFish.FrontEnd;
using SushiAndFish.Casino;
using SushiAndFish.Casino.Original;
using SushiAndFish.Casino.Original.CasinoLab;
using SushiAndFish.Casino.Original.Extensions;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static partial class CityExpansionPlayAudit
{
    const string Key="SushiAndFish.CityExpansionAudit.";
    static readonly List<string> errors=new List<string>();
    [Serializable] public class Receipt {public string utc,stage,error,profile;public bool passed,nativePanel,actualE,inputRestored;public List<Page> pages=new List<Page>();public List<Trip> trips=new List<Trip>();public List<string> diagnostics=new List<string>();public List<string> captures=new List<string>();public string[] errors;}
    [Serializable] public class Page {public int floor;public string text;public string[] choices;public bool exact,allFonts,insideScreen,captionInView;}
    [Serializable] public class Trip {public int from,to,samples;public bool closing,closedAtSwitch,opened,arrived;}
    static Receipt report;static Keyboard keyboard;static Mouse mouse;static bool exiting;static double finished;
    static string Reports=>CityExpansionInspection.Reports;
    static string ResultPath=>Path.Combine(Reports,SessionState.GetBool(Key+"screenshot",false)?"screenshot-followup-host.json":SessionState.GetBool(Key+"detailed",false)?"casino-detailed-host.json":SessionState.GetBool(Key+"roofCoast",false)?"town-roof-coast-host.json":"city-expansion-play-audit.json");
    static CityExpansionPlayAudit(){if(SessionState.GetBool(Key+"active",false)){EditorApplication.update+=Tick;Application.logMessageReceived+=Collect;}}
    public static void Start()
    {SessionState.SetBool(Key+"probe",false);StartMode(false);}
    public static void All()
    {SessionState.SetBool(Key+"probe",false);StartMode(true);}
    public static void VisualProbe()
    {SessionState.SetBool(Key+"probe",true);StartMode(false);}
    static void StartMode(bool fullWorld)
    {
        SessionState.SetBool(Key+"world",fullWorld);
        PlayerSettings.productName="SushiAndFish WorldRepair QA 20261016";
        Unity.Burst.BurstCompiler.Options.EnableBurstCompileSynchronously=true;
        SessionState.SetBool("SushiAndFish.ParkCasinoAudit.active",true);
        // Prevent the older casino audit from taking over this Play session.
        SessionState.SetBool("SushiAndFish.ParkCasinoAudit.started",true);
        SessionState.SetBool("SushiAndFish.ParkCasinoAudit.done",false);
        SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"started",false);SessionState.SetBool(Key+"done",false);
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        EditorApplication.update+=Tick;Application.logMessageReceived+=Collect;EditorApplication.EnterPlaymode();
    }
    static object Prior(string name)=>typeof(WorldIssuePlayAudit).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
    static void Collect(string m,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&errors.Count<70)errors.Add(m+"\n"+stack);}
    static void Stage(string name){report.stage=name;report.errors=errors.ToArray();File.WriteAllText(ResultPath,JsonUtility.ToJson(report,true));}
    static void Tick()
    {
        if(EditorApplication.isPaused)EditorApplication.isPaused=false;
        if(EditorApplication.isPlaying&&Application.isPlaying&&!SessionState.GetBool(Key+"started",false)){SessionState.SetBool(Key+"started",true);Run().Forget();}
        if(!EditorApplication.isPlayingOrWillChangePlaymode&&SessionState.GetBool(Key+"done",false)&&!exiting)
        {
            exiting=true;report=JsonUtility.FromJson<Receipt>(File.ReadAllText(ResultPath));
            report.errors=errors.ToArray();report.passed=string.IsNullOrEmpty(report.error)&&errors.Count==0&&report.actualE&&report.nativePanel&&report.inputRestored&&report.pages.Count>=26&&report.pages.All(p=>p.exact&&p.allFonts&&p.insideScreen&&p.captionInView)&&report.trips.Count>=13&&report.trips.All(t=>t.closing&&t.closedAtSwitch&&t.opened&&t.arrived);
            if(SessionState.GetBool(Key+"world",false))report.passed&=File.Exists(CityPath)&&JsonUtility.FromJson<WorldPlayReceipt>(File.ReadAllText(CityPath)).passed;
            if(SessionState.GetBool(Key+"probe",false))report.passed=string.IsNullOrEmpty(report.error)&&errors.Count==0&&SessionState.GetBool(Key+"probeDone",false);
            if(SessionState.GetBool(Key+"roofCoast",false))report.passed=string.IsNullOrEmpty(report.error)&&errors.Count==0&&File.Exists(CoastPath)&&JsonUtility.FromJson<CoastRevisionReceipt>(File.ReadAllText(CoastPath)).passed;
            if(SessionState.GetBool(Key+"detailed",false))report.passed=string.IsNullOrEmpty(report.error)&&errors.Count==0&&File.Exists(DetailedPath)&&JsonUtility.FromJson<InputReceipt>(File.ReadAllText(DetailedPath)).passed;
            if(SessionState.GetBool(Key+"coastAndDice",false))report.passed&=detailed!=null&&detailed.passed;
            if(SessionState.GetBool(Key+"screenshot",false))report.passed=string.IsNullOrEmpty(report.error)&&errors.Count==0&&completion!=null&&completion.passed;
            Stage(report.passed?"PASS":"FAILED");
            SessionState.SetBool(Key+"active",false);SessionState.SetBool("SushiAndFish.ParkCasinoAudit.active",false);
            PlayerSettings.productName="Deep Blue Sushi";EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");AssetDatabase.SaveAssets();finished=EditorApplication.timeSinceStartup;
        }
        if(exiting&&EditorApplication.timeSinceStartup-finished>4)EditorApplication.Exit(report.passed?0:1);
    }
    static async UniTask Pause(float seconds)=>await UniTask.Delay(TimeSpan.FromSeconds(seconds),ignoreTimeScale:true);
    static async UniTask Wait(Func<bool> predicate,int seconds=120){var until=DateTime.UtcNow.AddSeconds(seconds);while(!predicate()){if(DateTime.UtcNow>until)throw new TimeoutException(report.stage);await UniTask.Yield();}}
    static async UniTask KeyE(){InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.E));await Pause(.09f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Pause(.35f);}
    static async UniTask Aim(ParkCasinoIntegration casino,Vector3 target)
    {
        for(int i=0;i<5;i++){var d=target-CasinoRuntimeContext.PlayerCamera.transform.position;casino.HostPlayer.FirstPersonCharacter.TeleportRotation(Quaternion.Euler(0,Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,0));casino.HostPlayer.FirstPersonCharacter.PitchInstant(-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);await Pause(.04f);}
    }
    static async UniTask Click(DialoguePanelController panel,string text)
    {
        await Wait(()=>!CityExpansionInspection.Field<bool>(panel,"_isTyping"),10);
        var b=panel.ConversationButtons.Single(x=>x.gameObject.activeInHierarchy&&x.GetComponentInChildren<TMPro.TMP_Text>(true).text==text);
        b.OnSubmit(new BaseEventData(EventSystem.current));await Pause(.15f);
    }
    static string Greeting(int floor)=>floor==0?"老实赚钱，很枯燥吧！快来让你的积蓄翻倍吧！":floor==5?"怎么样，很壮观吧？接下来我们继续玩吧！":"还要继续玩吗？";
    static async UniTask CheckPage(ParkCasinoIntegration casino,DialoguePanelController panel,string text,string[] choices)
    {
        await Wait(()=>!CityExpansionInspection.Field<bool>(panel,"_isTyping"),10);Canvas.ForceUpdateCanvases();
        var buttons=panel.ConversationButtons.Where(b=>b.gameObject.activeInHierarchy).ToArray();
        var labels=buttons.Select(b=>b.GetComponentInChildren<TMPro.TMP_Text>(true)).ToArray();
        var page=new Page{floor=casino.CurrentFloor,text=panel.ConversationText,choices=labels.Select(t=>t.text).ToArray(),allFonts=true,insideScreen=true};
        page.exact=page.text==text&&page.choices.SequenceEqual(choices);
        foreach(var label in labels)
        {
            label.ForceMeshUpdate();foreach(char c in label.text)if(!char.IsWhiteSpace(c)&&!label.font.HasCharacter(c,true,true))page.allFonts=false;
            var corners=new Vector3[4];label.rectTransform.GetWorldCorners(corners);var canvas=label.GetComponentInParent<Canvas>();
            foreach(var corner in corners){var p=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,corner);if(p.x<0||p.y<0||p.x>Screen.width+1||p.y>Screen.height+1)page.insideScreen=false;}
        }
        var billboard=CityExpansionInspection.Field<PaperPixelGames.Core.Ai.NPCs.ResidentBillboardText>(casino.elevatorConversation,"billboard");
        var caption=CityExpansionInspection.Field<TMPro.TextMeshProUGUI>(billboard,"_text");caption.ForceMeshUpdate();
        page.captionInView=caption.gameObject.activeInHierarchy&&caption.GetComponentInParent<Canvas>().enabled&&caption.textInfo.characterCount>0;
        foreach(char c in caption.text)if(!char.IsWhiteSpace(c)&&!caption.font.HasCharacter(c,true,true))page.allFonts=false;
        foreach(var character in caption.textInfo.characterInfo.Take(caption.textInfo.characterCount).Where(c=>c.isVisible))
            foreach(var point in new[]{character.bottomLeft,character.topRight})
            {
                var view=CasinoRuntimeContext.PlayerCamera.WorldToViewportPoint(caption.transform.TransformPoint(point));
                page.captionInView&=view.z>CasinoRuntimeContext.PlayerCamera.nearClipPlane&&view.x>=0&&view.x<=1&&view.y>=0&&view.y<=1;
            }
        report.pages.Add(page);Stage(report.stage);
    }
    static async UniTask OpenNpc(ParkCasinoIntegration casino)
    {
        var npc=casino.ModuleRoot.GetComponentInChildren<ElevatorGuy>(true);var elevator=NetworkSingleton<ElevatorManager>.Instance;
        var colliders=npc.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger).ToArray();
        var target=colliders.Length>0?colliders[0].bounds.center:npc.transform.position+Vector3.up*casino.CurrentScale;
        Vector3 stand=elevator.playerSpawnPosition.position-Vector3.up*(.4f*casino.CurrentScale);
        casino.HostPlayer.TeleportPlayer(stand,true);casino.HostPlayer.FirstPersonCharacter.SetVelocity(Vector3.zero);await Pause(.3f);await Aim(casino,target);
        report.diagnostics.Add("NPC floor="+casino.CurrentFloor+" player="+casino.HostPlayer.transform.position+" target="+target+" hovered="+casino.LogicInteractor.TargetInteractable?.GetType().Name+" inside="+elevator.IsInElevator(casino.HostPlayer.transform.position+Vector3.up*.4f*casino.CurrentScale));
        if(!(casino.LogicInteractor.TargetInteractable is ElevatorGuy))throw new Exception("E did not target the elevator NPC.");
        await KeyE();await Wait(()=>casino.elevatorConversation.IsOpen,5);report.actualE=true;
    }
    static async UniTask TripTo(ParkCasinoIntegration casino,DialoguePanelController panel,string choice,int to)
    {
        var elevator=NetworkSingleton<ElevatorManager>.Instance;var door=CityExpansionInspection.Field<Transform>(elevator,"rightDoor");var trip=new Trip{from=casino.CurrentFloor,to=to,closedAtSwitch=true};
        await Click(panel,choice);if(!elevator.isTeleporting)throw new Exception("NPC choice did not enter the native elevator routine.");
        int previous=casino.CurrentFloor;float until=Time.realtimeSinceStartup+30;
        while(elevator.isTeleporting&&Time.realtimeSinceStartup<until)
        {
            trip.samples++;float x=door.localPosition.x;if(Mathf.Abs(x)<2.3f)trip.closing=true;
            if(casino.CurrentFloor!=previous){trip.closedAtSwitch&=Mathf.Abs(x)<.05f;previous=casino.CurrentFloor;}
            await UniTask.Yield();
        }
        trip.arrived=!elevator.isTeleporting&&casino.CurrentFloor==to;
        trip.opened=Mathf.Abs(door.localPosition.x-2.5f)<.05f;
        report.trips.Add(trip);Stage("ELEVATOR_ARRIVED_"+to);
        if(!trip.arrived)throw new Exception("Elevator did not arrive.");
    }
    static async UniTask Run()
    {
        report=new Receipt{utc=DateTime.UtcNow.ToString("o"),profile=Application.persistentDataPath};
        try
        {
            if(!report.profile.Contains("WorldRepair QA"))throw new Exception("Formal profile must stay isolated.");
            Stage("BOOTSTRAP_HOST");await Wait(()=>{Prior("Dismiss");return IslandFrontEndFlow.Active&&IslandFrontEndFlow.Active.BackdropLoaded;});await Pause(2);Prior("Dismiss");
            if(SessionState.GetBool(Key+"screenshot",false))await CompletionMenu();
            await Singleton<MainMenuManager>.Instance.CustomizationSceneHost.PreloadAsync();IslandFrontEndFlow.Active.ShowLoadGames();await Pause(1);
            Resources.FindObjectsOfTypeAll<SaveSlot>().Where(s=>s&&s.gameObject.scene.name=="MainMenuState"&&s.ContainsSave).OrderBy(s=>s.SlotIndex).First().LaunchOriginalSlot();
            Stage("WAIT_HOST_READY");await Wait(()=>(bool)Prior("Ready"),240);await Wait(()=>ParkCasinoIntegration.Instance&&ParkCasinoIntegration.Instance.Ready,180);await Pause(1);
            var casino=ParkCasinoIntegration.Instance;var panel=Singleton<GameUIManager>.Instance.DialoguePanelController;
            if(!casino.elevatorConversation)throw new Exception("Saved elevator conversation missing.");
            keyboard=InputSystem.AddDevice<Keyboard>("City verification keyboard");mouse=InputSystem.AddDevice<Mouse>("City verification mouse");Singleton<InputManager>.Instance.InputHandler.InputMap.asset.devices=new InputDevice[]{keyboard,mouse};
            report.nativePanel=panel==Object.FindFirstObjectByType<DialoguePanelController>();
            if(SessionState.GetBool(Key+"screenshot",false)){await CompletionRun(casino);return;}
            if(SessionState.GetBool(Key+"detailed",false)){await DetailedRun(casino);return;}
            if(SessionState.GetBool(Key+"roofCoast",false)){await RoofCoast(casino);if(SessionState.GetBool(Key+"coastAndDice",false)){detailed=new InputReceipt{utc=DateTime.UtcNow.ToString("o"),ready=true};casino.HostEconomy.SetSavedGoldBalance(0,"10000000000",casino.HostEconomy.ScrapBalance.Value);detailed.coins=casino.WholeCoins;await FullInputRounds(casino);detailed.passed=detailed.games.Count==2&&detailed.games.All(g=>string.IsNullOrEmpty(g.error)&&g.started&&g.finished&&g.result&&g.wallet&&g.grounded)&&detailed.buttons.All(b=>b.correct);DetailedStage(detailed.passed?"PASS":"FAILED");}return;}
            if(SessionState.GetBool(Key+"probe",false)){await ProbeVisuals(casino);SessionState.SetBool(Key+"probeDone",true);Stage("VISUAL_PROBE_DONE");return;}
            if(SessionState.GetBool(Key+"world",false))await World(casino);
            for(int floor=1;floor<=5;floor++)
            {
                Stage("LOBBY_NPC_TO_"+floor);await OpenNpc(casino);await CheckPage(casino,panel,Greeting(0),new[]{"好的","不了","结束"});
                if(floor==1&&SessionState.GetBool(Key+"world",false))await Snapshot("city-elevator-dialogue-lobby.png");
                await Click(panel,"好的");await CheckPage(casino,panel,"请问要去第几层，我来帮你！还是说你想见见我们老板？",new[]{"第一层","第二层","第三层","第四层","BOSS","返回"});
                if(floor==1&&SessionState.GetBool(Key+"world",false))await Snapshot("city-elevator-dialogue-floor-menu.png");
                if(floor==1){await Click(panel,"返回");await CheckPage(casino,panel,Greeting(0),new[]{"好的","不了","结束"});await Click(panel,"好的");}
                var name=floor==5?"BOSS":"第"+new[]{"","一","二","三","四"}[floor]+"层";
                await TripTo(casino,panel,name,floor);
                await OpenNpc(casino);await CheckPage(casino,panel,Greeting(floor),new[]{"好的","不了","结束"});
                if(floor==1)
                {
                    await Click(panel,"结束");
                    if(panel.IsVisible||casino.CurrentFloor!=floor)throw new Exception("Ending the conversation changed floors or left the panel open.");
                    await OpenNpc(casino);await CheckPage(casino,panel,Greeting(floor),new[]{"好的","不了","结束"});
                }
                if(floor==5&&SessionState.GetBool(Key+"world",false))await Snapshot("city-elevator-dialogue-boss.png");
                await Click(panel,"好的");var names=new[]{"第一层","第二层","第三层","第四层","BOSS"}.Where((v,i)=>i+1!=floor).Concat(new[]{"返回"}).ToArray();
                await CheckPage(casino,panel,"还要去哪些层呢？",names);await Click(panel,"返回");await CheckPage(casino,panel,Greeting(floor),new[]{"好的","不了","结束"});
                await TripTo(casino,panel,"不了",0);
            }
            // Exercise selecting a different floor from an occupied floor,
            // including entering BOSS directly, through the same native doors.
            await OpenNpc(casino);await Click(panel,"好的");await TripTo(casino,panel,"第一层",1);
            await OpenNpc(casino);await Click(panel,"好的");await CheckPage(casino,panel,"还要去哪些层呢？",new[]{"第二层","第三层","第四层","BOSS","返回"});await TripTo(casino,panel,"BOSS",5);
            await OpenNpc(casino);await CheckPage(casino,panel,Greeting(5),new[]{"好的","不了","结束"});await TripTo(casino,panel,"不了",0);
            await OpenNpc(casino);await Click(panel,"不了");if(panel.IsVisible)throw new Exception("Lobby decline did not close the conversation.");
            await OpenNpc(casino);await Click(panel,"结束");report.inputRestored=!panel.IsVisible&&!Singleton<GameUIManager>.Instance.IsShopUIActive&&Singleton<InputManager>.Instance.InputHandler.GameplayInputEnabled;
            if(SessionState.GetBool(Key+"world",false))
            {
                city.nativeShopsAfterElevator=await NativeShops(casino,true);
                city.passed&=city.nativeShopsAfterElevator;
                if(!city.nativeShopsAfterElevator)throw new Exception("Native shop dialogue did not restore after the elevator conversation.");
                await CityRoundTrip();
            }
            Stage("PLAY_CHECKS_DONE");
        }
        catch(Exception e){report.error=e.ToString();Stage("FAILED");}
        finally
        {
            try{await CleanupHost();}catch(Exception e){report.error+=(report.error==null?"":"\n")+e;}
            File.WriteAllText(ResultPath,JsonUtility.ToJson(report,true));SessionState.SetBool(Key+"done",true);EditorApplication.ExitPlaymode();
        }
    }
}
