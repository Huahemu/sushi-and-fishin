using System;
using DG.Tweening;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.FishFridge
{
	public class FridgeLid : BaseInteractable, IHeldItemMouse1Interactable
	{
		[Header("References")]
		[SerializeField]
		private Transform _lidTransform;

		[SerializeField]
		private Fridge _fridge;

		[Header("Rotation")]
		[SerializeField]
		private Vector3 _closedLocalEuler;

		[SerializeField]
		private Vector3 _openLocalEuler = new Vector3(0f, -110f, 0f);

		[Header("Tween")]
		[SerializeField]
		private float _rotationDuration = 0.25f;

		[SerializeField]
		private Ease _rotationEase = Ease.OutCubic;

		private readonly NetworkVariable<bool> _isOpen = new NetworkVariable<bool>(value: false);

		private Tweener _rotationTween;

		public bool CanInteractable => true;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (_fridge != null)
			{
				_fridge.RegisterLid(this);
			}
			NetworkVariable<bool> isOpen = _isOpen;
			isOpen.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isOpen.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnLidStateChanged));
			ApplyRotationImmediate(_isOpen.Value);
			if (_fridge != null)
			{
				_fridge.SetFridgeFishStatus(_isOpen.Value && _fridge.IsPhysicallyUsable);
				if (_fridge.Collider != null)
				{
					_fridge.Collider.enabled = _isOpen.Value && _fridge.IsPhysicallyUsable;
				}
			}
		}

		public override void OnNetworkDespawn()
		{
			base.OnNetworkDespawn();
			NetworkVariable<bool> isOpen = _isOpen;
			isOpen.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isOpen.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnLidStateChanged));
			KillRotationTween();
		}

		public override void OnDestroy()
		{
			KillRotationTween();
			base.OnDestroy();
		}

		private void OnLidStateChanged(bool previousValue, bool newValue)
		{
			PlayRotation(newValue);
			bool flag = _fridge == null || _fridge.IsPhysicallyUsable;
			if (_fridge != null)
			{
				_fridge.SetFridgeFishStatus(newValue & flag);
				if (_fridge.Collider != null)
				{
					_fridge.Collider.enabled = newValue & flag;
				}
			}
			if (!newValue)
			{
				_fridge.PlayCloseSFX();
			}
			else
			{
				_fridge.PlayOpenSFX();
			}
		}

		private void ApplyRotationImmediate(bool isOpen)
		{
			if (!(_lidTransform == null))
			{
				KillRotationTween();
				_lidTransform.localRotation = Quaternion.Euler(isOpen ? _openLocalEuler : _closedLocalEuler);
			}
		}

		public void ServerResetForEndDay()
		{
			if (base.IsServer)
			{
				_isOpen.Value = false;
				ClientResetRuntimeStateRpc();
			}
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
				FastBufferWriter bufferWriter = __beginSendRpc(1461134098u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1461134098u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ApplyRotationImmediate(isOpen: false);
			if (_fridge != null)
			{
				_fridge.SetFridgeFishStatus(isOpen: false);
				if (_fridge.Collider != null)
				{
					_fridge.Collider.enabled = false;
				}
			}
		}

		private void PlayRotation(bool isOpen)
		{
			if (!(_lidTransform == null))
			{
				KillRotationTween();
				_rotationTween = _lidTransform.DOLocalRotate(isOpen ? _openLocalEuler : _closedLocalEuler, _rotationDuration).SetEase(_rotationEase).SetLink(base.gameObject);
			}
		}

		private void KillRotationTween()
		{
			if (_rotationTween != null && _rotationTween.IsActive())
			{
				_rotationTween.Kill();
			}
			_rotationTween = null;
		}

		[Rpc(SendTo.Server)]
		private void ServerToggleRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1314045781u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1314045781u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!(_fridge != null) || _fridge.IsPhysicallyUsable)
				{
					_isOpen.Value = !_isOpen.Value;
				}
			}
		}

		protected override void ClientMouse1(Interactor interactor, InputActionPhase phase)
		{
			base.ClientMouse1(interactor, phase);
			if (phase == InputActionPhase.Started && (!(_fridge != null) || _fridge.IsPhysicallyUsable))
			{
				if (_fridge != null && !_isOpen.Value)
				{
					_fridge.TryOpenTutorialForFirstDoorOpen(interactor);
				}
				ServerToggleRpc();
			}
		}

		protected override void ClientCrossMouse1(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			ClientMouse1(interactor, phase);
		}

		protected override void ClientInteract(Interactor interactor, InputActionPhase phase)
		{
			base.ClientInteract(interactor, phase);
			if (phase == InputActionPhase.Started && !(_fridge == null) && !_fridge.IsPhysicallyUsable)
			{
				_fridge.TryUnlockPhysicalStateAndOpenTutorial(interactor);
			}
		}

		protected override void ClientCrossInteract(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			ClientInteract(interactor, phase);
		}

		public override void EnableGazeHints()
		{
			if (_fridge != null && !_fridge.IsPhysicallyUsable)
			{
				Singleton<HintsManager>.Instance.AddHint("Interact", "key_repair");
				_fridge.ShowUnlockTooltipLocal();
			}
			else
			{
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", _isOpen.Value ? "key_close" : "key_open");
			}
		}

		public override void DisableGazeHints()
		{
			_fridge?.HideUnlockTooltipLocal();
			Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
		}

		protected override void __initializeVariables()
		{
			if (_isOpen == null)
			{
				throw new Exception("FridgeLid._isOpen cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isOpen.Initialize(this);
			__nameNetworkVariable(_isOpen, "_isOpen");
			NetworkVariableFields.Add(_isOpen);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(1461134098u, __rpc_handler_1461134098, "ClientResetRuntimeStateRpc", RpcInvokePermission.Everyone);
			__registerRpc(1314045781u, __rpc_handler_1314045781, "ServerToggleRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_1461134098(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((FridgeLid)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FridgeLid)target).ClientResetRuntimeStateRpc();
				((FridgeLid)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1314045781(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((FridgeLid)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FridgeLid)target).ServerToggleRpc();
				((FridgeLid)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "FridgeLid";
		}
	}
}
