using System;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Interaction
{
	public abstract class BaseUsable : BaseInteractable
	{
		[Header("BaseUsable")]
		[SerializeField]
		private bool _disableCam;

		[SerializeField]
		private CinemachineCamera _usableCam;

		[SerializeField]
		private Transform _usePoint;

		[SerializeField]
		private Transform _enterLookPoint;

		[SerializeField]
		private Transform _exitLookPoint;

		public NetworkVariable<bool> CanUse = new NetworkVariable<bool>(value: true);

		public NetworkVariable<bool> InUse = new NetworkVariable<bool>(value: false);

		protected Interactor _currentInteractor;

		public Interactor CurrentInteractor => _currentInteractor;

		public CinemachineCamera UsableCam => _usableCam;

		protected virtual bool UsesBaseExitInput => true;

		protected virtual bool AppliesBaseUsePose => true;

		protected override void Awake()
		{
			base.Awake();
			if (!_disableCam && _usableCam != null)
			{
				_usableCam.enabled = false;
			}
			if (_enterLookPoint == null)
			{
				_enterLookPoint = base.transform;
			}
			if (_exitLookPoint == null)
			{
				_exitLookPoint = base.transform;
			}
		}

		public virtual bool IsAvailable()
		{
			if (CanUse.Value)
			{
				return !InUse.Value;
			}
			return false;
		}

		public override void EnableGazeHints()
		{
			Singleton<HintsManager>.Instance.AddHint("Interact", "key_use");
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
		}

		public virtual void EnableUseHints()
		{
			Singleton<HintsManager>.Instance.AddHint("Exit", "key_close");
		}

		public virtual void DisableUseHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("Exit");
		}

		protected override void ClientInteract(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				uint requestSequence;
				if (!IsAvailable())
				{
					UtilityMethods.Log("[BaseUsable] can't use or already in use!");
				}
				else if (!(interactor == null) && !(interactor.Player == null) && interactor.Player.CanStartControlAction(PlayerControlAction.EnterUsable) && interactor.Player.TryDisableActiveToolForModeChange() && interactor.TryBeginUseRequest(out requestSequence))
				{
					interactor.ServerUseEnterRpc(base.NetworkObject.ToReference(), requestSequence);
				}
			}
		}

		protected virtual bool CanCrossUse(Interactor interactor, BaseHoldable held)
		{
			return true;
		}

		protected override void ClientCrossInteract(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && CanCrossUse(interactor, held))
			{
				ClientInteract(interactor, phase);
			}
		}

		public virtual void ServerUseEnter(Interactor interactor)
		{
			base.NetworkObject.DontDestroyWithOwner = true;
			InUse.Value = true;
			if (AppliesBaseUsePose)
			{
				ApplyUsePoseLocal(interactor);
				ClientApplyUsePoseRpc(interactor.NetworkObject.ToReference(), _usePoint.position, GetUseLookRotation(interactor));
			}
		}

		public virtual void ServerUseExit(Interactor interactor)
		{
			InUse.Value = false;
		}

		public virtual void ServerUseInterrupted(Interactor interactor)
		{
			ServerUseExit(interactor);
		}

		public virtual void ClientUseEnter(Interactor interactor)
		{
			EnableUseHints();
			SetCamera(status: true);
			_currentInteractor = interactor;
			_currentInteractor.Player.StopHarpoonedPullLocal();
			_currentInteractor.SetInteractorStatus(status: false, exitCurrent: true);
			_currentInteractor.Player.PauseCharacter();
			if (AppliesBaseUsePose)
			{
				_currentInteractor.Player.TeleportPlayer(_usePoint.position, updateGround: false);
				_currentInteractor.Player.transform.rotation = GetUseLookRotation(_currentInteractor);
			}
			_currentInteractor.Player.ResetGroundLocomotionAnimatorForUseMode();
			_currentInteractor.Player.FirstPersonCharacter.LookAtInstant(_enterLookPoint.position);
			RegisterBaseExitInput();
		}

		public virtual void ClientUseExit(Interactor interactor)
		{
			UnregisterBaseExitInput();
			DisableUseHints();
			SetCamera(status: false);
			Interactor interactor2 = ((_currentInteractor != null) ? _currentInteractor : interactor);
			if (interactor2 != null)
			{
				interactor2.SetInteractorStatus(status: true);
				if (interactor2.Player != null)
				{
					interactor2.Player.ResumeCharacter();
				}
			}
			_currentInteractor = null;
		}

		protected void RequestUseExit()
		{
			if (!(_currentInteractor == null))
			{
				_currentInteractor.ServerUseExitRpc(base.NetworkObject.ToReference());
			}
		}

		private void RegisterBaseExitInput()
		{
			if (UsesBaseExitInput && !(Singleton<InputManager>.Instance == null) && !(Singleton<InputManager>.Instance.InputHandler == null))
			{
				Singleton<InputManager>.Instance.InputHandler.Exit -= OnBaseExitInput;
				Singleton<InputManager>.Instance.InputHandler.Exit += OnBaseExitInput;
			}
		}

		private void UnregisterBaseExitInput()
		{
			if (!(Singleton<InputManager>.Instance == null) && !(Singleton<InputManager>.Instance.InputHandler == null))
			{
				Singleton<InputManager>.Instance.InputHandler.Exit -= OnBaseExitInput;
			}
		}

		private void OnBaseExitInput(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				RequestUseExit();
			}
		}

		protected void SetCamera(bool status)
		{
			if (!_disableCam && !(_usableCam == null))
			{
				_usableCam.enabled = status;
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientApplyUsePoseRpc(NetworkObjectReference interactorRef, Vector3 position, Quaternion rotation)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3044626610u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in position);
				bufferWriter.WriteValueSafe(in rotation);
				__endSendRpc(ref bufferWriter, 3044626610u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (interactorRef.TryGetComponent<Interactor>(out var component))
				{
					ApplyUsePoseLocal(component, position, rotation);
				}
			}
		}

		private void ApplyUsePoseLocal(Interactor interactor)
		{
			ApplyUsePoseLocal(interactor, _usePoint.position, GetUseLookRotation(interactor));
		}

		private void ApplyUsePoseLocal(Interactor interactor, Vector3 position, Quaternion rotation)
		{
			if (!(interactor == null) && !(interactor.Player == null))
			{
				interactor.Player.transform.SetPositionAndRotation(position, rotation);
			}
		}

		private Quaternion GetUseLookRotation(Interactor interactor)
		{
			if (interactor == null || interactor.Player == null || _enterLookPoint == null)
			{
				return Quaternion.identity;
			}
			Vector3 vector = ((_usePoint != null) ? _usePoint.position : interactor.Player.transform.position);
			Vector3 vector2 = _enterLookPoint.position - vector;
			Vector3 vector3 = new Vector3(vector2.x, 0f, vector2.z);
			if (vector3.sqrMagnitude < 0.0001f)
			{
				return interactor.Player.transform.rotation;
			}
			return Quaternion.LookRotation(vector3.normalized, Vector3.up);
		}

		protected override void __initializeVariables()
		{
			if (CanUse == null)
			{
				throw new Exception("BaseUsable.CanUse cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			CanUse.Initialize(this);
			__nameNetworkVariable(CanUse, "CanUse");
			NetworkVariableFields.Add(CanUse);
			if (InUse == null)
			{
				throw new Exception("BaseUsable.InUse cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			InUse.Initialize(this);
			__nameNetworkVariable(InUse, "InUse");
			NetworkVariableFields.Add(InUse);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3044626610u, __rpc_handler_3044626610, "ClientApplyUsePoseRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3044626610(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out Quaternion value3);
				((BaseUsable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseUsable)target).ClientApplyUsePoseRpc(value, value2, value3);
				((BaseUsable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "BaseUsable";
		}
	}
}
