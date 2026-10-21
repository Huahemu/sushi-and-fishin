using System;
using System.Collections.Generic;

namespace PaperPixelGames.Core.Data
{
	[Serializable]
	public class Recipe
	{
		public List<IngredientNeeds> Ingredients;

		public List<ProcessedIngredientNeeds> ProcessedIngredients;
	}
}
