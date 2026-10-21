using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	public class WholesaleSO : HoldableSO
	{
		public enum WholesaleAssetSourceType : byte
		{
			Grocery = 0,
			Seafood = 1
		}

		public ItemType OutputType;

		public IngredientType IngredientType;

		public ProcessedType ProcessedType;

		public ServedDishType ServedDishType;

		public byte Quantity;

		public int QuantityPrice;

		public HoldableSO OutputItem;

		[Tooltip("Shown on the quick slot while this wholesale package is empty.")]
		public Sprite EmptySprite;

		public bool UseManualShopCategory;

		public WholesaleShopCategory ManualShopCategory = WholesaleShopCategory.Other;

		public WholesaleShopCategory ShopCategory;

		public WholesaleAssetSourceType AssetSourceType;

		public bool IsRawIngredient => OutputType == ItemType.RawIngredient;

		public bool IsProcIngredient => OutputType == ItemType.ProcessedIngredient;

		public bool IsServedDish => OutputType == ItemType.ServedDish;

		private void OnValidate()
		{
			RefreshOutputInfo();
			RefreshShopCategory();
		}

		private void RefreshOutputInfo()
		{
			OutputType = ItemType.Empty;
			IngredientType = IngredientType.Empty;
			ProcessedType = ProcessedType.Empty;
			ServedDishType = ServedDishType.Empty;
			if (!(OutputItem == null))
			{
				if (OutputItem is IngredientSO ingredientSO)
				{
					OutputType = ItemType.RawIngredient;
					IngredientType = ingredientSO.IngredientType;
				}
				else if (OutputItem is ProcessedIngredientSO processedIngredientSO)
				{
					OutputType = ItemType.ProcessedIngredient;
					IngredientType = processedIngredientSO.IngredientType;
					ProcessedType = processedIngredientSO.ProcessedType;
				}
				else if (OutputItem is ServedDishSO servedDishSO)
				{
					OutputType = ItemType.ServedDish;
					ServedDishType = servedDishSO.ServedDishType;
				}
			}
		}

		private void RefreshShopCategory()
		{
			if (UseManualShopCategory)
			{
				ShopCategory = ManualShopCategory;
			}
			else
			{
				ShopCategory = ResolveAutoShopCategory();
			}
		}

		private WholesaleShopCategory ResolveAutoShopCategory()
		{
			if (OutputItem == null)
			{
				return WholesaleShopCategory.Other;
			}
			if (IsServedDish)
			{
				return ServedDishType switch
				{
					ServedDishType.Beverage => WholesaleShopCategory.Beverage, 
					ServedDishType.SideDish => WholesaleShopCategory.SideDish, 
					_ => WholesaleShopCategory.Other, 
				};
			}
			if (IsRawIngredient || IsProcIngredient)
			{
				return IngredientType switch
				{
					IngredientType.Fish => WholesaleShopCategory.Fish, 
					IngredientType.ShellFish => WholesaleShopCategory.Fish, 
					IngredientType.Cephalopod => WholesaleShopCategory.Fish, 
					_ => WholesaleShopCategory.Ingredient, 
				};
			}
			return WholesaleShopCategory.Other;
		}

		public bool MatchesShopCategory(WholesaleShopCategory category)
		{
			return category switch
			{
				WholesaleShopCategory.None => true, 
				WholesaleShopCategory.Fish => IsFishCategory(ShopCategory), 
				_ => ShopCategory == category, 
			};
		}

		private bool IsFishCategory(WholesaleShopCategory category)
		{
			if (category != WholesaleShopCategory.Fish && category != WholesaleShopCategory.FishSunlightZone && category != WholesaleShopCategory.FishMidZone)
			{
				return category == WholesaleShopCategory.FishDeepZone;
			}
			return true;
		}
	}
}
