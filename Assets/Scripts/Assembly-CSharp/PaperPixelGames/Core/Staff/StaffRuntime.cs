using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Core.ProcessedIngredient;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen.Wholasale;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.ProcessedIngredient;
using PaperPixelGames.Core.TrashBin;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;

namespace PaperPixelGames.Core.Staff
{
	public sealed class StaffRuntime : IDisposable
	{
		private enum Phase
		{
			Select = 0,
			Collect = 1,
			Exit = 2,
			Market = 3,
			Return = 4,
			Store = 5
		}

		private sealed class Purchase
		{
			public WholesaleSO Product;

			public string Need;

			public int Count;

			public float Yield;
		}

		private sealed class Worker
		{
			public int Id;

			public StaffNpc Npc;

			public Phase Phase;

			public readonly List<BaseHoldable> Carry = new List<BaseHoldable>();

			public readonly List<BaseHoldable> Collection = new List<BaseHoldable>();

			public readonly List<Purchase> Purchases = new List<Purchase>();

			public bool Blue;

			public bool MarketCommitted;

			public readonly HashSet<StaffStatus> Feedback = new HashSet<StaffStatus>();

			public int FailedBalance = -1;

			public string FailedFingerprint;

			public readonly StaffPurchaseRetryPolicy PurchaseRetries = new StaffPurchaseRetryPolicy();

			public bool CompletedWork;

			public StaffPrepWorker Prep;

			public StaffChefWorker Chef;

			public StaffWaitressWorker Waitress;

			public float LastTick;
		}

		private readonly StaffManager manager;

		private readonly Dictionary<int, Worker> workers = new Dictionary<int, Worker>();

		private readonly HashSet<int> pending = new HashSet<int>();

		private readonly StaffReservationService reservations = new StaffReservationService();

		private readonly StaffTaskBoard tasks = new StaffTaskBoard();

		private float nextTick;

		private int lastSpawnFrame = -1;

		private float nextWorkerTick;

		private readonly Queue<Worker> scheduled = new Queue<Worker>();

		private static readonly ProfilerMarker TickMarker = new ProfilerMarker("Staff.Runtime.Tick");

		private static readonly ProfilerMarker PrepMarker = new ProfilerMarker("Staff.Prep.Tick");

		private static readonly ProfilerMarker ChefMarker = new ProfilerMarker("Staff.Chef.Tick");

		private static readonly ProfilerMarker WaitressMarker = new ProfilerMarker("Staff.Waitress.Tick");

		private static readonly ProfilerMarker StockerMarker = new ProfilerMarker("Staff.Stocker.Tick");

		public StaffRuntime(StaffManager manager)
		{
			this.manager = manager;
		}

		public bool IsItemReserved(ulong itemId, int employee)
		{
			return reservations.IsReservedByOther("item:" + itemId, employee);
		}

        // Exact original employee body registry for the friend adapter.
        public bool TryGetNpc(int employeeId,out StaffNpc npc)
        {
            npc=null;
            if(!workers.TryGetValue(employeeId,out var worker)||!worker.Npc||!worker.Npc.IsSpawned)return false;
            npc=worker.Npc;return true;
        }

		public bool CanSpawn(out string reason, StaffRole role = StaffRole.Stocker)
		{
			reason = StaffText.Get("missing_workplace");
			if (role == StaffRole.Waitress)
			{
				StaffWorkplaceRegistry instance = StaffWorkplaceRegistry.Instance;
				if ((object)instance == null || !instance.WaitressReady)
				{
					return false;
				}
			}
			if (role == StaffRole.PrepCook && FindPrepStation() == null)
			{
				return false;
			}
			if (role == StaffRole.SushiChef && FindChefDesk() == null)
			{
				return false;
			}
			if (StaffWorkplaceRegistry.Instance != null && StaffWorkplaceRegistry.Instance.IsReady)
			{
				return manager.Catalog?.NpcPrefab != null;
			}
			return false;
		}

		private StaffPrepWorkplace FindPrepStation()
		{
			return StaffWorkplaceRegistry.Instance?.PrepStations.FirstOrDefault((StaffPrepWorkplace p) => p.IsReady && p.Station.ServerStaffAvailable);
		}

		private StaffChefWorkplace FindChefDesk()
		{
			return StaffWorkplaceRegistry.Instance?.ChefDesks.FirstOrDefault((StaffChefWorkplace p) => p.IsReady && p.Desk.ServerStaffAvailable);
		}

		public void QueueSpawn(int id)
		{
			pending.Add(id);
		}

		public void Spawn(StaffEmployeeEntry employee)
		{
			if (workers.ContainsKey(employee.EmployeeId))
			{
				return;
			}
			if (lastSpawnFrame == Time.frameCount)
			{
				pending.Add(employee.EmployeeId);
				return;
			}
			if (!CanSpawn(out var _, employee.Role))
			{
				pending.Add(employee.EmployeeId);
				return;
			}
			StaffWorkplaceRegistry instance = StaffWorkplaceRegistry.Instance;
			StaffPrepWorkplace staffPrepWorkplace = ((employee.Role == StaffRole.PrepCook) ? FindPrepStation() : null);
			if (employee.Role == StaffRole.PrepCook && (staffPrepWorkplace == null || !staffPrepWorkplace.Station.ServerAssignStaff(employee.EmployeeId)))
			{
				pending.Add(employee.EmployeeId);
				return;
			}
			StaffChefWorkplace staffChefWorkplace = ((employee.Role == StaffRole.SushiChef) ? FindChefDesk() : null);
			if (employee.Role == StaffRole.SushiChef && (staffChefWorkplace == null || !staffChefWorkplace.Desk.ServerAssignStaff(employee.EmployeeId)))
			{
				pending.Add(employee.EmployeeId);
				return;
			}
			StaffNpc staffNpc = UnityEngine.Object.Instantiate(manager.Catalog.NpcPrefab, instance.SpawnPoint.position, instance.SpawnPoint.rotation);
			lastSpawnFrame = Time.frameCount;
			staffNpc.NetworkObject.Spawn(destroyWithScene: true);
			staffNpc.Initialize(employee);
			workers.Add(employee.EmployeeId, new Worker
			{
				Id = employee.EmployeeId,
				Npc = staffNpc,
				LastTick = Time.time,
				Prep = ((staffPrepWorkplace != null) ? new StaffPrepWorker(manager, staffNpc, staffPrepWorkplace, reservations) : null),
				Chef = ((staffChefWorkplace != null) ? new StaffChefWorker(manager, staffNpc, staffChefWorkplace, reservations) : null),
				Waitress = ((employee.Role == StaffRole.Waitress) ? new StaffWaitressWorker(manager, staffNpc, reservations) : null)
			});
		}

		public void Tick()
		{
			using (TickMarker.Auto())
			{
				float num = Mathf.Max(0.1f, (manager.Catalog != null) ? manager.Catalog.TaskPollInterval : 0.5f);
				StaffWorkplaceRegistry instance = StaffWorkplaceRegistry.Instance;
				if (Time.time >= nextTick)
				{
					tasks.Retain(new HashSet<string>(BaseHoldable.StaffWorldItems.Where((BaseHoldable i) => i != null && i.IsSpawned).Select(Key)));
					nextTick = Time.time + num;
					int[] array = pending.ToArray();
					foreach (int num3 in array)
					{
						if (!manager.TryGetEmployee(num3, out var employee))
						{
							pending.Remove(num3);
							continue;
						}
						if (employee.Departure != StaffDeparture.None)
						{
							manager.ServerCompleteDeparture(num3);
							pending.Remove(num3);
							continue;
						}
						// A friend employee between shifts keeps its team entry but has no body.
						if (SushiAndFish.FriendsLife.FriendJobs.OffShift(num3))
						{
							pending.Remove(num3);
							continue;
						}
						if (CanSpawn(out var _, employee.Role))
						{
							pending.Remove(num3);
							Spawn(employee);
							break;
						}
						manager.ServerSetStatus(num3, StaffStatus.MissingWorkplace);
					}
					if (scheduled.Count == 0)
					{
						foreach (Worker value2 in workers.Values)
						{
							scheduled.Enqueue(value2);
						}
						nextWorkerTick = Time.time;
					}
				}
				if (instance == null)
				{
					return;
				}
				GameProgressManager instance2 = Singleton<GameProgressManager>.Instance;
				if ((object)instance2 == null || instance2.RestaurantManager?.ServerStaffWorldReady != true || Time.time < nextWorkerTick)
				{
					return;
				}
				while (scheduled.Count > 0)
				{
					Worker worker = scheduled.Dequeue();
					if (!workers.TryGetValue(worker.Id, out var value) || value != worker)
					{
						continue;
					}
					if (!manager.TryGetEmployee(worker.Id, out var employee2))
					{
						Remove(worker);
						continue;
					}
					if (worker.Npc == null)
					{
						Remove(worker);
						pending.Add(employee2.EmployeeId);
						continue;
					}
					// Off shift a friend employee walks out like a departure; its team entry stays.
					if (SushiAndFish.FriendsLife.FriendJobs.OffShift(worker.Id))
					{
						employee2.Departure = StaffDeparture.Fired;
					}
					nextWorkerTick = Mathf.Max(Time.time, nextWorkerTick + num / (float)Mathf.Max(1, workers.Count));
					float num4 = Time.time - worker.LastTick;
					worker.LastTick = Time.time;
					if (!employee2.StockingStopped && employee2.Departure == StaffDeparture.None)
					{
						manager.Ledger.Tick(employee2, num4);
					}
					if (worker.Prep != null)
					{
						using (PrepMarker.Auto())
						{
							if (worker.Prep.Tick(employee2, instance))
							{
								manager.ServerCompleteDeparture(worker.Id);
								Remove(worker);
							}
							break;
						}
					}
					if (worker.Chef != null)
					{
						using (ChefMarker.Auto())
						{
							if (worker.Chef.Tick(employee2, instance))
							{
								manager.ServerCompleteDeparture(worker.Id);
								Remove(worker);
							}
							break;
						}
					}
					if (worker.Waitress != null)
					{
						using (WaitressMarker.Auto())
						{
							if (worker.Waitress.Tick(employee2, instance))
							{
								manager.ServerCompleteDeparture(worker.Id);
								Remove(worker);
							}
							break;
						}
					}
					using (StockerMarker.Auto())
					{
						worker.Carry.RemoveAll((BaseHoldable item) => item == null || !item.IsSpawned);
						foreach (BaseHoldable item in worker.Carry)
						{
							item.transform.position = worker.Npc.transform.position;
						}
						worker.Npc.SetHeldItem((worker.Carry.Count > 0) ? worker.Carry[0] : null);
						TickWorker(worker, employee2, instance);
						break;
					}
				}
			}
		}

		private void TickWorker(Worker worker, StaffEmployeeEntry employee, StaffWorkplaceRegistry world)
		{
			worker.Npc.SetRunning(manager.Catalog.CourierRuns(employee));
			bool flag = employee.StockingStopped || employee.Departure != StaffDeparture.None;
			if (flag && worker.Phase != Phase.Market && worker.Phase != Phase.Return && worker.Phase != Phase.Store)
			{
				ReleasePlan(worker);
				worker.Phase = ((worker.Carry.Count > 0) ? Phase.Store : Phase.Select);
			}
			if (worker.Phase == Phase.Select)
			{
				if (worker.Carry.Count > 0)
				{
					worker.Phase = Phase.Store;
				}
				else if (employee.Departure != StaffDeparture.None)
				{
					if (Move(worker, world.SpawnPoint))
					{
						manager.ServerCompleteDeparture(worker.Id);
						Remove(worker);
					}
				}
				else if (flag)
				{
					Status(worker, StaffStatus.Stopped);
					Move(worker, world.ClaimWaitingPoint(worker.Id, worker.Npc.transform.position));
				}
				else
				{
					Select(worker, employee, world);
				}
			}
			else if (worker.Phase == Phase.Collect)
			{
				if (worker.Collection.Count == 0)
				{
					worker.Phase = Phase.Store;
					return;
				}
				BaseHoldable baseHoldable = worker.Collection[0];
				if (!Collectible(baseHoldable, world, employee) || reservations.IsReservedByOther(Key(baseHoldable), worker.Id))
				{
					if (baseHoldable != null)
					{
						reservations.Release(Key(baseHoldable), worker.Id);
					}
					worker.Collection.RemoveAt(0);
					return;
				}
				Status(worker, StaffStatus.CollectingItems);
				if (Move(worker, baseHoldable.transform.position))
				{
					if (Collectible(baseHoldable, world, employee) && reservations.TryReserve(Key(baseHoldable), worker.Id) && baseHoldable.ServerTryStaffPickup(worker.Id))
					{
						worker.Carry.Add(baseHoldable);
						tasks.Complete(Key(baseHoldable));
					}
					reservations.Release(Key(baseHoldable), worker.Id);
					worker.Collection.RemoveAt(0);
				}
			}
			else if (worker.Phase == Phase.Exit)
			{
				if ((!employee.BuyMissingStock || !(worker.Blue ? employee.BlueMarketEnabled : employee.GreenMarketEnabled)) && world.IsInside(worker.Npc.transform.position))
				{
					ReleasePlan(worker);
					worker.Phase = Phase.Select;
					return;
				}
				Status(worker, worker.Blue ? StaffStatus.GoingToBlueMarket : StaffStatus.GoingToGreenMarket);
				if (Move(worker, world.ExitPoint))
				{
					worker.Phase = Phase.Market;
				}
			}
			else if (worker.Phase == Phase.Market)
			{
				if (!worker.MarketCommitted)
				{
					if (!world.IsInside(worker.Npc.transform.position))
					{
						worker.MarketCommitted = true;
					}
					else if (flag || !employee.BuyMissingStock || !(worker.Blue ? employee.BlueMarketEnabled : employee.GreenMarketEnabled))
					{
						ReleasePlan(worker);
						worker.Phase = ((worker.Carry.Count > 0) ? Phase.Store : Phase.Select);
						return;
					}
				}
				Transform transform = (worker.Blue ? world.BlueMarketPoint : world.GreenMarketPoint);
				if (transform == null)
				{
					ReleasePlan(worker);
					worker.Phase = Phase.Return;
				}
				else
				{
					if (!Move(worker, transform))
					{
						return;
					}
					Status(worker, StaffStatus.BuyingSupplies);
					if (worker.Purchases.Count == 0)
					{
						worker.Phase = Phase.Return;
						return;
					}
					Purchase purchase = worker.Purchases[0];
					bool flag2 = new StaffStockPolicy(world).Build().TryGetValue(purchase.Need, out var value) && value.Missing > 0f;
					StaffStatus failure = StaffStatus.PurchaseFailed;
					if (flag2 && StaffGameplayApi.TryPurchase(employee, purchase.Product, worker.Blue, world, out var item, out var _, out failure))
					{
						worker.PurchaseRetries.Clear(purchase.Product.Id, worker.Blue);
						worker.Carry.Add(item);
						purchase.Count--;
						worker.FailedBalance = -1;
						worker.Feedback.Clear();
					}
					else
					{
						purchase.Count = 0;
						if (flag2)
						{
							worker.PurchaseRetries.Record(purchase.Product.Id, worker.Blue, failure, Time.timeAsDouble, Singleton<GameProgressManager>.Instance.EconomyHandler.ShopBalance.Value, Singleton<GameProgressManager>.Instance.IsWholesaleProgressionUnlocked(purchase.Product), worker.Blue ? employee.BlueMarketEnabled : employee.GreenMarketEnabled, world.MarketOpen(worker.Blue));
							Feedback(worker, failure);
						}
					}
					if (purchase.Count <= 0)
					{
						worker.Purchases.RemoveAt(0);
					}
					if (worker.Purchases.Count == 0)
					{
						worker.Phase = Phase.Return;
					}
				}
			}
			else if (worker.Phase == Phase.Return)
			{
				Status(worker, StaffStatus.ReturningToRestaurant);
				if (Move(worker, world.ExitPoint))
				{
					worker.Phase = Phase.Store;
				}
			}
			else
			{
				if (worker.Phase != Phase.Store)
				{
					return;
				}
				worker.Collection.Clear();
				if (worker.Carry.Count == 0)
				{
					if (worker.CompletedWork)
					{
						manager.Ledger.Task(employee);
					}
					worker.CompletedWork = false;
					ReleasePlan(worker);
					worker.Phase = Phase.Select;
					return;
				}
				BaseHoldable baseHoldable2 = worker.Carry[0];
				if (PaperPixelGames.Core.TrashBin.TrashBin.IsEmptyStaffContainer(baseHoldable2))
				{
					Status(worker, StaffStatus.DiscardingEmptyBoxes);
					if (!world.TryFindTrash(worker.Npc.transform.position, out var trash, out var point))
					{
						Feedback(worker, StaffStatus.MissingWorkplace);
						worker.Npc.Stop();
					}
					else if (Move(worker, point) && trash.ServerStaffDiscardEmptyBox(worker.Id, baseHoldable2))
					{
						worker.Carry.RemoveAt(0);
						worker.CompletedWork = true;
						worker.Npc.SetHeldItem((worker.Carry.Count > 0) ? worker.Carry[0] : null);
					}
					return;
				}
				if (CanStockFridge(baseHoldable2, world))
				{
					Status(worker, StaffStatus.StockingItems);
					if (!Move(worker, world.ServicePoint(world.BeverageFridge)))
					{
						return;
					}
					if (world.BeverageFridge.ServerStaffStock(worker.Id, (WholesaleEntity)baseHoldable2) > 0)
					{
						worker.CompletedWork = true;
						worker.Npc.SetHeldItem(baseHoldable2);
						return;
					}
				}
				Transform transform2 = world.StorageFor(baseHoldable2.ItemSO.Id);
				if (transform2 == null || !world.TrySamplePosition(transform2.position, out var _))
				{
					transform2 = world.ClaimWaitingPoint(worker.Id, worker.Npc.transform.position);
					if (transform2 == null)
					{
						Feedback(worker, StaffStatus.MissingWorkplace);
						return;
					}
				}
				Status(worker, StaffStatus.StockingItems);
				if (Move(worker, transform2))
				{
					baseHoldable2.ServerStaffDrop(worker.Id, transform2.position, transform2.rotation);
					if (transform2 == world.StorageFor(baseHoldable2.ItemSO.Id))
					{
						StaffQuestProgress.Complete(QuestId.PlaceIngredientStorage);
					}
					worker.CompletedWork = true;
					worker.Carry.RemoveAt(0);
					worker.Npc.SetHeldItem((worker.Carry.Count > 0) ? worker.Carry[0] : null);
				}
			}
		}

		private bool Move(Worker worker, Transform destination)
		{
			if (destination == null)
			{
				worker.Npc.Stop();
				return false;
			}
			bool num = Move(worker, destination.position);
			if (num)
			{
				worker.Npc.FacePoint(destination);
			}
			return num;
		}

		private bool Move(Worker worker, Vector3 destination)
		{
			float speed = (manager.TryGetEmployee(worker.Id, out var employee) ? manager.Catalog.CourierMoveSpeed(employee) : manager.Catalog.StockerMoveSpeed);
			bool result = worker.Npc.MoveTo(destination, speed);
			if (worker.Npc.InvalidTarget)
			{
				ReleasePlan(worker);
				worker.Phase = ((worker.Carry.Count > 0) ? Phase.Store : Phase.Select);
				Feedback(worker, StaffStatus.MissingWorkplace);
			}
			if (worker.Npc.Recovered)
			{
				reservations.ReleaseAll(worker.Id);
				worker.Collection.RemoveAll((BaseHoldable item) => item == null || !item.IsSpawned || !reservations.TryReserve(Key(item), worker.Id));
			}
			return result;
		}

		private void Select(Worker worker, StaffEmployeeEntry employee, StaffWorkplaceRegistry world)
		{
			StaffStockPolicy staffStockPolicy = new StaffStockPolicy(world);
			Dictionary<string, StaffStockNeed> dictionary = staffStockPolicy.Build();
			Dictionary<string, int> dictionary2 = new Dictionary<string, int>();
			List<BaseHoldable> list = BaseHoldable.StaffWorldItems.Where((BaseHoldable item) => Collectible(item, world, employee) && !reservations.IsReservedByOther(Key(item), worker.Id)).ToList();
			double now = manager.NetworkManager.ServerTime.Time;
			list.Sort((BaseHoldable a, BaseHoldable b) => CollectionScore(b, worker, now).CompareTo(CollectionScore(a, worker, now)));
			foreach (BaseHoldable item in list)
			{
				string quickSlotStackId = item.QuickSlotStackId;
				dictionary2.TryGetValue(quickSlotStackId, out var value);
				if ((value != 0 || dictionary2.Count < employee.SkillLevel) && value < item.ItemSO.MaxStack && reservations.TryReserve(Key(item), worker.Id))
				{
					dictionary2[quickSlotStackId] = value + 1;
					worker.Collection.Add(item);
				}
			}
			bool flag = dictionary.Values.Any((StaffStockNeed n) => n.Priority == 3);
			if (worker.Collection.Count > 0 && !flag)
			{
				worker.Phase = Phase.Collect;
			}
			else if (employee.BuyMissingStock && PlanPurchase(worker, employee, world, staffStockPolicy, dictionary))
			{
				foreach (BaseHoldable item2 in worker.Collection)
				{
					reservations.Release(Key(item2), worker.Id);
				}
				worker.Collection.Clear();
				worker.MarketCommitted = false;
				worker.Phase = Phase.Exit;
			}
			else if (worker.Collection.Count > 0)
			{
				worker.Phase = Phase.Collect;
			}
			else
			{
				if (worker.Feedback.Count == 0)
				{
					Status(worker, StaffStatus.Waiting);
				}
				Move(worker, world.ClaimWaitingPoint(worker.Id, worker.Npc.transform.position));
			}
		}

		private double CollectionScore(BaseHoldable item, Worker worker, double now)
		{
			return tasks.Score(Key(item), now, 0.0, 100f / (1f + Vector3.Distance(item.transform.position, worker.Npc.transform.position)));
		}

		private bool PlanPurchase(Worker worker, StaffEmployeeEntry employee, StaffWorkplaceRegistry world, StaffStockPolicy stock, Dictionary<string, StaffStockNeed> needs)
		{
			int num = (Singleton<GameProgressManager>.Instance?.EconomyHandler?.ShopBalance.Value).GetValueOrDefault();
			foreach (Worker value2 in workers.Values)
			{
				if (value2.Id == worker.Id)
				{
					continue;
				}
				foreach (Purchase purchase in value2.Purchases)
				{
					if (needs.TryGetValue(purchase.Need, out var value))
					{
						value.Current += (float)purchase.Count * purchase.Yield;
					}
				}
			}
			string text = string.Join(";", from n in needs.Values
				orderby n.Id
				select n.Id + ":" + n.Missing) + ":" + employee.GreenMarketEnabled + employee.BlueMarketEnabled + world.GreenMarketOpen + world.BlueMarketOpen + (Singleton<GameProgressManager>.Instance?.ProgressionManager?.CurrentLevel).GetValueOrDefault();
			if (!worker.PurchaseRetries.HasFailures && worker.FailedBalance == num && worker.FailedFingerprint == text)
			{
				return false;
			}
			List<(WholesaleSO, StaffStockNeed, bool, float)> list = new List<(WholesaleSO, StaffStockNeed, bool, float)>();
			bool flag = false;
			StaffStatus? staffStatus = null;
			bool[] array = new bool[2] { false, true };
			foreach (bool flag2 in array)
			{
				bool flag3 = (flag2 ? employee.BlueMarketEnabled : employee.GreenMarketEnabled);
				foreach (WholesaleSO item in world.Products(flag2))
				{
					if (item?.OutputItem == null || item.Quantity <= 0 || world.StorageFor(item.Id) == null)
					{
						continue;
					}
					bool flag4 = Singleton<GameProgressManager>.Instance.IsWholesaleProgressionUnlocked(item);
					bool flag5 = worker.PurchaseRetries.IsBlocked(item.Id, flag2, Time.timeAsDouble, num, flag4, flag3, world.MarketOpen(flag2), out var status);
					if (!flag3 || !world.MarketOpen(flag2))
					{
						continue;
					}
					foreach (StaffStockNeed value3 in needs.Values)
					{
						if (value3.Missing <= 0f)
						{
							continue;
						}
						float num3 = stock.PackageYield(item, value3.Id);
						if (num3 <= 0f)
						{
							continue;
						}
						if (!flag4)
						{
							flag = true;
						}
						else if (flag5)
						{
							StaffStatus valueOrDefault = staffStatus.GetValueOrDefault();
							if (!staffStatus.HasValue)
							{
								valueOrDefault = status;
								staffStatus = valueOrDefault;
							}
						}
						else
						{
							list.Add((item, value3, flag2, num3));
						}
					}
				}
			}
			list.Sort(((WholesaleSO product, StaffStockNeed need, bool blue, float yield) a, (WholesaleSO product, StaffStockNeed need, bool blue, float yield) b) =>
			{
				int num6 = b.need.Priority.CompareTo(a.need.Priority);
				if (num6 != 0)
				{
					return num6;
				}
				int num7 = (b.need.Missing / b.need.Target).CompareTo(a.need.Missing / a.need.Target);
				if (num7 != 0)
				{
					return num7;
				}
				int num8 = ((float)StaffStockPolicy.PackagePrice(a.product) / a.yield).CompareTo((float)StaffStockPolicy.PackagePrice(b.product) / b.yield);
				return (num8 == 0) ? string.CompareOrdinal(a.product.Id, b.product.Id) : num8;
			});
			HashSet<string> hashSet = new HashSet<string>();
			foreach (var item2 in list)
			{
				if (worker.Purchases.Count > 0 && worker.Blue != item2.Item3)
				{
					continue;
				}
				BaseHoldable baseHoldable = ((item2.Item1.Prefab != null) ? item2.Item1.Prefab.GetComponent<BaseHoldable>() : null);
				if (baseHoldable?.ItemSO == null)
				{
					continue;
				}
				string quickSlotStackId = baseHoldable.QuickSlotStackId;
				if (hashSet.Count < employee.SkillLevel && !hashSet.Contains(quickSlotStackId))
				{
					int num4 = StaffStockPolicy.PackagePrice(item2.Item1);
					int num5 = StaffStockPolicy.PackagesRequired(item2.Item2.Missing, item2.Item4, baseHoldable.ItemSO.MaxStack, num, num4);
					if (num5 > 0)
					{
						worker.Blue = item2.Item3;
						worker.Purchases.Add(new Purchase
						{
							Product = item2.Item1,
							Need = item2.Item2.Id,
							Count = num5,
							Yield = item2.Item4
						});
						hashSet.Add(quickSlotStackId);
						num -= num5 * num4;
						item2.Item2.Current += (float)num5 * item2.Item4;
					}
				}
			}
			if (worker.Purchases.Count > 0)
			{
				worker.FailedBalance = -1;
				worker.Feedback.Clear();
				return true;
			}
			if (list.Count > 0)
			{
				Feedback(worker, StaffStatus.InsufficientMoney);
			}
			else if (flag)
			{
				Feedback(worker, StaffStatus.ProductsLocked);
			}
			else if (staffStatus.HasValue)
			{
				Feedback(worker, staffStatus.Value);
			}
			else
			{
				worker.Feedback.Clear();
			}
			worker.FailedBalance = (Singleton<GameProgressManager>.Instance?.EconomyHandler?.ShopBalance.Value).GetValueOrDefault();
			worker.FailedFingerprint = text;
			return false;
		}

		private static string Key(BaseHoldable item)
		{
			return "item:" + item.NetworkObjectId;
		}

		private bool Collectible(BaseHoldable item, StaffWorkplaceRegistry world, StaffEmployeeEntry employee)
		{
			if (item == null || !item.IsSpawned || item.IsStaffCarried || !item.CanHold.Value || item.IsPlayerInventoryOwnedForPersistence || item.transform.parent != null)
			{
				return false;
			}
			if (manager.ProtectKitchenShelf && StaffStockerProtectedShelf.ContainsItemPosition(item.transform.position))
			{
				return false;
			}
			if (!manager.CollectStationStorage && StaffStockerProtectedShelf.ContainsItem(item, StaffStockerProtectedShelf.ShelfKind.StationStorage))
			{
				return false;
			}
			if (!employee.CollectFilletsEnabled && item is IngredientEntity && item.ItemSO is IngredientSO ingredient && IsFillet(ingredient, world))
			{
				return false;
			}
			if (!(item is WholesaleEntity) && !(item is SideDishIngredientEntity) && (!(item is IngredientEntity) || !(item.ItemSO is IngredientSO)))
			{
				return false;
			}
			if (item.ItemSO == null || !world.IsInside(item.transform.position))
			{
				return false;
			}
			if (!world.TrySamplePosition(item.transform.position, out var _))
			{
				return false;
			}
			PaperPixelGames.Core.TrashBin.TrashBin trash;
			Transform point;
			if (PaperPixelGames.Core.TrashBin.TrashBin.IsEmptyStaffContainer(item))
			{
				return world.TryFindTrash(item.transform.position, out trash, out point);
			}
			if (CanStockFridge(item, world))
			{
				return true;
			}
			Transform transform = world.StorageFor(item.ItemSO.Id);
			if (transform != null)
			{
				return Vector3.Distance(item.transform.position, transform.position) > 0.8f;
			}
			return false;
		}

		private static bool IsFillet(IngredientSO ingredient, StaffWorkplaceRegistry world)
		{
			if (ingredient == null || world.Database == null)
			{
				return false;
			}
			foreach (ItemSO creatureItem in world.Database.CreatureItems)
			{
				if (creatureItem is CreatureSO creatureSO && creatureSO.IngredientSO == ingredient)
				{
					return true;
				}
			}
			return false;
		}

		private static bool CanStockFridge(BaseHoldable item, StaffWorkplaceRegistry world)
		{
			if (item is WholesaleEntity { CurrentCount: >0 } wholesaleEntity && wholesaleEntity.WholesaleSO?.OutputItem is BeverageSO item2 && world.BeverageFridge != null && world.BeverageFridge.ServerStaffSpace(item2) > 0)
			{
				Transform transform = world.ServicePoint(world.BeverageFridge);
				NavMeshHit hit;
				if ((object)transform != null)
				{
					return world.TrySamplePosition(transform.position, out hit);
				}
			}
			return false;
		}

		private void Status(Worker worker, StaffStatus status)
		{
			manager.ServerSetStatus(worker.Id, status);
		}

		private void Feedback(Worker worker, StaffStatus status)
		{
			Status(worker, status);
			if (worker.Feedback.Add(status))
			{
				manager.ServerStaffFeedback(worker.Id, status);
			}
		}

		private void ReleasePlan(Worker worker)
		{
			reservations.ReleaseAll(worker.Id);
			worker.Collection.Clear();
			worker.Purchases.Clear();
		}

		private void Remove(Worker worker)
		{
			SushiAndFish.FriendsLife.FriendsEconomyService.Instance?.ObserveNativeStaffRemoval(worker.Id, worker.Npc);
			StaffWorkplaceRegistry.Instance?.ReleaseWaitingPoint(worker.Id);
			worker.Prep?.Dispose();
			worker.Chef?.Dispose();
			worker.Waitress?.Dispose();
			ReleasePlan(worker);
			foreach (BaseHoldable item in worker.Carry)
			{
				if (item.CanWriteServerState())
				{
					item.NetworkObject.Despawn();
				}
			}
			if (worker.Npc.CanWriteServerState())
			{
				worker.Npc.NetworkObject.Despawn();
			}
			workers.Remove(worker.Id);
		}

		public void Dispose()
		{
			Worker[] array = workers.Values.ToArray();
			foreach (Worker worker in array)
			{
				Remove(worker);
			}
			pending.Clear();
		}
	}
}
