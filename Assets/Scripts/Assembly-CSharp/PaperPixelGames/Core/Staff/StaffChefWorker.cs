using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen;
using PaperPixelGames.Core.Kitchen.ServedDishes;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Core.Struct;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;

namespace PaperPixelGames.Core.Staff
{
	public sealed class StaffChefWorker : IDisposable
	{
		private ServedDishSushi readyTransfer;

		private bool readyTransferActive;

		private Vector3 conveyorApproach;

		private bool returningReadyPlate;

		private float nextReadyCheck;

		private readonly StaffManager manager;

		private readonly StaffNpc npc;

		private StaffChefWorkplace workplace;

		private readonly StaffReservationService reservations;

		private readonly int id;

		private SushiRecipeDefinition recipe;

		private StaffChefIngredients ingredients;

		private ConveyorBelt belt;

		private int slot = -1;

		private bool committed;

		private bool forOrder;

		private bool forReserve;

		private bool partial;

		private NetworkObject output;

		private bool rewarded;

		private float finishAt;

		private int menuIndex;

		private Transform reservedOutput;

		private bool outputBlocked;

		private StaffStatus? feedback;

		private static readonly ProfilerMarker SelectMarker = new ProfilerMarker("Staff.Chef.Select");

		private static string ReadyKey(ServedDishSushi dish)
		{
			return "item:" + dish.NetworkObjectId;
		}

		private bool ReadyAvailable(ServedDishSushi dish, StaffWorkplaceRegistry world)
		{
			if (dish != null && dish.IsSpawned && dish.IsServer && dish.ItemSO != null && !dish.IsStaffCarried && dish.CanHold.Value && !dish.IsPlayerInventoryOwnedForPersistence && dish.transform.parent == null && world.IsInside(dish.transform.position))
			{
				return !reservations.IsReservedByOther(ReadyKey(dish), id);
			}
			return false;
		}

		private static Dictionary<string, int> OutstandingSushi()
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			TabletNetworkHandler instance = TabletNetworkHandler.Instance;
			if (instance == null)
			{
				return dictionary;
			}
			foreach (CustomerOrderEntry item in instance.ServerStaffOrders())
			{
				if (item.GetStatus() == OrderReceiptStatus.Served || item.GetStatus() == OrderReceiptStatus.Expired)
				{
					continue;
				}
				List<string> list = new List<string>();
				for (int i = 0; i < item.DeliveredFoodIds.Length; i++)
				{
					list.Add(item.DeliveredFoodIds[i].ToString());
				}
				for (int j = 0; j < item.FoodIds.Length; j++)
				{
					string text = item.FoodIds[j].ToString();
					if (!list.Remove(text))
					{
						dictionary.TryGetValue(text, out var value);
						dictionary[text] = value + 1;
					}
				}
			}
			return dictionary;
		}

		private bool TrySelectReadyTransfer(StaffEmployeeEntry employee, StaffWorkplaceRegistry world)
		{
			if (!employee.StockConveyorEnabled || Time.time < nextReadyCheck || world.Conveyor == null || !world.Conveyor.ServerStaffCanRefill)
			{
				return false;
			}
			nextReadyCheck = Time.time + Mathf.Max(1f, manager.Catalog.ChefEmptyConveyorCheckSeconds);
			HashSet<string> hashSet = new HashSet<string>(TabletNetworkHandler.Instance.ServerStaffMenuIds());
			List<ServedDishSushi> list = new List<ServedDishSushi>();
			Dictionary<string, int> counts = new Dictionary<string, int>();
			foreach (BaseHoldable staffWorldItem in BaseHoldable.StaffWorldItems)
			{
				if (staffWorldItem is ServedDishSushi servedDishSushi && ReadyAvailable(servedDishSushi, world) && hashSet.Contains(servedDishSushi.RuntimeRecipeIdentityId))
				{
					list.Add(servedDishSushi);
					counts.TryGetValue(servedDishSushi.RuntimeRecipeIdentityId, out var value);
					counts[servedDishSushi.RuntimeRecipeIdentityId] = value + 1;
				}
			}
			Dictionary<string, int> orders = OutstandingSushi();
			list.RemoveAll((ServedDishSushi d) => orders.TryGetValue(d.RuntimeRecipeIdentityId, out var value2) && counts[d.RuntimeRecipeIdentityId] <= value2);
			if (list.Count == 0)
			{
				return false;
			}
			ServedDishSushi dish = list[UnityEngine.Random.Range(0, list.Count)];
			if (!reservations.TryReserve(ReadyKey(dish), id))
			{
				return false;
			}
			belt = world.Conveyor;
			slot = belt.ServerStaffReserveEmptyRefill(id, manager.Catalog.ChefEmptyConveyorCheckSeconds);
			if (slot < 0)
			{
				Release(world);
				return false;
			}
			readyTransfer = dish;
			readyTransferActive = true;
			returningReadyPlate = false;
			conveyorApproach = ((world.ConveyorStockPoint != null) ? world.ConveyorStockPoint.position : belt.ServerStaffSlotPosition(slot));
			Status(StaffStatus.StockingConveyor);
			return true;
		}

		private bool ReadyStillSurplus(StaffWorkplaceRegistry world)
		{
			if (readyTransfer == null || !readyTransfer.IsSpawned || readyTransfer.ItemSO == null)
			{
				return false;
			}
			string runtimeRecipeIdentityId = readyTransfer.RuntimeRecipeIdentityId;
			if (!OutstandingSushi().TryGetValue(runtimeRecipeIdentityId, out var value))
			{
				return true;
			}
			int num = (readyTransfer.ServerStaffCarriedBy(id) ? 1 : 0);
			foreach (BaseHoldable staffWorldItem in BaseHoldable.StaffWorldItems)
			{
				if (staffWorldItem is ServedDishSushi servedDishSushi && ReadyAvailable(servedDishSushi, world) && servedDishSushi.RuntimeRecipeIdentityId == runtimeRecipeIdentityId)
				{
					num++;
				}
			}
			return num > value;
		}

		private void TickReadyTransfer(StaffEmployeeEntry employee, StaffWorkplaceRegistry world, bool stop)
		{
			if (readyTransfer == null || !readyTransfer.IsSpawned)
			{
				Release(world);
				return;
			}
			bool flag = readyTransfer.ServerStaffCarriedBy(id);
			if (flag)
			{
				readyTransfer.transform.position = npc.transform.position;
				npc.SetHeldItem(readyTransfer);
			}
			bool flag2 = stop || !employee.StockConveyorEnabled || belt == null || !belt.ServerStaffRunning || !ReadyStillSurplus(world);
			if (flag2 && !flag)
			{
				Release(world);
				return;
			}
			returningReadyPlate |= flag2;
			if (returningReadyPlate)
			{
				Transform transform = world.OutputPlacement.Choose(workplace.ReadySushiPoints, readyTransfer.RuntimeRecipeIdentityId);
				if (transform == null && workplace.ReadySushiPoints.Count == 0)
				{
					transform = workplace.OutputPoint;
				}
				if (transform == null)
				{
					Feedback(StaffStatus.StorageFull);
					return;
				}
				if (!npc.MoveTo(transform, manager.Catalog.ChefMoveSpeed))
				{
					if (!npc.InvalidTarget)
					{
						return;
					}
					transform = world.ClaimWaitingPoint(id, npc.transform.position);
					if (transform == null || !npc.MoveTo(transform, manager.Catalog.ChefMoveSpeed))
					{
						return;
					}
				}
				readyTransfer.ServerStaffDrop(id, transform.position, transform.rotation);
				Release(world);
				return;
			}
			Status(StaffStatus.StockingConveyor);
			if (!flag)
			{
				if (!ReadyAvailable(readyTransfer, world))
				{
					Release(world);
					return;
				}
				if (!npc.MoveTo(readyTransfer.transform.position, manager.Catalog.ChefMoveSpeed))
				{
					if (npc.InvalidTarget)
					{
						Release(world);
					}
					return;
				}
				if (npc.Recovered)
				{
					reservations.ReleaseAll(id);
				}
				if (!ReadyAvailable(readyTransfer, world) || !reservations.TryReserve(ReadyKey(readyTransfer), id) || !readyTransfer.ServerTryStaffPickup(id))
				{
					Release(world);
				}
				else
				{
					npc.SetHeldItem(readyTransfer);
				}
			}
			else if (!npc.MoveTo(conveyorApproach, manager.Catalog.ChefMoveSpeed))
			{
				if (npc.InvalidTarget)
				{
					returningReadyPlate = true;
				}
			}
			else if (npc.Recovered)
			{
				reservations.ReleaseAll(id);
				belt.ServerStaffReleaseSlots(id);
				returningReadyPlate = true;
			}
			else
			{
				if (world.ConveyorStockPoint != null)
				{
					npc.FacePoint(world.ConveyorStockPoint);
				}
				if (!belt.ServerStaffPlace(id, slot, readyTransfer))
				{
					returningReadyPlate = true;
					return;
				}
				manager.Ledger.Task(employee);
				Release(world);
			}
		}

		public StaffChefWorker(StaffManager manager, StaffNpc npc, StaffChefWorkplace workplace, StaffReservationService reservations)
		{
			this.manager = manager;
			this.npc = npc;
			this.workplace = workplace;
			this.reservations = reservations;
			id = npc.EmployeeId.Value;
		}

		public bool Tick(StaffEmployeeEntry employee, StaffWorkplaceRegistry world)
		{
			bool flag = employee.StockingStopped || employee.Departure != StaffDeparture.None;
			if (readyTransferActive)
			{
				TickReadyTransfer(employee, world, flag);
				return false;
			}
			if (committed)
			{
				if (Time.time < finishAt)
				{
					return false;
				}
				NetworkObject networkObject = ResolveRecipePrefab(recipe);
				if (networkObject == null || workplace.OutputPoint == null)
				{
					Status(StaffStatus.MissingWorkplace);
					return false;
				}
				if (output == null)
				{
					Transform transform = ((reservedOutput != null) ? reservedOutput : world.OutputPlacement.Choose(workplace.ReadySushiPoints, recipe.RecipeId));
					if (transform == null && workplace.ReadySushiPoints.Count == 0)
					{
						transform = workplace.OutputPoint;
					}
					if (transform == null)
					{
						Feedback(StaffStatus.StorageFull);
						return false;
					}
					output = UnityEngine.Object.Instantiate(networkObject, transform.position, transform.rotation);
					if (recipe.IdentityKind == SushiRecipeIdentityKind.SavedCustom && (!output.TryGetComponent<ServedDishSushi>(out var component) || !component.ServerInitializeCustomSushi(recipe.Composition, SushiRecipeIdentityKind.SavedCustom, recipe.RecipeId, recipe.DisplayName, recipe.Price)))
					{
						UnityEngine.Object.Destroy(output.gameObject);
						output = null;
						Status(StaffStatus.MissingWorkplace);
						return false;
					}
					output.Spawn(destroyWithScene: true);
					if (recipe.BuiltInSushi != null)
					{
						StaffQuestProgress.SushiProduced(recipe.BuiltInSushi, world.Database);
					}
				}
				ServedDishSushi component2 = output.GetComponent<ServedDishSushi>();
				if (!flag && employee.StockConveyorEnabled && belt != null && slot >= 0 && component2 != null)
				{
					belt.ServerStaffPlace(id, slot, component2);
				}
				if (!rewarded)
				{
					rewarded = true;
					StaffPrepGameplayApi.Reward(employee, ProgressionXpSource.SushiPrepared, recipe.RecipeId, 1);
				}
				workplace.Desk?.ServerStaffFinish(id);
				Release(world);
				return false;
			}
			if (flag)
			{
				Release(world);
				workplace.Desk?.ServerReleaseStaff(id);
				Status(StaffStatus.Stopped);
				if (employee.Departure == StaffDeparture.None)
				{
					npc.MoveTo(world.ClaimWaitingPoint(id, npc.transform.position), manager.Catalog.ChefMoveSpeed);
					return false;
				}
				return npc.MoveTo(world.SpawnPoint, manager.Catalog.ChefMoveSpeed);
			}
			if (!EnsureWorkplace(world))
			{
				Feedback(StaffStatus.MissingWorkplace);
				npc.MoveTo(world.ClaimWaitingPoint(id, npc.transform.position), manager.Catalog.ChefMoveSpeed);
				return false;
			}
			if (!workplace.IsReady || !workplace.Desk.PhysicalStateController.IsPhysicallyUsable)
			{
				Release(world);
				Feedback(StaffStatus.MissingWorkplace);
				return false;
			}
			if (!npc.MoveTo(workplace.WorkPoint, manager.Catalog.ChefMoveSpeed))
			{
				if (npc.InvalidTarget)
				{
					Release(world);
					Feedback(StaffStatus.MissingWorkplace);
				}
				return false;
			}
			if (npc.Recovered)
			{
				Release(world);
				return false;
			}
			npc.FacePoint(workplace.WorkPoint);
			if (recipe == null)
			{
				if (!workplace.Desk.ServerStaffPreserveUnsupported(id, workplace.OutputPoint.position, world.OutputPlacement, workplace.PanOutputs))
				{
					Feedback(StaffStatus.StorageFull);
					return false;
				}
				Select(employee, world);
				return false;
			}
			if ((!partial && forReserve && !ReadyRecipeNeeds(world, manager.Catalog, id, employee.PrepareOrdersEnabled).Any((SushiRecipeDefinition r) => r.RecipeId == recipe.RecipeId)) || (!partial && forOrder && !forReserve && (!employee.PrepareOrdersEnabled || !MissingRecipeOrders(world, id).Any((SushiRecipeDefinition r) => r.RecipeId == recipe.RecipeId))) || (!forOrder && !employee.StockConveyorEnabled) || (slot >= 0 && (belt == null || !belt.ServerStaffRunning)))
			{
				Release(world);
				return false;
			}
			if (!partial && !TabletNetworkHandler.Instance.ServerStaffMenuIds().Contains(recipe.RecipeId))
			{
				Release(world);
				return false;
			}
			if (!ingredients.Commit(recipe, manager.Catalog.ChefPanSwapSeconds))
			{
				StaffStatus status = (ingredients.StorageBlocked ? StaffStatus.StorageFull : StaffStatus.WaitingForIngredients);
				Release(world);
				Feedback(status);
				return false;
			}
			committed = true;
			finishAt = Time.time + manager.Catalog.ChefProductionSeconds / employee.WorkSpeed + (ingredients.HasSwap ? manager.Catalog.ChefPanSwapSeconds : 0f);
			npc.SetWorking(value: true, employee.WorkSpeed);
			Status(forOrder ? StaffStatus.PreparingOrder : StaffStatus.StockingConveyor);
			return false;
		}

		private bool EnsureWorkplace(StaffWorkplaceRegistry world)
		{
			if (workplace.Desk != null && workplace.Desk.StaffEmployeeId == id)
			{
				return true;
			}
			StaffChefWorkplace staffChefWorkplace = ((workplace.IsReady && workplace.Desk.ServerStaffAvailable) ? workplace : world.ChefDesks.FirstOrDefault((StaffChefWorkplace point) => point.IsReady && point.Desk.ServerStaffAvailable));
			if (staffChefWorkplace == null || !staffChefWorkplace.Desk.ServerAssignStaff(id))
			{
				return false;
			}
			workplace = staffChefWorkplace;
			return true;
		}

		private static bool AvailableForOrder(ServedDishSushi dish, StaffWorkplaceRegistry world)
		{
			if (dish == null || !dish.IsServer || !dish.IsSpawned || dish.ItemSO == null || dish.IsPlayerInventoryOwnedForPersistence || (!dish.IsStaffCarried && !world.IsInside(dish.transform.position)))
			{
				return false;
			}
			if (dish.transform.GetComponentInParent<ConveyorBelt>() != null)
			{
				return false;
			}
			Tray componentInParent = dish.GetComponentInParent<Tray>();
			if (!(componentInParent == null))
			{
				if (!componentInParent.IsDirty.Value && !componentInParent.IsPlayerInventoryOwnedForPersistence)
				{
					if (!componentInParent.CanHold.Value)
					{
						return componentInParent.IsStaffCarried;
					}
					return true;
				}
				return false;
			}
			return true;
		}

		public static IEnumerable<SushiSO> MissingOrders(StaffWorkplaceRegistry world, int excludeEmployee = 0)
		{
			return from recipe in MissingRecipeOrders(world, excludeEmployee)
				where recipe.BuiltInSushi != null
				select recipe.BuiltInSushi;
		}

		public static IEnumerable<SushiRecipeDefinition> MissingRecipeOrders(StaffWorkplaceRegistry world, int excludeEmployee = 0)
		{
			TabletNetworkHandler instance = TabletNetworkHandler.Instance;
			if (instance == null)
			{
				yield break;
			}
			Dictionary<string, int> available = new Dictionary<string, int>();
			foreach (ServedDishSushi item in BaseHoldable.StaffWorldItems.OfType<ServedDishSushi>())
			{
				if (AvailableForOrder(item, world))
				{
					Add(item.RuntimeRecipeIdentityId);
				}
			}
			foreach (string item2 in world.ChefProductionExcept(excludeEmployee))
			{
				Add(item2);
			}
			HashSet<string> menu = new HashSet<string>(instance.ServerStaffMenuIds());
			foreach (CustomerOrderEntry order in from o in instance.ServerStaffOrders()
				where o.GetStatus() != OrderReceiptStatus.Served && o.GetStatus() != OrderReceiptStatus.Expired
				orderby o.ExpireServerTime
				select o)
			{
				List<string> delivered = new List<string>();
				for (int num = 0; num < order.DeliveredFoodIds.Length; num++)
				{
					delivered.Add(order.DeliveredFoodIds[num].ToString());
				}
				for (int i = 0; i < order.FoodIds.Length; i++)
				{
					string text = order.FoodIds[i].ToString();
					if (!delivered.Remove(text) && menu.Contains(text) && TryResolveRecipe(world, text, out var resolved))
					{
						if (available.TryGetValue(text, out var value) && value > 0)
						{
							available[text] = value - 1;
						}
						else
						{
							yield return resolved;
						}
					}
				}
			}
			void Add(string key)
			{
				available.TryGetValue(key, out var value2);
				available[key] = value2 + 1;
			}
		}

		public static IEnumerable<SushiSO> ReadyNeeds(StaffWorkplaceRegistry world, StaffCatalogSO catalog, int excludeEmployee = 0, bool includeOrders = true)
		{
			return from recipe in ReadyRecipeNeeds(world, catalog, excludeEmployee, includeOrders)
				where recipe.BuiltInSushi != null
				select recipe.BuiltInSushi;
		}

		public static IEnumerable<SushiRecipeDefinition> ReadyRecipeNeeds(StaffWorkplaceRegistry world, StaffCatalogSO catalog, int excludeEmployee = 0, bool includeOrders = true)
		{
			TabletNetworkHandler instance = TabletNetworkHandler.Instance;
			SushiRecipeDefinition[] array = instance?.ServerStaffMenuIds().Select((string key) => (!TryResolveRecipe(world, key, out var resolved)) ? null : resolved).Where((SushiRecipeDefinition resolved) => resolved != null)
				.GroupBy((SushiRecipeDefinition resolved) => resolved.RecipeId)
				.Select((IGrouping<string, SushiRecipeDefinition> group) => group.First())
				.ToArray() ?? Array.Empty<SushiRecipeDefinition>();
			if (array.Length == 0 || catalog.ChefReadyPlateTarget <= 0)
			{
				yield break;
			}
			Dictionary<string, int> desired = array.ToDictionary((SushiRecipeDefinition r) => r.RecipeId, (SushiRecipeDefinition _) => 0);
			if (catalog.ChefReadyTargetPerRecipe)
			{
				SushiRecipeDefinition[] array2 = array;
				foreach (SushiRecipeDefinition sushiRecipeDefinition in array2)
				{
					desired[sushiRecipeDefinition.RecipeId] = catalog.ChefReadyPlateTarget;
				}
			}
			else
			{
				for (int num2 = 0; num2 < catalog.ChefReadyPlateTarget; num2++)
				{
					desired[array[num2 % array.Length].RecipeId]++;
				}
			}
			Dictionary<string, int> dictionary = array.ToDictionary((SushiRecipeDefinition r) => r.RecipeId, (SushiRecipeDefinition _) => 0);
			foreach (CustomerOrderEntry item in from o in instance.ServerStaffOrders()
				where includeOrders && o.GetStatus() != OrderReceiptStatus.Served && o.GetStatus() != OrderReceiptStatus.Expired
				select o)
			{
				List<string> list = new List<string>();
				for (int num3 = 0; num3 < item.DeliveredFoodIds.Length; num3++)
				{
					list.Add(item.DeliveredFoodIds[num3].ToString());
				}
				for (int num4 = 0; num4 < item.FoodIds.Length; num4++)
				{
					string text = item.FoodIds[num4].ToString();
					if (!list.Remove(text) && desired.ContainsKey(text))
					{
						desired[text]++;
						dictionary[text]++;
					}
				}
			}
			foreach (ServedDishSushi item2 in BaseHoldable.StaffWorldItems.OfType<ServedDishSushi>())
			{
				if (AvailableForOrder(item2, world) && desired.ContainsKey(item2.RuntimeRecipeIdentityId))
				{
					string runtimeRecipeIdentityId = item2.RuntimeRecipeIdentityId;
					if (!item2.IsStaffCarried && item2.transform.parent == null)
					{
						desired[runtimeRecipeIdentityId]--;
					}
					else if (dictionary[runtimeRecipeIdentityId] > 0)
					{
						desired[runtimeRecipeIdentityId]--;
						dictionary[runtimeRecipeIdentityId]--;
					}
				}
			}
			foreach (string item3 in world.ChefProductionExcept(excludeEmployee))
			{
				if (desired.ContainsKey(item3))
				{
					desired[item3]--;
				}
			}
			SushiRecipeDefinition[] array3 = array;
			foreach (SushiRecipeDefinition sushiRecipeDefinition2 in array3)
			{
				if (desired[sushiRecipeDefinition2.RecipeId] > 0)
				{
					yield return sushiRecipeDefinition2;
				}
			}
		}

		private void Select(StaffEmployeeEntry employee, StaffWorkplaceRegistry world)
		{
			using (SelectMarker.Auto())
			{
				outputBlocked = false;
				SushiRecipeDefinition sushiRecipeDefinition = SushiRecipeResolver.CreateBuiltInDefinition(workplace.Desk.ServerStaffPartialRecipe);
				SushiRecipeDefinition[] array = (employee.PrepareOrdersEnabled ? MissingRecipeOrders(world).ToArray() : Array.Empty<SushiRecipeDefinition>());
				if (sushiRecipeDefinition != null || array.Length != 0)
				{
					if (sushiRecipeDefinition != null)
					{
						if (!TryPlan(sushiRecipeDefinition, world, order: true))
						{
							Feedback(outputBlocked ? StaffStatus.StorageFull : StaffStatus.WaitingForIngredients);
						}
						return;
					}
					SushiRecipeDefinition[] array2 = array;
					foreach (SushiRecipeDefinition selected in array2)
					{
						if (TryPlan(selected, world, order: true))
						{
							return;
						}
					}
					Feedback(outputBlocked ? StaffStatus.StorageFull : StaffStatus.WaitingForIngredients);
				}
				else
				{
					if (TrySelectReadyTransfer(employee, world))
					{
						return;
					}
					SushiRecipeDefinition[] array3 = ReadyRecipeNeeds(world, manager.Catalog, 0, employee.PrepareOrdersEnabled).ToArray();
					if (array3.Length != 0)
					{
						SushiRecipeDefinition[] array2 = array3;
						foreach (SushiRecipeDefinition selected2 in array2)
						{
							if (TryPlan(selected2, world, order: true, reserve: true))
							{
								return;
							}
						}
						Feedback(outputBlocked ? StaffStatus.StorageFull : StaffStatus.WaitingForIngredients);
						return;
					}
					if (!employee.StockConveyorEnabled)
					{
						Status(StaffStatus.Waiting);
						return;
					}
					belt = world.Conveyor;
					if (belt == null || !belt.ServerStaffRunning)
					{
						Feedback(StaffStatus.ConveyorDisabled);
						return;
					}
					slot = belt.ServerStaffReserveSlot(id);
					if (slot < 0)
					{
						Feedback(StaffStatus.WaitingForConveyorSlot);
						return;
					}
					SushiRecipeDefinition[] array4 = TabletNetworkHandler.Instance?.ServerStaffMenuIds().Select((string key) => (!TryResolveRecipe(world, key, out var resolved)) ? null : resolved).Where((SushiRecipeDefinition resolved) => resolved != null)
						.ToArray() ?? Array.Empty<SushiRecipeDefinition>();
					for (int num = 0; num < array4.Length; num++)
					{
						int num2 = (menuIndex + num) % array4.Length;
						if (!TryPlan(array4[num2], world, order: false))
						{
							reservations.ReleaseAll(id);
							continue;
						}
						menuIndex = (num2 + 1) % array4.Length;
						return;
					}
					Release(world);
					Feedback(outputBlocked ? StaffStatus.StorageFull : StaffStatus.WaitingForIngredients);
				}
			}
		}

		private bool TryPlan(SushiRecipeDefinition selected, StaffWorkplaceRegistry world, bool order, bool reserve = false)
		{
			world.OutputPlacement.Release(id);
			reservedOutput = null;
			if (selected == null)
			{
				return false;
			}
			if (workplace.ReadySushiPoints.Count > 0 && !world.OutputPlacement.TryReserve(workplace.ReadySushiPoints, selected.RecipeId, id, out reservedOutput))
			{
				outputBlocked = true;
				return false;
			}
			ingredients = new StaffChefIngredients(workplace, world, reservations, id);
			NetworkObject networkObject = ResolveRecipePrefab(selected);
			if (networkObject == null || networkObject.GetComponent<ServedDishSushi>() == null || !ingredients.Plan(selected))
			{
				outputBlocked |= ingredients.StorageBlocked;
				reservations.ReleaseAll(id);
				world.OutputPlacement.Release(id);
				return false;
			}
			recipe = selected;
			forOrder = order;
			forReserve = reserve;
			partial = selected.BuiltInSushi != null && workplace.Desk.ServerStaffPartialRecipe == selected.BuiltInSushi;
			world.SetChefProduction(id, selected.RecipeId);
			feedback = null;
			Status(ingredients.HasSwap ? StaffStatus.SwappingGastroPan : (order ? StaffStatus.PreparingOrder : StaffStatus.StockingConveyor));
			return true;
		}

		private static bool TryResolveRecipe(StaffWorkplaceRegistry world, string recipeId, out SushiRecipeDefinition resolved)
		{
			CustomSushiRecipeCatalog customCatalog = Singleton<GameProgressManager>.Instance?.RestaurantManager?.CustomSushiRecipeCatalog;
			return new SushiRecipeResolver(world?.Database, customCatalog).TryResolveById(recipeId, out resolved);
		}

		private static NetworkObject ResolveRecipePrefab(SushiRecipeDefinition resolved)
		{
			if (resolved?.BuiltInSushi?.Prefab != null)
			{
				return resolved.BuiltInSushi.Prefab;
			}
			return Singleton<GameProgressManager>.Instance?.RestaurantManager?.ResolveCustomSushiPhysicalPrefab(resolved?.SushiType ?? SushiType.Empty);
		}

		private void Status(StaffStatus status)
		{
			manager.ServerSetStatus(id, status);
		}

		private void Feedback(StaffStatus status)
		{
			Status(status);
			if (feedback != status)
			{
				feedback = status;
				manager.ServerStaffFeedback(id, status);
			}
		}

		private void Release(StaffWorkplaceRegistry world)
		{
			reservations.ReleaseAll(id);
			world?.ClearChefProduction(id);
			world?.OutputPlacement.Release(id);
			reservedOutput = null;
			belt?.ServerStaffReleaseSlots(id);
			belt = null;
			slot = -1;
			recipe = null;
			ingredients = null;
			committed = false;
			output = null;
			rewarded = false;
			readyTransfer = null;
			readyTransferActive = false;
			npc.SetHeld(null);
			npc.SetWorking(value: false, 1f);
		}

		public void Dispose()
		{
			if (readyTransfer.CanWriteServerState() && readyTransfer.ServerStaffCarriedBy(id))
			{
				readyTransfer.NetworkObject.Despawn();
			}
			Release(StaffWorkplaceRegistry.Instance);
			workplace.Desk?.ServerReleaseStaff(id);
		}
	}
}
