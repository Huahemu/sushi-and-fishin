using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen.ChefDesk;
using PaperPixelGames.Core.Kitchen.PreparationStation.GastronomPan;
using PaperPixelGames.Core.Kitchen.Wholasale;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.ProcessedIngredient;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Staff
{
	public sealed class StaffChefIngredients
	{
		private sealed class Line
		{
			public string Id;

			public int Amount;

			public ChefDeskGastroPan Slot;

			public readonly List<BaseHoldable> Sources = new List<BaseHoldable>();

			public bool Swap;

			public Transform ReturnPoint;

			public string ReturnType;
		}

		private readonly List<Line> lines = new List<Line>();

		private readonly StaffChefWorkplace workplace;

		private readonly StaffWorkplaceRegistry world;

		private readonly StaffReservationService reservations;

		private readonly int employee;

		private bool planned;

		public bool HasSwap => lines.Any((Line l) => l.Swap);

		public bool StorageBlocked { get; private set; }

		public StaffChefIngredients(StaffChefWorkplace workplace, StaffWorkplaceRegistry world, StaffReservationService reservations, int employee)
		{
			this.workplace = workplace;
			this.world = world;
			this.reservations = reservations;
			this.employee = employee;
		}

		public static Dictionary<string, int> RecipeCost(SushiSO recipe)
		{
			return RecipeCost(SushiRecipeResolver.CreateBuiltInDefinition(recipe));
		}

		public static Dictionary<string, int> RecipeCost(SushiRecipeDefinition recipe)
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			if (recipe?.Composition?.IngredientIds == null)
			{
				return dictionary;
			}
			foreach (string ingredientId in recipe.Composition.IngredientIds)
			{
				if (!string.IsNullOrEmpty(ingredientId))
				{
					dictionary.TryGetValue(ingredientId, out var value);
					dictionary[ingredientId] = value + 1;
				}
			}
			return dictionary;
		}

		public bool Plan(SushiSO recipe)
		{
			return Plan(SushiRecipeResolver.CreateBuiltInDefinition(recipe));
		}

		public bool Plan(SushiRecipeDefinition recipe)
		{
			planned = false;
			lines.Clear();
			StorageBlocked = false;
			Dictionary<string, int> dictionary = workplace.Desk.ServerStaffCredits(recipe?.BuiltInSushi);
			Dictionary<string, int> cost = RecipeCost(recipe);
			if (cost.Count == 0 || workplace.Desk.GastroPanPrefab == null)
			{
				return false;
			}
			HashSet<ChefDeskGastroPan> usedSlots = new HashSet<ChefDeskGastroPan>();
			foreach (KeyValuePair<string, int> need in cost)
			{
				int amount = Mathf.Max(0, need.Value - (dictionary.TryGetValue(need.Key, out var value) ? value : 0));
				if (amount == 0)
				{
					continue;
				}
				ProcessedIngredientSO processedIngredientSO = world.Database.FindById(need.Key) as ProcessedIngredientSO;
				Line line = new Line
				{
					Id = need.Key,
					Amount = amount
				};
				if (processedIngredientSO != null)
				{
					line.Slot = workplace.Desk.GastroPans.FirstOrDefault((ChefDeskGastroPan p) => p != null && !usedSlots.Contains(p) && p.CurrentIngredientID.ToString() == need.Key);
					Line line2 = line;
					if ((object)line2.Slot == null)
					{
						line2.Slot = (from p in workplace.Desk.GastroPans
							where p != null && !usedSlots.Contains(p) && (p.IngredientCount == 0 || !cost.ContainsKey(p.CurrentIngredientID.ToString()))
							orderby p.IngredientCount
							select p).FirstOrDefault();
					}
					if (line.Slot == null || (recipe.BuiltInSushi != null && amount > processedIngredientSO.MaxPanCapacity))
					{
						return false;
					}
					usedSlots.Add(line.Slot);
					if (!reservations.TryReserve("chefpan:" + line.Slot.NetworkObjectId, employee))
					{
						return false;
					}
					if (line.Slot.CurrentIngredientID.ToString() == need.Key && line.Slot.IngredientCount >= amount)
					{
						lines.Add(line);
						continue;
					}
					GastroPan gastroPan = (from p in BaseHoldable.StaffWorldItems.OfType<GastroPan>()
						where Available(p) && p.IngredientId.ToString() == need.Key && p.IngredientCount >= amount
						orderby p.IngredientCount descending
						select p).FirstOrDefault();
					if (gastroPan != null)
					{
						if (!reservations.TryReserve("item:" + gastroPan.NetworkObjectId, employee))
						{
							return false;
						}
						line.Sources.Add(gastroPan);
						line.Swap = true;
					}
				}
				int num = ((line.Slot != null && line.Slot.CurrentIngredientID.ToString() == need.Key) ? line.Slot.IngredientCount : 0);
				if (!line.Swap)
				{
					int num2 = amount - num;
					foreach (var item in from item in BaseHoldable.StaffWorldItems.Where(Available)
						select new
						{
							Item = item,
							Count = SourceCount(item, need.Key)
						} into source
						where source.Count > 0
						orderby source.Count, source.Item.NetworkObjectId
						select source)
					{
						if (reservations.TryReserve("item:" + item.Item.NetworkObjectId, employee))
						{
							line.Sources.Add(item.Item);
							num2 -= item.Count;
							if (num2 <= 0)
							{
								break;
							}
						}
					}
					if (num2 > 0)
					{
						return false;
					}
				}
				lines.Add(line);
			}
			foreach (Line line3 in lines)
			{
				if (line3.Slot != null && line3.Slot.IngredientCount > 0 && (line3.Swap || line3.Slot.CurrentIngredientID.ToString() != line3.Id))
				{
					line3.ReturnType = line3.Slot.CurrentIngredientID.ToString();
					if (!world.OutputPlacement.TryReserve(PrepReturnSlots(line3.ReturnType, randomize: true), line3.ReturnType, employee, out line3.ReturnPoint))
					{
						StorageBlocked = true;
						return false;
					}
				}
			}
			planned = true;
			return true;
		}

		private List<Transform> PrepReturnSlots(string ingredientId, bool randomize = false)
		{
			List<StaffPrepWorkplace> list = world.PrepStations.Where((StaffPrepWorkplace p) => p.Station != null && p.Station.IsSpawned && p.Station.PhysicalStateController.IsPhysicallyUsable).ToList();
			if (randomize)
			{
				for (int num = list.Count - 1; num > 0; num--)
				{
					int num2 = Random.Range(0, num + 1);
					int index = num;
					List<StaffPrepWorkplace> list2 = list;
					int index2 = num2;
					StaffPrepWorkplace staffPrepWorkplace = list[num2];
					StaffPrepWorkplace staffPrepWorkplace2 = list[num];
					StaffPrepWorkplace staffPrepWorkplace3 = (list[index] = staffPrepWorkplace);
					staffPrepWorkplace3 = (list2[index2] = staffPrepWorkplace2);
				}
			}
			return (from point in list.SelectMany((StaffPrepWorkplace p) => (!p.PreparedPanSlots.Any((Transform slot) => slot != null)) ? (from slot in p.PreparedPanPoints
					where slot.Item != null && slot.Item.Id == ingredientId
					select slot.Point) : p.PreparedPanSlots)
				where point != null && world.IsInside(point.position)
				select point).Distinct().ToList();
		}

		private bool Available(BaseHoldable item)
		{
			if (StaffPrepGameplayApi.Available(item, world) && !reservations.IsReservedByOther("item:" + item.NetworkObjectId, employee))
			{
				return !lines.Any((Line l) => l.Sources.Contains(item));
			}
			return false;
		}

		private int SourceCount(BaseHoldable item, string ingredientId)
		{
			if (item is GastroPan { IngredientId: var ingredientId2 } gastroPan)
			{
				if (!(ingredientId2.ToString() == ingredientId))
				{
					return 0;
				}
				return gastroPan.IngredientCount;
			}
			if (item is IngredientEntity ingredientEntity && (ingredientEntity.ItemSO.Id == ingredientId || (ingredientEntity.IsPlaceableOnChefDesk && ingredientEntity.IngredientSO?.ProcessedIngredients.FirstOrDefault()?.Id == ingredientId)))
			{
				return ingredientEntity.IngredientCount;
			}
			if (item is WholesaleEntity wholesaleEntity && (wholesaleEntity.WholesaleSO.OutputItem?.Id == ingredientId || (ingredientId == world.Database.ProcessedNoriSO.Id && wholesaleEntity.WholesaleSO.OutputItem is IngredientSO ingredientSO && ingredientSO.ProcessedIngredients.Any((ProcessedIngredientSO p) => p.Id == ingredientId))))
			{
				return wholesaleEntity.CurrentCount;
			}
			return 0;
		}

		public bool Validate()
		{
			if (!planned || !workplace.IsReady || workplace.Desk.StaffEmployeeId != employee || !workplace.Desk.PhysicalStateController.IsPhysicallyUsable)
			{
				return false;
			}
			foreach (Line line in lines)
			{
				if (line.Slot != null && (!line.Slot.IsSpawned || reservations.IsReservedByOther("chefpan:" + line.Slot.NetworkObjectId, employee)))
				{
					return false;
				}
				if (line.Slot != null && line.Slot.IngredientCount > 0 && (line.Swap || line.Slot.CurrentIngredientID.ToString() != line.Id))
				{
					if (line.Slot.CurrentIngredientID.ToString() != line.ReturnType || !(world.Database.FindById(line.ReturnType) is ProcessedIngredientSO processedIngredientSO) || line.Slot.IngredientCount > processedIngredientSO.MaxPanCapacity)
					{
						return false;
					}
					if (!world.OutputPlacement.CanPlace(PrepReturnSlots(line.ReturnType), line.ReturnPoint, line.ReturnType))
					{
						StorageBlocked = true;
						return false;
					}
				}
				int num = ((line.Slot != null && line.Slot.CurrentIngredientID.ToString() == line.Id) ? line.Slot.IngredientCount : 0);
				int num2 = ((!line.Swap) ? num : 0);
				foreach (BaseHoldable source in line.Sources)
				{
					if (!StaffPrepGameplayApi.Available(source, world) || reservations.IsReservedByOther("item:" + source.NetworkObjectId, employee))
					{
						return false;
					}
					num2 += SourceCount(source, line.Id);
				}
				if (num2 < line.Amount)
				{
					return false;
				}
			}
			return true;
		}

		public bool Commit(SushiSO recipe, float swapSeconds)
		{
			return Commit(SushiRecipeResolver.CreateBuiltInDefinition(recipe), swapSeconds);
		}

		public bool Commit(SushiRecipeDefinition recipe, float swapSeconds)
		{
			if (!Validate())
			{
				return false;
			}
			NetworkObject component = workplace.Desk.GastroPanPrefab.GetComponent<NetworkObject>();
			if (component == null || component.GetComponent<GastroPan>() == null)
			{
				return false;
			}
			HashSet<BaseHoldable> excluded = new HashSet<BaseHoldable>(lines.SelectMany((Line line) => line.Sources));
			planned = false;
			foreach (Line line in lines)
			{
				bool flag = line.Sources.Count > 0;
				int num = ((line.Slot != null && line.Slot.CurrentIngredientID.ToString() == line.Id) ? line.Slot.IngredientCount : 0);
				if (line.Slot != null && (line.Swap || line.Slot.CurrentIngredientID.ToString() != line.Id) && line.Slot.IngredientCount > 0)
				{
					ProcessedIngredientSO output = world.Database.FindById(line.Slot.CurrentIngredientID.ToString()) as ProcessedIngredientSO;
					if (line.ReturnPoint == null)
					{
						return false;
					}
					GastroPan pan = StaffPrepGameplayApi.CreatePan(component, output, line.Slot.IngredientCount, line.ReturnPoint.position, line.ReturnPoint.rotation);
					world.OutputPlacement.MergePan(PrepReturnSlots(line.ReturnType), pan, reservations, employee, excluded);
					num = 0;
				}
				foreach (BaseHoldable source in line.Sources)
				{
					int num2 = (line.Swap ? SourceCount(source, line.Id) : Mathf.Min(SourceCount(source, line.Id), Mathf.Max(0, line.Amount - num)));
					if (num2 == 0)
					{
						continue;
					}
					if (source is GastroPan gastroPan)
					{
						gastroPan.IngredientCount -= (byte)num2;
						if (gastroPan.IngredientCount == 0)
						{
							gastroPan.NetworkObject.Despawn();
						}
					}
					else if (source is IngredientEntity ingredientEntity)
					{
						ingredientEntity.IngredientCount -= (byte)num2;
						if (ingredientEntity.IngredientCount == 0)
						{
							ingredientEntity.NetworkObject.Despawn();
						}
					}
					else if (source is WholesaleEntity wholesaleEntity)
					{
						wholesaleEntity.ServerTryConsumeAmount((byte)num2, out var _);
					}
					num += num2;
				}
				if (line.Slot != null)
				{
					line.Slot.ServerStaffSet(employee, (ProcessedIngredientSO)world.Database.FindById(line.Id), num - line.Amount, workplace.PanStackPoint.position, line.Swap ? swapSeconds : 0f);
				}
				if (flag && line.Slot != null)
				{
					StaffQuestProgress.ChefIngredient(line.Id, world.Database);
				}
			}
			foreach (KeyValuePair<string, int> item in RecipeCost(recipe))
			{
				Singleton<GameProgressManager>.Instance?.RestaurantManager?.ServerRemoveStockRpc(item.Key, item.Value);
			}
			workplace.Desk.ServerStaffBegin(employee, recipe);
			if (recipe.BuiltInSushi != null)
			{
				StaffQuestProgress.ChefStarted(recipe.BuiltInSushi, world.Database);
			}
			return true;
		}
	}
}
