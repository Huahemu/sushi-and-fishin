using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Core.ProcessedIngredient;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen;
using PaperPixelGames.Core.Kitchen.Wholasale;
using PaperPixelGames.Core.Restaurant;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;

namespace PaperPixelGames.Core.Staff
{
	public sealed class StaffServiceTrayPlan
	{
		public sealed class BatchSnapshot
		{
			internal readonly List<Tray> Trays = new List<Tray>();

			private readonly Dictionary<ServedDishSO, List<ServedDish>> dishes = new Dictionary<ServedDishSO, List<ServedDish>>();

			private readonly Dictionary<string, List<ServedDish>> dishesByRecipeId = new Dictionary<string, List<ServedDish>>();

			private readonly Dictionary<ServedDishSO, List<WholesaleEntity>> packs = new Dictionary<ServedDishSO, List<WholesaleEntity>>();

			private readonly Dictionary<ServedDishSO, List<SideDishIngredientEntity>> jars = new Dictionary<ServedDishSO, List<SideDishIngredientEntity>>();

			private readonly Dictionary<string, Transform> dropPoints = new Dictionary<string, Transform>();

			private readonly Dictionary<Tray, bool> homes = new Dictionary<Tray, bool>();

			private readonly Dictionary<Tray, int> relevance = new Dictionary<Tray, int>();

			private readonly CustomerSeatOrder[] orders;

			private readonly Dictionary<int, int> orderOccurrences = new Dictionary<int, int>();

			private readonly StaffWorkplaceRegistry world;

			private static readonly ProfilerMarker SnapshotMarker = new ProfilerMarker("Staff.Waitress.BatchSnapshot");

			public BatchSnapshot(StaffWorkplaceRegistry world)
			{
				using (SnapshotMarker.Auto())
				{
					this.world = world;
					foreach (BaseHoldable staffWorldItem in BaseHoldable.StaffWorldItems)
					{
						if (!Available(staffWorldItem, world))
						{
							continue;
						}
						if (staffWorldItem is Tray item)
						{
							Trays.Add(item);
						}
						if (staffWorldItem is ServedDish { ItemSO: ServedDishSO itemSO } servedDish)
						{
							Add(dishes, itemSO, servedDish);
							if (!string.IsNullOrWhiteSpace(servedDish.RuntimeRecipeIdentityId))
							{
								if (!dishesByRecipeId.TryGetValue(servedDish.RuntimeRecipeIdentityId, out var value))
								{
									dishesByRecipeId.Add(servedDish.RuntimeRecipeIdentityId, value = new List<ServedDish>());
								}
								value.Add(servedDish);
							}
						}
						if (staffWorldItem is WholesaleEntity wholesaleEntity && wholesaleEntity.WholesaleSO.OutputItem is ServedDishSO key)
						{
							Add(packs, key, wholesaleEntity);
						}
						if (staffWorldItem is SideDishIngredientEntity sideDishIngredientEntity && sideDishIngredientEntity.StaffOutputItem != null)
						{
							Add(jars, sideDishIngredientEntity.StaffOutputItem, sideDishIngredientEntity);
						}
					}
					orders = world.ServicePoints.Select((StaffServicePoint p) => p.Target).OfType<RestaurantTable>().SelectMany((RestaurantTable t) => t.ServerStaffOrders)
						.ToArray();
					CustomerSeatOrder[] array = orders;
					foreach (CustomerSeatOrder customerSeatOrder in array)
					{
						orderOccurrences.TryGetValue(customerSeatOrder.OrderId, out var value2);
						orderOccurrences[customerSeatOrder.OrderId] = value2 + 1;
					}
				}
			}

			private static void Add<T>(Dictionary<ServedDishSO, List<T>> index, ServedDishSO key, T item)
			{
				if (!index.TryGetValue(key, out var value))
				{
					index.Add(key, value = new List<T>());
				}
				value.Add(item);
			}

			private static IEnumerable<T> Get<T>(Dictionary<ServedDishSO, List<T>> index, ServedDishSO key)
			{
				if (!index.TryGetValue(key, out var value))
				{
					return Array.Empty<T>();
				}
				return value;
			}

			internal IEnumerable<ServedDish> Dishes(ServedDishSO item)
			{
				return Get(dishes, item);
			}

			internal IEnumerable<ServedDish> Dishes(string recipeId)
			{
				if (string.IsNullOrWhiteSpace(recipeId) || !dishesByRecipeId.TryGetValue(recipeId, out var value))
				{
					return Array.Empty<ServedDish>();
				}
				return value;
			}

			internal IEnumerable<WholesaleEntity> Packs(ServedDishSO item)
			{
				return Get(packs, item);
			}

			internal IEnumerable<SideDishIngredientEntity> Jars(ServedDishSO item)
			{
				return Get(jars, item);
			}

			internal Transform DropPoint(string itemId)
			{
				if (!dropPoints.TryGetValue(itemId, out var value))
				{
					value = (dropPoints[itemId] = world.ReadyDishPoint(itemId));
				}
				return value;
			}

			internal bool Home(Tray tray)
			{
				if (!homes.TryGetValue(tray, out var value))
				{
					value = (homes[tray] = world.TrayTrolleys.Any((TrayTrolley c) => c != null && c.ServerStaffAtHome(tray)));
				}
				return value;
			}

			internal int Relevant(Tray tray, CustomerSeatOrder seat)
			{
				if (!relevance.TryGetValue(tray, out var value))
				{
					value = (relevance[tray] = orders.Count((CustomerSeatOrder o) => tray.ServerStaffMatches(o.Order)));
				}
				orderOccurrences.TryGetValue(seat.OrderId, out var value2);
				return value - (tray.ServerStaffMatches(seat.Order) ? value2 : 0);
			}
		}

		public sealed class Source
		{
			public byte Slot;

			public ServedDishSO Item;

			public string IdentityId;

			public ServedDish Dish;

			public WholesaleEntity Pack;

			public SideDishIngredientEntity Jar;

			public BeverageFridge Fridge;

			public string Key
			{
				get
				{
					if (!(Dish != null))
					{
						if (!(Pack != null))
						{
							if (!(Jar != null))
							{
								return "beverage:" + Fridge.NetworkObjectId + ":" + IdentityId;
							}
							return ItemKey(Jar);
						}
						return ItemKey(Pack);
					}
					return ItemKey(Dish);
				}
			}

			public Vector3 Position
			{
				get
				{
					if (!(Dish != null))
					{
						if (!(Pack != null))
						{
							if (!(Jar != null))
							{
								return Fridge.transform.position;
							}
							return Jar.transform.position;
						}
						return Pack.transform.position;
					}
					return Dish.transform.position;
				}
			}
		}

		private static readonly ProfilerMarker PlanMarker = new ProfilerMarker("Staff.Waitress.PlanTray");

		public RestaurantTable Table;

		public int OrderId;

		public CustomerOrderData Order;

		public Tray Tray;

		public readonly List<Source> Sources = new List<Source>();

		public readonly HashSet<string> Keys = new HashSet<string>();

		public static string ItemKey(BaseHoldable item)
		{
			return "item:" + item.NetworkObjectId;
		}

		public static string OrderKey(int orderId)
		{
			return "service-order:" + orderId;
		}

		private static bool HasFridgeApproach(BeverageFridge fridge, StaffWorkplaceRegistry world)
		{
			if (fridge != null)
			{
				Transform transform = world.ServicePoint(fridge);
				NavMeshHit hit;
				if ((object)transform != null && transform.gameObject.activeInHierarchy)
				{
					return world.TrySamplePosition(transform.position, out hit);
				}
			}
			return false;
		}

		public static bool Available(BaseHoldable item, StaffWorkplaceRegistry world)
		{
			if (item == null || !item.IsSpawned || !item.CanHold.Value || item.IsStaffCarried || item.IsPlayerInventoryOwnedForPersistence || !world.IsInside(item.transform.position))
			{
				return false;
			}
			if (item.GetComponentInParent<ConveyorBelt>() != null)
			{
				return false;
			}
			Tray tray = item.GetComponentInParent<Tray>();
			if (tray != null && (tray.IsDirty.Value || !tray.CanHold.Value || tray.IsStaffCarried || tray.IsPlayerInventoryOwnedForPersistence))
			{
				return false;
			}
			if (tray != null)
			{
				TrayTrolley trayTrolley = world.TrayTrolleys.FirstOrDefault((TrayTrolley t) => t != null && t.ServerStaffAtHome(tray));
				if (trayTrolley != null && !trayTrolley.IsPhysicallyUsable)
				{
					return false;
				}
			}
			if (!(item.transform.parent == null) && (!(tray != null) || !(tray != item)))
			{
				if (item is Tray tray2)
				{
					TrayTrolley componentInParent = tray2.GetComponentInParent<TrayTrolley>();
					if ((object)componentInParent != null)
					{
						return componentInParent.ServerStaffAtHome(tray2);
					}
				}
				return false;
			}
			return true;
		}

		public bool Valid(int employee, StaffWorkplaceRegistry world, StaffReservationService reservations)
		{
			if (Table == null || !Table.IsSpawned || !Table.ServerStaffOrderValid(OrderId) || Tray == null || !Tray.IsSpawned || Tray.IsDirty.Value || (!Tray.ServerStaffCarriedBy(employee) && !Available(Tray, world)))
			{
				return false;
			}
			foreach (string key in Keys)
			{
				if (reservations.IsReservedByOther(key, employee))
				{
					return false;
				}
			}
			foreach (Source source in Sources)
			{
				ServedDish servedDish = Tray.ServerStaffDish(source.Slot);
				if (servedDish != null && ((source.Slot == 0) ? (servedDish.RuntimeRecipeIdentityId == source.IdentityId) : (servedDish.ItemSO?.Id == source.IdentityId)))
				{
					continue;
				}
				if (source.Dish != null)
				{
					if (!Available(source.Dish, world))
					{
						return false;
					}
				}
				else if (source.Pack != null)
				{
					if (!Available(source.Pack, world) || source.Pack.CurrentCount <= 0)
					{
						return false;
					}
				}
				else if (source.Jar != null)
				{
					if (!Available(source.Jar, world) || source.Jar.IngredientCount <= 0)
					{
						return false;
					}
				}
				else if (source.Fridge == null || source.Fridge.ServerStaffShelf(source.Item) < 0 || !HasFridgeApproach(source.Fridge, world))
				{
					return false;
				}
			}
			return true;
		}

		public static StaffServiceTrayPlan TryCreate(RestaurantTable table, CustomerSeatOrder seat, StaffWorkplaceRegistry world, StaffReservationService reservations, int employee, Vector3 origin, HashSet<string> batchKeys, BatchSnapshot snapshot = null)
		{
			using (PlanMarker.Auto())
			{
				string text = OrderKey(seat.OrderId);
				if (batchKeys.Contains(text) || reservations.IsReservedByOther(text, employee))
				{
					return null;
				}
				if (snapshot == null)
				{
					snapshot = new BatchSnapshot(world);
				}
				foreach (Tray tray in snapshot.Trays.Where(Free).OrderBy((Tray t) =>
				{
					if (!t.ServerStaffEmpty)
					{
						if (!t.ServerStaffMatches(seat.Order))
						{
							return 3;
						}
						return 2;
					}
					return Home(t) ? 1 : 0;
				}).ThenBy(Relevant)
					.ThenBy((Tray t) => Vector3.SqrMagnitude(t.transform.position - origin)))
				{
					if (!tray.ServerStaffEmpty && !tray.ServerStaffMatches(seat.Order) && !Home(tray))
					{
						continue;
					}
					StaffServiceTrayPlan plan = new StaffServiceTrayPlan
					{
						Table = table,
						OrderId = seat.OrderId,
						Order = seat.Order,
						Tray = tray
					};
					plan.Keys.Add(text);
					plan.Keys.Add(ItemKey(tray));
					ServedDishSO[] array = new ServedDishSO[3]
					{
						seat.Order.MainDishSO,
						seat.Order.SideDishSO,
						seat.Order.BeverageSO
					};
					bool flag = true;
					byte b = 0;
					while (b < 3)
					{
						ServedDish servedDish = tray.ServerStaffDish(b);
						string text2 = ((b == 0) ? seat.Order.MainDishIdentityId : array[b]?.Id);
						bool flag2 = servedDish != null && ((b == 0) ? (servedDish.RuntimeRecipeIdentityId == text2) : (servedDish.ItemSO?.Id == text2));
						if (servedDish != null && !flag2 && DropPoint(servedDish.RuntimeRecipeIdentityId) == null)
						{
							flag = false;
							break;
						}
						Transform middle;
						if (!(string.IsNullOrWhiteSpace(text2) | flag2))
						{
							ServedDishSO servedDishSO = array[b];
							string text3 = ((servedDishSO != null && world.BeverageFridge != null) ? ("beverage:" + world.BeverageFridge.NetworkObjectId + ":" + servedDishSO.Id) : null);
							bool flag3 = servedDishSO != null && world.BeverageFridge != null && world.BeverageFridge.ServerStaffShelf(servedDishSO) >= 0 && !batchKeys.Contains(text3) && !reservations.IsReservedByOther(text3, employee) && HasFridgeApproach(world.BeverageFridge, world);
							middle = DropPoint(text2);
							ServedDish servedDish2 = (flag3 ? null : snapshot.Dishes(text2).Where((ServedDish d) =>
							{
								if (Free(d) && d.GetComponentInParent<Tray>() != tray && !plan.Keys.Contains(ItemKey(d)))
								{
									Tray componentInParent2 = d.GetComponentInParent<Tray>();
									if ((object)componentInParent2 != null)
									{
										if (!batchKeys.Contains(ItemKey(componentInParent2)))
										{
											return !reservations.IsReservedByOther(ItemKey(componentInParent2), employee);
										}
										return false;
									}
									return true;
								}
								return false;
							}).OrderBy(Priority)
								.ThenBy((ServedDish d) => (d.transform.position - origin).sqrMagnitude)
								.FirstOrDefault());
							Source source = new Source
							{
								Slot = b,
								Item = servedDishSO,
								IdentityId = text2,
								Dish = servedDish2
							};
							if (servedDish2 == null)
							{
								if (flag3)
								{
									source.Fridge = world.BeverageFridge;
								}
								else if (servedDishSO != null)
								{
									source.Pack = snapshot.Packs(servedDishSO).FirstOrDefault((WholesaleEntity p) => Free(p) && p.CurrentCount > 0 && !plan.Keys.Contains(ItemKey(p)));
								}
								if (source.Fridge == null && source.Pack == null)
								{
									source.Jar = ((servedDishSO != null) ? snapshot.Jars(servedDishSO).FirstOrDefault((SideDishIngredientEntity j) => Free(j) && j.IngredientCount > 0 && !plan.Keys.Contains(ItemKey(j))) : null);
								}
								if (source.Fridge == null && source.Pack == null && source.Jar == null)
								{
									flag = false;
									break;
								}
							}
							if (servedDish2 == null && (servedDishSO?.Prefab == null || servedDishSO.Prefab.GetComponent<ServedDish>() == null))
							{
								flag = false;
								break;
							}
							plan.Sources.Add(source);
							plan.Keys.Add(source.Key);
							if (servedDish2 != null)
							{
								Tray componentInParent = servedDish2.GetComponentInParent<Tray>();
								if ((object)componentInParent != null)
								{
									plan.Keys.Add(ItemKey(componentInParent));
								}
							}
						}
						b++;
						int Priority(ServedDish d)
						{
							if (middle != null && (d.transform.position - middle.position).sqrMagnitude < 0.3f)
							{
								return 0;
							}
							if (d.GetComponentInParent<Tray>() != null)
							{
								return 1;
							}
							if (!world.ChefDesks.Any((StaffChefWorkplace c) => c.OutputPoint != null && (d.transform.position - c.OutputPoint.position).sqrMagnitude < 1f))
							{
								return 2;
							}
							return 3;
						}
					}
					if (!flag)
					{
						continue;
					}
					foreach (string key in plan.Keys)
					{
						reservations.TryReserve(key, employee);
					}
					foreach (string key2 in plan.Keys)
					{
						batchKeys.Add(key2);
					}
					return plan;
				}
				return null;
			}
			Transform DropPoint(string itemId)
			{
				return snapshot.DropPoint(itemId);
			}
			bool Free(BaseHoldable item)
			{
				if (!batchKeys.Contains(ItemKey(item)))
				{
					return !reservations.IsReservedByOther(ItemKey(item), employee);
				}
				return false;
			}
			bool Home(Tray t)
			{
				return snapshot.Home(t);
			}
			int Relevant(Tray t)
			{
				return snapshot.Relevant(t, seat);
			}
		}
	}
}
