using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Tablet
{
	public class MenuItemDetailPage : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI _menuItemName;

		[SerializeField]
		private BaseButton _addButton;

		[SerializeField]
		private Image _itemImage;

		[SerializeField]
		private TextMeshProUGUI _itemPrice;

		[Header("Ingredients")]
		[SerializeField]
		private IngredientItemUI _ingredientItemUIPrefab;

		[SerializeField]
		private Transform _ingredientsContainer;

		[SerializeField]
		private List<IngredientItemUI> _ingredientItems = new List<IngredientItemUI>();

		private ServedDishSO _itemData;

		private SushiRecipeDefinition _recipeData;

		private string _recipeId = string.Empty;

		private bool _isSushi;

		private readonly List<BaseIngredientNeed> _displayedIngredientNeeds = new List<BaseIngredientNeed>();

		private readonly List<string> _displayedIngredientIds = new List<string>();

		private bool _isCurrentItemInMenu;

		public void UpdateItemDetailPage(ServedDishSO itemData)
		{
			ClearIngredientItems();
			if (itemData == null)
			{
				UtilityMethods.Log("[MenuItemDetailPage] Update blocked. Item data is null.", LogType.Warning);
				return;
			}
			_itemData = itemData;
			_recipeData = null;
			_recipeId = itemData.Id;
			_isSushi = itemData.ServedDishType == ServedDishType.Sushi;
			RefreshMenuButtonState();
			_menuItemName.text = _itemData.LocalizedName;
			_itemImage.sprite = _itemData.Sprite;
			int num = ((TabletNetworkHandler.Instance != null) ? TabletNetworkHandler.Instance.GetEffectiveServedDishPrice(_itemData) : _itemData.Price);
			_itemPrice.text = "$" + num;
			if (TabletNetworkHandler.Instance != null && TabletNetworkHandler.Instance.TryGetDailyDishBonus(_itemData.Id, out var bonusAmount))
			{
				_itemPrice.text += $"  <color=#D89A16>+{bonusAmount} Daily Special</color>";
			}
			if (_itemData.Recipe == null)
			{
				UtilityMethods.Log("[MenuItemDetailPage] Recipe missing. ItemId=" + _itemData.Id, LogType.Warning);
				return;
			}
			foreach (BaseIngredientNeed sortedIngredientNeed in GetSortedIngredientNeeds(_itemData))
			{
				if (!IsValidIngredientNeed(sortedIngredientNeed))
				{
					UtilityMethods.Log("[MenuItemDetailPage] Ingredient row skipped because recipe need has no item id. ItemId=" + _itemData.Id, LogType.Warning);
					continue;
				}
				IngredientItemUI ingredientItemUI = Object.Instantiate(_ingredientItemUIPrefab, _ingredientsContainer);
				_ingredientItems.Add(ingredientItemUI);
				_displayedIngredientNeeds.Add(sortedIngredientNeed);
				_displayedIngredientIds.Add(sortedIngredientNeed.IngredientId);
				ingredientItemUI.Initialize(sortedIngredientNeed);
			}
			CheckIngredientAmounts();
		}

		public void UpdateItemDetailPage(SushiRecipeDefinition recipe)
		{
			if (recipe?.BuiltInSushi != null)
			{
				UpdateItemDetailPage(recipe.BuiltInSushi);
				return;
			}
			ClearIngredientItems();
			if (recipe?.Composition?.IngredientIds == null)
			{
				return;
			}
			_itemData = null;
			_recipeData = recipe;
			_recipeId = recipe.RecipeId;
			_isSushi = true;
			RefreshMenuButtonState();
			_menuItemName.text = recipe.DisplayName;
			_itemImage.sprite = recipe.Icon;
			_itemImage.enabled = recipe.Icon != null;
			int num = ((TabletNetworkHandler.Instance != null) ? TabletNetworkHandler.Instance.GetEffectiveRecipePrice(recipe.RecipeId, recipe.Price) : recipe.Price);
			_itemPrice.text = "$" + num;
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			List<string> list = new List<string>();
			foreach (string ingredientId in recipe.Composition.IngredientIds)
			{
				if (!string.IsNullOrWhiteSpace(ingredientId))
				{
					if (dictionary.ContainsKey(ingredientId))
					{
						dictionary[ingredientId]++;
						continue;
					}
					dictionary.Add(ingredientId, 1);
					list.Add(ingredientId);
				}
			}
			ItemDatabaseSO itemDatabaseSO = Singleton<GameProgressManager>.Instance?.ItemDatabaseSO;
			foreach (string item in list)
			{
				IngredientItemUI ingredientItemUI = Object.Instantiate(_ingredientItemUIPrefab, _ingredientsContainer);
				_ingredientItems.Add(ingredientItemUI);
				_displayedIngredientIds.Add(item);
				ingredientItemUI.Initialize(itemDatabaseSO?.FindById(item), dictionary[item]);
			}
			CheckIngredientAmounts();
		}

		public void Initialize()
		{
			_addButton.Clicked -= OnClickAddButton;
			_addButton.Clicked += OnClickAddButton;
		}

		public void CheckIngredientAmounts()
		{
			RestaurantManager restaurantManager = Singleton<GameProgressManager>.Instance.RestaurantManager;
			if (!(restaurantManager == null))
			{
				int num = Mathf.Min(_displayedIngredientIds.Count, _ingredientItems.Count);
				for (int i = 0; i < num; i++)
				{
					int stockedItemAmount = restaurantManager.GetStockedItemAmount(_displayedIngredientIds[i]);
					_ingredientItems[i].UpdateIngredientAmount(stockedItemAmount);
				}
			}
		}

		private void OnClickAddButton()
		{
			if (!string.IsNullOrWhiteSpace(_recipeId))
			{
				MenuApp menuApp = GetMenuApp();
				menuApp?.RefreshActiveTabSelection();
				if (_isCurrentItemInMenu)
				{
					RemoveItemFromMenu(menuApp);
				}
				else
				{
					AddSushiToMenu();
				}
			}
		}

		private void CheckHasEnoughItem()
		{
			RestaurantManager restaurantManager = Singleton<GameProgressManager>.Instance.RestaurantManager;
			int stockedItemAmount = restaurantManager.GetStockedItemAmount(_itemData.Id);
			int num = CalculateCraftableSushiCount(restaurantManager, _itemData);
			bool num2 = stockedItemAmount >= 3;
			bool flag = num >= 3;
			if (num2 & flag)
			{
				AddSushiToMenu();
				return;
			}
			string message = BuildSushiMenuWarningMessage(stockedItemAmount, num, 3);
			Singleton<NotifyManager>.Instance.ShowPopup(_itemData.LocalizedName, message, OnConfirm);
		}

		private void OnConfirm()
		{
			AddSushiToMenu();
			Singleton<NotifyManager>.Instance.HidePopup();
		}

		private void AddSushiToMenu()
		{
			ServiceLocator.ForSceneOf(this).Get<TabletNetworkHandler>(out var service);
			service.ServerAddItemToMenuRpc(_recipeId);
		}

		private void RemoveItemFromMenu(MenuApp menuApp)
		{
			if (menuApp != null && !menuApp.CanRemoveRecipeFromMenu(_recipeId, _isSushi))
			{
				Singleton<NotifyManager>.Instance.ShowToast(TabletLocalization.Get("tablet.menu.requires_sushi", "You must have at least one sushi on the menu."));
				RefreshMenuButtonState();
			}
			else
			{
				ServiceLocator.ForSceneOf(this).Get<TabletNetworkHandler>(out var service);
				service.ServerRemoveItemFromMenuRpc(_recipeId);
			}
		}

		private void RefreshMenuButtonState()
		{
			if (!(_addButton == null) && !string.IsNullOrWhiteSpace(_recipeId))
			{
				MenuApp menuApp = GetMenuApp();
				_isCurrentItemInMenu = menuApp != null && menuApp.IsItemInMenu(_recipeId);
				_addButton.gameObject.SetActive(value: true);
				if (_isCurrentItemInMenu)
				{
					_addButton.SetLocalizedText("UiTable", "tablet.menu.remove_from_menu", "Remove From Menu");
				}
				else
				{
					_addButton.SetLocalizedText("UiTable", "tablet.menu.add_to_menu", "Add to Menu");
				}
			}
		}

		private static MenuApp GetMenuApp()
		{
			if (!(Singleton<PlayersManager>.Instance != null) || !(Singleton<PlayersManager>.Instance.LocalPlayer != null) || !(Singleton<PlayersManager>.Instance.LocalPlayer.TabletController != null))
			{
				return null;
			}
			return Singleton<PlayersManager>.Instance.LocalPlayer.TabletController.MenuApp;
		}

		private string BuildSushiMenuWarningMessage(int currentStock, int craftableCount, int threshold)
		{
			if (currentStock < threshold && craftableCount < threshold)
			{
				if (craftableCount <= 0)
				{
					return $"You only have {currentStock} sushi in stock and you don't have enough ingredients to make more than {threshold} sushi. Do you still want to add this sushi to the menu?";
				}
				return $"You only have {currentStock} sushi in stock and you can only make {craftableCount} more with your current ingredients. Do you still want to add this sushi to the menu?";
			}
			if (currentStock < threshold)
			{
				return $"You only have {currentStock} sushi in stock. Do you still want to add this sushi to the menu?";
			}
			if (craftableCount < threshold)
			{
				if (craftableCount <= 0)
				{
					return $"You don't have enough ingredients to make more than {threshold} sushi. Do you still want to add this sushi to the menu?";
				}
				return $"You only have enough ingredients to make {craftableCount} more sushi. Do you still want to add this sushi to the menu?";
			}
			return "Are you sure you want to add this sushi to the menu?";
		}

		private int CalculateCraftableSushiCount(RestaurantManager restaurantManager, ServedDishSO itemData)
		{
			if (restaurantManager == null || itemData == null || itemData.Recipe == null)
			{
				return 0;
			}
			int num = int.MaxValue;
			foreach (BaseIngredientNeed sortedIngredientNeed in GetSortedIngredientNeeds(itemData))
			{
				if (!IsValidIngredientNeed(sortedIngredientNeed))
				{
					return 0;
				}
				int num2 = restaurantManager.GetStockedItemAmount(sortedIngredientNeed.IngredientId) / sortedIngredientNeed.RequiredAmount;
				if (num2 < num)
				{
					num = num2;
				}
			}
			if (num == int.MaxValue)
			{
				return 0;
			}
			return num;
		}

		private void ClearIngredientItems()
		{
			foreach (IngredientItemUI item in _ingredientItems.ToList())
			{
				_ingredientItems.Remove(item);
				if (item != null)
				{
					Object.Destroy(item.gameObject);
				}
			}
			_displayedIngredientNeeds.Clear();
			_displayedIngredientIds.Clear();
		}

		private static List<BaseIngredientNeed> GetSortedIngredientNeeds(ServedDishSO itemData)
		{
			List<BaseIngredientNeed> list = new List<BaseIngredientNeed>();
			if (itemData?.Recipe?.Ingredients != null)
			{
				list.AddRange(itemData.Recipe.Ingredients);
			}
			if (itemData?.Recipe?.ProcessedIngredients != null)
			{
				list.AddRange(itemData.Recipe.ProcessedIngredients);
			}
			return list.OrderBy((BaseIngredientNeed need) => need?.Order ?? int.MaxValue).ToList();
		}

		private static bool IsValidIngredientNeed(BaseIngredientNeed need)
		{
			if (need != null && !string.IsNullOrEmpty(need.IngredientId))
			{
				return need.RequiredAmount > 0;
			}
			return false;
		}
	}
}
