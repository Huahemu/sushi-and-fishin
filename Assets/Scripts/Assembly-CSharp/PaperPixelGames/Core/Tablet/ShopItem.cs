using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Tablet
{
	public class ShopItem : MonoBehaviour
	{
		[SerializeField]
		private WholesaleSO _itemData;

		[Header("UI")]
		[SerializeField]
		private BaseButton _addToCartButton;

		[SerializeField]
		private TextMeshProUGUI _nameText;

		[SerializeField]
		private TextMeshProUGUI _priceText;

		[SerializeField]
		private Image _iconImage;

		[SerializeField]
		private GameObject _lockedRoot;

		[SerializeField]
		private TextMeshProUGUI _lockedLevelText;

		[SerializeField]
		[Range(0.1f, 1f)]
		private float _lockedAlpha = 0.45f;

		private ShopApp _shopApp;

		public WholesaleSO Data => _itemData;

		public void SetItemData(WholesaleSO itemData)
		{
			_itemData = itemData;
			RefreshVisuals();
		}

		public void Initialize(ShopApp app)
		{
			if (_itemData == null)
			{
				UtilityMethods.Log("ShopItem is missing item data");
				base.gameObject.SetActive(value: false);
				return;
			}
			if (!_itemData.Active)
			{
				UtilityMethods.Log("item data is not active");
				base.gameObject.SetActive(value: false);
				return;
			}
			base.gameObject.SetActive(value: true);
			_shopApp = app;
			_addToCartButton.Clicked -= OnClickAddButton;
			_addToCartButton.Clicked += OnClickAddButton;
			RefreshVisuals();
			RefreshProgressionState();
		}

		private void RefreshVisuals()
		{
			if (!(_itemData == null))
			{
				RefreshLocalizedText();
				if (_priceText != null)
				{
					int num = _itemData.QuantityPrice * _itemData.Quantity;
					_priceText.text = (num * 100).ConvertMoneyText();
				}
				if (_iconImage != null)
				{
					_iconImage.sprite = _itemData.Sprite;
				}
				RefreshProgressionState();
			}
		}

		public void RefreshLocalizedText()
		{
			if (!(_itemData == null))
			{
				if (_nameText != null)
				{
					_nameText.text = $"{_itemData.LocalizedName}(x{_itemData.Quantity})";
				}
				bool unlocked = _shopApp == null || _shopApp.IsShopItemUnlocked(_itemData);
				RefreshLockedLevelText(unlocked);
			}
		}

		public void RefreshProgressionState()
		{
			bool flag = _shopApp == null || _shopApp.IsShopItemUnlocked(_itemData);
			if (_lockedRoot != null)
			{
				_lockedRoot.SetActive(!flag);
			}
			RefreshLockedLevelText(flag);
			if (_addToCartButton != null)
			{
				_addToCartButton.interactable = flag;
			}
			float alpha = (flag ? 1f : _lockedAlpha);
			SetGraphicAlpha(_iconImage, alpha);
			SetGraphicAlpha(_nameText, alpha);
			SetGraphicAlpha(_priceText, alpha);
		}

		private void RefreshLockedLevelText(bool unlocked)
		{
			if (!(_lockedLevelText == null))
			{
				int num = ((!unlocked && Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.GetRequiredWholesaleProgressionLevel(_itemData) : (-1));
				bool flag = !unlocked && num > 0;
				_lockedLevelText.gameObject.SetActive(flag);
				_lockedLevelText.text = (flag ? LocalizationDatabase.GetFormattedString("UiTable", "ui.market.unlocks_at_level", "Unlocks at Level {0}", num) : string.Empty);
			}
		}

		private void OnDestroy()
		{
			if (_addToCartButton != null)
			{
				_addToCartButton.Clicked -= OnClickAddButton;
			}
		}

		private void OnClickAddButton()
		{
			_shopApp.AddToCart(_itemData);
		}

		private static void SetGraphicAlpha(Graphic graphic, float alpha)
		{
			if (!(graphic == null))
			{
				Color color = graphic.color;
				color.a = alpha;
				graphic.color = color;
			}
		}
	}
}
