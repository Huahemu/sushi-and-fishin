using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Framework.Enums;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;

namespace PaperPixelGames.Core.Staff
{
	public sealed class StaffWaitressWorker : IDisposable
	{
		private readonly StaffManager manager;

		private readonly StaffNpc npc;

		private readonly StaffReservationService reservations;

		private readonly StaffTaskBoard tasks = new StaffTaskBoard();

		private readonly int id;

		private readonly List<StaffServiceTrayPlan> plans = new List<StaffServiceTrayPlan>();

		private readonly List<Tray> carry = new List<Tray>();

		private readonly Dictionary<Tray, StaffServiceTrayPlan> deliveries = new Dictionary<Tray, StaffServiceTrayPlan>();

		private readonly HashSet<string> batchKeys = new HashSet<string>();

		private Component target;

		private int targetVersion;

		private StaffStatus action;

		private float finishAt;

		private bool started;

		private StaffStatus? feedback;

		private float moveSpeed;

		private static readonly ProfilerMarker SelectMarker = new ProfilerMarker("Staff.Waitress.Select");

		public StaffWaitressWorker(StaffManager manager, StaffNpc npc, StaffReservationService reservations)
		{
			this.manager = manager;
			this.npc = npc;
			this.reservations = reservations;
			id = npc.EmployeeId.Value;
		}

		public bool Tick(StaffEmployeeEntry employee, StaffWorkplaceRegistry world)
		{
			moveSpeed = manager.Catalog.CourierMoveSpeed(employee);
			npc.SetRunning(manager.Catalog.CourierRuns(employee));
			carry.RemoveAll((Tray t) => t == null || !t.IsSpawned || !t.ServerStaffCarriedBy(id));
			foreach (Tray item in carry)
			{
				item.transform.position = npc.transform.position;
			}
			npc.SetHeldTray(carry.FirstOrDefault());
			bool flag = employee.StockingStopped || employee.Departure != StaffDeparture.None;
			if (flag)
			{
				CancelPlans();
				if (target != null)
				{
					ClearTarget();
				}
			}
			else if (!employee.ServeOrdersEnabled && plans.Count > 0)
			{
				CancelPlans();
			}
			if (plans.Count > 0)
			{
				Prepare(employee, world);
				return false;
			}
			if (carry.Count > 0)
			{
				FinishCarry(employee, world);
				return false;
			}
			if (flag)
			{
				npc.SetWorking(value: false, 1f);
				Status(StaffStatus.Stopped);
				if (employee.Departure == StaffDeparture.None)
				{
					Move(world.ClaimWaitingPoint(id, npc.transform.position), world);
					return false;
				}
				return Move(world.SpawnPoint, world);
			}
			if (!world.WaitressReady)
			{
				Feedback(StaffStatus.MissingWorkplace);
				return false;
			}
			if (target != null)
			{
				Perform(employee, world);
				return false;
			}
			Select(employee, world);
			return false;
		}

		private void Select(StaffEmployeeEntry employee, StaffWorkplaceRegistry world)
		{
			double now;
			List<(Component target, StaffStatus action, double score)> choices;
			using (SelectMarker.Auto())
			{
				now = manager.NetworkManager.ServerTime.Time;
				choices = new List<(Component, StaffStatus, double)>();
				foreach (StaffServicePoint servicePoint in world.ServicePoints)
				{
					if (servicePoint.Target == null || servicePoint.Point == null)
					{
						continue;
					}
					if (servicePoint.Target is RestaurantTable { IsSpawned: not false } restaurantTable)
					{
						if (restaurantTable.CurrentState == RestaurantTableState.AwaitingPayment)
						{
							Add(restaurantTable, StaffStatus.CollectingPayment, 0.0, 500.0);
						}
						if (restaurantTable.CurrentState == RestaurantTableState.Cleaning)
						{
							Add(restaurantTable, StaffStatus.CollectingDirtyTrays, 0.0, 300.0);
						}
						if (employee.TakeOrdersEnabled && restaurantTable.CurrentState == RestaurantTableState.TakingOrder)
						{
							Add(restaurantTable, StaffStatus.TakingOrder, restaurantTable.ActiveOrderExpireServerTime, 50.0);
						}
						if (employee.ServeOrdersEnabled && restaurantTable.ServerStaffOrders.Any())
						{
							Add(restaurantTable, StaffStatus.DeliveringOrders, restaurantTable.ActiveOrderExpireServerTime, 100.0);
						}
					}
					if (servicePoint.Target is ConveyorBeltTable { ServerStaffPaymentAvailable: not false } conveyorBeltTable)
					{
						Add(conveyorBeltTable, StaffStatus.ScanningPayment, 0.0, 500.0);
					}
				}
				tasks.Retain(new HashSet<string>(choices.Select(((Component target, StaffStatus action, double score) c) => TargetKey(c.target))));
				bool flag = false;
				bool flag2 = false;
				foreach (var item in choices.OrderByDescending(((Component target, StaffStatus action, double score) c) => c.score))
				{
					if (item.action == StaffStatus.DeliveringOrders)
					{
						if (!flag2)
						{
							flag2 = true;
							BuildBatch(employee, world, (RestaurantTable)item.target);
							if (plans.Count > 0)
							{
								feedback = null;
								return;
							}
							flag = true;
						}
					}
					else if (reservations.TryReserve(TargetKey(item.target), id))
					{
						target = item.target;
						action = item.action;
						started = false;
						if (target is ConveyorBeltTable conveyorBeltTable2)
						{
							targetVersion = conveyorBeltTable2.ServerStaffPaymentVersion;
						}
						Status(action);
						feedback = null;
						return;
					}
				}
				Tray tray = (from t in BaseHoldable.StaffWorldItems.OfType<Tray>()
					where t != null && t.IsSpawned && t.CanHold.Value && !t.IsStaffCarried && !t.IsPlayerInventoryOwnedForPersistence && t.transform.parent == null && world.IsInside(t.transform.position) && !world.TrayTrolleys.Any((TrayTrolley c) => c != null && c.ServerStaffAtHome(t)) && (t.IsDirty.Value || world.TrayReturnPoint == null || (t.transform.position - world.TrayReturnPoint.position).sqrMagnitude > 0.04f) && !reservations.IsReservedByOther(StaffServiceTrayPlan.ItemKey(t), id)
					orderby (t.transform.position - npc.transform.position).sqrMagnitude
					select t).FirstOrDefault();
				if (tray != null && reservations.TryReserve(StaffServiceTrayPlan.ItemKey(tray), id))
				{
					target = tray;
					action = StaffStatus.ReturningTrays;
					return;
				}
				if (flag)
				{
					Feedback(BaseHoldable.StaffWorldItems.OfType<Tray>().Any((Tray t) => StaffServiceTrayPlan.Available(t, world)) ? StaffStatus.WaitingForDishes : StaffStatus.NoTray);
				}
				else
				{
					feedback = null;
					Status(StaffStatus.Waiting);
				}
				Move(world.ClaimWaitingPoint(id, npc.transform.position), world);
			}
			void Add(Component t, StaffStatus a, double deadline, double priority)
			{
				string text = TargetKey(t);
				if (!reservations.IsReservedByOther(text, id))
				{
					choices.Add((t, a, tasks.Score(text, now, deadline, priority)));
				}
			}
		}

		private void BuildBatch(StaffEmployeeEntry employee, StaffWorkplaceRegistry world, RestaurantTable first)
		{
			batchKeys.Clear();
			StaffServiceTrayPlan.BatchSnapshot snapshot = new StaffServiceTrayPlan.BatchSnapshot(world);
			foreach (RestaurantTable item in new RestaurantTable[1] { first }.Concat(from t in world.ServicePoints.Select((StaffServicePoint p) => p.Target).OfType<RestaurantTable>()
				where t != first
				orderby t.ActiveOrderExpireServerTime
				select t))
			{
				CustomerSeatOrder[] array = item.ServerStaffOrders.ToArray();
				foreach (CustomerSeatOrder seat in array)
				{
					if (plans.Count >= StaffRules.ClampSkill(employee.SkillLevel))
					{
						return;
					}
					StaffServiceTrayPlan staffServiceTrayPlan = StaffServiceTrayPlan.TryCreate(item, seat, world, reservations, id, npc.transform.position, batchKeys, snapshot);
					if (staffServiceTrayPlan != null)
					{
						plans.Add(staffServiceTrayPlan);
					}
				}
			}
		}

		private void Prepare(StaffEmployeeEntry employee, StaffWorkplaceRegistry world)
		{
			StaffServiceTrayPlan staffServiceTrayPlan = plans[0];
			if (!staffServiceTrayPlan.Valid(id, world, reservations))
			{
				CancelPlans();
				return;
			}
			if (!staffServiceTrayPlan.Tray.ServerStaffCarriedBy(id))
			{
				Status(StaffStatus.CollectingDishes);
				if (Move(staffServiceTrayPlan.Tray.transform.position, world))
				{
					if (!staffServiceTrayPlan.Valid(id, world, reservations) || !staffServiceTrayPlan.Tray.ServerStaffTryPickup(id))
					{
						CancelPlans();
					}
					else
					{
						carry.Add(staffServiceTrayPlan.Tray);
					}
				}
				return;
			}
			ServedDishSO[] array = new ServedDishSO[3]
			{
				staffServiceTrayPlan.Order.MainDishSO,
				staffServiceTrayPlan.Order.SideDishSO,
				staffServiceTrayPlan.Order.BeverageSO
			};
			for (byte b = 0; b < 3; b++)
			{
				ServedDish servedDish = staffServiceTrayPlan.Tray.ServerStaffDish(b);
				bool flag = servedDish != null && ((b == 0) ? (servedDish.RuntimeRecipeIdentityId == staffServiceTrayPlan.Order.MainDishIdentityId) : (servedDish.ItemSO == array[b]));
				if (!((servedDish == null) | flag))
				{
					Transform transform = world.ReadyDishPoint(servedDish.RuntimeRecipeIdentityId, npc.transform.position);
					if (transform == null)
					{
						CancelPlans();
						return;
					}
					Status(StaffStatus.ReconfiguringTray);
					if (Move(transform, world))
					{
						Transform transform2 = world.ReadyDishPoint(servedDish.RuntimeRecipeIdentityId, npc.transform.position);
						if (transform2 != null && transform2 == transform)
						{
							staffServiceTrayPlan.Tray.ServerStaffStoreDish(id, b, transform2);
						}
					}
					return;
				}
			}
			foreach (StaffServiceTrayPlan.Source source in staffServiceTrayPlan.Sources)
			{
				ServedDish servedDish2 = staffServiceTrayPlan.Tray.ServerStaffDish(source.Slot);
				if (servedDish2 != null && ((source.Slot == 0) ? (servedDish2.RuntimeRecipeIdentityId == source.IdentityId) : (servedDish2.ItemSO == source.Item)))
				{
					continue;
				}
				Status(StaffStatus.CollectingDishes);
				Transform transform3 = ((source.Fridge != null) ? world.ServicePoint(source.Fridge) : null);
				if ((transform3 != null) ? (!Move(transform3, world)) : (!Move(source.Position, world)))
				{
					return;
				}
				if (!staffServiceTrayPlan.Valid(id, world, reservations))
				{
					CancelPlans();
					return;
				}
				ServedDish servedDish3 = source.Dish;
				if (servedDish3 == null && source.Jar != null && source.Jar.ServerStaffTake(out var dish))
				{
					servedDish3 = dish;
					servedDish3.transform.position = npc.transform.position;
				}
				if (servedDish3 == null && source.Fridge != null)
				{
					servedDish3 = source.Fridge.ServerStaffTake(source.Item, npc.transform.position);
				}
				if (servedDish3 == null && source.Pack != null)
				{
					NetworkObject networkObject = UnityEngine.Object.Instantiate(source.Item.Prefab, npc.transform.position, Quaternion.identity);
					networkObject.Spawn(destroyWithScene: true);
					servedDish3 = networkObject.GetComponent<ServedDish>();
					if (!source.Pack.ServerTryConsumeAmount(1, out var consumedAmount) || consumedAmount != 1)
					{
						networkObject.Despawn();
						CancelPlans();
						return;
					}
				}
				if (servedDish3 == null || !staffServiceTrayPlan.Tray.ServerStaffAddDish(id, servedDish3, source.Slot))
				{
					CancelPlans();
				}
				return;
			}
			deliveries[staffServiceTrayPlan.Tray] = staffServiceTrayPlan;
			plans.RemoveAt(0);
		}

		private void FinishCarry(StaffEmployeeEntry employee, StaffWorkplaceRegistry world)
		{
			Tray tray = (from t in carry
				where !t.IsDirty.Value && deliveries.TryGetValue(t, out var value2) && value2.Table != null && value2.Table.ServerStaffOrderValid(value2.OrderId)
				orderby (deliveries[t].Table.transform.position - npc.transform.position).sqrMagnitude
				select t).FirstOrDefault();
			if (tray != null)
			{
				StaffServiceTrayPlan staffServiceTrayPlan = deliveries[tray];
				Transform transform = world.ServicePoint(staffServiceTrayPlan.Table);
				if (transform == null)
				{
					deliveries.Remove(tray);
					return;
				}
				Status(StaffStatus.DeliveringOrders);
				if (Move(transform, world))
				{
					if (staffServiceTrayPlan.Table.ServerStaffDeliver(id, employee.Name.ToString(), staffServiceTrayPlan.OrderId, tray))
					{
						carry.Remove(tray);
						deliveries.Remove(tray);
						StaffServiceGameplayApi.Record(employee, 1, 0, 0, staffServiceTrayPlan.OrderId.ToString());
						Release(staffServiceTrayPlan);
					}
					else
					{
						deliveries.Remove(tray);
						Release(staffServiceTrayPlan);
					}
				}
				return;
			}
			Tray tray2 = carry.FirstOrDefault((Tray t) => t.IsDirty.Value);
			if (tray2 != null)
			{
				if (world.CleaningPoint == null)
				{
					Feedback(StaffStatus.MissingWorkplace);
					return;
				}
				Status(StaffStatus.CleaningTrays);
				if (!Move(world.CleaningPoint, world))
				{
					return;
				}
				if (!started)
				{
					started = true;
					finishAt = Time.time + manager.Catalog.WaitressCleanSeconds;
					npc.SetWorking(value: true, 1f);
				}
				else if (!(Time.time < finishAt))
				{
					if (tray2.ServerStaffClean(id))
					{
						StaffServiceGameplayApi.Record(employee, 0, 0, 1, tray2.ItemSO.Id);
					}
					started = false;
					npc.SetWorking(value: false, 1f);
				}
				return;
			}
			Tray tray3 = carry[0];
			TrayTrolley trayTrolley = (from t in world.TrayTrolleys
				where t != null && world.ServicePoint(t) != null && t.ServerStaffCanReturn(tray3)
				orderby (!t.TryGetTraySlotIndex(tray3, out var _)) ? 1 : 0, (world.ServicePoint(t).position - npc.transform.position).sqrMagnitude
				select t).FirstOrDefault();
			Transform transform2 = ((trayTrolley != null) ? world.ServicePoint(trayTrolley) : world.TrayReturnPoint);
			if (transform2 == null)
			{
				transform2 = world.ClaimWaitingPoint(id, npc.transform.position);
			}
			Status(StaffStatus.ReturningTrays);
			if (Move(transform2, world) && (!(trayTrolley != null) || trayTrolley.ServerStaffReturn(id, tray3)))
			{
				if (trayTrolley == null)
				{
					tray3.ServerStaffDrop(id, (world.TrayReturnPoint != null) ? world.TrayReturnPoint.position : transform2.position, transform2.rotation);
				}
				carry.Remove(tray3);
				if (deliveries.Remove(tray3, out var value))
				{
					Release(value);
				}
				if (carry.Count == 0)
				{
					reservations.ReleaseAll(id);
					batchKeys.Clear();
				}
			}
		}

		private void Perform(StaffEmployeeEntry employee, StaffWorkplaceRegistry world)
		{
			Transform transform = world.ServicePoint(target);
			Component component = target;
			Tray loose = component as Tray;
			if ((object)loose != null)
			{
				if (!loose.IsSpawned || !loose.CanHold.Value || loose.IsStaffCarried || loose.IsPlayerInventoryOwnedForPersistence || !world.IsInside(loose.transform.position) || world.TrayTrolleys.Any((TrayTrolley t) => t != null && t.ServerStaffAtHome(loose)))
				{
					ClearTarget();
				}
				else if (Move(loose.transform.position, world))
				{
					if (loose.ServerStaffTryPickup(id))
					{
						carry.Add(loose);
					}
					ClearTarget();
				}
				return;
			}
			if (transform == null)
			{
				ClearTarget();
				return;
			}
			if (target is RestaurantTable restaurantTable)
			{
				if (restaurantTable.IsSpawned)
				{
					int num;
					if (action != StaffStatus.TakingOrder)
					{
						num = (((action == StaffStatus.CollectingPayment) ? (restaurantTable.CurrentState == RestaurantTableState.AwaitingPayment) : (restaurantTable.CurrentState == RestaurantTableState.Cleaning)) ? 1 : 0);
					}
					else
					{
						if (!employee.TakeOrdersEnabled)
						{
							goto IL_0156;
						}
						num = ((restaurantTable.CurrentState == RestaurantTableState.TakingOrder) ? 1 : 0);
					}
					if (num != 0)
					{
						Status(action);
						if (!Move(transform, world))
						{
							return;
						}
						if (action == StaffStatus.TakingOrder)
						{
							if (!started)
							{
								started = true;
								finishAt = Time.time + manager.Catalog.WaitressOrderSeconds;
								npc.SetWorking(value: true, 1f);
								return;
							}
							if (Time.time < finishAt)
							{
								return;
							}
							if (restaurantTable.ServerStaffTakeOrder(employee))
							{
								manager.Ledger.Task(employee);
							}
						}
						else if (action == StaffStatus.CollectingPayment)
						{
							restaurantTable.ServerStaffCollectPayment(employee);
						}
						else
						{
							for (int num2 = 0; num2 < StaffRules.ClampSkill(employee.SkillLevel); num2++)
							{
								Tray tray = restaurantTable.ServerStaffTakeDirtyTray(id);
								if (tray == null)
								{
									break;
								}
								carry.Add(tray);
							}
						}
						ClearTarget();
						return;
					}
				}
				goto IL_0156;
			}
			if (!(target is ConveyorBeltTable conveyorBeltTable))
			{
				return;
			}
			if (!conveyorBeltTable.ServerStaffPaymentAvailable || conveyorBeltTable.ServerStaffPaymentVersion != targetVersion)
			{
				ClearTarget();
				return;
			}
			Status(StaffStatus.ScanningPayment);
			if (!Move(transform, world))
			{
				return;
			}
			if (!started)
			{
				started = conveyorBeltTable.ServerStaffBeginScan(id, targetVersion, manager.Catalog.WaitressScanMinSeconds, manager.Catalog.WaitressScanMaxSeconds);
				npc.SetWorking(started, 1f);
				if (!started)
				{
					ClearTarget();
				}
			}
			else if (conveyorBeltTable.ServerStaffFinishScan(employee) || !conveyorBeltTable.ServerStaffScanning(id))
			{
				ClearTarget();
			}
			return;
			IL_0156:
			ClearTarget();
		}

		private bool Move(Transform point, StaffWorkplaceRegistry world)
		{
			if (point == null)
			{
				npc.Stop();
				return false;
			}
			bool num = Move(point.position, world);
			if (num)
			{
				npc.FacePoint(point);
			}
			return num;
		}

		private bool Move(Vector3 point, StaffWorkplaceRegistry world)
		{
			bool result = npc.MoveTo(point, moveSpeed);
			if (npc.Recovered)
			{
				return result;
			}
			if (npc.InvalidTarget)
			{
				CancelPlans();
				ClearTarget();
				reservations.ReleaseAll(id);
				foreach (StaffServiceTrayPlan value in deliveries.Values)
				{
					if (value.Table != null && value.Table.ServerStaffOrderValid(value.OrderId))
					{
						reservations.TryReserve(StaffServiceTrayPlan.OrderKey(value.OrderId), id);
					}
				}
				deliveries.Clear();
				Feedback(StaffStatus.MissingWorkplace);
				return false;
			}
			return result;
		}

		private static string TargetKey(Component t)
		{
			if (!(t is BaseHoldable item))
			{
				return "service-target:" + t.GetInstanceID();
			}
			return StaffServiceTrayPlan.ItemKey(item);
		}

		private void ClearTarget()
		{
			if (target is ConveyorBeltTable conveyorBeltTable)
			{
				conveyorBeltTable.ServerStaffCancelScan(id);
			}
			if (target != null)
			{
				reservations.Release(TargetKey(target), id);
				tasks.Complete(TargetKey(target));
			}
			target = null;
			started = false;
			npc.SetWorking(value: false, 1f);
		}

		private void Release(StaffServiceTrayPlan plan)
		{
			foreach (string key in plan.Keys)
			{
				reservations.Release(key, id);
			}
		}

		private void CancelPlans()
		{
			foreach (StaffServiceTrayPlan plan in plans)
			{
				if (plan.Tray != null && plan.Tray.ServerStaffCarriedBy(id) && plan.Tray.ServerStaffMatches(plan.Order))
				{
					deliveries[plan.Tray] = plan;
				}
				else
				{
					Release(plan);
				}
			}
			plans.Clear();
			batchKeys.Clear();
		}

		private void Status(StaffStatus value)
		{
			manager.ServerSetStatus(id, value);
		}

		private void Feedback(StaffStatus value)
		{
			Status(value);
			if (feedback != value)
			{
				feedback = value;
				manager.ServerStaffFeedback(id, value);
			}
		}

		public void Dispose()
		{
			CancelPlans();
			ClearTarget();
			reservations.ReleaseAll(id);
			Tray[] array = carry.ToArray();
			foreach (Tray tray in array)
			{
				if (tray.CanWriteServerState())
				{
					tray.NetworkObject.Despawn();
				}
			}
			carry.Clear();
			deliveries.Clear();
		}
	}
}
