using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Netcode.Transports.Facepunch;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States;
using PaperPixelGames.Framework.States.Game;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Networking
{
	[RequireComponent(typeof(FacepunchTransport))]
	public class NgoNetworkService : MonoBehaviour, INgoNetworkGateway
	{
		private static readonly FieldInfo SceneEventDataStoreField = typeof(NetworkSceneManager).GetField("SceneEventDataStore", BindingFlags.Instance | BindingFlags.NonPublic);

		private FacepunchTransport _transport;

		private NetworkManager _networkManager;

		private bool _initialized;

		private NetworkSceneManager _diagnosticSceneManager;

		private readonly ClientJoinDeadline _clientJoinDeadline = new ClientJoinDeadline();

		private AsyncOperation _sceneLoad;

		private readonly List<AsyncOperation> _pendingSceneOperations = new List<AsyncOperation>();

		private double _transportTimeoutSeconds = 120.0;

		public bool SceneOperationsSettled
		{
			get
			{
				_pendingSceneOperations.RemoveAll((AsyncOperation operation) => operation?.isDone ?? true);
				return _pendingSceneOperations.Count == 0;
			}
		}

		public ClientJoinStage JoinStage => _clientJoinDeadline.Stage;

		public long JoinAttempt => _clientJoinDeadline.Attempt;

		public double JoinStageStartedAt => _clientJoinDeadline.StageStartedAt;

		public double JoinLastProgressAt => _clientJoinDeadline.LastProgressAt;

		public double JoinStageLimitSeconds => _clientJoinDeadline.StageLimitSeconds;

		public double ApprovalTimeoutSeconds => (_networkManager != null) ? _networkManager.NetworkConfig.ClientConnectionBufferTimeout : 120;

		public double SceneTimeoutSeconds => Math.Max(600, (_networkManager != null) ? _networkManager.NetworkConfig.LoadSceneTimeOut : 0);

		public bool IsStopped
		{
			get
			{
				if (_networkManager != null && !_networkManager.IsListening && !_networkManager.ShutdownInProgress && !_networkManager.IsClient)
				{
					return !_networkManager.IsServer;
				}
				return false;
			}
		}

		public event Action ServerStarted;

		public event Action<ulong> ClientConnected;

		public event Action<ulong> ClientDisconnected;

		public event Action TransportFailure;

		public event Action<bool> ServerStopped;

		public event Action<bool> ClientStopped;

		public event Action<ClientJoinStage> ClientJoinStageChanged;

		public event Action<NetworkManager.ConnectionApprovalRequest, NetworkManager.ConnectionApprovalResponse> ApprovalRequested;

		private void Update()
		{
			if (_clientJoinDeadline.IsWaiting && _networkManager != null && _networkManager.IsClient && !_networkManager.IsServer)
			{
				if (_networkManager.IsApproved && _clientJoinDeadline.BeginSceneSynchronization(JoinAttempt, Time.realtimeSinceStartupAsDouble))
				{
					PublishJoinStage();
				}
				if (_sceneLoad != null)
				{
					_clientJoinDeadline.ObserveSceneProgress(JoinAttempt, Time.realtimeSinceStartupAsDouble, _sceneLoad.progress);
				}
			}
			if (!Netcode.Transports.Local.OfflineSingleplayerMode.Enabled && MultiplayerDiagnostics.Tick() && _transport != null)
			{
				_transport.SampleDiagnostics();
			}
		}

		private void BindDiagnosticSceneEvents()
		{
			NetworkSceneManager networkSceneManager = ((_networkManager != null) ? _networkManager.SceneManager : null);
			if (networkSceneManager != _diagnosticSceneManager)
			{
				if (_diagnosticSceneManager != null)
				{
					_diagnosticSceneManager.OnSceneEvent -= OnDiagnosticSceneEvent;
				}
				_diagnosticSceneManager = networkSceneManager;
				if (_diagnosticSceneManager != null)
				{
					_diagnosticSceneManager.OnSceneEvent += OnDiagnosticSceneEvent;
				}
			}
		}

		private void OnDiagnosticSceneEvent(SceneEvent sceneEvent)
		{
			if (sceneEvent.AsyncOperation != null && !sceneEvent.AsyncOperation.isDone && !_pendingSceneOperations.Contains(sceneEvent.AsyncOperation))
			{
				_pendingSceneOperations.Add(sceneEvent.AsyncOperation);
			}
			MultiplayerDiagnostics.Record("scene_" + sceneEvent.SceneEventType, sceneEvent.SceneName, sceneEvent.ClientId);
			if (!(_networkManager == null) && !_networkManager.IsServer && _clientJoinDeadline.IsWaiting && sceneEvent.ClientId == _networkManager.LocalClientId)
			{
				double realtimeSinceStartupAsDouble = Time.realtimeSinceStartupAsDouble;
				if (sceneEvent.SceneEventType == SceneEventType.Synchronize && _clientJoinDeadline.BeginSceneSynchronization(JoinAttempt, realtimeSinceStartupAsDouble))
				{
					PublishJoinStage();
				}
				if (sceneEvent.SceneEventType == SceneEventType.Load && _sceneLoad != sceneEvent.AsyncOperation)
				{
					_sceneLoad = sceneEvent.AsyncOperation;
					_clientJoinDeadline.BeginSceneLoad(JoinAttempt, realtimeSinceStartupAsDouble);
				}
				if (sceneEvent.SceneEventType == SceneEventType.LoadComplete)
				{
					_clientJoinDeadline.ObserveSceneProgress(JoinAttempt, realtimeSinceStartupAsDouble, 1f);
					_sceneLoad = null;
				}
			}
		}

		private void OnJoinTransportEvent(NetworkEvent type, ulong clientId, ArraySegment<byte> payload, float receiveTime)
		{
			if (type == NetworkEvent.Connect && _networkManager != null && !_networkManager.IsServer && _clientJoinDeadline.TransportConnected(JoinAttempt, Time.realtimeSinceStartupAsDouble))
			{
				PublishJoinStage();
			}
		}

		private void PublishJoinStage()
		{
			if (JoinStage == ClientJoinStage.SceneSynchronization)
			{
				MultiplayerDiagnostics.WatchStartup();
			}
			MultiplayerDiagnostics.Record("join_wait_stage", FormattableString.Invariant($"joinAttempt={JoinAttempt} stage={JoinStage} limitSeconds={JoinStageLimitSeconds}"), ulong.MaxValue);
			ClientJoinStageChanged?.Invoke(JoinStage);
		}

		public JoinWaitStatus CheckJoinDeadline(long attempt, double now)
		{
			return _clientJoinDeadline.Evaluate(attempt, now);
		}

		public void ConfigureJoinTimeouts(float transportTimeoutSeconds)
		{
			if (!(_networkManager == null) && !_networkManager.IsListening)
			{
				_transportTimeoutSeconds = ((float.IsNaN(transportTimeoutSeconds) || float.IsInfinity(transportTimeoutSeconds)) ? 120f : Math.Max(1f, transportTimeoutSeconds));
				_networkManager.NetworkConfig.ClientConnectionBufferTimeout = Math.Max(_networkManager.NetworkConfig.ClientConnectionBufferTimeout, (int)Math.Ceiling(_transportTimeoutSeconds));
				_networkManager.NetworkConfig.LoadSceneTimeOut = (int)SceneTimeoutSeconds;
			}
		}

		public void Initialize()
		{
			if (!_initialized)
			{
				_initialized = true;
				_transport = GetComponent<FacepunchTransport>();
				_transport.OnTransportEvent += OnJoinTransportEvent;
				_networkManager = NetworkManager.Singleton;
				if ((bool)_networkManager)
				{
					if (Netcode.Transports.Local.OfflineSingleplayerMode.Enabled)
					{
						var local = _networkManager.GetComponent<Netcode.Transports.Local.LocalSingleplayerTransport>();
						if (!local) local = _networkManager.gameObject.AddComponent<Netcode.Transports.Local.LocalSingleplayerTransport>();
						_networkManager.NetworkConfig.NetworkTransport = local;
					}
					ConfigureProductionDiagnostics(_networkManager);
					_networkManager.OnPreShutdown += PrepareNetworkSceneManagerForShutdown;
					_networkManager.OnServerStarted += OnServerStartedInternal;
					_networkManager.OnClientConnectedCallback += OnClientConnectedInternal;
					_networkManager.OnClientDisconnectCallback += OnClientDisconnectedInternal;
					NetworkManager networkManager = _networkManager;
					networkManager.ConnectionApprovalCallback = (Action<NetworkManager.ConnectionApprovalRequest, NetworkManager.ConnectionApprovalResponse>)Delegate.Combine(networkManager.ConnectionApprovalCallback, new Action<NetworkManager.ConnectionApprovalRequest, NetworkManager.ConnectionApprovalResponse>(OnConnectionApprovalInternal));
					_networkManager.OnTransportFailure += OnTransportFailureInternal;
					_networkManager.OnServerStopped += OnServerStoppedInternal;
					_networkManager.OnClientStopped += OnClientStoppedInternal;
				}
				Debug.Log("[NgoNetworkService] Initialized.");
			}
		}

		private static void ConfigureProductionDiagnostics(NetworkManager networkManager)
		{
			if (!Debug.isDebugBuild)
			{
				networkManager.NetworkConfig.EnableNetworkLogs = false;
				networkManager.NetworkConfig.NetworkMessageMetrics = false;
				networkManager.NetworkConfig.NetworkProfilingMetrics = false;
			}
		}

		private void OnDestroy()
		{
			_clientJoinDeadline.Cancel();
			if (_transport != null)
			{
				_transport.OnTransportEvent -= OnJoinTransportEvent;
			}
			if (_diagnosticSceneManager != null)
			{
				_diagnosticSceneManager.OnSceneEvent -= OnDiagnosticSceneEvent;
			}
			if ((bool)_networkManager)
			{
				_networkManager.OnPreShutdown -= PrepareNetworkSceneManagerForShutdown;
				_networkManager.OnServerStarted -= OnServerStartedInternal;
				_networkManager.OnClientConnectedCallback -= OnClientConnectedInternal;
				_networkManager.OnClientDisconnectCallback -= OnClientDisconnectedInternal;
				NetworkManager networkManager = _networkManager;
				networkManager.ConnectionApprovalCallback = (Action<NetworkManager.ConnectionApprovalRequest, NetworkManager.ConnectionApprovalResponse>)Delegate.Remove(networkManager.ConnectionApprovalCallback, new Action<NetworkManager.ConnectionApprovalRequest, NetworkManager.ConnectionApprovalResponse>(OnConnectionApprovalInternal));
				_networkManager.OnTransportFailure -= OnTransportFailureInternal;
				_networkManager.OnServerStopped -= OnServerStoppedInternal;
				_networkManager.OnClientStopped -= OnClientStoppedInternal;
			}
		}

		private void PrepareNetworkSceneManagerForShutdown()
		{
			_clientJoinDeadline.Cancel();
			_sceneLoad = null;
			NetworkSceneManager networkSceneManager = ((_networkManager != null) ? _networkManager.SceneManager : null);
			if (networkSceneManager == null || SceneEventDataStoreField == null)
			{
				return;
			}
			try
			{
				if (SceneEventDataStoreField.GetValue(networkSceneManager) == null)
				{
					object value = Activator.CreateInstance(SceneEventDataStoreField.FieldType);
					SceneEventDataStoreField.SetValue(networkSceneManager, value);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[NgoNetworkService] Network scene shutdown guard failed: " + ex.Message);
			}
		}

		public bool StartHost(byte[] connectionData)
		{
			if (!IsStopped || !SceneOperationsSettled)
			{
				return false;
			}
			_clientJoinDeadline.Cancel();
			_sceneLoad = null;
			MultiplayerDiagnostics.BeginAttempt("host");
			if (!Netcode.Transports.Local.OfflineSingleplayerMode.Enabled) { ConfigureSteamTimeouts(); RecordDiagnosticConfiguration(); }
			if (!NetworkManager.Singleton || (!Netcode.Transports.Local.OfflineSingleplayerMode.Enabled && (!SteamClient.IsValid || !SteamClient.IsLoggedOn)))
			{
				return false;
			}
			if (!ConfigureConnectionData(connectionData))
			{
				return false;
			}
			try
			{
				bool flag = NetworkManager.Singleton.StartHost();
				BindDiagnosticSceneEvents();
				MultiplayerDiagnostics.Record("ngo_start_result", $"started={flag}", ulong.MaxValue);
				return flag;
			}
			catch (Exception arg)
			{
				Debug.LogError($"[NgoNetworkService] StartHost failed: {arg}");
				return false;
			}
		}

		public bool StartClient(SteamId target, byte[] connectionData, bool exitCurrentState = true)
		{
			if (!IsStopped || !SceneOperationsSettled)
			{
				return false;
			}
			_clientJoinDeadline.Cancel();
			_sceneLoad = null;
			MultiplayerDiagnostics.BeginAttempt("client");
			ConfigureSteamTimeouts();
			RecordDiagnosticConfiguration();
			if (!NetworkManager.Singleton || !SteamClient.IsValid || !SteamClient.IsLoggedOn)
			{
				return false;
			}
			if (!ConfigureConnectionData(connectionData))
			{
				return false;
			}
			_transport.targetSteamId = target;
			if (MultiplayerFaultInjection.ConsumeForcedClientStartFailure())
			{
				MultiplayerDiagnostics.Record("client_start_failure_injected", "Development-only one-shot; no network connection started.", ulong.MaxValue);
				return false;
			}
			_clientJoinDeadline.Begin(Time.realtimeSinceStartupAsDouble, _transportTimeoutSeconds, _networkManager.NetworkConfig.ClientConnectionBufferTimeout, SceneTimeoutSeconds);
			PublishJoinStage();
			bool flag;
			try
			{
				flag = NetworkManager.Singleton.StartClient();
				BindDiagnosticSceneEvents();
				MultiplayerDiagnostics.Record("ngo_start_result", $"started={flag} targetSteam={target}", ulong.MaxValue);
			}
			catch (Exception arg)
			{
				_clientJoinDeadline.Cancel();
				Debug.LogError($"[NgoNetworkService] StartClient failed: {arg}");
				return false;
			}
			if (!flag)
			{
				_clientJoinDeadline.Cancel();
			}
			if (flag & exitCurrentState)
			{
				Singleton<StateManager>.Instance.ExitCurrentState();
			}
			return flag;
		}

		public bool TryGetAuthenticatedSteamId(ulong networkClientId, out ulong steamId)
		{
			steamId = 0uL;
			if (Netcode.Transports.Local.OfflineSingleplayerMode.Enabled)
			{
				if (_networkManager && networkClientId == NetworkManager.ServerClientId) { steamId = Netcode.Transports.Local.OfflineSingleplayerMode.LocalUserId; return true; }
				return false;
			}
			if (_networkManager == null || _transport == null)
			{
				return false;
			}
			if (_networkManager.IsHost && networkClientId == 0L)
			{
				steamId = (SteamClient.IsValid ? SteamClient.SteamId.Value : 0);
				return steamId != 0;
			}
			ulong transportIdFromClientId = _networkManager.GetTransportIdFromClientId(networkClientId);
			if (transportIdFromClientId != ulong.MaxValue)
			{
				return _transport.TryGetRemoteSteamId(transportIdFromClientId, out steamId);
			}
			return false;
		}

		private bool ConfigureConnectionData(byte[] connectionData)
		{
			if (!IsStopped)
			{
				return false;
			}
			if (connectionData == null || connectionData.Length == 0)
			{
				return false;
			}
			_networkManager.NetworkConfig.ConnectionData = connectionData;
			return true;
		}

		public void Shutdown(bool kickRemotesIfHost = true)
		{
			_clientJoinDeadline.Cancel();
			_sceneLoad = null;
			if (!NetworkManager.Singleton)
			{
				return;
			}
			if (NetworkManager.Singleton.IsHost & kickRemotesIfHost)
			{
				ulong[] array = NetworkManager.Singleton.ConnectedClientsIds.Where((ulong id) => id != NetworkManager.Singleton.LocalClientId).ToArray();
				foreach (ulong clientId in array)
				{
					FacepunchTransport.DisconnectClientWithReason(NetworkManager.Singleton, clientId, $"PPG:{MultiplayerFailureCode.HostUnavailable}");
				}
			}
			NetworkManager.Singleton.Shutdown();
		}

		private void OnServerStartedInternal()
		{
			BindDiagnosticSceneEvents();
			MultiplayerDiagnostics.Record("ngo_server_started", "", ulong.MaxValue);
			Debug.Log("[NgoNetworkService] OnServerStarted");
			ServerStarted?.Invoke();
		}

		private void OnClientConnectedInternal(ulong clientId)
		{
			if (_networkManager != null && !_networkManager.IsServer && clientId == _networkManager.LocalClientId)
			{
				_clientJoinDeadline.Complete(JoinAttempt);
				_sceneLoad = null;
				PublishJoinStage();
			}
			MultiplayerDiagnostics.Record("ngo_client_connected", $"localClient={_networkManager.LocalClientId}", clientId);
			Debug.Log($"[NgoNetworkService] OnClientConnected {clientId}");
			ClientConnected?.Invoke(clientId);
		}

		private void OnClientDisconnectedInternal(ulong clientId)
		{
			if (_networkManager != null && !_networkManager.IsServer && clientId == _networkManager.LocalClientId)
			{
				_clientJoinDeadline.Cancel();
				_sceneLoad = null;
			}
			MultiplayerDiagnostics.Record("ngo_client_disconnected", _networkManager.DisconnectReason, clientId, critical: true);
			MultiplayerDiagnostics.Flush("ngo_client_disconnected");
			Debug.Log($"[NgoNetworkService] OnClientDisconnected {clientId}");
			ClientDisconnected?.Invoke(clientId);
		}

		private void OnConnectionApprovalInternal(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
		{
			ApprovalRequested?.Invoke(req, res);
			try
			{
				TryGetAuthenticatedSteamId(req.ClientNetworkId, out var steamId);
				MultiplayerDiagnostics.Record("approval_decision", $"approved={res.Approved} pending={res.Pending} peerSteam={steamId} transportId={_networkManager.GetTransportIdFromClientId(req.ClientNetworkId)} reason={res.Reason}", req.ClientNetworkId, !res.Approved);
			}
			catch (Exception)
			{
				MultiplayerDiagnostics.Record("approval_diagnostic_unavailable", "", req.ClientNetworkId);
			}
		}

		private void OnTransportFailureInternal()
		{
			MultiplayerDiagnostics.Record("ngo_transport_failure", "", ulong.MaxValue, critical: true);
			MultiplayerDiagnostics.Flush("ngo_transport_failure");
			Debug.LogError("[NgoNetworkService] Transport failure.");
			TransportFailure?.Invoke();
		}

		private void OnServerStoppedInternal(bool wasHost)
		{
			ServerStopped?.Invoke(wasHost);
		}

		private void OnClientStoppedInternal(bool wasHost)
		{
			_clientJoinDeadline.Cancel();
			_sceneLoad = null;
			ClientStopped?.Invoke(wasHost);
		}

		private void ConfigureSteamTimeouts()
		{
			if (!SteamClient.IsValid || _networkManager == null || _networkManager.IsListening)
			{
				return;
			}
			try
			{
				bool flag = SteamConnectionTimeoutPolicy.Apply(_transportTimeoutSeconds, () => SteamNetworkingUtils.ConnectionTimeout, (int value) =>
				{
					SteamNetworkingUtils.ConnectionTimeout = value;
				}, () => SteamNetworkingUtils.Timeout, (int value) =>
				{
					SteamNetworkingUtils.Timeout = value;
				});
				MultiplayerDiagnostics.Record("steam_timeout_policy", FormattableString.Invariant($"scope=global initialMinimumMs={SteamConnectionTimeoutPolicy.InitialTimeoutMilliseconds(_transportTimeoutSeconds)} connectedMinimumMs={60000} verified={flag}"), ulong.MaxValue, !flag);
				if (!flag)
				{
					Debug.LogWarning("[NgoNetworkService] Steam timeout readback did not meet the requested policy.");
				}
			}
			catch (Exception ex)
			{
				MultiplayerDiagnostics.Record("steam_timeout_policy_failed", ex.Message, ulong.MaxValue, critical: true);
				Debug.LogWarning("[NgoNetworkService] Steam timeout configuration failed: " + ex.Message);
			}
		}

		private void RecordDiagnosticConfiguration()
		{
			if (_networkManager == null)
			{
				return;
			}
			NetworkConfig networkConfig = _networkManager.NetworkConfig;
			MultiplayerDiagnostics.Record("ngo_config", FormattableString.Invariant($"approvalTimeoutSeconds={networkConfig.ClientConnectionBufferTimeout} sceneTimeoutSeconds={networkConfig.LoadSceneTimeOut} spawnTimeoutSeconds={networkConfig.SpawnTimeout} sceneManagement={networkConfig.EnableSceneManagement} version={Application.version}"), ulong.MaxValue);
			if (!SteamClient.IsValid)
			{
				return;
			}
			try
			{
				MultiplayerDiagnostics.Record("steam_timeout_config", FormattableString.Invariant($"connectionTimeoutMs={SteamNetworkingUtils.ConnectionTimeout} connectedTimeoutMs={SteamNetworkingUtils.Timeout}"), ulong.MaxValue);
			}
			catch (Exception)
			{
				MultiplayerDiagnostics.Record("steam_timeout_config_unavailable", "", ulong.MaxValue);
			}
		}
	}
}
