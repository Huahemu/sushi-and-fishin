using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.FishFridge;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen.PreparationStation.GastronomPan;
using PaperPixelGames.Core.Kitchen.Wholasale;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.ProcessedIngredient;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Staff
{
	public sealed class StaffPrepWorker : IDisposable
	{
		private enum Phase
		{
			Select = 0,
			PickupInput = 1,
			Input = 2,
			Fridge = 3,
			Fillet = 4,
			Cut = 5,
			Finish = 6
		}

		private readonly StaffManager manager;

		private readonly StaffReservationService reservations;

		private readonly StaffNpc npc;

		private readonly int id;

		private StaffPrepWorkplace workplace;

		private NetworkObject panPrefab;

		private readonly List<GastroPan> pans = new List<GastroPan>();

		private readonly List<GastroPan> movingPans = new List<GastroPan>();

		private readonly List<BaseHoldable> carried = new List<BaseHoldable>();

		private StaffPrepRecipe recipe;

		private StaffPrepMachine machine;

		private BaseHoldable input;

		private Phase phase;

		private float finishAt;

		private int outputCount;

		private bool rewarded;

		private bool resumed;

		private float retryAt;

		private StaffStatus? feedback;

		public StaffPrepWorker(StaffManager manager, StaffNpc npc, StaffPrepWorkplace workplace, StaffReservationService reservations)
		{
			this.manager = manager;
			this.npc = npc;
			this.workplace = workplace;
			this.reservations = reservations;
			id = npc.EmployeeId.Value;
			panPrefab = workplace.Station.StaffPanPrefab;
		}

		public bool Tick(StaffEmployeeEntry employee, StaffWorkplaceRegistry world)
		{
			bool flag = employee.StockingStopped || employee.Departure != StaffDeparture.None;
			carried.RemoveAll((BaseHoldable i) => i == null || !i.IsSpawned);
			pans.RemoveAll((GastroPan p) => p == null || !p.IsSpawned);
			movingPans.RemoveAll((GastroPan p) => p == null || !p.IsSpawned || p.CanHold.Value);
			foreach (BaseHoldable item in carried)
			{
				item.transform.position = npc.transform.position;
			}
			npc.SetHeldItem((carried.Count > 0) ? carried[0] : null);
			if (phase == Phase.Select)
			{
				if (!flag && !EnsureWorkplace(world))
				{
					Status(StaffStatus.MissingWorkplace);
					Move(world.ClaimWaitingPoint(id, npc.transform.position));
					return false;
				}
				if (!flag && !resumed && workplace.Station != null)
				{
					resumed = true;
					foreach (GastroPan item2 in BaseHoldable.StaffWorldItems.OfType<GastroPan>())
					{
						if (item2 != null && item2.IsSpawned && !item2.IsStaffCarried && !item2.IsPlayerInventoryOwnedForPersistence && Vector3.Distance(item2.transform.position, workplace.OutputPoint.position) < 0.5f)
						{
							pans.Add(item2);
							item2.CanHold.Value = false;
						}
					}
					if (workplace.Station.ServerStaffTakePending(id, out var output, out var count))
					{
						GastroPan gastroPan = StaffPrepGameplayApi.CreatePan(workplace.Station.StaffPanPrefab, output, count, workplace.OutputPoint.position);
						if (gastroPan == null)
						{
							resumed = false;
							Status(StaffStatus.MissingWorkplace);
							return false;
						}
						workplace.Station.ServerStaffClearPending(id);
						pans.Add(gastroPan);
					}
					workplace.Station.ServerStaffEjectItem(id, workplace.OutputPoint.position);
				}
				bool flag2 = DispatchPans(world, flag);
				if (movingPans.Count > 0)
				{
					Status(StaffStatus.SendingGastroPan);
					return false;
				}
				if (!flag2)
				{
					Feedback(StaffStatus.StorageFull);
					return false;
				}
				if (flag && pans.Count > 0)
				{
					Status(StaffStatus.MissingWorkplace);
					return false;
				}
				if (flag)
				{
					DropCarried(world);
					npc.SetWorking(value: false, 1f);
					workplace.Station?.ServerReleaseStaff(id);
					resumed = false;
					Status(StaffStatus.Stopped);
					if (employee.Departure == StaffDeparture.None)
					{
						Move(world.ClaimWaitingPoint(id, npc.transform.position));
						return false;
					}
					return Move(world.SpawnPoint);
				}
				if (!(workplace.Station == null) && workplace.Station.IsSpawned)
				{
					PhysicalStateController physicalStateController = workplace.Station.PhysicalStateController;
					if ((object)physicalStateController != null && physicalStateController.IsPhysicallyUsable)
					{
						if (Time.time >= retryAt)
						{
							Select(employee, world);
						}
						return false;
					}
				}
				Status(StaffStatus.MissingWorkplace);
				return false;
			}
			if (flag && (phase == Phase.PickupInput || phase == Phase.Input || phase == Phase.Fridge))
			{
				Release();
				DropCarried(world);
				phase = Phase.Select;
				return false;
			}
			if (phase == Phase.PickupInput)
			{
				Status(StaffStatus.CollectingItems);
				if (!StaffPrepGameplayApi.Available(input, world) || !StaffPrepGameplayApi.IsInput(input, recipe.Input))
				{
					Cancel();
					return false;
				}
				if (!Move(input.transform.position))
				{
					return false;
				}
				if (!input.ServerTryStaffPickup(id))
				{
					Cancel();
					return false;
				}
				carried.Add(input);
				input.transform.position = npc.transform.position;
				npc.SetHeldItem(input);
				phase = Phase.Input;
				return false;
			}
			if (phase == Phase.Fridge)
			{
				if (world.FishFridge == null || world.FishFridgePoint == null)
				{
					Cancel();
					return false;
				}
				if (!Move(world.FishFridgePoint))
				{
					return false;
				}
				DeadFish deadFish = world.FishFridge.ServerStaffTakeFish(recipe.WholeFish, id, npc.transform.position);
				if (deadFish == null)
				{
					Cancel();
					return false;
				}
				carried.Add(deadFish);
				phase = Phase.Fillet;
				finishAt = 0f;
				return false;
			}
			if (phase == Phase.Fillet)
			{
				Status(StaffStatus.UsingFilletMachine);
				if (machine?.FilletMachine == null || !machine.FilletMachine.IsSpawned)
				{
					DropCarried(world);
					Cancel();
					return false;
				}
				if (!Move(machine.WorkPoint))
				{
					return false;
				}
				if (finishAt == 0f)
				{
					finishAt = Time.time + manager.Catalog.PrepFilletSeconds / employee.WorkSpeed;
					machine.FilletMachine.ServerStaffBegin(id, recipe.WholeFish.Id);
					npc.SetWorking(value: true, employee.WorkSpeed);
				}
				if (Time.time < finishAt)
				{
					return false;
				}
				DeadFish deadFish2 = carried.OfType<DeadFish>().FirstOrDefault();
				if (deadFish2 == null || recipe.Input.Prefab == null)
				{
					DropCarried(world);
					Cancel();
					return false;
				}
				int num = deadFish2.FishSO.RollFilletYield();
				for (int num2 = 0; num2 < num; num2++)
				{
					UnityEngine.Object.Instantiate(recipe.Input.Prefab, machine.OutputPoint.position, Quaternion.identity).Spawn(destroyWithScene: true);
					Singleton<GameProgressManager>.Instance?.RestaurantManager?.ServerAddStockRpc(recipe.Input.Id, 1);
				}
				carried.Remove(deadFish2);
				deadFish2.ServerDeregisterInventoryStock();
				deadFish2.NetworkObject.Despawn();
				StaffPrepGameplayApi.Reward(employee, ProgressionXpSource.FishFilleted, recipe.WholeFish.Id, num);
				if (recipe.WholeFish == world.Database.MackerelSO)
				{
					StaffQuestProgress.Complete(QuestId.TakeMackerelFillet);
				}
				machine.FilletMachine.ServerStaffComplete(id);
				machine = null;
				npc.SetHeld(null);
				Release();
				phase = Phase.Select;
				npc.SetWorking(value: false, 1f);
				return false;
			}
			if (phase == Phase.Input)
			{
				if (recipe.Kind == StaffPrepKind.Rice && (machine?.RiceCooker == null || !machine.RiceCooker.ServerStaffCanBegin(id)))
				{
					Cancel();
					retryAt = Time.time + 2f;
					Feedback(StaffStatus.WaitingForMachine);
					return false;
				}
				if (input is IngredientEntity && !input.ServerStaffCarriedBy(id))
				{
					Cancel();
					return false;
				}
				Transform transform = ((recipe.Kind != StaffPrepKind.Rice) ? workplace.WorkPoint : machine?.WorkPoint);
				if (transform == null)
				{
					Cancel();
					return false;
				}
				if (!Move(transform))
				{
					return false;
				}
				string resource = ItemKey(input);
				if (!StaffPrepGameplayApi.Consume(input, recipe.Input, world, out outputCount, recipe.Output, id))
				{
					Cancel();
					return false;
				}
				carried.Remove(input);
				npc.SetHeld(null);
				rewarded = false;
				world.SetPrepProduction(id, recipe.Output.Id, outputCount);
				reservations.Release(resource, id);
				input = null;
				if (recipe.Kind == StaffPrepKind.Rice)
				{
					machine.RiceCooker.ServerStaffBegin(id);
				}
				else
				{
					workplace.Station.ServerStaffVisual(id, recipe.Input.Id);
				}
				StaffQuestProgress.PrepStarted(recipe, world.Database);
				finishAt = Time.time + ((recipe.Kind == StaffPrepKind.Rice) ? manager.Catalog.PrepRiceSeconds : manager.Catalog.PrepCutSeconds) / employee.WorkSpeed;
				phase = Phase.Cut;
				npc.FacePoint(transform);
				npc.SetWorking(value: true, employee.WorkSpeed);
			}
			if (phase == Phase.Cut)
			{
				Status((recipe.Kind == StaffPrepKind.Rice) ? StaffStatus.CookingRice : ((recipe.WholeFish != null) ? StaffStatus.CuttingLongStrip : StaffStatus.PreparingCucumber));
				if (Time.time < finishAt && workplace.Station != null)
				{
					PhysicalStateController physicalStateController2 = workplace.Station.PhysicalStateController;
					if ((object)physicalStateController2 != null && physicalStateController2.IsPhysicallyUsable)
					{
						return false;
					}
				}
				phase = Phase.Finish;
			}
			if (phase == Phase.Finish)
			{
				if (!CommitOutput(world))
				{
					Status(StaffStatus.MissingWorkplace);
					return false;
				}
				StaffQuestProgress.PrepFinished(recipe, world.Database);
				if (!rewarded)
				{
					rewarded = true;
					StaffPrepGameplayApi.Reward(employee, (recipe.Kind == StaffPrepKind.Rice) ? ProgressionXpSource.RiceCooked : ProgressionXpSource.PrepCutCompleted, recipe.Output.Id, outputCount);
				}
				workplace.Station?.ServerStaffVisual(id, null);
				Release(completed: true);
				phase = Phase.Select;
				npc.SetWorking(value: false, 1f);
				DispatchPans(world, flag);
			}
			return false;
		}

		private bool EnsureWorkplace(StaffWorkplaceRegistry world)
		{
			if (workplace.Station != null && workplace.Station.StaffEmployeeId == id)
			{
				return true;
			}
			StaffPrepWorkplace staffPrepWorkplace = ((workplace.IsReady && workplace.Station.ServerStaffAvailable) ? workplace : world.PrepStations.FirstOrDefault((StaffPrepWorkplace point) => point.IsReady && point.Station.ServerStaffAvailable));
			if (staffPrepWorkplace == null || !staffPrepWorkplace.Station.ServerAssignStaff(id))
			{
				return false;
			}
			workplace = staffPrepWorkplace;
			panPrefab = workplace.Station.StaffPanPrefab;
			resumed = false;
			return true;
		}

		private void Select(StaffEmployeeEntry employee, StaffWorkplaceRegistry world)
		{
			Dictionary<string, StaffStockNeed> dictionary = new StaffStockPolicy(world).Build(preparedOnly: true);
			StaffStatus? staffStatus = null;
			string itemId = null;
			bool flag = false;
			bool flag2 = false;
			foreach (StaffStockNeed need in from n in dictionary.Values
				orderby n.Priority descending, n.Current / Mathf.Max(1f, n.Target), n.Deadline
				select n)
			{
				StaffPrepRecipe selected = world.PrepRecipes.FirstOrDefault((StaffPrepRecipe r) => r.Input != null && r.Output != null && r.Output.Id == need.Id);
				if (selected == null || (selected.Kind == StaffPrepKind.Rice && !employee.RiceCookerEnabled))
				{
					continue;
				}
				flag = true;
				bool flag3 = pans.Any((GastroPan p) => p.IngredientId.ToString() == need.Id && p.IngredientCount < p.MaxCapacity);
				if (need.Missing <= 0f && !flag3)
				{
					continue;
				}
				flag2 = true;
				if (!reservations.TryReserve("production:" + need.Id, id))
				{
					continue;
				}
				if (selected.Kind != StaffPrepKind.Rice && workplace.PreparedPanSlots.Any((Transform p) => p != null) && !world.OutputPlacement.TryReserve(workplace.PreparedPanSlots, need.Id, id, out var _))
				{
					reservations.Release("production:" + need.Id, id);
					staffStatus = StaffStatus.StorageFull;
					itemId = null;
					continue;
				}
				recipe = selected;
				if (selected.Kind == StaffPrepKind.Rice)
				{
					machine = world.PrepMachines.FirstOrDefault((StaffPrepMachine m) => m.IsReady && m.RiceCooker != null && m.RiceCooker.ServerStaffAvailable);
					if (machine == null || !machine.RiceCooker.ServerStaffClaim(id))
					{
						Release();
						StaffStatus valueOrDefault = staffStatus.GetValueOrDefault();
						if (!staffStatus.HasValue)
						{
							valueOrDefault = StaffStatus.WaitingForMachine;
							staffStatus = valueOrDefault;
						}
						continue;
					}
				}
				input = (from i in BaseHoldable.StaffWorldItems
					where StaffPrepGameplayApi.Available(i, world) && StaffPrepGameplayApi.IsInput(i, selected.Input)
					orderby i is WholesaleEntity descending
					select i).FirstOrDefault((BaseHoldable i) => !reservations.IsReservedByOther(ItemKey(i), id));
				if (input != null && reservations.TryReserve(ItemKey(input), id))
				{
					phase = ((input is IngredientEntity) ? Phase.PickupInput : Phase.Input);
					feedback = null;
					Status(StaffStatus.CollectingItems);
					return;
				}
				if (selected.WholeFish != null && employee.FilletMachineEnabled)
				{
					Fridge fishFridge = world.FishFridge;
					if ((object)fishFridge != null && fishFridge.ServerStaffHasFish(selected.WholeFish))
					{
						machine = world.PrepMachines.FirstOrDefault((StaffPrepMachine m) => m.IsReady && m.FilletMachine != null && m.FilletMachine.ServerStaffAvailable);
						if (machine != null && machine.FilletMachine.ServerStaffClaim(id))
						{
							phase = Phase.Fridge;
							feedback = null;
							Status(StaffStatus.CollectingItems);
							return;
						}
						Release();
						StaffStatus valueOrDefault = staffStatus.GetValueOrDefault();
						if (!staffStatus.HasValue)
						{
							valueOrDefault = StaffStatus.WaitingForMachine;
							staffStatus = valueOrDefault;
						}
						continue;
					}
				}
				Release();
				if (!staffStatus.HasValue)
				{
					staffStatus = StaffStatus.WaitingForIngredients;
					itemId = selected.Input.Id;
				}
			}
			if (staffStatus.HasValue)
			{
				Feedback(staffStatus.Value, itemId);
			}
			else
			{
				feedback = null;
				Status((flag && !flag2) ? StaffStatus.ReadyStockFull : StaffStatus.Waiting);
			}
			Move(workplace.WorkPoint);
		}

		private bool CommitOutput(StaffWorkplaceRegistry world)
		{
			if (recipe.Kind == StaffPrepKind.Rice)
			{
				if (recipe.Output.Prefab == null)
				{
					return false;
				}
				Transform transform = ((machine?.OutputPoint != null) ? machine.OutputPoint : workplace.OutputPoint);
				NetworkObject networkObject = UnityEngine.Object.Instantiate(recipe.Output.Prefab, transform.position, Quaternion.identity);
				networkObject.Spawn(destroyWithScene: true);
				if (networkObject.TryGetComponent<IngredientEntity>(out var component))
				{
					component.IngredientCount = (byte)outputCount;
				}
			}
			else
			{
				NetworkObject networkObject2 = panPrefab;
				if (networkObject2 == null)
				{
					return false;
				}
				int num = outputCount;
				while (num > 0)
				{
					GastroPan gastroPan = pans.FirstOrDefault((GastroPan p) => p.IngredientId.ToString() == recipe.Output.Id && p.IngredientCount < p.MaxCapacity);
					if (gastroPan == null)
					{
						int num2 = Mathf.Min(num, recipe.Output.MaxPanCapacity);
						Transform transform2 = ((workplace.Station != null) ? workplace.Station.GastropanCenterTransform : null);
						gastroPan = StaffPrepGameplayApi.CreatePan(networkObject2, recipe.Output, num2, (transform2 != null) ? transform2.position : workplace.OutputPoint.position);
						if (gastroPan == null)
						{
							return false;
						}
						pans.Add(gastroPan);
						num -= num2;
					}
					else
					{
						int num3 = Mathf.Min(num, gastroPan.MaxCapacity - gastroPan.IngredientCount);
						gastroPan.IngredientCount += (byte)num3;
						num -= num3;
					}
					gastroPan.CanHold.Value = false;
				}
			}
			Singleton<GameProgressManager>.Instance?.RestaurantManager?.ServerAddStockRpc(recipe.Output.Id, outputCount);
			return true;
		}

		private bool DispatchPans(StaffWorkplaceRegistry world, bool all)
		{
			bool result = true;
			GastroPan[] array = pans.ToArray();
			foreach (GastroPan gastroPan in array)
			{
				if (!all && phase == Phase.Select && !rewarded && gastroPan.IngredientCount < gastroPan.MaxCapacity)
				{
					continue;
				}
				bool flag = workplace.PreparedPanSlots.Any((Transform p) => p != null);
				Transform transform = (flag ? world.OutputPlacement.PreparePan(workplace.PreparedPanSlots, gastroPan, reservations, id) : workplace.PreparedPanPoint(gastroPan.IngredientId.ToString()));
				if (gastroPan == null || !gastroPan.IsSpawned)
				{
					pans.Remove(gastroPan);
				}
				else if (transform == null)
				{
					result = false;
					Status(StaffStatus.StorageFull);
				}
				else if (!workplace.Station.CanWriteServerState())
				{
					if (!(transform == null))
					{
						gastroPan.CanHold.Value = true;
						gastroPan.ServerStaffDetachFromStation(id);
						if (gastroPan.ServerTryStaffPickup(id))
						{
							gastroPan.ServerStaffDrop(id, transform.position, transform.rotation);
							pans.Remove(gastroPan);
						}
					}
				}
				else if (workplace.Station.ServerStaffDispatchPan(id, gastroPan, transform, flag))
				{
					if (flag)
					{
						world.OutputPlacement.Track(gastroPan, transform);
					}
					movingPans.Add(gastroPan);
					pans.Remove(gastroPan);
				}
			}
			return result;
		}

		private bool Move(Transform point)
		{
			if (point == null)
			{
				npc.Stop();
				return false;
			}
			bool num = Move(point.position, (point == workplace.WorkPoint) ? manager.Catalog.PrepWorkArrivalRadius : (-1f));
			if (num)
			{
				npc.FacePoint(point);
			}
			return num;
		}

		private bool Move(Vector3 point, float arrivalTolerance = -1f)
		{
			bool result = npc.MoveTo(point, manager.Catalog.PrepMoveSpeed, arrivalTolerance);
			if (npc.InvalidTarget && (phase == Phase.PickupInput || phase == Phase.Input || phase == Phase.Fridge || phase == Phase.Fillet))
			{
				Cancel();
				return false;
			}
			if (npc.Recovered && (phase == Phase.PickupInput || phase == Phase.Input || phase == Phase.Fridge))
			{
				Cancel();
				return false;
			}
			if (npc.Recovered && phase == Phase.Fillet)
			{
				reservations.ReleaseAll(id);
				machine?.FilletMachine?.ServerStaffRelease(id);
				if (machine?.FilletMachine == null || !machine.FilletMachine.ServerStaffClaim(id) || !reservations.TryReserve("production:" + recipe.Output.Id, id))
				{
					Cancel();
					return false;
				}
			}
			return result;
		}

		private void DropCarried(StaffWorkplaceRegistry world)
		{
			foreach (BaseHoldable item in carried)
			{
				if (!(item == null) && item.IsSpawned)
				{
					if (item is IngredientEntity)
					{
						item.ServerStaffDrop(id, npc.transform.position, npc.transform.rotation);
						continue;
					}
					Transform transform = world.StorageFor(item.ItemSO.Id) ?? workplace.OutputPoint;
					item.ServerStaffDrop(id, transform.position, transform.rotation);
				}
			}
			carried.Clear();
			npc.SetHeld(null);
		}

		private static string ItemKey(BaseHoldable item)
		{
			if (!(item != null))
			{
				return string.Empty;
			}
			return "item:" + item.NetworkObjectId;
		}

		private void Status(StaffStatus status, string itemId = null)
		{
			manager.ServerSetStatus(id, status, itemId ?? string.Empty);
		}

		private void Feedback(StaffStatus status, string itemId = null)
		{
			Status(status, itemId);
			if (feedback != status)
			{
				feedback = status;
				manager.ServerStaffFeedback(id, status, itemId ?? string.Empty);
			}
		}

		private void Release(bool completed = false)
		{
			StaffWorkplaceRegistry.Instance?.ClearPrepProduction(id);
			StaffWorkplaceRegistry.Instance?.OutputPlacement.Release(id);
			reservations.ReleaseAll(id);
			machine?.RiceCooker?.ServerStaffRelease(id, completed);
			machine?.FilletMachine?.ServerStaffRelease(id);
			machine = null;
			input = null;
		}

		private void Cancel()
		{
			Release();
			if (StaffWorkplaceRegistry.Instance != null)
			{
				DropCarried(StaffWorkplaceRegistry.Instance);
			}
			phase = Phase.Select;
			npc.SetWorking(value: false, 1f);
		}

		public void Dispose()
		{
			Release();
			workplace.Station?.ServerReleaseStaff(id);
			foreach (GastroPan pan in pans)
			{
				if (pan.CanWriteServerState())
				{
					pan.CanHold.Value = true;
				}
			}
			foreach (BaseHoldable item in carried)
			{
				if (item.CanWriteServerState())
				{
					item.NetworkObject.Despawn();
				}
			}
		}
	}
}
