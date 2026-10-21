using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Core.ProcessedIngredient;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen;
using PaperPixelGames.Core.Kitchen.ChefDesk;
using PaperPixelGames.Core.Kitchen.PreparationStation.GastronomPan;
using PaperPixelGames.Core.Kitchen.Wholasale;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.ProcessedIngredient;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.Struct;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.Patterns;
using Unity.Profiling;
using UnityEngine;

namespace PaperPixelGames.Core.Staff
{
	public sealed class StaffStockPolicy
	{
		private readonly StaffWorkplaceRegistry world;

		private static readonly ProfilerMarker BuildMarker = new ProfilerMarker("Staff.Stock.Build");

		private static readonly ProfilerMarker DemandMarker = new ProfilerMarker("Staff.Stock.Demand");

		private static readonly ProfilerMarker InventoryMarker = new ProfilerMarker("Staff.Stock.Inventory");

		private static readonly ProfilerMarker AllocateMarker = new ProfilerMarker("Staff.Stock.Allocate");

		private static readonly ProfilerMarker ConvertMarker = new ProfilerMarker("Staff.Stock.Convert");

		public StaffStockPolicy(StaffWorkplaceRegistry world)
		{
			this.world = world;
			world.ConversionTable.Refresh(world);
		}

		public Dictionary<string, StaffStockNeed> Build(bool preparedOnly = false)
		{
			using (BuildMarker.Auto())
			{
				Dictionary<string, StaffStockNeed> dictionary = new Dictionary<string, StaffStockNeed>();
				TabletNetworkHandler instance = TabletNetworkHandler.Instance;
				if (instance == null)
				{
					return dictionary;
				}
				using (DemandMarker.Auto())
				{
					foreach (string item in instance.ServerStaffMenuIds())
					{
						AddDish(dictionary, item, 3, order: false, 0.0);
					}
					ApplyReadyReserves(dictionary);
					foreach (CustomerOrderEntry item2 in instance.ServerStaffOrders())
					{
						if (item2.GetStatus() == OrderReceiptStatus.Served || item2.GetStatus() == OrderReceiptStatus.Expired)
						{
							continue;
						}
						List<string> list = new List<string>();
						for (int i = 0; i < item2.DeliveredFoodIds.Length; i++)
						{
							list.Add(item2.DeliveredFoodIds[i].ToString());
						}
						for (int j = 0; j < item2.FoodIds.Length; j++)
						{
							string text = item2.FoodIds[j].ToString();
							if (!list.Remove(text))
							{
								AddDish(dictionary, text, 1, order: true, item2.ExpireServerTime);
							}
						}
					}
				}
				Dictionary<string, float> dictionary2 = new Dictionary<string, float>();
				using (InventoryMarker.Auto())
				{
					world.BeverageFridge?.ServerStaffAccumulateStock(dictionary2);
					if (!preparedOnly && world.FishFridge != null && world.FishFridge.IsSpawned && world.FishFridge.IsServer)
					{
						foreach (FridgeFishData fridgeFish in world.FishFridge.Save().FridgeFishes)
						{
							Accumulate(dictionary2, fridgeFish.Id, fridgeFish.Weights.Count);
						}
					}
					foreach (KeyValuePair<string, int> item3 in world.PendingPrepProduction)
					{
						Accumulate(dictionary2, item3.Key, item3.Value);
					}
					foreach (ChefDeskGastroPan staffStockSource in ChefDeskGastroPan.StaffStockSources)
					{
						if (staffStockSource != null && staffStockSource.IsSpawned && staffStockSource.IngredientCount > 0)
						{
							Accumulate(dictionary2, staffStockSource.CurrentIngredientID.ToString(), (int)staffStockSource.IngredientCount);
						}
					}
					foreach (BaseHoldable staffWorldItem in BaseHoldable.StaffWorldItems)
					{
						if (staffWorldItem == null || !staffWorldItem.IsSpawned || staffWorldItem.ItemSO == null || staffWorldItem.IsPlayerInventoryOwnedForPersistence || (!staffWorldItem.IsStaffCarried && !world.IsInside(staffWorldItem.transform.position)))
						{
							continue;
						}
						string id = staffWorldItem.ItemSO.Id;
						float num = 1f;
						if (staffWorldItem is WholesaleEntity wholesaleEntity)
						{
							if (wholesaleEntity.WholesaleSO.OutputItem == null)
							{
								continue;
							}
							id = wholesaleEntity.WholesaleSO.OutputItem.Id;
							num = (int)wholesaleEntity.CurrentCount;
						}
						else if (staffWorldItem is SideDishIngredientEntity sideDishIngredientEntity && sideDishIngredientEntity.StaffOutputItem != null)
						{
							id = sideDishIngredientEntity.StaffOutputItem.Id;
							num = (int)sideDishIngredientEntity.IngredientCount;
						}
						else if (staffWorldItem is IngredientEntity ingredientEntity)
						{
							num = (int)ingredientEntity.IngredientCount;
							IngredientSO ingredientSO = ingredientEntity.IngredientSO;
							if ((object)ingredientSO != null && ingredientSO.ProcessedIngredients?.Count > 0 && ingredientEntity.IngredientSO.ProcessedIngredients[0] != null)
							{
								num /= (float)Mathf.Max(1, ingredientEntity.IngredientSO.ProcessedIngredients[0].OutputYield);
							}
						}
						else if (staffWorldItem is GastroPan { IngredientId: var ingredientId } gastroPan)
						{
							id = ingredientId.ToString();
							num = (int)gastroPan.IngredientCount;
						}
						else if (staffWorldItem is ServedDish servedDish && !string.IsNullOrWhiteSpace(servedDish.RuntimeRecipeIdentityId))
						{
							id = servedDish.RuntimeRecipeIdentityId;
						}
						Accumulate(dictionary2, id, num);
					}
				}
				string[] array = (preparedOnly ? Array.Empty<string>() : dictionary2.Keys.ToArray());
				using (AllocateMarker.Auto())
				{
					foreach (StaffStockNeed item4 in from n in dictionary.Values
						orderby n.Orders > 0f descending, (!(n.Deadline > 0.0)) ? double.MaxValue : n.Deadline, n.Id
						select n)
					{
						if (dictionary2.TryGetValue(item4.Id, out var value))
						{
							item4.Current += value;
							dictionary2[item4.Id] = 0f;
						}
						string[] array2 = array;
						foreach (string text2 in array2)
						{
							if (item4.Current >= item4.Target)
							{
								break;
							}
							if (!(dictionary2[text2] <= 0f))
							{
								float num3 = Convert(text2, item4.Id, 1f);
								if (!(num3 <= 0f) && !(dictionary2[text2] <= 0f))
								{
									float num4 = Mathf.Min(dictionary2[text2], (item4.Target - item4.Current) / num3);
									item4.Current += num4 * num3;
									dictionary2[text2] -= num4;
								}
							}
						}
					}
				}
				return dictionary;
			}
		}

		private void ApplyReadyReserves(Dictionary<string, StaffStockNeed> needs)
		{
			StaffCatalogSO staffCatalogSO = StaffManager.Instance?.Catalog;
			ProcessedIngredientSO processedIngredientSO = world.PrepRecipes.FirstOrDefault((StaffPrepRecipe r) => r.Kind == StaffPrepKind.Rice)?.Output;
			if (processedIngredientSO != null && (staffCatalogSO?.PrepReadyRiceCups ?? 10) > 0 && !needs.ContainsKey(processedIngredientSO.Id))
			{
				needs[processedIngredientSO.Id] = new StaffStockNeed
				{
					Id = processedIngredientSO.Id
				};
			}
			Dictionary<string, float> dictionary = new Dictionary<string, float>();
			foreach (ChefDeskGastroPan staffStockSource in ChefDeskGastroPan.StaffStockSources)
			{
				if (staffStockSource != null && staffStockSource.IsSpawned && staffStockSource.IsServer)
				{
					Accumulate(dictionary, staffStockSource.CurrentIngredientID.ToString(), (int)staffStockSource.IngredientCount);
				}
			}
			if (processedIngredientSO != null)
			{
				foreach (BaseHoldable staffWorldItem in BaseHoldable.StaffWorldItems)
				{
					if (staffWorldItem is GastroPan { IsSpawned: not false, IsServer: not false, IsPlayerInventoryOwnedForPersistence: false } gastroPan && world.IsInside(gastroPan.transform.position) && gastroPan.IngredientId.ToString() == processedIngredientSO.Id)
					{
						Accumulate(dictionary, processedIngredientSO.Id, (int)gastroPan.IngredientCount);
					}
				}
			}
			foreach (StaffStockNeed value2 in needs.Values)
			{
				if (world.Database.FindById(value2.Id) is ProcessedIngredientSO processedIngredientSO2)
				{
					float num = ((processedIngredientSO2 == processedIngredientSO) ? ((staffCatalogSO?.PrepReadyRiceCups ?? 10) * Mathf.Max(1, processedIngredientSO2.OutputYield)) : ((staffCatalogSO?.PrepReadyPanStacks ?? 1) * processedIngredientSO2.MaxPanCapacity));
					if (!(num <= 0f))
					{
						dictionary.TryGetValue(value2.Id, out var value);
						value2.Target = Mathf.Max(value2.Target, num + value);
					}
				}
			}
		}

		private static void Accumulate(Dictionary<string, float> stock, string id, float amount)
		{
			if (!string.IsNullOrEmpty(id) && !(amount <= 0f))
			{
				stock.TryGetValue(id, out var value);
				stock[id] = value + amount;
			}
		}

		private void AddDish(Dictionary<string, StaffStockNeed> needs, string id, int count, bool order, double deadline)
		{
			CustomSushiRecipeCatalog customCatalog = Singleton<GameProgressManager>.Instance?.RestaurantManager?.CustomSushiRecipeCatalog;
			if (new SushiRecipeResolver(world.Database, customCatalog).TryResolveById(id, out var definition))
			{
				foreach (KeyValuePair<string, int> item in StaffChefIngredients.RecipeCost(definition))
				{
					Add(needs, item.Key, count * item.Value, order, deadline);
				}
				return;
			}
			if (!(world.Database.FindById(id) is ServedDishSO servedDishSO))
			{
				return;
			}
			if (servedDishSO.Recipe == null || (servedDishSO.Recipe.Ingredients?.Count ?? 0) + (servedDishSO.Recipe.ProcessedIngredients?.Count ?? 0) == 0)
			{
				Add(needs, id, count, order, deadline);
				return;
			}
			if (servedDishSO.Recipe.Ingredients != null)
			{
				foreach (IngredientNeeds ingredient in servedDishSO.Recipe.Ingredients)
				{
					if (ingredient?.Data != null)
					{
						Add(needs, ingredient.Data.Id, count * ingredient.Amount, order, deadline);
					}
				}
			}
			if (servedDishSO.Recipe.ProcessedIngredients == null)
			{
				return;
			}
			foreach (ProcessedIngredientNeeds processedIngredient in servedDishSO.Recipe.ProcessedIngredients)
			{
				if (processedIngredient?.Data != null)
				{
					Add(needs, processedIngredient.Data.Id, count * processedIngredient.Amount, order, deadline);
				}
			}
		}

		private static void Add(Dictionary<string, StaffStockNeed> needs, string id, int count, bool order, double deadline)
		{
			if (!needs.TryGetValue(id, out var value))
			{
				StaffStockNeed obj = new StaffStockNeed
				{
					Id = id
				};
				value = obj;
				needs.Add(id, obj);
			}
			value.Target += count;
			if (order)
			{
				value.Orders += count;
				if (deadline > 0.0 && (value.Deadline == 0.0 || deadline < value.Deadline))
				{
					value.Deadline = deadline;
				}
			}
		}

		public float Convert(string source, string target, float amount)
		{
			using (ConvertMarker.Auto())
			{
				return world.ConversionTable.Convert(source, target, amount);
			}
		}

		public static int PackagePrice(WholesaleSO item)
		{
			return checked(item.QuantityPrice * item.Quantity * 100);
		}

		public float PackageYield(WholesaleSO product, string target)
		{
			SideDishIngredientEntity sideDishIngredientEntity = ((product.Prefab != null) ? product.Prefab.GetComponent<SideDishIngredientEntity>() : null);
			if (!(sideDishIngredientEntity != null) || !(sideDishIngredientEntity.StaffOutputItem != null))
			{
				return Convert(product.OutputItem.Id, target, (int)product.Quantity);
			}
			return Convert(sideDishIngredientEntity.StaffOutputItem.Id, target, sideDishIngredientEntity.StaffPackageQuantity);
		}

		public static int PackagesRequired(float missing, float yield, int room, int balance, int price)
		{
			if (missing <= 0f || yield <= 0f || price <= 0)
			{
				return 0;
			}
			return Math.Max(0, Math.Min(Math.Min((int)Math.Ceiling(missing / yield), room), balance / price));
		}
	}
}
