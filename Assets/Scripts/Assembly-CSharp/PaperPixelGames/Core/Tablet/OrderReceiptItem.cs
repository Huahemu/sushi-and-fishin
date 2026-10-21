using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Core.Struct;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Tablet
{
	public class OrderReceiptItem : MonoBehaviour
	{
		private struct FoodAggregate
		{
			public FoodView Food;

			public int OrderedCount;

			public int DeliveredCount;

			public bool HasServiceAttempt;

			public int RemainingCount => Mathf.Max(0, OrderedCount - DeliveredCount);
		}

		private readonly struct FoodView
		{
			public readonly string Name;

			public readonly Sprite Icon;

			public readonly int Price;

			public FoodView(string name, Sprite icon, int price)
			{
				Name = name ?? string.Empty;
				Icon = icon;
				Price = Mathf.Max(0, price);
			}
		}

		[Serializable]
		private class ClientOrderSlot
		{
			[SerializeField]
			private Transform _root;

			[SerializeField]
			private GameObject _background;

			[SerializeField]
			private GameObject _empty;

			[SerializeField]
			private Transform _itemContainer;

			public Transform ItemContainer => _itemContainer;

			public bool IsValid => _root != null;

			public void Reset()
			{
				if (_root != null)
				{
					_root.gameObject.SetActive(value: true);
				}
				if (_itemContainer != null)
				{
					_itemContainer.gameObject.SetActive(value: true);
				}
				SetContentState(hasItems: false);
			}

			public void SetContentState(bool hasItems)
			{
				if (_background != null)
				{
					_background.SetActive(hasItems);
				}
				if (_empty != null)
				{
					_empty.SetActive(!hasItems);
				}
			}
		}

		[Header("Header")]
		[SerializeField]
		private TextMeshProUGUI _tableNoText;

		[SerializeField]
		private TextMeshProUGUI _customerNameText;

		[SerializeField]
		private TextMeshProUGUI _waitressText;

		[SerializeField]
		private TextMeshProUGUI _totalPriceText;

		[Header("Side")]
		[SerializeField]
		private Image _orderSideIcon;

		[SerializeField]
		private List<Color> _sideColors = new List<Color>();

		[Header("Timer")]
		[SerializeField]
		private TextMeshProUGUI _remainingTimeText;

		[Header("Items")]
		[SerializeField]
		private ItemDatabaseSO _itemDatabase;

		[SerializeField]
		private CustomerOrderFoodItem _orderFoodItemPrefab;

		[SerializeField]
		private Transform _itemsContainer;

		[SerializeField]
		private List<ClientOrderSlot> _clientOrderSlots = new List<ClientOrderSlot>();

		private readonly Dictionary<int, CustomerOrderEntry> _ordersById = new Dictionary<int, CustomerOrderEntry>();

		private readonly List<CustomerOrderFoodItem> _spawnedItems = new List<CustomerOrderFoodItem>();

		private string _tableKey;

		private int _totalPrice;

		private int _tablePaymentTotal;

		private RestaurantTableState _currentState;

		private bool _isLocked;

		private RestaurantTable _table;

		private bool _hasTimer;

		private double _expireServerTime;

		private float _timerRefreshCooldown;

		private const float TimerRefreshInterval = 0.25f;

		public string TableKey => _tableKey;

		public bool IsEmpty => _ordersById.Count == 0;

		private void OnEnable()
		{
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
			RefreshWaitressName();
		}

		private void OnDisable()
		{
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
		}

		private void OnSelectedLocaleChanged(Locale locale)
		{
			RefreshWaitressName();
		}

		private void RefreshWaitressName()
		{
			if (!(_waitressText == null))
			{
				_waitressText.text = ((ShouldDisplayInfoTexts() && TryGetLatestOrder(out var latestEntry)) ? (" " + WaitressDisplayName(latestEntry)) : string.Empty);
			}
		}

		public static string WaitressDisplayName(CustomerOrderEntry entry)
		{
			if (!entry.IsStaffWaitress)
			{
				return entry.WaitressName.ToString();
			}
			return StaffNames.DisplayName(entry.WaitressName.ToString(), entry.StaffWaitressGender);
		}

		public void InitializeTable(string tableKey, RestaurantTableState state)
		{
			_tableKey = tableKey;
			ApplySideColor();
			SetTableState(state);
		}

		public void SetTable(RestaurantTable table)
		{
			_table = table;
			RefreshStatePresentation(default, hasLatestEntry: false);
		}

		public void InitializeTable(CustomerOrderEntry entry)
		{
			_tableKey = entry.TableNo.ToString();
			ApplySideColor();
			_ordersById[entry.OrderId] = entry;
			SetTableState(RestaurantTableState.DeliveringOrder);
		}

		public void SetTableState(RestaurantTableState state, bool isLocked = false)
		{
			RestaurantTableState currentState = _currentState;
			_currentState = state;
			_isLocked = isLocked;
			if (ShouldClearOrdersForState(state, isLocked) || ShouldClearOrdersForNewOrderCycle(currentState, state, isLocked))
			{
				_ordersById.Clear();
				if (ShouldClearOrdersForState(state, isLocked))
				{
					_tablePaymentTotal = 0;
				}
			}
			RefreshTableNoText(state, isLocked);
			RefreshStatePresentation(default, hasLatestEntry: false);
		}

		public void SetPaymentTotal(int totalPayment)
		{
			_tablePaymentTotal = Mathf.Max(0, totalPayment);
			if (ShouldDisplayInfoTexts() || ShouldDisplayOrderItems())
			{
				RefreshTotalPriceText();
			}
		}

		public void AddOrUpdateOrder(CustomerOrderEntry entry)
		{
			if (string.IsNullOrWhiteSpace(_tableKey))
			{
				_tableKey = entry.TableNo.ToString();
				ApplySideColor();
			}
			_ordersById[entry.OrderId] = entry;
			RefreshStatePresentation(entry, hasLatestEntry: true);
		}

		public void RemoveOrder(int orderId)
		{
			if (ShouldPreserveOrderSnapshot())
			{
				RefreshStatePresentation(default, hasLatestEntry: false);
			}
			else if (_ordersById.Remove(orderId))
			{
				RefreshStatePresentation(default, hasLatestEntry: false);
			}
		}

		public void ClearOrders()
		{
			_ordersById.Clear();
			RefreshStatePresentation(default, hasLatestEntry: false);
		}

		private void Update()
		{
			if (ShouldDisplayTimer())
			{
				_timerRefreshCooldown -= Time.deltaTime;
				if (!(_timerRefreshCooldown > 0f))
				{
					_timerRefreshCooldown = 0.25f;
					ResolveTimer();
					RefreshTimerText(force: false);
				}
			}
		}

		private void RefreshHeaderTexts(CustomerOrderEntry latestEntry, bool hasLatestEntry)
		{
			if (_customerNameText != null)
			{
				_customerNameText.text = ((_ordersById.Count == 0) ? TabletLocalization.Get("tablet.orders.no_customers", "No customers") : (((_ordersById.Count <= 1) & hasLatestEntry) ? latestEntry.CustomerName.ToString() : TabletLocalization.Format("tablet.orders.customer_count", "{0} Customers", _ordersById.Count)));
			}
			if (_waitressText != null)
			{
				_waitressText.text = (hasLatestEntry ? (" " + WaitressDisplayName(latestEntry)) : string.Empty);
			}
			RefreshTotalPriceText();
		}

		private void RefreshStatePresentation(CustomerOrderEntry latestEntry, bool hasLatestEntry)
		{
			if (!ShouldDisplayOrderItems())
			{
				ClearOrderVisuals();
			}
			if (!hasLatestEntry)
			{
				hasLatestEntry = TryGetLatestOrder(out latestEntry);
			}
			if (ShouldDisplayInfoTexts())
			{
				RefreshHeaderTexts(latestEntry, hasLatestEntry);
			}
			else
			{
				ResetInfoTexts();
			}
			if (ShouldDisplayOrderItems())
			{
				RebuildFoodItems();
			}
			if (ShouldDisplayTimer())
			{
				ResolveTimer();
				RefreshTimerText(force: true);
			}
			else
			{
				ResetTimerText();
			}
		}

		private bool ShouldDisplayOrderItems()
		{
			if (!_isLocked && (_currentState == RestaurantTableState.DeliveringOrder || _currentState == RestaurantTableState.Eating))
			{
				return _ordersById.Count > 0;
			}
			return false;
		}

		private bool ShouldDisplayInfoTexts()
		{
			if (!_isLocked && (_currentState == RestaurantTableState.DeliveringOrder || _currentState == RestaurantTableState.Eating || _currentState == RestaurantTableState.AwaitingPayment))
			{
				return _ordersById.Count > 0;
			}
			return false;
		}

		private bool ShouldDisplayTimer()
		{
			if (!_isLocked && (_currentState == RestaurantTableState.TakingOrder || _currentState == RestaurantTableState.DeliveringOrder))
			{
				return _table != null;
			}
			return false;
		}

		private bool ShouldPreserveOrderSnapshot()
		{
			if (!_isLocked)
			{
				if (_currentState != RestaurantTableState.TakingOrder && _currentState != RestaurantTableState.DeliveringOrder && _currentState != RestaurantTableState.Eating)
				{
					return _currentState == RestaurantTableState.AwaitingPayment;
				}
				return true;
			}
			return false;
		}

		private static bool ShouldClearOrdersForState(RestaurantTableState state, bool isLocked)
		{
			if (!isLocked && state != RestaurantTableState.Empty && state != RestaurantTableState.WaitingOtherCustomers)
			{
				return state == RestaurantTableState.Cleaning;
			}
			return true;
		}

		private static bool ShouldClearOrdersForNewOrderCycle(RestaurantTableState previousState, RestaurantTableState nextState, bool isLocked)
		{
			if (!isLocked && previousState == RestaurantTableState.Eating)
			{
				return nextState == RestaurantTableState.TakingOrder;
			}
			return false;
		}

		private bool TryGetLatestOrder(out CustomerOrderEntry latestEntry)
		{
			latestEntry = default;
			using (Dictionary<int, CustomerOrderEntry>.ValueCollection.Enumerator enumerator = _ordersById.Values.GetEnumerator())
			{
				if (enumerator.MoveNext())
				{
					CustomerOrderEntry current = enumerator.Current;
					latestEntry = current;
					return true;
				}
			}
			return false;
		}

		private void ResetInfoTexts()
		{
			if (_customerNameText != null)
			{
				_customerNameText.text = string.Empty;
			}
			if (_waitressText != null)
			{
				_waitressText.text = string.Empty;
			}
			if (_totalPriceText != null)
			{
				_totalPriceText.text = "—";
			}
		}

		private void ResetTimerText()
		{
			_hasTimer = false;
			_expireServerTime = 0.0;
			if (_remainingTimeText != null)
			{
				_remainingTimeText.text = "—";
			}
		}

		private void ClearOrderVisuals()
		{
			ClearSpawnedItems();
			if (_clientOrderSlots.Count > 0)
			{
				ResetClientOrderSlots();
			}
			_totalPrice = 0;
		}

		private void RefreshTableNoText(RestaurantTableState state, bool isLocked)
		{
			if (!(_tableNoText == null))
			{
				_tableNoText.text = TabletLocalization.Format("tablet.orders.table_status", "TABLE {0} | {1}", _tableKey, GetTableStatusText(state, isLocked));
			}
		}

		private static string GetTableStatusText(RestaurantTableState state, bool isLocked)
		{
			if (isLocked)
			{
				return TabletLocalization.Get("tablet.orders.status.locked", "Locked");
			}
			return state switch
			{
				RestaurantTableState.Empty => TabletLocalization.Get("tablet.orders.status.empty", "Empty"), 
				RestaurantTableState.WaitingOtherCustomers => TabletLocalization.Get("tablet.orders.status.waiting_customers", "Waiting Customers"), 
				RestaurantTableState.TakingOrder => TabletLocalization.Get("tablet.orders.status.request_order", "Request Order"), 
				RestaurantTableState.DeliveringOrder => TabletLocalization.Get("tablet.orders.status.preparing_order", "Preparing Order"), 
				RestaurantTableState.Eating => TabletLocalization.Get("tablet.orders.status.eating", "Eating"), 
				RestaurantTableState.AwaitingPayment => TabletLocalization.Get("tablet.orders.status.awaiting_payment", "Awaiting Payment"), 
				RestaurantTableState.Cleaning => TabletLocalization.Get("tablet.orders.status.needs_cleaning", "Needs Cleaning"), 
				_ => state.ToString(), 
			};
		}

		private void RebuildFoodItems()
		{
			ClearSpawnedItems();
			_totalPrice = 0;
			if (_clientOrderSlots.Count > 0)
			{
				ResetClientOrderSlots();
				RebuildFoodItemsByClientOrder();
				RefreshTotalPriceText();
				return;
			}
			Dictionary<string, FoodAggregate> dictionary = new Dictionary<string, FoodAggregate>();
			List<string> list = new List<string>();
			foreach (CustomerOrderEntry value4 in _ordersById.Values)
			{
				List<string> list2 = ((value4.GetStatus() == OrderReceiptStatus.Preparing) ? new List<string>() : null);
				for (int i = 0; i < value4.FoodIds.Length; i++)
				{
					string text = value4.FoodIds[i].ToString();
					if (!string.IsNullOrWhiteSpace(text) && TryFindFood(text, out var food))
					{
						if (!dictionary.TryGetValue(text, out var value))
						{
							value = new FoodAggregate
							{
								Food = food,
								OrderedCount = 0,
								DeliveredCount = 0,
								HasServiceAttempt = false
							};
							dictionary.Add(text, value);
							list.Add(text);
						}
						value.OrderedCount++;
						dictionary[text] = value;
						list2?.Add(text);
						_totalPrice += GetEffectivePrice(text, food.Price);
					}
				}
				for (int j = 0; j < value4.DeliveredFoodIds.Length; j++)
				{
					string text2 = value4.DeliveredFoodIds[j].ToString();
					if (!string.IsNullOrWhiteSpace(text2) && dictionary.TryGetValue(text2, out var value2))
					{
						value2.DeliveredCount++;
						dictionary[text2] = value2;
						list2?.Remove(text2);
					}
				}
				if (list2 == null)
				{
					continue;
				}
				foreach (string item in list2)
				{
					FoodAggregate value3 = dictionary[item];
					value3.HasServiceAttempt = true;
					dictionary[item] = value3;
				}
			}
			for (int k = 0; k < list.Count; k++)
			{
				string key = list[k];
				FoodAggregate foodAggregate = dictionary[key];
				CustomerOrderFoodItem customerOrderFoodItem = UnityEngine.Object.Instantiate(_orderFoodItemPrefab, _itemsContainer);
				customerOrderFoodItem.Initialize(foodAggregate.Food.Icon, foodAggregate.Food.Name, foodAggregate.RemainingCount, foodAggregate.HasServiceAttempt);
				_spawnedItems.Add(customerOrderFoodItem);
			}
			RefreshTotalPriceText();
		}

		private void RefreshTotalPriceText()
		{
			if (!(_totalPriceText == null))
			{
				int num = Mathf.Max(_totalPrice, _tablePaymentTotal);
				_totalPriceText.text = ((_clientOrderSlots.Count > 0) ? $"{num}" : TabletLocalization.Format("tablet.orders.total", "Total: ${0}", num));
			}
		}

		private void RebuildFoodItemsByClientOrder()
		{
			bool[] array = new bool[_clientOrderSlots.Count];
			foreach (CustomerOrderEntry value3 in _ordersById.Values)
			{
				ClientOrderSlot clientOrderSlot = GetClientOrderSlot(value3.ClientOrderIndex);
				Transform transform = clientOrderSlot?.ItemContainer;
				if (transform == null)
				{
					transform = _itemsContainer;
				}
				Dictionary<string, FoodAggregate> dictionary = new Dictionary<string, FoodAggregate>();
				List<string> list = new List<string>();
				for (int i = 0; i < value3.FoodIds.Length; i++)
				{
					string text = value3.FoodIds[i].ToString();
					if (!string.IsNullOrWhiteSpace(text) && TryFindFood(text, out var food))
					{
						if (!dictionary.TryGetValue(text, out var value))
						{
							value = new FoodAggregate
							{
								Food = food,
								OrderedCount = 0,
								DeliveredCount = 0
							};
							dictionary.Add(text, value);
							list.Add(text);
						}
						value.OrderedCount++;
						dictionary[text] = value;
						_totalPrice += GetEffectivePrice(text, food.Price);
					}
				}
				for (int j = 0; j < value3.DeliveredFoodIds.Length; j++)
				{
					string text2 = value3.DeliveredFoodIds[j].ToString();
					if (!string.IsNullOrWhiteSpace(text2) && dictionary.TryGetValue(text2, out var value2))
					{
						value2.DeliveredCount++;
						dictionary[text2] = value2;
					}
				}
				for (int k = 0; k < list.Count; k++)
				{
					string key = list[k];
					FoodAggregate foodAggregate = dictionary[key];
					CustomerOrderFoodItem customerOrderFoodItem = UnityEngine.Object.Instantiate(_orderFoodItemPrefab, transform);
					customerOrderFoodItem.Initialize(foodAggregate.Food.Icon, foodAggregate.Food.Name, foodAggregate.RemainingCount, value3.GetStatus() == OrderReceiptStatus.Preparing);
					_spawnedItems.Add(customerOrderFoodItem);
				}
				if (clientOrderSlot != null && clientOrderSlot.IsValid && list.Count > 0)
				{
					array[value3.ClientOrderIndex] = true;
				}
			}
			for (int l = 0; l < _clientOrderSlots.Count; l++)
			{
				_clientOrderSlots[l].SetContentState(array[l]);
			}
		}

		private ClientOrderSlot GetClientOrderSlot(int clientOrderIndex)
		{
			if (clientOrderIndex < 0 || clientOrderIndex >= _clientOrderSlots.Count)
			{
				return null;
			}
			return _clientOrderSlots[clientOrderIndex];
		}

		private void ResetClientOrderSlots()
		{
			for (int i = 0; i < _clientOrderSlots.Count; i++)
			{
				_clientOrderSlots[i].Reset();
			}
		}

		private ServedDishSO FindFood(string foodId)
		{
			if (_itemDatabase == null)
			{
				return null;
			}
			return _itemDatabase.FindById(foodId) as ServedDishSO;
		}

		private bool TryFindFood(string foodId, out FoodView food)
		{
			food = default;
			ServedDishSO servedDishSO = FindFood(foodId);
			if (servedDishSO != null)
			{
				food = new FoodView(servedDishSO.LocalizedName, servedDishSO.Sprite, servedDishSO.Price);
				return true;
			}
			RestaurantManager restaurantManager = Singleton<GameProgressManager>.Instance?.RestaurantManager;
			CustomSushiRecipeData customSushiRecipeData = restaurantManager?.CustomSushiRecipeCatalog.FindById(foodId);
			if (customSushiRecipeData == null)
			{
				return false;
			}
			food = new FoodView(customSushiRecipeData.DisplayName, restaurantManager.ResolveCustomSushiPlaceholderIcon(customSushiRecipeData.SushiType), customSushiRecipeData.Price);
			return true;
		}

		private static int GetEffectivePrice(string recipeId, int price)
		{
			if (!(TabletNetworkHandler.Instance != null))
			{
				return Mathf.Max(0, price);
			}
			return TabletNetworkHandler.Instance.GetEffectiveRecipePrice(recipeId, price);
		}

		private void ResolveTimer()
		{
			_expireServerTime = ((_table != null) ? _table.ActiveOrderExpireServerTime : 0.0);
			_hasTimer = _expireServerTime > 0.0;
		}

		private void RefreshTimerText(bool force)
		{
			if (!_hasTimer)
			{
				if (_remainingTimeText != null)
				{
					_remainingTimeText.text = "—";
				}
				return;
			}
			double num = _expireServerTime - GetNetworkTime();
			if (num <= 0.0)
			{
				if (_remainingTimeText != null)
				{
					_remainingTimeText.text = "00:00";
				}
				return;
			}
			int num2 = Mathf.FloorToInt((float)num / 60f);
			int num3 = Mathf.FloorToInt((float)num % 60f);
			if (_remainingTimeText != null)
			{
				_remainingTimeText.text = $"{num2:00}:{num3:00}";
			}
		}

		private double GetNetworkTime()
		{
			if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
			{
				return NetworkManager.Singleton.ServerTime.Time;
			}
			return Time.timeAsDouble;
		}

		private void ApplySideColor()
		{
			if (!(_orderSideIcon == null) && _sideColors != null && _sideColors.Count != 0)
			{
				int index = Mathf.Abs(_tableKey.GetHashCode()) % _sideColors.Count;
				_orderSideIcon.color = _sideColors[index];
			}
		}

		private void ClearSpawnedItems()
		{
			for (int i = 0; i < _spawnedItems.Count; i++)
			{
				if (_spawnedItems[i] != null)
				{
					UnityEngine.Object.Destroy(_spawnedItems[i].gameObject);
				}
			}
			_spawnedItems.Clear();
		}
	}
}
