using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using PaperPixelGames.Core.Economy;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.States.MainMenu;
using PaperPixelGames.Framework.States.Game;
using SushiAndFish.Casino;
using SushiAndFish.Casino.Original;
using SushiAndFish.Casino.Original.CasinoLab;
using SushiAndFish.Casino.Original.Extensions;
using SushiAndFish.FrontEnd;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
using PlayerSettings=UnityEditor.PlayerSettings;

[InitializeOnLoad]
public static class ParkCasinoPlayAudit
{
    const string Key="SushiAndFish.ParkCasinoAudit.";
    static string Reports=>IslandRevision20261018.Reports;
    static string ResultPath=>Path.Combine(Reports,SessionState.GetString(Key+"report","casino-host-play-audit.json"));
    static readonly List<string> errors=new List<string>();
    [Serializable]public class Result
    {
        public string utc,stage,error,profile;public bool ready,passed,avatarRetained,sourceHudHidden,walletLinked,saveRoundTrip,doorWalked,emptyPressIgnored,ordinaryHostInput;
        public float doorStart,doorEnd;public int sourceGames,buttonPresses,completedHandPresses,slotsSymbols,slotsUnculledSymbols;
        public bool ticketGoldLinked,heldItemUsed,heldItemThrown,animationRetargeted,returnedToMenu,reenteredWithGold,nativeServicesDisposed,interiorWalked,handMoved,correctElevatorButton,slotsVisualReady;
        public List<Floor> floors=new List<Floor>();public List<Game> games=new List<Game>();public List<Label> labels=new List<Label>();public List<string> captures=new List<string>();public List<string> diagnostics=new List<string>();public List<Frame> frames=new List<Frame>();public string[] errors;
    }
    [Serializable]public class Floor {public int requested,current;public int[] active;public bool ready,hostStillLoaded;public Vector3 hostPosition,mapOrigin;}
    [Serializable]public class Game {public string name,type;public bool started,finished,resultRegistered,exactWallet,grounded,originalVisualLayers;public long bet,payout,balanceBefore,balanceAfter;public string storedBefore,storedAfter;public float localFootHeight;}
    [Serializable]public class Label {public string requested,text;public float font,width,meshWidth;public int chars,visible;public bool allInside;}
    [Serializable]public class Frame {public string file;public float pinkFraction;}
    static Keyboard virtualKeyboard;
    static Mouse virtualMouse;
    static Result report;static double finished;static bool exiting;
    static readonly List<(string type,long bet,long payout)> outcomes=new List<(string,long,long)>();
    static ParkCasinoPlayAudit(){EditorApplication.delayCall+=RequestedStart;if(SessionState.GetBool(Key+"active",false)){EditorApplication.update+=Tick;Application.logMessageReceived+=Collect;}}
    static void RequestedStart()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=RequestedStart;return;}
        var request=Path.Combine(Reports,"casino-play-audit.request");if(!File.Exists(request))return;
        File.Delete(request);Start();
    }
    public static void Start()
    {
        PlayerSettings.productName="SushiAndFish WorldRepair QA 20261016";
        Unity.Burst.BurstCompiler.Options.EnableBurstCompileSynchronously=true;
        SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"started",false);SessionState.SetBool(Key+"done",false);
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");EditorApplication.update+=Tick;Application.logMessageReceived+=Collect;EditorApplication.EnterPlaymode();
    }
    public static void Smoke(){SessionState.SetBool(Key+"smoke",true);Start();}
    public static void Full(){SessionState.SetBool(Key+"smoke",false);SessionState.SetBool(Key+"slotsOnly",false);SessionState.SetString(Key+"report","casino-host-play-audit.json");Start();}
    public static void CityRegression(){SessionState.SetBool(Key+"smoke",false);SessionState.SetBool(Key+"slotsOnly",false);SessionState.SetBool(Key+"cityRegression",true);SessionState.SetString(Key+"report","casino-city-regression-play-audit.json");Start();}
    public static void SlotsProbe(){CasinoSpriteReferenceAudit.Inspect();SessionState.SetBool(Key+"smoke",false);SessionState.SetBool(Key+"slotsOnly",true);SessionState.SetString(Key+"report","casino-host-slots-probe.json");Start();}
    static void Collect(string message,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&errors.Count<70)errors.Add(message+"\n"+(stack.Length>5000?stack.Substring(0,5000):stack));}
    static void Tick()
    {
        if(EditorApplication.isPaused)EditorApplication.isPaused=false;
        if(EditorApplication.isPlaying&&Application.isPlaying&&!SessionState.GetBool(Key+"started",false)){SessionState.SetBool(Key+"started",true);Run().Forget();}
        if(!EditorApplication.isPlayingOrWillChangePlaymode&&SessionState.GetBool(Key+"done",false)&&!exiting)
        {
            exiting=true;report=JsonUtility.FromJson<Result>(File.ReadAllText(ResultPath));
            report.errors=errors.ToArray();report.passed=report.ready&&string.IsNullOrEmpty(report.error)&&errors.Count==0&&report.walletLinked&&report.ticketGoldLinked&&report.saveRoundTrip&&report.avatarRetained&&report.sourceHudHidden&&report.doorWalked&&report.interiorWalked&&report.ordinaryHostInput&&report.emptyPressIgnored&&report.correctElevatorButton&&report.handMoved&&report.completedHandPresses>0&&report.animationRetargeted&&report.heldItemUsed&&report.heldItemThrown&&report.returnedToMenu&&report.nativeServicesDisposed&&report.reenteredWithGold&&report.floors.Count==6&&report.floors.All(f=>f.ready&&f.hostStillLoaded&&f.current==f.requested&&f.active.SequenceEqual(new[]{f.requested}))&&report.games.Count==16&&report.games.All(g=>g.started&&g.finished&&g.resultRegistered&&g.exactWallet&&g.grounded)&&report.labels.Count==4&&report.labels.All(l=>l.allInside&&l.text==l.requested);
            report.passed=report.passed&&report.slotsVisualReady&&report.games.All(g=>g.originalVisualLayers);
            Stage(report.passed?"PASS":"FAILED");PlayerSettings.productName="Deep Blue Sushi";EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");AssetDatabase.SaveAssets();finished=EditorApplication.timeSinceStartup;
        }
        if(exiting&&EditorApplication.timeSinceStartup-finished>4)EditorApplication.Exit(report.passed?0:1);
    }
    static void Stage(string name){report.stage=name;report.errors=errors.ToArray();File.WriteAllText(ResultPath,JsonUtility.ToJson(report,true));}
    static object Prior(string name)=>typeof(WorldIssuePlayAudit).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
    static async UniTask Pause(float seconds)=>await UniTask.Delay(TimeSpan.FromSeconds(seconds),ignoreTimeScale:true);
    static async UniTask Wait(Func<bool> predicate,int seconds=180){var deadline=DateTime.UtcNow.AddSeconds(seconds);while(!predicate()){if(DateTime.UtcNow>deadline)throw new TimeoutException(report.stage);await UniTask.Yield();}}
    static async UniTask Capture(string file)
    {
        if(SessionState.GetBool(Key+"cityRegression",false))file="city-regression-"+file;
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
        var camera=(Camera)Prior("WorldCamera");
        var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.enabled&&c.renderMode!=RenderMode.WorldSpace).ToArray();
        var modes=canvases.Select(c=>(c.renderMode,c.worldCamera,c.planeDistance)).ToArray();
        var old=camera.targetTexture;var active=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try
        {
            foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.2f;}
            // Camera.Render bypasses the full Editor batch render loop. Run
            // the same native shader bindings used at a normal context start.
            OriginalNativeShaderRestoration.ApplyPending();
            SushiAndFish.Casino.Original.RecoveryShaderSupport.BeginRendering();
            var lighting=MonoSingleton<LightbakerManager>.Instance;
            if(lighting&&lighting.targetLightVolume)
            {
                var v=lighting.targetLightVolume;
                report.diagnostics.Add("LIGHT "+file+" volume="+v.name+" offset="+v.gridOffset+" global="+Shader.GetGlobalVector("gridOffset")+" spacing="+v.raySpacing+" globalSpacing="+Shader.GetGlobalFloat("raySpacing")+" brightness="+Shader.GetGlobalFloat("brightness")+" map="+Shader.GetGlobalTexture("_LightMap")?.name+" wanted="+v.fileName+" ambient="+RenderSettings.ambientIntensity);
            }
            if(file.Contains("floor-4")||file.Contains("render-"))
            {
                var color=VolumeManager.instance.stack.GetComponent<ColorAdjustments>();var tone=VolumeManager.instance.stack.GetComponent<Tonemapping>();
                report.diagnostics.Add("POST "+file+" exposure="+color?.postExposure.value+" tone="+tone?.mode.value+" fog="+RenderSettings.fog+" fogDensity="+RenderSettings.fogDensity);
                var casino=ParkCasinoIntegration.Instance;
                foreach(var hit in Physics.RaycastAll(casino.HostPlayer.transform.position+Vector3.up,Vector3.down,5,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
                    foreach(var m in hit.collider.GetComponent<Renderer>()?hit.collider.GetComponent<Renderer>().sharedMaterials:Array.Empty<Material>())
                        if(m)report.diagnostics.Add("GROUND "+file+" "+hit.collider.name+" "+m.name+" shader="+m.shader?.name+" tex="+string.Join(";",m.GetTexturePropertyNames().Select(n=>n+"="+m.GetTexture(n)?.name)));
            }
            if(file.Contains("Blackjack")||file.Contains("Crash"))
            {
                var game=CasinoLabSelection.SelectedGame;
                var bloom=VolumeManager.instance.stack.GetComponent<Bloom>();
                report.diagnostics.Add("RENDER_STATE "+file+" cameraMask="+camera.cullingMask+" bloom="+bloom?.intensity.value+" threshold="+bloom?.threshold.value+" player="+ParkCasinoIntegration.Instance.ModuleRoot.InverseTransformPoint(ParkCasinoIntegration.Instance.HostPlayer.transform.position));
                foreach(var renderer in ParkCasinoIntegration.Instance.ModuleRoot.GetComponentsInChildren<Renderer>(true).Where(r=>r.gameObject.activeInHierarchy&&r.sharedMaterials.Any(m=>m&&m.name.IndexOf("Carpet",StringComparison.OrdinalIgnoreCase)>=0)).Take(16))
                    report.diagnostics.Add("CARPET "+renderer.name+" layer="+renderer.gameObject.layer+" mask="+((camera.cullingMask&(1<<renderer.gameObject.layer))!=0)+" active="+renderer.gameObject.activeInHierarchy+" enabled="+renderer.enabled+" forceOff="+renderer.forceRenderingOff+" bounds="+renderer.bounds+" materials="+string.Join(";",renderer.sharedMaterials.Where(m=>m).Select(m=>m.name+"="+m.shader?.name)));
                foreach(var renderer in game.GetComponentsInChildren<Renderer>(true).Concat(game.Keypad.GetComponentsInChildren<Renderer>(true)))
                    foreach(var m in renderer.sharedMaterials.Where(m=>m))
                        report.diagnostics.Add("MAT "+file+" "+renderer.name+" "+m.name+" shader="+m.shader?.name+" supported="+(m.shader&&m.shader.isSupported)+" passes="+m.passCount+" owned="+CasinoShaderOwnership.Owns(m)+" asset="+AssetDatabase.GetAssetPath(m));
            }
            Canvas.ForceUpdateCanvases();camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(Reports,file),texture.EncodeToPNG());
            var pixels=texture.GetPixels32();int pink=0;for(int i=0;i<pixels.Length;i+=8){var p=pixels[i];if(p.r>190&&p.b>190&&p.g<100)pink++;}report.frames.Add(new Frame{file=file,pinkFraction=pink/(pixels.Length/8f)});
        }
        finally
        {
            for(int i=0;i<canvases.Length;i++)if(canvases[i]){canvases[i].renderMode=modes[i].renderMode;canvases[i].worldCamera=modes[i].worldCamera;canvases[i].planeDistance=modes[i].planeDistance;}
            camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(texture);
            SushiAndFish.Casino.Original.RecoveryShaderSupport.EndRendering();
        }
        report.captures.Add(file);
    }
    static async UniTask KeyE()
    {
        InputSystem.QueueStateEvent(virtualKeyboard,new KeyboardState(UnityEngine.InputSystem.Key.E));await Pause(.08f);InputSystem.QueueStateEvent(virtualKeyboard,new KeyboardState());await Pause(.45f);
    }
    static async UniTask Aim(ParkCasinoIntegration casino,Vector3 target)
    {
        for(int i=0;i<5;i++)
        {
            var direction=target-CasinoRuntimeContext.PlayerCamera.transform.position;
            float yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
            casino.HostPlayer.FirstPersonCharacter.TeleportRotation(Quaternion.Euler(0,yaw,0));
            casino.HostPlayer.FirstPersonCharacter.PitchInstant(-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg);
            await Pause(.04f);
        }
    }
    static IEnumerator Replace(int index,Action done){yield return CasinoLabSelection.ReplaceCluster(index);done();}
    static async UniTask Travel(ParkCasinoIntegration casino,int floor)
    {
        var elevator=NetworkSingleton<ElevatorManager>.Instance;
        casino.HostPlayer.TeleportPlayer(elevator.playerSpawnPosition.position-Vector3.up*(.4f*casino.CurrentScale),true);await Pause(.3f);
        var button=CasinoLabBootstrap.Field<List<Transform>>(elevator,"buttonList")[floor].GetComponent<InteractableEventTrigger>();
        button.ServerOnInteract(casino.LogicInteractor);
        await Pause(.1f);await Wait(()=>!elevator.isTeleporting&&casino.Bootstrap.ready,30);await Pause(.6f);
        var active=Object.FindObjectsByType<CasinoFloor>(FindObjectsSortMode.None).Select(f=>f.floorIndex).OrderBy(i=>i).ToArray();
        report.floors.Add(new Floor{requested=floor,current=CasinoLabBootstrap.Field<int>(elevator,"_currentFloorIndex"),active=active,ready=casino.Bootstrap.ready,hostStillLoaded=SceneManager.GetSceneByName("GameState").isLoaded,hostPosition=casino.HostPlayer.transform.position,mapOrigin=casino.ModuleRoot.position});
        await Capture("casino-host-floor-"+floor+".png");Stage("FLOOR_"+floor);
    }
    static async UniTask Run()
    {
        report=new Result{utc=DateTime.UtcNow.ToString("o"),profile=Application.persistentDataPath};
        try
        {
            if(!report.profile.Contains("WorldRepair QA"))throw new Exception("Formal save profile must stay isolated.");
            Stage("BOOTSTRAP_HOST");await Wait(()=>{Prior("Dismiss");return IslandFrontEndFlow.Active&&IslandFrontEndFlow.Active.BackdropLoaded;});await Pause(2);Prior("Dismiss");
            await Singleton<MainMenuManager>.Instance.CustomizationSceneHost.PreloadAsync();IslandFrontEndFlow.Active.ShowLoadGames();await Pause(1);
            var slot=Resources.FindObjectsOfTypeAll<SaveSlot>().Where(s=>s&&s.gameObject.scene.name=="MainMenuState"&&s.ContainsSave).OrderBy(s=>s.SlotIndex).First();slot.LaunchOriginalSlot();
            Stage("WAIT_HOST_READY");await Wait(()=>(bool)Prior("Ready"),240);await Pause(2);
            var casino=Object.FindFirstObjectByType<ParkCasinoIntegration>();if(!casino)throw new Exception("Park casino component missing from saved host scene.");
            Stage("WAIT_CASINO_READY");await Wait(()=>casino.Ready,180);report.ready=true;
            var player=casino.HostPlayer;var avatar=player.gameObject;var character=player.FirstPersonCharacter;
            virtualKeyboard=InputSystem.AddDevice<Keyboard>("Casino verification keyboard");virtualMouse=InputSystem.AddDevice<Mouse>("Casino verification mouse");
            var handler=Singleton<InputManager>.Instance.InputHandler;
            handler.InputMap.asset.devices=new InputDevice[]{virtualKeyboard,virtualMouse};
            handler.Interact+=phase=>report.diagnostics.Add("HOST_INTERACT_PHASE "+phase);
            report.diagnostics.Add("HOST_INPUT map="+player.PlayerInput.currentActionMap?.name+" realHandler="+handler.GameplayInputEnabled+" tool="+player.IsToolActive+" held="+player.Interactor.IsHolding+" using="+player.Interactor.IsUsing+" paused="+player.FirstPersonCharacterInput.IsInputPaused);
            report.avatarRetained=avatar==Singleton<PlayersManager>.Instance.LocalPlayer.gameObject&&player.IsOwner&&character.enabled;
            report.sourceGames=Object.FindObjectsByType<GameBase>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            var wallet=casino.HostEconomy;wallet.SetSavedGoldBalance(0,"100000000",wallet.ScrapBalance.Value);await Pause(.6f);
            var money=NetworkSingleton<MoneyManager>.Instance;var profile=casino.LogicInteractor.GetComponent<PlayerProfile>();
            decimal start=wallet.ExactGoldBalance;
            bool spent=money.TryChangeBalance(-17,profile,ChangeType.Bet);bool earn=money.TryChangeBalance(29,profile,ChangeType.GameResult);
            report.walletLinked=spent&&earn&&wallet.ExactGoldBalance==start+1200m&&money.balance==wallet.CasinoGoldCoins;
            decimal ticketStart=wallet.ExactGoldBalance;
            report.ticketGoldLinked=money.TryChangeTicketBalance(-3)&&money.TryChangeTicketBalance(5)&&wallet.ExactGoldBalance==ticketStart+200m&&money.ticketBalance==money.balance;
            money.TryChangeTicketBalance(-2);
            var progress=Singleton<GameProgressManager>.Instance;var saved=progress.Save();string json=JsonUtility.ToJson(saved);var restored=JsonUtility.FromJson<GameProgressData>(json);
            wallet.SetSavedGoldBalance(0,"0",wallet.ScrapBalance.Value);progress.Load(restored);report.saveRoundTrip=wallet.ExactGoldBalance==start+1200m&&money.balance==wallet.CasinoGoldCoins;
            Stage("PARK_DOOR_WALK");
            var startPoint=casino.ModuleRoot.TransformPoint(new Vector3(0,.3f,48));player.TeleportPlayer(startPoint,true);character.SetVelocity(Vector3.zero);await Pause(.6f);
            character.LookAtInstant(casino.ModuleRoot.TransformPoint(new Vector3(0,4,63)));report.doorStart=casino.ModuleRoot.InverseTransformPoint(player.transform.position).z;
            player.FirstPersonCharacterInput.InputMove=Vector2.up;
            float until=Time.realtimeSinceStartup+3.5f;
            while(Time.realtimeSinceStartup<until&&casino.ModuleRoot.InverseTransformPoint(player.transform.position).z<63)
            {player.FirstPersonCharacterInput.InputMove=Vector2.up;await UniTask.Yield();}
            player.FirstPersonCharacterInput.InputMove=Vector2.zero;character.SetMovementDirection(Vector3.zero);report.doorEnd=casino.ModuleRoot.InverseTransformPoint(player.transform.position).z;report.doorWalked=report.doorEnd>60;
            report.diagnostics.Add("DOOR feet="+player.transform.position+" capsules="+string.Join(";",player.GetComponentsInChildren<CapsuleCollider>().Select(c=>c.name+" center="+c.center+" height="+c.height+" radius="+c.radius+" enabled="+c.enabled)));
            var overlaps=Physics.OverlapCapsule(player.transform.position+Vector3.up*.32f,player.transform.position+Vector3.up*1.48f,.29f,~0,QueryTriggerInteraction.Ignore).Where(c=>!c.transform.IsChildOf(player.transform)).Take(8);
            report.diagnostics.Add("DOOR_BLOCKERS "+string.Join(";",overlaps.Select(c=>c.name+" "+c.bounds.ToString())));
            await Capture("casino-host-park-entrance.png");
            Stage("CASINO_BUTTON_E_AND_HOST_HANDS");
            var elevator=NetworkSingleton<ElevatorManager>.Instance;var floorButton=CasinoLabBootstrap.Field<List<Transform>>(elevator,"buttonList")[1];
            var target=floorButton.GetComponent<Collider>()?floorButton.GetComponent<Collider>().bounds.center:floorButton.position;
            Vector3 face=Vector3.ProjectOnPlane(floorButton.forward,Vector3.up).normalized;
            Vector3 stand=target+face*.65f;stand.y=casino.ModuleRoot.TransformPoint(new Vector3(0,.3f,0)).y;
            player.TeleportPlayer(stand,true);await Pause(.3f);await Aim(casino,target);
            report.diagnostics.Add("BUTTON camera="+CasinoRuntimeContext.PlayerCamera.name+" eye="+CasinoRuntimeContext.PlayerCamera.transform.position+" target="+target+" hovered="+casino.LogicInteractor.TargetInteractable?.GetType().Name+" range="+casino.LogicInteractor.raycastDistance+" handler="+handler.GameplayInputEnabled);
            var cam=CasinoRuntimeContext.PlayerCamera;
            report.diagnostics.Add("BUTTON_RAY forward="+cam.transform.forward+" angle="+Vector3.Angle(cam.transform.forward,target-cam.transform.position)+" globalTriggers="+Physics.queriesHitTriggers+" colliders="+string.Join(";",floorButton.GetComponentsInChildren<Collider>().Select(c=>c.name+" enabled="+c.enabled+" trigger="+c.isTrigger+" layer="+c.gameObject.layer)));
            report.diagnostics.Add("BUTTON_HITS "+string.Join(";",Physics.SphereCastAll(new Ray(cam.transform.position,cam.transform.forward),casino.LogicInteractor.raycastRadius,casino.LogicInteractor.raycastDistance,~0,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance).Take(20).Select(h=>h.transform.name+"@"+h.distance+" interact="+(h.transform.GetComponent<IInteractable>()!=null))));
            int presses=casino.PressCount;
            var rightBefore=player.RigBones.FpsRig.RightHand.localPosition;var leftBefore=player.RigBones.FpsRig.LeftHand.localPosition;
            InputSystem.QueueStateEvent(virtualKeyboard,new KeyboardState(UnityEngine.InputSystem.Key.E));await Pause(.09f);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            report.handMoved=Vector3.Distance(rightBefore,player.RigBones.FpsRig.RightHand.localPosition)>.05f||Vector3.Distance(leftBefore,player.RigBones.FpsRig.LeftHand.localPosition)>.05f;
            await Capture("casino-host-button-hand-press.png");InputSystem.QueueStateEvent(virtualKeyboard,new KeyboardState());await Pause(.5f);report.buttonPresses=casino.PressCount-presses;
            report.completedHandPresses=player.GetComponent<CasinoButtonHandPress>().CompletedPresses;
            await Wait(()=>!elevator.isTeleporting&&casino.Bootstrap.ready,30);
            report.correctElevatorButton=casino.CurrentFloor==1;
            character.LookAtInstant(player.transform.position+Vector3.up*20);await Pause(.2f);int emptyBefore=casino.PressCount;await KeyE();report.emptyPressIgnored=casino.PressCount==emptyBefore;
            // A real E press can already select floor1. Reset only the native
            // elevator state for the complete original 1..5..0 travel sequence.
            if(casino.CurrentFloor!=0)await Travel(casino,0);
            report.floors.Clear();
            foreach(int floor in new[]{1,2,3,4,5,0})await Travel(casino,floor);
            await Travel(casino,4);report.floors.RemoveAt(report.floors.Count-1);
            await Aim(casino,player.transform.position-Vector3.forward*10+Vector3.up*1.5f);
            var walkStart=player.transform.position;InputSystem.QueueStateEvent(virtualKeyboard,new KeyboardState(UnityEngine.InputSystem.Key.W));await Pause(.8f);InputSystem.QueueStateEvent(virtualKeyboard,new KeyboardState());await Pause(.2f);
            report.interiorWalked=Vector3.Distance(walkStart,player.transform.position)>1f&&casino.ModuleRoot.InverseTransformPoint(player.transform.position).y>-.1f;
            report.ordinaryHostInput=report.interiorWalked;
            if(SessionState.GetBool(Key+"smoke",false))
            {
                var renderCamera=CasinoRuntimeContext.PlayerCamera;var cameraData=renderCamera.GetComponent<UniversalAdditionalCameraData>();
                bool post=cameraData.renderPostProcessing;cameraData.renderPostProcessing=false;
                await Capture("casino-render-without-post.png");cameraData.renderPostProcessing=post;
                var pointBefore=casino.ModuleRoot.InverseTransformPoint(player.transform.position);var originBefore=casino.nativeMapOrigin;
                casino.nativeMapOrigin=new Vector3(0,200,0);typeof(ParkCasinoIntegration).GetMethod("ApplyMapPose",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(casino,null);
                player.TeleportPlayer(casino.ModuleRoot.TransformPoint(pointBefore),true);await Pause(.3f);await Capture("casino-render-near-origin.png");
                casino.nativeMapOrigin=originBefore;typeof(ParkCasinoIntegration).GetMethod("ApplyMapPose",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(casino,null);
                player.TeleportPlayer(casino.ModuleRoot.TransformPoint(pointBefore),true);await Pause(.3f);
            }
            NetworkSingleton<GameResultsManager>.Instance.OnResultRegistered+=(bet,payout,p,type,pos,a,b,c,data)=>outcomes.Add((type.ToString(),bet,payout));
            Stage("ORIGINAL_16_GAME_ROUNDS");
            for(int i=0;i<16;i++)
            {
                if(SessionState.GetBool(Key+"smoke",false)&&i>2)continue;
                if(SessionState.GetBool(Key+"slotsOnly",false)&&i!=12)continue;
                SceneManager.SetActiveScene(casino.ModuleRoot.gameObject.scene);
                bool replaced=false;casino.Bootstrap.StartCoroutine(Replace(i,()=>replaced=true));await Wait(()=>replaced,30);
                SceneManager.SetActiveScene(casino.gameObject.scene);
                var game=CasinoLabSelection.SelectedGame;if(!game)throw new Exception("Missing original game "+CasinoLabSelection.Names[i]);
                var point=game.Keypad?game.Keypad.transform:game.transform;
                var cluster=(GameObject)typeof(CasinoLabSelection).GetField("currentCluster",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
                var outward=point.position-cluster.transform.position;outward.y=0;if(outward.sqrMagnitude<.1f)outward=-point.forward;outward.Normalize();
                var position=point.position+outward*2.2f;position.y=casino.nativeMapOrigin.y+.3f;
                player.TeleportPlayer(position,true);await Pause(.25f);await Aim(casino,point.position);casino.RegisterCasinoButtons();
                var row=new Game{name=CasinoLabSelection.Names[i],type=game.GameType.ToString(),balanceBefore=money.balance,storedBefore=wallet.SavedGoldStorage};
                int sourceView=CasinoResources.Load<Camera>("Camera").cullingMask;
                var drawLayers=game.GetComponentsInChildren<Renderer>(true).Select(r=>r.gameObject.layer).Concat(game.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Select(g=>g.gameObject.layer)).Distinct();
                row.originalVisualLayers=drawLayers.All(layer=>layer==6||layer==7||layer==10||(sourceView&(1<<layer))==0||(CasinoRuntimeContext.PlayerCamera.cullingMask&(1<<layer))!=0);
                int resultCount=outcomes.Count(o=>o.type==row.type);int turn=CasinoLabBootstrap.Field<int>(game,"gameTurn");
                game.Keypad.SetBetMinimum();row.bet=game.currentBet;
                if(game is Keno k)foreach(var b in k.GetComponentsInChildren<KenoButton>().Take(3))b.ServerOnInteract(casino.LogicInteractor);
                if(game is Roulette r)r.GetComponentsInChildren<RouletteButton>().First().ServerOnInteract(casino.LogicInteractor);
                if(game is MoneyWheel mw)mw.SelectBettingOption(mw.GetComponentsInChildren<MoneyWheelButton>().First().betOption);
                if(game is DuckRace d)d.ServerPlaceBet(0);
                if(game is HiLoGame h)h.SetOver(true);
                if(game is Baccarat ba)ba.PlaceBetOnPlayer(casino.LogicInteractor);else game.TryStartGame(casino.LogicInteractor);
                row.started=game.isPlaying||CasinoLabBootstrap.Field<int>(game,"gameTurn")>turn;
                if(game is Craps originalCraps)
                {
                    var dm=CasinoLabBootstrap.Field<DiceManager>(originalCraps,"diceManager");
                    var dice=CasinoLabBootstrap.Field<List<Dice>>(dm,"_diceList").First();
                    dice.ServerOnInteract(casino.LogicInteractor);await Pause(.25f);
                    InputSystem.QueueStateEvent(virtualMouse,new MouseState().WithButton(MouseButton.Left));await Pause(.12f);InputSystem.QueueStateEvent(virtualMouse,new MouseState());await Pause(.1f);
                    report.heldItemUsed=CasinoLabBootstrap.Field<bool>(dice,"_isShaking");
                    var throwAction=handler.InputMap.asset.FindAction("Player/Throw");
                    report.diagnostics.Add("THROW_BINDINGS "+string.Join(";",throwAction.bindings.Select(b=>b.effectivePath)));
                    var keyBinding=throwAction.bindings.First(b=>b.effectivePath.StartsWith("<Keyboard>/")).effectivePath.Substring(11);
                    var throwKey=(Key)Enum.Parse(typeof(Key),keyBinding,true);
                    InputSystem.QueueStateEvent(virtualKeyboard,new KeyboardState(throwKey));await Pause(.15f);InputSystem.QueueStateEvent(virtualKeyboard,new KeyboardState());await Pause(.3f);
                    report.heldItemThrown=!casino.LogicInteractor.GetComponent<PlayerInventory>().NetworkholdingItem&&CasinoLabBootstrap.Field<bool>(dice,"_isRolling");
                }
                await Pause(.5f);await Capture("casino-host-game-"+row.name+".png");
                if(game is Slots)
                {
                    var pictures=game.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                    var sourceSymbols=game.GetComponentsInChildren<SlotReel>(true).SelectMany(r=>r.symbols).Where(s=>s).ToArray();
                    report.slotsSymbols=sourceSymbols.Length;report.slotsUnculledSymbols=sourceSymbols.Count(s=>s.isActiveAndEnabled&&!s.canvasRenderer.cull);
                    report.slotsVisualReady=sourceSymbols.Length>0&&sourceSymbols.All(s=>s.sprite&&s.sprite.texture&&s.sprite.texture.width>4&&s.isActiveAndEnabled&&!s.canvasRenderer.cull&&(CasinoRuntimeContext.PlayerCamera.cullingMask&(1<<s.gameObject.layer))!=0);
                    foreach(var pic in sourceSymbols.Concat(pictures).Distinct().Take(18))report.diagnostics.Add("SLOTS_IMAGE "+pic.name+" layer="+pic.gameObject.layer+" active="+pic.isActiveAndEnabled+" cull="+pic.canvasRenderer.cull+" sprite="+(pic.sprite?pic.sprite.name:"MISSING")+" texture="+(pic.mainTexture?pic.mainTexture.name:"MISSING")+" texSize="+(pic.mainTexture?pic.mainTexture.width:0)+" shader="+pic.materialForRendering?.shader?.name+" supported="+(pic.materialForRendering?.shader&&pic.materialForRendering.shader.isSupported)+" rect="+pic.rectTransform.rect+" position="+pic.transform.position+" canvas="+pic.canvas?.name+" canvasEnabled="+(pic.canvas&&pic.canvas.enabled)+" mode="+pic.canvas?.renderMode);
                    if(SessionState.GetBool(Key+"slotsOnly",false))
                    {
                        var rects=game.GetComponentsInChildren<UnityEngine.UI.RectMask2D>(true);var rectStates=rects.Select(m=>m.enabled).ToArray();foreach(var m in rects)m.enabled=false;
                        await Capture("casino-slots-without-rect-masks.png");for(int mi=0;mi<rects.Length;mi++)rects[mi].enabled=rectStates[mi];
                        var masks=game.GetComponentsInChildren<UnityEngine.UI.Mask>(true);var maskStates=masks.Select(m=>m.enabled).ToArray();foreach(var m in masks)m.enabled=false;
                        await Capture("casino-slots-without-stencil-masks.png");for(int mi=0;mi<masks.Length;mi++)masks[mi].enabled=maskStates[mi];
                        var materials=pictures.Select(p=>p.material).ToArray();foreach(var pic in pictures)pic.material=null;
                        await Capture("casino-slots-default-material.png");for(int mi=0;mi<pictures.Length;mi++)pictures[mi].material=materials[mi];
                    }
                }
                if(i==0&&SessionState.GetBool(Key+"smoke",false))
                {
                    var renderCamera=CasinoRuntimeContext.PlayerCamera;var data=renderCamera.GetComponent<UniversalAdditionalCameraData>();
                    bool post=data.renderPostProcessing;data.renderPostProcessing=false;await Capture("casino-blackjack-without-post.png");data.renderPostProcessing=post;
                    var volumes=casino.ModuleRoot.GetComponentsInChildren<Volume>(true);var layers=volumes.Select(v=>v.gameObject.layer).ToArray();int mask=data.volumeLayerMask;
                    for(int vi=0;vi<volumes.Length;vi++)volumes[vi].gameObject.layer=3;data.volumeLayerMask=1<<3;
                    await Capture("casino-blackjack-source-volumes.png");data.volumeLayerMask=mask;for(int vi=0;vi<volumes.Length;vi++)volumes[vi].gameObject.layer=layers[vi];
                    int oldMask=renderCamera.cullingMask;renderCamera.cullingMask=oldMask|(1<<1)|(1<<2)|(1<<3)|(1<<24);await Capture("casino-blackjack-source-layers.png");renderCamera.cullingMask=oldMask;
                }
                row.localFootHeight=casino.ModuleRoot.InverseTransformPoint(player.transform.position).y;row.grounded=row.localFootHeight>-.1f&&row.localFootHeight<1.5f;
                var deadline=DateTime.UtcNow.AddSeconds(120);float next=0;bool first=false;
                while(game&&(game.isPlaying||outcomes.Count(o=>o.type==row.type)==resultCount)&&DateTime.UtcNow<deadline)
                {
                    if(Time.time>=next)
                    {
                        next=Time.time+1;
                        if(game is Blackjack b)b.PlayerStand(casino.LogicInteractor);
                        if(game is Poker p)p.ConfirmSelection(casino.LogicInteractor);
                        if(game is Crash c)c.Cashout();
                        if(game is CrossyRoad cr){if(!first)cr.TakeStep();else cr.CashOut();}
                        if(game is DragonTower dt){if(!first)dt.GetComponentsInChildren<DragonTowerButton>().First(b=>CasinoLabBootstrap.Field<int>(b,"floorIndex")==0).ServerOnInteract(casino.LogicInteractor);else dt.Cashout(casino.LogicInteractor);}
                        if(game is Minesweeper ms){if(!first)ms.RevealTile(ms.GetComponentsInChildren<MinesweeperTile>().First());else ms.CashOut();}
                        if(game is Craps cp)
                        {
                            var dm=CasinoLabBootstrap.Field<DiceManager>(cp,"diceManager");var zone=CasinoLabBootstrap.Field<Transform>(dm,"diceZone");
                            foreach(var dice in CasinoLabBootstrap.Field<List<Dice>>(dm,"_diceList"))
                                if(dice.IsInteractable&&!CasinoLabBootstrap.Field<bool>(dice,"_isRolling")){dice.ServerOnInteract(casino.LogicInteractor);dice.ServerThrow(zone.position+Vector3.up,Quaternion.identity,Vector3.down*2,Vector3.one);}
                        }
                        first=true;
                    }
                    await UniTask.Yield();
                }
                row.finished=!game.isPlaying;row.resultRegistered=outcomes.Count(o=>o.type==row.type)>resultCount;
                if(row.resultRegistered){var outcome=outcomes.Last(o=>o.type==row.type);row.bet=outcome.bet;row.payout=outcome.payout;}
                row.balanceAfter=money.balance;row.storedAfter=wallet.SavedGoldStorage;
                row.exactWallet=money.balance==wallet.CasinoGoldCoins&&wallet.ExactGoldBalance==decimal.Parse(row.storedBefore)+(row.payout-row.bet)*100m;
                report.games.Add(row);Stage("GAME_"+row.name);
            }
            Stage("FULL_NUMBER_HUD_GEOMETRY");
            var ui=wallet.EconomyUI;var label=CasinoLabBootstrap.Field<TextMeshProUGUI>(ui,"_shopBalanceText");
            wallet.SetSavedGoldBalance(0,"0",wallet.ScrapBalance.Value);await Pause(.5f);
            wallet.SetSavedGoldBalance(0,"100000",wallet.ScrapBalance.Value);await Pause(.1f);
            var mid=CasinoLabBootstrap.Field<decimal>(ui,"displayedGoldStored");
            wallet.SetSavedGoldBalance(0,"50000",wallet.ScrapBalance.Value);
            var retarget=CasinoLabBootstrap.Field<decimal>(ui,"displayedGoldStored");await Pause(.5f);
            report.animationRetargeted=mid>0&&mid<100000&&mid==retarget&&label.text==GoldAmount.FullNumber(50000);
            foreach(var cents in new[]{"0","12300","100000000000000","922337203685477580700"})
            {
                wallet.SetSavedGoldBalance(0,cents,wallet.ScrapBalance.Value);await Pause(.6f);Canvas.ForceUpdateCanvases();label.ForceMeshUpdate(true,true);
                var visible=label.textInfo.characterInfo.Take(label.textInfo.characterCount).Where(ch=>ch.isVisible).ToArray();var rect=label.rectTransform.rect;
                report.labels.Add(new Label{requested=GoldAmount.FullNumber(decimal.Parse(cents)),text=label.text,font=label.fontSize,width=rect.width,meshWidth=label.textBounds.size.x,chars=label.textInfo.characterCount,visible=visible.Length,allInside=visible.All(ch=>ch.bottomLeft.x>=rect.xMin-1&&ch.topRight.x<=rect.xMax+1&&ch.bottomLeft.y>=rect.yMin-1&&ch.topRight.y<=rect.yMax+1)});
                await Capture("casino-host-gold-"+cents+".png");
            }
            report.sourceHudHidden=Resources.FindObjectsOfTypeAll<Canvas>().Where(c=>c&&c.enabled&&c.gameObject.scene.IsValid()&&c.renderMode!=RenderMode.WorldSpace).All(c=>!c.GetComponentInParent<LocalManager>()&&!c.GetComponentInParent<GameUI>());
            foreach(var c in Resources.FindObjectsOfTypeAll<Canvas>().Where(c=>c&&c.enabled&&c.gameObject.scene.IsValid()&&c.renderMode!=RenderMode.WorldSpace&&(c.GetComponentInParent<LocalManager>()||c.GetComponentInParent<GameUI>())))report.diagnostics.Add("RESIDUAL_HUD "+c.name+" parent="+c.transform.parent?.name+" active="+c.gameObject.activeInHierarchy+" rt="+c.worldCamera?.targetTexture);
            Stage("RETURN_AND_REENTER");
            wallet.SetSavedGoldBalance(0,"123456789012300",wallet.ScrapBalance.Value);await Pause(.5f);
            var saveManager=Singleton<PaperPixelGames.Core.SaveSystem.SaveManager>.Instance;int slotIndex=saveManager.CurrentSaveSlotIndex;var savedResult=saveManager.SaveHostSession();report.diagnostics.Add("QA_DISK_SAVE "+savedResult);
            Singleton<GameSessionLifecycle>.Instance.ReturnToMainMenu(ReturnToMainMenuRequest.UserRequested());
            await Wait(()=>IslandFrontEndFlow.Active&&IslandFrontEndFlow.Active.BackdropLoaded&&!Singleton<GameSessionLifecycle>.Instance.ReturnToMenuInProgress,120);await Pause(2);
            report.returnedToMenu=true;report.nativeServicesDisposed=!ParkCasinoIntegration.Instance&&!Mirror.NetworkServer.active&&!Mirror.NetworkClient.active&&!Object.FindFirstObjectByType<LocalManager>()&&!Object.FindFirstObjectByType<CasinoLabBootstrap>();
            Stage("REENTER_HOST");IslandFrontEndFlow.Active.ShowLoadGames();await Pause(1);
            var sameSlot=Resources.FindObjectsOfTypeAll<SaveSlot>().First(s=>s&&s.gameObject.scene.name=="MainMenuState"&&s.ContainsSave&&s.SlotIndex==slotIndex);sameSlot.LaunchOriginalSlot();
            await Wait(()=>(bool)Prior("Ready"),240);await Wait(()=>ParkCasinoIntegration.Instance&&ParkCasinoIntegration.Instance.Ready,180);await Pause(1);
            report.reenteredWithGold=ParkCasinoIntegration.Instance.HostEconomy.ExactGoldBalance==123456789012300m&&NetworkSingleton<MoneyManager>.Instance.balance==1234567890123L;
        }
        catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        finally
        {
            try
            {
                if(ParkCasinoIntegration.Instance&&Singleton<GameSessionLifecycle>.TryGetInstance(out var session))
                {
                    session.ReturnToMainMenu(ReturnToMainMenuRequest.UserRequested());
                    await Wait(()=>IslandFrontEndFlow.Active&&IslandFrontEndFlow.Active.BackdropLoaded&&!session.ReturnToMenuInProgress,120);await Pause(1);
                }
            }
            catch(Exception e){report.error+="\nGraceful QA shutdown: "+e;}
            Stage("EXITING_PLAY");SessionState.SetBool(Key+"done",true);EditorApplication.ExitPlaymode();
        }
    }
}
