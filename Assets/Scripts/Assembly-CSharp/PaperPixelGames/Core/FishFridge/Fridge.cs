using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Progress;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.Tutorials;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.FishFridge
{
	public class Fridge : BaseInteractable, ISaveable<FridgeData>
	{
		private const string FridgeTutorialSeenKey = "StationTutorial.FishFridge.Seen";

		[SerializeField]
		private List<FridgeFish> _fishStocks = new List<FridgeFish>();

		[SerializeField]
		private FridgeLid _fridgeLid;

		[SerializeField]
		private Collider _collider;

		[SerializeField]
		private PhysicalStateController _physicalStateController;

		[SerializeField]
		private PhysicalUnlockController _physicalUnlockController;

		[SerializeField]
		private StationTutorialData _fridgeTutorialData;

		[Space]
		[SerializeField]
		private AudioSource _oneShotSource;

		[SerializeField]
		private AudioClip _openClip;

		[SerializeField]
		private AudioClip _closeClip;

		[SerializeField]
		private AudioClip _insertClip;

		[SerializeField]
		private AudioClip _extractClip;

		private readonly List<FridgeStock> _stocks = new List<FridgeStock>();

		private readonly Dictionary<string, FridgeFish> _fishVisualMap = new Dictionary<string, FridgeFish>();

		public Collider Collider => _collider;

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

		public FridgeData Save()
		{
			List<FridgeFishData> list = new List<FridgeFishData>();
			for (int i = 0; i < _stocks.Count; i++)
			{
				FridgeStock stock = _stocks[i];
				if (IsValidStock(stock))
				{
					FridgeFishData fridgeFishData = list.Find((FridgeFishData x) => x.Id == stock.Id);
					if (fridgeFishData == null)
					{
						fridgeFishData = new FridgeFishData
						{
							Id = stock.Id,
							Weights = new List<float>()
						};
						list.Add(fridgeFishData);
					}
					if (stock.Weights != null && stock.Weights.Count > 0)
					{
						fridgeFishData.Weights.AddRange(stock.Weights);
					}
				}
			}
			return new FridgeData
			{
				FridgeFishes = list
			};
		}

		public void Load(FridgeData data)
		{
			if (base.IsServer)
			{
				ServerAdjustAllRestaurantStock(-1);
			}
			_stocks.Clear();
			if (data?.FridgeFishes != null)
			{
				for (int i = 0; i < data.FridgeFishes.Count; i++)
				{
					FridgeFishData fridgeFishData = data.FridgeFishes[i];
					if (fridgeFishData != null && !string.IsNullOrWhiteSpace(fridgeFishData.Id))
					{
						FridgeStock orCreateStock = GetOrCreateStock(fridgeFishData.Id);
						if (fridgeFishData.Weights != null && fridgeFishData.Weights.Count > 0)
						{
							orCreateStock.Weights.AddRange(fridgeFishData.Weights);
						}
					}
				}
			}
			RemoveInvalidOrEmptyStocks();
			if (base.IsServer)
			{
				ServerAdjustAllRestaurantStock(1);
			}
			RefreshAllFishStockStates();
		}

		protected override void Awake()
		{
			base.Awake();
			BuildFishVisualMap();
		}

		public override void OnNetworkDespawn()
		{
			if (base.IsServer)
			{
				ServerAdjustAllRestaurantStock(-1);
			}
			base.OnNetworkDespawn();
		}

		public override void EnableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
			Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_store_fish");
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
		}

		protected override bool CanCrossTrigger(Interactor interactor, BaseHoldable held)
		{
			if (base.CanCrossTrigger(interactor, held))
			{
				if (IsPhysicallyUsable)
				{
					return held is DeadFish;
				}
				return true;
			}
			return false;
		}

		protected override void ClientCrossMouse1(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				if (!IsPhysicallyUsable)
				{
					TryUnlockPhysicalStateAndOpenTutorial(interactor);
				}
				else if (held is DeadFish deadFish)
				{
                    if(SushiAndFish.FishingIntegration.FishingMigration.DepositFromNativeFridge(this,interactor))return;
					ServerAddStockRpc(deadFish.NetworkObject.ToReference(), interactor.NetworkObject.ToReference());
					PlayInsertSFX();
				}
			}
		}

		private void BuildFishVisualMap()
		{
			_fishVisualMap.Clear();
			for (int i = 0; i < _fishStocks.Count; i++)
			{
				FridgeFish fridgeFish = _fishStocks[i];
				if (fridgeFish == null)
				{
					continue;
				}
				if (fridgeFish.FishSO == null)
				{
					UtilityMethods.Log($"[Fridge] FishSO is missing at index {i}.", LogType.Warning);
					continue;
				}
				string id = fridgeFish.FishSO.Id;
				if (string.IsNullOrWhiteSpace(id))
				{
					UtilityMethods.Log($"[Fridge] FishSO.Id is null or empty at index {i}.", LogType.Warning);
					continue;
				}
				if (_fishVisualMap.ContainsKey(id))
				{
					UtilityMethods.Log("[Fridge] Duplicate FishSO.Id detected: " + id, LogType.Warning);
					continue;
				}
				_fishVisualMap.Add(id, fridgeFish);
				fridgeFish.Initialize(this, fridgeFish.FishSO);
				fridgeFish.RefreshStateLocal();
				fridgeFish.SetFridgeOpenStateLocal(isOpen: false);
			}
		}

		public void SetFridgeFishStatus(bool isOpen)
		{
			foreach (FridgeFish fishStock in _fishStocks)
			{
				if (!(fishStock == null))
				{
					fishStock.SetFridgeOpenStateLocal(isOpen);
				}
			}
		}

		public void RegisterLid(FridgeLid fridgeLid)
		{
			if (!(fridgeLid == null))
			{
				_fridgeLid = fridgeLid;
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
			if (IsPhysicallyUsable)
			{
				return false;
			}
			return TryUnlockPhysicalState(interactor);
		}

		public void TryOpenTutorialForFirstDoorOpen(Interactor interactor)
		{
			if (!(interactor == null) && PlayerPrefs.GetInt("StationTutorial.FishFridge.Seen", 0) != 1 && !(Singleton<GameUIManager>.Instance == null) && !(Singleton<GameUIManager>.Instance.StationTutorialPopupUI == null))
			{
				PlayerPrefs.SetInt("StationTutorial.FishFridge.Seen", 1);
				PlayerPrefs.Save();
				Singleton<GameUIManager>.Instance.StationTutorialPopupUI.OpenAfterRepair(_fridgeTutorialData, interactor.Player);
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

		public void ServerResetForEndDay()
		{
			if (base.IsServer)
			{
				RemoveInvalidOrEmptyStocks();
				RefreshAllFishStockStates();
				if (_fridgeLid != null)
				{
					_fridgeLid.ServerResetForEndDay();
				}
				else
				{
					ClientResetRuntimeStateRpc();
				}
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
				FastBufferWriter bufferWriter = __beginSendRpc(696634363u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 696634363u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				SetFridgeFishStatus(isOpen: false);
				if (_collider != null)
				{
					_collider.enabled = false;
				}
			}
		}

		private void RefreshAllFishStockStates()
		{
			if (!base.IsServer)
			{
				return;
			}
			for (int i = 0; i < _fishStocks.Count; i++)
			{
				FridgeFish fridgeFish = _fishStocks[i];
				if (!(fridgeFish == null) && !(fridgeFish.FishSO == null))
				{
					string id = fridgeFish.FishSO.Id;
					int stockCount = GetStockCount(id);
					fridgeFish.SetStockStateServer(stockCount);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerAddStockRpc(NetworkObjectReference fishRef, NetworkObjectReference interactorRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(12204307u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in fishRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 12204307u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!IsPhysicallyUsable)
			{
				UtilityMethods.Log("[Fridge] Add stock blocked. Fridge is locked.", LogType.Warning);
				return;
			}
			if (!fishRef.TryGet(out var networkObject) || networkObject == null)
			{
				UtilityMethods.Log("[Fridge] fishRef is invalid.", LogType.Warning);
				return;
			}
			if (!interactorRef.TryGet(out var networkObject2) || networkObject2 == null)
			{
				UtilityMethods.Log("[Fridge] interactorRef is invalid.", LogType.Warning);
				return;
			}
			if (!networkObject2.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != senderClientId)
			{
				UtilityMethods.Log("[Fridge] Interactor ownership validation failed.", LogType.Warning);
				return;
			}
			if (!networkObject.TryGetComponent<DeadFish>(out var component2))
			{
				UtilityMethods.Log("[Fridge] DeadFish component not found on fishRef.", LogType.Warning);
				return;
			}
			if (component2.OwnerClientId != senderClientId || component2.CanHold.Value || (object)component.CurrentHeld != component2)
			{
				UtilityMethods.Log("[Fridge] Held fish validation failed.", LogType.Warning);
				return;
			}
			if (component2.FishSO == null)
			{
				UtilityMethods.Log("[Fridge] DeadFish FishSO is null.", LogType.Warning);
				return;
			}
			string id = component2.FishSO.Id;
			if (string.IsNullOrWhiteSpace(id))
			{
				UtilityMethods.Log("[Fridge] DeadFish FishSO.Id is null or empty.", LogType.Warning);
				return;
			}
			if (!_fishVisualMap.TryGetValue(id, out var value) || value == null)
			{
				UtilityMethods.Log("[Fridge] FridgeFish mapping not found for fish id: " + id, LogType.Warning);
				return;
			}
			FridgeStock orCreateStock = GetOrCreateStock(id);
			orCreateStock.Weights.Add(component2.Weight.Value);
			ServerAdjustRestaurantStock(id, 1);
			value.SetStockStateServer(orCreateStock.Weights.Count);
			ClientFinalizeAddStockRpc(fishRef, base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp));
			if (networkObject.IsSpawned)
			{
				networkObject.Despawn();
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientFinalizeAddStockRpc(NetworkObjectReference fishRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(812611266u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in fishRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 812611266u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!(Singleton<QuickSlotHandler>.Instance == null) && fishRef.TryGetComponent<DeadFish>(out var component))
				{
					Singleton<QuickSlotHandler>.Instance.RemoveSpecificItem(component);
				}
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerRemoveStockRpc(FixedString64Bytes fixedId, NetworkObjectReference interactorRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2658514119u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in fixedId, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 2658514119u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!IsPhysicallyUsable)
			{
				UtilityMethods.Log("[Fridge] Remove stock blocked. Fridge is locked.", LogType.Warning);
				return;
			}
			string text = fixedId.ToString();
			if (string.IsNullOrWhiteSpace(text))
			{
				UtilityMethods.Log("[Fridge] Remove request failed. Fish id is null or empty.", LogType.Warning);
				return;
			}
			if (!interactorRef.TryGet(out var networkObject) || networkObject == null)
			{
				UtilityMethods.Log("[Fridge] Remove request failed. interactorRef is invalid.", LogType.Warning);
				return;
			}
			if (!networkObject.TryGetComponent<Interactor>(out var component))
			{
				UtilityMethods.Log("[Fridge] Remove request failed. Interactor component not found.", LogType.Warning);
				return;
			}
			FridgeStock fridgeStock = FindStock(text);
			if (fridgeStock == null || fridgeStock.Weights == null || fridgeStock.Weights.Count == 0)
			{
				UtilityMethods.Log("[Fridge] Remove request failed. No stock for fish id: " + text, LogType.Warning);
				return;
			}
			FishSO fishById = Singleton<SeaCreatureManager>.Instance.GetFishById(text);
			if (fishById == null)
			{
				UtilityMethods.Log("[Fridge] FishSO not found for id: " + text, LogType.Warning);
				return;
			}
			DeadFish deadFish = Singleton<SeaCreatureManager>.Instance.SpawnDeadFish(fishById, base.transform.position, component.OwnerClientId, interactorRef, select: true, canHold: true);
			if (deadFish == null)
			{
				UtilityMethods.Log("[Fridge] DeadFish could not be spawned for id: " + text, LogType.Warning);
				return;
			}
			int index = fridgeStock.Weights.Count - 1;
			float value = fridgeStock.Weights[index];
			fridgeStock.Weights.RemoveAt(index);
			ServerAdjustRestaurantStock(text, -1);
			bool num = fridgeStock.Weights.Count > 0;
			if (_fishVisualMap.TryGetValue(text, out var value2) && value2 != null)
			{
				value2.SetStockStateServer(fridgeStock.Weights.Count);
			}
			if (!num)
			{
				RemoveStockIfEmpty(fridgeStock);
			}
			deadFish.Weight.Value = value;
		}

        public bool TryPeekSharedFish(string id,out float weight){weight=0;var stock=FindStock(id);if(!IsServer||stock?.Weights==null||stock.Weights.Count==0)return false;weight=stock.Weights[stock.Weights.Count-1];return true;}
        public bool TakeSharedFish(string id){if(!TryPeekSharedFish(id,out _))return false;var stock=FindStock(id);stock.Weights.RemoveAt(stock.Weights.Count-1);ServerAdjustRestaurantStock(id,-1);if(_fishVisualMap.TryGetValue(id,out var visual)&&visual)visual.SetStockStateServer(stock.Weights.Count);RemoveStockIfEmpty(stock);return true;}
        public bool StoreSharedFish(DeadFish fish,Interactor interactor){
            if(!IsServer||!fish||!fish.IsSpawned||!fish.FishSO||!interactor||interactor.CurrentHeld!=fish||fish.CanHold.Value||fish.OwnerClientId!=interactor.OwnerClientId)return false;
            string id=fish.FishSO.Id;
            fish.ServerDeregisterInventoryStock();GetOrCreateStock(id).Weights.Add(fish.Weight.Value);ServerAdjustRestaurantStock(id,1);
            if(_fishVisualMap.TryGetValue(id,out var visual)&&visual)visual.SetStockStateServer(FindStock(id).Weights.Count);
            ClientFinalizeAddStockRpc(fish.NetworkObject.ToReference(),base.RpcTarget.Single(interactor.OwnerClientId,RpcTargetUse.Temp));
            fish.NetworkObject.Despawn();return true;
        }

		public ushort GetStockCount(string fishId)
		{
			if (string.IsNullOrWhiteSpace(fishId))
			{
				return 0;
			}
			FridgeStock fridgeStock = FindStock(fishId);
			if (fridgeStock == null || fridgeStock.Weights == null)
			{
				return 0;
			}
			return (ushort)System.Math.Min(ushort.MaxValue,fridgeStock.Weights.Count);
		}

		private FridgeStock FindStock(string fishId)
		{
			if (string.IsNullOrWhiteSpace(fishId))
			{
				return null;
			}
			return _stocks.Find((FridgeStock x) => x != null && x.Id == fishId);
		}

		private FridgeStock GetOrCreateStock(string fishId)
		{
			FridgeStock fridgeStock = FindStock(fishId);
			if (fridgeStock != null)
			{
				return fridgeStock;
			}
			fridgeStock = new FridgeStock
			{
				Id = fishId,
				Weights = new List<float>()
			};
			_stocks.Add(fridgeStock);
			return fridgeStock;
		}

		private bool IsValidStock(FridgeStock stock)
		{
			if (stock != null)
			{
				return !string.IsNullOrWhiteSpace(stock.Id);
			}
			return false;
		}

		private void RemoveStockIfEmpty(FridgeStock stock)
		{
			if (stock != null && (stock.Weights == null || stock.Weights.Count <= 0))
			{
				_stocks.Remove(stock);
			}
		}

		private void RemoveInvalidOrEmptyStocks()
		{
			for (int num = _stocks.Count - 1; num >= 0; num--)
			{
				FridgeStock fridgeStock = _stocks[num];
				if (fridgeStock == null || string.IsNullOrWhiteSpace(fridgeStock.Id))
				{
					_stocks.RemoveAt(num);
				}
				else if (fridgeStock.Weights == null || fridgeStock.Weights.Count == 0)
				{
					_stocks.RemoveAt(num);
				}
			}
		}

		private void ServerAdjustAllRestaurantStock(int direction)
		{
			if (!base.IsServer || direction == 0)
			{
				return;
			}
			for (int i = 0; i < _stocks.Count; i++)
			{
				FridgeStock fridgeStock = _stocks[i];
				if (IsValidStock(fridgeStock) && fridgeStock.Weights != null && fridgeStock.Weights.Count != 0)
				{
					ServerAdjustRestaurantStock(fridgeStock.Id, fridgeStock.Weights.Count * direction);
				}
			}
		}

		private void ServerAdjustRestaurantStock(string fishId, int amount)
		{
			if (!base.IsServer || string.IsNullOrWhiteSpace(fishId) || amount == 0)
			{
				return;
			}
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (!(restaurantManager == null))
			{
				if (amount > 0)
				{
					restaurantManager.ServerAddStockRpc(fishId, amount);
				}
				else
				{
					restaurantManager.ServerRemoveStockRpc(fishId, -amount);
				}
			}
		}

		public void PlayOpenSFX()
		{
			_oneShotSource.PlayOneShot(_openClip);
		}

		public void PlayCloseSFX()
		{
			_oneShotSource.PlayOneShot(_closeClip);
		}

		public void PlayInsertSFX()
		{
			_oneShotSource.PlayOneShot(_insertClip);
		}

		public void PlayExtractSFX()
		{
			_oneShotSource.PlayOneShot(_extractClip);
		}

		public bool ServerStaffHasFish(FishSO fish)
		{
			if (base.IsServer && base.IsSpawned && fish != null && _physicalStateController != null && _physicalStateController.IsPhysicallyUsable)
			{
				return _stocks.Any((FridgeStock s) => s.Id == fish.Id && s.Weights.Count > 0);
			}
			return false;
		}

		public DeadFish ServerStaffTakeFish(FishSO fish, int employeeId, Vector3 position)
		{
			if (!ServerStaffHasFish(fish) || fish.DeadPrefab == null)
			{
				return null;
			}
			FridgeStock fridgeStock = _stocks.First((FridgeStock s) => s.Id == fish.Id && s.Weights.Count > 0);
			NetworkObject networkObject = Object.Instantiate(fish.DeadPrefab, position, Quaternion.identity);
			DeadFish component = networkObject.GetComponent<DeadFish>();
			if (component == null)
			{
				Object.Destroy(networkObject.gameObject);
				return null;
			}
			component.FishId.Value = fish.Id;
			networkObject.Spawn(destroyWithScene: true);
			if (component == null || !component.ServerTryStaffPickup(employeeId))
			{
				networkObject.Despawn();
				return null;
			}
			int index = fridgeStock.Weights.Count - 1;
			component.Weight.Value = fridgeStock.Weights[index];
			fridgeStock.Weights.RemoveAt(index);
			ServerAdjustRestaurantStock(fish.Id, -1);
			if (_fishVisualMap.TryGetValue(fish.Id, out var value) && value != null)
			{
				value.SetStockStateServer(fridgeStock.Weights.Count);
			}
			RemoveStockIfEmpty(fridgeStock);
			return component;
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(696634363u, __rpc_handler_696634363, "ClientResetRuntimeStateRpc", RpcInvokePermission.Everyone);
			__registerRpc(12204307u, __rpc_handler_12204307, "ServerAddStockRpc", RpcInvokePermission.Everyone);
			__registerRpc(812611266u, __rpc_handler_812611266, "ClientFinalizeAddStockRpc", RpcInvokePermission.Everyone);
			__registerRpc(2658514119u, __rpc_handler_2658514119, "ServerRemoveStockRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_696634363(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((Fridge)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Fridge)target).ClientResetRuntimeStateRpc();
				((Fridge)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_12204307(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((Fridge)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Fridge)target).ServerAddStockRpc(value, value2, ext);
				((Fridge)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_812611266(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((Fridge)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Fridge)target).ClientFinalizeAddStockRpc(value, ext);
				((Fridge)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2658514119(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				((Fridge)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Fridge)target).ServerRemoveStockRpc(value, value2);
				((Fridge)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "Fridge";
		}
	}
}
