using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.UI.Notify;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace PaperPixelGames.Core.Tablet
{
	public class MenuApp : BaseApp
	{
		[SerializeField]
		private ItemDatabaseSO _itemDatabaseSO;

		[SerializeField]
		private FoodItem _foodItemPrefab;

		[Header("Containers")]
		[SerializeField]
		private Transform _sushiContainer;

		[SerializeField]
		private Transform _beverageContainer;

		[SerializeField]
		private Transform _sideDishContainer;

		[Header("Container Buttons")]
		[SerializeField]
		private BaseButton _sushiContainerButton;

		[SerializeField]
		private BaseButton _beverageContainerButton;

		[SerializeField]
		private BaseButton _sideDishContainerButton;

		[Header("Container Info Buttons")]
		[SerializeField]
		private BaseButton _beverageInfoButton;

		[SerializeField]
		private BaseButton _sideDishInfoButton;

		[Header("Menu")]
		[SerializeField]
		private RestaurantMenuItem _restaurantMenuItemPrefab;

		[SerializeField]
		private Transform _menuContainer;

		[SerializeField]
		private MenuItemDetailPage _menuItemDetailPage;

		[SerializeField]
		private TMP_InputField _menuSearchInput;

		[Header("Custom Recipe Icons")]
		[SerializeField]
		private Sprite _customMakiIcon;

		[SerializeField]
		private Sprite _customUramakiIcon;

		[SerializeField]
		private Sprite _customTemakiIcon;

		private RestaurantManager _restaurantManager;

		private float _dessertOrderChance = 0.2f;

		private float _beverageOrderChance = 0.5f;

		private readonly List<ItemSO> _servedDishes = new List<ItemSO>();

		private readonly List<ServedDishSO> _sushiList = new List<ServedDishSO>();

		private readonly List<ServedDishSO> _beverageList = new List<ServedDishSO>();

		private readonly List<ServedDishSO> _dessertList = new List<ServedDishSO>();

		private readonly List<ServedDishSO> _sideDishList = new List<ServedDishSO>();

		private readonly List<SushiRecipeDefinition> _customSushiList = new List<SushiRecipeDefinition>();

		private readonly List<ItemSO> _menuList = new List<ItemSO>();

		private readonly HashSet<string> _menuRecipeIds = new HashSet<string>();

		private readonly Dictionary<string, RestaurantMenuItem> _menuItemUIDict = new Dictionary<string, RestaurantMenuItem>();

		private readonly Dictionary<string, bool> _dishAvailabilityMap = new Dictionary<string, bool>();

		private readonly Dictionary<string, FoodItem> _foodCards = new Dictionary<string, FoodItem>();

		private bool _stockVisualsDirty;

		private static readonly ProfilerMarker StockRefreshMarker = new ProfilerMarker("Tablet.Menu.RefreshStock");

		private readonly Dictionary<string, List<string>> _wholesaleIdsByOutputDishId = new Dictionary<string, List<string>>();

		private string _prioritizedQuestMenuSushiId = string.Empty;

		private FoodItem _selectedFoodItem;

		private Transform _activeContainer;

		private bool _initialized;

		private bool _searchInputOwnsGameplayLock;

		public List<ItemSO> MenuList => _menuList;

		public IReadOnlyCollection<string> MenuRecipeIds => _menuRecipeIds;

		public override void Initialize()
		{
			if (!_initialized)
			{
				_initialized = true;
				LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
				LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
				_sushiContainerButton.Clicked += OnSushiContainerButtonClick;
				_beverageContainerButton.Clicked += OnBeverageContainerButtonClick;
				_sideDishContainerButton.Clicked += OnSideDishContainerButtonClick;
				ConfigureLocalizedCategoryLabels();
				if (_menuSearchInput != null)
				{
					_menuSearchInput.onValueChanged.AddListener(OnMenuSearchChanged);
					_menuSearchInput.onSelect.AddListener(OnMenuSearchSelected);
					_menuSearchInput.onDeselect.AddListener(OnMenuSearchDeselected);
				}
				if (_beverageInfoButton != null)
				{
					_beverageInfoButton.Clicked += OnBeverageInfoButtonClick;
				}
				if (_sideDishInfoButton != null)
				{
					_sideDishInfoButton.Clicked += OnSideDishInfoButtonClick;
				}
				_restaurantManager = Singleton<GameProgressManager>.Instance.RestaurantManager;
				if (_restaurantManager != null)
				{
					_restaurantManager.StockChanged += OnStockChanged;
					_restaurantManager.CustomSushiRecipeCatalogChanged += OnCustomRecipeCatalogChanged;
				}
				_menuItemDetailPage.Initialize();
				GetAllMenuItems();
				RefreshAllFoodItems();
				PrepareContainers();
				ApplyMenuSearchFilter();
				OnSushiContainerButtonClick();
			}
		}

		private void OnDestroy()
		{
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			_sushiContainerButton.Clicked -= OnSushiContainerButtonClick;
			_beverageContainerButton.Clicked -= OnBeverageContainerButtonClick;
			_sideDishContainerButton.Clicked -= OnSideDishContainerButtonClick;
			if (_menuSearchInput != null)
			{
				_menuSearchInput.onValueChanged.RemoveListener(OnMenuSearchChanged);
				_menuSearchInput.onSelect.RemoveListener(OnMenuSearchSelected);
				_menuSearchInput.onDeselect.RemoveListener(OnMenuSearchDeselected);
			}
			SetSearchInputGameplayLock(shouldLock: false);
			if (_beverageInfoButton != null)
			{
				_beverageInfoButton.Clicked -= OnBeverageInfoButtonClick;
			}
			if (_sideDishInfoButton != null)
			{
				_sideDishInfoButton.Clicked -= OnSideDishInfoButtonClick;
			}
			if (_restaurantManager != null)
			{
				_restaurantManager.StockChanged -= OnStockChanged;
				_restaurantManager.CustomSushiRecipeCatalogChanged -= OnCustomRecipeCatalogChanged;
			}
		}

		private void OnSelectedLocaleChanged(Locale locale)
		{
			if (!_initialized)
			{
				return;
			}
			RefreshFoodItemsPreservingSelection();
			foreach (RestaurantMenuItem value in _menuItemUIDict.Values)
			{
				value?.RefreshLocalization();
			}
			ApplyMenuSearchFilter();
		}

		private void ConfigureLocalizedCategoryLabels()
		{
			_sushiContainerButton?.SetLocalizedText("UiTable", "tablet.menu.category.sushi", "Sushi");
			_beverageContainerButton?.SetLocalizedText("UiTable", "tablet.menu.category.beverages", "Beverages");
			_sideDishContainerButton?.SetLocalizedText("UiTable", "tablet.menu.category.side_dishes", "Side Dishes");
		}

		public void RefreshOnOpen()
		{
			_restaurantManager?.ClientRequestMissingCustomSushiThumbnails();
			RefreshFoodItemsPreservingSelection();
			ApplyMenuSearchFilter();
			ShowDailySushiSpecialIntroIfNeeded();
		}

		public void RefreshDailySushiSpecials()
		{
			if (!_initialized)
			{
				return;
			}
			RefreshFoodItemsPreservingSelection();
			foreach (RestaurantMenuItem value in _menuItemUIDict.Values)
			{
				value?.RefreshPrice();
			}
			ShowDailySushiSpecialIntroIfNeeded();
		}

		private void ShowDailySushiSpecialIntroIfNeeded()
		{
			if (PlayerPrefs.GetInt("DailySushiSpecialIntroSeen", 0) != 1 && base.gameObject.activeInHierarchy && !(TabletNetworkHandler.Instance == null) && TabletNetworkHandler.Instance.HasDailySushiSpecials && !(Singleton<NotifyManager>.Instance == null))
			{
				PlayerPrefs.SetInt("DailySushiSpecialIntroSeen", 1);
				PlayerPrefs.Save();
				Singleton<NotifyManager>.Instance.ShowPopup(LocalizationDatabase.GetString("UiTable", "tablet.menu.daily_special.title", "Daily Special"), LocalizationDatabase.GetString("UiTable", "tablet.menu.daily_special.description", "Sushi, side dishes, or beverages with this badge sell for a higher price today. Add the item to your menu to use the bonus."), () =>
				{
				}, null, "Accept", useDescriptionBackground: true, 0f, closeOnEscape: true, "ui.popup.accept");
			}
		}

		private void OnDisable()
		{
			SetSearchInputGameplayLock(shouldLock: false);
		}

		private void Update()
		{
			bool flag = _menuSearchInput != null && _menuSearchInput.isFocused;
			if (flag != _searchInputOwnsGameplayLock)
			{
				SetSearchInputGameplayLock(flag);
			}
		}

		private void OnStockChanged(string itemId, int amount)
		{
			_stockVisualsDirty = true;
		}

		private void LateUpdate()
		{
			if (_initialized && _stockVisualsDirty && base.isActiveAndEnabled)
			{
				RefreshStockVisuals();
			}
		}

		private void RefreshStockVisuals()
		{
			using (StockRefreshMarker.Auto())
			{
				_stockVisualsDirty = false;
				RebuildAvailabilityMap();
				bool flag = false;
				foreach (KeyValuePair<string, FoodItem> foodCard in _foodCards)
				{
					FoodItem value = foodCard.Value;
					if (!(value == null))
					{
						bool flag2 = _dishAvailabilityMap.TryGetValue(value.RecipeId, out var value2) & value2;
						if (value.IsAvailable != flag2)
						{
							value.SetDimmed(!flag2);
							flag = true;
						}
					}
				}
				if (flag)
				{
					ReorderFoodContainer(_sushiContainer, _sushiList);
					ReorderFoodContainer(_beverageContainer, _beverageList);
					ReorderFoodContainer(_sideDishContainer, _sideDishList);
				}
				if (_menuItemDetailPage != null && _selectedFoodItem != null)
				{
					_menuItemDetailPage.CheckIngredientAmounts();
				}
			}
		}

		private void ReorderFoodContainer(Transform container, List<ServedDishSO> sourceList)
		{
			if (container == null)
			{
				return;
			}
			List<FoodItem> list = new List<FoodItem>();
			foreach (ServedDishSO source in sourceList)
			{
				if (source != null && _foodCards.TryGetValue(source.Id, out var value) && value != null && value.transform.parent == container)
				{
					list.Add(value);
				}
			}
			if (container == _sushiContainer)
			{
				foreach (SushiRecipeDefinition customSushi in _customSushiList)
				{
					if (_foodCards.TryGetValue(customSushi.RecipeId, out var value2) && value2 != null && value2.transform.parent == container)
					{
						list.Add(value2);
					}
				}
			}
			int num = 0;
			foreach (FoodItem item in from card in list
				orderby IsPrioritizedQuestMenuSushi(card.ItemData) descending, card.IsAvailable descending, card.RecipeData?.Price ?? card.ItemData?.Price ?? 0, card.RecipeData?.DisplayName ?? card.ItemData?.LocalizedName ?? string.Empty
				select card)
			{
				if (item.transform.GetSiblingIndex() != num)
				{
					item.transform.SetSiblingIndex(num);
				}
				num++;
			}
		}

		private void OnSushiContainerButtonClick()
		{
			ActivateContainer(_sushiContainer);
		}

		private void OnBeverageContainerButtonClick()
		{
			ActivateContainer(_beverageContainer);
		}

		private void OnSideDishContainerButtonClick()
		{
			ActivateContainer(_sideDishContainer);
		}

		private void OnBeverageInfoButtonClick()
		{
			ShowOptionalMenuExtraInfo(LocalizationDatabase.GetString("UiTable", "tablet.menu.category.beverages", "Beverages"), LocalizationDatabase.GetString("UiTable", "tablet.menu.info.beverages.description", "Customers can ask for drinks only if you add drinks to today's menu. Drinks are optional extras: even when a customer asks for one, you can complete the main sushi order without serving it. Serving drinks earns extra money."));
		}

		private void OnSideDishInfoButtonClick()
		{
			ShowOptionalMenuExtraInfo(LocalizationDatabase.GetString("UiTable", "tablet.menu.category.side_dishes", "Side Dishes"), LocalizationDatabase.GetString("UiTable", "tablet.menu.info.side_dishes.description", "Customers can ask for side dishes only if you add side dishes to today's menu. Side dishes are optional extras: even when a customer asks for one, you can complete the main sushi order without serving it. Serving side dishes earns extra money."));
		}

		private void ShowOptionalMenuExtraInfo(string title, string description)
		{
			RefreshTabButtonSelection();
			Singleton<NotifyManager>.Instance.ShowPopup(title, description, () =>
			{
			}, null, "Accept", useDescriptionBackground: true, 90f, closeOnEscape: true, "ui.popup.accept");
		}

		private void ActivateContainer(Transform target)
		{
			if (!(target == null))
			{
				_sushiContainer.gameObject.SetActive(target == _sushiContainer);
				_beverageContainer.gameObject.SetActive(target == _beverageContainer);
				_sideDishContainer.gameObject.SetActive(target == _sideDishContainer);
				_activeContainer = target;
				RefreshTabButtonSelection();
				ApplyMenuSearchFilter();
				if (_menuItemDetailPage != null)
				{
					_menuItemDetailPage.gameObject.SetActive(value: true);
				}
				SelectFirstItemIn(target);
			}
		}

		private void OnFoodItemClicked(FoodItem clicked)
		{
			if (clicked == null)
			{
				return;
			}
			if (_selectedFoodItem == clicked)
			{
				clicked.SetSelected(selected: true);
				RefreshTabButtonSelection();
				clicked.ShowDetails();
				return;
			}
			if (_selectedFoodItem != null)
			{
				_selectedFoodItem.SetSelected(selected: false);
			}
			_selectedFoodItem = clicked;
			_selectedFoodItem.SetSelected(selected: true);
			RefreshTabButtonSelection();
			_selectedFoodItem.ShowDetails();
		}

		private void SelectFirstItemIn(Transform container)
		{
			if (container == null)
			{
				return;
			}
			FoodItem foodItem = null;
			FoodItem foodItem2 = null;
			int childCount = container.childCount;
			for (int i = 0; i < childCount; i++)
			{
				Transform child = container.GetChild(i);
				if (!(child == null) && child.TryGetComponent<FoodItem>(out var component))
				{
					if ((object)foodItem == null)
					{
						foodItem = component;
					}
					if (component.IsAvailable)
					{
						foodItem2 = component;
						break;
					}
				}
			}
			FoodItem foodItem3 = ((foodItem2 != null) ? foodItem2 : foodItem);
			if (foodItem3 == null)
			{
				if (_selectedFoodItem != null)
				{
					_selectedFoodItem.SetSelected(selected: false);
				}
				_selectedFoodItem = null;
				if (_menuItemDetailPage != null)
				{
					_menuItemDetailPage.gameObject.SetActive(value: false);
				}
			}
			else
			{
				OnFoodItemClicked(foodItem3);
			}
		}

		private void GetAllMenuItems()
		{
			_servedDishes.Clear();
			_sushiList.Clear();
			_beverageList.Clear();
			_dessertList.Clear();
			_sideDishList.Clear();
			_customSushiList.Clear();
			_wholesaleIdsByOutputDishId.Clear();
			_servedDishes.AddRange(_itemDatabaseSO.GetAllOfType(ItemType.ServedDish));
			foreach (ItemSO servedDish in _servedDishes)
			{
				if (servedDish is ServedDishSO servedDishSO)
				{
					if (servedDishSO.ServedDishType == ServedDishType.Sushi)
					{
						_sushiList.Add(servedDishSO);
					}
					else if (servedDishSO.ServedDishType == ServedDishType.Beverage)
					{
						_beverageList.Add(servedDishSO);
					}
					else if (servedDishSO.ServedDishType == ServedDishType.Dessert)
					{
						_dessertList.Add(servedDishSO);
					}
					else if (servedDishSO.ServedDishType == ServedDishType.SideDish)
					{
						_sideDishList.Add(servedDishSO);
					}
				}
			}
			IReadOnlyList<CustomSushiRecipeData> readOnlyList = _restaurantManager?.CustomSushiRecipeCatalog?.Recipes;
			if (readOnlyList != null)
			{
				foreach (CustomSushiRecipeData item in readOnlyList)
				{
					SushiRecipeDefinition sushiRecipeDefinition = SushiRecipeResolver.CreateCustomDefinition(item, ResolveCustomRecipeIcon(item.SushiType));
					if (sushiRecipeDefinition != null)
					{
						_customSushiList.Add(sushiRecipeDefinition);
					}
				}
			}
			BuildWholesaleOutputDishIndex();
		}

		private void BuildWholesaleOutputDishIndex()
		{
			foreach (ItemSO item in _itemDatabaseSO.GetAllOfType(ItemType.Wholesale))
			{
				if (item is WholesaleSO { OutputItem: ServedDishSO { ServedDishType: var servedDishType } outputItem } wholesaleSO && (servedDishType == ServedDishType.Beverage || servedDishType == ServedDishType.SideDish) && !string.IsNullOrEmpty(outputItem.Id) && !string.IsNullOrEmpty(wholesaleSO.Id))
				{
					if (!_wholesaleIdsByOutputDishId.TryGetValue(outputItem.Id, out var value))
					{
						value = new List<string>();
						_wholesaleIdsByOutputDishId.Add(outputItem.Id, value);
					}
					if (!value.Contains(wholesaleSO.Id))
					{
						value.Add(wholesaleSO.Id);
					}
				}
			}
		}

		private void RefreshAllFoodItems()
		{
			_stockVisualsDirty = false;
			RebuildAvailabilityMap();
			_prioritizedQuestMenuSushiId = ResolveActiveQuestMenuSushiId();
			RebuildFoodContainer(_sushiContainer, _sushiList);
			AppendCustomSushiFoodItems();
			ReorderFoodContainer(_sushiContainer, _sushiList);
			RebuildFoodContainer(_beverageContainer, _beverageList);
			RebuildFoodContainer(_sideDishContainer, _sideDishList);
			ApplyMenuSearchFilter();
		}

		private void RebuildAvailabilityMap()
		{
			_dishAvailabilityMap.Clear();
			foreach (ItemSO servedDish in _servedDishes)
			{
				if (servedDish is ServedDishSO servedDishSO)
				{
					_dishAvailabilityMap[servedDishSO.Id] = CanShowDishAsAvailable(servedDishSO);
				}
			}
			foreach (SushiRecipeDefinition customSushi in _customSushiList)
			{
				_dishAvailabilityMap[customSushi.RecipeId] = CanShowCustomRecipeAsAvailable(customSushi);
			}
		}

		private void RebuildFoodContainer(Transform container, List<ServedDishSO> sourceList)
		{
			if (container == null)
			{
				return;
			}
			for (int num = container.childCount - 1; num >= 0; num--)
			{
				Transform child = container.GetChild(num);
				if (child.TryGetComponent<FoodItem>(out var old))
				{
					bool num2 = old.ItemData != null && sourceList.Contains(old.ItemData);
					bool flag = container == _sushiContainer && _customSushiList.Any((SushiRecipeDefinition recipe) => recipe.RecipeId == old.RecipeId);
					if (!(num2 | flag))
					{
						if (!string.IsNullOrWhiteSpace(old.RecipeId))
						{
							_foodCards.Remove(old.RecipeId);
						}
						child.gameObject.SetActive(value: false);
						child.SetParent(null, worldPositionStays: false);
						UnityEngine.Object.Destroy(child.gameObject);
					}
				}
			}
			foreach (ServedDishSO item in sourceList.OrderByDescending(IsPrioritizedQuestMenuSushi).ThenByDescending(IsDishAvailable).ThenBy((ServedDishSO x) => x.Price)
				.ThenBy((ServedDishSO x) => x.LocalizedName)
				.ToList())
			{
				bool flag2 = IsDishAvailable(item);
				if (!_foodCards.TryGetValue(item.Id, out var value) || value == null)
				{
					value = (_foodCards[item.Id] = UnityEngine.Object.Instantiate(_foodItemPrefab, container));
				}
				if (value.transform.parent != container)
				{
					value.transform.SetParent(container, worldPositionStays: false);
				}
				value.transform.SetAsLastSibling();
				value.Initialize(item, _menuItemDetailPage, OnFoodItemClicked);
				value.SetInMenu(IsItemInMenu(item.Id));
				value.SetDimmed(!flag2);
			}
		}

		private void AppendCustomSushiFoodItems()
		{
			if (_sushiContainer == null)
			{
				return;
			}
			foreach (SushiRecipeDefinition item in from x in _customSushiList
				orderby x.Price, x.DisplayName
				select x)
			{
				if (!_foodCards.TryGetValue(item.RecipeId, out var value) || value == null)
				{
					value = (_foodCards[item.RecipeId] = UnityEngine.Object.Instantiate(_foodItemPrefab, _sushiContainer));
				}
				if (value.transform.parent != _sushiContainer)
				{
					value.transform.SetParent(_sushiContainer, worldPositionStays: false);
				}
				value.transform.SetAsLastSibling();
				value.Initialize(item, _menuItemDetailPage, OnFoodItemClicked, OnDeleteCustomSushiRequested);
				value.SetInMenu(IsItemInMenu(item.RecipeId));
				value.SetDimmed(!IsCustomRecipeAvailable(item));
			}
		}

		private bool IsCustomRecipeAvailable(SushiRecipeDefinition recipe)
		{
			bool value = default;
			return (recipe != null && _dishAvailabilityMap.TryGetValue(recipe.RecipeId, out value)) & value;
		}

		private static void OnDeleteCustomSushiRequested(string recipeId)
		{
			TabletNetworkHandler.Instance?.ServerDeleteCustomSushiRecipeRpc(recipeId);
		}

		private bool CanShowCustomRecipeAsAvailable(SushiRecipeDefinition recipe)
		{
			if (recipe != null && !string.IsNullOrWhiteSpace(recipe.RecipeId) && _restaurantManager != null)
			{
				return _restaurantManager.GetStockedItemAmount(recipe.RecipeId) > 0;
			}
			return false;
		}

		private void ClearContainer(Transform container)
		{
			for (int num = container.childCount - 1; num >= 0; num--)
			{
				Transform child = container.GetChild(num);
				child.gameObject.SetActive(value: false);
				child.SetParent(null, worldPositionStays: false);
				UnityEngine.Object.Destroy(child.gameObject);
			}
		}

		private bool IsDishAvailable(ServedDishSO servedDish)
		{
			if (servedDish == null || string.IsNullOrEmpty(servedDish.Id))
			{
				return false;
			}
			bool value;
			return _dishAvailabilityMap.TryGetValue(servedDish.Id, out value) & value;
		}

		private bool CanShowDishAsAvailable(ServedDishSO servedDish)
		{
			if (servedDish == null || string.IsNullOrEmpty(servedDish.Id) || _restaurantManager == null)
			{
				return false;
			}
			if (_restaurantManager.GetStockedItemAmount(servedDish.Id) > 0)
			{
				return true;
			}
			ServedDishType servedDishType = servedDish.ServedDishType;
			if (servedDishType == ServedDishType.Beverage || servedDishType == ServedDishType.SideDish)
			{
				return HasStockedWholesaleOutputForDish(servedDish.Id);
			}
			return false;
		}

		private bool HasStockedWholesaleOutputForDish(string dishId)
		{
			if (!_wholesaleIdsByOutputDishId.TryGetValue(dishId, out var value))
			{
				return false;
			}
			for (int i = 0; i < value.Count; i++)
			{
				if (_restaurantManager.GetStockedItemAmount(value[i]) > 0)
				{
					return true;
				}
			}
			return false;
		}

		private bool IsPrioritizedQuestMenuSushi(ServedDishSO servedDish)
		{
			if (servedDish != null && servedDish.ServedDishType == ServedDishType.Sushi && !string.IsNullOrWhiteSpace(servedDish.Id))
			{
				return servedDish.Id == _prioritizedQuestMenuSushiId;
			}
			return false;
		}

		private string ResolveActiveQuestMenuSushiId()
		{
			QuestReferenceHub instance = QuestReferenceHub.Instance;
			if (instance == null || _itemDatabaseSO == null)
			{
				return string.Empty;
			}
			if (instance.IsActive(QuestId.AddCucumberMakiToMenu))
			{
				return GetServedDishId(_itemDatabaseSO.CucumberMakiServeredDishSO);
			}
			if (instance.IsActive(QuestId.AddMackerelNigiriToMenu))
			{
				return GetServedDishId(_itemDatabaseSO.MackarelNigiriServeredDishSO);
			}
			if (instance.IsActive(QuestId.AddChinookSalmonDishToMenu))
			{
				return GetServedDishId(_itemDatabaseSO.ChinookSalmonTemakiServeredDishSO);
			}
			if (instance.IsActive(QuestId.AddSquidNigiriToTheMenu))
			{
				return GetServedDishId(_itemDatabaseSO.SquidNigiriServeredDishSO);
			}
			return string.Empty;
		}

		private static string GetServedDishId(ItemSO itemSO)
		{
			if (!(itemSO is ServedDishSO servedDishSO) || string.IsNullOrWhiteSpace(servedDishSO.Id))
			{
				return string.Empty;
			}
			return servedDishSO.Id;
		}

		private void PrepareContainers()
		{
			_sushiContainer.gameObject.SetActive(value: true);
			_beverageContainer.gameObject.SetActive(value: false);
			_sideDishContainer.gameObject.SetActive(value: false);
		}

		public bool IsItemInMenu(string itemId)
		{
			if (!string.IsNullOrEmpty(itemId))
			{
				return _menuRecipeIds.Contains(itemId);
			}
			return false;
		}

		public bool CanRemoveItemFromMenu(ServedDishSO item)
		{
			return CanRemoveRecipeFromMenu(item?.Id, (object)item != null && item.ServedDishType == ServedDishType.Sushi);
		}

		public bool CanRemoveRecipeFromMenu(string recipeId, bool isSushi)
		{
			if (!isSushi)
			{
				return true;
			}
			int num = 0;
			foreach (string menuRecipeId in _menuRecipeIds)
			{
				if (TryResolveSushiRecipe(menuRecipeId, out var _))
				{
					num++;
				}
			}
			return num > 1;
		}

		public void RefreshActiveTabSelection()
		{
			RefreshTabButtonSelection();
		}

		public void AddItemToMenu(ServedDishSO itemSO)
		{
			_menuList.Add(itemSO);
		}

		public bool TryGetRandomCustomerSelection(System.Random rng, out ServedDishSO mainDish, out ServedDishSO dessert, out ServedDishSO beverage)
		{
			mainDish = null;
			dessert = null;
			beverage = null;
			if (_menuList == null || _menuList.Count == 0)
			{
				return false;
			}
			List<ServedDishSO> list = new List<ServedDishSO>();
			List<ServedDishSO> list2 = new List<ServedDishSO>();
			List<ServedDishSO> list3 = new List<ServedDishSO>();
			foreach (ItemSO menu in _menuList)
			{
				if (menu is ServedDishSO servedDishSO)
				{
					switch (servedDishSO.ServedDishType)
					{
					case ServedDishType.Dessert:
						list2.Add(servedDishSO);
						break;
					case ServedDishType.Beverage:
						list3.Add(servedDishSO);
						break;
					case ServedDishType.Sushi:
						list.Add(servedDishSO);
						break;
					}
				}
			}
			if (list.Count == 0)
			{
				return false;
			}
			int index = rng.Next(0, list.Count);
			mainDish = list[index];
			if (list2.Count > 0 && rng.NextDouble() < (double)_dessertOrderChance)
			{
				int index2 = rng.Next(0, list2.Count);
				dessert = list2[index2];
			}
			if (list3.Count > 0 && rng.NextDouble() < (double)_beverageOrderChance)
			{
				int index3 = rng.Next(0, list3.Count);
				beverage = list3[index3];
			}
			return true;
		}

		public void OnMenuListChanged(NetworkListEvent<FixedString64Bytes> change)
		{
			string entryId = change.Value.ToString();
			switch (change.Type)
			{
			case NetworkListEvent<FixedString64Bytes>.EventType.Add:
			case NetworkListEvent<FixedString64Bytes>.EventType.Insert:
			case NetworkListEvent<FixedString64Bytes>.EventType.Value:
				HandleMenuEntryAddedOrUpdated(entryId);
				break;
			case NetworkListEvent<FixedString64Bytes>.EventType.Remove:
			case NetworkListEvent<FixedString64Bytes>.EventType.RemoveAt:
				HandleMenuEntryRemoved(entryId);
				break;
			case NetworkListEvent<FixedString64Bytes>.EventType.Clear:
				HandleMenuCleared();
				break;
			}
			RefreshFoodItemsPreservingSelection();
		}

		private void HandleMenuEntryAddedOrUpdated(string entryId)
		{
			if (string.IsNullOrEmpty(entryId))
			{
				return;
			}
			_menuRecipeIds.Add(entryId);
			if (_menuItemUIDict.ContainsKey(entryId))
			{
				return;
			}
			ItemSO itemSO = _itemDatabaseSO.FindById(entryId);
			if (itemSO == null)
			{
				if (TryResolveSushiRecipe(entryId, out var recipe) && recipe.IdentityKind == SushiRecipeIdentityKind.SavedCustom)
				{
					RestaurantMenuItem restaurantMenuItem = UnityEngine.Object.Instantiate(_restaurantMenuItemPrefab, _menuContainer);
					restaurantMenuItem.Initialize(WithCustomIcon(recipe), this);
					_menuItemUIDict[entryId] = restaurantMenuItem;
				}
			}
			else if (itemSO is ServedDishSO data)
			{
				if (!_menuList.Contains(itemSO))
				{
					_menuList.Add(itemSO);
				}
				RestaurantMenuItem restaurantMenuItem2 = UnityEngine.Object.Instantiate(_restaurantMenuItemPrefab, _menuContainer);
				restaurantMenuItem2.Initialize(data, this);
				_menuItemUIDict[entryId] = restaurantMenuItem2;
			}
		}

		private void HandleMenuEntryRemoved(string entryId)
		{
			if (string.IsNullOrEmpty(entryId))
			{
				return;
			}
			_menuRecipeIds.Remove(entryId);
			if (_menuItemUIDict.TryGetValue(entryId, out var value) && value != null)
			{
				UnityEngine.Object.Destroy(value.gameObject);
				_menuItemUIDict.Remove(entryId);
			}
			for (int num = _menuList.Count - 1; num >= 0; num--)
			{
				if (_menuList[num] != null && _menuList[num].Id == entryId)
				{
					_menuList.RemoveAt(num);
				}
			}
		}

		private void HandleMenuCleared()
		{
			foreach (KeyValuePair<string, RestaurantMenuItem> item in _menuItemUIDict)
			{
				if (item.Value != null)
				{
					UnityEngine.Object.Destroy(item.Value.gameObject);
				}
			}
			_menuItemUIDict.Clear();
			_menuList.Clear();
			_menuRecipeIds.Clear();
		}

		private void OnMenuSearchChanged(string searchText)
		{
			ApplyMenuSearchFilter();
		}

		private void OnMenuSearchSelected(string _)
		{
			SetSearchInputGameplayLock(shouldLock: true);
		}

		private void OnMenuSearchDeselected(string _)
		{
			SetSearchInputGameplayLock(shouldLock: false);
		}

		private void SetSearchInputGameplayLock(bool shouldLock)
		{
			if (_searchInputOwnsGameplayLock == shouldLock)
			{
				return;
			}
			InputHandler inputHandler = Singleton<InputManager>.Instance?.InputHandler;
			if (inputHandler == null)
			{
				if (!shouldLock)
				{
					_searchInputOwnsGameplayLock = false;
				}
			}
			else
			{
				inputHandler.SetGameplayInputSuppressed(GetInstanceID(), shouldLock);
				_searchInputOwnsGameplayLock = shouldLock;
			}
		}

		private void ApplyMenuSearchFilter()
		{
			string searchText = ((_menuSearchInput != null) ? _menuSearchInput.text : string.Empty);
			ApplySearchToContainer(_sushiContainer, searchText);
			ApplySearchToContainer(_beverageContainer, searchText);
			ApplySearchToContainer(_sideDishContainer, searchText);
		}

		private static void ApplySearchToContainer(Transform container, string searchText)
		{
			if (container == null)
			{
				return;
			}
			for (int i = 0; i < container.childCount; i++)
			{
				if (container.GetChild(i).TryGetComponent<FoodItem>(out var component))
				{
					component.gameObject.SetActive(component.MatchesSearch(searchText));
				}
			}
		}

		public void SetMenuItems(List<string> itemIds)
		{
			if (itemIds == null || itemIds.Count == 0)
			{
				HandleMenuCleared();
				RefreshFoodItemsPreservingSelection();
				return;
			}
			HashSet<string> hashSet = new HashSet<string>(itemIds.Count);
			for (int i = 0; i < itemIds.Count; i++)
			{
				string text = itemIds[i];
				if (!string.IsNullOrEmpty(text))
				{
					hashSet.Add(text);
				}
			}
			_menuRecipeIds.Clear();
			foreach (string item in hashSet)
			{
				_menuRecipeIds.Add(item);
			}
			if (hashSet.Count == 0)
			{
				HandleMenuCleared();
				RefreshFoodItemsPreservingSelection();
				return;
			}
			if (_menuItemUIDict.Count > 0)
			{
				List<string> list = null;
				foreach (KeyValuePair<string, RestaurantMenuItem> item2 in _menuItemUIDict)
				{
					if (!hashSet.Contains(item2.Key))
					{
						if (list == null)
						{
							list = new List<string>();
						}
						list.Add(item2.Key);
					}
				}
				if (list != null)
				{
					for (int j = 0; j < list.Count; j++)
					{
						HandleMenuEntryRemoved(list[j]);
					}
				}
			}
			for (int k = 0; k < itemIds.Count; k++)
			{
				string text2 = itemIds[k];
				if (!string.IsNullOrEmpty(text2))
				{
					HandleMenuEntryAddedOrUpdated(text2);
				}
			}
			for (int num = _menuList.Count - 1; num >= 0; num--)
			{
				ItemSO itemSO = _menuList[num];
				if (itemSO == null || string.IsNullOrEmpty(itemSO.Id) || !hashSet.Contains(itemSO.Id))
				{
					_menuList.RemoveAt(num);
				}
			}
			foreach (string item3 in hashSet)
			{
				bool flag = false;
				for (int l = 0; l < _menuList.Count; l++)
				{
					if (_menuList[l] != null && _menuList[l].Id == item3)
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					ItemSO itemSO2 = _itemDatabaseSO.FindById(item3);
					if (itemSO2 != null)
					{
						_menuList.Add(itemSO2);
					}
				}
			}
			RefreshFoodItemsPreservingSelection();
		}

		private void RefreshFoodItemsPreservingSelection()
		{
			string text = ((_selectedFoodItem != null) ? _selectedFoodItem.RecipeId : string.Empty);
			_selectedFoodItem = null;
			RefreshAllFoodItems();
			RefreshTabButtonSelection();
			if (!(_activeContainer == null) && (string.IsNullOrEmpty(text) || !SelectItemByIdIn(_activeContainer, text)))
			{
				SelectFirstItemIn(_activeContainer);
			}
		}

		private bool SelectItemByIdIn(Transform container, string itemId)
		{
			if (container == null || string.IsNullOrEmpty(itemId))
			{
				return false;
			}
			int childCount = container.childCount;
			for (int i = 0; i < childCount; i++)
			{
				Transform child = container.GetChild(i);
				if (!(child == null) && child.TryGetComponent<FoodItem>(out var component) && !(component.RecipeId != itemId))
				{
					OnFoodItemClicked(component);
					return true;
				}
			}
			return false;
		}

		private void OnCustomRecipeCatalogChanged()
		{
			if (_initialized)
			{
				List<string> menuItems = _menuRecipeIds.ToList();
				GetAllMenuItems();
				SetMenuItems(menuItems);
			}
		}

		private bool TryResolveSushiRecipe(string recipeId, out SushiRecipeDefinition recipe)
		{
			return new SushiRecipeResolver(_itemDatabaseSO, _restaurantManager?.CustomSushiRecipeCatalog).TryResolveById(recipeId, out recipe);
		}

		private SushiRecipeDefinition WithCustomIcon(SushiRecipeDefinition recipe)
		{
			if (recipe == null || recipe.BuiltInSushi != null || recipe.Icon != null)
			{
				return recipe;
			}
			return new SushiRecipeDefinition(recipe.RecipeId, recipe.DisplayName, recipe.Composition, recipe.Price, ResolveCustomRecipeIcon(recipe.SushiType), recipe.IdentityKind, recipe.BuiltInSushi);
		}

		private Sprite ResolveCustomRecipeIcon(SushiType sushiType)
		{
			return sushiType switch
			{
				SushiType.Maki => _customMakiIcon, 
				SushiType.Uramaki => _customUramakiIcon, 
				SushiType.Temaki => _customTemakiIcon, 
				_ => null, 
			};
		}

		private void RefreshTabButtonSelection()
		{
			if (_sushiContainerButton != null)
			{
				_sushiContainerButton.SetSelected(_activeContainer == _sushiContainer);
			}
			if (_beverageContainerButton != null)
			{
				_beverageContainerButton.SetSelected(_activeContainer == _beverageContainer);
			}
			if (_sideDishContainerButton != null)
			{
				_sideDishContainerButton.SetSelected(_activeContainer == _sideDishContainer);
			}
		}

		public override void Show()
		{
			base.Show();
			RefreshOnOpen();
			QuestReferenceHub.Instance.QuestComplete(QuestId.OpenMenuApp);
			QuestReferenceHub.Instance.QuestComplete(QuestId.OpenMenuApp01);
		}
	}
}
