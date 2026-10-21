using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace PaperPixelGames.Core.Tablet
{
	public class InventoryApp : BaseApp
	{
		[Header("References")]
		[SerializeField]
		private InventoryItemUI _itemPrefab;

		[SerializeField]
		private Transform _inventoryItemContainer;

		[SerializeField]
		private GameObject _emptyInventoryObject;

		[SerializeField]
		private ItemDatabaseSO _itemDatabase;

		[Header("Category")]
		[SerializeField]
		private string _fallbackCategoryLabel = "wholesale";

		[SerializeField]
		private List<InventoryCategoryButtonBinding> _categoryButtons = new List<InventoryCategoryButtonBinding>();

		[SerializeField]
		private string _debugItemId;

		[SerializeField]
		private int _debugAmount = 1;

		private readonly Dictionary<string, InventoryItemUI> _stockEntries = new Dictionary<string, InventoryItemUI>();

		private readonly Dictionary<string, string> _stockEntryCategories = new Dictionary<string, string>();

		private bool _isSubscribed;

		private bool _categoryButtonsInitialized;

		private string _currentCategory;

		public override void Initialize()
		{
			base.Initialize();
			if (!Application.isPlaying)
			{
				return;
			}
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
			if (TryGetRestaurantManager(out var restaurantManager))
			{
				if (_isSubscribed)
				{
					restaurantManager.StockChanged -= OnStockChanged;
				}
				InitCategoryButtons();
				SelectInitialCategory();
				BuildInitialList(restaurantManager.ItemsInStock);
				restaurantManager.StockChanged += OnStockChanged;
				_isSubscribed = true;
			}
		}

		private void OnDestroy()
		{
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			UnsubscribeFromStockEvents();
		}

		public override void Show()
		{
			base.Show();
			RefreshLocalizedContent();
		}

		private void OnSelectedLocaleChanged(Locale locale)
		{
			RefreshLocalizedContent();
		}

		private void RefreshLocalizedContent()
		{
			foreach (InventoryItemUI value in _stockEntries.Values)
			{
				if (value != null)
				{
					value.RefreshLocalizedText();
				}
			}
		}

		private void UnsubscribeFromStockEvents()
		{
			if (_isSubscribed)
			{
				if (TryGetRestaurantManager(out var restaurantManager))
				{
					restaurantManager.StockChanged -= OnStockChanged;
				}
				_isSubscribed = false;
			}
		}

		private bool TryGetRestaurantManager(out RestaurantManager restaurantManager)
		{
			restaurantManager = null;
			if (Singleton<GameProgressManager>.Instance == null)
			{
				UtilityMethods.Log("Inventory App: GameProgressManager.Instance is null.", LogType.Warning);
				return false;
			}
			restaurantManager = Singleton<GameProgressManager>.Instance.RestaurantManager;
			if (restaurantManager == null)
			{
				UtilityMethods.Log("Inventory App: RestaurantManager is null.", LogType.Warning);
				return false;
			}
			return true;
		}

		private void BuildInitialList(IReadOnlyDictionary<string, int> stockItems)
		{
			ClearList();
			if (stockItems == null)
			{
				UpdateEmptyState();
				return;
			}
			foreach (KeyValuePair<string, int> stockItem in stockItems)
			{
				CreateOrUpdateEntry(stockItem.Key, stockItem.Value);
			}
			UpdateEmptyState();
		}

		private void ClearList()
		{
			foreach (KeyValuePair<string, InventoryItemUI> stockEntry in _stockEntries)
			{
				if (!(stockEntry.Value == null))
				{
					DestroyEntryObject(stockEntry.Value.gameObject);
				}
			}
			_stockEntries.Clear();
			_stockEntryCategories.Clear();
		}

		private void OnStockChanged(string itemId, int amount)
		{
			CreateOrUpdateEntry(itemId, amount);
			UpdateEmptyState();
		}

		private void CreateOrUpdateEntry(string itemId, int amount)
		{
			if (string.IsNullOrWhiteSpace(itemId))
			{
				UtilityMethods.Log("Inventory App: Item id is null or empty.", LogType.Warning);
				return;
			}
			if (_itemDatabase == null)
			{
				UtilityMethods.Log("Inventory App: ItemDatabaseSO is missing.", LogType.Warning);
				return;
			}
			if (_itemPrefab == null)
			{
				UtilityMethods.Log("Inventory App: Item prefab is missing.", LogType.Warning);
				return;
			}
			if (_inventoryItemContainer == null)
			{
				UtilityMethods.Log("Inventory App: Inventory item container is missing.", LogType.Warning);
				return;
			}
			amount = Mathf.Max(0, amount);
			if (!_stockEntries.TryGetValue(itemId, out var value) || value == null)
			{
				ItemSO itemSO = _itemDatabase.FindById(itemId);
				if (itemSO == null)
				{
					UtilityMethods.Log("Inventory App: Could not find ItemSO for itemId: " + itemId, LogType.Warning);
					return;
				}
				value = UnityEngine.Object.Instantiate(_itemPrefab, _inventoryItemContainer);
				string text = ResolveCategoryLabel(itemSO);
				value.Initialize(itemSO, amount, text);
				_stockEntries[itemId] = value;
				_stockEntryCategories[itemId] = text;
				ApplyEntryVisibility(itemId, value);
			}
			else
			{
				value.UpdateAmount(amount);
				ApplyEntryVisibility(itemId, value);
			}
		}

		private void InitCategoryButtons()
		{
			if (_categoryButtonsInitialized)
			{
				return;
			}
			_categoryButtonsInitialized = true;
			if (_categoryButtons == null || _categoryButtons.Count == 0)
			{
				return;
			}
			foreach (InventoryCategoryButtonBinding categoryButton in _categoryButtons)
			{
				if (categoryButton != null && !(categoryButton.Button == null))
				{
					string capturedCategory = categoryButton.Category;
					categoryButton.Button.Clicked += () =>
					{
						SetCategory(capturedCategory);
					};
				}
			}
		}

		private void SelectInitialCategory()
		{
			if (_categoryButtons == null || _categoryButtons.Count == 0)
			{
				SetCategory(string.Empty);
				return;
			}
			foreach (InventoryCategoryButtonBinding categoryButton in _categoryButtons)
			{
				if (categoryButton != null)
				{
					SetCategory(categoryButton.Category);
					return;
				}
			}
			SetCategory(string.Empty);
		}

		public void SetCategory(string category)
		{
			_currentCategory = category ?? string.Empty;
			RefreshCategoryButtons();
			RefreshEntryVisibility();
			UpdateEmptyState();
		}

		private void RefreshCategoryButtons()
		{
			if (_categoryButtons == null)
			{
				return;
			}
			foreach (InventoryCategoryButtonBinding categoryButton in _categoryButtons)
			{
				if (categoryButton != null && !(categoryButton.Button == null))
				{
					categoryButton.Button.SetSelected(IsSelectedCategory(categoryButton.Category));
				}
			}
		}

		private void RefreshEntryVisibility()
		{
			foreach (KeyValuePair<string, InventoryItemUI> stockEntry in _stockEntries)
			{
				ApplyEntryVisibility(stockEntry.Key, stockEntry.Value);
			}
		}

		private void ApplyEntryVisibility(string itemId, InventoryItemUI entry)
		{
			if (!(entry == null))
			{
				entry.gameObject.SetActive(ShouldShowEntry(itemId));
			}
		}

		private bool ShouldShowEntry(string itemId)
		{
			if (string.IsNullOrWhiteSpace(_currentCategory))
			{
				return true;
			}
			if (_stockEntryCategories.TryGetValue(itemId, out var value))
			{
				return string.Equals(value, _currentCategory, StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}

		private bool IsSelectedCategory(string category)
		{
			return string.Equals(category ?? string.Empty, _currentCategory, StringComparison.OrdinalIgnoreCase);
		}

		private string ResolveCategoryLabel(ItemSO itemSo)
		{
			if (itemSo == null)
			{
				return _fallbackCategoryLabel;
			}
			return itemSo.ItemType switch
			{
				ItemType.Wholesale => "wholesale", 
				ItemType.RawIngredient => "ingredient", 
				ItemType.ProcessedIngredient => "processedingredient", 
				ItemType.ServedDish => "servedish", 
				ItemType.Creature => "creatures", 
				_ => _fallbackCategoryLabel, 
			};
		}

		private void UpdateEmptyState()
		{
			bool flag = HasAnyVisibleEntry();
			if (_inventoryItemContainer != null)
			{
				_inventoryItemContainer.gameObject.SetActive(flag);
			}
			if (_emptyInventoryObject != null)
			{
				_emptyInventoryObject.SetActive(!flag);
			}
		}

		private bool HasAnyVisibleEntry()
		{
			foreach (KeyValuePair<string, InventoryItemUI> stockEntry in _stockEntries)
			{
				if (stockEntry.Value != null && stockEntry.Value.gameObject.activeSelf)
				{
					return true;
				}
			}
			return false;
		}

		private void DestroyEntryObject(GameObject target)
		{
			if (!(target == null))
			{
				if (Application.isPlaying)
				{
					UnityEngine.Object.Destroy(target);
				}
				else
				{
					UnityEngine.Object.DestroyImmediate(target);
				}
			}
		}
	}
}
