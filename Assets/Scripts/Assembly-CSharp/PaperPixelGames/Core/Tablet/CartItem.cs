using PaperPixelGames.Core.Data;
using PaperPixelGames.Framework.UI;
using TMPro;
using UnityEngine;

namespace PaperPixelGames.Core.Tablet
{
	public class CartItem : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI _itemAmount;

		[SerializeField]
		private TextMeshProUGUI _itemName;

		[SerializeField]
		private TextMeshProUGUI _itemPrice;

		[SerializeField]
		private BaseButton _removeButton;

		private int _amount;

		private WholesaleSO _data;

		private ShopApp _shopApp;

		public int Amount => _amount;

		public WholesaleSO Data => _data;

		public void Initialize(WholesaleSO data, ShopApp app)
		{
			_removeButton.Clicked += OnClickRemoveButton;
			_data = data;
			_shopApp = app;
			_amount++;
			RefreshUI();
		}

		private void OnClickRemoveButton()
		{
			_shopApp.RemoveFromCart(this);
		}

		public void UpdateAmount(int amount = 1)
		{
			_amount += amount;
			RefreshUI();
		}

		public void RefreshLocalizedText()
		{
			RefreshUI();
		}

		private void RefreshUI()
		{
			_itemName.text = _data.LocalizedName;
			_itemAmount.text = $"x{_amount}";
			decimal num = _data.Quantity * _data.QuantityPrice;
			_itemPrice.text = $"{num * (decimal)_amount}$";
		}
	}
}
