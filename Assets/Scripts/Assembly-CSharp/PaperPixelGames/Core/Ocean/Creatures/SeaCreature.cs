using System;
using System.Collections;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Indicator;
using PaperPixelGames.Framework.Utility;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	[RequireComponent(typeof(NetworkObject))]
	[RequireComponent(typeof(NetworkTransform))]
	public abstract class SeaCreature : NetworkBehaviour
	{
		[Header("Creature")]
		[SerializeField]
		protected CreatureSO _creatureSO;

		[Header("Network")]
		[SerializeField]
		protected NetworkTransform _networkTransform;

		[Header("Scan")]
		[SerializeField]
		private OffscreenIndicatorTarget _scanIndicatorTarget;

		[Header("Debug")]
		[SerializeField]
		protected bool _log;

		protected SeaLayer _currentSeaLayer;

		protected SeaArea _currentSeaArea;

		protected int _runtimeId = -1;

		protected bool _isServerSimulationActive;

		protected bool _isLocallyLayerVisible = true;

		protected bool _isLayerActivityInitialized;

		protected bool _clientVisualInitDone;

		private static readonly int SeaLayerRevealProgressId = Shader.PropertyToID("_SeaLayerRevealProgress");

		private bool _registeredWithRegistry;

		private bool _clientVisualSpawnFailureLogged;

		private Coroutine _clientInitCoroutine;

		private Renderer[] _graphicRenderers;

		private MaterialPropertyBlock _graphicPropertyBlock;

		private float _localPresentationProgress = 1f;

		private Vector3 _serverHomePosition;

		private bool _hasServerHomePosition;

		protected readonly NetworkVariable<byte> _seaLayerIndex = new NetworkVariable<byte>(0);

		protected readonly NetworkVariable<byte> _seaAreaIndex = new NetworkVariable<byte>(0);

		protected readonly NetworkVariable<FixedString64Bytes> _creatureId = new NetworkVariable<FixedString64Bytes>();

		protected readonly NetworkVariable<bool> _isStandalone = new NetworkVariable<bool>(value: false);

		protected readonly NetworkVariable<bool> _isLayerRetreating = new NetworkVariable<bool>(value: false);

		protected GameObject _activeGraphic;

		private bool _ownsActiveGraphic;

		public GameObject SpawnedGraphic => _activeGraphic;

		public CreatureSO CreatureSO => _creatureSO;

		public FishSO FishSO => _creatureSO as FishSO;

		public SeaLayer CurrentSeaLayer => _currentSeaLayer;

		public SeaArea CurrentSeaArea => _currentSeaArea;

		protected virtual bool UseEmbeddedGraphic => false;

		protected virtual GameObject EmbeddedGraphicRoot => null;

		protected virtual bool KeepEmbeddedGraphicRootActive => false;

		public byte SeaLayerIndex => _seaLayerIndex.Value;

		public byte SeaAreaIndex => _seaAreaIndex.Value;

		public string CreatureId => _creatureId.Value.ToString();

		public bool IsStandalone => _isStandalone.Value;

		public bool IsServerSimulationActive => _isServerSimulationActive;

		public bool IsLayerRetreating => _isLayerRetreating.Value;

		public bool IsLocallyLayerVisible => _isLocallyLayerVisible;

		public float LocalPresentationProgress => _localPresentationProgress;

		public OffscreenIndicatorTarget ScanIndicatorTarget => GetOrCreateScanIndicatorTarget();

		public virtual bool IsServerLayerRetreatComplete => true;

		protected virtual bool TryGetScanIndicatorIconColorOverride(out Color color)
		{
			color = default;
			return false;
		}

		protected virtual void Awake()
		{
			CacheComponents();
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			CacheComponents();
			SubscribeNetworkVariables();
			if (base.IsClient)
			{
				if (_isStandalone.Value)
				{
					CompleteStandaloneClientInit();
				}
				else
				{
					StartClientVisualInit();
				}
			}
			if (base.IsServer)
			{
				OnServerCreatureSpawned();
			}
		}

		public override void OnNetworkDespawn()
		{
			UnregisterFromRegistry();
			CleanupClientVisualInit();
			UnsubscribeNetworkVariables();
			CleanupGraphic();
			_clientVisualInitDone = false;
			_clientVisualSpawnFailureLogged = false;
			_isLayerActivityInitialized = false;
			_localPresentationProgress = 1f;
			_currentSeaLayer = null;
			_currentSeaArea = null;
			_isServerSimulationActive = false;
			_hasServerHomePosition = false;
			if (base.IsServer)
			{
				OnServerCreatureDespawned();
			}
			base.OnNetworkDespawn();
		}

		public virtual bool ServerInit(SeaLayer seaLayer, CreatureSO creatureSO, int runtimeId = -1, SeaArea seaArea = null)
		{
			if (seaLayer == null)
			{
				Log("ServerInit failed because SeaLayer is null.", LogType.Warning);
				return false;
			}
			if (creatureSO == null)
			{
				Log("ServerInit failed because CreatureSO is null.", LogType.Warning);
				return false;
			}
			SeaArea seaArea2 = ((seaArea != null) ? seaArea : seaLayer.GetRandomSeaArea());
			if (seaArea2 == null)
			{
				Log("ServerInit failed because SeaArea could not be resolved for layer " + seaLayer.name + ".", LogType.Warning);
				return false;
			}
			if (base.NetworkObject == null)
			{
				Log("ServerInit failed because NetworkObject is null.", LogType.Warning);
				return false;
			}
			_isLayerActivityInitialized = false;
			_currentSeaLayer = seaLayer;
			_currentSeaArea = seaArea2;
			_creatureSO = creatureSO;
			_runtimeId = runtimeId;
			_isStandalone.Value = false;
			_serverHomePosition = base.transform.position;
			_hasServerHomePosition = true;
			_isLayerRetreating.Value = false;
			_seaLayerIndex.Value = seaLayer.Index;
			_seaAreaIndex.Value = seaArea2.Index;
			_creatureId.Value = new FixedString64Bytes(_creatureSO.Id ?? string.Empty);
			if (!base.NetworkObject.IsSpawned)
			{
				base.NetworkObject.Spawn(destroyWithScene: true);
			}
			ConfigureScanIndicatorTarget();
			OnServerInitialized();
			TryRegisterWithRegistry();
			return true;
		}

		public virtual bool ServerInitStandalone(CreatureSO creatureSO, int runtimeId = -1)
		{
			if (creatureSO == null)
			{
				Log("Standalone ServerInit failed because CreatureSO is null.", LogType.Warning);
				return false;
			}
			if (base.NetworkObject == null)
			{
				Log("Standalone ServerInit failed because NetworkObject is null.", LogType.Warning);
				return false;
			}
			_isLayerActivityInitialized = false;
			_currentSeaLayer = null;
			_currentSeaArea = null;
			_creatureSO = creatureSO;
			_runtimeId = runtimeId;
			_hasServerHomePosition = false;
			_isStandalone.Value = true;
			_isLayerRetreating.Value = false;
			_creatureId.Value = new FixedString64Bytes(_creatureSO.Id ?? string.Empty);
			if (!base.NetworkObject.IsSpawned)
			{
				base.NetworkObject.Spawn(destroyWithScene: true);
			}
			ConfigureScanIndicatorTarget();
			OnServerInitialized();
			return true;
		}

		public virtual void SetServerSimulationActive(bool active)
		{
			if (base.IsServer && _isServerSimulationActive != active)
			{
				_isServerSimulationActive = active;
				if (active)
				{
					OnServerSimulationStarted();
					return;
				}
				OnServerSimulationStopped();
				ServerSetLayerRetreatPresentationActive(active: false);
			}
		}

		public virtual bool ServerTryBeginLayerRetreat(float speedMultiplier, out float estimatedDuration)
		{
			estimatedDuration = 0f;
			return false;
		}

		public virtual void ServerCancelLayerRetreat()
		{
		}

		public virtual void ServerForceCompleteLayerRetreat()
		{
		}

		public virtual void ApplyLayerActivity(bool locallyVisible, bool forceRefresh = false)
		{
			ApplyLocalPresentation(locallyVisible ? 1f : 0f, forceRefresh);
		}

		public virtual void ApplyLocalPresentation(float progress, bool forceRefresh = false)
		{
			float num = (ShouldForceLocalVisible() ? 1f : Mathf.Clamp01(progress));
			bool flag = num > 0.0001f;
			bool flag2 = !_isLayerActivityInitialized || _isLocallyLayerVisible != flag;
			bool flag3 = !_isLayerActivityInitialized || !Mathf.Approximately(_localPresentationProgress, num);
			if (forceRefresh || flag2 || flag3)
			{
				_isLayerActivityInitialized = true;
				_isLocallyLayerVisible = flag;
				_localPresentationProgress = num;
				if (flag)
				{
					SetGraphicActive(active: true);
				}
				ApplyGraphicRevealProgress(num);
				if (!flag)
				{
					SetGraphicActive(active: false);
				}
				if (flag2 | forceRefresh)
				{
					OnLayerActivityChanged(flag);
				}
			}
		}

		public virtual void ServerPrepareForDespawn()
		{
			if (base.IsServer)
			{
				SetServerSimulationActive(active: false);
			}
		}

		public void RefreshLayerActivity()
		{
			float progress = 1f;
			ISeaCreatureResolver seaCreatureResolver = FindService<ISeaCreatureResolver>();
			if (seaCreatureResolver != null)
			{
				progress = seaCreatureResolver.GetLocalCreaturePresentationProgress(this);
			}
			ApplyLocalPresentation(progress, forceRefresh: true);
		}

		public virtual bool CanReceiveHit(PPG_Player player)
		{
			return player != null;
		}

		[Rpc(SendTo.Server)]
		public void ServerCreatureHitRpc(NetworkObjectReference playerRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3114465655u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in playerRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3114465655u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!playerRef.TryGetComponent<PPG_Player>(out var component))
				{
					Log("ServerCreatureHitRpc failed because player reference could not be resolved.", LogType.Warning);
				}
				else
				{
					ServerTryReceiveHit(component);
				}
			}
		}

		public bool ServerTryReceiveHit(PPG_Player player)
		{
			if (!base.IsServer)
			{
				Log("ServerTryReceiveHit failed. Server only.", LogType.Warning);
				return false;
			}
			if (player == null)
			{
				Log("ServerTryReceiveHit failed. Player is null.", LogType.Warning);
				return false;
			}
			if (!CanReceiveHit(player))
			{
				Log($"ServerTryReceiveHit rejected. Player={player.PlayerName}");
				return false;
			}
			OnServerHit(player);
			return true;
		}

		public void ChangeAnim(CreatureAnim anim)
		{
			if (base.IsServer && base.IsSpawned && !(base.NetworkObject == null) && base.NetworkObject.IsSpawned)
			{
				ClientApplyAnimRpc((byte)anim);
			}
		}

		protected void ChangeAnimLocal(CreatureAnim anim)
		{
			ApplyAnim(anim);
		}

		[Rpc(SendTo.Everyone)]
		private void ClientApplyAnimRpc(byte anim)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1662944629u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in anim, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1662944629u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ApplyAnim((CreatureAnim)anim);
			}
		}

		protected virtual void CacheComponents()
		{
			if (_networkTransform == null)
			{
				TryGetComponent<NetworkTransform>(out _networkTransform);
			}
		}

		protected virtual void SubscribeNetworkVariables()
		{
			NetworkVariable<byte> seaLayerIndex = _seaLayerIndex;
			seaLayerIndex.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(seaLayerIndex.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnSeaLayerIndexChanged));
			NetworkVariable<byte> seaAreaIndex = _seaAreaIndex;
			seaAreaIndex.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(seaAreaIndex.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnSeaAreaIndexChanged));
			NetworkVariable<FixedString64Bytes> creatureId = _creatureId;
			creatureId.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Combine(creatureId.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(OnCreatureIdChanged));
			NetworkVariable<bool> isLayerRetreating = _isLayerRetreating;
			isLayerRetreating.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isLayerRetreating.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnLayerRetreatingChanged));
		}

		protected virtual void UnsubscribeNetworkVariables()
		{
			NetworkVariable<byte> seaLayerIndex = _seaLayerIndex;
			seaLayerIndex.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(seaLayerIndex.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnSeaLayerIndexChanged));
			NetworkVariable<byte> seaAreaIndex = _seaAreaIndex;
			seaAreaIndex.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(seaAreaIndex.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnSeaAreaIndexChanged));
			NetworkVariable<FixedString64Bytes> creatureId = _creatureId;
			creatureId.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Remove(creatureId.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(OnCreatureIdChanged));
			NetworkVariable<bool> isLayerRetreating = _isLayerRetreating;
			isLayerRetreating.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isLayerRetreating.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnLayerRetreatingChanged));
		}

		protected virtual void StartClientVisualInit()
		{
			if (!_clientVisualInitDone && _clientInitCoroutine == null)
			{
				_clientInitCoroutine = StartCoroutine(ClientResolveAndSpawnGraphicCoroutine());
			}
		}

		protected virtual void CleanupClientVisualInit()
		{
			if (_clientInitCoroutine != null)
			{
				StopCoroutine(_clientInitCoroutine);
				_clientInitCoroutine = null;
			}
		}

		private void CompleteStandaloneClientInit()
		{
			if (!_clientVisualInitDone)
			{
				_clientVisualInitDone = true;
				ConfigureScanIndicatorTarget();
				OnClientVisualInitialized();
				ApplyLayerActivity(locallyVisible: true, forceRefresh: true);
			}
		}

		protected virtual IEnumerator ClientResolveAndSpawnGraphicCoroutine()
		{
			float elapsed = 0f;
			while (base.IsSpawned && !_clientVisualInitDone)
			{
				if (TryResolveClientReferences())
				{
					CompleteClientVisualInit();
					if (_clientVisualInitDone)
					{
						_clientInitCoroutine = null;
						yield break;
					}
				}
				elapsed += 0.1f;
				if (elapsed >= 5f)
				{
					Log($"Client visual init still waiting. CreatureId={CreatureId}, Layer={_seaLayerIndex.Value}, Area={_seaAreaIndex.Value}", LogType.Warning);
					elapsed = 0f;
				}
				yield return new WaitForSeconds(0.1f);
			}
			_clientInitCoroutine = null;
		}

		protected virtual void CompleteClientVisualInit()
		{
			if (_clientVisualInitDone)
			{
				return;
			}
			SpawnGraphic();
			if (_activeGraphic == null)
			{
				if (!_clientVisualSpawnFailureLogged)
				{
					Log("Client visual init failed. Graphic is null. CreatureId=" + CreatureId + ", CreatureSO=" + ((_creatureSO != null) ? _creatureSO.name : "null"), LogType.Warning);
					_clientVisualSpawnFailureLogged = true;
				}
				return;
			}
			_clientVisualInitDone = true;
			_clientVisualSpawnFailureLogged = false;
			OnClientVisualInitialized();
			ConfigureScanIndicatorTarget();
			if (!base.IsServer)
			{
				TryRegisterWithRegistry();
			}
			RefreshLayerActivity();
			Log($"Client visual initialized. CreatureId={CreatureId}, Layer={_seaLayerIndex.Value}, Area={_seaAreaIndex.Value}, Graphic={_activeGraphic.name}");
		}

		protected virtual bool TryResolveClientReferences()
		{
			ISeaCreatureResolver seaCreatureResolver = FindService<ISeaCreatureResolver>();
			if (seaCreatureResolver == null)
			{
				return false;
			}
			if (_currentSeaLayer == null && seaCreatureResolver.TryGetLayer(_seaLayerIndex.Value, out var layer))
			{
				_currentSeaLayer = layer;
			}
			if (_currentSeaLayer != null && _currentSeaArea == null)
			{
				_currentSeaArea = _currentSeaLayer.GetSeaAreaByIndex(_seaAreaIndex.Value);
			}
			if (!HasValidCreatureDataForCurrentId())
			{
				string creatureId = CreatureId;
				if (!string.IsNullOrWhiteSpace(creatureId) && seaCreatureResolver != null && seaCreatureResolver.TryGetCreature(creatureId, out var creatureSO))
				{
					_creatureSO = creatureSO;
				}
			}
			if (_creatureSO == null)
			{
				return false;
			}
			if (!UseEmbeddedGraphic)
			{
				return _creatureSO.GraphicObjectPrefab != null;
			}
			return EmbeddedGraphicRoot != null;
		}

		protected virtual bool HasValidCreatureDataForCurrentId()
		{
			if (_creatureSO == null)
			{
				return false;
			}
			string creatureId = CreatureId;
			if (string.IsNullOrWhiteSpace(creatureId))
			{
				return true;
			}
			return _creatureSO.Id == creatureId;
		}

		protected virtual void SpawnGraphic()
		{
			CleanupGraphic();
			if (UseEmbeddedGraphic)
			{
				_activeGraphic = EmbeddedGraphicRoot;
				_ownsActiveGraphic = false;
				if (_activeGraphic == null)
				{
					Log("Embedded graphic is enabled but EmbeddedGraphicRoot is null.", LogType.Warning);
					return;
				}
				_activeGraphic.SetActive(value: true);
				OnGraphicSpawned(_activeGraphic);
				CacheGraphicRenderers();
			}
			else if (_creatureSO == null || _creatureSO.GraphicObjectPrefab == null)
			{
				Log("Graphic spawn failed. CreatureSO or GraphicObjectPrefab is null.", LogType.Warning);
			}
			else
			{
				_activeGraphic = UnityEngine.Object.Instantiate(_creatureSO.GraphicObjectPrefab, base.transform);
				_ownsActiveGraphic = true;
				_activeGraphic.transform.localPosition = Vector3.zero;
				_activeGraphic.transform.localRotation = Quaternion.identity;
				_activeGraphic.transform.localScale = Vector3.one;
				OnGraphicSpawned(_activeGraphic);
				CacheGraphicRenderers();
			}
		}

		protected virtual void CleanupGraphic()
		{
			if (!(_activeGraphic == null))
			{
				if (_ownsActiveGraphic)
				{
					UnityEngine.Object.Destroy(_activeGraphic);
				}
				else if (KeepEmbeddedGraphicRootActive)
				{
					SetGraphicRenderersActive(active: false);
				}
				else
				{
					_activeGraphic.SetActive(value: false);
				}
				_activeGraphic = null;
				_ownsActiveGraphic = false;
				_graphicRenderers = null;
			}
		}

		protected virtual void SetGraphicActive(bool active)
		{
			if (_activeGraphic == null)
			{
				return;
			}
			if (!_ownsActiveGraphic && KeepEmbeddedGraphicRootActive)
			{
				if (!_activeGraphic.activeSelf)
				{
					_activeGraphic.SetActive(value: true);
				}
				SetGraphicRenderersActive(active);
			}
			else if (_activeGraphic.activeSelf != active)
			{
				_activeGraphic.SetActive(active);
			}
		}

		private void SetGraphicRenderersActive(bool active)
		{
			if (_graphicRenderers == null)
			{
				CacheGraphicRenderers();
			}
			if (_graphicRenderers == null)
			{
				return;
			}
			for (int i = 0; i < _graphicRenderers.Length; i++)
			{
				Renderer renderer = _graphicRenderers[i];
				if (renderer != null)
				{
					renderer.enabled = active;
				}
			}
		}

		private void CacheGraphicRenderers()
		{
			_graphicRenderers = ((_activeGraphic != null) ? _activeGraphic.GetComponentsInChildren<Renderer>(includeInactive: true) : null);
		}

		private void ApplyGraphicRevealProgress(float progress)
		{
			if (_activeGraphic == null)
			{
				return;
			}
			if (_graphicRenderers == null)
			{
				CacheGraphicRenderers();
			}
			if (_graphicRenderers == null || _graphicRenderers.Length == 0)
			{
				return;
			}
			if (_graphicPropertyBlock == null)
			{
				_graphicPropertyBlock = new MaterialPropertyBlock();
			}
			for (int i = 0; i < _graphicRenderers.Length; i++)
			{
				Renderer renderer = _graphicRenderers[i];
				if (!(renderer == null))
				{
					renderer.GetPropertyBlock(_graphicPropertyBlock);
					_graphicPropertyBlock.SetFloat(SeaLayerRevealProgressId, progress);
					renderer.SetPropertyBlock(_graphicPropertyBlock);
					_graphicPropertyBlock.Clear();
				}
			}
		}

		protected virtual bool ShouldForceLocalVisible()
		{
			return _isLayerRetreating.Value;
		}

		protected bool TryGetServerHomePosition(out Vector3 homePosition)
		{
			homePosition = _serverHomePosition;
			if (base.IsServer)
			{
				return _hasServerHomePosition;
			}
			return false;
		}

		protected void ServerSetLayerRetreatPresentationActive(bool active)
		{
			if (base.IsServer)
			{
				bool num = _isLayerRetreating.Value != active;
				_isLayerRetreating.Value = active;
				if (num && base.IsClient)
				{
					RefreshLayerActivity();
				}
			}
		}

		protected bool ServerSetCurrentSeaArea(SeaArea seaArea)
		{
			if (!base.IsServer || seaArea == null || _currentSeaLayer == null || !_currentSeaLayer.ContainsSeaArea(seaArea))
			{
				return false;
			}
			_currentSeaArea = seaArea;
			_seaAreaIndex.Value = seaArea.Index;
			return true;
		}

		private void OnLayerRetreatingChanged(bool oldValue, bool newValue)
		{
			if (base.IsClient && oldValue != newValue)
			{
				RefreshLayerActivity();
			}
		}

		protected virtual void OnSeaLayerIndexChanged(byte oldValue, byte newValue)
		{
			if (oldValue != newValue && !base.IsServer)
			{
				_currentSeaLayer = null;
				_currentSeaArea = ((_currentSeaLayer != null) ? _currentSeaLayer.GetSeaAreaByIndex(newValue) : null);
				if (base.IsClient && !_clientVisualInitDone)
				{
					StartClientVisualInit();
				}
			}
		}

		protected virtual void OnSeaAreaIndexChanged(byte oldValue, byte newValue)
		{
			if (oldValue != newValue && !base.IsServer)
			{
				_currentSeaArea = null;
				if (base.IsClient && !_clientVisualInitDone)
				{
					StartClientVisualInit();
				}
			}
		}

		protected virtual void OnCreatureIdChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
		{
			if (!oldValue.Equals(newValue))
			{
				if (!HasValidCreatureDataForCurrentId())
				{
					_creatureSO = null;
				}
				if (base.IsClient && !_clientVisualInitDone)
				{
					StartClientVisualInit();
				}
			}
		}

		protected virtual void TryRegisterWithRegistry()
		{
			if (!_registeredWithRegistry)
			{
				ISeaCreatureRegistry seaCreatureRegistry = FindService<ISeaCreatureRegistry>();
				if (seaCreatureRegistry != null)
				{
					seaCreatureRegistry.RegisterCreature(this);
					_registeredWithRegistry = true;
				}
			}
		}

		protected virtual void UnregisterFromRegistry()
		{
			if (_registeredWithRegistry)
			{
				ISeaCreatureRegistry seaCreatureRegistry = FindService<ISeaCreatureRegistry>();
				if (seaCreatureRegistry != null)
				{
					seaCreatureRegistry.UnregisterCreature(this);
					_registeredWithRegistry = false;
				}
			}
		}

		protected virtual T FindService<T>() where T : class
		{
			if (!SeaCreatureManager.TryGetActiveInstance(out var manager))
			{
				return null;
			}
			return manager as T;
		}

		protected virtual void OnServerCreatureSpawned()
		{
		}

		protected virtual void OnServerCreatureDespawned()
		{
		}

		protected virtual void OnServerInitialized()
		{
		}

		protected virtual void OnServerSimulationStarted()
		{
		}

		protected virtual void OnServerSimulationStopped()
		{
		}

		protected virtual void OnClientVisualInitialized()
		{
		}

		protected virtual void OnGraphicSpawned(GameObject graphic)
		{
		}

		protected virtual void OnLayerActivityChanged(bool locallyVisible)
		{
		}

		protected virtual void RaiseCreatureHitEvent(PPG_Player hitter)
		{
			EventBus<CreatureHitEvent>.Raise(new CreatureHitEvent(this, hitter, base.transform.position));
		}

		protected virtual void RaiseCreatureDiedEvent(PPG_Player killer)
		{
			EventBus<CreatureDiedEvent>.Raise(new CreatureDiedEvent(this, killer, base.transform.position));
		}

		protected virtual void OnServerHit(PPG_Player player)
		{
			RaiseCreatureHitEvent(player);
			Log($"Hit by {player.PlayerName}");
		}

		protected virtual void ApplyAnim(CreatureAnim anim)
		{
		}

		private OffscreenIndicatorTarget GetOrCreateScanIndicatorTarget()
		{
			if (_scanIndicatorTarget == null)
			{
				_scanIndicatorTarget = GetComponentInChildren<OffscreenIndicatorTarget>(includeInactive: true);
			}
			if (_scanIndicatorTarget == null)
			{
				_scanIndicatorTarget = base.gameObject.AddComponent<OffscreenIndicatorTarget>();
			}
			ConfigureScanIndicatorTarget();
			return _scanIndicatorTarget;
		}

		private void ConfigureScanIndicatorTarget()
		{
			if (!(_scanIndicatorTarget == null) && !(_creatureSO == null) && !(_creatureSO.Sprite == null))
			{
				_scanIndicatorTarget.UpdateIcon(_creatureSO.Sprite);
				if (TryGetScanIndicatorIconColorOverride(out var color))
				{
					_scanIndicatorTarget.Settings.ApplyIconColor = true;
					_scanIndicatorTarget.Settings.IconColor = color;
				}
			}
		}

		public virtual void ServerCreatureSens(PPG_Player player, byte sensType)
		{
			if (base.IsServer)
			{
				Log(string.Format("ServerCreatureSens type={0} player={1}", sensType, (player != null) ? ((object)player.PlayerName) : ((object)"null")));
			}
		}

		protected void Log(string message, LogType type = LogType.Log)
		{
			if (_log)
			{
				UtilityMethods.Log("[" + GetType().Name + "] " + message, type);
			}
		}

		protected override void __initializeVariables()
		{
			if (_seaLayerIndex == null)
			{
				throw new Exception("SeaCreature._seaLayerIndex cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_seaLayerIndex.Initialize(this);
			__nameNetworkVariable(_seaLayerIndex, "_seaLayerIndex");
			NetworkVariableFields.Add(_seaLayerIndex);
			if (_seaAreaIndex == null)
			{
				throw new Exception("SeaCreature._seaAreaIndex cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_seaAreaIndex.Initialize(this);
			__nameNetworkVariable(_seaAreaIndex, "_seaAreaIndex");
			NetworkVariableFields.Add(_seaAreaIndex);
			if (_creatureId == null)
			{
				throw new Exception("SeaCreature._creatureId cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_creatureId.Initialize(this);
			__nameNetworkVariable(_creatureId, "_creatureId");
			NetworkVariableFields.Add(_creatureId);
			if (_isStandalone == null)
			{
				throw new Exception("SeaCreature._isStandalone cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isStandalone.Initialize(this);
			__nameNetworkVariable(_isStandalone, "_isStandalone");
			NetworkVariableFields.Add(_isStandalone);
			if (_isLayerRetreating == null)
			{
				throw new Exception("SeaCreature._isLayerRetreating cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isLayerRetreating.Initialize(this);
			__nameNetworkVariable(_isLayerRetreating, "_isLayerRetreating");
			NetworkVariableFields.Add(_isLayerRetreating);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3114465655u, __rpc_handler_3114465655, "ServerCreatureHitRpc", RpcInvokePermission.Everyone);
			__registerRpc(1662944629u, __rpc_handler_1662944629, "ClientApplyAnimRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3114465655(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((SeaCreature)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SeaCreature)target).ServerCreatureHitRpc(value);
				((SeaCreature)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1662944629(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((SeaCreature)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SeaCreature)target).ClientApplyAnimRpc(value);
				((SeaCreature)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "SeaCreature";
		}
	}
}
