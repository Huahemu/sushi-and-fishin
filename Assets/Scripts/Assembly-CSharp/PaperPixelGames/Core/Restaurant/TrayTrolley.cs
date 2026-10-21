using System.Collections.Generic;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Progress;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Restaurant
{
	public class TrayTrolley : BaseInteractable
	{
		[SerializeField]
		private List<TrolleySlot> _slots = new List<TrolleySlot>();

		[SerializeField]
		private PhysicalStateController _physicalStateController;

		[SerializeField]
		private PhysicalUnlockController _physicalUnlockController;

		[SerializeField]
		[Min(0f)]
		private float _placementReachPadding = 1.5f;

		[SerializeField]
		[Min(0.01f)]
		private float _slotOccupancyRadius = 0.12f;

		public PhysicalStateController PhysicalStateController => _physicalStateController;

		public bool IsPhysicallyUsable
		{
			get
			{
				if (!(_physicalStateController == null))
				{
					return _physicalStateController.IsPhysicallyUsable;
				}
				return true;
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (_physicalStateController != null)
			{
				_physicalStateController.OnPhysicallyUsableChanged += OnPhysicallyUsableChanged;
			}
			ApplySlotPhysicalAvailability(IsPhysicallyUsable);
			if (base.IsServer)
			{
				ServerSuppressInitialTrayTooltips();
				DelayHelper instance = Singleton<DelayHelper>.Instance;
				if (instance != null)
				{
					instance.DelayActionUntilEndOfFrame(ServerSuppressInitialTrayTooltips);
				}
			}
		}

		public override void OnNetworkDespawn()
		{
			if (_physicalStateController != null)
			{
				_physicalStateController.OnPhysicallyUsableChanged -= OnPhysicallyUsableChanged;
			}
			base.OnNetworkDespawn();
		}

		private void OnPhysicallyUsableChanged(bool usable)
		{
			ApplySlotPhysicalAvailability(usable);
			if (_inGaze)
			{
				RefreshGazeFeedback();
				if (usable)
				{
					_physicalUnlockController?.HideTooltipLocal();
				}
				else
				{
					_physicalUnlockController?.ShowTooltipLocal();
				}
			}
		}

		public void ApplyTrayPhysicalAvailability(Tray tray)
		{
			if (!(tray == null) && TryGetTraySlotIndex(tray, out var slotIndex))
			{
				TrolleySlot trolleySlot = _slots[slotIndex];
				tray.ClientApplyTrolleyAvailability(trolleySlot.SnapPoint, IsPhysicallyUsable);
			}
		}

		private void ApplySlotPhysicalAvailability(bool usable)
		{
			if (_slots == null)
			{
				return;
			}
			foreach (TrolleySlot slot in _slots)
			{
				if (!(slot?.Tray == null) && !(slot.SnapPoint == null))
				{
					slot.Tray.ClientApplyTrolleyAvailability(slot.SnapPoint, usable);
				}
			}
		}

		public override void EnableGazeHints()
		{
			if (!IsPhysicallyUsable)
			{
				Singleton<HintsManager>.Instance.AddHint("Interact", "key_repair");
				_physicalUnlockController?.ShowTooltipLocal();
			}
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
			_physicalUnlockController?.HideTooltipLocal();
		}

		protected override void ClientInteract(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && !IsPhysicallyUsable)
			{
				_physicalUnlockController?.TryUnlock(interactor);
			}
		}

		protected override void ClientCrossInteract(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			ClientInteract(interactor, phase);
		}

		public bool TryRequestTrayPlacement(Interactor interactor, Tray tray, FixedSnapPlacementZone snapZone)
		{
			if (interactor == null || tray == null || snapZone == null)
			{
				return false;
			}
			if (!CanPreviewTrayPlacement(tray, snapZone, out var slotIndex))
			{
				return false;
			}
			ServerPlaceTrayInSlotRpc(tray.NetworkObject.ToReference(), interactor.NetworkObject.ToReference(), slotIndex);
			return true;
		}

		public bool CanPreviewTrayPlacement(Tray tray, FixedSnapPlacementZone snapZone)
		{
			int slotIndex;
			return CanPreviewTrayPlacement(tray, snapZone, out slotIndex);
		}

		private bool CanPreviewTrayPlacement(Tray tray, FixedSnapPlacementZone snapZone, out int slotIndex)
		{
			slotIndex = -1;
			if (tray == null || snapZone == null || !IsPhysicallyUsable)
			{
				return false;
			}
			if (!TryGetSlotIndex(snapZone, out slotIndex))
			{
				return false;
			}
			TrolleySlot trolleySlot = _slots[slotIndex];
			if (trolleySlot == null || trolleySlot.SnapPoint == null || !trolleySlot.SnapPoint.CanSnap(tray))
			{
				return false;
			}
			return !IsSlotOccupied(trolleySlot, tray);
		}

		[Rpc(SendTo.Server)]
		private void ServerPlaceTrayInSlotRpc(NetworkObjectReference trayRef, NetworkObjectReference interactorRef, int slotIndex, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(47959557u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in trayRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				BytePacker.WriteValueBitPacked(bufferWriter, slotIndex);
				__endSendRpc(ref bufferWriter, 47959557u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsServer || !IsPhysicallyUsable)
			{
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!interactorRef.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != senderClientId)
			{
				UtilityMethods.Log("[TrayTrolley] Tray placement rejected. Invalid interactor.", LogType.Warning);
				return;
			}
			if (!trayRef.TryGetComponent<Tray>(out var component2) || component2.OwnerClientId != senderClientId || component2.CanHold.Value || component.CurrentHeld != component2)
			{
				UtilityMethods.Log("[TrayTrolley] Tray placement rejected. Tray is not held by the requesting interactor.", LogType.Warning);
				return;
			}
			if (slotIndex < 0 || slotIndex >= _slots.Count)
			{
				UtilityMethods.Log($"[TrayTrolley] Tray placement rejected. Invalid slot index:{slotIndex}", LogType.Warning);
				return;
			}
			TrolleySlot trolleySlot = _slots[slotIndex];
			if (trolleySlot == null || trolleySlot.SnapPoint == null || !trolleySlot.SnapPoint.CanSnap(component2))
			{
				UtilityMethods.Log("[TrayTrolley] Tray placement rejected. Slot does not accept this tray.", LogType.Warning);
				return;
			}
			if (IsSlotOccupied(trolleySlot, component2))
			{
				UtilityMethods.Log($"[TrayTrolley] Tray placement rejected. Slot is occupied. Slot:{slotIndex}", LogType.Warning);
				return;
			}
			float num = component.InteractRange + _placementReachPadding;
			if ((component.transform.position - trolleySlot.SnapPoint.transform.position).sqrMagnitude > num * num)
			{
				UtilityMethods.Log($"[TrayTrolley] Tray placement rejected. Slot is out of reach. Tray:{component2.name}, Slot:{slotIndex}, MaxDistance:{num:0.00}", LogType.Warning);
			}
			else if (!component2.ServerPlaceOnTrayTrolley(trolleySlot.SnapPoint.transform.position, trolleySlot.SnapPoint.transform.rotation.eulerAngles))
			{
				UtilityMethods.Log($"[TrayTrolley] Tray placement failed. Tray:{component2.name}, Slot:{slotIndex}", LogType.Warning);
			}
		}

		private bool TryGetSlotIndex(FixedSnapPlacementZone snapZone, out int slotIndex)
		{
			slotIndex = -1;
			if (_slots == null)
			{
				return false;
			}
			for (int i = 0; i < _slots.Count; i++)
			{
				TrolleySlot trolleySlot = _slots[i];
				if (trolleySlot != null && !(trolleySlot.SnapPoint == null) && !(trolleySlot.SnapPoint.transform != snapZone.transform))
				{
					slotIndex = i;
					return true;
				}
			}
			return false;
		}

		public bool TryGetTraySlotIndex(Tray tray, out int slotIndex)
		{
			slotIndex = -1;
			if (tray == null || _slots == null)
			{
				return false;
			}
			for (int i = 0; i < _slots.Count; i++)
			{
				if (!(_slots[i]?.Tray != tray))
				{
					slotIndex = i;
					return true;
				}
			}
			return false;
		}

		public bool TryGetTrayBySlotIndex(int slotIndex, out Tray tray)
		{
			tray = null;
			if (_slots == null || slotIndex < 0 || slotIndex >= _slots.Count)
			{
				return false;
			}
			tray = _slots[slotIndex]?.Tray;
			return tray != null;
		}

		public bool TryGetTrayHomeSnapPoint(Tray tray, out SnapPoint snapPoint)
		{
			snapPoint = null;
			if (!TryGetTraySlotIndex(tray, out var slotIndex))
			{
				return false;
			}
			snapPoint = _slots[slotIndex]?.SnapPoint;
			return snapPoint != null;
		}

		private bool IsSlotOccupied(TrolleySlot targetSlot, Tray trayBeingPlaced)
		{
			float num = _slotOccupancyRadius * _slotOccupancyRadius;
			Vector3 position = targetSlot.SnapPoint.transform.position;
			foreach (BaseHoldable stackWorldItem in BaseHoldable.StackWorldItems)
			{
				if (stackWorldItem is Tray { IsSpawned: not false } tray && !(tray.NetworkManager != base.NetworkManager) && !(tray == trayBeingPlaced) && tray.CanHold.Value && !tray.IsStaffCarried && !tray.IsPlayerInventoryOwnedForPersistence && (tray.transform.position - position).sqrMagnitude <= num)
				{
					return true;
				}
			}
			return false;
		}

		[Rpc(SendTo.Server)]
		public void ServerResetTrayTransformsRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(2574318166u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2574318166u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ResetTrayTransformsServer();
				ClientApplyTrayTransformsRpc();
			}
		}

		private void ResetTrayTransformsServer()
		{
			if (_slots == null)
			{
				return;
			}
			foreach (TrolleySlot slot in _slots)
			{
				TryApplyTrayTransform(slot);
			}
		}

		private void ServerSuppressInitialTrayTooltips()
		{
			if (!base.IsServer || _slots == null)
			{
				return;
			}
			foreach (TrolleySlot slot in _slots)
			{
				Tray tray = slot?.Tray;
				if (!(tray == null) && !(tray.NetworkObject == null) && tray.NetworkObject.IsSpawned && tray.CanHold.Value)
				{
					tray.ServerSetPlacementTooltipSuppressed(suppressed: true);
				}
			}
		}

		private bool TryApplyTrayTransform(TrolleySlot slot)
		{
			if (slot == null)
			{
				return false;
			}
			if (slot.Tray == null || slot.SnapPoint == null)
			{
				return false;
			}
			if (!slot.Tray.NetworkObject.IsSpawned)
			{
				return false;
			}
			if (!slot.Tray.CanHold.Value)
			{
				return false;
			}
			slot.Tray.transform.SetPositionAndRotation(slot.SnapPoint.transform.position, slot.SnapPoint.transform.rotation);
			slot.Tray.ServerSetPlacementTooltipSuppressed(suppressed: true);
			return true;
		}

		[Rpc(SendTo.NotServer)]
		private void ClientApplyTrayTransformsRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1472051309u, rpcParams, attributeParams, SendTo.NotServer, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1472051309u, rpcParams, attributeParams, SendTo.NotServer, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_slots == null)
			{
				return;
			}
			foreach (TrolleySlot slot in _slots)
			{
				if (slot != null && !(slot.Tray == null) && !(slot.SnapPoint == null) && slot.Tray.CanHold.Value)
				{
					slot.Tray.transform.SetPositionAndRotation(slot.SnapPoint.transform.position, slot.SnapPoint.transform.rotation);
				}
			}
		}

		public bool ServerStaffAtHome(Tray tray)
		{
			if (tray == null || !tray.IsSpawned || !tray.CanHold.Value || tray.IsStaffCarried || tray.IsPlayerInventoryOwnedForPersistence || _slots == null)
			{
				return false;
			}
			float num = _slotOccupancyRadius * _slotOccupancyRadius;
			foreach (TrolleySlot slot in _slots)
			{
				if (slot?.SnapPoint != null && slot.SnapPoint.CanSnap(tray) && (tray.transform.position - slot.SnapPoint.transform.position).sqrMagnitude <= num)
				{
					return true;
				}
			}
			return false;
		}

		private bool TryFindStaffReturnPoint(Tray tray, out Transform point)
		{
			point = null;
			if (!base.IsServer || !base.IsSpawned || !IsPhysicallyUsable || tray == null || tray.IsDirty.Value || _slots == null)
			{
				return false;
			}
			if (TryGetTraySlotIndex(tray, out var slotIndex) && Free(_slots[slotIndex]))
			{
				point = _slots[slotIndex].SnapPoint.transform;
				return true;
			}
			foreach (TrolleySlot slot in _slots)
			{
				if (Free(slot))
				{
					point = slot.SnapPoint.transform;
					return true;
				}
			}
			return false;
			bool Free(TrolleySlot slot)
			{
				if (slot?.SnapPoint != null && slot.SnapPoint.CanSnap(tray))
				{
					return !IsSlotOccupied(slot, tray);
				}
				return false;
			}
		}

		public bool ServerStaffCanReturn(Tray tray)
		{
			Transform point;
			return TryFindStaffReturnPoint(tray, out point);
		}

		public bool ServerStaffReturn(int employeeId, Tray tray)
		{
			if (tray == null || !tray.ServerStaffCarriedBy(employeeId) || !TryFindStaffReturnPoint(tray, out var point))
			{
				return false;
			}
			return tray.ServerStaffPlace(employeeId, point.position, point.rotation, onTable: false);
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(47959557u, __rpc_handler_47959557, "ServerPlaceTrayInSlotRpc", RpcInvokePermission.Everyone);
			__registerRpc(2574318166u, __rpc_handler_2574318166, "ServerResetTrayTransformsRpc", RpcInvokePermission.Everyone);
			__registerRpc(1472051309u, __rpc_handler_1472051309, "ClientApplyTrayTransformsRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_47959557(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				ByteUnpacker.ReadValueBitPacked(reader, out int value3);
				RpcParams ext = rpcParams.Ext;
				((TrayTrolley)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((TrayTrolley)target).ServerPlaceTrayInSlotRpc(value, value2, value3, ext);
				((TrayTrolley)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2574318166(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((TrayTrolley)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((TrayTrolley)target).ServerResetTrayTransformsRpc();
				((TrayTrolley)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1472051309(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((TrayTrolley)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((TrayTrolley)target).ClientApplyTrayTransformsRpc();
				((TrayTrolley)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "TrayTrolley";
		}
	}
}
