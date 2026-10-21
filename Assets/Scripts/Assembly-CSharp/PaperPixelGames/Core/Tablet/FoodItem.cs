using System;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Framework.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Tablet
{
	public class FoodItem : MonoBehaviour
	{
		[Header("UI")]
		[SerializeField]
		private Image _backgroundImage;

		[FormerlySerializedAs("_itemImage")]
		[SerializeField]
		private Image _iconImage;

		[FormerlySerializedAs("_itemPrice")]
		[SerializeField]
		private TextMeshProUGUI _priceText;

		[SerializeField]
		private Image _priceBackgroundImage;

		[SerializeField]
		private TextMeshProUGUI _nameText;

		[SerializeField]
		private BaseButton _detailsButton;

		[SerializeField]
		private BaseButton _deleteCustomRecipeButton;

		[SerializeField]
		private GameObject _selectedOutline;

		[SerializeField]
		private GameObject _inMenuMarker;

		[SerializeField]
		private GameObject _dailySpecialBadge;

		[SerializeField]
		private TextMeshProUGUI _dailySpecialText;

		[Header("State")]
		[SerializeField]
		[Range(0.1f, 1f)]
		private float _dimmedAlpha = 0.35f;

		[SerializeField]
		private Color _inMenuBackgroundColor = new Color(0.78f, 0.95f, 0.86f, 1f);

		[SerializeField]
		private Color _inMenuPriceBackgroundColor = new Color(0.55f, 0.82f, 0.66f, 1f);

		private ServedDishSO _itemData;

		private SushiRecipeDefinition _recipeData;

		private MenuItemDetailPage _menuItemDetailPage;

		private Action<FoodItem> _onClicked;

		private Action<string> _onDeleteCustomRecipe;

		private Color _defaultBackgroundColor;

		private Color _defaultPriceBackgroundColor;

		private bool _defaultColorsCached;

		private bool _isInMenu;

		public ServedDishSO ItemData => _itemData;

		public SushiRecipeDefinition RecipeData => _recipeData;

		public string RecipeId => _recipeData?.RecipeId ?? _itemData?.Id ?? string.Empty;

		public bool IsAvailable { get; private set; } = true;

		public void Initialize(ServedDishSO data, MenuItemDetailPage menuItemDetailPage, Action<FoodItem> onClicked)
		{
			_itemData = data;
			_recipeData = null;
			_menuItemDetailPage = menuItemDetailPage;
			_onClicked = onClicked;
			_onDeleteCustomRecipe = null;
			RegisterButton();
			RefreshDeleteButton();
			RefreshVisuals();
			SetSelected(selected: false);
			SetInMenu(inMenu: false);
			SetDimmed(dimmed: false);
		}

		public void Initialize(SushiRecipeDefinition recipe, MenuItemDetailPage menuItemDetailPage, Action<FoodItem> onClicked, Action<string> onDeleteCustomRecipe = null)
		{
			_itemData = recipe?.BuiltInSushi;
			_recipeData = recipe;
			_menuItemDetailPage = menuItemDetailPage;
			_onClicked = onClicked;
			_onDeleteCustomRecipe = onDeleteCustomRecipe;
			RegisterButton();
			RefreshDeleteButton();
			RefreshVisuals();
			SetSelected(selected: false);
			SetInMenu(inMenu: false);
			SetDimmed(dimmed: false);
		}

		private void OnDestroy()
		{
			UnregisterButton();
			UnregisterDeleteButton();
		}

		private void RegisterButton()
		{
			if (_detailsButton == null)
			{
				_detailsButton = GetComponent<BaseButton>();
			}
			if (!(_detailsButton == null))
			{
				_detailsButton.Clicked -= OnClickItemButton;
				_detailsButton.Clicked += OnClickItemButton;
			}
		}

		private void UnregisterButton()
		{
			if (!(_detailsButton == null))
			{
				_detailsButton.Clicked -= OnClickItemButton;
			}
		}

		private void RefreshDeleteButton()
		{
			if (!(_deleteCustomRecipeButton == null))
			{
				_deleteCustomRecipeButton.Clicked -= OnClickDeleteCustomRecipe;
				SushiRecipeDefinition recipeData = _recipeData;
				bool flag = recipeData != null && recipeData.IdentityKind == SushiRecipeIdentityKind.SavedCustom && _onDeleteCustomRecipe != null;
				_deleteCustomRecipeButton.gameObject.SetActive(flag);
				if (flag)
				{
					_deleteCustomRecipeButton.Clicked += OnClickDeleteCustomRecipe;
				}
			}
		}

		private void UnregisterDeleteButton()
		{
			if (_deleteCustomRecipeButton != null)
			{
				_deleteCustomRecipeButton.Clicked -= OnClickDeleteCustomRecipe;
			}
		}

		private void OnClickDeleteCustomRecipe()
		{
			SushiRecipeDefinition recipeData = _recipeData;
			if (recipeData != null && recipeData.IdentityKind == SushiRecipeIdentityKind.SavedCustom)
			{
				_onDeleteCustomRecipe?.Invoke(RecipeId);
			}
		}

		private void RefreshVisuals()
		{
			if (_itemData == null && _recipeData == null)
			{
				base.gameObject.SetActive(value: false);
				return;
			}
			base.gameObject.SetActive(value: true);
			if (_iconImage != null)
			{
				_iconImage.sprite = _recipeData?.Icon ?? _itemData?.Sprite;
				_iconImage.preserveAspect = true;
				_iconImage.enabled = _iconImage.sprite != null;
			}
			if (_nameText != null)
			{
				_nameText.text = GetItemName();
			}
			if (_priceText != null)
			{
				int num = ((TabletNetworkHandler.Instance != null) ? TabletNetworkHandler.Instance.GetEffectiveRecipePrice(RecipeId, GetBasePrice()) : GetBasePrice());
				_priceText.text = "$" + num;
			}
			byte bonusAmount = 0;
			bool flag = TabletNetworkHandler.Instance != null && TabletNetworkHandler.Instance.TryGetDailyDishBonus(RecipeId, out bonusAmount);
			if (_dailySpecialBadge != null)
			{
				_dailySpecialBadge.SetActive(flag);
			}
			if (flag && _priceText != null)
			{
				int basePrice = GetBasePrice();
				int effectiveRecipePrice = TabletNetworkHandler.Instance.GetEffectiveRecipePrice(RecipeId, basePrice);
				_priceText.text = $"<s>${basePrice}</s> <color=#D89A16>${effectiveRecipePrice}</color>";
			}
			if (flag && _dailySpecialText == null)
			{
				CreateDailySpecialLabel();
			}
			if (_dailySpecialText != null)
			{
				_dailySpecialText.text = (flag ? $"+{bonusAmount}" : string.Empty);
			}
		}

		private string GetItemName()
		{
			if (_itemData == null && _recipeData == null)
			{
				return string.Empty;
			}
			if (_recipeData != null)
			{
				if (!(_recipeData.BuiltInSushi != null))
				{
					return _recipeData.DisplayName;
				}
				return _recipeData.BuiltInSushi.LocalizedName;
			}
			if (!string.IsNullOrWhiteSpace(_itemData.LocalizedName))
			{
				return _itemData.LocalizedName;
			}
			return _itemData.name;
		}

		private void OnClickItemButton()
		{
			if (!(_itemData == null) || _recipeData != null)
			{
				_onClicked?.Invoke(this);
				ShowDetails();
			}
		}

		public void SetSelected(bool selected)
		{
			if (_selectedOutline != null)
			{
				_selectedOutline.SetActive(selected);
			}
		}

		public void SetInMenu(bool inMenu)
		{
			CacheDefaultColors();
			_isInMenu = inMenu;
			if (_inMenuMarker != null)
			{
				_inMenuMarker.SetActive(inMenu);
			}
			ApplyMenuStateColors();
		}

		public void ShowDetails()
		{
			if (!(_menuItemDetailPage == null) && (!(_itemData == null) || _recipeData != null))
			{
				if (_recipeData != null)
				{
					_menuItemDetailPage.UpdateItemDetailPage(_recipeData);
				}
				else
				{
					_menuItemDetailPage.UpdateItemDetailPage(_itemData);
				}
				_menuItemDetailPage.gameObject.SetActive(value: true);
			}
		}

		public void SetDimmed(bool dimmed)
		{
			IsAvailable = !dimmed;
			float alpha = (dimmed ? _dimmedAlpha : 1f);
			SetGraphicAlpha(_backgroundImage, alpha);
			SetGraphicAlpha(_iconImage, alpha);
			SetGraphicAlpha(_priceBackgroundImage, alpha);
			SetGraphicAlpha(_priceText, alpha);
			SetGraphicAlpha(_nameText, alpha);
			if (_dailySpecialBadge != null)
			{
				SetGraphicAlpha(_dailySpecialBadge.GetComponent<Image>(), alpha);
			}
			SetGraphicAlpha(_dailySpecialText, alpha);
		}

		public bool MatchesSearch(string searchText)
		{
			if (string.IsNullOrWhiteSpace(searchText))
			{
				return true;
			}
			return LocalizationDatabase.ContainsLocalized((_nameText != null && !string.IsNullOrWhiteSpace(_nameText.text)) ? _nameText.text : GetItemName(), searchText);
		}

		private int GetBasePrice()
		{
			return Mathf.Max(0, _recipeData?.Price ?? _itemData?.Price ?? 0);
		}

		private void CacheDefaultColors()
		{
			if (!_defaultColorsCached)
			{
				_defaultBackgroundColor = ((_backgroundImage != null) ? _backgroundImage.color : Color.white);
				_defaultPriceBackgroundColor = ((_priceBackgroundImage != null) ? _priceBackgroundImage.color : Color.white);
				_defaultColorsCached = true;
			}
		}

		private void ApplyMenuStateColors()
		{
			ApplyStateColor(_backgroundImage, _isInMenu ? _inMenuBackgroundColor : _defaultBackgroundColor);
			ApplyStateColor(_priceBackgroundImage, _isInMenu ? _inMenuPriceBackgroundColor : _defaultPriceBackgroundColor);
		}

		private static void ApplyStateColor(Graphic graphic, Color targetColor)
		{
			if (!(graphic == null))
			{
				targetColor.a = graphic.color.a;
				graphic.color = targetColor;
			}
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

		private void CreateDailySpecialLabel()
		{
			if (!(_dailySpecialBadge == null))
			{
				GameObject gameObject = new GameObject("PercentText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
				gameObject.transform.SetParent(_dailySpecialBadge.transform, worldPositionStays: false);
				RectTransform component = gameObject.GetComponent<RectTransform>();
				component.anchorMin = Vector2.zero;
				component.anchorMax = Vector2.one;
				component.offsetMin = Vector2.zero;
				component.offsetMax = Vector2.zero;
				_dailySpecialText = gameObject.GetComponent<TextMeshProUGUI>();
				_dailySpecialText.alignment = TextAlignmentOptions.Center;
				_dailySpecialText.fontSize = 3.1f;
				_dailySpecialText.fontStyle = FontStyles.Bold;
				_dailySpecialText.enableAutoSizing = true;
				_dailySpecialText.fontSizeMin = 1.5f;
				_dailySpecialText.fontSizeMax = 3.1f;
				_dailySpecialText.color = new Color(0.08f, 0.34f, 0.08f);
				_dailySpecialText.raycastTarget = false;
			}
		}
	}
}
