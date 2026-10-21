using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Interaction.ToolTip;
using PaperPixelGames.Core.Kitchen;
using PaperPixelGames.Core.Kitchen.Wholasale;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.ProcessedIngredient;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Core.ProcessedIngredient
{
	public class SideDishIngredientEntity : IngredientEntity
	{
		[SerializeField]
		private byte _maxEntityCount;

		[SerializeField]
		private NetworkObject _outPutObjPrefab;

		[Header("Count Visuals")]
		[SerializeField]
		private GameObject _normalVisual;

		[SerializeField]
		private GameObject _emptyVisual;

		private bool _serverRegisteredOutputStock;

		private bool _takeInHandRequestPending;

		private bool HasCountVisuals
		{
			get
			{
				if (_normalVisual != null)
				{
					return _emptyVisual != null;
				}
				return false;
			}
		}

		public SideDishSO StaffOutputItem
		{
			get
			{
				if (!TryGetOutputSideDish(out var outputSideDish))
				{
					return null;
				}
				return outputSideDish;
			}
		}

		public int StaffPackageQuantity => _maxEntityCount;

		public int ServerTransferStackContentsTo(SideDishIngredientEntity target)
		{
			if (!base.IsServer || !base.IsSpawned || target == null || !target.IsServer || !target.IsSpawned || target == this || target.ItemSO != base.ItemSO || base.IsStaffCarried || base.IsPlayerInventoryOwnedForPersistence || !CanHold.Value)
			{
				return 0;
			}
			int num = Mathf.Min(base.IngredientCount, target._maxEntityCount - target.IngredientCount);
			if (num <= 0)
			{
				return 0;
			}
			base.IngredientCount -= (byte)num;
			target.IngredientCount += (byte)num;
			return num;
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			_takeInHandRequestPending = false;
			NetworkVariable<byte> ingredientCountState = base.IngredientCountState;
			ingredientCountState.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(ingredientCountState.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnIngredientCountChanged));
			EnsureCountVisualsFollowHoldState();
			if (base.IsServer)
			{
				base.IngredientCount = _maxEntityCount;
				ServerRegisterOutputStock();
			}
			RefreshCountVisual();
		}

		public override void OnNetworkDespawn()
		{
			_takeInHandRequestPending = false;
			NetworkVariable<byte> ingredientCountState = base.IngredientCountState;
			ingredientCountState.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(ingredientCountState.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnIngredientCountChanged));
			if (base.IsServer)
			{
				ServerDeregisterOutputStock();
			}
			base.OnNetworkDespawn();
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

		public override void EnableGazeHints()
		{
			if (Singleton<HintsManager>.Instance == null)
			{
				return;
			}
			Singleton<HintsManager>.Instance.ClearAllHints();
			BaseHoldable baseHoldable = Singleton<QuickSlotHandler>.Instance?.SelectedItem;
			if (baseHoldable is Tray tray)
			{
				if (tray.TryGetSideDish(out var sideDish) && IsMatchingOutputSideDish(sideDish))
				{
					Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_return_item_to_pack");
				}
				else
				{
					Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_hold");
				}
				Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_take_item_to_tray");
			}
			else if (baseHoldable is ServedDish)
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

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
			Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
		}

		public override void EnableHeldHints()
		{
			Singleton<HintsManager>.Instance.ClearAllHints();
			Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_place");
			AddHeldThrowHint();
		}

		public override void DisableHeldHints()
		{
			if (!(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
				RemoveHeldThrowHint();
			}
		}

		private void OnQuickSlotSelectedItemChanged(BaseHoldable selectedItem)
		{
			if (_inGaze)
			{
				RefreshGazeFeedback();
			}
		}

		private void OnIngredientCountChanged(byte previousCount, byte currentCount)
		{
			RefreshCountVisual();
			RefreshHeldCountVisuals();
			if (_inGaze)
			{
				Singleton<ToolTipHandler>.Instance?.OnGazeEnter(this);
				RefreshGazeFeedback();
			}
		}

		private void RefreshCountVisual()
		{
			if (HasCountVisuals)
			{
				bool flag = _normalVisual.activeSelf || _emptyVisual.activeSelf;
				bool flag2 = base.IngredientCount == 0;
				_normalVisual.SetActive(flag && !flag2);
				_emptyVisual.SetActive(flag & flag2);
			}
		}

		private void EnsureCountVisualsFollowHoldState()
		{
			if (HasCountVisuals)
			{
				if (_deactiveObjects == null)
				{
					_deactiveObjects = new List<GameObject>();
				}
				if (!_deactiveObjects.Contains(_normalVisual))
				{
					_deactiveObjects.Add(_normalVisual);
				}
				if (!_deactiveObjects.Contains(_emptyVisual))
				{
					_deactiveObjects.Add(_emptyVisual);
				}
			}
		}

		protected override void SpawnFpsShadow(Transform parent)
		{
			base.SpawnFpsShadow(parent);
			RefreshHeldCountVisual(_fpsShadowObject);
		}

		protected override void SpawnTpsShadow(Transform parent, bool isOwner)
		{
			base.SpawnTpsShadow(parent, isOwner);
			RefreshHeldCountVisual(_tpsShadowObject);
		}

		private void RefreshHeldCountVisuals()
		{
			RefreshHeldCountVisual(_fpsShadowObject);
			RefreshHeldCountVisual(_tpsShadowObject);
			RefreshHeldCountVisual(_fpsParentedShadowObject);
			RefreshHeldCountVisual(_tpsParentedShadowObject);
		}

		private void RefreshHeldCountVisual(GameObject heldVisual)
		{
			ApplyStaffHeldContents(heldVisual, base.IngredientCount);
		}

		public void ApplyStaffHeldContents(GameObject heldVisual, int count)
		{
			if (!(heldVisual == null))
			{
				heldVisual.GetComponentInChildren<SideDishHeldCountVisual>(includeInactive: true)?.Apply(count == 0);
			}
		}

		public override void ClientRefreshRuntimeRepresentationState()
		{
			base.ClientRefreshRuntimeRepresentationState();
			RefreshCountVisual();
			RefreshHeldCountVisuals();
		}

		protected override void ClientDrop(Vector3 pos, Quaternion rot)
		{
			base.ClientDrop(pos, rot);
			ClientRefreshRuntimeRepresentationState();
		}

		protected override void ClientCrossMouse1(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (phase != InputActionPhase.Started || interactor?.NetworkObject == null)
			{
				return;
			}
			if (held is Tray tray)
			{
				if (tray.TryGetSideDish(out var sideDish) && IsMatchingOutputSideDish(sideDish))
				{
					ServerTryReturnSideDishFromTrayRpc(interactor.NetworkObject.ToReference(), tray.NetworkObject.ToReference());
				}
				else
				{
					base.ClientCrossMouse1(interactor, held, phase);
				}
			}
			else if (held is ServedDish)
			{
				ServerTryReturnSideDishInHandRpc(interactor.NetworkObject.ToReference(), held.NetworkObject.ToReference());
			}
			else
			{
				base.ClientCrossMouse1(interactor, held, phase);
			}
		}

		protected override void ClientMouse2(Interactor interactor, InputActionPhase phase)
		{
			if (phase != InputActionPhase.Started || _takeInHandRequestPending || interactor?.NetworkObject == null)
			{
				return;
			}
			if (!TryGetOutputHoldable(out var outputHoldable))
			{
				UtilityMethods.Log("[SideDishIngredientEntity] Take failed. Output holdable is missing.", LogType.Error);
				return;
			}
			if (Singleton<QuickSlotHandler>.Instance == null || !Singleton<QuickSlotHandler>.Instance.CanHold(outputHoldable))
			{
				Singleton<NotifyManager>.Instance?.ShowToast("Slots are full!", NotificationState.Error);
				return;
			}
			_takeInHandRequestPending = true;
			ServerTryTakeInHandRpc(interactor.NetworkObject.ToReference());
			if (base.StaffStackPoint < 0)
			{
				Singleton<ToolTipHandler>.Instance?.OnGazeEnter(this);
			}
		}

		protected override void ClientCrossMouse2(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				if (held is Tray tray)
				{
					ServerTryTakeSideDishToTrayRpc(interactor.NetworkObject.ToReference(), tray.NetworkObject.ToReference());
				}
				else
				{
					ClientMouse2(interactor, phase);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTryReturnSideDishInHandRpc(NetworkObjectReference interactorRef, NetworkObjectReference heldRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(841084681u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in heldRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 841084681u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				RpcParams rpcParams3 = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
				if (!interactorRef.TryGet(out var networkObject) || !networkObject.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != senderClientId || !heldRef.TryGet(out var networkObject2) || !networkObject2.IsSpawned || !networkObject2.TryGetComponent<ServedDish>(out var component2) || component2.OwnerClientId != senderClientId || component.CurrentHeld != component2 || component2.CanHold.Value || component2.IsStaffCarried || !StaffStackPackage.ServerCanAccessContents(this, component))
				{
					ClientShowTransferNotifyRpc("This action cannot be completed.", rpcParams3);
					return;
				}
				if (base.IngredientCount >= _maxEntityCount)
				{
					ClientShowTransferNotifyRpc("Container is full.", rpcParams3);
					return;
				}
				if (!IsMatchingOutputSideDish(component2))
				{
					ClientShowTransferNotifyRpc("This item does not belong in this container.", rpcParams3);
					return;
				}
				base.IngredientCount++;
				ClientRemoveAcceptedHeldItemRpc(heldRef, rpcParams3);
				networkObject2.Despawn(destroy: false);
				ClientPlayInteractionAudioRpc(rpcParams3);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTryTakeSideDishToTrayRpc(NetworkObjectReference interactorRef, NetworkObjectReference trayRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3623174715u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in trayRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3623174715u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				RpcParams rpcParams3 = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
				ServedDish servedDish;
				if (!interactorRef.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != senderClientId || !trayRef.TryGetComponent<Tray>(out var component2) || component2.OwnerClientId != senderClientId || component.CurrentHeld != component2 || component2.CanHold.Value || base.IngredientCount <= 0 || !StaffStackPackage.ServerCanAccessContents(this, component))
				{
					ClientShowTransferNotifyRpc("This action cannot be completed.", rpcParams3);
				}
				else if (!ServerTryCreateOutputSideDish(out servedDish))
				{
					ClientShowTransferNotifyRpc("Container is empty.", rpcParams3);
				}
				else if (!component2.ServerTryPlaceWorldDishOnHeldTray(servedDish, senderClientId))
				{
					ServerReturnOutputSideDishToStock(servedDish);
					ClientShowTransferNotifyRpc("The tray slot is already occupied.", rpcParams3);
				}
				else
				{
					ClientPlayInteractionAudioRpc(rpcParams3);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTryReturnSideDishFromTrayRpc(NetworkObjectReference interactorRef, NetworkObjectReference trayRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1727969390u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in trayRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 1727969390u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				RpcParams rpcParams3 = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
				if (!interactorRef.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != senderClientId || !trayRef.TryGetComponent<Tray>(out var component2) || component2.OwnerClientId != senderClientId || component.CurrentHeld != component2 || component2.CanHold.Value || !TryGetOutputSideDish(out var outputSideDish) || !StaffStackPackage.ServerCanAccessContents(this, component))
				{
					ClientShowTransferNotifyRpc("This action cannot be completed.", rpcParams3);
					return;
				}
				if (base.IngredientCount >= _maxEntityCount)
				{
					ClientShowTransferNotifyRpc("Container is full.", rpcParams3);
					return;
				}
				if (!component2.ServerTryRemoveOptionalDishForWholesaleReturn(outputSideDish.Id, out var dish))
				{
					ClientShowTransferNotifyRpc("This item does not belong in this container.", rpcParams3);
					return;
				}
				ServerReturnOutputSideDishToStock(dish);
				component2.ServerRefreshHeldTrayContentsGraphics();
				ClientPlayInteractionAudioRpc(rpcParams3);
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
				FastBufferWriter bufferWriter = __beginSendRpc(1114185519u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 1114185519u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			RpcParams rpcParams3 = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
			if (!interactorRef.TryGet(out var networkObject) || !networkObject.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != senderClientId)
			{
				UtilityMethods.Log("[SideDishIngredientEntity] Take failed. Sender does not own a valid interactor.", LogType.Warning);
				ClientCompleteTakeInHandRequestRpc(rpcParams3);
				return;
			}
			if (!StaffStackPackage.ServerCanAccessContents(this, component))
			{
				ClientCompleteTakeInHandRequestRpc(rpcParams3);
				return;
			}
			if (base.IngredientCount <= 0)
			{
				ClientShowPackNotifyRpc(WholesaleEntity.PackNotifyId.PackEmpty, rpcParams3);
				ClientCompleteTakeInHandRequestRpc(rpcParams3);
				return;
			}
			if (!TryGetOutputHoldable(out var _))
			{
				UtilityMethods.Log("[SideDishIngredientEntity] Take failed. Output holdable is missing.", LogType.Error);
				ClientCompleteTakeInHandRequestRpc(rpcParams3);
				return;
			}
			NetworkObject networkObject2 = UnityEngine.Object.Instantiate(_outPutObjPrefab);
			BaseHoldable componentInChildren = networkObject2.GetComponentInChildren<BaseHoldable>(includeInactive: true);
			if (componentInChildren == null)
			{
				UtilityMethods.Log("[SideDishIngredientEntity] Take failed. Spawned output has no holdable component.", LogType.Error);
				UnityEngine.Object.Destroy(networkObject2.gameObject);
				ClientCompleteTakeInHandRequestRpc(rpcParams3);
				return;
			}
			networkObject2.Spawn(destroyWithScene: true);
			component.ServerHoldRpc(networkObject2.ToReference());
			if (component.CurrentHeld != componentInChildren || componentInChildren.OwnerClientId != senderClientId || componentInChildren.CanHold.Value)
			{
				UtilityMethods.Log("[SideDishIngredientEntity] Take failed. Server could not assign the spawned item.", LogType.Warning);
				networkObject2.Despawn();
				ClientCompleteTakeInHandRequestRpc(rpcParams3);
			}
			else
			{
				base.IngredientCount--;
				ClientPlayInteractionAudioRpc(rpcParams3);
				ClientCompleteTakeInHandRequestRpc(rpcParams3);
			}
		}

		protected override void OnServerIngredientCountChanged(byte previousCount, byte currentCount)
		{
			if (_serverRegisteredOutputStock)
			{
				int num = currentCount - previousCount;
				if (num != 0)
				{
					ServerAdjustOutputStock(num);
				}
			}
		}

		private void ServerRegisterOutputStock()
		{
			if (!_serverRegisteredOutputStock && TryGetOutputSideDish(out var _))
			{
				_serverRegisteredOutputStock = true;
				ServerAdjustOutputStock(base.IngredientCount);
			}
		}

		private void ServerDeregisterOutputStock()
		{
			if (_serverRegisteredOutputStock)
			{
				ServerAdjustOutputStock(-base.IngredientCount);
				_serverRegisteredOutputStock = false;
			}
		}

		private void ServerAdjustOutputStock(int delta)
		{
			if (!base.IsServer || delta == 0 || !TryGetOutputSideDish(out var outputSideDish))
			{
				return;
			}
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (!(restaurantManager == null))
			{
				if (delta > 0)
				{
					restaurantManager.ServerAddStockRpc(outputSideDish.Id, delta);
				}
				else
				{
					restaurantManager.ServerRemoveStockRpc(outputSideDish.Id, -delta);
				}
			}
		}

		public bool ServerStaffTake(out ServedDish dish)
		{
			dish = null;
			if (!CanHold.Value || base.IsStaffCarried || base.IsPlayerInventoryOwnedForPersistence || base.transform.parent != null)
			{
				return false;
			}
			return ServerTryCreateOutputSideDish(out dish);
		}

		public bool ServerTryCreateOutputSideDishForTray(ulong requesterClientId, out ServedDish servedDish)
		{
			servedDish = null;
			if (!base.IsServer)
			{
				return false;
			}
			if (base.NetworkObject == null || !base.NetworkObject.IsSpawned)
			{
				return false;
			}
			if (base.OwnerClientId != requesterClientId)
			{
				return false;
			}
			if (CanHold.Value)
			{
				return false;
			}
			if (base.IngredientCount <= 0)
			{
				RpcParams rpcParams = base.RpcTarget.Single(requesterClientId, RpcTargetUse.Temp);
				ClientShowPackNotifyRpc(WholesaleEntity.PackNotifyId.PackEmpty, rpcParams);
				return false;
			}
			return ServerTryCreateOutputSideDish(out servedDish);
		}

		private bool ServerTryCreateOutputSideDish(out ServedDish servedDish)
		{
			servedDish = null;
			if (!base.IsServer)
			{
				return false;
			}
			if (base.NetworkObject == null || !base.NetworkObject.IsSpawned)
			{
				return false;
			}
			if (base.IngredientCount <= 0)
			{
				return false;
			}
			if (_outPutObjPrefab == null)
			{
				return false;
			}
			NetworkObject networkObject = UnityEngine.Object.Instantiate(_outPutObjPrefab);
			servedDish = networkObject.GetComponentInChildren<ServedDish>(includeInactive: true);
			if (servedDish == null)
			{
				UnityEngine.Object.Destroy(networkObject.gameObject);
				return false;
			}
			networkObject.Spawn(destroyWithScene: true);
			base.IngredientCount--;
			return true;
		}

		public void ServerReturnOutputSideDishToStock(ServedDish servedDish)
		{
			if (base.IsServer)
			{
				if (servedDish != null && servedDish.NetworkObject != null && servedDish.NetworkObject.IsSpawned)
				{
					servedDish.NetworkObject.Despawn(destroy: false);
				}
				if (base.IngredientCount < _maxEntityCount)
				{
					base.IngredientCount++;
				}
			}
		}

		private bool TryGetOutputHoldable(out BaseHoldable outputHoldable)
		{
			outputHoldable = ((_outPutObjPrefab != null) ? _outPutObjPrefab.GetComponentInChildren<BaseHoldable>(includeInactive: true) : null);
			if (outputHoldable != null)
			{
				return outputHoldable.ItemSO != null;
			}
			return false;
		}

		private bool TryGetOutputSideDish(out SideDishSO outputSideDish)
		{
			outputSideDish = null;
			if (!TryGetOutputHoldable(out var outputHoldable))
			{
				return false;
			}
			outputSideDish = outputHoldable.ItemSO as SideDishSO;
			if (outputSideDish != null)
			{
				return !string.IsNullOrEmpty(outputSideDish.Id);
			}
			return false;
		}

		private bool IsMatchingOutputSideDish(ServedDish dish)
		{
			if (dish != null && dish.ItemSO != null && TryGetOutputSideDish(out var outputSideDish))
			{
				return dish.ItemSO.Id == outputSideDish.Id;
			}
			return false;
		}

		[Rpc(SendTo.SpecifiedInParams)]
		public void ClientShowPackNotifyRpc(WholesaleEntity.PackNotifyId id, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3232622618u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in id, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 3232622618u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				string item = WholesaleEntity.PackNotifyText.Resolve(id).message;
				Singleton<NotifyManager>.Instance.ShowToast(item);
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
				FastBufferWriter bufferWriter = __beginSendRpc(3648798357u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in acceptedHeldRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3648798357u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
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

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientCompleteTakeInHandRequestRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(11282815u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 11282815u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_takeInHandRequestPending = false;
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientPlayInteractionAudioRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1140110620u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1140110620u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
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
		private void ClientShowTransferNotifyRpc(string message, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2636292687u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bool value = message != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(message);
				}
				__endSendRpc(ref bufferWriter, 2636292687u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Singleton<NotifyManager>.Instance?.ShowToast(message, NotificationState.Error);
			}
		}

		public override TooltipData GetTooltip()
		{
			TooltipData data = base.ItemSO.Tooltip.Data;
			data.Title = base.ItemSO.LocalizedName;
			data.Body = $"{base.IngredientCount}/{_maxEntityCount}";
			return data;
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(841084681u, __rpc_handler_841084681, "ServerTryReturnSideDishInHandRpc", RpcInvokePermission.Everyone);
			__registerRpc(3623174715u, __rpc_handler_3623174715, "ServerTryTakeSideDishToTrayRpc", RpcInvokePermission.Everyone);
			__registerRpc(1727969390u, __rpc_handler_1727969390, "ServerTryReturnSideDishFromTrayRpc", RpcInvokePermission.Everyone);
			__registerRpc(1114185519u, __rpc_handler_1114185519, "ServerTryTakeInHandRpc", RpcInvokePermission.Everyone);
			__registerRpc(3232622618u, __rpc_handler_3232622618, "ClientShowPackNotifyRpc", RpcInvokePermission.Everyone);
			__registerRpc(3648798357u, __rpc_handler_3648798357, "ClientRemoveAcceptedHeldItemRpc", RpcInvokePermission.Everyone);
			__registerRpc(11282815u, __rpc_handler_11282815, "ClientCompleteTakeInHandRequestRpc", RpcInvokePermission.Everyone);
			__registerRpc(1140110620u, __rpc_handler_1140110620, "ClientPlayInteractionAudioRpc", RpcInvokePermission.Everyone);
			__registerRpc(2636292687u, __rpc_handler_2636292687, "ClientShowTransferNotifyRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_841084681(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((SideDishIngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SideDishIngredientEntity)target).ServerTryReturnSideDishInHandRpc(value, value2, ext);
				((SideDishIngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3623174715(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((SideDishIngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SideDishIngredientEntity)target).ServerTryTakeSideDishToTrayRpc(value, value2, ext);
				((SideDishIngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1727969390(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((SideDishIngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SideDishIngredientEntity)target).ServerTryReturnSideDishFromTrayRpc(value, value2, ext);
				((SideDishIngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1114185519(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((SideDishIngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SideDishIngredientEntity)target).ServerTryTakeInHandRpc(value, ext);
				((SideDishIngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3232622618(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out WholesaleEntity.PackNotifyId value, default(FastBufferWriter.ForEnums));
				RpcParams ext = rpcParams.Ext;
				((SideDishIngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SideDishIngredientEntity)target).ClientShowPackNotifyRpc(value, ext);
				((SideDishIngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3648798357(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((SideDishIngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SideDishIngredientEntity)target).ClientRemoveAcceptedHeldItemRpc(value, ext);
				((SideDishIngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_11282815(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((SideDishIngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SideDishIngredientEntity)target).ClientCompleteTakeInHandRequestRpc(ext);
				((SideDishIngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1140110620(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((SideDishIngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SideDishIngredientEntity)target).ClientPlayInteractionAudioRpc(ext);
				((SideDishIngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2636292687(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				string s = null;
				if (value)
				{
					reader.ReadValueSafe(out s, false);
				}
				RpcParams ext = rpcParams.Ext;
				((SideDishIngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SideDishIngredientEntity)target).ClientShowTransferNotifyRpc(s, ext);
				((SideDishIngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "SideDishIngredientEntity";
		}
	}
}
