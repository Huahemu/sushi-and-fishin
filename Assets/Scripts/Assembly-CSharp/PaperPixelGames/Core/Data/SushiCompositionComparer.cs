using System;
using System.Collections.Generic;

namespace PaperPixelGames.Core.Data
{
	public static class SushiCompositionComparer
	{
		public static bool Equals(SushiCompositionData left, SushiCompositionData right)
		{
			if (left == right)
			{
				return true;
			}
			if (left == null || right == null || left.SushiType != right.SushiType)
			{
				return false;
			}
			IReadOnlyList<string> ingredientIds = left.IngredientIds;
			IReadOnlyList<string> ingredientIds2 = right.IngredientIds;
			int num = ingredientIds?.Count ?? 0;
			int num2 = ingredientIds2?.Count ?? 0;
			if (num != num2)
			{
				return false;
			}
			for (int i = 0; i < num; i++)
			{
				if (!string.Equals(ingredientIds[i], ingredientIds2[i], StringComparison.Ordinal))
				{
					return false;
				}
			}
			return true;
		}
	}
}
