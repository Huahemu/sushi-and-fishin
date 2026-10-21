using System;
using PaperPixelGames.Core.Interaction;

namespace PaperPixelGames.Core.Data
{
	[Serializable]
	public sealed class CustomSushiRecipeData
	{
		public string RecipeId = string.Empty;

		public string DisplayName = string.Empty;

		public SushiCompositionData Composition = new SushiCompositionData();

		public int Price;

		public string IconId = string.Empty;

		public string IconJpegBase64 = string.Empty;

		public SushiType SushiType => Composition?.SushiType ?? SushiType.Empty;

		public CustomSushiRecipeData Clone()
		{
			return new CustomSushiRecipeData
			{
				RecipeId = RecipeId,
				DisplayName = DisplayName,
				Composition = (Composition?.Clone() ?? new SushiCompositionData()),
				Price = Price,
				IconId = IconId,
				IconJpegBase64 = IconJpegBase64
			};
		}
	}
}
