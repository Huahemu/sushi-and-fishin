using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen.ServedDishes;
using PaperPixelGames.Core.Kitchen.Wholasale;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Progress;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Core.Tutorials;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen
{
	public class BeverageFridge : NetworkBehaviour
	{
		private const byte EmptyType = 0;

		private const byte SaveVersion = 2;

		private const string BeverageFridgeTutorialSeenKey = "StationTutorial.BeverageFridge.Seen";

		private readonly NetworkList<BeverageShelfState> _shelfStates = new NetworkList<BeverageShelfState>();

		private readonly NetworkVariable<bool> _isDoorOpen = new NetworkVariable<bool>(value: false);

		[SerializeField]
		private List<BeverageShelf> _shelfList = new List<BeverageShelf>();

		[SerializeField]
		private BeverageDoor _beverageDoor;

		[SerializeField]
		private PhysicalStateController _physicalStateController;

		[SerializeField]
		private PhysicalUnlockController _physicalUnlockController;

		[SerializeField]
		private StationTutorialData _beverageFridgeTutorialData;

		[Header("SFX")]
		[SerializeField]
		private AudioSource _loopEngineSource;

		[SerializeField]
		private AudioSource _audioSource;

		[SerializeField]
		private AudioClip _doorOpenSfx;

		[SerializeField]
		private AudioClip _doorCloseSfx;

		[SerializeField]
		private AudioClip _takeSfx;

		[SerializeField]
		private AudioClip _placeSfx;

		private float _doorOpenLoopVolumeMultiplier = 1.85f;

		private float _defaultLoopEngineVolume;

		private bool _loopEngineVolumeCached;

		public bool IsDoorOpen => _isDoorOpen.Value;

		public AudioSource AudioSource => _audioSource;

		public AudioClip DoorOpenSfx => _doorOpenSfx;

		public AudioClip DoorCloseSfx => _doorCloseSfx;

		public BeverageDoor BeverageDoor => _beverageDoor;

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
			RefreshShelfReferences();
			_shelfStates.OnListChanged += OnShelfStatesChanged;
			NetworkVariable<bool> isDoorOpen = _isDoorOpen;
			isDoorOpen.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isDoorOpen.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnDoorBoolChanged));
			if (base.IsServer)
			{
				EnsureShelfStates();
			}
			RebuildAllShelves();
			if (_beverageDoor != null)
			{
				_beverageDoor.ChangeDoorStatus(_isDoorOpen.Value && IsPhysicallyUsable, playSfx: false);
			}
			CacheLoopEngineVolume();
			UpdateLoopEngineVolume(_isDoorOpen.Value);
			if (_loopEngineSource != null && !_loopEngineSource.isPlaying)
			{
				_loopEngineSource.Play();
			}
		}

		public override void OnNetworkDespawn()
		{
			if (base.IsServer)
			{
				ServerRemoveRegisteredShelfStock();
			}
			_shelfStates.OnListChanged -= OnShelfStatesChanged;
			NetworkVariable<bool> isDoorOpen = _isDoorOpen;
			isDoorOpen.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isDoorOpen.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnDoorBoolChanged));
			base.OnNetworkDespawn();
		}

		private void RefreshShelfReferences()
		{
			for (int i = 0; i < _shelfList.Count; i++)
			{
				if (!(_shelfList[i] == null))
				{
					_shelfList[i].Initialize(this, (byte)i);
				}
			}
		}

		private void EnsureShelfStates()
		{
			if (base.IsServer)
			{
				while (_shelfStates.Count < _shelfList.Count)
				{
					_shelfStates.Add(new BeverageShelfState(0, 0));
				}
				while (_shelfStates.Count > _shelfList.Count)
				{
					_shelfStates.RemoveAt(_shelfStates.Count - 1);
				}
			}
		}

		private void OnShelfStatesChanged(NetworkListEvent<BeverageShelfState> change)
		{
			if (change.Index < 0 || change.Index >= _shelfList.Count)
			{
				RebuildAllShelves();
			}
			else
			{
				RebuildShelf(change.Index);
			}
		}

		private void OnDoorBoolChanged(bool previousValue, bool newValue)
		{
			if (_beverageDoor != null)
			{
				_beverageDoor.ChangeDoorStatus(newValue && IsPhysicallyUsable);
			}
			UpdateLoopEngineVolume(newValue);
			RefreshShelfHints();
		}

		private void RefreshShelfHints()
		{
			for (int i = 0; i < _shelfList.Count; i++)
			{
				if (!(_shelfList[i] == null))
				{
					_shelfList[i].RefreshHints();
				}
			}
		}

		public void ServerResetForEndDay()
		{
			if (base.IsServer)
			{
				EnsureShelfStates();
				_isDoorOpen.Value = false;
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
				FastBufferWriter bufferWriter = __beginSendRpc(2475595570u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2475595570u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			RebuildAllShelves();
			if (_beverageDoor != null)
			{
				_beverageDoor.ChangeDoorStatus(isOpen: false, playSfx: false);
				_beverageDoor.ResetRuntimeState();
			}
			CacheLoopEngineVolume();
			UpdateLoopEngineVolume(doorOpen: false);
			if (_loopEngineSource != null && !_loopEngineSource.isPlaying)
			{
				_loopEngineSource.Play();
			}
			for (int i = 0; i < _shelfList.Count; i++)
			{
				if (!(_shelfList[i] == null))
				{
					_shelfList[i].ResetRuntimeState();
				}
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerToggleDoorRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3317748990u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3317748990u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsServer)
			{
				if (!IsPhysicallyUsable)
				{
					UtilityMethods.Log("[BeverageFridge] Toggle blocked. Fridge is locked.", LogType.Warning);
				}
				else
				{
					_isDoorOpen.Value = !_isDoorOpen.Value;
				}
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerTakeFromShelfRpc(byte shelfIndex, NetworkObjectReference interactorRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3868096947u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in shelfIndex, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3868096947u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
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
			if (!IsPhysicallyUsable)
			{
				UtilityMethods.Log("[BeverageFridge] Take blocked. Fridge is locked.", LogType.Warning);
			}
			else if (!_isDoorOpen.Value)
			{
				UtilityMethods.Log("<color=yellow>[BeverageFridge] Take blocked. Door is closed.</color>");
			}
			else
			{
				if (!TryGetShelf(shelfIndex, out var shelf))
				{
					return;
				}
				if (!interactorRef.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != senderClientId)
				{
					UtilityMethods.Log($"[BeverageFridge] Take rejected. Interactor ownership mismatch. Sender={senderClientId}", LogType.Warning);
					return;
				}
				BeverageShelfState nextState = _shelfStates[shelfIndex];
				if (nextState.Count <= 0 || nextState.Type == 0)
				{
					return;
				}
				BeverageType type = (BeverageType)nextState.Type;
				BeverageSO beverageSO = shelf.GetBeverageSO(type);
				if (beverageSO == null)
				{
					UtilityMethods.Log($"[BeverageFridge] Take failed. Missing BeverageSO for type: {type}", LogType.Error);
					return;
				}
				NetworkObject instance = null;
				if (beverageSO.Prefab == null || !beverageSO.Prefab.TryInstantiateAndSpawn(out instance, Vector3.zero, Quaternion.identity) || instance == null || !instance.TryGetComponent<BaseHoldable>(out var component2))
				{
					if (instance != null && instance.IsSpawned)
					{
						instance.Despawn();
					}
					UtilityMethods.Log($"[BeverageFridge] Take failed. Beverage could not be spawned. Type={type}", LogType.Error);
					return;
				}
				nextState.Count--;
				if (nextState.Count <= 0)
				{
					nextState.Type = 0;
				}
				ServerSetShelfState(shelfIndex, nextState);
				component2.ServerLazyHold(senderClientId, interactorRef, select: true, hold: true);
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerTakeFromShelfToTrayRpc(byte shelfIndex, NetworkObjectReference interactorRef, NetworkObjectReference trayRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1737710723u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in shelfIndex, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in trayRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 1737710723u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			RpcParams rpcParams3 = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
			if (!IsPhysicallyUsable)
			{
				ClientShowTrayTransferToastRpc("This fridge must be repaired first.", rpcParams3);
				return;
			}
			if (!_isDoorOpen.Value)
			{
				ClientShowTrayTransferToastRpc("Open the fridge door first.", rpcParams3);
				return;
			}
			if (!interactorRef.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != senderClientId || !trayRef.TryGetComponent<Tray>(out var component2) || component2.OwnerClientId != senderClientId || component.CurrentHeld != component2 || component2.CanHold.Value || !TryGetShelf(shelfIndex, out var shelf))
			{
				ClientShowTrayTransferToastRpc("This action cannot be completed.", rpcParams3);
				return;
			}
			BeverageShelfState nextState = _shelfStates[shelfIndex];
			if (nextState.Count <= 0 || nextState.Type == 0)
			{
				ClientShowTrayTransferToastRpc("Shelf empty.", rpcParams3);
				return;
			}
			BeverageSO beverageSO = shelf.GetBeverageSO((BeverageType)nextState.Type);
			NetworkObject instance = null;
			if (beverageSO?.Prefab == null || !beverageSO.Prefab.TryInstantiateAndSpawn(out instance, Vector3.zero, Quaternion.identity) || instance == null || !instance.TryGetComponent<ServedDish>(out var component3))
			{
				if (instance != null && instance.IsSpawned)
				{
					instance.Despawn();
				}
				ClientShowTrayTransferToastRpc("This item cannot be placed on the tray.", rpcParams3);
				return;
			}
			if (!component2.ServerTryPlaceWorldDishOnHeldTray(component3, senderClientId))
			{
				instance.Despawn();
				ClientShowTrayTransferToastRpc("The tray slot is already occupied.", rpcParams3);
				return;
			}
			nextState.Count--;
			if (nextState.Count <= 0)
			{
				nextState.Type = 0;
			}
			ServerSetShelfState(shelfIndex, nextState);
			ClientPlayTrayTransferSfxRpc(isTake: true, rpcParams3);
		}

		[Rpc(SendTo.Server)]
		public void ServerReturnBeverageFromTrayRpc(byte shelfIndex, NetworkObjectReference interactorRef, NetworkObjectReference trayRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1261597026u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in shelfIndex, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in trayRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 1261597026u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			RpcParams rpcParams3 = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
			if (!IsPhysicallyUsable)
			{
				ClientShowTrayTransferToastRpc("This fridge must be repaired first.", rpcParams3);
				return;
			}
			if (!_isDoorOpen.Value)
			{
				ClientShowTrayTransferToastRpc("Open the fridge door first.", rpcParams3);
				return;
			}
			if (!interactorRef.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != senderClientId || !trayRef.TryGetComponent<Tray>(out var component2) || component2.OwnerClientId != senderClientId || component.CurrentHeld != component2 || component2.CanHold.Value || !TryGetShelf(shelfIndex, out var shelf))
			{
				ClientShowTrayTransferToastRpc("This action cannot be completed.", rpcParams3);
				return;
			}
			if (!component2.TryGetBeverageDish(out var beverage) || !(beverage.ItemSO is BeverageSO { BeverageType: var beverageType } beverageSO))
			{
				ClientShowTrayTransferToastRpc("This item does not belong in this container.", rpcParams3);
				return;
			}
			BeverageShelfState beverageShelfState = _shelfStates[shelfIndex];
			if (shelf.GetBeverageSO(beverageType) == null || (beverageShelfState.Count > 0 && (uint)beverageShelfState.Type != (uint)beverageType))
			{
				ClientShowTrayTransferToastRpc("Different beverage already placed.", rpcParams3);
				return;
			}
			if (beverageShelfState.Count >= shelf.GetCapacityFor(beverageType))
			{
				ClientShowTrayTransferToastRpc("Shelf full.", rpcParams3);
				return;
			}
			if (!component2.ServerTryRemoveOptionalDishForWholesaleReturn(beverageSO.Id, out var dish))
			{
				ClientShowTrayTransferToastRpc("This item does not belong in this container.", rpcParams3);
				return;
			}
			if (!ServerTryFillAtShelf(shelfIndex, beverageType))
			{
				ClientShowTrayTransferToastRpc("Shelf full.", rpcParams3);
				return;
			}
			dish.Destroy();
			component2.ServerRefreshHeldTrayContentsGraphics();
			ClientPlayTrayTransferSfxRpc(isTake: false, rpcParams3);
		}

		[Rpc(SendTo.Server)]
		public void ServerPlaceLooseBeverageRpc(byte shelfIndex, byte beverageType, NetworkObjectReference interactorRef, NetworkObjectReference heldRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2693575450u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in shelfIndex, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in beverageType, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in heldRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 2693575450u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
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
			BeverageShelf shelf;
			if (!IsPhysicallyUsable)
			{
				UtilityMethods.Log("[BeverageFridge] Place blocked. Fridge is locked.", LogType.Warning);
			}
			else if (!_isDoorOpen.Value)
			{
				UtilityMethods.Log("<color=yellow>[BeverageFridge] Place blocked. Door is closed.</color>");
			}
			else if (TryGetShelf(shelfIndex, out shelf))
			{
				NetworkObject networkObject;
				BaseHoldable component2;
				ServedDishBeverage component3;
				if (!interactorRef.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != senderClientId)
				{
					UtilityMethods.Log($"[BeverageFridge] Place rejected. Interactor ownership mismatch. Sender={senderClientId}", LogType.Warning);
				}
				else if (!heldRef.TryGet(out networkObject) || !networkObject.TryGetComponent<BaseHoldable>(out component2))
				{
					UtilityMethods.Log("[BeverageFridge] Place failed. Held object is null.", LogType.Error);
				}
				else if (component2.OwnerClientId != senderClientId || component.CurrentHeld != component2 || component2.CanHold.Value)
				{
					UtilityMethods.Log($"[BeverageFridge] Place rejected. Beverage is not held by sender. Sender={senderClientId}", LogType.Warning);
				}
				else if (component2.ItemSO is BeverageSO beverageSO && component2.TryGetComponent<ServedDishBeverage>(out component3) && (uint)beverageSO.BeverageType == beverageType && ServerTryFillAtShelf(shelfIndex, beverageSO.BeverageType))
				{
					RpcParams rpcParams3 = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
					ClientRemovePlacedLooseBeverageRpc(heldRef, rpcParams3);
					component2.Destroy();
				}
			}
		}

		public bool ServerTryFillAtShelf(byte shelfIndex, BeverageType type)
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (!IsPhysicallyUsable)
			{
				UtilityMethods.Log("[BeverageFridge] Fill blocked. Fridge is locked.", LogType.Warning);
				return false;
			}
			if (!_isDoorOpen.Value)
			{
				UtilityMethods.Log("<color=yellow>[BeverageFridge] Fill blocked. Door is closed.</color>");
				return false;
			}
			if (!IsValidType(type))
			{
				return false;
			}
			if (!TryGetShelf(shelfIndex, out var shelf))
			{
				return false;
			}
			BeverageShelfState nextState = _shelfStates[shelfIndex];
			BeverageType type2 = (BeverageType)nextState.Type;
			if (nextState.Count > 0 && type2 != type)
			{
				return false;
			}
			int capacityFor = shelf.GetCapacityFor(type);
			if (capacityFor <= 0)
			{
				return false;
			}
			if (nextState.Count >= capacityFor)
			{
				return false;
			}
			nextState.Type = (byte)type;
			nextState.Count++;
			ServerSetShelfState(shelfIndex, nextState);
			return true;
		}

		public bool ServerTryFill(BeverageType type)
		{
			if (!base.IsServer)
			{
				return false;
			}
			for (byte b = 0; b < _shelfList.Count; b++)
			{
				if (ServerTryFillAtShelf(b, type))
				{
					return true;
				}
			}
			return false;
		}

		private void ServerSetShelfState(int shelfIndex, BeverageShelfState nextState)
		{
			if (base.IsServer && shelfIndex >= 0 && shelfIndex < _shelfStates.Count)
			{
				BeverageShelfState state = _shelfStates[shelfIndex];
				if (!state.Equals(nextState))
				{
					ServerAdjustShelfStock(shelfIndex, state, -1);
					_shelfStates[shelfIndex] = nextState;
					ServerAdjustShelfStock(shelfIndex, nextState, 1);
				}
			}
		}

		private void ServerRemoveRegisteredShelfStock()
		{
			if (base.IsServer)
			{
				for (int i = 0; i < _shelfStates.Count; i++)
				{
					ServerAdjustShelfStock(i, _shelfStates[i], -1);
				}
			}
		}

		private void ServerAdjustShelfStock(int shelfIndex, BeverageShelfState state, int direction)
		{
			if (direction == 0 || state.Count <= 0 || state.Type == 0 || shelfIndex < 0 || shelfIndex >= _shelfList.Count)
			{
				return;
			}
			BeverageSO beverageSO = _shelfList[shelfIndex]?.GetBeverageSO((BeverageType)state.Type);
			if (beverageSO == null || string.IsNullOrEmpty(beverageSO.Id))
			{
				return;
			}
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (!(restaurantManager == null))
			{
				int num = state.Count * direction;
				if (num > 0)
				{
					restaurantManager.ServerAddStockRpc(beverageSO.Id, num);
				}
				else
				{
					restaurantManager.ServerRemoveStockRpc(beverageSO.Id, -num);
				}
			}
		}

		private bool TryGetShelf(byte shelfIndex, out BeverageShelf shelf)
		{
			shelf = null;
			if (shelfIndex >= _shelfList.Count)
			{
				return false;
			}
			shelf = _shelfList[shelfIndex];
			return shelf != null;
		}

		private void RebuildAllShelves()
		{
			for (int i = 0; i < _shelfList.Count; i++)
			{
				RebuildShelf(i);
			}
		}

		private void RebuildShelf(int index)
		{
			if (index < 0 || index >= _shelfList.Count)
			{
				return;
			}
			BeverageShelf beverageShelf = _shelfList[index];
			if (!(beverageShelf == null))
			{
				if (index >= _shelfStates.Count)
				{
					beverageShelf.RebuildVisuals(BeverageType.Empty, 0);
					return;
				}
				BeverageShelfState beverageShelfState = _shelfStates[index];
				beverageShelf.RebuildVisuals((BeverageType)beverageShelfState.Type, beverageShelfState.Count);
			}
		}

		public static bool IsValidType(BeverageType type)
		{
			if (type != BeverageType.Coke && type != BeverageType.Fanta && type != BeverageType.Sprite)
			{
				return type == BeverageType.Ramune;
			}
			return true;
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientRemovePlacedLooseBeverageRpc(NetworkObjectReference acceptedHeldRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(221414854u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in acceptedHeldRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 221414854u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
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

		public void PlayTakeSfx()
		{
			if (!(_audioSource == null) && !(_takeSfx == null))
			{
				_audioSource.PlayOneShot(_takeSfx);
			}
		}

		public void PlayPlaceSfx()
		{
			if (!(_audioSource == null) && !(_placeSfx == null))
			{
				_audioSource.PlayOneShot(_placeSfx);
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientShowTrayTransferToastRpc(string message, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2277855019u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bool value = message != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(message);
				}
				__endSendRpc(ref bufferWriter, 2277855019u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Singleton<NotifyManager>.Instance?.ShowToast(message, NotificationState.Error);
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientPlayTrayTransferSfxRpc(bool isTake, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(4118145354u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in isTake, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 4118145354u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (isTake)
				{
					PlayTakeSfx();
				}
				else
				{
					PlayPlaceSfx();
				}
			}
		}

		public bool TryUnlockPhysicalState(Interactor interactor)
		{
			if (_physicalUnlockController != null)
			{
				return _physicalUnlockController.TryUnlock(interactor);
			}
			return false;
		}

		public bool TryUnlockPhysicalStateAndOpenTutorial(Interactor interactor)
		{
			if (interactor == null)
			{
				return false;
			}
			if (IsPhysicallyUsable)
			{
				return false;
			}
			return TryUnlockPhysicalState(interactor);
		}

		public void TryOpenTutorialForFirstDoorOpen(Interactor interactor)
		{
			if (!(interactor == null) && PlayerPrefs.GetInt("StationTutorial.BeverageFridge.Seen", 0) != 1 && !(Singleton<GameUIManager>.Instance == null) && !(Singleton<GameUIManager>.Instance.StationTutorialPopupUI == null))
			{
				PlayerPrefs.SetInt("StationTutorial.BeverageFridge.Seen", 1);
				PlayerPrefs.Save();
				Singleton<GameUIManager>.Instance.StationTutorialPopupUI.OpenAfterRepair(_beverageFridgeTutorialData, interactor.Player);
			}
		}

		public void ShowUnlockTooltipLocal()
		{
			_physicalUnlockController?.ShowTooltipLocal();
		}

		public void HideUnlockTooltipLocal()
		{
			_physicalUnlockController?.HideTooltipLocal();
		}

		private void CacheLoopEngineVolume()
		{
			if (!(_loopEngineSource == null) && !_loopEngineVolumeCached)
			{
				_defaultLoopEngineVolume = _loopEngineSource.volume;
				_loopEngineVolumeCached = true;
			}
		}

		private void UpdateLoopEngineVolume(bool doorOpen)
		{
			if (!(_loopEngineSource == null))
			{
				CacheLoopEngineVolume();
				float volume = (doorOpen ? (_defaultLoopEngineVolume * _doorOpenLoopVolumeMultiplier) : _defaultLoopEngineVolume);
				_loopEngineSource.volume = volume;
			}
		}

		public List<byte> Save()
		{
			List<byte> list = new List<byte>
			{
				2,
				(byte)_shelfList.Count
			};
			for (int i = 0; i < _shelfList.Count; i++)
			{
				BeverageShelfState beverageShelfState = ((i < _shelfStates.Count) ? _shelfStates[i] : new BeverageShelfState(0, 0));
				list.Add(beverageShelfState.Type);
				list.Add(beverageShelfState.Count);
			}
			return list;
		}

		public void Load(List<byte> beverages)
		{
			if (base.IsServer)
			{
				EnsureShelfStates();
				if (beverages == null || beverages.Count == 0)
				{
					ResetAllShelfStates();
				}
				else if (beverages[0] == 2)
				{
					LoadShelfBasedSave(beverages);
				}
				else
				{
					LoadLegacyTypeBasedSave(beverages);
				}
			}
		}

		private void LoadShelfBasedSave(List<byte> data)
		{
			ResetAllShelfStates();
			if (data.Count < 2)
			{
				return;
			}
			int num = data[1];
			int num2 = 2;
			for (int i = 0; i < num && i < _shelfList.Count; i++)
			{
				if (num2 + 1 >= data.Count)
				{
					break;
				}
				byte b = data[num2++];
				byte b2 = data[num2++];
				BeverageType type = (BeverageType)b;
				if (b2 <= 0 || !IsValidType(type))
				{
					ServerSetShelfState(i, new BeverageShelfState(0, 0));
					continue;
				}
				int capacityFor = _shelfList[i].GetCapacityFor(type);
				byte count = (byte)Mathf.Clamp(b2, 0, capacityFor);
				ServerSetShelfState(i, new BeverageShelfState(b, count));
			}
		}

		private void LoadLegacyTypeBasedSave(List<byte> data)
		{
			ResetAllShelfStates();
			byte count = (byte)((data.Count > 0) ? data[0] : 0);
			byte count2 = (byte)((data.Count > 1) ? data[1] : 0);
			byte count3 = (byte)((data.Count > 2) ? data[2] : 0);
			byte count4 = (byte)((data.Count > 3) ? data[3] : 0);
			DistributeLegacyCount(BeverageType.Coke, count);
			DistributeLegacyCount(BeverageType.Fanta, count2);
			DistributeLegacyCount(BeverageType.Sprite, count3);
			DistributeLegacyCount(BeverageType.Ramune, count4);
		}

		private void DistributeLegacyCount(BeverageType type, byte count)
		{
			int num = count;
			for (int i = 0; i < _shelfList.Count; i++)
			{
				if (num <= 0)
				{
					break;
				}
				if (_shelfStates[i].Count <= 0)
				{
					int capacityFor = _shelfList[i].GetCapacityFor(type);
					if (capacityFor > 0)
					{
						int num2 = Mathf.Min(num, capacityFor);
						ServerSetShelfState(i, new BeverageShelfState((byte)type, (byte)num2));
						num -= num2;
					}
				}
			}
		}

		private void ResetAllShelfStates()
		{
			for (int i = 0; i < _shelfStates.Count; i++)
			{
				ServerSetShelfState(i, new BeverageShelfState(0, 0));
			}
		}

		private int StaffShelfSpace(int index, BeverageSO item)
		{
			BeverageShelf beverageShelf = _shelfList[index];
			BeverageShelfState beverageShelfState = _shelfStates[index];
			if (beverageShelf == null || beverageShelf.GetBeverageSO(item.BeverageType) != item || (beverageShelfState.Count > 0 && (uint)beverageShelfState.Type != (uint)item.BeverageType))
			{
				return 0;
			}
			return Mathf.Max(0, Mathf.Min(255, beverageShelf.GetCapacityFor(item.BeverageType)) - beverageShelfState.Count);
		}

		public int ServerStaffSpace(BeverageSO item)
		{
			if (!base.IsServer || !base.IsSpawned || !IsPhysicallyUsable || item == null)
			{
				return 0;
			}
			int num = 0;
			for (int i = 0; i < _shelfStates.Count; i++)
			{
				num += StaffShelfSpace(i, item);
			}
			return num;
		}

		public int ServerStaffStock(int employeeId, WholesaleEntity pack)
		{
			if (pack == null || !pack.IsSpawned || !pack.ServerStaffCarriedBy(employeeId) || !(pack.WholesaleSO?.OutputItem is BeverageSO beverageSO) || ServerStaffSpace(beverageSO) == 0)
			{
				return 0;
			}
			int num = 0;
			for (int i = 0; i < 2; i++)
			{
				for (int j = 0; j < _shelfStates.Count; j++)
				{
					if (pack.CurrentCount <= 0)
					{
						break;
					}
					BeverageShelfState nextState = _shelfStates[j];
					if (nextState.Count > 0 == (i == 0))
					{
						int num2 = Mathf.Min(pack.CurrentCount, StaffShelfSpace(j, beverageSO));
						if (num2 > 0 && pack.ServerTryConsumeAmount((byte)num2, out var consumedAmount))
						{
							nextState.Type = (byte)beverageSO.BeverageType;
							nextState.Count += consumedAmount;
							ServerSetShelfState(j, nextState);
							num += consumedAmount;
						}
					}
				}
			}
			return num;
		}

		public void ServerStaffAccumulateStock(Dictionary<string, float> stock)
		{
			if (!base.IsServer || !base.IsSpawned)
			{
				return;
			}
			for (int i = 0; i < _shelfStates.Count; i++)
			{
				BeverageShelfState beverageShelfState = _shelfStates[i];
				if (beverageShelfState.Count != 0 && !(_shelfList[i] == null))
				{
					BeverageSO beverageSO = _shelfList[i].GetBeverageSO((BeverageType)beverageShelfState.Type);
					if (!(beverageSO == null))
					{
						stock.TryGetValue(beverageSO.Id, out var value);
						stock[beverageSO.Id] = value + (float)(int)beverageShelfState.Count;
					}
				}
			}
		}

		public int ServerStaffShelf(ServedDishSO item)
		{
			if (!base.IsServer || !base.IsSpawned || !IsPhysicallyUsable || item == null)
			{
				return -1;
			}
			for (int i = 0; i < _shelfStates.Count; i++)
			{
				if (_shelfStates[i].Count > 0 && _shelfList[i] != null && _shelfList[i].GetBeverageSO((BeverageType)_shelfStates[i].Type) == item)
				{
					return i;
				}
			}
			return -1;
		}

		public ServedDish ServerStaffTake(ServedDishSO item, Vector3 point)
		{
			int num = ServerStaffShelf(item);
			if (num < 0 || item.Prefab == null || item.Prefab.GetComponent<ServedDish>() == null)
			{
				return null;
			}
			NetworkObject networkObject = UnityEngine.Object.Instantiate(item.Prefab, point, Quaternion.identity);
			networkObject.Spawn(destroyWithScene: true);
			BeverageShelfState nextState = _shelfStates[num];
			nextState.Count--;
			if (nextState.Count == 0)
			{
				nextState.Type = 0;
			}
			ServerSetShelfState(num, nextState);
			return networkObject.GetComponent<ServedDish>();
		}

		protected override void __initializeVariables()
		{
			if (_shelfStates == null)
			{
				throw new Exception("BeverageFridge._shelfStates cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_shelfStates.Initialize(this);
			__nameNetworkVariable(_shelfStates, "_shelfStates");
			NetworkVariableFields.Add(_shelfStates);
			if (_isDoorOpen == null)
			{
				throw new Exception("BeverageFridge._isDoorOpen cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isDoorOpen.Initialize(this);
			__nameNetworkVariable(_isDoorOpen, "_isDoorOpen");
			NetworkVariableFields.Add(_isDoorOpen);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(2475595570u, __rpc_handler_2475595570, "ClientResetRuntimeStateRpc", RpcInvokePermission.Everyone);
			__registerRpc(3317748990u, __rpc_handler_3317748990, "ServerToggleDoorRpc", RpcInvokePermission.Everyone);
			__registerRpc(3868096947u, __rpc_handler_3868096947, "ServerTakeFromShelfRpc", RpcInvokePermission.Everyone);
			__registerRpc(1737710723u, __rpc_handler_1737710723, "ServerTakeFromShelfToTrayRpc", RpcInvokePermission.Everyone);
			__registerRpc(1261597026u, __rpc_handler_1261597026, "ServerReturnBeverageFromTrayRpc", RpcInvokePermission.Everyone);
			__registerRpc(2693575450u, __rpc_handler_2693575450, "ServerPlaceLooseBeverageRpc", RpcInvokePermission.Everyone);
			__registerRpc(221414854u, __rpc_handler_221414854, "ClientRemovePlacedLooseBeverageRpc", RpcInvokePermission.Everyone);
			__registerRpc(2277855019u, __rpc_handler_2277855019, "ClientShowTrayTransferToastRpc", RpcInvokePermission.Everyone);
			__registerRpc(4118145354u, __rpc_handler_4118145354, "ClientPlayTrayTransferSfxRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_2475595570(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((BeverageFridge)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BeverageFridge)target).ClientResetRuntimeStateRpc();
				((BeverageFridge)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3317748990(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((BeverageFridge)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BeverageFridge)target).ServerToggleDoorRpc();
				((BeverageFridge)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3868096947(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((BeverageFridge)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BeverageFridge)target).ServerTakeFromShelfRpc(value, value2, ext);
				((BeverageFridge)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1737710723(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value3, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((BeverageFridge)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BeverageFridge)target).ServerTakeFromShelfToTrayRpc(value, value2, value3, ext);
				((BeverageFridge)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1261597026(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value3, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((BeverageFridge)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BeverageFridge)target).ServerReturnBeverageFromTrayRpc(value, value2, value3, ext);
				((BeverageFridge)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2693575450(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out NetworkObjectReference value3, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value4, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((BeverageFridge)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BeverageFridge)target).ServerPlaceLooseBeverageRpc(value, value2, value3, value4, ext);
				((BeverageFridge)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_221414854(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((BeverageFridge)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BeverageFridge)target).ClientRemovePlacedLooseBeverageRpc(value, ext);
				((BeverageFridge)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2277855019(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
				((BeverageFridge)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BeverageFridge)target).ClientShowTrayTransferToastRpc(s, ext);
				((BeverageFridge)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4118145354(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((BeverageFridge)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BeverageFridge)target).ClientPlayTrayTransferSfxRpc(value, ext);
				((BeverageFridge)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "BeverageFridge";
		}
	}
}
