using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Core.ProcessedIngredient;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.FishFridge;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen;
using PaperPixelGames.Core.Kitchen.PreparationStation.GastronomPan;
using PaperPixelGames.Core.Kitchen.Wholasale;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.ProcessedIngredient;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Core.TrashBin;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace PaperPixelGames.Core.Staff
{
	public sealed class StaffWorkplaceRegistry : MonoBehaviour
	{
		public ItemDatabaseSO Database;

		public Transform SpawnPoint;

		public Transform WaitingPoint;

		[Tooltip("Shared waiting slots. Each slot is reserved by one employee while waiting. Waiting (n) siblings of WaitingPoint are also discovered on startup.")]
		public List<Transform> WaitingPoints = new List<Transform>();

		[Min(0.03f)]
		public float WaitingArrivalRadius = 0.1f;

		public Transform ExitPoint;

		public Transform GreenMarketPoint;

		public Transform BlueMarketPoint;

		public Bounds RestaurantBounds;

		public List<StaffStoragePoint> Storage = new List<StaffStoragePoint>();

		public List<StaffSupplyConversion> Conversions = new List<StaffSupplyConversion>();

		public List<StaffPrepWorkplace> PrepStations = new List<StaffPrepWorkplace>();

		public List<StaffPrepMachine> PrepMachines = new List<StaffPrepMachine>();

		public List<StaffPrepRecipe> PrepRecipes = new List<StaffPrepRecipe>();

		public List<StaffChefWorkplace> ChefDesks = new List<StaffChefWorkplace>();

		public ConveyorBelt Conveyor;

		[Tooltip("Optional Chef approach point. Otherwise a point beside the reserved conveyor slot is used.")]
		public Transform ConveyorStockPoint;

		[Tooltip("Optional Stocker trash approach point. Otherwise the existing nearby cleaning point is used.")]
		public Transform StockerTrashPoint;

		public List<StaffServicePoint> ServicePoints = new List<StaffServicePoint>();

		public List<TrayTrolley> TrayTrolleys = new List<TrayTrolley>();

		public BeverageFridge BeverageFridge;

		public Transform CleaningPoint;

		public Transform TrayReturnPoint;

		public List<StaffStoragePoint> ReadyDishPoints = new List<StaffStoragePoint>();

		public readonly StaffOutputPlacement OutputPlacement = new StaffOutputPlacement();

		internal readonly StaffConversionTable ConversionTable = new StaffConversionTable();

		private readonly Dictionary<int, string> chefProduction = new Dictionary<int, string>();

		public Fridge FishFridge;

		public Transform FishFridgePoint;

		public List<WholesaleSO> GreenProducts = new List<WholesaleSO>();

		public List<WholesaleSO> BlueProducts = new List<WholesaleSO>();

		public bool GreenMarketOpen = true;

		public bool BlueMarketOpen = true;

		private readonly Dictionary<string, Transform> points = new Dictionary<string, Transform>();

		private readonly Dictionary<int, KeyValuePair<string, int>> prepProduction = new Dictionary<int, KeyValuePair<string, int>>();

		private readonly List<Transform> stackPoints = new List<Transform>();

		private readonly List<StaffStorageStack> stackViews = new List<StaffStorageStack>();

		private float nextStackRefresh;

		private readonly List<Transform> waitingSlots = new List<Transform>();

		private readonly Dictionary<int, Transform> waitingOwners = new Dictionary<int, Transform>();

		private readonly List<Transform> overflowWaitingSlots = new List<Transform>();

		public static StaffWorkplaceRegistry Instance { get; private set; }

		public bool WaitressReady
		{
			get
			{
				if (CleaningPoint != null && TrayReturnPoint != null && TrayTrolleys.Any((TrayTrolley t) => t != null))
				{
					return ServicePoints.Any((StaffServicePoint p) => p.Target != null && p.Point != null);
				}
				return false;
			}
		}

		public IEnumerable<string> PendingChefProduction => chefProduction.Values;

		public IEnumerable<KeyValuePair<string, int>> PendingPrepProduction => prepProduction.Values;

		public bool IsReady
		{
			get
			{
				if (Database != null && SpawnPoint != null && WaitingPoint != null && ExitPoint != null && GreenMarketPoint != null && BlueMarketPoint != null)
				{
					return points.Count > 0;
				}
				return false;
			}
		}

		public IReadOnlyList<Transform> WaitingSlots => waitingSlots;

		public bool TryFindTrash(Vector3 origin, out PaperPixelGames.Core.TrashBin.TrashBin trash, out Transform point)
		{
			trash = null;
			point = null;
			float num = float.MaxValue;
			foreach (PaperPixelGames.Core.TrashBin.TrashBin staffBin in PaperPixelGames.Core.TrashBin.TrashBin.StaffBins)
			{
				if (!(staffBin == null) && staffBin.ServerStaffUsable && IsInside(staffBin.transform.position))
				{
					Transform transform = ServicePoint(staffBin);
					if (transform == null && StockerTrashPoint != null && (StockerTrashPoint.position - staffBin.transform.position).sqrMagnitude < 9f)
					{
						transform = StockerTrashPoint;
					}
					if (transform == null && CleaningPoint != null && (CleaningPoint.position - staffBin.transform.position).sqrMagnitude < 9f)
					{
						transform = CleaningPoint;
					}
					if (transform == null)
					{
						transform = staffBin.transform;
					}
					float sqrMagnitude = (origin - transform.position).sqrMagnitude;
					if (!(sqrMagnitude >= num) && TrySamplePosition(transform.position, out var _))
					{
						num = sqrMagnitude;
						trash = staffBin;
						point = transform;
					}
				}
			}
			return trash != null;
		}

		public Transform ReadyDishPoint(string id, Vector3? origin = null)
		{
			Transform transform = ReadyDishPoints.FirstOrDefault((StaffStoragePoint p) => p.Item != null && p.Item.Id == id && p.Point != null)?.Point;
			bool flag = Database?.FindById(id) is SushiSO || Singleton<GameProgressManager>.Instance?.RestaurantManager?.CustomSushiRecipeCatalog.FindById(id) != null;
			if (transform != null || !flag)
			{
				return transform;
			}
			IEnumerable<StaffChefWorkplace> source = ChefDesks.Where((StaffChefWorkplace c) => c.ReadySushiPoints.Any((Transform p) => p != null));
			StaffChefWorkplace staffChefWorkplace = (origin.HasValue ? source.OrderBy((StaffChefWorkplace c) => c.ReadySushiPoints.Where((Transform p) => p != null).Min((Transform p) => (p.position - origin.Value).sqrMagnitude)).FirstOrDefault() : source.FirstOrDefault());
			if (staffChefWorkplace == null)
			{
				return null;
			}
			return OutputPlacement.Choose(staffChefWorkplace.ReadySushiPoints, id);
		}

		public Transform ServicePoint(Component target)
		{
			return ServicePoints.FirstOrDefault((StaffServicePoint p) => p.Target == target)?.Point;
		}

		public IEnumerable<string> ChefProductionExcept(int employeeId)
		{
			return from p in chefProduction
				where p.Key != employeeId
				select p.Value;
		}

		internal void SetChefProduction(int employeeId, string recipeId)
		{
			chefProduction[employeeId] = recipeId;
		}

		internal void ClearChefProduction(int employeeId)
		{
			chefProduction.Remove(employeeId);
		}

		internal void SetPrepProduction(int employeeId, string outputId, int amount)
		{
			prepProduction[employeeId] = new KeyValuePair<string, int>(outputId, amount);
		}

		internal void ClearPrepProduction(int employeeId)
		{
			prepProduction.Remove(employeeId);
		}

		private void Awake()
		{
			Instance = this;
			CacheWaitingPoints();
			foreach (StaffStoragePoint item in Storage)
			{
				if (item.Item != null && item.Point != null)
				{
					points[item.Item.Id] = item.Point;
				}
			}
			foreach (WholesaleSO item2 in GreenProducts.Concat(BlueProducts))
			{
				HoldableSO holdableSO = ((!(item2?.Prefab != null)) ? null : item2.Prefab.GetComponent<BaseHoldable>()?.ItemSO);
				if (holdableSO != null && !points.ContainsKey(holdableSO.Id) && points.TryGetValue(item2.Id, out var value))
				{
					points[holdableSO.Id] = value;
				}
			}
		}

		private void OnDestroy()
		{
			DestroyStorageStacks();
			if (Instance == this)
			{
				Instance = null;
			}
		}

		public Transform StorageFor(string id)
		{
			if (id == null || !points.TryGetValue(id, out var value))
			{
				return null;
			}
			return value;
		}

		public bool IsInside(Vector3 position)
		{
			return RestaurantBounds.Contains(position);
		}

		public bool TrySamplePosition(Vector3 position, out NavMeshHit hit)
		{
			return NavMesh.SamplePosition(position, out hit, 2f, new NavMeshQueryFilter
			{
				agentTypeID = ((StaffManager.Instance?.Catalog?.NpcPrefab != null) ? StaffManager.Instance.Catalog.NpcPrefab.GetComponent<NavMeshAgent>().agentTypeID : 0),
				areaMask = -1
			});
		}

		public bool MarketOpen(bool blue)
		{
			if (!blue)
			{
				return GreenMarketOpen;
			}
			return BlueMarketOpen;
		}

		public IReadOnlyList<WholesaleSO> Products(bool blue)
		{
			if (!blue)
			{
				return GreenProducts;
			}
			return BlueProducts;
		}

		public static string StackType(BaseHoldable item)
		{
			if (!(item is GastroPan gastroPan))
			{
				if (!(item is WholesaleEntity wholesaleEntity))
				{
					if (!(item is ServedDish servedDish) || string.IsNullOrWhiteSpace(servedDish.RuntimeRecipeIdentityId))
					{
						if (!(item.ItemSO != null))
						{
							return "";
						}
						return "item:" + item.ItemSO.Id;
					}
					return "item:" + servedDish.RuntimeRecipeIdentityId;
				}
				return "pack:" + wholesaleEntity.WholesaleSO.Id;
			}
			return "pan:" + gastroPan.IngredientId.ToString();
		}

		public static int StackContent(BaseHoldable item)
		{
			if (!(item is GastroPan gastroPan))
			{
				if (!(item is WholesaleEntity wholesaleEntity))
				{
					if (!(item is IngredientEntity ingredientEntity))
					{
						return 1;
					}
					return ingredientEntity.IngredientCount;
				}
				return wholesaleEntity.CurrentCount;
			}
			return gastroPan.IngredientCount;
		}

		private static bool StackPhysical(BaseHoldable item)
		{
			if (item != null && item.IsSpawned && item.ItemSO != null && !item.IsStaffCarried && !item.IsPlayerInventoryOwnedForPersistence && item.CanHold.Value)
			{
				return item.transform.parent == null;
			}
			return false;
		}

		private bool StackAccepts(Transform point, BaseHoldable item)
		{
			if (!Storage.Any((StaffStoragePoint s) => s.Point == point && s.Item != null && (s.Item.Id == item.ItemSO.Id || (s.Item.Prefab != null && s.Item.Prefab.GetComponent<BaseHoldable>()?.ItemSO == item.ItemSO))) && !ReadyDishPoints.Any((StaffStoragePoint s) => s.Point == point && s.Item == item.ItemSO) && (!(item is GastroPan) || (!PrepStations.Any((StaffPrepWorkplace s) => s.PreparedPanSlots.Contains(point) || s.PanStackPoint == point || s.PreparedPanPoints.Any((StaffStoragePoint p) => p.Point == point)) && !ChefDesks.Any((StaffChefWorkplace s) => s.PanOutputs.Contains(point) || s.PanStackPoint == point))) && (!(item is IngredientEntity) || (!PrepMachines.Any((StaffPrepMachine s) => s.OutputPoint == point) && !PrepStations.Any((StaffPrepWorkplace s) => s.OutputPoint == point))))
			{
				if (item.ItemSO is SushiSO)
				{
					return ChefDesks.Any((StaffChefWorkplace s) => s.ReadySushiPoints.Contains(point) || s.PanOutputs.Contains(point) || (s.ReadySushiPoints.Count == 0 && s.OutputPoint == point));
				}
				return false;
			}
			return true;
		}

		private void Update()
		{
			if (!(Time.unscaledTime < nextStackRefresh))
			{
				nextStackRefresh = Time.unscaledTime + 0.15f;
				RefreshStorageStacks();
			}
		}

		public void RefreshStorageStacks()
		{
			foreach (Transform item in (from p in Storage.Select((StaffStoragePoint s) => s.Point).Concat(ReadyDishPoints.Select((StaffStoragePoint s) => s.Point)).Concat(PrepStations.SelectMany((StaffPrepWorkplace s) => (s.PreparedPanSlots.Count <= 0) ? new List<Transform> { s.PanStackPoint } : s.PreparedPanSlots))
					.Concat(ChefDesks.SelectMany((StaffChefWorkplace s) => s.ReadySushiPoints.Concat(s.PanOutputs)))
					.Concat(PrepMachines.Select((StaffPrepMachine s) => s.OutputPoint))
					.Concat(PrepStations.Select((StaffPrepWorkplace s) => s.OutputPoint))
					.Concat(PrepStations.Select((StaffPrepWorkplace s) => s.PanStackPoint))
					.Concat(PrepStations.SelectMany((StaffPrepWorkplace s) => s.PreparedPanPoints.Select((StaffStoragePoint p) => p.Point)))
					.Concat(ChefDesks.Select((StaffChefWorkplace s) => s.PanStackPoint))
					.Concat(from s in ChefDesks
						where s.ReadySushiPoints.Count == 0
						select s.OutputPoint)
				where p != null
				select p).Distinct())
			{
				if (!stackPoints.Contains(item))
				{
					stackPoints.Add(item);
					GameObject obj = new GameObject("Staff storage stack");
					SceneManager.MoveGameObjectToScene(obj, base.gameObject.scene);
					StaffStorageStack staffStorageStack = obj.AddComponent<StaffStorageStack>();
					staffStorageStack.Initialize(item);
					stackViews.Add(staffStorageStack);
				}
			}
			BaseHoldable[] array = BaseHoldable.StackWorldItems.Where((BaseHoldable i) => i != null && i.IsSpawned && i.NetworkManager == NetworkManager.Singleton).ToArray();
			if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
			{
				BaseHoldable[] array2 = array;
				foreach (BaseHoldable baseHoldable in array2)
				{
					int num2 = -1;
					if (StackPhysical(baseHoldable))
					{
						float num3 = 0.0101f;
						for (int num4 = 0; num4 < stackPoints.Count; num4++)
						{
							if (!(stackPoints[num4] == null))
							{
								float sqrMagnitude = (baseHoldable.transform.position - stackPoints[num4].position).sqrMagnitude;
								if (!(sqrMagnitude >= num3) && StackAccepts(stackPoints[num4], baseHoldable))
								{
									num3 = sqrMagnitude;
									num2 = num4;
								}
							}
						}
					}
					if (num2 >= 0 && baseHoldable is GastroPan { IngredientCount: 0 })
					{
						baseHoldable.NetworkObject.Despawn();
					}
					else
					{
						baseHoldable.ServerSetStaffStack(num2);
					}
				}
			}
			Dictionary<int, List<BaseHoldable>> dictionary = (from item in array
				where StackPhysical(item) && item.StaffStackPoint >= 0
				group item by item.StaffStackPoint).ToDictionary((IGrouping<int, BaseHoldable> g) => g.Key, (IGrouping<int, BaseHoldable> g) => g.OrderBy((BaseHoldable item) => item.NetworkObjectId).ToList());
			for (int num5 = 0; num5 < stackViews.Count; num5++)
			{
				stackViews[num5].Refresh(dictionary.TryGetValue(num5, out var value) ? value : new List<BaseHoldable>());
			}
		}

		public bool ServerTryGetPackageStackPoint(BaseHoldable anchor, BaseHoldable incoming, int expectedIndex, out Transform point)
		{
			point = null;
			if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || anchor == incoming || !StaffStackPackage.Matches(anchor, incoming) || anchor.NetworkManager != NetworkManager.Singleton || incoming.NetworkManager != NetworkManager.Singleton || !incoming.IsSpawned)
			{
				return false;
			}
			RefreshStorageStacks();
			if (!StackPhysical(anchor) || expectedIndex < 0 || expectedIndex >= stackPoints.Count || anchor.StaffStackPoint != expectedIndex)
			{
				return false;
			}
			Transform transform = stackPoints[expectedIndex];
			if (transform == null || !StackAccepts(transform, incoming))
			{
				return false;
			}
			point = transform;
			return true;
		}

		public void FillStackPackage(BaseHoldable item, int employee = 0, int pointOverride = -1)
		{
			if (item == null || !item.IsServer || StackContent(item) <= 0)
			{
				return;
			}
			int num = ((pointOverride >= 0) ? pointOverride : item.StaffStackPoint);
			if (num < 0)
			{
				return;
			}
			foreach (BaseHoldable item2 in BaseHoldable.StaffWorldItems.OrderBy((BaseHoldable p) => p.NetworkObjectId))
			{
				if (item2 == item || item2.StaffStackPoint != num || !StackPhysical(item2))
				{
					continue;
				}
				StaffManager instance = StaffManager.Instance;
				if ((object)instance != null && instance.IsStackItemReserved(item2.NetworkObjectId, employee))
				{
					continue;
				}
				if (item is WholesaleEntity wholesaleEntity && item2 is WholesaleEntity wholesaleEntity2)
				{
					wholesaleEntity2.ServerTransferStackContentsTo(wholesaleEntity);
					if (wholesaleEntity.CurrentCount >= wholesaleEntity.WholesaleSO.Quantity)
					{
						break;
					}
				}
				else if (item is SideDishIngredientEntity sideDishIngredientEntity && item2 is SideDishIngredientEntity sideDishIngredientEntity2)
				{
					sideDishIngredientEntity2.ServerTransferStackContentsTo(sideDishIngredientEntity);
					if (sideDishIngredientEntity.IngredientCount >= sideDishIngredientEntity.StaffPackageQuantity)
					{
						break;
					}
				}
			}
		}

		private void DestroyStorageStacks()
		{
			foreach (StaffStorageStack stackView in stackViews)
			{
				if (stackView != null)
				{
					UnityEngine.Object.Destroy(stackView.gameObject);
				}
			}
			foreach (BaseHoldable stackWorldItem in BaseHoldable.StackWorldItems)
			{
				if (stackWorldItem != null)
				{
					stackWorldItem.ClientSetStackHidden(hidden: false);
				}
			}
		}

		public void CacheWaitingPoints()
		{
			waitingSlots.Clear();
			waitingOwners.Clear();
			Add(WaitingPoint);
			foreach (Transform waitingPoint in WaitingPoints)
			{
				Add(waitingPoint);
			}
			foreach (Transform overflowWaitingSlot in overflowWaitingSlots)
			{
				Add(overflowWaitingSlot);
			}
			if (!(WaitingPoint != null) || !(WaitingPoint.parent != null))
			{
				return;
			}
			foreach (Transform item in WaitingPoint.parent)
			{
				if (item.name.StartsWith(WaitingPoint.name + " (") && item.name.EndsWith(")"))
				{
					Add(item);
				}
			}
			void Add(Transform point)
			{
				if (point != null && !waitingSlots.Contains(point))
				{
					waitingSlots.Add(point);
				}
			}
		}

		public Transform ClaimWaitingPoint(int employeeId, Vector3 origin)
		{
			if (waitingOwners.TryGetValue(employeeId, out var value))
			{
				if (value != null && value.gameObject.activeInHierarchy)
				{
					return value;
				}
				waitingOwners.Remove(employeeId);
			}
			Transform transform = null;
			float num = float.MaxValue;
			foreach (Transform waitingSlot in waitingSlots)
			{
				if (!(waitingSlot == null) && waitingSlot.gameObject.activeInHierarchy && !waitingOwners.ContainsValue(waitingSlot))
				{
					float sqrMagnitude = (waitingSlot.position - origin).sqrMagnitude;
					if (!(sqrMagnitude >= num) && TrySamplePosition(waitingSlot.position, out var _))
					{
						num = sqrMagnitude;
						transform = waitingSlot;
					}
				}
			}
			if (transform == null && WaitingPoint != null && waitingSlots.Count < 10)
			{
				for (int i = 1; i <= 4; i++)
				{
					if (!(transform == null))
					{
						break;
					}
					for (int j = 0; j < 12; j++)
					{
						if (!(transform == null))
						{
							break;
						}
						float f = (float)j * MathF.PI / 6f;
						Vector3 position = WaitingPoint.position + new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f)) * ((float)i * 1.25f);
						if (!TrySamplePosition(position, out var hit2))
						{
							continue;
						}
						bool flag = false;
						foreach (Transform waitingSlot2 in waitingSlots)
						{
							if (waitingSlot2 != null && (waitingSlot2.position - hit2.position).sqrMagnitude < 1f)
							{
								flag = true;
								break;
							}
						}
						if (!flag)
						{
							transform = new GameObject("Staff waiting overflow " + overflowWaitingSlots.Count).transform;
							transform.SetParent(base.transform, worldPositionStays: true);
							transform.SetPositionAndRotation(hit2.position, WaitingPoint.rotation);
							waitingSlots.Add(transform);
							overflowWaitingSlots.Add(transform);
						}
					}
				}
			}
			if (transform != null)
			{
				waitingOwners[employeeId] = transform;
			}
			return transform;
		}

		public void ReleaseWaitingPoint(int employeeId)
		{
			waitingOwners.Remove(employeeId);
		}

		internal bool OwnsWaitingDestination(int employeeId, Vector3 destination)
		{
			if (waitingOwners.TryGetValue(employeeId, out var value) && value != null)
			{
				return (value.position - destination).sqrMagnitude <= 0.0001f;
			}
			return false;
		}

		internal void ReleaseWaitingPointIfMovingElsewhere(int employeeId, Vector3 destination)
		{
			if (waitingOwners.TryGetValue(employeeId, out var value) && (value == null || (value.position - destination).sqrMagnitude > 0.0001f))
			{
				waitingOwners.Remove(employeeId);
			}
		}
	}
}
