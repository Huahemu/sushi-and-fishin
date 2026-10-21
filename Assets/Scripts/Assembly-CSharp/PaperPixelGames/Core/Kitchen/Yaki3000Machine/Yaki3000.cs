using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.ProcessedIngredient;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Kitchen.Yaki3000Machine
{
	public class Yaki3000 : BaseUsable
	{
		[SerializeField]
		private GameObject _objToolTipGO;

		[Header("References")]
		[SerializeField]
		private ItemDatabaseSO _itemDatabaseSO;

		[SerializeField]
		private Yaki3000Canvas _yaki3000Canvas;

		[Header("Ghost Objects")]
		[SerializeField]
		private GameObject _tamagoyakiGhost;

		[SerializeField]
		private GameObject _taiyakiGhost;

		[SerializeField]
		private GameObject _dorayakiGhost;

		[SerializeField]
		private GameObject _dangoGhost;

		[Header("Timing")]
		[SerializeField]
		private float _preparationTime = 5f;

		private readonly NetworkVariable<byte> _selectedProductType = new NetworkVariable<byte>(0);

		private readonly NetworkVariable<byte> _currentState = new NetworkVariable<byte>(0);

		private readonly NetworkVariable<float> _preparationTimeRemaining = new NetworkVariable<float>(0f);

		private readonly NetworkList<IngredientEntry> _machineIngredientEntries = new NetworkList<IngredientEntry>();

		private readonly NetworkVariable<FixedString64Bytes> _dessertSO_ID = new NetworkVariable<FixedString64Bytes>();

		private readonly NetworkList<FixedString64Bytes> _ingredientStack = new NetworkList<FixedString64Bytes>();

		private List<Yaki3000IngredientSO> _cachedDesserts = new List<Yaki3000IngredientSO>();

		private Yaki3000IngredientSO _dessertSO;

		private Coroutine _preparationRoutine;

		[SerializeField]
		private Transform _ghostExitPoint;

		[SerializeField]
		private Transform _ghostDropPoint;

		private float _ghostPopDuration = 0.25f;

		private float _ghostDropDuration = 0.35f;

		private float _ghostPopHeight = 0.12f;

		private Tween _ghostTween;

		public ushort CurrentState => _currentState.Value;

		public ItemDatabaseSO ItemDatabaseSO => _itemDatabaseSO;

		public string DessertSO_ID => _dessertSO_ID.Value.ToString();

		public NetworkList<IngredientEntry> MachineIngredientsEntries => _machineIngredientEntries;

		public List<Yaki3000IngredientSO> CaschedDesserts => _cachedDesserts;

		public Yaki3000State State => (Yaki3000State)_currentState.Value;

		public Yaki3000Product SelectedProduct => (Yaki3000Product)_selectedProductType.Value;

		public event Action<byte, byte> NetStateChanged;

		public event Action<FixedString64Bytes, FixedString64Bytes> NetDessertIdChanged;

		public event Action<NetworkListEvent<IngredientEntry>> NetIngredientsChanged;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			CacheDesserts();
			_yaki3000Canvas.Initiazlie();
			NetworkVariable<byte> currentState = _currentState;
			currentState.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(currentState.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(HandleStateChanged));
			NetworkVariable<FixedString64Bytes> dessertSO_ID = _dessertSO_ID;
			dessertSO_ID.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Combine(dessertSO_ID.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(HandleDessertIdChanged));
			_machineIngredientEntries.OnListChanged += HandleIngredientsChanged;
		}

		public override void OnNetworkDespawn()
		{
			base.OnNetworkDespawn();
			KillGhostTween();
			NetworkVariable<byte> currentState = _currentState;
			currentState.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(currentState.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(HandleStateChanged));
			NetworkVariable<FixedString64Bytes> dessertSO_ID = _dessertSO_ID;
			dessertSO_ID.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Remove(dessertSO_ID.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(HandleDessertIdChanged));
			_machineIngredientEntries.OnListChanged -= HandleIngredientsChanged;
		}

		private void CacheDesserts()
		{
			if (!(_itemDatabaseSO == null))
			{
				_cachedDesserts = _itemDatabaseSO.GetAllOfType(ItemType.ServedDish).OfType<Yaki3000IngredientSO>().ToList();
			}
		}

		public override void ServerUseEnter(Interactor interactor)
		{
			base.ServerUseEnter(interactor);
			if (base.NetworkObject.OwnerClientId != interactor.OwnerClientId)
			{
				base.NetworkObject.ChangeOwnership(interactor.OwnerClientId);
			}
		}

		public override void ServerUseExit(Interactor interactor)
		{
			base.ServerUseExit(interactor);
			base.NetworkObject.RemoveOwnership();
		}

		public override void ClientUseEnter(Interactor interactor)
		{
			base.ClientUseEnter(interactor);
			interactor.Player.SwitchTo("PrepStation", cursorVisible: true);
			interactor.Player.ForKitchenUsable(value: false);
		}

		public override void ClientUseExit(Interactor interactor)
		{
			base.ClientUseExit(interactor);
			interactor.Player.SwitchTo("Player", cursorVisible: false);
			interactor.Player.ForKitchenUsable(value: true);
		}

		protected override bool CanCrossTrigger(Interactor interactor, BaseHoldable held)
		{
			if (CanInteract.Value)
			{
				return CanCross.Value;
			}
			return false;
		}

		protected override void ClientCrossInteract(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				base.ClientCrossInteract(interactor, held, phase);
			}
		}

		protected override void ClientCrossMouse1(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			base.ClientCrossMouse1(interactor, held, phase);
			if (phase != InputActionPhase.Started)
			{
				return;
			}
			BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(interactor.OwnerClientId, RpcTargetUse.Temp);
			if (held == null || held.NetworkObject == null || held.ItemSO == null)
			{
				ClientShowNotifyRpc("Info", "You must be holding an ingredient to add it to the machine.", baseRpcTarget);
				return;
			}
			switch (State)
			{
			case Yaki3000State.Cooking:
				ClientShowNotifyRpc("Info", "The machine is cooking. Please wait.", baseRpcTarget);
				break;
			case Yaki3000State.Cooked:
				ClientShowNotifyRpc("Info", "The product is ready. Take it out first (Right Click).", baseRpcTarget);
				break;
			case Yaki3000State.Empty:
				ClientShowNotifyRpc("Info", "Select a recipe first.", baseRpcTarget);
				break;
			default:
				ClientShowNotifyRpc("Info", "This action is not available right now.", baseRpcTarget);
				break;
			case Yaki3000State.Preparing:
			{
				if (_dessertSO_ID.Value.IsEmpty)
				{
					ClientShowNotifyRpc("Info", "Select a recipe first.", baseRpcTarget);
					break;
				}
				Yaki3000IngredientSO yaki3000IngredientSO = _itemDatabaseSO.FindById(_dessertSO_ID.Value.ToString()) as Yaki3000IngredientSO;
				if (yaki3000IngredientSO == null || yaki3000IngredientSO.Recipe == null)
				{
					ClientShowNotifyRpc("Info", "Select a recipe first.", baseRpcTarget);
					break;
				}
				if (held.TryGetComponent<DispenserIngredientEntity>(out var _))
				{
					ServerDispenseIntoMachineRpc(held.NetworkObject.ToReference());
					break;
				}
				HoldableSO itemSO = held.ItemSO;
				IngredientSO ingredientSO = itemSO as IngredientSO;
				if ((object)ingredientSO == null)
				{
					ClientShowNotifyRpc("Info", "Only ingredients can be added to the machine.", baseRpcTarget);
					break;
				}
				bool num = yaki3000IngredientSO.Recipe.Ingredients != null && yaki3000IngredientSO.Recipe.Ingredients.Any((IngredientNeeds n) => n.IngredientId == ingredientSO.Id);
				bool flag = yaki3000IngredientSO.Recipe.ProcessedIngredients != null && yaki3000IngredientSO.Recipe.ProcessedIngredients.Any((ProcessedIngredientNeeds n) => n.IngredientId == ingredientSO.Id);
				if (!num && !flag)
				{
					ClientShowNotifyRpc("Info", "This ingredient isn’t part of the selected recipe.", baseRpcTarget);
				}
				else
				{
					ServerTryAddHeldIngredientRpc(interactor.NetworkObject.ToReference(), held.NetworkObject.ToReference());
				}
				break;
			}
			}
		}

		protected override void ClientMouse2(Interactor interactor, InputActionPhase phase)
		{
			base.ClientMouse2(interactor, phase);
			if (phase == InputActionPhase.Started)
			{
				ServerMouse2Rpc(interactor.NetworkObject.ToReference());
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerTryStartCookingRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3314377980u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3314377980u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp);
				if (_dessertSO == null)
				{
					ClientShowNotifyRpc("Info", "No product selected. Cooking cannot start.", baseRpcTarget);
					return;
				}
				if (State != Yaki3000State.Preparing)
				{
					ClientShowNotifyRpc("Info", "Cooking cannot start right now.", baseRpcTarget);
					return;
				}
				if (!IsRecipeComplete(_dessertSO))
				{
					ClientShowNotifyRpc("Info", "Recipe is not complete. Cooking cannot start.", baseRpcTarget);
					return;
				}
				if (_preparationRoutine != null)
				{
					ClientShowNotifyRpc("Info", "Cooking is already running.", baseRpcTarget);
					return;
				}
				ClientShowNotifyRpc("Success", "Cooking started!", baseRpcTarget);
				ServerStartCooking();
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerMouse2Rpc(NetworkObjectReference interactorRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(250722202u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 250722202u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!interactorRef.TryGetComponent<Interactor>(out var component))
			{
				UtilityMethods.Log("[Yaki3000] ServerMouse2Rpc failed!");
				return;
			}
			switch (State)
			{
			case Yaki3000State.Cooked:
				ServerGiveCookedProduct(component);
				break;
			case Yaki3000State.Cooking:
				ClientShowNotifyRpc("Info", "The machine is cooking. Please wait.", base.RpcTarget.Single(component.OwnerClientId, RpcTargetUse.Temp));
				break;
			default:
				ServerTryGiveBackLastIngredient(component);
				break;
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTryAddHeldIngredientRpc(NetworkObjectReference interactorRef, NetworkObjectReference heldRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1915117012u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in heldRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 1915117012u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!interactorRef.TryGetComponent<Interactor>(out var component))
			{
				UtilityMethods.Log("[Yaki3000] ServerMouse2Rpc failed!");
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
			if (State != Yaki3000State.Preparing)
			{
				ClientShowNotifyRpc("Info", "You can’t add items right now.", baseRpcTarget);
			}
			else if (_dessertSO == null || _dessertSO.Recipe == null)
			{
				ClientShowNotifyRpc("Info", "You need to select a recipe first.", baseRpcTarget);
			}
			else
			{
				if (!heldRef.TryGetComponent<BaseHoldable>(out var component2) || component2 == null || component2.ItemSO == null)
				{
					return;
				}
				if (component.OwnerClientId != senderClientId || component2.OwnerClientId != senderClientId || component2.CanHold.Value || (object)component.CurrentHeld != component2)
				{
					UtilityMethods.Log("[Yaki3000] Held ingredient validation failed.", LogType.Warning);
					return;
				}
				if (!(component2.ItemSO is IngredientSO ingredientSO))
				{
					ClientShowNotifyRpc("Info", "This item isn’t an ingredient.", baseRpcTarget);
					return;
				}
				if (!IsIngredientInRecipe(_dessertSO, ingredientSO.Id))
				{
					ClientShowNotifyRpc("Info", "This ingredient isn’t part of the recipe.", baseRpcTarget);
					return;
				}
				FixedString64Bytes id = new FixedString64Bytes(ingredientSO.Id);
				ServerUpsertIngredientEntry(id);
				ServerPushIngredientStack(id);
				if (IsRecipeComplete(_dessertSO))
				{
					_yaki3000Canvas.ClientNotifyRecipeCompletedRpc();
				}
				component.ClientDestroyHeldItemRpc(heldRef);
			}
		}

		private void ServerUpsertIngredientEntry(FixedString64Bytes id)
		{
			for (int i = 0; i < _machineIngredientEntries.Count; i++)
			{
				if (_machineIngredientEntries[i].IngredientID.Equals(id))
				{
					if (_machineIngredientEntries[i].Count == byte.MaxValue)
					{
						ClientShowNotifyRpc("Info", "Too many of this ingredient in the machine.");
						return;
					}
					IngredientEntry value = _machineIngredientEntries[i];
					value.Count++;
					_machineIngredientEntries[i] = value;
					ClientUpdateIngredientSlotUIRPC(id, value.Count);
					return;
				}
			}
			_machineIngredientEntries.Add(new IngredientEntry(id, 1));
			ClientUpdateIngredientSlotUIRPC(id, 1);
		}

		[Rpc(SendTo.Server)]
		private void ServerDispenseIntoMachineRpc(NetworkObjectReference dispenserRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1245230626u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in dispenserRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 1245230626u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!dispenserRef.TryGetComponent<DispenserIngredientEntity>(out var component) || State != Yaki3000State.Preparing)
			{
				return;
			}
			if (!component.ServerCanDispense(1))
			{
				ClientShowNotifyRpc("Info", "Dispenser is empty.");
				return;
			}
			if (_dessertSO == null || _dessertSO.Recipe == null)
			{
				ClientShowNotifyRpc("Info", "You need to select a recipe first");
				return;
			}
			string id = component.IngredientSO.Id;
			if (!IsIngredientInRecipe(_dessertSO, id))
			{
				ClientShowNotifyRpc("Info", "This ingredient isn’t part of the recipe");
				return;
			}
			component.ServerDispense(1);
			FixedString64Bytes id2 = new FixedString64Bytes(id);
			ServerUpsertIngredientEntry(id2);
			ServerPushIngredientStack(id2);
			if (IsRecipeComplete(_dessertSO))
			{
				_yaki3000Canvas?.ClientNotifyRecipeCompletedRpc();
			}
		}

		private static bool IsIngredientInRecipe(Yaki3000IngredientSO dessert, string ingredientId)
		{
			bool num = dessert.Recipe.Ingredients != null && dessert.Recipe.Ingredients.Any((IngredientNeeds n) => n.IngredientId == ingredientId);
			bool flag = dessert.Recipe.ProcessedIngredients != null && dessert.Recipe.ProcessedIngredients.Any((ProcessedIngredientNeeds n) => n.IngredientId == ingredientId);
			return num | flag;
		}

		[Rpc(SendTo.Server)]
		public void ServerSetProductRpc(byte productType)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1244284625u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in productType, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1244284625u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			byte productType2 = productType;
			Yaki3000State state = State;
			if (state != Yaki3000State.Empty && state != Yaki3000State.Preparing)
			{
				UtilityMethods.Log("Urun secilemedi");
				return;
			}
			bool flag = HasAnyIngredient();
			if ((State == Yaki3000State.Preparing) & flag)
			{
				UtilityMethods.Log("Machine is preparing and already has ingredients. Product cannot be changed.");
				return;
			}
			_selectedProductType.Value = productType2;
			_currentState.Value = 1;
			_dessertSO = _cachedDesserts.FirstOrDefault((Yaki3000IngredientSO x) => (uint)x.Yaki3000ProductType == productType2);
			if (_dessertSO == null || _dessertSO.Recipe == null)
			{
				UtilityMethods.Log("No matching DessertSO found for selected product (or recipe missing). Reverting state.");
				_selectedProductType.Value = 0;
				_currentState.Value = 0;
				_dessertSO_ID.Value = default;
				if (_yaki3000Canvas != null)
				{
					_yaki3000Canvas.ResetRpc();
				}
			}
			else
			{
				_dessertSO_ID.Value = _dessertSO.Id;
				UtilityMethods.Log("Selected product updated.");
				_yaki3000Canvas.ServerNotifyDessertSelected();
			}
		}

		private bool HasAnyIngredient()
		{
			for (int i = 0; i < _machineIngredientEntries.Count; i++)
			{
				if (_machineIngredientEntries[i].Count > 0)
				{
					return true;
				}
			}
			return false;
		}

		private void ServerStartCooking()
		{
			_preparationRoutine = StartCoroutine(CookingRoutineServer());
			_yaki3000Canvas.ClientOnCookingStartedRpc();
		}

		private IEnumerator CookingRoutineServer()
		{
			_currentState.Value = 2;
			_preparationTimeRemaining.Value = _preparationTime;
			float startTime = Time.time;
			float timeout = _preparationTime + 5f;
			while (_preparationTimeRemaining.Value > 0f)
			{
				yield return new WaitForSeconds(1f);
				_preparationTimeRemaining.Value--;
				UtilityMethods.Log($"Yaki3000 cooking... {_preparationTimeRemaining.Value} seconds left.");
				if (Time.time - startTime > timeout)
				{
					UtilityMethods.Log("Cooking timeout reached. Forcing completion.");
					break;
				}
			}
			_preparationTimeRemaining.Value = 0f;
			_preparationRoutine = null;
			_currentState.Value = 3;
			ApplyCookedGhostRpc(_selectedProductType.Value);
			UtilityMethods.Log("Yaki3000 cooking finished.");
		}

		private void ServerGiveCookedProduct(Interactor interactor)
		{
			Yaki3000IngredientSO yaki3000IngredientSO = ((_dessertSO != null) ? _dessertSO : _cachedDesserts.FirstOrDefault((Yaki3000IngredientSO d) => d.Yaki3000ProductType == SelectedProduct));
			if (yaki3000IngredientSO == null || yaki3000IngredientSO.Prefab == null)
			{
				UtilityMethods.Log($"Cooked product not found for: {SelectedProduct}");
				return;
			}
			NetworkObject networkObject = UnityEngine.Object.Instantiate(yaki3000IngredientSO.Prefab);
			BaseHoldable component = networkObject.GetComponent<BaseHoldable>();
			if (component == null || component.NetworkObject == null)
			{
				Debug.LogError("Cooked prefab is missing BaseHoldable or NetworkObject!");
				UnityEngine.Object.Destroy(networkObject);
			}
			else
			{
				component.NetworkObject.Spawn(destroyWithScene: true);
				interactor.ServerHoldRpc(component.NetworkObject.ToReference());
				ServerResetMachine();
			}
		}

		private void ServerResetMachine()
		{
			_currentState.Value = 0;
			_selectedProductType.Value = 0;
			_machineIngredientEntries.Clear();
			_ingredientStack.Clear();
			_dessertSO = null;
			_dessertSO_ID.Value = default;
			if (_yaki3000Canvas != null)
			{
				_yaki3000Canvas.ResetRpc();
			}
			HideAllGhostsRpc();
		}

		[Rpc(SendTo.Server)]
		public void ServerResetToDessertPanelRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(235880823u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 235880823u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp);
				if (_machineIngredientEntries.Count > 0 || _ingredientStack.Count > 0)
				{
					ClientShowNotifyRpc("Info", "You can’t go back while ingredients are inside.", baseRpcTarget);
				}
				else
				{
					ServerResetMachine();
				}
			}
		}

		private void ServerTryGiveBackLastIngredient(Interactor interactor)
		{
			BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(interactor.OwnerClientId, RpcTargetUse.Temp);
			if (!TryPeekLastIngredient(out var ingredientId))
			{
				ClientShowNotifyRpc("Info", "There are no ingredients inside the machine.", baseRpcTarget);
				return;
			}
			ItemSO itemSO = _itemDatabaseSO.FindById(ingredientId.ToString());
			if (itemSO == null)
			{
				ClientShowNotifyRpc("Info", "An unexpected error occurred. Action canceled.", baseRpcTarget);
				return;
			}
			NetworkObject networkObject = UnityEngine.Object.Instantiate(itemSO.Prefab);
			BaseHoldable component = networkObject.GetComponent<BaseHoldable>();
			if (component == null || component.NetworkObject == null)
			{
				ClientShowNotifyRpc("Info", "An unexpected error occurred. Action canceled.", baseRpcTarget);
				UnityEngine.Object.Destroy(networkObject);
			}
			else
			{
				component.NetworkObject.Spawn(destroyWithScene: true);
				interactor.ServerHoldRpc(component.NetworkObject.ToReference());
				ServerRemoveLastIngredientConfirmed();
			}
		}

		private bool TryPeekLastIngredient(out FixedString64Bytes ingredientId)
		{
			if (_ingredientStack.Count == 0)
			{
				ingredientId = default;
				return false;
			}
			NetworkList<FixedString64Bytes> ingredientStack = _ingredientStack;
			ingredientId = ingredientStack[ingredientStack.Count - 1];
			return true;
		}

		private void ServerRemoveLastIngredientConfirmed()
		{
			if (_ingredientStack.Count == 0)
			{
				return;
			}
			NetworkList<FixedString64Bytes> ingredientStack = _ingredientStack;
			FixedString64Bytes fixedString64Bytes = ingredientStack[ingredientStack.Count - 1];
			_ingredientStack.RemoveAt(_ingredientStack.Count - 1);
			for (int i = 0; i < _machineIngredientEntries.Count; i++)
			{
				if (_machineIngredientEntries[i].IngredientID.Equals(fixedString64Bytes))
				{
					IngredientEntry value = _machineIngredientEntries[i];
					value.Count = (byte)Mathf.Max(0, value.Count - 1);
					if (value.Count == 0)
					{
						_machineIngredientEntries.RemoveAt(i);
						ClientUpdateIngredientSlotUIRPC(fixedString64Bytes, 0);
					}
					else
					{
						_machineIngredientEntries[i] = value;
						ClientUpdateIngredientSlotUIRPC(fixedString64Bytes, value.Count);
					}
					break;
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientUpdateIngredientSlotUIRPC(FixedString64Bytes ingredientId, byte count)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2418916518u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in ingredientId, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in count, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2418916518u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			FixedString64Bytes ingredientId2 = ingredientId;
			if (!(_yaki3000Canvas == null) && _yaki3000Canvas.RecipeIngredientSlots != null)
			{
				RecipeIngredientUI recipeIngredientUI = _yaki3000Canvas.RecipeIngredientSlots.FirstOrDefault((RecipeIngredientUI x) => x.IngredientID == ingredientId2);
				if (!(recipeIngredientUI == null))
				{
					recipeIngredientUI.UpdateAmount(count);
					UtilityMethods.Log("Ingredient UI Updated");
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void HideAllGhostsRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(965829490u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 965829490u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				KillGhostTween();
				ResetGhostGO(_tamagoyakiGhost);
				ResetGhostGO(_taiyakiGhost);
				ResetGhostGO(_dorayakiGhost);
				ResetGhostGO(_dangoGhost);
			}
		}

		private void ResetGhostGO(GameObject ghost)
		{
			if (!(ghost == null))
			{
				ghost.SetActive(value: false);
				Transform transform = ghost.transform;
				transform.localScale = Vector3.zero;
				if (_ghostExitPoint != null)
				{
					transform.position = _ghostExitPoint.position;
					transform.rotation = _ghostExitPoint.rotation;
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ApplyCookedGhostRpc(byte productType)
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
				FastBufferWriter bufferWriter = __beginSendRpc(910167687u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in productType, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 910167687u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				PlayCookedGhostDrop(productType);
				if (_yaki3000Canvas != null && _yaki3000Canvas.InfoText != null)
				{
					_yaki3000Canvas.InfoText.text = "CookFinish";
				}
			}
		}

		private void PlayCookedGhostDrop(byte product)
		{
			Yaki3000Product product2 = (Yaki3000Product)product;
			KillGhostTween();
			HideAllGhostsLocal();
			GameObject ghostGO = GetGhostGO(product2);
			if (!(ghostGO == null))
			{
				ghostGO.SetActive(value: true);
				Transform transform = ghostGO.transform;
				if (_ghostExitPoint != null)
				{
					transform.position = _ghostExitPoint.position;
					transform.rotation = _ghostExitPoint.rotation;
				}
				transform.localScale = Vector3.zero;
				Vector3 vector = transform.position + Vector3.up * _ghostPopHeight;
				Vector3 endValue = ((_ghostDropPoint != null) ? _ghostDropPoint.position : vector);
				_ghostTween = DOTween.Sequence().Append(transform.DOScale(1f, _ghostPopDuration).SetEase(Ease.OutBack)).Join(transform.DOMove(vector, _ghostPopDuration).SetEase(Ease.OutQuad))
					.Append(transform.DOMove(endValue, _ghostDropDuration).SetEase(Ease.InQuad));
			}
		}

		private GameObject GetGhostGO(Yaki3000Product product)
		{
			return product switch
			{
				Yaki3000Product.Tamagoyaki => _tamagoyakiGhost, 
				Yaki3000Product.Taiyaki => _taiyakiGhost, 
				Yaki3000Product.Dorayaki => _dorayakiGhost, 
				Yaki3000Product.Dango => _dangoGhost, 
				_ => null, 
			};
		}

		private void HideAllGhostsLocal()
		{
			if (_tamagoyakiGhost != null)
			{
				_tamagoyakiGhost.SetActive(value: false);
			}
			if (_taiyakiGhost != null)
			{
				_taiyakiGhost.SetActive(value: false);
			}
			if (_dorayakiGhost != null)
			{
				_dorayakiGhost.SetActive(value: false);
			}
			if (_dangoGhost != null)
			{
				_dangoGhost.SetActive(value: false);
			}
		}

		private void KillGhostTween()
		{
			if (_ghostTween != null && _ghostTween.IsActive())
			{
				_ghostTween.Kill();
			}
			_ghostTween = null;
		}

		private void ServerPushIngredientStack(FixedString64Bytes id)
		{
			_ingredientStack.Add(id);
		}

		private bool IsRecipeComplete(Yaki3000IngredientSO dessertSO)
		{
			if (dessertSO == null || dessertSO.Recipe == null)
			{
				return false;
			}
			if (dessertSO.Recipe.Ingredients != null)
			{
				foreach (IngredientNeeds ingredient in dessertSO.Recipe.Ingredients)
				{
					if (!HasEnoughIngredient(ingredient.IngredientId, ingredient.RequiredAmount))
					{
						return false;
					}
				}
			}
			if (dessertSO.Recipe.ProcessedIngredients != null)
			{
				foreach (ProcessedIngredientNeeds processedIngredient in dessertSO.Recipe.ProcessedIngredients)
				{
					if (!HasEnoughIngredient(processedIngredient.IngredientId, processedIngredient.RequiredAmount))
					{
						return false;
					}
				}
			}
			return true;
		}

		private bool HasEnoughIngredient(string requiredIngredientId, int requiredAmount)
		{
			if (string.IsNullOrEmpty(requiredIngredientId) || requiredAmount <= 0)
			{
				return true;
			}
			int num = 0;
			foreach (IngredientEntry machineIngredientEntry in _machineIngredientEntries)
			{
				FixedString64Bytes ingredientID = machineIngredientEntry.IngredientID;
				if (ingredientID.ToString() == requiredIngredientId)
				{
					num += machineIngredientEntry.Count;
				}
			}
			return num >= requiredAmount;
		}

		[Rpc(SendTo.Owner)]
		public void ClientShowNotifyRpc(FixedString64Bytes header, FixedString64Bytes message)
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
				FastBufferWriter bufferWriter = __beginSendRpc(960394393u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in header, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in message, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 960394393u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Singleton<NotifyManager>.Instance.ShowToast(header.ToString(), message.ToString());
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		public void ClientShowNotifyRpc(FixedString64Bytes header, FixedString128Bytes message, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3107210215u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in header, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in message, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 3107210215u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Singleton<NotifyManager>.Instance.ShowToast(header.ToString(), message.ToString());
			}
		}

		public override void EnableGazeHints()
		{
			Singleton<HintsManager>.Instance.AddHint("Interact", "", "");
			_objToolTipGO.SetActive(value: true);
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
			_objToolTipGO.SetActive(value: false);
		}

		private void HandleStateChanged(byte prev, byte next)
		{
			NetStateChanged?.Invoke(prev, next);
		}

		private void HandleDessertIdChanged(FixedString64Bytes prev, FixedString64Bytes next)
		{
			NetDessertIdChanged?.Invoke(prev, next);
		}

		private void HandleIngredientsChanged(NetworkListEvent<IngredientEntry> changeEvent)
		{
			NetIngredientsChanged?.Invoke(changeEvent);
		}

		protected override void __initializeVariables()
		{
			if (_selectedProductType == null)
			{
				throw new Exception("Yaki3000._selectedProductType cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_selectedProductType.Initialize(this);
			__nameNetworkVariable(_selectedProductType, "_selectedProductType");
			NetworkVariableFields.Add(_selectedProductType);
			if (_currentState == null)
			{
				throw new Exception("Yaki3000._currentState cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_currentState.Initialize(this);
			__nameNetworkVariable(_currentState, "_currentState");
			NetworkVariableFields.Add(_currentState);
			if (_preparationTimeRemaining == null)
			{
				throw new Exception("Yaki3000._preparationTimeRemaining cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_preparationTimeRemaining.Initialize(this);
			__nameNetworkVariable(_preparationTimeRemaining, "_preparationTimeRemaining");
			NetworkVariableFields.Add(_preparationTimeRemaining);
			if (_machineIngredientEntries == null)
			{
				throw new Exception("Yaki3000._machineIngredientEntries cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_machineIngredientEntries.Initialize(this);
			__nameNetworkVariable(_machineIngredientEntries, "_machineIngredientEntries");
			NetworkVariableFields.Add(_machineIngredientEntries);
			if (_dessertSO_ID == null)
			{
				throw new Exception("Yaki3000._dessertSO_ID cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_dessertSO_ID.Initialize(this);
			__nameNetworkVariable(_dessertSO_ID, "_dessertSO_ID");
			NetworkVariableFields.Add(_dessertSO_ID);
			if (_ingredientStack == null)
			{
				throw new Exception("Yaki3000._ingredientStack cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_ingredientStack.Initialize(this);
			__nameNetworkVariable(_ingredientStack, "_ingredientStack");
			NetworkVariableFields.Add(_ingredientStack);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3314377980u, __rpc_handler_3314377980, "ServerTryStartCookingRpc", RpcInvokePermission.Everyone);
			__registerRpc(250722202u, __rpc_handler_250722202, "ServerMouse2Rpc", RpcInvokePermission.Everyone);
			__registerRpc(1915117012u, __rpc_handler_1915117012, "ServerTryAddHeldIngredientRpc", RpcInvokePermission.Everyone);
			__registerRpc(1245230626u, __rpc_handler_1245230626, "ServerDispenseIntoMachineRpc", RpcInvokePermission.Everyone);
			__registerRpc(1244284625u, __rpc_handler_1244284625, "ServerSetProductRpc", RpcInvokePermission.Everyone);
			__registerRpc(235880823u, __rpc_handler_235880823, "ServerResetToDessertPanelRpc", RpcInvokePermission.Everyone);
			__registerRpc(2418916518u, __rpc_handler_2418916518, "ClientUpdateIngredientSlotUIRPC", RpcInvokePermission.Everyone);
			__registerRpc(965829490u, __rpc_handler_965829490, "HideAllGhostsRpc", RpcInvokePermission.Everyone);
			__registerRpc(910167687u, __rpc_handler_910167687, "ApplyCookedGhostRpc", RpcInvokePermission.Everyone);
			__registerRpc(960394393u, __rpc_handler_960394393, "ClientShowNotifyRpc", RpcInvokePermission.Everyone);
			__registerRpc(3107210215u, __rpc_handler_3107210215, "ClientShowNotifyRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3314377980(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Yaki3000)target).ServerTryStartCookingRpc(ext);
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_250722202(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Yaki3000)target).ServerMouse2Rpc(value);
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1915117012(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Yaki3000)target).ServerTryAddHeldIngredientRpc(value, value2, ext);
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1245230626(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Yaki3000)target).ServerDispenseIntoMachineRpc(value);
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1244284625(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Yaki3000)target).ServerSetProductRpc(value);
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_235880823(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Yaki3000)target).ServerResetToDessertPanelRpc(ext);
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2418916518(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Yaki3000)target).ClientUpdateIngredientSlotUIRPC(value, value2);
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_965829490(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Yaki3000)target).HideAllGhostsRpc();
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_910167687(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Yaki3000)target).ApplyCookedGhostRpc(value);
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_960394393(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out FixedString64Bytes value2, default(FastBufferWriter.ForFixedStrings));
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Yaki3000)target).ClientShowNotifyRpc(value, value2);
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3107210215(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out FixedString128Bytes value2, default(FastBufferWriter.ForFixedStrings));
				RpcParams ext = rpcParams.Ext;
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Yaki3000)target).ClientShowNotifyRpc(value, value2, ext);
				((Yaki3000)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "Yaki3000";
		}
	}
}
