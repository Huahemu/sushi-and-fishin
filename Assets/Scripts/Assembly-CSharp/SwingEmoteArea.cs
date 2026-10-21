using System;
using PaperPixelGames.Core;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Framework.WorldUISystem;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class SwingEmoteArea : NetworkBehaviour, IBasicInteractable, IToolActiveInteractable
{
	[SerializeField]
	private StaticEmotes _emote;

	[SerializeField]
	private bool _allowOpenEmotesInput;

	[SerializeField]
	private WorldUIAnchor _worldUIAnchor;

	[Header("Swing")]
	[SerializeField]
	private Transform _swingPivot;

	[SerializeField]
	private Transform _swingMovingTransform;

	[SerializeField]
	private Transform _swingSeatPoint;

	[SerializeField]
	private Vector3 _swingAxis = Vector3.right;

	[SerializeField]
	private float _swingGravity = 220f;

	[SerializeField]
	private float _swingLinearDamping = 0.08f;

	[SerializeField]
	private float _swingAirDrag = 0.00035f;

	[SerializeField]
	private float _swingPumpAcceleration = 420f;

	[SerializeField]
	private float _swingStartPumpMultiplier = 0.1f;

	[SerializeField]
	private float _swingMistimedPumpBrake = 0.35f;

	[SerializeField]
	private float _swingMaxAngularVelocity = 900f;

	[SerializeField]
	[Min(0.04f)]
	private float _swingMotionSyncInterval = 0.1f;

	[SerializeField]
	[Range(0f, 1f)]
	private float _swingObserverAngleBlend = 0.35f;

	[SerializeField]
	[Range(0f, 1f)]
	private float _swingObserverVelocityBlend = 0.65f;

	[SerializeField]
	private float _releaseVelocityScale = 1f;

	[SerializeField]
	private float _releaseUpBoost = 1.15f;

	[SerializeField]
	private float _releaseMaxSpeed = 18f;

	[Header("Swing Look")]
	[SerializeField]
	[Range(0f, 180f)]
	private float _lookMaxYaw = 70f;

	[SerializeField]
	[Range(0f, 90f)]
	private float _lookMaxPitch = 30f;

	private readonly NetworkVariable<bool> _swingOccupied = new NetworkVariable<bool>(value: false);

	private Transform _activeSwingPlayerTransform;

	private PPG_Player _activeSwingPlayer;

	private NetworkObjectReference _activeSwingPlayerReference;

	private bool _hasActiveSwingPlayer;

	private bool _swingLocallyOccupied;

	private bool _swingLocalOwner;

	private float _swingLocalAngle;

	private float _swingLocalAngularVelocity;

	private float _swingLocalPumpInput;

	private float _nextSwingMotionSyncTime;

	private float _swingDisplayAngle;

	private Quaternion _swingPivotBaseLocalRotation;

	private Quaternion _swingMovingBaseLocalRotation;

	private const float SwingRestAngleThreshold = 0.05f;

	private const float SwingRestVelocityThreshold = 0.1f;

	public bool CanGaze => true;

	public bool CanInteractable => true;

	public WorldUIAnchor WorldUIAnchor => _worldUIAnchor;

	public bool AllowOpenEmotesInput => _allowOpenEmotesInput;

	public Transform SeatPoint
	{
		get
		{
			if (!(_swingSeatPoint != null))
			{
				return _swingPivot;
			}
			return _swingSeatPoint;
		}
	}

	public float LookMaxYaw => _lookMaxYaw;

	public float LookMaxPitch => _lookMaxPitch;

	public Vector3 GetReleaseVelocity()
	{
		if (_swingPivot == null || SeatPoint == null)
		{
			return Vector3.zero;
		}
		Vector3 rhs = SeatPoint.position - _swingPivot.position;
		if (rhs.sqrMagnitude < 0.0001f)
		{
			return Vector3.zero;
		}
		Vector3 lhs = _swingPivot.TransformDirection(_swingAxis.normalized);
		float num = _swingLocalAngularVelocity * (MathF.PI / 180f);
		Vector3 result = Vector3.Cross(lhs, rhs) * num * Mathf.Max(0f, _releaseVelocityScale) + Vector3.up * _releaseUpBoost;
		float num2 = Mathf.Max(1f, _releaseMaxSpeed);
		if (result.magnitude > num2)
		{
			result = result.normalized * num2;
		}
		return result;
	}

	private void Awake()
	{
		if (_swingPivot == null)
		{
			_swingPivot = base.transform;
		}
		_swingPivotBaseLocalRotation = _swingPivot.localRotation;
		if (_swingMovingTransform != null)
		{
			_swingMovingBaseLocalRotation = _swingMovingTransform.localRotation;
		}
	}

	public override void OnNetworkDespawn()
	{
		ReleaseActivePlayerLocally(forceCancelObservedLock: true);
		base.OnNetworkDespawn();
	}

	private void OnDisable()
	{
		ReleaseActivePlayerLocally(forceCancelObservedLock: true);
	}

	private void Update()
	{
		if (_swingLocallyOccupied || Mathf.Abs(_swingLocalAngle) > 0.05f || Mathf.Abs(_swingLocalAngularVelocity) > 0.1f || Mathf.Abs(_swingDisplayAngle) > 0.05f || !(_activeSwingPlayerTransform == null) || !(_activeSwingPlayer == null))
		{
			SimulateSwing(Time.deltaTime);
			TickSwingMotionSync();
			ApplySwingVisual();
			SnapActivePlayerToSwingSeat();
		}
	}

	public void GazeEnter()
	{
		UtilityMethods.Log("[SwingEmoteArea] Gaze Entered " + base.name);
		PPG_Player pPG_Player = Singleton<PlayersManager>.Instance?.LocalPlayer;
		if (((object)pPG_Player == null || !pPG_Player.IsUsingEmoteArea) && ((object)pPG_Player == null || pPG_Player.CanEnterEmoteArea))
		{
			Singleton<HintsManager>.Instance?.AddHint("Interact", "key_interact");
		}
	}

	public void GazeExit()
	{
		UtilityMethods.Log("[SwingEmoteArea] Gaze Exit " + base.name);
		Singleton<HintsManager>.Instance?.RemoveHint("Interact");
	}

	public void TriggerCrossInput(Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase)
	{
		TriggerInput(interactor, inputKind, phase);
	}

	public void TriggerInput(Interactor interactor, InputKind inputKind, InputActionPhase phase)
	{
		if (_swingOccupied.Value || inputKind != InputKind.Interact || phase != InputActionPhase.Canceled)
		{
			return;
		}
		if (_swingPivot == null)
		{
			UtilityMethods.Log("[SwingEmoteArea] Start failed. Swing pivot is null on " + base.name + ".", LogType.Error);
		}
		else if (interactor.Player.TryPrepareForEmoteArea())
		{
			Singleton<HintsManager>.Instance?.RemoveHint("Interact");
			interactor.Player.StartSwingEmote(_emote, this);
			if (_worldUIAnchor != null)
			{
				_worldUIAnchor.SetVisible(value: false);
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerStartSwingRpc(NetworkObjectReference playerRef, RpcParams rpcParams = default(RpcParams))
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
			FastBufferWriter bufferWriter = __beginSendRpc(4272758259u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in playerRef, default(FastBufferWriter.ForNetworkSerializable));
			__endSendRpc(ref bufferWriter, 4272758259u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		ulong senderClientId = rpcParams.Receive.SenderClientId;
		RpcParams rpcParams3 = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
		if (!playerRef.TryGet(out var networkObject) || networkObject.OwnerClientId != senderClientId)
		{
			UtilityMethods.Log("[SwingEmoteArea] Start rejected. Sender does not own the player reference.", LogType.Warning);
			ClientRejectSwingStartRpc(playerRef, rpcParams3);
		}
		else if (_hasActiveSwingPlayer)
		{
			if (!_activeSwingPlayerReference.Equals(playerRef))
			{
				UtilityMethods.Log("[SwingEmoteArea] Start rejected. Swing is already occupied.", LogType.Warning);
				ClientRejectSwingStartRpc(playerRef, rpcParams3);
			}
		}
		else if (_swingOccupied.Value)
		{
			UtilityMethods.Log("[SwingEmoteArea] Start rejected. Occupancy state is unavailable.", LogType.Warning);
			ClientRejectSwingStartRpc(playerRef, rpcParams3);
		}
		else
		{
			_activeSwingPlayerReference = playerRef;
			_hasActiveSwingPlayer = true;
			_swingOccupied.Value = true;
			ClientStartSwingRpc(playerRef);
		}
	}

	[Rpc(SendTo.SpecifiedInParams)]
	private void ClientRejectSwingStartRpc(NetworkObjectReference playerRef, RpcParams rpcParams = default(RpcParams))
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
			FastBufferWriter bufferWriter = __beginSendRpc(4294057420u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in playerRef, default(FastBufferWriter.ForNetworkSerializable));
			__endSendRpc(ref bufferWriter, 4294057420u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (playerRef.TryGet(out var networkObject) && networkObject.IsOwner && networkObject.TryGetComponent<PPG_Player>(out var component))
			{
				component.HandleSwingStartRejected(this);
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerStopSwingRpc(RpcParams rpcParams = default(RpcParams))
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
			FastBufferWriter bufferWriter = __beginSendRpc(4168332401u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 4168332401u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!_hasActiveSwingPlayer || !_activeSwingPlayerReference.TryGet(out var networkObject) || networkObject.OwnerClientId != rpcParams.Receive.SenderClientId)
			{
				UtilityMethods.Log("[SwingEmoteArea] Stop rejected. Sender does not own the active swing player.", LogType.Warning);
				return;
			}
			_swingOccupied.Value = false;
			Vector3 exitPosition = ((SeatPoint != null) ? SeatPoint.position : networkObject.transform.position);
			Quaternion identity = Quaternion.identity;
			ClientStopSwingRpc(_activeSwingPlayerReference, exitPosition, identity);
			_activeSwingPlayerReference = default;
			_hasActiveSwingPlayer = false;
		}
	}

	public void SetLocalSwingPumpInput(float input)
	{
		_swingLocalPumpInput = Mathf.Clamp(input, -1f, 1f);
	}

	[Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
	private void ServerSyncSwingMotionRpc(float angle, float angularVelocity, RpcParams rpcParams = default(RpcParams))
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
			RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
			{
				Delivery = RpcDelivery.Unreliable
			};
			FastBufferWriter bufferWriter = __beginSendRpc(676603274u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Unreliable);
			bufferWriter.WriteValueSafe(in angle, default(FastBufferWriter.ForPrimitives));
			bufferWriter.WriteValueSafe(in angularVelocity, default(FastBufferWriter.ForPrimitives));
			__endSendRpc(ref bufferWriter, 676603274u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Unreliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_hasActiveSwingPlayer && _activeSwingPlayerReference.TryGet(out var networkObject) && networkObject.OwnerClientId == rpcParams.Receive.SenderClientId)
			{
				ClientSyncSwingMotionRpc(networkObject.OwnerClientId, angle, angularVelocity);
			}
		}
	}

	[Rpc(SendTo.Everyone, Delivery = RpcDelivery.Unreliable)]
	private void ClientSyncSwingMotionRpc(ulong riderOwnerClientId, float angle, float angularVelocity)
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
				Delivery = RpcDelivery.Unreliable
			};
			RpcParams rpcParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(118618823u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Unreliable);
			BytePacker.WriteValueBitPacked(bufferWriter, riderOwnerClientId);
			bufferWriter.WriteValueSafe(in angle, default(FastBufferWriter.ForPrimitives));
			bufferWriter.WriteValueSafe(in angularVelocity, default(FastBufferWriter.ForPrimitives));
			__endSendRpc(ref bufferWriter, 118618823u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Unreliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		if (_swingLocallyOccupied && !(_activeSwingPlayer == null) && _activeSwingPlayer.OwnerClientId == riderOwnerClientId && !_swingLocalOwner)
		{
			float num = Mathf.DeltaAngle(_swingLocalAngle, angle);
			if (Mathf.Abs(num) > 20f)
			{
				_swingLocalAngle = angle;
			}
			else
			{
				_swingLocalAngle += num * _swingObserverAngleBlend;
			}
			_swingLocalAngularVelocity = Mathf.Lerp(_swingLocalAngularVelocity, angularVelocity, _swingObserverVelocityBlend);
		}
	}

	[Rpc(SendTo.Everyone)]
	private void ClientStartSwingRpc(NetworkObjectReference playerRef)
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
			FastBufferWriter bufferWriter = __beginSendRpc(597287063u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in playerRef, default(FastBufferWriter.ForNetworkSerializable));
			__endSendRpc(ref bufferWriter, 597287063u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		ReleaseActivePlayerLocally(forceCancelObservedLock: true);
		if (!playerRef.TryGet(out var networkObject))
		{
			UtilityMethods.Log("[SwingEmoteArea] Failed to resolve player reference.", LogType.Warning);
			return;
		}
		_activeSwingPlayerTransform = networkObject.transform;
		_activeSwingPlayer = null;
		BeginLocalSwingMotion(networkObject.IsOwner);
		if (networkObject.TryGetComponent<PPG_Player>(out var component))
		{
			_activeSwingPlayer = component;
			component.StartObservedSwingEmote(_emote);
		}
		SnapActivePlayerToSwingSeat();
	}

	[Rpc(SendTo.Everyone)]
	private void ClientStopSwingRpc(NetworkObjectReference playerRef, Vector3 exitPosition, Quaternion exitRotation)
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
			FastBufferWriter bufferWriter = __beginSendRpc(3976740151u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in playerRef, default(FastBufferWriter.ForNetworkSerializable));
			bufferWriter.WriteValueSafe(in exitPosition);
			bufferWriter.WriteValueSafe(in exitRotation);
			__endSendRpc(ref bufferWriter, 3976740151u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		Vector3 vector = ((SeatPoint != null) ? SeatPoint.position : exitPosition);
		if (playerRef.TryGet(out var networkObject))
		{
			if (networkObject.TryGetComponent<PPG_Player>(out var component))
			{
				if (component.IsOwner)
				{
					component.ApplySwingExitPose(vector, exitRotation);
				}
				else
				{
					component.EndObservedSwingVisualLock(vector, exitRotation);
				}
			}
			else
			{
				networkObject.transform.SetPositionAndRotation(vector, exitRotation);
			}
		}
		EndLocalSwingMotion();
		ReleaseActivePlayerLocally(forceCancelObservedLock: false);
	}

	public void ReleasePlayerLocally(Transform playerTransform)
	{
		if (!(playerTransform != null) || !(_activeSwingPlayerTransform != playerTransform))
		{
			ReleaseActivePlayerLocally(forceCancelObservedLock: false);
		}
	}

	private void SimulateSwing(float deltaTime)
	{
		float swingLocalAngle = _swingLocalAngle;
		float swingLocalAngularVelocity = _swingLocalAngularVelocity;
		float num = swingLocalAngle * (MathF.PI / 180f);
		float num2 = (0f - Mathf.Max(_swingGravity, 180f)) * Mathf.Sin(num);
		num2 -= _swingLinearDamping * swingLocalAngularVelocity;
		num2 -= _swingAirDrag * swingLocalAngularVelocity * Mathf.Abs(swingLocalAngularVelocity);
		if (_swingLocallyOccupied)
		{
			num2 += CalculatePumpAcceleration(num, swingLocalAngularVelocity);
		}
		float num3 = Mathf.Max(_swingMaxAngularVelocity, 720f);
		swingLocalAngularVelocity = Mathf.Clamp(swingLocalAngularVelocity + num2 * deltaTime, 0f - num3, num3);
		swingLocalAngle += swingLocalAngularVelocity * deltaTime;
		_swingLocalAngle = swingLocalAngle;
		_swingLocalAngularVelocity = swingLocalAngularVelocity;
		if (!_swingLocallyOccupied && IsSwingAtRest())
		{
			_swingLocalAngle = 0f;
			_swingLocalAngularVelocity = 0f;
		}
	}

	private float CalculatePumpAcceleration(float angleRad, float velocity)
	{
		float swingLocalPumpInput = _swingLocalPumpInput;
		if (Mathf.Abs(swingLocalPumpInput) < 0.01f)
		{
			return 0f;
		}
		float num = Mathf.Clamp01(Mathf.Abs(Mathf.Cos(angleRad)));
		num *= num;
		if (Mathf.Abs(velocity) < 2f)
		{
			return swingLocalPumpInput * Mathf.Max(_swingPumpAcceleration, 260f) * Mathf.Clamp01(_swingStartPumpMultiplier) * num;
		}
		float num2 = Mathf.Sign(swingLocalPumpInput);
		float b = Mathf.Sign(velocity);
		bool num3 = Mathf.Approximately(num2, b);
		float num4 = Mathf.Max(_swingPumpAcceleration, 260f);
		if (num3)
		{
			return num2 * num4 * num;
		}
		return num2 * num4 * _swingMistimedPumpBrake * num;
	}

	private void ApplySwingVisual()
	{
		if (!(_swingPivot == null))
		{
			_swingDisplayAngle = _swingLocalAngle;
			Quaternion quaternion = Quaternion.AngleAxis(_swingDisplayAngle, _swingAxis.normalized);
			_swingPivot.localRotation = _swingPivotBaseLocalRotation * quaternion;
			if (_swingMovingTransform != null && _swingMovingTransform != _swingPivot)
			{
				_swingMovingTransform.localRotation = _swingMovingBaseLocalRotation * quaternion;
			}
		}
	}

	private void SnapActivePlayerToSwingSeat()
	{
		if (!(SeatPoint == null))
		{
			if (_activeSwingPlayer != null)
			{
				_activeSwingPlayer.ApplySwingSeatVisualPose(SeatPoint.position, SeatPoint.rotation);
			}
			else if (!(_activeSwingPlayerTransform == null))
			{
				_activeSwingPlayerTransform.SetPositionAndRotation(SeatPoint.position, SeatPoint.rotation);
			}
		}
	}

	private void ReleaseActivePlayerLocally(bool forceCancelObservedLock)
	{
		if (forceCancelObservedLock)
		{
			_activeSwingPlayer?.ForceCancelObservedSwingVisualLock();
		}
		_activeSwingPlayer = null;
		_activeSwingPlayerTransform = null;
	}

	private bool IsSwingAtRest()
	{
		if (Mathf.Abs(_swingLocalAngle) <= 0.05f)
		{
			return Mathf.Abs(_swingLocalAngularVelocity) <= 0.1f;
		}
		return false;
	}

	private void BeginLocalSwingMotion(bool localOwner)
	{
		_swingLocallyOccupied = true;
		_swingLocalOwner = localOwner;
		_swingLocalPumpInput = 0f;
		_nextSwingMotionSyncTime = 0f;
	}

	private void EndLocalSwingMotion()
	{
		_swingLocallyOccupied = false;
		_swingLocalOwner = false;
		_swingLocalPumpInput = 0f;
	}

	private void TickSwingMotionSync()
	{
		if (_swingLocallyOccupied && _swingLocalOwner && !(Time.time < _nextSwingMotionSyncTime))
		{
			_nextSwingMotionSyncTime = Time.time + Mathf.Max(0.04f, _swingMotionSyncInterval);
			ServerSyncSwingMotionRpc(_swingLocalAngle, _swingLocalAngularVelocity);
		}
	}

	protected override void __initializeVariables()
	{
		if (_swingOccupied == null)
		{
			throw new Exception("SwingEmoteArea._swingOccupied cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		_swingOccupied.Initialize(this);
		__nameNetworkVariable(_swingOccupied, "_swingOccupied");
		NetworkVariableFields.Add(_swingOccupied);
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		__registerRpc(4272758259u, __rpc_handler_4272758259, "ServerStartSwingRpc", RpcInvokePermission.Everyone);
		__registerRpc(4294057420u, __rpc_handler_4294057420, "ClientRejectSwingStartRpc", RpcInvokePermission.Everyone);
		__registerRpc(4168332401u, __rpc_handler_4168332401, "ServerStopSwingRpc", RpcInvokePermission.Everyone);
		__registerRpc(676603274u, __rpc_handler_676603274, "ServerSyncSwingMotionRpc", RpcInvokePermission.Everyone);
		__registerRpc(118618823u, __rpc_handler_118618823, "ClientSyncSwingMotionRpc", RpcInvokePermission.Everyone);
		__registerRpc(597287063u, __rpc_handler_597287063, "ClientStartSwingRpc", RpcInvokePermission.Everyone);
		__registerRpc(3976740151u, __rpc_handler_3976740151, "ClientStopSwingRpc", RpcInvokePermission.Everyone);
		base.__initializeRpcs();
	}

	private static void __rpc_handler_4272758259(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
			RpcParams ext = rpcParams.Ext;
			((SwingEmoteArea)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((SwingEmoteArea)target).ServerStartSwingRpc(value, ext);
			((SwingEmoteArea)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_4294057420(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
			RpcParams ext = rpcParams.Ext;
			((SwingEmoteArea)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((SwingEmoteArea)target).ClientRejectSwingStartRpc(value, ext);
			((SwingEmoteArea)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_4168332401(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			RpcParams ext = rpcParams.Ext;
			((SwingEmoteArea)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((SwingEmoteArea)target).ServerStopSwingRpc(ext);
			((SwingEmoteArea)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_676603274(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out float value, default(FastBufferWriter.ForPrimitives));
			reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
			RpcParams ext = rpcParams.Ext;
			((SwingEmoteArea)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((SwingEmoteArea)target).ServerSyncSwingMotionRpc(value, value2, ext);
			((SwingEmoteArea)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_118618823(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
			reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
			reader.ReadValueSafe(out float value3, default(FastBufferWriter.ForPrimitives));
			((SwingEmoteArea)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((SwingEmoteArea)target).ClientSyncSwingMotionRpc(value, value2, value3);
			((SwingEmoteArea)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_597287063(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
			((SwingEmoteArea)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((SwingEmoteArea)target).ClientStartSwingRpc(value);
			((SwingEmoteArea)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3976740151(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
			reader.ReadValueSafe(out Vector3 value2);
			reader.ReadValueSafe(out Quaternion value3);
			((SwingEmoteArea)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((SwingEmoteArea)target).ClientStopSwingRpc(value, value2, value3);
			((SwingEmoteArea)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	protected override string __getTypeName()
	{
		return "SwingEmoteArea";
	}
}
