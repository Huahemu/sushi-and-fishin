using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	public sealed class SushiRecipeDefinition
	{
		public string RecipeId { get; }

		public string DisplayName { get; }

		public SushiCompositionData Composition { get; }

		public int Price { get; }

		public Sprite Icon { get; }

		public SushiRecipeIdentityKind IdentityKind { get; }

		public SushiSO BuiltInSushi { get; }

		public SushiType SushiType => Composition?.SushiType ?? SushiType.Empty;

		public bool IsOrderable => IdentityKind != SushiRecipeIdentityKind.UnsavedCustom;

		public SushiRecipeDefinition(string recipeId, string displayName, SushiCompositionData composition, int price, Sprite icon, SushiRecipeIdentityKind identityKind, SushiSO builtInSushi = null)
		{
			RecipeId = recipeId ?? string.Empty;
			DisplayName = displayName ?? string.Empty;
			Composition = composition?.Clone();
			Price = Mathf.Max(0, price);
			Icon = icon;
			IdentityKind = identityKind;
			BuiltInSushi = builtInSushi;
		}
	}
}
