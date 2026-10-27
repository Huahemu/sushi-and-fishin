using PaperPixelGames.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Tablet
{
	public class InventoryItemUI : MonoBehaviour
	{
		private enum StockState
		{
			Good = 0,
			LowStock = 1,
			OutOfStock = 2
		}

		[Header("Item")]
		[SerializeField]
		private TextMeshProUGUI _itemNameText;

		[SerializeField]
		private TextMeshProUGUI _categoryText;

		[SerializeField]
		private Image _itemIconImage;

		[Header("Amount")]
		[SerializeField]
		private TextMeshProUGUI _amountText;

		[Header("Stock Status")]
		[SerializeField]
		private GameObject _goodStatusRoot;

		[SerializeField]
		private TextMeshProUGUI _goodStatusText;

		[SerializeField]
		private GameObject _warningStatusRoot;

		[SerializeField]
		private TextMeshProUGUI _warningStatusText;

		[Header("Config")]
		[SerializeField]
		private string _unitLabel = "pcs";

		[SerializeField]
		private int _lowStockThreshold = 6;

		[Header("Colors")]
		[SerializeField]
		private Color _normalAmountColor = new Color(0.13f, 0.1f, 0.08f);

		[SerializeField]
		private Color _outOfStockAmountColor = new Color(0.75f, 0.08f, 0.08f);

		[SerializeField]
		private Color _goodColor = new Color(0.25f, 0.45f, 0.18f);

		[SerializeField]
		private Color _warningColor = new Color(0.75f, 0.08f, 0.08f);

		private ItemSO _item;

		private int _amount;

		private string _categoryLabel;

		public ItemSO Item => _item;

		public int Amount => _amount;

		public void Initialize(ItemSO item, int amount, string category)
		{
			_item = item;
			_categoryLabel = category;
			if (_itemIconImage != null)
			{
				_itemIconImage.sprite = ((item != null) ? item.Sprite : null);
				_itemIconImage.enabled = item != null && item.Sprite != null;
			}
			SushiAndFish.FishingIntegration.FishingTablet.PaintInventory(item,_itemIconImage);
            _amount = Mathf.Max(0, amount);
			RefreshLocalizedText();
		}

        public void SetLongAmount(long amount){if(_amountText)_amountText.text=amount.ToString("N0")+" 条";}
        public void PrepareWarehouseRow(){if(_goodStatusRoot)_goodStatusRoot.SetActive(false);if(_warningStatusRoot)_warningStatusRoot.SetActive(false);if(_amountText){_amountText.rectTransform.sizeDelta=new Vector2(95,16);_amountText.rectTransform.anchoredPosition=new Vector2(20,0);_amountText.enableAutoSizing=true;_amountText.fontSizeMin=3.5f;_amountText.fontSizeMax=6;_amountText.textWrappingMode=TextWrappingModes.NoWrap;}}
		public void UpdateAmount(int newAmount)
		{
			_amount = Mathf.Max(0, newAmount);
			RefreshAmountText();
			RefreshStockState();
		}

		public void RefreshLocalizedText()
		{
			if (_itemNameText != null)
			{
				_itemNameText.text = ((_item != null) ? _item.LocalizedName : TabletLocalization.Get("tablet.common.unknown", "Unknown"));
			}
			if (_categoryText != null)
			{
				_categoryText.text = GetCategoryLabel(_categoryLabel);
			}
			RefreshAmountText();
			RefreshStockState();
		}

		private void RefreshAmountText()
		{
			if (_amountText != null)
			{
				_amountText.text = TabletLocalization.Format("tablet.inventory.amount_pieces", "{0}\n {1}", _amount, TabletLocalization.Get("tablet.inventory.unit.pieces", _unitLabel));
			}
		}

		public void SetCategory(string categoryLabel)
		{
			_categoryLabel = categoryLabel;
			if (_categoryText != null)
			{
				_categoryText.text = GetCategoryLabel(categoryLabel);
			}
		}

		private static string GetCategoryLabel(string category)
		{
			if (string.IsNullOrWhiteSpace(category))
			{
				return "-";
			}
			return TabletLocalization.Get("tablet.inventory.category." + category, category);
		}

		private void RefreshStockState()
		{
			StockState stockState = GetStockState(_amount);
			switch (stockState)
			{
			case StockState.Good:
				ApplyGoodState();
				break;
			case StockState.LowStock:
				ApplyWarningState(TabletLocalization.Get("tablet.inventory.low_stock", "LOW STOCK"));
				break;
			case StockState.OutOfStock:
				ApplyWarningState(TabletLocalization.Get("tablet.inventory.out_of_stock", "OUT OF STOCK"));
				break;
			}
			if (_amountText != null)
			{
				_amountText.color = ((stockState == StockState.OutOfStock) ? _outOfStockAmountColor : _normalAmountColor);
			}
		}

		private StockState GetStockState(int amount)
		{
			if (amount <= 0)
			{
				return StockState.OutOfStock;
			}
			if (amount <= _lowStockThreshold)
			{
				return StockState.LowStock;
			}
			return StockState.Good;
		}

		private void ApplyGoodState()
		{
			if (_goodStatusRoot != null)
			{
				_goodStatusRoot.SetActive(value: true);
			}
			if (_goodStatusText != null)
			{
				_goodStatusText.text = TabletLocalization.Get("tablet.inventory.good", "Good");
				_goodStatusText.color = _goodColor;
			}
			if (_warningStatusRoot != null)
			{
				_warningStatusRoot.SetActive(value: false);
			}
		}

		private void ApplyWarningState(string statusText)
		{
			if (_goodStatusRoot != null)
			{
				_goodStatusRoot.SetActive(value: false);
			}
			if (_warningStatusRoot != null)
			{
				_warningStatusRoot.SetActive(value: true);
			}
			if (_warningStatusText != null)
			{
				_warningStatusText.text = statusText;
				_warningStatusText.color = _warningColor;
			}
		}
	}
}
