using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Interaction;

namespace PaperPixelGames.Core.Data
{
	public sealed class CustomSushiRecipeCatalog
	{
		private const string RecipeIdPrefix = "CustomRecipe_";

		private readonly List<CustomSushiRecipeData> _recipes = new List<CustomSushiRecipeData>();

		private int _nextSequence = 1;

		public IReadOnlyList<CustomSushiRecipeData> Recipes => _recipes;

		public int NextSequence => _nextSequence;

		public void Load(IEnumerable<CustomSushiRecipeData> recipes, int nextSequence)
		{
			_recipes.Clear();
			_nextSequence = Math.Max(1, nextSequence);
			if (recipes == null)
			{
				return;
			}
			foreach (CustomSushiRecipeData recipe in recipes)
			{
				if (IsValid(recipe) && !ContainsRecipeId(recipe.RecipeId) && FindExact(recipe.Composition) == null)
				{
					_recipes.Add(recipe.Clone());
					AdvanceSequencePast(recipe.RecipeId);
				}
			}
		}

		public CustomSushiRecipeData FindById(string recipeId)
		{
			if (string.IsNullOrWhiteSpace(recipeId))
			{
				return null;
			}
			for (int i = 0; i < _recipes.Count; i++)
			{
				if (string.Equals(_recipes[i].RecipeId, recipeId, StringComparison.Ordinal))
				{
					return _recipes[i];
				}
			}
			return null;
		}

		public CustomSushiRecipeData FindExact(SushiCompositionData composition)
		{
			if (composition == null)
			{
				return null;
			}
			for (int i = 0; i < _recipes.Count; i++)
			{
				if (SushiCompositionComparer.Equals(_recipes[i].Composition, composition))
				{
					return _recipes[i];
				}
			}
			return null;
		}

		public bool TryRemove(string recipeId, out CustomSushiRecipeData removedRecipe)
		{
			removedRecipe = null;
			if (string.IsNullOrWhiteSpace(recipeId))
			{
				return false;
			}
			for (int i = 0; i < _recipes.Count; i++)
			{
				if (string.Equals(_recipes[i].RecipeId, recipeId, StringComparison.Ordinal))
				{
					removedRecipe = _recipes[i];
					_recipes.RemoveAt(i);
					return true;
				}
			}
			return false;
		}

		public bool TryCreate(string displayName, SushiCompositionData composition, int price, out CustomSushiRecipeData recipe, out CustomSushiRecipeData existingRecipe)
		{
			recipe = null;
			existingRecipe = FindExact(composition);
			if (existingRecipe != null || composition == null || composition.Count == 0)
			{
				return false;
			}
			string displayName2 = (string.IsNullOrWhiteSpace(displayName) ? $"customSushi_{_nextSequence:000}" : displayName.Trim());
			recipe = new CustomSushiRecipeData
			{
				RecipeId = AllocateRecipeId(),
				DisplayName = displayName2,
				Composition = composition.Clone(),
				Price = Math.Max(0, price)
			};
			_recipes.Add(recipe);
			return true;
		}

		public List<CustomSushiRecipeData> CreateSaveSnapshot()
		{
			List<CustomSushiRecipeData> list = new List<CustomSushiRecipeData>(_recipes.Count);
			for (int i = 0; i < _recipes.Count; i++)
			{
				list.Add(_recipes[i].Clone());
			}
			return list;
		}

		private string AllocateRecipeId()
		{
			string text;
			do
			{
				text = string.Format("{0}{1:0000}", "CustomRecipe_", _nextSequence);
				_nextSequence++;
			}
			while (ContainsRecipeId(text));
			return text;
		}

		private bool ContainsRecipeId(string recipeId)
		{
			return FindById(recipeId) != null;
		}

		private void AdvanceSequencePast(string recipeId)
		{
			if (!string.IsNullOrWhiteSpace(recipeId) && recipeId.StartsWith("CustomRecipe_", StringComparison.Ordinal) && int.TryParse(recipeId.Substring("CustomRecipe_".Length), out var result))
			{
				_nextSequence = Math.Max(_nextSequence, result + 1);
			}
		}

		private static bool IsValid(CustomSushiRecipeData recipe)
		{
			if (recipe != null && !string.IsNullOrWhiteSpace(recipe.RecipeId) && recipe.Composition != null && recipe.Composition.SushiType != SushiType.Empty)
			{
				return recipe.Composition.Count > 0;
			}
			return false;
		}
	}
}
