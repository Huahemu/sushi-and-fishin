using System;
using System.Collections.Generic;
using DistantLands.Cozy;
using PaperPixelGames.Core.DailyReport;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Economy;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Core.Struct;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using Steamworks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Tablet
{
	public class TabletNetworkHandler : NetworkBehaviour, ISaveable<TabletData>
	{
		private const byte DailySpecialBonusStep = 5;

		private const byte DailySpecialBonusMin = 5;

		private const byte DailySpecialBonusMax = 25;

		[SerializeField]
		private ItemDatabaseSO _itemDatabase;

		[Header("Customer Order Chances")]
		[SerializeField]
		[Range(0f, 0.99f)]
		private float _sideDishBaseOrderChance = 0.3f;

		[SerializeField]
		[Range(0f, 0.99f)]
		private float _beverageBaseOrderChance = 0.5f;

		[SerializeField]
		[Range(0f, 0.5f)]
		private float _additionalOptionalItemChance = 0.1f;

		[SerializeField]
		[Range(0f, 0.99f)]
		private float _maxOptionalItemOrderChance = 0.8f;

		[SerializeField]
		[Range(0f, 0.99f)]
		private float _serveSideDishQuestOptionalOrderChance = 0.9f;

		[SerializeField]
		private float _orderDurationSeconds = 300f;

		[Header("Daily Dish Specials")]
		[SerializeField]
		[Range(0f, 1f)]
		private float _dailySideDishSpecialChance = 0.15f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _dailyBeverageSpecialChance = 0.15f;

		private int _nextOrderId;

		private bool _bindingsDone;

		private bool _menuDirty;

		private bool _fishDirty;

		private bool _ordersDirty;

		private bool _habitatDirty;

		private bool _uiReady;

		private bool _dailySpecialsDirty;

		private bool _dailySpecialsBound;

		private readonly NetworkVariable<int> _dailySushiSpecialDay = new NetworkVariable<int>(0);

		private readonly NetworkVariable<bool> _dailySushiSpecialFeatureActivated = new NetworkVariable<bool>(value: false);

		private EventBinding<LocalPlayerReadyEvent> _localPlayerReadyBinding;

		private NetworkList<FixedString64Bytes> _menuItems = new NetworkList<FixedString64Bytes>();

		private NetworkList<FishBookEntry> _fishBookEntries = new NetworkList<FishBookEntry>();

		private NetworkList<FishPersonalBestEntry> _fishPersonalBestEntries = new NetworkList<FishPersonalBestEntry>();

		private NetworkList<CustomerOrderEntry> _customerOrderEntries = new NetworkList<CustomerOrderEntry>();

		private NetworkList<byte> _discoveredSeaLayerIndices = new NetworkList<byte>();

		private NetworkList<DailySushiSpecialEntry> _dailySushiSpecials = new NetworkList<DailySushiSpecialEntry>();

		public static TabletNetworkHandler Instance { get; private set; }

		public bool HasDailySushiSpecials
		{
			get
			{
				if (_dailySushiSpecials != null)
				{
					return _dailySushiSpecials.Count > 0;
				}
				return false;
			}
		}

		public bool HasSushiOnMenu
		{
			get
			{
				if (_menuItems == null || _itemDatabase == null)
				{
					return false;
				}
				for (int i = 0; i < _menuItems.Count; i++)
				{
					string text = _menuItems[i].ToString();
					if (_itemDatabase.FindById(text) is ServedDishSO { ServedDishType: ServedDishType.Sushi })
					{
						return true;
					}
					if (TryResolveRecipe(text, out var recipe) && recipe.IdentityKind == SushiRecipeIdentityKind.SavedCustom)
					{
						return true;
					}
				}
				return false;
			}
		}

		public event Action FishBookChanged;

		public IEnumerable<string> ServerStaffMenuIds()
		{
			if (base.IsServer)
			{
				for (int i = 0; i < _menuItems.Count; i++)
				{
					yield return _menuItems[i].ToString();
				}
			}
		}

		public IEnumerable<CustomerOrderEntry> ServerStaffOrders()
		{
			if (base.IsServer)
			{
				for (int i = 0; i < _customerOrderEntries.Count; i++)
				{
					yield return _customerOrderEntries[i];
				}
			}
		}

		private void Awake()
		{
			Instance = this;
			ServiceLocator.ForSceneOf(this).Register(typeof(TabletNetworkHandler), this);
			_localPlayerReadyBinding = new EventBinding<LocalPlayerReadyEvent>(OnLocalPlayerReadyBinding);
			EventBus<LocalPlayerReadyEvent>.Register(_localPlayerReadyBinding);
		}

		public override void OnDestroy()
		{
			if (Instance == this)
			{
				Instance = null;
			}
			EventBus<LocalPlayerReadyEvent>.Deregister(_localPlayerReadyBinding);
			base.OnDestroy();
		}

		private void OnLocalPlayerReadyBinding(LocalPlayerReadyEvent @event)
		{
			Bindings();
			TryMarkUiReadyFromLocalTablet();
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			Bindings();
			if (base.IsServer)
			{
				ServerLoadFromCurrentSaveSlot();
				RefreshDailySushiSpecialFeatureState();
			}
			BindDailySpecialEvents();
			RefreshRestaurantMenuAvailabilityAlert();
			TryMarkUiReadyFromLocalTablet();
		}

		public void NotifyLocalTabletUiReady()
		{
			Bindings();
			MarkLocalTabletUiReadyAndSynchronize();
		}

		private void ServerLoadFromCurrentSaveSlot()
		{
			if (base.IsServer)
			{
				SaveSlotData data;
				if (Singleton<SaveManager>.Instance == null)
				{
					UtilityMethods.Log("[TabletNetworkHandler] Load skipped. SaveManager is null.", LogType.Warning);
				}
				else if (!Singleton<SaveManager>.Instance.TryGetCurrentSaveSlot(out data))
				{
					UtilityMethods.Log("[TabletNetworkHandler] Load skipped. Current save slot is invalid.", LogType.Warning);
				}
				else if (data.TabletData == null)
				{
					UtilityMethods.Log("[TabletNetworkHandler] No TabletData found. Using default state.");
				}
				else
				{
					Load(data.TabletData);
				}
			}
		}

		private void Bindings()
		{
			if (!_bindingsDone)
			{
				_bindingsDone = true;
				UtilityMethods.Log("[TabletNetworkHandler] Bindings");
				_fishBookEntries.OnListChanged += OnFishBookListChanged;
				_fishPersonalBestEntries.OnListChanged += OnFishPersonalBestListChanged;
				_menuItems.OnListChanged += OnMenuItemsListChanged;
				_customerOrderEntries.OnListChanged += OnOrderListChanged;
				_discoveredSeaLayerIndices.OnListChanged += OnHabitatDiscoveryListChanged;
				_dailySushiSpecials.OnListChanged += OnDailySushiSpecialsListChanged;
			}
		}

		private bool CanUseLocalTabletUi()
		{
			if (Singleton<PlayersManager>.Instance != null && Singleton<PlayersManager>.Instance.LocalPlayer != null && Singleton<PlayersManager>.Instance.LocalPlayer.TabletController != null)
			{
				return Singleton<PlayersManager>.Instance.LocalPlayer.TabletController.IsInitialized;
			}
			return false;
		}

		private void TryMarkUiReadyFromLocalTablet()
		{
			if (CanUseLocalTabletUi())
			{
				MarkLocalTabletUiReadyAndSynchronize();
			}
		}

		private void MarkLocalTabletUiReadyAndSynchronize()
		{
			if (!_uiReady)
			{
				_menuDirty = true;
			}
			_ordersDirty = true;
			_uiReady = true;
			FlushDirtyToUi(base.IsServer);
		}

		private void OnFishBookListChanged(NetworkListEvent<FishBookEntry> change)
		{
			FishBookChanged?.Invoke();
			if (!CanUseLocalTabletUi())
			{
				_fishDirty = true;
			}
			else
			{
				Singleton<PlayersManager>.Instance.LocalPlayer.TabletController.FishBookApp.OnFishBookEntryChanged(change);
			}
		}

		private void OnFishPersonalBestListChanged(NetworkListEvent<FishPersonalBestEntry> change)
		{
			if (CanUseLocalTabletUi())
			{
				Singleton<PlayersManager>.Instance.LocalPlayer.TabletController.FishBookApp.RefreshPersonalBest();
			}
		}

		private void OnMenuItemsListChanged(NetworkListEvent<FixedString64Bytes> change)
		{
			RefreshRestaurantMenuAvailabilityAlert();
			if (!CanUseLocalTabletUi())
			{
				_menuDirty = true;
			}
			else
			{
				Singleton<PlayersManager>.Instance.LocalPlayer.TabletController.MenuApp.OnMenuListChanged(change);
			}
		}

		private static void RefreshRestaurantMenuAvailabilityAlert()
		{
			Singleton<GameProgressManager>.Instance?.RestaurantManager?.RefreshLocalMenuAvailabilityAlert();
		}

		private void OnOrderListChanged(NetworkListEvent<CustomerOrderEntry> change)
		{
			if (!CanUseLocalTabletUi())
			{
				_ordersDirty = true;
			}
			else if (_ordersDirty)
			{
				RefreshOrdersFromSnapshot(base.IsServer);
				_ordersDirty = false;
			}
			else
			{
				Singleton<PlayersManager>.Instance.LocalPlayer.TabletController.OrdersApp.OnOrderEntryChanged(change);
			}
		}

		private void OnHabitatDiscoveryListChanged(NetworkListEvent<byte> change)
		{
			if (!CanUseLocalTabletUi())
			{
				_habitatDirty = true;
			}
			else
			{
				Singleton<PlayersManager>.Instance.LocalPlayer.TabletController.MapApplication.SetDiscoveredHabitats(GetDiscoveredHabitatSnapshot(), ((uint)change.Type == 0u || (uint)change.Type == 1u) ? new byte?(change.Value) : ((byte?)null));
			}
		}

		private void FlushDirtyToUi(bool isServer)
		{
			if (_uiReady && CanUseLocalTabletUi())
			{
				if (_menuDirty)
				{
					RefreshMenuFromSnapshot();
					_menuDirty = false;
				}
				if (_fishDirty)
				{
					RefreshFishBookFromSnapshot();
					_fishDirty = false;
				}
				if (_ordersDirty)
				{
					RefreshOrdersFromSnapshot(isServer);
					_ordersDirty = false;
				}
				if (_habitatDirty)
				{
					RefreshHabitatDiscoveryFromSnapshot();
					_habitatDirty = false;
				}
				if (_dailySpecialsDirty)
				{
					Singleton<PlayersManager>.Instance.LocalPlayer.TabletController.MenuApp.RefreshDailySushiSpecials();
					_dailySpecialsDirty = false;
				}
			}
		}

		private void RefreshMenuFromSnapshot()
		{
			MenuApp menuApp = Singleton<PlayersManager>.Instance.LocalPlayer.TabletController.MenuApp;
			List<string> list = new List<string>(_menuItems.Count);
			for (int i = 0; i < _menuItems.Count; i++)
			{
				list.Add(_menuItems[i].ToString());
			}
			menuApp.SetMenuItems(list);
		}

		private void RefreshFishBookFromSnapshot()
		{
			FishBookApp fishBookApp = Singleton<PlayersManager>.Instance.LocalPlayer.TabletController.FishBookApp;
			List<FishBookEntry> list = new List<FishBookEntry>(_fishBookEntries.Count);
			for (int i = 0; i < _fishBookEntries.Count; i++)
			{
				list.Add(_fishBookEntries[i]);
			}
			fishBookApp.SetEntries(list);
		}

		public bool TryGetHeaviestFishBookCatch(out FishBookChampion champion)
		{
			return TryGetHeaviestFishBookCatch(out champion, null);
		}

		public bool TryGetHeaviestFishBookCatch(out FishBookChampion champion, Func<FixedString64Bytes, bool> eligibilityFilter)
		{
			champion = default;
			bool flag = false;
			if (_fishBookEntries == null)
			{
				return false;
			}
			for (int i = 0; i < _fishBookEntries.Count; i++)
			{
				FishBookEntry fishBookEntry = _fishBookEntries[i];
				if (fishBookEntry.IsUnlocked && !(fishBookEntry.Score1.BestWeightKg <= 0f) && !fishBookEntry.FishId.IsEmpty && (eligibilityFilter == null || eligibilityFilter(fishBookEntry.FishId)))
				{
					bool flag2 = !flag || fishBookEntry.Score1.BestWeightKg > champion.BestWeightKg;
					bool flag3 = flag && Mathf.Approximately(fishBookEntry.Score1.BestWeightKg, champion.BestWeightKg) && string.CompareOrdinal(fishBookEntry.FishId.ToString(), champion.FishId.ToString()) < 0;
					if (flag2 || flag3)
					{
						champion = new FishBookChampion(fishBookEntry.FishId, fishBookEntry.Score1.BestWeightKg, fishBookEntry.Score1.SteamId, fishBookEntry.Score1.PlayerName);
						flag = true;
					}
				}
			}
			return flag;
		}

		private void RefreshOrdersFromSnapshot(bool isServer)
		{
			OrdersApp ordersApp = Singleton<PlayersManager>.Instance.LocalPlayer.TabletController.OrdersApp;
			List<CustomerOrderEntry> list = new List<CustomerOrderEntry>(_customerOrderEntries.Count);
			for (int i = 0; i < _customerOrderEntries.Count; i++)
			{
				list.Add(_customerOrderEntries[i]);
			}
			ordersApp.SetOrders(list, isServer);
		}

		private void RefreshHabitatDiscoveryFromSnapshot()
		{
			Singleton<PlayersManager>.Instance.LocalPlayer.TabletController.MapApplication.SetDiscoveredHabitats(GetDiscoveredHabitatSnapshot(), null);
		}

		private List<byte> GetDiscoveredHabitatSnapshot()
		{
			List<byte> list = new List<byte>(_discoveredSeaLayerIndices.Count);
			for (int i = 0; i < _discoveredSeaLayerIndices.Count; i++)
			{
				list.Add(_discoveredSeaLayerIndices[i]);
			}
			return list;
		}

		public override void OnNetworkDespawn()
		{
			UnbindDailySpecialEvents();
			base.OnNetworkDespawn();
			if (_fishBookEntries != null)
			{
				_fishBookEntries.OnListChanged -= OnFishBookListChanged;
			}
			if (_fishPersonalBestEntries != null)
			{
				_fishPersonalBestEntries.OnListChanged -= OnFishPersonalBestListChanged;
			}
			if (_customerOrderEntries != null)
			{
				_customerOrderEntries.OnListChanged -= OnOrderListChanged;
			}
			if (_menuItems != null)
			{
				_menuItems.OnListChanged -= OnMenuItemsListChanged;
			}
			if (_discoveredSeaLayerIndices != null)
			{
				_discoveredSeaLayerIndices.OnListChanged -= OnHabitatDiscoveryListChanged;
			}
			if (_dailySushiSpecials != null)
			{
				_dailySushiSpecials.OnListChanged -= OnDailySushiSpecialsListChanged;
			}
			_bindingsDone = false;
			_uiReady = false;
			_ordersDirty = true;
		}

		private void BindDailySpecialEvents()
		{
			if (!_dailySpecialsBound)
			{
				CozyWeather.Events.onNewDay += OnNewDay;
				_dailySpecialsBound = true;
			}
		}

		private void UnbindDailySpecialEvents()
		{
			if (_dailySpecialsBound)
			{
				CozyWeather.Events.onNewDay -= OnNewDay;
				_dailySpecialsBound = false;
			}
		}

		private void OnNewDay()
		{
			RefreshDailySushiSpecialFeatureState();
		}

		private void RefreshDailySushiSpecialFeatureState()
		{
			if (base.IsServer && IsDailySushiSpecialFeatureUnlocked())
			{
				_dailySushiSpecialFeatureActivated.Value = true;
				EnsureDailySushiSpecialsForCurrentDay();
			}
		}

		private void OnDailySushiSpecialsListChanged(NetworkListEvent<DailySushiSpecialEntry> change)
		{
			if (!CanUseLocalTabletUi())
			{
				_dailySpecialsDirty = true;
				_ordersDirty = true;
			}
			else
			{
				Singleton<PlayersManager>.Instance.LocalPlayer.TabletController.MenuApp.RefreshDailySushiSpecials();
				RefreshOrdersFromSnapshot(base.IsServer);
			}
		}

		public int GetEffectiveServedDishPrice(ServedDishSO dish)
		{
			if (dish == null)
			{
				return 0;
			}
			return GetEffectiveRecipePrice(dish.Id, dish.Price);
		}

		public int GetEffectiveRecipePrice(string recipeId, int snapshotPrice)
		{
			int num = Mathf.Max(0, snapshotPrice);
			if (!TryGetDailyDishBonus(recipeId, out var bonusAmount))
			{
				return num;
			}
			return num + bonusAmount;
		}

		public bool TryGetDailyDishBonus(string dishId, out byte bonusAmount)
		{
			bonusAmount = 0;
			if (!_dailySushiSpecialFeatureActivated.Value || string.IsNullOrWhiteSpace(dishId) || _dailySushiSpecials == null)
			{
				return false;
			}
			for (int i = 0; i < _dailySushiSpecials.Count; i++)
			{
				DailySushiSpecialEntry dailySushiSpecialEntry = _dailySushiSpecials[i];
				if (dailySushiSpecialEntry.SushiId.Equals(dishId) && IsValidDailySpecialBonus(dailySushiSpecialEntry.BonusPercentage))
				{
					bonusAmount = dailySushiSpecialEntry.BonusPercentage;
					return true;
				}
			}
			return false;
		}

		private void EnsureDailySushiSpecialsForCurrentDay()
		{
			if (!base.IsServer)
			{
				return;
			}
			if (!_dailySushiSpecialFeatureActivated.Value || !IsDailySushiSpecialFeatureUnlocked())
			{
				_dailySushiSpecialDay.Value = 0;
				_dailySushiSpecials.Clear();
				return;
			}
			int num = Mathf.Max(1, (!(CozyWeather.instance != null) || !(CozyWeather.instance.timeModule != null)) ? 1 : CozyWeather.instance.timeModule.currentDay);
			if (_dailySushiSpecialDay.Value == num && _dailySushiSpecials.Count > 0)
			{
				return;
			}
			_dailySushiSpecialDay.Value = num;
			_dailySushiSpecials.Clear();
			if (_itemDatabase == null)
			{
				return;
			}
			List<string> list = new List<string>();
			List<ServedDishSO> list2 = new List<ServedDishSO>();
			List<ServedDishSO> list3 = new List<ServedDishSO>();
			foreach (ItemSO item in _itemDatabase.GetAllOfType(ItemType.ServedDish))
			{
				if (item is ServedDishSO servedDishSO && !string.IsNullOrWhiteSpace(servedDishSO.Id) && (!(Singleton<GameProgressManager>.Instance != null) || Singleton<GameProgressManager>.Instance.IsServedDishProgressionUnlocked(servedDishSO)))
				{
					switch (servedDishSO.ServedDishType)
					{
					case ServedDishType.Sushi:
						list.Add(servedDishSO.Id);
						break;
					case ServedDishType.SideDish:
						list2.Add(servedDishSO);
						break;
					case ServedDishType.Beverage:
						list3.Add(servedDishSO);
						break;
					}
				}
			}
			IReadOnlyList<CustomSushiRecipeData> readOnlyList = Singleton<GameProgressManager>.Instance?.RestaurantManager?.CustomSushiRecipeCatalog.Recipes;
			if (readOnlyList != null)
			{
				foreach (CustomSushiRecipeData item2 in readOnlyList)
				{
					if (item2 != null && !string.IsNullOrWhiteSpace(item2.RecipeId))
					{
						list.Add(item2.RecipeId);
					}
				}
			}
			if (list.Count != 0)
			{
				System.Random random = new System.Random();
				int num2 = random.Next(1, Mathf.Min(2, list.Count) + 1);
				for (int i = 0; i < num2; i++)
				{
					int num3 = random.Next(i, list.Count);
					int index = i;
					List<string> list4 = list;
					int index2 = num3;
					string text = list[num3];
					string text2 = list[i];
					string text3 = (list[index] = text);
					text3 = (list4[index2] = text2);
					AddDailySpecial(list[i], random);
				}
				bool flag = list2.Count > 0 && random.NextDouble() < (double)_dailySideDishSpecialChance;
				bool flag2 = list3.Count > 0 && random.NextDouble() < (double)_dailyBeverageSpecialChance;
				if (flag & flag2)
				{
					AddDailySpecial((random.Next(0, 2) == 0) ? list2 : list3, random);
				}
				else if (flag)
				{
					AddDailySpecial(list2, random);
				}
				else if (flag2)
				{
					AddDailySpecial(list3, random);
				}
			}
		}

		private void AddDailySpecial(List<ServedDishSO> candidates, System.Random random)
		{
			if (candidates != null && candidates.Count != 0)
			{
				AddDailySpecial(candidates[random.Next(0, candidates.Count)], random);
			}
		}

		private void AddDailySpecial(ServedDishSO dish, System.Random random)
		{
			if (!(dish == null) && !string.IsNullOrWhiteSpace(dish.Id))
			{
				_dailySushiSpecials.Add(new DailySushiSpecialEntry
				{
					SushiId = dish.Id,
					BonusPercentage = GetWeightedDailySpecialBonus(random)
				});
			}
		}

		private void AddDailySpecial(string recipeId, System.Random random)
		{
			if (!string.IsNullOrWhiteSpace(recipeId))
			{
				_dailySushiSpecials.Add(new DailySushiSpecialEntry
				{
					SushiId = recipeId,
					BonusPercentage = GetWeightedDailySpecialBonus(random)
				});
			}
		}

		private static byte GetWeightedDailySpecialBonus(System.Random random)
		{
			int num = random.Next(100);
			if (num < 35)
			{
				return 5;
			}
			if (num < 62)
			{
				return 10;
			}
			if (num < 82)
			{
				return 15;
			}
			if (num < 94)
			{
				return 20;
			}
			return 25;
		}

		private static bool IsValidDailySpecialBonus(byte bonusPercentage)
		{
			if (bonusPercentage >= 5 && bonusPercentage <= 25)
			{
				return bonusPercentage % 5 == 0;
			}
			return false;
		}

		private static byte NormalizeSavedDailySpecialBonus(byte bonusPercentage)
		{
			if (bonusPercentage >= 1 && bonusPercentage < 5)
			{
				return (byte)(bonusPercentage * 5);
			}
			if (!IsValidDailySpecialBonus(bonusPercentage))
			{
				return 0;
			}
			return bonusPercentage;
		}

		private static bool IsDailySushiSpecialFeatureUnlocked()
		{
			if (QuestReferenceHub.Instance != null)
			{
				return QuestReferenceHub.Instance.IsCompleted(QuestId.ServeMackerelNigiri);
			}
			return false;
		}

		private static void AddScoreIfValid(FishLeaderboardEntry score, List<FishLeaderboardEntryData> targetList)
		{
			if (score.SteamId != 0L || !Mathf.Approximately(score.BestWeightKg, 0f) || score.PlayerName.Length != 0)
			{
				FishLeaderboardEntryData item = new FishLeaderboardEntryData
				{
					PlayerName = score.PlayerName.ToString(),
					BestWeightKg = score.BestWeightKg,
					SteamID = score.SteamId
				};
				targetList.Add(item);
			}
		}

		public TabletData Save()
		{
			if (!base.IsServer)
			{
				return null;
			}
			TabletData tabletData = new TabletData
			{
				WallpaperName = "",
				WallpaperData = new List<DefaultWallpaperData>(),
				MenuItems = new List<string>(),
				DailySushiSpecialFeatureActivated = _dailySushiSpecialFeatureActivated.Value,
				DailySushiSpecialDay = _dailySushiSpecialDay.Value,
				DailySushiSpecials = new List<DailySushiSpecialEntryData>(),
				FishBookEntries = new List<FishBookEntryData>(),
				FishPersonalBestEntries = new List<FishPersonalBestEntryData>(),
				DiscoveredSeaLayerIndices = new List<byte>(),
				StaffData = ((StaffManager.Instance != null) ? (StaffManager.Instance.Save() ?? new StaffData()) : new StaffData())
			};
			for (int i = 0; i < _menuItems.Count; i++)
			{
				FixedString64Bytes fixedString64Bytes = _menuItems[i];
				tabletData.MenuItems.Add(fixedString64Bytes.ToString());
			}
			for (int j = 0; j < _dailySushiSpecials.Count; j++)
			{
				DailySushiSpecialEntry dailySushiSpecialEntry = _dailySushiSpecials[j];
				tabletData.DailySushiSpecials.Add(new DailySushiSpecialEntryData
				{
					SushiId = dailySushiSpecialEntry.SushiId.ToString(),
					BonusPercentage = dailySushiSpecialEntry.BonusPercentage
				});
			}
			for (int k = 0; k < _discoveredSeaLayerIndices.Count; k++)
			{
				tabletData.DiscoveredSeaLayerIndices.Add(_discoveredSeaLayerIndices[k]);
			}
			for (int l = 0; l < _fishBookEntries.Count; l++)
			{
				FishBookEntry fishBookEntry = _fishBookEntries[l];
				FishBookEntryData fishBookEntryData = new FishBookEntryData
				{
					FishID = fishBookEntry.FishId.ToString(),
					IsUnlocked = fishBookEntry.IsUnlocked,
					TotalCaughtAmount = fishBookEntry.TotalCaughtAmount,
					LeaderboardEntries = new List<FishLeaderboardEntryData>()
				};
				AddScoreIfValid(fishBookEntry.Score1, fishBookEntryData.LeaderboardEntries);
				AddScoreIfValid(fishBookEntry.Score2, fishBookEntryData.LeaderboardEntries);
				AddScoreIfValid(fishBookEntry.Score3, fishBookEntryData.LeaderboardEntries);
				tabletData.FishBookEntries.Add(fishBookEntryData);
			}
			for (int m = 0; m < _fishPersonalBestEntries.Count; m++)
			{
				FishPersonalBestEntry fishPersonalBestEntry = _fishPersonalBestEntries[m];
				if (!fishPersonalBestEntry.FishId.IsEmpty && !(fishPersonalBestEntry.BestWeightKg <= 0f))
				{
					tabletData.FishPersonalBestEntries.Add(new FishPersonalBestEntryData
					{
						FishID = fishPersonalBestEntry.FishId.ToString(),
						SteamID = fishPersonalBestEntry.SteamId,
						BestWeightKg = fishPersonalBestEntry.BestWeightKg
					});
				}
			}
			UtilityMethods.Log("[Server-TabletApp] Saving tablet data: " + tabletData);
			return tabletData;
		}

		public void Load(TabletData data)
		{
			if (!base.IsServer || data == null)
			{
				return;
			}
			_menuItems.Clear();
			if (data.MenuItems != null)
			{
				foreach (string menuItem in data.MenuItems)
				{
					if (!string.IsNullOrWhiteSpace(menuItem))
					{
						_menuItems.Add(menuItem);
						UtilityMethods.Log("[TabletNetworkHandler] Loaded menu item: " + menuItem);
					}
				}
			}
			_dailySushiSpecialFeatureActivated.Value = data.DailySushiSpecialFeatureActivated;
			_dailySushiSpecialDay.Value = Mathf.Max(0, data.DailySushiSpecialDay);
			_dailySushiSpecials.Clear();
			if (data.DailySushiSpecials != null)
			{
				HashSet<string> hashSet = new HashSet<string>();
				int num = 0;
				int num2 = 0;
				foreach (DailySushiSpecialEntryData dailySushiSpecial in data.DailySushiSpecials)
				{
					byte b = (byte)((dailySushiSpecial != null) ? NormalizeSavedDailySpecialBonus(dailySushiSpecial.BonusPercentage) : 0);
					if (dailySushiSpecial == null || string.IsNullOrWhiteSpace(dailySushiSpecial.SushiId) || b == 0 || hashSet.Count >= 3 || !hashSet.Add(dailySushiSpecial.SushiId) || _itemDatabase == null)
					{
						continue;
					}
					ServedDishSO servedDishSO = _itemDatabase.FindById(dailySushiSpecial.SushiId) as ServedDishSO;
					bool flag = TryResolveRecipe(dailySushiSpecial.SushiId, out var recipe) && recipe.IdentityKind == SushiRecipeIdentityKind.SavedCustom;
					if (((object)servedDishSO != null && servedDishSO.ServedDishType == ServedDishType.Sushi) | flag)
					{
						if (num >= 2)
						{
							continue;
						}
						num++;
						goto IL_01ea;
					}
					ServedDishType? servedDishType = servedDishSO?.ServedDishType;
					bool flag2;
					if (servedDishType.HasValue)
					{
						ServedDishType valueOrDefault = servedDishType.GetValueOrDefault();
						if (valueOrDefault - 4 <= ServedDishType.Sushi)
						{
							flag2 = true;
							goto IL_01db;
						}
					}
					flag2 = false;
					goto IL_01db;
					IL_01ea:
					_dailySushiSpecials.Add(new DailySushiSpecialEntry
					{
						SushiId = dailySushiSpecial.SushiId,
						BonusPercentage = b
					});
					continue;
					IL_01db:
					if (!flag2 || num2 >= 1)
					{
						continue;
					}
					num2++;
					goto IL_01ea;
				}
			}
			_fishBookEntries.Clear();
			_fishPersonalBestEntries.Clear();
			if (data.FishBookEntries != null)
			{
				foreach (FishBookEntryData fishBookEntry in data.FishBookEntries)
				{
					if (fishBookEntry == null)
					{
						continue;
					}
					FishLeaderboardEntry score = default;
					FishLeaderboardEntry score2 = default;
					FishLeaderboardEntry score3 = default;
					if (!string.IsNullOrWhiteSpace(fishBookEntry.FishID))
					{
						if (fishBookEntry.LeaderboardEntries != null && fishBookEntry.LeaderboardEntries.Count > 0)
						{
							FishLeaderboardEntryData fishLeaderboardEntryData = fishBookEntry.LeaderboardEntries[0];
							score = new FishLeaderboardEntry
							{
								PlayerName = fishLeaderboardEntryData.PlayerName,
								BestWeightKg = fishLeaderboardEntryData.BestWeightKg,
								SteamId = fishLeaderboardEntryData.SteamID
							};
						}
						else
						{
							score.Clear();
						}
						if (fishBookEntry.LeaderboardEntries != null && fishBookEntry.LeaderboardEntries.Count > 1)
						{
							FishLeaderboardEntryData fishLeaderboardEntryData2 = fishBookEntry.LeaderboardEntries[1];
							score2 = new FishLeaderboardEntry
							{
								PlayerName = fishLeaderboardEntryData2.PlayerName,
								BestWeightKg = fishLeaderboardEntryData2.BestWeightKg,
								SteamId = fishLeaderboardEntryData2.SteamID
							};
						}
						else
						{
							score2.Clear();
						}
						if (fishBookEntry.LeaderboardEntries != null && fishBookEntry.LeaderboardEntries.Count > 2)
						{
							FishLeaderboardEntryData fishLeaderboardEntryData3 = fishBookEntry.LeaderboardEntries[2];
							score3 = new FishLeaderboardEntry
							{
								PlayerName = fishLeaderboardEntryData3.PlayerName,
								BestWeightKg = fishLeaderboardEntryData3.BestWeightKg,
								SteamId = fishLeaderboardEntryData3.SteamID
							};
						}
						else
						{
							score3.Clear();
						}
						FishBookEntry item = new FishBookEntry
						{
							FishId = fishBookEntry.FishID,
							IsUnlocked = fishBookEntry.IsUnlocked,
							TotalCaughtAmount = fishBookEntry.TotalCaughtAmount,
							Score1 = score,
							Score2 = score2,
							Score3 = score3
						};
						_fishBookEntries.Add(item);
						UtilityMethods.Log("[TabletNetworkHandler] Loaded fish book entry: " + fishBookEntry.FishID);
					}
				}
			}
			if (data.FishPersonalBestEntries != null)
			{
				foreach (FishPersonalBestEntryData fishPersonalBestEntry in data.FishPersonalBestEntries)
				{
					if (fishPersonalBestEntry != null && !string.IsNullOrWhiteSpace(fishPersonalBestEntry.FishID) && !(fishPersonalBestEntry.BestWeightKg <= 0f))
					{
						_fishPersonalBestEntries.Add(new FishPersonalBestEntry
						{
							FishId = fishPersonalBestEntry.FishID,
							SteamId = fishPersonalBestEntry.SteamID,
							BestWeightKg = fishPersonalBestEntry.BestWeightKg
						});
					}
				}
			}
			_discoveredSeaLayerIndices.Clear();
			if (data.DiscoveredSeaLayerIndices != null)
			{
				foreach (byte discoveredSeaLayerIndex in data.DiscoveredSeaLayerIndices)
				{
					if (!_discoveredSeaLayerIndices.Contains(discoveredSeaLayerIndex))
					{
						_discoveredSeaLayerIndices.Add(discoveredSeaLayerIndex);
						UtilityMethods.Log("[TabletNetworkHandler] Loaded discovered habitat: " + discoveredSeaLayerIndex);
					}
				}
			}
			Singleton<GameProgressManager>.Instance?.RpcRelay?.ServerReconcileSharedCollectionAchievements();
		}

		public bool IsHabitatDiscovered(byte seaLayerIndex)
		{
			if (_discoveredSeaLayerIndices != null)
			{
				return _discoveredSeaLayerIndices.Contains(seaLayerIndex);
			}
			return false;
		}

		public IReadOnlyList<byte> GetDiscoveredHabitats()
		{
			return GetDiscoveredHabitatSnapshot();
		}

		public void ServerDiscoverHabitat(byte seaLayerIndex)
		{
			if (base.IsServer && !_discoveredSeaLayerIndices.Contains(seaLayerIndex))
			{
				_discoveredSeaLayerIndices.Add(seaLayerIndex);
				UtilityMethods.Log($"[TabletNetworkHandler] Habitat discovered. SeaLayerIndex={seaLayerIndex}");
				ClientShowHabitatDiscoveredRpc(seaLayerIndex);
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerDiscoverHabitatRpc(byte seaLayerIndex)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1049594244u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in seaLayerIndex, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1049594244u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ServerDiscoverHabitat(seaLayerIndex);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientShowHabitatDiscoveredRpc(byte seaLayerIndex)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2206807909u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in seaLayerIndex, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2206807909u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				string habitatName = $"Habitat {seaLayerIndex}";
				if (Singleton<SeaCreatureManager>.Instance != null && Singleton<SeaCreatureManager>.Instance.TryGetLayer(seaLayerIndex, out var layer) && layer != null)
				{
					habitatName = layer.DisplayName;
				}
				Singleton<NotifyManager>.Instance?.ShowHabitatDiscovered(habitatName);
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerAddItemToMenuRpc(string itemID)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(120914624u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bool value = itemID != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(itemID);
				}
				__endSendRpc(ref bufferWriter, 120914624u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_menuItems.Contains(itemID))
			{
				return;
			}
			ItemSO itemSO = _itemDatabase.FindById(itemID);
			bool num = itemSO is ServedDishSO;
			bool flag = TryResolveRecipe(itemID, out var recipe) && recipe.IdentityKind == SushiRecipeIdentityKind.SavedCustom;
			if (!num && !flag)
			{
				return;
			}
			_menuItems.Add(itemID);
			UtilityMethods.Log("UI: " + (flag ? recipe.DisplayName : itemSO.Name) + " added to menu on ServerRpc!");
			if (itemSO is SushiSO sushiSO)
			{
				if (sushiSO.Id == _itemDatabase.CucumberMakiServeredDishSO.Id)
				{
					QuestReferenceHub.Instance.QuestComplete(QuestId.AddCucumberMakiToMenu);
				}
				else if (sushiSO.Id == _itemDatabase.MackarelNigiriServeredDishSO.Id)
				{
					QuestReferenceHub.Instance.QuestComplete(QuestId.AddMackerelNigiriToMenu);
				}
				else if (sushiSO.Id == _itemDatabase.ChinookSalmonTemakiServeredDishSO.Id)
				{
					QuestReferenceHub.Instance.QuestComplete(QuestId.AddChinookSalmonDishToMenu);
				}
				else if (sushiSO.Id == _itemDatabase.SquidNigiriServeredDishSO.Id)
				{
					QuestReferenceHub.Instance.QuestComplete(QuestId.AddSquidNigiriToTheMenu);
				}
			}
			else if (itemSO is SideDishSO || itemSO is BeverageSO)
			{
				QuestReferenceHub.Instance.QuestComplete(QuestId.AddASideDishToMenu);
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerRemoveItemFromMenuRpc(string itemId)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(4061288923u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bool value = itemId != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(itemId);
				}
				__endSendRpc(ref bufferWriter, 4061288923u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_menuItems.Contains(itemId))
			{
				ItemSO itemSO = _itemDatabase.FindById(itemId);
				bool flag = TryResolveRecipe(itemId, out var recipe) && recipe.IdentityKind == SushiRecipeIdentityKind.SavedCustom;
				if ((!(itemSO == null) || flag) && (!((itemSO is ServedDishSO servedDishSO && servedDishSO.ServedDishType == ServedDishType.Sushi) | flag) || HasAnotherSushiOnMenu(itemId)))
				{
					_menuItems.Remove(itemId);
					UtilityMethods.Log("UI: " + (flag ? recipe.DisplayName : itemSO.Name) + " Removed from menu on ServerRpc!");
				}
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerDeleteCustomSushiRecipeRpc(string recipeId, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1763683869u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bool value = recipeId != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(recipeId);
				}
				__endSendRpc(ref bufferWriter, 1763683869u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (string.IsNullOrWhiteSpace(recipeId))
			{
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			RestaurantManager restaurantManager = Singleton<GameProgressManager>.Instance?.RestaurantManager;
			if (restaurantManager?.CustomSushiRecipeCatalog.FindById(recipeId) == null)
			{
				return;
			}
			if (HasActiveOrderForRecipe(recipeId) || restaurantManager.ServerHasPendingTableOrderForRecipe(recipeId))
			{
				SendCustomSushiDeleteToast(senderClientId, "notification.chef.custom_recipe_delete_blocked_order", NotificationState.Error);
				return;
			}
			if (HasPendingChefProductionForRecipe(recipeId) || restaurantManager.ServerHasActiveChefDeskRecipe(recipeId))
			{
				SendCustomSushiDeleteToast(senderClientId, "notification.chef.custom_recipe_delete_blocked_preparation", NotificationState.Error);
				return;
			}
			if (_menuItems.Contains(recipeId))
			{
				if (!restaurantManager.ServerTryDeleteCustomSushiRecipe(recipeId))
				{
					return;
				}
				_menuItems.Remove(recipeId);
			}
			else if (!restaurantManager.ServerTryDeleteCustomSushiRecipe(recipeId))
			{
				return;
			}
			for (int num = _dailySushiSpecials.Count - 1; num >= 0; num--)
			{
				if (_dailySushiSpecials[num].SushiId.Equals(recipeId))
				{
					_dailySushiSpecials.RemoveAt(num);
				}
			}
			SendCustomSushiDeleteToast(senderClientId, "notification.chef.custom_recipe_deleted", NotificationState.Success);
		}

		private static bool HasPendingChefProductionForRecipe(string recipeId)
		{
			StaffWorkplaceRegistry instance = StaffWorkplaceRegistry.Instance;
			if (instance == null || string.IsNullOrWhiteSpace(recipeId))
			{
				return false;
			}
			foreach (string item in instance.PendingChefProduction)
			{
				if (string.Equals(item, recipeId, StringComparison.Ordinal))
				{
					return true;
				}
			}
			return false;
		}

		private void SendCustomSushiDeleteToast(ulong targetClientId, string localizationKey, NotificationState state)
		{
			ClientShowCustomSushiDeleteToastRpc(localizationKey, state, base.RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientShowCustomSushiDeleteToastRpc(FixedString128Bytes localizationKey, NotificationState state, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1270839673u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in localizationKey, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in state, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 1270839673u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Singleton<NotifyManager>.Instance?.ShowToast(localizationKey.ToString(), state, 2.5f);
			}
		}

		private bool HasActiveOrderForRecipe(string recipeId)
		{
			if (string.IsNullOrWhiteSpace(recipeId))
			{
				return false;
			}
			FixedString32Bytes other = new FixedString32Bytes(recipeId);
			for (int i = 0; i < _customerOrderEntries.Count; i++)
			{
				CustomerOrderEntry customerOrderEntry = _customerOrderEntries[i];
				for (int j = 0; j < customerOrderEntry.FoodIds.Length; j++)
				{
					if (customerOrderEntry.FoodIds[j].Equals(other))
					{
						return true;
					}
				}
			}
			return false;
		}

		private bool HasAnotherSushiOnMenu(string excludedItemId)
		{
			for (int i = 0; i < _menuItems.Count; i++)
			{
				string text = _menuItems[i].ToString();
				if (!(text == excludedItemId))
				{
					if (_itemDatabase.FindById(text) is ServedDishSO { ServedDishType: ServedDishType.Sushi })
					{
						return true;
					}
					if (TryResolveRecipe(text, out var recipe) && recipe.IdentityKind == SushiRecipeIdentityKind.SavedCustom)
					{
						return true;
					}
				}
			}
			return false;
		}

		public void ServerAddToFishBook(FishSO data, float weight, ulong steamId, ulong ownerClientId)
		{
			if (!base.IsServer)
			{
				return;
			}
			if (data == null)
			{
				UtilityMethods.Log("[TabletNetworkHandler] ServerAddToFishBook skipped. FishSO is null.", LogType.Warning);
				return;
			}
			int num = FindFishIndex(data.Id);
			bool num2 = num >= 0 && _fishBookEntries[num].IsUnlocked;
			ServerRegisterPersonalBest(data.Id, weight, steamId);
			string text = ResolveSafePlayerName(steamId);
			if (ServiceLocator.ForSceneOf(this).TryGet<DailyReportBroadcaster>(out var service) && service != null)
			{
				service.ServerRecordCaught(data.Id, weight, text, steamId);
			}
			else
			{
				UtilityMethods.Log("[TabletNetworkHandler] DailyReportBroadcaster not found while recording fish catch.", LogType.Warning);
			}
			if (num < 0)
			{
				FishBookEntry item = new FishBookEntry
				{
					FishId = data.Id,
					IsUnlocked = true,
					TotalCaughtAmount = 1
				};
				item.Score1 = new FishLeaderboardEntry
				{
					PlayerName = text,
					BestWeightKg = weight,
					SteamId = steamId
				};
				item.Score2.Clear();
				item.Score3.Clear();
				_fishBookEntries.Add(item);
			}
			else
			{
				FishBookEntry item = _fishBookEntries[num];
				item.IsUnlocked = true;
				item.TotalCaughtAmount++;
				UpdateScoreboard(ref item, weight, text, steamId);
				_fishBookEntries[num] = item;
			}
			if (!num2 && ServerIsFishBookComplete())
			{
				Singleton<GameProgressManager>.Instance?.RpcRelay?.ServerUnlockSteamAchievementForAllClients("ACH_NO_MORE_QUESTION_MARKS");
			}
			UtilityMethods.Log("Server updated fish book for " + data.Id);
			ClientRegisterCatchRpc(data.Id, weight, base.RpcTarget.Single(ownerClientId, RpcTargetUse.Temp));
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientRegisterCatchRpc(FixedString64Bytes fishId, float weight, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3187155635u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in fishId, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in weight, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3187155635u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				RegisterCatch(fishId.ToString(), weight);
				if (CanUseLocalTabletUi())
				{
					RefreshFishBookFromSnapshot();
				}
			}
		}

		private string ResolveSafePlayerName(ulong steamId)
		{
			if (Singleton<PlayersManager>.Instance != null && Singleton<PlayersManager>.Instance.TryGetPlayerNameBySteamId(steamId, out var playerName) && !string.IsNullOrWhiteSpace(playerName))
			{
				return playerName;
			}
			if (SteamClient.IsValid && steamId == SteamClient.SteamId.Value)
			{
				return SteamClient.Name;
			}
			return $"Player {steamId}";
		}

		private int FindFishIndex(string fishId)
		{
			for (int i = 0; i < _fishBookEntries.Count; i++)
			{
				if (_fishBookEntries[i].FishId.Equals(fishId))
				{
					return i;
				}
			}
			return -1;
		}

		public bool ServerIsFishBookComplete()
		{
			if (!base.IsServer || _itemDatabase == null || _itemDatabase.CreatureItems == null)
			{
				return false;
			}
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < _itemDatabase.CreatureItems.Count; i++)
			{
				if (_itemDatabase.CreatureItems[i] is FishSO fishSO && !string.IsNullOrWhiteSpace(fishSO.Id))
				{
					hashSet.Add(fishSO.Id);
				}
			}
			if (hashSet.Count == 0)
			{
				return false;
			}
			for (int j = 0; j < _fishBookEntries.Count; j++)
			{
				FishBookEntry fishBookEntry = _fishBookEntries[j];
				if (fishBookEntry.IsUnlocked)
				{
					hashSet.Remove(fishBookEntry.FishId.ToString());
				}
			}
			return hashSet.Count == 0;
		}

		public bool ServerDebugPrepareFishBookExcept(string excludedFishId, out int unlockedFishCount)
		{
			unlockedFishCount = 0;
			if (!base.IsServer || _itemDatabase == null || _itemDatabase.CreatureItems == null || string.IsNullOrWhiteSpace(excludedFishId))
			{
				return false;
			}
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < _itemDatabase.CreatureItems.Count; i++)
			{
				if (_itemDatabase.CreatureItems[i] is FishSO fishSO && !string.IsNullOrWhiteSpace(fishSO.Id))
				{
					hashSet.Add(fishSO.Id);
				}
			}
			if (!hashSet.Contains(excludedFishId))
			{
				UtilityMethods.Log("[TabletNetworkHandler][FishBookDebug] Excluded fish was not found. FishId=" + excludedFishId, LogType.Warning);
				return false;
			}
			foreach (string item2 in hashSet)
			{
				int num = FindFishIndex(item2);
				bool flag = !string.Equals(item2, excludedFishId, StringComparison.Ordinal);
				if (num < 0)
				{
					FishBookEntry item = new FishBookEntry
					{
						FishId = item2,
						IsUnlocked = flag,
						TotalCaughtAmount = 0
					};
					item.Score1.Clear();
					item.Score2.Clear();
					item.Score3.Clear();
					_fishBookEntries.Add(item);
				}
				else
				{
					FishBookEntry value = _fishBookEntries[num];
					value.IsUnlocked = flag;
					if (!flag)
					{
						value.TotalCaughtAmount = 0;
						value.Score1.Clear();
						value.Score2.Clear();
						value.Score3.Clear();
					}
					_fishBookEntries[num] = value;
				}
				if (flag)
				{
					unlockedFishCount++;
				}
			}
			UtilityMethods.Log($"[TabletNetworkHandler][FishBookDebug] Test state prepared. Unlocked={unlockedFishCount}, RemainingFishId={excludedFishId}");
			return true;
		}

		private void UpdateScoreboard(ref FishBookEntry entry, float weight, string playerName, ulong steamId)
		{
			FishLeaderboardEntry fishLeaderboardEntry = new FishLeaderboardEntry
			{
				PlayerName = playerName,
				BestWeightKg = weight,
				SteamId = steamId
			};
			if (weight >= entry.Score1.BestWeightKg)
			{
				entry.Score3 = entry.Score2;
				entry.Score2 = entry.Score1;
				entry.Score1 = fishLeaderboardEntry;
			}
			else if (weight >= entry.Score2.BestWeightKg)
			{
				entry.Score3 = entry.Score2;
				entry.Score2 = fishLeaderboardEntry;
			}
			else if (weight >= entry.Score3.BestWeightKg)
			{
				entry.Score3 = fishLeaderboardEntry;
			}
		}

		public void RegisterCatch(string fishId, float weight)
		{
		}

		public byte GetFishingCatchMilestone(FishSO fish, float weight, ulong steamId)
		{
			int num = FindFishIndex(fish.Id);
			if (num < 0 || !_fishBookEntries[num].IsUnlocked)
			{
				return 1;
			}
			float personalBest = GetPersonalBest(fish.Id, steamId);
			if (!(personalBest > 0f) || !(weight > personalBest))
			{
				return 0;
			}
			return 2;
		}

		public float GetPersonalBest(string fishId, ulong? steamId = null)
		{
			ulong num = steamId ?? (Singleton<PlayersManager>.Instance?.LocalPlayer?.SteamID.Value).GetValueOrDefault();
			for (int i = 0; i < _fishPersonalBestEntries.Count; i++)
			{
				FishPersonalBestEntry fishPersonalBestEntry = _fishPersonalBestEntries[i];
				if (fishPersonalBestEntry.SteamId == num && fishPersonalBestEntry.FishId.Equals(fishId))
				{
					return fishPersonalBestEntry.BestWeightKg;
				}
			}
			int num2 = FindFishIndex(fishId);
			if (num2 >= 0)
			{
				FishBookEntry fishBookEntry = _fishBookEntries[num2];
				float num3 = 0f;
				FishLeaderboardEntry[] array = new FishLeaderboardEntry[3] { fishBookEntry.Score1, fishBookEntry.Score2, fishBookEntry.Score3 };
				for (int j = 0; j < array.Length; j++)
				{
					if (array[j].SteamId == num)
					{
						num3 = Mathf.Max(num3, array[j].BestWeightKg);
					}
				}
				return num3;
			}
			return 0f;
		}

		private void ServerRegisterPersonalBest(string fishId, float weight, ulong steamId)
		{
			if (!base.IsServer || string.IsNullOrWhiteSpace(fishId) || weight <= 0f)
			{
				return;
			}
			for (int i = 0; i < _fishPersonalBestEntries.Count; i++)
			{
				FishPersonalBestEntry value = _fishPersonalBestEntries[i];
				if (value.SteamId == steamId && value.FishId.Equals(fishId))
				{
					if (weight > value.BestWeightKg)
					{
						value.BestWeightKg = weight;
						_fishPersonalBestEntries[i] = value;
					}
					return;
				}
			}
			_fishPersonalBestEntries.Add(new FishPersonalBestEntry
			{
				FishId = fishId,
				SteamId = steamId,
				BestWeightKg = weight
			});
		}

		public int ServerAddOrder(string tableNo, string customerName, string waitressName, CustomerOrderFoodPayload foodPayload, double expireServerTime = -1.0, int clientOrderIndex = -1, bool isStaffWaitress = false, StaffGender staffWaitressGender = StaffGender.Male)
		{
			if (!base.IsServer)
			{
				return -1;
			}
			int num = _nextOrderId++;
			double num2 = ((NetworkManager.Singleton != null) ? (NetworkManager.Singleton.ServerTime.Time + (double)Mathf.Max(1f, _orderDurationSeconds)) : (Time.timeAsDouble + (double)Mathf.Max(1f, _orderDurationSeconds)));
			double num3 = ((expireServerTime > 0.0) ? expireServerTime : num2);
			CustomerOrderEntry item = new CustomerOrderEntry
			{
				OrderId = num,
				TableNo = tableNo,
				ClientOrderIndex = clientOrderIndex,
				CustomerName = customerName,
				WaitressName = waitressName,
				IsStaffWaitress = isStaffWaitress,
				StaffWaitressGender = staffWaitressGender,
				FoodIds = foodPayload.FoodIds,
				DeliveredFoodIds = default,
				ExpireServerTime = num3,
				Status = 0
			};
			_customerOrderEntries.Add(item);
			UtilityMethods.Log("[TabletNetworkHandler] Order added. " + $"OrderId={num}, TableNo={tableNo}, Customer={customerName}, ClientOrderIndex={clientOrderIndex}, " + $"ExpireServerTime={num3:0.00}, " + $"CustomExpireUsed={expireServerTime > 0.0}");
			return num;
		}

		public void ServerRemoveOrder(int orderId)
		{
			if (!base.IsServer)
			{
				return;
			}
			for (int i = 0; i < _customerOrderEntries.Count; i++)
			{
				if (_customerOrderEntries[i].OrderId == orderId)
				{
					_customerOrderEntries.RemoveAt(i);
					break;
				}
			}
		}

		public void ServerExtendTableOrderDeadline(string tableNo, double deadline)
		{
			if (!base.IsServer)
			{
				return;
			}
			for (int i = 0; i < _customerOrderEntries.Count; i++)
			{
				CustomerOrderEntry value = _customerOrderEntries[i];
				if (!(value.TableNo.ToString() != tableNo) && !(value.ExpireServerTime >= deadline))
				{
					OrderReceiptStatus status = value.GetStatus();
					if (status != OrderReceiptStatus.Served && status != OrderReceiptStatus.Expired)
					{
						value.ExpireServerTime = deadline;
						_customerOrderEntries[i] = value;
					}
				}
			}
		}

		public void ServerRemoveOrdersForTable(string tableNo)
		{
			if (!base.IsServer)
			{
				return;
			}
			for (int num = _customerOrderEntries.Count - 1; num >= 0; num--)
			{
				if (_customerOrderEntries[num].TableNo.Equals(tableNo))
				{
					_customerOrderEntries.RemoveAt(num);
				}
			}
		}

		public bool ServerRegisterDeliveredFoods(int orderId, CustomerOrderFoodPayload deliveredPayload)
		{
			if (!base.IsServer)
			{
				return false;
			}
			for (int i = 0; i < _customerOrderEntries.Count; i++)
			{
				CustomerOrderEntry value = _customerOrderEntries[i];
				if (value.OrderId == orderId)
				{
					if (!value.TryRegisterDeliveredFoods(deliveredPayload.FoodIds))
					{
						return false;
					}
					if (value.IsFullyDelivered())
					{
						value.Status = 3;
					}
					else
					{
						value.Status = 1;
					}
					_customerOrderEntries[i] = value;
					return true;
				}
			}
			return false;
		}

		public bool ServerUnregisterDeliveredFoods(int orderId, CustomerOrderFoodPayload deliveredPayload)
		{
			if (!base.IsServer)
			{
				return false;
			}
			for (int i = 0; i < _customerOrderEntries.Count; i++)
			{
				CustomerOrderEntry value = _customerOrderEntries[i];
				if (value.OrderId == orderId)
				{
					if (!value.TryUnregisterDeliveredFoods(deliveredPayload.FoodIds))
					{
						return false;
					}
					if (value.DeliveredFoodIds.Length <= 0)
					{
						value.Status = 0;
					}
					else if (value.IsFullyDelivered())
					{
						value.Status = 3;
					}
					else
					{
						value.Status = 1;
					}
					_customerOrderEntries[i] = value;
					return true;
				}
			}
			return false;
		}

		public bool ServerIsTableFullyDelivered(string tableNo)
		{
			if (!base.IsServer)
			{
				return false;
			}
			bool result = false;
			for (int i = 0; i < _customerOrderEntries.Count; i++)
			{
				CustomerOrderEntry customerOrderEntry = _customerOrderEntries[i];
				if (customerOrderEntry.TableNo.Equals(tableNo))
				{
					result = true;
					if (!customerOrderEntry.IsFullyDelivered())
					{
						return false;
					}
				}
			}
			return result;
		}

		public bool ServerTryGetRandomCustomerSelection(System.Random rng, out SushiRecipeDefinition mainDish, out ServedDishSO sideDish, out ServedDishSO beverage)
		{
			mainDish = null;
			sideDish = null;
			beverage = null;
			if (!base.IsServer)
			{
				UtilityMethods.Log("[TabletNetworkHandler] ServerTryGetRandomCustomerSelection blocked. Only server can generate customer orders.", LogType.Warning);
				return false;
			}
			if (rng == null)
			{
				rng = new System.Random();
			}
			if (_itemDatabase == null)
			{
				UtilityMethods.Log("[TabletNetworkHandler] Cannot generate customer order. ItemDatabase is null.", LogType.Warning);
				return false;
			}
			if (_menuItems == null || _menuItems.Count == 0)
			{
				UtilityMethods.Log("[TabletNetworkHandler] Cannot generate customer order. Menu is empty.", LogType.Warning);
				return false;
			}
			List<SushiRecipeDefinition> list = new List<SushiRecipeDefinition>();
			List<ServedDishSO> list2 = new List<ServedDishSO>();
			List<ServedDishSO> list3 = new List<ServedDishSO>();
			for (int i = 0; i < _menuItems.Count; i++)
			{
				string text = _menuItems[i].ToString();
				if (string.IsNullOrWhiteSpace(text))
				{
					continue;
				}
				if (!(_itemDatabase.FindById(text) is ServedDishSO servedDishSO))
				{
					if (TryResolveRecipe(text, out var recipe) && recipe.IdentityKind == SushiRecipeIdentityKind.SavedCustom)
					{
						list.Add(recipe);
					}
					continue;
				}
				switch (servedDishSO.ServedDishType)
				{
				case ServedDishType.Sushi:
				{
					SushiRecipeDefinition sushiRecipeDefinition = SushiRecipeResolver.CreateBuiltInDefinition(servedDishSO as SushiSO);
					if (sushiRecipeDefinition != null)
					{
						list.Add(sushiRecipeDefinition);
					}
					break;
				}
				case ServedDishType.Dessert:
				case ServedDishType.SideDish:
					list2.Add(servedDishSO);
					break;
				case ServedDishType.Beverage:
					list3.Add(servedDishSO);
					break;
				}
			}
			if (list.Count == 0)
			{
				UtilityMethods.Log("[TabletNetworkHandler] Cannot generate customer order. Menu has no main dish / sushi.", LogType.Warning);
				return false;
			}
			mainDish = list[rng.Next(0, list.Count)];
			bool flag = QuestReferenceHub.Instance != null && QuestReferenceHub.Instance.IsActive(QuestId.ServeSideDish);
			if (list2.Count > 0)
			{
				float num = (flag ? _serveSideDishQuestOptionalOrderChance : GetOptionalItemOrderChance(list2.Count, _sideDishBaseOrderChance));
				if (rng.NextDouble() < (double)num)
				{
					sideDish = list2[rng.Next(0, list2.Count)];
				}
			}
			if (list3.Count > 0)
			{
				float num2 = (flag ? _serveSideDishQuestOptionalOrderChance : GetOptionalItemOrderChance(list3.Count, _beverageBaseOrderChance));
				if (rng.NextDouble() < (double)num2)
				{
					beverage = list3[rng.Next(0, list3.Count)];
				}
			}
			return true;
		}

		private bool TryResolveRecipe(string recipeId, out SushiRecipeDefinition recipe)
		{
			CustomSushiRecipeCatalog customCatalog = Singleton<GameProgressManager>.Instance?.RestaurantManager?.CustomSushiRecipeCatalog;
			return new SushiRecipeResolver(_itemDatabase, customCatalog).TryResolveById(recipeId, out recipe);
		}

		private float GetOptionalItemOrderChance(int availableItemCount, float baseChance)
		{
			if (availableItemCount <= 0)
			{
				return 0f;
			}
			float value = baseChance + (float)(availableItemCount - 1) * _additionalOptionalItemChance;
			float max = Mathf.Clamp(_maxOptionalItemOrderChance, 0f, 0.99f);
			return Mathf.Clamp(value, 0f, max);
		}

		public bool ServerTryDeliverFoodToTable(string tableNo, string foodId)
		{
			if (!base.IsServer)
			{
				return false;
			}
			FixedString32Bytes foodId2 = foodId;
			for (int i = 0; i < _customerOrderEntries.Count; i++)
			{
				CustomerOrderEntry value = _customerOrderEntries[i];
				if (value.TableNo.Equals(tableNo) && value.TryRegisterDeliveredFood(foodId2))
				{
					_customerOrderEntries[i] = value;
					if (ServerIsTableFullyDelivered(tableNo))
					{
						ServerRemoveOrdersForTable(tableNo);
					}
					return true;
				}
			}
			return false;
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestUpgradeRpc(ToolType toolType, int unlockCost, int upgradeCost)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3163249856u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in toolType, default(FastBufferWriter.ForEnums));
				BytePacker.WriteValueBitPacked(bufferWriter, unlockCost);
				BytePacker.WriteValueBitPacked(bufferWriter, upgradeCost);
				__endSendRpc(ref bufferWriter, 3163249856u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsServer)
			{
				ServiceLocator.ForSceneOf(this).Get<EconomyHandler>(out var service);
				if (!service.HasSufficientBalance(unlockCost))
				{
					UtilityMethods.Log("[ScrapForge] ServerRequestUpgradeRpc not enough balance!");
					return;
				}
				if (!service.HasSufficientScrap(upgradeCost))
				{
					UtilityMethods.Log("[ScrapForge] ServerRequestUpgradeRpc not enough scrap!");
					return;
				}
				service.SpendMoney(unlockCost);
				service.SpendScrap(upgradeCost);
				ClientReceiveUpgradeRpc(toolType);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientReceiveUpgradeRpc(ToolType toolType)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1806808843u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in toolType, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 1806808843u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override void __initializeVariables()
		{
			if (_dailySushiSpecialDay == null)
			{
				throw new Exception("TabletNetworkHandler._dailySushiSpecialDay cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_dailySushiSpecialDay.Initialize(this);
			__nameNetworkVariable(_dailySushiSpecialDay, "_dailySushiSpecialDay");
			NetworkVariableFields.Add(_dailySushiSpecialDay);
			if (_dailySushiSpecialFeatureActivated == null)
			{
				throw new Exception("TabletNetworkHandler._dailySushiSpecialFeatureActivated cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_dailySushiSpecialFeatureActivated.Initialize(this);
			__nameNetworkVariable(_dailySushiSpecialFeatureActivated, "_dailySushiSpecialFeatureActivated");
			NetworkVariableFields.Add(_dailySushiSpecialFeatureActivated);
			if (_menuItems == null)
			{
				throw new Exception("TabletNetworkHandler._menuItems cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_menuItems.Initialize(this);
			__nameNetworkVariable(_menuItems, "_menuItems");
			NetworkVariableFields.Add(_menuItems);
			if (_fishBookEntries == null)
			{
				throw new Exception("TabletNetworkHandler._fishBookEntries cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_fishBookEntries.Initialize(this);
			__nameNetworkVariable(_fishBookEntries, "_fishBookEntries");
			NetworkVariableFields.Add(_fishBookEntries);
			if (_fishPersonalBestEntries == null)
			{
				throw new Exception("TabletNetworkHandler._fishPersonalBestEntries cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_fishPersonalBestEntries.Initialize(this);
			__nameNetworkVariable(_fishPersonalBestEntries, "_fishPersonalBestEntries");
			NetworkVariableFields.Add(_fishPersonalBestEntries);
			if (_customerOrderEntries == null)
			{
				throw new Exception("TabletNetworkHandler._customerOrderEntries cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_customerOrderEntries.Initialize(this);
			__nameNetworkVariable(_customerOrderEntries, "_customerOrderEntries");
			NetworkVariableFields.Add(_customerOrderEntries);
			if (_discoveredSeaLayerIndices == null)
			{
				throw new Exception("TabletNetworkHandler._discoveredSeaLayerIndices cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_discoveredSeaLayerIndices.Initialize(this);
			__nameNetworkVariable(_discoveredSeaLayerIndices, "_discoveredSeaLayerIndices");
			NetworkVariableFields.Add(_discoveredSeaLayerIndices);
			if (_dailySushiSpecials == null)
			{
				throw new Exception("TabletNetworkHandler._dailySushiSpecials cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_dailySushiSpecials.Initialize(this);
			__nameNetworkVariable(_dailySushiSpecials, "_dailySushiSpecials");
			NetworkVariableFields.Add(_dailySushiSpecials);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(1049594244u, __rpc_handler_1049594244, "ServerDiscoverHabitatRpc", RpcInvokePermission.Everyone);
			__registerRpc(2206807909u, __rpc_handler_2206807909, "ClientShowHabitatDiscoveredRpc", RpcInvokePermission.Everyone);
			__registerRpc(120914624u, __rpc_handler_120914624, "ServerAddItemToMenuRpc", RpcInvokePermission.Everyone);
			__registerRpc(4061288923u, __rpc_handler_4061288923, "ServerRemoveItemFromMenuRpc", RpcInvokePermission.Everyone);
			__registerRpc(1763683869u, __rpc_handler_1763683869, "ServerDeleteCustomSushiRecipeRpc", RpcInvokePermission.Everyone);
			__registerRpc(1270839673u, __rpc_handler_1270839673, "ClientShowCustomSushiDeleteToastRpc", RpcInvokePermission.Everyone);
			__registerRpc(3187155635u, __rpc_handler_3187155635, "ClientRegisterCatchRpc", RpcInvokePermission.Everyone);
			__registerRpc(3163249856u, __rpc_handler_3163249856, "ServerRequestUpgradeRpc", RpcInvokePermission.Everyone);
			__registerRpc(1806808843u, __rpc_handler_1806808843, "ClientReceiveUpgradeRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_1049594244(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((TabletNetworkHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((TabletNetworkHandler)target).ServerDiscoverHabitatRpc(value);
				((TabletNetworkHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2206807909(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((TabletNetworkHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((TabletNetworkHandler)target).ClientShowHabitatDiscoveredRpc(value);
				((TabletNetworkHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_120914624(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				string s = null;
				if (value)
				{
					reader.ReadValueSafe(out s, false);
				}
				((TabletNetworkHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((TabletNetworkHandler)target).ServerAddItemToMenuRpc(s);
				((TabletNetworkHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4061288923(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				string s = null;
				if (value)
				{
					reader.ReadValueSafe(out s, false);
				}
				((TabletNetworkHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((TabletNetworkHandler)target).ServerRemoveItemFromMenuRpc(s);
				((TabletNetworkHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1763683869(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				string s = null;
				if (value)
				{
					reader.ReadValueSafe(out s, false);
				}
				RpcParams ext = rpcParams.Ext;
				((TabletNetworkHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((TabletNetworkHandler)target).ServerDeleteCustomSushiRecipeRpc(s, ext);
				((TabletNetworkHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1270839673(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString128Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out NotificationState value2, default(FastBufferWriter.ForEnums));
				RpcParams ext = rpcParams.Ext;
				((TabletNetworkHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((TabletNetworkHandler)target).ClientShowCustomSushiDeleteToastRpc(value, value2, ext);
				((TabletNetworkHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3187155635(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((TabletNetworkHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((TabletNetworkHandler)target).ClientRegisterCatchRpc(value, value2, ext);
				((TabletNetworkHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3163249856(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out ToolType value, default(FastBufferWriter.ForEnums));
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				ByteUnpacker.ReadValueBitPacked(reader, out int value3);
				((TabletNetworkHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((TabletNetworkHandler)target).ServerRequestUpgradeRpc(value, value2, value3);
				((TabletNetworkHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1806808843(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out ToolType value, default(FastBufferWriter.ForEnums));
				((TabletNetworkHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((TabletNetworkHandler)target).ClientReceiveUpgradeRpc(value);
				((TabletNetworkHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "TabletNetworkHandler";
		}
	}
}
