using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;

namespace PaperPixelGames.Core.Staff
{
	public static class StaffQuestProgress
	{
		public static void Complete(QuestId id)
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if ((object)singleton != null && singleton.IsServer)
			{
				QuestReferenceHub.Instance?.CompleteIfActive(id);
			}
		}

		public static void Add(QuestId id)
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if ((object)singleton != null && singleton.IsServer)
			{
				QuestReferenceHub.Instance?.AddProgress(id);
			}
		}

		public static void SushiProduced(SushiSO recipe, ItemDatabaseSO db)
		{
			if (!(recipe == null) && !(db == null))
			{
				if (recipe == db.CucumberMakiServeredDishSO)
				{
					Add(QuestId.MakeCucumberMaki);
				}
				else if (recipe == db.MackarelNigiriServeredDishSO)
				{
					Add(QuestId.MakeMackerelNigiri);
				}
				else if (recipe == db.ChinookSalmonTemakiServeredDishSO)
				{
					Add(QuestId.MakeAChinookSalmonDish);
				}
				else if (recipe == db.SquidNigiriServeredDishSO)
				{
					Add(QuestId.MakeSquidNigiri);
				}
			}
		}

		public static void ChefStarted(SushiSO recipe, ItemDatabaseSO db)
		{
			Complete(QuestId.UseChefDesk);
			Complete(QuestId.UseChefDesk_01);
			if (recipe == db?.CucumberMakiServeredDishSO)
			{
				Complete(QuestId.SelectCucumberMakiRecipeFromRecipeBook);
			}
			if (recipe == db?.MackarelNigiriServeredDishSO)
			{
				Complete(QuestId.SelectMackerelNigiriRecipeFromRecipeBook);
			}
		}

		public static void ChefIngredient(string id, ItemDatabaseSO db)
		{
			if (!(db == null))
			{
				if (id == db.ProcessedNoriSO?.Id)
				{
					Complete(QuestId.AddNoriToChefDeskGastroPan);
				}
				if (id == db.CookedRice?.Id)
				{
					Complete(QuestId.AddCookedRiceToChefDeskGastroPan);
				}
				if (id == db.CucumberLongStripSO?.Id)
				{
					Complete(QuestId.AddSlicedCucumberToChefDeskGastroPan);
				}
			}
		}

		public static void PrepStarted(StaffPrepRecipe recipe, ItemDatabaseSO db)
		{
			if (recipe.Kind == StaffPrepKind.Rice)
			{
				Complete(QuestId.TakeRawRiceFromRiceSack);
				Complete(QuestId.AddRiceToRiceCooker);
				Complete(QuestId.StartTheRiceCooker);
				return;
			}
			if (recipe.Output == db.CucumberLongStripSO)
			{
				Complete(QuestId.TakeOneCucumberFromCucumberWholesale);
				Complete(QuestId.UsePrepStation);
				Complete(QuestId.SelectLongStripKnifePrepStation);
			}
			if (recipe.Output == db.MackeralNigiriSO)
			{
				Complete(QuestId.UsePrepStation_01);
				Complete(QuestId.SelectNigiriCutKnifePrepStation);
			}
		}

		public static void PrepFinished(StaffPrepRecipe recipe, ItemDatabaseSO db)
		{
			if (recipe.Kind == StaffPrepKind.Rice)
			{
				Complete(QuestId.TakeCookedRice);
			}
			if (recipe.Output == db.CucumberLongStripSO)
			{
				Complete(QuestId.SliceCucumberAtPrepStation);
				Complete(QuestId.TakeSlicedCucumber);
			}
			if (recipe.Output == db.MackeralNigiriSO)
			{
				Complete(QuestId.SliceMackerelFilletIntoNigiri);
				Complete(QuestId.TakeSlicedMackerel);
			}
		}

		public static void Purchased(WholesaleSO product, ItemDatabaseSO db)
		{
			if (!(product == null) && !(db == null))
			{
				if (product == db?.WholesaleCucumber)
				{
					Complete(QuestId.BuyCucumber);
				}
				if (product == db?.WholesaleNori)
				{
					Complete(QuestId.BuyNori);
				}
				if (product == db?.WholesaleRice)
				{
					Complete(QuestId.BuyRice);
				}
				if (product?.OutputItem is SideDishSO)
				{
					Complete(QuestId.BuyASideDish);
				}
			}
		}

		public static void TrayLoaded(ServedDishSO dish, byte slot)
		{
			ItemDatabaseSO itemDatabaseSO = Singleton<GameProgressManager>.Instance?.ItemDatabaseSO;
			if (!(dish == null) && !(itemDatabaseSO == null))
			{
				if (slot == 0 && dish == itemDatabaseSO?.CucumberMakiServeredDishSO)
				{
					Complete(QuestId.PlaceCucumberMakiOnTray);
				}
				if (slot == 0 && dish == itemDatabaseSO?.MackarelNigiriServeredDishSO)
				{
					Complete(QuestId.PlaceMackerelNigiriOnTray);
				}
				if (slot == 1 && dish is SideDishSO)
				{
					Complete(QuestId.TakeSideDishAndAddToTray);
				}
			}
		}
	}
}
