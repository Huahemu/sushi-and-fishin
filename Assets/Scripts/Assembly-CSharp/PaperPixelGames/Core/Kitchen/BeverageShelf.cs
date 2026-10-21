using System.Collections.Generic;
using EPOOutline;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen.ServedDishes;
using PaperPixelGames.Core.Kitchen.Wholasale;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Kitchen
{
	public class BeverageShelf : BaseInteractable, IHeldItemMouse1Interactable
	{
		[SerializeField]
		private BeverageFridge _beverageFridge;

		[SerializeField]
		private byte _shelfIndex;

		[SerializeField]
		private int _maxBeverages = 10;

		[SerializeField]
		private List<BeverageShelfVisualSet> _visualSets = new List<BeverageShelfVisualSet>();

		private BeverageType _currentType;

		private int _currentCount;

		private readonly List<Outlinable> _activeTakeOutlines = new List<Outlinable>();

		private BaseHoldable _pendingPlacedHeldItem;

		private float _pendingPlacedHeldItemUntil;

		private const float PendingPlacedHeldItemTimeout = 0.75f;

		public void Initialize(BeverageFridge beverageFridge, byte shelfIndex)
		{
			_beverageFridge = beverageFridge;
			_shelfIndex = shelfIndex;
		}

		protected override void ClientMouse1(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && !(_beverageFridge == null) && !_beverageFridge.IsDoorOpen)
			{
				_beverageFridge.BeverageDoor.TryToggleDoor(interactor, InputKind.Mouse1, phase);
			}
		}

		protected override void ClientCrossMouse1(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (phase != InputActionPhase.Started || _beverageFridge == null)
			{
				return;
			}
			if (!_beverageFridge.IsDoorOpen)
			{
				_beverageFridge.BeverageDoor.TryToggleDoor(interactor, InputKind.Mouse1, phase);
			}
			else if (held is Tray tray)
			{
				if (tray.TryGetBeverageDish(out var _))
				{
					TryReturnBeverageFromTray(interactor, tray);
				}
				else
				{
					TryTakeBeverageToTray(interactor, tray);
				}
			}
			else
			{
				TryPlaceHeldBeverage(interactor, held);
			}
		}

		protected override void ClientInteract(Interactor interactor, InputActionPhase phase)
		{
			base.ClientInteract(interactor, phase);
			if (phase == InputActionPhase.Started && !(_beverageFridge == null) && !_beverageFridge.IsPhysicallyUsable)
			{
				_beverageFridge.TryUnlockPhysicalStateAndOpenTutorial(interactor);
			}
		}

		protected override void ClientCrossInteract(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			ClientInteract(interactor, phase);
		}

		protected override void ClientMouse2(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				base.ClientMouse2(interactor, phase);
				TryTakeBeverage(interactor);
			}
		}

		protected override void ClientCrossMouse2(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				base.ClientCrossMouse2(interactor, held, phase);
				if (held is Tray tray)
				{
					TryTakeBeverageToTray(interactor, tray);
				}
				else
				{
					TryTakeBeverage(interactor);
				}
			}
		}

		private void TryPlaceHeldBeverage(Interactor interactor, BaseHoldable held)
		{
			if (!EnsureDoorOpen() || _beverageFridge == null || interactor == null || held == null)
			{
				return;
			}
			if (held.TryGetComponent<WholesaleEntity>(out var component))
			{
				TryPlaceFromWholesale(component);
				return;
			}
			if (!(held.ItemSO is BeverageSO beverageSO))
			{
				Singleton<NotifyManager>.Instance.ShowToast("Wrong item.", NotificationState.Error);
				return;
			}
			if (!held.TryGetComponent<ServedDishBeverage>(out var _))
			{
				Singleton<NotifyManager>.Instance.ShowToast("Wrong beverage.", NotificationState.Error);
				return;
			}
			BeverageType beverageType = beverageSO.BeverageType;
			if (!CanAccept(beverageType))
			{
				ShowRejectReason(beverageType);
				return;
			}
			MarkHeldItemAsPendingPlacement(held);
			ClearPlacementPreview();
			RefreshGazeFeedback();
			_beverageFridge.PlayPlaceSfx();
			_beverageFridge.ServerPlaceLooseBeverageRpc(_shelfIndex, (byte)beverageType, interactor.NetworkObject.ToReference(), held.NetworkObject.ToReference());
		}

		private void TryPlaceFromWholesale(WholesaleEntity wholesaleEntity)
		{
			if (EnsureDoorOpen() && !(wholesaleEntity == null))
			{
				if (!(wholesaleEntity.WholesaleSO.OutputItem is BeverageSO { BeverageType: var beverageType }))
				{
					Singleton<NotifyManager>.Instance.ShowToast("Wrong item.", NotificationState.Error);
					return;
				}
				if (!CanAccept(beverageType))
				{
					ShowRejectReason(beverageType);
					return;
				}
				_beverageFridge.PlayPlaceSfx();
				wholesaleEntity.ServerTryConsumeAndFillBeverageRpc(_beverageFridge.NetworkObject.ToReference(), _shelfIndex, (byte)beverageType);
			}
		}

		private void TryTakeBeverage(Interactor interactor)
		{
			if (!EnsureDoorOpen() || _beverageFridge == null || interactor == null)
			{
				return;
			}
			if (_currentCount <= 0)
			{
				Singleton<NotifyManager>.Instance.ShowToast("Shelf empty.");
				return;
			}
			BeverageSO beverageSO = GetBeverageSO(_currentType);
			if (beverageSO == null || Singleton<QuickSlotHandler>.Instance == null || !Singleton<QuickSlotHandler>.Instance.CanHold(beverageSO))
			{
				Singleton<NotifyManager>.Instance.ShowToast("No empty slot.", NotificationState.Error);
				return;
			}
			ClearTakeOutline();
			_beverageFridge.PlayTakeSfx();
			_beverageFridge.ServerTakeFromShelfRpc(_shelfIndex, interactor.NetworkObject.ToReference());
		}

		private void TryTakeBeverageToTray(Interactor interactor, Tray tray)
		{
			if (EnsureDoorOpen() && !(interactor?.NetworkObject == null) && !(tray?.NetworkObject == null))
			{
				if (_currentCount <= 0)
				{
					Singleton<NotifyManager>.Instance.ShowToast("Shelf empty.");
					return;
				}
				ClearTakeOutline();
				_beverageFridge.ServerTakeFromShelfToTrayRpc(_shelfIndex, interactor.NetworkObject.ToReference(), tray.NetworkObject.ToReference());
			}
		}

		private void TryReturnBeverageFromTray(Interactor interactor, Tray tray)
		{
			if (EnsureDoorOpen() && !(interactor?.NetworkObject == null) && !(tray?.NetworkObject == null))
			{
				_beverageFridge.ServerReturnBeverageFromTrayRpc(_shelfIndex, interactor.NetworkObject.ToReference(), tray.NetworkObject.ToReference());
			}
		}

		private bool CanAccept(BeverageType type)
		{
			if (!BeverageFridge.IsValidType(type))
			{
				return false;
			}
			if (GetVisualSet(type) == null)
			{
				return false;
			}
			if (_currentCount > 0 && _currentType != type)
			{
				return false;
			}
			return _currentCount < GetCapacityFor(type);
		}

		private void ShowRejectReason(BeverageType type)
		{
			if (_currentCount > 0 && _currentType != type)
			{
				Singleton<NotifyManager>.Instance.ShowToast("Different beverage already placed.", NotificationState.Error);
			}
			else if (_currentCount >= GetCapacityFor(type))
			{
				Singleton<NotifyManager>.Instance.ShowToast("Shelf full.", NotificationState.Error);
			}
			else
			{
				Singleton<NotifyManager>.Instance.ShowToast("Wrong beverage.", NotificationState.Error);
			}
		}

		public int GetCapacityFor(BeverageType type)
		{
			return GetVisualSet(type)?.GetCapacity(_maxBeverages) ?? 0;
		}

		public BeverageSO GetBeverageSO(BeverageType type)
		{
			return GetVisualSet(type)?.BeverageSO;
		}

		public void RebuildVisuals(BeverageType type, int count)
		{
			ClearTakeOutline();
			_currentType = ((count > 0) ? type : BeverageType.Empty);
			_currentCount = Mathf.Max(0, count);
			for (int i = 0; i < _visualSets.Count; i++)
			{
				_visualSets[i]?.DisableAllOutlines();
				_visualSets[i]?.SetAllActive(active: false);
			}
			if (_currentCount <= 0)
			{
				RefreshHints();
				return;
			}
			BeverageShelfVisualSet visualSet = GetVisualSet(type);
			if (visualSet == null)
			{
				RefreshHints();
				return;
			}
			_currentCount = Mathf.Clamp(_currentCount, 0, visualSet.GetCapacity(_maxBeverages));
			visualSet.Rebuild(_currentCount, _maxBeverages);
			RefreshHints();
		}

		private BeverageShelfVisualSet GetVisualSet(BeverageType type)
		{
			for (int i = 0; i < _visualSets.Count; i++)
			{
				BeverageShelfVisualSet beverageShelfVisualSet = _visualSets[i];
				if (beverageShelfVisualSet != null && beverageShelfVisualSet.Type == type)
				{
					return beverageShelfVisualSet;
				}
			}
			return null;
		}

		private bool EnsureDoorOpen()
		{
			if (_beverageFridge != null && _beverageFridge.IsDoorOpen)
			{
				return true;
			}
			if (Singleton<NotifyManager>.Instance != null)
			{
				Singleton<NotifyManager>.Instance.ShowToast("Open the fridge door first.");
			}
			return false;
		}

		private bool CanPlaceHeldItem(BaseHoldable held)
		{
			if (held == null)
			{
				return false;
			}
			if (IsHeldItemPendingPlacement(held))
			{
				return false;
			}
			if (_beverageFridge == null || !_beverageFridge.IsDoorOpen)
			{
				return false;
			}
			if (held.TryGetComponent<WholesaleEntity>(out var component))
			{
				if (component.WholesaleSO == null)
				{
					return false;
				}
				if (component.CurrentCount <= 0)
				{
					return false;
				}
				if (!(component.WholesaleSO.OutputItem is BeverageSO beverageSO))
				{
					return false;
				}
				return CanAccept(beverageSO.BeverageType);
			}
			if (!(held.ItemSO is BeverageSO beverageSO2))
			{
				return false;
			}
			if (!held.TryGetComponent<ServedDishBeverage>(out var _))
			{
				return false;
			}
			return CanAccept(beverageSO2.BeverageType);
		}

		private void RefreshPlacementPreview()
		{
			ClearPlacementPreview();
			if (_beverageFridge == null || !_beverageFridge.IsDoorOpen)
			{
				return;
			}
			BaseHoldable held = ((Singleton<QuickSlotHandler>.Instance != null) ? Singleton<QuickSlotHandler>.Instance.SelectedItem : null);
			if (!TryGetPlaceableBeverageType(held, out var type) || !CanAccept(type))
			{
				return;
			}
			BeverageShelfVisualSet visualSet = GetVisualSet(type);
			if (visualSet != null)
			{
				int currentCount = _currentCount;
				if (currentCount >= 0 && currentCount < visualSet.GetCapacity(_maxBeverages))
				{
					visualSet.ShowPreviewAt(currentCount);
				}
			}
		}

		private void ClearPlacementPreview()
		{
			for (int i = 0; i < _visualSets.Count; i++)
			{
				if (_visualSets[i] != null)
				{
					_visualSets[i].SetAllPreviewActive(active: false);
				}
			}
		}

		private bool TryGetPlaceableBeverageType(BaseHoldable held, out BeverageType type)
		{
			type = BeverageType.Empty;
			if (held == null)
			{
				return false;
			}
			if (IsHeldItemPendingPlacement(held))
			{
				return false;
			}
			if (held is Tray tray)
			{
				return TryGetTrayBeverageType(tray, out type);
			}
			if (held.TryGetComponent<WholesaleEntity>(out var component))
			{
				if (component.WholesaleSO == null)
				{
					return false;
				}
				if (component.CurrentCount <= 0)
				{
					return false;
				}
				if (!(component.WholesaleSO.OutputItem is BeverageSO beverageSO))
				{
					return false;
				}
				type = beverageSO.BeverageType;
				return true;
			}
			if (!(held.ItemSO is BeverageSO beverageSO2))
			{
				return false;
			}
			if (!held.TryGetComponent<ServedDishBeverage>(out var _))
			{
				return false;
			}
			type = beverageSO2.BeverageType;
			return true;
		}

		private static bool TryGetTrayBeverageType(Tray tray, out BeverageType type)
		{
			type = BeverageType.Empty;
			if (tray == null || !tray.TryGetBeverageDish(out var beverage) || !(beverage?.ItemSO is BeverageSO beverageSO))
			{
				return false;
			}
			type = beverageSO.BeverageType;
			return true;
		}

		private void MarkHeldItemAsPendingPlacement(BaseHoldable held)
		{
			if (!(held == null))
			{
				_pendingPlacedHeldItem = held;
				_pendingPlacedHeldItemUntil = Time.unscaledTime + 0.75f;
			}
		}

		private bool IsHeldItemPendingPlacement(BaseHoldable held)
		{
			if (held == null)
			{
				return false;
			}
			if (_pendingPlacedHeldItem == null)
			{
				return false;
			}
			if (Time.unscaledTime > _pendingPlacedHeldItemUntil)
			{
				ClearPendingPlacement();
				return false;
			}
			return (object)_pendingPlacedHeldItem == held;
		}

		private void ClearPendingPlacement()
		{
			_pendingPlacedHeldItem = null;
			_pendingPlacedHeldItemUntil = 0f;
		}

		private void RefreshTakeOutline()
		{
			ClearTakeOutline();
			if (!_inGaze || _beverageFridge == null || !_beverageFridge.IsDoorOpen || _currentCount <= 0)
			{
				return;
			}
			BeverageShelfVisualSet visualSet = GetVisualSet(_currentType);
			if (visualSet == null)
			{
				return;
			}
			int index = _currentCount - 1;
			GameObject beverageObjectAt = visualSet.GetBeverageObjectAt(index);
			if (beverageObjectAt == null || !beverageObjectAt.activeInHierarchy)
			{
				return;
			}
			Outlinable[] componentsInChildren = beverageObjectAt.GetComponentsInChildren<Outlinable>(includeInactive: true);
			if (componentsInChildren == null || componentsInChildren.Length == 0)
			{
				return;
			}
			foreach (Outlinable outlinable in componentsInChildren)
			{
				if (!(outlinable == null))
				{
					outlinable.enabled = true;
					_activeTakeOutlines.Add(outlinable);
				}
			}
		}

		private void ClearTakeOutline()
		{
			for (int i = 0; i < _activeTakeOutlines.Count; i++)
			{
				Outlinable outlinable = _activeTakeOutlines[i];
				if (!(outlinable == null))
				{
					outlinable.enabled = false;
				}
			}
			_activeTakeOutlines.Clear();
		}

		public override void GazeEnter()
		{
			base.GazeEnter();
			SubscribeToQuickSlotSelectionChanged();
			RefreshPlacementPreview();
			RefreshTakeOutline();
		}

		public override void GazeExit()
		{
			UnsubscribeFromQuickSlotSelectionChanged();
			ClearPlacementPreview();
			ClearTakeOutline();
			base.GazeExit();
		}

		public override void EnableGazeHints()
		{
			if (Singleton<HintsManager>.Instance == null)
			{
				return;
			}
			Singleton<HintsManager>.Instance.ClearAllHints();
			if (_beverageFridge == null)
			{
				return;
			}
			if (!_beverageFridge.IsPhysicallyUsable)
			{
				Singleton<HintsManager>.Instance.AddHint("Interact", "key_repair");
				_beverageFridge.ShowUnlockTooltipLocal();
				return;
			}
			if (!_beverageFridge.IsDoorOpen)
			{
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_openlid");
				return;
			}
			BaseHoldable baseHoldable = ((Singleton<QuickSlotHandler>.Instance != null) ? Singleton<QuickSlotHandler>.Instance.SelectedItem : null);
			if (baseHoldable is Tray tray)
			{
				if (TryGetTrayBeverageType(tray, out var type) && CanAccept(type))
				{
					Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_place");
				}
				if (_currentCount > 0 && !tray.TryGetBeverageDish(out var _))
				{
					Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_take_item_to_tray");
					Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_take_item_to_tray");
				}
			}
			else if (baseHoldable != null && CanPlaceHeldItem(baseHoldable))
			{
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_place");
			}
			if (_currentCount > 0 && !(baseHoldable is Tray))
			{
				Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_take");
			}
		}

		public override void DisableGazeHints()
		{
			_beverageFridge?.HideUnlockTooltipLocal();
			if (!(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
				Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
				Singleton<HintsManager>.Instance.RemoveHint("Interact");
			}
		}

		public void RefreshHints()
		{
			RefreshGazeFeedback();
			RefreshPlacementPreview();
			RefreshTakeOutline();
		}

		public void ResetRuntimeState()
		{
			ClearPendingPlacement();
			ClearPlacementPreview();
			ClearTakeOutline();
			for (int i = 0; i < _visualSets.Count; i++)
			{
				_visualSets[i]?.DisableAllOutlines();
			}
			RefreshHints();
		}

		private void SubscribeToQuickSlotSelectionChanged()
		{
			if (!(Singleton<QuickSlotHandler>.Instance == null))
			{
				Singleton<QuickSlotHandler>.Instance.SelectedItemChanged -= OnQuickSlotSelectedItemChanged;
				Singleton<QuickSlotHandler>.Instance.SelectedItemChanged += OnQuickSlotSelectedItemChanged;
			}
		}

		private void UnsubscribeFromQuickSlotSelectionChanged()
		{
			if (!(Singleton<QuickSlotHandler>.Instance == null))
			{
				Singleton<QuickSlotHandler>.Instance.SelectedItemChanged -= OnQuickSlotSelectedItemChanged;
			}
		}

		private void OnQuickSlotSelectedItemChanged(BaseHoldable selectedItem)
		{
			if (_inGaze)
			{
				RefreshHints();
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "BeverageShelf";
		}
	}
}
