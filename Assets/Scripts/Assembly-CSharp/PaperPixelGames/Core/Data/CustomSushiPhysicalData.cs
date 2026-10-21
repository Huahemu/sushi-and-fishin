using System;

namespace PaperPixelGames.Core.Data
{
	[Serializable]
	public sealed class CustomSushiPhysicalData
	{
		public SushiRecipeIdentityKind IdentityKind = SushiRecipeIdentityKind.UnsavedCustom;

		public string RecipeId = string.Empty;

		public string DisplayName = string.Empty;

		public SushiCompositionData Composition = new SushiCompositionData();

		public int Price;

		public CustomSushiPhysicalData Clone()
		{
			return new CustomSushiPhysicalData
			{
				IdentityKind = IdentityKind,
				RecipeId = (RecipeId ?? string.Empty),
				DisplayName = (DisplayName ?? string.Empty),
				Composition = Composition?.Clone(),
				Price = Math.Max(0, Price)
			};
		}
	}
}
