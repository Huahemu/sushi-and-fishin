using System;
using System.Collections;
using System.Collections.Generic;
using Netcode.Transports.Facepunch;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States.Game;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Networking
{
	public class GameStartGate : NetworkBehaviour
	{
		[Header("Start Rules")]
		[SerializeField]
		private int _minPlayersToStart = 1;

		[SerializeField]
		private float _sceneReadyTimeoutSeconds = 240f;

		[SerializeField]
		private float _bootstrapTimeoutSeconds = 60f;

		private Coroutine _startupWatchdogCoroutine;

		private bool _startupFailed;

		private bool _initialSpawnTriggered;

		private bool _lateJoinWorldReady;

		private int _initialExpectedPlayerCount = 1;

		private readonly HashSet<ulong> _readyClients = new HashSet<ulong>();

		private readonly HashSet<ulong> _acceptedReadyClients = new HashSet<ulong>();

		private readonly NetworkVariable<bool> _started = new NetworkVariable<bool>(value: false);

		private readonly HashSet<ulong> _bootstrapCompletedClients = new HashSet<ulong>();

		private readonly HashSet<ulong> _initialCohortClients = new HashSet<ulong>();

		private readonly HashSet<ulong> _pendingLateJoinClients = new HashSet<ulong>();

		private readonly HashSet<ulong> _refreshLateJoinClients = new HashSet<ulong>();

		private readonly Dictionary<ulong, float> _lateJoinDeadlines = new Dictionary<ulong, float>();

		private readonly List<ulong> _expiredLateJoinClients = new List<ulong>();

		public bool IsGameStarted => _started.Value;

		public int ReadyClientCount => _readyClients.Count;

		public int BootstrapCompletedClientCount => _bootstrapCompletedClients.Count;

		private float LateBootstrapBudget => Mathf.Max(120f, _bootstrapTimeoutSeconds);

		public override void OnNetworkSpawn()
		{
			MultiplayerDiagnostics.Record("gate_spawned", FormattableString.Invariant($"sceneReadyTimeoutSeconds={_sceneReadyTimeoutSeconds} bootstrapTimeoutSeconds={_bootstrapTimeoutSeconds}"), ulong.MaxValue);
			if (base.IsServer && NetworkManager.Singleton != null)
			{
				_bootstrapCompletedClients.Clear();
				_readyClients.Clear();
				_acceptedReadyClients.Clear();
				_initialCohortClients.Clear();
				_pendingLateJoinClients.Clear();
				_refreshLateJoinClients.Clear();
				_lateJoinDeadlines.Clear();
				_expiredLateJoinClients.Clear();
				_initialSpawnTriggered = false;
				_lateJoinWorldReady = false;
				_started.Value = false;
				_startupFailed = false;
				_initialExpectedPlayerCount = ResolveInitialExpectedPlayerCount();
				NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
				NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
				MultiplayerCoordinator instance = Singleton<MultiplayerCoordinator>.Instance;
				if (instance != null)
				{
					instance.TargetPlayerCountChanged += OnTargetPlayerCountChanged;
				}
			}
			if (NetworkManager.Singleton.IsHost)
			{
				Singleton<MultiplayerCoordinator>.Instance?.NotifyHostGameSceneReady();
			}
			LoadingScreen.UpdateLocalizedText("loading.status.waiting_for_players", "Waiting for everyone to finish loading...", string.Empty, string.Empty);
			if (base.IsServer)
			{
				ServerHandleClientReady(NetworkManager.Singleton.LocalClientId);
			}
			else
			{
				ServerReportReadyServerRpc();
			}
			StartStartupWatchdog();
		}

		public override void OnNetworkDespawn()
		{
			if (base.IsServer && NetworkManager.Singleton != null)
			{
				NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
				NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
				MultiplayerCoordinator instance = Singleton<MultiplayerCoordinator>.Instance;
				if (instance != null)
				{
					instance.TargetPlayerCountChanged -= OnTargetPlayerCountChanged;
				}
			}
			if (_startupWatchdogCoroutine != null)
			{
				StopCoroutine(_startupWatchdogCoroutine);
				_startupWatchdogCoroutine = null;
			}
			_startupFailed = true;
			_lateJoinWorldReady = false;
			_acceptedReadyClients.Clear();
			_readyClients.Clear();
			_initialCohortClients.Clear();
			_bootstrapCompletedClients.Clear();
			_pendingLateJoinClients.Clear();
			_refreshLateJoinClients.Clear();
			_lateJoinDeadlines.Clear();
			_expiredLateJoinClients.Clear();
		}

		[Rpc(SendTo.Server)]
		private void ServerReportReadyServerRpc(RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(4158074654u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 4158074654u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ServerHandleClientReady(rpcParams.Receive.SenderClientId);
			}
		}

		private void OnClientConnected(ulong clientId)
		{
			if (base.IsServer && base.IsSpawned && !_startupFailed && !_started.Value)
			{
				UtilityMethods.Log($"<b><color=#8FD14F>[Multiplayer]</color></b> Client connected -> clientId={clientId} (connectedCount={NetworkManager.Singleton.ConnectedClientsIds.Count}).");
				TryStart();
			}
		}

		private void OnClientDisconnected(ulong clientId)
		{
			if (!base.IsServer)
			{
				return;
			}
			_refreshLateJoinClients.Remove(clientId);
			RpcRelay.ActiveInstance?.ServerForgetBootstrap(clientId);
			_pendingLateJoinClients.Remove(clientId);
			_acceptedReadyClients.Remove(clientId);
			_bootstrapCompletedClients.Remove(clientId);
			_lateJoinDeadlines.Remove(clientId);
			bool flag = _initialCohortClients.Remove(clientId);
			if (_readyClients.Remove(clientId))
			{
				UtilityMethods.Log($"<b><color=#FFB020>[Multiplayer]</color></b> Client disconnected -> removed from ready: clientId={clientId}.");
			}
			if (!_started.Value)
			{
				if (!_initialSpawnTriggered & flag)
				{
					_initialExpectedPlayerCount = Mathf.Max(_minPlayersToStart, Mathf.Max(_initialCohortClients.Count, ResolveInitialExpectedPlayerCount()));
				}
				if (_initialSpawnTriggered)
				{
					TryCompleteInitialBootstrap();
				}
				else
				{
					TryStart();
				}
			}
		}

		private int GetExpectedPlayerCount()
		{
			if (!_initialSpawnTriggered)
			{
				return Mathf.Max(_minPlayersToStart, _initialExpectedPlayerCount);
			}
			return Mathf.Max(_minPlayersToStart, _initialCohortClients.Count);
		}

		private int ResolveInitialExpectedPlayerCount()
		{
			MultiplayerCoordinator instance = Singleton<MultiplayerCoordinator>.Instance;
			int b = ((instance != null) ? instance.TargetPlayerCount : _minPlayersToStart);
			return Mathf.Max(_minPlayersToStart, b);
		}

		private void OnTargetPlayerCountChanged(int targetCount)
		{
			if (base.IsServer && !_initialSpawnTriggered && !_started.Value)
			{
				_initialExpectedPlayerCount = Mathf.Max(_minPlayersToStart, Mathf.Max(_initialCohortClients.Count, targetCount));
				UtilityMethods.Log("<b><color=#8FD14F>[Multiplayer]</color></b> Initial target player count updated." + $"\n   ExpectedCount: {_initialExpectedPlayerCount}" + $"\n   CohortCount: {_initialCohortClients.Count}");
				TryStart();
			}
		}

		private void TryStart()
		{
			if (!base.IsServer || !base.IsSpawned || _startupFailed || _initialSpawnTriggered || _started.Value)
			{
				return;
			}
			NetworkManager singleton = NetworkManager.Singleton;
			if (singleton == null)
			{
				return;
			}
			int expectedPlayerCount = GetExpectedPlayerCount();
			if (_initialCohortClients.Count < expectedPlayerCount)
			{
				LoadingScreen.UpdateLocalizedText("loading.title.joining_session", "Joining Session", "loading.status.waiting_for_players", "Waiting for everyone to finish loading...");
				UtilityMethods.Log("<b><color=#FFB020>[Multiplayer]</color></b> Gate waiting... " + $"(cohort={_initialCohortClients.Count}/{expectedPlayerCount}, ready={_readyClients.Count}, connected={singleton.ConnectedClientsIds.Count}).");
				return;
			}
			foreach (ulong initialCohortClient in _initialCohortClients)
			{
				if (!singleton.ConnectedClients.ContainsKey(initialCohortClient) || !_readyClients.Contains(initialCohortClient))
				{
					LoadingScreen.UpdateLocalizedText("loading.title.joining_session", "Joining Session", "loading.status.waiting_for_players", "Waiting for everyone to finish loading...");
					UtilityMethods.Log($"<b><color=#FFB020>[Multiplayer]</color></b> Gate waiting... missing connected/ready -> clientId={initialCohortClient}");
					return;
				}
			}
			if (!_initialCohortClients.Contains(singleton.LocalClientId) || !_readyClients.Contains(singleton.LocalClientId))
			{
				return;
			}
			_initialSpawnTriggered = true;
			_initialExpectedPlayerCount = _initialCohortClients.Count;
			foreach (ulong initialCohortClient2 in _initialCohortClients)
			{
				Singleton<MultiplayerCoordinator>.Instance.SpawnPlayerForClient(initialCohortClient2);
			}
			Singleton<MultiplayerCoordinator>.Instance?.NotifyInitialBootstrapStarted();
			UtilityMethods.Log("<b><color=#8FD14F>[Multiplayer]</color></b> All ready + enough connected -> initial player spawn triggered.");
		}

		public void ServerMarkBootstrapComplete(ulong clientId, JoinBootstrapMode mode, ulong sessionEpoch)
		{
			MultiplayerDiagnostics.Record("bootstrap_completion_received", $"mode={mode} receivedEpoch={sessionEpoch}", clientId);
			if (!base.IsServer || !base.IsSpawned || _startupFailed)
			{
				return;
			}
			if (Singleton<MultiplayerCoordinator>.Instance == null || sessionEpoch == 0L || sessionEpoch != Singleton<MultiplayerCoordinator>.Instance.SessionEpoch)
			{
				UtilityMethods.Log("<b><color=#FF4040>[Bootstrap]</color></b> Completion rejected for stale session." + $"\n   ClientId: {clientId}" + $"\n   ReceivedEpoch: {sessionEpoch}" + $"\n   CurrentEpoch: {Singleton<MultiplayerCoordinator>.Instance?.SessionEpoch ?? 0}", LogType.Warning);
			}
			else if (!HasSpawnedPlayer(clientId))
			{
				UtilityMethods.Log("<b><color=#FF4040>[Bootstrap]</color></b> Completion rejected because the client has no spawned player." + $"\n   ClientId: {clientId}" + $"\n   Mode: {mode}", LogType.Error);
				ServerHandleBootstrapFailure(clientId, "Bootstrap completion arrived before the network player was spawned.");
			}
			else if (_bootstrapCompletedClients.Contains(clientId))
			{
				MultiplayerDiagnostics.Record("bootstrap_completion_duplicate", $"mode={mode}", clientId);
				JoinBootstrapMode joinBootstrapMode = ((!_initialCohortClients.Contains(clientId)) ? JoinBootstrapMode.Late : JoinBootstrapMode.Initial);
				if (_started.Value && mode == joinBootstrapMode && Singleton<GameProgressManager>.TryGetInstance(out var instance) && instance.RpcRelay != null && instance.RpcRelay.IsSpawned)
				{
					instance.RpcRelay.ServerSendBeginGameplay(clientId);
				}
			}
			else if (_started.Value)
			{
				if (!_lateJoinWorldReady)
				{
					MultiplayerDiagnostics.Record("bootstrap_completion_before_world_ready", "", clientId);
					return;
				}
				if (mode != JoinBootstrapMode.Late)
				{
					UtilityMethods.Log("<b><color=#FF4040>[Bootstrap]</color></b> Completion mode rejected for running session." + $"\n   ClientId: {clientId}" + $"\n   Mode: {mode}", LogType.Error);
					ServerHandleBootstrapFailure(clientId, $"Unexpected bootstrap mode {mode} for a running session.");
					return;
				}
				RpcRelay rpcRelay = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RpcRelay : null);
				if (rpcRelay == null)
				{
					ServerHandleBootstrapFailure(clientId, "RpcRelay was unavailable after late-join bootstrap completed.");
					return;
				}
				_pendingLateJoinClients.Remove(clientId);
				_lateJoinDeadlines.Remove(clientId);
				_bootstrapCompletedClients.Add(clientId);
				UtilityMethods.Log($"<b><color=#8FD14F>[Bootstrap]</color></b> Late bootstrap complete -> clientId={clientId}, sending single begin gameplay.");
				rpcRelay.ServerSendBeginGameplay(clientId);
			}
			else if (mode != JoinBootstrapMode.Initial || !_initialCohortClients.Contains(clientId))
			{
				UtilityMethods.Log("<b><color=#FF4040>[Bootstrap]</color></b> Initial completion rejected." + $"\n   ClientId: {clientId}" + $"\n   Mode: {mode}" + $"\n   InInitialCohort: {_initialCohortClients.Contains(clientId)}", LogType.Error);
				ServerHandleBootstrapFailure(clientId, $"Unexpected initial bootstrap completion mode={mode}, inCohort={_initialCohortClients.Contains(clientId)}.");
			}
			else
			{
				bool flag = _bootstrapCompletedClients.Add(clientId);
				UtilityMethods.Log($"<b><color=#8FD14F>[Bootstrap]</color></b> Initial bootstrap complete -> clientId={clientId}, added={flag}, completed={_bootstrapCompletedClients.Count}/{GetExpectedPlayerCount()}");
				if (flag)
				{
					TryCompleteInitialBootstrap();
				}
			}
		}

		private void TryCompleteInitialBootstrap()
		{
			if (!base.IsServer || _started.Value || !_initialSpawnTriggered || _startupFailed)
			{
				return;
			}
			foreach (ulong initialCohortClient in _initialCohortClients)
			{
				if (!_bootstrapCompletedClients.Contains(initialCohortClient))
				{
					return;
				}
			}
			RpcRelay rpcRelay = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RpcRelay : null);
			if (rpcRelay == null)
			{
				FailStartup(MultiplayerFailure.Create(MultiplayerFailureCode.BootstrapFailed, MultiplayerFailureStage.Bootstrap, "RpcRelay was unavailable after initial bootstrap completed."));
				return;
			}
			_started.Value = true;
			foreach (ulong initialCohortClient2 in _initialCohortClients)
			{
				rpcRelay.ServerSendBeginGameplay(initialCohortClient2);
			}
			TryOpenLateJoinGate();
		}

		private void TryOpenLateJoinGate()
		{
			if (base.IsServer && base.IsSpawned && !_startupFailed && _started.Value && !_lateJoinWorldReady)
			{
				NetworkManager singleton = NetworkManager.Singleton;
				if (!(singleton == null) && singleton.IsHost && Singleton<JoinBootstrapCoordinator>.TryGetInstance(out var instance) && instance.BootstrapCompleted && Singleton<MultiplayerCoordinator>.TryGetInstance(out var instance2) && instance2.SessionEpoch != 0L && instance.CurrentContext.IsValid && instance.CurrentContext.SessionEpoch == instance2.SessionEpoch)
				{
					_lateJoinWorldReady = true;
					MultiplayerDiagnostics.Record("late_join_world_ready", $"pending={_pendingLateJoinClients.Count}", ulong.MaxValue);
					instance2.NotifyGameRunning();
					SpawnQueuedLateJoinClients();
				}
			}
		}

		public bool TryResolveBootstrapMode(ulong clientId, out JoinBootstrapMode mode)
		{
			mode = JoinBootstrapMode.Initial;
			if (!base.IsServer || !base.IsSpawned || _startupFailed || !_initialSpawnTriggered || !_acceptedReadyClients.Contains(clientId) || _bootstrapCompletedClients.Contains(clientId) || !HasSpawnedPlayer(clientId))
			{
				return false;
			}
			if (_initialCohortClients.Contains(clientId))
			{
				return !_started.Value;
			}
			if (!_started.Value || !_lateJoinWorldReady)
			{
				return false;
			}
			mode = JoinBootstrapMode.Late;
			return true;
		}

		private void ServerHandleClientReady(ulong clientId)
		{
			MultiplayerDiagnostics.Record("gate_ready_received", "", clientId);
			if (!base.IsServer || !base.IsSpawned || _startupFailed)
			{
				return;
			}
			NetworkManager singleton = NetworkManager.Singleton;
			if (singleton == null || !singleton.ConnectedClients.ContainsKey(clientId))
			{
				return;
			}
			if (!_acceptedReadyClients.Add(clientId))
			{
				MultiplayerDiagnostics.Record("gate_ready_duplicate", "", clientId);
				return;
			}
			Singleton<MultiplayerCoordinator>.Instance?.NotifyClientSceneReady(clientId);
			if (_started.Value && _lateJoinWorldReady)
			{
				UtilityMethods.Log($"<b><color=#8FD14F>[Multiplayer]</color></b> Late join ready -> spawn immediately clientId={clientId}.");
				Singleton<MultiplayerCoordinator>.Instance.SpawnPlayerForClient(clientId);
				TrackLateJoinDeadline(clientId);
				return;
			}
			if (_initialSpawnTriggered)
			{
				bool flag = _pendingLateJoinClients.Add(clientId);
				UtilityMethods.Log("<b><color=#FFB020>[Multiplayer]</color></b> Client ready before host world activation -> queued as late join." + $"\n   ClientId: {clientId}" + $"\n   Added: {flag}" + $"\n   PendingLateJoinCount: {_pendingLateJoinClients.Count}");
				return;
			}
			if (!_initialCohortClients.Contains(clientId))
			{
				if (_initialCohortClients.Count >= GetExpectedPlayerCount())
				{
					_pendingLateJoinClients.Add(clientId);
					return;
				}
				_initialCohortClients.Add(clientId);
			}
			bool flag2 = _readyClients.Add(clientId);
			UtilityMethods.Log("<b><color=#8FD14F>[Multiplayer]</color></b> Ready received" + $"\n   ClientId: {clientId}" + $"\n   Added: {flag2}" + $"\n   ReadyCount: {_readyClients.Count}" + $"\n   ConnectedCount: {NetworkManager.Singleton.ConnectedClientsIds.Count}" + $"\n   ExpectedCount: {GetExpectedPlayerCount()}");
			TryStart();
		}

		private void SpawnQueuedLateJoinClients()
		{
			if (!base.IsServer || !_started.Value || !_lateJoinWorldReady || _pendingLateJoinClients.Count == 0)
			{
				return;
			}
			ulong[] array = new ulong[_pendingLateJoinClients.Count];
			_pendingLateJoinClients.CopyTo(array);
			_pendingLateJoinClients.Clear();
			foreach (ulong num in array)
			{
				if (NetworkManager.Singleton.ConnectedClients.ContainsKey(num))
				{
					bool num2 = _refreshLateJoinClients.Remove(num);
					if (!num2)
					{
						Singleton<MultiplayerCoordinator>.Instance.SpawnPlayerForClient(num);
					}
					TrackLateJoinDeadline(num);
					if (num2)
					{
						RpcRelay.ActiveInstance?.ServerSendBootstrap(num, refreshOnly: true);
					}
				}
			}
		}

		public void ServerHandleBootstrapFailure(ulong clientId, string reason)
		{
			if (!base.IsServer)
			{
				return;
			}
			string text = (string.IsNullOrWhiteSpace(reason) ? "Unknown bootstrap failure." : reason);
			if (!_started.Value && _initialCohortClients.Contains(clientId))
			{
				NetworkManager singleton = NetworkManager.Singleton;
				if (singleton != null && clientId == singleton.LocalClientId)
				{
					FailStartup(MultiplayerFailure.Create(MultiplayerFailureCode.BootstrapFailed, MultiplayerFailureStage.Bootstrap, $"Host bootstrap failed for clientId={clientId}. {text}"));
					return;
				}
				RemoveRemoteFromInitialCohort(clientId, text);
				TryCompleteInitialBootstrap();
				return;
			}
			_pendingLateJoinClients.Remove(clientId);
			_lateJoinDeadlines.Remove(clientId);
			UtilityMethods.Log("<b><color=#FF4040>[Bootstrap]</color></b> Late-join bootstrap failed. Disconnecting only the affected client." + $"\n   ClientId: {clientId}" + "\n   Reason: " + text, LogType.Error);
			NetworkManager singleton2 = NetworkManager.Singleton;
			if (singleton2 != null && clientId != singleton2.LocalClientId && singleton2.ConnectedClients.ContainsKey(clientId))
			{
				FacepunchTransport.DisconnectClientWithReason(singleton2, clientId, $"PPG:{MultiplayerFailureCode.BootstrapFailed}");
			}
		}

		private void TrackLateJoinDeadline(ulong clientId)
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if (base.IsServer && base.IsSpawned && !_startupFailed && _started.Value && _lateJoinWorldReady && !(singleton == null) && clientId != singleton.LocalClientId && _acceptedReadyClients.Contains(clientId) && !_initialCohortClients.Contains(clientId) && !_bootstrapCompletedClients.Contains(clientId) && !_lateJoinDeadlines.ContainsKey(clientId))
			{
				_lateJoinDeadlines[clientId] = Time.realtimeSinceStartup + LateBootstrapBudget;
				MultiplayerDiagnostics.Record("late_bootstrap_deadline_started", FormattableString.Invariant($"timeoutSeconds={LateBootstrapBudget} expiresAt={_lateJoinDeadlines[clientId]:F3}"), clientId);
			}
		}

		private void CheckLateJoinTimeouts()
		{
			if (_lateJoinDeadlines.Count == 0)
			{
				return;
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			_expiredLateJoinClients.Clear();
			foreach (KeyValuePair<ulong, float> lateJoinDeadline in _lateJoinDeadlines)
			{
				if (realtimeSinceStartup >= lateJoinDeadline.Value)
				{
					_expiredLateJoinClients.Add(lateJoinDeadline.Key);
				}
			}
			for (int i = 0; i < _expiredLateJoinClients.Count; i++)
			{
				ulong clientId = _expiredLateJoinClients[i];
				ServerHandleBootstrapFailure(clientId, $"Timed out after {LateBootstrapBudget:0} seconds while waiting for late-join bootstrap completion.");
			}
		}

		private static bool HasSpawnedPlayer(ulong clientId)
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if (singleton != null && singleton.ConnectedClients.TryGetValue(clientId, out var value))
			{
				return value.PlayerObject != null;
			}
			return false;
		}

		private void StartStartupWatchdog()
		{
			if (base.IsServer)
			{
				if (_startupWatchdogCoroutine != null)
				{
					StopCoroutine(_startupWatchdogCoroutine);
				}
				_startupWatchdogCoroutine = StartCoroutine(CoStartupWatchdog());
			}
		}

		private IEnumerator CoStartupWatchdog()
		{
			float gateDeadline = Time.realtimeSinceStartup + _sceneReadyTimeoutSeconds;
			while (!_initialSpawnTriggered && !_startupFailed)
			{
				if (Time.realtimeSinceStartup >= gateDeadline)
				{
					DeferClientsMissingSceneReady();
					TryStart();
					if (!_initialSpawnTriggered)
					{
						FailStartup(MultiplayerFailure.Create(MultiplayerFailureCode.ConnectionTimedOut, MultiplayerFailureStage.SceneSync, $"Host could not pass the scene-ready gate. connected={NetworkManager.Singleton.ConnectedClientsIds.Count}, " + $"ready={_readyClients.Count}, expected={GetExpectedPlayerCount()}"));
						yield break;
					}
				}
				yield return null;
			}
			float bootstrapDeadline = Time.realtimeSinceStartup + _bootstrapTimeoutSeconds;
			while (!_started.Value && !_startupFailed)
			{
				if (Time.realtimeSinceStartup >= bootstrapDeadline)
				{
					if (!PruneClientsMissingInitialBootstrap())
					{
						FailStartup(MultiplayerFailure.Create(MultiplayerFailureCode.BootstrapFailed, MultiplayerFailureStage.Bootstrap, $"Host bootstrap timeout. completed={_bootstrapCompletedClients.Count}, " + $"expected={GetExpectedPlayerCount()}"));
						yield break;
					}
					TryCompleteInitialBootstrap();
				}
				yield return null;
			}
			float worldActivationDeadline = Time.realtimeSinceStartup + _bootstrapTimeoutSeconds;
			while (base.IsServer && base.IsSpawned && !_startupFailed)
			{
				TryOpenLateJoinGate();
				if (!_lateJoinWorldReady && Time.realtimeSinceStartup >= worldActivationDeadline)
				{
					FailStartup(MultiplayerFailure.Create(MultiplayerFailureCode.BootstrapFailed, MultiplayerFailureStage.Bootstrap, "Host did not finish local gameplay activation before the late-join world deadline."));
					break;
				}
				CheckLateJoinTimeouts();
				yield return null;
			}
		}

		private void DeferClientsMissingSceneReady()
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if (singleton == null)
			{
				return;
			}
			int num = 0;
			foreach (ulong connectedClientsId in singleton.ConnectedClientsIds)
			{
				if (connectedClientsId != singleton.LocalClientId && !_readyClients.Contains(connectedClientsId))
				{
					num++;
					MultiplayerDiagnostics.Record("initial_scene_client_deferred", "", connectedClientsId);
				}
			}
			int expectedPlayerCount = GetExpectedPlayerCount();
			_initialExpectedPlayerCount = Mathf.Max(_minPlayersToStart, _initialCohortClients.Count);
			Singleton<MultiplayerCoordinator>.Instance?.ServerReduceInitialTarget(_initialExpectedPlayerCount);
			MultiplayerDiagnostics.Record("initial_scene_group_continued", $"expectedBefore={expectedPlayerCount} initialCount={_initialCohortClients.Count} deferredConnected={num}", ulong.MaxValue);
			UtilityMethods.Log("<b><color=#FFB020>[Multiplayer]</color></b> Scene group wait ended; unready clients may continue as late joins." + $"\n   DeferredConnectedClients: {num}" + $"\n   RemainingCohort: {_initialCohortClients.Count}" + $"\n   NewExpected: {_initialExpectedPlayerCount}", LogType.Warning);
		}

		private bool PruneClientsMissingInitialBootstrap()
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if (singleton == null)
			{
				return false;
			}
			ulong localClientId = singleton.LocalClientId;
			if (_initialCohortClients.Contains(localClientId) && !_bootstrapCompletedClients.Contains(localClientId))
			{
				return false;
			}
			List<ulong> list = new List<ulong>();
			foreach (ulong initialCohortClient in _initialCohortClients)
			{
				if (!_bootstrapCompletedClients.Contains(initialCohortClient))
				{
					list.Add(initialCohortClient);
				}
			}
			for (int i = 0; i < list.Count; i++)
			{
				ulong num = list[i];
				RpcRelay.ActiveInstance?.ServerDeferBootstrap(num);
				_initialCohortClients.Remove(num);
				_readyClients.Remove(num);
				_refreshLateJoinClients.Add(num);
				_pendingLateJoinClients.Add(num);
				MultiplayerDiagnostics.Record("initial_bootstrap_deferred", "Fresh Late snapshot after host activation.", num);
			}
			_initialExpectedPlayerCount = Mathf.Max(_minPlayersToStart, _initialCohortClients.Count);
			Singleton<MultiplayerCoordinator>.Instance?.ServerReduceInitialTarget(_initialExpectedPlayerCount);
			return true;
		}

		private void RemoveRemoteFromInitialCohort(ulong clientId, string reason)
		{
			_refreshLateJoinClients.Remove(clientId);
			RpcRelay.ActiveInstance?.ServerForgetBootstrap(clientId);
			_initialCohortClients.Remove(clientId);
			_readyClients.Remove(clientId);
			_bootstrapCompletedClients.Remove(clientId);
			_pendingLateJoinClients.Remove(clientId);
			_lateJoinDeadlines.Remove(clientId);
			_initialExpectedPlayerCount = Mathf.Max(_minPlayersToStart, _initialCohortClients.Count);
			Singleton<MultiplayerCoordinator>.Instance?.ServerReduceInitialTarget(_initialExpectedPlayerCount);
			UtilityMethods.Log("<b><color=#FFB020>[Bootstrap]</color></b> Removing failed remote client from initial cohort." + $"\n   ClientId: {clientId}" + "\n   Reason: " + reason + $"\n   Remaining: {_initialCohortClients.Count}", LogType.Warning);
			NetworkManager singleton = NetworkManager.Singleton;
			if (singleton != null && clientId != singleton.LocalClientId && singleton.ConnectedClients.ContainsKey(clientId))
			{
				FacepunchTransport.DisconnectClientWithReason(singleton, clientId, $"PPG:{MultiplayerFailureCode.BootstrapFailed}");
			}
		}

		private void FailStartup(MultiplayerFailure failure)
		{
			if (!_startupFailed)
			{
				_startupFailed = true;
				UtilityMethods.Log("<b><color=#FF4040>[Multiplayer]</color></b> Startup failed" + $"\n   Code: {failure.Code}" + $"\n   Stage: {failure.Stage}" + "\n   Reason: " + failure.DebugDetail, LogType.Error);
				ClientStartupFailedRpc((byte)failure.Code, (byte)failure.Stage, failure.DebugDetail);
				if (Singleton<GameSessionLifecycle>.Instance != null)
				{
					Singleton<GameSessionLifecycle>.Instance.ReturnToMainMenu(ReturnToMainMenuRequest.StartupFailure(failure));
				}
				else
				{
					UtilityMethods.Log("<b><color=#FF4040>[Multiplayer]</color></b> Startup failure return failed. GameSessionLifecycle is null.\n   Reason: " + failure.DebugDetail, LogType.Error);
				}
			}
		}

		[Rpc(SendTo.NotServer)]
		private void ClientStartupFailedRpc(byte failureCode, byte failureStage, string reason)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(213068404u, rpcParams, attributeParams, SendTo.NotServer, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in failureCode, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in failureStage, default(FastBufferWriter.ForPrimitives));
				bool value = reason != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(reason);
				}
				__endSendRpc(ref bufferWriter, 213068404u, rpcParams, attributeParams, SendTo.NotServer, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				MultiplayerFailure failure = MultiplayerFailure.Create((MultiplayerFailureCode)failureCode, (MultiplayerFailureStage)failureStage, reason);
				if (Singleton<GameSessionLifecycle>.Instance != null)
				{
					Singleton<GameSessionLifecycle>.Instance.ReturnToMainMenu(ReturnToMainMenuRequest.StartupFailure(failure));
				}
				else
				{
					UtilityMethods.Log("<b><color=#FF4040>[Multiplayer]</color></b> Client startup failure return failed. GameSessionLifecycle is null.\n   Reason: " + reason, LogType.Error);
				}
			}
		}

		protected override void __initializeVariables()
		{
			if (_started == null)
			{
				throw new Exception("GameStartGate._started cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_started.Initialize(this);
			__nameNetworkVariable(_started, "_started");
			NetworkVariableFields.Add(_started);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(4158074654u, __rpc_handler_4158074654, "ServerReportReadyServerRpc", RpcInvokePermission.Everyone);
			__registerRpc(213068404u, __rpc_handler_213068404, "ClientStartupFailedRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_4158074654(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((GameStartGate)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GameStartGate)target).ServerReportReadyServerRpc(ext);
				((GameStartGate)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_213068404(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				string s = null;
				if (value3)
				{
					reader.ReadValueSafe(out s, false);
				}
				((GameStartGate)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GameStartGate)target).ClientStartupFailedRpc(value, value2, s);
				((GameStartGate)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "GameStartGate";
		}
	}
}
