using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Ocean
{
	public class Totti : BaseInteractable, IAttachableObject, IToolActiveInteractable
	{
		private static readonly int HashBlend = Animator.StringToHash("Blend");

		private static readonly int HashIsSwimming = Animator.StringToHash("IsSwiming");

		[Header("Refs")]
		[SerializeField]
		private TottiPathFollower _pathFollower;

		[SerializeField]
		private Animator _animator;

		[SerializeField]
		private Collider _collider;

		[SerializeField]
		private Canvas _holdCanvas;

		[SerializeField]
		private Image _holdImage;

		[SerializeField]
		private float _holdTime;

		[SerializeField]
		private AudioClipSO _loopSFX;

		[SerializeField]
		private AudioClipSO _swimSFX;

		[SerializeField]
		[Min(1f)]
		private float _swimSfxSampleRate = 30f;

		[SerializeField]
		[Min(0f)]
		private int _firstSwimSfxFrame = 15;

		[SerializeField]
		[Min(0f)]
		private int _secondSwimSfxFrame = 45;

		[Header("Seat Settings")]
		[SerializeField]
		private int _maxSeatCount = 4;

		[Header("Turn Animation")]
		[SerializeField]
		private float _maxTurnDegPerSec = 120f;

		[SerializeField]
		private float _deadZoneDegPerSec = 3f;

		[SerializeField]
		private float _animatorDamp = 0.12f;

		[Header("End Of Ride Boost")]
		[SerializeField]
		private bool _boostEndOfRide;

		[SerializeField]
		private Vector3 _endOfRideBoostLocalDirection = new Vector3(0f, 0.35f, 1f);

		[SerializeField]
		[Min(0f)]
		private float _endOfRideBoostImpulse = 10f;

		[SerializeField]
		[Min(0.01f)]
		private float _endOfRideBoostGizmoImpulseScale = 0.25f;

		[Header("Debug")]
		[SerializeField]
		private bool _logDebug;

		private readonly Dictionary<ulong, int> _clientSeatMap = new Dictionary<ulong, int>();

		private readonly Dictionary<int, NetworkObjectReference> _seatOccupants = new Dictionary<int, NetworkObjectReference>();

		private Vector3 _previousPosition;

		private Vector3 _previousForward;

		private bool _localPlayerIsBoarded;

		private bool _isSwimmingState;

		private bool _swimStrokeSfxPlayedForSession;

		private Tweener _holdTween;

		private OwnedLoopHandle _loopSfxHandle;

		private AudioSource _loopSfxSource;

		private Coroutine _swimStrokeSfxCoroutine;

		[Header("Seats")]
		[field: SerializeField]
		public List<AttachSeat> Seats { get; set; } = new List<AttachSeat>();

		public PlayerAttachmentMode AttachmentMode => PlayerAttachmentMode.Mount;

		public bool ServerTryReleasePlayerForRecovery(PPG_Player player, string reason)
		{
			if (!base.IsServer || player == null || !_clientSeatMap.TryGetValue(player.OwnerClientId, out var value) || !_seatOccupants.TryGetValue(value, out var value2) || !value2.TryGet(out var networkObject) || !networkObject.TryGetComponent<Interactor>(out var component))
			{
				return false;
			}
			UnseatPlayerServer(component, value, value2);
			UtilityMethods.Log($"[Totti] Player released by recovery. ClientId={player.OwnerClientId} Reason={reason}");
			return true;
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			_holdCanvas.enabled = false;
			_isSwimmingState = false;
			_swimStrokeSfxPlayedForSession = false;
			if (_pathFollower == null)
			{
				UtilityMethods.Log("[Totti] PathFollower reference is missing.");
				base.enabled = false;
				return;
			}
			_pathFollower.OnPreStartStarted += HandlePreStartStartedServer;
			_pathFollower.OnRideStarted += HandleRideStartedServer;
			_pathFollower.OnReachedDestination += HandleReachedDestinationServer;
			_pathFollower.OnReturnedHome += HandleReturnedHomeServer;
			_pathFollower.OnRideAborted += HandleRideAbortedServer;
			_previousPosition = base.transform.position;
			_previousForward = Flatten(base.transform.forward);
		}

		public override void OnNetworkDespawn()
		{
			if (base.IsServer)
			{
				DetachAllRidersServer(notifyClients: false);
			}
			StopLoopSfx();
			StopSwimStrokeSfxSequence(resetSession: true);
			ClearLocalRideRuntime();
			if (_pathFollower != null)
			{
				_pathFollower.OnPreStartStarted -= HandlePreStartStartedServer;
				_pathFollower.OnRideStarted -= HandleRideStartedServer;
				_pathFollower.OnReachedDestination -= HandleReachedDestinationServer;
				_pathFollower.OnReturnedHome -= HandleReturnedHomeServer;
				_pathFollower.OnRideAborted -= HandleRideAbortedServer;
			}
			base.OnNetworkDespawn();
		}

		private void OnDisable()
		{
			StopLoopSfx();
			StopSwimStrokeSfxSequence(resetSession: true);
		}

		private void Update()
		{
			UpdateTurnAnimator();
		}

		protected override bool CanTrigger(Interactor interactor)
		{
			if (base.CanTrigger(interactor))
			{
				return !_localPlayerIsBoarded;
			}
			return false;
		}

		protected override void ClientInteract(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && !(interactor == null) && interactor.IsLocalPlayer && !_localPlayerIsBoarded)
			{
				PrepareLocalPlayerForBoardRequest(interactor);
				ServerTryBoardRpc(interactor.NetworkObject.ToReference());
			}
		}

		protected override void ClientCrossInteract(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			ClientInteract(interactor, phase);
		}

		private void PrepareLocalPlayerForBoardRequest(Interactor interactor)
		{
			if (!(interactor == null) && interactor.IsLocalPlayer && !(interactor.Player == null))
			{
				interactor.Player.DisableActiveTool();
				if (Singleton<QuickSlotHandler>.Instance != null)
				{
					Singleton<QuickSlotHandler>.Instance.DeselectSlot();
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTryBoardRpc(NetworkObjectReference interactorRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3787872840u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3787872840u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!interactorRef.TryGet(out var networkObject) || !networkObject.TryGetComponent<Interactor>(out var component) || component.Player == null)
			{
				return;
			}
			if (Seats == null || Seats.Count == 0)
			{
				if (_logDebug)
				{
					UtilityMethods.Log("[Totti] Seats list is empty.");
				}
				return;
			}
			if (_pathFollower == null)
			{
				UtilityMethods.Log("[Totti] Boarding rejected. PathFollower reference is missing.", LogType.Warning);
				return;
			}
			if (!_pathFollower.HasValidPath)
			{
				UtilityMethods.Log("[Totti] Boarding rejected. PathFollower spline is missing.", LogType.Warning);
				return;
			}
			if (IsAlreadyBoarded(component.OwnerClientId))
			{
				if (_logDebug)
				{
					UtilityMethods.Log("[Totti] Player already boarded.");
				}
				return;
			}
			int firstAvailableSeatIndex = GetFirstAvailableSeatIndex();
			if (firstAvailableSeatIndex < 0)
			{
				if (_logDebug)
				{
					UtilityMethods.Log("[Totti] No available seat.");
				}
				return;
			}
			AssignSeatServer(firstAvailableSeatIndex, component);
			if (_pathFollower.State == TottiPathFollower.RideState.Idle)
			{
				_pathFollower.StartRideServer();
			}
		}

		private void AssignSeatServer(int seatIndex, Interactor interactor)
		{
			interactor.Player.FootIK.enabled = false;
			_clientSeatMap[interactor.OwnerClientId] = seatIndex;
			_seatOccupants[seatIndex] = interactor.NetworkObject.ToReference();
			interactor.Player.ClientAssignParentConstraintSeatRpc(base.NetworkObject.ToReference(), (byte)seatIndex, 0u);
			NotifySeatAssignedClientRpc(seatIndex, interactor.NetworkObject.ToReference());
			if (_logDebug)
			{
				UtilityMethods.Log($"[Totti] Seat assigned. SeatIndex={seatIndex} ClientId={interactor.OwnerClientId}");
			}
		}

		private void HandlePreStartStartedServer()
		{
			NotifySwimmingStateClientRpc(isSwimming: true);
		}

		private void HandleRideStartedServer()
		{
			NotifySwimmingStateClientRpc(isSwimming: true);
		}

		private void HandleReachedDestinationServer()
		{
			if (_logDebug)
			{
				UtilityMethods.Log("[Totti] Reached destination. Detaching all riders.");
			}
			DetachAllRidersServer(notifyClients: true, _boostEndOfRide);
		}

		private void HandleReturnedHomeServer()
		{
			NotifySwimmingStateClientRpc(isSwimming: false);
			DetachAllRidersServer();
		}

		private void HandleRideAbortedServer()
		{
			DetachAllRidersServer();
			NotifySwimmingStateClientRpc(isSwimming: false);
		}

		private void DetachAllRidersServer(bool notifyClients = true, bool applyEndOfRideBoost = false)
		{
			List<KeyValuePair<int, NetworkObjectReference>> list = new List<KeyValuePair<int, NetworkObjectReference>>(_seatOccupants);
			bool flag = applyEndOfRideBoost && _endOfRideBoostImpulse > 0f;
			Vector3 endOfRideBoostDirection = (flag ? ResolveEndOfRideBoostDirection() : Vector3.zero);
			foreach (KeyValuePair<int, NetworkObjectReference> item in list)
			{
				int key = item.Key;
				NetworkObjectReference value = item.Value;
				if (!value.TryGet(out var networkObject))
				{
					ClearSeatServer(key);
					continue;
				}
				if (!networkObject.TryGetComponent<Interactor>(out var component))
				{
					ClearSeatServer(key);
					continue;
				}
				if (component.Player != null)
				{
					component.Player.ClientDeAssignParentConstraintRpc();
					component.Player.FootIK.enabled = true;
				}
				if (notifyClients)
				{
					NotifySeatDetachedClientRpc(key, value, flag, endOfRideBoostDirection, _endOfRideBoostImpulse);
				}
				ClearSeatServer(key);
			}
		}

		public void ServerEndDayReset()
		{
			if (base.IsServer)
			{
				bool num = _pathFollower != null && _pathFollower.IsBusy;
				_pathFollower?.AbortRideServer();
				if (!num || _seatOccupants.Count > 0)
				{
					DetachAllRidersServer();
					NotifySwimmingStateClientRpc(isSwimming: false);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void NotifySwimmingStateClientRpc(bool isSwimming)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3748195396u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in isSwimming, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3748195396u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!(_animator == null))
			{
				_isSwimmingState = isSwimming;
				_animator.SetBool(HashIsSwimming, isSwimming);
				RefreshLoopSfxState();
				if (!isSwimming)
				{
					_animator.SetFloat(HashBlend, 0f);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void NotifySeatAssignedClientRpc(int seatIndex, NetworkObjectReference interactorRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3481227451u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, seatIndex);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3481227451u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (TryResolveLocalInteractor(interactorRef, out var interactor))
			{
				_collider.enabled = false;
				_localPlayerIsBoarded = true;
				RefreshLoopSfxState();
				PrepareLocalPlayerForRide(interactor, seatIndex);
				Singleton<DelayHelper>.Instance.DelayActionUntilEndOfFrame(() =>
				{
					GazeExit();
					Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_leave_hold");
				});
			}
		}

		[Rpc(SendTo.Everyone)]
		private void NotifySeatDetachedClientRpc(int seatIndex, NetworkObjectReference interactorRef, bool applyEndOfRideBoost, Vector3 endOfRideBoostDirection, float endOfRideBoostImpulse)
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
				FastBufferWriter bufferWriter = __beginSendRpc(777924158u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, seatIndex);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in applyEndOfRideBoost, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in endOfRideBoostDirection);
				bufferWriter.WriteValueSafe(in endOfRideBoostImpulse, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 777924158u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (TryResolveLocalInteractor(interactorRef, out var interactor))
			{
				_localPlayerIsBoarded = false;
				RefreshLoopSfxState();
				_collider.enabled = true;
				RestoreLocalPlayerAfterRide(interactor);
				Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
				StopUnSeatHold();
				if (applyEndOfRideBoost)
				{
					ApplyEndOfRideBoostLocal(interactor, endOfRideBoostDirection, endOfRideBoostImpulse);
				}
			}
		}

		private void PrepareLocalPlayerForRide(Interactor interactor, int seatIndex)
		{
			if (!(interactor == null) && interactor.IsLocalPlayer && !(interactor.Player == null))
			{
				interactor.Player.SwitchToTpsCam();
				interactor.Player.AnimSetBool(PPG_Player.TOTTI, value: true);
				if (IsValidSeatIndex(seatIndex))
				{
					Vector3 lookAt = Seats[seatIndex].LookAt;
					interactor.Player.FirstPersonCharacter.LookAtInstant(lookAt);
				}
			}
		}

		private void RestoreLocalPlayerAfterRide(Interactor interactor)
		{
			if (!(interactor == null) && interactor.IsLocalPlayer && !(interactor.Player == null))
			{
				interactor.Player.SwitchToFpsCam();
				interactor.Player.AnimSetBool(PPG_Player.TOTTI, value: false);
			}
		}

		private void ClearLocalRideRuntime()
		{
			PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
			bool flag = pPG_Player != null && pPG_Player.SeatedTotti == this;
			if (_localPlayerIsBoarded || flag)
			{
				if (flag)
				{
					pPG_Player.ClientDeAssignParentConstraint();
				}
				if (pPG_Player != null && pPG_Player.Interactor != null)
				{
					RestoreLocalPlayerAfterRide(pPG_Player.Interactor);
				}
				_localPlayerIsBoarded = false;
				RefreshLoopSfxState();
				if (_collider != null)
				{
					_collider.enabled = true;
				}
				if (Singleton<HintsManager>.Instance != null)
				{
					Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
				}
				StopUnSeatHold();
			}
		}

		private int GetFirstAvailableSeatIndex()
		{
			int num = Mathf.Min(_maxSeatCount, Seats.Count);
			for (int i = 0; i < num; i++)
			{
				if (!_seatOccupants.ContainsKey(i))
				{
					return i;
				}
			}
			return -1;
		}

		private bool IsAlreadyBoarded(ulong clientId)
		{
			return _clientSeatMap.ContainsKey(clientId);
		}

		private void ClearSeatServer(int seatIndex)
		{
			ulong key = 0uL;
			bool flag = false;
			foreach (KeyValuePair<ulong, int> item in _clientSeatMap)
			{
				if (item.Value == seatIndex)
				{
					key = item.Key;
					flag = true;
					break;
				}
			}
			if (flag)
			{
				_clientSeatMap.Remove(key);
			}
			_seatOccupants.Remove(seatIndex);
		}

		private bool TryResolveLocalInteractor(NetworkObjectReference interactorRef, out Interactor interactor)
		{
			interactor = null;
			if (!interactorRef.TryGet(out var networkObject))
			{
				return false;
			}
			if (!networkObject.TryGetComponent<Interactor>(out interactor))
			{
				return false;
			}
			return interactor.IsLocalPlayer;
		}

		private bool IsValidSeatIndex(int seatIndex)
		{
			if (Seats != null && seatIndex >= 0)
			{
				return seatIndex < Seats.Count;
			}
			return false;
		}

		private Vector3 ResolveEndOfRideBoostDirection()
		{
			return ResolveEndOfRideBoostDirection(base.transform.rotation);
		}

		private Vector3 ResolveEndOfRideBoostDirection(Quaternion sourceRotation)
		{
			Vector3 vector = _endOfRideBoostLocalDirection;
			if (vector.sqrMagnitude < 0.0001f)
			{
				vector = Vector3.forward;
			}
			return sourceRotation * vector.normalized;
		}

		private void ApplyEndOfRideBoostLocal(Interactor interactor, Vector3 direction, float impulse)
		{
			if (!(interactor == null) && interactor.IsLocalPlayer && !(interactor.Player == null) && !(direction.sqrMagnitude < 0.0001f) && !(impulse <= 0f))
			{
				interactor.Player.FirstPersonCharacter.ImpulseInDirection(direction, impulse);
				if (_logDebug)
				{
					UtilityMethods.Log($"[Totti] End of ride boost applied. ClientId={interactor.OwnerClientId} Direction={direction.normalized} Impulse={impulse}");
				}
			}
		}

		private void UpdateTurnAnimator()
		{
			if (_animator == null || _pathFollower == null)
			{
				return;
			}
			if (!_isSwimmingState)
			{
				_previousPosition = base.transform.position;
				_previousForward = Flatten(base.transform.forward);
				return;
			}
			float num = Mathf.Max(Time.deltaTime, 1E-05f);
			Vector3 position = base.transform.position;
			Vector3 vector = Flatten(position - _previousPosition);
			Vector3 vector2 = ((vector.sqrMagnitude > 1E-06f) ? vector : Flatten(base.transform.forward));
			float num2 = Vector3.SignedAngle(_previousForward, vector2, Vector3.up) / num;
			float num3 = Mathf.Abs(num2);
			float value = 0f;
			if (num3 > _deadZoneDegPerSec)
			{
				value = Mathf.Clamp(num2 / _maxTurnDegPerSec, -1f, 1f);
			}
			_animator.SetFloat(HashBlend, value, _animatorDamp, num);
			_previousPosition = position;
			_previousForward = vector2;
		}

		public override void EnableGazeHints()
		{
			if (!_localPlayerIsBoarded)
			{
				Singleton<HintsManager>.Instance.AddHint("Interact", "key_use");
			}
		}

		public override void DisableGazeHints()
		{
			if (!_localPlayerIsBoarded)
			{
				Singleton<HintsManager>.Instance.RemoveHint("Interact");
			}
		}

		private Vector3 Flatten(Vector3 value)
		{
			value.y = 0f;
			return value;
		}

		public void OnSwimSFX()
		{
			PlaySwimStrokeSfx();
		}

		private void RefreshLoopSfxState()
		{
			if (_isSwimmingState && _localPlayerIsBoarded)
			{
				StartLoopSfx();
				StartSwimStrokeSfxSequence();
			}
			else
			{
				StopLoopSfx();
				StopSwimStrokeSfxSequence(resetSession: true);
			}
		}

		private void StartSwimStrokeSfxSequence()
		{
			if (!_swimStrokeSfxPlayedForSession && _swimStrokeSfxCoroutine == null)
			{
				_swimStrokeSfxPlayedForSession = true;
				_swimStrokeSfxCoroutine = StartCoroutine(PlaySwimStrokeSfxSequence());
			}
		}

		private IEnumerator PlaySwimStrokeSfxSequence()
		{
			float swimSfxTime = GetSwimSfxTime(_firstSwimSfxFrame);
			float secondDelay = Mathf.Max(0f, GetSwimSfxTime(_secondSwimSfxFrame) - swimSfxTime);
			yield return new WaitForSeconds(swimSfxTime);
			PlaySwimStrokeSfx();
			yield return new WaitForSeconds(secondDelay);
			PlaySwimStrokeSfx();
			_swimStrokeSfxCoroutine = null;
		}

		private float GetSwimSfxTime(int frame)
		{
			return (float)frame / Mathf.Max(1f, _swimSfxSampleRate);
		}

		private void PlaySwimStrokeSfx()
		{
			if (_isSwimmingState && _localPlayerIsBoarded && !(_swimSFX == null) && !(Singleton<SoundManager>.Instance == null))
			{
				Singleton<SoundManager>.Instance.PlaySFX(_swimSFX, base.transform.position, base.transform);
			}
		}

		private void StopSwimStrokeSfxSequence(bool resetSession)
		{
			if (_swimStrokeSfxCoroutine != null)
			{
				StopCoroutine(_swimStrokeSfxCoroutine);
				_swimStrokeSfxCoroutine = null;
			}
			if (resetSession)
			{
				_swimStrokeSfxPlayedForSession = false;
			}
		}

		private void StartLoopSfx()
		{
			if (!(_loopSFX == null) && !(Singleton<SoundManager>.Instance == null) && (_loopSfxHandle == null || !_loopSfxHandle.IsActive || !(_loopSfxSource != null) || !_loopSfxSource.isPlaying))
			{
				StopLoopSfx();
				_loopSfxHandle = Singleton<SoundManager>.Instance.PlayOwnedLoop(_loopSFX, base.transform.position, base.transform, $"Totti-{base.NetworkObjectId}");
				_loopSfxSource = _loopSfxHandle?.Source;
				if (_loopSfxSource == null)
				{
					_loopSfxHandle = null;
					return;
				}
				_loopSfxSource.loop = true;
				_loopSfxSource.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			}
		}

		private void StopLoopSfx()
		{
			if (Singleton<SoundManager>.Instance != null)
			{
				Singleton<SoundManager>.Instance.ReleaseOwnedLoop(_loopSfxHandle);
			}
			_loopSfxHandle = null;
			_loopSfxSource = null;
		}

		public void StartUnSeatHold(ulong clientId)
		{
			_holdCanvas.enabled = true;
			_holdTween = _holdImage.DOFillAmount(1f, _holdTime).SetEase(Ease.Linear).OnComplete(() =>
			{
				StopUnSeatHold();
				ServerRequestUnseatRpc(clientId);
			});
		}

		public void StopUnSeatHold()
		{
			_holdCanvas.enabled = false;
			_holdTween?.Kill();
			_holdImage.fillAmount = 0f;
		}

		[Rpc(SendTo.Server)]
		public void ServerRequestUnseatRpc(ulong clientId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2218004912u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, clientId);
				__endSendRpc(ref bufferWriter, 2218004912u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			NetworkObjectReference value2;
			NetworkObject networkObject;
			Interactor component;
			if (!_clientSeatMap.TryGetValue(clientId, out var value))
			{
				if (_logDebug)
				{
					UtilityMethods.Log($"[Totti] Unseat rejected. Client not seated. ClientId={clientId}");
				}
			}
			else if (!_seatOccupants.TryGetValue(value, out value2))
			{
				ClearSeatServer(value);
			}
			else if (!value2.TryGet(out networkObject))
			{
				ClearSeatServer(value);
			}
			else if (!networkObject.TryGetComponent<Interactor>(out component))
			{
				ClearSeatServer(value);
			}
			else
			{
				UnseatPlayerServer(component, value, value2);
			}
		}

		private void UnseatPlayerServer(Interactor interactor, int seatIndex, NetworkObjectReference occupantRef)
		{
			if (interactor == null || interactor.Player == null)
			{
				ClearSeatServer(seatIndex);
				return;
			}
			interactor.Player.ClientDeAssignParentConstraintRpc();
			NotifySeatDetachedClientRpc(seatIndex, occupantRef, applyEndOfRideBoost: false, Vector3.zero, 0f);
			ClearSeatServer(seatIndex);
			if (_logDebug)
			{
				UtilityMethods.Log($"[Totti] Player unseated. SeatIndex={seatIndex} ClientId={interactor.OwnerClientId}");
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3787872840u, __rpc_handler_3787872840, "ServerTryBoardRpc", RpcInvokePermission.Everyone);
			__registerRpc(3748195396u, __rpc_handler_3748195396, "NotifySwimmingStateClientRpc", RpcInvokePermission.Everyone);
			__registerRpc(3481227451u, __rpc_handler_3481227451, "NotifySeatAssignedClientRpc", RpcInvokePermission.Everyone);
			__registerRpc(777924158u, __rpc_handler_777924158, "NotifySeatDetachedClientRpc", RpcInvokePermission.Everyone);
			__registerRpc(2218004912u, __rpc_handler_2218004912, "ServerRequestUnseatRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3787872840(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((Totti)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Totti)target).ServerTryBoardRpc(value);
				((Totti)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3748195396(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((Totti)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Totti)target).NotifySwimmingStateClientRpc(value);
				((Totti)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3481227451(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				((Totti)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Totti)target).NotifySeatAssignedClientRpc(value, value2);
				((Totti)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_777924158(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out Vector3 value4);
				reader.ReadValueSafe(out float value5, default(FastBufferWriter.ForPrimitives));
				((Totti)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Totti)target).NotifySeatDetachedClientRpc(value, value2, value3, value4, value5);
				((Totti)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2218004912(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((Totti)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Totti)target).ServerRequestUnseatRpc(value);
				((Totti)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "Totti";
		}
	}
}
