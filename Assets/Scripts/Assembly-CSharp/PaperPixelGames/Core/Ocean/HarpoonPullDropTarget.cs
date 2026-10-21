using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using EPOOutline;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.PlayerTools.Harpoon;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace PaperPixelGames.Core.Ocean
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(NetworkObject))]
	public sealed class HarpoonPullDropTarget : NetworkBehaviour
	{
		private const float PullSessionStartTimeoutSeconds = 8f;

		[Header("References")]
		[FormerlySerializedAs("_visualRoot")]
		[SerializeField]
		private GameObject _fallingVisualRoot;

		[SerializeField]
		private Transform _hookPoint;

		[SerializeField]
		private Outlinable _outlinable;

		[Header("FX")]
		[SerializeField]
		private ParticleSystem _fallVfxPrimary;

		[SerializeField]
		private ParticleSystem _fallVfxSecondary;

		[SerializeField]
		private ParticleSystem _fallVfxTertiary;

		[SerializeField]
		private AudioClipSO _pullSuccessSfx;

		[SerializeField]
		private AudioClipSO _fallCompleteSfx;

		[Header("Fall")]
		[SerializeField]
		[Min(0f)]
		private float _fallDistance = 2f;

		[Tooltip("Minimum fall duration, including boss-configured drops.")]
		[SerializeField]
		[Min(0.01f)]
		private float _fallDuration = 0.6f;

		[SerializeField]
		private Ease _fallEase = Ease.InQuad;

		[Header("Challenge")]
		[SerializeField]
		private FishingMode _challengeFishingMode = FishingMode.Spam;

		[SerializeField]
		private CreatureRarity _challengeRarity;

		[SerializeField]
		[Min(1f)]
		private int _successZoneCount = 1;

		[Header("Reset")]
		[SerializeField]
		[Min(0f)]
		private float _resetHeightOffset = 1f;

		[SerializeField]
		[Min(0.01f)]
		private float _resetDuration = 0.45f;

		[SerializeField]
		private Ease _resetEase = Ease.OutBack;

		[SerializeField]
		[Min(0f)]
		private float _resetShakeStrength = 0.035f;

		[SerializeField]
		[Min(1f)]
		private int _resetShakeVibrato = 8;

		[SerializeField]
		[Range(0f, 180f)]
		private float _resetShakeRandomness = 45f;

		private readonly NetworkVariable<bool> _canBeHooked = new NetworkVariable<bool>(value: true);

		private readonly NetworkVariable<bool> _encounterAvailable = new NetworkVariable<bool>(value: true);

		private readonly NetworkVariable<bool> _bossFallCompleted = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<Vector3> _bossFallTargetPosition = new NetworkVariable<Vector3>();

		private readonly NetworkVariable<bool> _bossOutlineVisible = new NetworkVariable<bool>(value: false);

		private Vector3 _originalPosition;

		private Quaternion _originalRotation;

		private Vector3 _originalFallingPosition;

		private Quaternion _originalFallingRotation;

		private Collider[] _interactionColliders;

		private Collider[] _fallingPieceColliders;

		private Sequence _activeSequence;

		private Coroutine _serverFallCompleteCoroutine;

		private HarpoonTool _currentHarpoon;

		private bool _pullSessionActive;

		private bool _pullSessionFinished;

		private float _pullSessionStartedAt;

		private bool _serverFallCompletedThisDrop;

		private ulong _pullPrimaryClientId;

		private int _pullZoneCount;

		private HarpoonSuccessZoneNetData _pullSuccessZoneNetData;

		private bool _hasPullSuccessZoneNetData;

		private readonly HashSet<ulong> _pullParticipantClientIds = new HashSet<ulong>();

		private readonly HashSet<int> _pullCompletedZoneIndexes = new HashSet<int>();

		private bool _hasConfiguredFallImpactHeight;

		private float _configuredFallImpactHeight;

		private float _configuredFallSpeed;

		public Transform HookPoint
		{
			get
			{
				if (!(_hookPoint != null))
				{
					return base.transform;
				}
				return _hookPoint;
			}
		}

		public bool CanBeHooked
		{
			get
			{
				if (_encounterAvailable.Value)
				{
					return _canBeHooked.Value;
				}
				return false;
			}
		}

		public FishingMode ChallengeFishingMode => _challengeFishingMode;

		public CreatureRarity ChallengeRarity => _challengeRarity;

		public int SuccessZoneCount => Mathf.Max(1, _successZoneCount);

		public bool PullSessionActive => _pullSessionActive;

		public bool PullSessionFinished => _pullSessionFinished;

		public ulong PullPrimaryClientId => _pullPrimaryClientId;

		private Transform FallingTransform
		{
			get
			{
				if (!(_fallingVisualRoot != null))
				{
					return null;
				}
				return _fallingVisualRoot.transform;
			}
		}

		public event Action<HarpoonPullDropTarget> ServerFallCompleted;

		private void Awake()
		{
			CacheOriginalTransform();
			CacheInteractionColliders();
			ApplyOutlineAvailability();
		}

		private void Update()
		{
			if (base.IsServer && base.IsSpawned && _pullSessionActive && !_pullSessionFinished && (_currentHarpoon == null || !_currentHarpoon.IsSpawned || (!_hasPullSuccessZoneNetData && Time.realtimeSinceStartup - _pullSessionStartedAt >= 8f)))
			{
				ServerCancelPullSession(PullDropTargetCancelReason.PrimaryCancelled);
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			CacheOriginalTransform();
			CacheInteractionColliders();
			NetworkVariable<bool> canBeHooked = _canBeHooked;
			canBeHooked.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(canBeHooked.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnHookAvailabilityChanged));
			NetworkVariable<bool> encounterAvailable = _encounterAvailable;
			encounterAvailable.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(encounterAvailable.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnEncounterAvailabilityChanged));
			NetworkVariable<bool> bossOutlineVisible = _bossOutlineVisible;
			bossOutlineVisible.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(bossOutlineVisible.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnBossOutlineVisibilityChanged));
			if (base.IsServer && base.NetworkManager != null)
			{
				base.NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
			}
			ApplyInteractionAvailability();
			ApplyEncounterVisualAvailability(_encounterAvailable.Value);
			ApplyOutlineAvailability();
			ApplyLateJoinFallState();
		}

		public override void OnNetworkDespawn()
		{
			if (base.IsServer && base.NetworkManager != null)
			{
				base.NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
			}
			NetworkVariable<bool> canBeHooked = _canBeHooked;
			canBeHooked.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(canBeHooked.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnHookAvailabilityChanged));
			NetworkVariable<bool> encounterAvailable = _encounterAvailable;
			encounterAvailable.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(encounterAvailable.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnEncounterAvailabilityChanged));
			NetworkVariable<bool> bossOutlineVisible = _bossOutlineVisible;
			bossOutlineVisible.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(bossOutlineVisible.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnBossOutlineVisibilityChanged));
			StopServerFallCompleteTimer();
			KillActiveSequence();
			SetOutlineActive(active: false);
			base.OnNetworkDespawn();
		}

		private void OnClientDisconnected(ulong clientId)
		{
			if (_pullSessionActive && !_pullSessionFinished)
			{
				if (clientId == _pullPrimaryClientId)
				{
					ServerCancelPullSession(PullDropTargetCancelReason.PrimaryCancelled);
				}
				else
				{
					ServerLeavePullSession(clientId);
				}
			}
		}

		public override void OnDestroy()
		{
			StopServerFallCompleteTimer();
			KillActiveSequence();
			base.OnDestroy();
		}

		public void ServerConfigureBossFall(float impactHeight, float fallSpeed)
		{
			if (base.IsServer && !float.IsNaN(impactHeight) && !float.IsInfinity(impactHeight) && !float.IsNaN(fallSpeed) && !float.IsInfinity(fallSpeed) && !(fallSpeed <= 0f))
			{
				_hasConfiguredFallImpactHeight = true;
				_configuredFallImpactHeight = impactHeight;
				_configuredFallSpeed = fallSpeed;
			}
		}

		public void ServerSetBossEncounterAvailability(bool available)
		{
			if (base.IsServer)
			{
				ServerCancelPullSession(PullDropTargetCancelReason.TargetReset);
				StopServerFallCompleteTimer();
				_serverFallCompletedThisDrop = false;
				_bossFallCompleted.Value = false;
				_bossFallTargetPosition.Value = _originalFallingPosition;
				_currentHarpoon = null;
				_encounterAvailable.Value = available;
				_canBeHooked.Value = available;
				ClientApplyBossEncounterAvailabilityRpc(available);
			}
		}

		public void ServerSetBossOutlineVisibility(bool visible)
		{
			if (base.IsServer)
			{
				_bossOutlineVisible.Value = visible;
			}
		}

		public void RequestHook(HarpoonTool harpoon, Vector3 hitPoint, Vector3 hitNormal)
		{
			if (!(harpoon == null) && !(harpoon.NetworkObject == null))
			{
				_currentHarpoon = harpoon;
				if (base.IsServer)
				{
					ServerSetHooked(harpoon);
				}
				else
				{
					ServerRequestHookRpc(harpoon.NetworkObject.ToReference());
				}
			}
		}

		public void CompletePull(HarpoonTool harpoon)
		{
			if (!(harpoon == null) && !(harpoon.NetworkObject == null))
			{
				if (base.IsServer)
				{
					ServerCompletePull(harpoon);
				}
				else
				{
					ServerCompletePullRpc(harpoon.NetworkObject.ToReference());
				}
			}
		}

		public void CancelHook(HarpoonTool harpoon)
		{
			if (!(harpoon == null) && !(harpoon.NetworkObject == null))
			{
				if (base.IsServer)
				{
					ServerCancelHook(harpoon);
				}
				else
				{
					ServerCancelHookRpc(harpoon.NetworkObject.ToReference());
				}
			}
		}

		public void ResetTarget()
		{
			if (base.IsServer)
			{
				ServerResetTarget();
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestHookRpc(NetworkObjectReference harpoonReference, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(375487686u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in harpoonReference, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 375487686u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (harpoonReference.TryGetComponent<HarpoonTool>(out var component) && component.IsSpawned && component.OwnerClientId == rpcParams.Receive.SenderClientId)
				{
					ServerSetHooked(component);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerCompletePullRpc(NetworkObjectReference harpoonReference, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(4085415091u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in harpoonReference, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 4085415091u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (harpoonReference.TryGetComponent<HarpoonTool>(out var component) && component.IsSpawned && component.OwnerClientId == rpcParams.Receive.SenderClientId)
				{
					ServerCompletePull(component);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerCancelHookRpc(NetworkObjectReference harpoonReference, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2370416124u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in harpoonReference, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 2370416124u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (harpoonReference.TryGetComponent<HarpoonTool>(out var component) && component.IsSpawned && component.OwnerClientId == rpcParams.Receive.SenderClientId)
				{
					ServerCancelHook(component);
				}
			}
		}

		private void ServerSetHooked(HarpoonTool harpoon)
		{
			ServerBeginPullSession(harpoon, SuccessZoneCount);
		}

		private void ServerCompletePull(HarpoonTool harpoon)
		{
			if (base.IsServer && !(harpoon == null) && _encounterAvailable.Value && _pullSessionActive && _pullSessionFinished && harpoon.OwnerClientId == _pullPrimaryClientId && !_bossFallCompleted.Value)
			{
				_currentHarpoon = null;
				_canBeHooked.Value = false;
				ServerStartFall();
			}
		}

		private void ServerCancelHook(HarpoonTool harpoon)
		{
			if (!base.IsServer || harpoon == null || _bossFallCompleted.Value)
			{
				return;
			}
			if (!_pullSessionActive)
			{
				if (!(_currentHarpoon != null) || !(_currentHarpoon != harpoon))
				{
					_currentHarpoon = null;
					_canBeHooked.Value = _encounterAvailable.Value;
				}
			}
			else if (harpoon.OwnerClientId == _pullPrimaryClientId)
			{
				ServerCancelPullSession(PullDropTargetCancelReason.PrimaryCancelled);
			}
			else
			{
				ServerLeavePullSession(harpoon.OwnerClientId);
			}
		}

		private void ServerResetTarget()
		{
			ServerCancelPullSession(PullDropTargetCancelReason.TargetReset);
			_currentHarpoon = null;
			_canBeHooked.Value = false;
			_serverFallCompletedThisDrop = false;
			_bossFallCompleted.Value = false;
			_bossFallTargetPosition.Value = _originalFallingPosition;
			StopServerFallCompleteTimer();
			ClientPlayResetRpc();
		}

		public bool ServerBeginPullSession(HarpoonTool primaryHarpoon, int zoneCount)
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (!CanServerAcceptHook(primaryHarpoon))
			{
				return false;
			}
			ServerClearPullSession();
			ClearPullSuccessZoneNetData();
			_currentHarpoon = primaryHarpoon;
			_canBeHooked.Value = false;
			_pullSessionActive = true;
			_pullSessionFinished = false;
			_pullSessionStartedAt = Time.realtimeSinceStartup;
			_pullPrimaryClientId = primaryHarpoon.OwnerClientId;
			_pullZoneCount = Mathf.Clamp(zoneCount, 1, 8);
			_pullParticipantClientIds.Add(primaryHarpoon.OwnerClientId);
			ClientPullParticipantCountChangedRpc(_pullParticipantClientIds.Count);
			return true;
		}

		public bool ServerCanJoinPullSession(HarpoonTool harpoon)
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (harpoon == null)
			{
				return false;
			}
			if (!ServerHasActivePullSession())
			{
				return false;
			}
			if (harpoon.OwnerClientId == _pullPrimaryClientId)
			{
				return false;
			}
			return true;
		}

		public bool ServerTryJoinPullSession(HarpoonTool harpoon)
		{
			if (!ServerCanJoinPullSession(harpoon))
			{
				return false;
			}
			if (_pullParticipantClientIds.Add(harpoon.OwnerClientId))
			{
				ClientPullParticipantCountChangedRpc(_pullParticipantClientIds.Count);
			}
			return true;
		}

		public bool ServerLeavePullSession(ulong clientId)
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (!_pullSessionActive)
			{
				return false;
			}
			bool num = _pullParticipantClientIds.Remove(clientId);
			if (num)
			{
				ClientPullParticipantCountChangedRpc(_pullParticipantClientIds.Count);
			}
			return num;
		}

		public void ServerSetPullSuccessZoneNetData(HarpoonSuccessZoneNetData zoneNetData)
		{
			if (base.IsServer && ServerHasActivePullSession())
			{
				int num = Mathf.Clamp(zoneNetData.Count, 0, 8);
				if (num > 0)
				{
					_pullZoneCount = num;
					zoneNetData.Count = (byte)num;
					_pullSuccessZoneNetData = zoneNetData;
					_hasPullSuccessZoneNetData = true;
				}
			}
		}

		public bool ServerTryGetPullSuccessZoneNetData(out HarpoonSuccessZoneNetData zoneNetData)
		{
			zoneNetData = _pullSuccessZoneNetData;
			if (!base.IsServer)
			{
				return false;
			}
			if (!ServerHasActivePullSession())
			{
				return false;
			}
			if (!_hasPullSuccessZoneNetData)
			{
				return false;
			}
			return zoneNetData.Count > 0;
		}

		public byte ServerGetCompletedPullZoneMask()
		{
			if (!base.IsServer)
			{
				return 0;
			}
			byte b = 0;
			foreach (int pullCompletedZoneIndex in _pullCompletedZoneIndexes)
			{
				if (pullCompletedZoneIndex >= 0 && pullCompletedZoneIndex <= 7)
				{
					b |= (byte)(1 << pullCompletedZoneIndex);
				}
			}
			return b;
		}

		public float ServerGetPullGlobalProgress01()
		{
			if (!base.IsServer || _pullZoneCount <= 0)
			{
				return 0f;
			}
			return Mathf.Clamp01((float)_pullCompletedZoneIndexes.Count / (float)_pullZoneCount);
		}

		[Rpc(SendTo.Server)]
		public void ServerReportPullZoneCompletedRpc(int zoneIndex, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(627384091u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, zoneIndex);
				__endSendRpc(ref bufferWriter, 627384091u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsServer || !ServerHasActivePullSession())
			{
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (_pullParticipantClientIds.Contains(senderClientId) && zoneIndex >= 0 && zoneIndex < _pullZoneCount && _pullCompletedZoneIndexes.Add(zoneIndex))
			{
				bool num = _pullCompletedZoneIndexes.Count >= _pullZoneCount;
				if (num)
				{
					_pullSessionFinished = true;
				}
				ClientPullZoneCompletedRpc(zoneIndex, ServerGetPullGlobalProgress01());
				if (num)
				{
					ClientPullFinishedRpc(_pullPrimaryClientId);
					ServerFinishPullSession();
				}
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerLeavePullSessionRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(956504057u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 956504057u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsServer)
			{
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				if (senderClientId == _pullPrimaryClientId)
				{
					ServerCancelPullSession(PullDropTargetCancelReason.PrimaryCancelled);
				}
				else
				{
					ServerLeavePullSession(senderClientId);
				}
			}
		}

		private bool CanServerAcceptHook(HarpoonTool harpoon)
		{
			if (harpoon == null || !harpoon.IsSpawned)
			{
				return false;
			}
			if (!CanBeHooked || _bossFallCompleted.Value || _pullSessionFinished)
			{
				return false;
			}
			if (!base.isActiveAndEnabled)
			{
				return false;
			}
			return true;
		}

		private bool ServerHasActivePullSession()
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (!_pullSessionActive)
			{
				return false;
			}
			if (_pullSessionFinished)
			{
				return false;
			}
			return !_canBeHooked.Value;
		}

		private void ServerFinishPullSession()
		{
			if (base.IsServer)
			{
				_currentHarpoon = null;
				_canBeHooked.Value = false;
				ServerStartFall();
			}
		}

		private void ServerCancelPullSession(PullDropTargetCancelReason reason)
		{
			if (base.IsServer)
			{
				if (_pullSessionActive && !_pullSessionFinished)
				{
					ClientPullCancelledRpc(_pullPrimaryClientId, reason);
				}
				ServerClearPullSession();
				_currentHarpoon = null;
				_canBeHooked.Value = _encounterAvailable.Value && !_bossFallCompleted.Value;
			}
		}

		private void ServerStartFall()
		{
			if (base.IsServer && _serverFallCompleteCoroutine == null && !_serverFallCompletedThisDrop)
			{
				ResolveFallPresentation(out var targetPosition, out var duration);
				_bossFallTargetPosition.Value = targetPosition;
				_bossFallCompleted.Value = true;
				ClientPlayFallRpc(targetPosition, duration);
				_serverFallCompleteCoroutine = StartCoroutine(ServerFallCompleteRoutine(duration));
			}
		}

		private IEnumerator ServerFallCompleteRoutine(float duration)
		{
			yield return new WaitForSeconds(duration);
			_serverFallCompleteCoroutine = null;
			if (base.IsServer && !_serverFallCompletedThisDrop)
			{
				_serverFallCompletedThisDrop = true;
				ServerFallCompleted?.Invoke(this);
			}
		}

		private void StopServerFallCompleteTimer()
		{
			if (_serverFallCompleteCoroutine != null)
			{
				StopCoroutine(_serverFallCompleteCoroutine);
				_serverFallCompleteCoroutine = null;
			}
		}

		private void ServerClearPullSession()
		{
			_pullSessionActive = false;
			_pullSessionFinished = false;
			_pullSessionStartedAt = 0f;
			_pullPrimaryClientId = 0uL;
			_pullZoneCount = 0;
			_pullParticipantClientIds.Clear();
			_pullCompletedZoneIndexes.Clear();
			ClearPullSuccessZoneNetData();
		}

		private void ClearPullSuccessZoneNetData()
		{
			_pullSuccessZoneNetData.Clear();
			_hasPullSuccessZoneNetData = false;
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientPullZoneCompletedRpc(int zoneIndex, float globalProgress01)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1216156550u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, zoneIndex);
				bufferWriter.WriteValueSafe(in globalProgress01, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1216156550u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				EventBus<PullDropTargetZoneCompletedEvent>.Raise(new PullDropTargetZoneCompletedEvent(this, zoneIndex, globalProgress01));
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientPullFinishedRpc(ulong primaryClientId)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(4282101129u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, primaryClientId);
				__endSendRpc(ref bufferWriter, 4282101129u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				EventBus<PullDropTargetFinishedEvent>.Raise(new PullDropTargetFinishedEvent(this, primaryClientId));
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientPullCancelledRpc(ulong primaryClientId, PullDropTargetCancelReason reason)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3970350070u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, primaryClientId);
				bufferWriter.WriteValueSafe(in reason, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 3970350070u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				EventBus<PullDropTargetCancelledEvent>.Raise(new PullDropTargetCancelledEvent(this, primaryClientId, reason));
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientPullParticipantCountChangedRpc(int participantCount)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3348775044u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, participantCount);
				__endSendRpc(ref bufferWriter, 3348775044u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				EventBus<PullDropTargetParticipantCountChangedEvent>.Raise(new PullDropTargetParticipantCountChangedEvent(this, participantCount));
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientPlayFallRpc(Vector3 targetPosition, float duration)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1159008444u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in targetPosition);
				bufferWriter.WriteValueSafe(in duration, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1159008444u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				PlayFall(targetPosition, duration);
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientPlayResetRpc()
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1374193079u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1374193079u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				PlayReset();
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientApplyBossEncounterAvailabilityRpc(bool available)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1158114396u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in available, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1158114396u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				KillActiveSequence();
				ResetPresentationTransforms();
				ApplyEncounterVisualAvailability(available);
				ApplyInteractionAvailability();
			}
		}

		private void PlayFall(Vector3 targetPosition, float duration)
		{
			KillActiveSequence();
			SetFallingVisualActive(active: true);
			Transform fallingTransform = FallingTransform;
			if (!(fallingTransform == null))
			{
				if (Singleton<SoundManager>.Instance != null && _pullSuccessSfx != null)
				{
					Singleton<SoundManager>.Instance.PlaySFX(_pullSuccessSfx, base.transform.position, base.transform);
				}
				_activeSequence = DOTween.Sequence();
				_activeSequence.Append(fallingTransform.DOMove(targetPosition, duration).SetEase(_fallEase)).OnComplete(HandleFallCompleted).OnKill(() =>
				{
					_activeSequence = null;
				});
			}
		}

		private void ResolveFallPresentation(out Vector3 targetPosition, out float duration)
		{
			targetPosition = _originalFallingPosition + Vector3.down * _fallDistance;
			duration = Mathf.Max(0.01f, _fallDuration);
			if (_hasConfiguredFallImpactHeight && !(_configuredFallSpeed <= 0f))
			{
				float num = ResolveLowestVisualOffsetFromRoot();
				targetPosition = _originalFallingPosition;
				targetPosition.y = _configuredFallImpactHeight - num;
				float num2 = Mathf.Abs(_originalFallingPosition.y - targetPosition.y);
				duration = Mathf.Max(duration, num2 / _configuredFallSpeed);
			}
		}

		private float ResolveLowestVisualOffsetFromRoot()
		{
			Transform fallingTransform = FallingTransform;
			if (fallingTransform == null)
			{
				return 0f;
			}
			Renderer[] componentsInChildren = fallingTransform.GetComponentsInChildren<Renderer>(includeInactive: true);
			bool flag = false;
			float num = fallingTransform.position.y;
			foreach (Renderer renderer in componentsInChildren)
			{
				if (!(renderer == null) && !(renderer is ParticleSystemRenderer))
				{
					float y = renderer.bounds.min.y;
					if (!flag || y < num)
					{
						flag = true;
						num = y;
					}
				}
			}
			if (!flag)
			{
				return 0f;
			}
			return num - fallingTransform.position.y;
		}

		private void PlayReset()
		{
			KillActiveSequence();
			SetFallingVisualActive(active: true);
			Transform fallingTransform = FallingTransform;
			if (!(fallingTransform == null))
			{
				fallingTransform.SetPositionAndRotation(_originalFallingPosition + Vector3.up * _resetHeightOffset, _originalFallingRotation);
				_activeSequence = DOTween.Sequence();
				_activeSequence.Append(fallingTransform.DOMove(_originalFallingPosition, _resetDuration).SetEase(_resetEase)).Join(fallingTransform.DORotateQuaternion(_originalFallingRotation, _resetDuration).SetEase(_resetEase));
				Tween tween = CreateResetShakeTween();
				if (tween != null)
				{
					_activeSequence.Join(tween);
				}
				_activeSequence.OnComplete(HandleResetCompleted).OnKill(() =>
				{
					_activeSequence = null;
				});
			}
		}

		private Tween CreateResetShakeTween()
		{
			if (_resetShakeStrength <= 0f)
			{
				return null;
			}
			Transform fallingTransform = FallingTransform;
			if (fallingTransform == null)
			{
				return null;
			}
			return fallingTransform.DOShakePosition(_resetDuration, _resetShakeStrength, _resetShakeVibrato, _resetShakeRandomness).SetRelative();
		}

		private void HandleResetCompleted()
		{
			ResetPresentationTransforms();
			SetFallingVisualActive(_encounterAvailable.Value);
			if (base.IsServer)
			{
				_canBeHooked.Value = _encounterAvailable.Value && !_bossFallCompleted.Value;
			}
		}

		private void HandleFallCompleted()
		{
			Transform fallingTransform = FallingTransform;
			Vector3 vector = ((fallingTransform != null) ? (fallingTransform.position + Vector3.up * ResolveLowestVisualOffsetFromRoot()) : base.transform.position);
			SetFallingVisualActive(active: false);
			if (Singleton<SoundManager>.Instance != null && _fallCompleteSfx != null)
			{
				Singleton<SoundManager>.Instance.PlaySFX(_fallCompleteSfx, vector);
			}
			PlayFallImpactVfx(_fallVfxPrimary, vector);
			PlayFallImpactVfx(_fallVfxSecondary, vector);
			PlayFallImpactVfx(_fallVfxTertiary, vector);
		}

		private static void PlayFallImpactVfx(ParticleSystem vfx, Vector3 impactPosition)
		{
			if (!(vfx == null))
			{
				vfx.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
				vfx.transform.position = impactPosition;
				vfx.Play(withChildren: true);
			}
		}

		private void SetFallingVisualActive(bool active)
		{
			if (_fallingVisualRoot != null && _fallingVisualRoot.activeSelf != active)
			{
				_fallingVisualRoot.SetActive(active);
			}
		}

		private void CacheInteractionColliders()
		{
			Collider[] componentsInChildren = GetComponentsInChildren<Collider>(includeInactive: true);
			Transform fallingTransform = FallingTransform;
			if (fallingTransform == null || fallingTransform == base.transform)
			{
				_interactionColliders = componentsInChildren;
				_fallingPieceColliders = Array.Empty<Collider>();
				return;
			}
			List<Collider> list = new List<Collider>();
			List<Collider> list2 = new List<Collider>();
			Transform transform = fallingTransform;
			foreach (Collider collider in componentsInChildren)
			{
				if (!(collider == null))
				{
					if (collider.transform == transform || collider.transform.IsChildOf(transform))
					{
						list2.Add(collider);
					}
					else
					{
						list.Add(collider);
					}
				}
			}
			_interactionColliders = list.ToArray();
			_fallingPieceColliders = list2.ToArray();
		}

		private void OnHookAvailabilityChanged(bool previousValue, bool currentValue)
		{
			ApplyInteractionAvailability();
			ApplyOutlineAvailability();
		}

		private void OnEncounterAvailabilityChanged(bool previousValue, bool currentValue)
		{
			ApplyEncounterVisualAvailability(currentValue);
			ApplyInteractionAvailability();
			ApplyOutlineAvailability();
		}

		private void OnBossOutlineVisibilityChanged(bool previousValue, bool currentValue)
		{
			ApplyOutlineAvailability();
		}

		private void ApplyEncounterVisualAvailability(bool available)
		{
			SetFallingVisualActive(available);
		}

		private void ApplyInteractionAvailability()
		{
			if (_interactionColliders == null)
			{
				return;
			}
			bool flag = _encounterAvailable.Value && _canBeHooked.Value;
			for (int i = 0; i < _interactionColliders.Length; i++)
			{
				Collider collider = _interactionColliders[i];
				if (collider != null)
				{
					collider.enabled = flag;
				}
			}
			if (_fallingPieceColliders == null)
			{
				return;
			}
			for (int j = 0; j < _fallingPieceColliders.Length; j++)
			{
				Collider collider2 = _fallingPieceColliders[j];
				if (collider2 != null)
				{
					collider2.enabled = false;
				}
			}
		}

		private void ApplyOutlineAvailability()
		{
			SetOutlineActive(_bossOutlineVisible.Value && _encounterAvailable.Value && _canBeHooked.Value && !_bossFallCompleted.Value);
		}

		private void SetOutlineActive(bool active)
		{
			if (_outlinable != null && _outlinable.enabled != active)
			{
				_outlinable.enabled = active;
			}
		}

		public bool ServerRepairBossInteractionColliders()
		{
			if (!base.IsServer || !base.IsSpawned || !base.isActiveAndEnabled || !base.gameObject.activeInHierarchy)
			{
				return false;
			}
			CacheInteractionColliders();
			ApplyInteractionAvailability();
			if (!CanBeHooked || _interactionColliders == null || _interactionColliders.Length == 0)
			{
				return false;
			}
			for (int i = 0; i < _interactionColliders.Length; i++)
			{
				if (_interactionColliders[i] != null && _interactionColliders[i].enabled)
				{
					return true;
				}
			}
			return false;
		}

		private void ApplyLateJoinFallState()
		{
			if (_bossFallCompleted.Value)
			{
				KillActiveSequence();
				if (FallingTransform != null)
				{
					FallingTransform.position = _bossFallTargetPosition.Value;
				}
				SetFallingVisualActive(active: false);
			}
		}

		private void CacheOriginalTransform()
		{
			_originalPosition = base.transform.position;
			_originalRotation = base.transform.rotation;
			Transform fallingTransform = FallingTransform;
			_originalFallingPosition = ((fallingTransform != null) ? fallingTransform.position : base.transform.position);
			_originalFallingRotation = ((fallingTransform != null) ? fallingTransform.rotation : base.transform.rotation);
		}

		private void ResetPresentationTransforms()
		{
			base.transform.SetPositionAndRotation(_originalPosition, _originalRotation);
			Transform fallingTransform = FallingTransform;
			if (fallingTransform != null)
			{
				fallingTransform.SetPositionAndRotation(_originalFallingPosition, _originalFallingRotation);
			}
		}

		private void KillActiveSequence()
		{
			if (_activeSequence != null)
			{
				_activeSequence.Kill();
				_activeSequence = null;
			}
		}

		protected override void __initializeVariables()
		{
			if (_canBeHooked == null)
			{
				throw new Exception("HarpoonPullDropTarget._canBeHooked cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_canBeHooked.Initialize(this);
			__nameNetworkVariable(_canBeHooked, "_canBeHooked");
			NetworkVariableFields.Add(_canBeHooked);
			if (_encounterAvailable == null)
			{
				throw new Exception("HarpoonPullDropTarget._encounterAvailable cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_encounterAvailable.Initialize(this);
			__nameNetworkVariable(_encounterAvailable, "_encounterAvailable");
			NetworkVariableFields.Add(_encounterAvailable);
			if (_bossFallCompleted == null)
			{
				throw new Exception("HarpoonPullDropTarget._bossFallCompleted cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_bossFallCompleted.Initialize(this);
			__nameNetworkVariable(_bossFallCompleted, "_bossFallCompleted");
			NetworkVariableFields.Add(_bossFallCompleted);
			if (_bossFallTargetPosition == null)
			{
				throw new Exception("HarpoonPullDropTarget._bossFallTargetPosition cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_bossFallTargetPosition.Initialize(this);
			__nameNetworkVariable(_bossFallTargetPosition, "_bossFallTargetPosition");
			NetworkVariableFields.Add(_bossFallTargetPosition);
			if (_bossOutlineVisible == null)
			{
				throw new Exception("HarpoonPullDropTarget._bossOutlineVisible cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_bossOutlineVisible.Initialize(this);
			__nameNetworkVariable(_bossOutlineVisible, "_bossOutlineVisible");
			NetworkVariableFields.Add(_bossOutlineVisible);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(375487686u, __rpc_handler_375487686, "ServerRequestHookRpc", RpcInvokePermission.Everyone);
			__registerRpc(4085415091u, __rpc_handler_4085415091, "ServerCompletePullRpc", RpcInvokePermission.Everyone);
			__registerRpc(2370416124u, __rpc_handler_2370416124, "ServerCancelHookRpc", RpcInvokePermission.Everyone);
			__registerRpc(627384091u, __rpc_handler_627384091, "ServerReportPullZoneCompletedRpc", RpcInvokePermission.Everyone);
			__registerRpc(956504057u, __rpc_handler_956504057, "ServerLeavePullSessionRpc", RpcInvokePermission.Everyone);
			__registerRpc(1216156550u, __rpc_handler_1216156550, "ClientPullZoneCompletedRpc", RpcInvokePermission.Server);
			__registerRpc(4282101129u, __rpc_handler_4282101129, "ClientPullFinishedRpc", RpcInvokePermission.Server);
			__registerRpc(3970350070u, __rpc_handler_3970350070, "ClientPullCancelledRpc", RpcInvokePermission.Server);
			__registerRpc(3348775044u, __rpc_handler_3348775044, "ClientPullParticipantCountChangedRpc", RpcInvokePermission.Server);
			__registerRpc(1159008444u, __rpc_handler_1159008444, "ClientPlayFallRpc", RpcInvokePermission.Server);
			__registerRpc(1374193079u, __rpc_handler_1374193079, "ClientPlayResetRpc", RpcInvokePermission.Server);
			__registerRpc(1158114396u, __rpc_handler_1158114396, "ClientApplyBossEncounterAvailabilityRpc", RpcInvokePermission.Server);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_375487686(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonPullDropTarget)target).ServerRequestHookRpc(value, ext);
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4085415091(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonPullDropTarget)target).ServerCompletePullRpc(value, ext);
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2370416124(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonPullDropTarget)target).ServerCancelHookRpc(value, ext);
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_627384091(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				RpcParams ext = rpcParams.Ext;
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonPullDropTarget)target).ServerReportPullZoneCompletedRpc(value, ext);
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_956504057(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonPullDropTarget)target).ServerLeavePullSessionRpc(ext);
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1216156550(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonPullDropTarget)target).ClientPullZoneCompletedRpc(value, value2);
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4282101129(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonPullDropTarget)target).ClientPullFinishedRpc(value);
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3970350070(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				reader.ReadValueSafe(out PullDropTargetCancelReason value2, default(FastBufferWriter.ForEnums));
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonPullDropTarget)target).ClientPullCancelledRpc(value, value2);
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3348775044(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonPullDropTarget)target).ClientPullParticipantCountChangedRpc(value);
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1159008444(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonPullDropTarget)target).ClientPlayFallRpc(value, value2);
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1374193079(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonPullDropTarget)target).ClientPlayResetRpc();
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1158114396(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonPullDropTarget)target).ClientApplyBossEncounterAvailabilityRpc(value);
				((HarpoonPullDropTarget)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "HarpoonPullDropTarget";
		}
	}
}
