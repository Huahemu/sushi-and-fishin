using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Interaction.ToolTip;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Kitchen.Wholasale
{
	// Small per-instance payload for the existing QuickSlot CustomData field.
	internal static class WholesaleQuickSlotCountCodec
	{
		private const string Prefix = "wholesale-count/v1/";
		public static string Encode(byte quantity)
		{
			return Prefix + quantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
		}
		public static bool TryDecode(string data, out byte quantity)
		{
			quantity = 0;
			return !string.IsNullOrEmpty(data) && data.StartsWith(Prefix, StringComparison.Ordinal) &&
				byte.TryParse(data.Substring(Prefix.Length), System.Globalization.NumberStyles.None,
					System.Globalization.CultureInfo.InvariantCulture, out quantity);
		}
	}

	public class WholesaleEntity : BaseHoldable, ISaveable<WholesaleData>
	{
		public enum PackNotifyId : byte
		{
			None = 0,
			PackEmpty = 1,
			PackFullCancelled = 2,
			IdMismatchCancelled = 3
		}

		public static class PackNotifyText
		{
			public static (string header, string message) Resolve(PackNotifyId id)
			{
				return id switch
				{
					PackNotifyId.PackEmpty => (header: "Pack", message: "The pack is empty."), 
					PackNotifyId.PackFullCancelled => (header: "Pack", message: "The pack is full. Action cancelled."), 
					PackNotifyId.IdMismatchCancelled => (header: "Pack", message: "Item ID mismatch. Action cancelled."), 
					_ => (header: "", message: ""), 
				};
			}
		}

		private readonly NetworkVariable<byte> _currentEntityCount = new NetworkVariable<byte>(0);

		[SerializeField]
		private Transform _entityVisualRoot;

		[SerializeField]
		private bool _updateEntityVisuals = true;

		private readonly List<GameObject> _entityVisuals = new List<GameObject>();

		private bool _hasLoadedData;

		public WholesaleSO WholesaleSO => _itemSO as WholesaleSO;

		public byte CurrentCount => _currentEntityCount.Value;

		public override Sprite QuickSlotDisplaySprite
		{
			get
			{
				if (CurrentCount != 0 || !(WholesaleSO != null) || !(WholesaleSO.EmptySprite != null))
				{
					return base.QuickSlotDisplaySprite;
				}
				return WholesaleSO.EmptySprite;
			}
		}

		public int ServerTransferStackContentsTo(WholesaleEntity target)
		{
			if (!base.IsServer || !base.IsSpawned || target == null || !target.IsServer || !target.IsSpawned || target == this || target.WholesaleSO != WholesaleSO || base.IsStaffCarried || base.IsPlayerInventoryOwnedForPersistence || !CanHold.Value)
			{
				return 0;
			}
			int num = Mathf.Min(CurrentCount, WholesaleSO.Quantity - target.CurrentCount);
			if (num <= 0)
			{
				return 0;
			}
			_currentEntityCount.Value -= (byte)num;
			target._currentEntityCount.Value += (byte)num;
			return num;
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<byte> currentEntityCount = _currentEntityCount;
			currentEntityCount.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(currentEntityCount.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnCurrentEntityCountChanged));
			PopulateEntityVisuals();
			if (base.IsServer)
			{
				if (!_hasLoadedData)
				{
					_currentEntityCount.Value = WholesaleSO.Quantity;
				}
				if (Singleton<GameProgressManager>.Instance != null && Singleton<GameProgressManager>.Instance.RestaurantManager != null)
				{
					Singleton<GameProgressManager>.Instance.RestaurantManager.RegisterWholesaleData(this);
				}
				else
				{
					UtilityMethods.Log("<color=yellow>[WholesaleEntity] RestaurantManager is missing. RegisterWholesaleData skipped.</color>");
				}
			}
			RebuildVisuals(_currentEntityCount.Value);
		}

		public override void OnNetworkDespawn()
		{
			if (base.IsServer && Singleton<GameProgressManager>.Instance != null && Singleton<GameProgressManager>.Instance.RestaurantManager != null)
			{
				Singleton<GameProgressManager>.Instance.RestaurantManager.DeRegisterWholesaleData(this);
			}
			NetworkVariable<byte> currentEntityCount = _currentEntityCount;
			currentEntityCount.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(currentEntityCount.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnCurrentEntityCountChanged));
			base.OnNetworkDespawn();
		}

		protected override void ClientCrossMouse1(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (phase != InputActionPhase.Started)
			{
				return;
			}
			if (held is Tray tray && IsSupportedTrayPackOutput())
			{
				if (HasMatchingOutputOnTray(tray))
				{
					ServerTryReturnOneFromTrayRpc(interactor.NetworkObject.ToReference(), tray.NetworkObject.ToReference());
				}
				else
				{
					base.ClientCrossMouse1(interactor, held, phase);
				}
			}
			else if (!TryRequestReturnHeldItemToPack(interactor, held))
			{
				base.ClientCrossMouse1(interactor, held, phase);
			}
		}

		protected override void ClientMouse2(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && !(interactor == null) && !(interactor.NetworkObject == null) && !(WholesaleSO?.OutputItem == null))
			{
				if (Singleton<QuickSlotHandler>.Instance == null || !Singleton<QuickSlotHandler>.Instance.CanHold(WholesaleSO.OutputItem))
				{
					Singleton<NotifyManager>.Instance?.ShowToast("Slots are full!", NotificationState.Error);
				}
				else
				{
					ServerTryTakeInHandRpc(interactor.NetworkObject.ToReference());
				}
			}
		}

		protected override void ClientCrossMouse2(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				if (held is Tray tray && IsSupportedTrayPackOutput())
				{
					ServerTryTakeOneToTrayRpc(interactor.NetworkObject.ToReference(), tray.NetworkObject.ToReference());
				}
				else
				{
					ClientMouse2(interactor, phase);
				}
			}
		}

		private bool EnsureEntityVisualsPopulated()
		{
			if (_entityVisuals.Count > 0)
			{
				return true;
			}
			PopulateEntityVisuals();
			return _entityVisuals.Count > 0;
		}

		private bool TryRequestReturnHeldItemToPack(Interactor interactor, BaseHoldable held)
		{
			if (interactor == null || interactor.NetworkObject == null)
			{
				return false;
			}
			if (held == null || held.NetworkObject == null || held.ItemSO == null)
			{
				return false;
			}
			if (!IsSupportedReturnHeldItem(held))
			{
				return false;
			}
			ServerTryPutBackIntoPackRpc(interactor.NetworkObject.ToReference(), held.NetworkObject.ToReference());
			return true;
		}

		private static bool IsSupportedReturnHeldItem(BaseHoldable held)
		{
			if (held == null || held.ItemSO == null)
			{
				return false;
			}
			if (!(held.ItemSO is IngredientSO) && !(held.ItemSO is BeverageSO) && !(held.ItemSO is ProcessedIngredientSO) && !(held.ItemSO is SideDishSO))
			{
				return held is ServedDish;
			}
			return true;
		}

		private bool IsSupportedPackOutput()
		{
			if (WholesaleSO == null || WholesaleSO.OutputItem == null)
			{
				return false;
			}
			if (!(WholesaleSO.OutputItem is IngredientSO) && !(WholesaleSO.OutputItem is BeverageSO) && !(WholesaleSO.OutputItem is ProcessedIngredientSO))
			{
				return WholesaleSO.OutputItem is SideDishSO;
			}
			return true;
		}

		private bool IsSupportedTrayPackOutput()
		{
			if (WholesaleSO != null)
			{
				if (!(WholesaleSO.OutputItem is SideDishSO))
				{
					return WholesaleSO.OutputItem is BeverageSO;
				}
				return true;
			}
			return false;
		}

		private bool HasMatchingOutputOnTray(Tray tray)
		{
			if (tray == null || WholesaleSO?.OutputItem == null)
			{
				return false;
			}
			ServedDish beverage;
			if (WholesaleSO.OutputItem is SideDishSO)
			{
				if (!tray.TryGetSideDish(out beverage))
				{
					return false;
				}
			}
			else
			{
				if (!(WholesaleSO.OutputItem is BeverageSO))
				{
					return false;
				}
				if (!tray.TryGetBeverageDish(out beverage))
				{
					return false;
				}
			}
			if (beverage != null && beverage.ItemSO != null)
			{
				return beverage.ItemSO.Id == WholesaleSO.OutputItem.Id;
			}
			return false;
		}

		private bool TryValidateReturnRequest(BaseHoldable held, out string packId, out PackNotifyId rejectionId)
		{
			packId = string.Empty;
			rejectionId = PackNotifyId.None;
			if (!IsSupportedReturnHeldItem(held))
			{
				return false;
			}
			if (WholesaleSO == null)
			{
				UtilityMethods.Log("<color=yellow>[WholesaleEntity] Return failed. WholesaleSO is null.</color>");
				return false;
			}
			if (WholesaleSO.OutputItem == null)
			{
				UtilityMethods.Log("<color=yellow>[WholesaleEntity] Return failed. OutputItem is null.</color>");
				return false;
			}
			if (!IsSupportedPackOutput())
			{
				UtilityMethods.Log("<color=yellow>[WholesaleEntity] Return failed. OutputItem type is not supported.</color>");
				return false;
			}
			string id = held.ItemSO.Id;
			packId = WholesaleSO.OutputItem.Id;
			if (id != packId)
			{
				rejectionId = PackNotifyId.IdMismatchCancelled;
				return false;
			}
			if (_currentEntityCount.Value >= WholesaleSO.Quantity)
			{
				rejectionId = PackNotifyId.PackFullCancelled;
				return false;
			}
			return true;
		}

		private void PopulateEntityVisuals()
		{
			_entityVisuals.Clear();
			if (_entityVisualRoot == null)
			{
				UtilityMethods.Log("[WholesaleEntity] _entityVisualRoot is not assigned.");
				return;
			}
			if (_entityVisualRoot.childCount == 0)
			{
				UtilityMethods.Log("[WholesaleEntity] _entityVisualRoot has no children.");
				return;
			}
			for (int i = 0; i < _entityVisualRoot.childCount; i++)
			{
				Transform child = _entityVisualRoot.GetChild(i);
				if (child == null)
				{
					UtilityMethods.Log($"[WholesaleEntity] Child at index {i} is null.");
				}
				else
				{
					_entityVisuals.Add(child.gameObject);
				}
			}
		}

		private void OnCurrentEntityCountChanged(byte oldValue, byte newValue)
		{
			HandleVisualUpdate(newValue);
		}

		protected virtual void HandleVisualUpdate(byte newValue)
		{
			if (_updateEntityVisuals)
			{
				RebuildEntity(newValue);
			}
			RebuildVisuals(newValue);
			Singleton<QuickSlotHandler>.Instance?.RefreshDisplayForItem(this);
			GazeRefresh();
		}

		private void RebuildEntity(byte newValue)
		{
			TryApplyVisualCount(_tpsShadowObject, newValue, "TPS");
			TryApplyVisualCount(_fpsShadowObject, newValue, "FPS");
		}

		public virtual void ApplyStaffHeldContents(GameObject graphic, byte count)
		{
			if (_updateEntityVisuals)
			{
				TryApplyVisualCount(graphic, count, "Staff TPS");
			}
		}

		private static void TryApplyVisualCount(GameObject shadowObject, byte newValue, string label)
		{
			if (shadowObject == null)
			{
				return;
			}
			Transform transform = shadowObject.transform;
			if (transform == null)
			{
				return;
			}
			if (transform.childCount <= 0)
			{
				UtilityMethods.Log("[WholesaleEntity] " + label + " shadow has no child root.");
				return;
			}
			Transform child = transform.GetChild(0);
			if (child == null)
			{
				UtilityMethods.Log("[WholesaleEntity] " + label + " root is null.");
				return;
			}
			int childCount = child.childCount;
			if (childCount <= 0)
			{
				return;
			}
			for (int i = 0; i < childCount; i++)
			{
				Transform child2 = child.GetChild(i);
				if (child2 == null)
				{
					UtilityMethods.Log($"[WholesaleEntity] {label} visual index {i} is null.");
				}
				else
				{
					child2.gameObject.SetActive(ShouldVisualStayActive(i, childCount, newValue));
				}
			}
		}

		protected override void ClientHoldEveryone()
		{
			base.ClientHoldEveryone();
			RebuildEntity(_currentEntityCount.Value);
		}

		private void RebuildVisuals(byte newValue)
		{
			if (_entityVisuals == null)
			{
				UtilityMethods.Log("[WholesaleEntity] _entityVisuals is null.");
				return;
			}
			if (_entityVisuals.Count == 0)
			{
				UtilityMethods.Log("[WholesaleEntity] _entityVisuals is empty.");
				return;
			}
			if (!EnsureEntityVisualsPopulated())
			{
				UtilityMethods.Log("[WholesaleEntity] World visual cache is empty. Object: " + base.name + ", Item: " + base.ItemSO?.Id);
				return;
			}
			for (int i = 0; i < _entityVisuals.Count; i++)
			{
				GameObject gameObject = _entityVisuals[i];
				if (gameObject == null)
				{
					UtilityMethods.Log($"[WholesaleEntity] Visual at index {i} is null.");
				}
				else
				{
					gameObject.SetActive(ShouldVisualStayActive(i, _entityVisuals.Count, newValue));
				}
			}
		}

		private static bool ShouldVisualStayActive(int index, int totalCount, byte currentCount)
		{
			int num = Mathf.Clamp(currentCount, 0, totalCount);
			int num2 = totalCount - num;
			return index >= num2;
		}

		[Rpc(SendTo.Server)]
		private void ServerTryTakeOneToTrayRpc(NetworkObjectReference interactorRef, NetworkObjectReference trayRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3736166368u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in trayRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3736166368u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			RpcParams rpcParams3 = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
			Interactor component;
			Tray component2;
			NetworkObject instance;
			ServedDish component3;
			if (_currentEntityCount.Value <= 0)
			{
				ClientShowPackNotifyRpc(PackNotifyId.PackEmpty, rpcParams3);
			}
			else if (!interactorRef.TryGetComponent<Interactor>(out component) || component.OwnerClientId != senderClientId || !trayRef.TryGetComponent<Tray>(out component2) || component2.OwnerClientId != senderClientId || component.CurrentHeld != component2 || component2.CanHold.Value || !ServerCanTakeOutput(component) || !IsSupportedTrayPackOutput() || WholesaleSO.OutputItem?.Prefab == null)
			{
				ClientShowPackNotifyRpc(PackNotifyId.IdMismatchCancelled, rpcParams3);
			}
			else if (!WholesaleSO.OutputItem.Prefab.TryInstantiateAndSpawn(out instance, Vector3.zero, Quaternion.identity) || instance == null || !instance.TryGetComponent<ServedDish>(out component3))
			{
				if (instance != null && instance.IsSpawned)
				{
					instance.Despawn();
				}
			}
			else if (!component2.ServerTryPlaceWorldDishOnHeldTray(component3, senderClientId))
			{
				instance.Despawn();
				ClientShowPackNotifyRpc(PackNotifyId.PackFullCancelled, rpcParams3);
			}
			else
			{
				Consume();
				ServerAddOutputStock(WholesaleSO.OutputItem, 1);
				ServerCompleteTakeOneQuest(WholesaleSO.OutputItem);
				ClientPlayAudioRpc(rpcParams3);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTryReturnOneFromTrayRpc(NetworkObjectReference interactorRef, NetworkObjectReference trayRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3269489278u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in trayRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3269489278u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			RpcParams rpcParams3 = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
			if (!interactorRef.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != senderClientId || !trayRef.TryGetComponent<Tray>(out var component2) || component2.OwnerClientId != senderClientId || component.CurrentHeld != component2 || component2.CanHold.Value || !IsSupportedTrayPackOutput())
			{
				ClientShowPackNotifyRpc(PackNotifyId.IdMismatchCancelled, rpcParams3);
				return;
			}
			if (_currentEntityCount.Value >= WholesaleSO.Quantity)
			{
				ClientShowPackNotifyRpc(PackNotifyId.PackFullCancelled, rpcParams3);
				return;
			}
			string id = WholesaleSO.OutputItem.Id;
			if (!component2.ServerTryRemoveOptionalDishForWholesaleReturn(id, out var dish))
			{
				ClientShowPackNotifyRpc(PackNotifyId.IdMismatchCancelled, rpcParams3);
				return;
			}
			ServerRemoveOutputStock(dish.ItemSO, 1);
			ServerAddWholesaleStock(1);
			_currentEntityCount.Value++;
			dish.Destroy();
			component2.ServerRefreshHeldTrayContentsGraphics();
			ClientPlayAudioRpc(rpcParams3);
		}

		[Rpc(SendTo.Server)]
		private void ServerTryPutBackIntoPackRpc(NetworkObjectReference interactorRef, NetworkObjectReference heldRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1721005426u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in heldRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 1721005426u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			RpcParams rpcParams3 = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
			NetworkObject networkObject2;
			BaseHoldable component2;
			if (!interactorRef.TryGet(out var networkObject) || !networkObject.TryGetComponent<Interactor>(out var component))
			{
				UtilityMethods.Log("<color=yellow>[WholesaleEntity] Put back failed. Interactor reference is invalid.</color>");
			}
			else if (component.OwnerClientId != senderClientId)
			{
				UtilityMethods.Log("<color=yellow>[WholesaleEntity] Put back failed. Sender does not own the interactor.</color>");
			}
			else if (!heldRef.TryGet(out networkObject2) || !networkObject2.IsSpawned || !networkObject2.TryGetComponent<BaseHoldable>(out component2))
			{
				UtilityMethods.Log("<color=yellow>[WholesaleEntity] Put back failed. Held reference is invalid.</color>");
			}
			else
			{
				if (!ServerCanTakeOutput(component))
				{
					return;
				}
				if (!TryValidateReturnRequest(component2, out var _, out var rejectionId))
				{
					if (rejectionId != PackNotifyId.None)
					{
						ClientShowPackNotifyRpc(rejectionId, rpcParams3);
					}
					return;
				}
				if (Singleton<GameProgressManager>.Instance == null || Singleton<GameProgressManager>.Instance.RestaurantManager == null)
				{
					UtilityMethods.Log("<color=yellow>[WholesaleEntity] Put back failed. RestaurantManager is missing.</color>");
					return;
				}
				if (component2.OwnerClientId != senderClientId || component.CurrentHeld != component2 || component2.CanHold.Value || component2.IsStaffCarried)
				{
					UtilityMethods.Log("<color=yellow>[WholesaleEntity] Put back failed. Item is not currently held by the sender.</color>");
					ClientShowPackNotifyRpc(PackNotifyId.IdMismatchCancelled, rpcParams3);
					return;
				}
				ServerRemoveOutputStock(component2.ItemSO, 1);
				ServerAddWholesaleStock(1);
				_currentEntityCount.Value++;
				ClientRemoveAcceptedHeldItemRpc(heldRef, rpcParams3);
				networkObject2.Despawn(destroy: false);
				ClientPlayAudioRpc(rpcParams3);
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientRemoveAcceptedHeldItemRpc(NetworkObjectReference acceptedHeldRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1456666926u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in acceptedHeldRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 1456666926u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!(Singleton<QuickSlotHandler>.Instance == null) && acceptedHeldRef.TryGet(out var networkObject) && networkObject.TryGetComponent<BaseHoldable>(out var component))
				{
					Singleton<QuickSlotHandler>.Instance.RemoveSpecificItem(component);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTryTakeInHandRpc(NetworkObjectReference interactorRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(4011743754u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 4011743754u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			RpcParams rpcParams3 = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
			if (!interactorRef.TryGet(out var networkObject) || !networkObject.TryGetComponent<Interactor>(out var component))
			{
				UtilityMethods.Log("<color=yellow>[WholesaleEntity] Take failed. Interactor reference is invalid.</color>");
			}
			else if (component.OwnerClientId != senderClientId)
			{
				UtilityMethods.Log("<color=yellow>[WholesaleEntity] Take failed. Sender does not own the interactor.</color>");
			}
			else if (ServerCanTakeOutput(component))
			{
				if (_currentEntityCount.Value <= 0)
				{
					UtilityMethods.Log("<color=yellow>[WholesaleEntity] Pack is empty.</color>");
					ClientShowPackNotifyRpc(PackNotifyId.PackEmpty, rpcParams3);
					return;
				}
				if (WholesaleSO == null)
				{
					UtilityMethods.Log("<color=yellow>[WholesaleEntity] Take failed. WholesaleSO is null.</color>");
					return;
				}
				if (WholesaleSO.OutputItem == null)
				{
					UtilityMethods.Log("<color=yellow>[WholesaleEntity] Take failed. OutputItem is null.</color>");
					return;
				}
				if (WholesaleSO.OutputItem.Prefab == null)
				{
					UtilityMethods.Log("<color=yellow>[WholesaleEntity] Take failed. Output item prefab is missing.</color>");
					return;
				}
				if (!WholesaleSO.OutputItem.Prefab.TryInstantiateAndSpawn(out var instance, Vector3.zero, Quaternion.identity) || instance == null)
				{
					UtilityMethods.Log("<color=red>[WholesaleEntity] Take failed. Output item could not be spawned.</color>");
					return;
				}
				if (!instance.TryGetComponent<BaseHoldable>(out var component2))
				{
					UtilityMethods.Log("<color=red>[WholesaleEntity] Take failed. Output item is missing BaseHoldable.</color>");
					instance.Despawn();
					return;
				}
				component2.ServerLazyHold(senderClientId, interactorRef, select: true, hold: true);
				Consume();
				ServerAddOutputStock(WholesaleSO.OutputItem, 1);
				ServerCompleteTakeOneQuest(WholesaleSO.OutputItem);
				ClientPlayAudioRpc(rpcParams3);
			}
		}

		private bool ServerCanTakeOutput(Interactor interactor)
		{
			return StaffStackPackage.ServerCanAccessContents(this, interactor);
		}

		private void ServerCompleteTakeOneQuest(HoldableSO outputItem)
		{
			if (!base.IsServer || outputItem == null)
			{
				return;
			}
			ItemDatabaseSO itemDatabaseSO = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.ItemDatabaseSO : null);
			if (!(itemDatabaseSO == null))
			{
				HoldableSO wholesaleSO = WholesaleSO;
				if (IsSameItem(wholesaleSO, itemDatabaseSO.WholesaleRice))
				{
					QuestReferenceHub.Instance?.QuestComplete(QuestId.TakeRawRiceFromRiceSack);
				}
				else if (IsSameItem(wholesaleSO, itemDatabaseSO.WholesaleCucumber))
				{
					QuestReferenceHub.Instance?.QuestComplete(QuestId.TakeOneCucumberFromCucumberWholesale);
				}
			}
		}

		private static bool IsSameItem(HoldableSO item, ItemSO target)
		{
			if (item == null || target == null)
			{
				return false;
			}
			if (!(item == target))
			{
				return item.Id == target.Id;
			}
			return true;
		}

		private void Consume()
		{
			if (_currentEntityCount.Value > 0)
			{
				_currentEntityCount.Value--;
				ServerRemoveWholesaleStock(1);
			}
		}

		public override TooltipData GetTooltip()
		{
			if (WholesaleSO == null)
			{
				return default;
			}
			TooltipData data = WholesaleSO.Tooltip.Data;
			data.Title = WholesaleSO.LocalizedName;
			data.Body = $"{_currentEntityCount.Value}/{WholesaleSO.Quantity}";
			return data;
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientPlayAudioRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1110216874u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1110216874u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!(Singleton<SoundManager>.Instance == null) && !(_itemSO == null))
				{
					Singleton<SoundManager>.Instance.PlaySFX(SoundType.Item_Pick, _itemSO.HoldableMaterialType, _itemSO.FurnitureWeight, base.transform.position);
				}
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		public void ClientShowPackNotifyRpc(PackNotifyId id, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3293795817u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in id, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 3293795817u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!(Singleton<NotifyManager>.Instance == null))
			{
				string item = PackNotifyText.Resolve(id).message;
				if (!string.IsNullOrWhiteSpace(item))
				{
					Singleton<NotifyManager>.Instance.ShowToast(item, NotificationState.Error);
				}
			}
		}

		public bool ServerTryConsumeAmount(byte amount, out byte consumedAmount)
		{
			consumedAmount = 0;
			if (!base.IsServer)
			{
				UtilityMethods.Log("<color=yellow>[WholesaleEntity] Consume blocked. Only server can consume wholesale count.</color>");
				return false;
			}
			if (amount == 0 || _currentEntityCount.Value <= 0)
			{
				return false;
			}
			byte b = (byte)Mathf.Min(amount, _currentEntityCount.Value);
			_currentEntityCount.Value -= b;
			consumedAmount = b;
			ServerRemoveWholesaleStock(consumedAmount);
			return consumedAmount > 0;
		}

		[Rpc(SendTo.Server)]
		public void ServerTryConsumeAndFillBeverageRpc(NetworkObjectReference fridgeRef, byte shelfIndex, byte beverageType, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(189629370u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in fridgeRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in shelfIndex, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in beverageType, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 189629370u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
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
			RpcParams rpcParams3 = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
			if (_currentEntityCount.Value <= 0)
			{
				ClientShowPackNotifyRpc(PackNotifyId.PackEmpty, rpcParams3);
				return;
			}
			if (WholesaleSO == null || WholesaleSO.OutputItem == null)
			{
				UtilityMethods.Log("<color=yellow>[WholesaleEntity] Beverage fill failed. WholesaleSO or OutputItem is null.</color>");
				return;
			}
			if (!(WholesaleSO.OutputItem is BeverageSO beverageSO))
			{
				UtilityMethods.Log("<color=yellow>[WholesaleEntity] Beverage fill failed. OutputItem is not BeverageSO.</color>");
				return;
			}
			BeverageType beverageType2 = (BeverageType)beverageType;
			NetworkObject networkObject;
			BeverageFridge component;
			if (beverageSO.BeverageType != beverageType2)
			{
				UtilityMethods.Log("<color=yellow>[WholesaleEntity] Beverage fill failed. Pack beverage type mismatch.</color>");
				ClientShowPackNotifyRpc(PackNotifyId.IdMismatchCancelled, rpcParams3);
			}
			else if (!fridgeRef.TryGet(out networkObject))
			{
				UtilityMethods.Log("<color=yellow>[WholesaleEntity] Beverage fill failed. Fridge reference is invalid.</color>");
			}
			else if (!networkObject.TryGetComponent<BeverageFridge>(out component))
			{
				UtilityMethods.Log("<color=yellow>[WholesaleEntity] Beverage fill failed. BeverageFridge component is missing.</color>");
			}
			else if (!component.ServerTryFillAtShelf(shelfIndex, beverageType2))
			{
				ClientFridgeFullFeedbackRpc(rpcParams3);
			}
			else
			{
				Consume();
				ClientConsumedOneFeedbackRpc(_currentEntityCount.Value, rpcParams3);
				ClientPlayAudioRpc(rpcParams3);
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientFridgeFullFeedbackRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2119134010u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2119134010u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (Singleton<NotifyManager>.Instance != null)
				{
					Singleton<NotifyManager>.Instance.ShowToast("Fridge is full.", NotificationState.Error);
				}
				UtilityMethods.Log("<color=yellow>[WholesaleEntity] Fridge is full.</color>");
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientConsumedOneFeedbackRpc(int newCount, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2599453959u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, newCount);
				__endSendRpc(ref bufferWriter, 2599453959u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				UtilityMethods.Log($"<color=yellow>[WholesaleEntity] Consumed 1 item. Remaining: {newCount}</color>");
			}
		}

		public override void EnableGazeHints()
		{
			if (!(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.ClearAllHints();
				BaseHoldable baseHoldable = ((Singleton<QuickSlotHandler>.Instance != null) ? Singleton<QuickSlotHandler>.Instance.SelectedItem : null);
				if (baseHoldable is Tray tray && IsSupportedTrayPackOutput())
				{
					Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", HasMatchingOutputOnTray(tray) ? "key_return_item_to_pack" : "key_hold");
					Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_take_item_to_tray");
				}
				else if (IsSupportedReturnHeldItem(baseHoldable))
				{
					Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_return_item_to_pack");
					Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_take_item_from_pack");
				}
				else
				{
					Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_hold");
					Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_take_item_from_pack");
				}
			}
		}

		public override void GazeEnter()
		{
			base.GazeEnter();
			if (!(Singleton<QuickSlotHandler>.Instance == null))
			{
				Singleton<QuickSlotHandler>.Instance.SelectedItemChanged -= OnQuickSlotSelectedItemChanged;
				Singleton<QuickSlotHandler>.Instance.SelectedItemChanged += OnQuickSlotSelectedItemChanged;
			}
		}

		public override void GazeExit()
		{
			if (Singleton<QuickSlotHandler>.Instance != null)
			{
				Singleton<QuickSlotHandler>.Instance.SelectedItemChanged -= OnQuickSlotSelectedItemChanged;
			}
			base.GazeExit();
		}

		private void OnQuickSlotSelectedItemChanged(BaseHoldable selectedItem)
		{
			if (_inGaze)
			{
				RefreshGazeFeedback();
			}
		}

		public override void DisableGazeHints()
		{
			if ((!base.IsOwner || !(_ownerInteractor != null)) && !(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
				Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
			}
		}

		public override void EnableHeldHints()
		{
			if (!(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.ClearAllHints();
				Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_place");
				AddHeldThrowHint();
			}
		}

		public override void DisableHeldHints()
		{
			if (!(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
				RemoveHeldThrowHint();
			}
		}

		public override string SaveCustomData()
		{
			return WholesaleQuickSlotCountCodec.Encode(CurrentCount);
		}

		public override void LoadCustomData(FixedString128Bytes data)
		{
			// Empty legacy data has no remaining quantity to infer. The original
			// spawn default remains; valid zero is an explicit encoded value.
			if (WholesaleQuickSlotCountCodec.TryDecode(data.ToString(), out var quantity))
				Load(new WholesaleData { Id = base.ItemSO.Id, Quantity = quantity });
		}

		public WholesaleData Save()
		{
			return new WholesaleData
			{
				Id = base.ItemSO.Id,
				Quantity = _currentEntityCount.Value,
				TransformData = GetTransformData()
			};
		}

		public void Load(WholesaleData data)
		{
			if (!base.IsServer)
			{
				UtilityMethods.Log("<color=yellow>[WholesaleEntity] Load blocked. Only server can load wholesale data.</color>");
				return;
			}
			byte b = data.Quantity;
			if (WholesaleSO != null)
			{
				b = (byte)Mathf.Clamp(b, 0, WholesaleSO.Quantity);
			}
			byte value = _currentEntityCount.Value;
			_currentEntityCount.Value = b;
			_hasLoadedData = true;
			SyncLoadedWholesaleStock(value, b);
		}

		private void ServerAddWholesaleStock(int amount)
		{
			if (base.IsServer && amount > 0 && !(WholesaleSO == null) && !string.IsNullOrEmpty(WholesaleSO.Id))
			{
				((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null)?.ServerAddStockRpc(WholesaleSO.Id, amount);
			}
		}

		private void ServerRemoveWholesaleStock(int amount)
		{
			if (base.IsServer && amount > 0 && !(WholesaleSO == null) && !string.IsNullOrEmpty(WholesaleSO.Id))
			{
				((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null)?.ServerRemoveStockRpc(WholesaleSO.Id, amount);
			}
		}

		private void ServerAddOutputStock(HoldableSO outputItem, int amount)
		{
			if (ShouldManageOutputStock(outputItem, amount))
			{
				((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null)?.ServerAddStockRpc(outputItem.Id, amount);
			}
		}

		private void ServerRemoveOutputStock(HoldableSO outputItem, int amount)
		{
			if (ShouldManageOutputStock(outputItem, amount))
			{
				((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null)?.ServerRemoveStockRpc(outputItem.Id, amount);
			}
		}

		private bool ShouldManageOutputStock(HoldableSO outputItem, int amount)
		{
			if (!base.IsServer || amount <= 0 || outputItem == null || string.IsNullOrEmpty(outputItem.Id))
			{
				return false;
			}
			if (outputItem.ItemType != ItemType.RawIngredient)
			{
				return outputItem.ItemType == ItemType.ProcessedIngredient;
			}
			return true;
		}

		private void SyncLoadedWholesaleStock(byte previousQuantity, byte loadedQuantity)
		{
			if (base.IsServer && previousQuantity != loadedQuantity)
			{
				int num = loadedQuantity - previousQuantity;
				if (num > 0)
				{
					ServerAddWholesaleStock(num);
				}
				else
				{
					ServerRemoveWholesaleStock(-num);
				}
			}
		}

		public TransformData GetTransformData()
		{
			return new TransformData
			{
				Position = base.transform.position,
				Rotation = base.transform.rotation.eulerAngles
			};
		}

		protected override void __initializeVariables()
		{
			if (_currentEntityCount == null)
			{
				throw new Exception("WholesaleEntity._currentEntityCount cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_currentEntityCount.Initialize(this);
			__nameNetworkVariable(_currentEntityCount, "_currentEntityCount");
			NetworkVariableFields.Add(_currentEntityCount);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3736166368u, __rpc_handler_3736166368, "ServerTryTakeOneToTrayRpc", RpcInvokePermission.Everyone);
			__registerRpc(3269489278u, __rpc_handler_3269489278, "ServerTryReturnOneFromTrayRpc", RpcInvokePermission.Everyone);
			__registerRpc(1721005426u, __rpc_handler_1721005426, "ServerTryPutBackIntoPackRpc", RpcInvokePermission.Everyone);
			__registerRpc(1456666926u, __rpc_handler_1456666926, "ClientRemoveAcceptedHeldItemRpc", RpcInvokePermission.Everyone);
			__registerRpc(4011743754u, __rpc_handler_4011743754, "ServerTryTakeInHandRpc", RpcInvokePermission.Everyone);
			__registerRpc(1110216874u, __rpc_handler_1110216874, "ClientPlayAudioRpc", RpcInvokePermission.Everyone);
			__registerRpc(3293795817u, __rpc_handler_3293795817, "ClientShowPackNotifyRpc", RpcInvokePermission.Everyone);
			__registerRpc(189629370u, __rpc_handler_189629370, "ServerTryConsumeAndFillBeverageRpc", RpcInvokePermission.Everyone);
			__registerRpc(2119134010u, __rpc_handler_2119134010, "ClientFridgeFullFeedbackRpc", RpcInvokePermission.Everyone);
			__registerRpc(2599453959u, __rpc_handler_2599453959, "ClientConsumedOneFeedbackRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3736166368(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((WholesaleEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((WholesaleEntity)target).ServerTryTakeOneToTrayRpc(value, value2, ext);
				((WholesaleEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3269489278(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((WholesaleEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((WholesaleEntity)target).ServerTryReturnOneFromTrayRpc(value, value2, ext);
				((WholesaleEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1721005426(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((WholesaleEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((WholesaleEntity)target).ServerTryPutBackIntoPackRpc(value, value2, ext);
				((WholesaleEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1456666926(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((WholesaleEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((WholesaleEntity)target).ClientRemoveAcceptedHeldItemRpc(value, ext);
				((WholesaleEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4011743754(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((WholesaleEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((WholesaleEntity)target).ServerTryTakeInHandRpc(value, ext);
				((WholesaleEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1110216874(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((WholesaleEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((WholesaleEntity)target).ClientPlayAudioRpc(ext);
				((WholesaleEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3293795817(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out PackNotifyId value, default(FastBufferWriter.ForEnums));
				RpcParams ext = rpcParams.Ext;
				((WholesaleEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((WholesaleEntity)target).ClientShowPackNotifyRpc(value, ext);
				((WholesaleEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_189629370(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out byte value3, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((WholesaleEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((WholesaleEntity)target).ServerTryConsumeAndFillBeverageRpc(value, value2, value3, ext);
				((WholesaleEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2119134010(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((WholesaleEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((WholesaleEntity)target).ClientFridgeFullFeedbackRpc(ext);
				((WholesaleEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2599453959(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				RpcParams ext = rpcParams.Ext;
				((WholesaleEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((WholesaleEntity)target).ClientConsumedOneFeedbackRpc(value, ext);
				((WholesaleEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "WholesaleEntity";
		}
	}
}
