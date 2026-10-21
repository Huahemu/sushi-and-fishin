using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DG.Tweening;
using DistantLands.Cozy;
using PaperPixelGames.Core;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.FishFridge;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen;
using PaperPixelGames.Core.Kitchen.ChefDesk;
using PaperPixelGames.Core.Kitchen.PreparationStation.GastronomPan;
using PaperPixelGames.Core.Kitchen.PreparationStation.Station;
using PaperPixelGames.Core.Kitchen.ServedDishes;
using PaperPixelGames.Core.Kitchen.Wholasale;
using PaperPixelGames.Core.Lighthouse;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.ProcessedIngredient;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Core.Restaurant.Dirt;
using PaperPixelGames.Core.Restaurant.Sign;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Indicator;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking.Chat;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityServiceLocator;

public class RestaurantManager : NetworkBehaviour, ISaveable<RestaurantData>
{
	[Serializable]
	private class CompassTargetRoute
	{
		public List<Transform> Targets = new List<Transform>();
	}

	public struct StockSnapshotEntry : INetworkSerializable
	{
		public FixedString64Bytes ItemId;

		public int Amount;

		public StockSnapshotEntry(string itemId, int amount)
		{
			ItemId = itemId;
			Amount = amount;
		}

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref ItemId, default(FastBufferWriter.ForFixedStrings));
			serializer.SerializeValue(ref Amount, default(FastBufferWriter.ForPrimitives));
		}
	}

	private enum SeatingWaveTargetType
	{
		Dining = 0,
		Conveyor = 1
	}

	private struct SeatingWaveTarget
	{
		public SeatingWaveTargetType Type;

		public RestaurantTable DiningTable;

		public ConveyorBeltTable ConveyorTable;
	}

	private const string EmptyMenuAlertHeader = "MENU IS EMPTY";

	private const string EmptyMenuAlertDescription = "Add at least one sushi to the menu. Dining customers will not arrive.";

	private readonly CustomSushiRecipeCatalog _customSushiRecipeCatalog = new CustomSushiRecipeCatalog();

	private readonly NetworkList<CustomSushiRecipeNetworkData> _customSushiRecipesNetwork = new NetworkList<CustomSushiRecipeNetworkData>();

	[SerializeField]
	private OffscreenIndicatorTarget _restaurantIndicator;

	[SerializeField]
	private CozyWeatherManager _cozyManager;

	[SerializeField]
	private List<RestaurantTable> _tables;

	[SerializeField]
	private List<ConveyorBeltTable> _conveyorTables;

	[SerializeField]
	private List<TrayTrolley> _trolleys;

	[SerializeField]
	private ConveyorBelt _conveyorBelt;

	[SerializeField]
	private FilletMachine _filletMachine;

	[Header("Restaurant Saveable Data References")]
	[SerializeField]
	private Fridge _fishFridge;

	[SerializeField]
	private BeverageFridge _beverageFridge;

	[SerializeField]
	private List<ChefDesk> _chefDesks;

	[SerializeField]
	private List<PrepStation> _prepStations;

	[SerializeField]
	private List<ChefDeskGastroPan> _chefDeskGastropans;

	[SerializeField]
	private ItemDatabaseSO _databaseSO;

	[SerializeField]
	private RestaurantSignManager _restaurantSignManager;

	[SerializeField]
	private HoldableDirtManager _holdableDirtManager;

	[SerializeField]
	private Radio _radio;

	[SerializeField]
	private FishTrophy _fishTrophy;

	[Header("Spawn Data")]
	[SerializeField]
	private NetworkObject _gastroPanPrefab;

	[Header("Customer Flow")]
	[SerializeField]
	private bool _enableConveyorCustomerWaves = true;

	[SerializeField]
	[Min(1f)]
	private int _openingRushIntervalMinutes = 2;

	[SerializeField]
	[Min(1f)]
	private int _sustainCheckIntervalMinutes = 15;

	[SerializeField]
	[Range(0f, 1f)]
	private float _diningSelectionWeight = 0.7f;

	[Header("Compass Targets")]
	[SerializeField]
	private List<CompassTargetRoute> _compassTargetRoutes = new List<CompassTargetRoute>();

	[SerializeField]
	[Min(1f)]
	private byte _lighthouseCompassLevel = 4;

	[SerializeField]
	private LighthouseCompassDoor _lighthouseCompassDoor;

	[Header("Seating Unlock Prices")]
	[SerializeField]
	private TableUnlockPriceSO _tableUnlockPriceSO;

	[Header("Lights")]
	[SerializeField]
	private List<Light> _lights;

	[SerializeField]
	[Range(0f, 1f)]
	private float _dayLightMultiplier;

	[SerializeField]
	[Range(0f, 2f)]
	private float _nightLightMultiplier = 1f;

	[SerializeField]
	private bool _disableLightsWhenOff = true;

	[SerializeField]
	[Min(0f)]
	private float _offThreshold = 0.01f;

	[Header("Restaurant Active Time")]
	public int OpenTime = 9;

	public int CloseTime = 21;

	private const int RestaurantOpeningReminderHour = 11;

	private readonly List<float> _cachedNightLightIntensities = new List<float>();

	private readonly List<Tweener> _lightTweens = new List<Tweener>();

	private bool _cozyBloomBound;

	private bool _serverRestaurantLoaded;

	private bool _serverTimeEventsScheduled;

	private int _lastCustomerServiceEndedFeedbackDay = int.MinValue;

	private int _lastRestaurantOpeningReminderDay = int.MinValue;

	private int _lastRestaurantOpeningReminderObservedDay = int.MinValue;

	private int _lastRestaurantOpeningReminderObservedMinute = -1;

	private float _nextCustomerServiceEndedPollTime;

	private readonly HashSet<int> _servedTablesForServeTwoQuest = new HashSet<int>();

	private readonly List<SeatingWaveTarget> _availableWaveTargets = new List<SeatingWaveTarget>();

	private int _availableDiningWaveTargetCount;

	private int _availableConveyorWaveTargetCount;

	private bool _customerDemand;

	private bool _customerScheduled;

	private int _scheduledCustomerMinuteOfDay;

	private int _lastCustomerExecutedMinuteOfDay = -1;

	private bool _openingRushActive;

	private int _openingRushTargetOccupied;

	private int _lastInitializedCustomerDay = -1;

	private int _lastDiningCustomerCount;

	private readonly List<WholesaleEntity> _wholeSaleItemsInStock = new List<WholesaleEntity>();

	private readonly List<IngredientEntity> _ingredientEntities = new List<IngredientEntity>();

	private readonly List<GastroPan> _gastropans = new List<GastroPan>();

	private readonly List<DeadFish> _deadFishes = new List<DeadFish>();

	private readonly List<ServedDish> _servedDishes = new List<ServedDish>();

	private readonly HashSet<BeachBall> _beachBalls = new HashSet<BeachBall>();

	private RestaurantUpgradeData _restaurantUpgradeData = new RestaurantUpgradeData();

	private RestaurantSignCustomizationData _restaurantSignCustomizationData = new RestaurantSignCustomizationData();

	private RestaurantDirtData _restaurantDirtData = new RestaurantDirtData();

	private readonly NetworkList<byte> _unlockedDiningTableIds = new NetworkList<byte>();

	private readonly Dictionary<string, int> _itemsInStock = new Dictionary<string, int>();

	private bool _isRestaurantOpen;

	private NetworkVariable<byte> _currentCompassTargetLevel = new NetworkVariable<byte>(0);

	private NetworkVariable<byte> _currentCompassTargetStepIndex = new NetworkVariable<byte>(0);

	public CustomSushiRecipeCatalog CustomSushiRecipeCatalog => _customSushiRecipeCatalog;

	public RestaurantSignManager RestaurantSignManager => _restaurantSignManager;

	public Transform WorldAnchor
	{
		get
		{
			if (!(_restaurantIndicator != null))
			{
				if (!(_restaurantSignManager != null))
				{
					return null;
				}
				return _restaurantSignManager.transform;
			}
			return _restaurantIndicator.transform;
		}
	}

	public FilletMachine FilletMachine => _filletMachine;

	public FishTrophy FishTrophy => _fishTrophy;

	public bool IsRestaurantOpen
	{
		get
		{
			return _isRestaurantOpen;
		}
		set
		{
			if (_isRestaurantOpen != value)
			{
				_isRestaurantOpen = value;
				RefreshLocalMenuAvailabilityAlert();
			}
		}
	}

	public bool ServerStaffWorldReady
	{
		get
		{
			if (base.IsServer)
			{
				return _serverRestaurantLoaded;
			}
			return false;
		}
	}

	public IReadOnlyDictionary<string, int> ItemsInStock => _itemsInStock;

	public IReadOnlyList<WholesaleEntity> WholesaleItemsInStock => _wholeSaleItemsInStock;

	public bool IsAnyRestaurantTableUnlocked => GetPermanentlyUnlockedDiningTableCount() > 0;

	public bool IsAnyConveyorTableUnlocked => _conveyorTables.Any((ConveyorBeltTable t) => t != null && t.PhysicalStateController.IsPhysicallyUsable);

	public bool HasRepairedConveyorChairForQuest
	{
		get
		{
			if ((_restaurantUpgradeData?.UnlockedConveyorBeltTableIds?.Count).GetValueOrDefault() <= 0)
			{
				return _conveyorTables?.Any((ConveyorBeltTable t) => t != null && t.PhysicalStateController != null && t.PhysicalStateController.IsPhysicallyUsable) ?? false;
			}
			return true;
		}
	}

	public bool IsConveyorBeltRepaired
	{
		get
		{
			if (!(_conveyorBelt != null) || !(_conveyorBelt.PhysicalStateController != null) || !_conveyorBelt.PhysicalStateController.IsPhysicallyUsable)
			{
				if (_restaurantUpgradeData != null)
				{
					return _restaurantUpgradeData.IsConveyorBeltUnlocked;
				}
				return false;
			}
			return true;
		}
	}

	public bool IsAnyTableUnlocked
	{
		get
		{
			if (!IsAnyRestaurantTableUnlocked)
			{
				return IsAnyConveyorTableUnlocked;
			}
			return true;
		}
	}

	public List<RestaurantTable> Tables => _tables;

	public byte CurrentCompassTargetIndex
	{
		get
		{
			return _currentCompassTargetStepIndex.Value;
		}
		set
		{
			_currentCompassTargetStepIndex.Value = value;
		}
	}

	public byte CurrentCompassTargetLevel
	{
		get
		{
			return _currentCompassTargetLevel.Value;
		}
		set
		{
			_currentCompassTargetLevel.Value = value;
		}
	}

	public byte CurrentCompassTargetStepIndex
	{
		get
		{
			return _currentCompassTargetStepIndex.Value;
		}
		set
		{
			_currentCompassTargetStepIndex.Value = value;
		}
	}

	public event Action<string, int> StockChanged;

	public event Action DiningTableOwnershipChanged;

	public event Action CustomSushiRecipeCatalogChanged;

	public bool RegisterBeachBall(BeachBall beachBall)
	{
		if (!base.IsServer || beachBall == null)
		{
			return false;
		}
		_beachBalls.Add(beachBall);
		return true;
	}

	public void UnregisterBeachBall(BeachBall beachBall)
	{
		if (!(beachBall == null))
		{
			_beachBalls.Remove(beachBall);
		}
	}

	public void ServerResetBeachBallsForEndDay()
	{
		if (!base.IsServer || _beachBalls.Count == 0)
		{
			return;
		}
		List<BeachBall> list = new List<BeachBall>(_beachBalls);
		int num = 0;
		for (int i = 0; i < list.Count; i++)
		{
			BeachBall beachBall = list[i];
			if (beachBall == null || !beachBall.IsSpawned)
			{
				_beachBalls.Remove(beachBall);
				continue;
			}
			beachBall.ServerResetToInitialSpawn("End-day reset");
			num++;
		}
		UtilityMethods.Log($"[RestaurantManager] End-day beach ball reset completed. Count:{num}");
	}

	public void ServerResetFridgesForEndDay()
	{
		if (base.IsServer)
		{
			_fishFridge?.ServerResetForEndDay();
			_beverageFridge?.ServerResetForEndDay();
		}
	}

	public void ServerResetPrepStationsForEndDay()
	{
		if (!base.IsServer || _prepStations == null)
		{
			return;
		}
		for (int i = 0; i < _prepStations.Count; i++)
		{
			if (!(_prepStations[i] == null))
			{
				_prepStations[i].ServerResetForEndDay();
			}
		}
	}

	public void ServerResetChefDesksForEndDay()
	{
		if (!base.IsServer || _chefDesks == null)
		{
			return;
		}
		for (int i = 0; i < _chefDesks.Count; i++)
		{
			if (!(_chefDesks[i] == null))
			{
				_chefDesks[i].ServerResetForEndDay();
			}
		}
	}

	public void ServerResetRestaurantTablesForEndDay()
	{
		if (!base.IsServer || _tables == null)
		{
			return;
		}
		for (int i = 0; i < _tables.Count; i++)
		{
			if (!(_tables[i] == null))
			{
				_tables[i].ServerResetForEndDay();
			}
		}
	}

	public void ServerResetConveyorTablesForEndDay()
	{
		if (!base.IsServer || _conveyorTables == null)
		{
			return;
		}
		for (int i = 0; i < _conveyorTables.Count; i++)
		{
			if (!(_conveyorTables[i] == null) && _conveyorTables[i].PhysicalStateController.IsPhysicallyUsable)
			{
				_conveyorTables[i].ServerResetForEndDay();
			}
		}
	}

	public void ServerResetConveyorBeltForEndDay()
	{
		if (base.IsServer)
		{
			_conveyorBelt?.ServerResetForEndDay();
		}
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();
		CustomSushiThumbnailStore.Clear();
		_itemsInStock.Clear();
		CacheRestaurantLightIntensities();
		BindCozyBloomEvents();
		RefreshRestaurantLightsImmediate();
		NetworkVariable<byte> currentCompassTargetLevel = _currentCompassTargetLevel;
		currentCompassTargetLevel.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(currentCompassTargetLevel.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnCompassTargetChanged));
		NetworkVariable<byte> currentCompassTargetStepIndex = _currentCompassTargetStepIndex;
		currentCompassTargetStepIndex.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(currentCompassTargetStepIndex.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnCompassTargetChanged));
		_unlockedDiningTableIds.OnListChanged += OnUnlockedDiningTableIdsChanged;
		_customSushiRecipesNetwork.OnListChanged += OnCustomSushiRecipesNetworkChanged;
		if (base.IsServer)
		{
			ServerSynchronizeCustomSushiRecipeCatalog();
		}
		else
		{
			ClientRebuildCustomSushiRecipeCatalog();
			ServerRequestCustomSushiThumbnailsRpc();
		}
		RefreshCompassTargetForLocalPlayer();
		RefreshLocalMenuAvailabilityAlert();
	}

	private void Update()
	{
		if (base.IsServer && base.IsSpawned && _serverRestaurantLoaded && !(Time.unscaledTime < _nextCustomerServiceEndedPollTime))
		{
			_nextCustomerServiceEndedPollTime = Time.unscaledTime + 1f;
			ServerTryNotifyCustomerServiceEnded();
			ServerTryNotifyRestaurantOpeningReminder();
		}
	}

	public override void OnNetworkDespawn()
	{
		UnbindCozyBloomEvents();
		KillRestaurantLightTweens();
		if (base.IsServer)
		{
			CozyWeather.Events.onNewHour -= ServerOnNewHour;
			CozyWeather.Events.onNewMinute -= ServerOnNewMinute;
		}
		_serverRestaurantLoaded = false;
		_serverTimeEventsScheduled = false;
		_beachBalls.Clear();
		NetworkVariable<byte> currentCompassTargetLevel = _currentCompassTargetLevel;
		currentCompassTargetLevel.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(currentCompassTargetLevel.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnCompassTargetChanged));
		NetworkVariable<byte> currentCompassTargetStepIndex = _currentCompassTargetStepIndex;
		currentCompassTargetStepIndex.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(currentCompassTargetStepIndex.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnCompassTargetChanged));
		_unlockedDiningTableIds.OnListChanged -= OnUnlockedDiningTableIdsChanged;
		_customSushiRecipesNetwork.OnListChanged -= OnCustomSushiRecipesNetworkChanged;
		TryHideEmptyMenuAlert();
		CustomSushiThumbnailStore.Clear();
		base.OnNetworkDespawn();
	}

	private void OnUnlockedDiningTableIdsChanged(NetworkListEvent<byte> change)
	{
		DiningTableOwnershipChanged?.Invoke();
	}

	private void OnCustomSushiRecipesNetworkChanged(NetworkListEvent<CustomSushiRecipeNetworkData> change)
	{
		if (!base.IsServer)
		{
			ClientRebuildCustomSushiRecipeCatalog();
		}
		CustomSushiRecipeCatalogChanged?.Invoke();
	}

	private void ClientRebuildCustomSushiRecipeCatalog()
	{
		if (!base.IsServer)
		{
			List<CustomSushiRecipeData> list = new List<CustomSushiRecipeData>(_customSushiRecipesNetwork.Count);
			for (int i = 0; i < _customSushiRecipesNetwork.Count; i++)
			{
				list.Add(_customSushiRecipesNetwork[i].ToData());
			}
			_customSushiRecipeCatalog.Load(list, 1);
		}
	}

	private void ServerSynchronizeCustomSushiRecipeCatalog()
	{
		if (!base.IsServer)
		{
			return;
		}
		if (base.IsSpawned)
		{
			ClientClearCustomSushiThumbnailsRpc();
		}
		_customSushiRecipesNetwork.Clear();
		IReadOnlyList<CustomSushiRecipeData> recipes = _customSushiRecipeCatalog.Recipes;
		for (int i = 0; i < recipes.Count; i++)
		{
			if (CustomSushiRecipeNetworkData.TryCreate(recipes[i], out var result))
			{
				_customSushiRecipesNetwork.Add(result);
			}
		}
		CustomSushiRecipeCatalogChanged?.Invoke();
		ServerSendCustomSushiThumbnails();
	}

	[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
	private void ClientClearCustomSushiThumbnailsRpc()
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
			{
				InvokePermission = RpcInvokePermission.Server
			};
			RpcParams rpcParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(2418819073u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 2418819073u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			CustomSushiThumbnailStore.Clear();
			CustomSushiRecipeCatalogChanged?.Invoke();
		}
	}

	[Rpc(SendTo.Server)]
	private void ServerRequestCustomSushiThumbnailsRpc(RpcParams rpcParams = default(RpcParams))
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
			FastBufferWriter bufferWriter = __beginSendRpc(2157891532u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 2157891532u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsServer)
			{
				ServerSendCustomSushiThumbnails(base.RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
			}
		}
	}

	private void ServerSendCustomSushiThumbnails(RpcParams rpcParams = default(RpcParams))
	{
		if (!base.IsServer || !base.IsSpawned)
		{
			return;
		}
		foreach (CustomSushiRecipeData recipe in _customSushiRecipeCatalog.Recipes)
		{
			byte[] array = CustomSushiThumbnailStore.DecodeSavedJpeg(recipe);
			if (array != null)
			{
				ClientReceiveCustomSushiThumbnailRpc(recipe.RecipeId, array, rpcParams);
			}
			else if (recipe.IconId == "photo")
			{
				UtilityMethods.Log("[RestaurantManager] Saved custom sushi photo is missing or invalid. Recipe=" + recipe.RecipeId, LogType.Warning);
			}
		}
	}

	public void ClientRequestMissingCustomSushiThumbnails()
	{
		if (!base.IsSpawned)
		{
			return;
		}
		foreach (CustomSushiRecipeData recipe in _customSushiRecipeCatalog.Recipes)
		{
			if (!(recipe.IconId != "photo") && !(CustomSushiThumbnailStore.GetIcon(recipe.RecipeId) != null))
			{
				if (base.IsServer)
				{
					ServerSendCustomSushiThumbnails();
				}
				else
				{
					ServerRequestCustomSushiThumbnailsRpc();
				}
				break;
			}
		}
	}

	[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server, AllowTargetOverride = true)]
	private void ClientReceiveCustomSushiThumbnailRpc(FixedString64Bytes recipeId, byte[] jpeg, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
			{
				InvokePermission = RpcInvokePermission.Server,
				AllowTargetOverride = true
			};
			FastBufferWriter bufferWriter = __beginSendRpc(3413436067u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in recipeId, default(FastBufferWriter.ForFixedStrings));
			bool value = jpeg != null;
			bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
			if (value)
			{
				bufferWriter.WriteValueSafe(jpeg, default(FastBufferWriter.ForPrimitives));
			}
			__endSendRpc(ref bufferWriter, 3413436067u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
		{
			if (CustomSushiThumbnailStore.SetIcon(recipeId.ToString(), jpeg))
			{
				CustomSushiRecipeCatalogChanged?.Invoke();
			}
			else
			{
				UtilityMethods.Log($"[RestaurantManager] Custom sushi photo could not be decoded. Recipe={recipeId}", LogType.Warning);
			}
		}
	}

	public bool ServerTryCreateCustomSushiRecipe(string displayName, SushiCompositionData composition, int price, byte[] iconJpeg, out CustomSushiRecipeData recipe, out SushiRecipeDefinition existingRecipe)
	{
		recipe = null;
		existingRecipe = null;
		if (!base.IsServer || composition == null)
		{
			return false;
		}
		SushiRecipeResolver sushiRecipeResolver = new SushiRecipeResolver(_databaseSO, _customSushiRecipeCatalog);
		existingRecipe = sushiRecipeResolver.ResolveExact(composition);
		if (existingRecipe != null)
		{
			return false;
		}
		string text = displayName?.Trim() ?? string.Empty;
		CustomSushiRecipeData existingRecipe2 = null;
		if (Encoding.UTF8.GetByteCount(text) > FixedString128Bytes.UTF8MaxLengthInBytes || !_customSushiRecipeCatalog.TryCreate(text, composition, price, out recipe, out existingRecipe2))
		{
			if (existingRecipe2 != null)
			{
				existingRecipe = SushiRecipeResolver.CreateCustomDefinition(existingRecipe2);
			}
			return false;
		}
		if (CustomSushiThumbnailStore.IsValidJpeg(iconJpeg))
		{
			recipe.IconId = "photo";
			recipe.IconJpegBase64 = Convert.ToBase64String(iconJpeg);
		}
		if (!CustomSushiRecipeNetworkData.TryCreate(recipe, out var result))
		{
			return false;
		}
		_customSushiRecipesNetwork.Add(result);
		CustomSushiRecipeCatalogChanged?.Invoke();
		if (recipe.IconId == "photo")
		{
			ClientReceiveCustomSushiThumbnailRpc(recipe.RecipeId, iconJpeg);
		}
		return true;
	}

	public bool ServerTryAttachCustomSushiThumbnail(string recipeId, byte[] iconJpeg)
	{
		if (!base.IsServer || !CustomSushiThumbnailStore.IsValidJpeg(iconJpeg))
		{
			return false;
		}
		CustomSushiRecipeData customSushiRecipeData = _customSushiRecipeCatalog.FindById(recipeId);
		if (!CustomSushiThumbnailStore.NeedsPhotoRepair(customSushiRecipeData))
		{
			return false;
		}
		customSushiRecipeData.IconId = "photo";
		customSushiRecipeData.IconJpegBase64 = Convert.ToBase64String(iconJpeg);
		for (int i = 0; i < _customSushiRecipesNetwork.Count; i++)
		{
			if (!(_customSushiRecipesNetwork[i].RecipeId.ToString() != recipeId))
			{
				if (CustomSushiRecipeNetworkData.TryCreate(customSushiRecipeData, out var result))
				{
					_customSushiRecipesNetwork[i] = result;
				}
				break;
			}
		}
		CustomSushiRecipeCatalogChanged?.Invoke();
		ClientReceiveCustomSushiThumbnailRpc(customSushiRecipeData.RecipeId, iconJpeg);
		return true;
	}

	public bool ServerTryDeleteCustomSushiRecipe(string recipeId)
	{
		if (!base.IsServer || !_customSushiRecipeCatalog.TryRemove(recipeId, out var _))
		{
			return false;
		}
		for (int num = _customSushiRecipesNetwork.Count - 1; num >= 0; num--)
		{
			if (_customSushiRecipesNetwork[num].RecipeId.ToString() == recipeId)
			{
				_customSushiRecipesNetwork.RemoveAt(num);
			}
		}
		CustomSushiThumbnailStore.RemoveIcon(recipeId);
		if (base.IsSpawned)
		{
			ClientRemoveCustomSushiThumbnailRpc(recipeId);
		}
		CustomSushiRecipeCatalogChanged?.Invoke();
		return true;
	}

	public bool ServerHasPendingTableOrderForRecipe(string recipeId)
	{
		if (!base.IsServer || string.IsNullOrWhiteSpace(recipeId) || _tables == null)
		{
			return false;
		}
		for (int i = 0; i < _tables.Count; i++)
		{
			if (_tables[i] != null && _tables[i].ServerHasOrderForRecipe(recipeId))
			{
				return true;
			}
		}
		return false;
	}

	public bool ServerHasActiveChefDeskRecipe(string recipeId)
	{
		if (!base.IsServer || string.IsNullOrWhiteSpace(recipeId) || _chefDesks == null)
		{
			return false;
		}
		for (int i = 0; i < _chefDesks.Count; i++)
		{
			if (_chefDesks[i] != null && _chefDesks[i].ServerHasActiveRecipe(recipeId))
			{
				return true;
			}
		}
		return false;
	}

	[Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
	private void ClientRemoveCustomSushiThumbnailRpc(FixedString64Bytes recipeId)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
			{
				InvokePermission = RpcInvokePermission.Server
			};
			RpcParams rpcParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(2970539448u, rpcParams, attributeParams, SendTo.NotServer, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in recipeId, default(FastBufferWriter.ForFixedStrings));
			__endSendRpc(ref bufferWriter, 2970539448u, rpcParams, attributeParams, SendTo.NotServer, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			CustomSushiThumbnailStore.RemoveIcon(recipeId.ToString());
			CustomSushiRecipeCatalogChanged?.Invoke();
		}
	}

	public void ServerResetRestaurantTables()
	{
		ServerResetRestaurantTablesForEndDay();
	}

	public void ServerOnRestaurantOpened()
	{
		if (base.IsServer)
		{
			ServerEnsureDailyCustomerFlowInitialized();
			_openingRushTargetOccupied = GetTargetOccupiedSeatingCount();
			_openingRushActive = _openingRushTargetOccupied > 0;
			_customerDemand = true;
			_customerScheduled = false;
			ServerEnsureCustomerScheduled();
			ServerTryNotifyCustomerServiceEnded();
			UtilityMethods.Log($"[SERVER][RestaurantFlow] Opened. targetOccupied={_openingRushTargetOccupied}");
		}
	}

	public void ServerOnRestaurantClosed()
	{
		if (base.IsServer)
		{
			ServerCancelPendingDiningArrivalsForRestaurantClose();
			ServerHandleConveyorTablesForRestaurantClose();
			_customerScheduled = false;
			_customerDemand = false;
			_openingRushActive = false;
		}
	}

	private void ServerCancelPendingDiningArrivalsForRestaurantClose()
	{
		if (_tables == null)
		{
			return;
		}
		for (int i = 0; i < _tables.Count; i++)
		{
			RestaurantTable restaurantTable = _tables[i];
			if (!(restaurantTable == null))
			{
				restaurantTable.ServerCancelPendingArrivalsForRestaurantClose();
			}
		}
	}

	private void ServerHandleConveyorTablesForRestaurantClose()
	{
		if (_conveyorTables == null)
		{
			return;
		}
		for (int i = 0; i < _conveyorTables.Count; i++)
		{
			ConveyorBeltTable conveyorBeltTable = _conveyorTables[i];
			if (!(conveyorBeltTable == null) && (!(conveyorBeltTable.PhysicalStateController != null) || conveyorBeltTable.PhysicalStateController.IsPhysicallyUsable))
			{
				conveyorBeltTable.ServerHandleRestaurantClosed();
			}
		}
	}

	public bool ResidentArrivedToTable(int tableId, int seatIndex, string residentName)
	{
		if (!IsRestaurantOpen)
		{
			UtilityMethods.Log($"[RestaurantManager] ResidentArrivedToTable rejected because restaurant is closed. tableId={tableId}, seatIndex={seatIndex}, resident={residentName}", LogType.Warning);
			return false;
		}
		RestaurantTable restaurantTable = _tables.FirstOrDefault((RestaurantTable t) => t != null && t.Id == tableId);
		if (restaurantTable == null)
		{
			UtilityMethods.Log($"[RestaurantManager] ResidentArrivedToTable: Table not found. tableId={tableId}", LogType.Warning);
			return false;
		}
		return restaurantTable.OnServerCustomerArrived(seatIndex, residentName);
	}

	public bool CanAcceptDiningResident(int tableId, int seatIndex)
	{
		if (!IsRestaurantOpen)
		{
			return false;
		}
		RestaurantTable restaurantTable = _tables.FirstOrDefault((RestaurantTable t) => t != null && t.Id == tableId);
		if (restaurantTable != null)
		{
			return restaurantTable.CanAcceptResidentArrival(seatIndex);
		}
		return false;
	}

	public bool ResidentArrivedToConveyorTable(int tableId)
	{
		if (!IsRestaurantOpen)
		{
			UtilityMethods.Log($"[RestaurantManager] ResidentArrivedToConveyorTable rejected because restaurant is closed. tableId={tableId}", LogType.Warning);
			return false;
		}
		ConveyorBeltTable conveyorBeltTable = _conveyorTables.FirstOrDefault((ConveyorBeltTable t) => t != null && t.SeatId == tableId);
		if (conveyorBeltTable == null)
		{
			UtilityMethods.Log($"[RestaurantManager] ResidentArrivedToConveyorTable: Table not found. tableId={tableId}", LogType.Warning);
			return false;
		}
		return conveyorBeltTable.OnServerCustomerArrived();
	}

	public bool CanAcceptConveyorResident(int tableId)
	{
		if (!IsRestaurantOpen)
		{
			return false;
		}
		ConveyorBeltTable conveyorBeltTable = _conveyorTables.FirstOrDefault((ConveyorBeltTable t) => t != null && t.SeatId == tableId);
		if (conveyorBeltTable != null)
		{
			return conveyorBeltTable.CanAcceptResidentArrival();
		}
		return false;
	}

	public Transform GetSeatTransform(int tableId, int seatIndex)
	{
		RestaurantTable restaurantTable = _tables.FirstOrDefault((RestaurantTable t) => t != null && t.Id == tableId);
		if (restaurantTable != null && restaurantTable.SeatingPoints != null && seatIndex >= 0 && seatIndex < restaurantTable.SeatingPoints.Count)
		{
			return restaurantTable.SeatingPoints[seatIndex];
		}
		UtilityMethods.Log($"[RestaurantManager] Table or seat not found. Table={tableId}, Seat={seatIndex}", LogType.Error);
		return null;
	}

	public Transform GetConveyorSeatTransform(int tableId)
	{
		ConveyorBeltTable conveyorBeltTable = _conveyorTables.FirstOrDefault((ConveyorBeltTable t) => t != null && t.SeatId == tableId);
		if (conveyorBeltTable != null)
		{
			return conveyorBeltTable.CustomerSeatPoint;
		}
		UtilityMethods.Log($"[RestaurantManager] Conveyor table {tableId} seat transform not found!", LogType.Error);
		return null;
	}

	public bool CheckTableIsEmpty(int tableId)
	{
		foreach (RestaurantTable table in _tables)
		{
			if (table.Id == tableId)
			{
				return table.CheckIsTableEmpty();
			}
		}
		return false;
	}

	public void ServerNotifyTableEmptied(int tableId)
	{
		if (base.IsServer)
		{
			UtilityMethods.Log($"[SERVER][RestaurantFlow] Dining table emptied: {tableId}");
			_customerDemand = true;
			ServerEnsureCustomerScheduled();
		}
	}

	public void ServerNotifyConveyorTableEmptied(int seatId)
	{
		if (base.IsServer && _enableConveyorCustomerWaves)
		{
			UtilityMethods.Log($"[SERVER][RestaurantFlow] Conveyor table emptied: {seatId}");
			_customerDemand = true;
			ServerEnsureCustomerScheduled();
		}
	}

	public void ServerAddWavesForNewUnlockedTable()
	{
		ServerNotifySeatingUnlocked();
	}

	public void ServerNotifyConveyorTableUnlocked(int seatId)
	{
		if (base.IsServer)
		{
			UtilityMethods.Log($"[SERVER][RestaurantFlow] Conveyor table unlocked: {seatId}");
			ServerNotifySeatingUnlocked();
		}
	}

	public void ServerNotifySeatingUnlocked()
	{
		if (base.IsServer)
		{
			_openingRushTargetOccupied = GetTargetOccupiedSeatingCount();
			_customerDemand = true;
			ServerEnsureCustomerScheduled();
			UtilityMethods.Log($"[SERVER][RestaurantFlow] Seating unlocked. targetOccupied={_openingRushTargetOccupied}");
		}
	}

	private void ServerOnNewHour()
	{
		ServerTryNotifyCustomerServiceEnded();
		ServerTryNotifyRestaurantOpeningReminder();
		ServerEnsureDailyCustomerFlowInitialized();
		ServerEnsureCustomerScheduled();
		ServerTryExecuteCustomerFlow();
	}

	private void ServerOnNewMinute()
	{
		ServerTryNotifyCustomerServiceEnded();
		ServerTryNotifyRestaurantOpeningReminder();
		ServerEnsureDailyCustomerFlowInitialized();
		ServerEnsureCustomerScheduled();
		ServerTryExecuteCustomerFlow();
	}

	private void ServerTryNotifyCustomerServiceEnded()
	{
		if (base.IsServer && !(_cozyManager == null) && _cozyManager.CurrentTime.hours >= CloseTime)
		{
			int currentDay = _cozyManager.CurrentDay;
			if (_lastCustomerServiceEndedFeedbackDay != currentDay)
			{
				_lastCustomerServiceEndedFeedbackDay = currentDay;
				GlobalChatManager.BroadcastLocalizedRestaurantFromServer(this, "chat.restaurant.no_more_customers", "No more customers will arrive today.", RestaurantChatTone.Warning);
				ClientShowCustomerServiceEndedFeedbackRpc();
			}
		}
	}

	private void ServerTryNotifyRestaurantOpeningReminder()
	{
		if (!base.IsServer || _cozyManager == null)
		{
			return;
		}
		int currentDay = _cozyManager.CurrentDay;
		int minuteOfDay = GetMinuteOfDay();
		int num = 660;
		if (_lastRestaurantOpeningReminderObservedDay == int.MinValue)
		{
			_lastRestaurantOpeningReminderObservedDay = currentDay;
			_lastRestaurantOpeningReminderObservedMinute = minuteOfDay;
			return;
		}
		if (_lastRestaurantOpeningReminderObservedDay != currentDay)
		{
			_lastRestaurantOpeningReminderObservedDay = currentDay;
			_lastRestaurantOpeningReminderObservedMinute = minuteOfDay;
			return;
		}
		bool num2 = _lastRestaurantOpeningReminderObservedMinute < num && minuteOfDay >= num;
		_lastRestaurantOpeningReminderObservedMinute = minuteOfDay;
		if (num2 && !IsRestaurantOpen && _lastRestaurantOpeningReminderDay != currentDay)
		{
			_lastRestaurantOpeningReminderDay = currentDay;
			GlobalChatManager.BroadcastLocalizedRestaurantFromServer(this, "chat.restaurant.closed_reminder", "Customers won't arrive while the restaurant is closed.", RestaurantChatTone.Info);
			ClientShowRestaurantOpeningReminderRpc();
		}
	}

	[Rpc(SendTo.Everyone)]
	private void ClientShowCustomerServiceEndedFeedbackRpc()
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
			FastBufferWriter bufferWriter = __beginSendRpc(2187154040u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 2187154040u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			Singleton<NotifyManager>.Instance?.ShowAlert("RESTAURANT", "No more customers will arrive today.", 7f, AlertPriority.High, autoClose: true, usePriorityPulse: false);
		}
	}

	[Rpc(SendTo.Everyone)]
	private void ClientShowRestaurantOpeningReminderRpc()
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
			FastBufferWriter bufferWriter = __beginSendRpc(38087338u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 38087338u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			Singleton<NotifyManager>.Instance?.ShowAlert("RESTAURANT", "Customers won't arrive while the restaurant is closed.", 7f, AlertPriority.High, autoClose: true, usePriorityPulse: false);
		}
	}

	private void ServerResetDailyCustomerFlow()
	{
		int unlockedSeatingCount = GetUnlockedSeatingCount();
		_customerDemand = false;
		_customerScheduled = false;
		_scheduledCustomerMinuteOfDay = 0;
		_lastCustomerExecutedMinuteOfDay = -1;
		_openingRushActive = false;
		_openingRushTargetOccupied = 0;
		UtilityMethods.Log($"[SERVER][RestaurantFlow] Daily flow reset. unlocked={unlockedSeatingCount}");
	}

	private void ServerEnsureDailyCustomerFlowInitialized()
	{
		if (!base.IsServer || _cozyManager == null)
		{
			return;
		}
		int currentDay = _cozyManager.CurrentDay;
		if (_lastInitializedCustomerDay != currentDay)
		{
			_lastInitializedCustomerDay = currentDay;
			ServerResetDailyCustomerFlow();
			if (IsRestaurantOpen)
			{
				_openingRushTargetOccupied = GetTargetOccupiedSeatingCount();
				_openingRushActive = _openingRushTargetOccupied > 0;
				_customerDemand = GetCurrentOccupiedSeatingCount() < _openingRushTargetOccupied;
			}
			UtilityMethods.Log("[SERVER][RestaurantFlow] Daily customer flow initialized. " + $"day={currentDay}, " + $"open={IsRestaurantOpen}, demand={_customerDemand}, target={_openingRushTargetOccupied}");
		}
	}

	private void ServerEnsureCustomerScheduled()
	{
		if (base.IsServer && _customerDemand && !_customerScheduled && ServerCanCustomerSpawn())
		{
			if (!HasEmptyCustomerSeating())
			{
				_customerDemand = false;
			}
			else if (BuildAvailableWaveTargets() != 0)
			{
				int minuteOfDay = GetMinuteOfDay();
				int num = (_openingRushActive ? _openingRushIntervalMinutes : _sustainCheckIntervalMinutes);
				_customerScheduled = true;
				_scheduledCustomerMinuteOfDay = minuteOfDay + num;
				UtilityMethods.Log($"[SERVER][RestaurantFlow] Scheduled -> {FormatMinuteOfDay(_scheduledCustomerMinuteOfDay)} openingRush={_openingRushActive}");
			}
		}
	}

	private void ServerTryExecuteCustomerFlow()
	{
		if (!base.IsServer || !_customerScheduled)
		{
			return;
		}
		if (!ServerCanCustomerSpawn())
		{
			_customerScheduled = false;
			if (IsRestaurantOpen)
			{
				_customerDemand = true;
			}
		}
		else if (!HasEmptyCustomerSeating())
		{
			_customerScheduled = false;
			_customerDemand = false;
		}
		else
		{
			if (GetMinuteOfDay() < _scheduledCustomerMinuteOfDay)
			{
				return;
			}
			if (_lastCustomerExecutedMinuteOfDay == _scheduledCustomerMinuteOfDay)
			{
				_customerScheduled = false;
				return;
			}
			bool num = ServerExecuteSingleCustomerFlow(out var hadAvailableTargetsAfterAttempt);
			_lastCustomerExecutedMinuteOfDay = _scheduledCustomerMinuteOfDay;
			_customerScheduled = false;
			int currentOccupiedSeatingCount = GetCurrentOccupiedSeatingCount();
			int targetOccupiedSeatingCount = GetTargetOccupiedSeatingCount();
			if (_openingRushActive && currentOccupiedSeatingCount >= targetOccupiedSeatingCount)
			{
				_openingRushActive = false;
				UtilityMethods.Log("[SERVER][RestaurantFlow] Opening rush completed.");
			}
			_customerDemand = currentOccupiedSeatingCount < targetOccupiedSeatingCount;
			if (!num & hadAvailableTargetsAfterAttempt)
			{
				_customerDemand = true;
			}
			if (_customerDemand)
			{
				ServerEnsureCustomerScheduled();
			}
		}
	}

	private bool ServerExecuteSingleCustomerFlow(out bool hadAvailableTargetsAfterAttempt)
	{
		hadAvailableTargetsAfterAttempt = false;
		if (!base.IsServer)
		{
			return false;
		}
		int num = BuildAvailableWaveTargets();
		hadAvailableTargetsAfterAttempt = num > 0;
		if (num == 0)
		{
			UtilityMethods.Log("[SERVER][RestaurantFlow] No available seating target.");
			return false;
		}
		SeatingWaveTarget? seatingWaveTarget = SelectNextWaveTarget();
		if (!seatingWaveTarget.HasValue)
		{
			return false;
		}
		SeatingWaveTarget value = seatingWaveTarget.Value;
		if (value.Type == SeatingWaveTargetType.Dining)
		{
			RestaurantTable diningTable = value.DiningTable;
			if (diningTable == null)
			{
				return false;
			}
			int num2 = (_lastDiningCustomerCount = SelectDiningCustomerCount(diningTable.SeatCount));
			diningTable.ServerChangeSeatedCountRpc(num2);
			diningTable.ServerChangeState(RestaurantTableState.WaitingOtherCustomers);
			int num3 = ((Singleton<ResidentManager>.Instance != null) ? Singleton<ResidentManager>.Instance.CallResidentsForRestaurant(num2, diningTable.Id) : 0);
			if (num3 != num2)
			{
				diningTable.ServerChangeSeatedCountRpc(num3);
			}
			if (num3 <= 0)
			{
				UtilityMethods.Log($"[SERVER][RestaurantFlow] Dining assignment cancelled because no resident could reach the table. TableId={diningTable.Id}, Requested={num2}", LogType.Warning);
				diningTable.ServerChangeState(RestaurantTableState.Empty);
				return false;
			}
			UtilityMethods.Log($"[SERVER][RestaurantFlow] Dining selected. TableId={diningTable.Id}, Requested={num2}, Assigned={num3}");
			return true;
		}
		ConveyorBeltTable conveyorTable = value.ConveyorTable;
		if (conveyorTable == null)
		{
			return false;
		}
		conveyorTable.ServerReserveIncomingCustomer();
		ClientCallConveyorResidentRpc(conveyorTable.SeatId);
		UtilityMethods.Log($"[SERVER][RestaurantFlow] Conveyor selected. TableId={conveyorTable.SeatId}");
		return true;
	}

	private int SelectDiningCustomerCount(int seatCount)
	{
		if (seatCount <= 1)
		{
			return 1;
		}
		return UnityEngine.Random.Range((_lastDiningCustomerCount != 1) ? 1 : 2, seatCount + 1);
	}

	private int BuildAvailableWaveTargets()
	{
		_availableWaveTargets.Clear();
		_availableDiningWaveTargetCount = 0;
		_availableConveyorWaveTargetCount = 0;
		if (ServerHasValidMenu())
		{
			foreach (RestaurantTable table in _tables)
			{
				if (!(table == null) && !(table.PhysicalStateController == null) && table.PhysicalStateController.IsPhysicallyUsable && table.CurrentState == RestaurantTableState.Empty && !table.HasPendingIncomingCustomers)
				{
					_availableWaveTargets.Add(new SeatingWaveTarget
					{
						Type = SeatingWaveTargetType.Dining,
						DiningTable = table
					});
					_availableDiningWaveTargetCount++;
				}
			}
		}
		if (_enableConveyorCustomerWaves)
		{
			foreach (ConveyorBeltTable conveyorTable in _conveyorTables)
			{
				if (!(conveyorTable == null) && !(conveyorTable.PhysicalStateController == null) && conveyorTable.PhysicalStateController.IsPhysicallyUsable && conveyorTable.CurrentState == ConveyorBeltTableState.Empty && !conveyorTable.HasIncomingCustomer)
				{
					_availableWaveTargets.Add(new SeatingWaveTarget
					{
						Type = SeatingWaveTargetType.Conveyor,
						ConveyorTable = conveyorTable
					});
					_availableConveyorWaveTargetCount++;
				}
			}
		}
		return _availableWaveTargets.Count;
	}

	private bool HasEmptyCustomerSeating()
	{
		if (HasEmptyDiningTable())
		{
			return true;
		}
		if (_enableConveyorCustomerWaves)
		{
			return HasEmptyConveyorTable();
		}
		return false;
	}

	private bool HasEmptyDiningTable()
	{
		if (_tables == null)
		{
			return false;
		}
		for (int i = 0; i < _tables.Count; i++)
		{
			RestaurantTable restaurantTable = _tables[i];
			if (!(restaurantTable == null) && !(restaurantTable.PhysicalStateController == null) && restaurantTable.PhysicalStateController.IsPhysicallyUsable && restaurantTable.CurrentState == RestaurantTableState.Empty && !restaurantTable.HasPendingIncomingCustomers)
			{
				return true;
			}
		}
		return false;
	}

	private bool HasEmptyConveyorTable()
	{
		if (_conveyorTables == null)
		{
			return false;
		}
		for (int i = 0; i < _conveyorTables.Count; i++)
		{
			ConveyorBeltTable conveyorBeltTable = _conveyorTables[i];
			if (!(conveyorBeltTable == null) && !(conveyorBeltTable.PhysicalStateController == null) && conveyorBeltTable.PhysicalStateController.IsPhysicallyUsable && conveyorBeltTable.CurrentState == ConveyorBeltTableState.Empty && !conveyorBeltTable.HasIncomingCustomer)
			{
				return true;
			}
		}
		return false;
	}

	private SeatingWaveTarget? SelectNextWaveTarget()
	{
		if (_availableWaveTargets.Count == 0)
		{
			return null;
		}
		bool flag = _availableDiningWaveTargetCount > 0;
		bool flag2 = _availableConveyorWaveTargetCount > 0;
		if (flag & flag2)
		{
			if (UnityEngine.Random.value < _diningSelectionWeight)
			{
				return GetWaveTargetAtIndex(SeatingWaveTargetType.Dining, UnityEngine.Random.Range(0, _availableDiningWaveTargetCount));
			}
			return GetWaveTargetAtIndex(SeatingWaveTargetType.Conveyor, UnityEngine.Random.Range(0, _availableConveyorWaveTargetCount));
		}
		if (flag)
		{
			return GetWaveTargetAtIndex(SeatingWaveTargetType.Dining, UnityEngine.Random.Range(0, _availableDiningWaveTargetCount));
		}
		if (flag2)
		{
			return GetWaveTargetAtIndex(SeatingWaveTargetType.Conveyor, UnityEngine.Random.Range(0, _availableConveyorWaveTargetCount));
		}
		return null;
	}

	private SeatingWaveTarget? GetWaveTargetAtIndex(SeatingWaveTargetType type, int targetIndex)
	{
		int num = 0;
		for (int i = 0; i < _availableWaveTargets.Count; i++)
		{
			SeatingWaveTarget value = _availableWaveTargets[i];
			if (value.Type == type)
			{
				if (num == targetIndex)
				{
					return value;
				}
				num++;
			}
		}
		return null;
	}

	private bool ServerHasValidMenu()
	{
		if (TryGetTabletNetworkHandler(out var tabletNetworkHandler))
		{
			return tabletNetworkHandler.HasSushiOnMenu;
		}
		return false;
	}

	public void RefreshLocalMenuAvailabilityAlert()
	{
		NotifyManager instance = Singleton<NotifyManager>.Instance;
		if (!(instance == null) && TryGetTabletNetworkHandler(out var tabletNetworkHandler))
		{
			if (IsRestaurantOpen && !tabletNetworkHandler.HasSushiOnMenu)
			{
				instance.ShowAlert("MENU IS EMPTY", "Add at least one sushi to the menu. Dining customers will not arrive.", -1f, AlertPriority.Low, autoClose: false);
			}
			else
			{
				TryHideEmptyMenuAlert();
			}
		}
	}

	private bool TryGetTabletNetworkHandler(out TabletNetworkHandler tabletNetworkHandler)
	{
		if (ServiceLocator.ForSceneOf(this).TryGet<TabletNetworkHandler>(out tabletNetworkHandler))
		{
			return tabletNetworkHandler != null;
		}
		return false;
	}

	private static void TryHideEmptyMenuAlert()
	{
		Singleton<NotifyManager>.Instance?.TryHideAlert("MENU IS EMPTY", "Add at least one sushi to the menu. Dining customers will not arrive.");
	}

	public bool ServerCanCustomerSpawn()
	{
		int hours = _cozyManager.CurrentTime.hours;
		if (IsRestaurantOpen && hours >= OpenTime)
		{
			return hours < CloseTime;
		}
		return false;
	}

	private int GetMinuteOfDay()
	{
		int hours = _cozyManager.CurrentTime.hours;
		int minutes = _cozyManager.CurrentTime.minutes;
		return hours * 60 + minutes;
	}

	private string FormatMinuteOfDay(int minuteOfDay)
	{
		minuteOfDay %= 1440;
		if (minuteOfDay < 0)
		{
			minuteOfDay += 1440;
		}
		int num = minuteOfDay / 60;
		int num2 = minuteOfDay % 60;
		return $"{num:00}:{num2:00}";
	}

	private int GetTargetOccupiedSeatingCount()
	{
		return GetUnlockedSeatingCount();
	}

	public int GetUnlockedDiningTableCount()
	{
		int num = 0;
		foreach (RestaurantTable table in _tables)
		{
			if (table != null && table.PhysicalStateController.IsPhysicallyUsable)
			{
				num++;
			}
		}
		return num;
	}

	public void ServerMarkDiningTableUnlocked(int tableId)
	{
		if (base.IsServer)
		{
			if (_restaurantUpgradeData == null)
			{
				_restaurantUpgradeData = new RestaurantUpgradeData();
			}
			RestaurantUpgradeData restaurantUpgradeData = _restaurantUpgradeData;
			if (restaurantUpgradeData.UnlockedTableIds == null)
			{
				restaurantUpgradeData.UnlockedTableIds = new List<byte>();
			}
			byte item = (byte)tableId;
			if (!_restaurantUpgradeData.UnlockedTableIds.Contains(item))
			{
				_restaurantUpgradeData.UnlockedTableIds.Add(item);
			}
			if (!_unlockedDiningTableIds.Contains(item))
			{
				_unlockedDiningTableIds.Add(item);
			}
		}
	}

	public void ServerRecordDiningTableOpenState(int tableId, bool isOpen)
	{
		if (!base.IsServer)
		{
			return;
		}
		if (_restaurantUpgradeData == null)
		{
			_restaurantUpgradeData = new RestaurantUpgradeData();
		}
		RestaurantUpgradeData restaurantUpgradeData = _restaurantUpgradeData;
		if (restaurantUpgradeData.ClosedDiningTableIds == null)
		{
			restaurantUpgradeData.ClosedDiningTableIds = new List<byte>();
		}
		byte item = (byte)tableId;
		List<byte> unlockedTableIds = _restaurantUpgradeData.UnlockedTableIds;
		if (unlockedTableIds == null || !unlockedTableIds.Contains(item))
		{
			UtilityMethods.Log($"[RestaurantManager] Dining table open state was not recorded because the table is not permanently unlocked. TableId={tableId}", LogType.Warning);
			return;
		}
		_restaurantUpgradeData.HasExplicitDiningTableOpenState = true;
		if (isOpen)
		{
			while (_restaurantUpgradeData.ClosedDiningTableIds.Remove(item))
			{
			}
		}
		else if (!_restaurantUpgradeData.ClosedDiningTableIds.Contains(item))
		{
			_restaurantUpgradeData.ClosedDiningTableIds.Add(item);
		}
	}

	public bool IsDiningTablePermanentlyUnlocked(int tableId)
	{
		byte item = (byte)tableId;
		if (_unlockedDiningTableIds.Contains(item))
		{
			return true;
		}
		if (base.IsServer)
		{
			RestaurantUpgradeData restaurantUpgradeData = _restaurantUpgradeData;
			if (restaurantUpgradeData == null)
			{
				return false;
			}
			return restaurantUpgradeData.UnlockedTableIds?.Contains(item) == true;
		}
		return false;
	}

	public int GetPermanentlyUnlockedDiningTableCount()
	{
		HashSet<byte> hashSet = new HashSet<byte>();
		for (int i = 0; i < _unlockedDiningTableIds.Count; i++)
		{
			hashSet.Add(_unlockedDiningTableIds[i]);
		}
		if (base.IsServer && _restaurantUpgradeData?.UnlockedTableIds != null)
		{
			foreach (byte unlockedTableId in _restaurantUpgradeData.UnlockedTableIds)
			{
				hashSet.Add(unlockedTableId);
			}
		}
		return hashSet.Count;
	}

	public int GetUnlockedConveyorTableCount()
	{
		int num = 0;
		foreach (ConveyorBeltTable conveyorTable in _conveyorTables)
		{
			if (conveyorTable != null && conveyorTable.PhysicalStateController.IsPhysicallyUsable)
			{
				num++;
			}
		}
		return num;
	}

	private int GetUnlockedSeatingCount()
	{
		int unlockedDiningTableCount = GetUnlockedDiningTableCount();
		int num = (_enableConveyorCustomerWaves ? GetUnlockedConveyorTableCount() : 0);
		return unlockedDiningTableCount + num;
	}

	private int GetOccupiedDiningTableCount()
	{
		int num = 0;
		foreach (RestaurantTable table in _tables)
		{
			if (!(table == null) && table.PhysicalStateController.IsPhysicallyUsable && table.CurrentState != RestaurantTableState.Empty)
			{
				num++;
			}
		}
		return num;
	}

	private int GetOccupiedConveyorTableCount()
	{
		if (!_enableConveyorCustomerWaves)
		{
			return 0;
		}
		int num = 0;
		foreach (ConveyorBeltTable conveyorTable in _conveyorTables)
		{
			if (!(conveyorTable == null) && conveyorTable.PhysicalStateController.IsPhysicallyUsable && conveyorTable.CurrentState != ConveyorBeltTableState.Empty)
			{
				num++;
			}
		}
		return num;
	}

	private int GetCurrentOccupiedSeatingCount()
	{
		return GetOccupiedDiningTableCount() + GetOccupiedConveyorTableCount();
	}

	[Rpc(SendTo.Everyone)]
	private void ClientCallConveyorResidentRpc(int tableId)
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
			FastBufferWriter bufferWriter = __beginSendRpc(4268695144u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, tableId);
			__endSendRpc(ref bufferWriter, 4268695144u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			Singleton<ResidentManager>.Instance.CallResidentForConveyorRestaurant(tableId);
		}
	}

	[Rpc(SendTo.Server)]
	private void ServerRequestStockSnapshotRpc(RpcParams rpcParams = default(RpcParams))
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
			FastBufferWriter bufferWriter = __beginSendRpc(1561095352u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 1561095352u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		if (!base.IsServer)
		{
			return;
		}
		ulong senderClientId = rpcParams.Receive.SenderClientId;
		StockSnapshotEntry[] array = new StockSnapshotEntry[_itemsInStock.Count];
		int num = 0;
		foreach (KeyValuePair<string, int> item in _itemsInStock)
		{
			array[num] = new StockSnapshotEntry(item.Key, item.Value);
			num++;
		}
		ClientReceiveStockSnapshotRpc(array, base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp));
	}

	[Rpc(SendTo.SpecifiedInParams)]
	private void ClientReceiveStockSnapshotRpc(StockSnapshotEntry[] snapshot, RpcParams rpcParams = default(RpcParams))
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
			FastBufferWriter bufferWriter = __beginSendRpc(1800974908u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			bool value = snapshot != null;
			bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
			if (value)
			{
				bufferWriter.WriteValueSafe(snapshot, default(FastBufferWriter.ForNetworkSerializable));
			}
			__endSendRpc(ref bufferWriter, 1800974908u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		if (base.IsServer)
		{
			return;
		}
		_itemsInStock.Clear();
		if (snapshot == null)
		{
			return;
		}
		for (int i = 0; i < snapshot.Length; i++)
		{
			StockSnapshotEntry stockSnapshotEntry = snapshot[i];
			FixedString64Bytes itemId = stockSnapshotEntry.ItemId;
			string text = itemId.ToString();
			if (!string.IsNullOrEmpty(text))
			{
				_itemsInStock[text] = stockSnapshotEntry.Amount;
				InvokeStockChanged(text, stockSnapshotEntry.Amount);
			}
		}
		UtilityMethods.Log($"[RestaurantManager] Stock snapshot synced. Count={_itemsInStock.Count}");
	}

	[Rpc(SendTo.Server)]
	public void ServerAddStockRpc(FixedString64Bytes itemId, int amount)
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
			FastBufferWriter bufferWriter = __beginSendRpc(4252636073u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in itemId, default(FastBufferWriter.ForFixedStrings));
			BytePacker.WriteValueBitPacked(bufferWriter, amount);
			__endSendRpc(ref bufferWriter, 4252636073u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		if (base.IsServer && !string.IsNullOrEmpty(itemId.ToString()) && amount != 0)
		{
			if (_itemsInStock.ContainsKey(itemId.ToString()))
			{
				_itemsInStock[itemId.ToString()] += amount;
			}
			else
			{
				_itemsInStock[itemId.ToString()] = amount;
			}
			InvokeStockChanged(itemId.ToString(), _itemsInStock[itemId.ToString()]);
			ClientSyncStockRpc(itemId, _itemsInStock[itemId.ToString()]);
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerRemoveStockRpc(FixedString64Bytes itemId, int amount)
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
			FastBufferWriter bufferWriter = __beginSendRpc(2882172893u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in itemId, default(FastBufferWriter.ForFixedStrings));
			BytePacker.WriteValueBitPacked(bufferWriter, amount);
			__endSendRpc(ref bufferWriter, 2882172893u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		if (base.IsServer && _itemsInStock.ContainsKey(itemId.ToString()))
		{
			_itemsInStock[itemId.ToString()] -= amount;
			if (_itemsInStock[itemId.ToString()] < 0)
			{
				_itemsInStock[itemId.ToString()] = 0;
			}
			InvokeStockChanged(itemId.ToString(), _itemsInStock[itemId.ToString()]);
			ClientSyncStockRpc(itemId, _itemsInStock[itemId.ToString()]);
		}
	}

	[Rpc(SendTo.NotServer)]
	public void ClientSyncStockRpc(FixedString64Bytes itemId, int amount)
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
			FastBufferWriter bufferWriter = __beginSendRpc(4214869977u, rpcParams, attributeParams, SendTo.NotServer, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in itemId, default(FastBufferWriter.ForFixedStrings));
			BytePacker.WriteValueBitPacked(bufferWriter, amount);
			__endSendRpc(ref bufferWriter, 4214869977u, rpcParams, attributeParams, SendTo.NotServer, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			_itemsInStock[itemId.ToString()] = amount;
			InvokeStockChanged(itemId.ToString(), amount);
		}
	}

	public int GetStockedItemAmount(string itemId)
	{
		if (!_itemsInStock.TryGetValue(itemId, out var value))
		{
			return 0;
		}
		return value;
	}

	public bool ServerTryConsumeStock(string itemId, int amount)
	{
		if (!base.IsServer || string.IsNullOrEmpty(itemId) || amount <= 0)
		{
			return false;
		}
		if (!_itemsInStock.TryGetValue(itemId, out var value) || value < amount)
		{
			return false;
		}
		int num = value - amount;
		_itemsInStock[itemId] = num;
		InvokeStockChanged(itemId, num);
		ClientSyncStockRpc(itemId, num);
		return true;
	}

	private void InvokeStockChanged(string itemId, int amount)
	{
		StockChanged?.Invoke(itemId, amount);
	}

	protected override void OnInSceneObjectsSpawned()
	{
		base.OnInSceneObjectsSpawned();
		if (!base.IsServer)
		{
			ServerRequestStockSnapshotRpc();
		}
		else
		{
			ServerLoadRestaurantAfterInSceneObjectsSpawned();
		}
	}

	private void ServerLoadRestaurantAfterInSceneObjectsSpawned()
	{
		if (_serverRestaurantLoaded)
		{
			return;
		}
		_serverRestaurantLoaded = true;
		if (Singleton<SaveManager>.Instance == null || !Singleton<SaveManager>.Instance.TryGetCurrentSaveSlot(out var data) || data == null)
		{
			UtilityMethods.Log("[RestaurantManager] Restaurant load skipped. Current save slot is invalid.", LogType.Error);
			return;
		}
		UtilityMethods.Log("[RestaurantManager] Loading restaurant after in-scene NetworkObjects spawned.");
		Load(data.RestaurantData);
		ServerEnsureDailyCustomerFlowInitialized();
		ServerTryNotifyCustomerServiceEnded();
		if (IsRestaurantOpen)
		{
			ServerOnRestaurantOpened();
		}
		ServerScheduleTimeEvents();
	}

	private void ServerScheduleTimeEvents()
	{
		if (_serverTimeEventsScheduled)
		{
			return;
		}
		_serverTimeEventsScheduled = true;
		Singleton<DelayHelper>.Instance.DelayAction(10f, () =>
		{
			if (base.IsServer && base.IsSpawned)
			{
				CozyWeather.Events.onNewHour -= ServerOnNewHour;
				CozyWeather.Events.onNewMinute -= ServerOnNewMinute;
				CozyWeather.Events.onNewHour += ServerOnNewHour;
				CozyWeather.Events.onNewMinute += ServerOnNewMinute;
			}
		});
	}

	public RestaurantData Save()
	{
		SaveRestaurantUpgradeData();
		return new RestaurantData
		{
			BeverageData = SaveValueSafely("BeverageData", () => (!(_beverageFridge != null)) ? new List<byte>() : _beverageFridge.Save(), new List<byte>()),
			GastropanData = SaveListSafely(_gastropans, "GastropanData", (GastroPan g) => (!g.IsStaffCarried) ? g.Save() : null),
			IngredientData = SaveListSafely(_ingredientEntities, "IngredientData", (IngredientEntity ingredient) => (!ingredient.IsPlayerInventoryOwnedForPersistence && !ingredient.IsStaffCarried) ? ingredient.Save() : null),
			DeadFishData = SaveListSafely(_deadFishes, "DeadFishData", (DeadFish fish) => (!fish.IsPlayerInventoryOwnedForPersistence && !fish.IsStaffCarried) ? fish.Save() : null),
			WholesaleData = SaveListSafely(_wholeSaleItemsInStock, "WholesaleData", (WholesaleEntity w) => (!w.IsStaffCarried) ? w.Save() : null),
			ChefDeskGastropanData = SaveValueSafely("ChefDeskGastropanData", SaveChefDeskGastropans, new List<ChefDeskGastropanData>()),
			RestaurantUpgradeData = (_restaurantUpgradeData ?? new RestaurantUpgradeData()),
			RestaurantSignCustomizationData = SaveValueSafely("RestaurantSignCustomizationData", () => (!(_restaurantSignManager != null)) ? new RestaurantSignCustomizationData() : _restaurantSignManager.Save(), new RestaurantSignCustomizationData()),
			RestaurantDirtData = SaveValueSafely("RestaurantDirtData", () => (!(_holdableDirtManager != null)) ? new RestaurantDirtData() : _holdableDirtManager.Save(), new RestaurantDirtData()),
			ServedDishData = SaveListSafely(_servedDishes, "ServedDishData", (ServedDish d) => (!d.IsStaffCarried) ? d.Save() : null),
			FridgeData = SaveValueSafely("FridgeData", () => (!(_fishFridge != null)) ? new FridgeData() : _fishFridge.Save(), new FridgeData()),
			RadioData = SaveValueSafely("RadioData", () => (!(_radio != null)) ? new RadioData() : _radio.Save(), new RadioData()),
			FishTrophyData = SaveValueSafely("FishTrophyData", () => (!(_fishTrophy != null)) ? new FishTrophyData() : _fishTrophy.Save(), new FishTrophyData()),
			CustomSushiRecipes = _customSushiRecipeCatalog.CreateSaveSnapshot(),
			NextCustomSushiRecipeSequence = _customSushiRecipeCatalog.NextSequence
		};
	}

	private static T SaveValueSafely<T>(string label, Func<T> saveFunc, T fallback) where T : class
	{
		try
		{
			T val = saveFunc();
			if (val != null)
			{
				return val;
			}
			UtilityMethods.Log("[RestaurantManager] " + label + " save returned null. Using empty fallback.", LogType.Warning);
			return fallback;
		}
		catch (Exception arg)
		{
			UtilityMethods.Log($"[RestaurantManager] {label} save failed. Using empty fallback. Exception={arg}", LogType.Error);
			return fallback;
		}
	}

	private static List<TData> SaveListSafely<TSource, TData>(IEnumerable<TSource> source, string label, Func<TSource, TData> saveFunc) where TData : class
	{
		List<TData> list = new List<TData>();
		if (source == null)
		{
			return list;
		}
		int num = 0;
		foreach (TSource item in source)
		{
			try
			{
				if (item == null)
				{
					continue;
				}
				TData val = saveFunc(item);
				if (val != null)
				{
					list.Add(val);
				}
				goto IL_0060;
			}
			catch (Exception arg)
			{
				UtilityMethods.Log($"[RestaurantManager] {label}[{num}] save skipped. Exception={arg}", LogType.Error);
				goto IL_0060;
			}
			IL_0060:
			num++;
		}
		return list;
	}

	public void Load(RestaurantData data)
	{
		if (data == null)
		{
			data = new RestaurantData();
		}
		RestaurantData restaurantData = data;
		if (restaurantData.GastropanData == null)
		{
			restaurantData.GastropanData = new List<GastropanData>();
		}
		restaurantData = data;
		if (restaurantData.IngredientData == null)
		{
			restaurantData.IngredientData = new List<IngredientData>();
		}
		restaurantData = data;
		if (restaurantData.DeadFishData == null)
		{
			restaurantData.DeadFishData = new List<DeadFishData>();
		}
		restaurantData = data;
		if (restaurantData.WholesaleData == null)
		{
			restaurantData.WholesaleData = new List<WholesaleData>();
		}
		restaurantData = data;
		if (restaurantData.ChefDeskGastropanData == null)
		{
			restaurantData.ChefDeskGastropanData = new List<ChefDeskGastropanData>();
		}
		restaurantData = data;
		if (restaurantData.ServedDishData == null)
		{
			restaurantData.ServedDishData = new List<ServedDishData>();
		}
		restaurantData = data;
		if (restaurantData.CustomSushiRecipes == null)
		{
			restaurantData.CustomSushiRecipes = new List<CustomSushiRecipeData>();
		}
		_customSushiRecipeCatalog.Load(data.CustomSushiRecipes, data.NextCustomSushiRecipeSequence);
		if (base.IsServer)
		{
			ServerSynchronizeCustomSushiRecipeCatalog();
		}
		_restaurantUpgradeData = data.RestaurantUpgradeData;
		LoadRestaurantUpgradeData();
		if (base.IsServer && Singleton<SaveManager>.Instance != null)
		{
			data = Singleton<SaveManager>.Instance.ApplyTemporaryStaffStackMigration(data);
		}
		_radio?.Load(data.RadioData);
		_fishFridge?.Load(data.FridgeData);
		_beverageFridge?.Load(data.BeverageData);
		_restaurantSignManager?.Load(data.RestaurantSignCustomizationData);
		_holdableDirtManager?.Load(data.RestaurantDirtData);
		_fishTrophy?.Load(data.FishTrophyData);
		if (data.RestaurantSignCustomizationData != null && !data.RestaurantSignCustomizationData.Repaired)
		{
			Singleton<OffscreenIndicatorHandler>.Instance.ShowUntilReached(_restaurantIndicator);
		}
		foreach (GastropanData gastropanDatum in data.GastropanData)
		{
			if (gastropanDatum != null)
			{
				_gastroPanPrefab.TryInstantiateAndSpawn(out var instance, gastropanDatum.TransformData.Position, Quaternion.Euler(gastropanDatum.TransformData.Rotation));
				instance.GetComponent<GastroPan>().Load(gastropanDatum);
			}
		}
		foreach (IngredientData ingredientDatum in data.IngredientData)
		{
			if (ingredientDatum == null)
			{
				continue;
			}
			if (ingredientDatum.TransformData == null)
			{
				UtilityMethods.Log("[RestaurantManager] Ingredient load skipped because transform data is missing. IngredientId=" + ingredientDatum.IngredientId, LogType.Warning);
				continue;
			}
			if (_filletMachine != null && _filletMachine.IsSavedIngredientAtOutput(ingredientDatum.TransformData.Position))
			{
				UtilityMethods.Log("[RestaurantManager] Removed stale fillet-machine output ingredient from save. IngredientId=" + ingredientDatum.IngredientId, LogType.Warning);
				continue;
			}
			ItemSO itemSO = _databaseSO.FindById(ingredientDatum.IngredientId);
			if (itemSO != null && itemSO.Prefab != null)
			{
				itemSO.Prefab.TryInstantiateAndSpawn(out var instance2, ingredientDatum.TransformData.Position, Quaternion.Euler(ingredientDatum.TransformData.Rotation));
				IngredientEntity component = instance2.GetComponent<IngredientEntity>();
				if (component != null)
				{
					component.Load(ingredientDatum);
				}
			}
		}
		foreach (DeadFishData deadFishDatum in data.DeadFishData)
		{
			if (deadFishDatum != null)
			{
				FishSO fishSO = _databaseSO.FindById(deadFishDatum.FishId) as FishSO;
				if (fishSO != null)
				{
					fishSO.DeadPrefab.TryInstantiateAndSpawn(out var instance3, deadFishDatum.TransformData.Position, Quaternion.Euler(deadFishDatum.TransformData.Rotation));
					instance3.GetComponent<DeadFish>().Load(deadFishDatum);
				}
			}
		}
		foreach (WholesaleData wholesaleDatum in data.WholesaleData)
		{
			if (wholesaleDatum != null)
			{
				WholesaleSO wholesaleSO = _databaseSO.FindById(wholesaleDatum.Id) as WholesaleSO;
				if (wholesaleSO != null)
				{
					wholesaleSO.Prefab.TryInstantiateAndSpawn(out var instance4, wholesaleDatum.TransformData.Position, Quaternion.Euler(wholesaleDatum.TransformData.Rotation));
					instance4.GetComponent<WholesaleEntity>().Load(wholesaleDatum);
				}
			}
		}
		LoadChefDeskGastropans(data.ChefDeskGastropanData);
		foreach (ServedDishData servedDishDatum in data.ServedDishData)
		{
			if (servedDishDatum == null)
			{
				continue;
			}
			ServedDishData servedDishData = servedDishDatum;
			if (servedDishData.TransformData == null)
			{
				servedDishData.TransformData = new TransformData();
			}
			NetworkObject instance5;
			if (servedDishDatum.CustomSushi?.Composition != null)
			{
				NetworkObject networkObject = ResolveCustomSushiPhysicalPrefab(servedDishDatum.CustomSushi.Composition.SushiType);
				if (networkObject == null)
				{
					UtilityMethods.Log($"[RestaurantManager] Custom sushi restore skipped; prefab missing. Type={servedDishDatum.CustomSushi.Composition.SushiType}", LogType.Warning);
					continue;
				}
				instance5 = UnityEngine.Object.Instantiate(networkObject, servedDishDatum.TransformData.Position, Quaternion.Euler(servedDishDatum.TransformData.Rotation));
				if (!instance5.TryGetComponent<ServedDishSushi>(out var component2) || !component2.ServerInitializeCustomSushi(servedDishDatum.CustomSushi.Composition, servedDishDatum.CustomSushi.IdentityKind, servedDishDatum.CustomSushi.RecipeId, servedDishDatum.CustomSushi.DisplayName, servedDishDatum.CustomSushi.Price))
				{
					UnityEngine.Object.Destroy(instance5.gameObject);
					continue;
				}
				instance5.Spawn(destroyWithScene: true);
			}
			else
			{
				ServedDishSO servedDishSO = _databaseSO.FindById(servedDishDatum.Id) as ServedDishSO;
				if (servedDishSO?.Prefab == null)
				{
					continue;
				}
				servedDishSO.Prefab.TryInstantiateAndSpawn(out instance5, servedDishDatum.TransformData.Position, Quaternion.Euler(servedDishDatum.TransformData.Rotation));
			}
			ServedDish component3 = instance5.GetComponent<ServedDish>();
			if (!(component3 == null))
			{
				component3.Load(servedDishDatum);
				if (servedDishDatum.HasTrayAttachment && TryGetTrayBySaveLocation(servedDishDatum.TrayTrolleyIndex, servedDishDatum.TraySlotIndex, out var tray))
				{
					tray.ServerRestoreDishInSlot(component3, servedDishDatum.DishSlotIndex);
				}
				else if (servedDishDatum.HasConveyorAttachment && (_conveyorBelt == null || !_conveyorBelt.ServerRestoreDishToSlot(component3, servedDishDatum.ConveyorSlotIndex)))
				{
					UtilityMethods.Log("[RestaurantManager] Conveyor dish restore failed. " + $"DishId={servedDishDatum.Id} Slot={servedDishDatum.ConveyorSlotIndex}", LogType.Warning);
				}
			}
		}
	}

	public NetworkObject ResolveCustomSushiPhysicalPrefab(SushiType sushiType)
	{
		if (_chefDesks == null)
		{
			return null;
		}
		for (int i = 0; i < _chefDesks.Count; i++)
		{
			ChefDesk chefDesk = _chefDesks[i];
			NetworkObject networkObject = ((chefDesk != null) ? chefDesk.GetCustomPhysicalPrefab(sushiType) : null);
			if (networkObject != null)
			{
				return networkObject;
			}
		}
		return null;
	}

	public Sprite ResolveCustomSushiPlaceholderIcon(SushiType sushiType)
	{
		NetworkObject networkObject = ResolveCustomSushiPhysicalPrefab(sushiType);
		if (!(networkObject != null) || !networkObject.TryGetComponent<ServedDish>(out var component))
		{
			return null;
		}
		return component.ItemSO?.Sprite;
	}

	public bool TryGetTraySaveLocation(Tray tray, out int trolleyIndex, out int traySlotIndex)
	{
		trolleyIndex = -1;
		traySlotIndex = -1;
		if (tray == null || _trolleys == null)
		{
			return false;
		}
		for (int i = 0; i < _trolleys.Count; i++)
		{
			TrayTrolley trayTrolley = _trolleys[i];
			if (!(trayTrolley == null) && trayTrolley.TryGetTraySlotIndex(tray, out var slotIndex))
			{
				trolleyIndex = i;
				traySlotIndex = slotIndex;
				return true;
			}
		}
		return false;
	}

	public bool TryGetConveyorSaveSlot(ServedDish servedDish, out int slotIndex)
	{
		slotIndex = -1;
		if (_conveyorBelt != null)
		{
			return _conveyorBelt.TryGetSaveSlot(servedDish, out slotIndex);
		}
		return false;
	}

	public bool ClientTryDebugFillConveyorBelt(out string message)
	{
		if (_conveyorBelt == null || !_conveyorBelt.IsSpawned)
		{
			message = "Conveyor belt is not available.";
			return false;
		}
		if (_conveyorBelt.PhysicalStateController == null || !_conveyorBelt.PhysicalStateController.IsPhysicallyUsable)
		{
			message = "Conveyor belt must be repaired first.";
			return false;
		}
		List<SushiSO> list = BuildDebugConveyorSushiPool();
		if (list.Count == 0)
		{
			message = "No valid sushi prefabs found in the item database.";
			return false;
		}
		if (base.IsServer)
		{
			int num = _conveyorBelt.ServerDebugReplaceAllWithRandomSushi(list);
			message = $"Filled conveyor belt with {num} random sushi.";
			return num > 0;
		}
		ServerDebugFillConveyorBeltRpc();
		message = "Conveyor belt fill requested.";
		return true;
	}

	[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
	private void ServerDebugFillConveyorBeltRpc()
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
			{
				InvokePermission = RpcInvokePermission.Everyone
			};
			RpcParams rpcParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(3625287616u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 3625287616u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_conveyorBelt == null || !_conveyorBelt.IsSpawned)
			{
				UtilityMethods.Log("[RestaurantManager][BeltDebug] Fill failed. Conveyor belt is not available.", LogType.Warning);
				return;
			}
			List<SushiSO> list = BuildDebugConveyorSushiPool();
			int num = _conveyorBelt.ServerDebugReplaceAllWithRandomSushi(list);
			UtilityMethods.Log($"[RestaurantManager][BeltDebug] Fill completed. Placed={num} Pool={list.Count}");
		}
	}

	private List<SushiSO> BuildDebugConveyorSushiPool()
	{
		if (_databaseSO == null)
		{
			return new List<SushiSO>();
		}
		return (from sushi in _databaseSO.GetItemsByItemType(ItemType.ServedDish).OfType<SushiSO>()
			where sushi != null && sushi.Prefab != null && sushi.Prefab.GetComponent<ServedDishSushi>() != null
			select sushi).ToList();
	}

	public bool TryGetTrayBySaveLocation(int trolleyIndex, int traySlotIndex, out Tray tray)
	{
		tray = null;
		if (_trolleys == null || trolleyIndex < 0 || trolleyIndex >= _trolleys.Count)
		{
			return false;
		}
		TrayTrolley trayTrolley = _trolleys[trolleyIndex];
		if (trayTrolley != null)
		{
			return trayTrolley.TryGetTrayBySlotIndex(traySlotIndex, out tray);
		}
		return false;
	}

	private List<ChefDeskGastropanData> SaveChefDeskGastropans()
	{
		List<ChefDeskGastropanData> list = new List<ChefDeskGastropanData>();
		if (_chefDesks != null)
		{
			for (int i = 0; i < _chefDesks.Count; i++)
			{
				ChefDesk chefDesk = _chefDesks[i];
				if (chefDesk == null || chefDesk.GastroPans == null)
				{
					continue;
				}
				for (int j = 0; j < chefDesk.GastroPans.Count; j++)
				{
					ChefDeskGastroPan chefDeskGastroPan = chefDesk.GastroPans[j];
					if (!(chefDeskGastroPan == null))
					{
						ChefDeskGastropanData chefDeskGastropanData = chefDeskGastroPan.Save();
						chefDeskGastropanData.HasChefDeskIndex = true;
						chefDeskGastropanData.ChefDeskIndex = (byte)i;
						chefDeskGastropanData.Index = (byte)j;
						list.Add(chefDeskGastropanData);
					}
				}
			}
		}
		if (list.Count > 0 || _chefDeskGastropans == null)
		{
			return list;
		}
		for (int k = 0; k < _chefDeskGastropans.Count; k++)
		{
			ChefDeskGastroPan chefDeskGastroPan2 = _chefDeskGastropans[k];
			if (!(chefDeskGastroPan2 == null))
			{
				ChefDeskGastropanData chefDeskGastropanData2 = chefDeskGastroPan2.Save();
				chefDeskGastropanData2.HasChefDeskIndex = false;
				list.Add(chefDeskGastropanData2);
			}
		}
		return list;
	}

	private void LoadChefDeskGastropans(List<ChefDeskGastropanData> panData)
	{
		if (panData == null || panData.Count == 0)
		{
			return;
		}
		if (!panData.Any((ChefDeskGastropanData data) => data?.HasChefDeskIndex ?? false))
		{
			LoadChefDeskGastropansByFlatOrder(panData);
			return;
		}
		foreach (ChefDeskGastropanData panDatum in panData)
		{
			if (panDatum != null && panDatum.HasChefDeskIndex)
			{
				ChefDeskGastroPan chefDeskGastroPan = FindChefDeskGastroPan(panDatum.ChefDeskIndex, panDatum.Index);
				if (!(chefDeskGastroPan == null))
				{
					chefDeskGastroPan.Load(panDatum);
				}
			}
		}
	}

	private void LoadChefDeskGastropansByFlatOrder(List<ChefDeskGastropanData> panData)
	{
		if (_chefDeskGastropans == null)
		{
			return;
		}
		int num = Mathf.Min(_chefDeskGastropans.Count, panData.Count);
		for (int i = 0; i < num; i++)
		{
			if (!(_chefDeskGastropans[i] == null) && panData[i] != null)
			{
				_chefDeskGastropans[i].Load(panData[i]);
			}
		}
	}

	private ChefDeskGastroPan FindChefDeskGastroPan(byte chefDeskIndex, byte panIndex)
	{
		if (_chefDesks == null || chefDeskIndex >= _chefDesks.Count)
		{
			return null;
		}
		ChefDesk chefDesk = _chefDesks[chefDeskIndex];
		if (chefDesk == null || chefDesk.GastroPans == null)
		{
			return null;
		}
		IReadOnlyList<ChefDeskGastroPan> gastroPans = chefDesk.GastroPans;
		if (panIndex < gastroPans.Count && gastroPans[panIndex] != null)
		{
			return gastroPans[panIndex];
		}
		return gastroPans.FirstOrDefault((ChefDeskGastroPan pan) => pan != null && pan.Index == panIndex);
	}

	private void LoadRestaurantUpgradeData()
	{
		if (_restaurantUpgradeData == null)
		{
			_restaurantUpgradeData = new RestaurantUpgradeData();
		}
		RestaurantUpgradeData restaurantUpgradeData = _restaurantUpgradeData;
		if (restaurantUpgradeData.UnlockedTableIds == null)
		{
			restaurantUpgradeData.UnlockedTableIds = new List<byte>();
		}
		restaurantUpgradeData = _restaurantUpgradeData;
		if (restaurantUpgradeData.ClosedDiningTableIds == null)
		{
			restaurantUpgradeData.ClosedDiningTableIds = new List<byte>();
		}
		restaurantUpgradeData = _restaurantUpgradeData;
		if (restaurantUpgradeData.UnlockedConveyorBeltTableIds == null)
		{
			restaurantUpgradeData.UnlockedConveyorBeltTableIds = new List<byte>();
		}
		restaurantUpgradeData = _restaurantUpgradeData;
		if (restaurantUpgradeData.UnlockedChefDeskIndexes == null)
		{
			restaurantUpgradeData.UnlockedChefDeskIndexes = new List<byte>();
		}
		restaurantUpgradeData = _restaurantUpgradeData;
		if (restaurantUpgradeData.UnlockedPrepStationIndexes == null)
		{
			restaurantUpgradeData.UnlockedPrepStationIndexes = new List<byte>();
		}
		restaurantUpgradeData = _restaurantUpgradeData;
		if (restaurantUpgradeData.UnlockedTrayTrolleyIndexes == null)
		{
			restaurantUpgradeData.UnlockedTrayTrolleyIndexes = new List<byte>();
		}
		if (_tables == null)
		{
			_tables = new List<RestaurantTable>();
		}
		if (_conveyorTables == null)
		{
			_conveyorTables = new List<ConveyorBeltTable>();
		}
		if (_chefDesks == null)
		{
			_chefDesks = new List<ChefDesk>();
		}
		if (_prepStations == null)
		{
			_prepStations = new List<PrepStation>();
		}
		if (_trolleys == null)
		{
			_trolleys = new List<TrayTrolley>();
		}
		if (base.IsServer)
		{
			_unlockedDiningTableIds.Clear();
			foreach (byte item in _restaurantUpgradeData.UnlockedTableIds.Distinct())
			{
				_unlockedDiningTableIds.Add(item);
			}
		}
		_conveyorBelt?.PhysicalStateController?.ServerSetUsable(_restaurantUpgradeData.IsConveyorBeltUnlocked);
		QuestManager.Instance?.ServerReconcileRepairedConveyorQuest(IsConveyorBeltRepaired);
		_fishFridge?.PhysicalStateController?.ServerSetUsable(_restaurantUpgradeData.IsFishFridgeUnlocked);
		_beverageFridge?.PhysicalStateController?.ServerSetUsable(_restaurantUpgradeData.IsBeverageFridgeUnlocked);
		if (_restaurantUpgradeData.HasExplicitStationUnlockData)
		{
			for (int i = 0; i < _chefDesks.Count; i++)
			{
				bool value = _restaurantUpgradeData.UnlockedChefDeskIndexes.Contains((byte)i);
				_chefDesks[i]?.PhysicalStateController?.ServerSetUsable(value);
			}
			for (int j = 0; j < _prepStations.Count; j++)
			{
				bool value2 = _restaurantUpgradeData.UnlockedPrepStationIndexes.Contains((byte)j);
				_prepStations[j]?.PhysicalStateController?.ServerSetUsable(value2);
			}
		}
		else if (_restaurantUpgradeData.UnlockedTableIds.Count > 0)
		{
			foreach (ChefDesk chefDesk in _chefDesks)
			{
				chefDesk?.PhysicalStateController?.ServerSetUsable(value: true);
			}
			foreach (PrepStation prepStation in _prepStations)
			{
				prepStation?.PhysicalStateController?.ServerSetUsable(value: true);
			}
		}
		foreach (RestaurantTable table in _tables)
		{
			if (!(table == null) && !(table.PhysicalStateController == null))
			{
				byte tableId = (byte)table.Id;
				table.PhysicalStateController.ServerSetUsable(_restaurantUpgradeData.ShouldDiningTableLoadOpen(tableId));
			}
		}
		foreach (ConveyorBeltTable conveyorTable in _conveyorTables)
		{
			if (!(conveyorTable == null) && !(conveyorTable.PhysicalStateController == null))
			{
				if (_restaurantUpgradeData.UnlockedConveyorBeltTableIds.Contains((byte)conveyorTable.SeatId))
				{
					conveyorTable.PhysicalStateController.ServerSetUsable(value: true);
				}
				else
				{
					conveyorTable.PhysicalStateController.ServerSetUsable(value: false);
				}
			}
		}
		QuestManager.Instance?.ServerReconcileRepairedConveyorChairQuest(HasRepairedConveyorChairForQuest);
		for (int k = 0; k < _trolleys.Count; k++)
		{
			TrayTrolley trayTrolley = _trolleys[k];
			if (!(trayTrolley == null) && !(trayTrolley.PhysicalStateController == null))
			{
				bool value3 = _restaurantUpgradeData.UnlockedTrayTrolleyIndexes.Contains((byte)k);
				trayTrolley.PhysicalStateController.ServerSetUsable(value3);
			}
		}
		UtilityMethods.Log("[RestaurantManager] LoadRestaurantUpgradeData" + $"\n   Conveyor: {_restaurantUpgradeData.IsConveyorBeltUnlocked}" + $"\n   FishFridge: {_restaurantUpgradeData.IsFishFridgeUnlocked}" + $"\n   BeverageFridge: {_restaurantUpgradeData.IsBeverageFridgeUnlocked}" + $"\n   Tables: {_restaurantUpgradeData.UnlockedTableIds.Count}" + $"\n   ExplicitTableOpenState: {_restaurantUpgradeData.HasExplicitDiningTableOpenState}" + $"\n   ClosedTables: {_restaurantUpgradeData.ClosedDiningTableIds.Count}" + $"\n   ConveyorTables: {_restaurantUpgradeData.UnlockedConveyorBeltTableIds.Count}" + $"\n   TrayTrolleys: {_restaurantUpgradeData.UnlockedTrayTrolleyIndexes.Count}" + $"\n   ChefDesks: {_restaurantUpgradeData.UnlockedChefDeskIndexes.Count}" + $"\n   PrepStations: {_restaurantUpgradeData.UnlockedPrepStationIndexes.Count}");
	}

	private void SaveRestaurantUpgradeData()
	{
		if (_restaurantUpgradeData == null)
		{
			_restaurantUpgradeData = new RestaurantUpgradeData();
		}
		RestaurantUpgradeData restaurantUpgradeData = _restaurantUpgradeData;
		if (restaurantUpgradeData.UnlockedTableIds == null)
		{
			restaurantUpgradeData.UnlockedTableIds = new List<byte>();
		}
		restaurantUpgradeData = _restaurantUpgradeData;
		if (restaurantUpgradeData.ClosedDiningTableIds == null)
		{
			restaurantUpgradeData.ClosedDiningTableIds = new List<byte>();
		}
		restaurantUpgradeData = _restaurantUpgradeData;
		if (restaurantUpgradeData.UnlockedConveyorBeltTableIds == null)
		{
			restaurantUpgradeData.UnlockedConveyorBeltTableIds = new List<byte>();
		}
		restaurantUpgradeData = _restaurantUpgradeData;
		if (restaurantUpgradeData.UnlockedChefDeskIndexes == null)
		{
			restaurantUpgradeData.UnlockedChefDeskIndexes = new List<byte>();
		}
		restaurantUpgradeData = _restaurantUpgradeData;
		if (restaurantUpgradeData.UnlockedPrepStationIndexes == null)
		{
			restaurantUpgradeData.UnlockedPrepStationIndexes = new List<byte>();
		}
		restaurantUpgradeData = _restaurantUpgradeData;
		if (restaurantUpgradeData.UnlockedTrayTrolleyIndexes == null)
		{
			restaurantUpgradeData.UnlockedTrayTrolleyIndexes = new List<byte>();
		}
		_restaurantUpgradeData.UnlockedConveyorBeltTableIds.Clear();
		_restaurantUpgradeData.UnlockedChefDeskIndexes.Clear();
		_restaurantUpgradeData.UnlockedPrepStationIndexes.Clear();
		_restaurantUpgradeData.UnlockedTrayTrolleyIndexes.Clear();
		_restaurantUpgradeData.ClosedDiningTableIds.Clear();
		_restaurantUpgradeData.HasExplicitStationUnlockData = true;
		if (_tables == null)
		{
			_tables = new List<RestaurantTable>();
		}
		if (_conveyorTables == null)
		{
			_conveyorTables = new List<ConveyorBeltTable>();
		}
		if (_chefDesks == null)
		{
			_chefDesks = new List<ChefDesk>();
		}
		if (_prepStations == null)
		{
			_prepStations = new List<PrepStation>();
		}
		if (_trolleys == null)
		{
			_trolleys = new List<TrayTrolley>();
		}
		_restaurantUpgradeData.IsConveyorBeltUnlocked = _conveyorBelt != null && _conveyorBelt.PhysicalStateController != null && _conveyorBelt.PhysicalStateController.IsPhysicallyUsable;
		_restaurantUpgradeData.IsFishFridgeUnlocked = _fishFridge != null && _fishFridge.PhysicalStateController != null && _fishFridge.PhysicalStateController.IsPhysicallyUsable;
		_restaurantUpgradeData.IsBeverageFridgeUnlocked = _beverageFridge != null && _beverageFridge.PhysicalStateController != null && _beverageFridge.PhysicalStateController.IsPhysicallyUsable;
		foreach (RestaurantTable table in _tables)
		{
			if (!(table == null) && !(table.PhysicalStateController == null))
			{
				byte item = (byte)table.Id;
				if (table.PhysicalStateController.IsPhysicallyUsable && !_restaurantUpgradeData.UnlockedTableIds.Contains(item))
				{
					_restaurantUpgradeData.UnlockedTableIds.Add(item);
				}
				if (base.IsServer && table.PhysicalStateController.IsPhysicallyUsable && !_unlockedDiningTableIds.Contains(item))
				{
					_unlockedDiningTableIds.Add(item);
				}
				if (_restaurantUpgradeData.HasExplicitDiningTableOpenState && !table.PhysicalStateController.IsPhysicallyUsable && _restaurantUpgradeData.UnlockedTableIds.Contains(item))
				{
					_restaurantUpgradeData.ClosedDiningTableIds.Add(item);
				}
			}
		}
		foreach (ConveyorBeltTable conveyorTable in _conveyorTables)
		{
			if (!(conveyorTable == null) && !(conveyorTable.PhysicalStateController == null) && conveyorTable.PhysicalStateController.IsPhysicallyUsable)
			{
				_restaurantUpgradeData.UnlockedConveyorBeltTableIds.Add((byte)conveyorTable.SeatId);
			}
		}
		for (int i = 0; i < _chefDesks.Count; i++)
		{
			if (!(_chefDesks[i] == null) && !(_chefDesks[i].PhysicalStateController == null) && _chefDesks[i].PhysicalStateController.IsPhysicallyUsable)
			{
				_restaurantUpgradeData.UnlockedChefDeskIndexes.Add((byte)i);
			}
		}
		for (int j = 0; j < _prepStations.Count; j++)
		{
			if (!(_prepStations[j] == null) && !(_prepStations[j].PhysicalStateController == null) && _prepStations[j].PhysicalStateController.IsPhysicallyUsable)
			{
				_restaurantUpgradeData.UnlockedPrepStationIndexes.Add((byte)j);
			}
		}
		for (int k = 0; k < _trolleys.Count; k++)
		{
			if (!(_trolleys[k] == null) && !(_trolleys[k].PhysicalStateController == null) && _trolleys[k].PhysicalStateController.IsPhysicallyUsable)
			{
				_restaurantUpgradeData.UnlockedTrayTrolleyIndexes.Add((byte)k);
			}
		}
		UtilityMethods.Log("[RestaurantManager] SaveRestaurantUpgradeData" + $"\n   Conveyor: {_restaurantUpgradeData.IsConveyorBeltUnlocked}" + $"\n   FishFridge: {_restaurantUpgradeData.IsFishFridgeUnlocked}" + $"\n   BeverageFridge: {_restaurantUpgradeData.IsBeverageFridgeUnlocked}" + $"\n   Tables: {_restaurantUpgradeData.UnlockedTableIds.Count}" + $"\n   ExplicitTableOpenState: {_restaurantUpgradeData.HasExplicitDiningTableOpenState}" + $"\n   ClosedTables: {_restaurantUpgradeData.ClosedDiningTableIds.Count}" + $"\n   ConveyorTables: {_restaurantUpgradeData.UnlockedConveyorBeltTableIds.Count}" + $"\n   TrayTrolleys: {_restaurantUpgradeData.UnlockedTrayTrolleyIndexes.Count}" + $"\n   ChefDesks: {_restaurantUpgradeData.UnlockedChefDeskIndexes.Count}" + $"\n   PrepStations: {_restaurantUpgradeData.UnlockedPrepStationIndexes.Count}");
	}

	public void RegisterGastropanData(GastroPan gastropan)
	{
		if (!_gastropans.Contains(gastropan))
		{
			_gastropans.Add(gastropan);
		}
	}

	public void DeRegisterGastropanData(GastroPan gastropan)
	{
		if (_gastropans.Contains(gastropan))
		{
			_gastropans.Remove(gastropan);
		}
	}

	public void RegisterIngredientData(IngredientEntity ingredient)
	{
		if (!_ingredientEntities.Contains(ingredient))
		{
			_ingredientEntities.Add(ingredient);
		}
	}

	public void DeRegisterIngredientData(IngredientEntity ingredient)
	{
		if (_ingredientEntities.Contains(ingredient))
		{
			_ingredientEntities.Remove(ingredient);
		}
	}

	public void RegisterDeadFish(DeadFish deadFish)
	{
		if (!_deadFishes.Contains(deadFish))
		{
			_deadFishes.Add(deadFish);
		}
	}

	public void DeRegisterDeadFish(DeadFish deadFish)
	{
		if (_deadFishes.Contains(deadFish))
		{
			_deadFishes.Remove(deadFish);
		}
	}

	public void RegisterWholesaleData(WholesaleEntity wholesale)
	{
		if (!_wholeSaleItemsInStock.Contains(wholesale))
		{
			_wholeSaleItemsInStock.Add(wholesale);
		}
		if (wholesale != null && wholesale.WholesaleSO != null && !string.IsNullOrEmpty(wholesale.WholesaleSO.Id))
		{
			ServerAddStockRpc(wholesale.WholesaleSO.Id, wholesale.CurrentCount);
		}
	}

	public void DeRegisterWholesaleData(WholesaleEntity wholesale)
	{
		if (_wholeSaleItemsInStock.Contains(wholesale))
		{
			_wholeSaleItemsInStock.Remove(wholesale);
		}
		if (wholesale != null && wholesale.WholesaleSO != null && !string.IsNullOrEmpty(wholesale.WholesaleSO.Id))
		{
			ServerRemoveStockRpc(wholesale.WholesaleSO.Id, wholesale.CurrentCount);
		}
	}

	public void RegisterServedDishData(ServedDish servedDish)
	{
		if (!_servedDishes.Contains(servedDish))
		{
			_servedDishes.Add(servedDish);
		}
	}

	public void DeRegisterServedDishData(ServedDish wholesale)
	{
		if (_servedDishes.Contains(wholesale))
		{
			_servedDishes.Remove(wholesale);
		}
	}

	private void OnCompassTargetChanged(byte old, byte @new)
	{
		ClientSetCompassTarget(CurrentCompassTargetLevel, CurrentCompassTargetStepIndex);
	}

	[Rpc(SendTo.Server)]
	public void ServerSetCompassTargetRpc(byte level)
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
			FastBufferWriter bufferWriter = __beginSendRpc(2833029867u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in level, default(FastBufferWriter.ForPrimitives));
			__endSendRpc(ref bufferWriter, 2833029867u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			ServerSetCompassTargetFromLevel(level);
		}
	}

	public bool ServerSetCompassTargetFromLevel(byte level)
	{
		if (!base.IsServer)
		{
			return false;
		}
		if (!TryGetCompassTargetStepForLevel(level, 0, out var targetStepIndex))
		{
			return false;
		}
		CurrentCompassTargetLevel = level;
		CurrentCompassTargetStepIndex = targetStepIndex;
		ClientSetCompassTarget(level, targetStepIndex);
		return true;
	}

	[Rpc(SendTo.Server)]
	public void ServerAdvanceCompassTargetStepRpc()
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
			FastBufferWriter bufferWriter = __beginSendRpc(557912269u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 557912269u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			ServerAdvanceCompassTargetStep();
		}
	}

	public bool ServerAdvanceCompassTargetStep()
	{
		if (!base.IsServer)
		{
			return false;
		}
		byte requestedStepIndex = (byte)(CurrentCompassTargetStepIndex + 1);
		if (!TryGetCompassTargetStepForLevel(CurrentCompassTargetLevel, requestedStepIndex, out var targetStepIndex))
		{
			return false;
		}
		CurrentCompassTargetStepIndex = targetStepIndex;
		ClientSetCompassTarget(CurrentCompassTargetLevel, targetStepIndex);
		return true;
	}

	public bool ServerSetCompassTargetRouteState(byte level, byte stepIndex)
	{
		if (!base.IsServer)
		{
			return false;
		}
		if (!TryGetCompassTargetStepForLevel(level, stepIndex, out var targetStepIndex))
		{
			return false;
		}
		CurrentCompassTargetLevel = level;
		CurrentCompassTargetStepIndex = targetStepIndex;
		ClientSetCompassTarget(level, targetStepIndex);
		return true;
	}

	public bool TryGetCompassTargetIndexForLevel(byte level, out byte targetIndex)
	{
		return TryGetCompassTargetStepForLevel(level, 0, out targetIndex);
	}

	public bool TryGetCompassTargetStepForLevel(byte level, byte requestedStepIndex, out byte targetStepIndex)
	{
		targetStepIndex = 0;
		if (level == 0)
		{
			UtilityMethods.Log("[RestaurantManager][Compass] Level 0 does not have a target.", LogType.Warning);
			return false;
		}
		if (!TryGetCompassTarget(level, requestedStepIndex, out var _))
		{
			return false;
		}
		targetStepIndex = requestedStepIndex;
		return true;
	}

	public void RefreshCompassTargetForLocalPlayer()
	{
		ClientSetCompassTarget(CurrentCompassTargetLevel, CurrentCompassTargetStepIndex);
	}

	public void RefreshLighthouseCompassDoorForLocalPlayer()
	{
		_lighthouseCompassDoor?.RefreshForLocalPlayer();
	}

	private void ClientSetCompassTarget(byte level, byte stepIndex)
	{
		if (!(Singleton<PlayersManager>.Instance == null))
		{
			PPG_Player localPlayer = Singleton<PlayersManager>.Instance.LocalPlayer;
			if (!(localPlayer == null) && TryGetCompassTarget(level, stepIndex, out var target))
			{
				localPlayer.SetCompassTarget(target, stepIndex);
			}
		}
	}

	private bool TryGetCompassTarget(byte level, byte stepIndex, out Transform target)
	{
		target = null;
		if (level == 0)
		{
			return false;
		}
		if (level == _lighthouseCompassLevel)
		{
			if (stepIndex != 0)
			{
				UtilityMethods.Log($"[RestaurantManager][Compass] Invalid lighthouse route step. level={level}, step={stepIndex}", LogType.Warning);
				return false;
			}
			if (_lighthouseCompassDoor == null || _lighthouseCompassDoor.CompassTarget == null)
			{
				UtilityMethods.Log("[RestaurantManager][Compass] Lighthouse compass door target is missing.", LogType.Warning);
				return false;
			}
			target = _lighthouseCompassDoor.CompassTarget;
			return true;
		}
		int num = level - 1;
		List<Transform> list = null;
		if (_compassTargetRoutes != null && num >= 0 && num < _compassTargetRoutes.Count && _compassTargetRoutes[num] != null && _compassTargetRoutes[num].Targets != null && _compassTargetRoutes[num].Targets.Count > 0)
		{
			list = _compassTargetRoutes[num].Targets;
		}
		if (list == null || list.Count == 0)
		{
			UtilityMethods.Log($"[RestaurantManager][Compass] Target route is empty. level={level}", LogType.Warning);
			return false;
		}
		if (stepIndex >= list.Count)
		{
			UtilityMethods.Log($"[RestaurantManager][Compass] Invalid route step. level={level}, step={stepIndex}, stepCount={list.Count}", LogType.Warning);
			return false;
		}
		target = list[stepIndex];
		if (target == null)
		{
			UtilityMethods.Log($"[RestaurantManager][Compass] Route target reference is missing. level={level}, step={stepIndex}", LogType.Warning);
			return false;
		}
		return true;
	}

	public int GetNextDiningTableUnlockPrice()
	{
		int permanentlyUnlockedDiningTableCount = GetPermanentlyUnlockedDiningTableCount();
		if (_tableUnlockPriceSO == null)
		{
			UtilityMethods.Log("[RestaurantManager] TableUnlockPriceSO is missing. Falling back to $100.", LogType.Warning);
			return 100;
		}
		return _tableUnlockPriceSO.GetDiningTableUnlockPrice(permanentlyUnlockedDiningTableCount);
	}

	public int GetNextConveyorTableUnlockPrice()
	{
		int unlockedConveyorTableCount = GetUnlockedConveyorTableCount();
		if (_tableUnlockPriceSO == null)
		{
			UtilityMethods.Log("[RestaurantManager] SeatingUnlockPriceSO is missing. Falling back to $100.", LogType.Warning);
			return 100;
		}
		return _tableUnlockPriceSO.GetConveyorTableUnlockPrice(unlockedConveyorTableCount);
	}

	public int GetConveyorBeltUnlockPrice()
	{
		if (_tableUnlockPriceSO == null)
		{
			UtilityMethods.Log("[RestaurantManager] TableUnlockPriceSO is missing. Falling back to $1000 for conveyor belt.", LogType.Warning);
			return 1000;
		}
		return _tableUnlockPriceSO.GetConveyorBeltUnlockPrice();
	}

	public void ResetTrolleys()
	{
		foreach (TrayTrolley trolley in _trolleys)
		{
			trolley.ServerResetTrayTransformsRpc();
		}
	}

	private void CacheRestaurantLightIntensities()
	{
		_cachedNightLightIntensities.Clear();
		_lightTweens.Clear();
		if (_lights == null)
		{
			return;
		}
		foreach (Light light in _lights)
		{
			_cachedNightLightIntensities.Add((light != null) ? Mathf.Max(0f, light.intensity) : 0f);
			_lightTweens.Add(null);
		}
	}

	private void EnsureRestaurantLightCache()
	{
		if (_lights != null && (_cachedNightLightIntensities.Count != _lights.Count || _lightTweens.Count != _lights.Count))
		{
			CacheRestaurantLightIntensities();
		}
	}

	private void BindCozyBloomEvents()
	{
		if (!_cozyBloomBound)
		{
			if (_cozyManager == null)
			{
				UtilityMethods.Log("[RestaurantManager] CozyWeatherManager reference is missing for light sync.", LogType.Warning);
				return;
			}
			_cozyManager.BloomIntensityUpdated += HandleBloomIntensityUpdated;
			_cozyBloomBound = true;
		}
	}

	private void UnbindCozyBloomEvents()
	{
		if (_cozyBloomBound)
		{
			if (_cozyManager != null)
			{
				_cozyManager.BloomIntensityUpdated -= HandleBloomIntensityUpdated;
			}
			_cozyBloomBound = false;
		}
	}

	private void RefreshRestaurantLightsImmediate()
	{
		if (!(_cozyManager == null))
		{
			ApplyRestaurantLightsByBloom(_cozyManager.GetCurrentBloomIntensityTarget(), 0f, Ease.Linear);
		}
	}

	private void HandleBloomIntensityUpdated(float bloomIntensity, float duration, Ease ease)
	{
		ApplyRestaurantLightsByBloom(bloomIntensity, duration, ease);
	}

	private void ApplyRestaurantLightsByBloom(float bloomIntensity, float duration, Ease ease)
	{
		EnsureRestaurantLightCache();
		if (_lights == null || _lights.Count == 0)
		{
			return;
		}
		float t = Mathf.Clamp01(bloomIntensity);
		float num = Mathf.Lerp(_dayLightMultiplier, _nightLightMultiplier, t);
		for (int i = 0; i < _lights.Count; i++)
		{
			Light light = _lights[i];
			if (light == null)
			{
				continue;
			}
			float num2 = ((i < _cachedNightLightIntensities.Count) ? _cachedNightLightIntensities[i] : light.intensity);
			float num3 = Mathf.Max(0f, num2 * num);
			_lightTweens[i]?.Kill();
			_lightTweens[i] = null;
			if (duration <= 0f)
			{
				SetRestaurantLightIntensity(light, num3);
				continue;
			}
			if (num3 > _offThreshold)
			{
				light.enabled = true;
			}
			Light capturedLight = light;
			float capturedTargetIntensity = num3;
			int capturedIndex = i;
			_lightTweens[i] = DOTween.To(() => capturedLight.intensity, (float x) =>
			{
				capturedLight.intensity = x;
			}, capturedTargetIntensity, duration).SetEase(ease).SetUpdate(isIndependentUpdate: true)
				.OnComplete(() =>
				{
					if (capturedIndex < _lightTweens.Count)
					{
						_lightTweens[capturedIndex] = null;
					}
					if (_disableLightsWhenOff && capturedLight != null && capturedTargetIntensity <= _offThreshold)
					{
						capturedLight.enabled = false;
					}
				});
		}
	}

	private void SetRestaurantLightIntensity(Light light, float intensity)
	{
		if (!(light == null))
		{
			light.intensity = intensity;
			if (_disableLightsWhenOff)
			{
				light.enabled = intensity > _offThreshold;
			}
			else
			{
				light.enabled = true;
			}
		}
	}

	private void KillRestaurantLightTweens()
	{
		for (int i = 0; i < _lightTweens.Count; i++)
		{
			_lightTweens[i]?.Kill();
			_lightTweens[i] = null;
		}
	}

	protected override void __initializeVariables()
	{
		if (_customSushiRecipesNetwork == null)
		{
			throw new Exception("RestaurantManager._customSushiRecipesNetwork cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		_customSushiRecipesNetwork.Initialize(this);
		__nameNetworkVariable(_customSushiRecipesNetwork, "_customSushiRecipesNetwork");
		NetworkVariableFields.Add(_customSushiRecipesNetwork);
		if (_unlockedDiningTableIds == null)
		{
			throw new Exception("RestaurantManager._unlockedDiningTableIds cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		_unlockedDiningTableIds.Initialize(this);
		__nameNetworkVariable(_unlockedDiningTableIds, "_unlockedDiningTableIds");
		NetworkVariableFields.Add(_unlockedDiningTableIds);
		if (_currentCompassTargetLevel == null)
		{
			throw new Exception("RestaurantManager._currentCompassTargetLevel cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		_currentCompassTargetLevel.Initialize(this);
		__nameNetworkVariable(_currentCompassTargetLevel, "_currentCompassTargetLevel");
		NetworkVariableFields.Add(_currentCompassTargetLevel);
		if (_currentCompassTargetStepIndex == null)
		{
			throw new Exception("RestaurantManager._currentCompassTargetStepIndex cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		_currentCompassTargetStepIndex.Initialize(this);
		__nameNetworkVariable(_currentCompassTargetStepIndex, "_currentCompassTargetStepIndex");
		NetworkVariableFields.Add(_currentCompassTargetStepIndex);
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		__registerRpc(2418819073u, __rpc_handler_2418819073, "ClientClearCustomSushiThumbnailsRpc", RpcInvokePermission.Server);
		__registerRpc(2157891532u, __rpc_handler_2157891532, "ServerRequestCustomSushiThumbnailsRpc", RpcInvokePermission.Everyone);
		__registerRpc(3413436067u, __rpc_handler_3413436067, "ClientReceiveCustomSushiThumbnailRpc", RpcInvokePermission.Server);
		__registerRpc(2970539448u, __rpc_handler_2970539448, "ClientRemoveCustomSushiThumbnailRpc", RpcInvokePermission.Server);
		__registerRpc(2187154040u, __rpc_handler_2187154040, "ClientShowCustomerServiceEndedFeedbackRpc", RpcInvokePermission.Everyone);
		__registerRpc(38087338u, __rpc_handler_38087338, "ClientShowRestaurantOpeningReminderRpc", RpcInvokePermission.Everyone);
		__registerRpc(4268695144u, __rpc_handler_4268695144, "ClientCallConveyorResidentRpc", RpcInvokePermission.Everyone);
		__registerRpc(1561095352u, __rpc_handler_1561095352, "ServerRequestStockSnapshotRpc", RpcInvokePermission.Everyone);
		__registerRpc(1800974908u, __rpc_handler_1800974908, "ClientReceiveStockSnapshotRpc", RpcInvokePermission.Everyone);
		__registerRpc(4252636073u, __rpc_handler_4252636073, "ServerAddStockRpc", RpcInvokePermission.Everyone);
		__registerRpc(2882172893u, __rpc_handler_2882172893, "ServerRemoveStockRpc", RpcInvokePermission.Everyone);
		__registerRpc(4214869977u, __rpc_handler_4214869977, "ClientSyncStockRpc", RpcInvokePermission.Everyone);
		__registerRpc(3625287616u, __rpc_handler_3625287616, "ServerDebugFillConveyorBeltRpc", RpcInvokePermission.Everyone);
		__registerRpc(2833029867u, __rpc_handler_2833029867, "ServerSetCompassTargetRpc", RpcInvokePermission.Everyone);
		__registerRpc(557912269u, __rpc_handler_557912269, "ServerAdvanceCompassTargetStepRpc", RpcInvokePermission.Everyone);
		base.__initializeRpcs();
	}

	private static void __rpc_handler_2418819073(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RestaurantManager)target).ClientClearCustomSushiThumbnailsRpc();
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2157891532(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			RpcParams ext = rpcParams.Ext;
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RestaurantManager)target).ServerRequestCustomSushiThumbnailsRpc(ext);
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3413436067(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
			byte[] value3 = null;
			if (value2)
			{
				reader.ReadValueSafe(out value3, default(FastBufferWriter.ForPrimitives));
			}
			RpcParams ext = rpcParams.Ext;
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RestaurantManager)target).ClientReceiveCustomSushiThumbnailRpc(value, value3, ext);
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2970539448(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RestaurantManager)target).ClientRemoveCustomSushiThumbnailRpc(value);
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2187154040(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RestaurantManager)target).ClientShowCustomerServiceEndedFeedbackRpc();
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_38087338(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RestaurantManager)target).ClientShowRestaurantOpeningReminderRpc();
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_4268695144(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out int value);
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RestaurantManager)target).ClientCallConveyorResidentRpc(value);
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1561095352(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			RpcParams ext = rpcParams.Ext;
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RestaurantManager)target).ServerRequestStockSnapshotRpc(ext);
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1800974908(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			StockSnapshotEntry[] value2 = null;
			if (value)
			{
				reader.ReadValueSafe(out value2, default(FastBufferWriter.ForNetworkSerializable));
			}
			RpcParams ext = rpcParams.Ext;
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RestaurantManager)target).ClientReceiveStockSnapshotRpc(value2, ext);
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_4252636073(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
			ByteUnpacker.ReadValueBitPacked(reader, out int value2);
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RestaurantManager)target).ServerAddStockRpc(value, value2);
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2882172893(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
			ByteUnpacker.ReadValueBitPacked(reader, out int value2);
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RestaurantManager)target).ServerRemoveStockRpc(value, value2);
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_4214869977(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
			ByteUnpacker.ReadValueBitPacked(reader, out int value2);
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RestaurantManager)target).ClientSyncStockRpc(value, value2);
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3625287616(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RestaurantManager)target).ServerDebugFillConveyorBeltRpc();
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2833029867(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RestaurantManager)target).ServerSetCompassTargetRpc(value);
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_557912269(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RestaurantManager)target).ServerAdvanceCompassTargetStepRpc();
			((RestaurantManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	protected override string __getTypeName()
	{
		return "RestaurantManager";
	}
}
