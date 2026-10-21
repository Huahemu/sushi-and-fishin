using System.Linq;
using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	public sealed class SushiRecipeResolver
	{
		private readonly ItemDatabaseSO _itemDatabase;

		private readonly CustomSushiRecipeCatalog _customCatalog;

		public SushiRecipeResolver(ItemDatabaseSO itemDatabase, CustomSushiRecipeCatalog customCatalog)
		{
			_itemDatabase = itemDatabase;
			_customCatalog = customCatalog;
		}

		public bool TryResolveById(string recipeId, out SushiRecipeDefinition definition)
		{
			definition = null;
			if (string.IsNullOrWhiteSpace(recipeId))
			{
				return false;
			}
			if (_itemDatabase != null && _itemDatabase.FindById(recipeId) is SushiSO sushiSO)
			{
				definition = CreateBuiltInDefinition(sushiSO);
				return definition != null;
			}
			CustomSushiRecipeData customSushiRecipeData = _customCatalog?.FindById(recipeId);
			if (customSushiRecipeData == null)
			{
				return false;
			}
			definition = CreateCustomDefinition(customSushiRecipeData);
			return true;
		}

		public SushiRecipeDefinition ResolveExact(SushiCompositionData composition)
		{
			if (composition == null)
			{
				return null;
			}
			if (_itemDatabase != null)
			{
				foreach (SushiSO item in _itemDatabase.GetAllOfType(ItemType.ServedDish).OfType<SushiSO>())
				{
					if (SushiCompositionData.TryCreateFromBuiltIn(item, out var composition2) && SushiCompositionComparer.Equals(composition2, composition))
					{
						return CreateBuiltInDefinition(item);
					}
				}
			}
			CustomSushiRecipeData customSushiRecipeData = _customCatalog?.FindExact(composition);
			if (customSushiRecipeData == null)
			{
				return null;
			}
			return CreateCustomDefinition(customSushiRecipeData);
		}

		public static SushiRecipeDefinition CreateBuiltInDefinition(SushiSO sushiSO)
		{
			if (!SushiCompositionData.TryCreateFromBuiltIn(sushiSO, out var composition))
			{
				return null;
			}
			return new SushiRecipeDefinition(sushiSO.Id, sushiSO.LocalizedName, composition, sushiSO.Price, sushiSO.Sprite, SushiRecipeIdentityKind.BuiltIn, sushiSO);
		}

		public static SushiRecipeDefinition CreateCustomDefinition(CustomSushiRecipeData recipe, Sprite icon = null)
		{
			if (recipe?.Composition == null)
			{
				return null;
			}
			return new SushiRecipeDefinition(recipe.RecipeId, recipe.DisplayName, recipe.Composition, recipe.Price, CustomSushiThumbnailStore.GetIcon(recipe.RecipeId) ?? icon, SushiRecipeIdentityKind.SavedCustom);
		}
	}
}
