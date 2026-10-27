using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nav3D.API;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public sealed class SeaCreatureManager : Singleton<SeaCreatureManager>, ISeaCreatureResolver, ISeaCreatureRegistry
	{
		private sealed class LocalCreaturePresentationEntry
		{
			public SeaCreature Creature;

			public float RevealStartTime;

			public float Progress;
		}

		private sealed class LocalLayerPresentationContext
		{
			public SeaLayer Layer;

			public readonly List<LocalCreaturePresentationEntry> Entries = new List<LocalCreaturePresentationEntry>();

			public readonly Dictionary<SeaCreature, LocalCreaturePresentationEntry> EntryByCreature = new Dictionary<SeaCreature, LocalCreaturePresentationEntry>();

			public Coroutine Routine;

			public float NextCreatureRevealTime;
		}

		private const int MaxActiveLayersPerPlayer = 2;

		private static SeaCreatureManager _activeInstance;

		[SerializeField]
		private Nav3DInitializer _nav3DInitializer;

		[Header("Database")]
		[SerializeField]
		private ItemDatabaseSO _itemDatabaseSO;

		[Header("Debug")]
		[SerializeField]
		private bool _log;

		[Header("End Day Despawn")]
		[SerializeField]
		[Min(1f)]
		private int _endDayDespawnPerFrame = 5;

		[Header("Normal Fish Server Simulation LOD")]
		[SerializeField]
		[Min(0.05f)]
		private float _normalFishLodRefreshInterval = 0.25f;

		[SerializeField]
		[Min(1f)]
		private float _normalFishFullDistance = 150f;

		[SerializeField]
		[Min(1f)]
		private float _normalFishFullExitDistance = 180f;

		[SerializeField]
		[Min(1f)]
		private float _normalFishMediumDistance = 1050f;

		[SerializeField]
		[Min(1f)]
		private float _normalFishMediumExitDistance = 1100f;

		[SerializeField]
		[Min(0.02f)]
		private float _normalFishMediumTickInterval = 0.1f;

		[SerializeField]
		[Min(0.05f)]
		private float _normalFishFarTickInterval = 0.25f;

		[SerializeField]
		[Min(0.05f)]
		private float _normalFishMediumPositionThreshold = 0.2f;

		[SerializeField]
		[Min(0.05f)]
		private float _normalFishFarPositionThreshold = 0.75f;

		[SerializeField]
		[Min(1f)]
		private float _normalFishMediumRotationThreshold = 5f;

		[SerializeField]
		[Min(1f)]
		private float _normalFishFarRotationThreshold = 12f;

		[Header("Layers")]
		[SerializeField]
		private List<SeaLayer> _layers = new List<SeaLayer>();

		private bool _isEndDayResetRunning;

		private float _nextNormalFishLodRefreshTime;

		private readonly Dictionary<byte, SeaLayer> _layerByIndex = new Dictionary<byte, SeaLayer>();

		private readonly HashSet<byte> _invalidLayerIndices = new HashSet<byte>();

		private readonly List<SeaCreature> _creatures = new List<SeaCreature>();

		private readonly Dictionary<byte, List<SeaCreature>> _creaturesByLayerIndex = new Dictionary<byte, List<SeaCreature>>();

		private readonly HashSet<byte> _localActiveLayerIndices = new HashSet<byte>();

		private readonly HashSet<SeaLayer> _localOccupiedLayers = new HashSet<SeaLayer>();

		private readonly List<SeaLayer> _localOccupiedLayerOrder = new List<SeaLayer>();

		private readonly List<SeaLayer> _localActiveLayers = new List<SeaLayer>();

		private readonly Dictionary<ulong, HashSet<SeaLayer>> _serverOccupiedLayersByClient = new Dictionary<ulong, HashSet<SeaLayer>>();

		private readonly Dictionary<SeaLayer, LocalLayerPresentationContext> _localPresentationByLayer = new Dictionary<SeaLayer, LocalLayerPresentationContext>();

		private readonly Dictionary<ulong, SeaLayer> _serverPrimaryLayerByClient = new Dictionary<ulong, SeaLayer>();

		private readonly Dictionary<ulong, List<SeaLayer>> _serverOccupiedLayerOrderByClient = new Dictionary<ulong, List<SeaLayer>>();

		private readonly Dictionary<ulong, List<SeaLayer>> _serverActiveLayersByClient = new Dictionary<ulong, List<SeaLayer>>();

		private readonly List<ulong> _serverClientIdBuffer = new List<ulong>();

		private readonly List<JellyfishTrap> _jellyfishTraps = new List<JellyfishTrap>();

		private readonly List<OctopusTrap> _octopusTraps = new List<OctopusTrap>();

		private readonly List<Totti> _tottis = new List<Totti>();

		private readonly Dictionary<ulong, SharkCreature> _debugSharksByClient = new Dictionary<ulong, SharkCreature>();

		private PPG_Player _localOccupancyPlayer;

		private SeaLayer _localPrimaryLayer;

		private static readonly int DeadAmountId = Shader.PropertyToID("_DeadAmount");

		private readonly List<Renderer> _cachedDeadRenderers = new List<Renderer>(8);

		private MaterialPropertyBlock _deadMpb;

		public override SingletonScope Scope => SingletonScope.Scene;

		public ItemDatabaseSO ItemDatabase => _itemDatabaseSO;

		public IReadOnlyList<SeaLayer> Layers => _layers;

		public static bool TryGetActiveInstance(out SeaCreatureManager manager)
		{
			manager = _activeInstance;
			return manager != null;
		}

		protected override void Awake()
		{
			base.Awake();
			_activeInstance = this;
			_nav3DInitializer.Init();
			InitializeSerializedLayers();
			AdoptOceanRuntimeObjectsInScene();
		}

		private void OnDestroy()
		{
			StopAllLocalLayerPresentations();
			ClearLocalLayerOccupancy();
			ClearServerLayerOccupancy();
			_debugSharksByClient.Clear();
			if (_activeInstance == this)
			{
				_activeInstance = null;
			}
		}

		private void Update()
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if (!(singleton == null) && singleton.IsServer && !(Time.unscaledTime < _nextNormalFishLodRefreshTime))
			{
				_nextNormalFishLodRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, _normalFishLodRefreshInterval);
				RefreshNormalFishServerLod();
			}
		}

		private void RefreshNormalFishServerLod()
		{
			PlayersManager instance = Singleton<PlayersManager>.Instance;
			for (int num = _creatures.Count - 1; num >= 0; num--)
			{
				if (_creatures[num] is NormalFishCreature normalFishCreature && !(normalFishCreature == null) && normalFishCreature.IsSpawned)
				{
					if (!normalFishCreature.CanUseServerDistanceLod)
					{
						normalFishCreature.ServerRestoreFullSimulationLod();
					}
					else
					{
						float nearestPlayerDistanceSquared = GetNearestPlayerDistanceSquared(normalFishCreature.transform.position, instance);
						NormalFishServerLodTier normalFishServerLodTier = ResolveNormalFishLodTier(normalFishCreature.ServerLodTier, nearestPlayerDistanceSquared);
						switch (normalFishServerLodTier)
						{
						case NormalFishServerLodTier.Full:
							normalFishCreature.ServerApplyDistanceLod(normalFishServerLodTier, 0f, 0f, 0f);
							break;
						case NormalFishServerLodTier.Medium:
							normalFishCreature.ServerApplyDistanceLod(normalFishServerLodTier, _normalFishMediumTickInterval, _normalFishMediumPositionThreshold, _normalFishMediumRotationThreshold);
							break;
						default:
							normalFishCreature.ServerApplyDistanceLod(NormalFishServerLodTier.Far, _normalFishFarTickInterval, _normalFishFarPositionThreshold, _normalFishFarRotationThreshold);
							break;
						}
					}
				}
			}
		}

		private static float GetNearestPlayerDistanceSquared(Vector3 position, PlayersManager playersManager)
		{
			if (playersManager == null || playersManager.Players == null)
			{
				return float.PositiveInfinity;
			}
			float num = float.PositiveInfinity;
			foreach (KeyValuePair<ulong, PPG_Player> player in playersManager.Players)
			{
				PPG_Player value = player.Value;
				if (!(value == null) && value.IsSpawned)
				{
					float sqrMagnitude = (value.transform.position - position).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
					}
				}
			}
			return num;
		}

		private NormalFishServerLodTier ResolveNormalFishLodTier(NormalFishServerLodTier currentTier, float distanceSquared)
		{
			float num = _normalFishFullDistance * _normalFishFullDistance;
			float num2 = Mathf.Max(_normalFishFullDistance, _normalFishFullExitDistance);
			num2 *= num2;
			float num3 = Mathf.Max(_normalFishFullExitDistance, _normalFishMediumDistance);
			num3 *= num3;
			float num4 = Mathf.Max(_normalFishMediumDistance, _normalFishMediumExitDistance);
			num4 *= num4;
			return currentTier switch
			{
				NormalFishServerLodTier.Full => (!(distanceSquared <= num2)) ? ((distanceSquared <= num4) ? NormalFishServerLodTier.Medium : NormalFishServerLodTier.Far) : NormalFishServerLodTier.Full, 
				NormalFishServerLodTier.Medium => (!(distanceSquared <= num)) ? ((distanceSquared <= num4) ? NormalFishServerLodTier.Medium : NormalFishServerLodTier.Far) : NormalFishServerLodTier.Full, 
				_ => (!(distanceSquared <= num)) ? ((distanceSquared <= num3) ? NormalFishServerLodTier.Medium : NormalFishServerLodTier.Far) : NormalFishServerLodTier.Full, 
			};
		}

		private void InitializeSerializedLayers()
		{
			RebuildLayerIndexCache();
			for (int i = 0; i < _layers.Count; i++)
			{
				SeaLayer seaLayer = _layers[i];
				if (!(seaLayer == null))
				{
					if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && seaLayer.HasServerActiveOccupants)
					{
						SetLayerSimulationActive(seaLayer.Index, active: true);
					}
					Log($"Serialized layer initialized: {seaLayer.name} Index={seaLayer.Index}");
				}
			}
		}

		public bool TryGetLayer(byte layerIndex, out SeaLayer layer)
		{
			layer = null;
			if (_invalidLayerIndices.Contains(layerIndex))
			{
				Log($"Layer index {layerIndex} is invalid because duplicates exist.", LogType.Error);
				return false;
			}
			if (_layerByIndex.TryGetValue(layerIndex, out layer))
			{
				return layer != null;
			}
			return false;
		}

		private void RebuildLayerIndexCache()
		{
			_layerByIndex.Clear();
			_invalidLayerIndices.Clear();
			for (int i = 0; i < _layers.Count; i++)
			{
				SeaLayer seaLayer = _layers[i];
				if (seaLayer == null)
				{
					continue;
				}
				byte index = seaLayer.Index;
				if (!_invalidLayerIndices.Contains(index))
				{
					if (_layerByIndex.TryGetValue(index, out var value) && value != seaLayer)
					{
						Log($"Duplicate SeaLayer index detected. Index={index} Existing={value.name} Incoming={seaLayer.name}", LogType.Error);
						_invalidLayerIndices.Add(index);
						_layerByIndex.Remove(index);
					}
					else
					{
						_layerByIndex[index] = seaLayer;
					}
				}
			}
		}

		private void AdoptOceanRuntimeObjectsInScene()
		{
			JellyfishTrap[] array = UnityEngine.Object.FindObjectsByType<JellyfishTrap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			for (int i = 0; i < array.Length; i++)
			{
				RegisterJellyfishTrap(array[i]);
			}
			OctopusTrap[] array2 = UnityEngine.Object.FindObjectsByType<OctopusTrap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			for (int j = 0; j < array2.Length; j++)
			{
				RegisterOctopusTrap(array2[j]);
			}
			Totti[] array3 = UnityEngine.Object.FindObjectsByType<Totti>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			for (int k = 0; k < array3.Length; k++)
			{
				RegisterTotti(array3[k]);
			}
		}

		public void RegisterJellyfishTrap(JellyfishTrap jellyfishTrap)
		{
			if (!(jellyfishTrap == null) && !_jellyfishTraps.Contains(jellyfishTrap))
			{
				_jellyfishTraps.Add(jellyfishTrap);
			}
		}

		public void UnregisterJellyfishTrap(JellyfishTrap jellyfishTrap)
		{
			if (!(jellyfishTrap == null))
			{
				_jellyfishTraps.Remove(jellyfishTrap);
			}
		}

		public void RegisterOctopusTrap(OctopusTrap octopusTrap)
		{
			if (!(octopusTrap == null) && !_octopusTraps.Contains(octopusTrap))
			{
				_octopusTraps.Add(octopusTrap);
			}
		}

		public void UnregisterOctopusTrap(OctopusTrap octopusTrap)
		{
			if (!(octopusTrap == null))
			{
				_octopusTraps.Remove(octopusTrap);
			}
		}

		public void RegisterTotti(Totti totti)
		{
			if (!(totti == null) && !_tottis.Contains(totti))
			{
				_tottis.Add(totti);
			}
		}

		public void UnregisterTotti(Totti totti)
		{
			if (!(totti == null))
			{
				_tottis.Remove(totti);
			}
		}

		public void RegisterCreature(SeaCreature creature)
		{
			if (!(creature == null) && !_creatures.Contains(creature))
			{
				_creatures.Add(creature);
				byte b = ((creature.CurrentSeaLayer != null) ? creature.CurrentSeaLayer.Index : creature.SeaLayerIndex);
				if (!_creaturesByLayerIndex.TryGetValue(b, out var value))
				{
					value = new List<SeaCreature>();
					_creaturesByLayerIndex[b] = value;
				}
				if (!value.Contains(creature))
				{
					value.Add(creature);
				}
				if (IsLocalLayerActive(b) && TryGetLayer(b, out var layer) && _localPresentationByLayer.TryGetValue(layer, out var value2))
				{
					AddCreatureToLocalPresentation(value2, creature);
				}
				else
				{
					creature.ApplyLayerActivity(IsLocalLayerActive(b), forceRefresh: true);
				}
				if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && creature.CurrentSeaLayer != null && creature.CurrentSeaLayer.HasServerActiveOccupants)
				{
					creature.SetServerSimulationActive(active: true);
				}
				Log($"Creature registered: {creature.name} Layer={b}");
			}
		}

		public void UnregisterCreature(SeaCreature creature)
		{
			if (creature == null)
			{
				return;
			}
			_creatures.Remove(creature);
			byte seaLayerIndex = creature.SeaLayerIndex;
			if (_creaturesByLayerIndex.TryGetValue(seaLayerIndex, out var value))
			{
				value.Remove(creature);
				if (value.Count == 0)
				{
					_creaturesByLayerIndex.Remove(seaLayerIndex);
				}
			}
			RemoveCreatureFromLocalPresentation(creature);
			if (creature is SharkCreature shark)
			{
				RemoveDebugSharkTracking(shark);
			}
			Log($"Creature unregistered: {creature.name} Layer={seaLayerIndex}");
		}

		public IReadOnlyList<SeaCreature> GetCreaturesInLayer(byte layerIndex)
		{
			if (!_creaturesByLayerIndex.TryGetValue(layerIndex, out var value))
			{
				return Array.Empty<SeaCreature>();
			}
			return value;
		}

		public void SetLayerSimulationActive(byte layerIndex, bool active)
		{
			if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || !_creaturesByLayerIndex.TryGetValue(layerIndex, out var value))
			{
				return;
			}
			for (int num = value.Count - 1; num >= 0; num--)
			{
				SeaCreature seaCreature = value[num];
				if (seaCreature == null || !seaCreature.IsSpawned)
				{
					value.RemoveAt(num);
				}
				else
				{
					seaCreature.SetServerSimulationActive(active);
				}
			}
			Log($"Layer simulation changed. Layer={layerIndex} Active={active}");
		}

		public int ServerBeginLayerRetreat(byte layerIndex, float speedMultiplier, List<SeaCreature> participants, out float maximumEstimatedDuration)
		{
			maximumEstimatedDuration = 0f;
			if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || participants == null)
			{
				return 0;
			}
			participants.Clear();
			if (!_creaturesByLayerIndex.TryGetValue(layerIndex, out var value))
			{
				return 0;
			}
			for (int num = value.Count - 1; num >= 0; num--)
			{
				SeaCreature seaCreature = value[num];
				float estimatedDuration;
				if (seaCreature == null || !seaCreature.IsSpawned)
				{
					value.RemoveAt(num);
				}
				else if (seaCreature.ServerTryBeginLayerRetreat(speedMultiplier, out estimatedDuration))
				{
					participants.Add(seaCreature);
					maximumEstimatedDuration = Mathf.Max(maximumEstimatedDuration, estimatedDuration);
				}
			}
			Log($"Layer retreat started. Layer={layerIndex} " + $"Participants={participants.Count} Estimated={maximumEstimatedDuration:0.00}s");
			return participants.Count;
		}

		public bool ServerAreLayerRetreatsComplete(List<SeaCreature> participants)
		{
			if (participants == null || participants.Count == 0)
			{
				return true;
			}
			for (int num = participants.Count - 1; num >= 0; num--)
			{
				SeaCreature seaCreature = participants[num];
				if (seaCreature == null || !seaCreature.IsSpawned)
				{
					participants.RemoveAt(num);
				}
				else if (!seaCreature.IsServerLayerRetreatComplete)
				{
					return false;
				}
			}
			return true;
		}

		public void ServerCancelLayerRetreats(List<SeaCreature> participants)
		{
			if (participants == null)
			{
				return;
			}
			for (int num = participants.Count - 1; num >= 0; num--)
			{
				SeaCreature seaCreature = participants[num];
				if (seaCreature != null && seaCreature.IsSpawned)
				{
					seaCreature.ServerCancelLayerRetreat();
				}
			}
			participants.Clear();
		}

		public void ServerForceCompleteLayerRetreats(List<SeaCreature> participants)
		{
			if (participants == null)
			{
				return;
			}
			for (int num = participants.Count - 1; num >= 0; num--)
			{
				SeaCreature seaCreature = participants[num];
				if (seaCreature != null && seaCreature.IsSpawned)
				{
					seaCreature.ServerForceCompleteLayerRetreat();
				}
			}
		}

		public void RegisterLocalLayerOccupancy(SeaLayer layer, PPG_Player player)
		{
			if (!(layer == null) && !(player == null) && player.IsOwner)
			{
				if (_localOccupancyPlayer != null && _localOccupancyPlayer != player)
				{
					ClearLocalLayerOccupancy();
				}
				_localOccupancyPlayer = player;
				if (_localOccupiedLayers.Add(layer))
				{
					MoveLayerToEnd(_localOccupiedLayerOrder, layer);
					SetLocalPrimaryLayer(layer, player);
					ActivateLocalLayer(layer, player);
					Log($"Local occupancy added. Layer={layer.Index} Occupied={_localOccupiedLayers.Count}");
				}
			}
		}

		public void UnregisterLocalLayerOccupancy(SeaLayer layer, PPG_Player player)
		{
			if (!(layer == null) && !(player == null) && !(player != _localOccupancyPlayer) && _localOccupiedLayers.Remove(layer))
			{
				_localOccupiedLayerOrder.Remove(layer);
				if (_localOccupiedLayers.Count == 0)
				{
					SetLocalPrimaryLayer(null, player);
					DeactivateAllLocalLayers();
					_localOccupancyPlayer = null;
				}
				else if (_localPrimaryLayer == layer)
				{
					SeaLayer mostRecentLayer = GetMostRecentLayer(_localOccupiedLayerOrder);
					SetLocalPrimaryLayer(mostRecentLayer, player);
					ActivateLocalLayer(mostRecentLayer, player);
				}
				Log($"Local occupancy removed. Layer={layer.Index} Occupied={_localOccupiedLayers.Count}");
			}
		}

		public bool IsLocalLayerActive(byte layerIndex)
		{
			return _localActiveLayerIndices.Contains(layerIndex);
		}

		public float GetLocalCreaturePresentationProgress(SeaCreature creature)
		{
			if (creature == null || creature.IsStandalone)
			{
				return 1f;
			}
			byte seaLayerIndex = creature.SeaLayerIndex;
			if (!IsLocalLayerActive(seaLayerIndex))
			{
				return 0f;
			}
			if (!TryGetLayer(seaLayerIndex, out var layer) || !_localPresentationByLayer.TryGetValue(layer, out var value))
			{
				return 1f;
			}
			if (!value.EntryByCreature.TryGetValue(creature, out var value2))
			{
				return 0f;
			}
			return value2.Progress;
		}

		public void ApplyLocalLayerActivity(byte layerIndex)
		{
			bool flag = IsLocalLayerActive(layerIndex);
			if (!_creaturesByLayerIndex.TryGetValue(layerIndex, out var value))
			{
				return;
			}
			for (int num = value.Count - 1; num >= 0; num--)
			{
				SeaCreature seaCreature = value[num];
				if (seaCreature == null)
				{
					value.RemoveAt(num);
				}
				else
				{
					seaCreature.ApplyLayerActivity(flag);
				}
			}
			Log($"Applied local layer activity. Layer={layerIndex} Active={flag}");
		}

		private void StartLocalLayerPresentation(SeaLayer layer, PPG_Player player)
		{
			if (layer == null || player == null || !player.IsOwner || _localPresentationByLayer.ContainsKey(layer))
			{
				return;
			}
			LocalLayerPresentationContext localLayerPresentationContext = new LocalLayerPresentationContext
			{
				Layer = layer
			};
			_localPresentationByLayer.Add(layer, localLayerPresentationContext);
			layer.BeginLocalPresentation();
			List<SeaCreature> list = new List<SeaCreature>();
			if (_creaturesByLayerIndex.TryGetValue(layer.Index, out var value))
			{
				for (int num = value.Count - 1; num >= 0; num--)
				{
					SeaCreature seaCreature = value[num];
					if (seaCreature == null)
					{
						value.RemoveAt(num);
					}
					else
					{
						list.Add(seaCreature);
					}
				}
			}
			Vector3 playerPosition = player.transform.position;
			list.Sort((SeaCreature left, SeaCreature right) =>
			{
				float sqrMagnitude = (left.transform.position - playerPosition).sqrMagnitude;
				return (right.transform.position - playerPosition).sqrMagnitude.CompareTo(sqrMagnitude);
			});
			float num2 = Time.time + layer.LocalSimulationPrewarmDuration;
			float num3 = ((list.Count > 1) ? (layer.LocalCreatureStaggerDuration / (float)(list.Count - 1)) : 0f);
			for (int num4 = 0; num4 < list.Count; num4++)
			{
				SeaCreature seaCreature2 = list[num4];
				LocalCreaturePresentationEntry localCreaturePresentationEntry = new LocalCreaturePresentationEntry
				{
					Creature = seaCreature2,
					RevealStartTime = num2 + num3 * (float)num4,
					Progress = 0f
				};
				localLayerPresentationContext.Entries.Add(localCreaturePresentationEntry);
				localLayerPresentationContext.EntryByCreature.Add(seaCreature2, localCreaturePresentationEntry);
				seaCreature2.ApplyLocalPresentation(0f, forceRefresh: true);
			}
			localLayerPresentationContext.NextCreatureRevealTime = ((list.Count > 0) ? (num2 + layer.LocalCreatureStaggerDuration) : num2);
			localLayerPresentationContext.Routine = StartCoroutine(LocalLayerPresentationCoroutine(localLayerPresentationContext, num2));
			Log($"Local presentation started. Layer={layer.Index} " + $"Creatures={list.Count} Prewarm={layer.LocalSimulationPrewarmDuration:F2}s");
		}

		private IEnumerator LocalLayerPresentationCoroutine(LocalLayerPresentationContext context, float firstRevealTime)
		{
			SeaLayer layer = context.Layer;
			LocalLayerPresentationContext value;
			while (layer != null && _localPresentationByLayer.TryGetValue(layer, out value) && value == context && IsLocalLayerActive(layer.Index))
			{
				float time = Time.time;
				float localBoidsRevealProgress = layer.EvaluateLocalReveal((time - firstRevealTime) / layer.LocalBoidsRevealDuration);
				layer.SetLocalBoidsRevealProgress(localBoidsRevealProgress);
				for (int num = context.Entries.Count - 1; num >= 0; num--)
				{
					LocalCreaturePresentationEntry localCreaturePresentationEntry = context.Entries[num];
					SeaCreature creature = localCreaturePresentationEntry.Creature;
					if (creature == null)
					{
						context.Entries.RemoveAt(num);
					}
					else if (!(localCreaturePresentationEntry.Progress >= 1f))
					{
						float normalizedTime = (time - localCreaturePresentationEntry.RevealStartTime) / layer.LocalCreatureRevealDuration;
						float num2 = layer.EvaluateLocalReveal(normalizedTime);
						if (!Mathf.Approximately(localCreaturePresentationEntry.Progress, num2))
						{
							localCreaturePresentationEntry.Progress = num2;
							creature.ApplyLocalPresentation(num2);
						}
					}
				}
				yield return null;
			}
			context.Routine = null;
		}

		private void AddCreatureToLocalPresentation(LocalLayerPresentationContext context, SeaCreature creature)
		{
			if (!(creature == null) && context != null && !(context.Layer == null) && !context.EntryByCreature.ContainsKey(creature))
			{
				float num = Mathf.Max(Time.time, context.NextCreatureRevealTime);
				LocalCreaturePresentationEntry localCreaturePresentationEntry = new LocalCreaturePresentationEntry
				{
					Creature = creature,
					RevealStartTime = num,
					Progress = 0f
				};
				context.Entries.Add(localCreaturePresentationEntry);
				context.EntryByCreature.Add(creature, localCreaturePresentationEntry);
				context.NextCreatureRevealTime = num + context.Layer.LocalSpawnedCreatureRevealSpacing;
				creature.ApplyLocalPresentation(0f, forceRefresh: true);
			}
		}

		private void RemoveCreatureFromLocalPresentation(SeaCreature creature)
		{
			if (creature == null)
			{
				return;
			}
			foreach (LocalLayerPresentationContext value2 in _localPresentationByLayer.Values)
			{
				if (value2.EntryByCreature.TryGetValue(creature, out var value))
				{
					value2.EntryByCreature.Remove(creature);
					value2.Entries.Remove(value);
					break;
				}
			}
		}

		private void StopLocalLayerPresentation(SeaLayer expectedLayer = null)
		{
			if (!(expectedLayer == null) && _localPresentationByLayer.TryGetValue(expectedLayer, out var value))
			{
				if (value.Routine != null)
				{
					StopCoroutine(value.Routine);
					value.Routine = null;
				}
				expectedLayer.EndLocalPresentation();
				_localPresentationByLayer.Remove(expectedLayer);
				value.Entries.Clear();
				value.EntryByCreature.Clear();
				value.NextCreatureRevealTime = 0f;
			}
		}

		private void StopAllLocalLayerPresentations()
		{
			if (_localPresentationByLayer.Count != 0)
			{
				List<SeaLayer> list = new List<SeaLayer>(_localPresentationByLayer.Keys);
				for (int i = 0; i < list.Count; i++)
				{
					StopLocalLayerPresentation(list[i]);
				}
			}
		}

		private void SetLocalPrimaryLayer(SeaLayer selectedLayer, PPG_Player player)
		{
			if (_localPrimaryLayer == selectedLayer)
			{
				return;
			}
			SeaLayer localPrimaryLayer = _localPrimaryLayer;
			_localPrimaryLayer = selectedLayer;
			if (localPrimaryLayer != null)
			{
				localPrimaryLayer.SetLocalPrimaryActive(player, active: false);
			}
			if (player != null && !player.IsServer)
			{
				if (selectedLayer != null)
				{
					player.SetPrimarySeaLayer(selectedLayer);
				}
				else if (localPrimaryLayer != null)
				{
					player.ClearPrimarySeaLayer(localPrimaryLayer);
				}
			}
			if (selectedLayer != null)
			{
				selectedLayer.SetLocalPrimaryActive(player, active: true);
			}
			Log("Local primary changed. Previous=" + GetLayerLogName(localPrimaryLayer) + " " + $"Current={GetLayerLogName(selectedLayer)} Occupied={_localOccupiedLayers.Count}");
		}

		private void ActivateLocalLayer(SeaLayer layer, PPG_Player player)
		{
			if (!(layer == null) && !(player == null) && player.IsOwner)
			{
				bool num = _localActiveLayers.Remove(layer);
				_localActiveLayers.Add(layer);
				if (!num)
				{
					_localActiveLayerIndices.Add(layer.Index);
					StartLocalLayerPresentation(layer, player);
				}
				while (_localActiveLayers.Count > 2)
				{
					DeactivateLocalLayer(_localActiveLayers[0]);
				}
				Log($"Local layer activated. Layer={layer.Index} " + $"Active={_localActiveLayers.Count} Primary={GetLayerLogName(_localPrimaryLayer)}");
			}
		}

		private void DeactivateLocalLayer(SeaLayer layer)
		{
			if (!(layer == null) && _localActiveLayers.Remove(layer))
			{
				StopLocalLayerPresentation(layer);
				_localActiveLayerIndices.Remove(layer.Index);
				ApplyLocalLayerActivity(layer.Index);
				Log($"Local layer deactivated. Layer={layer.Index} Active={_localActiveLayers.Count}");
			}
		}

		private void DeactivateAllLocalLayers()
		{
			while (_localActiveLayers.Count > 0)
			{
				if (_localActiveLayers[0] == null)
				{
					_localActiveLayers.RemoveAt(0);
				}
				else
				{
					DeactivateLocalLayer(_localActiveLayers[0]);
				}
			}
			_localActiveLayerIndices.Clear();
			StopAllLocalLayerPresentations();
		}

		public void RefreshAllLocalLayerActivities()
		{
			for (int num = _creatures.Count - 1; num >= 0; num--)
			{
				SeaCreature seaCreature = _creatures[num];
				if (seaCreature == null)
				{
					_creatures.RemoveAt(num);
				}
				else
				{
					seaCreature.ApplyLocalPresentation(GetLocalCreaturePresentationProgress(seaCreature), forceRefresh: true);
				}
			}
		}

		public void ClearLocalLayerOccupancy()
		{
			if (_localOccupiedLayers.Count != 0 || _localActiveLayerIndices.Count != 0 || _localOccupiedLayerOrder.Count != 0 || _localActiveLayers.Count != 0 || _localPresentationByLayer.Count != 0 || !(_localPrimaryLayer == null) || !(_localOccupancyPlayer == null))
			{
				PPG_Player localOccupancyPlayer = _localOccupancyPlayer;
				SetLocalPrimaryLayer(null, localOccupancyPlayer);
				_localOccupiedLayers.Clear();
				_localOccupiedLayerOrder.Clear();
				DeactivateAllLocalLayers();
				_localOccupancyPlayer = null;
				RefreshAllLocalLayerActivities();
			}
		}

		public bool ClearLocalPlayerLayerOccupancy(PPG_Player player)
		{
			if (player == null || !player.IsOwner)
			{
				return false;
			}
			bool flag = player == _localOccupancyPlayer && (_localPrimaryLayer != null || _localOccupiedLayers.Count > 0 || _localActiveLayers.Count > 0 || _localPresentationByLayer.Count > 0);
			if (player == _localOccupancyPlayer)
			{
				SetLocalPrimaryLayer(null, player);
				_localOccupiedLayers.Clear();
				_localOccupiedLayerOrder.Clear();
				DeactivateAllLocalLayers();
				_localOccupancyPlayer = null;
				RefreshAllLocalLayerActivities();
			}
			for (int i = 0; i < _layers.Count; i++)
			{
				SeaLayer seaLayer = _layers[i];
				if (seaLayer != null)
				{
					flag |= seaLayer.ClearLocalClientOccupancy(player.OwnerClientId);
				}
			}
			return flag;
		}

		public void SetServerLayerOccupancy(SeaLayer layer, PPG_Player player, bool occupied)
		{
			if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || layer == null || player == null)
			{
				return;
			}
			ulong ownerClientId = player.OwnerClientId;
			_serverOccupiedLayersByClient.TryGetValue(ownerClientId, out var value);
			if (occupied)
			{
				if (value == null)
				{
					value = new HashSet<SeaLayer>();
					_serverOccupiedLayersByClient[ownerClientId] = value;
				}
				if (value.Add(layer))
				{
					MoveLayerToEnd(GetOrCreateLayerOrder(_serverOccupiedLayerOrderByClient, ownerClientId), layer);
					SetServerPrimaryLayer(ownerClientId, player, layer);
					ActivateServerLayer(ownerClientId, layer);
				}
			}
			else
			{
				if (value == null || !value.Remove(layer))
				{
					return;
				}
				if (_serverOccupiedLayerOrderByClient.TryGetValue(ownerClientId, out var value2))
				{
					value2.Remove(layer);
					if (value2.Count == 0)
					{
						_serverOccupiedLayerOrderByClient.Remove(ownerClientId);
					}
				}
				SeaLayer value3;
				if (value.Count == 0)
				{
					_serverOccupiedLayersByClient.Remove(ownerClientId);
					SetServerPrimaryLayer(ownerClientId, player, null);
					DeactivateAllServerLayers(ownerClientId);
				}
				else if (_serverPrimaryLayerByClient.TryGetValue(ownerClientId, out value3) && value3 == layer)
				{
					SeaLayer mostRecentLayer = GetMostRecentLayer(value2);
					SetServerPrimaryLayer(ownerClientId, player, mostRecentLayer);
					ActivateServerLayer(ownerClientId, mostRecentLayer);
				}
			}
		}

		public void RemoveServerClientOccupancy(ulong clientId)
		{
			_serverOccupiedLayersByClient.Remove(clientId);
			_serverOccupiedLayerOrderByClient.Remove(clientId);
			PPG_Player player = null;
			if (Singleton<PlayersManager>.TryGetInstance(out var instance))
			{
				instance.TryGetPlayer(clientId, out player);
			}
			SetServerPrimaryLayer(clientId, player, null);
			DeactivateAllServerLayers(clientId);
			for (int i = 0; i < _layers.Count; i++)
			{
				SeaLayer seaLayer = _layers[i];
				if (seaLayer != null)
				{
					seaLayer.ClearServerClientOccupancy(clientId);
				}
			}
		}

		public void RemovePlayerLayerOccupancy(PPG_Player player)
		{
			if (!(player == null))
			{
				if (player.IsOwner)
				{
					ClearLocalPlayerLayerOccupancy(player);
				}
				if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
				{
					RemoveServerClientOccupancy(player.OwnerClientId);
				}
			}
		}

		public void RemoveLayerOccupancy(SeaLayer layer)
		{
			if (layer == null)
			{
				return;
			}
			bool flag = _localPrimaryLayer == layer;
			_localOccupiedLayers.Remove(layer);
			_localOccupiedLayerOrder.Remove(layer);
			DeactivateLocalLayer(layer);
			if (_localOccupiedLayers.Count == 0)
			{
				SetLocalPrimaryLayer(null, _localOccupancyPlayer);
				DeactivateAllLocalLayers();
				_localOccupancyPlayer = null;
			}
			else if (flag)
			{
				SeaLayer mostRecentLayer = GetMostRecentLayer(_localOccupiedLayerOrder);
				SetLocalPrimaryLayer(mostRecentLayer, _localOccupancyPlayer);
				ActivateLocalLayer(mostRecentLayer, _localOccupancyPlayer);
			}
			_serverClientIdBuffer.Clear();
			foreach (KeyValuePair<ulong, HashSet<SeaLayer>> item in _serverOccupiedLayersByClient)
			{
				if (item.Value != null && item.Value.Contains(layer))
				{
					_serverClientIdBuffer.Add(item.Key);
				}
			}
			foreach (KeyValuePair<ulong, List<SeaLayer>> item2 in _serverActiveLayersByClient)
			{
				if (item2.Value != null && item2.Value.Contains(layer) && !_serverClientIdBuffer.Contains(item2.Key))
				{
					_serverClientIdBuffer.Add(item2.Key);
				}
			}
			for (int i = 0; i < _serverClientIdBuffer.Count; i++)
			{
				ulong num = _serverClientIdBuffer[i];
				PPG_Player player = null;
				if (Singleton<PlayersManager>.TryGetInstance(out var instance))
				{
					instance.TryGetPlayer(num, out player);
				}
				bool flag2 = _serverPrimaryLayerByClient.TryGetValue(num, out var value) && value == layer;
				if (_serverOccupiedLayersByClient.TryGetValue(num, out var value2))
				{
					value2.Remove(layer);
					if (value2.Count == 0)
					{
						_serverOccupiedLayersByClient.Remove(num);
					}
				}
				if (_serverOccupiedLayerOrderByClient.TryGetValue(num, out var value3))
				{
					value3.Remove(layer);
					if (value3.Count == 0)
					{
						_serverOccupiedLayerOrderByClient.Remove(num);
					}
				}
				DeactivateServerLayer(num, layer);
				if (!_serverOccupiedLayersByClient.ContainsKey(num))
				{
					SetServerPrimaryLayer(num, player, null);
					DeactivateAllServerLayers(num);
				}
				else if (flag2)
				{
					SeaLayer mostRecentLayer2 = GetMostRecentLayer(value3);
					SetServerPrimaryLayer(num, player, mostRecentLayer2);
					ActivateServerLayer(num, mostRecentLayer2);
				}
			}
			_serverClientIdBuffer.Clear();
		}

		private void SetServerPrimaryLayer(ulong clientId, PPG_Player player, SeaLayer selectedLayer)
		{
			_serverPrimaryLayerByClient.TryGetValue(clientId, out var value);
			if (value == selectedLayer)
			{
				return;
			}
			if (selectedLayer != null)
			{
				_serverPrimaryLayerByClient[clientId] = selectedLayer;
			}
			else
			{
				_serverPrimaryLayerByClient.Remove(clientId);
			}
			if (player != null)
			{
				if (selectedLayer != null)
				{
					player.SetPrimarySeaLayer(selectedLayer);
				}
				else if (value != null)
				{
					player.ClearPrimarySeaLayer(value);
				}
			}
			Log($"Server primary changed. ClientId={clientId} " + "Previous=" + GetLayerLogName(value) + " Current=" + GetLayerLogName(selectedLayer));
		}

		private void ClearServerLayerOccupancy()
		{
			_serverClientIdBuffer.Clear();
			foreach (ulong key in _serverPrimaryLayerByClient.Keys)
			{
				_serverClientIdBuffer.Add(key);
			}
			foreach (ulong key2 in _serverActiveLayersByClient.Keys)
			{
				if (!_serverClientIdBuffer.Contains(key2))
				{
					_serverClientIdBuffer.Add(key2);
				}
			}
			foreach (ulong key3 in _serverOccupiedLayersByClient.Keys)
			{
				if (!_serverClientIdBuffer.Contains(key3))
				{
					_serverClientIdBuffer.Add(key3);
				}
			}
			for (int i = 0; i < _serverClientIdBuffer.Count; i++)
			{
				RemoveServerClientOccupancy(_serverClientIdBuffer[i]);
			}
			_serverClientIdBuffer.Clear();
			_serverOccupiedLayersByClient.Clear();
			_serverOccupiedLayerOrderByClient.Clear();
			_serverActiveLayersByClient.Clear();
			_serverPrimaryLayerByClient.Clear();
		}

		private static string GetLayerLogName(SeaLayer layer)
		{
			if (!(layer != null))
			{
				return "None";
			}
			return $"{layer.DisplayName}({layer.Index})";
		}

		private void ActivateServerLayer(ulong clientId, SeaLayer layer)
		{
			if (!(layer == null))
			{
				List<SeaLayer> orCreateLayerOrder = GetOrCreateLayerOrder(_serverActiveLayersByClient, clientId);
				bool num = orCreateLayerOrder.Remove(layer);
				orCreateLayerOrder.Add(layer);
				if (!num)
				{
					layer.SetServerClientActive(clientId, active: true);
				}
				while (orCreateLayerOrder.Count > 2)
				{
					DeactivateServerLayer(clientId, orCreateLayerOrder[0]);
				}
				Log($"Server layer activated. ClientId={clientId} Layer={layer.Index} " + $"Active={orCreateLayerOrder.Count}");
			}
		}

		private void DeactivateServerLayer(ulong clientId, SeaLayer layer)
		{
			if (!(layer == null) && _serverActiveLayersByClient.TryGetValue(clientId, out var value) && value.Remove(layer))
			{
				layer.SetServerClientActive(clientId, active: false);
				if (value.Count == 0)
				{
					_serverActiveLayersByClient.Remove(clientId);
				}
				Log($"Server layer deactivated. ClientId={clientId} Layer={layer.Index} " + $"Active={value.Count}");
			}
		}

		public SeaLayer FindNearestLayer(Vector3 worldPosition)
		{
			SeaLayer seaLayer = null;
			float num = float.PositiveInfinity;
			for (int i = 0; i < _layers.Count; i++)
			{
				SeaLayer seaLayer2 = _layers[i];
				if (!(seaLayer2 == null))
				{
					float num2 = (seaLayer2.TryGetWorldBounds(out var bounds) ? bounds.SqrDistance(worldPosition) : (seaLayer2.transform.position - worldPosition).sqrMagnitude);
					if (!(num2 > num) && (!Mathf.Approximately(num2, num) || !(seaLayer != null) || seaLayer2.Index < seaLayer.Index))
					{
						seaLayer = seaLayer2;
						num = num2;
					}
				}
			}
			return seaLayer;
		}

		private void DeactivateAllServerLayers(ulong clientId)
		{
			if (!_serverActiveLayersByClient.TryGetValue(clientId, out var value))
			{
				return;
			}
			while (value.Count > 0)
			{
				if (value[0] == null)
				{
					value.RemoveAt(0);
				}
				else
				{
					DeactivateServerLayer(clientId, value[0]);
				}
			}
			_serverActiveLayersByClient.Remove(clientId);
		}

		public SeaCreature SpawnCreature(CreatureSO creatureSO, SeaLayer seaLayer, SeaArea preferredArea = null, int runtimeId = -1)
		{
			if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
			{
				Log("SpawnCreature called on non-server.", LogType.Warning);
				return null;
			}
			if (creatureSO == null)
			{
				Log("SpawnCreature failed. CreatureSO is null.", LogType.Warning);
				return null;
			}
			if (seaLayer == null)
			{
				Log("SpawnCreature failed for " + creatureSO.name + ". SeaLayer is null.", LogType.Warning);
				return null;
			}
			SeaArea seaArea = ((preferredArea != null) ? preferredArea : seaLayer.GetRandomSeaArea());
			if (seaArea == null)
			{
				Log("SpawnCreature failed for " + creatureSO.name + ". SeaArea could not be resolved.", LogType.Warning);
				return null;
			}
			byte randomPointIndex = seaArea.GetRandomPointIndex();
			Vector3 pointByIndex = seaArea.GetPointByIndex(randomPointIndex);
			return SpawnCreatureAtPositionInternal(creatureSO, seaLayer, seaArea, pointByIndex, runtimeId);
		}

		private static List<SeaLayer> GetOrCreateLayerOrder(Dictionary<ulong, List<SeaLayer>> layersByClient, ulong clientId)
		{
			if (layersByClient.TryGetValue(clientId, out var value))
			{
				return value;
			}
			value = new List<SeaLayer>(2);
			layersByClient.Add(clientId, value);
			return value;
		}

		private static void MoveLayerToEnd(List<SeaLayer> layers, SeaLayer layer)
		{
			if (layers != null && !(layer == null))
			{
				layers.Remove(layer);
				layers.Add(layer);
			}
		}

		private static SeaLayer GetMostRecentLayer(List<SeaLayer> layers)
		{
			if (layers == null || layers.Count == 0)
			{
				return null;
			}
			return layers[layers.Count - 1];
		}

		public SeaCreature SpawnCreatureAtPosition(CreatureSO creatureSO, SeaLayer seaLayer, Vector3 spawnPosition, int runtimeId = -1)
		{
			if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
			{
				Log("SpawnCreatureAtPosition called on non-server.", LogType.Warning);
				return null;
			}
			if (creatureSO == null || seaLayer == null)
			{
				Log("SpawnCreatureAtPosition failed. CreatureSO or SeaLayer is null.", LogType.Warning);
				return null;
			}
			SeaArea closestSeaArea = seaLayer.GetClosestSeaArea(spawnPosition);
			if (closestSeaArea == null)
			{
				Log("SpawnCreatureAtPosition failed for " + creatureSO.name + ". SeaArea could not be resolved.", LogType.Warning);
				return null;
			}
			return SpawnCreatureAtPositionInternal(creatureSO, seaLayer, closestSeaArea, spawnPosition, runtimeId);
		}

		public bool ServerSpawnDebugSharkForPlayer(PPG_Player targetPlayer)
		{
			if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
			{
				return false;
			}
			if (targetPlayer == null || !targetPlayer.IsSpawned)
			{
				Log("[SharkDebug] Spawn failed. Target player is invalid.", LogType.Warning);
				return false;
			}
			SharkSO sharkSO = ((_itemDatabaseSO != null) ? _itemDatabaseSO.CreatureItems.OfType<SharkSO>().FirstOrDefault((SharkSO item) => item != null && item.Prefab != null && item.Prefab.GetComponent<SharkCreature>() != null) : null);
			if (sharkSO == null)
			{
				Log("[SharkDebug] Spawn failed. No valid SharkSO found.", LogType.Warning);
				return false;
			}
			ulong ownerClientId = targetPlayer.OwnerClientId;
			ServerKillDebugSharkForClient(ownerClientId, "ReplacedBySharkOn");
			Vector3 forward = targetPlayer.transform.forward;
			if (forward.sqrMagnitude < 0.0001f)
			{
				forward = Vector3.forward;
			}
			Vector3 vector = targetPlayer.transform.position - forward.normalized * 15f;
			SeaLayer seaLayer = ((targetPlayer.CurrentSeaLayer != null) ? targetPlayer.CurrentSeaLayer : FindNearestLayer(vector));
			if (seaLayer == null)
			{
				Log($"[SharkDebug] Spawn failed. No SeaLayer found. ClientId={ownerClientId}", LogType.Warning);
				return false;
			}
			SeaCreature seaCreature = SpawnCreatureAtPosition(sharkSO, seaLayer, vector);
			if (!(seaCreature is SharkCreature sharkCreature))
			{
				if (seaCreature != null && seaCreature.NetworkObject != null && seaCreature.NetworkObject.IsSpawned)
				{
					seaCreature.ServerPrepareForDespawn();
					seaCreature.NetworkObject.Despawn();
				}
				Log($"[SharkDebug] Spawn failed. SharkCreature was not created. ClientId={ownerClientId}", LogType.Warning);
				return false;
			}
			Vector3 vector2 = targetPlayer.transform.position - vector;
			if (vector2.sqrMagnitude > 0.0001f)
			{
				sharkCreature.transform.rotation = Quaternion.LookRotation(vector2.normalized, Vector3.up);
			}
			sharkCreature.SetServerSimulationActive(active: true);
			if (!sharkCreature.ServerStartDebugChase(targetPlayer))
			{
				sharkCreature.ServerPrepareForDespawn();
				if (sharkCreature.NetworkObject != null && sharkCreature.NetworkObject.IsSpawned)
				{
					sharkCreature.NetworkObject.Despawn();
				}
				Log($"[SharkDebug] Spawn rolled back. Chase could not start. ClientId={ownerClientId}", LogType.Warning);
				return false;
			}
			_debugSharksByClient[ownerClientId] = sharkCreature;
			Log($"[SharkDebug] Shark spawned. ClientId={ownerClientId} Position={vector}");
			return true;
		}

		public bool ServerKillDebugSharkForClient(ulong clientId, string reason)
		{
			if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
			{
				return false;
			}
			if (!_debugSharksByClient.TryGetValue(clientId, out var value))
			{
				return false;
			}
			_debugSharksByClient.Remove(clientId);
			if (value == null || value.NetworkObject == null || !value.NetworkObject.IsSpawned)
			{
				return false;
			}
			bool flag = value.ServerDebugDie(reason);
			Log($"[SharkDebug] Shark kill requested. ClientId={clientId} Reason={reason} Success={flag}");
			return flag;
		}

		private void RemoveDebugSharkTracking(SharkCreature shark)
		{
			if (shark == null || _debugSharksByClient.Count == 0)
			{
				return;
			}
			ulong num = ulong.MaxValue;
			foreach (KeyValuePair<ulong, SharkCreature> item in _debugSharksByClient)
			{
				if (!(item.Value != shark))
				{
					num = item.Key;
					break;
				}
			}
			if (num != ulong.MaxValue)
			{
				_debugSharksByClient.Remove(num);
			}
		}

		private SeaCreature SpawnCreatureAtPositionInternal(CreatureSO creatureSO, SeaLayer seaLayer, SeaArea spawnArea, Vector3 spawnPosition, int runtimeId)
		{
			NetworkObject creaturePrefab = GetCreaturePrefab(creatureSO);
			if (creaturePrefab == null)
			{
				Log("SpawnCreature failed for " + creatureSO.name + ". Creature prefab is null.", LogType.Warning);
				return null;
			}
			NetworkObject networkObject = UnityEngine.Object.Instantiate(creaturePrefab, spawnPosition, Quaternion.identity);
			if (networkObject == null)
			{
				Log("SpawnCreature failed for " + creatureSO.name + ". Instantiate returned null.", LogType.Warning);
				return null;
			}
			if (!networkObject.TryGetComponent<SeaCreature>(out var component))
			{
				Log("SpawnCreature failed. Prefab does not contain SeaCreature: " + creaturePrefab.name, LogType.Error);
				UnityEngine.Object.Destroy(networkObject.gameObject);
				return null;
			}
			if (!component.ServerInit(seaLayer, creatureSO, runtimeId, spawnArea))
			{
				Log("SpawnCreature failed. ServerInit returned false for " + creatureSO.name + ".", LogType.Warning);
				if (networkObject.IsSpawned)
				{
					networkObject.Despawn();
				}
				else
				{
					UnityEngine.Object.Destroy(networkObject.gameObject);
				}
				return null;
			}
			return component;
		}

		private NetworkObject GetCreaturePrefab(CreatureSO creatureSO)
		{
			if (creatureSO == null)
			{
				return null;
			}
			return creatureSO.Prefab;
		}

		public DeadFish SpawnDeadFish(FishSO fishSO, Vector3 position, ulong clientId, NetworkObjectReference interactorRef, bool select, bool canHold, bool useFallbackDropPosition = false, Vector3 fallbackDropPosition = default(Vector3), bool requireQuickSlot = false)
		{
			if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
			{
				Log("SpawnDeadFish called on non-server.", LogType.Warning);
				return null;
			}
			if (fishSO == null)
			{
				Log("SpawnDeadFish failed. FishSO is null.", LogType.Warning);
				return null;
			}
			if (fishSO.DeadPrefab == null)
			{
				Log("SpawnDeadFish failed. DeadPrefab is null for fish " + fishSO.name + ".", LogType.Warning);
				return null;
			}
			fishSO.DeadPrefab.TryInstantiateAndSpawn(out var instance, position, Quaternion.identity);
			if (instance == null)
			{
				Log("SpawnDeadFish failed. Instantiate/spawn returned null for fish " + fishSO.name + ".", LogType.Warning);
				return null;
			}
			if (!instance.TryGetComponent<DeadFish>(out var component))
			{
				Log("SpawnDeadFish failed. Spawned prefab does not contain DeadFish component: " + instance.name, LogType.Warning);
				if (instance.IsSpawned)
				{
					instance.Despawn();
				}
				else
				{
					UnityEngine.Object.Destroy(instance.gameObject);
				}
				return null;
			}
			component.ServerLazyHold(fishSO.Id, clientId, interactorRef, select, canHold, useFallbackDropPosition, fallbackDropPosition, requireQuickSlot);
			return component;
		}

		public DeadFish SpawnWorldDeadFish(FishSO fishSO, Vector3 position, Quaternion rotation)
		{
			if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
			{
				Log("SpawnWorldDeadFish called on non-server.", LogType.Warning);
				return null;
			}
			if (fishSO == null)
			{
				Log("SpawnWorldDeadFish failed. FishSO is null.", LogType.Warning);
				return null;
			}
			if (fishSO.DeadPrefab == null)
			{
				Log("SpawnWorldDeadFish failed. DeadPrefab is null for fish " + fishSO.name + ".", LogType.Warning);
				return null;
			}
			NetworkObject networkObject = UnityEngine.Object.Instantiate(fishSO.DeadPrefab, position, rotation);
			if (networkObject == null)
			{
				Log("SpawnWorldDeadFish failed. Instantiate returned null for fish " + fishSO.name + ".", LogType.Warning);
				return null;
			}
			if (!networkObject.TryGetComponent<DeadFish>(out var component))
			{
				Log("SpawnWorldDeadFish failed. Spawned prefab does not contain DeadFish component: " + networkObject.name, LogType.Warning);
				UnityEngine.Object.Destroy(networkObject.gameObject);
				return null;
			}
			component.FishId.Value = fishSO.Id;
			component.Weight.Value = fishSO.GetRandomWeight();
			networkObject.Spawn(destroyWithScene: true);
			Log($"World dead fish spawned. Fish={fishSO.name} Position={position} Rotation={rotation.eulerAngles}");
			return component;
		}

		public FishSO GetFishById(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return null;
			}
			if (_itemDatabaseSO == null || _itemDatabaseSO.CreatureItems == null)
			{
				Log("GetFishById failed. ItemDatabaseSO or CreatureItems is null.", LogType.Warning);
				return null;
			}
			return _itemDatabaseSO.CreatureItems.OfType<FishSO>().FirstOrDefault((FishSO item) => item != null && item.Id == id) ?? (_itemDatabaseSO.FindById(id) as FishSO);
		}

		public void ApplyDeadShaderState(GameObject graphic)
		{
			if (graphic == null)
			{
				return;
			}
			if (_deadMpb == null)
			{
				_deadMpb = new MaterialPropertyBlock();
			}
			_cachedDeadRenderers.Clear();
			graphic.GetComponentsInChildren(includeInactive: true, _cachedDeadRenderers);
			for (int i = 0; i < _cachedDeadRenderers.Count; i++)
			{
				Renderer renderer = _cachedDeadRenderers[i];
				if (!(renderer == null) && RendererSupportsProperty(renderer, DeadAmountId))
				{
					renderer.GetPropertyBlock(_deadMpb);
					_deadMpb.SetFloat(DeadAmountId, 1f);
					renderer.SetPropertyBlock(_deadMpb);
				}
			}
		}

		public void ApplyDeadEyeShaderState(GameObject graphic)
		{
			ApplyDeadShaderState(graphic);
		}

		private static bool RendererSupportsProperty(Renderer renderer, int propertyId)
		{
			Material[] sharedMaterials = renderer.sharedMaterials;
			if (sharedMaterials == null)
			{
				return false;
			}
			foreach (Material material in sharedMaterials)
			{
				if (material != null && material.HasProperty(propertyId))
				{
					return true;
				}
			}
			return false;
		}

		public bool TryGetCreature(string creatureId, out CreatureSO creatureSO)
		{
			creatureSO = null;
			if (string.IsNullOrWhiteSpace(creatureId))
			{
				return false;
			}
			if (_itemDatabaseSO == null || _itemDatabaseSO.CreatureItems == null)
			{
				Log("TryGetCreature failed. ItemDatabaseSO or CreatureItems is null.", LogType.Warning);
				return false;
			}
			creatureSO = _itemDatabaseSO.CreatureItems.OfType<CreatureSO>().FirstOrDefault((CreatureSO item) => item != null && item.Id == creatureId) ?? (_itemDatabaseSO.FindById(creatureId) as CreatureSO);
			return creatureSO != null;
		}

		public CreatureSO GetCreatureById(string creatureId)
		{
			if (!TryGetCreature(creatureId, out var creatureSO))
			{
				return null;
			}
			return creatureSO;
		}

		public void EndDayReset()
		{
			if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
			{
				Log("EndDayReset ignored. Only server can reset sea layers.", LogType.Warning);
			}
			else if (_isEndDayResetRunning)
			{
				Log("EndDayReset ignored. Reset is already running.", LogType.Warning);
			}
			else
			{
				StartCoroutine(ServerEndDayResetCoroutine());
			}
		}

		private IEnumerator ServerEndDayResetCoroutine()
		{
			_isEndDayResetRunning = true;
			bool completed = false;
			Exception failure = null;
			List<SeaLayer> layerSnapshot = CreateLayerSnapshot();
			List<SeaLayer> begunLayerResets = new List<SeaLayer>(layerSnapshot.Count);
			IEnumerator resetRoutine = ServerEndDayResetInternalCoroutine(layerSnapshot, begunLayerResets);
			while (true)
			{
				object obj = null;
				bool flag;
				try
				{
					flag = resetRoutine.MoveNext();
					if (flag)
					{
						obj = resetRoutine.Current;
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
			(resetRoutine as IDisposable)?.Dispose();
			CompleteBegunLayerResets(begunLayerResets);
			_isEndDayResetRunning = false;
			if (completed)
			{
				Log($"End day reset completed. LayerCount={layerSnapshot.Count}");
			}
			else
			{
				Log("End day reset interrupted. Runtime lock released after best-effort layer cleanup. Error=" + (failure?.Message ?? "Unknown"), LogType.Error);
			}
		}

		private IEnumerator ServerEndDayResetInternalCoroutine(List<SeaLayer> layerSnapshot, List<SeaLayer> begunLayerResets)
		{
			ServerResetOceanRuntimeObjects();
			ClearServerLayerOccupancy();
			for (int i = 0; i < layerSnapshot.Count; i++)
			{
				SeaLayer seaLayer = layerSnapshot[i];
				if (!(seaLayer == null))
				{
					seaLayer.ServerBeginEndDayReset();
					begunLayerResets.Add(seaLayer);
				}
			}
			List<NetworkObject> despawnQueue = new List<NetworkObject>(_creatures.Count + 32);
			HashSet<NetworkObject> uniqueObjects = new HashSet<NetworkObject>();
			CollectSpawnedCreatureNetworkObjects(despawnQueue, uniqueObjects);
			for (int j = 0; j < layerSnapshot.Count; j++)
			{
				SeaLayer seaLayer2 = layerSnapshot[j];
				if (!(seaLayer2 == null))
				{
					seaLayer2.ServerCollectSpawnedDeadFish(despawnQueue, uniqueObjects);
				}
			}
			Log($"End day reset begin. LayerCount={layerSnapshot.Count} " + $"DespawnQueue={despawnQueue.Count} BudgetPerFrame={_endDayDespawnPerFrame}");
			int totalDespawned = 0;
			int despawnedThisFrame = 0;
			int frameCount = 1;
			for (int k = 0; k < despawnQueue.Count; k++)
			{
				NetworkObject networkObject = despawnQueue[k];
				if (!(networkObject == null))
				{
					ServerPrepareNetworkObjectForEndDayDespawn(networkObject);
					if (networkObject.IsSpawned)
					{
						networkObject.Despawn();
						totalDespawned++;
						despawnedThisFrame++;
					}
					else if (networkObject.gameObject != null)
					{
						UnityEngine.Object.Destroy(networkObject.gameObject);
						totalDespawned++;
						despawnedThisFrame++;
					}
					if (despawnedThisFrame >= _endDayDespawnPerFrame)
					{
						Log($"End day despawn batch. Frame={frameCount} DespawnedThisFrame={despawnedThisFrame} Remaining={despawnQueue.Count - k - 1}");
						despawnedThisFrame = 0;
						frameCount++;
						yield return null;
					}
				}
			}
			_creatures.Clear();
			_creaturesByLayerIndex.Clear();
			Log($"End day despawn completed. LayerCount={layerSnapshot.Count} TotalDespawned={totalDespawned} Frames={frameCount}");
		}

		private List<SeaLayer> CreateLayerSnapshot()
		{
			List<SeaLayer> list = new List<SeaLayer>(_layers.Count);
			for (int i = 0; i < _layers.Count; i++)
			{
				SeaLayer seaLayer = _layers[i];
				if (!(seaLayer == null))
				{
					list.Add(seaLayer);
				}
			}
			return list;
		}

		private void CompleteBegunLayerResets(List<SeaLayer> begunLayerResets)
		{
			for (int i = 0; i < begunLayerResets.Count; i++)
			{
				SeaLayer seaLayer = begunLayerResets[i];
				if (!(seaLayer == null))
				{
					try
					{
						seaLayer.ServerCompleteEndDayReset();
					}
					catch (Exception ex)
					{
						Log("Layer end-day completion failed. Layer=" + seaLayer.name + " Error=" + ex.Message, LogType.Error);
					}
				}
			}
		}

		private void ServerResetOceanRuntimeObjects()
		{
			for (int num = _jellyfishTraps.Count - 1; num >= 0; num--)
			{
				JellyfishTrap jellyfishTrap = _jellyfishTraps[num];
				if (jellyfishTrap == null)
				{
					_jellyfishTraps.RemoveAt(num);
				}
				else
				{
					jellyfishTrap.ServerEndDayReset();
				}
			}
			for (int num2 = _octopusTraps.Count - 1; num2 >= 0; num2--)
			{
				OctopusTrap octopusTrap = _octopusTraps[num2];
				if (octopusTrap == null)
				{
					_octopusTraps.RemoveAt(num2);
				}
				else
				{
					octopusTrap.ServerEndDayReset();
				}
			}
			for (int num3 = _tottis.Count - 1; num3 >= 0; num3--)
			{
				Totti totti = _tottis[num3];
				if (totti == null)
				{
					_tottis.RemoveAt(num3);
				}
				else
				{
					totti.ServerEndDayReset();
				}
			}
		}

		private void CollectSpawnedCreatureNetworkObjects(List<NetworkObject> results, HashSet<NetworkObject> uniqueObjects)
		{
			if (results == null || uniqueObjects == null)
			{
				return;
			}
			for (int num = _creatures.Count - 1; num >= 0; num--)
			{
				SeaCreature seaCreature = _creatures[num];
				if (seaCreature == null)
				{
					_creatures.RemoveAt(num);
				}
				else
				{
					NetworkObject networkObject = seaCreature.NetworkObject;
					if (networkObject == null)
					{
						_creatures.RemoveAt(num);
					}
					else if (uniqueObjects.Add(networkObject))
					{
						results.Add(networkObject);
					}
				}
			}
		}

		private void ServerPrepareNetworkObjectForEndDayDespawn(NetworkObject networkObject)
		{
			if (!(networkObject == null) && networkObject.TryGetComponent<SeaCreature>(out var component))
			{
				component.ServerPrepareForDespawn();
			}
		}

		public void ResetAllLayerSpawnStates()
		{
			for (int i = 0; i < _layers.Count; i++)
			{
				SeaLayer seaLayer = _layers[i];
				if (!(seaLayer == null))
				{
					seaLayer.ResetSpawnState();
				}
			}
		}

		public void ClearLocalLayerActivity()
		{
			ClearLocalLayerOccupancy();
		}

		private void Log(string message, LogType type = LogType.Log)
		{
			if (_log)
			{
				UtilityMethods.Log("[SeaCreatureManager] " + message, type);
			}
		}
	}
}
