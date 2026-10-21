using System;
using DG.Tweening;
using EPOOutline;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Interaction
{
	public class DoorBehaviour : NetworkBehaviour, IBasicInteractable, IToolActiveMouse1Interactable
	{
		private enum DoorMotionType
		{
			Rotation = 0,
			Position = 1
		}

		[Header("Door")]
		[SerializeField]
		private Transform _doorTransform;

		[SerializeField]
		private Animator _animator;

		[SerializeField]
		private string _openBoolName = "IsOpen";

		[SerializeField]
		private bool _useTransformMotion = true;

		[SerializeField]
		private DoorMotionType _motionType;

		[SerializeField]
		private Vector3 _closedLocalEuler;

		[SerializeField]
		private Vector3 _openLocalEuler = new Vector3(0f, 90f, 0f);

		[SerializeField]
		private Vector3 _closedLocalPosition;

		[SerializeField]
		private Vector3 _openLocalPosition;

		[SerializeField]
		[Min(0f)]
		private float _motionDuration = 0.35f;

		[SerializeField]
		private Ease _motionEase = Ease.OutCubic;

		[Header("Highlight")]
		[SerializeField]
		private Outlinable _outlinable;

		[Header("Audio")]
		[SerializeField]
		private AudioClipSO _openSfx;

		[SerializeField]
		private AudioClipSO _closeSfx;

		private readonly NetworkVariable<bool> _isOpen = new NetworkVariable<bool>(value: false);

		private bool _isGazing;

		public bool CanGaze => true;

		public bool CanInteractable => true;

		public bool IsOpen => _isOpen.Value;

		private void Awake()
		{
			if (_doorTransform == null)
			{
				_doorTransform = base.transform;
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<bool> isOpen = _isOpen;
			isOpen.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isOpen.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnOpenChanged));
			ApplyOpenState(_isOpen.Value, playSfx: false);
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<bool> isOpen = _isOpen;
			isOpen.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isOpen.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnOpenChanged));
			KillMotionTween();
			ClearHints();
			ClearHighlight();
			base.OnNetworkDespawn();
		}

		public void GazeEnter()
		{
			_isGazing = true;
			RefreshHints();
			SetHighlight();
		}

		public void GazeExit()
		{
			_isGazing = false;
			ClearHints();
			ClearHighlight();
		}

		public void TriggerInput(Interactor interactor, InputKind inputKind, InputActionPhase phase)
		{
			if (inputKind == InputKind.Mouse1 && phase == InputActionPhase.Started)
			{
				ServerToggleDoorRpc();
			}
		}

		public void TriggerCrossInput(Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase)
		{
			TriggerInput(interactor, inputKind, phase);
		}

		[Rpc(SendTo.Server)]
		public void ServerSetOpenRpc(bool isOpen)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1892900208u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in isOpen, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1892900208u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				SetOpenServer(isOpen);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerToggleDoorRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(4230733734u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 4230733734u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				SetOpenServer(!_isOpen.Value);
			}
		}

		private void SetOpenServer(bool isOpen)
		{
			if (!base.IsServer)
			{
				UtilityMethods.Log("[DoorBehaviour] SetOpenServer called on non-server.", LogType.Warning);
			}
			else if (_isOpen.Value != isOpen)
			{
				_isOpen.Value = isOpen;
			}
		}

		private void OnOpenChanged(bool wasOpen, bool isOpen)
		{
			ApplyOpenState(isOpen, playSfx: true);
		}

		private void ApplyOpenState(bool isOpen, bool playSfx)
		{
			if (_animator != null)
			{
				_animator.SetBool(_openBoolName, isOpen);
			}
			if (_useTransformMotion && _doorTransform != null)
			{
				ApplyTransformMotion(isOpen, playSfx);
			}
			if (playSfx)
			{
				PlayDoorSfx(isOpen);
			}
			RefreshHints();
		}

		private void RefreshHints()
		{
			if (_isGazing && !(Singleton<HintsManager>.Instance == null))
			{
				ClearHints();
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", _isOpen.Value ? "key_close" : "key_open");
			}
		}

		private void ClearHints()
		{
			Singleton<HintsManager>.Instance?.RemoveHint("LeftMouseMechanic");
		}

		private void SetHighlight()
		{
			if (!(_outlinable == null))
			{
				Singleton<HighlightManager>.Instance?.SetHighlight(_outlinable);
			}
		}

		private void ClearHighlight()
		{
			if (!(_outlinable == null))
			{
				HighlightManager instance = Singleton<HighlightManager>.Instance;
				if (instance != null)
				{
					instance.ClearCurrentHighlight(_outlinable);
				}
				else
				{
					_outlinable.enabled = false;
				}
			}
		}

		private void PlayDoorSfx(bool isOpen)
		{
			if (!(Singleton<SoundManager>.Instance == null))
			{
				AudioClipSO audioClipSO = (isOpen ? _openSfx : _closeSfx);
				if (!(audioClipSO == null))
				{
					Transform transform = ((_doorTransform != null) ? _doorTransform : base.transform);
					Singleton<SoundManager>.Instance.PlaySFX(audioClipSO, transform.position, transform);
				}
			}
		}

		private void ApplyTransformMotion(bool isOpen, bool animate)
		{
			KillMotionTween();
			switch (_motionType)
			{
			case DoorMotionType.Rotation:
			{
				Vector3 vector2 = (isOpen ? _openLocalEuler : _closedLocalEuler);
				if (!animate || _motionDuration <= 0f)
				{
					_doorTransform.localRotation = Quaternion.Euler(vector2);
				}
				else
				{
					_doorTransform.DOLocalRotate(vector2, _motionDuration).SetEase(_motionEase);
				}
				break;
			}
			case DoorMotionType.Position:
			{
				Vector3 vector = (isOpen ? _openLocalPosition : _closedLocalPosition);
				if (!animate || _motionDuration <= 0f)
				{
					_doorTransform.localPosition = vector;
				}
				else
				{
					_doorTransform.DOLocalMove(vector, _motionDuration).SetEase(_motionEase);
				}
				break;
			}
			}
		}

		private void KillMotionTween()
		{
			if (_doorTransform != null)
			{
				_doorTransform.DOKill();
			}
		}

		protected override void __initializeVariables()
		{
			if (_isOpen == null)
			{
				throw new Exception("DoorBehaviour._isOpen cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isOpen.Initialize(this);
			__nameNetworkVariable(_isOpen, "_isOpen");
			NetworkVariableFields.Add(_isOpen);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(1892900208u, __rpc_handler_1892900208, "ServerSetOpenRpc", RpcInvokePermission.Everyone);
			__registerRpc(4230733734u, __rpc_handler_4230733734, "ServerToggleDoorRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_1892900208(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((DoorBehaviour)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DoorBehaviour)target).ServerSetOpenRpc(value);
				((DoorBehaviour)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4230733734(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((DoorBehaviour)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DoorBehaviour)target).ServerToggleDoorRpc();
				((DoorBehaviour)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "DoorBehaviour";
		}
	}
}
