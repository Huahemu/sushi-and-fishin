using System;
using Assets.Scripts.Core.ProcessedIngredient;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Interaction.ToolTip;
using PaperPixelGames.Core.Kitchen;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Restaurant
{
	public class Tray : BaseHoldable
	{
		private const string DirtyShadowRootName = "DirtyRoot";

		private const ulong NoInventoryOwnerClientId = ulong.MaxValue;

		[SerializeField]
		private GameObject _dirtyRoot;

		[SerializeField]
		private GameObject _cleanRoot;

		[Tooltip("Shown in the quick-slot UI when this tray has been returned dirty.")]
		[SerializeField]
		private Sprite _dirtyQuickSlotSprite;

		[Space]
		[SerializeField]
		private SnapPoint _mainDishPoint;

		[SerializeField]
		private SnapPoint _sideDishPoint;

		[SerializeField]
		private SnapPoint _beveragePoint;

		[NonSerialized]
		public NetworkVariable<bool> IsDirty = new NetworkVariable<bool>(value: false);

		[NonSerialized]
		private NetworkVariable<NetworkObjectReference> _mainDishRef = new NetworkVariable<NetworkObjectReference>();

		[NonSerialized]
		private NetworkVariable<NetworkObjectReference> _sideDishRef = new NetworkVariable<NetworkObjectReference>();

		[NonSerialized]
		private NetworkVariable<NetworkObjectReference> _beverageRef = new NetworkVariable<NetworkObjectReference>();

		private bool _dishPlacementLocked;

		private ulong _serverInventoryOwnerClientId = ulong.MaxValue;

		private SnapPoint _homeTrolleySnapPoint;

		private bool _crossMouse1HoldPerformed;

		public override Sprite QuickSlotDisplaySprite
		{
			get
			{
				if (!IsDirty.Value || !(_dirtyQuickSlotSprite != null))
				{
					return base.QuickSlotDisplaySprite;
				}
				return _dirtyQuickSlotSprite;
			}
		}

		public bool ServerStaffEmpty
		{
			get
			{
				if (!TryGetMainDish(out var servedDish) && !TryGetSideDish(out servedDish))
				{
					return !TryGetBeverageDish(out servedDish);
				}
				return false;
			}
		}

		public override TooltipData GetTooltip()
		{
			TooltipConfig tooltip = _itemSO.Tooltip;
			string body = string.Empty;
			AppendSlotTooltip(ref body, 0);
			AppendSlotTooltip(ref body, 1);
			AppendSlotTooltip(ref body, 2);
			if (body == string.Empty)
			{
				body = (IsDirty.Value ? LocalizationDatabase.GetString("UiTable", "tooltip.dirty", "Dirty") : LocalizationDatabase.GetString("UiTable", "tooltip.empty", "Empty"));
			}
			return new TooltipData
			{
				Title = ((_itemSO != null) ? _itemSO.LocalizedName : tooltip.Data.Title),
				Body = body
			};
		}

		public bool TryGetQuickSlotContentSprites(out Sprite mainDishSprite, out Sprite sideDishSprite, out Sprite beverageSprite)
		{
			mainDishSprite = GetSlotSprite(0);
			sideDishSprite = GetSlotSprite(1);
			beverageSprite = GetSlotSprite(2);
			if (IsDirty.Value)
			{
				mainDishSprite = null;
				sideDishSprite = null;
				beverageSprite = null;
				return false;
			}
			if (!(mainDishSprite != null) && !(sideDishSprite != null))
			{
				return beverageSprite != null;
			}
			return true;
		}

		private Sprite GetSlotSprite(byte index)
		{
			if (!TryGetSlotHoldable(index, out var holdable) || !(holdable.ItemSO != null))
			{
				return null;
			}
			return holdable.ItemSO.Sprite;
		}

		private void AppendSlotTooltip(ref string body, byte index)
		{
			if (TryGetSlotHoldable(index, out var holdable) && !(holdable.ItemSO == null))
			{
				if (!string.IsNullOrEmpty(body))
				{
					body += "\n";
				}
				body += holdable.ItemSO.LocalizedName;
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<bool> isDirty = IsDirty;
			isDirty.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isDirty.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnDirtyChanged));
			NetworkVariable<NetworkObjectReference> mainDishRef = _mainDishRef;
			mainDishRef.OnValueChanged = (NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate)Delegate.Combine(mainDishRef.OnValueChanged, new NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate(OnTrayContentsChanged));
			NetworkVariable<NetworkObjectReference> sideDishRef = _sideDishRef;
			sideDishRef.OnValueChanged = (NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate)Delegate.Combine(sideDishRef.OnValueChanged, new NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate(OnTrayContentsChanged));
			NetworkVariable<NetworkObjectReference> beverageRef = _beverageRef;
			beverageRef.OnValueChanged = (NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate)Delegate.Combine(beverageRef.OnValueChanged, new NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate(OnTrayContentsChanged));
			ApplyDirtyVisuals(IsDirty.Value);
			TrayTrolley componentInParent = GetComponentInParent<TrayTrolley>(includeInactive: true);
			if (componentInParent != null && componentInParent.TryGetTrayHomeSnapPoint(this, out var snapPoint))
			{
				_homeTrolleySnapPoint = snapPoint;
			}
			componentInParent?.ApplyTrayPhysicalAvailability(this);
			if (base.IsServer)
			{
				base.NetworkObject.DontDestroyWithOwner = true;
				if (base.NetworkManager != null)
				{
					base.NetworkManager.OnClientDisconnectCallback += ServerOnClientDisconnected;
				}
			}
		}

		public override void OnNetworkDespawn()
		{
			if (base.IsServer && base.NetworkManager != null)
			{
				base.NetworkManager.OnClientDisconnectCallback -= ServerOnClientDisconnected;
			}
			NetworkVariable<bool> isDirty = IsDirty;
			isDirty.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isDirty.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnDirtyChanged));
			NetworkVariable<NetworkObjectReference> mainDishRef = _mainDishRef;
			mainDishRef.OnValueChanged = (NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate)Delegate.Remove(mainDishRef.OnValueChanged, new NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate(OnTrayContentsChanged));
			NetworkVariable<NetworkObjectReference> sideDishRef = _sideDishRef;
			sideDishRef.OnValueChanged = (NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate)Delegate.Remove(sideDishRef.OnValueChanged, new NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate(OnTrayContentsChanged));
			NetworkVariable<NetworkObjectReference> beverageRef = _beverageRef;
			beverageRef.OnValueChanged = (NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate)Delegate.Remove(beverageRef.OnValueChanged, new NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate(OnTrayContentsChanged));
			base.OnNetworkDespawn();
		}

		private void ServerOnClientDisconnected(ulong clientId)
		{
			if (base.IsServer && clientId == _serverInventoryOwnerClientId && !CanHold.Value && base.IsPlayerInventoryOwnedForPersistence && !(base.NetworkObject == null) && base.NetworkObject.IsSpawned)
			{
				Vector3 pos = ((_homeTrolleySnapPoint != null) ? _homeTrolleySnapPoint.transform.position : base.transform.position);
				Vector3 rot = ((_homeTrolleySnapPoint != null) ? _homeTrolleySnapPoint.transform.rotation.eulerAngles : base.transform.rotation.eulerAngles);
				_serverInventoryOwnerClientId = ulong.MaxValue;
				ServerSetPlacementTooltipSuppressed(_homeTrolleySnapPoint != null);
				ServerPlace(pos, rot, forget: false);
				UtilityMethods.Log($"[Tray] Recovered disconnected client's tray. ClientId:{clientId}, Tray:{base.name}, HomeSlot:{_homeTrolleySnapPoint != null}");
			}
		}

		private void OnDirtyChanged(bool oldVal, bool newVal)
		{
			UtilityMethods.Log("[Tray] OnDirtyChanged:" + newVal);
			ApplyDirtyVisuals(newVal);
			RefreshQuickSlotDisplay();
		}

		private void OnTrayContentsChanged(NetworkObjectReference previousValue, NetworkObjectReference newValue)
		{
			RefreshQuickSlotDisplay();
			if (_inGaze)
			{
				Singleton<ToolTipHandler>.Instance?.OnGazeEnter(this);
			}
			if (_ownerInteractor != null && _ownerInteractor.CurrentHeld == this)
			{
				_ownerInteractor.RefreshCurrentHints();
			}
		}

		private void LateUpdate()
		{
			if (!base.IsSpawned)
			{
				return;
			}
			for (byte b = 0; b < 3; b++)
			{
				if (TryGetSlotHoldable(b, out var holdable) && !(holdable.transform.parent != base.transform))
				{
					SnapPoint snapPointByIndex = GetSnapPointByIndex(b);
					if (!(snapPointByIndex == null) && ((holdable.transform.position - snapPointByIndex.transform.position).sqrMagnitude > 1E-06f || Quaternion.Angle(holdable.transform.rotation, snapPointByIndex.transform.rotation) > 0.01f))
					{
						holdable.transform.SetPositionAndRotation(snapPointByIndex.transform.position, snapPointByIndex.transform.rotation);
					}
				}
			}
		}

		private void RefreshQuickSlotDisplay()
		{
			Singleton<QuickSlotHandler>.Instance?.RefreshDisplayForItem(this);
		}

		private void ApplyDirtyVisuals(bool isDirty)
		{
			if (_dirtyRoot != null)
			{
				_dirtyRoot.SetActive(isDirty);
			}
			if (_cleanRoot != null)
			{
				_cleanRoot.SetActive(!isDirty);
			}
			HandleDirtyShadowGraphic(isDirty);
		}

		[Rpc(SendTo.Server)]
		public void ServerDirtyStatusRpc(bool status, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3378631521u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in status, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3378631521u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!base.IsStaffCarried)
				{
					ServerSetDirtyStatus(status, rpcParams.Receive.SenderClientId);
				}
			}
		}

		public void ServerSetDirtyStatus(bool status, ulong feedbackClientId)
		{
			if (base.IsServer)
			{
				bool value = IsDirty.Value;
				IsDirty.Value = status;
				if (value && !status)
				{
					QuestReferenceHub.Instance.QuestComplete(QuestId.ClearTheTray);
					Singleton<GameProgressManager>.Instance?.ServerGrantProgressionXp(ProgressionXpSource.DirtyTrayCleaned, 1, "dirty_tray_cleaned", feedbackClientId);
				}
			}
		}

		public override void ServerUnParentFromMe(BaseHoldable child)
		{
			base.ServerUnParentFromMe(child);
			ServerClearSlotByHoldable(child);
		}

		public void ServerEatingDone()
		{
			if (!base.IsServer)
			{
				return;
			}
			for (int num = _parentedChilds.Count - 1; num >= 0; num--)
			{
				NetworkObjectReference reference = _parentedChilds[num];
				if (!reference.Equals(default) && reference.TryGetComponent<BaseHoldable>(out var component) && !(component is Tray))
				{
					ServerUnParentFromMe(component);
					component.Destroy();
				}
			}
			_mainDishRef.Value = default;
			_sideDishRef.Value = default;
			_beverageRef.Value = default;
			IsDirty.Value = true;
		}

		public void ServerResetAfterRestaurantTableRelease()
		{
			if (base.IsServer)
			{
				_dishPlacementLocked = false;
				ServerEatingDone();
				IsDirty.Value = false;
				CanHold.Value = true;
				CanGaze.Value = true;
				ServerChangeChildsStatus(status: true, forget: false);
			}
		}

		public bool ServerPlaceOnRestaurantTable(Vector3 position, Vector3 rotation)
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (base.NetworkObject == null || !base.NetworkObject.IsSpawned)
			{
				return false;
			}
			if (CanHold.Value)
			{
				return false;
			}
			ServerSetPlacementTooltipSuppressed(suppressed: false);
			ServerPlace(position, rotation, forget: true);
			return true;
		}

		public bool ServerPlaceOnTrayTrolley(Vector3 position, Vector3 rotation)
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (base.NetworkObject == null || !base.NetworkObject.IsSpawned)
			{
				return false;
			}
			if (CanHold.Value)
			{
				return false;
			}
			ServerSetPlacementTooltipSuppressed(suppressed: true);
			ServerPlace(position, rotation, forget: false);
			ClientDisableStoredContentsCollidersRpc();
			return true;
		}

		public override void ServerHold(Interactor interactor)
		{
			if (interactor == null)
			{
				return;
			}
			TrayTrolley componentInParent = GetComponentInParent<TrayTrolley>(includeInactive: true);
			if (componentInParent != null && !componentInParent.IsPhysicallyUsable)
			{
				UtilityMethods.Log("[Tray] Hold rejected because the trolley is not physically usable. Tray:" + base.name, LogType.Warning);
				return;
			}
			base.ServerHold(interactor);
			if (base.IsServer && base.OwnerClientId == interactor.OwnerClientId && !CanHold.Value && base.IsPlayerInventoryOwnedForPersistence)
			{
				_serverInventoryOwnerClientId = interactor.OwnerClientId;
			}
		}

		public void ClientApplyTrolleyAvailability(SnapPoint snapPoint, bool available)
		{
			if (!(snapPoint == null) && !(base.transform.parent != snapPoint.transform) && CanHold.Value)
			{
				ObjectsStatus(available);
				CollidersStatus(available);
			}
		}

		public DeliveryEvaluation EvaluateDelivery(CustomerOrderData orderData)
		{
			if (orderData == null)
			{
				return new DeliveryEvaluation(OrderMatchResult.None, OrderIssueFlags.None, 0, 0);
			}
			if (!orderData.HasMainDish)
			{
				return new DeliveryEvaluation(OrderMatchResult.None, OrderIssueFlags.None, 0, 0);
			}
			if (!TryGetSlotServedDish(0, out var servedDish))
			{
				return new DeliveryEvaluation(OrderMatchResult.None, OrderIssueFlags.None, 0, 0);
			}
			if (servedDish.ItemSO == null)
			{
				return new DeliveryEvaluation(OrderMatchResult.None, OrderIssueFlags.None, 0, 0);
			}
			if (!string.Equals(orderData.MainDishIdentityId, servedDish.RuntimeRecipeIdentityId, StringComparison.Ordinal))
			{
				return new DeliveryEvaluation(OrderMatchResult.None, OrderIssueFlags.None, 0, 0);
			}
			OrderIssueFlags issues = OrderIssueFlags.None;
			HoldableSO deliveredHoldableSO = GetDeliveredHoldableSO(1);
			HoldableSO deliveredHoldableSO2 = GetDeliveredHoldableSO(2);
			bool flag = CompareOptionalForFeedback(orderData.SideDishSO, deliveredHoldableSO, OrderIssueFlags.SideMissing, OrderIssueFlags.SideWrong, OrderIssueFlags.SideExtra, ref issues);
			bool flag2 = CompareOptionalForFeedback(orderData.BeverageSO, deliveredHoldableSO2, OrderIssueFlags.BeverageMissing, OrderIssueFlags.BeverageWrong, OrderIssueFlags.BeverageExtra, ref issues);
			return new DeliveryEvaluation((flag & flag2) ? OrderMatchResult.Full : ((!(flag | flag2)) ? OrderMatchResult.MainOnly : OrderMatchResult.MainSide), earned: CalculateMatchedDeliveredPayment(orderData, deliveredHoldableSO, deliveredHoldableSO2), issues: issues, penalty: 0);
		}

		private HoldableSO GetDeliveredHoldableSO(byte index)
		{
			if (!TryGetSlotHoldable(index, out var holdable))
			{
				return null;
			}
			return holdable.ItemSO;
		}

		private int CalculateMatchedDeliveredPayment(CustomerOrderData orderData, HoldableSO deliveredSide, HoldableSO deliveredBeverage)
		{
			int num = 0;
			num += GetServedDishPrice(0);
			num += GetMatchedOptionalServedDishPrice(orderData.SideDishSO, deliveredSide);
			num += GetMatchedOptionalServedDishPrice(orderData.BeverageSO, deliveredBeverage);
			return Mathf.Max(0, num);
		}

		private static int GetMatchedOptionalServedDishPrice(HoldableSO expected, HoldableSO delivered)
		{
			if (expected == null || delivered == null)
			{
				return 0;
			}
			if (expected.Id != delivered.Id)
			{
				return 0;
			}
			if (delivered is ServedDishSO servedDish)
			{
				return GetEffectiveServedDishPrice(servedDish);
			}
			if (expected is ServedDishSO servedDish2)
			{
				return GetEffectiveServedDishPrice(servedDish2);
			}
			return 0;
		}

		private int GetServedDishPrice(byte index)
		{
			if (!TryGetSlotServedDish(index, out var servedDish))
			{
				return 0;
			}
			if (!(TabletNetworkHandler.Instance != null))
			{
				return servedDish.RuntimeDishPrice;
			}
			return TabletNetworkHandler.Instance.GetEffectiveRecipePrice(servedDish.RuntimeRecipeIdentityId, servedDish.RuntimeDishPrice);
		}

		private static int GetEffectiveServedDishPrice(ServedDishSO servedDish)
		{
			if (!(TabletNetworkHandler.Instance != null))
			{
				return Mathf.Max(0, (servedDish != null) ? servedDish.Price : 0);
			}
			return TabletNetworkHandler.Instance.GetEffectiveServedDishPrice(servedDish);
		}

		private static bool CompareOptionalForFeedback(HoldableSO expected, HoldableSO delivered, OrderIssueFlags missingFlag, OrderIssueFlags wrongFlag, OrderIssueFlags extraFlag, ref OrderIssueFlags issues)
		{
			if (expected == null)
			{
				if (delivered != null)
				{
					issues |= extraFlag;
				}
				return true;
			}
			if (delivered == null)
			{
				issues |= missingFlag;
				return false;
			}
			if (expected.Id != delivered.Id)
			{
				issues |= wrongFlag;
				return false;
			}
			return true;
		}

		private void ServerHandleMainDishPlaced(ServedDish servedDish)
		{
			if (servedDish == null || !base.IsServer)
			{
				return;
			}
			HoldableSO itemSO = servedDish.ItemSO;
			if (!(itemSO == null))
			{
				if (itemSO.Id == Singleton<GameProgressManager>.Instance.ItemDatabaseSO.CucumberMakiServeredDishSO.Id)
				{
					QuestReferenceHub.Instance.QuestComplete(QuestId.PlaceCucumberMakiOnTray);
				}
				if (itemSO.Id == Singleton<GameProgressManager>.Instance.ItemDatabaseSO.MackarelNigiriServeredDishSO.Id)
				{
					QuestReferenceHub.Instance.QuestComplete(QuestId.PlaceMackerelNigiriOnTray);
				}
			}
		}

		public bool TryGetMainDishId(out string mainDishId)
		{
			mainDishId = null;
			if (!TryGetSlotServedDish(0, out var servedDish))
			{
				return false;
			}
			if (string.IsNullOrWhiteSpace(servedDish.RuntimeRecipeIdentityId))
			{
				return false;
			}
			mainDishId = servedDish.RuntimeRecipeIdentityId;
			return true;
		}

		private bool HasSideDish()
		{
			BaseHoldable holdable;
			return TryGetSlotHoldable(1, out holdable);
		}

		private bool HasBeverage()
		{
			BaseHoldable holdable;
			return TryGetSlotHoldable(2, out holdable);
		}

		public bool HasAnyExtraDish()
		{
			if (!HasSideDish())
			{
				return HasBeverage();
			}
			return true;
		}

		public bool TryGetMainDish(out ServedDish servedDish)
		{
			return TryGetSlotServedDish(0, out servedDish);
		}

		private NetworkVariable<NetworkObjectReference> GetSlotRefByIndex(byte index)
		{
			return index switch
			{
				0 => _mainDishRef, 
				1 => _sideDishRef, 
				2 => _beverageRef, 
				_ => null, 
			};
		}

		public SnapPoint GetSnapPointByIndex(byte index)
		{
			return index switch
			{
				0 => _mainDishPoint, 
				1 => _sideDishPoint, 
				2 => _beveragePoint, 
				_ => null, 
			};
		}

		public bool TryGetBeverageDish(out ServedDish beverage)
		{
			return TryGetSlotServedDish(2, out beverage);
		}

		public bool TryGetSideDish(out ServedDish sideDish)
		{
			return TryGetSlotServedDish(1, out sideDish);
		}

		private static byte ResolveTraySlotIndex(ServedDish dish)
		{
			if (dish?.ItemSO is BeverageSO)
			{
				return 2;
			}
			if (dish?.ItemSO is SideDishSO)
			{
				return 1;
			}
			if (!(dish != null))
			{
				return 0;
			}
			return dish.GetIndexByDish();
		}

		private bool TryGetSlotHoldable(byte index, out BaseHoldable holdable)
		{
			holdable = null;
			NetworkVariable<NetworkObjectReference> slotRefByIndex = GetSlotRefByIndex(index);
			if (slotRefByIndex == null)
			{
				return false;
			}
			if (slotRefByIndex.Value.TryGetComponent<BaseHoldable>(out holdable, base.NetworkManager))
			{
				return holdable != null;
			}
			return false;
		}

		private bool TryGetSlotServedDish(byte index, out ServedDish servedDish)
		{
			servedDish = null;
			if (!TryGetSlotHoldable(index, out var holdable))
			{
				return false;
			}
			servedDish = holdable as ServedDish;
			return servedDish != null;
		}

		private bool ServerIsSlotAvailable(byte index)
		{
			if (!base.IsServer)
			{
				return false;
			}
			NetworkVariable<NetworkObjectReference> slotRefByIndex = GetSlotRefByIndex(index);
			if (slotRefByIndex == null)
			{
				return false;
			}
			if (!slotRefByIndex.Value.TryGetComponent<BaseHoldable>(out var component) || component == null)
			{
				slotRefByIndex.Value = default;
				return true;
			}
			return false;
		}

		private void ServerSetSlotReference(byte index, BaseHoldable holdable)
		{
			if (base.IsServer)
			{
				NetworkVariable<NetworkObjectReference> slotRefByIndex = GetSlotRefByIndex(index);
				if (slotRefByIndex != null)
				{
					slotRefByIndex.Value = ((holdable == null) ? default(NetworkObjectReference) : holdable.NetworkObject.ToReference());
				}
			}
		}

		private void ServerClearSlotByHoldable(BaseHoldable holdable)
		{
			if (!base.IsServer || holdable == null)
			{
				return;
			}
			for (byte b = 0; b < 3; b++)
			{
				NetworkVariable<NetworkObjectReference> slotRefByIndex = GetSlotRefByIndex(b);
				if (slotRefByIndex != null)
				{
					if (!slotRefByIndex.Value.TryGetComponent<BaseHoldable>(out var component) || component == null)
					{
						slotRefByIndex.Value = default;
					}
					else if (component == holdable)
					{
						slotRefByIndex.Value = default;
					}
				}
			}
		}

		public bool ServerRestoreDishInSlot(ServedDish dish, byte slotIndex, bool staffPlacement = false)
		{
			if (!base.IsServer || dish == null)
			{
				return false;
			}
			if (base.NetworkObject == null || !base.NetworkObject.IsSpawned)
			{
				return false;
			}
			if (dish.NetworkObject == null || !dish.NetworkObject.IsSpawned)
			{
				return false;
			}
			SnapPoint snapPointByIndex = GetSnapPointByIndex(slotIndex);
			if (snapPointByIndex == null)
			{
				return false;
			}
			if (!snapPointByIndex.CanSnap(dish))
			{
				return false;
			}
			if (!ServerIsSlotAvailable(slotIndex))
			{
				return false;
			}
			if (!ServerDetachDishFromPreviousParent(dish))
			{
				return false;
			}
			Vector3 position = snapPointByIndex.transform.position;
			Vector3 eulerAngles = snapPointByIndex.transform.rotation.eulerAngles;
			ServerSetSlotReference(slotIndex, dish);
			if (!dish.ServerPlaceOnParentDirect(base.NetworkObject, position, eulerAngles, null, staffPlacement))
			{
				ServerClearSlotByHoldable(dish);
				return false;
			}
			return true;
		}

		protected override void ClientHoldEveryone()
		{
			HandleDirtyShadowGraphic(IsDirty.Value);
			ClientRefreshParentedChildrenRuntimeRepresentationStates();
		}

		private void HandleDirtyShadowGraphic(bool status)
		{
			SetDirtyShadowGraphicStatus(_tpsShadowObject, status);
			SetDirtyShadowGraphicStatus(_fpsShadowObject, status);
		}

		private static void SetDirtyShadowGraphicStatus(GameObject shadowObject, bool status)
		{
			if (!(shadowObject == null))
			{
				Transform transform = FindChildRecursive(shadowObject.transform, "DirtyRoot");
				if (!(transform == null))
				{
					transform.gameObject.SetActive(status);
				}
			}
		}

		private static Transform FindChildRecursive(Transform root, string childName)
		{
			if (root == null)
			{
				return null;
			}
			for (int i = 0; i < root.childCount; i++)
			{
				Transform child = root.GetChild(i);
				if (child.name == childName)
				{
					return child;
				}
				Transform transform = FindChildRecursive(child, childName);
				if (transform != null)
				{
					return transform;
				}
			}
			return null;
		}

		[Rpc(SendTo.Server)]
		public void ServerTryPlaceDishOnTrayRpc(NetworkObjectReference dishRef, bool bypass, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2930546568u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in dishRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in bypass, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2930546568u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_dishPlacementLocked || base.IsStaffCarried || IsDirty.Value || !dishRef.TryGetComponent<ServedDish>(out var component) || component.IsStaffCarried)
			{
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (component.OwnerClientId != senderClientId || (!bypass && component.CanHold.Value))
			{
				return;
			}
			byte b = ResolveTraySlotIndex(component);
			SnapPoint snapPointByIndex = GetSnapPointByIndex(b);
			if (snapPointByIndex == null || !snapPointByIndex.CanSnap(component) || !ServerIsSlotAvailable(b))
			{
				return;
			}
			Vector3 position = snapPointByIndex.transform.position;
			Vector3 eulerAngles = snapPointByIndex.transform.rotation.eulerAngles;
			_dishPlacementLocked = true;
			CanHold.Value = false;
			ServerSetSlotReference(b, component);
			if (!component.ServerPlaceOnParentDirect(base.NetworkObject, position, eulerAngles))
			{
				ServerClearSlotByHoldable(component);
				_dishPlacementLocked = false;
				CanHold.Value = true;
				return;
			}
			if (b == 0)
			{
				ServerHandleMainDishPlaced(component);
			}
			if (b == 1)
			{
				QuestReferenceHub.Instance.QuestComplete(QuestId.TakeSideDishAndAddToTray);
			}
			_dishPlacementLocked = false;
			CanHold.Value = true;
		}

		protected override void ClientCrossMouse1(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			switch (phase)
			{
			case InputActionPhase.Started:
				_crossMouse1HoldPerformed = false;
				break;
			case InputActionPhase.Performed:
				_crossMouse1HoldPerformed = true;
				TryHandleTapPlacement(held);
				break;
			case InputActionPhase.Canceled:
				if (!_crossMouse1HoldPerformed)
				{
					if (held is ServedDish || held is SideDishIngredientEntity)
					{
						TryHandleTapPlacement(held);
					}
					else if (CanCrossHold(interactor, held))
					{
						interactor.TryHold(this);
					}
				}
				break;
			}
		}

		private void TryHandleTapPlacement(BaseHoldable held)
		{
			if (!IsDirty.Value)
			{
				if (held is SideDishIngredientEntity sideDishIngredientEntity)
				{
					ServerTryPlaceSideDishFromIngredientOnTrayRpc(sideDishIngredientEntity.NetworkObject.ToReference());
					return;
				}
				if (held is ServedDish servedDish)
				{
					ServerTryPlaceDishOnTrayRpc(servedDish.NetworkObject.ToReference(), bypass: false);
					return;
				}
				_ = held is Tray;
				Singleton<NotifyManager>.Instance.ShowToast("Can't place!");
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerTryPlaceSideDishFromIngredientOnTrayRpc(NetworkObjectReference ingredientRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3741134943u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in ingredientRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3741134943u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_dishPlacementLocked || base.IsStaffCarried || IsDirty.Value)
			{
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!ingredientRef.TryGetComponent<SideDishIngredientEntity>(out var component) || component.OwnerClientId != senderClientId || component.CanHold.Value)
			{
				return;
			}
			SnapPoint snapPointByIndex = GetSnapPointByIndex(1);
			if (snapPointByIndex == null || !ServerIsSlotAvailable(1))
			{
				return;
			}
			_dishPlacementLocked = true;
			if (!component.ServerTryCreateOutputSideDishForTray(senderClientId, out var servedDish))
			{
				_dishPlacementLocked = false;
				return;
			}
			if (ResolveTraySlotIndex(servedDish) != 1 || !snapPointByIndex.CanSnap(servedDish))
			{
				component.ServerReturnOutputSideDishToStock(servedDish);
				_dishPlacementLocked = false;
				return;
			}
			Vector3 position = snapPointByIndex.transform.position;
			Vector3 eulerAngles = snapPointByIndex.transform.rotation.eulerAngles;
			ServerSetSlotReference(1, servedDish);
			if (!servedDish.ServerPlaceOnParentDirect(base.NetworkObject, position, eulerAngles))
			{
				ServerClearSlotByHoldable(servedDish);
				component.ServerReturnOutputSideDishToStock(servedDish);
				_dishPlacementLocked = false;
			}
			else
			{
				QuestReferenceHub.Instance.QuestComplete(QuestId.TakeSideDishAndAddToTray);
				_dishPlacementLocked = false;
			}
		}

		protected override void ClientCrossMouse2(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			interactor.CurrentHeldTrigger(InputKind.Mouse2, phase);
		}

		public override void EnableGazeHints()
		{
			Singleton<HintsManager>.Instance.ClearAllHints();
			Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_hold");
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
		}

		public override void EnableHeldHints()
		{
			Singleton<HintsManager>.Instance.ClearAllHints();
			bool flag = _ownerInteractor != null && _ownerInteractor.Placer != null && _ownerInteractor.Placer.IsTrayTrolleyPassivePreview;
			Singleton<HintsManager>.Instance.AddHint(flag ? "LeftMouseMechanic" : "RightMouseMechanic", "key_place");
			AddHeldThrowHint();
		}

		public override void DisableHeldHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
			Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
			RemoveHeldThrowHint();
		}

		private bool HasAnyDish()
		{
			if (TryGetSlotHoldable(0, out var holdable))
			{
				return true;
			}
			if (TryGetSlotHoldable(1, out holdable))
			{
				return true;
			}
			if (TryGetSlotHoldable(2, out holdable))
			{
				return true;
			}
			return HasAnyDishInParentedChildren();
		}

		private bool HasAnyDishInParentedChildren()
		{
			for (int i = 0; i < _parentedChilds.Count; i++)
			{
				NetworkObjectReference reference = _parentedChilds[i];
				if (!reference.Equals(default) && reference.TryGetComponent<BaseHoldable>(out var component) && !(component is Tray))
				{
					return true;
				}
			}
			return false;
		}

		[Rpc(SendTo.Server)]
		public void ServerTryPlaceWorldDishOnHeldTrayRpc(NetworkObjectReference dishRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(4187419913u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in dishRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 4187419913u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (dishRef.TryGetComponent<ServedDish>(out var component) && !component.IsStaffCarried)
				{
					ServerTryPlaceWorldDishOnHeldTray(component, rpcParams.Receive.SenderClientId);
				}
			}
		}

		public bool ServerTryPlaceWorldDishOnHeldTray(ServedDish dish, ulong senderClientId)
		{
			if (!base.IsServer || _dishPlacementLocked || IsDirty.Value || base.IsStaffCarried)
			{
				return false;
			}
			if (dish == null || base.OwnerClientId != senderClientId || CanHold.Value)
			{
				return false;
			}
			if (!dish.CanHold.Value)
			{
				return false;
			}
			if (dish.transform.parent != null && dish.transform.parent.GetComponentInParent<Tray>() != null)
			{
				return false;
			}
			byte b = ResolveTraySlotIndex(dish);
			SnapPoint snapPointByIndex = GetSnapPointByIndex(b);
			if (snapPointByIndex == null)
			{
				return false;
			}
			if (!snapPointByIndex.CanSnap(dish))
			{
				return false;
			}
			if (!ServerIsSlotAvailable(b))
			{
				return false;
			}
			if (!ServerDetachDishFromPreviousParent(dish))
			{
				return false;
			}
			Vector3 position = snapPointByIndex.transform.position;
			Vector3 eulerAngles = snapPointByIndex.transform.rotation.eulerAngles;
			_dishPlacementLocked = true;
			ServerSetSlotReference(b, dish);
			dish.ServerRaiseTakenFromDeskEventIfNeeded();
			if (!dish.ServerPlaceOnParentDirect(base.NetworkObject, position, eulerAngles))
			{
				ServerClearSlotByHoldable(dish);
				_dishPlacementLocked = false;
				return false;
			}
			if (b == 0)
			{
				ServerHandleMainDishPlaced(dish);
			}
			if (b == 1)
			{
				QuestReferenceHub.Instance.QuestComplete(QuestId.TakeSideDishAndAddToTray);
			}
			ServerRefreshHeldTrayChildAfterDirectDishPlacement(dish.NetworkObject.ToReference());
			_dishPlacementLocked = false;
			return true;
		}

		public bool ServerTryRemoveOptionalDishForWholesaleReturn(string itemId, out ServedDish dish)
		{
			dish = null;
			if (!base.IsServer || IsDirty.Value || string.IsNullOrWhiteSpace(itemId))
			{
				return false;
			}
			for (byte b = 1; b <= 2; b++)
			{
				if (TryGetSlotServedDish(b, out var servedDish) && !(servedDish.ItemSO == null) && !(servedDish.ItemSO.Id != itemId))
				{
					ServerUnParentFromMe(servedDish);
					dish = servedDish;
					return true;
				}
			}
			return false;
		}

		public void ServerRefreshHeldTrayContentsGraphics()
		{
			if (base.IsServer && !CanHold.Value)
			{
				RefreshHeldTrayContentsAfterPlacementReplication();
			}
		}

		private void ServerRefreshHeldTrayChildAfterDirectDishPlacement(NetworkObjectReference childRef)
		{
			if (!base.IsServer || CanHold.Value)
			{
				return;
			}
			Singleton<DelayHelper>.Instance.DelayActionUntilEndOfFrame(() =>
			{
				if (base.IsServer && !CanHold.Value)
				{
					ServerChangeChildsStatus(status: false, forget: false);
					ClientRefreshHeldTrayParentedChildGraphicsRpc(childRef);
				}
			});
		}

		private void RefreshHeldTrayContentsAfterPlacementReplication()
		{
			Singleton<DelayHelper>.Instance.DelayActionUntilEndOfFrame(() =>
			{
				if (base.IsServer && !CanHold.Value)
				{
					ServerChangeChildsStatus(status: false, forget: false);
					ClientRefreshHeldTrayParentedGraphicsRpc();
				}
			});
		}

		[Rpc(SendTo.Everyone)]
		private void ClientDisableStoredContentsCollidersRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(2735699079u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2735699079u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!CanHold.Value)
			{
				return;
			}
			foreach (NetworkObjectReference parentedChild in _parentedChilds)
			{
				if (parentedChild.TryGetComponent<BaseHoldable>(out var component))
				{
					component.CollidersStatus(status: false);
				}
			}
		}

		private bool ServerDetachDishFromPreviousParent(ServedDish dish)
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (dish == null)
			{
				return false;
			}
			if (dish.transform.parent != null && dish.transform.parent.TryGetComponent<BaseHoldable>(out var component))
			{
				component.ServerUnParentFromMe(dish);
			}
			if (dish.NetworkObject.CurrentParent != null && !dish.NetworkObject.TryRemoveParent())
			{
				return false;
			}
			return true;
		}

		[Rpc(SendTo.Everyone)]
		private void ClientRefreshHeldTrayParentedGraphicsRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1229612206u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1229612206u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!CanHold.Value)
			{
				SpawnTpsParenteds(base.IsOwner);
				if (base.IsOwner)
				{
					SpawnFpsParenteds();
				}
				ClientRefreshParentedChildrenRuntimeRepresentationStates();
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientRefreshHeldTrayParentedChildGraphicsRpc(NetworkObjectReference childRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(4236582035u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in childRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 4236582035u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!CanHold.Value && childRef.TryGetComponent<BaseHoldable>(out var component))
				{
					ClientRefreshParentedChildRuntimeRepresentationState(component);
				}
			}
		}

		public bool ServerStaffTryPickup(int employeeId)
		{
			if (!base.IsServer || !base.IsSpawned || !CanHold.Value || base.IsStaffCarried || base.IsPlayerInventoryOwnedForPersistence)
			{
				return false;
			}
			if (base.transform.parent != null)
			{
				TrayTrolley componentInParent = base.transform.GetComponentInParent<TrayTrolley>();
				if (componentInParent == null || !componentInParent.IsPhysicallyUsable || !componentInParent.ServerStaffAtHome(this))
				{
					return false;
				}
				if (!base.NetworkObject.TryRemoveParent())
				{
					return false;
				}
			}
			return ServerTryStaffPickup(employeeId);
		}

		public ServedDish ServerStaffDish(byte slot)
		{
			if (!TryGetSlotServedDish(slot, out var servedDish))
			{
				return null;
			}
			return servedDish;
		}

		public bool ServerStaffMatches(CustomerOrderData order)
		{
			if (IsDirty.Value || order == null || !order.HasMainDish)
			{
				return false;
			}
			ServedDishSO[] array = new ServedDishSO[3] { order.MainDishSO, order.SideDishSO, order.BeverageSO };
			for (byte b = 0; b < 3; b++)
			{
				ServedDish servedDish = ServerStaffDish(b);
				if (b == 0)
				{
					if (servedDish == null || servedDish.RuntimeRecipeIdentityId != order.MainDishIdentityId)
					{
						return false;
					}
				}
				else if ((array[b] == null) ? (servedDish != null) : (servedDish?.ItemSO?.Id != array[b].Id))
				{
					return false;
				}
			}
			return true;
		}

		public bool ServerStaffPlace(int employeeId, Vector3 position, Quaternion rotation, bool onTable)
		{
			if (!ServerStaffCarriedBy(employeeId) || (!onTable && IsDirty.Value))
			{
				return false;
			}
			ServerStaffDrop(employeeId, position, rotation);
			ServerSetPlacementTooltipSuppressed(!onTable);
			CanHold.Value = !onTable;
			CanGaze.Value = !onTable;
			ServerChangeChildsStatus(status: true, onTable);
			if (!onTable)
			{
				ClientDisableStoredContentsCollidersRpc();
			}
			return true;
		}

		public bool ServerStaffClean(int employeeId)
		{
			if (!ServerStaffCarriedBy(employeeId) || !IsDirty.Value)
			{
				return false;
			}
			ServerEatingDone();
			IsDirty.Value = false;
			_dishPlacementLocked = false;
			StaffQuestProgress.Complete(QuestId.ClearTheTray);
			return true;
		}

		public bool ServerStaffAddDish(int employeeId, ServedDish dish, byte slot)
		{
			if (!ServerStaffCarriedBy(employeeId) || IsDirty.Value || dish == null || dish.IsStaffCarried || dish.IsPlayerInventoryOwnedForPersistence)
			{
				return false;
			}
			if (!ServerRestoreDishInSlot(dish, slot, staffPlacement: true))
			{
				return false;
			}
			StaffQuestProgress.TrayLoaded(dish.ItemSO as ServedDishSO, slot);
			ServerStaffSetCarrier(employeeId);
			return true;
		}

		public bool ServerStaffStoreDish(int employeeId, byte slot, Transform point)
		{
			if (!ServerStaffCarriedBy(employeeId) || point == null)
			{
				return false;
			}
			ServedDish servedDish = ServerStaffDish(slot);
			if (servedDish == null)
			{
				return true;
			}
			ServerUnParentFromMe(servedDish);
			servedDish.NetworkObject.TryRemoveParent();
			servedDish.ServerStaffDrop(employeeId, point.position, point.rotation);
			return true;
		}

		protected override void __initializeVariables()
		{
			if (IsDirty == null)
			{
				throw new Exception("Tray.IsDirty cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			IsDirty.Initialize(this);
			__nameNetworkVariable(IsDirty, "IsDirty");
			NetworkVariableFields.Add(IsDirty);
			if (_mainDishRef == null)
			{
				throw new Exception("Tray._mainDishRef cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_mainDishRef.Initialize(this);
			__nameNetworkVariable(_mainDishRef, "_mainDishRef");
			NetworkVariableFields.Add(_mainDishRef);
			if (_sideDishRef == null)
			{
				throw new Exception("Tray._sideDishRef cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_sideDishRef.Initialize(this);
			__nameNetworkVariable(_sideDishRef, "_sideDishRef");
			NetworkVariableFields.Add(_sideDishRef);
			if (_beverageRef == null)
			{
				throw new Exception("Tray._beverageRef cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_beverageRef.Initialize(this);
			__nameNetworkVariable(_beverageRef, "_beverageRef");
			NetworkVariableFields.Add(_beverageRef);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3378631521u, __rpc_handler_3378631521, "ServerDirtyStatusRpc", RpcInvokePermission.Everyone);
			__registerRpc(2930546568u, __rpc_handler_2930546568, "ServerTryPlaceDishOnTrayRpc", RpcInvokePermission.Everyone);
			__registerRpc(3741134943u, __rpc_handler_3741134943, "ServerTryPlaceSideDishFromIngredientOnTrayRpc", RpcInvokePermission.Everyone);
			__registerRpc(4187419913u, __rpc_handler_4187419913, "ServerTryPlaceWorldDishOnHeldTrayRpc", RpcInvokePermission.Everyone);
			__registerRpc(2735699079u, __rpc_handler_2735699079, "ClientDisableStoredContentsCollidersRpc", RpcInvokePermission.Everyone);
			__registerRpc(1229612206u, __rpc_handler_1229612206, "ClientRefreshHeldTrayParentedGraphicsRpc", RpcInvokePermission.Everyone);
			__registerRpc(4236582035u, __rpc_handler_4236582035, "ClientRefreshHeldTrayParentedChildGraphicsRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3378631521(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((Tray)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Tray)target).ServerDirtyStatusRpc(value, ext);
				((Tray)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2930546568(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((Tray)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Tray)target).ServerTryPlaceDishOnTrayRpc(value, value2, ext);
				((Tray)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3741134943(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((Tray)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Tray)target).ServerTryPlaceSideDishFromIngredientOnTrayRpc(value, ext);
				((Tray)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4187419913(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((Tray)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Tray)target).ServerTryPlaceWorldDishOnHeldTrayRpc(value, ext);
				((Tray)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2735699079(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((Tray)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Tray)target).ClientDisableStoredContentsCollidersRpc();
				((Tray)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1229612206(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((Tray)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Tray)target).ClientRefreshHeldTrayParentedGraphicsRpc();
				((Tray)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4236582035(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((Tray)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Tray)target).ClientRefreshHeldTrayParentedChildGraphicsRpc(value);
				((Tray)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "Tray";
		}
	}
}
