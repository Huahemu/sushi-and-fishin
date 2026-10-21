using System;
using PaperPixelGames.Core.Data;

namespace PaperPixelGames.Core.Staff
{
	[Serializable]
	public sealed class StaffPrepRecipe
	{
		public IngredientSO Input;

		public ProcessedIngredientSO Output;

		public StaffPrepKind Kind;

		public FishSO WholeFish;
	}
}
