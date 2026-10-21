using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Interaction;

namespace PaperPixelGames.Core.Data
{
	public static class CustomSushiPricing
	{
		public const float MakiBasePrice = 6f;

		public const float UramakiBasePrice = 8f;

		public const float TemakiBasePrice = 6f;

		public const float MinimumFillingPlacementValue = 1f;

		public const float SeafoodPlacementPremium = 1f;

		public static int Calculate(SushiCompositionData composition, string noriId, string riceId, Func<string, ProcessedIngredientSO> resolveIngredient, int? exactRecipePrice = null)
		{
			if (exactRecipePrice.HasValue)
			{
				return Math.Max(0, exactRecipePrice.Value);
			}
			if (composition?.IngredientIds == null || !CustomSushiRules.IsSupportedType(composition.SushiType))
			{
				return 0;
			}
			double num = GetBasePrice(composition.SushiType);
			IReadOnlyList<string> ingredientIds = composition.IngredientIds;
			for (int i = 0; i < ingredientIds.Count; i++)
			{
				string text = ingredientIds[i];
				if (!string.Equals(text, noriId, StringComparison.Ordinal) && !string.Equals(text, riceId, StringComparison.Ordinal))
				{
					ProcessedIngredientSO processedIngredientSO = resolveIngredient?.Invoke(text);
					if (processedIngredientSO != null)
					{
						num += GetPlacementValue(processedIngredientSO);
					}
				}
			}
			if (num >= 2147483647.0)
			{
				return int.MaxValue;
			}
			return Math.Max(0, (int)Math.Round(num, MidpointRounding.AwayFromZero));
		}

		public static double GetPlacementValue(ProcessedIngredientSO ingredient)
		{
			if (ingredient == null)
			{
				return 0.0;
			}
			double num = Math.Max(0.0, ingredient.CustomSushiPlacementValue);
			IngredientType ingredientType = ingredient.IngredientType;
			if (ingredientType != IngredientType.Fish && ingredientType != IngredientType.ShellFish && ingredientType != IngredientType.Cephalopod)
			{
				return Math.Max(1.0, num);
			}
			return Math.Max(1.0, num + 1.0);
		}

		public static float GetBasePrice(SushiType sushiType)
		{
			return sushiType switch
			{
				SushiType.Maki => 6f, 
				SushiType.Uramaki => 8f, 
				SushiType.Temaki => 6f, 
				_ => 0f, 
			};
		}

		public static bool TryGetEquivalentBuiltInPrice(SushiCompositionData composition, IEnumerable<SushiSO> builtInRecipes, out int price)
		{
			price = 0;
			if (composition?.IngredientIds == null || builtInRecipes == null)
			{
				return false;
			}
			Dictionary<string, int> first = CustomSushiRules.CountIngredientPlacements(composition.IngredientIds);
			foreach (SushiSO builtInRecipe in builtInRecipes)
			{
				if (!(builtInRecipe == null) && builtInRecipe.SushiType == composition.SushiType && SushiCompositionData.TryCreateFromBuiltIn(builtInRecipe, out var composition2) && composition2.IngredientIds.Count == composition.IngredientIds.Count)
				{
					Dictionary<string, int> second = CustomSushiRules.CountIngredientPlacements(composition2.IngredientIds);
					if (HaveSameCounts(first, second))
					{
						price = Math.Max(0, builtInRecipe.Price);
						return true;
					}
				}
			}
			return false;
		}

		private static bool HaveSameCounts(IReadOnlyDictionary<string, int> first, IReadOnlyDictionary<string, int> second)
		{
			if (first.Count != second.Count)
			{
				return false;
			}
			foreach (KeyValuePair<string, int> item in first)
			{
				if (!second.TryGetValue(item.Key, out var value) || value != item.Value)
				{
					return false;
				}
			}
			return true;
		}
	}
}
