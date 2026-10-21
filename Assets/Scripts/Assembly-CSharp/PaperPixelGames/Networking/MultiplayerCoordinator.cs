using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Netcode.Transports.Facepunch;
using PaperPixelGames.Core;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.SceneManagement;
using PaperPixelGames.Framework.States;
using PaperPixelGames.Framework.States.Game;
using PaperPixelGames.Framework.States.MainMenu;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking.Chat;
using PaperPixelGames.Steam;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PaperPixelGames.Networking
{
	[DisallowMultipleComponent]
	public sealed class MultiplayerCoordinator : Singleton<MultiplayerCoordinator>
	{
		[Header("References")]
		[SerializeField]
		private NetworkObject _playerPrefab;

		[SerializeField]
		private SteamLobbyService _steam;

		[SerializeField]
		private NgoNetworkService _ngo;

		[Header("Config")]
		[SerializeField]
		private int _maxMembers = 5;

		[SerializeField]
		private bool _friendsOnly;

		[SerializeField]
		[Min(1f)]
		private float _hostReadyTimeoutSeconds = 60f;

		[SerializeField]
		[Min(1f)]
		private float _clientConnectTimeoutSeconds = 120f;

		[SerializeField]
		[Min(1f)]
		private float _launchReadyTimeoutSeconds = 20f;

		[SerializeField]
		[Min(1f)]
		private float _hostStartTimeoutSeconds = 30f;

		[SerializeField]
		[Min(1f)]
		private float _reconnectGraceSeconds = 45f;

		[SerializeField]
		[Min(1f)]
		private float _heartbeatIntervalSeconds = 10f;

		[SerializeField]
		[Min(1f)]
		private float _heartbeatMaxAgeSeconds = 30f;

		[Header("Optional Data")]
		[SerializeField]
		private SteamBanListData _steamBanListData;

		private bool _isHostStarted;

		private bool _isLateJoiner;

		private NetworkConnectionState _netState;

		private bool _hostStartInProgress;

		private Coroutine _hostReadyTimeoutCor;

		private HostPreparationWait _hostPreparationWait;

		private Coroutine _clientConnectTimeoutCor;

		private bool _initialized;

		private bool _clientWaitingForHost;

		private bool _clientJoinInProgress;

		private bool _lobbyCreateInProgress;

		private bool _lobbyJoinInProgress;

		private bool _hasPendingLobbyJoin;

		private Lobby _pendingLobbyJoin;

		private ulong _joiningLobbyId;

		private SteamId _joiningOwnerId;

		private int _targetPlayerCount = 1;

		private Lobby _currentLobby;

		private readonly SteamLobbyOperationController _lobbyOperations = new SteamLobbyOperationController();

		private Coroutine _steamHealthMonitor;

		private Coroutine _launchPreparationCoroutine;

		private Coroutine _hostStartTimeoutCoroutine;

		private Coroutine _heartbeatCoroutine;

		private Coroutine _reconnectCoroutine;

		private ClientReconnectRecovery _reconnectRecovery;

		private bool _sessionReturnPrepared;

		private long _clientCancellationVersion;

		private Coroutine _cohortConnectionDeadlineCoroutine;

		private Coroutine _metadataReadinessCoroutine;

		private HostPreparationWait _metadataPreparationWait;

		private ulong _metadataReadinessLobbyId;

		private ulong _metadataReadinessFailedLobbyId;

		private bool _lastSteamReady;

		private ulong _sessionEpoch;

		private ulong _networkHostSteamId;

		private ulong _lastHandledPrepareEpoch;

		private ulong _incompatibleSessionEpoch;

		private ulong _incompatibleGameVersionEpoch;

		private float _nextLobbyVersionRefreshTime;

		private string _lobbyVersionAlertMessage = string.Empty;

		private NotifyManager _lobbyVersionAlertManager;

		private bool _intentionalNetworkShutdown;

		private bool _launchCohortFrozen;

		private MultiplayerFailure _pendingVisibleFailure;

		private bool _hasPendingVisibleFailure;

		private bool _pendingVisibleFailureToastDelivered;

		private bool _pendingVisibleFailureChatDelivered;

		private string _pendingVisibleFailureSignature = string.Empty;

		private string _lastVisibleFailureSignature = string.Empty;

		private float _lastVisibleFailureRealtime = float.NegativeInfinity;

		private readonly LaunchPreparationWait _launchPreparation = new LaunchPreparationWait();

		private readonly HashSet<ulong> _launchCohortSteamIds = new HashSet<ulong>();

		private readonly AdmissionReservations _reservedSteamIds = new AdmissionReservations();

		private readonly HashSet<ulong> _activeSteamIds = new HashSet<ulong>();

		private readonly Dictionary<ulong, ulong> _steamIdByClientId = new Dictionary<ulong, ulong>();

		private readonly ServerSceneReadyDeadlines _serverSceneReadyDeadlines = new ServerSceneReadyDeadlines();

		private double _initialSceneWaitDeadline;

		private float _phaseStartedRealtime;

		private readonly Dictionary<ulong, int> _spawnSlotByClientId = new Dictionary<ulong, int>();

		private readonly HashSet<int> _occupiedSpawnSlots = new HashSet<int>();

		private bool _cachedIsInForeignLobby;

		private const string SteamConnectionRequiredMessage = "Steam connection is required to play. Please start the game through Steam.";

		private TaskCompletionSource<LobbyJoinOutcome> _roomJoinCompletion;

		public bool IsHostStarted
		{
			get
			{
				return _isHostStarted;
			}
			set
			{
				_isHostStarted = value;
			}
		}

		public bool IsLateJoiner
		{
			get
			{
				return _isLateJoiner;
			}
			set
			{
				_isLateJoiner = value;
			}
		}

		public long ClientCancellationVersion => _clientCancellationVersion;

		public bool IsClientSessionWorkBlocked
		{
			get
			{
				if (!_intentionalNetworkShutdown)
				{
					if (_reconnectRecovery != null)
					{
						if (!_reconnectRecovery.Cancelled && !_reconnectRecovery.StoppingNetwork && !_reconnectRecovery.AttemptInterrupted)
						{
							return !_reconnectRecovery.AttemptInProgress;
						}
						return true;
					}
					return false;
				}
				return true;
			}
		}

		public bool IsSessionShutdownComplete
		{
			get
			{
				if (!(_ngo == null))
				{
					if (_ngo.IsStopped)
					{
						return _ngo.SceneOperationsSettled;
					}
					return false;
				}
				return true;
			}
		}

		public bool CanCancelClientJoin
		{
			get
			{
				if (!_intentionalNetworkShutdown && !_sessionReturnPrepared && (!Singleton<GameSessionLifecycle>.TryGetInstance(out var instance) || !instance.ReturnToMenuInProgress) && !IsHostStarted && (!(NetworkManager.Singleton != null) || !NetworkManager.Singleton.IsServer) && SessionPhase != MultiplayerSessionPhase.Running)
				{
					if ((!_hasPendingLobbyJoin || !_lobbyJoinInProgress) && !_clientWaitingForHost && !_clientJoinInProgress && _reconnectRecovery == null && (!IsLoadingStateActive || !IsLateJoiner) && SessionPhase != MultiplayerSessionPhase.SceneSync)
					{
						return SessionPhase == MultiplayerSessionPhase.InitialBootstrap;
					}
					return true;
				}
				return false;
			}
		}

		private IReadOnlyCollection<ulong> _launchReadySteamIds => _launchPreparation.ReadySteamIds;

		private double HostPreparationTimeoutSeconds => Math.Max(600.0, _hostReadyTimeoutSeconds);

		private double LaunchPreparationTimeoutSeconds => Math.Max(120.0, _launchReadyTimeoutSeconds);

		private static bool IsMainMenuStateActive
		{
			get
			{
				if (Singleton<StateManager>.Instance != null)
				{
					return Singleton<StateManager>.Instance.CurrentState is MainMenuState;
				}
				return false;
			}
		}

		private static bool IsLoadingStateActive
		{
			get
			{
				if (Singleton<StateManager>.Instance != null)
				{
					return Singleton<StateManager>.Instance.CurrentState is LoadingState;
				}
				return false;
			}
		}

		public bool IsSingleplayerSession { get; private set; }

		public SteamLobbyService LobbyService => _steam;

		public LobbyRoomService RoomService { get; private set; }

		public int TargetPlayerCount => _targetPlayerCount;

		public MultiplayerSessionPhase SessionPhase { get; private set; }

		public ulong SessionEpoch => _sessionEpoch;

		public long ActiveOperationId => _lobbyOperations.ActiveOperationId;

		public bool IsSteamConnectionAvailable
		{
			get
			{
				if (_steam != null)
				{
					return _steam.IsSteamAvailable;
				}
				return false;
			}
		}

		public bool IsSteamConnectionBlocked => !Netcode.Transports.Local.OfflineSingleplayerMode.Enabled && !IsSteamConnectionAvailable;

		public string SteamConnectionBlockMessage => "Steam connection is required to play. Please start the game through Steam.";

		public bool HasPendingInitialSceneSynchronization
		{
			get
			{
				if (Time.realtimeSinceStartupAsDouble < _initialSceneWaitDeadline)
				{
					return _serverSceneReadyDeadlines.HasPendingCohort(_launchCohortSteamIds, Time.realtimeSinceStartupAsDouble);
				}
				return false;
			}
		}

		public bool LobbyBlocksSingleplayer
		{
			get
			{
				LobbyRoomService roomService = RoomService;
				if ((object)roomService == null || !roomService.Snapshot.BlocksSingleplayer)
				{
					if (TryGetActiveLobby(out var lobby))
					{
						if (lobby.MemberCount <= 1)
						{
							return IsInForeignLobby;
						}
						return true;
					}
					return false;
				}
				return true;
			}
		}

		public bool IsInForeignLobby
		{
			get
			{
				if (!SteamClient.IsValid)
				{
					return false;
				}
				if (!TryGetActiveLobby(out var lobby))
				{
					return false;
				}
				return (ulong)lobby.Owner.Id != (ulong)SteamClient.SteamId;
			}
		}

		public bool IsCurrentLobbyOwner
		{
			get
			{
				if (!SteamClient.IsValid)
				{
					return false;
				}
				if (!TryGetActiveLobby(out var lobby))
				{
					return false;
				}
				return (ulong)lobby.Owner.Id == (ulong)SteamClient.SteamId;
			}
		}

		public bool CanStartGameFromSaveSlot
		{
			get
			{
				if (IsSingleplayerSession && LobbyBlocksSingleplayer)
				{
					return false;
				}
				if ((!IsSingleplayerSession && _lobbyJoinInProgress) || _hasPendingLobbyJoin || _sessionReturnPrepared || _intentionalNetworkShutdown)
				{
					return false;
				}
				if (!IsSteamConnectionAvailable && !Netcode.Transports.Local.OfflineSingleplayerMode.Enabled)
				{
					return false;
				}
				if (IsInForeignLobby)
				{
					return false;
				}
				if (_clientJoinInProgress)
				{
					return false;
				}
				if (_hostStartInProgress)
				{
					return false;
				}
				NetworkConnectionState netState = _netState;
				if (netState == NetworkConnectionState.Connecting || netState == NetworkConnectionState.Connected)
				{
					return false;
				}
				return true;
			}
		}

		private bool HasClientConnectionOwner
		{
			get
			{
				if (_reconnectRecovery == null && !_clientJoinInProgress)
				{
					return ClientReconnectRecovery.CanRecoverPhase(SessionPhase);
				}
				return true;
			}
		}

		public event Action<bool> ForeignLobbyStateChanged;

		public event Action<bool> SteamConnectionAvailabilityChanged;

		public event Action<int> TargetPlayerCountChanged;

		public bool CanContinueMenuDeparture(long version)
		{
			if (version == _clientCancellationVersion && !_sessionReturnPrepared && !_intentionalNetworkShutdown && IsMainMenuStateActive && (IsHostStarted || IsLateJoiner))
			{
				if (Singleton<GameSessionLifecycle>.TryGetInstance(out var instance))
				{
					return !instance.ReturnToMenuInProgress;
				}
				return true;
			}
			return false;
		}

		private void Update()
		{
			TryPresentPendingFailure();
			CheckServerSceneReadyDeadlines();
			if (Time.realtimeSinceStartup >= _nextLobbyVersionRefreshTime)
			{
				_nextLobbyVersionRefreshTime = Time.realtimeSinceStartup + 0.5f;
				RefreshLobbyVersionAlert();
			}
		}

		public void Initialize()
		{
			if (Netcode.Transports.Local.OfflineSingleplayerMode.Enabled) IsSingleplayerSession = true;
			if (_initialized)
			{
				return;
			}
			_phaseStartedRealtime = Time.realtimeSinceStartup;
			if (!_steam)
			{
				_steam = UnityEngine.Object.FindFirstObjectByType<SteamLobbyService>();
			}
			if (!_ngo)
			{
				_ngo = UnityEngine.Object.FindFirstObjectByType<NgoNetworkService>();
			}
			if (_steam == null || _ngo == null)
			{
				UtilityMethods.Log("<b><color=#FF4040>[Multiplayer]</color></b> Initialization blocked. Required services are missing." + $"\n   HasSteamService: {_steam != null}" + $"\n   HasNgoService: {_ngo != null}", LogType.Error);
				ShowFailure(MultiplayerFailure.Create(MultiplayerFailureCode.Unknown, MultiplayerFailureStage.Session, "Required Steam or NGO multiplayer services are missing."));
				return;
			}
			if (!Netcode.Transports.Local.OfflineSingleplayerMode.Enabled) _steam.Initialize();
			RoomService = _steam.GetComponent<LobbyRoomService>();
			if (RoomService == null)
			{
				RoomService = _steam.gameObject.AddComponent<LobbyRoomService>();
			}
			_steam.RoomService = RoomService;
			RoomService.Initialize(_steam, this);
			_ngo.Initialize();
			_ngo.ConfigureJoinTimeouts(_clientConnectTimeoutSeconds);
			_steam.LobbyCreated += OnLobbyCreated;
			_steam.LobbyEntered += OnLobbyEntered;
			_steam.LobbyMemberJoined += OnLobbyMemberJoined;
			_steam.LobbyMemberLeft += OnLobbyMemberLeft;
			_steam.LobbyInvite += OnLobbyInvite;
			_steam.GameLobbyJoinRequested += OnGameLobbyJoinRequested;
			_steam.LobbyDataChanged += OnLobbyDataChanged;
			_steam.LobbyMemberDataChanged += OnLobbyMemberDataChanged;
			_steam.LobbyInternalMessageReceived += OnLobbyInternalMessageReceived;
			_ngo.ServerStarted += OnServerStarted;
			_ngo.ClientConnected += OnClientConnected;
			_ngo.ClientJoinStageChanged += OnClientJoinStageChanged;
			_ngo.ClientDisconnected += OnClientDisconnected;
			_ngo.ApprovalRequested += OnConnectionApproval;
			_ngo.TransportFailure += OnTransportFailure;
			_ngo.ServerStopped += OnServerStopped;
			_ngo.ClientStopped += OnClientStopped;
			_initialized = true;
			string[] commandLineArgs = Environment.GetCommandLineArgs();
			for (int i = 0; i + 1 < commandLineArgs.Length; i++)
			{
				if (!(commandLineArgs[i] != "+connect_lobby") && ulong.TryParse(commandLineArgs[i + 1], out var result) && result != 0L)
				{
					_pendingLobbyJoin = new Lobby(result);
					_hasPendingLobbyJoin = true;
					break;
				}
			}
			if (!Netcode.Transports.Local.OfflineSingleplayerMode.Enabled) _steamHealthMonitor = StartCoroutine(CoMonitorSteamHealth());
		}

		private void OnDestroy()
		{
			ClearLobbyVersionAlert();
			if ((bool)_steam)
			{
				_steam.LobbyCreated -= OnLobbyCreated;
				_steam.LobbyEntered -= OnLobbyEntered;
				_steam.LobbyMemberJoined -= OnLobbyMemberJoined;
				_steam.LobbyMemberLeft -= OnLobbyMemberLeft;
				_steam.LobbyInvite -= OnLobbyInvite;
				_steam.GameLobbyJoinRequested -= OnGameLobbyJoinRequested;
				_steam.LobbyDataChanged -= OnLobbyDataChanged;
				_steam.LobbyMemberDataChanged -= OnLobbyMemberDataChanged;
				_steam.LobbyInternalMessageReceived -= OnLobbyInternalMessageReceived;
			}
			if ((bool)_ngo)
			{
				_ngo.ServerStarted -= OnServerStarted;
				_ngo.ClientConnected -= OnClientConnected;
				_ngo.ClientJoinStageChanged -= OnClientJoinStageChanged;
				_ngo.ClientDisconnected -= OnClientDisconnected;
				_ngo.ApprovalRequested -= OnConnectionApproval;
				_ngo.TransportFailure -= OnTransportFailure;
				_ngo.ServerStopped -= OnServerStopped;
				_ngo.ClientStopped -= OnClientStopped;
			}
			_lobbyOperations.Dispose();
		}

		private IEnumerator CoMonitorSteamHealth()
		{
			WaitForSecondsRealtime wait = new WaitForSecondsRealtime(2f);
			while (this != null)
			{
				if (MultiplayerFaultInjection.ConsumeForcedTransportDisconnect() && NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
				{
					UtilityMethods.Log("[FaultInjection] Forcing NGO shutdown.", LogType.Warning);
					NetworkManager.Singleton.Shutdown();
				}
				if (_steam != null && !_steam.IsInitializedSuccessfully && SteamClient.IsValid)
				{
					_steam.Initialize();
				}
				bool isSteamConnectionAvailable = IsSteamConnectionAvailable;
				if (isSteamConnectionAvailable != _lastSteamReady)
				{
					bool lastSteamReady = _lastSteamReady;
					_lastSteamReady = isSteamConnectionAvailable;
					SteamConnectionAvailabilityChanged?.Invoke(isSteamConnectionAvailable);
					UtilityMethods.Log("<b><color=#" + (isSteamConnectionAvailable ? "8FD14F" : "FFB020") + ">[Steam Health]</color></b> State changed." + $"\n   Ready: {isSteamConnectionAvailable}" + $"\n   SteamValid: {SteamClient.IsValid}" + $"\n   LoggedOn: {SteamClient.IsValid && SteamClient.IsLoggedOn}" + $"\n   ServiceInitialized: {_steam != null && _steam.IsInitializedSuccessfully}", isSteamConnectionAvailable ? LogType.Log : LogType.Warning);
					if (lastSteamReady && !isSteamConnectionAvailable)
					{
						ShowFailure(MultiplayerFailure.Create(MultiplayerFailureCode.SteamUnavailable, MultiplayerFailureStage.SteamHealth, "Steam became unavailable while multiplayer was active."));
					}
					if (isSteamConnectionAvailable && IsMainMenuStateActive)
					{
						if (_hasPendingLobbyJoin)
						{
							JoinPendingLobbyIfReady();
						}
						else if ((ulong)_steam.CurrentLobby.Id == 0L)
						{
							CreateLobby();
						}
					}
				}
				TryPresentPendingFailure();
				yield return wait;
			}
		}

		public void StartGame()
		{
			if (_sessionReturnPrepared || _intentionalNetworkShutdown)
			{
				return;
			}
			if (!IsSteamConnectionAvailable && !Netcode.Transports.Local.OfflineSingleplayerMode.Enabled)
			{
				UtilityMethods.Log("<b><color=#FFB020>[Multiplayer]</color></b> StartGame blocked. Steam connection is unavailable.", LogType.Warning);
				ShowFailure(MultiplayerFailure.Create(MultiplayerFailureCode.SteamUnavailable, MultiplayerFailureStage.SteamHealth, "StartGame was requested while Steam was unavailable."));
			}
			else if ((_netState == NetworkConnectionState.Disconnected || _netState == NetworkConnectionState.Connecting) && !_hostStartInProgress)
			{
				_hostStartInProgress = true;
				_netState = NetworkConnectionState.Connecting;
				SetSessionPhase(MultiplayerSessionPhase.HostStarting);
				if (!_ngo.StartHost(BuildConnectionPayload()))
				{
					ReturnToMainMenuAfterFailure(MultiplayerFailure.Create(MultiplayerFailureCode.HostStartFailed, MultiplayerFailureStage.HostStart, "NetworkManager.StartHost returned false."), isStartupFailure: true);
					return;
				}
				StopHostStartTimeout();
				_hostStartTimeoutCoroutine = StartCoroutine(CoHostStartTimeout());
				UtilityMethods.Log("<b><color=#8FD14F>[Multiplayer]</color></b> Host start requested.");
			}
		}

		private IEnumerator CoHostStartTimeout()
		{
			yield return new WaitForSecondsRealtime(_hostStartTimeoutSeconds);
			_hostStartTimeoutCoroutine = null;
			if (_hostStartInProgress)
			{
				ReturnToMainMenuAfterFailure(MultiplayerFailure.Create(MultiplayerFailureCode.HostStartFailed, MultiplayerFailureStage.HostStart, $"Host did not start within {_hostStartTimeoutSeconds:0} seconds."), isStartupFailure: true);
			}
		}

		private void StopHostStartTimeout()
		{
			if (_hostStartTimeoutCoroutine != null)
			{
				StopCoroutine(_hostStartTimeoutCoroutine);
				_hostStartTimeoutCoroutine = null;
			}
		}

		public async void CreateLobby()
		{
			if (IsSingleplayerSession || _sessionReturnPrepared || (Singleton<GameSessionLifecycle>.TryGetInstance(out var instance) && instance.ReturnToMenuInProgress))
			{
				return;
			}
			if (!_initialized)
			{
				Initialize();
			}
			if (!IsSteamConnectionAvailable)
			{
				UtilityMethods.Log("<b><color=#FFB020>[Multiplayer]</color></b> CreateLobby blocked. Steam connection is unavailable.", LogType.Warning);
				ShowFailure(MultiplayerFailure.Create(MultiplayerFailureCode.SteamUnavailable, MultiplayerFailureStage.LobbyCreate, "Lobby creation was requested while Steam was unavailable."));
			}
			else
			{
				if (_lobbyCreateInProgress || _lobbyJoinInProgress || _hasPendingLobbyJoin || (ulong)_steam.CurrentLobby.Id != 0L)
				{
					return;
				}
				_lobbyCreateInProgress = true;
				LobbyOperationLease operation = _lobbyOperations.Begin(LobbyOperationKind.Create, 0uL);
				try
				{
					_currentLobby = default;
					_cachedIsInForeignLobby = false;
					ForeignLobbyStateChanged?.Invoke(obj: false);
					_isHostStarted = false;
					_isLateJoiner = false;
					SetSessionPhase(MultiplayerSessionPhase.MenuWaiting);
					int attemptCount = 0;
					float[] retryDelays = new float[3] { 5f, 15f, 30f };
					while (_lobbyOperations.IsCurrent(operation) && IsSteamConnectionAvailable && !_hasPendingLobbyJoin)
					{
						attemptCount++;
						LobbyCreationOutcome lobbyCreationOutcome = await _steam.CreateLobbyAsync(_maxMembers, _friendsOnly, operation.CancellationToken, 15);
						if (!_lobbyOperations.IsCurrent(operation))
						{
							break;
						}
						if (lobbyCreationOutcome.Succeeded)
						{
							_lobbyOperations.Complete(operation);
							break;
						}
						if (!IsTransientLobbyCreationFailure(lobbyCreationOutcome.SteamResult))
						{
							ReportLobbyCreationFailure(lobbyCreationOutcome.SteamResult, attemptCount);
							ShowFailure(MultiplayerFailure.Create(MultiplayerFailureCode.LobbyCreateFailed, MultiplayerFailureStage.LobbyCreate, $"Steam result={lobbyCreationOutcome.SteamResult}, attempts={attemptCount}."));
							_lobbyOperations.Complete(operation);
							break;
						}
						float num = retryDelays[Math.Min(attemptCount - 1, retryDelays.Length - 1)];
						UtilityMethods.Log("<b><color=#FFB020>[Multiplayer]</color></b> Lobby creation will be retried." + $"\n   Result: {lobbyCreationOutcome.SteamResult}" + $"\n   Attempt: {attemptCount}" + $"\n   OperationId: {operation.OperationId}" + $"\n   RetryDelaySeconds: {num:0.##}", LogType.Warning);
						await Task.Delay(TimeSpan.FromSeconds(num), operation.CancellationToken);
						if (this == null || !_lobbyOperations.IsCurrent(operation) || _hasPendingLobbyJoin || _lobbyJoinInProgress || (ulong)_steam.CurrentLobby.Id != 0L)
						{
							break;
						}
					}
				}
				catch (OperationCanceledException)
				{
				}
				catch (Exception ex2)
				{
					UtilityMethods.Log($"<b><color=#FF4040>[Multiplayer]</color></b> Lobby creation exception. Exception={ex2}", LogType.Error);
					if (_lobbyOperations.IsCurrent(operation))
					{
						ShowFailure(MultiplayerFailure.Create(MultiplayerFailureCode.LobbyCreateFailed, MultiplayerFailureStage.LobbyCreate, ex2.Message));
						_lobbyOperations.Complete(operation);
					}
				}
				finally
				{
					_lobbyCreateInProgress = false;
				}
			}
		}

		private static bool IsTransientLobbyCreationFailure(Result result)
		{
			if (result != Result.Timeout && result != Result.NoConnection)
			{
				return result == Result.Fail;
			}
			return true;
		}

		private void ReportLobbyCreationFailure(Result result, int attemptCount)
		{
			bool flag = result == Result.AccessDenied;
			LogType logType = ((!flag) ? LogType.Warning : LogType.Error);
			UtilityMethods.Log("<b><color=#" + (flag ? "FF4040" : "FFB020") + ">[Multiplayer]</color></b> Lobby creation failed." + $"\n   Result: {result}" + $"\n   Attempts: {attemptCount}" + $"\n   AppId: {(SteamClient.IsValid ? SteamClient.AppId : ((AppId)0))}" + $"\n   SteamClientValid: {SteamClient.IsValid}" + $"\n   SteamLoggedOn: {SteamClient.IsValid && SteamClient.IsLoggedOn}", logType);
		}

		public Task<bool> RefreshLobbiesAsync(int maxResults = 20)
		{
			if (!IsSteamConnectionAvailable)
			{
				return Task.FromResult(result: false);
			}
			return _steam.RefreshLobbiesAsync(maxResults);
		}

		public void Disconnect()
		{
			RoomService?.CancelPendingJoin();
			RequestSessionReturn(ReturnToMainMenuRequest.UserRequested());
		}

		public Task<LobbyJoinOutcome> RequestRoomJoinAsync(Lobby lobby)
		{
			_roomJoinCompletion?.TrySetCanceled();
			_roomJoinCompletion = new TaskCompletionSource<LobbyJoinOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
			Task<LobbyJoinOutcome> task = _roomJoinCompletion.Task;
			if ((ulong)lobby.Id == 0L || ((ulong)_steam.CurrentLobby.Id == (ulong)lobby.Id && RoomService != null && RoomService.IsLocalAdmitted))
			{
				_roomJoinCompletion.TrySetResult(new LobbyJoinOutcome(((ulong)lobby.Id == 0L) ? ((Lobby?)null) : new Lobby?(lobby), ((ulong)lobby.Id != 0L) ? RoomEnter.Success : RoomEnter.Error));
				return task;
			}
			QueueLobbyJoin(lobby);
			return task;
		}

		internal void CancelRoomJoinRequest()
		{
			if (_lobbyJoinInProgress || _hasPendingLobbyJoin)
			{
				_lobbyOperations.CancelActive();
			}
			_hasPendingLobbyJoin = false;
			_roomJoinCompletion?.TrySetCanceled();
		}

		public LobbyRoomResult KickRoomClient(ulong clientId)
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if (singleton == null || !singleton.IsServer || !IsCurrentLobbyOwner || RoomService == null)
			{
				return LobbyRoomResult.Fail("Only the host can remove players.");
			}
			if (clientId == 0L || !singleton.ConnectedClients.ContainsKey(clientId))
			{
				return LobbyRoomResult.Fail("Choose a connected remote player.");
			}
			if (_ngo == null || !_ngo.TryGetAuthenticatedSteamId(clientId, out var steamId) || steamId == 0L || steamId == SteamClient.SteamId.Value)
			{
				return LobbyRoomResult.Fail("Steam identity is unavailable.");
			}
			return RoomService.KickConnectedMember(steamId);
		}

		public void RemoveRoomMember(ulong authenticatedSteamId, bool banned)
		{
			if (!IsCurrentLobbyOwner || authenticatedSteamId == 0L || authenticatedSteamId == SteamClient.SteamId.Value)
			{
				return;
			}
			_launchPreparation.RemoveMember(authenticatedSteamId);
			_launchCohortSteamIds.Remove(authenticatedSteamId);
			NetworkManager singleton = NetworkManager.Singleton;
			if (singleton == null || !singleton.IsServer)
			{
				return;
			}
			ulong[] array = singleton.ConnectedClientsIds.ToArray();
			foreach (ulong num in array)
			{
				if (_ngo.TryGetAuthenticatedSteamId(num, out var steamId) && steamId == authenticatedSteamId)
				{
					FacepunchTransport.DisconnectClientWithReason(singleton, num, $"PPG:{(banned ? MultiplayerFailureCode.Banned : MultiplayerFailureCode.NotAuthorized)}");
				}
			}
		}

		public bool CancelClientJoin(long expectedVersion)
		{
			if (expectedVersion != _clientCancellationVersion || !CanCancelClientJoin)
			{
				return false;
			}
			MultiplayerDiagnostics.Record("client_join_cancel_requested", $"phase={SessionPhase} version={expectedVersion}", ulong.MaxValue);
			ReturnToMainMenuRequest request = ReturnToMainMenuRequest.UserRequested();
			request.DebugReason = "User cancelled the client join or recovery from the loading screen.";
			RequestSessionReturn(request);
			return true;
		}

		private void RequestSessionReturn(ReturnToMainMenuRequest request)
		{
			if (Singleton<GameSessionLifecycle>.TryGetInstance(out var instance))
			{
				instance.ReturnToMainMenu(request);
				return;
			}
			BeginSessionReturn(request);
			ResetSessionForMainMenu(request);
			if (!IsMainMenuStateActive)
			{
				Singleton<StateManager>.Instance?.SwitchState<MainMenuState>();
			}
			else
			{
				EnsureMenuLobby();
			}
		}

		public void SetFriendsOnly(bool friendsOnly)
		{
			_friendsOnly = friendsOnly;
		}

		public void SetMaxMembers(int maxMembers)
		{
			_maxMembers = Mathf.Max(1, maxMembers);
		}

		private void OnLobbyCreated(Lobby lobby)
		{
			if (!IsSingleplayerSession)
			{
				UtilityMethods.Log($"[Coordinator] OnLobbyCreated -> StartHost lobby={lobby.Id} owner={lobby.Owner.Id}");
				SetSessionPhase(MultiplayerSessionPhase.MenuWaiting);
				PublishLobbySessionState("waiting");
				RefreshMainMenuLobbyUi(lobby);
				RefreshForeignLobbyState();
			}
		}

		private void OnLobbyEntered(Lobby lobby)
		{
			if (!(_steam == null) && (ulong)lobby.Id == (ulong)_steam.CurrentLobby.Id && !_sessionReturnPrepared && !IsSingleplayerSession)
			{
				_metadataReadinessFailedLobbyId = 0uL;
				MultiplayerDiagnostics.SetContext(lobby.Id, _sessionEpoch, SteamClient.IsValid ? SteamClient.SteamId.Value : 0, _networkHostSteamId, ActiveOperationId);
				MultiplayerDiagnostics.Record("lobby_entered", "", ulong.MaxValue);
				_currentLobby = lobby;
				HandleLobbyState(lobby);
				_ = (ulong)lobby.Owner.Id;
				_ = (ulong)SteamClient.SteamId;
				RefreshMainMenuLobbyUi(lobby);
				RefreshForeignLobbyState();
			}
		}

		private void OnLobbyDataChanged(Lobby lobby)
		{
			if (!(_steam == null) && (ulong)lobby.Id == (ulong)_steam.CurrentLobby.Id)
			{
				_currentLobby = lobby;
				HandleLobbyState(lobby);
				if (lobby.GetData("session_state") != "waiting")
				{
					UtilityMethods.Log("[Coordinator] Member data changes bypass state is not waiting!");
					return;
				}
				RefreshMainMenuLobbyUi(lobby);
				RefreshForeignLobbyState();
			}
		}

		private void OnLobbyMemberDataChanged(Lobby lobby, Friend member)
		{
			RefreshLobbyVersionAlert();
		}

		private void HandleLobbyState(Lobby lobby)
		{
			if (_steam == null || (ulong)lobby.Id == 0L || (ulong)lobby.Id != (ulong)_steam.CurrentLobby.Id || _sessionReturnPrepared || SessionPhase == MultiplayerSessionPhase.ReturningToMenu || (RoomService != null && !IsCurrentLobbyOwner && !RoomService.IsLocalAdmitted))
			{
				return;
			}
			bool flag = LobbySessionMetadata.TryRead(lobby, out var descriptor);
			string text = (flag ? descriptor.State : lobby.GetData("session_state"));
			if (flag && text == "closing" && (_sessionEpoch == 0L || descriptor.Epoch == _sessionEpoch) && (_networkHostSteamId == 0L || descriptor.NetworkHostSteamId == _networkHostSteamId) && descriptor.NetworkHostSteamId != SteamClient.SteamId.Value)
			{
				HandleHostSessionClosed("metadata");
			}
			else if (flag && text != "waiting" && descriptor.NetworkHostSteamId != 0L && lobby.Owner.Id.Value != descriptor.NetworkHostSteamId)
			{
				ReturnToMainMenuAfterFailure(MultiplayerFailure.Create(MultiplayerFailureCode.HostUnavailable, MultiplayerFailureStage.Session, "Steam lobby ownership no longer matches the immutable network host; host migration is disabled."), isStartupFailure: false, LogType.Warning);
			}
			else
			{
				if (_reconnectRecovery != null)
				{
					return;
				}
				if (flag && text != "waiting" && !string.Equals(descriptor.ReleaseVersion, ReleaseVersion.Value, StringComparison.Ordinal))
				{
					if (_incompatibleSessionEpoch != descriptor.Epoch)
					{
						_incompatibleSessionEpoch = descriptor.Epoch;
						RefreshLobbyVersionAlert();
					}
				}
				else if ((ulong)lobby.Owner.Id == (ulong)SteamClient.SteamId)
				{
					if (flag && descriptor.NetworkHostSteamId != 0L && descriptor.NetworkHostSteamId != SteamClient.SteamId.Value && text != "waiting")
					{
						ReturnToMainMenuAfterFailure(MultiplayerFailure.Create(MultiplayerFailureCode.HostUnavailable, MultiplayerFailureStage.ClientConnect, "Steam lobby ownership changed away from the immutable network host."), isStartupFailure: false, LogType.Warning);
					}
					else if (_clientWaitingForHost && IsSameLobby(lobby))
					{
						StopHostReadyTimeout();
						_clientWaitingForHost = false;
						ReturnToMainMenuAfterFailure("The original host left before the game session became ready.", isStartupFailure: false, LogType.Warning);
					}
				}
				else
				{
					if (_clientWaitingForHost || _metadataReadinessCoroutine != null || (_metadataReadinessFailedLobbyId == lobby.Id.Value && text != "waiting"))
					{
						return;
					}
					if (_clientJoinInProgress)
					{
						IsSameLobby(lobby);
						return;
					}
					NetworkConnectionState netState = _netState;
					if (netState == NetworkConnectionState.Connecting || netState == NetworkConnectionState.Connected)
					{
						return;
					}
					bool flag3;
					switch (text)
					{
					case "waiting":
						StopSessionMetadataReadinessWait();
						RefreshMainMenuLobbyUi(lobby);
						break;
					case "connecting":
						if (!flag)
						{
							BeginSessionMetadataReadinessWait(lobby);
							break;
						}
						StopSessionMetadataReadinessWait();
						if (descriptor.LaunchCohort.Contains(SteamClient.SteamId.Value))
						{
							_sessionEpoch = descriptor.Epoch;
							_networkHostSteamId = descriptor.NetworkHostSteamId;
							UtilityMethods.Log("[Coordinator] Lobby state=" + text + ", waiting for host startup.");
							IsLateJoiner = true;
							if (IsMainMenuStateActive && Singleton<MainMenuManager>.Instance != null)
							{
								Singleton<MainMenuManager>.Instance.AnimateBoat();
							}
						}
						break;
					case "host_ingame":
					case "ingame":
						{
							if (!flag)
							{
								BeginSessionMetadataReadinessWait(lobby);
								break;
							}
							if (flag && descriptor.Epoch == _incompatibleSessionEpoch)
							{
								break;
							}
							bool flag2 = flag && LobbySessionMetadata.IsHeartbeatFresh(descriptor, LobbySessionMetadata.UtcNowUnixSeconds, Mathf.CeilToInt(_heartbeatMaxAgeSeconds));
							flag3 = flag && descriptor.LaunchCohort.Contains(SteamClient.SteamId.Value);
							bool num;
							if (!(text == "host_ingame"))
							{
								if (!flag)
								{
									goto IL_03bd;
								}
								num = descriptor.Joinable;
							}
							else
							{
								num = flag3;
							}
							if (num)
							{
								if (!flag2)
								{
									BeginSessionMetadataReadinessWait(lobby);
									break;
								}
								StopSessionMetadataReadinessWait();
								_metadataReadinessFailedLobbyId = 0uL;
								_sessionEpoch = descriptor.Epoch;
								_networkHostSteamId = descriptor.NetworkHostSteamId;
								UtilityMethods.Log("[Coordinator] Lobby state=" + text + ", trying client connect.");
								if (IsMainMenuStateActive)
								{
									IsLateJoiner = true;
									Singleton<MainMenuManager>.Instance?.AnimateBoat();
								}
								else
								{
									SteamId ownerId = descriptor.NetworkHostSteamId;
									StartClient(ownerId, lobby);
								}
								break;
							}
							goto IL_03bd;
						}
						IL_03bd:
						if ((text == "ingame") | flag3)
						{
							ShowFailure(MultiplayerFailure.Create(MultiplayerFailureCode.SessionExpired, MultiplayerFailureStage.ClientConnect, "Lobby session metadata is missing, closed, or stale."));
						}
						break;
					}
				}
			}
		}

		private void BeginSessionMetadataReadinessWait(Lobby lobby)
		{
			if ((ulong)lobby.Id != 0L && _metadataReadinessFailedLobbyId != lobby.Id.Value && (_metadataReadinessCoroutine == null || _metadataReadinessLobbyId != lobby.Id.Value))
			{
				StopSessionMetadataReadinessWait();
				_metadataReadinessLobbyId = lobby.Id.Value;
				bool flag = LobbySessionMetadata.TryRead(lobby, out var descriptor);
				_metadataPreparationWait = new HostPreparationWait(Time.realtimeSinceStartupAsDouble, lobby.Id.Value, (_networkHostSteamId != 0L) ? _networkHostSteamId : lobby.Owner.Id.Value, (_sessionEpoch != 0L) ? _sessionEpoch : (flag ? descriptor.Epoch : 0), SteamClient.IsValid ? SteamClient.SteamId.Value : 0, ReleaseVersion.Value, 120.0);
				_metadataReadinessCoroutine = StartCoroutine(CoWaitForCommittedSessionMetadata(_metadataPreparationWait));
			}
		}

		private IEnumerator CoWaitForCommittedSessionMetadata(HostPreparationWait monitor)
		{
			yield return null;
			if (monitor != _metadataPreparationWait)
			{
				yield break;
			}
			ulong lobbyId = monitor.LobbyId;
			MultiplayerDiagnostics.Record("metadata_refresh_wait_begin", $"lobby={lobbyId} epoch={monitor.Epoch}", ulong.MaxValue);
			WaitForSecondsRealtime poll = new WaitForSecondsRealtime(0.25f);
			while (monitor == _metadataPreparationWait && _metadataReadinessLobbyId == lobbyId && SessionPhase != MultiplayerSessionPhase.ReturningToMenu && !(_steam == null) && _steam.CurrentLobby.Id.Value == lobbyId)
			{
				Lobby currentLobby = _steam.CurrentLobby;
				bool flag = LobbySessionMetadata.TryRead(currentLobby, out var descriptor);
				double realtimeSinceStartupAsDouble = Time.realtimeSinceStartupAsDouble;
				HostPreparationStatus hostPreparationStatus = monitor.Evaluate(realtimeSinceStartupAsDouble, currentLobby.Id, currentLobby.Owner.Id, flag ? new LobbySessionDescriptor?(descriptor) : ((LobbySessionDescriptor?)null), LobbySessionMetadata.UtcNowUnixSeconds, Mathf.CeilToInt(_heartbeatMaxAgeSeconds));
				bool flag2 = flag && !IsLobbyHostReady(descriptor.State) && (hostPreparationStatus == HostPreparationStatus.Waiting || hostPreparationStatus == HostPreparationStatus.Warning);
				if ((hostPreparationStatus == HostPreparationStatus.Ready) | flag2)
				{
					_metadataReadinessCoroutine = null;
					_metadataPreparationWait = null;
					_metadataReadinessLobbyId = 0uL;
					_metadataReadinessFailedLobbyId = 0uL;
					MultiplayerDiagnostics.Record("metadata_refresh_wait_complete", "state=" + descriptor.State, ulong.MaxValue);
					HandleLobbyState(currentLobby);
					yield break;
				}
				if (hostPreparationStatus != HostPreparationStatus.Waiting && hostPreparationStatus != HostPreparationStatus.Warning)
				{
					_metadataReadinessCoroutine = null;
					_metadataPreparationWait = null;
					_metadataReadinessLobbyId = 0uL;
					_metadataReadinessFailedLobbyId = lobbyId;
					MultiplayerFailure failure = HostPreparationFailure(hostPreparationStatus, realtimeSinceStartupAsDouble - monitor.StartedAt);
					MultiplayerDiagnostics.Record("metadata_refresh_wait_failed", $"status={hostPreparationStatus}", ulong.MaxValue, critical: true);
					ShowFailure(failure);
					yield break;
				}
				if (hostPreparationStatus == HostPreparationStatus.Warning)
				{
					MultiplayerDiagnostics.Record("metadata_refresh_wait_warning", FormattableString.Invariant($"elapsedSeconds={realtimeSinceStartupAsDouble - monitor.StartedAt:F3}"), ulong.MaxValue);
				}
				if (monitor.ShouldRefresh(realtimeSinceStartupAsDouble))
				{
					RefreshSessionMetadata(currentLobby);
				}
				yield return poll;
			}
			if (monitor == _metadataPreparationWait)
			{
				_metadataReadinessCoroutine = null;
				_metadataPreparationWait = null;
				_metadataReadinessLobbyId = 0uL;
			}
		}

		private void RefreshSessionMetadata(Lobby lobby)
		{
			if (!IsSteamConnectionAvailable || (ulong)_steam.CurrentLobby.Id != (ulong)lobby.Id)
			{
				return;
			}
			try
			{
				bool flag = lobby.Refresh();
				MultiplayerDiagnostics.Record("metadata_refresh_requested", $"accepted={flag}", ulong.MaxValue);
			}
			catch (Exception ex)
			{
				MultiplayerDiagnostics.Record("metadata_refresh_failed", ex.Message, ulong.MaxValue);
			}
		}

		private void StopSessionMetadataReadinessWait()
		{
			if (_metadataReadinessCoroutine != null)
			{
				StopCoroutine(_metadataReadinessCoroutine);
			}
			_metadataReadinessCoroutine = null;
			_metadataPreparationWait = null;
			_metadataReadinessLobbyId = 0uL;
		}

		private void OnGameLobbyJoinRequested(Lobby lobby, SteamId id)
		{
			UtilityMethods.Log($"[Coordinator] OnGameLobbyJoinRequested id={id}");
			RoomService?.CancelPendingJoin();
			QueueLobbyJoin(lobby);
		}

		private void QueueLobbyJoin(Lobby lobby)
		{
			if (!IsHostStarted)
			{
				IsSingleplayerSession = false;
			}
			if ((ulong)_steam.CurrentLobby.Id != (ulong)lobby.Id || (ulong)lobby.Id == 0L)
			{
				_pendingLobbyJoin = lobby;
				_hasPendingLobbyJoin = true;
				_clientCancellationVersion++;
				_lobbyOperations.CancelActive();
				if (RequiresReturnBeforeLobbyJoin())
				{
					UtilityMethods.Log("<b><color=#FFB020>[Multiplayer]</color></b> Lobby join queued until the active session returns to main menu." + $"\n   LobbyId: {lobby.Id}");
					RequestSessionReturn(ReturnToMainMenuRequest.JoinRequested(lobby.Id.Value));
				}
				else
				{
					JoinPendingLobbyIfReady();
				}
			}
		}

		private void OnLobbyInternalMessageReceived(Lobby lobby, Friend sender, string payload)
		{
			if (_sessionReturnPrepared || _intentionalNetworkShutdown || _steam == null || !LobbyLaunchProtocol.TryParse(payload, out var message) || (ulong)lobby.Id == 0L || (ulong)lobby.Id != (ulong)_steam.CurrentLobby.Id)
			{
				return;
			}
			if (LobbyLaunchProtocol.IsClosureForSession(message, sender.Id.Value, SteamClient.SteamId.Value, _networkHostSteamId, _sessionEpoch))
			{
				HandleHostSessionClosed("lobby_message");
			}
			else
			{
				if (_reconnectRecovery != null)
				{
					return;
				}
				if (message.Kind == LobbyLaunchMessageKind.Prepare)
				{
					if ((RoomService != null && !RoomService.IsLocalAdmitted) || !LobbyLaunchProtocol.IsPrepareForRemoteClient(message, sender.Id.Value, lobby.Owner.Id.Value, SteamClient.SteamId.Value, _lastHandledPrepareEpoch))
					{
						return;
					}
					int num = ((Singleton<GameController>.Instance != null) ? Singleton<GameController>.Instance.Version : 0);
					if (message.GameVersion != num || !string.Equals(message.ReleaseVersion, ReleaseVersion.Value, StringComparison.Ordinal))
					{
						_incompatibleSessionEpoch = message.Epoch;
						_incompatibleGameVersionEpoch = ((message.GameVersion != num) ? message.Epoch : 0);
						RefreshLobbyVersionAlert();
						ShowFailure(MultiplayerFailure.Create(MultiplayerFailureCode.VersionMismatch, MultiplayerFailureStage.LaunchPreparation, $"HostGame={message.GameVersion}, LocalGame={num}, HostRelease={message.ReleaseVersion}, LocalRelease={ReleaseVersion.Value}."));
						return;
					}
					if (!IsSteamConnectionAvailable)
					{
						ShowFailure(MultiplayerFailure.Create(MultiplayerFailureCode.SteamUnavailable, MultiplayerFailureStage.LaunchPreparation, "PrepareLaunch was received while Steam was unavailable."));
						return;
					}
					if (message.DeadlineUnixSeconds < LobbySessionMetadata.UtcNowUnixSeconds)
					{
						ShowFailure(MultiplayerFailure.Create(MultiplayerFailureCode.SessionExpired, MultiplayerFailureStage.LaunchPreparation, "PrepareLaunch arrived after its readiness deadline."));
						return;
					}
					_lastHandledPrepareEpoch = message.Epoch;
					_metadataReadinessFailedLobbyId = 0uL;
					StopSessionMetadataReadinessWait();
					_sessionEpoch = message.Epoch;
					_networkHostSteamId = message.HostSteamId;
					IsHostStarted = false;
					IsLateJoiner = true;
					SetSessionPhase(MultiplayerSessionPhase.PreparingLaunch);
					LobbyLaunchMessage message2 = new LobbyLaunchMessage
					{
						Kind = LobbyLaunchMessageKind.Ready,
						Epoch = message.Epoch,
						HostSteamId = message.HostSteamId,
						GameVersion = num,
						ReleaseVersion = ReleaseVersion.Value,
						DeadlineUnixSeconds = message.DeadlineUnixSeconds
					};
					if (!_steam.SendLobbyInternalMessage(LobbyLaunchProtocol.Serialize(message2)))
					{
						ShowFailure(MultiplayerFailure.Create(MultiplayerFailureCode.LaunchPreparationFailed, MultiplayerFailureStage.LaunchPreparation, "LaunchReady could not be sent to the Steam lobby."));
					}
					if (IsMainMenuStateActive && Singleton<MainMenuManager>.Instance != null)
					{
						Singleton<MainMenuManager>.Instance.AnimateBoat();
					}
				}
				else if (message.Kind == LobbyLaunchMessageKind.Ready && IsCurrentLobbyOwner && SessionPhase == MultiplayerSessionPhase.PreparingLaunch && message.Epoch == _sessionEpoch && message.HostSteamId == SteamClient.SteamId.Value)
				{
					bool flag = lobby.Members.Any((Friend member) => (ulong)member.Id == (ulong)sender.Id);
					if ((!(RoomService != null) || RoomService.IsMemberAdmitted(sender.Id.Value)) && flag && message.GameVersion == ((Singleton<GameController>.Instance != null) ? Singleton<GameController>.Instance.Version : 0) && string.Equals(message.ReleaseVersion, ReleaseVersion.Value, StringComparison.Ordinal) && _launchPreparation.AcceptReady(sender.Id.Value, Time.realtimeSinceStartupAsDouble))
					{
						MultiplayerDiagnostics.Record("launch_member_ready", $"ready={_launchReadySteamIds.Count} expected={_launchPreparation.ExpectedCount}", ulong.MaxValue);
						UpdateLaunchPreparationLoadingStatus();
					}
				}
			}
		}

		private bool RequiresReturnBeforeLobbyJoin()
		{
			if (Singleton<GameSessionLifecycle>.Instance != null && Singleton<GameSessionLifecycle>.Instance.ReturnToMenuInProgress)
			{
				return true;
			}
			if (!IsMainMenuStateActive)
			{
				return true;
			}
			if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
			{
				return true;
			}
			if (!IsLateJoiner && !IsHostStarted && (!Singleton<MainMenuManager>.TryGetInstance(out var instance) || !instance.IsBoatDepartureInProgress) && !_lobbyJoinInProgress && _reconnectRecovery == null && !_hostStartInProgress && !_clientJoinInProgress)
			{
				NetworkConnectionState netState = _netState;
				return netState == NetworkConnectionState.Connecting || netState == NetworkConnectionState.Connected;
			}
			return true;
		}

		private async void JoinPendingLobbyIfReady()
		{
			if (!_hasPendingLobbyJoin || _lobbyJoinInProgress || !IsSteamConnectionAvailable || !IsMainMenuStateActive || _sessionReturnPrepared || !IsSessionShutdownComplete || (Singleton<GameSessionLifecycle>.TryGetInstance(out var instance) && instance.ReturnToMenuInProgress))
			{
				return;
			}
			Lobby targetLobby = _pendingLobbyJoin;
			TaskCompletionSource<LobbyJoinOutcome> roomCompletion = _roomJoinCompletion;
			ulong targetLobbyId = targetLobby.Id.Value;
			_lobbyJoinInProgress = true;
			bool createReplacementLobby = false;
			LobbyOperationLease operation = _lobbyOperations.Begin(LobbyOperationKind.Join, targetLobbyId);
			try
			{
				if ((ulong)_steam.CurrentLobby.Id != 0L && (ulong)_steam.CurrentLobby.Id != (ulong)targetLobby.Id)
				{
					_steam.LeaveCurrentLobby();
				}
				UtilityMethods.Log($"[Coordinator] Joining pending lobby. LobbyId={targetLobby.Id}");
				LobbyJoinOutcome result = await _steam.JoinLobbyAsync(targetLobby, operation.CancellationToken);
				if (!_lobbyOperations.IsCurrent(operation))
				{
					return;
				}
				roomCompletion?.TrySetResult(result);
				if (result.Succeeded)
				{
					if (_hasPendingLobbyJoin && _pendingLobbyJoin.Id.Value == targetLobbyId)
					{
						_hasPendingLobbyJoin = false;
					}
					_lobbyOperations.Complete(operation);
					return;
				}
				UtilityMethods.Log($"[Coordinator] Pending lobby join failed. LobbyId={targetLobby.Id}, Result={result.RoomEnter}, TimedOut={result.TimedOut}", LogType.Warning);
				MultiplayerFailureCode code = ((result.RoomEnter == RoomEnter.Full) ? MultiplayerFailureCode.LobbyFull : MultiplayerFailureCode.LobbyJoinFailed);
				ShowFailure(MultiplayerFailure.Create(code, MultiplayerFailureStage.LobbyJoin, $"LobbyId={targetLobbyId}, RoomEnter={result.RoomEnter}, TimedOut={result.TimedOut}."));
				if (_hasPendingLobbyJoin && _pendingLobbyJoin.Id.Value == targetLobbyId)
				{
					_hasPendingLobbyJoin = false;
					createReplacementLobby = true;
				}
				_lobbyOperations.Complete(operation);
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex2)
			{
				UtilityMethods.Log($"[Coordinator] Pending lobby join exception. LobbyId={targetLobby.Id}, Exception={ex2}", LogType.Error);
				if (_lobbyOperations.IsCurrent(operation))
				{
					if (_hasPendingLobbyJoin && _pendingLobbyJoin.Id.Value == targetLobbyId)
					{
						_hasPendingLobbyJoin = false;
						createReplacementLobby = true;
					}
					ShowFailure(MultiplayerFailure.Create(MultiplayerFailureCode.LobbyJoinFailed, MultiplayerFailureStage.LobbyJoin, ex2.Message));
				}
			}
			finally
			{
				if (roomCompletion != null && !roomCompletion.Task.IsCompleted)
				{
					roomCompletion.TrySetCanceled();
				}
				_lobbyJoinInProgress = false;
				if (_hasPendingLobbyJoin)
				{
					JoinPendingLobbyIfReady();
				}
				else if (createReplacementLobby || ((ulong)_steam.CurrentLobby.Id == 0L && IsMainMenuStateActive && !_sessionReturnPrepared && !_intentionalNetworkShutdown && IsSessionShutdownComplete))
				{
					CreateLobby();
				}
			}
		}

		private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
		{
			UtilityMethods.Log($"[Coordinator] OnLobbyMemberJoined lobby={lobby.Id} owner={lobby.Owner.Id} friend={friend.Id}");
			RefreshMainMenuLobbyUi(lobby);
			RefreshForeignLobbyState();
		}

		private void OnLobbyMemberLeft(Lobby lobby, Friend friend)
		{
			UtilityMethods.Log($"[Coordinator] OnLobbyMemberLeft lobby={lobby.Id} owner={lobby.Owner.Id} friend={friend.Id}");
			if (_clientWaitingForHost && IsSameLobby(lobby) && (ulong)friend.Id == (ulong)_joiningOwnerId)
			{
				StopHostReadyTimeout();
				_clientWaitingForHost = false;
				ReturnToMainMenuAfterFailure("The host left before the game session became ready.", isStartupFailure: false, LogType.Warning);
				return;
			}
			RefreshMainMenuLobbyUi(lobby);
			RefreshForeignLobbyState();
			_launchPreparation.RemoveMember(friend.Id.Value);
			if (IsCurrentLobbyOwner && IsHostStarted && _launchCohortSteamIds.Remove(friend.Id.Value))
			{
				SetTargetPlayerCount(Mathf.Max(1, _launchCohortSteamIds.Count));
			}
		}

		private void OnLobbyInvite(Friend friend, Lobby lobby)
		{
			UtilityMethods.Log("[Coordinator] OnLobbyInvite from=" + friend.Name);
		}

		public void StartClient()
		{
			if (_sessionReturnPrepared || _intentionalNetworkShutdown || _reconnectRecovery != null || _clientWaitingForHost || _clientJoinInProgress)
			{
				return;
			}
			_metadataReadinessFailedLobbyId = 0uL;
			LobbySessionDescriptor descriptor;
			if (!TryGetActiveLobby(out var lobby))
			{
				ReturnToMainMenuAfterFailure(MultiplayerFailure.Create(MultiplayerFailureCode.LobbyJoinFailed, MultiplayerFailureStage.ClientConnect, "The Steam lobby is no longer available."));
			}
			else if (!LobbySessionMetadata.TryRead(lobby, out descriptor))
			{
				WaitForHostReady(lobby.Owner.Id, lobby, lobby.GetData("session_state"));
			}
			else if (AcceptPreparationSession(descriptor))
			{
				_sessionEpoch = descriptor.Epoch;
				_networkHostSteamId = descriptor.NetworkHostSteamId;
				SteamId ownerId = descriptor.NetworkHostSteamId;
				string state = descriptor.State;
				if (IsLobbyHostReady(state) && IsClientAdmissionOpen(descriptor))
				{
					StartClient(ownerId, lobby);
				}
				else
				{
					WaitForHostReady(ownerId, lobby, state);
				}
			}
		}

		private void WaitForHostReady(SteamId ownerId, Lobby lobby, string state)
		{
			if ((_clientWaitingForHost && IsSameLobby(lobby)) || _clientJoinInProgress)
			{
				return;
			}
			NetworkConnectionState netState = _netState;
			if (netState == NetworkConnectionState.Connecting || netState == NetworkConnectionState.Connected || !NetworkManager.Singleton || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsHost || !IsLoadingStateActive)
			{
				return;
			}
			if (LobbySessionMetadata.TryRead(lobby, out var descriptor))
			{
				if (!AcceptPreparationSession(descriptor))
				{
					return;
				}
				_sessionEpoch = descriptor.Epoch;
				_networkHostSteamId = descriptor.NetworkHostSteamId;
				ownerId = descriptor.NetworkHostSteamId;
				if (!ValidateClientConnectionTargetOrReturnToMenu(ownerId, lobby, requireFreshHeartbeat: false))
				{
					return;
				}
			}
			else if (ownerId.Value == 0L || (ulong)lobby.Id == 0L || (ulong)_steam.CurrentLobby.Id != (ulong)lobby.Id)
			{
				ReturnToMainMenuAfterFailure(MultiplayerFailure.Create(MultiplayerFailureCode.HostUnavailable, MultiplayerFailureStage.ClientConnect, "Host metadata is not available and the lobby owner is invalid."));
				return;
			}
			_clientWaitingForHost = true;
			_joiningLobbyId = lobby.Id.Value;
			_joiningOwnerId = ownerId;
			MultiplayerDiagnostics.SetContext(lobby.Id, _sessionEpoch, SteamClient.IsValid ? SteamClient.SteamId.Value : 0, _networkHostSteamId, ActiveOperationId);
			MultiplayerDiagnostics.WatchStartup();
			MultiplayerDiagnostics.Record("host_ready_wait_begin", FormattableString.Invariant($"state={state} timeoutSeconds={HostPreparationTimeoutSeconds}"), ulong.MaxValue);
			LoadingScreen.UpdateLocalizedText("loading.title.joining_session", "Joining Session", "loading.status.waiting_for_host", "Waiting for the host to create the game session...");
			StartHostReadyTimeout();
			UtilityMethods.Log("<b><color=#8FD14F>[Multiplayer]</color></b> Waiting for host readiness." + $"\n   LobbyId: {lobby.Id}" + $"\n   OwnerId: {ownerId}" + "\n   LobbyState: " + state + $"\n   TimeoutSeconds: {HostPreparationTimeoutSeconds:0}");
		}

		private bool AcceptPreparationSession(LobbySessionDescriptor descriptor)
		{
			if (_sessionEpoch == 0L || (_sessionEpoch == descriptor.Epoch && (_networkHostSteamId == 0L || _networkHostSteamId == descriptor.NetworkHostSteamId)))
			{
				return true;
			}
			ReturnToMainMenuAfterFailure(HostPreparationFailure(HostPreparationStatus.SessionChanged, 0.0), isStartupFailure: false, LogType.Warning);
			return false;
		}

		private void StartClient(SteamId ownerId, Lobby lobby)
		{
			if (_sessionReturnPrepared || _intentionalNetworkShutdown || _reconnectRecovery != null)
			{
				return;
			}
			UtilityMethods.Log($"[Coordinator] StartClient Begin owner={ownerId} lobby={lobby.Id}");
			if (_clientJoinInProgress)
			{
				return;
			}
			NetworkConnectionState netState = _netState;
			if (netState == NetworkConnectionState.Connecting || netState == NetworkConnectionState.Connected || !NetworkManager.Singleton || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsHost || !IsLoadingStateActive || !ValidateClientConnectionTargetOrReturnToMenu(ownerId, lobby, requireFreshHeartbeat: false))
			{
				return;
			}
			if (!LobbySessionMetadata.TryRead(lobby, out var descriptor) || !IsLobbyHostReady(descriptor.State) || !LobbySessionMetadata.IsHeartbeatFresh(descriptor, LobbySessionMetadata.UtcNowUnixSeconds, Mathf.CeilToInt(_heartbeatMaxAgeSeconds)))
			{
				WaitForHostReady(ownerId, lobby, descriptor.State);
				return;
			}
			StopHostReadyTimeout();
			StopSessionMetadataReadinessWait();
			_clientWaitingForHost = false;
			_clientJoinInProgress = true;
			_joiningLobbyId = lobby.Id;
			_joiningOwnerId = ownerId;
			_netState = NetworkConnectionState.Connecting;
			SetSessionPhase(MultiplayerSessionPhase.ClientConnecting);
			if (_ngo.StartClient(ownerId, BuildConnectionPayload()))
			{
				LoadingScreen.UpdateLocalizedText("loading.title.joining_session", "Joining Session", "loading.status.loading_world", "Loading the game world...");
				StartClientConnectionTimeout();
			}
			else
			{
				HandleClientConnectionFailure(MultiplayerFailure.Create(MultiplayerFailureCode.ClientStartFailed, MultiplayerFailureStage.ClientConnect, "NetworkManager.StartClient returned false."));
			}
		}

		private bool ValidateClientConnectionTargetOrReturnToMenu(SteamId ownerId, Lobby lobby, bool requireFreshHeartbeat = true)
		{
			if (TryValidateClientConnectionTarget(ownerId, lobby, out var failureReason, out var failureCode, requireFreshHeartbeat))
			{
				return true;
			}
			UtilityMethods.Log("<b><color=#FF4040>[Multiplayer]</color></b> Client connection target is no longer valid.\n   Reason: " + failureReason + $"\n   LobbyId: {lobby.Id}" + $"\n   RequestedOwnerId: {ownerId}" + $"\n   CurrentOwnerId: {lobby.Owner.Id}" + $"\n   LocalSteamId: {(SteamClient.IsValid ? SteamClient.SteamId.Value : 0)}", LogType.Error);
			ReturnToMainMenuAfterFailure(MultiplayerFailure.Create(failureCode, MultiplayerFailureStage.ClientConnect, failureReason));
			return false;
		}

		private bool TryValidateClientConnectionTarget(SteamId ownerId, Lobby lobby, out string failureReason, out MultiplayerFailureCode failureCode, bool requireFreshHeartbeat = true)
		{
			failureCode = MultiplayerFailureCode.HostUnavailable;
			if (!IsSteamConnectionAvailable)
			{
				failureReason = "Steam connection is unavailable.";
				failureCode = MultiplayerFailureCode.SteamUnavailable;
				return false;
			}
			if (lobby.Id.Value == 0L)
			{
				failureReason = "Lobby ID is invalid.";
				return false;
			}
			if ((ulong)_steam.CurrentLobby.Id == 0L || (ulong)_steam.CurrentLobby.Id != (ulong)lobby.Id)
			{
				failureReason = "The requested lobby is no longer the active Steam lobby.";
				return false;
			}
			if (ownerId.Value == 0L || lobby.Owner.Id.Value == 0L)
			{
				failureReason = "Lobby owner ID is invalid.";
				return false;
			}
			if (!LobbySessionMetadata.TryRead(lobby, out var descriptor))
			{
				failureReason = "Session metadata is missing or invalid.";
				return false;
			}
			if (descriptor.NetworkHostSteamId != ownerId.Value || descriptor.Epoch != _sessionEpoch)
			{
				failureReason = "Network host or session epoch changed before the connection started.";
				failureCode = MultiplayerFailureCode.SessionChanged;
				return false;
			}
			if (!string.Equals(descriptor.ReleaseVersion, ReleaseVersion.Value, StringComparison.Ordinal))
			{
				failureReason = "The lobby release version does not match this game build.";
				failureCode = MultiplayerFailureCode.VersionMismatch;
				return false;
			}
			if (lobby.Owner.Id.Value != descriptor.NetworkHostSteamId)
			{
				failureReason = "Lobby ownership changed; host migration is not supported.";
				failureCode = MultiplayerFailureCode.HostUnavailable;
				return false;
			}
			bool flag = descriptor.LaunchCohort.Contains(SteamClient.SteamId.Value);
			bool flag2 = ((descriptor.State == "host_ingame") ? flag : descriptor.Joinable);
			if (IsLobbyHostReady(descriptor.State) && !flag2)
			{
				failureReason = "Session admission is closed. State=" + descriptor.State + ", " + $"Joinable={descriptor.Joinable}, InitialCohortMember={flag}.";
				failureCode = MultiplayerFailureCode.SessionExpired;
				return false;
			}
			long utcNowUnixSeconds = LobbySessionMetadata.UtcNowUnixSeconds;
			if (requireFreshHeartbeat && IsLobbyHostReady(descriptor.State) && !LobbySessionMetadata.IsHeartbeatFresh(descriptor, utcNowUnixSeconds, Mathf.CeilToInt(_heartbeatMaxAgeSeconds)))
			{
				long num = utcNowUnixSeconds - descriptor.HeartbeatUnixSeconds;
				failureReason = "Host heartbeat is not fresh. State=" + descriptor.State + ", " + $"Heartbeat={descriptor.HeartbeatUnixSeconds}, Now={utcNowUnixSeconds}, " + $"AgeSeconds={num}, MaxAgeSeconds={_heartbeatMaxAgeSeconds:0}, " + $"MaxFutureSkewSeconds={30}.";
				failureCode = MultiplayerFailureCode.SessionExpired;
				return false;
			}
			if ((ulong)ownerId == (ulong)SteamClient.SteamId)
			{
				failureReason = "The local user became lobby owner before the client connection started.";
				return false;
			}
			failureReason = string.Empty;
			failureCode = MultiplayerFailureCode.None;
			return true;
		}

		private static bool IsLobbyHostReady(string state)
		{
			if (!(state == "host_ingame"))
			{
				return state == "ingame";
			}
			return true;
		}

		private static bool IsClientAdmissionOpen(LobbySessionDescriptor descriptor)
		{
			if (descriptor.State == "ingame")
			{
				return descriptor.Joinable;
			}
			if (descriptor.State == "host_ingame" && SteamClient.IsValid)
			{
				return descriptor.LaunchCohort.Contains(SteamClient.SteamId.Value);
			}
			return false;
		}

		private void StartHostReadyTimeout()
		{
			StopHostReadyTimeout();
			StopSessionMetadataReadinessWait();
			_hostPreparationWait = new HostPreparationWait(Time.realtimeSinceStartupAsDouble, _joiningLobbyId, _joiningOwnerId.Value, _sessionEpoch, SteamClient.IsValid ? SteamClient.SteamId.Value : 0, ReleaseVersion.Value, HostPreparationTimeoutSeconds);
			_hostReadyTimeoutCor = StartCoroutine(CoHostReadyTimeout());
		}

		private IEnumerator CoHostReadyTimeout()
		{
			HostPreparationWait monitor = _hostPreparationWait;
			yield return null;
			WaitForSecondsRealtime poll = new WaitForSecondsRealtime(0.25f);
			while (monitor != null && monitor == _hostPreparationWait && _clientWaitingForHost && SessionPhase != MultiplayerSessionPhase.ReturningToMenu && _joiningLobbyId == monitor.LobbyId && _joiningOwnerId.Value == monitor.HostId && _sessionEpoch == monitor.Epoch && !(_steam == null) && _steam.CurrentLobby.Id.Value == monitor.LobbyId)
			{
				Lobby currentLobby = _steam.CurrentLobby;
				bool flag = LobbySessionMetadata.TryRead(currentLobby, out var descriptor);
				double realtimeSinceStartupAsDouble = Time.realtimeSinceStartupAsDouble;
				HostPreparationStatus hostPreparationStatus = monitor.Evaluate(realtimeSinceStartupAsDouble, currentLobby.Id, currentLobby.Owner.Id, flag ? new LobbySessionDescriptor?(descriptor) : ((LobbySessionDescriptor?)null), LobbySessionMetadata.UtcNowUnixSeconds, Mathf.CeilToInt(_heartbeatMaxAgeSeconds));
				if (_sessionEpoch == 0L && monitor.Epoch != 0L)
				{
					_sessionEpoch = monitor.Epoch;
					_networkHostSteamId = monitor.HostId;
				}
				switch (hostPreparationStatus)
				{
				case HostPreparationStatus.Ready:
					_hostReadyTimeoutCor = null;
					_hostPreparationWait = null;
					_clientWaitingForHost = false;
					MultiplayerDiagnostics.Record("host_ready_wait_complete", FormattableString.Invariant($"elapsedSeconds={realtimeSinceStartupAsDouble - monitor.StartedAt:F3}"), ulong.MaxValue);
					StartClient(_joiningOwnerId, currentLobby);
					yield break;
				default:
					_hostReadyTimeoutCor = null;
					_hostPreparationWait = null;
					_clientWaitingForHost = false;
					MultiplayerDiagnostics.Record("host_ready_wait_failed", $"status={hostPreparationStatus}", ulong.MaxValue, critical: true);
					if (hostPreparationStatus == HostPreparationStatus.TimedOut)
					{
						SendClientJoinTimeoutAnalytics("HostReady", (float)(realtimeSinceStartupAsDouble - monitor.StartedAt));
					}
					ReturnToMainMenuAfterFailure(HostPreparationFailure(hostPreparationStatus, realtimeSinceStartupAsDouble - monitor.StartedAt), isStartupFailure: false, LogType.Warning);
					yield break;
				case HostPreparationStatus.Waiting:
				case HostPreparationStatus.Warning:
					if (hostPreparationStatus == HostPreparationStatus.Warning)
					{
						MultiplayerDiagnostics.Record("host_ready_wait_warning", FormattableString.Invariant($"elapsedSeconds={realtimeSinceStartupAsDouble - monitor.StartedAt:F3}"), ulong.MaxValue);
					}
					if (monitor.ShouldRefresh(realtimeSinceStartupAsDouble))
					{
						RefreshSessionMetadata(currentLobby);
					}
					yield return poll;
					break;
				}
			}
		}

		private static MultiplayerFailure HostPreparationFailure(HostPreparationStatus status, double elapsed)
		{
			return MultiplayerFailure.Create(status switch
			{
				HostPreparationStatus.HostUnavailable => MultiplayerFailureCode.HostUnavailable, 
				HostPreparationStatus.SessionChanged => MultiplayerFailureCode.SessionChanged, 
				HostPreparationStatus.VersionMismatch => MultiplayerFailureCode.VersionMismatch, 
				HostPreparationStatus.AdmissionClosed => MultiplayerFailureCode.SessionExpired, 
				_ => MultiplayerFailureCode.ConnectionTimedOut, 
			}, MultiplayerFailureStage.LaunchPreparation, FormattableString.Invariant($"Host readiness validation ended. Status={status}, ElapsedSeconds={elapsed:F1}."));
		}

		private void StopHostReadyTimeout()
		{
			_hostPreparationWait = null;
			if (_hostReadyTimeoutCor != null)
			{
				StopCoroutine(_hostReadyTimeoutCor);
				_hostReadyTimeoutCor = null;
			}
		}

		private void StartClientConnectionTimeout()
		{
			StopClientConnectionTimeout();
			_clientConnectTimeoutCor = StartCoroutine(CoClientConnectionTimeout());
		}

		private IEnumerator CoClientConnectionTimeout()
		{
			long attempt = _ngo.JoinAttempt;
			ulong epoch = _sessionEpoch;
			ulong lobbyId = _joiningLobbyId;
			yield return null;
			while (_clientJoinInProgress && _netState == NetworkConnectionState.Connecting && epoch == _sessionEpoch && lobbyId == _joiningLobbyId && attempt == _ngo.JoinAttempt && (!(Singleton<GameSessionLifecycle>.Instance != null) || !Singleton<GameSessionLifecycle>.Instance.ReturnToMenuInProgress))
			{
				if ((bool)NetworkManager.Singleton && NetworkManager.Singleton.IsConnectedClient)
				{
					_clientJoinInProgress = false;
					_netState = NetworkConnectionState.Connected;
					break;
				}
				double realtimeSinceStartupAsDouble = Time.realtimeSinceStartupAsDouble;
				JoinWaitStatus joinWaitStatus = _ngo.CheckJoinDeadline(attempt, realtimeSinceStartupAsDouble);
				if (joinWaitStatus == JoinWaitStatus.Inactive)
				{
					break;
				}
				if (joinWaitStatus == JoinWaitStatus.Warning)
				{
					MultiplayerDiagnostics.Record("join_wait_warning", FormattableString.Invariant($"joinAttempt={attempt} stage={_ngo.JoinStage} elapsed={realtimeSinceStartupAsDouble - _ngo.JoinStageStartedAt:F1} limitSeconds={_ngo.JoinStageLimitSeconds}"), ulong.MaxValue);
				}
				if (joinWaitStatus == JoinWaitStatus.TimedOut || joinWaitStatus == JoinWaitStatus.NoProgress)
				{
					ClientJoinStage joinStage = _ngo.JoinStage;
					MultiplayerFailureStage stage = joinStage switch
					{
						ClientJoinStage.AwaitingApproval => MultiplayerFailureStage.ConnectionApproval, 
						ClientJoinStage.SceneSynchronization => MultiplayerFailureStage.SceneSync, 
						_ => MultiplayerFailureStage.ClientConnect, 
					};
					_clientConnectTimeoutCor = null;
					double num = ((joinWaitStatus == JoinWaitStatus.NoProgress) ? 360.0 : _ngo.JoinStageLimitSeconds);
					SendClientJoinTimeoutAnalytics(joinStage.ToString(), (float)num);
					HandleClientConnectionFailure(MultiplayerFailure.Create(MultiplayerFailureCode.ConnectionTimedOut, stage, FormattableString.Invariant($"Join attempt {attempt}: stage={joinStage}, status={joinWaitStatus}, elapsed={realtimeSinceStartupAsDouble - _ngo.JoinStageStartedAt:F1}s, idle={realtimeSinceStartupAsDouble - _ngo.JoinLastProgressAt:F1}s, limit={num:F0}s.")));
					yield break;
				}
				yield return null;
			}
			_clientConnectTimeoutCor = null;
		}

		private void OnClientJoinStageChanged(ClientJoinStage stage)
		{
			if (_clientJoinInProgress && _netState == NetworkConnectionState.Connecting && (!(Singleton<GameSessionLifecycle>.Instance != null) || !Singleton<GameSessionLifecycle>.Instance.ReturnToMenuInProgress) && stage == ClientJoinStage.SceneSynchronization)
			{
				SetSessionPhase(MultiplayerSessionPhase.SceneSync);
			}
		}

		private void StopClientConnectionTimeout()
		{
			if (_clientConnectTimeoutCor != null)
			{
				StopCoroutine(_clientConnectTimeoutCor);
				_clientConnectTimeoutCor = null;
			}
		}

		private void SendClientJoinTimeoutAnalytics(string stage, float timeoutSeconds)
		{
		}

		private void ReturnToMainMenuAfterFailure(string reason, bool isStartupFailure = false, LogType failureLogType = LogType.Error)
		{
			ReturnToMainMenuAfterFailure(MultiplayerFailure.Create(MultiplayerFailureCode.Unknown, isStartupFailure ? MultiplayerFailureStage.HostStart : MultiplayerFailureStage.ClientConnect, reason), isStartupFailure, failureLogType);
		}

		private void ReturnToMainMenuAfterFailure(MultiplayerFailure failure, bool isStartupFailure = false, LogType failureLogType = LogType.Error)
		{
			MultiplayerDiagnostics.Record("connection_failure", $"code={failure.Code} stage={failure.Stage} detail={failure.DebugDetail}", ulong.MaxValue, critical: true);
			MultiplayerDiagnostics.Flush("connection_failure");
			UtilityMethods.Log("[Coordinator] Connection failed. Returning to main menu." + $"\n   Code: {failure.Code}" + $"\n   Stage: {failure.Stage}" + "\n   Detail: " + failure.DebugDetail + $"\n   OperationId: {ActiveOperationId}" + $"\n   SessionEpoch: {_sessionEpoch}", failureLogType);
			ReportMultiplayerTelemetry("failure", failure);
			ReturnToMainMenuRequest request = (isStartupFailure ? ReturnToMainMenuRequest.StartupFailure(failure) : ReturnToMainMenuRequest.ConnectionFailed(failure));
			bool num = Singleton<GameSessionLifecycle>.TryGetInstance(out var _);
			RequestSessionReturn(request);
			if (!num)
			{
				ShowFailure(failure);
			}
		}

		private void ShowFailure(MultiplayerFailure failure)
		{
			MultiplayerDiagnostics.Record("visible_failure", $"code={failure.Code} stage={failure.Stage} detail={failure.DebugDetail}", ulong.MaxValue, critical: true);
			UtilityMethods.Log("<b><color=#FFB020>[Multiplayer Failure]</color></b>" + $"\n   Code: {failure.Code}" + $"\n   Stage: {failure.Stage}" + "\n   Detail: " + failure.DebugDetail + $"\n   OperationId: {ActiveOperationId}" + $"\n   SessionEpoch: {_sessionEpoch}", LogType.Warning);
			ReportMultiplayerTelemetry("failure_visible", failure);
			string text = $"{failure.Code}:{failure.Stage}:{_sessionEpoch}:{failure.DebugDetail}";
			if (_hasPendingVisibleFailure && string.Equals(text, _pendingVisibleFailureSignature, StringComparison.Ordinal))
			{
				TryPresentPendingFailure();
			}
			else if (!string.Equals(text, _lastVisibleFailureSignature, StringComparison.Ordinal) || !(Time.realtimeSinceStartup - _lastVisibleFailureRealtime < 5f))
			{
				_pendingVisibleFailure = failure;
				_hasPendingVisibleFailure = true;
				_pendingVisibleFailureToastDelivered = false;
				_pendingVisibleFailureChatDelivered = false;
				_pendingVisibleFailureSignature = text;
				TryPresentPendingFailure();
			}
		}

		private void TryPresentPendingFailure()
		{
			if (_hasPendingVisibleFailure)
			{
				MultiplayerFailure pendingVisibleFailure = _pendingVisibleFailure;
				if (!_pendingVisibleFailureToastDelivered && Singleton<NotifyManager>.TryGetInstance(out var instance))
				{
					string message = LocalizationDatabase.GetString("UiTable", pendingVisibleFailure.LocalizationKey, pendingVisibleFailure.UserMessage);
					instance.ShowToast(message, NotificationState.Error);
					_pendingVisibleFailureToastDelivered = true;
				}
				if (!_pendingVisibleFailureChatDelivered && Singleton<GlobalChatManager>.TryGetInstance(out var instance2))
				{
					instance2.AddLocalizedMultiplayerFailure(pendingVisibleFailure, preserveForNextLobby: false);
					_pendingVisibleFailureChatDelivered = true;
				}
				if (_pendingVisibleFailureToastDelivered && _pendingVisibleFailureChatDelivered)
				{
					_hasPendingVisibleFailure = false;
					_pendingVisibleFailure = default;
					_lastVisibleFailureSignature = _pendingVisibleFailureSignature;
					_lastVisibleFailureRealtime = Time.realtimeSinceStartup;
					_pendingVisibleFailureSignature = string.Empty;
				}
			}
		}

		private bool HasLobbyVersionMismatch(Lobby lobby)
		{
			if (_steam == null || (ulong)lobby.Id == 0L || (ulong)lobby.Id != (ulong)_steam.CurrentLobby.Id)
			{
				return false;
			}
			if (LobbySessionMetadata.TryRead(lobby, out var descriptor) && (ReleaseVersion.IsKnownMismatch(ReleaseVersion.Value, descriptor.ReleaseVersion) || (descriptor.State != "waiting" && _incompatibleGameVersionEpoch != 0L && descriptor.Epoch == _incompatibleGameVersionEpoch)))
			{
				return true;
			}
			foreach (Friend member in lobby.Members)
			{
				if ((ulong)member.Id != (ulong)SteamClient.SteamId && (!(RoomService != null) || RoomService.IsMemberAdmitted(member.Id.Value)) && _steam.TryGetMemberReleaseVersion(lobby, member, out var releaseVersion) && ReleaseVersion.IsKnownMismatch(ReleaseVersion.Value, releaseVersion))
				{
					return true;
				}
			}
			return false;
		}

		private void RefreshLobbyVersionAlert()
		{
			NotifyManager instance2;
			if (!IsMainMenuStateActive || !IsSteamConnectionAvailable || (Singleton<GameSessionLifecycle>.TryGetInstance(out var instance) && instance.ReturnToMenuInProgress) || !HasLobbyVersionMismatch(_steam.CurrentLobby))
			{
				ClearLobbyVersionAlert();
			}
			else if (Singleton<NotifyManager>.TryGetInstance(out instance2))
			{
				MultiplayerFailure multiplayerFailure = MultiplayerFailure.LobbyVersionMismatch(MultiplayerFailureStage.LobbyJoin);
				string text = LocalizationDatabase.GetString("UiTable", multiplayerFailure.LocalizationKey, multiplayerFailure.UserMessage);
				if (!(_lobbyVersionAlertManager == instance2) || !(_lobbyVersionAlertMessage == text) || !instance2.IsAlertShowing)
				{
					ClearLobbyVersionAlert();
					_lobbyVersionAlertManager = instance2;
					_lobbyVersionAlertMessage = text;
					instance2.ShowAlert(text, -1f, AlertPriority.Critical, autoClose: false);
				}
			}
		}

		private void ClearLobbyVersionAlert()
		{
			if (_lobbyVersionAlertManager != null && !string.IsNullOrEmpty(_lobbyVersionAlertMessage))
			{
				_lobbyVersionAlertManager.TryHideAlert(string.Empty, _lobbyVersionAlertMessage);
			}
			_lobbyVersionAlertManager = null;
			_lobbyVersionAlertMessage = string.Empty;
		}

		public void RefreshCurrentLobbyMenuUi()
		{
			TryPresentPendingFailure();
			if (!(_steam == null) && (ulong)_steam.CurrentLobby.Id != 0L)
			{
				RefreshMainMenuLobbyUi(_steam.CurrentLobby);
			}
		}

		private void RefreshMainMenuLobbyUi(Lobby lobby)
		{
			RefreshLobbyVersionAlert();
			if (!IsMainMenuStateActive || (ulong)lobby.Id == 0L)
			{
				return;
			}
			MainMenuManager instance = Singleton<MainMenuManager>.Instance;
			if (!(instance == null))
			{
				if (instance.LobbyMembersUI != null)
				{
					instance.LobbyMembersUI.RebuildFromLobbyAsync(lobby).FireAndForget("[Coordinator]");
				}
				if (_steam != null && _steam.TryGetLobbyMenuData(lobby, out var _))
				{
					instance.SetCharModelByCount(lobby.MemberCount);
				}
			}
		}

		private bool IsSameLobby(Lobby lobby)
		{
			if ((ulong)lobby.Id != 0L)
			{
				return (ulong)lobby.Id == _joiningLobbyId;
			}
			return false;
		}

		private void OnServerStarted()
		{
			UtilityMethods.Log("[Coordinator] OnServerStarted -> Switch GameState");
			_hostStartInProgress = false;
			StopHostStartTimeout();
			_netState = NetworkConnectionState.Connected;
			SetSessionPhase(MultiplayerSessionPhase.HostStarting);
			ClearSpawnSlotAssignments();
			StopHeartbeat();
			if (!IsSingleplayerSession)
			{
				_heartbeatCoroutine = StartCoroutine(CoPublishHeartbeat());
			}
			Singleton<StateManager>.Instance.SwitchState<GameState>();
		}

		private void OnTransportFailure()
		{
			if (_intentionalNetworkShutdown || (Singleton<GameSessionLifecycle>.Instance != null && Singleton<GameSessionLifecycle>.Instance.ReturnToMenuInProgress))
			{
				return;
			}
			if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
			{
				ReturnToMainMenuAfterFailure(MultiplayerFailure.Create(MultiplayerFailureCode.TransportFailure, MultiplayerFailureStage.Session, "Host transport failed."), SessionPhase != MultiplayerSessionPhase.Running);
				return;
			}
			MultiplayerFailure failure = MultiplayerFailure.Create(MultiplayerFailureCode.TransportFailure, MultiplayerFailureStage.ClientConnect, "Client transport failed.");
			if (HasClientConnectionOwner && !HandleReconnectFailure(failure) && !TryBeginReconnect(failure))
			{
				ReturnToMainMenuAfterFailure(failure);
			}
		}

		private void OnServerStopped(bool wasHost)
		{
			if (!_intentionalNetworkShutdown && wasHost && (!(Singleton<GameSessionLifecycle>.Instance != null) || !Singleton<GameSessionLifecycle>.Instance.ReturnToMenuInProgress))
			{
				ReturnToMainMenuAfterFailure(MultiplayerFailure.Create(MultiplayerFailureCode.HostUnavailable, MultiplayerFailureStage.Session, "The local host server stopped unexpectedly."), SessionPhase != MultiplayerSessionPhase.Running);
			}
		}

		private void OnClientStopped(bool wasHost)
		{
			if (!(_intentionalNetworkShutdown | wasHost) && HasClientConnectionOwner && (!(Singleton<GameSessionLifecycle>.Instance != null) || !Singleton<GameSessionLifecycle>.Instance.ReturnToMenuInProgress))
			{
				MultiplayerFailure failure = FailureFromDisconnectReason((NetworkManager.Singleton != null) ? NetworkManager.Singleton.DisconnectReason : string.Empty);
				if (!HandleReconnectFailure(failure) && !TryBeginReconnect(failure))
				{
					ReturnToMainMenuAfterFailure(failure);
				}
			}
		}

		private void OnClientConnected(ulong clientId)
		{
			if (_intentionalNetworkShutdown || (Singleton<GameSessionLifecycle>.Instance != null && Singleton<GameSessionLifecycle>.Instance.ReturnToMenuInProgress) || (_reconnectRecovery != null && (_reconnectRecovery.StoppingNetwork || _reconnectRecovery.AttemptInterrupted)))
			{
				return;
			}
			UtilityMethods.Log($"[Coordinator] OnClientConnected clientId={clientId}");
			if (_steamIdByClientId.TryGetValue(clientId, out var value))
			{
				_reservedSteamIds.Release(value, clientId);
				_activeSteamIds.Add(value);
				_serverSceneReadyDeadlines.Connected(clientId, Time.realtimeSinceStartupAsDouble);
			}
			if (!NetworkManager.Singleton.IsHost)
			{
				_netState = NetworkConnectionState.Connected;
			}
			if ((bool)NetworkManager.Singleton && clientId == NetworkManager.Singleton.LocalClientId)
			{
				_clientJoinInProgress = false;
				StopClientConnectionTimeout();
				if (!NetworkManager.Singleton.IsHost)
				{
					SetSessionPhase(MultiplayerSessionPhase.SceneSync);
				}
			}
		}

		public void SpawnPlayerForClient(ulong clientId)
		{
			if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer)
			{
				return;
			}
			PlayersManager instance = Singleton<PlayersManager>.Instance;
			if (instance == null)
			{
				HandlePlayerSpawnFailure(clientId, MultiplayerFailureCode.BootstrapFailed, "PlayersManager was unavailable while spawning the network player.");
			}
			else if (_playerPrefab == null)
			{
				HandlePlayerSpawnFailure(clientId, MultiplayerFailureCode.BootstrapFailed, "The network player prefab was not assigned.");
			}
			else
			{
				if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var value) && value.PlayerObject != null)
				{
					return;
				}
				if (!TryGetOrAssignSpawnSlot(clientId, out var slotIndex))
				{
					UtilityMethods.Log($"<b><color=#FFB020>[Spawn]</color></b> Could not spawn player. No free spawn slot for clientId={clientId}.", LogType.Warning);
					HandlePlayerSpawnFailure(clientId, MultiplayerFailureCode.LobbyFull, "No free player spawn slot was available.");
					return;
				}
				Transform playerSpawnPointForSlot = instance.GetPlayerSpawnPointForSlot(slotIndex);
				Vector3 position;
				Quaternion rotation;
				if (playerSpawnPointForSlot != null)
				{
					position = playerSpawnPointForSlot.position;
					rotation = playerSpawnPointForSlot.rotation;
				}
				else
				{
					position = instance.GetFallbackSpawnPosition(slotIndex);
					rotation = instance.GetFallbackSpawnRotation();
					UtilityMethods.Log($"<b><color=#FFB020>[Spawn]</color></b> Spawn slot {slotIndex} has no assigned point. Using fallback position.", LogType.Warning);
				}
				try
				{
					NetworkManager.Singleton.SpawnManager.InstantiateAndSpawn(_playerPrefab, clientId, destroyWithScene: true, isPlayerObject: true, forceOverride: false, position, rotation);
				}
				catch (Exception ex)
				{
					ReleaseSpawnSlot(clientId);
					HandlePlayerSpawnFailure(clientId, MultiplayerFailureCode.BootstrapFailed, "Network player spawn threw an exception: " + ex.Message);
					return;
				}
				MultiplayerDiagnostics.Record("player_spawn_call_completed", $"slot={slotIndex}", clientId);
				UtilityMethods.Log($"<b><color=#8FD14F>[Spawn]</color></b> Spawned player for clientId={clientId}, slot={slotIndex}.");
			}
		}

		private void HandlePlayerSpawnFailure(ulong clientId, MultiplayerFailureCode failureCode, string debugDetail)
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if (!(singleton == null))
			{
				MultiplayerFailure failure = MultiplayerFailure.Create(failureCode, MultiplayerFailureStage.Bootstrap, debugDetail);
				UtilityMethods.Log("<b><color=#FF4040>[Spawn]</color></b> Network player spawn failed." + $"\n   ClientId: {clientId}" + $"\n   Code: {failure.Code}" + "\n   Detail: " + failure.DebugDetail, LogType.Error);
				if (clientId == singleton.LocalClientId)
				{
					ReturnToMainMenuAfterFailure(failure, SessionPhase != MultiplayerSessionPhase.Running);
				}
				else if (singleton.ConnectedClients.ContainsKey(clientId))
				{
					FacepunchTransport.DisconnectClientWithReason(singleton, clientId, $"PPG:{failure.Code}");
				}
			}
		}

		private bool TryGetOrAssignSpawnSlot(ulong clientId, out int slotIndex)
		{
			if (_spawnSlotByClientId.TryGetValue(clientId, out slotIndex))
			{
				return true;
			}
			for (int i = ((clientId != 0L) ? 1 : 0); i < 5; i++)
			{
				if (!_occupiedSpawnSlots.Contains(i))
				{
					slotIndex = i;
					_spawnSlotByClientId[clientId] = slotIndex;
					_occupiedSpawnSlots.Add(slotIndex);
					return true;
				}
			}
			slotIndex = -1;
			return false;
		}

		private void ReleaseSpawnSlot(ulong clientId)
		{
			if (_spawnSlotByClientId.TryGetValue(clientId, out var value))
			{
				_spawnSlotByClientId.Remove(clientId);
				_occupiedSpawnSlots.Remove(value);
			}
		}

		private void ClearSpawnSlotAssignments()
		{
			_spawnSlotByClientId.Clear();
			_occupiedSpawnSlots.Clear();
		}

		private void OnClientDisconnected(ulong clientId)
		{
			if (Singleton<GameSessionLifecycle>.Instance != null && Singleton<GameSessionLifecycle>.Instance.ReturnToMenuInProgress)
			{
				UtilityMethods.Log("<b><color=#FFB020>[Multiplayer]</color></b> OnClientDisconnected ignored because return to menu is already in progress.");
				return;
			}
			UtilityMethods.Log(string.Format("[Coordinator] OnClientDisconnected clientId={0}, state={1}", clientId, "GameState"));
			ReleaseSpawnSlot(clientId);
			_serverSceneReadyDeadlines.Remove(clientId);
			if (_steamIdByClientId.TryGetValue(clientId, out var value))
			{
				_activeSteamIds.Remove(value);
				_steamIdByClientId.Remove(clientId);
				if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && SessionPhase == MultiplayerSessionPhase.Running && _sessionEpoch != 0L && _steam != null && (ulong)_steam.CurrentLobby.Id != 0L)
				{
					AdmissionReservations.Lease lease = ReserveAdmissionSlot(value, clientId, _reconnectGraceSeconds, "reconnect_grace");
					StartCoroutine(CoReleaseReservationAfter(lease));
				}
				else
				{
					_reservedSteamIds.Release(value, clientId);
				}
			}
			if (!NetworkManager.Singleton || clientId != NetworkManager.Singleton.LocalClientId || _intentionalNetworkShutdown || !HasClientConnectionOwner)
			{
				return;
			}
			MultiplayerFailure failure = FailureFromDisconnectReason(NetworkManager.Singleton.DisconnectReason);
			if (!HandleReconnectFailure(failure) && !TryBeginReconnect(failure))
			{
				_clientJoinInProgress = false;
				_clientWaitingForHost = false;
				_joiningLobbyId = 0uL;
				_netState = NetworkConnectionState.Disconnected;
				StopHostReadyTimeout();
				StopClientConnectionTimeout();
				if (Singleton<GameSessionLifecycle>.Instance != null)
				{
					string disconnectReason = NetworkManager.Singleton.DisconnectReason;
					Singleton<GameSessionLifecycle>.Instance.ReturnToMainMenu(ReturnToMainMenuRequest.LocalClientDisconnected(FailureFromDisconnectReason(disconnectReason)));
				}
				else
				{
					UtilityMethods.Log("<b><color=#FF4040>[Multiplayer]</color></b> Local disconnect return failed. GameSessionLifecycle is null.", LogType.Error);
					ReturnToMainMenuAfterFailure(FailureFromDisconnectReason(NetworkManager.Singleton.DisconnectReason));
				}
			}
		}

		private void OnConnectionApproval(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
		{
			if (Netcode.Transports.Local.OfflineSingleplayerMode.Enabled && IsSingleplayerSession)
			{
				bool local = req.ClientNetworkId == NetworkManager.ServerClientId;
				res.Approved = local; res.CreatePlayerObject = false; res.Pending = false;
				res.Reason = local ? string.Empty : "This session only accepts its local host player.";
				if (local) _steamIdByClientId[req.ClientNetworkId] = Netcode.Transports.Local.OfflineSingleplayerMode.LocalUserId;
				return;
			}
			if (IsSingleplayerSession && req.ClientNetworkId != 0L)
			{
				res.Reason = $"PPG:{MultiplayerFailureCode.NotAuthorized}";
				res.Approved = false;
				res.CreatePlayerObject = false;
				res.Pending = false;
				return;
			}
			ConnectionApprovalPolicy connectionApprovalPolicy = new ConnectionApprovalPolicy();
			_ngo.TryGetAuthenticatedSteamId(req.ClientNetworkId, out var steamId);
			int num = CountOccupiedAdmissionSlots();
			HashSet<ulong> hashSet = ((_steamBanListData?.BanList != null) ? new HashSet<ulong>(_steamBanListData.BanList) : new HashSet<ulong>());
			if (RoomService != null)
			{
				hashSet.UnionWith(RoomService.BannedIds);
			}
			ConnectionApprovalContext connectionApprovalContext = new ConnectionApprovalContext
			{
				Request = req,
				ConnectedClientCount = NetworkManager.Singleton.ConnectedClients.Count,
				ReservedSlotCount = _reservedSteamIds.Count,
				OccupiedPlatformSlotCount = num,
				MaxMembers = _maxMembers,
				IsFriendsOnly = _steam.IsFriendsOnly,
				HostPlatformId = SteamClient.SteamId.Value,
				AuthenticatedPlatformId = steamId,
				ActivePlatformIds = _activeSteamIds,
				ReservedPlatformIds = _reservedSteamIds,
				RequireLobbyMembership = true,
				IsCurrentLobbyMember = IsCurrentLobbyMember(steamId),
				RequireRoomAdmission = (RoomService != null),
				IsRoomAdmitted = (RoomService != null && RoomService.IsMemberAdmitted(steamId)),
				IsHostFriend = (steamId != 0L && new Friend(steamId).IsFriend),
				BannedIds = hashSet,
				ServerVersion = Singleton<GameController>.Instance.Version,
				ServerReleaseVersion = ReleaseVersion.Value,
				ServerSessionEpoch = _sessionEpoch
			};
			ApprovalResult approvalResult = connectionApprovalPolicy.Evaluate(connectionApprovalContext, out var parsed);
			if (approvalResult.Approved && steamId != connectionApprovalContext.HostPlatformId && !_serverSceneReadyDeadlines.TryApprove(req.ClientNetworkId, steamId, Time.realtimeSinceStartupAsDouble, _ngo.ApprovalTimeoutSeconds + _ngo.SceneTimeoutSeconds))
			{
				approvalResult = ApprovalResult.Deny("Steam identity already has an in-flight scene join", MultiplayerFailureCode.IdentityMismatch);
			}
			if (!approvalResult.Approved)
			{
				res.Reason = $"PPG:{approvalResult.FailureCode}";
				res.Approved = false;
				res.Pending = false;
				res.CreatePlayerObject = false;
				UtilityMethods.Log("<b><color=#FF4040>[Connection Approval]</color></b> Connection rejected." + $"\n   ClientId: {req.ClientNetworkId}" + "\n   Reason: " + approvalResult.Reason + $"\n   ClientGameVersion: {parsed.ClientVersion}" + "\n   ClientReleaseVersion: " + parsed.ReleaseVersion + $"\n   ServerGameVersion: {connectionApprovalContext.ServerVersion}" + "\n   ServerReleaseVersion: " + connectionApprovalContext.ServerReleaseVersion + $"\n   AuthenticatedSteamId: {steamId}" + $"\n   NgoConnectedCount: {connectionApprovalContext.ConnectedClientCount}" + $"\n   ActiveSteamCount: {_activeSteamIds.Count}" + $"\n   ReservedSteamCount: {_reservedSteamIds.Count}" + $"\n   OccupiedPlatformSlots: {num}/{_maxMembers}" + $"\n   HasOwnReservation: {_reservedSteamIds.Contains(steamId)}", LogType.Warning);
			}
			else
			{
				res.Reason = string.Empty;
				res.Approved = true;
				res.Pending = false;
				res.CreatePlayerObject = false;
				if (steamId != SteamClient.SteamId.Value)
				{
					ReserveAdmissionSlot(steamId, req.ClientNetworkId, _ngo.ApprovalTimeoutSeconds + _ngo.SceneTimeoutSeconds, "approval");
					_steamIdByClientId[req.ClientNetworkId] = steamId;
				}
				UtilityMethods.Log("<b><color=#8FD14F>[Connection Approval]</color></b> Connection approved." + $"\n   ClientId: {req.ClientNetworkId}" + $"\n   SteamId: {parsed.PlatformId}" + $"\n   GameVersion: {parsed.ClientVersion}" + "\n   ReleaseVersion: " + parsed.ReleaseVersion + $"\n   ProtocolVersion: {parsed.ProtocolVersion}" + $"\n   NgoConnectedCount: {connectionApprovalContext.ConnectedClientCount}" + $"\n   OccupiedPlatformSlotsBeforeApproval: {num}/{_maxMembers}");
			}
		}

		private int CountOccupiedAdmissionSlots()
		{
			return AdmissionSlotCounter.CountOccupiedSlots(SteamClient.IsValid ? SteamClient.SteamId.Value : 0, _activeSteamIds, _reservedSteamIds);
		}

		private bool IsCurrentLobbyMember(ulong steamId)
		{
			if (steamId == 0L || _steam == null || (ulong)_steam.CurrentLobby.Id == 0L)
			{
				return false;
			}
			foreach (Friend member in _steam.CurrentLobby.Members)
			{
				if (member.Id.Value == steamId)
				{
					return true;
				}
			}
			return false;
		}

		private AdmissionReservations.Lease ReserveAdmissionSlot(ulong steamId, ulong clientId, double seconds, string reason)
		{
			AdmissionReservations.Lease result = _reservedSteamIds.Reserve(steamId, clientId, _sessionEpoch, _steam.CurrentLobby.Id.Value, Time.realtimeSinceStartupAsDouble + Math.Max(1.0, seconds));
			MultiplayerDiagnostics.Record("reservation_created", FormattableString.Invariant($"reason={reason} generation={result.Generation} expiresAt={result.ExpiresAt:F3}"), clientId);
			return result;
		}

		private IEnumerator CoReleaseReservationAfter(AdmissionReservations.Lease lease)
		{
			yield return new WaitForSecondsRealtime((float)Math.Max(0.0, lease.ExpiresAt - Time.realtimeSinceStartupAsDouble));
			ulong lobbyId = ((_steam != null) ? _steam.CurrentLobby.Id.Value : 0);
			if (_reservedSteamIds.TryExpire(lease, Time.realtimeSinceStartupAsDouble, _sessionEpoch, lobbyId, _activeSteamIds.Contains(lease.SteamId), _serverSceneReadyDeadlines.ContainsSteamId(lease.SteamId)))
			{
				MultiplayerDiagnostics.Record("reservation_expired", $"generation={lease.Generation}", lease.ClientId);
			}
		}

		public void NotifyClientSceneReady(ulong clientId)
		{
			_serverSceneReadyDeadlines.Ready(clientId);
		}

		private void CheckServerSceneReadyDeadlines()
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if (singleton == null || !singleton.IsServer || singleton.ShutdownInProgress)
			{
				return;
			}
			double realtimeSinceStartupAsDouble = Time.realtimeSinceStartupAsDouble;
			ulong clientId;
			while (_serverSceneReadyDeadlines.TryTakeExpired(realtimeSinceStartupAsDouble, out clientId))
			{
				MultiplayerDiagnostics.Record("server_scene_ready_timeout", "Approved client did not finish scene/ready preparation.", clientId, critical: true);
				if (_steamIdByClientId.TryGetValue(clientId, out var value) && !_activeSteamIds.Contains(value))
				{
					_reservedSteamIds.Release(value, clientId);
				}
				if (singleton.ConnectedClients.ContainsKey(clientId))
				{
					FacepunchTransport.DisconnectClientWithReason(singleton, clientId, $"PPG:{MultiplayerFailureCode.ConnectionTimedOut}");
				}
			}
		}

		private byte[] BuildConnectionPayload()
		{
			int clientVersion = ((Singleton<GameController>.Instance != null) ? Singleton<GameController>.Instance.Version : 0);
			ulong platformId = Netcode.Transports.Local.OfflineSingleplayerMode.Enabled ? Netcode.Transports.Local.OfflineSingleplayerMode.LocalUserId : (SteamClient.IsValid ? SteamClient.SteamId.Value : 0);
			return MultiplayerConnectionPayload.Create(clientVersion, platformId, ReleaseVersion.Value, _sessionEpoch);
		}

		public bool TrySelectSingleplayer()
		{
			if (Netcode.Transports.Local.OfflineSingleplayerMode.Enabled && IsMainMenuStateActive && IsSessionShutdownComplete && (!Singleton<GameSessionLifecycle>.TryGetInstance(out var offlineLifecycle) || !offlineLifecycle.ReturnToMenuInProgress))
			{
				_sessionReturnPrepared = false;
				_intentionalNetworkShutdown = false;
			}
			if (LobbyBlocksSingleplayer)
			{
				return false;
			}
			if (_sessionReturnPrepared || _intentionalNetworkShutdown || IsHostStarted || _hostStartInProgress || _clientJoinInProgress || _netState != NetworkConnectionState.Disconnected)
			{
				return false;
			}
			RoomService?.CancelPendingJoin();
			_lobbyOperations.CancelActive();
			_clientCancellationVersion++;
			_hasPendingLobbyJoin = false;
			_pendingLobbyJoin = default;
			ResetConnectionRuntimeFlags();
			IsSingleplayerSession = true;
			_steam?.LeaveCurrentLobby();
			_currentLobby = default;
			_cachedIsInForeignLobby = false;
			ClearLobbyVersionAlert();
			if (Singleton<GlobalChatManager>.TryGetInstance(out var instance))
			{
				instance.ClearSession();
			}
			ForeignLobbyStateChanged?.Invoke(obj: false);
			return true;
		}

		public void SelectMultiplayer()
		{
			if (!IsHostStarted && _netState == NetworkConnectionState.Disconnected)
			{
				IsSingleplayerSession = false;
			}
		}

		private void PrepareSingleplayerLaunch(ulong hostSteamId)
		{
			IsHostStarted = true;
			IsLateJoiner = false;
			_sessionEpoch = SessionEpochGenerator.Next(hostSteamId);
			_networkHostSteamId = hostSteamId;
			_launchCohortSteamIds.Clear();
			_launchCohortSteamIds.Add(_networkHostSteamId);
			_launchCohortFrozen = true;
			SetTargetPlayerCount(1);
			SetSessionPhase(MultiplayerSessionPhase.HostStarting);
		}

		public bool TryBeginHostLaunch()
		{
			if (IsSingleplayerSession)
			{
				if (LobbyBlocksSingleplayer)
				{
					return false;
				}
				if (!CanStartGameFromSaveSlot)
				{
					return false;
				}
				PrepareSingleplayerLaunch(Netcode.Transports.Local.OfflineSingleplayerMode.Enabled ? Netcode.Transports.Local.OfflineSingleplayerMode.LocalUserId : SteamClient.SteamId.Value);
				return true;
			}
			if (RoomService != null && !RoomService.CanLaunch)
			{
				if (Singleton<NotifyManager>.TryGetInstance(out var instance))
				{
					instance.ShowToast(LobbyRoomLabel.Text("launch_ready_required", "Every player must be ready before the host starts.", "Ev sahibi oyunu başlatmadan önce herkes hazır olmalı."), NotificationState.Error);
				}
				return false;
			}
			if (!CanStartGameFromSaveSlot)
			{
				UtilityMethods.Log("<b><color=#FFB020>[Multiplayer]</color></b> HostStart blocked" + $"\n   SteamConnectionAvailable: {IsSteamConnectionAvailable}" + $"\n   IsInForeignLobby: {IsInForeignLobby}" + $"\n   NetState: {_netState}" + $"\n   ClientJoinInProgress: {_clientJoinInProgress}" + $"\n   HostStartInProgress: {_hostStartInProgress}", LogType.Warning);
				if (!IsSteamConnectionAvailable)
				{
					ShowFailure(MultiplayerFailure.Create(MultiplayerFailureCode.SteamUnavailable, MultiplayerFailureStage.LaunchPreparation, "Host launch was requested while Steam was unavailable."));
				}
				else
				{
					string message = LocalizationDatabase.GetString("UiTable", "ui.save_slot.leave_lobby_first", "You cannot start a save while you are in another player's lobby.");
					if (Singleton<NotifyManager>.TryGetInstance(out var instance2))
					{
						instance2.ShowToast(message, NotificationState.Error);
					}
				}
				return false;
			}
			if (!TryGetActiveLobby(out var lobby) || (ulong)lobby.Owner.Id != (ulong)SteamClient.SteamId)
			{
				ShowFailure(MultiplayerFailure.Create(MultiplayerFailureCode.LobbyCreateFailed, MultiplayerFailureStage.LaunchPreparation, "Host does not own an active Steam lobby."));
				return false;
			}
			if (HasLobbyVersionMismatch(lobby))
			{
				RefreshLobbyVersionAlert();
				ShowFailure(MultiplayerFailure.LobbyVersionMismatch(MultiplayerFailureStage.LaunchPreparation));
				return false;
			}
			ClearLobbyVersionAlert();
			IsHostStarted = true;
			IsLateJoiner = false;
			_sessionEpoch = SessionEpochGenerator.Next(SteamClient.SteamId.Value);
			_networkHostSteamId = SteamClient.SteamId.Value;
			_launchPreparation.Begin(Time.realtimeSinceStartupAsDouble, LaunchPreparationTimeoutSeconds, from member in lobby.Members
				select member.Id.Value into id
				where RoomService == null || RoomService.IsMemberAdmitted(id)
				select id, _networkHostSteamId);
			_launchCohortSteamIds.Clear();
			_launchCohortFrozen = false;
			SetSessionPhase(MultiplayerSessionPhase.PreparingLaunch);
			LobbySessionMetadata.Publish(lobby, "preparing", _sessionEpoch, _networkHostSteamId, joinable: false, _launchReadySteamIds, 0L);
			StopHeartbeat();
			_heartbeatCoroutine = StartCoroutine(CoPublishHeartbeat());
			MultiplayerDiagnostics.Record("launch_preparation_begin", FormattableString.Invariant($"expected={_launchPreparation.ExpectedCount} timeoutSeconds={LaunchPreparationTimeoutSeconds}"), ulong.MaxValue);
			LobbyLaunchMessage message2 = new LobbyLaunchMessage
			{
				Kind = LobbyLaunchMessageKind.Prepare,
				Epoch = _sessionEpoch,
				HostSteamId = _networkHostSteamId,
				GameVersion = ((Singleton<GameController>.Instance != null) ? Singleton<GameController>.Instance.Version : 0),
				ReleaseVersion = ReleaseVersion.Value,
				DeadlineUnixSeconds = LobbySessionMetadata.UtcNowUnixSeconds + (long)Math.Ceiling(LaunchPreparationTimeoutSeconds)
			};
			if (!_steam.SendLobbyInternalMessage(LobbyLaunchProtocol.Serialize(message2)))
			{
				UtilityMethods.Log("<b><color=#FFB020>[Launch]</color></b> Prepare message could not be sent; host will continue alone.", LogType.Warning);
				ShowFailure(MultiplayerFailure.Create(MultiplayerFailureCode.LaunchPreparationFailed, MultiplayerFailureStage.LaunchPreparation, "PrepareLaunch could not be sent; the host will continue with the local cohort."));
			}
			if (_launchPreparationCoroutine != null)
			{
				StopCoroutine(_launchPreparationCoroutine);
			}
			_launchPreparationCoroutine = StartCoroutine(CoCompleteLaunchPreparation());
			return true;
		}

		public void ContinuePendingLaunchFromLoadingState()
		{
			if (_reconnectRecovery != null || _sessionReturnPrepared || _intentionalNetworkShutdown)
			{
				return;
			}
			if (IsHostStarted || HasLocalHostLaunchAuthority())
			{
				IsHostStarted = true;
				IsLateJoiner = false;
				if (_launchCohortFrozen && !_hostStartInProgress && _netState == NetworkConnectionState.Disconnected)
				{
					StartGame();
				}
				else
				{
					UpdateLaunchPreparationLoadingStatus();
				}
			}
			else if (IsLateJoiner)
			{
				StartClient();
			}
		}

		private bool HasLocalHostLaunchAuthority()
		{
			if (!SteamClient.IsValid || _sessionEpoch == 0L || !TryGetActiveLobby(out var lobby) || (ulong)lobby.Owner.Id != (ulong)SteamClient.SteamId || !LobbySessionMetadata.TryRead(lobby, out var descriptor))
			{
				return false;
			}
			if (descriptor.Epoch == _sessionEpoch && descriptor.NetworkHostSteamId == SteamClient.SteamId.Value)
			{
				string state = descriptor.State;
				return state == "preparing" || state == "connecting";
			}
			return false;
		}

		private IEnumerator CoCompleteLaunchPreparation()
		{
			ulong epoch = _sessionEpoch;
			ulong lobbyId = ((_steam != null) ? _steam.CurrentLobby.Id.Value : 0);
			yield return null;
			WaitForSecondsRealtime poll = new WaitForSecondsRealtime(0.1f);
			while (true)
			{
				if (_sessionEpoch != epoch || _steam == null || _steam.CurrentLobby.Id.Value != lobbyId || !IsCurrentLobbyOwner || SessionPhase != MultiplayerSessionPhase.PreparingLaunch)
				{
					yield break;
				}
				if (_launchPreparation.ShouldComplete(Time.realtimeSinceStartupAsDouble))
				{
					break;
				}
				UpdateLaunchPreparationLoadingStatus();
				yield return poll;
			}
			_launchPreparationCoroutine = null;
			MultiplayerDiagnostics.Record("launch_preparation_complete", $"allReady={_launchPreparation.AllReady} ready={_launchReadySteamIds.Count} expected={_launchPreparation.ExpectedCount}", ulong.MaxValue);
			FreezeLaunchCohort();
			if (IsLoadingStateActive)
			{
				StartGame();
			}
		}

		private void FreezeLaunchCohort()
		{
			if (_launchCohortFrozen || !TryGetActiveLobby(out var lobby))
			{
				return;
			}
			HashSet<ulong> hashSet = new HashSet<ulong>(lobby.Members.Select((Friend member) => member.Id.Value));
			_launchCohortSteamIds.Clear();
			foreach (ulong launchReadySteamId in _launchReadySteamIds)
			{
				if (hashSet.Contains(launchReadySteamId) && (RoomService == null || RoomService.IsMemberAdmitted(launchReadySteamId)))
				{
					_launchCohortSteamIds.Add(launchReadySteamId);
				}
			}
			_launchCohortSteamIds.Add(SteamClient.SteamId.Value);
			_launchCohortFrozen = true;
			SetTargetPlayerCount(_launchCohortSteamIds.Count);
			SetSessionPhase(MultiplayerSessionPhase.HostStarting);
			LobbySessionMetadata.Publish(lobby, "connecting", _sessionEpoch, _networkHostSteamId, joinable: false, _launchCohortSteamIds, 0L);
			UtilityMethods.Log("<b><color=#8FD14F>[Launch]</color></b> Launch cohort frozen." + $"\n   Epoch: {_sessionEpoch}" + $"\n   Ready: {_launchReadySteamIds.Count}" + $"\n   Cohort: {_launchCohortSteamIds.Count}");
		}

		private void UpdateLaunchPreparationLoadingStatus()
		{
			int num = Mathf.Max(1, _launchPreparation.ExpectedCount);
			LoadingScreen.UpdateLocalizedText("loading.title.hosting_session", "Hosting Session", "loading.status.preparing_players", $"Preparing players: {_launchReadySteamIds.Count}/{num}");
		}

		private bool TryGetActiveLobby(out Lobby lobby)
		{
			lobby = default;
			if (_steam != null && (ulong)_steam.CurrentLobby.Id != 0L)
			{
				lobby = _steam.CurrentLobby;
				return true;
			}
			if ((ulong)_currentLobby.Id != 0L)
			{
				lobby = _currentLobby;
				return true;
			}
			return false;
		}

		private void RefreshForeignLobbyState()
		{
			RefreshLobbyVersionAlert();
			bool isInForeignLobby = IsInForeignLobby;
			if (_cachedIsInForeignLobby != isInForeignLobby)
			{
				_cachedIsInForeignLobby = isInForeignLobby;
				ForeignLobbyStateChanged?.Invoke(isInForeignLobby);
				UtilityMethods.Log("<b><color=#8FD14F>[Multiplayer]</color></b> Foreign lobby state changed" + $"\n   IsInForeignLobby: {isInForeignLobby}");
			}
		}

		public void LeaveForeignLobbyFromMainMenu()
		{
			if (_sessionReturnPrepared || _intentionalNetworkShutdown)
			{
				return;
			}
			if (RequiresReturnBeforeLobbyJoin())
			{
				RequestSessionReturn(ReturnToMainMenuRequest.UserRequested());
			}
			else if (IsInForeignLobby)
			{
				UtilityMethods.Log("<b><color=#FFB020>[Multiplayer]</color></b> Leaving foreign lobby from main menu.");
				StopHostStartRoutine();
				ResetConnectionRuntimeFlags();
				if ((bool)NetworkManager.Singleton && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsHost))
				{
					_ngo.Shutdown();
				}
				if (Singleton<GlobalChatManager>.Instance != null)
				{
					Singleton<GlobalChatManager>.Instance.ClearSession();
				}
				if (_steam != null && (ulong)_steam.CurrentLobby.Id != 0L)
				{
					_steam.LeaveCurrentLobby();
				}
				_currentLobby = default;
				RefreshForeignLobbyState();
				CreateLobby();
				Singleton<NotifyManager>.Instance.ShowToast(LobbyRoomLabel.Text("result.left_lobby", "Left the lobby.", "Lobiden ayrıldın."));
			}
		}

		public void BeginSessionReturn(ReturnToMainMenuRequest request)
		{
			if (request.Reason != ReturnToMainMenuReason.JoinRequested)
			{
				RoomService?.CancelPendingJoin();
			}
			if (!_sessionReturnPrepared)
			{
				_sessionReturnPrepared = true;
				_clientCancellationVersion++;
				_intentionalNetworkShutdown = true;
				if (Singleton<MainMenuManager>.TryGetInstance(out var instance))
				{
					instance.CancelBoatDeparture();
				}
				try
				{
					PublishHostSessionClosing();
				}
				catch (Exception ex)
				{
					MultiplayerDiagnostics.Record("host_session_closing_failed", ex.Message, ulong.MaxValue, critical: true);
				}
				StopHostStartRoutine();
				StopConnectionWork();
				_clientWaitingForHost = (_clientJoinInProgress = false);
				_lobbyOperations.CancelActive();
				if (request.Reason != ReturnToMainMenuReason.JoinRequested)
				{
					_hasPendingLobbyJoin = false;
					_pendingLobbyJoin = default;
				}
				SetSessionPhase(MultiplayerSessionPhase.ReturningToMenu);
				CancelClientWorldWork();
				if (request.ShutdownNetwork && _ngo != null && !_ngo.IsStopped && (!(NetworkManager.Singleton != null) || !NetworkManager.Singleton.ShutdownInProgress))
				{
					_ngo.Shutdown(request.KickRemotesIfHost);
				}
			}
		}

		public void CompleteSessionReturn()
		{
			if (IsSessionShutdownComplete)
			{
				_sessionReturnPrepared = false;
				EnsureMenuLobby();
			}
		}

		private static void CancelClientWorldWork()
		{
			if (!(NetworkManager.Singleton != null) || !NetworkManager.Singleton.IsServer)
			{
				RpcRelay.ActiveInstance?.CancelClientSessionWork();
				if (Singleton<JoinBootstrapCoordinator>.TryGetInstance(out var instance))
				{
					instance.ResetState();
				}
			}
		}

		public void ResetSessionForMainMenu(ReturnToMainMenuRequest request)
		{
			BeginSessionReturn(request);
			MultiplayerDiagnostics.Record("session_reset", $"leaveLobby={request.LeaveSteamLobby} shutdown={request.ShutdownNetwork}", ulong.MaxValue);
			ClearLobbyVersionAlert();
			SetSessionPhase(MultiplayerSessionPhase.ReturningToMenu);
			_intentionalNetworkShutdown = true;
			StopHostStartRoutine();
			ResetConnectionRuntimeFlags();
			if (request.ShutdownNetwork && _ngo != null && !_ngo.IsStopped && (!(NetworkManager.Singleton != null) || !NetworkManager.Singleton.ShutdownInProgress))
			{
				_ngo.Shutdown(request.KickRemotesIfHost);
			}
			if (Singleton<PlayersManager>.Instance != null)
			{
				Singleton<PlayersManager>.Instance.ClearSessionStateForMainMenu();
			}
			if (request.ClearChat && Singleton<GlobalChatManager>.Instance != null)
			{
				Singleton<GlobalChatManager>.Instance.ClearSession();
			}
			if (request.LeaveSteamLobby && _steam != null && (ulong)_steam.CurrentLobby.Id != 0L)
			{
				_steam.LeaveCurrentLobby();
			}
			_currentLobby = default;
			_cachedIsInForeignLobby = false;
			ForeignLobbyStateChanged?.Invoke(obj: false);
			UtilityMethods.Log("<b><color=#FFB020>[Multiplayer]</color></b> Session reset for main menu" + $"\n   Reason: {request.Reason}" + "\n   DebugReason: " + request.DebugReason);
		}

		public void EnsureMenuLobby()
		{
			if (Netcode.Transports.Local.OfflineSingleplayerMode.Enabled) { IsSingleplayerSession = true; return; }
			if (!IsSingleplayerSession && (!Singleton<GameSessionLifecycle>.TryGetInstance(out var instance) || !instance.ReturnToMenuInProgress) && IsSessionShutdownComplete)
			{
				_sessionReturnPrepared = false;
				_intentionalNetworkShutdown = false;
				if (!_initialized)
				{
					Initialize();
				}
				if (!IsSteamConnectionAvailable)
				{
					UtilityMethods.Log("<b><color=#FFB020>[Multiplayer]</color></b> EnsureMenuLobby blocked. Steam connection is unavailable.", LogType.Warning);
				}
				else if (_hasPendingLobbyJoin)
				{
					JoinPendingLobbyIfReady();
				}
				else if ((ulong)_steam.CurrentLobby.Id != 0L)
				{
					RefreshForeignLobbyState();
				}
				else
				{
					CreateLobby();
				}
			}
		}

		private void StopHostStartRoutine()
		{
			_hostStartInProgress = false;
		}

		private void StopConnectionWork()
		{
			_reconnectRecovery?.Cancel();
			_reconnectRecovery = null;
			StopHostReadyTimeout();
			StopClientConnectionTimeout();
			StopHostStartTimeout();
			StopHeartbeat();
			StopSessionMetadataReadinessWait();
			_metadataReadinessFailedLobbyId = 0uL;
			if (_cohortConnectionDeadlineCoroutine != null)
			{
				StopCoroutine(_cohortConnectionDeadlineCoroutine);
				_cohortConnectionDeadlineCoroutine = null;
			}
			if (_launchPreparationCoroutine != null)
			{
				StopCoroutine(_launchPreparationCoroutine);
				_launchPreparationCoroutine = null;
			}
			if (_reconnectCoroutine != null)
			{
				StopCoroutine(_reconnectCoroutine);
				_reconnectCoroutine = null;
			}
		}

		private void ResetConnectionRuntimeFlags()
		{
			IsSingleplayerSession = false;
			StopConnectionWork();
			_hostStartInProgress = false;
			_clientWaitingForHost = false;
			_clientJoinInProgress = false;
			_joiningLobbyId = 0uL;
			_joiningOwnerId = default;
			_isHostStarted = false;
			_isLateJoiner = false;
			_launchCohortFrozen = false;
			_sessionEpoch = 0uL;
			_networkHostSteamId = 0uL;
			_lastHandledPrepareEpoch = 0uL;
			_incompatibleSessionEpoch = 0uL;
			_incompatibleGameVersionEpoch = 0uL;
			_launchPreparation.Clear();
			_launchCohortSteamIds.Clear();
			_reservedSteamIds.Clear();
			_activeSteamIds.Clear();
			_steamIdByClientId.Clear();
			_serverSceneReadyDeadlines.Clear();
			_initialSceneWaitDeadline = 0.0;
			SetTargetPlayerCount(1);
			ClearSpawnSlotAssignments();
			_netState = NetworkConnectionState.Disconnected;
			SetSessionPhase(MultiplayerSessionPhase.MenuWaiting);
		}

		public void NotifyHostGameSceneReady()
		{
			if (!_sessionReturnPrepared && !_intentionalNetworkShutdown && (bool)NetworkManager.Singleton && NetworkManager.Singleton.IsHost)
			{
				if (_initialSceneWaitDeadline <= 0.0)
				{
					_initialSceneWaitDeadline = Time.realtimeSinceStartupAsDouble + (double)_clientConnectTimeoutSeconds + _ngo.ApprovalTimeoutSeconds + _ngo.SceneTimeoutSeconds + 30.0;
				}
				SetSessionPhase(MultiplayerSessionPhase.HostSceneReady);
				PublishLobbySessionState("host_ingame");
				if (_cohortConnectionDeadlineCoroutine != null)
				{
					StopCoroutine(_cohortConnectionDeadlineCoroutine);
				}
				_cohortConnectionDeadlineCoroutine = StartCoroutine(CoPruneMissingCohortConnections());
			}
		}

		public void NotifyInitialBootstrapStarted()
		{
			if (!_sessionReturnPrepared && !_intentionalNetworkShutdown && (bool)NetworkManager.Singleton && NetworkManager.Singleton.IsServer)
			{
				SetSessionPhase(MultiplayerSessionPhase.InitialBootstrap);
			}
		}

		public void NotifyGameRunning()
		{
			if (!_sessionReturnPrepared && !_intentionalNetworkShutdown && (bool)NetworkManager.Singleton && NetworkManager.Singleton.IsServer)
			{
				SetSessionPhase(MultiplayerSessionPhase.Running);
				PublishLobbySessionState("ingame");
			}
		}

		public void NotifyLocalGameplayStarted()
		{
			if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
			{
				SetSessionPhase(MultiplayerSessionPhase.Running);
			}
		}

		private void SetSessionPhase(MultiplayerSessionPhase phase)
		{
			MultiplayerDiagnostics.SetContext(_currentLobby.Id, _sessionEpoch, SteamClient.IsValid ? SteamClient.SteamId.Value : 0, _networkHostSteamId, ActiveOperationId);
			if (SessionPhase != phase)
			{
				MultiplayerSessionPhase sessionPhase = SessionPhase;
				float num = Mathf.Max(0f, Time.realtimeSinceStartup - _phaseStartedRealtime);
				SessionPhase = phase;
				_phaseStartedRealtime = Time.realtimeSinceStartup;
				MultiplayerDiagnostics.Record("application_phase", FormattableString.Invariant($"previous={sessionPhase} current={phase} previousSeconds={num:F3}"), ulong.MaxValue);
				if (phase == MultiplayerSessionPhase.HostStarting || phase == MultiplayerSessionPhase.ClientConnecting)
				{
					MultiplayerDiagnostics.Record("application_timeouts", FormattableString.Invariant($"hostReady={HostPreparationTimeoutSeconds} clientConnect={_clientConnectTimeoutSeconds} launchReady={LaunchPreparationTimeoutSeconds} hostStart={_hostStartTimeoutSeconds} reconnectGrace={_reconnectGraceSeconds} heartbeatInterval={_heartbeatIntervalSeconds} heartbeatMaxAge={_heartbeatMaxAgeSeconds} metadataRefresh={120.0}"), ulong.MaxValue);
				}
				UtilityMethods.Log($"<b><color=#8FD14F>[Multiplayer Phase]</color></b> {sessionPhase} -> {SessionPhase}");
				ReportMultiplayerTelemetry("phase", default, sessionPhase, num);
			}
		}

		private void ReportMultiplayerTelemetry(string eventName, MultiplayerFailure failure = default(MultiplayerFailure), MultiplayerSessionPhase previousPhase = MultiplayerSessionPhase.MenuWaiting, float stageDurationSeconds = 0f)
		{
			if (_steam != null)
			{
				_ = _steam.CurrentLobby;
			}
			UnityEngine.Object.FindFirstObjectByType<GameStartGate>();
		}

		private void SetTargetPlayerCount(int count)
		{
			int num = Mathf.Max(1, count);
			if (_targetPlayerCount != num)
			{
				_targetPlayerCount = num;
				TargetPlayerCountChanged?.Invoke(_targetPlayerCount);
			}
		}

		public void ServerReduceInitialTarget(int count)
		{
			if (!(NetworkManager.Singleton == null) && NetworkManager.Singleton.IsServer && SessionPhase != MultiplayerSessionPhase.Running)
			{
				SetTargetPlayerCount(Mathf.Max(1, Mathf.Min(_targetPlayerCount, count)));
			}
		}

		private void PublishHostSessionClosing()
		{
			if (SteamClient.IsValid && !(_steam == null) && _sessionEpoch != 0L && _networkHostSteamId == SteamClient.SteamId.Value)
			{
				Lobby currentLobby = _steam.CurrentLobby;
				if ((ulong)currentLobby.Id != 0L && (ulong)currentLobby.Owner.Id == (ulong)SteamClient.SteamId)
				{
					bool flag = LobbySessionMetadata.Publish(currentLobby, "closing", _sessionEpoch, _networkHostSteamId, joinable: false, _launchCohortSteamIds, 0L);
					bool flag2 = _steam.SendLobbyInternalMessage(LobbyLaunchProtocol.Serialize(new LobbyLaunchMessage
					{
						Kind = LobbyLaunchMessageKind.Closed,
						Epoch = _sessionEpoch,
						HostSteamId = _networkHostSteamId,
						ReleaseVersion = ReleaseVersion.Value
					}));
					MultiplayerDiagnostics.Record("host_session_closing", $"metadata={flag} notice={flag2}", ulong.MaxValue, critical: true);
				}
			}
		}

		private void HandleHostSessionClosed(string source)
		{
			if (!_sessionReturnPrepared && !_intentionalNetworkShutdown)
			{
				MultiplayerDiagnostics.Record("host_session_closed", "source=" + source, ulong.MaxValue, critical: true);
				ReturnToMainMenuAfterFailure(MultiplayerFailure.Create(MultiplayerFailureCode.HostUnavailable, MultiplayerFailureStage.Session, "The host closed the game session."), isStartupFailure: false, LogType.Warning);
			}
		}

		private void PublishLobbySessionState(string state)
		{
			if (!SteamClient.IsValid || _steam == null)
			{
				return;
			}
			Lobby currentLobby = _steam.CurrentLobby;
			if ((ulong)currentLobby.Id != 0L && (ulong)currentLobby.Owner.Id == (ulong)SteamClient.SteamId)
			{
				bool joinable = state == "ingame";
				if (state == "waiting")
				{
					LobbySessionMetadata.ClearSession(currentLobby);
				}
				else
				{
					LobbySessionMetadata.Publish(currentLobby, state, _sessionEpoch, (_networkHostSteamId != 0L) ? _networkHostSteamId : SteamClient.SteamId.Value, joinable, _launchCohortSteamIds, 0L);
				}
				string data = currentLobby.GetData("session_state");
				if (!string.Equals(data, state, StringComparison.Ordinal))
				{
					UtilityMethods.Log("<b><color=#FF4040>[Multiplayer Phase]</color></b> Lobby state publish verification failed.\n   Expected: " + state + "\n   Actual: " + data + $"\n   Lobby: {currentLobby.Id}", LogType.Error);
					ShowFailure(MultiplayerFailure.Create(MultiplayerFailureCode.LaunchPreparationFailed, MultiplayerFailureStage.LaunchPreparation, "Lobby state publish verification failed. Expected=" + state + ", Actual=" + data + "."));
				}
			}
		}

		private IEnumerator CoPublishHeartbeat()
		{
			ulong epoch = _sessionEpoch;
			ulong lobbyId = ((_steam != null) ? _steam.CurrentLobby.Id.Value : 0);
			WaitForSecondsRealtime wait = new WaitForSecondsRealtime(Mathf.Max(1f, _heartbeatIntervalSeconds));
			while (epoch != 0L && _sessionEpoch == epoch && IsHostStarted && _steam != null && _steam.CurrentLobby.Id.Value == lobbyId && IsCurrentLobbyOwner)
			{
				LobbySessionMetadata.PublishHeartbeat(_steam.CurrentLobby, epoch);
				yield return wait;
			}
			_heartbeatCoroutine = null;
		}

		private IEnumerator CoPruneMissingCohortConnections()
		{
			if (IsSingleplayerSession) { _cohortConnectionDeadlineCoroutine = null; yield break; }
			double deadline = Time.realtimeSinceStartupAsDouble + (double)_clientConnectTimeoutSeconds + _ngo.ApprovalTimeoutSeconds;
			while (Time.realtimeSinceStartupAsDouble < deadline || HasPendingInitialSceneSynchronization)
			{
				int num = 1;
				foreach (ulong launchCohortSteamId in _launchCohortSteamIds)
				{
					if (launchCohortSteamId != SteamClient.SteamId.Value && _activeSteamIds.Contains(launchCohortSteamId))
					{
						num++;
					}
				}
				if (num >= _launchCohortSteamIds.Count)
				{
					_cohortConnectionDeadlineCoroutine = null;
					yield break;
				}
				yield return null;
			}
			_launchCohortSteamIds.RemoveWhere((ulong steamId) => steamId != SteamClient.SteamId.Value && !_activeSteamIds.Contains(steamId));
			SetTargetPlayerCount(Mathf.Max(1, _launchCohortSteamIds.Count));
			_cohortConnectionDeadlineCoroutine = null;
			if (_steam != null && (ulong)_steam.CurrentLobby.Id != 0L)
			{
				LobbySessionMetadata.Publish(_steam.CurrentLobby, (SessionPhase == MultiplayerSessionPhase.Running) ? "ingame" : "host_ingame", _sessionEpoch, _networkHostSteamId, SessionPhase == MultiplayerSessionPhase.Running, _launchCohortSteamIds, 0L);
			}
			UtilityMethods.Log("<b><color=#FFB020>[Launch]</color></b> Initial connection deadline expired; missing clients moved to late join." + $"\n   RemainingTarget: {_targetPlayerCount}", LogType.Warning);
		}

		private void StopHeartbeat()
		{
			if (_heartbeatCoroutine != null)
			{
				StopCoroutine(_heartbeatCoroutine);
				_heartbeatCoroutine = null;
			}
		}

		private bool HandleReconnectFailure(MultiplayerFailure failure)
		{
			if (_reconnectRecovery == null)
			{
				return false;
			}
			_reconnectRecovery.ReportFailure(failure);
			_netState = NetworkConnectionState.Disconnected;
			_clientJoinInProgress = false;
			return true;
		}

		private void HandleClientConnectionFailure(MultiplayerFailure failure)
		{
			if (!_intentionalNetworkShutdown && !_sessionReturnPrepared && !HandleReconnectFailure(failure) && !TryBeginReconnect(failure))
			{
				ReturnToMainMenuAfterFailure(failure, isStartupFailure: false, LogType.Warning);
			}
		}

		private bool TryBeginReconnect(MultiplayerFailure failure)
		{
			if (_intentionalNetworkShutdown || _reconnectRecovery != null || !ClientReconnectRecovery.CanRecoverPhase(SessionPhase) || !ClientReconnectRecovery.CanRetry(failure.Code) || NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer || _ngo == null || _sessionEpoch == 0L || _networkHostSteamId == 0L || !IsSteamConnectionAvailable || !TryGetActiveLobby(out var lobby))
			{
				return false;
			}
			bool flag = SessionPhase != MultiplayerSessionPhase.Running;
			ClientReconnectRecovery clientReconnectRecovery = new ClientReconnectRecovery(lobby.Id.Value, _networkHostSteamId, _sessionEpoch, Time.realtimeSinceStartupAsDouble, flag ? 600.0 : ((double)_reconnectGraceSeconds), flag, SteamClient.SteamId.Value);
			if (!TryValidateReconnectTarget(clientReconnectRecovery, out var _))
			{
				return false;
			}
			_reconnectRecovery = clientReconnectRecovery;
			CancelClientWorldWork();
			_joiningLobbyId = clientReconnectRecovery.LobbyId;
			_joiningOwnerId = clientReconnectRecovery.HostId;
			_netState = NetworkConnectionState.Disconnected;
			_clientJoinInProgress = false;
			StopClientConnectionTimeout();
			SetSessionPhase(MultiplayerSessionPhase.Reconnecting);
			_reconnectCoroutine = StartCoroutine(CoReconnect(clientReconnectRecovery, failure.DebugDetail));
			return true;
		}

		private bool IsReconnectCurrent(ClientReconnectRecovery recovery)
		{
			if (_reconnectRecovery == recovery && recovery.Matches(_joiningLobbyId, _networkHostSteamId, _sessionEpoch) && (_steam == null || _steam.CurrentLobby.Id.Value == recovery.LobbyId) && !_intentionalNetworkShutdown)
			{
				if (!(Singleton<GameSessionLifecycle>.Instance == null))
				{
					return !Singleton<GameSessionLifecycle>.Instance.ReturnToMenuInProgress;
				}
				return true;
			}
			return false;
		}

		private bool TryValidateReconnectTarget(ClientReconnectRecovery recovery, out MultiplayerFailure failure)
		{
			MultiplayerFailureCode multiplayerFailureCode = ((!IsSteamConnectionAvailable) ? MultiplayerFailureCode.SteamUnavailable : ((TryGetActiveLobby(out var lobby) && LobbySessionMetadata.TryRead(lobby, out var descriptor)) ? recovery.ValidateTarget(lobby.Id.Value, lobby.Owner.Id.Value, descriptor, ReleaseVersion.Value, LobbySessionMetadata.UtcNowUnixSeconds, Mathf.CeilToInt(_heartbeatMaxAgeSeconds)) : MultiplayerFailureCode.HostUnavailable));
			failure = ((multiplayerFailureCode == MultiplayerFailureCode.None) ? default(MultiplayerFailure) : MultiplayerFailure.Create(multiplayerFailureCode, MultiplayerFailureStage.Reconnect, "The captured reconnect target is no longer valid."));
			return !failure.HasFailure;
		}

		private IEnumerator CoReconnect(ClientReconnectRecovery recovery, string debugReason)
		{
			yield return null;
			MultiplayerFailure terminalFailure = default;
			try
			{
				if (!IsReconnectCurrent(recovery))
				{
					yield break;
				}
				MultiplayerDiagnostics.Record("reconnect_begin", FormattableString.Invariant($"lobby={recovery.LobbyId} epoch={recovery.Epoch} host={recovery.HostId} initialJoin={recovery.InitialJoin} admissionDeadline={recovery.AdmissionDeadline:F3}"), ulong.MaxValue);
				while (IsReconnectCurrent(recovery) && recovery.CanStartAttempt(Time.realtimeSinceStartupAsDouble))
				{
					double retryAt = Time.realtimeSinceStartupAsDouble + recovery.NextDelaySeconds;
					while (IsReconnectCurrent(recovery) && !recovery.TerminalFailure.HasFailure && Time.realtimeSinceStartupAsDouble < Math.Min(retryAt, recovery.AdmissionDeadline))
					{
						yield return null;
					}
					if (!IsReconnectCurrent(recovery))
					{
						yield break;
					}
					if (!recovery.CanStartAttempt(Time.realtimeSinceStartupAsDouble) || !TryValidateReconnectTarget(recovery, out terminalFailure))
					{
						break;
					}
					if (recovery.Attempts == 0)
					{
						LoadingScreen.ShowLocalized("loading.title.reconnecting", "Reconnecting", "loading.status.reconnecting", "Restoring the connection...");
					}
					recovery.StoppingNetwork = true;
					double shutdownDeadline = Time.realtimeSinceStartupAsDouble + 30.0;
					if (!_ngo.IsStopped && NetworkManager.Singleton != null && !NetworkManager.Singleton.ShutdownInProgress)
					{
						_ngo.Shutdown(kickRemotesIfHost: false);
					}
					MultiplayerDiagnostics.Record("reconnect_shutdown_wait", $"retry={recovery.Attempts + 1}", ulong.MaxValue);
					yield return null;
					while (IsReconnectCurrent(recovery) && !_ngo.IsStopped && Time.realtimeSinceStartupAsDouble < shutdownDeadline)
					{
						yield return null;
					}
					if (!IsReconnectCurrent(recovery))
					{
						yield break;
					}
					if (!_ngo.IsStopped)
					{
						terminalFailure = MultiplayerFailure.Create(MultiplayerFailureCode.ReconnectTimedOut, MultiplayerFailureStage.Reconnect, "NGO shutdown did not finish within 30 seconds; no new client was started.");
						break;
					}
					recovery.StoppingNetwork = false;
					yield return CoResetClientWorldForRecovery(recovery);
					if (!IsReconnectCurrent(recovery))
					{
						yield break;
					}
					if (recovery.TerminalFailure.HasFailure || !TryValidateReconnectTarget(recovery, out terminalFailure) || !recovery.TryStartAttempt(Time.realtimeSinceStartupAsDouble))
					{
						break;
					}
					_netState = NetworkConnectionState.Connecting;
					_clientJoinInProgress = true;
					SetSessionPhase(MultiplayerSessionPhase.Reconnecting);
					bool flag = _ngo.StartClient(recovery.HostId, BuildConnectionPayload(), exitCurrentState: false);
					recovery.BindNetworkAttempt(_ngo.JoinAttempt);
					MultiplayerDiagnostics.Record("reconnect_attempt_started", $"retry={recovery.Attempts} joinAttempt={recovery.NetworkAttempt} started={flag}", ulong.MaxValue);
					if (!flag)
					{
						recovery.ReportFailure(MultiplayerFailure.Create(MultiplayerFailureCode.ClientStartFailed, MultiplayerFailureStage.Reconnect, "NetworkManager.StartClient returned false during recovery."));
					}
					else
					{
						yield return CoWaitForReconnectAttempt(recovery);
					}
					if (!IsReconnectCurrent(recovery))
					{
						yield break;
					}
					if (recovery.TerminalFailure.HasFailure)
					{
						break;
					}
					if (!recovery.AttemptInterrupted && _netState == NetworkConnectionState.Connected)
					{
						MultiplayerDiagnostics.Record("reconnect_connected", $"retry={recovery.Attempts} joinAttempt={recovery.NetworkAttempt}", ulong.MaxValue);
						_clientJoinInProgress = false;
						if (SessionPhase == MultiplayerSessionPhase.Reconnecting)
						{
							SetSessionPhase(MultiplayerSessionPhase.SceneSync);
						}
						yield return CoWaitForRecoveryGameplay(recovery);
						if (!IsReconnectCurrent(recovery))
						{
							yield break;
						}
						if (SessionPhase == MultiplayerSessionPhase.Running && !recovery.AttemptInterrupted)
						{
							MultiplayerDiagnostics.Record("reconnect_gameplay_restored", $"retry={recovery.Attempts}", ulong.MaxValue);
							yield break;
						}
					}
				}
				if (IsReconnectCurrent(recovery))
				{
					terminalFailure = (recovery.TerminalFailure.HasFailure ? recovery.TerminalFailure : (terminalFailure.HasFailure ? terminalFailure : (recovery.LastFailure.HasFailure ? recovery.LastFailure : MultiplayerFailure.Create(MultiplayerFailureCode.ReconnectTimedOut, MultiplayerFailureStage.Reconnect, debugReason))));
					_reconnectRecovery = null;
					_reconnectCoroutine = null;
					recovery.Cancel();
					_clientJoinInProgress = false;
					_netState = NetworkConnectionState.Disconnected;
					MultiplayerDiagnostics.Record("reconnect_failed", $"retry={recovery.Attempts} code={terminalFailure.Code}", ulong.MaxValue, critical: true);
					ReturnToMainMenuAfterFailure(terminalFailure);
				}
			}
			finally
			{
				MultiplayerCoordinator multiplayerCoordinator = this;
				if (multiplayerCoordinator._reconnectRecovery == recovery)
				{
					recovery.Cancel();
					multiplayerCoordinator._reconnectRecovery = null;
					multiplayerCoordinator._reconnectCoroutine = null;
				}
			}
		}

		private IEnumerator CoResetClientWorldForRecovery(ClientReconnectRecovery recovery)
		{
			recovery.StoppingNetwork = true;
			CancelClientWorldWork();
			double deadline = Time.realtimeSinceStartupAsDouble + 600.0;
			PPGSceneManager scenes = Singleton<PPGSceneManager>.Instance;
			while (IsReconnectCurrent(recovery) && (!_ngo.SceneOperationsSettled || (scenes != null && scenes.IsLoading)) && Time.realtimeSinceStartupAsDouble < deadline)
			{
				yield return null;
			}
			if (!IsReconnectCurrent(recovery))
			{
				yield break;
			}
			if (!_ngo.IsStopped || !_ngo.SceneOperationsSettled || scenes == null || scenes.IsLoading || Singleton<StateManager>.Instance == null)
			{
				recovery.ReportFailure(MultiplayerFailure.Create(MultiplayerFailureCode.ReconnectTimedOut, MultiplayerFailureStage.Reconnect, "Previous scene work did not settle; no replacement client was started."));
				yield break;
			}
			MultiplayerDiagnostics.Record("reconnect_world_reset_begin", $"retry={recovery.Attempts + 1}", ulong.MaxValue);
			try
			{
				if (Singleton<PlayersManager>.TryGetInstance(out var instance))
				{
					instance.ClearSessionStateForMainMenu();
				}
				DeformableManagerResetUtility.ResetDefaultManager();
				Singleton<StateManager>.Instance.SwitchState<LoadingState>();
			}
			catch (Exception ex)
			{
				recovery.ReportFailure(MultiplayerFailure.Create(MultiplayerFailureCode.BootstrapFailed, MultiplayerFailureStage.Reconnect, "Clean scene reset failed: " + ex.Message));
			}
			if (recovery.TerminalFailure.HasFailure)
			{
				yield break;
			}
			yield return null;
			while (IsReconnectCurrent(recovery) && scenes.IsLoading && Time.realtimeSinceStartupAsDouble < deadline)
			{
				yield return null;
			}
			if (IsReconnectCurrent(recovery))
			{
				if (scenes.IsLoading || SceneManager.GetActiveScene().name != "LoadingState" || !IsLoadingStateActive)
				{
					recovery.ReportFailure(MultiplayerFailure.Create(MultiplayerFailureCode.ReconnectTimedOut, MultiplayerFailureStage.Reconnect, "Clean loading scene did not become ready."));
					yield break;
				}
				recovery.StoppingNetwork = false;
				MultiplayerDiagnostics.Record("reconnect_world_reset_complete", $"retry={recovery.Attempts + 1}", ulong.MaxValue);
			}
		}

		private IEnumerator CoWaitForRecoveryGameplay(ClientReconnectRecovery recovery)
		{
			double deadline = Time.realtimeSinceStartupAsDouble + 600.0;
			while (IsReconnectCurrent(recovery) && !recovery.AttemptInterrupted && SessionPhase != MultiplayerSessionPhase.Running)
			{
				if (_ngo.JoinAttempt != recovery.NetworkAttempt)
				{
					recovery.Cancel();
					break;
				}
				if (Time.realtimeSinceStartupAsDouble >= deadline)
				{
					recovery.ReportFailure(MultiplayerFailure.Create(MultiplayerFailureCode.BootstrapFailed, MultiplayerFailureStage.Bootstrap, "Recovered connection did not reach gameplay within its preparation budget."));
					break;
				}
				yield return null;
			}
		}

		private IEnumerator CoWaitForReconnectAttempt(ClientReconnectRecovery recovery)
		{
			while (IsReconnectCurrent(recovery) && !recovery.AttemptInterrupted)
			{
				if (_ngo.JoinAttempt != recovery.NetworkAttempt)
				{
					recovery.Cancel();
					break;
				}
				if (_netState == NetworkConnectionState.Connected)
				{
					break;
				}
				double realtimeSinceStartupAsDouble = Time.realtimeSinceStartupAsDouble;
				JoinWaitStatus joinWaitStatus = _ngo.CheckJoinDeadline(recovery.NetworkAttempt, realtimeSinceStartupAsDouble);
				if (joinWaitStatus == JoinWaitStatus.Warning)
				{
					MultiplayerDiagnostics.Record("reconnect_wait_warning", FormattableString.Invariant($"joinAttempt={recovery.NetworkAttempt} stage={_ngo.JoinStage} elapsed={realtimeSinceStartupAsDouble - _ngo.JoinStageStartedAt:F1} limitSeconds={_ngo.JoinStageLimitSeconds}"), ulong.MaxValue);
				}
				if (joinWaitStatus == JoinWaitStatus.TimedOut || joinWaitStatus == JoinWaitStatus.NoProgress || joinWaitStatus == JoinWaitStatus.Inactive)
				{
					recovery.ReportFailure(MultiplayerFailure.Create((joinWaitStatus == JoinWaitStatus.Inactive) ? MultiplayerFailureCode.TransportFailure : MultiplayerFailureCode.ConnectionTimedOut, (_ngo.JoinStage == ClientJoinStage.SceneSynchronization) ? MultiplayerFailureStage.SceneSync : ((_ngo.JoinStage == ClientJoinStage.AwaitingApproval) ? MultiplayerFailureStage.ConnectionApproval : MultiplayerFailureStage.ClientConnect), $"Reconnect join attempt {recovery.NetworkAttempt}: stage={_ngo.JoinStage}, status={joinWaitStatus}."));
					MultiplayerDiagnostics.Record("reconnect_attempt_ended", $"joinAttempt={recovery.NetworkAttempt} stage={_ngo.JoinStage} status={joinWaitStatus}", ulong.MaxValue);
					break;
				}
				yield return null;
			}
		}

		private static MultiplayerFailure FailureFromDisconnectReason(string reason)
		{
			MultiplayerFailureCode code = MultiplayerFailureCode.TransportFailure;
			if (!string.IsNullOrWhiteSpace(reason) && reason.StartsWith("PPG:", StringComparison.Ordinal) && Enum.TryParse<MultiplayerFailureCode>(reason.Substring(4), out var result) && result != MultiplayerFailureCode.None && Enum.IsDefined(typeof(MultiplayerFailureCode), result))
			{
				code = result;
			}
			else if (!string.IsNullOrWhiteSpace(reason) && reason.StartsWith("PPG:", StringComparison.Ordinal))
			{
				code = MultiplayerFailureCode.Unknown;
			}
			return MultiplayerFailure.Create(code, MultiplayerFailureStage.ClientConnect, reason);
		}
	}
}
