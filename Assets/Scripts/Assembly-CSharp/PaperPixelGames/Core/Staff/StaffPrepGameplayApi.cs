using System;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen.PreparationStation.GastronomPan;
using PaperPixelGames.Core.Kitchen.Wholasale;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.ProcessedIngredient;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Staff
{
	public static class StaffPrepGameplayApi
	{
		public static bool Available(BaseHoldable item, StaffWorkplaceRegistry world)
		{
			if (item != null && item.IsSpawned && !item.IsStaffCarried && item.CanHold.Value && !item.IsPlayerInventoryOwnedForPersistence && item.transform.parent == null)
			{
				return world.IsInside(item.transform.position);
			}
			return false;
		}

		public static bool IsInput(BaseHoldable item, IngredientSO input)
		{
			if (!(item is WholesaleEntity wholesaleEntity))
			{
				if (item is IngredientEntity ingredientEntity && ingredientEntity.ItemSO == input)
				{
					return ingredientEntity.IngredientCount > 0;
				}
				return false;
			}
			if (wholesaleEntity.WholesaleSO.OutputItem == input)
			{
				return wholesaleEntity.CurrentCount > 0;
			}
			return false;
		}

		public static bool Consume(BaseHoldable item, IngredientSO input, StaffWorkplaceRegistry world, out int yield, ProcessedIngredientSO output, int employeeId)
		{
			yield = 0;
			if (!((item is IngredientEntity) ? item.ServerStaffCarriedBy(employeeId) : Available(item, world)) || !IsInput(item, input))
			{
				return false;
			}
			if (item is WholesaleEntity wholesaleEntity)
			{
				if (!wholesaleEntity.ServerTryConsumeAmount(1, out var consumedAmount) || consumedAmount != 1)
				{
					return false;
				}
				yield = output.OutputYield;
			}
			else if (item is IngredientEntity ingredientEntity)
			{
				int num = ((input.ProcessedIngredients.Count <= 0) ? 1 : Mathf.Max(1, input.ProcessedIngredients[0].OutputYield));
				yield = Mathf.Max(1, Mathf.CeilToInt((float)(output.OutputYield * ingredientEntity.IngredientCount) / (float)num));
				ingredientEntity.NetworkObject.Despawn();
				Singleton<GameProgressManager>.Instance?.RestaurantManager?.ServerRemoveStockRpc(input.Id, 1);
			}
			return yield > 0;
		}

		public static GastroPan CreatePan(NetworkObject prefab, ProcessedIngredientSO output, int count, Vector3 point, Quaternion? rotation = null)
		{
			if (prefab == null || output == null || count <= 0 || prefab.GetComponent<GastroPan>() == null)
			{
				return null;
			}
			NetworkObject networkObject = UnityEngine.Object.Instantiate(prefab, point, rotation ?? Quaternion.identity);
			networkObject.Spawn(destroyWithScene: true);
			GastroPan component = networkObject.GetComponent<GastroPan>();
			component.ServerNonChefDeskInitialize(output.Id, (byte)Mathf.Min(count, output.MaxPanCapacity));
			return component;
		}

		public static double Reward(StaffEmployeeEntry employee, ProgressionXpSource source, string outputId, int count)
		{
			GameProgressManager instance = Singleton<GameProgressManager>.Instance;
			double valueOrDefault = (instance?.ProgressionManager?.TotalXp).GetValueOrDefault();
			instance?.ProgressionManager?.ServerGrantStaffXp(source, outputId);
			double num = Math.Max(0.0, (instance?.ProgressionManager?.TotalXp).GetValueOrDefault() - valueOrDefault);
			StaffManager.Instance?.Ledger.Production(employee, count, num);
			return num;
		}
	}
}
