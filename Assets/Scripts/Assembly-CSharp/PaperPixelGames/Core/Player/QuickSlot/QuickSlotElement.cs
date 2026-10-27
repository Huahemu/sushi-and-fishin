using System.Collections.Generic;
using DG.Tweening;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Player.QuickSlot
{
	public class QuickSlotElement : MonoBehaviour
	{
		[SerializeField]
		private Image _iconImage;

		[SerializeField]
		private GameObject _selectedObject;

		[SerializeField]
		private TextMeshProUGUI _slotNumberObject;

		[SerializeField]
		private TextMeshProUGUI _stackCountText;

		private int _maxStackSize = 1;

		private string _itemName;

		private Stack<BaseHoldable> _items;

		public Stack<BaseHoldable> Items => _items;

		public int Index { get; private set; }

		public string ItemId { get; private set; }

		public string StackId { get; private set; }

		private int _stackCount => _items.Count;

		public bool IsEmpty => _stackCount == 0;

		public bool IsMaxStack => _stackCount >= _maxStackSize;

		public int StackCount
		{
			get
			{
				if (_items != null)
				{
					return _items.Count;
				}
				return 0;
			}
		}

		public int AvailableStackCapacity => Mathf.Max(0, _maxStackSize - _stackCount);

		public string DisplayName => _itemName;

		private QuickSlotHandler _quickSlotHandler => Singleton<QuickSlotHandler>.Instance;

		public void Init(int index)
		{
			Index = index;
			_items = new Stack<BaseHoldable>();
			ItemId = "";
			StackId = "";
			_itemName = "";
			_selectedObject.SetActive(value: false);
			_slotNumberObject.text = $"{Index + 1}";
			_slotNumberObject.gameObject.SetActive(value: true);
			_iconImage.gameObject.SetActive(value: false);
			_maxStackSize = 1;
			UpdateStackUI();
		}

		public void AddItem(BaseHoldable item)
		{
			if (IsEmpty)
			{
				_itemName = item.QuickSlotDisplayName;
				RefreshIcon(item);
				_iconImage.gameObject.SetActive(value: true);
				_slotNumberObject.gameObject.SetActive(value: false);
				_maxStackSize = item.ItemSO.MaxStack;
				ItemId = item.ItemSO.Id;
				StackId = item.QuickSlotStackId;
			}
			else
			{
				_items.Peek().SetVisibility(visible: false);
			}
			_items.Push(item);
			UpdateStackUI();
		}

		public BaseHoldable PeekItem()
		{
			if (IsEmpty)
			{
				return null;
			}
			return _items.Peek();
		}

		public void RefreshDisplayFromTopItem()
		{
			BaseHoldable baseHoldable = PeekItem();
			if (!(baseHoldable == null))
			{
				_itemName = baseHoldable.QuickSlotDisplayName;
				StackId = baseHoldable.QuickSlotStackId;
				RefreshIcon(baseHoldable);
				_iconImage.gameObject.SetActive(value: true);
			}
		}

		public void RemoveItem()
		{
			if (!IsEmpty)
			{
				_items.Pop();
				UpdateStackUI();
				if (_stackCount <= 0)
				{
					ResetSlotVisualState();
				}
			}
		}

		public void SetStatus(bool isSelected)
		{
			base.transform.DOKill();
			if (isSelected)
			{
				_selectedObject.SetActive(!IsEmpty);
				base.transform.DOScale(1.2f, 0.35f).SetEase(Ease.OutElastic);
				_quickSlotHandler.UpdateInfoText(_itemName);
			}
			else
			{
				_quickSlotHandler.UpdateInfoText("");
				_selectedObject.SetActive(value: false);
				base.transform.DOScale(1f, 0.05f).SetEase(Ease.OutCirc);
			}
		}

		private void UpdateStackUI()
		{
			_stackCountText.text = ((_stackCount > 1) ? $"{_stackCount}" : "");
		}

		private void RefreshIcon(BaseHoldable item)
		{
			_iconImage.sprite = ((item != null) ? item.QuickSlotDisplaySprite : null);
            SushiAndFish.FishingIntegration.FishingTablet.PaintHeld(item,_iconImage);
			TrayQuickSlotIconLayers trayQuickSlotIconLayers = _iconImage.GetComponent<TrayQuickSlotIconLayers>();
			if (item is Tray tray)
			{
				if ((object)trayQuickSlotIconLayers == null)
				{
					trayQuickSlotIconLayers = _iconImage.gameObject.AddComponent<TrayQuickSlotIconLayers>();
				}
				trayQuickSlotIconLayers.Set(tray);
			}
			else if (trayQuickSlotIconLayers != null)
			{
				trayQuickSlotIconLayers.Clear();
			}
		}

		public void DropSlot(Vector3 pos, bool all = true)
		{
			if (IsEmpty)
			{
				ResetSlotVisualState();
				return;
			}
			if (all)
			{
				while (_items.Count > 0)
				{
					BaseHoldable baseHoldable = _items.Pop();
					if (baseHoldable != null)
					{
						baseHoldable.Drop(pos, Quaternion.identity);
					}
				}
			}
			else
			{
				BaseHoldable baseHoldable2 = _items.Pop();
				if (baseHoldable2 != null)
				{
					baseHoldable2.Drop(pos, Quaternion.identity);
				}
			}
			UpdateStackUI();
			if (_stackCount <= 0)
			{
				ResetSlotVisualState();
			}
		}

		public List<BaseHoldable> ExtractAllItems()
		{
			List<BaseHoldable> list = new List<BaseHoldable>();
			if (IsEmpty)
			{
				ResetSlotVisualState();
				return list;
			}
			while (_items.Count > 0)
			{
				BaseHoldable baseHoldable = _items.Pop();
				if (baseHoldable != null)
				{
					list.Add(baseHoldable);
				}
			}
			ResetSlotVisualState();
			return list;
		}

		private void ResetSlotVisualState()
		{
			_items.Clear();
			_itemName = "";
			ItemId = "";
			StackId = "";
			_maxStackSize = 1;
			_quickSlotHandler.UpdateInfoText("");
			_iconImage.sprite = null;_iconImage.material=null;_iconImage.useSpriteMesh=false;
			TrayQuickSlotIconLayers component = _iconImage.GetComponent<TrayQuickSlotIconLayers>();
			if (component != null)
			{
				component.Clear();
			}
			_iconImage.gameObject.SetActive(value: false);
			_selectedObject.SetActive(value: false);
			_slotNumberObject.gameObject.SetActive(value: true);
			UpdateStackUI();
		}

		public void DisableItem()
		{
			if (!_items.TryPop(out var result))
			{
				UtilityMethods.Log("[QuickSlotElement] DisableItem trypop failed!");
				return;
			}
			result.Disable(clearQuickSlotOnOwner: false);
			UpdateStackUI();
			if (_stackCount <= 0)
			{
				ResetSlotVisualState();
			}
		}

		public void DestroyItem(bool allStack)
		{
			if (IsEmpty)
			{
				return;
			}
			if (!allStack)
			{
				_items.Pop().Destroy();
			}
			else
			{
				foreach (BaseHoldable item in _items)
				{
					item.Destroy();
				}
				_items.Clear();
			}
			UpdateStackUI();
			if (_stackCount <= 0)
			{
				ResetSlotVisualState();
			}
		}

		public bool ContainsItem(BaseHoldable item)
		{
			if (item != null && _items != null)
			{
				return _items.Contains(item);
			}
			return false;
		}

		public bool RemoveSpecificItem(BaseHoldable item)
		{
			if (item == null || IsEmpty)
			{
				return false;
			}
			bool flag = false;
			Stack<BaseHoldable> stack = new Stack<BaseHoldable>();
			while (_items.Count > 0)
			{
				BaseHoldable baseHoldable = _items.Pop();
				if ((object)baseHoldable == item)
				{
					flag = true;
				}
				else
				{
					stack.Push(baseHoldable);
				}
			}
			while (stack.Count > 0)
			{
				_items.Push(stack.Pop());
			}
			if (!flag)
			{
				return false;
			}
			UpdateStackUI();
			if (_stackCount <= 0)
			{
				ResetSlotVisualState();
			}
			return true;
		}
	}
}
