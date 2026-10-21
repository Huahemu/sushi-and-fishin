namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Linq;
using global::System.Reflection;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::UnityEngine;
using global::UnityEngine.SceneManagement;
using global::UnityEngine.InputSystem;

namespace CasinoLab
{
    [DefaultExecutionOrder(-30000)]
    public sealed class CasinoLabBootstrap:MonoBehaviour
    {
        public static CasinoLabBootstrap Instance;
        public CustomNetworkManager networkPrefab;
        public GameObject supportServicesPrefab;
        public int seed=20261015;
        public bool ready;
        public string status="Loading original casino";
        CustomNetworkManager network;
        GameObject hostSupportInstance;
        public GameObject HostSupportInstance => hostSupportInstance;
        LocalManager hostLocalInstance;
        bool initializing;
        int pendingFloor=-1;
        public static event Action Ready;
        public static T Field<T>(object target,string name)
        {
            for(var t=target.GetType();t!=null;t=t.BaseType)
            {
                var f=t.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                if(f!=null)return (T)f.GetValue(target);
            }
            throw new MissingFieldException(target.GetType().FullName,name);
        }
        public static void Set(object target,string name,object value)
        {
            for(var t=target.GetType();t!=null;t=t.BaseType)
            {
                var f=t.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                if(f!=null) {f.SetValue(target,value);return;}
            }
            throw new MissingFieldException(target.GetType().FullName,name);
        }
        void Awake()
        {
            if(Instance!=null) {Destroy(gameObject);return;}
            Instance=this;DontDestroyOnLoad(gameObject);CasinoLabMode.Active=true;
            // Host HUD is retained; no standalone test menu.
            
            Application.runInBackground=true;
            if(Application.isBatchMode)
            {
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
                if(Keyboard.current==null)InputSystem.AddDevice<Keyboard>("Lab test keyboard");
                if(Mouse.current==null)InputSystem.AddDevice<Mouse>("Lab test mouse");
            }
        }
        IEnumerator Start()
        {
            var settings=SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
            settings.useAPIUpdates=false;settings.gameHasStarted=true;
            var lobby=SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
            lobby.currentPlayerCount=1;lobby.maxPlayers=1;lobby.createLobbyOnStart=false;
            lobby.players.Clear();lobby.players.Add(new PlayerInfo("Guest",0,Color.white));
            SushiAndFish.Casino.Original.CasinoResources.Load<InputReader>("InputReader");
            if(supportServicesPrefab!=null){hostSupportInstance=Instantiate(supportServicesPrefab);DontDestroyOnLoad(hostSupportInstance);}
            hostLocalInstance=Instantiate(SushiAndFish.Casino.Original.CasinoResources.Load<LocalManager>("LocalManager"));
            yield return null;
            yield return SceneManager.LoadSceneAsync("CasinoScene", LoadSceneMode.Additive);
            var loadedCasino=SceneManager.GetSceneByName("CasinoScene");
            foreach(var root in loadedCasino.GetRootGameObjects())CasinoRuntimeContext.SceneLoaded?.Invoke(root);
            SceneManager.SetActiveScene(loadedCasino);
            yield return null;
            ConfigureView();
            foreach(var floor in FindObjectsByType<CasinoFloor>(FindObjectsInactive.Include,FindObjectsSortMode.None))floor.gameObject.SetActive(true);
            network=Instantiate(networkPrefab);
            network.StartHost();
        }
        public void OnOriginalPlayersSpawned()
        {
            if(initializing)return;
            if(SceneManager.GetActiveScene().name.StartsWith("EndingCutscene_"))StartCoroutine(InitializeEnding());
            else StartCoroutine(InitializeCasino());
        }
        IEnumerator InitializeEnding()
        {
            initializing=true;ready=false;
            var game=NetworkSingleton<GameManager>.Instance;
            game.InitializeScene(SceneManager.GetActiveScene().name);
            yield return new WaitForSeconds(2);
            MonoSingleton<LocalManager>.Instance.mainCamera.enabled=true;
            status="Original ending scene — F8 can reset the casino";ready=true;initializing=false;
            Debug.Log("CASINO_LAB_ENDING_SCENE_READY "+SceneManager.GetActiveScene().name);
        }
        IEnumerator InitializeCasino()
        {
            initializing=true;ready=false;status="Preparing original floor generation";
            yield return null;
            var game=NetworkSingleton<GameManager>.Instance;
            ConfigureView();
            foreach(var player in MonoSingleton<LocalManager>.Instance.players)player.controller.ServerLock(true);
            foreach(var floor in FindObjectsByType<CasinoFloor>(FindObjectsInactive.Include,FindObjectsSortMode.None))floor.gameObject.SetActive(true);
            var gs=SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
            game.Networkstate=SceneManager.GetActiveScene().name=="WinStateScene"?GameState.Win:GameState.Game;
            game.NetworkcurrentQuota=gs.startingQuota;
            game.NetworkcurrentFloor=0;game.NetworkdaysLeft=gs.daysBeforeQuota;game.NetworkdaysPassed=0;
            game.NetworkrequiredQuotaToNextFloor=gs.floorData.Count>1?gs.floorData[1].requiredQuotaToAccess:0;
            Set(game,"_isTransitioning",false);Set(game,"_isTimeOver",false);game.Network_timer=0;
            NetworkSingleton<SeededRandomManager>.Instance.InitializeSeed(seed);
            NetworkSingleton<MoneyManager>.Instance.SetBalance(CasinoRuntimeContext.ReadCoins,null,ChangeType.Misc);
            NetworkSingleton<MoneyManager>.Instance.TrySetTicketBalance(0);
            NetworkSingleton<PayoutTracker>.Instance.InitializeStartingPoints();
            NetworkSingleton<GameResultsManager>.Instance.ClearResults();
            NetworkSingleton<UpgradeManager>.Instance.ServerResetAllUpgradesToDefaults();
            NetworkSingleton<OrganManager>.Instance.ServerApplyAllOrganSettings();
            NetworkSingleton<NavMeshManager>.Instance.ClearNavMesh();
            NetworkSingleton<ChallengeManager>.Instance.InitializeChallenges(false,false);
            // Scene switching spawns only identities whose parents are active.
            // Floors are now active and the wallet is ready for Boss OnStartServer.
            NetworkServer.SpawnObjects();
            var stamp=FindFirstObjectByType<StampManager>(FindObjectsInactive.Include);
            if(stamp!=null)
            {
                stamp.gameObject.SetActive(true);
                yield return stamp.InitializeManager();
                yield return new WaitForSeconds(0.35f);
            }
            var elevator=NetworkSingleton<ElevatorManager>.Instance;
            elevator.Initialize();elevator.RpcEnableAllButtons();
            var floorButtons=Field<List<Transform>>(elevator,"buttonList");
            bool bossScene=SceneManager.GetActiveScene().name=="WinStateScene";
            for(int i=0;i<floorButtons.Count;i++)floorButtons[i].gameObject.SetActive(true);
            for(int i=0;i<floorButtons.Count;i++)
            {
                if(!bossScene && FindFirstObjectByType<CasinoLabBossRoom>(FindObjectsInactive.Include)==null && i!=5)continue;
                int destination=i;
                var button=floorButtons[i].GetComponent<InteractableEventTrigger>();
                for(int j=0;j<button.serverOnInteractEvent.GetPersistentEventCount();j++)button.serverOnInteractEvent.SetPersistentListenerState(j,UnityEngine.Events.UnityEventCallState.Off);
                button.serverOnInteractEvent.AddListener(_=>
                {
                    if(!ready || elevator.isTeleporting || !MonoSingleton<LocalManager>.Instance.players.All(p=>elevator.IsInElevator(p.transform.position)))return;
                    if(destination==5)OpenBossRoom();
                    else OpenCasinoFloor(destination);
                });
            }
            var ui=NetworkSingleton<GameUI>.Instance;
            ui.ServerToggleTimer(false);ui.ServerToggleStatusUI(false);ui.ServerToggleMoneyUI(true);
            ui.ServerToggleCrosshair(true);ui.ServerToggleItemInputsUI(true);
            ui.ServerSetLoadingScreen(false,0);
            yield return null;
            foreach(var player in MonoSingleton<LocalManager>.Instance.players)
            {
                player.controller.ServerLock(false);
                player.controller.State=PlayerController.PlayerState.Free;
            }
            if(pendingFloor>=0)
            {
                int destination=pendingFloor;pendingFloor=-1;
                elevator.ServerForceTeleportPlayers(destination);
                while(elevator.isTeleporting)yield return null;
            }
            // A host town player already has an authored spawn. Target RPCs
            // sent here arrive after Ready invokes the host's follow callback,
            // so even an initialization-time callback guard cannot stop this
            // deferred entrance teleport from moving the host out of town.
            else if (!CasinoRuntimeContext.IsHostIntegration) ReturnToEntrance();
            RecoveryShaderSupport.RequestRuntimeRefresh();
            MonoSingleton<LocalManager>.Instance.mainCamera.enabled=true;
            UICursorSimple.Instance?.HideCursor();
            InputEvents.ActiveLayer=InputLayer.Default;
            status="Ready — original casino rules, unlimited test funds";
            initializing=false;ready=true;Ready?.Invoke();
            Debug.Log("CASINO_LAB_READY scene="+SceneManager.GetActiveScene().name+" players="+MonoSingleton<LocalManager>.Instance.players.Count+" games="+FindObjectsByType<GameBase>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
        }
        void LateUpdate()
        {
            if(!NetworkServer.active || !ready)return;
            var wallet=NetworkSingleton<MoneyManager>.Instance;
            // The host wallet is authoritative; never replenish test funds.
            
        }
        void ConfigureView()
        {
            if (CasinoRuntimeContext.IsHostIntegration) { MonoSingleton<LocalManager>.Instance.mainCamera = CasinoRuntimeContext.PlayerCamera; return; }
            var camera=MonoSingleton<LocalManager>.Instance.mainCamera;
            camera.enabled=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0.12f,0.14f,0.18f);
        }
        public void ReturnToEntrance()
        {
            if(SceneManager.GetActiveScene().name=="WinStateScene"){OpenCasinoFloor(0);return;}
            if(SceneManager.GetActiveScene().name.StartsWith("EndingCutscene_")){ResetCasino();return;}
            StartCoroutine(ReturnToEntranceRoutine());
        }
        IEnumerator ReturnToEntranceRoutine()
        {
            var elevator=NetworkSingleton<ElevatorManager>.Instance;
            if(FindFirstObjectByType<CasinoLabBossRoom>(FindObjectsInactive.Include)!=null && Field<int>(elevator,"_currentFloorIndex")==5)
                yield return TravelEmbedded(0);
            if(elevator!=null && Field<int>(elevator,"_currentFloorIndex")!=0)
            {
                elevator.ServerForceTeleportPlayers(0);
                while(elevator.isTeleporting)yield return null;
            }
            var marker=GameObject.Find("CasinoLabEntranceSpawn");
            if(marker==null)yield break;
            foreach(var player in MonoSingleton<LocalManager>.Instance.players)
            {
                player.controller.ServerTeleport(marker.transform.position);
                player.controller.ServerRotate(new Vector2(marker.transform.eulerAngles.y,0));
            }
        }
        public void ResetCasino()
        {
            if (CasinoRuntimeContext.IsHostIntegration) { OpenCasinoFloor(0); return; }
            if(!NetworkServer.active)return;
            pendingFloor=-1;ready=false;seed++;Set(network,"lastSceneReadySent",null);network.ServerChangeScene("CasinoScene");
        }
        public void OpenCasinoFloor(int floor)
        {
            if (CasinoRuntimeContext.IsHostIntegration) { if (ready && NetworkServer.active) StartCoroutine(TravelEmbedded(Mathf.Clamp(floor, 0, 4))); return; }
            if(!NetworkServer.active || !ready)return;
            if(SceneManager.GetActiveScene().name=="CasinoScene" && FindFirstObjectByType<CasinoLabBossRoom>(FindObjectsInactive.Include)!=null)
            {StartCoroutine(TravelEmbedded(Mathf.Clamp(floor,0,4)));return;}
            pendingFloor=Mathf.Clamp(floor,0,4);
            ready=false;Set(network,"lastSceneReadySent",null);network.ServerChangeScene("CasinoScene");
        }
        public void OpenBossRoom()
        {
            if (CasinoRuntimeContext.IsHostIntegration) { if (ready && NetworkServer.active) StartCoroutine(TravelEmbedded(5)); return; }
            if(!NetworkServer.active || !ready)return;
            if(SceneManager.GetActiveScene().name=="CasinoScene" && FindFirstObjectByType<CasinoLabBossRoom>(FindObjectsInactive.Include)!=null)
            {StartCoroutine(TravelEmbedded(5));return;}
            if(SceneManager.GetActiveScene().name=="WinStateScene")
            {
                NetworkSingleton<ElevatorManager>.Instance.ServerTryTeleportPlayers(1);return;
            }
            NetworkSingleton<MoneyManager>.Instance.SetBalance(CasinoRuntimeContext.ReadCoins,null,ChangeType.Misc);
            pendingFloor=1;ready=false;Set(network,"lastSceneReadySent",null);network.ServerChangeScene("WinStateScene");
        }
        IEnumerator TravelEmbedded(int destination)
        {
            var elevator=NetworkSingleton<ElevatorManager>.Instance;
            if(!ready || elevator.isTeleporting)yield break;
            int current=Field<int>(elevator,"_currentFloorIndex");
            if(current==destination)yield break;
            var boss=FindFirstObjectByType<CasinoLabBossRoom>(FindObjectsInactive.Include);
            ready=false;status="Moving between original casino floors";
            var game=NetworkSingleton<GameManager>.Instance;
            if(destination==5)
            {
                NetworkSingleton<MoneyManager>.Instance.SetBalance(CasinoRuntimeContext.ReadCoins,null,ChangeType.Misc);
                boss.PrepareVisit();game.Networkstate=GameState.Game;
            }
            else
            {
                if(current==5)boss.EndVisit();
                game.Networkstate=GameState.Game;
            }
            if(MonoSingleton<LocalManager>.Instance.players.All(p=>elevator.IsInElevator(p.transform.position)))elevator.ServerTryTeleportPlayers(destination);
            else elevator.ServerForceTeleportPlayers(destination);
            while(elevator.isTeleporting)yield return null;
            status=destination==5?"Ready — casino boss office":"Ready — original casino rules, unlimited test funds";ready=true;
            Debug.Log("CASINO_LAB_FLOOR_READY "+destination);
        }
        void OnApplicationQuit(){if(CasinoLabMode.Active && !CasinoRuntimeContext.IsHostIntegration)DG.Tweening.DOTween.KillAll(false);}
        public void ShutdownForHost()
        {
            StopAllCoroutines();
            if(network)
            {
                network.offlineScene="";
                if(NetworkServer.active||NetworkClient.active)network.StopHost();
                Destroy(network.gameObject);
            }
            if(hostLocalInstance)Destroy(hostLocalInstance.gameObject);
            if(hostSupportInstance)Destroy(hostSupportInstance);
            if(Instance==this)Instance=null;
            Destroy(gameObject);
        }
        public void ObserveEnding(GameState state)
        {
            Debug.Log("CASINO_LAB_ENDING_OBSERVED "+state+" (test mode stays in the casino)");
            status="Original result reached. F8 opens reset and room controls.";
        }
    }
}

}
