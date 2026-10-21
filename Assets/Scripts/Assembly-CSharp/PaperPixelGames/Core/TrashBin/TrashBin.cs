using System.Collections.Generic;
using Assets.Scripts.Core.ProcessedIngredient;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen.Wholasale;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.ProcessedIngredient;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Core.Restaurant.Dirt;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.TrashBin
{
	public class TrashBin : BaseInteractable
	{
		private static readonly HashSet<TrashBin> staffBins = new HashSet<TrashBin>();

		private const string TrashConfirmTitleKey = "ui.trash.confirm.title";

		private const string TrashConfirmFoodDescriptionKey = "ui.trash.confirm.food_description";

		private const string TrashConfirmDescriptionKey = "ui.trash.confirm.description";

		[SerializeField]
		private AudioSource _audioSource;

		private NetworkObjectReference _pendingTrashInteractorRef;

		private NetworkObjectReference _pendingTrashHoldableRef;

		public static IEnumerable<TrashBin> StaffBins => staffBins;

		public bool ServerStaffUsable
		{
			get
			{
				if (base.IsServer && base.IsSpawned && base.isActiveAndEnabled)
				{
					return CanInteract.Value;
				}
				return false;
			}
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStaffBins()
		{
			staffBins.Clear();
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (base.IsServer)
			{
				staffBins.Add(this);
			}
		}

		public override void OnNetworkDespawn()
		{
			staffBins.Remove(this);
			base.OnNetworkDespawn();
		}

		public static bool IsEmptyStaffContainer(BaseHoldable item)
		{
			if (!(item is WholesaleEntity { CurrentCount: 0 }))
			{
				if (item is SideDishIngredientEntity sideDishIngredientEntity)
				{
					return sideDishIngredientEntity.IngredientCount == 0;
				}
				return false;
			}
			return true;
		}

		public bool ServerStaffDiscardEmptyBox(int employeeId, BaseHoldable box)
		{
			if (!ServerStaffUsable || box == null || !box.IsSpawned || !box.ServerStaffCarriedBy(employeeId) || !IsEmptyStaffContainer(box))
			{
				return false;
			}
			ClientPlaySFXRpc();
			box.NetworkObject.Despawn();
			return true;
		}

		protected override bool CanCrossTrigger(Interactor interactor, BaseHoldable held)
		{
			return base.CanCrossTrigger(interactor, held);
		}

		protected override void ClientCrossMouse1(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			base.ClientCrossMouse1(interactor, held, phase);
			if (phase != InputActionPhase.Started)
			{
				return;
			}
			if (held is HoldableDirt || IsEmptyProduct(held))
			{
				TrashHeldItem(interactor, held);
			}
			else if (held is Tray tray)
			{
				if (tray.IsDirty.Value)
				{
					TrashHeldItem(interactor, tray);
				}
				else
				{
					Singleton<NotifyManager>.Instance.ShowToast("Already clean!");
				}
			}
			else
			{
				TryTrashWithConfirmation(interactor, held);
			}
		}

		private static bool IsEmptyProduct(BaseHoldable held)
		{
			if (!(held is WholesaleEntity { CurrentCount: 0 }))
			{
				if (held is IngredientEntity ingredientEntity)
				{
					return ingredientEntity.IngredientCount == 0;
				}
				return false;
			}
			return true;
		}

		private void TryTrashWithConfirmation(Interactor interactor, BaseHoldable held)
		{
			if (!(interactor == null) && !(interactor.NetworkObject == null) && !(held == null) && !(held.NetworkObject == null))
			{
				_pendingTrashInteractorRef = interactor.NetworkObject.ToReference();
				_pendingTrashHoldableRef = held.NetworkObject.ToReference();
				Singleton<NotifyManager>.Instance.ShowPopup(LocalizationDatabase.GetString("UiTable", "ui.trash.confirm.title", "Trash item?"), GetTrashConfirmationMessage(held), ConfirmPendingTrash, CancelPendingTrash);
			}
		}

		public void ConfirmPendingTrash()
		{
			Singleton<NotifyManager>.Instance.HidePopup();
			if (!_pendingTrashInteractorRef.TryGetComponent<Interactor>(out var component))
			{
				ClearPendingTrash();
				return;
			}
			if (!_pendingTrashHoldableRef.TryGetComponent<BaseHoldable>(out var component2) || Singleton<QuickSlotHandler>.Instance == null || Singleton<QuickSlotHandler>.Instance.SelectedItem != component2)
			{
				ClearPendingTrash();
				return;
			}
			TrashHeldItem(component, component2);
			ClearPendingTrash();
		}

		public void CancelPendingTrash()
		{
			Singleton<NotifyManager>.Instance.HidePopup();
			ClearPendingTrash();
		}

		private void TrashHeldItem(Interactor interactor, BaseHoldable held)
		{
			if (!(interactor == null) && !(interactor.NetworkObject == null) && !(held == null) && !(held.NetworkObject == null))
			{
				ServerTrashItemRpc(interactor.NetworkObject.ToReference(), held.NetworkObject.ToReference());
			}
		}

		private static string GetTrashConfirmationMessage(BaseHoldable held)
		{
			int num;
			object obj;
			if (!(held is WholesaleEntity { CurrentCount: >0 }))
			{
				if (held is IngredientEntity ingredientEntity)
				{
					num = ((ingredientEntity.IngredientCount > 0) ? 1 : 0);
					if (num != 0)
					{
						goto IL_0036;
					}
				}
				else
				{
					num = 0;
				}
				obj = "ui.trash.confirm.description";
				goto IL_003b;
			}
			num = 1;
			goto IL_0036;
			IL_0036:
			obj = "ui.trash.confirm.food_description";
			goto IL_003b;
			IL_003b:
			string key = (string)obj;
			string fallback = ((num != 0) ? "This item still has food inside. Are you sure you want to throw it away?" : "Are you sure you want to throw this item away?");
			return LocalizationDatabase.GetString("UiTable", key, fallback);
		}

		private void ClearPendingTrash()
		{
			_pendingTrashInteractorRef = default;
			_pendingTrashHoldableRef = default;
		}

		[Rpc(SendTo.Server)]
		private void ServerTrashItemRpc(NetworkObjectReference interactorRef, NetworkObjectReference holdableRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(497456985u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in holdableRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 497456985u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!interactorRef.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != senderClientId || !holdableRef.TryGetComponent<BaseHoldable>(out var component2) || component2.OwnerClientId != senderClientId || component2.CanHold.Value || (object)component.CurrentHeld != component2)
			{
				UtilityMethods.Log("[TrashBin] Trash request rejected because the expected held item could not be validated.", LogType.Warning);
				return;
			}
			if (component2 is Tray tray && !tray.IsDirty.Value)
			{
				UtilityMethods.Log("[TrashBin] Trash request rejected because the held tray is already clean.", LogType.Warning);
				return;
			}
			ClientPlaySFXRpc();
			if (component2 is Tray tray2)
			{
				tray2.ServerSetDirtyStatus(status: false, component.OwnerClientId);
				return;
			}
			Singleton<GameProgressManager>.Instance?.ServerGrantProgressionXp(ProgressionXpSource.TrashDisposed, null, component.OwnerClientId);
			component.ClientDestroyHeldItemRpc(holdableRef);
		}

		[Rpc(SendTo.Everyone)]
		private void ClientPlaySFXRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1679756300u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1679756300u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (_audioSource != null)
				{
					_audioSource.Play();
				}
			}
		}

		public override void EnableGazeHints()
		{
			if (TryGetTrashBinHint(out var localizationKey))
			{
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", localizationKey);
			}
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
		}

		private bool TryGetTrashBinHint(out string localizationKey)
		{
			localizationKey = null;
			if (Singleton<QuickSlotHandler>.Instance == null)
			{
				return false;
			}
			BaseHoldable selectedItem = Singleton<QuickSlotHandler>.Instance.SelectedItem;
			if (selectedItem == null)
			{
				return false;
			}
			if (selectedItem is HoldableDirt)
			{
				localizationKey = "key_trash";
				return true;
			}
			if (selectedItem is Tray tray)
			{
				if (!tray.IsDirty.Value)
				{
					return false;
				}
				localizationKey = "key_trash";
				return true;
			}
			localizationKey = "key_trash";
			return true;
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(497456985u, __rpc_handler_497456985, "ServerTrashItemRpc", RpcInvokePermission.Everyone);
			__registerRpc(1679756300u, __rpc_handler_1679756300, "ClientPlaySFXRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_497456985(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((TrashBin)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((TrashBin)target).ServerTrashItemRpc(value, value2, ext);
				((TrashBin)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1679756300(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((TrashBin)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((TrashBin)target).ClientPlaySFXRpc();
				((TrashBin)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "TrashBin";
		}
	}
}
