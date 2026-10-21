using System;
using System.Collections.Generic;
using DG.Tweening;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Economy;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Interaction.ToolTip;
using PaperPixelGames.Core.Kitchen.ServedDishes;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Progress;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Kitchen
{
	public class ConveyorBelt : BaseInteractable
	{
		private struct SlotPose
		{
			public Vector3 Position;

			public Quaternion Rotation;

			public SlotPose(Vector3 position, Quaternion rotation)
			{
				Position = position;
				Rotation = rotation;
			}
		}

		private sealed class BeltVisualState
		{
			public Renderer Renderer;

			public MaterialPropertyBlock PropertyBlock;

			public int TextureStPropertyId;

			public Vector2 TextureScale;

			public Vector2 BaseOffset;

			public Vector2 UvOffsetPerLoop;
		}

		[Header("Spline")]
		[SerializeField]
		private SplineContainer _spline;

		[SerializeField]
		[Min(0.01f)]
		private float _speedUnitsPerSec = 0.8f;

		[SerializeField]
		[Min(64f)]
		private int _distanceLookupSamples = 512;

		[Header("Logical Slots")]
		[SerializeField]
		[Min(0.01f)]
		private float _slotSpacing = 0.4f;

		[SerializeField]
		private float _slotStartOffset;

		[SerializeField]
		private Vector3 _slotLocalOffset = Vector3.zero;

		[Header("Slot Rotation")]
		[SerializeField]
		private bool _snapWorldRotation = true;

		[SerializeField]
		private bool _useSplineTangentRotation = true;

		[SerializeField]
		private Vector3 _slotRotationOffset = Vector3.zero;

		[Header("Visual (UV Scroll)")]
		[SerializeField]
		private Vector2 _uvScrollDirection = Vector2.down;

		[SerializeField]
		[Min(0f)]
		private float _uvScrollSpeedMultiplier = 1f;

		[SerializeField]
		private List<Renderer> _renderers = new List<Renderer>();

		[Header("Unlock")]
		[SerializeField]
		private PhysicalStateController _physicalStateController;

		[SerializeField]
		private Vector3 _unlockTooltipScale = Vector3.one * 0.015f;

		[Header("Start / Stop Button Feedback")]
		[SerializeField]
		private Transform _startStopButtonVisual;

		[SerializeField]
		private Vector3 _buttonPressedLocalOffset = new Vector3(0f, -0.035f, 0f);

		[SerializeField]
		[Min(0.05f)]
		private float _buttonInputCooldown = 0.25f;

		[SerializeField]
		[Min(0.01f)]
		private float _buttonPressInDuration = 0.06f;

		[SerializeField]
		[Min(0.01f)]
		private float _buttonPressOutDuration = 0.08f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _buttonPressVolume = 1f;

		[Header("SFX")]
		[SerializeField]
		private AudioClipSO _buttonClip;

		[SerializeField]
		private AudioClipSO _plateClip;

		[SerializeField]
		private AudioClipSO _buzzClip;

		[SerializeField]
		private AudioClipSO _noiseClip;

		[Space]
		[SerializeField]
		private Transform _buttonTransform;

		[SerializeField]
		private Transform _loopTransform;

		[Header("Debug")]
		[SerializeField]
		private bool _drawSlotGizmos = true;

		[SerializeField]
		private float _slotGizmoRadius = 0.08f;

		private OwnedLoopHandle _buzzLoop;

		private OwnedLoopHandle _noiseLoop;

		private Vector3 _buttonDefaultLocalPosition;

		private Sequence _buttonPressSequence;

		private bool _hasLocalGaze;

		private float _nextLocalToggleAllowedTime;

		private double _nextServerToggleAllowedTime;

		private float _nextSlotIntegrityCheckAt;

		private readonly NetworkVariable<bool> _isRunning = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<double> _serverStartTime = new NetworkVariable<double>(0.0);

		private readonly NetworkVariable<float> _serverStartBaseDistance = new NetworkVariable<float>(0f);

		private float _splineLength = 1f;

		private float[] _lookupT;

		private float[] _lookupDistance;

		private int _resolvedSlotCount = 1;

		private float _resolvedSlotSpacing = 0.4f;

		private readonly List<BeltVisualState> _beltVisualStates = new List<BeltVisualState>();

		private bool _hasVisualBeltPhase;

		private float _previousNormalizedBeltPhase;

		private float _accumulatedVisualBeltPhase;

		private bool _stoppedVisualRefreshPending = true;

		private NetworkList<NetworkObjectReference> _slotOccupants = new NetworkList<NetworkObjectReference>();

		private readonly HashSet<ulong> _pendingPlacementFinalizationIds = new HashSet<ulong>();

		private EventBinding<ConveryorBeltTakeItemEvent> _takeItemEvent;

		private const int MaxQueuedPlacementRequests = 32;

		private bool _localPlacementRequestPending;

		private int _localQueuedPlacementRequestCount;

		private NetworkObjectReference _localPendingPlacementDishRef;

		private Interactor _localPlacementInteractor;

		private readonly Dictionary<int, int> _staffSlots = new Dictionary<int, int>();

		private float nextStaffRefill;

		public PhysicalStateController PhysicalStateController => _physicalStateController;

		public bool ServerStaffEmpty
		{
			get
			{
				if (!ServerStaffRunning)
				{
					return false;
				}
				for (int i = 0; i < _slotOccupants.Count; i++)
				{
					if (!IsSlotEmpty(i))
					{
						return false;
					}
				}
				return true;
			}
		}

		public bool ServerStaffCanRefill
		{
			get
			{
				if (ServerStaffEmpty && _staffSlots.Count == 0)
				{
					return Time.time >= nextStaffRefill;
				}
				return false;
			}
		}

		public bool ServerStaffRunning
		{
			get
			{
				if (base.IsServer && base.IsSpawned && _isRunning.Value && _physicalStateController != null)
				{
					return _physicalStateController.IsPhysicallyUsable;
				}
				return false;
			}
		}

		protected override void Awake()
		{
			base.Awake();
			CacheButtonDefaultPose();
			if (_spline == null)
			{
				UtilityMethods.Log("[ConveyorBelt] Spline is not assigned.", LogType.Warning);
				base.enabled = false;
				return;
			}
			BuildDistanceLookup();
			ResolveLogicalSlotLayout();
			ValidateLogicalSlotSetup();
			CacheBeltVisualStates();
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			ResetVisualBeltPhase();
			NetworkVariable<bool> isRunning = _isRunning;
			isRunning.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isRunning.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(HandleRunningStateChanged));
			HandleRunningStateChanged(previousValue: false, _isRunning.Value);
			if (base.IsServer)
			{
				RebuildSlotOccupantsList();
				_serverStartTime.Value = base.NetworkManager.ServerTime.Time;
				_serverStartBaseDistance.Value = 0f;
			}
			_slotOccupants.OnListChanged += OnSlotOccupantsChanged;
			for (int i = 0; i < _slotOccupants.Count; i++)
			{
				ApplyOccupantVisual(i, _slotOccupants[i]);
			}
			if (base.IsServer)
			{
				_takeItemEvent = new EventBinding<ConveryorBeltTakeItemEvent>(OnServerTryTakeByItemTransform);
				EventBus<ConveryorBeltTakeItemEvent>.Register(_takeItemEvent);
			}
			if (_physicalStateController != null)
			{
				_physicalStateController.OnPhysicallyUsableChanged += HandlePhysicalRepair;
			}
			else
			{
				UtilityMethods.Log("[ConveyorBelt] PhysicalStateController is not assigned.", LogType.Warning);
			}
		}

		public override void OnNetworkDespawn()
		{
			ClearLocalPlacementRequests();
			_pendingPlacementFinalizationIds.Clear();
			base.OnNetworkDespawn();
			NetworkVariable<bool> isRunning = _isRunning;
			isRunning.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isRunning.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(HandleRunningStateChanged));
			_buttonPressSequence?.Kill();
			_buttonPressSequence = null;
			if (_physicalStateController != null)
			{
				_physicalStateController.OnPhysicallyUsableChanged -= HandlePhysicalRepair;
			}
			if (_slotOccupants != null)
			{
				_slotOccupants.OnListChanged -= OnSlotOccupantsChanged;
			}
			if (base.IsServer && _takeItemEvent != null)
			{
				EventBus<ConveryorBeltTakeItemEvent>.Deregister(_takeItemEvent);
			}
			StopConveyorLoops();
		}

		public override void EnableGazeHints()
		{
			if (IsHoldingSushiDish() && _physicalStateController != null && _physicalStateController.IsPhysicallyUsable)
			{
				Singleton<HintsManager>.Instance.ClearAllHints();
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_place");
			}
			else
			{
				AddCurrentInteractHint();
			}
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
			if (IsHoldingSushiDish())
			{
				Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
			}
		}

		private static bool IsHoldingSushiDish()
		{
			if (Singleton<QuickSlotHandler>.Instance != null)
			{
				return Singleton<QuickSlotHandler>.Instance.SelectedItem is ServedDishSushi;
			}
			return false;
		}

		private void AddCurrentInteractHint(bool? runningOverride = null)
		{
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
			if (_physicalStateController == null || !_physicalStateController.IsPhysicallyUsable)
			{
				Singleton<HintsManager>.Instance.AddHint("Interact", "key_repair");
				return;
			}
			bool flag = runningOverride ?? _isRunning.Value;
			Singleton<HintsManager>.Instance.AddHint("Interact", flag ? "key_stop" : "key_start");
		}

		private void RefreshLocalGazeHint(bool? runningOverride = null)
		{
			if (_hasLocalGaze)
			{
				AddCurrentInteractHint(runningOverride);
			}
		}

		private void HandleRunningStateChanged(bool previousValue, bool newValue)
		{
			RefreshLocalGazeHint(newValue);
			if (!newValue)
			{
				_stoppedVisualRefreshPending = true;
			}
			if (newValue)
			{
				StopConveyorLoops();
				if (!(Singleton<SoundManager>.Instance == null))
				{
					Vector3 position = ((_loopTransform != null) ? _loopTransform.position : base.transform.position);
					_buzzLoop = Singleton<SoundManager>.Instance.PlayOwnedLoop(_buzzClip, position, null, $"ConveyorBuzz-{base.NetworkObjectId}");
					_noiseLoop = Singleton<SoundManager>.Instance.PlayOwnedLoop(_noiseClip, position, null, $"ConveyorNoise-{base.NetworkObjectId}");
				}
			}
			else
			{
				StopConveyorLoops();
			}
		}

		private void StopConveyorLoops()
		{
			if (Singleton<SoundManager>.Instance != null)
			{
				Singleton<SoundManager>.Instance.ReleaseOwnedLoop(_buzzLoop);
				Singleton<SoundManager>.Instance.ReleaseOwnedLoop(_noiseLoop);
			}
			_buzzLoop = null;
			_noiseLoop = null;
		}

		public void ServerResetForEndDay()
		{
			if (base.IsServer)
			{
				float currentBaseDistance = GetCurrentBaseDistance();
				int num = ServerPruneInvalidSlotOccupantsForReset();
				_serverStartBaseDistance.Value = currentBaseDistance;
				_serverStartTime.Value = ((base.NetworkManager != null) ? base.NetworkManager.ServerTime.Time : 0.0);
				_isRunning.Value = false;
				_nextServerToggleAllowedTime = 0.0;
				ClientResetRuntimeStateRpc();
				UtilityMethods.Log("[ConveyorBelt] End-day reset completed. " + $"PreservedOccupants={CountValidSlotOccupants()} RemovedInvalidSlots={num}");
			}
		}

		private int ServerPruneInvalidSlotOccupantsForReset()
		{
			int num = 0;
			if (_slotOccupants == null)
			{
				return num;
			}
			int slotCount = GetSlotCount();
			for (int i = 0; i < _slotOccupants.Count; i++)
			{
				NetworkObjectReference networkObjectReference = _slotOccupants[i];
				if (!networkObjectReference.Equals(default) && (!networkObjectReference.TryGet(out var networkObject) || networkObject == null || !networkObject.IsSpawned))
				{
					_slotOccupants[i] = default;
					num++;
				}
			}
			while (_slotOccupants.Count < slotCount)
			{
				_slotOccupants.Add(default);
			}
			while (_slotOccupants.Count > slotCount)
			{
				_slotOccupants.RemoveAt(_slotOccupants.Count - 1);
			}
			return num;
		}

		private int CountValidSlotOccupants()
		{
			if (_slotOccupants == null)
			{
				return 0;
			}
			int num = 0;
			for (int i = 0; i < _slotOccupants.Count; i++)
			{
				if (_slotOccupants[i].TryGet(out var networkObject) && networkObject != null && networkObject.IsSpawned)
				{
					num++;
				}
			}
			return num;
		}

		[Rpc(SendTo.Everyone)]
		private void ClientResetRuntimeStateRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3419054762u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3419054762u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_buttonPressSequence?.Kill();
				_buttonPressSequence = null;
				if (_startStopButtonVisual != null)
				{
					_startStopButtonVisual.localPosition = _buttonDefaultLocalPosition;
				}
				_nextLocalToggleAllowedTime = 0f;
				RefreshLocalGazeHint(false);
			}
		}

		private void RebuildSlotOccupantsList()
		{
			_slotOccupants.Clear();
			int slotCount = GetSlotCount();
			for (int i = 0; i < slotCount; i++)
			{
				_slotOccupants.Add(default);
			}
		}

		private void ValidateLogicalSlotSetup()
		{
			_ = _splineLength;
			_ = 0.0001f;
		}

		private void ResolveLogicalSlotLayout()
		{
			float num = Mathf.Max(0.01f, _slotSpacing);
			_resolvedSlotCount = Mathf.Max(1, Mathf.FloorToInt(_splineLength / num));
			_resolvedSlotSpacing = _splineLength / (float)_resolvedSlotCount;
		}

		private int GetSlotCount()
		{
			return Mathf.Max(1, _resolvedSlotCount);
		}

		private void HandlePhysicalRepair(bool value)
		{
			RefreshLocalGazeHint();
			if (value && !(_interactableTooltip == null))
			{
				_interactableTooltip.ClearRuntimeTooltip();
				_interactableTooltip.IsEnabled = false;
				if (Singleton<ToolTipHandler>.Instance != null)
				{
					Singleton<ToolTipHandler>.Instance.OnGazeExit(_interactableTooltip);
				}
			}
		}

		public override void GazeEnter()
		{
			_hasLocalGaze = true;
			base.GazeEnter();
			if (!(_physicalStateController == null) && !_physicalStateController.IsPhysicallyUsable && !(_interactableTooltip == null))
			{
				RefreshUnlockTooltipLocal();
				if (_interactableTooltip.IsEnabled && !(Singleton<ToolTipHandler>.Instance == null))
				{
					Singleton<ToolTipHandler>.Instance.OnGazeEnter(_interactableTooltip, _interactableTooltip.GetWorldPosition());
				}
			}
		}

		public override void GazeExit()
		{
			_hasLocalGaze = false;
			base.GazeExit();
			ClearTooltip();
		}

		private void ClearTooltip()
		{
			if (!(_interactableTooltip == null) && _interactableTooltip.IsEnabled && !(Singleton<ToolTipHandler>.Instance == null))
			{
				Singleton<ToolTipHandler>.Instance.OnGazeExit(_interactableTooltip);
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerStartBeltRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1968940764u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1968940764u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && !(_physicalStateController == null) && _physicalStateController.IsPhysicallyUsable && !_isRunning.Value && TryBeginServerButtonCooldown())
				{
					float value = RepeatDistance(_serverStartBaseDistance.Value);
					_serverStartBaseDistance.Value = value;
					_serverStartTime.Value = base.NetworkManager.ServerTime.Time;
					_isRunning.Value = true;
					ClientPlayBeltButtonFeedbackRpc();
				}
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerStopBeltRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3981371782u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3981371782u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && !(_physicalStateController == null) && _physicalStateController.IsPhysicallyUsable && _isRunning.Value && TryBeginServerButtonCooldown())
				{
					_serverStartBaseDistance.Value = GetCurrentBaseDistance();
					_serverStartTime.Value = base.NetworkManager.ServerTime.Time;
					_isRunning.Value = false;
					ClientPlayBeltButtonFeedbackRpc();
				}
			}
		}

		private bool TryBeginServerButtonCooldown()
		{
			double num = ((base.NetworkManager != null) ? base.NetworkManager.ServerTime.Time : ((double)Time.time));
			if (num < _nextServerToggleAllowedTime)
			{
				return false;
			}
			_nextServerToggleAllowedTime = num + (double)_buttonInputCooldown;
			return true;
		}

		private void CacheButtonDefaultPose()
		{
			if (!(_startStopButtonVisual == null))
			{
				_buttonDefaultLocalPosition = _startStopButtonVisual.localPosition;
			}
		}

		private bool TryBeginLocalButtonCooldown()
		{
			if (Time.unscaledTime < _nextLocalToggleAllowedTime)
			{
				return false;
			}
			_nextLocalToggleAllowedTime = Time.unscaledTime + _buttonInputCooldown;
			return true;
		}

		private bool TryRequestToggleBeltClient()
		{
			if (_physicalStateController == null || !_physicalStateController.IsPhysicallyUsable)
			{
				return false;
			}
			if (!TryBeginLocalButtonCooldown())
			{
				return false;
			}
			bool flag = !_isRunning.Value;
			if (flag)
			{
				ServerStartBeltRpc();
			}
			else
			{
				ServerStopBeltRpc();
			}
			RefreshLocalGazeHint(flag);
			return true;
		}

		[Rpc(SendTo.Everyone)]
		private void ClientPlayBeltButtonFeedbackRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1032561129u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1032561129u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				PlayButtonPressAnimation();
				PlayButtonPressSfx();
			}
		}

		private void PlayButtonPressAnimation()
		{
			if (!(_startStopButtonVisual == null))
			{
				_buttonPressSequence?.Kill();
				Vector3 endValue = _buttonDefaultLocalPosition + _buttonPressedLocalOffset;
				_buttonPressSequence = DOTween.Sequence();
				_buttonPressSequence.Append(_startStopButtonVisual.DOLocalMove(endValue, _buttonPressInDuration).SetEase(Ease.OutQuad));
				float num = Mathf.Max(0f, _buttonInputCooldown - _buttonPressInDuration - _buttonPressOutDuration);
				if (num > 0f)
				{
					_buttonPressSequence.AppendInterval(num);
				}
				_buttonPressSequence.Append(_startStopButtonVisual.DOLocalMove(_buttonDefaultLocalPosition, _buttonPressOutDuration).SetEase(Ease.OutBack));
			}
		}

		private void PlayButtonPressSfx()
		{
			if (!(_buttonClip == null))
			{
				Singleton<SoundManager>.Instance.PlaySFX(_buttonClip, _buttonTransform.position);
			}
		}

		private void BuildDistanceLookup()
		{
			int num = Mathf.Max(64, _distanceLookupSamples);
			_lookupT = new float[num];
			_lookupDistance = new float[num];
			float num2 = 0f;
			Vector3 a = _spline.EvaluatePosition(0f);
			_lookupT[0] = 0f;
			_lookupDistance[0] = 0f;
			for (int i = 1; i < num; i++)
			{
				float num3 = (float)i / (float)(num - 1);
				Vector3 vector = _spline.EvaluatePosition(num3);
				num2 += Vector3.Distance(a, vector);
				_lookupT[i] = num3;
				_lookupDistance[i] = num2;
				a = vector;
			}
			_splineLength = Mathf.Max(0.0001f, num2);
		}

		private float DistanceToT(float distance)
		{
			if (_lookupT == null || _lookupDistance == null || _lookupDistance.Length == 0)
			{
				return 0f;
			}
			distance = RepeatDistance(distance);
			int num = 1;
			int num2 = _lookupDistance.Length - 1;
			while (num < num2)
			{
				int num3 = num + (num2 - num >> 1);
				if (_lookupDistance[num3] < distance)
				{
					num = num3 + 1;
				}
				else
				{
					num2 = num3;
				}
			}
			int num4 = num;
			int num5 = Mathf.Max(0, num4 - 1);
			float t = Mathf.InverseLerp(_lookupDistance[num5], _lookupDistance[num4], distance);
			return Mathf.Lerp(_lookupT[num5], _lookupT[num4], t);
		}

		private float RepeatDistance(float distance)
		{
			if (_splineLength <= 0.0001f)
			{
				return 0f;
			}
			distance %= _splineLength;
			if (distance < 0f)
			{
				distance += _splineLength;
			}
			return distance;
		}

		private static float Repeat01(float t)
		{
			t %= 1f;
			if (t < 0f)
			{
				t++;
			}
			return t;
		}

		private float GetCurrentBaseDistance()
		{
			float value = _serverStartBaseDistance.Value;
			if (!_isRunning.Value)
			{
				return RepeatDistance(value);
			}
			double num = base.NetworkManager.ServerTime.Time - _serverStartTime.Value;
			return RepeatDistance(value + (float)((double)_speedUnitsPerSec * num));
		}

		private float GetSlotDistance(int slotIndex)
		{
			return GetSlotDistance(slotIndex, GetCurrentBaseDistance());
		}

		private float GetSlotDistance(int slotIndex, float currentBaseDistance)
		{
			return RepeatDistance(currentBaseDistance + _slotStartOffset + (float)slotIndex * _resolvedSlotSpacing);
		}

		private SlotPose GetSlotPose(int slotIndex)
		{
			return GetSlotPose(slotIndex, GetCurrentBaseDistance());
		}

		private SlotPose GetSlotPose(int slotIndex, float currentBaseDistance)
		{
			float slotDistance = GetSlotDistance(slotIndex, currentBaseDistance);
			float t = DistanceToT(slotDistance);
			Vector3 position = (Vector3)_spline.EvaluatePosition(t) + _spline.transform.TransformVector(_slotLocalOffset);
			Quaternion slotRotation = GetSlotRotation(t);
			return new SlotPose(position, slotRotation);
		}

		private Quaternion GetSlotRotation(float t)
		{
			Quaternion quaternion2;
			if (_useSplineTangentRotation)
			{
				Vector3 vector = GetSplineTangent(t);
				if (vector.sqrMagnitude <= 0.0001f)
				{
					vector = _spline.transform.forward;
				}
				quaternion2 = Quaternion.LookRotation(vector.normalized, _spline.transform.up);
			}
			else
			{
				quaternion2 = _spline.transform.rotation;
			}
			return quaternion2 * Quaternion.Euler(_slotRotationOffset);
		}

		private Vector3 GetSplineTangent(float t)
		{
			float num = 1f / (float)Mathf.Max(64, _distanceLookupSamples);
			float t2 = Repeat01(t - num);
			float t3 = Repeat01(t + num);
			Vector3 vector = _spline.EvaluatePosition(t2);
			return (Vector3)_spline.EvaluatePosition(t3) - vector;
		}

		private void LateUpdate()
		{
			if (!base.IsSpawned || base.NetworkManager == null || !base.NetworkManager.IsListening || (!_isRunning.Value && !_stoppedVisualRefreshPending))
			{
				return;
			}
			float currentBaseDistance = GetCurrentBaseDistance();
			ApplyBeltVisual(currentBaseDistance);
			_stoppedVisualRefreshPending = false;
			if (_slotOccupants == null || _slotOccupants.Count == 0)
			{
				return;
			}
			int num = Mathf.Min(GetSlotCount(), _slotOccupants.Count);
			for (int i = 0; i < num; i++)
			{
				if (_slotOccupants[i].TryGet(out var networkObject) && !(networkObject == null) && networkObject.IsSpawned && !(networkObject.transform.parent != base.transform))
				{
					SlotPose slotPose = GetSlotPose(i, currentBaseDistance);
					networkObject.transform.SetPositionAndRotation(slotPose.Position, _snapWorldRotation ? slotPose.Rotation : networkObject.transform.rotation);
				}
			}
		}

		private void Update()
		{
			if (base.IsServer && base.IsSpawned && !(Time.unscaledTime < _nextSlotIntegrityCheckAt))
			{
				_nextSlotIntegrityCheckAt = Time.unscaledTime + 1f;
				ServerRecoverInvalidRuntimeSlotOccupants();
			}
		}

		private void ServerRecoverInvalidRuntimeSlotOccupants()
		{
			if (_slotOccupants == null)
			{
				return;
			}
			int slotCount = GetSlotCount();
			while (_slotOccupants.Count < slotCount)
			{
				_slotOccupants.Add(default);
			}
			while (_slotOccupants.Count > slotCount)
			{
				_slotOccupants.RemoveAt(_slotOccupants.Count - 1);
			}
			for (int i = 0; i < _slotOccupants.Count; i++)
			{
				NetworkObjectReference networkObjectReference = _slotOccupants[i];
				if (!networkObjectReference.Equals(default) && (!networkObjectReference.TryGet(out var networkObject) || networkObject == null || !networkObject.IsSpawned || networkObject.transform.parent != base.transform))
				{
					_slotOccupants[i] = default;
					UtilityMethods.Log($"[ConveyorBelt] Cleared invalid runtime occupant from slot {i}.", LogType.Warning);
				}
			}
		}

		private void CacheBeltVisualStates()
		{
			_beltVisualStates.Clear();
			if (_renderers == null || _renderers.Count == 0)
			{
				return;
			}
			Vector2 vector = ((_uvScrollDirection.sqrMagnitude > 0.0001f) ? _uvScrollDirection.normalized : Vector2.down);
			for (int i = 0; i < _renderers.Count; i++)
			{
				Renderer renderer = _renderers[i];
				if (renderer == null)
				{
					continue;
				}
				Material sharedMaterial = renderer.sharedMaterial;
				if (!(sharedMaterial == null))
				{
					string text = (sharedMaterial.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex");
					if (sharedMaterial.HasProperty(text) && !(sharedMaterial.GetTexture(text) == null))
					{
						Vector2 textureScale = sharedMaterial.GetTextureScale(text);
						Vector2 uvOffsetPerLoop = new Vector2(vector.x * Mathf.Abs(textureScale.x), vector.y * Mathf.Abs(textureScale.y));
						_beltVisualStates.Add(new BeltVisualState
						{
							Renderer = renderer,
							PropertyBlock = new MaterialPropertyBlock(),
							TextureStPropertyId = Shader.PropertyToID(text + "_ST"),
							TextureScale = textureScale,
							BaseOffset = sharedMaterial.GetTextureOffset(text),
							UvOffsetPerLoop = uvOffsetPerLoop
						});
					}
				}
			}
		}

		private void ApplyBeltVisual(float currentBaseDistance)
		{
			if (_beltVisualStates.Count == 0 || _splineLength <= 0.0001f)
			{
				return;
			}
			float normalizedBeltPhase = RepeatDistance(currentBaseDistance) / _splineLength;
			UpdateVisualBeltPhase(normalizedBeltPhase);
			for (int i = 0; i < _beltVisualStates.Count; i++)
			{
				BeltVisualState beltVisualState = _beltVisualStates[i];
				if (!(beltVisualState.Renderer == null))
				{
					Vector2 vector = beltVisualState.BaseOffset + beltVisualState.UvOffsetPerLoop * _accumulatedVisualBeltPhase;
					beltVisualState.Renderer.GetPropertyBlock(beltVisualState.PropertyBlock);
					beltVisualState.PropertyBlock.SetVector(beltVisualState.TextureStPropertyId, new Vector4(beltVisualState.TextureScale.x, beltVisualState.TextureScale.y, vector.x, vector.y));
					beltVisualState.Renderer.SetPropertyBlock(beltVisualState.PropertyBlock);
				}
			}
		}

		private void UpdateVisualBeltPhase(float normalizedBeltPhase)
		{
			float num = Mathf.Max(0f, _uvScrollSpeedMultiplier);
			if (!_hasVisualBeltPhase)
			{
				_hasVisualBeltPhase = true;
				_previousNormalizedBeltPhase = normalizedBeltPhase;
				_accumulatedVisualBeltPhase = normalizedBeltPhase * num;
				return;
			}
			float num2 = normalizedBeltPhase - _previousNormalizedBeltPhase;
			if (num2 < -0.5f)
			{
				num2++;
			}
			else if (num2 > 0.5f)
			{
				num2--;
			}
			_accumulatedVisualBeltPhase += num2 * num;
			_previousNormalizedBeltPhase = normalizedBeltPhase;
		}

		private void ResetVisualBeltPhase()
		{
			_hasVisualBeltPhase = false;
			_previousNormalizedBeltPhase = 0f;
			_accumulatedVisualBeltPhase = 0f;
		}

		private void OnSlotOccupantsChanged(NetworkListEvent<NetworkObjectReference> change)
		{
			if (!_isRunning.Value)
			{
				_stoppedVisualRefreshPending = true;
			}
			ApplyOccupantVisual(change.Index, change.Value);
		}

		private void ApplyOccupantVisual(int slotIndex, NetworkObjectReference occRef)
		{
			if (IsSlotIndexValid(slotIndex) && occRef.TryGet(out var networkObject) && !(networkObject == null))
			{
				SlotPose slotPose = GetSlotPose(slotIndex);
				networkObject.transform.position = slotPose.Position;
				if (_snapWorldRotation)
				{
					networkObject.transform.rotation = slotPose.Rotation;
				}
			}
		}

		private bool IsSlotEmpty(int index)
		{
			if (_slotOccupants == null)
			{
				return true;
			}
			if (index < 0 || index >= _slotOccupants.Count)
			{
				return false;
			}
			if (_slotOccupants[index].TryGet(out var networkObject))
			{
				return networkObject == null;
			}
			return true;
		}

		private bool IsFull()
		{
			if (_slotOccupants == null)
			{
				return false;
			}
			for (int i = 0; i < _slotOccupants.Count; i++)
			{
				if (IsSlotEmpty(i))
				{
					return false;
				}
			}
			return true;
		}

		private bool IsSlotIndexValid(int index)
		{
			if (_slotOccupants == null)
			{
				return false;
			}
			if (index >= 0 && index < _slotOccupants.Count)
			{
				return index < GetSlotCount();
			}
			return false;
		}

		public bool TryGetSaveSlot(ServedDish servedDish, out int slotIndex)
		{
			slotIndex = -1;
			if (servedDish == null || servedDish.NetworkObject == null)
			{
				return false;
			}
			slotIndex = FindSlotIndexByOccupant(servedDish.NetworkObject);
			return slotIndex >= 0;
		}

		public bool ServerRestoreDishToSlot(ServedDish servedDish, int requestedSlotIndex)
		{
			if (!base.IsServer || !base.IsSpawned)
			{
				return false;
			}
			if (!(servedDish is ServedDishSushi servedDishSushi) || servedDish.NetworkObject == null || !servedDish.NetworkObject.IsSpawned)
			{
				UtilityMethods.Log("[ConveyorBelt] Restore failed. Served dish is invalid or unsupported.", LogType.Warning);
				return false;
			}
			EnsureSlotOccupantCapacity();
			int num = requestedSlotIndex;
			if (!IsSlotIndexValid(num) || !IsSlotEmpty(num))
			{
				num = FindNearestEmptySlotIndex(servedDish.transform.position);
			}
			if (num < 0)
			{
				UtilityMethods.Log($"[ConveyorBelt] Restore failed. No empty slot. RequestedSlot={requestedSlotIndex} Dish={servedDish.name}", LogType.Warning);
				return false;
			}
			if (!servedDish.NetworkObject.TrySetParent(base.NetworkObject))
			{
				UtilityMethods.Log($"[ConveyorBelt] Restore failed. TrySetParent failed. Slot={num} Dish={servedDish.name}", LogType.Warning);
				return false;
			}
			_slotOccupants[num] = servedDish.NetworkObject.ToReference();
			servedDishSushi.ServerEnterConveyorPlateMode();
			SlotPose slotPose = GetSlotPose(num);
			Quaternion rotation = (_snapWorldRotation ? slotPose.Rotation : servedDish.transform.rotation);
			servedDish.transform.SetPositionAndRotation(slotPose.Position, rotation);
			UtilityMethods.Log($"[ConveyorBelt] Dish restored. RequestedSlot={requestedSlotIndex} " + $"ResolvedSlot={num} Dish={servedDish.name}");
			return true;
		}

		public int ServerDebugReplaceAllWithRandomSushi(IReadOnlyList<SushiSO> sushiPool)
		{
			if (!base.IsServer || !base.IsSpawned)
			{
				return 0;
			}
			if (_physicalStateController == null || !_physicalStateController.IsPhysicallyUsable)
			{
				return 0;
			}
			if (sushiPool == null || sushiPool.Count == 0)
			{
				UtilityMethods.Log("[ConveyorBelt][BeltDebug] Fill skipped. Sushi pool is empty.", LogType.Warning);
				return 0;
			}
			EnsureSlotOccupantCapacity();
			ServerClearSlotOccupantsForDebugFill();
			int num = 0;
			int num2 = Mathf.Min(GetSlotCount(), _slotOccupants.Count);
			for (int i = 0; i < num2; i++)
			{
				SushiSO sushiSO = sushiPool[UnityEngine.Random.Range(0, sushiPool.Count)];
				SlotPose slotPose = GetSlotPose(i);
				if (sushiSO.Prefab.TryInstantiateAndSpawn(out var instance, slotPose.Position, slotPose.Rotation))
				{
					if (!instance.TryGetComponent<ServedDishSushi>(out var component) || !ServerRestoreDishToSlot(component, i))
					{
						instance.Despawn();
					}
					else
					{
						num++;
					}
				}
			}
			UtilityMethods.Log($"[ConveyorBelt][BeltDebug] Random fill completed. Placed={num} Slots={num2}");
			return num;
		}

		private void ServerClearSlotOccupantsForDebugFill()
		{
			List<NetworkObject> list = new List<NetworkObject>();
			for (int i = 0; i < _slotOccupants.Count; i++)
			{
				if (_slotOccupants[i].TryGet(out var networkObject) && networkObject != null && networkObject.IsSpawned && !list.Contains(networkObject))
				{
					list.Add(networkObject);
				}
				_slotOccupants[i] = default;
			}
			for (int j = 0; j < list.Count; j++)
			{
				NetworkObject networkObject2 = list[j];
				if (networkObject2 != null && networkObject2.IsSpawned)
				{
					networkObject2.Despawn();
				}
			}
		}

		private void EnsureSlotOccupantCapacity()
		{
			int slotCount = GetSlotCount();
			while (_slotOccupants.Count < slotCount)
			{
				_slotOccupants.Add(default);
			}
			while (_slotOccupants.Count > slotCount)
			{
				_slotOccupants.RemoveAt(_slotOccupants.Count - 1);
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerTryPlaceNearestEmptySlotRpc(NetworkObjectReference interactorRef, NetworkObjectReference servedDishRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3154934907u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in servedDishRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3154934907u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			NetworkObjectReference servedDishRef2 = servedDishRef;
			if (!base.IsServer)
			{
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			RpcParams rpcParams3 = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
			if (_physicalStateController == null || !_physicalStateController.IsPhysicallyUsable)
			{
				ClientResolvePlacementRequestRpc(servedDishRef2, accepted: false, rpcParams3);
				return;
			}
			if (!interactorRef.TryGetComponent<Interactor>(out var component))
			{
				UtilityMethods.Log("[ConveyorBelt] Place failed: interactor not found.", LogType.Warning);
				ClientResolvePlacementRequestRpc(servedDishRef2, accepted: false, rpcParams3);
				return;
			}
			if (component.OwnerClientId != senderClientId)
			{
				UtilityMethods.Log("[ConveyorBelt] Place failed: sender does not own the interactor.", LogType.Warning);
				ClientResolvePlacementRequestRpc(servedDishRef2, accepted: false, rpcParams3);
				return;
			}
			if (!servedDishRef2.TryGetComponent<ServedDishSushi>(out var component2))
			{
				UtilityMethods.Log("[ConveyorBelt] Place failed: served dish not found.", LogType.Warning);
				ClientResolvePlacementRequestRpc(servedDishRef2, accepted: false, rpcParams3);
				return;
			}
			if (component2.NetworkObject == null || !component2.NetworkObject.IsSpawned)
			{
				UtilityMethods.Log("[ConveyorBelt] Place failed: served dish NetworkObject is invalid.", LogType.Warning);
				ClientResolvePlacementRequestRpc(servedDishRef2, accepted: false, rpcParams3);
				return;
			}
			if (component2.OwnerClientId != senderClientId || component2.CanHold.Value || (object)component.CurrentHeld != component2 || component2.transform.parent != null)
			{
				UtilityMethods.Log("[ConveyorBelt] Place failed: served dish is not the sender's current held item.", LogType.Warning);
				ClientResolvePlacementRequestRpc(servedDishRef2, accepted: false, rpcParams3);
				return;
			}
			if (FindSlotIndexByOccupant(component2.NetworkObject) >= 0)
			{
				UtilityMethods.Log("[ConveyorBelt] Place blocked: dish is already on conveyor.", LogType.Warning);
				ClientResolvePlacementRequestRpc(servedDishRef2, accepted: false, rpcParams3);
				return;
			}
			Vector3 worldPosition = ((component.Player != null) ? component.Player.transform.position : component.transform.position);
			int num = FindNearestEmptySlotIndex(worldPosition);
			if (num < 0)
			{
				ClientShowInfoRpc("Info", "Conveyor belt is full", rpcParams3);
				ClientResolvePlacementRequestRpc(servedDishRef2, accepted: false, rpcParams3);
			}
			else if (!ServerPlaceToSlotInternal(component, component2, num, () =>
			{
				RpcParams rpcParams4 = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
				ClientResolvePlacementRequestRpc(servedDishRef2, accepted: true, rpcParams4);
				ClientPlayPlatePlacedFeedbackRpc();
			}))
			{
				ClientResolvePlacementRequestRpc(servedDishRef2, accepted: false, rpcParams3);
			}
		}

		private int FindNearestEmptySlotIndex(Vector3 worldPosition)
		{
			int result = -1;
			float num = float.MaxValue;
			int slotCount = GetSlotCount();
			for (int i = 0; i < slotCount; i++)
			{
				if (IsSlotEmpty(i))
				{
					float sqrMagnitude = (GetSlotPose(i).Position - worldPosition).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						result = i;
					}
				}
			}
			return result;
		}

		private int FindNearestOccupiedSlotIndex(Vector3 worldPosition)
		{
			int result = -1;
			float num = float.MaxValue;
			int slotCount = GetSlotCount();
			for (int i = 0; i < slotCount; i++)
			{
				if (!IsSlotEmpty(i))
				{
					float sqrMagnitude = (GetSlotPose(i).Position - worldPosition).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						result = i;
					}
				}
			}
			return result;
		}

		private bool ServerPlaceToSlotInternal(Interactor interactor, ServedDishSushi dish, int slotIndex, Action onClientPlacementQueued)
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (!IsSlotIndexValid(slotIndex))
			{
				UtilityMethods.Log($"[ConveyorBelt] Place failed: invalid slot index {slotIndex}.", LogType.Warning);
				return false;
			}
			if (interactor == null)
			{
				UtilityMethods.Log("[ConveyorBelt] Place failed: interactor is null.", LogType.Warning);
				return false;
			}
			if (dish == null || dish.NetworkObject == null || !dish.NetworkObject.IsSpawned)
			{
				UtilityMethods.Log("[ConveyorBelt] Place failed: dish is invalid.", LogType.Warning);
				return false;
			}
			if (!IsSlotEmpty(slotIndex))
			{
				UtilityMethods.Log($"[ConveyorBelt] Place blocked: slot {slotIndex} is already occupied.", LogType.Warning);
				return false;
			}
			if (FindSlotIndexByOccupant(dish.NetworkObject) >= 0)
			{
				UtilityMethods.Log("[ConveyorBelt] Place blocked: dish is already registered in a slot.", LogType.Warning);
				return false;
			}
			SlotPose slotPose = GetSlotPose(slotIndex);
			ulong dishNetworkObjectId = dish.NetworkObjectId;
			_pendingPlacementFinalizationIds.Add(dishNetworkObjectId);
			if (!dish.ServerPlaceOnParentDirect(base.NetworkObject, slotPose.Position, slotPose.Rotation.eulerAngles, () =>
			{
				_pendingPlacementFinalizationIds.Remove(dishNetworkObjectId);
				onClientPlacementQueued?.Invoke();
			}))
			{
				_pendingPlacementFinalizationIds.Remove(dishNetworkObjectId);
				UtilityMethods.Log("[ConveyorBelt] Place failed: server-authoritative parent placement failed.", LogType.Warning);
				return false;
			}
			_slotOccupants[slotIndex] = dish.NetworkObject.ToReference();
			dish.ServerEnterConveyorPlateMode();
			QuestReferenceHub.Instance.AddProgress(QuestId.AddPlateToConveyor);
			return true;
		}

		public bool ServerTryGetClosestDishInProbe(Bounds probeBounds, float padding, out ServedDishSushi closestDish)
		{
			closestDish = null;
			if (!base.IsServer || !base.IsSpawned || base.NetworkManager == null || !base.NetworkManager.IsListening || base.NetworkManager.ShutdownInProgress || base.NetworkManager.SpawnManager == null || _slotOccupants == null)
			{
				return false;
			}
			float num = Mathf.Max(0f, padding);
			num *= num;
			float num2 = float.MaxValue;
			float currentBaseDistance = GetCurrentBaseDistance();
			int num3 = Mathf.Min(GetSlotCount(), _slotOccupants.Count);
			for (int i = 0; i < num3; i++)
			{
				if (_slotOccupants[i].TryGet(out var networkObject, base.NetworkManager) && !(networkObject == null) && networkObject.IsSpawned && !_pendingPlacementFinalizationIds.Contains(networkObject.NetworkObjectId) && !(networkObject.transform.parent != base.transform) && networkObject.TryGetComponent<ServedDishSushi>(out var component))
				{
					Vector3 position = GetSlotPose(i, currentBaseDistance).Position;
					float num4 = probeBounds.SqrDistance(position);
					if (!(num4 > num) && !(num4 >= num2))
					{
						num2 = num4;
						closestDish = component;
					}
				}
			}
			return closestDish != null;
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientResolvePlacementRequestRpc(NetworkObjectReference servedDishRef, bool accepted, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2787058617u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in servedDishRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in accepted, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2787058617u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ResolveLocalPlacementRequest(servedDishRef, accepted);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientPlayPlatePlacedFeedbackRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3389696703u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3389696703u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (Singleton<SoundManager>.Instance != null)
				{
					Singleton<SoundManager>.Instance.PlaySFX(_plateClip, _buttonTransform.position);
				}
			}
		}

		private void OnServerTryTakeByItemTransform(ConveryorBeltTakeItemEvent e)
		{
			if (base.IsServer)
			{
				Transform slotTransform = e.SlotTransform;
				if (!(slotTransform == null) && slotTransform.TryGetComponent<NetworkObject>(out var component) && !(component == null))
				{
					ServerReleaseOccupantInternal(component, out var _, plateGraphic: false);
				}
			}
		}

		private bool ServerReleaseOccupantInternal(NetworkObject netObj, out int releasedSlotIndex, bool plateGraphic)
		{
			releasedSlotIndex = -1;
			if (!base.IsServer)
			{
				return false;
			}
			if (netObj == null || !netObj.IsSpawned)
			{
				return false;
			}
			int num = FindSlotIndexByOccupant(netObj);
			if (num < 0)
			{
				return false;
			}
			if (netObj.transform.parent == base.transform && !netObj.TryRemoveParent())
			{
				UtilityMethods.Log("[ConveyorBelt] Release failed: TryRemoveParent failed.", LogType.Warning);
				return false;
			}
			_slotOccupants[num] = default;
			if (netObj.TryGetComponent<ServedDishSushi>(out var component))
			{
				component.ServerExitConveyorPlateMode(plateGraphic);
			}
			releasedSlotIndex = num;
			return true;
		}

		private int FindSlotIndexByOccupant(NetworkObject netObj)
		{
			if (_slotOccupants == null)
			{
				return -1;
			}
			if (netObj == null)
			{
				return -1;
			}
			for (int i = 0; i < _slotOccupants.Count; i++)
			{
				if (_slotOccupants[i].TryGet(out var networkObject) && networkObject == netObj)
				{
					return i;
				}
			}
			return -1;
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientShowInfoRpc(FixedString64Bytes header, FixedString128Bytes message, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1018046172u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in header, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in message, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 1018046172u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!(Singleton<NotifyManager>.Instance == null))
				{
					Singleton<NotifyManager>.Instance.ShowToast(header.ToString(), message.ToString());
				}
			}
		}

		public bool ServerReleaseFromSlot(NetworkObject dishNetObj)
		{
			int releasedSlotIndex;
			return ServerReleaseOccupantInternal(dishNetObj, out releasedSlotIndex, plateGraphic: true);
		}

		private void OnDrawGizmosSelected()
		{
			if (_drawSlotGizmos && !(_spline == null))
			{
				if (!Application.isPlaying)
				{
					BuildDistanceLookup();
					ResolveLogicalSlotLayout();
				}
				int slotCount = GetSlotCount();
				Gizmos.color = Color.cyan;
				for (int i = 0; i < slotCount; i++)
				{
					float distance = ((!Application.isPlaying) ? RepeatDistance(_slotStartOffset + (float)i * _resolvedSlotSpacing) : GetSlotDistance(i));
					float t = DistanceToT(distance);
					Gizmos.DrawSphere((Vector3)_spline.EvaluatePosition(t) + _spline.transform.TransformVector(_slotLocalOffset), _slotGizmoRadius);
				}
			}
		}

		protected override void ClientInteract(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				base.ClientInteract(interactor, phase);
				if (!_physicalStateController.IsPhysicallyUsable)
				{
					TryUnlockTableClient(interactor);
				}
				else
				{
					TryRequestToggleBeltClient();
				}
			}
		}

		protected override void ClientCrossInteract(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				base.ClientCrossInteract(interactor, held, phase);
				if (!_physicalStateController.IsPhysicallyUsable)
				{
					TryUnlockTableClient(interactor);
				}
				else
				{
					TryRequestToggleBeltClient();
				}
			}
		}

		private void TryUnlockTableClient(Interactor interactor)
		{
			ServerTryToUnlockConveyorBeltRpc(interactor.NetworkObject.ToReference());
		}

		[Rpc(SendTo.Server)]
		private void ServerTryToUnlockConveyorBeltRpc(NetworkObjectReference interactorRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(253351153u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 253351153u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsServer)
			{
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!interactorRef.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != senderClientId)
			{
				return;
			}
			ServiceLocator.ForSceneOf(this).Get<EconomyHandler>(out var service);
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (restaurantManager == null)
			{
				UtilityMethods.Log("[ConveyorBelt] Unlock failed. RestaurantManager is null.", LogType.Warning);
				return;
			}
			if (!Singleton<GameProgressManager>.Instance.IsProgressionUnlocked(ProgressionUnlockKey.Conveyor))
			{
				BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(component.OwnerClientId, RpcTargetUse.Temp);
				ClientShowProgressionLockedNotifyRpc(baseRpcTarget);
				return;
			}
			int conveyorBeltUnlockPrice = restaurantManager.GetConveyorBeltUnlockPrice();
			if (!service.HasSufficientBalance(conveyorBeltUnlockPrice))
			{
				BaseRpcTarget baseRpcTarget2 = base.RpcTarget.Single(component.OwnerClientId, RpcTargetUse.Temp);
				ClientShowNotifyRpc(baseRpcTarget2);
			}
			else
			{
				service.SpendMoney(conveyorBeltUnlockPrice);
				TryRepairPhysicalState();
			}
		}

		private void TryRepairPhysicalState()
		{
			if (!(_physicalStateController == null) && !_physicalStateController.IsPhysicallyUsable)
			{
				_physicalStateController.Repair();
				QuestManager.Instance?.ServerReconcileRepairedConveyorQuest(_physicalStateController.IsPhysicallyUsable);
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		public void ClientShowNotifyRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(26921204u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 26921204u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Singleton<NotifyManager>.Instance.ShowToast("Not enough money to repair the Conveyor Belt!");
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientShowProgressionLockedNotifyRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1415759978u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1415759978u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Singleton<NotifyManager>.Instance?.ShowToast("Reach the required restaurant level first.", NotificationState.Info, 2f);
			}
		}

		protected override void ClientCrossMouse1(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (phase != InputActionPhase.Started)
			{
				return;
			}
			base.ClientCrossMouse1(interactor, held, phase);
			if (!_physicalStateController.IsPhysicallyUsable)
			{
				return;
			}
			if (_localPlacementRequestPending)
			{
				if ((object)_localPlacementInteractor == interactor && _localQueuedPlacementRequestCount < 32)
				{
					_localQueuedPlacementRequestCount++;
				}
			}
			else
			{
				TryStartLocalPlacementRequest(interactor, held);
			}
		}

		private void TryStartLocalPlacementRequest(Interactor interactor, BaseHoldable held)
		{
			if (interactor == null || interactor.NetworkObject == null || !(held is ServedDishSushi servedDishSushi) || servedDishSushi.NetworkObject == null || !servedDishSushi.NetworkObject.IsSpawned)
			{
				ClearLocalPlacementRequests();
				return;
			}
			_localPlacementRequestPending = true;
			_localPendingPlacementDishRef = servedDishSushi.NetworkObject.ToReference();
			_localPlacementInteractor = interactor;
			ServerTryPlaceNearestEmptySlotRpc(interactor.NetworkObject.ToReference(), _localPendingPlacementDishRef);
		}

		private void ResolveLocalPlacementRequest(NetworkObjectReference servedDishRef, bool accepted)
		{
			if (!_localPlacementRequestPending || !_localPendingPlacementDishRef.Equals(servedDishRef))
			{
				UtilityMethods.Log("[ConveyorBelt] Ignored a stale placement response while preserving the active request.", LogType.Warning);
				return;
			}
			_localPlacementRequestPending = false;
			_localPendingPlacementDishRef = default;
			if (!accepted)
			{
				ClearLocalPlacementRequests();
				return;
			}
			if (_localQueuedPlacementRequestCount <= 0)
			{
				_localPlacementInteractor = null;
				return;
			}
			_localQueuedPlacementRequestCount--;
			Interactor localPlacementInteractor = _localPlacementInteractor;
			BaseHoldable baseHoldable = ((localPlacementInteractor != null) ? localPlacementInteractor.CurrentHeld : null);
			if (!(baseHoldable is ServedDishSushi))
			{
				ClearLocalPlacementRequests();
			}
			else
			{
				TryStartLocalPlacementRequest(localPlacementInteractor, baseHoldable);
			}
		}

		private void ClearLocalPlacementRequests()
		{
			_localPlacementRequestPending = false;
			_localQueuedPlacementRequestCount = 0;
			_localPendingPlacementDishRef = default;
			_localPlacementInteractor = null;
		}

		protected override void ClientCrossMouse2(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				base.ClientCrossMouse2(interactor, held, phase);
			}
		}

		public int GetCurrentUnlockPrice()
		{
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (restaurantManager == null)
			{
				return 1000;
			}
			return restaurantManager.GetConveyorBeltUnlockPrice();
		}

		private void RefreshUnlockTooltipLocal()
		{
			if (!(_interactableTooltip == null))
			{
				TooltipData baseTooltip = _interactableTooltip.GetBaseTooltip();
				baseTooltip.Title = ProgressionTooltipLocalization.GetTitle("progression.unlock.title.dead_battery", "Dead Battery");
				baseTooltip.Body = GetUnlockTooltipBody();
				baseTooltip.OnlyHeader = false;
				baseTooltip.SetSize = true;
				baseTooltip.Size = _unlockTooltipScale;
				_interactableTooltip.SetRuntimeTooltip(baseTooltip);
			}
		}

		private string GetUnlockTooltipBody()
		{
			GameProgressManager instance = Singleton<GameProgressManager>.Instance;
			if (instance != null && !instance.IsProgressionUnlocked(ProgressionUnlockKey.Conveyor))
			{
				int requiredProgressionLevel = instance.GetRequiredProgressionLevel(ProgressionUnlockKey.Conveyor);
				if (requiredProgressionLevel <= 0)
				{
					return ProgressionTooltipLocalization.GetRequiredLevelText();
				}
				return ProgressionTooltipLocalization.GetReachLevelText(requiredProgressionLevel);
			}
			return ProgressionTooltipLocalization.GetRepairCostText(GetCurrentUnlockPrice());
		}

		public int ServerStaffReserveEmptyRefill(int employeeId, float interval)
		{
			if (!ServerStaffCanRefill)
			{
				return -1;
			}
			int num = ServerStaffReserveSlot(employeeId);
			if (num >= 0)
			{
				nextStaffRefill = Time.time + Mathf.Max(1f, interval);
			}
			return num;
		}

		public Vector3 ServerStaffSlotPosition(int slot)
		{
			return GetSlotPose(slot).Position;
		}

		public int ServerStaffReserveSlot(int employeeId)
		{
			if (!ServerStaffRunning)
			{
				return -1;
			}
			for (int i = 0; i < _slotOccupants.Count; i++)
			{
				if (IsSlotEmpty(i) && !_staffSlots.ContainsKey(i))
				{
					_staffSlots[i] = employeeId;
					return i;
				}
			}
			return -1;
		}

		public void ServerStaffReleaseSlots(int employeeId)
		{
			List<int> list = new List<int>();
			foreach (KeyValuePair<int, int> staffSlot in _staffSlots)
			{
				if (staffSlot.Value == employeeId)
				{
					list.Add(staffSlot.Key);
				}
			}
			foreach (int item in list)
			{
				_staffSlots.Remove(item);
			}
		}

		public bool ServerStaffPlace(int employeeId, int slot, ServedDishSushi dish)
		{
			if (!ServerStaffRunning || dish == null || !dish.IsSpawned || (dish.IsStaffCarried && !dish.ServerStaffCarriedBy(employeeId)) || !IsSlotIndexValid(slot) || !IsSlotEmpty(slot) || !_staffSlots.TryGetValue(slot, out var value) || value != employeeId)
			{
				return false;
			}
			SlotPose slotPose = GetSlotPose(slot);
			if (!dish.ServerPlaceOnParentDirect(base.NetworkObject, slotPose.Position, slotPose.Rotation.eulerAngles, null, staffPlacement: true))
			{
				return false;
			}
			if (dish.ServerStaffCarriedBy(employeeId))
			{
				dish.ServerStaffSetCarrier(0);
			}
			_slotOccupants[slot] = dish.NetworkObject;
			dish.ServerEnterConveyorPlateMode();
			_staffSlots.Remove(slot);
			QuestReferenceHub.Instance?.AddProgress(QuestId.AddPlateToConveyor);
			return true;
		}

		protected override void __initializeVariables()
		{
			if (_isRunning == null)
			{
				throw new Exception("ConveyorBelt._isRunning cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isRunning.Initialize(this);
			__nameNetworkVariable(_isRunning, "_isRunning");
			NetworkVariableFields.Add(_isRunning);
			if (_serverStartTime == null)
			{
				throw new Exception("ConveyorBelt._serverStartTime cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_serverStartTime.Initialize(this);
			__nameNetworkVariable(_serverStartTime, "_serverStartTime");
			NetworkVariableFields.Add(_serverStartTime);
			if (_serverStartBaseDistance == null)
			{
				throw new Exception("ConveyorBelt._serverStartBaseDistance cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_serverStartBaseDistance.Initialize(this);
			__nameNetworkVariable(_serverStartBaseDistance, "_serverStartBaseDistance");
			NetworkVariableFields.Add(_serverStartBaseDistance);
			if (_slotOccupants == null)
			{
				throw new Exception("ConveyorBelt._slotOccupants cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_slotOccupants.Initialize(this);
			__nameNetworkVariable(_slotOccupants, "_slotOccupants");
			NetworkVariableFields.Add(_slotOccupants);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3419054762u, __rpc_handler_3419054762, "ClientResetRuntimeStateRpc", RpcInvokePermission.Everyone);
			__registerRpc(1968940764u, __rpc_handler_1968940764, "ServerStartBeltRpc", RpcInvokePermission.Everyone);
			__registerRpc(3981371782u, __rpc_handler_3981371782, "ServerStopBeltRpc", RpcInvokePermission.Everyone);
			__registerRpc(1032561129u, __rpc_handler_1032561129, "ClientPlayBeltButtonFeedbackRpc", RpcInvokePermission.Everyone);
			__registerRpc(3154934907u, __rpc_handler_3154934907, "ServerTryPlaceNearestEmptySlotRpc", RpcInvokePermission.Everyone);
			__registerRpc(2787058617u, __rpc_handler_2787058617, "ClientResolvePlacementRequestRpc", RpcInvokePermission.Everyone);
			__registerRpc(3389696703u, __rpc_handler_3389696703, "ClientPlayPlatePlacedFeedbackRpc", RpcInvokePermission.Everyone);
			__registerRpc(1018046172u, __rpc_handler_1018046172, "ClientShowInfoRpc", RpcInvokePermission.Everyone);
			__registerRpc(253351153u, __rpc_handler_253351153, "ServerTryToUnlockConveyorBeltRpc", RpcInvokePermission.Everyone);
			__registerRpc(26921204u, __rpc_handler_26921204, "ClientShowNotifyRpc", RpcInvokePermission.Everyone);
			__registerRpc(1415759978u, __rpc_handler_1415759978, "ClientShowProgressionLockedNotifyRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3419054762(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBelt)target).ClientResetRuntimeStateRpc();
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1968940764(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBelt)target).ServerStartBeltRpc();
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3981371782(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBelt)target).ServerStopBeltRpc();
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1032561129(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBelt)target).ClientPlayBeltButtonFeedbackRpc();
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3154934907(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBelt)target).ServerTryPlaceNearestEmptySlotRpc(value, value2, ext);
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2787058617(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBelt)target).ClientResolvePlacementRequestRpc(value, value2, ext);
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3389696703(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBelt)target).ClientPlayPlatePlacedFeedbackRpc();
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1018046172(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out FixedString128Bytes value2, default(FastBufferWriter.ForFixedStrings));
				RpcParams ext = rpcParams.Ext;
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBelt)target).ClientShowInfoRpc(value, value2, ext);
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_253351153(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBelt)target).ServerTryToUnlockConveyorBeltRpc(value, ext);
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_26921204(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBelt)target).ClientShowNotifyRpc(ext);
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1415759978(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBelt)target).ClientShowProgressionLockedNotifyRpc(ext);
				((ConveyorBelt)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "ConveyorBelt";
		}
	}
}
