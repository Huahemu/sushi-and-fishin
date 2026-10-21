using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Interaction;

namespace PaperPixelGames.Core.Data
{
	public static class CustomSushiRules
	{
		public const int BaseIngredientCount = 2;

		public const int MaxFillingPlacements = 20;

		public const int MaxTotalPlacements = 22;

		public static bool IsSupportedType(SushiType sushiType)
		{
			if (sushiType != SushiType.Maki && sushiType != SushiType.Uramaki)
			{
				return sushiType == SushiType.Temaki;
			}
			return true;
		}

		public static bool IsSupportedProcessedIngredient(string ingredientId, ProcessedType processedType, string noriId, string riceId)
		{
			if (string.IsNullOrWhiteSpace(ingredientId))
			{
				return false;
			}
			if (!string.Equals(ingredientId, noriId, StringComparison.Ordinal) && !string.Equals(ingredientId, riceId, StringComparison.Ordinal))
			{
				return processedType == ProcessedType.LongStrip;
			}
			return true;
		}

		public static bool TryGetRequiredBase(SushiType sushiType, string noriId, string riceId, out string firstIngredientId, out string secondIngredientId)
		{
			firstIngredientId = string.Empty;
			secondIngredientId = string.Empty;
			if (!IsSupportedType(sushiType) || string.IsNullOrWhiteSpace(noriId) || string.IsNullOrWhiteSpace(riceId))
			{
				return false;
			}
			bool flag = sushiType == SushiType.Uramaki;
			firstIngredientId = (flag ? riceId : noriId);
			secondIngredientId = (flag ? noriId : riceId);
			return true;
		}

		public static CustomSushiPlacementRejection ValidateNextPlacement(SushiType sushiType, IReadOnlyList<string> placedIngredientIds, string ingredientId, string noriId, string riceId, bool compositionLocked)
		{
			if (compositionLocked)
			{
				return CustomSushiPlacementRejection.CompositionLocked;
			}
			if (!TryGetRequiredBase(sushiType, noriId, riceId, out var firstIngredientId, out var secondIngredientId))
			{
				return CustomSushiPlacementRejection.UnsupportedSushiType;
			}
			if (string.IsNullOrWhiteSpace(ingredientId))
			{
				return CustomSushiPlacementRejection.InvalidIngredient;
			}
			int num = placedIngredientIds?.Count ?? 0;
			if (num < 2)
			{
				string b = ((num == 0) ? firstIngredientId : secondIngredientId);
				if (!string.Equals(ingredientId, b, StringComparison.Ordinal))
				{
					return CustomSushiPlacementRejection.WrongBaseIngredient;
				}
				return CustomSushiPlacementRejection.None;
			}
			if (string.Equals(ingredientId, noriId, StringComparison.Ordinal) || string.Equals(ingredientId, riceId, StringComparison.Ordinal))
			{
				return CustomSushiPlacementRejection.BaseIngredientCannotRepeat;
			}
			if (num < 22)
			{
				return CustomSushiPlacementRejection.None;
			}
			return CustomSushiPlacementRejection.FillingLimitReached;
		}

		public static bool HasValidBase(SushiType sushiType, IReadOnlyList<string> placedIngredientIds, string noriId, string riceId)
		{
			if (placedIngredientIds == null || placedIngredientIds.Count < 2 || !TryGetRequiredBase(sushiType, noriId, riceId, out var firstIngredientId, out var secondIngredientId))
			{
				return false;
			}
			if (string.Equals(placedIngredientIds[0], firstIngredientId, StringComparison.Ordinal))
			{
				return string.Equals(placedIngredientIds[1], secondIngredientId, StringComparison.Ordinal);
			}
			return false;
		}

		public static Dictionary<string, int> CountIngredientPlacements(IReadOnlyList<string> placedIngredientIds)
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.Ordinal);
			if (placedIngredientIds == null)
			{
				return dictionary;
			}
			for (int i = 0; i < placedIngredientIds.Count; i++)
			{
				string text = placedIngredientIds[i];
				if (!string.IsNullOrWhiteSpace(text))
				{
					dictionary.TryGetValue(text, out var value);
					dictionary[text] = value + 1;
				}
			}
			return dictionary;
		}

		public static bool CanReturnTopIngredient(IReadOnlyList<string> placedIngredientIds, string currentPanIngredientId, string lastEmptiedPlacementIngredientId)
		{
			if (placedIngredientIds == null || placedIngredientIds.Count == 0)
			{
				return false;
			}
			string text = placedIngredientIds[placedIngredientIds.Count - 1];
			if (!string.IsNullOrEmpty(text))
			{
				if (!string.Equals(currentPanIngredientId, text, StringComparison.Ordinal))
				{
					if (string.IsNullOrEmpty(currentPanIngredientId))
					{
						return string.Equals(lastEmptiedPlacementIngredientId, text, StringComparison.Ordinal);
					}
					return false;
				}
				return true;
			}
			return false;
		}

		public static int FindReturnableCustomIngredientIndex(IReadOnlyList<string> placedIngredientIds, string currentPanIngredientId, string lastEmptiedPlacementIngredientId)
		{
			if (placedIngredientIds == null || placedIngredientIds.Count == 0)
			{
				return -1;
			}
			string text = ((!string.IsNullOrEmpty(currentPanIngredientId)) ? currentPanIngredientId : lastEmptiedPlacementIngredientId);
			if (string.IsNullOrEmpty(text))
			{
				return -1;
			}
			for (int num = placedIngredientIds.Count - 1; num >= 0; num--)
			{
				if (string.Equals(placedIngredientIds[num], text, StringComparison.Ordinal))
				{
					if (num >= 2 || num == placedIngredientIds.Count - 1)
					{
						return num;
					}
					return -1;
				}
			}
			return -1;
		}
	}
}
