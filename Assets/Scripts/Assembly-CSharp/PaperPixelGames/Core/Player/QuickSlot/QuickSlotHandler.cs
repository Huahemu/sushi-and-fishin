using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen.ServedDishes;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Core.UI;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Player.QuickSlot
{
	public class QuickSlotHandler : Singleton<QuickSlotHandler>, ISaveable<QuickSlotSaveData>
	{
		[SerializeField]
		private EconomyUI _economyUI;

		[Space]
		[SerializeField]
		private Transform _slotsParent;

		[SerializeField]
		private QuickSlotElement _quickSlotElementPrefab;

		[SerializeField]
		private HeldSlotElement _heldSlotElement;

		[SerializeField]
		private TextMeshProUGUI _infoText;

		[SerializeField]
		private int _slotSize;

		[SerializeField]
		private float _scrollRepeatDelay = 0.12f;

		[Header("Fish Slip Spill")]
		[SerializeField]
		private float _fishSlipSpillDelay = 0.2f;

		[SerializeField]
		private float _fishSlipDropForwardOffset = 0.75f;

		[SerializeField]
		private float _fishSlipDropHeight = 0.2f;

		[SerializeField]
		private float _fishSlipItemSpacing = 0.18f;

		private List<QuickSlotElement> _slots = new List<QuickSlotElement>();

		private PPG_Player _player;

		private float _nextScrollTime;

		private int _selectedSlotIndex = -1;

		private int _availableSlot;

		private BaseHoldable _suspendedHeldSlotItem;

		private HeldSlotSuspensionReason _heldSlotSuspensionReasons;

		private BaseHoldable _lastNotifiedSelectedItem;

		public override SingletonScope Scope => SingletonScope.Scene;

		public bool HeldSlotAvailable => _heldSlotElement.IsEmpty;

		public bool AnySlotAvailable => _availableSlot > 0;

		public HeldSlotElement HeldSlotElement => _heldSlotElement;

		public int AvailableSlotCount => _availableSlot;

		public BaseHoldable SelectedItem
		{
			get
			{
				if (_heldSlotSuspensionReasons == HeldSlotSuspensionReason.None && _heldSlotElement != null && !_heldSlotElement.IsEmpty)
				{
					return _heldSlotElement.HeldItem;
				}
				if (!IsValidSlotIndex(_selectedSlotIndex))
				{
					return null;
				}
				return _slots[_selectedSlotIndex].PeekItem();
			}
		}

		public event Action<BaseHoldable> SelectedItemChanged;

		public int GetAvailableStackCapacity(HoldableSO holdableSO)
		{
			if (holdableSO == null || string.IsNullOrEmpty(holdableSO.Id))
			{
				return 0;
			}
			int num = 0;
			for (int i = 0; i < _slots.Count; i++)
			{
				QuickSlotElement quickSlotElement = _slots[i];
				if (!(quickSlotElement == null) && !quickSlotElement.IsEmpty && !(quickSlotElement.StackId != holdableSO.Id))
				{
					num += quickSlotElement.AvailableStackCapacity;
				}
			}
			return num;
		}

		public void Initialize(PPG_Player player)
		{
			_player = player;
			_availableSlot = _slotSize;
			for (int i = 0; i < _slotSize; i++)
			{
				QuickSlotElement quickSlotElement = UnityEngine.Object.Instantiate(_quickSlotElementPrefab, _slotsParent);
				_slots.Add(quickSlotElement);
				quickSlotElement.Init(i);
			}
			SetVisibility(status: true);
			RegisterInput();
			_heldSlotElement.Clear();
			_suspendedHeldSlotItem = null;
			_heldSlotSuspensionReasons = HeldSlotSuspensionReason.None;
			_economyUI.gameObject.SetActive(value: true);
			_heldSlotElement.gameObject.SetActive(value: true);
		}

		private void RegisterInput()
		{
			Singleton<InputManager>.Instance.InputHandler.Scroll += OnScroll;
			Singleton<InputManager>.Instance.InputHandler.Slot += OnSlotNumber;
		}

		public void UnRegisterInput()
		{
			Singleton<InputManager>.Instance.InputHandler.Scroll -= OnScroll;
			Singleton<InputManager>.Instance.InputHandler.Slot -= OnSlotNumber;
		}

		public void SetVisibility(bool status)
		{
			_slotsParent.gameObject.SetActive(status);
		}

		public bool CanHold(BaseHoldable holdable)
		{
			if (holdable == null || holdable.ItemSO == null)
			{
				return false;
			}
			if (holdable.RequiresHeldSlot)
			{
				return HeldSlotAvailable;
			}
			return CanHold(holdable.ItemSO, holdable.QuickSlotStackId);
		}

		public bool CanHold(HoldableSO holdableSO)
		{
			return CanHold(holdableSO, (holdableSO != null) ? holdableSO.Id : string.Empty);
		}

		private bool CanHold(HoldableSO holdableSO, string stackId)
		{
			if (holdableSO == null)
			{
				return false;
			}
			if (AnySlotAvailable)
			{
				return true;
			}
			for (int i = 0; i < _slots.Count; i++)
			{
				QuickSlotElement quickSlotElement = _slots[i];
				if (!(quickSlotElement == null) && !quickSlotElement.IsMaxStack && quickSlotElement.StackId == stackId)
				{
					return true;
				}
			}
			if (HeldSlotAvailable)
			{
				return true;
			}
			return false;
		}

		public bool CanHoldSlot(BaseHoldable holdable)
		{
			if (holdable == null || holdable.ItemSO == null)
			{
				return false;
			}
			if (holdable.RequiresHeldSlot)
			{
				return false;
			}
			return CanHoldSlot(holdable.ItemSO, holdable.QuickSlotStackId);
		}

		public bool CanHoldSlot(HoldableSO holdableSO)
		{
			return CanHoldSlot(holdableSO, (holdableSO != null) ? holdableSO.Id : string.Empty);
		}

		private bool CanHoldSlot(HoldableSO holdableSO, string stackId)
		{
			if (holdableSO == null)
			{
				return false;
			}
			if (AnySlotAvailable)
			{
				return true;
			}
			for (int i = 0; i < _slots.Count; i++)
			{
				QuickSlotElement quickSlotElement = _slots[i];
				if (!(quickSlotElement == null) && !quickSlotElement.IsMaxStack && quickSlotElement.StackId == stackId)
				{
					return true;
				}
			}
			return false;
		}

		public void ClearSlot()
		{
			if (!_heldSlotElement.IsEmpty)
			{
				_heldSlotElement.Clear();
				_player.Interactor.DeselectHeldItem(setVisibility: false);
				AutoSelectFilledSlot();
				RefreshHeldHints();
				return;
			}
			if (_selectedSlotIndex < 0)
			{
				RefreshHeldHints();
				return;
			}
			if (!IsValidSlotIndex(_selectedSlotIndex))
			{
				_selectedSlotIndex = -1;
				RefreshHeldHints();
				return;
			}
			QuickSlotElement quickSlotElement = _slots[_selectedSlotIndex];
			quickSlotElement.RemoveItem();
			if (quickSlotElement.IsEmpty)
			{
				if (_player != null && _player.Interactor != null)
				{
					_player.Interactor.DeselectHeldItem(setVisibility: false);
				}
				quickSlotElement.SetStatus(isSelected: false);
				_selectedSlotIndex = -1;
				_availableSlot = Mathf.Min(_slotSize, _availableSlot + 1);
				AutoSelectFilledSlot(quickSlotElement.Index + 1);
			}
			else
			{
				_player.Interactor.SelectItem(quickSlotElement.PeekItem(), startHold: false);
				RefreshHeldHints();
			}
		}

		public void UpdateInfoText(string text)
		{
			_infoText.text = text;
		}

		public void RefreshDisplayForItem(BaseHoldable item)
		{
			if (item == null)
			{
				return;
			}
			if (_heldSlotElement != null && !_heldSlotElement.IsEmpty && (object)_heldSlotElement.HeldItem == item)
			{
				_heldSlotElement.RefreshDisplay();
			}
			else
			{
				if (_slots == null)
				{
					return;
				}
				for (int i = 0; i < _slots.Count; i++)
				{
					QuickSlotElement quickSlotElement = _slots[i];
					if (!(quickSlotElement == null) && quickSlotElement.ContainsItem(item))
					{
						quickSlotElement.RefreshDisplayFromTopItem();
						if (i == _selectedSlotIndex && (_heldSlotElement == null || _heldSlotElement.IsEmpty))
						{
							UpdateInfoText(quickSlotElement.DisplayName);
						}
						break;
					}
				}
			}
		}

		public void SelectSlot(int index, bool startHold = false)
		{
			if (!IsValidSlotIndex(index))
			{
				UtilityMethods.Log($"[QuickSlotHandler] SelectSlot invalid index: {index}, Slots Count: {_slots?.Count ?? 0}", LogType.Warning);
			}
			else if (!PreserveHeldSlotSelection())
			{
				DeselectSlot();
				_selectedSlotIndex = index;
				QuickSlotElement quickSlotElement = _slots[_selectedSlotIndex];
				quickSlotElement.SetStatus(isSelected: true);
				if (!quickSlotElement.IsEmpty)
				{
					UtilityMethods.Log("[QuickSlotHandler] SelectSlot");
					_player.Interactor.SelectItem(quickSlotElement.PeekItem(), startHold);
				}
				RefreshHeldHints();
			}
		}

		private bool PreserveHeldSlotSelection()
		{
			if (_heldSlotElement == null || _heldSlotElement.IsEmpty || _heldSlotElement.HeldItem == null)
			{
				return false;
			}
			if (IsValidSlotIndex(_selectedSlotIndex))
			{
				_slots[_selectedSlotIndex].SetStatus(isSelected: false);
			}
			_selectedSlotIndex = -1;
			if (_player != null && _player.Interactor != null)
			{
				if (_heldSlotSuspensionReasons == HeldSlotSuspensionReason.None)
				{
					if ((object)_player.Interactor.CurrentHeld != _heldSlotElement.HeldItem)
					{
						_player.Interactor.SelectItem(_heldSlotElement.HeldItem, startHold: false);
					}
				}
				else if (_player.Interactor.CurrentHeld != null)
				{
					_player.Interactor.DeselectHeldItem(setVisibility: false);
				}
			}
			RefreshHeldHints();
			return true;
		}

		public void AddItem(BaseHoldable item, bool select = true)
		{
			UtilityMethods.Log("[QuickSlotHandler] AddItem:" + select);
			AddItemDefault(item, select);
		}

		private void AddItemDefault(BaseHoldable item, bool select)
		{
			if (item == null || item.ItemSO == null || item.ItemSO.Id == null)
			{
				UtilityMethods.Log("[QuickSlotHandler] Invalid item!", LogType.Warning);
				return;
			}
			if (item.RequiresHeldSlot)
			{
				AddHeldSlotOnlyItem(item);
				return;
			}
			if (_slots == null || _slots.Count == 0)
			{
				UtilityMethods.Log("[QuickSlotHandler] Slots not initialized!", LogType.Warning);
				return;
			}
			if (ContainsSpecificItem(item, out var slotIndex, out var isInHeldSlot))
			{
				UtilityMethods.Log("[QuickSlotHandler] AddItem blocked. Item already exists in quick slot: " + item.name, LogType.Warning);
				if (select && !isInHeldSlot && IsValidSlotIndex(slotIndex))
				{
					SelectSlot(slotIndex, startHold: true);
				}
				return;
			}
			string quickSlotStackId = item.QuickSlotStackId;
			QuickSlotElement quickSlotElement = FindStackableSlot(quickSlotStackId);
			if (quickSlotElement != null)
			{
				quickSlotElement.AddItem(item);
				SelectSlotSafe(quickSlotElement.Index);
				_availableSlot = CountEmptySlotsLocal();
				return;
			}
			QuickSlotElement quickSlotElement2 = FindEmptySlotPreferSelected();
			if (quickSlotElement2 != null)
			{
				quickSlotElement2.AddItem(item);
				SelectSlotSafe(quickSlotElement2.Index);
				_availableSlot = CountEmptySlotsLocal();
			}
			else if (_heldSlotElement != null && _heldSlotElement.IsEmpty)
			{
				DeselectSlot();
				_heldSlotElement.Set(item);
				_player.Interactor.SelectItem(item, startHold: true);
				RefreshHeldHints();
			}
			else
			{
				ShowSlotsFullToast();
				UtilityMethods.Log("[QuickSlotHandler] No available slot for this item!", LogType.Warning);
			}
			int CountEmptySlotsLocal()
			{
				int num = 0;
				for (int i = 0; i < _slots.Count; i++)
				{
					QuickSlotElement quickSlotElement3 = _slots[i];
					if (quickSlotElement3 != null && quickSlotElement3.IsEmpty)
					{
						num++;
					}
				}
				return num;
			}
			QuickSlotElement FindEmptySlotPreferSelected()
			{
				if (IsValidIndex(_selectedSlotIndex) && _slots[_selectedSlotIndex].IsEmpty)
				{
					return _slots[_selectedSlotIndex];
				}
				for (int i = 0; i < _slots.Count; i++)
				{
					QuickSlotElement quickSlotElement3 = _slots[i];
					if (quickSlotElement3 != null && quickSlotElement3.IsEmpty)
					{
						return quickSlotElement3;
					}
				}
				return null;
			}
			QuickSlotElement FindStackableSlot(string id)
			{
				if (IsValidIndex(_selectedSlotIndex))
				{
					QuickSlotElement quickSlotElement3 = _slots[_selectedSlotIndex];
					if (!quickSlotElement3.IsEmpty && quickSlotElement3.StackId == id && !quickSlotElement3.IsMaxStack)
					{
						return quickSlotElement3;
					}
				}
				for (int i = 0; i < _slots.Count; i++)
				{
					QuickSlotElement quickSlotElement4 = _slots[i];
					if (quickSlotElement4 != null && !quickSlotElement4.IsEmpty && quickSlotElement4.StackId == id && !quickSlotElement4.IsMaxStack)
					{
						return quickSlotElement4;
					}
				}
				return null;
			}
			bool IsValidIndex(int i)
			{
				if (i >= 0 && i < _slots.Count)
				{
					return _slots[i] != null;
				}
				return false;
			}
			void SelectSlotSafe(int index)
			{
				if ((select || (_selectedSlotIndex >= 0 && _selectedSlotIndex == index && _slots[_selectedSlotIndex].StackId == item.QuickSlotStackId)) && IsValidIndex(index))
				{
					SelectSlot(index, startHold: true);
				}
				else
				{
					item.SetVisibility(visible: false);
				}
			}
		}

		private void AddHeldSlotOnlyItem(BaseHoldable item)
		{
			if (_heldSlotElement == null || !_heldSlotElement.IsEmpty)
			{
				ShowSlotsFullToast();
				UtilityMethods.Log("[QuickSlotHandler] Held-slot-only item rejected because HeldSlot is occupied. Item:" + item.name, LogType.Warning);
				return;
			}
			DeselectSlot();
			_heldSlotElement.Set(item);
			_player.Interactor.SelectItem(item, startHold: true);
			RefreshHeldHints();
		}

		private int CountEmptySlots()
		{
			int num = 0;
			for (int i = 0; i < _slots.Count; i++)
			{
				QuickSlotElement quickSlotElement = _slots[i];
				if (quickSlotElement != null && quickSlotElement.IsEmpty)
				{
					num++;
				}
			}
			return num;
		}

		public void DeselectSlot(bool setVisibility = true)
		{
			if (!IsValidSlotIndex(_selectedSlotIndex))
			{
				_selectedSlotIndex = -1;
				RefreshHeldHints();
				return;
			}
			_slots[_selectedSlotIndex].SetStatus(isSelected: false);
			_selectedSlotIndex = -1;
			if (_player != null && _player.Interactor != null)
			{
				_player.Interactor.DeselectHeldItem(setVisibility);
			}
			RefreshHeldHints();
		}

		public void SuspendCurrentSelectionForMode(HeldSlotSuspensionReason reason)
		{
			if (reason == HeldSlotSuspensionReason.None || (_heldSlotSuspensionReasons & reason) != HeldSlotSuspensionReason.None)
			{
				return;
			}
			bool num = _heldSlotSuspensionReasons != HeldSlotSuspensionReason.None;
			_heldSlotSuspensionReasons |= reason;
			if (num)
			{
				return;
			}
			if (_heldSlotElement == null || _heldSlotElement.IsEmpty || _heldSlotElement.HeldItem == null)
			{
				DeselectSlot();
				return;
			}
			_suspendedHeldSlotItem = _heldSlotElement.HeldItem;
			if (_player != null && _player.Interactor != null)
			{
				_player.Interactor.DeselectHeldItem();
			}
			RefreshHeldHints();
		}

		public void ResumeCurrentSelectionForMode(HeldSlotSuspensionReason reason)
		{
			if (reason == HeldSlotSuspensionReason.None || (_heldSlotSuspensionReasons & reason) == 0)
			{
				return;
			}
			_heldSlotSuspensionReasons &= (HeldSlotSuspensionReason)(byte)(~(int)reason);
			if (_heldSlotSuspensionReasons == HeldSlotSuspensionReason.None)
			{
				BaseHoldable suspendedHeldSlotItem = _suspendedHeldSlotItem;
				_suspendedHeldSlotItem = null;
				if (suspendedHeldSlotItem == null || _heldSlotElement == null || _heldSlotElement.IsEmpty || (object)_heldSlotElement.HeldItem != suspendedHeldSlotItem)
				{
					RefreshHeldHints();
				}
				else if (!(_player == null) && !(_player.Interactor == null))
				{
					_player.Interactor.SelectItem(suspendedHeldSlotItem, startHold: false);
					RefreshHeldHints();
				}
			}
		}

		public void OnScroll(InputActionPhase phase, Vector2 input)
		{
			TryScrollSlotFromInput(phase, input);
		}

		public bool TryScrollSlotFromInput(InputActionPhase phase, Vector2 input)
		{
			if (_player == null || _player.Placer == null)
			{
				return false;
			}
			if (!CanUseQuickSlotsFromInput())
			{
				return false;
			}
			if (_player.Placer.InPlaceMode)
			{
				return false;
			}
			if (!HeldSlotAvailable)
			{
				return false;
			}
			if (phase != InputActionPhase.Performed)
			{
				return false;
			}
			if (Mathf.Abs(input.y) < 0.35f)
			{
				return false;
			}
			if (_slots == null || _slots.Count == 0)
			{
				return false;
			}
			float unscaledTime = Time.unscaledTime;
			if (unscaledTime < _nextScrollTime)
			{
				return false;
			}
			_nextScrollTime = unscaledTime + _scrollRepeatDelay;
			int num = ((_selectedSlotIndex >= 0) ? _selectedSlotIndex : 0) + ((!(input.y > 0f)) ? 1 : (-1));
			if (num < 0)
			{
				num = _slots.Count - 1;
			}
			if (num >= _slots.Count)
			{
				num = 0;
			}
			SelectSlot(num);
			return true;
		}

		public void OnSlotNumber(InputActionPhase phase, int input)
		{
			TrySelectSlotFromInput(phase, input, allowActiveToolFallback: true);
		}

		public bool TrySelectSlotFromInput(InputActionPhase phase, int input, bool allowActiveToolFallback)
		{
			if (phase != InputActionPhase.Started)
			{
				return false;
			}
			if (!CanChangeSlotSelection(allowActiveToolFallback))
			{
				return false;
			}
			if (!HeldSlotAvailable && _heldSlotElement.HeldItem != null && _heldSlotElement.HeldItem.RequiresHeldSlot)
			{
				return false;
			}
			if (!IsValidSlotIndex(input))
			{
				UtilityMethods.Log($"[QuickSlotHandler] Invalid slot input: {input}, Converted Index: {input}, Slots Count: {_slots?.Count ?? 0}", LogType.Warning);
				return false;
			}
			if (!HeldSlotAvailable && !_player.DisableActiveToolIf())
			{
				return false;
			}
			if (input == _selectedSlotIndex)
			{
				DeselectSlot();
				return true;
			}
			SelectSlot(input);
			return true;
		}

		public bool CanChangeSlotSelection(bool allowActiveToolFallback)
		{
			if (_player == null || _player.Placer == null)
			{
				return false;
			}
			if (!CanUseQuickSlotsFromInput())
			{
				return false;
			}
			if (_player.Placer.InPlaceMode)
			{
				return false;
			}
			if (HeldSlotAvailable)
			{
				return true;
			}
			if (_heldSlotElement.HeldItem != null && _heldSlotElement.HeldItem.RequiresHeldSlot)
			{
				return false;
			}
			if (allowActiveToolFallback)
			{
				return _player.ActiveTool;
			}
			return false;
		}

		private bool CanUseQuickSlotsFromInput()
		{
			if (!(_player == null))
			{
				return !_player.IsUsingEmoteArea;
			}
			return true;
		}

		private void RefreshAvailableSlots()
		{
			_availableSlot = CountEmptySlots();
		}

		public bool DropAllItems(Vector3 pos)
		{
			bool result = (_heldSlotElement != null && !_heldSlotElement.IsEmpty && _heldSlotElement.HeldItem != null) || _slots.Any((QuickSlotElement s) => !s.IsEmpty);
			DeselectSlot();
			_suspendedHeldSlotItem = null;
			HeldSlotElement.DropItem(pos);
			foreach (QuickSlotElement slot in _slots)
			{
				slot.DropSlot(pos);
			}
			_availableSlot = _slotSize;
			return result;
		}

		public bool SpillAllItemsForFishSlip(Vector3 playerPosition, Vector3 playerForward)
		{
			List<BaseHoldable> list = ExtractAllItemsForSpill();
			if (list.Count == 0)
			{
				return false;
			}
			StartCoroutine(PerformFishSlipSpillAfterDelay(list, playerPosition, playerForward));
			return true;
		}

		private IEnumerator PerformFishSlipSpillAfterDelay(List<BaseHoldable> items, Vector3 playerPosition, Vector3 playerForward)
		{
			float num = Mathf.Max(0f, _fishSlipSpillDelay);
			if (num > 0f)
			{
				yield return new WaitForSeconds(num);
			}
			Vector3 vector = Vector3.ProjectOnPlane(playerForward, Vector3.up);
			if (vector.sqrMagnitude < 0.0001f)
			{
				vector = ((_player != null) ? Vector3.ProjectOnPlane(_player.transform.forward, Vector3.up) : Vector3.forward);
			}
			vector = ((vector.sqrMagnitude > 0.0001f) ? vector.normalized : Vector3.forward);
			Vector3 normalized = Vector3.Cross(Vector3.up, vector).normalized;
			Vector3 basePosition = playerPosition + vector * Mathf.Max(0f, _fishSlipDropForwardOffset) + Vector3.up * Mathf.Max(0f, _fishSlipDropHeight);
			for (int i = 0; i < items.Count; i++)
			{
				BaseHoldable baseHoldable = items[i];
				if (!(baseHoldable == null))
				{
					Vector3 spillItemPosition = GetSpillItemPosition(basePosition, normalized, i, items.Count);
					if (baseHoldable.IsThrowable)
					{
						ThrowFishSlipItem(baseHoldable, spillItemPosition);
					}
					else
					{
						baseHoldable.Drop(spillItemPosition, Quaternion.identity);
					}
				}
			}
			UtilityMethods.Log($"[QuickSlotHandler] Fish slip spilled inventory items. Count:{items.Count}");
		}

		private List<BaseHoldable> ExtractAllItemsForSpill()
		{
			List<BaseHoldable> list = new List<BaseHoldable>();
			if (_heldSlotElement != null && !_heldSlotElement.IsEmpty)
			{
				BaseHoldable baseHoldable = _heldSlotElement.ExtractItem();
				if ((object)_suspendedHeldSlotItem == baseHoldable)
				{
					_suspendedHeldSlotItem = null;
				}
				if (baseHoldable != null)
				{
					list.Add(baseHoldable);
				}
			}
			if (_slots != null)
			{
				for (int i = 0; i < _slots.Count; i++)
				{
					QuickSlotElement quickSlotElement = _slots[i];
					if (!(quickSlotElement == null))
					{
						list.AddRange(quickSlotElement.ExtractAllItems());
					}
				}
			}
			_selectedSlotIndex = -1;
			RefreshAvailableSlots();
			if (_player != null && _player.Interactor != null)
			{
				_player.Interactor.DeselectHeldItem(setVisibility: false);
			}
			RefreshHeldHints();
			return list;
		}

		private Vector3 GetSpillItemPosition(Vector3 basePosition, Vector3 right, int index, int count)
		{
			float num = (float)index - (float)(count - 1) * 0.5f;
			return basePosition + right * (num * Mathf.Max(0f, _fishSlipItemSpacing));
		}

		private void ThrowFishSlipItem(BaseHoldable item, Vector3 startPosition)
		{
			if (_player != null && _player.Thrower != null && _player.Thrower.TryBuildDirectedThrowRequestPayload(item, startPosition, Vector3.up, out var data))
			{
				item.RequestThrow(data);
				return;
			}
			UtilityMethods.Log("[QuickSlotHandler] Fish slip throw payload failed. Dropping item instead. Item:" + item.name, LogType.Warning);
			item.Drop(startPosition, Quaternion.identity);
		}

		public void DisableCurrentItem()
		{
			if (!HeldSlotAvailable)
			{
				if (_player != null && _player.Interactor != null)
				{
					_player.Interactor.DeselectHeldItem(setVisibility: false);
				}
				HeldSlotElement.DisableItem();
				AutoSelectFilledSlot();
				RefreshHeldHints();
				return;
			}
			if (!IsValidSlotIndex(_selectedSlotIndex) || _slots[_selectedSlotIndex].IsEmpty)
			{
				AutoSelectFilledSlot();
				return;
			}
			int selectedSlotIndex = _selectedSlotIndex;
			_slots[_selectedSlotIndex].DisableItem();
			RefreshAvailableSlots();
			if (_slots[selectedSlotIndex].IsEmpty)
			{
				if (_player != null && _player.Interactor != null)
				{
					_player.Interactor.DeselectHeldItem(setVisibility: false);
				}
				_slots[selectedSlotIndex].SetStatus(isSelected: false);
				_selectedSlotIndex = -1;
				AutoSelectFilledSlot(selectedSlotIndex + 1);
			}
			else
			{
				SelectSlot(selectedSlotIndex);
			}
		}

		public void AutoSelectSlot()
		{
			AutoSelectFilledSlot();
		}

		private bool AutoSelectFilledSlot(int preferredStartIndex = 0)
		{
			if (_slots == null || _slots.Count == 0)
			{
				return false;
			}
			int num = Mathf.Clamp(preferredStartIndex, 0, _slots.Count - 1);
			for (int i = 0; i < _slots.Count; i++)
			{
				int index = (num + i) % _slots.Count;
				QuickSlotElement quickSlotElement = _slots[index];
				if (!(quickSlotElement == null) && !quickSlotElement.IsEmpty)
				{
					SelectSlot(index);
					return true;
				}
			}
			RefreshHeldHints();
			return false;
		}

		public void DestroyCurrentItem(bool allStack = false)
		{
			if (!HeldSlotAvailable)
			{
				_player.Interactor.DeselectHeldItem(setVisibility: false);
				HeldSlotElement.DestroyItem();
				AutoSelectFilledSlot();
			}
			else if (_selectedSlotIndex >= 0 && !_slots[_selectedSlotIndex].IsEmpty)
			{
				_player.Interactor.DeselectHeldItem(setVisibility: false);
				_slots[_selectedSlotIndex].DestroyItem(allStack);
				RefreshAvailableSlots();
				if (!_slots[_selectedSlotIndex].IsEmpty)
				{
					SelectSlot(_selectedSlotIndex);
					return;
				}
				int selectedSlotIndex = _selectedSlotIndex;
				_slots[selectedSlotIndex].SetStatus(isSelected: false);
				_selectedSlotIndex = -1;
				AutoSelectFilledSlot(selectedSlotIndex + 1);
			}
		}

		public void DestroySlotItem(int index, bool allStack = false)
		{
			if (index < 0 || index >= _slots.Count || _slots[index].IsEmpty)
			{
				return;
			}
			_slots[index].DestroyItem(allStack);
			RefreshAvailableSlots();
			if (_selectedSlotIndex != index)
			{
				return;
			}
			if (_slots[index].IsEmpty)
			{
				if (_player != null && _player.Interactor != null)
				{
					_player.Interactor.DeselectHeldItem(setVisibility: false);
				}
				_slots[index].SetStatus(isSelected: false);
				_selectedSlotIndex = -1;
				AutoSelectFilledSlot(index + 1);
			}
			else
			{
				SelectSlot(index);
			}
		}

		public bool SpawnItem(HoldableSO holdableSO, bool select)
		{
			if (!CanHold(holdableSO))
			{
				ShowSlotsFullToast();
				UtilityMethods.Log("Can't hold " + holdableSO.Name + " right now!", LogType.Warning);
				return false;
			}
			if (!ServiceLocator.ForSceneOf(this).TryGet<RpcRelay>(out var service))
			{
				return false;
			}
			service.ServerSpawnRpc(new FixedString64Bytes(holdableSO.Id), _player.OwnerClientId, _player.Interactor.NetworkObject.ToReference(), select);
			return true;
		}

		public bool RequestShopInventoryDelivery(HoldableSO holdableSO, Vector3 fallbackDropPosition)
		{
			if (holdableSO == null || _player == null || _player.Interactor == null)
			{
				return false;
			}
			if (!ServiceLocator.ForSceneOf(this).TryGet<RpcRelay>(out var service))
			{
				UtilityMethods.Log("[QuickSlotHandler][ShopDelivery] RpcRelay not found.", LogType.Warning);
				return false;
			}
			service.ServerSpawnShopInventoryItemRpc(new FixedString64Bytes(holdableSO.Id), _player.Interactor.NetworkObject.ToReference(), fallbackDropPosition);
			return true;
		}

		public void RefreshHeldHints()
		{
			HideInactiveInventoryItemVisuals(SelectedItem);
			NotifySelectedItemChanged();
			if (!(_player == null) && !(_player.Placer == null) && !(_player.Interactor == null) && !_player.Placer.InPlaceMode)
			{
				_player.Interactor.RefreshCurrentHints();
			}
		}

		private void HideInactiveInventoryItemVisuals(BaseHoldable selectedItem)
		{
			if (_heldSlotElement != null && !_heldSlotElement.IsEmpty && _heldSlotElement.HeldItem != null && (object)_heldSlotElement.HeldItem != selectedItem)
			{
				_heldSlotElement.HeldItem.ClientSetVisibility(visible: false);
			}
			if (_slots == null)
			{
				return;
			}
			for (int i = 0; i < _slots.Count; i++)
			{
				QuickSlotElement quickSlotElement = _slots[i];
				if (quickSlotElement == null || quickSlotElement.IsEmpty || quickSlotElement.Items == null)
				{
					continue;
				}
				foreach (BaseHoldable item in quickSlotElement.Items)
				{
					if (!(item == null) && (object)item != selectedItem)
					{
						item.ClientSetVisibility(visible: false);
					}
				}
			}
		}

		private void ShowSlotsFullToast()
		{
			if (!(Singleton<NotifyManager>.Instance == null))
			{
				Singleton<NotifyManager>.Instance.ShowToast("Slots are full!", NotificationState.Error);
			}
		}

		public void ForceHideAllInventoryItemVisualsOnly()
		{
			if (_heldSlotElement != null && !_heldSlotElement.IsEmpty && _heldSlotElement.HeldItem != null)
			{
				_heldSlotElement.HeldItem.SetVisibility(visible: false);
			}
			if (_slots != null)
			{
				for (int i = 0; i < _slots.Count; i++)
				{
					QuickSlotElement quickSlotElement = _slots[i];
					if (quickSlotElement == null || quickSlotElement.IsEmpty || quickSlotElement.Items == null)
					{
						continue;
					}
					foreach (BaseHoldable item in quickSlotElement.Items)
					{
						if (!(item == null))
						{
							item.SetVisibility(visible: false);
						}
					}
				}
			}
			BaseHoldable selectedItem = SelectedItem;
			if (selectedItem != null)
			{
				selectedItem.SetVisibility(visible: false);
			}
		}

		private void NotifySelectedItemChanged()
		{
			BaseHoldable selectedItem = SelectedItem;
			if ((object)_lastNotifiedSelectedItem != selectedItem)
			{
				_lastNotifiedSelectedItem = selectedItem;
				SelectedItemChanged?.Invoke(selectedItem);
			}
		}

		public QuickSlotSaveData Save()
		{
			QuickSlotSaveData quickSlotSaveData = QuickSlotSaveData.CreateDefault(_slotSize);
			quickSlotSaveData.Slots.Clear();
			if (_heldSlotElement != null && !_heldSlotElement.IsEmpty && _heldSlotElement.HeldItem != null && _heldSlotElement.HeldItem.ItemSO != null && !ShouldSkipQuickSlotSaveItem(_heldSlotElement.HeldItem))
			{
				quickSlotSaveData.Held = new HeldSlotSaveData
				{
					HasItem = true,
					Item = CreateSlotItemSaveData(_heldSlotElement.HeldItem)
				};
			}
			else
			{
				quickSlotSaveData.Held = HeldSlotSaveData.Empty;
			}
			for (int i = 0; i < _slotSize; i++)
			{
				QuickSlotElement quickSlotElement = ((i >= 0 && i < _slots.Count) ? _slots[i] : null);
				if (quickSlotElement == null || quickSlotElement.IsEmpty || SlotContainsUnsavableQuickSlotItem(quickSlotElement))
				{
					quickSlotSaveData.Slots.Add(new QuickSlotStackSaveData
					{
						Index = i,
						Items = new List<SlotItemData>()
					});
					continue;
				}
				List<SlotItemData> list = new List<SlotItemData>();
				foreach (BaseHoldable item in _slots[i].Items)
				{
					if (!(item == null) && !ShouldSkipQuickSlotSaveItem(item))
					{
						list.Add(CreateSlotItemSaveData(item));
					}
				}
				quickSlotSaveData.Slots.Add(new QuickSlotStackSaveData
				{
					Index = i,
					Items = list
				});
			}
			return quickSlotSaveData;
		}

		private static bool ShouldSkipQuickSlotSaveItem(BaseHoldable item)
		{
			if (!(item == null) && item.CanPersistInQuickSlotSave)
			{
				return item is Tray;
			}
			return true;
		}

		private static bool SlotContainsUnsavableQuickSlotItem(QuickSlotElement slot)
		{
			if (slot == null || slot.Items == null)
			{
				return false;
			}
			return slot.Items.Any(ShouldSkipQuickSlotSaveItem);
		}

		private static SlotItemData CreateSlotItemSaveData(BaseHoldable item)
		{
			return new SlotItemData
			{
				ItemId = item.ItemSO.Id,
				CustomData = item.SaveCustomData(),
				CustomSushi = ((item is ServedDishSushi servedDishSushi) ? servedDishSushi.CreateCustomSushiSaveData() : null)
			};
		}

		public void Load(QuickSlotSaveData data)
		{
			QuickSlotLoadPayload payload = BuildPayloadFromSave(data);
			if (!ServiceLocator.ForSceneOf(this).TryGet<RpcRelay>(out var service))
			{
				UtilityMethods.Log("[QuickSlotHandler] Load: RpcRelay not found!", LogType.Warning);
			}
			else
			{
				service.ServerLoadQuickSlotRpc(payload, _player.Interactor.NetworkObject.ToReference());
			}
		}

		private QuickSlotLoadPayload BuildPayloadFromSave(QuickSlotSaveData data)
		{
			List<QuickSlotItemNet> list = new List<QuickSlotItemNet>(6);
			if (data.Slots != null)
			{
				foreach (QuickSlotStackSaveData item in data.Slots.OrderBy((QuickSlotStackSaveData x) => x.Index))
				{
					if (item?.Items == null || item.Items.Count == 0)
					{
						continue;
					}
					for (int num = 0; num < item.Items.Count; num++)
					{
						SlotItemData slotItemData = item.Items[num];
						if (slotItemData != null && !string.IsNullOrEmpty(slotItemData.ItemId))
						{
							list.Add(BuildQuickSlotItemNet(slotItemData));
						}
					}
				}
			}
			if (data.Held != null && data.Held.HasItem && data.Held.Item != null && !string.IsNullOrEmpty(data.Held.Item.ItemId))
			{
				list.Add(BuildQuickSlotItemNet(data.Held.Item));
			}
			return new QuickSlotLoadPayload
			{
				Count = list.Count,
				Items = list.ToArray()
			};
		}

		private static QuickSlotItemNet BuildQuickSlotItemNet(SlotItemData item)
		{
			QuickSlotItemNet result = new QuickSlotItemNet
			{
				ItemId = new FixedString64Bytes(item.ItemId),
				CustomData = new FixedString128Bytes(item.CustomData ?? string.Empty)
			};
			if (CustomSushiPhysicalNetworkData.TryCreate(item.CustomSushi, out var result2))
			{
				result.HasCustomSushi = true;
				result.CustomSushi = result2;
			}
			return result;
		}

		private bool IsValidSlotIndex(int index)
		{
			if (_slots != null && index >= 0 && index < _slots.Count)
			{
				return _slots[index] != null;
			}
			return false;
		}

		public bool RemoveSpecificItem(BaseHoldable item, bool selectNextIfPossible = true)
		{
			if (item == null)
			{
				return false;
			}
			bool flag = false;
			bool flag2 = false;
			int preferredStartIndex = 0;
			if (_heldSlotElement != null && !_heldSlotElement.IsEmpty && (object)_heldSlotElement.HeldItem == item)
			{
				_heldSlotElement.Clear();
				if ((object)_suspendedHeldSlotItem == item)
				{
					_suspendedHeldSlotItem = null;
				}
				if (_player != null && _player.Interactor != null)
				{
					_player.Interactor.DeselectHeldItem(setVisibility: false);
				}
				flag = true;
				flag2 = selectNextIfPossible && _selectedSlotIndex < 0;
			}
			for (int i = 0; i < _slots.Count; i++)
			{
				QuickSlotElement quickSlotElement = _slots[i];
				if (quickSlotElement == null || quickSlotElement.IsEmpty || !quickSlotElement.ContainsItem(item))
				{
					continue;
				}
				bool num = i == _selectedSlotIndex;
				quickSlotElement.RemoveSpecificItem(item);
				flag = true;
				if (!num)
				{
					continue;
				}
				if (_player != null && _player.Interactor != null)
				{
					_player.Interactor.DeselectHeldItem(setVisibility: false);
				}
				if (quickSlotElement.IsEmpty)
				{
					quickSlotElement.SetStatus(isSelected: false);
					_selectedSlotIndex = -1;
					flag2 = selectNextIfPossible;
					preferredStartIndex = i + 1;
				}
				else if (selectNextIfPossible)
				{
					_selectedSlotIndex = i;
					quickSlotElement.SetStatus(isSelected: true);
					if (_player != null && _player.Interactor != null)
					{
						_player.Interactor.SelectItem(quickSlotElement.PeekItem(), startHold: false);
					}
				}
				else
				{
					_selectedSlotIndex = -1;
					quickSlotElement.SetStatus(isSelected: false);
				}
			}
			if (!flag)
			{
				return false;
			}
			RefreshAvailableSlots();
			if (flag2)
			{
				AutoSelectFilledSlot(preferredStartIndex);
			}
			RefreshHeldHints();
			UtilityMethods.Log("[QuickSlotHandler] Removed stale item reference: " + item.name);
			return true;
		}

		public bool TryGetItemLocation(BaseHoldable item, out int slotIndex, out bool isInHeldSlot)
		{
			return ContainsSpecificItem(item, out slotIndex, out isInHeldSlot);
		}

		private bool ContainsSpecificItem(BaseHoldable item, out int slotIndex, out bool isInHeldSlot)
		{
			slotIndex = -1;
			isInHeldSlot = false;
			if (item == null)
			{
				return false;
			}
			if (_heldSlotElement != null && !_heldSlotElement.IsEmpty && (object)_heldSlotElement.HeldItem == item)
			{
				isInHeldSlot = true;
				return true;
			}
			for (int i = 0; i < _slots.Count; i++)
			{
				QuickSlotElement quickSlotElement = _slots[i];
				if (!(quickSlotElement == null) && !quickSlotElement.IsEmpty && quickSlotElement.ContainsItem(item))
				{
					slotIndex = i;
					return true;
				}
			}
			return false;
		}
	}
}
