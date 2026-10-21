using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Interaction;

namespace PaperPixelGames.Core.Data
{
	[Serializable]
	public sealed class SushiCompositionData
	{
		public SushiType SushiType;

		public List<string> IngredientIds = new List<string>();

		public int Count => IngredientIds?.Count ?? 0;

		public SushiCompositionData Clone()
		{
			return new SushiCompositionData
			{
				SushiType = SushiType,
				IngredientIds = ((IngredientIds != null) ? new List<string>(IngredientIds) : new List<string>())
			};
		}

		public static bool TryCreateFromBuiltIn(SushiSO sushiSO, out SushiCompositionData composition)
		{
			composition = null;
			if (sushiSO == null || sushiSO.Recipe == null)
			{
				return false;
			}
			List<BaseIngredientNeed> list = new List<BaseIngredientNeed>();
			if (sushiSO.Recipe.Ingredients != null)
			{
				list.AddRange(sushiSO.Recipe.Ingredients);
			}
			if (sushiSO.Recipe.ProcessedIngredients != null)
			{
				list.AddRange(sushiSO.Recipe.ProcessedIngredients);
			}
			list = list.OrderBy((BaseIngredientNeed need) => need.Order).ToList();
			if (list.Count == 0)
			{
				return false;
			}
			List<string> list2 = new List<string>();
			for (int num = 0; num < list.Count; num++)
			{
				BaseIngredientNeed baseIngredientNeed = list[num];
				if (baseIngredientNeed == null || string.IsNullOrWhiteSpace(baseIngredientNeed.IngredientId) || baseIngredientNeed.RequiredAmount == 0)
				{
					return false;
				}
				for (int num2 = 0; num2 < baseIngredientNeed.RequiredAmount; num2++)
				{
					list2.Add(baseIngredientNeed.IngredientId);
				}
			}
			composition = new SushiCompositionData
			{
				SushiType = sushiSO.SushiType,
				IngredientIds = list2
			};
			return true;
		}
	}
}
