using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Progression;

namespace PaperPixelGames.Core.Progress
{
	internal static class ProgressionTooltipLocalization
	{
		public static string GetUnlockDisplayName(ProgressionUnlockKey unlockKey, string fallback)
		{
			return LocalizationDatabase.GetString("UiTable", unlockKey switch
			{
				ProgressionUnlockKey.DiningTable => "progression.unlock.name.dining_table", 
				ProgressionUnlockKey.Conveyor => "progression.unlock.name.conveyor", 
				ProgressionUnlockKey.ConveyorTable => "progression.unlock.name.conveyor_table", 
				ProgressionUnlockKey.BeverageFridge => "tutorial.beverage_fridge.title", 
				ProgressionUnlockKey.FishFridge => "tutorial.fish_fridge.title", 
				ProgressionUnlockKey.SecondPrepStation => "tutorial.prep_station.title", 
				ProgressionUnlockKey.SecondChefDesk => "tutorial.chef_desk.title", 
				ProgressionUnlockKey.SecondTrayTrolley => "progression.unlock.name.second_tray_trolley", 
				_ => string.Empty, 
			}, fallback);
		}

		public static string GetPhysicalUnlockTitle(ProgressionUnlockKey unlockKey, string fallback)
		{
			if (unlockKey == ProgressionUnlockKey.FishFridge || unlockKey == ProgressionUnlockKey.BeverageFridge)
			{
				return LocalizationDatabase.GetString("UiTable", "progression.unlock.title.broken_fridge", fallback);
			}
			return GetUnlockDisplayName(unlockKey, fallback);
		}

		public static string GetReachLevelText(int requiredLevel)
		{
			return LocalizationDatabase.GetFormattedString("UiTable", "progression.unlock.reach_level", "Reach Restaurant Level {0}", requiredLevel);
		}

		public static string GetRequiredLevelText()
		{
			return LocalizationDatabase.GetString("UiTable", "ui.market.warning.required_level", "Reach the required restaurant level first.");
		}

		public static string GetUnlockCostText(int price)
		{
			return LocalizationDatabase.GetFormattedString("UiTable", "progression.unlock.cost", "Unlock Cost: ${0}", price);
		}

		public static string GetRepairCostText(int price)
		{
			return LocalizationDatabase.GetFormattedString("UiTable", "progression.unlock.repair_cost", "Repair Cost: ${0}", price);
		}

		public static string GetUnlockActionText()
		{
			return LocalizationDatabase.GetString("UiTable", "progression.unlock.action", "Unlock");
		}

		public static string GetTitle(string key, string fallback)
		{
			return LocalizationDatabase.GetString("UiTable", key, fallback);
		}

		public static string GetToast(string key, string fallback, params object[] arguments)
		{
			return LocalizationDatabase.GetFormattedString("UiTable", key, fallback, arguments);
		}
	}
}
