using System;
using System.Collections;
using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Streaming;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using SDQQ1234;
using Unity.Netcode;
using UnityEngine;
using UnityServiceLocator;
using UnityUtils;

namespace PaperPixelGames.Core.Ocean
{
	[RequireComponent(typeof(Collider))]
	public sealed class SeaLayer : NetworkBehaviour
	{
		[Header("Identity")]
		[SerializeField]
		private byte _index;

		[SerializeField]
		private string _displayName;

		[Header("Diving Equipment Requirement")]
		[SerializeField]
		private bool _hasDivingEquipmentLevelRequirement;

		[SerializeField]
		private byte _requiredDivingEquipmentLevelIndex;

		[Header("Areas")]
		[SerializeField]
		private List<SeaArea> _seaAreas = new List<SeaArea>();

		[Header("Spawn")]
		[SerializeField]
		private List<SpawnableCreature> _spawnableCreatures = new List<SpawnableCreature>();

		[SerializeField]
		[Min(0f)]
		private float _spawnInterval = 0.1f;

		[Header("Trap Spawn")]
		[SerializeField]
		private OctopusTrap _octopusTrapPrefab;

		[SerializeField]
		private JellyfishTrap _jellyfishTrapPrefab;

		[SerializeField]
		private List<Transform> _octopusTrapSpawnPositions = new List<Transform>();

		[SerializeField]
		private List<Transform> _jellyfishTrapSpawnPositions = new List<Transform>();

		[Header("Activity")]
		[SerializeField]
		[Min(0f)]
		private float _serverDeactivationDelay = 1f;

		[SerializeField]
		[Min(1f)]
		private float _layerRetreatSpeedMultiplier = 3f;

		[SerializeField]
		[Min(0.5f)]
		private float _minimumLayerRetreatTimeout = 2f;

		[SerializeField]
		[Min(1f)]
		private float _maximumLayerRetreatTimeout = 8f;

		[Header("Local Presentation")]
		[SerializeField]
		[Min(0f)]
		private float _localSimulationPrewarmDuration = 0.35f;

		[SerializeField]
		[Min(0f)]
		private float _localCreatureStaggerDuration = 0.75f;

		[SerializeField]
		[Min(0.01f)]
		private float _localCreatureRevealDuration = 0.35f;

		[SerializeField]
		[Min(0f)]
		private float _localSpawnedCreatureRevealSpacing = 0.08f;

		[SerializeField]
		[Min(0.01f)]
		private float _localBoidsRevealDuration = 1.25f;

		[SerializeField]
		private AnimationCurve _localRevealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

		[Header("Local Feedback")]
		[SerializeField]
		[Range(0f, 1f)]
		private float _localAlertCenterBand = 0.65f;

		[SerializeField]
		[Min(0.05f)]
		private float _localAlertPositionCheckInterval = 0.2f;

		[SerializeField]
		private List<CS_BoidsVFX> _boids = new List<CS_BoidsVFX>();

		[SerializeField]
		private List<string> _dressingSceneNames = new List<string>();

		[Header("Dead Spawn Free Fall")]
		[SerializeField]
		[Min(0f)]
		private float _deadSpawnFreeFallHorizontalDrift = 0.08f;

		[SerializeField]
		[Min(0f)]
		private float _deadSpawnFreeFallTorque = 0.2f;

		[SerializeField]
		[Min(0.1f)]
		private float _deadSpawnFreeFallMaxDuration = 30f;

		[Header("Debug")]
		[SerializeField]
		private bool _log;

		private readonly HashSet<ulong> _clientsInLayer = new HashSet<ulong>();

		private readonly HashSet<ulong> _activeServerClients = new HashSet<ulong>();

		private readonly Dictionary<ulong, HashSet<int>> _localColliderIdsByClient = new Dictionary<ulong, HashSet<int>>();

		private readonly Dictionary<ulong, HashSet<int>> _serverColliderIdsByClient = new Dictionary<ulong, HashSet<int>>();

		private readonly HashSet<ulong> _serverReportedClientsInLayer = new HashSet<ulong>();

		private readonly Dictionary<byte, SeaArea> _seaAreaByIndex = new Dictionary<byte, SeaArea>();

		private readonly HashSet<byte> _invalidSeaAreaIndices = new HashSet<byte>();

		private Collider _triggerCollider;

		private readonly List<NetworkObject> _serverSpawnedDeadFish = new List<NetworkObject>();

		private readonly List<OctopusTrap> _serverSpawnedOctopusTraps = new List<OctopusTrap>();

		private readonly List<JellyfishTrap> _serverSpawnedJellyfishTraps = new List<JellyfishTrap>();

		private readonly List<SeaCreature> _serverRetreatParticipants = new List<SeaCreature>();

		private Coroutine _spawnRoutine;

		private Coroutine _serverDeactivateRoutine;

		private Coroutine _serverRetreatRoutine;

		private Coroutine _localAlertRoutine;

		private bool _isDressingLoaded;

		private bool _isSpawnRoutineRunning;

		private bool _serverSimulationActive;

		private bool _serverRetreatActive;

		private bool _isServerResettingForNewDay;

		private LayerSpawnState _spawnState;

		[Header("Editor Creature Rotation")]
		[SerializeField]
		private ItemDatabaseSO _editorItemDatabase;

		[SerializeField]
		[Min(0f)]
		private int _editorCreatureBatchIndex;

		public byte Index => _index;

		public string DisplayName
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(_displayName))
				{
					return _displayName;
				}
				return base.gameObject.name;
			}
		}

		public bool HasDivingEquipmentLevelRequirement => _hasDivingEquipmentLevelRequirement;

		public byte RequiredDivingEquipmentLevelIndex => _requiredDivingEquipmentLevelIndex;

		public LayerSpawnState SpawnState => _spawnState;

		public bool HasServerOccupants => _clientsInLayer.Count > 0;

		public bool HasServerActiveOccupants => _activeServerClients.Count > 0;

		public bool HasLocalOccupants => _localColliderIdsByClient.Count > 0;

		public IReadOnlyList<SeaArea> SeaAreas => _seaAreas;

		public IReadOnlyList<SpawnableCreature> SpawnableCreatures => _spawnableCreatures;

		public float LocalSimulationPrewarmDuration => _localSimulationPrewarmDuration;

		public float LocalCreatureStaggerDuration => _localCreatureStaggerDuration;

		public float LocalCreatureRevealDuration => _localCreatureRevealDuration;

		public float LocalSpawnedCreatureRevealSpacing => _localSpawnedCreatureRevealSpacing;

		public float LocalBoidsRevealDuration => _localBoidsRevealDuration;

		public bool AppliesDivingEquipmentLevelPenalty(byte equipmentLevelIndex)
		{
			if (_hasDivingEquipmentLevelRequirement)
			{
				return equipmentLevelIndex < _requiredDivingEquipmentLevelIndex;
			}
			return false;
		}

		public bool TryGetWorldBounds(out Bounds bounds)
		{
			CacheCollider();
			if (_triggerCollider == null)
			{
				bounds = default;
				return false;
			}
			bounds = _triggerCollider.bounds;
			return true;
		}

		private void Awake()
		{
			CacheCollider();
			ValidateTriggerCollider();
			RebuildSeaAreaIndexCache();
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			RebuildSeaAreaIndexCache();
			RefreshSeaAreaPointCaches();
			_clientsInLayer.Clear();
			_activeServerClients.Clear();
			_localColliderIdsByClient.Clear();
			_serverColliderIdsByClient.Clear();
			_serverReportedClientsInLayer.Clear();
			_serverSpawnedDeadFish.Clear();
			_serverSpawnedOctopusTraps.Clear();
			_serverSpawnedJellyfishTraps.Clear();
			_spawnState = LayerSpawnState.NotSpawned;
			_isSpawnRoutineRunning = false;
			_serverSimulationActive = false;
			_serverRetreatActive = false;
			_serverRetreatParticipants.Clear();
			_isServerResettingForNewDay = false;
			if (base.IsServer && base.NetworkManager != null)
			{
				base.NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
			}
			SetLocalBoidsActive(active: false);
		}

		public override void OnNetworkDespawn()
		{
			if (base.IsServer && base.NetworkManager != null)
			{
				base.NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
			}
			if (SeaCreatureManager.TryGetActiveInstance(out var manager))
			{
				manager.RemoveLayerOccupancy(this);
			}
			StopSpawnRoutine();
			StopServerDeactivateRoutine();
			StopServerRetreatRoutine(cancelParticipants: true);
			StopLocalAlertRoutine();
			_clientsInLayer.Clear();
			_activeServerClients.Clear();
			_localColliderIdsByClient.Clear();
			_serverColliderIdsByClient.Clear();
			_serverReportedClientsInLayer.Clear();
			_isServerResettingForNewDay = false;
			if (base.IsServer)
			{
				ServerDespawnSpawnedDeadFishInLayer();
				ServerDespawnSpawnedTrapsInLayer();
				SetServerSimulationActive(active: false);
			}
			SetLocalBoidsActive(active: false);
			base.OnNetworkDespawn();
		}

		private void OnTriggerEnter(Collider other)
		{
			if (other.CompareTag("Player") && TryGetPlayer(other, out var player))
			{
				bool flag = HandleLocalEnter(player, other);
				if (base.IsServer)
				{
					HandleServerColliderEnter(player, other);
				}
				else if (player.IsOwner & flag)
				{
					ReportLayerEnterToServer(player);
				}
			}
		}

		private void OnTriggerExit(Collider other)
		{
			if (other.CompareTag("Player") && TryGetPlayer(other, out var player))
			{
				bool flag = HandleLocalExit(player, other);
				if (base.IsServer)
				{
					HandleServerColliderExit(player, other);
				}
				else if (player.IsOwner & flag)
				{
					ReportLayerExitToServer(player);
				}
			}
		}

		private void ReportLayerEnterToServer(PPG_Player player)
		{
			if (!(player == null) && !(player.NetworkObject == null) && player.NetworkObject.IsSpawned)
			{
				ServerReportLayerEnterRpc(player.NetworkObject.ToReference());
			}
		}

		private void ReportLayerExitToServer(PPG_Player player)
		{
			if (!(player == null) && !(player.NetworkObject == null) && player.NetworkObject.IsSpawned)
			{
				ServerReportLayerExitRpc(player.NetworkObject.ToReference());
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerReportLayerEnterRpc(NetworkObjectReference playerReference, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3636578520u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in playerReference, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3636578520u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && TryResolveReportedPlayer(playerReference, rpcParams, out var player))
				{
					HandleServerReportedEnter(player);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerReportLayerExitRpc(NetworkObjectReference playerReference, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3189819861u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in playerReference, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3189819861u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && TryResolveReportedPlayer(playerReference, rpcParams, out var player))
				{
					HandleServerReportedExit(player);
				}
			}
		}

		private bool TryResolveReportedPlayer(NetworkObjectReference playerReference, RpcParams rpcParams, out PPG_Player player)
		{
			player = null;
			if (!playerReference.TryGetComponent<PPG_Player>(out player))
			{
				Log("Layer report rejected. Player reference could not be resolved.", LogType.Warning);
				return false;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (player.OwnerClientId != senderClientId)
			{
				Log($"Layer report rejected. Sender={senderClientId} PlayerOwner={player.OwnerClientId}", LogType.Warning);
				return false;
			}
			return true;
		}

		private bool HandleLocalEnter(PPG_Player player, Collider collider)
		{
			if (player == null || !player.IsOwner || collider == null)
			{
				return false;
			}
			if (!AddColliderOccupancy(_localColliderIdsByClient, player.OwnerClientId, collider.GetInstanceID()))
			{
				return false;
			}
			if (SeaCreatureManager.TryGetActiveInstance(out var manager))
			{
				manager.RegisterLocalLayerOccupancy(this, player);
			}
			else
			{
				Log("Local enter could not be registered because SeaCreatureManager is unavailable.", LogType.Error);
			}
			Log("Local enter. Player=" + player.name);
			return true;
		}

		private void TryDiscoverHabitat(PPG_Player player)
		{
			if (!(player == null) && player.IsOwner)
			{
				if (!ServiceLocator.ForSceneOf(this).TryGet<TabletNetworkHandler>(out var service) || service == null)
				{
					Log("Habitat discovery skipped. TabletNetworkHandler not found.", LogType.Warning);
				}
				else if (service.IsServer)
				{
					service.ServerDiscoverHabitat(_index);
				}
				else
				{
					service.ServerDiscoverHabitatRpc(_index);
				}
			}
		}

		private bool HandleLocalExit(PPG_Player player, Collider collider)
		{
			if (player == null || !player.IsOwner || collider == null)
			{
				return false;
			}
			if (!RemoveColliderOccupancy(_localColliderIdsByClient, player.OwnerClientId, collider.GetInstanceID()))
			{
				return false;
			}
			if (SeaCreatureManager.TryGetActiveInstance(out var manager))
			{
				manager.UnregisterLocalLayerOccupancy(this, player);
			}
			else
			{
				Log("Local exit could not be registered because SeaCreatureManager is unavailable.", LogType.Error);
			}
			Log("Local exit. Player=" + player.name);
			return true;
		}

		public void SetLocalPrimaryActive(PPG_Player player, bool active)
		{
			if (!active)
			{
				StopLocalAlertRoutine();
				Log("Local primary deactivated. Player=" + ((player != null) ? player.name : "None"));
			}
			else if (!(player == null) && player.IsOwner)
			{
				TryLoadDressingScenes();
				StartLocalAlertRoutine(player);
				TryDiscoverHabitat(player);
				Log("Local primary activated. Player=" + player.name);
			}
		}

		private void StartLocalAlertRoutine(PPG_Player player)
		{
			StopLocalAlertRoutine();
			_localAlertRoutine = StartCoroutine(ShowLocalAlertNearCenterCoroutine(player));
		}

		private IEnumerator ShowLocalAlertNearCenterCoroutine(PPG_Player player)
		{
			CacheCollider();
			if (_triggerCollider == null)
			{
				Log("Local alert skipped because the layer trigger collider is unavailable.", LogType.Warning);
				_localAlertRoutine = null;
				yield break;
			}
			WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.05f, _localAlertPositionCheckInterval));
			while (player != null && player.IsOwner)
			{
				Bounds bounds = _triggerCollider.bounds;
				float num = bounds.extents.y * Mathf.Clamp01(_localAlertCenterBand);
				if (Mathf.Abs(player.transform.position.y - bounds.center.y) <= num && Singleton<NotifyManager>.Instance != null)
				{
					Singleton<NotifyManager>.Instance.ShowAlert(DisplayName, 2.5f, AlertPriority.Low);
					_localAlertRoutine = null;
					yield break;
				}
				yield return wait;
			}
			_localAlertRoutine = null;
		}

		public float EvaluateLocalReveal(float normalizedTime)
		{
			float num = Mathf.Clamp01(normalizedTime);
			if (_localRevealCurve == null)
			{
				return num;
			}
			return Mathf.Clamp01(_localRevealCurve.Evaluate(num));
		}

		public void BeginLocalPresentation()
		{
			if (_boids == null)
			{
				return;
			}
			for (int i = 0; i < _boids.Count; i++)
			{
				CS_BoidsVFX cS_BoidsVFX = _boids[i];
				if (!(cS_BoidsVFX == null))
				{
					cS_BoidsVFX.StartSimulation(0f);
				}
			}
		}

		public void SetLocalBoidsRevealProgress(float progress)
		{
			if (_boids == null)
			{
				return;
			}
			float revealProgress = Mathf.Clamp01(progress);
			for (int i = 0; i < _boids.Count; i++)
			{
				CS_BoidsVFX cS_BoidsVFX = _boids[i];
				if (!(cS_BoidsVFX == null))
				{
					cS_BoidsVFX.SetRevealProgress(revealProgress);
				}
			}
		}

		public void EndLocalPresentation()
		{
			SetLocalBoidsActive(active: false);
		}

		private void HandleServerColliderEnter(PPG_Player player, Collider collider)
		{
			if (base.IsServer && !(player == null) && !(collider == null))
			{
				AddColliderOccupancy(_serverColliderIdsByClient, player.OwnerClientId, collider.GetInstanceID());
				UpdateServerClientOccupancy(player);
			}
		}

		private void HandleServerColliderExit(PPG_Player player, Collider collider)
		{
			if (base.IsServer && !(player == null) && !(collider == null))
			{
				RemoveColliderOccupancy(_serverColliderIdsByClient, player.OwnerClientId, collider.GetInstanceID());
				UpdateServerClientOccupancy(player);
			}
		}

		private void HandleServerReportedEnter(PPG_Player player)
		{
			if (base.IsServer && !(player == null))
			{
				_serverReportedClientsInLayer.Add(player.OwnerClientId);
				UpdateServerClientOccupancy(player);
			}
		}

		private void HandleServerReportedExit(PPG_Player player)
		{
			if (base.IsServer && !(player == null))
			{
				_serverReportedClientsInLayer.Remove(player.OwnerClientId);
				UpdateServerClientOccupancy(player);
			}
		}

		private void UpdateServerClientOccupancy(PPG_Player player)
		{
			ulong ownerClientId = player.OwnerClientId;
			bool flag = HasColliderOccupancy(_serverColliderIdsByClient, ownerClientId) || _serverReportedClientsInLayer.Contains(ownerClientId);
			if ((!flag) ? _clientsInLayer.Remove(ownerClientId) : _clientsInLayer.Add(ownerClientId))
			{
				Log($"Server occupancy changed. ClientId={ownerClientId} InLayer={flag} Occupants={_clientsInLayer.Count}");
				if (SeaCreatureManager.TryGetActiveInstance(out var manager))
				{
					manager.SetServerLayerOccupancy(this, player, flag);
				}
				else
				{
					Log("Server occupancy could not be registered because SeaCreatureManager is unavailable.", LogType.Error);
				}
			}
		}

		public void SetServerClientActive(ulong clientId, bool active)
		{
			if (base.IsServer && (active ? _activeServerClients.Add(clientId) : _activeServerClients.Remove(clientId)))
			{
				Log($"Server active occupancy changed. ClientId={clientId} " + $"Active={active} ActiveOccupants={_activeServerClients.Count}");
				ServerRepairRuntimeState();
			}
		}

		public bool ClearLocalClientOccupancy(ulong clientId)
		{
			return _localColliderIdsByClient.Remove(clientId);
		}

		public bool ClearServerClientOccupancy(ulong clientId)
		{
			if (!base.IsServer)
			{
				return false;
			}
			bool num = _serverColliderIdsByClient.Remove(clientId) | _serverReportedClientsInLayer.Remove(clientId) | _clientsInLayer.Remove(clientId);
			bool flag = _activeServerClients.Remove(clientId);
			if (num | flag)
			{
				ServerRepairRuntimeState(forceImmediateDeactivate: true);
			}
			return num | flag;
		}

		private void ServerRepairRuntimeState(bool forceImmediateDeactivate = false)
		{
			if (base.IsServer && !_isServerResettingForNewDay)
			{
				if (_activeServerClients.Count > 0)
				{
					StopServerDeactivateRoutine();
					StopServerRetreatRoutine(cancelParticipants: true);
					EnsureSpawnStarted();
					SetServerSimulationActive(active: true);
				}
				else if (forceImmediateDeactivate)
				{
					StopServerDeactivateRoutine();
					StopServerRetreatRoutine(cancelParticipants: true);
					SetServerSimulationActive(active: false);
				}
				else
				{
					ScheduleServerSimulationDeactivate();
				}
			}
		}

		private void EnsureSpawnStarted()
		{
			if (base.IsServer && !_isServerResettingForNewDay && _spawnState != LayerSpawnState.Spawned && _spawnState != LayerSpawnState.Spawning && !_isSpawnRoutineRunning && _spawnRoutine == null)
			{
				_spawnState = LayerSpawnState.Spawning;
				_isSpawnRoutineRunning = true;
				Coroutine spawnRoutine = StartCoroutine(SpawnCreaturesCoroutine());
				if (_isSpawnRoutineRunning)
				{
					_spawnRoutine = spawnRoutine;
				}
			}
		}

		private IEnumerator SpawnCreaturesCoroutine()
		{
			bool completed = false;
			Exception failure = null;
			IEnumerator spawnRoutine = SpawnCreaturesInternalCoroutine();
			while (true)
			{
				object obj = null;
				bool flag;
				try
				{
					flag = spawnRoutine.MoveNext();
					if (flag)
					{
						obj = spawnRoutine.Current;
					}
				}
				catch (Exception ex)
				{
					failure = ex;
					break;
				}
				if (!flag)
				{
					completed = true;
					break;
				}
				yield return obj;
			}
			(spawnRoutine as IDisposable)?.Dispose();
			_spawnState = (completed ? LayerSpawnState.Spawned : (_isServerResettingForNewDay ? LayerSpawnState.ResetPending : LayerSpawnState.NotSpawned));
			SetServerSimulationActive(_activeServerClients.Count > 0);
			if (completed)
			{
				_spawnRoutine = null;
				_isSpawnRoutineRunning = false;
				Log("Spawn completed.");
				yield break;
			}
			Log("Spawn interrupted. Scheduling retry while occupants remain in layer. Error=" + (failure?.Message ?? "Unknown"), LogType.Warning);
			if (_isServerResettingForNewDay || _activeServerClients.Count == 0)
			{
				_spawnRoutine = null;
				_isSpawnRoutineRunning = false;
				yield break;
			}
			yield return new WaitForSeconds(1f);
			_spawnRoutine = null;
			_isSpawnRoutineRunning = false;
			ServerRepairRuntimeState();
		}

		private IEnumerator SpawnCreaturesInternalCoroutine()
		{
			WaitForSeconds wait = ((_spawnInterval > 0f) ? new WaitForSeconds(_spawnInterval) : null);
			SeaCreatureManager manager = Singleton<SeaCreatureManager>.Instance;
			if (manager == null)
			{
				throw new InvalidOperationException("SeaCreatureManager.Instance is null.");
			}
			if (_spawnableCreatures == null)
			{
				ServerSpawnLayerTraps();
				yield break;
			}
			ServerSpawnLayerTraps();
			for (int i = 0; i < _spawnableCreatures.Count; i++)
			{
				SpawnableCreature spawnable = _spawnableCreatures[i];
				if (spawnable == null || spawnable.CreatureData == null || spawnable.Count <= 0)
				{
					continue;
				}
				int num = (spawnable.DeadSpawn ? CountExistingDeadFish(spawnable.CreatureData) : CountExistingCreatures(manager, spawnable.CreatureData));
				int missingCount = spawnable.Count - num;
				if (missingCount <= 0)
				{
					continue;
				}
				for (int spawnIndex = 0; spawnIndex < missingCount; spawnIndex++)
				{
					if (spawnable.DeadSpawn)
					{
						ServerSpawnDeadFish(manager, spawnable);
					}
					else
					{
						manager.SpawnCreature(spawnable.CreatureData, this, spawnable.PreferredArea);
					}
					if (wait != null)
					{
						yield return wait;
					}
					else
					{
						yield return null;
					}
				}
			}
		}

		private void ServerSpawnLayerTraps()
		{
			if (base.IsServer)
			{
				ServerSpawnTrapList(_octopusTrapPrefab, _octopusTrapSpawnPositions, _serverSpawnedOctopusTraps, "octopus");
				ServerSpawnTrapList(_jellyfishTrapPrefab, _jellyfishTrapSpawnPositions, _serverSpawnedJellyfishTraps, "jellyfish");
			}
		}

		private void ServerSpawnTrapList<TTrap>(TTrap prefab, List<Transform> spawnPositions, List<TTrap> spawnedTraps, string trapName) where TTrap : NetworkBehaviour
		{
			if (!base.IsServer || prefab == null || spawnPositions == null || spawnPositions.Count == 0)
			{
				return;
			}
			RemoveInvalidSpawnedTraps(spawnedTraps);
			for (int i = spawnedTraps.Count; i < spawnPositions.Count; i++)
			{
				Transform transform = spawnPositions[i];
				if (transform == null)
				{
					continue;
				}
				TTrap val = UnityEngine.Object.Instantiate(prefab, transform.position, transform.rotation);
				if (!(val == null))
				{
					NetworkObject networkObject = val.NetworkObject;
					if (networkObject == null)
					{
						Log("Trap spawn failed. " + trapName + " prefab does not have a NetworkObject.", LogType.Error);
						UnityEngine.Object.Destroy(val.gameObject);
					}
					else
					{
						networkObject.Spawn(destroyWithScene: true);
						spawnedTraps.Add(val);
						RegisterSpawnedTrapWithManager(val);
					}
				}
			}
		}

		private void RegisterSpawnedTrapWithManager<TTrap>(TTrap trap) where TTrap : NetworkBehaviour
		{
			if (!(trap == null))
			{
				SeaCreatureManager instance = Singleton<SeaCreatureManager>.Instance;
				if (instance == null)
				{
					Log("Spawned trap could not be registered because SeaCreatureManager.Instance is null. Trap=" + trap.name, LogType.Warning);
				}
				else if (trap is OctopusTrap octopusTrap)
				{
					instance.RegisterOctopusTrap(octopusTrap);
				}
				else if (trap is JellyfishTrap jellyfishTrap)
				{
					instance.RegisterJellyfishTrap(jellyfishTrap);
				}
			}
		}

		private static void RemoveInvalidSpawnedTraps<TTrap>(List<TTrap> spawnedTraps) where TTrap : NetworkBehaviour
		{
			if (spawnedTraps == null)
			{
				return;
			}
			for (int num = spawnedTraps.Count - 1; num >= 0; num--)
			{
				TTrap val = spawnedTraps[num];
				if (val == null || val.NetworkObject == null || !val.NetworkObject.IsSpawned)
				{
					spawnedTraps.RemoveAt(num);
				}
			}
		}

		private int CountExistingCreatures(SeaCreatureManager manager, CreatureSO creatureSO)
		{
			if (manager == null || creatureSO == null)
			{
				return 0;
			}
			IReadOnlyList<SeaCreature> creaturesInLayer = manager.GetCreaturesInLayer(_index);
			if (creaturesInLayer == null || creaturesInLayer.Count == 0)
			{
				return 0;
			}
			int num = 0;
			string text = ((creatureSO != null) ? creatureSO.Id : string.Empty);
			for (int i = 0; i < creaturesInLayer.Count; i++)
			{
				SeaCreature seaCreature = creaturesInLayer[i];
				if (!(seaCreature == null) && seaCreature.IsSpawned)
				{
					if (seaCreature.CreatureSO == creatureSO)
					{
						num++;
					}
					else if (!string.IsNullOrWhiteSpace(text) && seaCreature.CreatureId == text)
					{
						num++;
					}
				}
			}
			return num;
		}

		private int CountExistingDeadFish(CreatureSO creatureSO)
		{
			if (creatureSO == null || _serverSpawnedDeadFish.Count == 0)
			{
				return 0;
			}
			int num = 0;
			string id = creatureSO.Id;
			for (int num2 = _serverSpawnedDeadFish.Count - 1; num2 >= 0; num2--)
			{
				NetworkObject networkObject = _serverSpawnedDeadFish[num2];
				DeadFish component;
				if (networkObject == null || !networkObject.IsSpawned)
				{
					_serverSpawnedDeadFish.RemoveAt(num2);
				}
				else if (networkObject.TryGetComponent<DeadFish>(out component) && component.FishId.Value.ToString() == id)
				{
					num++;
				}
			}
			return num;
		}

		private DeadFish ServerSpawnDeadFish(SeaCreatureManager manager, SpawnableCreature spawnable)
		{
			if (!base.IsServer)
			{
				return null;
			}
			if (manager == null)
			{
				Log("Dead fish spawn failed. SeaCreatureManager is null.", LogType.Warning);
				return null;
			}
			if (spawnable == null || spawnable.CreatureData == null)
			{
				Log("Dead fish spawn failed. Spawnable or CreatureData is null.", LogType.Warning);
				return null;
			}
			if (!(spawnable.CreatureData is FishSO fishSO))
			{
				Log("Dead fish spawn skipped. CreatureData is not FishSO. Creature=" + spawnable.CreatureData.name, LogType.Warning);
				return null;
			}
			if (fishSO.DeadPrefab == null)
			{
				Log("Dead fish spawn skipped. DeadPrefab is null for " + fishSO.name + ".", LogType.Warning);
				return null;
			}
			if (!TryGetDeadSpawnSourcePoint(spawnable, out var spawnArea, out var pointIndex, out var sourcePosition))
			{
				return null;
			}
			DeadFish deadFish = manager.SpawnWorldDeadFish(fishSO, sourcePosition, Quaternion.identity);
			if (deadFish == null)
			{
				return null;
			}
			Vector3 torqueImpulse = UnityEngine.Random.onUnitSphere * _deadSpawnFreeFallTorque;
			int num = LayerMask.NameToLayer("BoidsCollider");
			int excludeLayerMaskValue = ((num >= 0) ? (1 << num) : 0);
			float f = UnityEngine.Random.Range(0f, MathF.PI * 2f);
			Vector3 normalized = new Vector3(Mathf.Cos(f) * _deadSpawnFreeFallHorizontalDrift, -1f, Mathf.Sin(f) * _deadSpawnFreeFallHorizontalDrift).normalized;
			if (!deadFish.ServerStartWorldFreeFall(sourcePosition, normalized, GetDeadSpawnSimulationClientId(), startedUnderwater: true, excludeLayerMaskValue, torqueImpulse, _deadSpawnFreeFallMaxDuration))
			{
				NetworkObject networkObject = deadFish.NetworkObject;
				if (networkObject != null && networkObject.IsSpawned)
				{
					networkObject.Despawn();
				}
				else
				{
					UnityEngine.Object.Destroy(deadFish.gameObject);
				}
				Log("Dead fish spawn skipped. Free fall could not start. Fish=" + fishSO.name + " " + $"Area={spawnArea.name} PointIndex={pointIndex} Source={sourcePosition}", LogType.Warning);
				return null;
			}
			if (deadFish.NetworkObject != null)
			{
				_serverSpawnedDeadFish.Add(deadFish.NetworkObject);
			}
			Log("Dead fish spawned. Fish=" + fishSO.name + " " + $"Area={spawnArea.name} PointIndex={pointIndex} " + $"Source={sourcePosition} FreeFallMaxDuration={_deadSpawnFreeFallMaxDuration}");
			return deadFish;
		}

		private bool TryGetDeadSpawnSourcePoint(SpawnableCreature spawnable, out SeaArea spawnArea, out byte pointIndex, out Vector3 sourcePosition)
		{
			spawnArea = null;
			pointIndex = 0;
			sourcePosition = default;
			if (spawnable == null)
			{
				return false;
			}
			spawnArea = ((spawnable.PreferredArea != null) ? spawnable.PreferredArea : GetRandomSeaArea());
			if (spawnArea == null)
			{
				Log("Dead fish spawn failed. SeaArea could not be resolved.", LogType.Warning);
				return false;
			}
			pointIndex = spawnArea.GetRandomPointIndex();
			sourcePosition = spawnArea.GetPointByIndex(pointIndex);
			return true;
		}

		private ulong GetDeadSpawnSimulationClientId()
		{
			if (base.NetworkManager != null)
			{
				foreach (ulong activeServerClient in _activeServerClients)
				{
					if (base.NetworkManager.ConnectedClients.ContainsKey(activeServerClient))
					{
						return activeServerClient;
					}
				}
				using IEnumerator<ulong> enumerator2 = base.NetworkManager.ConnectedClientsIds.GetEnumerator();
				if (enumerator2.MoveNext())
				{
					return enumerator2.Current;
				}
			}
			if (!(base.NetworkManager != null))
			{
				return 0uL;
			}
			return 0uL;
		}

		private void SetServerSimulationActive(bool active)
		{
			if (base.IsServer)
			{
				bool num = _serverSimulationActive != active;
				_serverSimulationActive = active;
				if (Singleton<SeaCreatureManager>.Instance != null)
				{
					Singleton<SeaCreatureManager>.Instance.SetLayerSimulationActive(_index, active);
				}
				else if (active)
				{
					Log("Simulation active state could not be applied because SeaCreatureManager.Instance is null.", LogType.Warning);
				}
				if (num)
				{
					Log($"Server simulation active={active}");
				}
			}
		}

		private void ScheduleServerSimulationDeactivate()
		{
			if (!base.IsServer)
			{
				return;
			}
			StopServerDeactivateRoutine();
			if (!_serverRetreatActive && !TryStartServerLayerRetreat())
			{
				if (_serverDeactivationDelay <= 0f)
				{
					SetServerSimulationActive(active: false);
				}
				else
				{
					_serverDeactivateRoutine = StartCoroutine(ServerDeactivateAfterDelayCoroutine());
				}
			}
		}

		private IEnumerator ServerDeactivateAfterDelayCoroutine()
		{
			yield return new WaitForSeconds(_serverDeactivationDelay);
			_serverDeactivateRoutine = null;
			if (base.IsServer && _activeServerClients.Count <= 0)
			{
				SetServerSimulationActive(active: false);
			}
		}

		private bool TryStartServerLayerRetreat()
		{
			if (!base.IsServer || _activeServerClients.Count > 0)
			{
				return false;
			}
			SeaCreatureManager instance = Singleton<SeaCreatureManager>.Instance;
			if (instance == null)
			{
				return false;
			}
			int num = instance.ServerBeginLayerRetreat(_index, _layerRetreatSpeedMultiplier, _serverRetreatParticipants, out var maximumEstimatedDuration);
			if (num <= 0)
			{
				_serverRetreatParticipants.Clear();
				return false;
			}
			float num2 = Mathf.Max(0.5f, _minimumLayerRetreatTimeout);
			float max = Mathf.Max(num2, _maximumLayerRetreatTimeout);
			float num3 = Mathf.Clamp(maximumEstimatedDuration * 2f + 0.5f, num2, max);
			_serverRetreatActive = true;
			_serverRetreatRoutine = StartCoroutine(ServerLayerRetreatCoroutine(num3));
			Log($"Server layer retreat scheduled. Participants={num} " + $"Timeout={num3:0.00}s");
			return true;
		}

		private IEnumerator ServerLayerRetreatCoroutine(float timeout)
		{
			float elapsed = 0f;
			bool completed = false;
			while (elapsed < timeout && base.IsServer && _activeServerClients.Count <= 0)
			{
				SeaCreatureManager instance = Singleton<SeaCreatureManager>.Instance;
				if (instance == null || instance.ServerAreLayerRetreatsComplete(_serverRetreatParticipants))
				{
					completed = true;
					break;
				}
				elapsed += Time.deltaTime;
				yield return null;
			}
			_serverRetreatRoutine = null;
			if (!base.IsServer || _activeServerClients.Count > 0)
			{
				StopServerRetreatRoutine(cancelParticipants: true);
				yield break;
			}
			SeaCreatureManager instance2 = Singleton<SeaCreatureManager>.Instance;
			if (!completed && instance2 != null)
			{
				instance2.ServerForceCompleteLayerRetreats(_serverRetreatParticipants);
				Log($"Server layer retreat timed out. Participants={_serverRetreatParticipants.Count}", LogType.Warning);
			}
			_serverRetreatActive = false;
			SetServerSimulationActive(active: false);
			_serverRetreatParticipants.Clear();
		}

		private void StopServerRetreatRoutine(bool cancelParticipants)
		{
			if (_serverRetreatRoutine != null)
			{
				StopCoroutine(_serverRetreatRoutine);
				_serverRetreatRoutine = null;
			}
			if (cancelParticipants && _serverRetreatParticipants.Count > 0 && SeaCreatureManager.TryGetActiveInstance(out var manager))
			{
				manager.ServerCancelLayerRetreats(_serverRetreatParticipants);
			}
			else
			{
				_serverRetreatParticipants.Clear();
			}
			_serverRetreatActive = false;
		}

		public void ResetSpawnState()
		{
			if (base.IsServer)
			{
				StopSpawnRoutine();
				StopServerDeactivateRoutine();
				StopServerRetreatRoutine(cancelParticipants: true);
				_spawnState = LayerSpawnState.NotSpawned;
				if (_activeServerClients.Count > 0)
				{
					_spawnState = LayerSpawnState.ResetPending;
					ServerRepairRuntimeState();
				}
				else
				{
					SetServerSimulationActive(active: false);
				}
				Log($"Spawn state reset. NewState={_spawnState}");
			}
		}

		public void ServerResetForNewDay()
		{
			if (base.IsServer)
			{
				if (Singleton<SeaCreatureManager>.Instance == null)
				{
					Log("ServerResetForNewDay failed. SeaCreatureManager.Instance is null.", LogType.Warning);
				}
				else
				{
					Singleton<SeaCreatureManager>.Instance.EndDayReset();
				}
			}
		}

		public void ServerBeginEndDayReset()
		{
			if (base.IsServer)
			{
				_isServerResettingForNewDay = true;
				StopSpawnRoutine();
				StopServerDeactivateRoutine();
				StopServerRetreatRoutine(cancelParticipants: true);
				_clientsInLayer.Clear();
				_activeServerClients.Clear();
				_serverColliderIdsByClient.Clear();
				_serverReportedClientsInLayer.Clear();
				_spawnState = LayerSpawnState.ResetPending;
				CacheCollider();
				ValidateTriggerCollider();
				RebuildSeaAreaIndexCache();
				RefreshSeaAreaPointCaches();
				SetServerSimulationActive(active: false);
				Log("Begin end-day reset.");
			}
		}

		public void ServerCompleteEndDayReset()
		{
			if (base.IsServer)
			{
				_spawnState = LayerSpawnState.NotSpawned;
				_isServerResettingForNewDay = false;
				ClientResetForNewDayRpc();
				Log("New day reset completed.");
			}
		}

		public void ServerCollectSpawnedDeadFish(List<NetworkObject> results, HashSet<NetworkObject> uniqueObjects)
		{
			if (!base.IsServer || results == null || uniqueObjects == null || _serverSpawnedDeadFish.Count == 0)
			{
				return;
			}
			int num = 0;
			for (int num2 = _serverSpawnedDeadFish.Count - 1; num2 >= 0; num2--)
			{
				NetworkObject networkObject = _serverSpawnedDeadFish[num2];
				if (networkObject == null)
				{
					_serverSpawnedDeadFish.RemoveAt(num2);
				}
				else
				{
					if (uniqueObjects.Add(networkObject))
					{
						results.Add(networkObject);
						num++;
					}
					_serverSpawnedDeadFish.RemoveAt(num2);
				}
			}
			Log($"Collected spawned dead fish for end-day despawn. Count={num}");
		}

		private void ServerDespawnSpawnedDeadFishInLayer()
		{
			if (!base.IsServer || _serverSpawnedDeadFish.Count == 0)
			{
				return;
			}
			int num = 0;
			for (int num2 = _serverSpawnedDeadFish.Count - 1; num2 >= 0; num2--)
			{
				NetworkObject networkObject = _serverSpawnedDeadFish[num2];
				if (networkObject == null)
				{
					_serverSpawnedDeadFish.RemoveAt(num2);
				}
				else
				{
					if (networkObject.IsSpawned)
					{
						networkObject.Despawn();
						num++;
					}
					else if (networkObject.gameObject != null)
					{
						UnityEngine.Object.Destroy(networkObject.gameObject);
						num++;
					}
					_serverSpawnedDeadFish.RemoveAt(num2);
				}
			}
			Log($"Spawned dead fish despawned. Count={num}");
		}

		private void ServerDespawnSpawnedTrapsInLayer()
		{
			if (base.IsServer)
			{
				ServerDespawnSpawnedTrapList(_serverSpawnedOctopusTraps);
				ServerDespawnSpawnedTrapList(_serverSpawnedJellyfishTraps);
			}
		}

		private void ServerDespawnSpawnedTrapList<TTrap>(List<TTrap> spawnedTraps) where TTrap : NetworkBehaviour
		{
			if (spawnedTraps == null || spawnedTraps.Count == 0)
			{
				return;
			}
			for (int num = spawnedTraps.Count - 1; num >= 0; num--)
			{
				TTrap val = spawnedTraps[num];
				if (val == null)
				{
					spawnedTraps.RemoveAt(num);
				}
				else
				{
					UnregisterSpawnedTrapFromManager(val);
					NetworkObject networkObject = val.NetworkObject;
					if (networkObject != null && networkObject.IsSpawned)
					{
						networkObject.Despawn();
					}
					else if (val.gameObject != null)
					{
						UnityEngine.Object.Destroy(val.gameObject);
					}
					spawnedTraps.RemoveAt(num);
				}
			}
		}

		private void UnregisterSpawnedTrapFromManager<TTrap>(TTrap trap) where TTrap : NetworkBehaviour
		{
			if (trap == null)
			{
				return;
			}
			SeaCreatureManager instance = Singleton<SeaCreatureManager>.Instance;
			if (!(instance == null))
			{
				if (trap is OctopusTrap octopusTrap)
				{
					instance.UnregisterOctopusTrap(octopusTrap);
				}
				else if (trap is JellyfishTrap jellyfishTrap)
				{
					instance.UnregisterJellyfishTrap(jellyfishTrap);
				}
			}
		}

		private void RefreshSeaAreaPointCaches()
		{
			if (_seaAreas == null)
			{
				return;
			}
			for (int i = 0; i < _seaAreas.Count; i++)
			{
				SeaArea seaArea = _seaAreas[i];
				if (!(seaArea == null))
				{
					seaArea.RefreshPointsCache();
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientResetForNewDayRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1214430128u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1214430128u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_localColliderIdsByClient.Clear();
				SetLocalBoidsActive(active: false);
				if (SeaCreatureManager.TryGetActiveInstance(out var manager))
				{
					manager.ClearLocalLayerOccupancy();
				}
				Log("Client new day reset completed.");
			}
		}

		public SeaArea GetRandomSeaArea()
		{
			if (_seaAreas == null || _seaAreas.Count == 0)
			{
				return null;
			}
			return _seaAreas.GetRandomElement();
		}

		public bool ContainsSeaArea(SeaArea area)
		{
			if (area != null && _seaAreas != null)
			{
				return _seaAreas.Contains(area);
			}
			return false;
		}

		public SeaArea GetClosestSeaArea(Vector3 worldPosition)
		{
			if (_seaAreas == null || _seaAreas.Count == 0)
			{
				return null;
			}
			SeaArea result = null;
			float num = float.PositiveInfinity;
			for (int i = 0; i < _seaAreas.Count; i++)
			{
				SeaArea seaArea = _seaAreas[i];
				if (!(seaArea == null))
				{
					float sqrMagnitude = (seaArea.GetClosestPointTo(worldPosition) - worldPosition).sqrMagnitude;
					if (!(sqrMagnitude >= num))
					{
						result = seaArea;
						num = sqrMagnitude;
					}
				}
			}
			return result;
		}

		public SeaArea GetSeaAreaByIndex(byte index)
		{
			if (_invalidSeaAreaIndices.Contains(index))
			{
				Log($"SeaArea index {index} is invalid because duplicates exist.", LogType.Error);
				return null;
			}
			if (!_seaAreaByIndex.TryGetValue(index, out var value))
			{
				return null;
			}
			return value;
		}

		private void RebuildSeaAreaIndexCache()
		{
			_seaAreaByIndex.Clear();
			_invalidSeaAreaIndices.Clear();
			if (_seaAreas == null)
			{
				return;
			}
			for (int i = 0; i < _seaAreas.Count; i++)
			{
				SeaArea seaArea = _seaAreas[i];
				if (seaArea == null)
				{
					continue;
				}
				byte index = seaArea.Index;
				if (!_invalidSeaAreaIndices.Contains(index))
				{
					if (_seaAreaByIndex.TryGetValue(index, out var value) && value != seaArea)
					{
						Log($"Duplicate SeaArea index detected. Index={index} Existing={value.name} Incoming={seaArea.name}", LogType.Error);
						_invalidSeaAreaIndices.Add(index);
						_seaAreaByIndex.Remove(index);
					}
					else
					{
						_seaAreaByIndex[index] = seaArea;
					}
				}
			}
		}

		private void TryLoadDressingScenes()
		{
			if (!_isDressingLoaded && _dressingSceneNames != null && _dressingSceneNames.Count != 0)
			{
				if (Singleton<DressingSceneManager>.Instance == null)
				{
					Log("DressingSceneManager.Instance is null.", LogType.Warning);
					return;
				}
				_isDressingLoaded = true;
				Singleton<DressingSceneManager>.Instance.LoadScenesAsync(_dressingSceneNames);
			}
		}

		private void SetLocalBoidsActive(bool active)
		{
			if (_boids == null)
			{
				return;
			}
			for (int i = 0; i < _boids.Count; i++)
			{
				CS_BoidsVFX cS_BoidsVFX = _boids[i];
				if (!(cS_BoidsVFX == null))
				{
					if (active)
					{
						cS_BoidsVFX.StartSimulation();
					}
					else
					{
						cS_BoidsVFX.StopSimulation();
					}
				}
			}
		}

		private void OnClientDisconnected(ulong clientId)
		{
			if (base.IsServer)
			{
				if (SeaCreatureManager.TryGetActiveInstance(out var manager))
				{
					manager.RemoveServerClientOccupancy(clientId);
				}
				_serverColliderIdsByClient.Remove(clientId);
				_serverReportedClientsInLayer.Remove(clientId);
				bool num = _clientsInLayer.Remove(clientId);
				bool flag = _activeServerClients.Remove(clientId);
				if (num)
				{
					Log($"Client disconnected. ClientId={clientId} Occupants={_clientsInLayer.Count}");
				}
				else
				{
					Log($"Client disconnected but was not registered in this layer. ClientId={clientId} Occupants={_clientsInLayer.Count}");
				}
				if (flag)
				{
					ServerRepairRuntimeState();
				}
			}
		}

		private static bool AddColliderOccupancy(Dictionary<ulong, HashSet<int>> colliderIdsByClient, ulong clientId, int colliderId)
		{
			if (!colliderIdsByClient.TryGetValue(clientId, out var value))
			{
				value = (colliderIdsByClient[clientId] = new HashSet<int>());
			}
			bool num = value.Count == 0;
			value.Add(colliderId);
			if (num)
			{
				return value.Count > 0;
			}
			return false;
		}

		private static bool RemoveColliderOccupancy(Dictionary<ulong, HashSet<int>> colliderIdsByClient, ulong clientId, int colliderId)
		{
			if (!colliderIdsByClient.TryGetValue(clientId, out var value))
			{
				return false;
			}
			if (!value.Remove(colliderId))
			{
				return false;
			}
			if (value.Count > 0)
			{
				return false;
			}
			colliderIdsByClient.Remove(clientId);
			return true;
		}

		private static bool HasColliderOccupancy(Dictionary<ulong, HashSet<int>> colliderIdsByClient, ulong clientId)
		{
			if (colliderIdsByClient.TryGetValue(clientId, out var value))
			{
				return value.Count > 0;
			}
			return false;
		}

		private static bool TryGetPlayer(Collider other, out PPG_Player player)
		{
			player = null;
			if (other == null)
			{
				return false;
			}
			if (!other.CompareTag("Player"))
			{
				return false;
			}
			player = other.GetComponentInParent<PPG_Player>();
			return player != null;
		}

		private void StopSpawnRoutine()
		{
			if (_spawnRoutine != null)
			{
				StopCoroutine(_spawnRoutine);
			}
			_spawnRoutine = null;
			_isSpawnRoutineRunning = false;
		}

		private void StopServerDeactivateRoutine()
		{
			if (_serverDeactivateRoutine != null)
			{
				StopCoroutine(_serverDeactivateRoutine);
				_serverDeactivateRoutine = null;
			}
		}

		private void StopLocalAlertRoutine()
		{
			if (_localAlertRoutine != null)
			{
				StopCoroutine(_localAlertRoutine);
				_localAlertRoutine = null;
			}
		}

		private void CacheCollider()
		{
			if (!(_triggerCollider != null))
			{
				TryGetComponent<Collider>(out _triggerCollider);
			}
		}

		private void ValidateTriggerCollider()
		{
			if (!(_triggerCollider == null) && !_triggerCollider.isTrigger)
			{
				_triggerCollider.isTrigger = true;
			}
		}

		private void Log(string message, LogType type = LogType.Log)
		{
			if (_log)
			{
				UtilityMethods.Log("[SeaLayer:" + DisplayName + "] " + message, type);
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3636578520u, __rpc_handler_3636578520, "ServerReportLayerEnterRpc", RpcInvokePermission.Everyone);
			__registerRpc(3189819861u, __rpc_handler_3189819861, "ServerReportLayerExitRpc", RpcInvokePermission.Everyone);
			__registerRpc(1214430128u, __rpc_handler_1214430128, "ClientResetForNewDayRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3636578520(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((SeaLayer)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SeaLayer)target).ServerReportLayerEnterRpc(value, ext);
				((SeaLayer)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3189819861(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((SeaLayer)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SeaLayer)target).ServerReportLayerExitRpc(value, ext);
				((SeaLayer)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1214430128(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((SeaLayer)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SeaLayer)target).ClientResetForNewDayRpc();
				((SeaLayer)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "SeaLayer";
		}
	}
}
