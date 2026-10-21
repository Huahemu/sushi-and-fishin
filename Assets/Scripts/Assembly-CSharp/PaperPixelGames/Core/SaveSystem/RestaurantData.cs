using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Data;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public class RestaurantData
	{
		public RestaurantUpgradeData RestaurantUpgradeData = new RestaurantUpgradeData();

		public List<GastropanData> GastropanData = new List<GastropanData>();

		public List<ChefDeskGastropanData> ChefDeskGastropanData = new List<ChefDeskGastropanData>();

		public List<IngredientData> IngredientData = new List<IngredientData>();

		public List<WholesaleData> WholesaleData = new List<WholesaleData>();

		public List<DeadFishData> DeadFishData = new List<DeadFishData>();

		public List<byte> BeverageData = new List<byte>();

		public RestaurantSignCustomizationData RestaurantSignCustomizationData = new RestaurantSignCustomizationData();

		public RestaurantDirtData RestaurantDirtData = new RestaurantDirtData();

		public List<ServedDishData> ServedDishData = new List<ServedDishData>();

		public List<CustomSushiRecipeData> CustomSushiRecipes = new List<CustomSushiRecipeData>();

		public int NextCustomSushiRecipeSequence = 1;

		public FridgeData FridgeData = new FridgeData();

		public RadioData RadioData = new RadioData();

		public FishTrophyData FishTrophyData = new FishTrophyData();
	}
}
