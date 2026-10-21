using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Economy;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Tablet
{
	public class ShopApp : BaseApp
	{
		[Header("Cart")]
		[SerializeField]
		private CartItem _cartItemPrefab;

		[SerializeField]
		private Transform _cartContainer;

		[SerializeField]
		private TextMeshProUGUI _cartWarningText;

		[SerializeField]
		private TextMeshProUGUI _cartTotalPriceText;

		[SerializeField]
		private BaseButton _purchaseButton;

		[SerializeField]
		private BaseButton _closeButton;

		[SerializeField]
		private List<ShopItem> _wholesaleItems = new List<ShopItem>();

		[SerializeField]
		private List<Transform> _spawnPoints;

		[Header("Filtering")]
		[SerializeField]
		private WholesaleShopCategory _defaultCategory;

		[SerializeField]
		private List<ShopCategoryButtonBinding> _categoryButtons = new List<ShopCategoryButtonBinding>();

		private WholesaleShopCategory _currentCategory;

		private ProgressionManager _boundProgressionManager;

		[Header("Editor Wholesale Item Generator")]
		[SerializeField]
		private ShopItem _shopItemPrefab;

		[SerializeField]
		private Transform _shopItemContainer;

		[SerializeField]
		private List<WholesaleSO> _wholesaleItemDatas = new List<WholesaleSO>();

		private int _currentSpawnIndex;

		private int _cartTotal;

		private bool _hasEnoughSlot = true;

		private bool _cancelRegistered;

		private string _cartWarningKey;

		private string _cartWarningFallback;

		private readonly Dictionary<WholesaleSO, CartItem> _cartItems = new Dictionary<WholesaleSO, CartItem>();

		public override void Initialize()
		{
			_purchaseButton.Clicked += OnPurchaseButtonClick;
			_closeButton.Clicked += OnCloseButtonCLick;
			_cartWarningText.gameObject.SetActive(value: false);
			InitItems();
			InitCategoryButtons();
			BindProgressionEvents();
			SetCategory(_defaultCategory);
			Hide();
		}

		private void OnDestroy()
		{
			if (_boundProgressionManager != null)
			{
				_boundProgressionManager.ProgressionChanged -= OnProgressionChanged;
			}
		}

		private void OnEnable()
		{
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
			RefreshLocalizedTexts();
		}

		private void OnDisable()
		{
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
		}

		public void RegisterEvents()
		{
			if (!_cancelRegistered)
			{
				InputHandler inputHandler = ((Singleton<InputManager>.Instance != null) ? Singleton<InputManager>.Instance.InputHandler : null);
				if (inputHandler == null)
				{
					UtilityMethods.Log("[ShopApp] Failed to register Cancel. InputHandler is null.", LogType.Warning);
					return;
				}
				inputHandler.Cancel += OnCancel;
				_cancelRegistered = true;
				UtilityMethods.Log("[ShopApp] Cancel registered.");
			}
		}

		public void UnRegisterEvents()
		{
			if (_cancelRegistered)
			{
				InputHandler inputHandler = ((Singleton<InputManager>.Instance != null) ? Singleton<InputManager>.Instance.InputHandler : null);
				if (inputHandler != null)
				{
					inputHandler.Cancel -= OnCancel;
				}
				_cancelRegistered = false;
				UtilityMethods.Log("[ShopApp] Cancel unregistered.");
			}
		}

		private void InitItems()
		{
			foreach (ShopItem wholesaleItem in _wholesaleItems)
			{
				wholesaleItem.Initialize(this);
			}
		}

		private void InitCategoryButtons()
		{
			if (_categoryButtons == null || _categoryButtons.Count == 0)
			{
				return;
			}
			foreach (ShopCategoryButtonBinding categoryButton in _categoryButtons)
			{
				if (categoryButton != null && !(categoryButton.Button == null))
				{
					WholesaleShopCategory capturedCategory = categoryButton.Category;
					categoryButton.Button.Clicked += () =>
					{
						SetCategory(capturedCategory);
					};
				}
			}
		}

		public void SetCategory(WholesaleShopCategory category)
		{
			_currentCategory = category;
			RefreshItemVisibility();
		}

		private void RefreshItemVisibility()
		{
			foreach (ShopItem wholesaleItem in _wholesaleItems)
			{
				if (!(wholesaleItem == null))
				{
					WholesaleSO data = wholesaleItem.Data;
					bool flag = ShouldShowItem(data);
					wholesaleItem.gameObject.SetActive(flag);
					if (flag)
					{
						wholesaleItem.RefreshProgressionState();
					}
				}
			}
			ApplyLevelDisplayOrder();
		}

		private void ApplyLevelDisplayOrder()
		{
			int num = 0;
			foreach (ShopItem item in from item in _wholesaleItems
				where item != null && ShouldShowItem(item.Data)
				orderby GetShopItemRequiredLevel(item.Data), item.Data.Name
				select item)
			{
				item.transform.SetSiblingIndex(num);
				num++;
			}
		}

		private int GetShopItemRequiredLevel(WholesaleSO item)
		{
			int num = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.GetRequiredWholesaleProgressionLevel(item) : (-1));
			if (num <= 0)
			{
				return int.MaxValue;
			}
			return num;
		}

		private bool ShouldShowItem(WholesaleSO data)
		{
			if (data == null)
			{
				return false;
			}
			if (_currentCategory == WholesaleShopCategory.None)
			{
				return true;
			}
			return data.ShopCategory == _currentCategory;
		}

		private bool ContainsLockedCartItem()
		{
			foreach (WholesaleSO key in _cartItems.Keys)
			{
				if (!IsShopItemUnlocked(key))
				{
					return true;
				}
			}
			return false;
		}

		public bool IsShopItemUnlocked(WholesaleSO item)
		{
			if (Singleton<GameProgressManager>.Instance == null)
			{
				return true;
			}
			return Singleton<GameProgressManager>.Instance.IsWholesaleProgressionUnlocked(item);
		}

		private void ShowCartWarning(string key, string fallback)
		{
			if (!(_cartWarningText == null))
			{
				_cartWarningKey = key;
				_cartWarningFallback = fallback;
				_cartWarningText.text = LocalizationDatabase.GetString("UiTable", key, fallback);
				_cartWarningText.gameObject.SetActive(value: true);
			}
		}

		private void HideCartWarning()
		{
			_cartWarningKey = string.Empty;
			_cartWarningFallback = string.Empty;
			if (_cartWarningText != null)
			{
				_cartWarningText.gameObject.SetActive(value: false);
			}
		}

		public void AddToCart(WholesaleSO item)
		{
			if (!IsShopItemUnlocked(item))
			{
				ShowCartWarning("ui.market.warning.required_level", "Reach the required restaurant level first.");
				return;
			}
			if (_cartItems.TryGetValue(item, out var value))
			{
				value.UpdateAmount();
			}
			else
			{
				CartItem cartItem = Object.Instantiate(_cartItemPrefab, _cartContainer);
				cartItem.Initialize(item, this);
				_cartItems.Add(item, cartItem);
			}
			UpdateCartTotal();
		}

		public void RemoveFromCart(CartItem cartItem)
		{
			if (_cartItems.ContainsKey(cartItem.Data))
			{
				cartItem.UpdateAmount(-1);
				if (cartItem.Amount <= 0)
				{
					Object.Destroy(cartItem.gameObject);
					_cartItems.Remove(cartItem.Data);
				}
				UpdateCartTotal();
			}
		}

		private void UpdateCartTotal()
		{
			_cartTotal = 0;
			foreach (KeyValuePair<WholesaleSO, CartItem> cartItem in _cartItems)
			{
				WholesaleSO key = cartItem.Key;
				CartItem value = cartItem.Value;
				if (!(key == null) && !(value == null))
				{
					int num = key.QuantityPrice * key.Quantity;
					_cartTotal += num * value.Amount;
				}
			}
			_cartTotalPriceText.text = $"${_cartTotal}";
			CheckHasInventorySlot();
			CheckHasEnoughCash();
		}

		private void CheckHasEnoughCash()
		{
			if (TryGetEconomyHandler(out var economyHandler))
			{
				if (!economyHandler.HasSufficientBalance(_cartTotal))
				{
					ShowCartWarning("ui.market.warning.insufficient_money", "Insufficient amount of money!");
				}
				else if (_hasEnoughSlot)
				{
					HideCartWarning();
				}
			}
		}

		private bool TryGetEconomyHandler(out EconomyHandler economyHandler)
		{
			economyHandler = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.EconomyHandler : null);
			if (economyHandler != null)
			{
				return true;
			}
			if (ServiceLocator.ForSceneOf(this).TryGet<EconomyHandler>(out economyHandler))
			{
				return economyHandler != null;
			}
			return false;
		}

		private void CheckHasInventorySlot()
		{
			int num = _cartItems.Values.Sum((CartItem cartItem) => cartItem.Amount);
			int num2 = BuildInventoryDeliveryPlan().Values.Sum();
			if (num > num2)
			{
				_hasEnoughSlot = false;
				ShowCartWarning("ui.market.warning.inventory_overflow", "Inventory won't fit everything. Some items will be placed on the desk!");
			}
			else
			{
				_hasEnoughSlot = true;
				HideCartWarning();
			}
		}

		private Dictionary<WholesaleSO, int> BuildInventoryDeliveryPlan()
		{
			Dictionary<WholesaleSO, int> dictionary = new Dictionary<WholesaleSO, int>();
			QuickSlotHandler instance = Singleton<QuickSlotHandler>.Instance;
			if (instance == null)
			{
				return dictionary;
			}
			int num = Mathf.Max(0, instance.AvailableSlotCount);
			foreach (KeyValuePair<WholesaleSO, CartItem> cartItem in _cartItems)
			{
				WholesaleSO key = cartItem.Key;
				CartItem value = cartItem.Value;
				if (!(key == null) && !(value == null) && value.Amount > 0)
				{
					int amount = value.Amount;
					int availableStackCapacity = instance.GetAvailableStackCapacity(key);
					int num2 = Mathf.Min(amount, availableStackCapacity);
					int num3 = amount - num2;
					int num4 = Mathf.Max(1, key.MaxStack);
					int b = Mathf.CeilToInt((float)num3 / (float)num4);
					int num5 = Mathf.Min(num, b);
					int num6 = Mathf.Min(num3, num5 * num4);
					dictionary[key] = num2 + num6;
					num -= num5;
				}
			}
			return dictionary;
		}

		private void OnPurchaseButtonClick()
		{
			if (!TryGetEconomyHandler(out var economyHandler))
			{
				ShowCartWarning("ui.market.warning.economy_not_ready", "Shop economy is not ready yet.");
				UtilityMethods.Log("[ShopApp] Purchase rejected. EconomyHandler is not ready.", LogType.Warning);
			}
			else if (ContainsLockedCartItem())
			{
				ShowCartWarning("ui.market.warning.required_level", "Reach the required restaurant level first.");
			}
			else if (economyHandler.HasSufficientBalance(_cartTotal))
			{
				economyHandler.SpendMoney(_cartTotal);
				Dictionary<WholesaleSO, int> dictionary = BuildInventoryDeliveryPlan();
				foreach (KeyValuePair<WholesaleSO, CartItem> cartItem in _cartItems)
				{
					WholesaleSO key = cartItem.Key;
					int amount = cartItem.Value.Amount;
					int num = (dictionary.TryGetValue(key, out var value) ? value : 0);
					for (int i = 0; i < amount; i++)
					{
						UtilityMethods.Log("Spawning an object: " + key.Name);
						Vector3 nextSpawnPoint = GetNextSpawnPoint();
						if (num > 0 && Singleton<QuickSlotHandler>.Instance.RequestShopInventoryDelivery(key, nextSpawnPoint))
						{
							num--;
							continue;
						}
						EventBus<SpawnWholesalerItemEvent>.Raise(new SpawnWholesalerItemEvent
						{
							ItemId = key.Id,
							Position = nextSpawnPoint
						});
					}
					TryRaiseIngredientQuest(key);
					if (IsSideDish(key))
					{
						QuestReferenceHub.Instance.QuestComplete(QuestId.BuyASideDish);
					}
				}
				foreach (Transform item in _cartContainer)
				{
					Object.Destroy(item.gameObject);
				}
				_cartItems.Clear();
				HideCartWarning();
				UpdateCartTotal();
				Close(completedPurchase: true);
			}
			else
			{
				ShowCartWarning("ui.market.warning.insufficient_money", "Insufficient amount of money!");
			}
		}

		private void OnCloseButtonCLick()
		{
			Close();
		}

		private Vector3 GetNextSpawnPoint()
		{
			if (_spawnPoints == null || _spawnPoints.Count == 0)
			{
				UtilityMethods.Log("Spawn points list is empty!");
				return Vector3.zero;
			}
			Vector3 position = _spawnPoints[_currentSpawnIndex].position;
			_currentSpawnIndex = (_currentSpawnIndex + 1) % _spawnPoints.Count;
			return position;
		}

		private void OnCancel(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && base.gameObject.activeInHierarchy)
			{
				Close();
			}
		}

		private void Close(bool completedPurchase = false)
		{
			if (base.gameObject.activeInHierarchy)
			{
				Hide();
				PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
				if (pPG_Player != null)
				{
					pPG_Player.SwitchToPlayer();
					pPG_Player.ShowCursor();
				}
				if (Singleton<GameUIManager>.Instance != null)
				{
					Singleton<GameUIManager>.Instance.IsShopUIActive = false;
					Singleton<GameUIManager>.Instance.DialoguePanelController?.NotifyShopClosedAfterPurchase(completedPurchase);
				}
			}
		}

		public override void Show()
		{
			if (!base.gameObject.activeInHierarchy)
			{
				base.gameObject.SetActive(value: true);
				BindProgressionEvents();
				if (Singleton<GameUIManager>.Instance != null)
				{
					Singleton<GameUIManager>.Instance.IsShopUIActive = true;
					Singleton<GameUIManager>.Instance.TimeUI?.SetVisibility(status: false);
				}
				RefreshItemVisibility();
				RefreshLocalizedTexts();
				RegisterEvents();
			}
		}

		private void BindProgressionEvents()
		{
			ProgressionManager progressionManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.ProgressionManager : null);
			if (!(_boundProgressionManager == progressionManager))
			{
				if (_boundProgressionManager != null)
				{
					_boundProgressionManager.ProgressionChanged -= OnProgressionChanged;
				}
				_boundProgressionManager = progressionManager;
				if (_boundProgressionManager != null)
				{
					_boundProgressionManager.ProgressionChanged += OnProgressionChanged;
				}
			}
		}

		private void OnProgressionChanged(int level, double currentXp, double nextLevelRequiredTotalXp)
		{
			RefreshItemVisibility();
			UpdateCartTotal();
		}

		private void OnSelectedLocaleChanged(Locale locale)
		{
			RefreshLocalizedTexts();
		}

		private void RefreshLocalizedTexts()
		{
			foreach (ShopItem wholesaleItem in _wholesaleItems)
			{
				wholesaleItem?.RefreshLocalizedText();
			}
			foreach (CartItem value in _cartItems.Values)
			{
				value?.RefreshLocalizedText();
			}
			if (_cartWarningText != null && _cartWarningText.gameObject.activeSelf && !string.IsNullOrWhiteSpace(_cartWarningKey))
			{
				_cartWarningText.text = LocalizationDatabase.GetString("UiTable", _cartWarningKey, _cartWarningFallback);
			}
		}

		public override void Hide()
		{
			UnRegisterEvents();
			if (_cartWarningText != null)
			{
				HideCartWarning();
			}
			if (base.gameObject.activeSelf)
			{
				base.gameObject.SetActive(value: false);
			}
			if (Singleton<GameUIManager>.Instance != null)
			{
				Singleton<GameUIManager>.Instance.TimeUI?.SetVisibility(status: true);
			}
		}

		private bool IsSideDish(WholesaleSO itemSO)
		{
			if (itemSO != null)
			{
				return itemSO.ShopCategory == WholesaleShopCategory.SideDish;
			}
			return false;
		}

		private void TryRaiseIngredientQuest(WholesaleSO wholesaleSO)
		{
			if (wholesaleSO == null)
			{
				return;
			}
			ItemDatabaseSO itemDatabaseSO = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.ItemDatabaseSO : null);
			if (!(itemDatabaseSO == null))
			{
				if (wholesaleSO == itemDatabaseSO.WholesaleCucumber)
				{
					QuestReferenceHub.Instance.QuestComplete(QuestId.BuyCucumber);
				}
				else if (wholesaleSO == itemDatabaseSO.WholesaleNori)
				{
					QuestReferenceHub.Instance.QuestComplete(QuestId.BuyNori);
				}
				else if (wholesaleSO == itemDatabaseSO.WholesaleRice)
				{
					QuestReferenceHub.Instance.QuestComplete(QuestId.BuyRice);
				}
			}
		}
	}
}
