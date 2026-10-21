using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.UI.Notify;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Tablet
{
	public class RestaurantMenuItem : MonoBehaviour
	{
		[SerializeField]
		private Image _itemImage;

		[SerializeField]
		private TextMeshProUGUI _itemName;

		[SerializeField]
		private TextMeshProUGUI _itemPrice;

		[SerializeField]
		private BaseButton _removeButton;

		private MenuApp _menuApp;

		private ServedDishSO _data;

		private SushiRecipeDefinition _recipe;

		private string RecipeId => _recipe?.RecipeId ?? _data?.Id ?? string.Empty;

		public void Initialize(ServedDishSO data, MenuApp menuApp)
		{
			_data = data;
			_recipe = null;
			_menuApp = menuApp;
			_removeButton.Clicked += OnClickRemoveButton;
			RefreshLocalization();
			RefreshPrice();
			_itemImage.sprite = data.Sprite;
		}

		public void Initialize(SushiRecipeDefinition recipe, MenuApp menuApp)
		{
			_data = recipe?.BuiltInSushi;
			_recipe = recipe;
			_menuApp = menuApp;
			_removeButton.Clicked -= OnClickRemoveButton;
			_removeButton.Clicked += OnClickRemoveButton;
			RefreshLocalization();
			RefreshPrice();
			_itemImage.sprite = recipe?.Icon;
			_itemImage.enabled = recipe?.Icon != null;
		}

		public void RefreshLocalization()
		{
			if ((!(_data == null) || _recipe != null) && !(_itemName == null))
			{
				_itemName.text = ((_recipe == null) ? (string.IsNullOrWhiteSpace(_data.LocalizedName) ? _data.name : _data.LocalizedName) : ((_recipe.BuiltInSushi != null) ? _recipe.BuiltInSushi.LocalizedName : _recipe.DisplayName));
			}
		}

		public void RefreshPrice()
		{
			if ((!(_data == null) || _recipe != null) && !(_itemPrice == null))
			{
				int num = ((TabletNetworkHandler.Instance != null) ? TabletNetworkHandler.Instance.GetEffectiveRecipePrice(RecipeId, _recipe?.Price ?? _data.Price) : (_recipe?.Price ?? _data.Price));
				_itemPrice.text = "$" + num;
			}
		}

		private void OnClickRemoveButton()
		{
			int num;
			if (_recipe == null)
			{
				ServedDishSO data = _data;
				num = (((object)data != null && data.ServedDishType == ServedDishType.Sushi) ? 1 : 0);
			}
			else
			{
				num = 1;
			}
			bool isSushi = (byte)num != 0;
			if (_menuApp != null && _menuApp.CanRemoveRecipeFromMenu(RecipeId, isSushi))
			{
				ServiceLocator.ForSceneOf(this).Get<TabletNetworkHandler>(out var service);
				service.ServerRemoveItemFromMenuRpc(RecipeId);
			}
			else
			{
				Singleton<NotifyManager>.Instance.ShowToast(TabletLocalization.Get("tablet.menu.requires_sushi", "You must have at least one sushi on the menu."));
			}
		}
	}
}
