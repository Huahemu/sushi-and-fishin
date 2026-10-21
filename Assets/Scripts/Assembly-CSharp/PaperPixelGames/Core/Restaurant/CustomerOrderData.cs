using System;
using PaperPixelGames.Core.Data;

namespace PaperPixelGames.Core.Restaurant
{
	[Serializable]
	public class CustomerOrderData
	{
		public ServedDishSO MainDishSO;

		public SushiRecipeDefinition MainDishRecipe;

		public string MainDishRecipeId;

		public ServedDishSO SideDishSO;

		public ServedDishSO BeverageSO;

		public string MainDishIdentityId
		{
			get
			{
				object obj;
				if (string.IsNullOrWhiteSpace(MainDishRecipeId))
				{
					obj = MainDishSO?.Id;
					if (obj == null)
					{
						return string.Empty;
					}
				}
				else
				{
					obj = MainDishRecipeId;
				}
				return (string)obj;
			}
		}

		public bool HasMainDish => !string.IsNullOrWhiteSpace(MainDishIdentityId);

		public int MainDishPrice
		{
			get
			{
				if (MainDishRecipe == null)
				{
					return MainDishSO?.Price ?? 0;
				}
				return MainDishRecipe.Price;
			}
		}

		public string MainDishDisplayName
		{
			get
			{
				object obj;
				if (MainDishRecipe == null)
				{
					obj = MainDishSO?.LocalizedName;
					if (obj == null)
					{
						return string.Empty;
					}
				}
				else
				{
					obj = MainDishRecipe.DisplayName;
				}
				return (string)obj;
			}
		}
	}
}
