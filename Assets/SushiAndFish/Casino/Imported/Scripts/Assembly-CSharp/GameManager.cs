namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Diagnostics;
using global::System.Linq;
using global::System.Runtime.CompilerServices;
using global::System.Runtime.InteropServices;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::MoreMountains.Feedbacks;
using global::SkyBrave_Toolkit.Scripts.Scriptable_Game_Events;
using global::SushiAndFish.Casino.Steamworks;
using global::UnityEngine;

public class GameManager : NetworkSingleton<GameManager>
{
	[CompilerGenerated]
	private sealed class _003CInitializeSceneRoutine_003Ed__59 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public string sceneName;

		public GameManager _003C_003E4__this;

		object IEnumerator<object>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CInitializeSceneRoutine_003Ed__59(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
		}

		private bool MoveNext()
		{
			int num = _003C_003E1__state;
			GameManager gameManager = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				switch (sceneName)
				{
				case "HomeScene":
					_003C_003E2__current = gameManager.LobbyInitializeRoutine();
					_003C_003E1__state = 1;
					return true;
				case "CasinoScene":
					_003C_003E2__current = gameManager.GameInitializeRoutine();
					_003C_003E1__state = 2;
					return true;
				case "LoseStateScene":
					_003C_003E2__current = gameManager.LoseInitializeRoutine();
					_003C_003E1__state = 3;
					return true;
				case "WinStateScene":
					_003C_003E2__current = gameManager.WinInitializeRoutine();
					_003C_003E1__state = 4;
					return true;
				case "EndingCutscene_Coinflip_Won":
				case "EndingCutscene_Coinflip_Lost":
				case "EndingCutscene_Debt_Paid":
					_003C_003E2__current = gameManager.CutsceneInitializeRoutine();
					_003C_003E1__state = 5;
					return true;
				case "SummaryScene":
					_003C_003E2__current = gameManager.SummaryInitializeRoutine();
					_003C_003E1__state = 6;
					return true;
				case "FollowUs":
					_003C_003E2__current = gameManager.FollowUsInitializeRoutine();
					_003C_003E1__state = 7;
					return true;
				case "GameTest":
					_003C_003E2__current = gameManager.TestInitializeRoutine();
					_003C_003E1__state = 8;
					return true;
				}
				goto IL_02a5;
			case 1:
				_003C_003E1__state = -1;
				goto IL_02a5;
			case 2:
				_003C_003E1__state = -1;
				goto IL_02a5;
			case 3:
				_003C_003E1__state = -1;
				goto IL_02a5;
			case 4:
				_003C_003E1__state = -1;
				goto IL_02a5;
			case 5:
				_003C_003E1__state = -1;
				goto IL_02a5;
			case 6:
				_003C_003E1__state = -1;
				goto IL_02a5;
			case 7:
				_003C_003E1__state = -1;
				goto IL_02a5;
			case 8:
				_003C_003E1__state = -1;
				goto IL_02a5;
			case 9:
				{
					_003C_003E1__state = -1;
					return false;
				}
				IL_02a5:
				gameManager.sceneInitCompleted.Raise();
				gameManager.RpcSceneInitialized(gameManager._sceneEpoch);
				_003C_003E2__current = null;
				_003C_003E1__state = 9;
				return true;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}
	}

	[Header("Feedbacks")]
	[SerializeField]
	private MMF_Player startFb;

	[SerializeField]
	private MMF_Player failFb;

	[SerializeField]
	private MMF_Player dayEndFb;

	[SerializeField]
	private DaySummaryUI daySummaryUI;

	[SerializeField]
	private GameOverUI gameOverUI;

	[Header("Debug")]
	[SyncVar(hook = "OnDaysChanged")]
	[ReadOnly]
	public int daysLeft;

	[SyncVar(hook = "OnDaysPassedChanged")]
	[ReadOnly]
	public int daysPassed;

	[SyncVar]
	[ReadOnly]
	public int successfulQuota;

	[SyncVar(hook = "OnQuotaChanged")]
	[ReadOnly]
	public long currentQuota;

	[SyncVar]
	[ReadOnly]
	public int currentFloor;

	[SyncVar]
	[ReadOnly]
	public long requiredQuotaToNextFloor;

	[SyncVar]
	[ReadOnly]
	public int currentTicketReward;

	[SyncVar]
	[ReadOnly]
	public bool isDebtPaid;

	[SyncVar]
	[ReadOnly]
	public GameState state;

	[SyncVar(hook = "OnTimerChanged")]
	private float _timer;

	private GameSettings _gs;

	public GameEvent onQuotaAchieved;

	public GameEvent onFloorProgressed;

	public GameEvent onDayEnded;

	public GameEvent sceneInitCompleted;

	private bool _isTimeOver;

	private bool _isTransitioning;

	private int _sceneEpoch;

	private readonly HashSet<int> _scenePlayReady = new HashSet<int>();

	private int _expectedScenePlayers;

	public Action<int, int> _Mirror_SyncVarHookDelegate_daysLeft;

	public Action<int, int> _Mirror_SyncVarHookDelegate_daysPassed;

	public Action<long, long> _Mirror_SyncVarHookDelegate_currentQuota;

	public Action<float, float> _Mirror_SyncVarHookDelegate__timer;

	public bool HasDayStarted { get; private set; }

	public int NetworkdaysLeft
	{
		get
		{
			return daysLeft;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref daysLeft, 1uL, _Mirror_SyncVarHookDelegate_daysLeft);
		}
	}

	public int NetworkdaysPassed
	{
		get
		{
			return daysPassed;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref daysPassed, 2uL, _Mirror_SyncVarHookDelegate_daysPassed);
		}
	}

	public int NetworksuccessfulQuota
	{
		get
		{
			return successfulQuota;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref successfulQuota, 4uL, null);
		}
	}

	public long NetworkcurrentQuota
	{
		get
		{
			return currentQuota;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref currentQuota, 8uL, _Mirror_SyncVarHookDelegate_currentQuota);
		}
	}

	public int NetworkcurrentFloor
	{
		get
		{
			return currentFloor;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref currentFloor, 16uL, null);
		}
	}

	public long NetworkrequiredQuotaToNextFloor
	{
		get
		{
			return requiredQuotaToNextFloor;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref requiredQuotaToNextFloor, 32uL, null);
		}
	}

	public int NetworkcurrentTicketReward
	{
		get
		{
			return currentTicketReward;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref currentTicketReward, 64uL, null);
		}
	}

	public bool NetworkisDebtPaid
	{
		get
		{
			return isDebtPaid;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref isDebtPaid, 128uL, null);
		}
	}

	public GameState Networkstate
	{
		get
		{
			return state;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref state, 256uL, null);
		}
	}

	public float Network_timer
	{
		get
		{
			return _timer;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _timer, 512uL, _Mirror_SyncVarHookDelegate__timer);
		}
	}

	public event Action<long, long> OnQuotaChangedEvent;

	protected override void OnAwake()
	{
		base.OnAwake();
		_gs = SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
	}

	public override void OnStartServer()
	{
		StartCoroutine(NetworkSingleton<SaveManager>.Instance.LoadGameSaveCoroutine());
	}

	private void OnTimerChanged(float oldValue, float newValue)
	{
		NetworkSingleton<GameUI>.Instance.SetTimerText(_gs.dayDuration - newValue);
	}

	private void OnDaysChanged(int oldValue, int newValue)
	{
	}

	private void OnDaysPassedChanged(int oldValue, int newValue)
	{
		NetworkSingleton<GameUI>.Instance.SetDaysText(newValue + 1);
	}

	private void OnQuotaChanged(long oldValue, long newValue)
	{
		OnQuotaChangedEvent?.Invoke(oldValue, newValue);
		NetworkSingleton<GameUI>.Instance.SetFloorText(currentFloor);
	}

	public void StartDay()
	{
		HasDayStarted = true;
	}

	private void Update()
	{
		if (base.isServer)
		{
			SetTimer();
		}
	}

	private void SetTimer()
	{
		if (CasinoLab.CasinoLabMode.Active && CasinoLab.CasinoLabMode.FreezeDayTimer) return;
		if (state == GameState.Game && !_isTransitioning && HasDayStarted && !_isTimeOver)
		{
			Network_timer = Mathf.Min(_timer + Time.deltaTime, _gs.dayDuration);
			if (_timer >= _gs.dayDuration)
			{
				StartCoroutine(OnTimerEnd());
			}
		}
	}

	[Server]
	public void ServerAdjustTimer(float deltaSeconds)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void GameManager::ServerAdjustTimer(System.Single)' called when server was not active");
		}
		else if (!Mathf.Approximately(deltaSeconds, 0f) && !(_gs == null))
		{
			float network_timer = Mathf.Clamp(_timer + deltaSeconds, 0f, _gs.dayDuration);
			Network_timer = network_timer;
		}
	}

	private IEnumerator OnTimerEnd()
	{
		_isTimeOver = true;
		RpcPlayDayEndFeedback();
		onDayEnded?.Raise();
		yield return new WaitForSeconds(0.5f);
		foreach (ConsumableItem item in NetworkSingleton<ItemManager>.Instance.spawnedItemInstances.ToList())
		{
			item.DestroyItem();
		}
		NetworkSingleton<ElevatorManager>.Instance.IsLocked = true;
		NetworkSingleton<ElevatorManager>.Instance.ServerForceTeleportPlayers(0);
	}

	private void ServerPayDebt()
	{
		NetworkSingleton<MoneyManager>.Instance.TryChangeTicketBalance(currentTicketReward);
		ProgressNextQuota();
	}

	[Server]
	private void ProgressNextQuota()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void GameManager::ProgressNextQuota()' called when server was not active");
			return;
		}
		NetworksuccessfulQuota = successfulQuota + 1;
		NetworkdaysLeft = _gs.daysBeforeQuota;
		NetworkSingleton<MoneyManager>.Instance.TryChangeTicketBalance(_gs.GetQuotaExcessReward(currentFloor, currentQuota, NetworkSingleton<MoneyManager>.Instance.balance));
		NetworkcurrentQuota = _gs.GetQuota(successfulQuota, currentQuota, NetworkSingleton<MoneyManager>.Instance.balance);
		onQuotaAchieved?.Raise();
		if (successfulQuota >= requiredQuotaToNextFloor)
		{
			ProgressFloor();
		}
	}

	[Server]
	public void ServerGetAuxiliaryMoney()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void GameManager::ServerGetAuxiliaryMoney()' called when server was not active");
		}
		else
		{
			NetworkSingleton<MoneyManager>.Instance.TryChangeBalance(_gs.GetAuxiliaryMoney(daysLeft, currentQuota), null, ChangeType.Misc);
		}
	}

	[Server]
	private void ProgressFloor()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void GameManager::ProgressFloor()' called when server was not active");
			return;
		}
		NetworkcurrentFloor = currentFloor + 1;
		if (_gs.floorData != null && currentFloor + 1 < _gs.floorData.Count)
		{
			NetworkrequiredQuotaToNextFloor = _gs.floorData[currentFloor + 1].requiredQuotaToAccess;
		}
		else
		{
			NetworkrequiredQuotaToNextFloor = long.MaxValue;
		}
		onFloorProgressed?.Raise();
	}

	[Server]
	public void ServerSetScene(GameState newState)
	{
		if (CasinoLab.CasinoLabMode.Active && newState != GameState.Game && newState != GameState.Win) { CasinoLab.CasinoLabBootstrap.Instance?.ObserveEnding(newState); return; }
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void GameManager::ServerSetScene(GameState)' called when server was not active");
		}
		else
		{
			StartCoroutine(ServerSetSceneRoutine(newState));
		}
	}

	private IEnumerator ServerSetSceneRoutine(GameState newState)
	{
		if (!_isTransitioning && state != newState)
		{
			_isTransitioning = true;
			RpcResetSummaryAndGameOverUI();
			NetworkSingleton<GameUI>.Instance.ServerSetLoadingScreen(isEnabled: true, 0.5f);
			yield return new WaitForSeconds(0.6f);
			RpcToggleInputs(1);
			switch (newState)
			{
			case GameState.Lobby:
				NetworkManager.singleton.ServerChangeScene("HomeScene");
				break;
			case GameState.Game:
				NetworkManager.singleton.ServerChangeScene("CasinoScene");
				break;
			case GameState.Lose:
				NetworkManager.singleton.ServerChangeScene("LoseStateScene");
				break;
			case GameState.Win:
				NetworkManager.singleton.ServerChangeScene("WinStateScene");
				break;
			case GameState.Test:
				NetworkManager.singleton.ServerChangeScene("GameTest");
				break;
			case GameState.Cutscene:
				NetworkManager.singleton.ServerChangeScene("EndingCutscene_Coinflip_Won");
				break;
			case GameState.Summary:
				NetworkManager.singleton.ServerChangeScene("SummaryScene");
				break;
			case GameState.FollowUs:
				NetworkManager.singleton.ServerChangeScene("FollowUs");
				break;
			}
		}
	}

	[Server]
	public void ShowDayStats()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void GameManager::ShowDayStats()' called when server was not active");
			return;
		}
		RpcDayStatsFeedback();
		ServerLockPlayers();
		ServerLockPlayerHeads();
		RpcLockInputs();
	}

	[Server]
	public void ShowGameOverStats()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void GameManager::ShowGameOverStats()' called when server was not active");
			return;
		}
		RpcPlayGameOverFeedback();
		ServerLockPlayers();
		ServerLockPlayerHeads();
		RpcLockInputs();
	}

	[Server]
	private void ServerLockPlayers()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void GameManager::ServerLockPlayers()' called when server was not active");
			return;
		}
		foreach (PlayerReferences player in MonoSingleton<LocalManager>.Instance.players)
		{
			player.controller.ServerLock(isLocked: true);
		}
	}

	[Server]
	private void ServerLockPlayerHeads()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void GameManager::ServerLockPlayerHeads()' called when server was not active");
			return;
		}
		foreach (PlayerReferences player in MonoSingleton<LocalManager>.Instance.players)
		{
			player.controller.ServerLockHead(isLocked: true);
		}
	}

	[ClientRpc]
	private void RpcLockInputs()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void GameManager::RpcLockInputs()", -1945655203, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void ProgressGame()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void GameManager::ProgressGame()' called when server was not active");
		}
		else
		{
			StartCoroutine(ProgressGameRoutine());
		}
	}

	private IEnumerator ProgressGameRoutine()
	{
		if (_isTransitioning)
		{
			yield break;
		}
		_isTransitioning = true;
		NetworkSingleton<GameUI>.Instance.ServerSetLoadingScreen(isEnabled: true, 0.5f);
		yield return new WaitForSeconds(1f);
		RpcToggleInputs(1);
		if (state == GameState.Lose || state == GameState.Summary)
		{
			NetworkSingleton<SaveManager>.Instance.ResetCurrentSaveToDefaults();
			NetworkSingleton<SaveManager>.Instance.LoadGame();
		}
		if (state == GameState.Lobby)
		{
			if (successfulQuota >= _gs.quotas.Length)
			{
				NetworkManager.singleton.ServerChangeScene("WinStateScene");
			}
			else
			{
				NetworkManager.singleton.ServerChangeScene("CasinoScene");
			}
			yield break;
		}
		if (state == GameState.Game)
		{
			NetworkdaysLeft = daysLeft - 1;
			NetworkdaysPassed = daysPassed + 1;
			if (daysLeft <= 0 && NetworkSingleton<MoneyManager>.Instance.balance < currentQuota)
			{
				NetworkManager.singleton.ServerChangeScene("LoseStateScene");
				yield break;
			}
			ServerPayDebt();
			NetworkManager.singleton.ServerChangeScene("HomeScene");
			yield break;
		}
		if (state == GameState.Lose)
		{
			NetworkManager.singleton.ServerChangeScene("HomeScene");
			yield break;
		}
		if (state == GameState.Cutscene)
		{
			NetworkManager.singleton.ServerChangeScene("SummaryScene");
		}
		if (state == GameState.Summary)
		{
			NetworkManager.singleton.ServerChangeScene("FollowUs");
		}
		if (state == GameState.FollowUs)
		{
			NetworkManager.singleton.ServerChangeScene("HomeScene");
		}
		if (state == GameState.Test)
		{
			NetworkManager.singleton.ServerChangeScene("HomeScene");
		}
	}

	[Server]
	public void ServerSetCutscene(int cutsceneIndex)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void GameManager::ServerSetCutscene(System.Int32)' called when server was not active");
		}
		else
		{
			StartCoroutine(SetCutsceneRoutine(cutsceneIndex));
		}
	}

	private IEnumerator SetCutsceneRoutine(int cutsceneIndex)
	{
		if (!_isTransitioning)
		{
			_isTransitioning = true;
			NetworkSingleton<GameUI>.Instance.ServerSetLoadingScreen(isEnabled: true, 0.1f, loadingScreen: false);
			yield return new WaitForSeconds(0.2f);
			RpcToggleInputs(1);
			switch (cutsceneIndex)
			{
			case 0:
				NetworkManager.singleton.ServerChangeScene("EndingCutscene_Coinflip_Won");
				break;
			case 1:
				NetworkManager.singleton.ServerChangeScene("EndingCutscene_Coinflip_Lost");
				break;
			case 2:
				NetworkManager.singleton.ServerChangeScene("EndingCutscene_Debt_Paid");
				break;
			case 3:
				NetworkManager.singleton.ServerChangeScene("SummaryScene");
				break;
			}
		}
	}

	[Server]
	public void InitializeScene(string sceneName)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void GameManager::InitializeScene(System.String)' called when server was not active");
			return;
		}
		_sceneEpoch++;
		_scenePlayReady.Clear();
		_expectedScenePlayers = 1;
		if (NetworkSingleton<PlayerSpawnManager>.Instance != null)
		{
			_expectedScenePlayers = Mathf.Max(1, NetworkSingleton<PlayerSpawnManager>.Instance.RegisteredCount);
		}
		StartCoroutine(InitializeSceneRoutine(sceneName));
	}

	[IteratorStateMachine(typeof(_003CInitializeSceneRoutine_003Ed__59))]
	[Server]
	private IEnumerator InitializeSceneRoutine(string sceneName)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator GameManager::InitializeSceneRoutine(System.String)' called when server was not active");
			return null;
		}
		return new _003CInitializeSceneRoutine_003Ed__59(0)
		{
			_003C_003E4__this = this,
			sceneName = sceneName
		};
	}

	private IEnumerator LobbyInitializeRoutine()
	{
		Networkstate = GameState.Lobby;
		SteamMatchmaking.SetLobbyJoinable(MonoSingleton<LobbyManager>.Instance.GetCurrentLobbyID(), bLobbyJoinable: true);
		PredictNextCasinoGames();
		yield return null;
		NetworkSingleton<SaveManager>.Instance.SaveGame();
		yield return StartCoroutine(NetworkSingleton<ItemStampManager>.Instance.InitializeManager());
		NetworkcurrentTicketReward = _gs.GetTicketReward(daysPassed);
		NetworkSingleton<ChallengeManager>.Instance.InitializeChallenges(enabled: true, canComplete: false);
		NetworkSingleton<MoneyManager>.Instance.TryChangeTicketBalance(_gs.dailyTicketReward);
		NetworkSingleton<OrganManager>.Instance.ServerApplyAllOrganSettings();
		RpcLoadAllPlayerCosmetics();
		NetworkSingleton<UpgradeManager>.Instance.ServerResetAllUpgradesToDefaults();
		NetworkSingleton<ItemManager>.Instance.ServerResetItems();
		NetworkSingleton<ChallengeManager>.Instance.ServerResetAllChallenges();
		NetworkSingleton<ChallengeManager>.Instance.ServerClearChallengesUI();
		NetworkSingleton<NavMeshManager>.Instance.ClearNavMesh();
		NetworkSingleton<GameUI>.Instance.ServerToggleTimer(isEnabled: false);
		NetworkSingleton<GameUI>.Instance.ServerToggleStatusUI(isEnabled: true);
		NetworkSingleton<GameUI>.Instance.ServerToggleMoneyUI(isEnabled: true);
		NetworkSingleton<GameUI>.Instance.ServerToggleCrosshair(isEnabled: true);
		NetworkSingleton<GameUI>.Instance.ServerToggleItemInputsUI(isEnabled: true);
		RpcSetInLobbyPresence();
		yield return SushiAndFish.Casino.Original.CasinoResources.UnloadUnusedAssets();
	}

	private IEnumerator GameInitializeRoutine()
	{
		Networkstate = GameState.Game;
		SteamMatchmaking.SetLobbyJoinable(MonoSingleton<LobbyManager>.Instance.GetCurrentLobbyID(), bLobbyJoinable: false);
		yield return StartCoroutine(NetworkSingleton<StampManager>.Instance.InitializeManager());
		yield return new WaitForSeconds(3f);
		NetworkSingleton<RequestSettingsFromApi>.Instance.ReloadSettings();
		NetworkSingleton<PayoutTracker>.Instance.InitializeStartingPoints();
		NetworkSingleton<GameResultsManager>.Instance.ClearResults();
		RpcClearDaySummary();
		NetworkSingleton<ChallengeManager>.Instance.InitializeChallenges(enabled: true, canComplete: true);
		NetworkSingleton<NavMeshManager>.Instance.ClearNavMesh();
		NetworkSingleton<ElevatorManager>.Instance.Initialize();
		NetworkSingleton<MoneyManager>.Instance.SetDayStartBalance();
		NetworkSingleton<OrganManager>.Instance.ServerApplyAllOrganSettings();
		RpcLoadAllPlayerCosmetics();
		HasDayStarted = false;
		_isTimeOver = false;
		Network_timer = 0f;
		NetworkSingleton<GameUI>.Instance.ServerToggleTimer(isEnabled: true);
		NetworkSingleton<GameUI>.Instance.ServerToggleStatusUI(isEnabled: true);
		NetworkSingleton<GameUI>.Instance.ServerToggleMoneyUI(isEnabled: true);
		NetworkSingleton<GameUI>.Instance.ServerToggleCrosshair(isEnabled: true);
		NetworkSingleton<GameUI>.Instance.ServerToggleItemInputsUI(isEnabled: true);
		if (daysPassed == 0)
		{
			RpcPlayStartFeedback();
		}
		RpcSetInGamePresence();
		yield return new WaitForSeconds(1f);
		yield return SushiAndFish.Casino.Original.CasinoResources.UnloadUnusedAssets();
	}

	private IEnumerator LoseInitializeRoutine()
	{
		Networkstate = GameState.Lose;
		SteamMatchmaking.SetLobbyJoinable(MonoSingleton<LobbyManager>.Instance.GetCurrentLobbyID(), bLobbyJoinable: false);
		NetworkSingleton<ChallengeManager>.Instance.InitializeChallenges(enabled: false, canComplete: false);
		NetworkSingleton<OrganManager>.Instance.ServerApplyAllOrganSettings();
		RpcLoadAllPlayerCosmetics();
		NetworkSingleton<GameUI>.Instance.ServerToggleTimer(isEnabled: false);
		NetworkSingleton<GameUI>.Instance.ServerToggleStatusUI(isEnabled: false);
		NetworkSingleton<GameUI>.Instance.ServerToggleMoneyUI(isEnabled: false);
		NetworkSingleton<GameUI>.Instance.ServerToggleCrosshair(isEnabled: true);
		NetworkSingleton<GameUI>.Instance.ServerToggleItemInputsUI(isEnabled: false);
		NetworkSingleton<NavMeshManager>.Instance.ClearNavMesh();
		yield return new WaitForSeconds(1f);
		yield return SushiAndFish.Casino.Original.CasinoResources.UnloadUnusedAssets();
		RpcPlayFailFeedback();
	}

	private IEnumerator WinInitializeRoutine()
	{
		Networkstate = GameState.Win;
		SteamMatchmaking.SetLobbyJoinable(MonoSingleton<LobbyManager>.Instance.GetCurrentLobbyID(), bLobbyJoinable: false);
		NetworkSingleton<ChallengeManager>.Instance.InitializeChallenges(enabled: false, canComplete: false);
		NetworkSingleton<OrganManager>.Instance.ServerApplyAllOrganSettings();
		RpcLoadAllPlayerCosmetics();
		NetworkSingleton<NavMeshManager>.Instance.ClearNavMesh();
		NetworkSingleton<ElevatorManager>.Instance.Initialize();
		NetworkSingleton<GameUI>.Instance.ServerToggleTimer(isEnabled: false);
		NetworkSingleton<GameUI>.Instance.ServerToggleStatusUI(isEnabled: false);
		NetworkSingleton<GameUI>.Instance.ServerToggleMoneyUI(isEnabled: true, showCont: false);
		NetworkSingleton<GameUI>.Instance.ServerToggleCrosshair(isEnabled: true);
		NetworkSingleton<GameUI>.Instance.ServerToggleItemInputsUI(isEnabled: false);
		yield return new WaitForSeconds(1f);
		yield return SushiAndFish.Casino.Original.CasinoResources.UnloadUnusedAssets();
	}

	private IEnumerator CutsceneInitializeRoutine()
	{
		Networkstate = GameState.Cutscene;
		NetworkSingleton<OrganManager>.Instance.ServerApplyAllOrganSettings();
		RpcLoadAllPlayerCosmetics();
		NetworkSingleton<CreditsRollManager>.Instance.BeginCreditsFromScenePlayers();
		NetworkSingleton<GameUI>.Instance.ServerToggleTimer(isEnabled: false);
		NetworkSingleton<GameUI>.Instance.ServerToggleStatusUI(isEnabled: false);
		NetworkSingleton<GameUI>.Instance.ServerToggleMoneyUI(isEnabled: false);
		NetworkSingleton<GameUI>.Instance.ServerToggleCrosshair(isEnabled: false);
		NetworkSingleton<GameUI>.Instance.ServerToggleItemInputsUI(isEnabled: false);
		yield return new WaitForSeconds(1f);
		yield return SushiAndFish.Casino.Original.CasinoResources.UnloadUnusedAssets();
		NetworkSingleton<EndingSequenceManager>.Instance.ServerStartSequence();
	}

	private IEnumerator SummaryInitializeRoutine()
	{
		Networkstate = GameState.Summary;
		NetworkSingleton<GameUI>.Instance.ServerToggleTimer(isEnabled: false);
		NetworkSingleton<GameUI>.Instance.ServerToggleStatusUI(isEnabled: false);
		NetworkSingleton<GameUI>.Instance.ServerToggleMoneyUI(isEnabled: false);
		NetworkSingleton<GameUI>.Instance.ServerToggleCrosshair(isEnabled: false);
		NetworkSingleton<GameUI>.Instance.ServerToggleItemInputsUI(isEnabled: false);
		ShowGameOverStats();
		yield return null;
		yield return SushiAndFish.Casino.Original.CasinoResources.UnloadUnusedAssets();
	}

	private IEnumerator FollowUsInitializeRoutine()
	{
		Networkstate = GameState.FollowUs;
		NetworkSingleton<GameUI>.Instance.ServerToggleTimer(isEnabled: false);
		NetworkSingleton<GameUI>.Instance.ServerToggleStatusUI(isEnabled: false);
		NetworkSingleton<GameUI>.Instance.ServerToggleMoneyUI(isEnabled: false);
		NetworkSingleton<GameUI>.Instance.ServerToggleCrosshair(isEnabled: false);
		NetworkSingleton<GameUI>.Instance.ServerToggleItemInputsUI(isEnabled: false);
		yield return null;
		yield return SushiAndFish.Casino.Original.CasinoResources.UnloadUnusedAssets();
	}

	private IEnumerator TestInitializeRoutine()
	{
		Networkstate = GameState.Test;
		SteamMatchmaking.SetLobbyJoinable(MonoSingleton<LobbyManager>.Instance.GetCurrentLobbyID(), bLobbyJoinable: true);
		NetworkSingleton<RequestSettingsFromApi>.Instance.ReloadSettings();
		NetworkSingleton<ChallengeManager>.Instance.InitializeChallenges(enabled: true, canComplete: true);
		NetworkSingleton<NavMeshManager>.Instance.ClearNavMesh();
		NetworkSingleton<OrganManager>.Instance.ServerApplyAllOrganSettings();
		RpcLoadAllPlayerCosmetics();
		NetworkSingleton<GameUI>.Instance.ServerToggleTimer(isEnabled: false);
		yield return null;
		yield return SushiAndFish.Casino.Original.CasinoResources.UnloadUnusedAssets();
	}

	[Server]
	public void ServerOnClientScenePlayReady(NetworkConnectionToClient conn, int epoch)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void GameManager::ServerOnClientScenePlayReady(Mirror.NetworkConnectionToClient,System.Int32)' called when server was not active");
		}
		else
		{
			if (epoch != _sceneEpoch || conn == null || !_scenePlayReady.Add(conn.connectionId) || _scenePlayReady.Count < _expectedScenePlayers)
			{
				return;
			}
			NetworkSingleton<GameUI>.Instance.ServerSetLoadingScreen(isEnabled: false, 0.5f);
			if (state == GameState.Lobby)
			{
				RpcToggleInputs(2);
				ServerLockPlayers();
			}
			else
			{
				GameState gameState = state;
				if (gameState != GameState.Cutscene && gameState != GameState.Summary && gameState != GameState.FollowUs)
				{
					RpcToggleInputs(0);
				}
			}
			_isTransitioning = false;
		}
	}

	[Server]
	private void PredictNextCasinoGames()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void GameManager::PredictNextCasinoGames()' called when server was not active");
			return;
		}
		NextCasinoPredicter.PredictFloorGames(1, 5);
		NextCasinoPredicter.PredictFloorGames(2, 5);
		NextCasinoPredicter.PredictFloorGames(3, 5);
		NextCasinoPredicter.PredictFloorGames(4, 5);
	}

	[ClientRpc]
	public void RpcLoadAllPlayerCosmetics()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void GameManager::RpcLoadAllPlayerCosmetics()", 1450340149, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSceneInitialized(int epoch)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(epoch);
		SendRPCInternal("System.Void GameManager::RpcSceneInitialized(System.Int32)", -1278576032, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetInLobbyPresence()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void GameManager::RpcSetInLobbyPresence()", -549038361, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetInGamePresence()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void GameManager::RpcSetInGamePresence()", -1924947969, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcToggleInputs(int inputLayerIndex)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(inputLayerIndex);
		SendRPCInternal("System.Void GameManager::RpcToggleInputs(System.Int32)", 329602083, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcDayStatsFeedback()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void GameManager::RpcDayStatsFeedback()", 1193006993, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcPlayGameOverFeedback()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void GameManager::RpcPlayGameOverFeedback()", 1573754606, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcPlayDayEndFeedback()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void GameManager::RpcPlayDayEndFeedback()", -1818792019, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcPlayStartFeedback()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void GameManager::RpcPlayStartFeedback()", 1628449086, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcPlayFailFeedback()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void GameManager::RpcPlayFailFeedback()", -1505895802, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcClearDaySummary()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void GameManager::RpcClearDaySummary()", 1541698864, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcResetSummaryAndGameOverUI()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void GameManager::RpcResetSummaryAndGameOverUI()", -1553746105, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcInitializeCreditsRoll(PlayerCreditsSnapshot[] snapshots)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		GeneratedNetworkCode._Write_PlayerCreditsSnapshot_005B_005D(writer, snapshots);
		SendRPCInternal("System.Void GameManager::RpcInitializeCreditsRoll(PlayerCreditsSnapshot[])", 574056377, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private IEnumerator InitializeCreditsRollCoroutine(PlayerCreditsSnapshot[] snapshots)
	{
		if (snapshots == null || snapshots.Length == 0)
		{
			UnityEngine.Debug.LogError("[GameManager] Received empty or null snapshots array in RPC!");
			yield break;
		}
		CreditsRollManager creditsManager = null;
		int attempts = 0;
		while (creditsManager == null && attempts < 10)
		{
			creditsManager = UnityEngine.Object.FindFirstObjectByType<CreditsRollManager>();
			if (creditsManager == null)
			{
				yield return null;
				attempts++;
			}
		}
		if (creditsManager != null)
		{
			creditsManager.BeginCredits(snapshots);
		}
		else
		{
			UnityEngine.Debug.LogError("[GameManager] CreditsRollManager not found after scene load!");
		}
	}

	[ClientRpc]
	private void RpcInitializeCreditsTextScroller(PlayerCreditsSnapshot[] snapshots)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		GeneratedNetworkCode._Write_PlayerCreditsSnapshot_005B_005D(writer, snapshots);
		SendRPCInternal("System.Void GameManager::RpcInitializeCreditsTextScroller(PlayerCreditsSnapshot[])", 319010527, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private IEnumerator InitializeCreditsTextScrollerCoroutine(PlayerCreditsSnapshot[] snapshots)
	{
		if (snapshots == null || snapshots.Length == 0)
		{
			UnityEngine.Debug.LogError("[GameManager] Received empty or null snapshots array in RPC!");
			yield break;
		}
		CreditsRollManager creditsManager = null;
		int attempts = 0;
		while (creditsManager == null && attempts < 10)
		{
			creditsManager = UnityEngine.Object.FindFirstObjectByType<CreditsRollManager>();
			if (creditsManager == null)
			{
				yield return null;
				attempts++;
			}
		}
		if (creditsManager != null)
		{
			creditsManager.BeginCredits(snapshots);
		}
		else
		{
			UnityEngine.Debug.LogWarning("[GameManager] CreditsRollManager not found after scene load!");
		}
	}

	public GameManager()
	{
		_Mirror_SyncVarHookDelegate_daysLeft = OnDaysChanged;
		_Mirror_SyncVarHookDelegate_daysPassed = OnDaysPassedChanged;
		_Mirror_SyncVarHookDelegate_currentQuota = OnQuotaChanged;
		_Mirror_SyncVarHookDelegate__timer = OnTimerChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcLockInputs()
	{
		InputEvents.ActiveLayer = InputLayer.Cutscene;
	}

	protected static void InvokeUserCode_RpcLockInputs(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcLockInputs called on server.");
		}
		else
		{
			((GameManager)obj).UserCode_RpcLockInputs();
		}
	}

	protected void UserCode_RpcLoadAllPlayerCosmetics()
	{
		PlayerCustomization[] array = UnityEngine.Object.FindObjectsByType<PlayerCustomization>(FindObjectsSortMode.None);
		foreach (PlayerCustomization playerCustomization in array)
		{
			if (playerCustomization.isLocalPlayer)
			{
				playerCustomization.LoadCosmetics();
				playerCustomization.LoadSavedPlayerColor();
			}
		}
	}

	protected static void InvokeUserCode_RpcLoadAllPlayerCosmetics(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcLoadAllPlayerCosmetics called on server.");
		}
		else
		{
			((GameManager)obj).UserCode_RpcLoadAllPlayerCosmetics();
		}
	}

	protected void UserCode_RpcSceneInitialized__Int32(int epoch)
	{
		if (NetworkClient.isConnected)
		{
			NetworkClient.Send(new ClientScenePlayReadyMessage
			{
				epoch = epoch
			});
		}
	}

	protected static void InvokeUserCode_RpcSceneInitialized__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcSceneInitialized called on server.");
		}
		else
		{
			((GameManager)obj).UserCode_RpcSceneInitialized__Int32(reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcSetInLobbyPresence()
	{
		MonoSingleton<SteamRichPresenceManager>.Instance.SetInHomePresence();
		if (MonoSingleton<DiscordRichPresenceManager>.Instance != null)
		{
			MonoSingleton<DiscordRichPresenceManager>.Instance.SetInHomePresence();
		}
	}

	protected static void InvokeUserCode_RpcSetInLobbyPresence(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcSetInLobbyPresence called on server.");
		}
		else
		{
			((GameManager)obj).UserCode_RpcSetInLobbyPresence();
		}
	}

	protected void UserCode_RpcSetInGamePresence()
	{
		MonoSingleton<SteamRichPresenceManager>.Instance.SetInGamePresence();
		if (MonoSingleton<DiscordRichPresenceManager>.Instance != null)
		{
			MonoSingleton<DiscordRichPresenceManager>.Instance.SetInGamePresence();
		}
	}

	protected static void InvokeUserCode_RpcSetInGamePresence(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcSetInGamePresence called on server.");
		}
		else
		{
			((GameManager)obj).UserCode_RpcSetInGamePresence();
		}
	}

	protected void UserCode_RpcToggleInputs__Int32(int inputLayerIndex)
	{
		switch (inputLayerIndex)
		{
		case 0:
			InputEvents.ActiveLayer = InputLayer.Default;
			break;
		case 1:
			InputEvents.ActiveLayer = InputLayer.Cutscene;
			break;
		case 2:
			InputEvents.ActiveLayer = InputLayer.SpawnBox;
			break;
		}
	}

	protected static void InvokeUserCode_RpcToggleInputs__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcToggleInputs called on server.");
		}
		else
		{
			((GameManager)obj).UserCode_RpcToggleInputs__Int32(reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcDayStatsFeedback()
	{
		daySummaryUI.Show();
	}

	protected static void InvokeUserCode_RpcDayStatsFeedback(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcDayStatsFeedback called on server.");
		}
		else
		{
			((GameManager)obj).UserCode_RpcDayStatsFeedback();
		}
	}

	protected void UserCode_RpcPlayGameOverFeedback()
	{
		gameOverUI.Show();
	}

	protected static void InvokeUserCode_RpcPlayGameOverFeedback(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcPlayGameOverFeedback called on server.");
		}
		else
		{
			((GameManager)obj).UserCode_RpcPlayGameOverFeedback();
		}
	}

	protected void UserCode_RpcPlayDayEndFeedback()
	{
		dayEndFb.PlayFeedbacks();
	}

	protected static void InvokeUserCode_RpcPlayDayEndFeedback(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcPlayDayEndFeedback called on server.");
		}
		else
		{
			((GameManager)obj).UserCode_RpcPlayDayEndFeedback();
		}
	}

	protected void UserCode_RpcPlayStartFeedback()
	{
		startFb.PlayFeedbacks();
	}

	protected static void InvokeUserCode_RpcPlayStartFeedback(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcPlayStartFeedback called on server.");
		}
		else
		{
			((GameManager)obj).UserCode_RpcPlayStartFeedback();
		}
	}

	protected void UserCode_RpcPlayFailFeedback()
	{
		failFb.PlayFeedbacks();
	}

	protected static void InvokeUserCode_RpcPlayFailFeedback(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcPlayFailFeedback called on server.");
		}
		else
		{
			((GameManager)obj).UserCode_RpcPlayFailFeedback();
		}
	}

	protected void UserCode_RpcClearDaySummary()
	{
		if (MonoSingleton<DaySummaryRuntime>.Instance != null)
		{
			MonoSingleton<DaySummaryRuntime>.Instance.Clear();
		}
	}

	protected static void InvokeUserCode_RpcClearDaySummary(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcClearDaySummary called on server.");
		}
		else
		{
			((GameManager)obj).UserCode_RpcClearDaySummary();
		}
	}

	protected void UserCode_RpcResetSummaryAndGameOverUI()
	{
		daySummaryUI.Reset();
		gameOverUI.Reset();
	}

	protected static void InvokeUserCode_RpcResetSummaryAndGameOverUI(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcResetSummaryAndGameOverUI called on server.");
		}
		else
		{
			((GameManager)obj).UserCode_RpcResetSummaryAndGameOverUI();
		}
	}

	protected void UserCode_RpcInitializeCreditsRoll__PlayerCreditsSnapshot_005B_005D(PlayerCreditsSnapshot[] snapshots)
	{
		UnityEngine.Debug.Log($"[GameManager] RpcInitializeCreditsRoll received {((snapshots != null) ? snapshots.Length : 0)} snapshots");
		StartCoroutine(InitializeCreditsRollCoroutine(snapshots));
	}

	protected static void InvokeUserCode_RpcInitializeCreditsRoll__PlayerCreditsSnapshot_005B_005D(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcInitializeCreditsRoll called on server.");
		}
		else
		{
			((GameManager)obj).UserCode_RpcInitializeCreditsRoll__PlayerCreditsSnapshot_005B_005D(GeneratedNetworkCode._Read_PlayerCreditsSnapshot_005B_005D(reader));
		}
	}

	protected void UserCode_RpcInitializeCreditsTextScroller__PlayerCreditsSnapshot_005B_005D(PlayerCreditsSnapshot[] snapshots)
	{
		UnityEngine.Debug.Log($"[GameManager] RpcInitializeCreditsTextScroller received {((snapshots != null) ? snapshots.Length : 0)} snapshots");
		StartCoroutine(InitializeCreditsRollCoroutine(snapshots));
	}

	protected static void InvokeUserCode_RpcInitializeCreditsTextScroller__PlayerCreditsSnapshot_005B_005D(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcInitializeCreditsTextScroller called on server.");
		}
		else
		{
			((GameManager)obj).UserCode_RpcInitializeCreditsTextScroller__PlayerCreditsSnapshot_005B_005D(GeneratedNetworkCode._Read_PlayerCreditsSnapshot_005B_005D(reader));
		}
	}

	static GameManager()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(GameManager), "System.Void GameManager::RpcLockInputs()", InvokeUserCode_RpcLockInputs);
		RemoteProcedureCalls.RegisterRpc(typeof(GameManager), "System.Void GameManager::RpcLoadAllPlayerCosmetics()", InvokeUserCode_RpcLoadAllPlayerCosmetics);
		RemoteProcedureCalls.RegisterRpc(typeof(GameManager), "System.Void GameManager::RpcSceneInitialized(System.Int32)", InvokeUserCode_RpcSceneInitialized__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(GameManager), "System.Void GameManager::RpcSetInLobbyPresence()", InvokeUserCode_RpcSetInLobbyPresence);
		RemoteProcedureCalls.RegisterRpc(typeof(GameManager), "System.Void GameManager::RpcSetInGamePresence()", InvokeUserCode_RpcSetInGamePresence);
		RemoteProcedureCalls.RegisterRpc(typeof(GameManager), "System.Void GameManager::RpcToggleInputs(System.Int32)", InvokeUserCode_RpcToggleInputs__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(GameManager), "System.Void GameManager::RpcDayStatsFeedback()", InvokeUserCode_RpcDayStatsFeedback);
		RemoteProcedureCalls.RegisterRpc(typeof(GameManager), "System.Void GameManager::RpcPlayGameOverFeedback()", InvokeUserCode_RpcPlayGameOverFeedback);
		RemoteProcedureCalls.RegisterRpc(typeof(GameManager), "System.Void GameManager::RpcPlayDayEndFeedback()", InvokeUserCode_RpcPlayDayEndFeedback);
		RemoteProcedureCalls.RegisterRpc(typeof(GameManager), "System.Void GameManager::RpcPlayStartFeedback()", InvokeUserCode_RpcPlayStartFeedback);
		RemoteProcedureCalls.RegisterRpc(typeof(GameManager), "System.Void GameManager::RpcPlayFailFeedback()", InvokeUserCode_RpcPlayFailFeedback);
		RemoteProcedureCalls.RegisterRpc(typeof(GameManager), "System.Void GameManager::RpcClearDaySummary()", InvokeUserCode_RpcClearDaySummary);
		RemoteProcedureCalls.RegisterRpc(typeof(GameManager), "System.Void GameManager::RpcResetSummaryAndGameOverUI()", InvokeUserCode_RpcResetSummaryAndGameOverUI);
		RemoteProcedureCalls.RegisterRpc(typeof(GameManager), "System.Void GameManager::RpcInitializeCreditsRoll(PlayerCreditsSnapshot[])", InvokeUserCode_RpcInitializeCreditsRoll__PlayerCreditsSnapshot_005B_005D);
		RemoteProcedureCalls.RegisterRpc(typeof(GameManager), "System.Void GameManager::RpcInitializeCreditsTextScroller(PlayerCreditsSnapshot[])", InvokeUserCode_RpcInitializeCreditsTextScroller__PlayerCreditsSnapshot_005B_005D);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteVarInt(daysLeft);
			writer.WriteVarInt(daysPassed);
			writer.WriteVarInt(successfulQuota);
			writer.WriteVarLong(currentQuota);
			writer.WriteVarInt(currentFloor);
			writer.WriteVarLong(requiredQuotaToNextFloor);
			writer.WriteVarInt(currentTicketReward);
			writer.WriteBool(isDebtPaid);
			GeneratedNetworkCode._Write_GameState(writer, state);
			writer.WriteFloat(_timer);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteVarInt(daysLeft);
		}
		if ((syncVarDirtyBits & 2L) != 0L)
		{
			writer.WriteVarInt(daysPassed);
		}
		if ((syncVarDirtyBits & 4L) != 0L)
		{
			writer.WriteVarInt(successfulQuota);
		}
		if ((syncVarDirtyBits & 8L) != 0L)
		{
			writer.WriteVarLong(currentQuota);
		}
		if ((syncVarDirtyBits & 0x10L) != 0L)
		{
			writer.WriteVarInt(currentFloor);
		}
		if ((syncVarDirtyBits & 0x20L) != 0L)
		{
			writer.WriteVarLong(requiredQuotaToNextFloor);
		}
		if ((syncVarDirtyBits & 0x40L) != 0L)
		{
			writer.WriteVarInt(currentTicketReward);
		}
		if ((syncVarDirtyBits & 0x80L) != 0L)
		{
			writer.WriteBool(isDebtPaid);
		}
		if ((syncVarDirtyBits & 0x100L) != 0L)
		{
			GeneratedNetworkCode._Write_GameState(writer, state);
		}
		if ((syncVarDirtyBits & 0x200L) != 0L)
		{
			writer.WriteFloat(_timer);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref daysLeft, _Mirror_SyncVarHookDelegate_daysLeft, reader.ReadVarInt());
			GeneratedSyncVarDeserialize(ref daysPassed, _Mirror_SyncVarHookDelegate_daysPassed, reader.ReadVarInt());
			GeneratedSyncVarDeserialize(ref successfulQuota, null, reader.ReadVarInt());
			GeneratedSyncVarDeserialize(ref currentQuota, _Mirror_SyncVarHookDelegate_currentQuota, reader.ReadVarLong());
			GeneratedSyncVarDeserialize(ref currentFloor, null, reader.ReadVarInt());
			GeneratedSyncVarDeserialize(ref requiredQuotaToNextFloor, null, reader.ReadVarLong());
			GeneratedSyncVarDeserialize(ref currentTicketReward, null, reader.ReadVarInt());
			GeneratedSyncVarDeserialize(ref isDebtPaid, null, reader.ReadBool());
			GeneratedSyncVarDeserialize(ref state, null, GeneratedNetworkCode._Read_GameState(reader));
			GeneratedSyncVarDeserialize(ref _timer, _Mirror_SyncVarHookDelegate__timer, reader.ReadFloat());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref daysLeft, _Mirror_SyncVarHookDelegate_daysLeft, reader.ReadVarInt());
		}
		if ((num & 2L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref daysPassed, _Mirror_SyncVarHookDelegate_daysPassed, reader.ReadVarInt());
		}
		if ((num & 4L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref successfulQuota, null, reader.ReadVarInt());
		}
		if ((num & 8L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref currentQuota, _Mirror_SyncVarHookDelegate_currentQuota, reader.ReadVarLong());
		}
		if ((num & 0x10L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref currentFloor, null, reader.ReadVarInt());
		}
		if ((num & 0x20L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref requiredQuotaToNextFloor, null, reader.ReadVarLong());
		}
		if ((num & 0x40L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref currentTicketReward, null, reader.ReadVarInt());
		}
		if ((num & 0x80L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref isDebtPaid, null, reader.ReadBool());
		}
		if ((num & 0x100L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref state, null, GeneratedNetworkCode._Read_GameState(reader));
		}
		if ((num & 0x200L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _timer, _Mirror_SyncVarHookDelegate__timer, reader.ReadFloat());
		}
	}
}

}
