using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Core.Struct;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Tablet
{
	public class OrdersApp : BaseApp
	{
		[SerializeField]
		private OrderReceiptItem _orderReceiptItemPrefab;

		[SerializeField]
		private Transform _ordersContainer;

		[SerializeField]
		private GameObject _emptyObject;

		[SerializeField]
		private ItemDatabaseSO _itemDatabase;

		[SerializeField]
		private bool _showOrderReceivedFeedback = true;

		private readonly Dictionary<string, OrderReceiptItem> _receiptsByTable = new Dictionary<string, OrderReceiptItem>();

		private readonly Dictionary<int, string> _orderIdToTableKey = new Dictionary<int, string>();

		private readonly Dictionary<string, RestaurantTableState> _tableStatesByKey = new Dictionary<string, RestaurantTableState>();

		private readonly List<PhysicalStateController> _boundPhysicalStateControllers = new List<PhysicalStateController>();

		private EventBinding<RestaurantTableStateChangeEvent> _restaurantTableStateChangeBinding;

		private EventBinding<RestaurantTablePaymentChangedEvent> _restaurantTablePaymentChangedBinding;

		private void Awake()
		{
			_restaurantTableStateChangeBinding = new EventBinding<RestaurantTableStateChangeEvent>(OnRestaurantTableStateChanged);
			_restaurantTablePaymentChangedBinding = new EventBinding<RestaurantTablePaymentChangedEvent>(OnRestaurantTablePaymentChanged);
		}

		private void OnEnable()
		{
			if (_restaurantTableStateChangeBinding != null)
			{
				EventBus<RestaurantTableStateChangeEvent>.Register(_restaurantTableStateChangeBinding);
			}
			if (_restaurantTablePaymentChangedBinding != null)
			{
				EventBus<RestaurantTablePaymentChangedEvent>.Register(_restaurantTablePaymentChangedBinding);
			}
			EnsureTableReceipts();
			RefreshTableStateSnapshot();
		}

		private void OnDisable()
		{
			if (_restaurantTableStateChangeBinding != null)
			{
				EventBus<RestaurantTableStateChangeEvent>.Deregister(_restaurantTableStateChangeBinding);
			}
			if (_restaurantTablePaymentChangedBinding != null)
			{
				EventBus<RestaurantTablePaymentChangedEvent>.Deregister(_restaurantTablePaymentChangedBinding);
			}
			UnbindPhysicalStateControllers();
		}

		public override void Show()
		{
			base.Show();
			QuestReferenceHub.Instance.QuestComplete(QuestId.OpenTheOrdersApp);
			EnsureTableReceipts();
			RefreshTableStateSnapshot();
			UpdateEmptyState();
		}

		public void OnOrderEntryChanged(NetworkListEvent<CustomerOrderEntry> change)
		{
			EnsureTableReceipts();
			switch (change.Type)
			{
			case NetworkListEvent<CustomerOrderEntry>.EventType.Add:
			case NetworkListEvent<CustomerOrderEntry>.EventType.Insert:
				AddOrderUI(change.Value, showUpdateToast: true);
				break;
			case NetworkListEvent<CustomerOrderEntry>.EventType.Value:
				AddOrderUI(change.Value);
				break;
			case NetworkListEvent<CustomerOrderEntry>.EventType.RemoveAt:
				RemoveOrderUI(change.Value);
				break;
			case NetworkListEvent<CustomerOrderEntry>.EventType.Clear:
				ClearAllOrders();
				break;
			}
			RefreshTableStateSnapshot();
			UpdateEmptyState();
		}

		public void AddOrderUI(CustomerOrderEntry entry)
		{
			AddOrderUI(entry, showUpdateToast: false);
		}

		private void AddOrderUI(CustomerOrderEntry entry, bool showUpdateToast)
		{
			if (entry.OrderId < 0)
			{
				return;
			}
			string tableKey = GetTableKey(entry);
			if (!string.IsNullOrWhiteSpace(tableKey) && (!TryGetTable(tableKey, out var table) || !IsTableLocked(table)))
			{
				if (_orderIdToTableKey.TryGetValue(entry.OrderId, out var value) && value != tableKey)
				{
					RemoveOrderFromTable(entry.OrderId, value);
				}
				OrderReceiptItem orderReceiptItem = EnsureReceiptForTable(tableKey, GetKnownTableState(tableKey));
				ApplyKnownPaymentTotal(tableKey, orderReceiptItem);
				if (!HasActiveOrderForTable(tableKey) && !orderReceiptItem.IsEmpty)
				{
					orderReceiptItem.ClearOrders();
				}
				_orderIdToTableKey[entry.OrderId] = tableKey;
				orderReceiptItem.AddOrUpdateOrder(entry);
				if (showUpdateToast)
				{
					ShowOrderReceivedFeedback(tableKey, entry);
				}
			}
		}

		private void ShowOrderReceivedFeedback(string tableKey, CustomerOrderEntry entry)
		{
			if (!_showOrderReceivedFeedback || Singleton<NotifyManager>.Instance == null || _itemDatabase == null)
			{
				return;
			}
			for (int i = 0; i < entry.FoodIds.Length; i++)
			{
				string text = entry.FoodIds[i].ToString();
				if (_itemDatabase.FindById(text) is ServedDishSO servedDishSO)
				{
					Singleton<NotifyManager>.Instance.ShowOrderItem(tableKey, text, servedDishSO.LocalizedName, servedDishSO.Sprite);
					continue;
				}
				CustomSushiRecipeData customSushiRecipeData = Singleton<GameProgressManager>.Instance?.RestaurantManager?.CustomSushiRecipeCatalog.FindById(text);
				if (customSushiRecipeData != null)
				{
					Sprite itemIcon = Singleton<GameProgressManager>.Instance.RestaurantManager.ResolveCustomSushiPlaceholderIcon(customSushiRecipeData.SushiType);
					Singleton<NotifyManager>.Instance.ShowOrderItem(tableKey, text, customSushiRecipeData.DisplayName, itemIcon);
				}
			}
		}

		private bool HasActiveOrderForTable(string tableKey)
		{
			foreach (string value in _orderIdToTableKey.Values)
			{
				if (value == tableKey)
				{
					return true;
				}
			}
			return false;
		}

		private void RemoveOrderUI(CustomerOrderEntry entry)
		{
			if (entry.OrderId < 0)
			{
				return;
			}
			if (_orderIdToTableKey.TryGetValue(entry.OrderId, out var value))
			{
				RemoveOrderFromTable(entry.OrderId, value);
				return;
			}
			string tableKey = GetTableKey(entry);
			if (!string.IsNullOrWhiteSpace(tableKey))
			{
				RemoveOrderFromTable(entry.OrderId, tableKey);
			}
		}

		private void RemoveOrderFromTable(int orderId, string tableKey)
		{
			if (!_receiptsByTable.TryGetValue(tableKey, out var value))
			{
				_orderIdToTableKey.Remove(orderId);
				return;
			}
			value.RemoveOrder(orderId);
			_orderIdToTableKey.Remove(orderId);
		}

		private void ClearAllOrders()
		{
			foreach (KeyValuePair<string, OrderReceiptItem> item in _receiptsByTable)
			{
				if (item.Value != null)
				{
					item.Value.ClearOrders();
				}
			}
			_orderIdToTableKey.Clear();
		}

		public void SetOrders(List<CustomerOrderEntry> entries, bool IsServer)
		{
			EnsureTableReceipts();
			ClearAllOrders();
			if (entries == null)
			{
				RefreshTableStateSnapshot();
				UpdateEmptyState();
				return;
			}
			for (int i = 0; i < entries.Count; i++)
			{
				CustomerOrderEntry entry = entries[i];
				if (entry.OrderId >= 0)
				{
					AddOrderUI(entry);
				}
			}
			RefreshTableStateSnapshot();
			UpdateEmptyState();
		}

		private string GetTableKey(CustomerOrderEntry entry)
		{
			return entry.TableNo.ToString();
		}

		private void UpdateEmptyState()
		{
			bool flag = _receiptsByTable.Count > 0;
			if (_emptyObject != null)
			{
				_emptyObject.SetActive(!flag);
			}
		}

		private void EnsureTableReceipts()
		{
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (restaurantManager == null || restaurantManager.Tables == null)
			{
				return;
			}
			List<RestaurantTable> sortedTables = GetSortedTables(restaurantManager);
			for (int i = 0; i < sortedTables.Count; i++)
			{
				RestaurantTable restaurantTable = sortedTables[i];
				if (!(restaurantTable == null))
				{
					string text = restaurantTable.Id.ToString();
					_tableStatesByKey[text] = restaurantTable.CurrentState;
					BindPhysicalStateController(restaurantTable.PhysicalStateController);
					if (IsTableLocked(restaurantTable))
					{
						RemoveReceiptForTable(text);
						continue;
					}
					OrderReceiptItem orderReceiptItem = EnsureReceiptForTable(text, restaurantTable.CurrentState);
					orderReceiptItem.SetTable(restaurantTable);
					orderReceiptItem.SetPaymentTotal(restaurantTable.TotalPaymentFinal);
					orderReceiptItem.transform.SetSiblingIndex(i);
				}
			}
		}

		private void RefreshTableStateSnapshot()
		{
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (restaurantManager == null || restaurantManager.Tables == null)
			{
				return;
			}
			List<RestaurantTable> sortedTables = GetSortedTables(restaurantManager);
			for (int i = 0; i < sortedTables.Count; i++)
			{
				RestaurantTable restaurantTable = sortedTables[i];
				if (!(restaurantTable == null))
				{
					string text = restaurantTable.Id.ToString();
					_tableStatesByKey[text] = restaurantTable.CurrentState;
					if (IsTableLocked(restaurantTable))
					{
						RemoveReceiptForTable(text);
						continue;
					}
					OrderReceiptItem orderReceiptItem = EnsureReceiptForTable(text, restaurantTable.CurrentState);
					orderReceiptItem.SetTable(restaurantTable);
					orderReceiptItem.SetTableState(restaurantTable.CurrentState);
					orderReceiptItem.SetPaymentTotal(restaurantTable.TotalPaymentFinal);
					orderReceiptItem.transform.SetSiblingIndex(i);
				}
			}
		}

		private OrderReceiptItem EnsureReceiptForTable(string tableKey, RestaurantTableState state)
		{
			if (_receiptsByTable.TryGetValue(tableKey, out var value) && value != null)
			{
				return value;
			}
			value = Object.Instantiate(_orderReceiptItemPrefab, _ordersContainer);
			_receiptsByTable[tableKey] = value;
			value.InitializeTable(tableKey, state);
			return value;
		}

		private RestaurantTableState GetKnownTableState(string tableKey)
		{
			if (!_tableStatesByKey.TryGetValue(tableKey, out var value))
			{
				return RestaurantTableState.Empty;
			}
			return value;
		}

		private void OnRestaurantTableStateChanged(RestaurantTableStateChangeEvent @event)
		{
			string text = @event.TableId.ToString();
			_tableStatesByKey[text] = @event.NewState;
			if (TryGetTable(text, out var table) && IsTableLocked(table))
			{
				RemoveReceiptForTable(text);
				UpdateEmptyState();
				return;
			}
			OrderReceiptItem orderReceiptItem = EnsureReceiptForTable(text, @event.NewState);
			orderReceiptItem.SetTable(table);
			orderReceiptItem.SetTableState(@event.NewState);
			ApplyKnownPaymentTotal(text, orderReceiptItem);
			RefreshTableStateSnapshot();
			UpdateEmptyState();
		}

		private void OnRestaurantTablePaymentChanged(RestaurantTablePaymentChangedEvent @event)
		{
			string key = @event.TableId.ToString();
			if (_receiptsByTable.TryGetValue(key, out var value) && !(value == null))
			{
				value.SetPaymentTotal(@event.TotalPaymentFinal);
			}
		}

		private void ApplyKnownPaymentTotal(string tableKey, OrderReceiptItem receipt)
		{
			if (!(receipt == null) && TryGetTable(tableKey, out var table))
			{
				receipt.SetPaymentTotal(table.TotalPaymentFinal);
			}
		}

		private void RemoveReceiptForTable(string tableKey)
		{
			if (!_receiptsByTable.TryGetValue(tableKey, out var value))
			{
				return;
			}
			if (value != null)
			{
				Object.Destroy(value.gameObject);
			}
			_receiptsByTable.Remove(tableKey);
			List<int> list = new List<int>();
			foreach (KeyValuePair<int, string> item in _orderIdToTableKey)
			{
				if (item.Value == tableKey)
				{
					list.Add(item.Key);
				}
			}
			for (int i = 0; i < list.Count; i++)
			{
				_orderIdToTableKey.Remove(list[i]);
			}
		}

		private List<RestaurantTable> GetSortedTables(RestaurantManager restaurantManager)
		{
			if (restaurantManager == null || restaurantManager.Tables == null)
			{
				return new List<RestaurantTable>();
			}
			return (from table in restaurantManager.Tables
				where table != null
				orderby table.Id
				select table).ToList();
		}

		private bool TryGetTable(string tableKey, out RestaurantTable table)
		{
			table = null;
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (restaurantManager == null || restaurantManager.Tables == null)
			{
				return false;
			}
			for (int i = 0; i < restaurantManager.Tables.Count; i++)
			{
				RestaurantTable restaurantTable = restaurantManager.Tables[i];
				if (!(restaurantTable == null) && !(restaurantTable.Id.ToString() != tableKey))
				{
					table = restaurantTable;
					return true;
				}
			}
			return false;
		}

		private static bool IsTableLocked(RestaurantTable table)
		{
			if (table != null && table.PhysicalStateController != null)
			{
				return !table.PhysicalStateController.IsPhysicallyUsable;
			}
			return false;
		}

		private void BindPhysicalStateController(PhysicalStateController physicalStateController)
		{
			if (!(physicalStateController == null) && !_boundPhysicalStateControllers.Contains(physicalStateController))
			{
				physicalStateController.OnPhysicallyUsableChanged += OnTablePhysicalStateChanged;
				_boundPhysicalStateControllers.Add(physicalStateController);
			}
		}

		private void UnbindPhysicalStateControllers()
		{
			for (int i = 0; i < _boundPhysicalStateControllers.Count; i++)
			{
				PhysicalStateController physicalStateController = _boundPhysicalStateControllers[i];
				if (physicalStateController != null)
				{
					physicalStateController.OnPhysicallyUsableChanged -= OnTablePhysicalStateChanged;
				}
			}
			_boundPhysicalStateControllers.Clear();
		}

		private void OnTablePhysicalStateChanged(bool isPhysicallyUsable)
		{
			RefreshTableStateSnapshot();
			UpdateEmptyState();
		}
	}
}
