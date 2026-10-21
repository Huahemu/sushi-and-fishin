using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Framework.Utility;

namespace PaperPixelGames.Core.SaveSystem
{
	public static class SaveMigrations
	{
		public static SaveSlotData MigrateToCurrent(SaveSlotData data)
		{
			if (data == null)
			{
				data = SaveSlotData.CreateNew();
			}
			int version = data.Version;
			SaveSlotData saveSlotData = data;
			if (saveSlotData.PlayerData == null)
			{
				saveSlotData.PlayerData = new PlayerData();
			}
			PlayerData playerData = data.PlayerData;
			if (playerData.TabletData == null)
			{
				playerData.TabletData = new TabletData();
			}
			playerData = data.PlayerData;
			if (playerData.QuickSlotData == null)
			{
				playerData.QuickSlotData = new QuickSlotSaveData();
			}
			playerData = data.PlayerData;
			if (playerData.ToolData == null)
			{
				playerData.ToolData = new ToolData();
			}
			MigrateToolData(data.PlayerData.ToolData, version);
			NormalizeQuickSlotData(data.PlayerData.QuickSlotData);
			NormalizeTabletData(data.PlayerData.TabletData);
			saveSlotData = data;
			if (saveSlotData.GameProgress == null)
			{
				saveSlotData.GameProgress = new GameProgressData();
			}
			GameProgressData gameProgress = data.GameProgress;
			if (gameProgress.QuestData == null)
			{
				gameProgress.QuestData = new QuestSaveData();
			}
			gameProgress = data.GameProgress;
			if (gameProgress.AnalyticsMilestones == null)
			{
				gameProgress.AnalyticsMilestones = new AnalyticsMilestoneData();
			}
			NormalizeQuestData(data.GameProgress.QuestData);
			BossProgressResetMigration.Apply(data, version);
			RestoreCompletedBossReward(data);
			saveSlotData = data;
			if (saveSlotData.RestaurantData == null)
			{
				saveSlotData.RestaurantData = new RestaurantData();
			}
			NormalizeRestaurantData(data.RestaurantData);
			saveSlotData = data;
			if (saveSlotData.DailyRecord == null)
			{
				saveSlotData.DailyRecord = new DailyRecordData();
			}
			saveSlotData = data;
			if (saveSlotData.TabletData == null)
			{
				saveSlotData.TabletData = new TabletData();
			}
			NormalizeTabletData(data.TabletData);
			saveSlotData = data;
			if (saveSlotData.HostSteamID == null)
			{
				saveSlotData.HostSteamID = string.Empty;
			}
			data.Version = 4;
			return data;
		}

		private static void RestoreCompletedBossReward(SaveSlotData data)
		{
			QuestSaveData questData = data.GameProgress.QuestData;
			if (questData.CompletedQuestIds.Contains("0d6b27f7cf374bad9fe13439a8989589") || questData.CompletedGroupIds.Contains("aca623a37aaa42f0813a5a7913b64348"))
			{
				data.PlayerData.ToolData.PoseidonTridentUnlocked = true;
			}
		}

		private static void MigrateToolData(ToolData data, int sourceVersion)
		{
			if (data != null)
			{
				if (sourceVersion < 2 && data.FishingRodUnlocked && data.FishingRodLevel > 0)
				{
					byte fishingRodLevel = data.FishingRodLevel;
					data.FishingRodLevel--;
					UtilityMethods.Log("[SaveMigrations] Fishing rod level converted to zero-based index. " + $"LegacyLevel={fishingRodLevel} LevelIndex={data.FishingRodLevel}");
				}
				data.FishingRodLevel = (byte)Math.Clamp((int)data.FishingRodLevel, 0, 4);
			}
		}

		private static void NormalizeRestaurantData(RestaurantData data)
		{
			if (data == null)
			{
				return;
			}
			RestaurantData restaurantData = data;
			if (restaurantData.RestaurantUpgradeData == null)
			{
				restaurantData.RestaurantUpgradeData = new RestaurantUpgradeData();
			}
			RestaurantUpgradeData restaurantUpgradeData = data.RestaurantUpgradeData;
			if (restaurantUpgradeData.UnlockedTableIds == null)
			{
				restaurantUpgradeData.UnlockedTableIds = new List<byte>();
			}
			restaurantUpgradeData = data.RestaurantUpgradeData;
			if (restaurantUpgradeData.ClosedDiningTableIds == null)
			{
				restaurantUpgradeData.ClosedDiningTableIds = new List<byte>();
			}
			restaurantUpgradeData = data.RestaurantUpgradeData;
			if (restaurantUpgradeData.UnlockedConveyorBeltTableIds == null)
			{
				restaurantUpgradeData.UnlockedConveyorBeltTableIds = new List<byte>();
			}
			restaurantUpgradeData = data.RestaurantUpgradeData;
			if (restaurantUpgradeData.UnlockedTrayTrolleyIndexes == null)
			{
				restaurantUpgradeData.UnlockedTrayTrolleyIndexes = new List<byte>();
			}
			restaurantUpgradeData = data.RestaurantUpgradeData;
			if (restaurantUpgradeData.UnlockedChefDeskIndexes == null)
			{
				restaurantUpgradeData.UnlockedChefDeskIndexes = new List<byte>();
			}
			restaurantUpgradeData = data.RestaurantUpgradeData;
			if (restaurantUpgradeData.UnlockedPrepStationIndexes == null)
			{
				restaurantUpgradeData.UnlockedPrepStationIndexes = new List<byte>();
			}
			restaurantData = data;
			if (restaurantData.GastropanData == null)
			{
				restaurantData.GastropanData = new List<GastropanData>();
			}
			restaurantData = data;
			if (restaurantData.ChefDeskGastropanData == null)
			{
				restaurantData.ChefDeskGastropanData = new List<ChefDeskGastropanData>();
			}
			restaurantData = data;
			if (restaurantData.IngredientData == null)
			{
				restaurantData.IngredientData = new List<IngredientData>();
			}
			restaurantData = data;
			if (restaurantData.WholesaleData == null)
			{
				restaurantData.WholesaleData = new List<WholesaleData>();
			}
			restaurantData = data;
			if (restaurantData.DeadFishData == null)
			{
				restaurantData.DeadFishData = new List<DeadFishData>();
			}
			restaurantData = data;
			if (restaurantData.BeverageData == null)
			{
				restaurantData.BeverageData = new List<byte>();
			}
			restaurantData = data;
			if (restaurantData.RestaurantSignCustomizationData == null)
			{
				restaurantData.RestaurantSignCustomizationData = new RestaurantSignCustomizationData();
			}
			restaurantData = data;
			if (restaurantData.RestaurantDirtData == null)
			{
				restaurantData.RestaurantDirtData = new RestaurantDirtData();
			}
			restaurantData = data;
			if (restaurantData.ServedDishData == null)
			{
				restaurantData.ServedDishData = new List<ServedDishData>();
			}
			restaurantData = data;
			if (restaurantData.CustomSushiRecipes == null)
			{
				restaurantData.CustomSushiRecipes = new List<CustomSushiRecipeData>();
			}
			data.NextCustomSushiRecipeSequence = Math.Max(1, data.NextCustomSushiRecipeSequence);
			restaurantData = data;
			if (restaurantData.FridgeData == null)
			{
				restaurantData.FridgeData = new FridgeData();
			}
			FridgeData fridgeData = data.FridgeData;
			if (fridgeData.FridgeFishes == null)
			{
				fridgeData.FridgeFishes = new List<FridgeFishData>();
			}
			restaurantData = data;
			if (restaurantData.RadioData == null)
			{
				restaurantData.RadioData = new RadioData();
			}
			restaurantData = data;
			if (restaurantData.FishTrophyData == null)
			{
				restaurantData.FishTrophyData = new FishTrophyData();
			}
			FishTrophyData fishTrophyData = data.FishTrophyData;
			if (fishTrophyData.RecordFishId == null)
			{
				fishTrophyData.RecordFishId = string.Empty;
			}
			fishTrophyData = data.FishTrophyData;
			if (fishTrophyData.AudioBytes == null)
			{
				fishTrophyData.AudioBytes = Array.Empty<byte>();
			}
			foreach (ServedDishData servedDishDatum in data.ServedDishData)
			{
				if (servedDishDatum == null)
				{
					continue;
				}
				ServedDishData servedDishData = servedDishDatum;
				if (servedDishData.TransformData == null)
				{
					servedDishData.TransformData = new TransformData();
				}
				if (servedDishDatum.HasTrayAttachment)
				{
					servedDishDatum.HasConveyorAttachment = false;
					servedDishDatum.ConveyorSlotIndex = -1;
					continue;
				}
				servedDishDatum.TrayTrolleyIndex = -1;
				servedDishDatum.TraySlotIndex = -1;
				if (!servedDishDatum.HasConveyorAttachment)
				{
					servedDishDatum.ConveyorSlotIndex = -1;
				}
			}
			foreach (FridgeFishData fridgeFish in data.FridgeData.FridgeFishes)
			{
				if (fridgeFish != null)
				{
					FridgeFishData fridgeFishData = fridgeFish;
					if (fridgeFishData.Weights == null)
					{
						fridgeFishData.Weights = new List<float>();
					}
				}
			}
		}

		private static void NormalizeTabletData(TabletData data)
		{
			if (data != null)
			{
				TabletData tabletData = data;
				if (tabletData.WallpaperName == null)
				{
					tabletData.WallpaperName = string.Empty;
				}
				tabletData = data;
				if (tabletData.WallpaperData == null)
				{
					tabletData.WallpaperData = new List<DefaultWallpaperData>();
				}
				tabletData = data;
				if (tabletData.MenuItems == null)
				{
					tabletData.MenuItems = new List<string>();
				}
				tabletData = data;
				if (tabletData.DailySushiSpecials == null)
				{
					tabletData.DailySushiSpecials = new List<DailySushiSpecialEntryData>();
				}
				tabletData = data;
				if (tabletData.FishBookEntries == null)
				{
					tabletData.FishBookEntries = new List<FishBookEntryData>();
				}
				tabletData = data;
				if (tabletData.FishPersonalBestEntries == null)
				{
					tabletData.FishPersonalBestEntries = new List<FishPersonalBestEntryData>();
				}
				tabletData = data;
				if (tabletData.DiscoveredSeaLayerIndices == null)
				{
					tabletData.DiscoveredSeaLayerIndices = new List<byte>();
				}
				tabletData = data;
				if (tabletData.StaffData == null)
				{
					tabletData.StaffData = new StaffData();
				}
				StaffData staffData = data.StaffData;
				if (staffData.Candidates == null)
				{
					staffData.Candidates = new List<StaffEmployeeData>();
				}
				staffData = data.StaffData;
				if (staffData.Team == null)
				{
					staffData.Team = new List<StaffEmployeeData>();
				}
			}
		}

		private static void NormalizeQuestData(QuestSaveData data)
		{
			if (data != null)
			{
				QuestSaveData questSaveData = data;
				if (questSaveData.ActiveSideGroupIds == null)
				{
					questSaveData.ActiveSideGroupIds = new List<string>();
				}
				questSaveData = data;
				if (questSaveData.CompletedGroupIds == null)
				{
					questSaveData.CompletedGroupIds = new List<string>();
				}
				questSaveData = data;
				if (questSaveData.CompletedQuestIds == null)
				{
					questSaveData.CompletedQuestIds = new List<string>();
				}
				questSaveData = data;
				if (questSaveData.QuestProgresses == null)
				{
					questSaveData.QuestProgresses = new List<QuestProgressSaveEntry>();
				}
			}
		}

		private static void NormalizeQuickSlotData(QuickSlotSaveData data)
		{
			if (data == null)
			{
				return;
			}
			QuickSlotSaveData quickSlotSaveData = data;
			if (quickSlotSaveData.Held == null)
			{
				quickSlotSaveData.Held = HeldSlotSaveData.Empty;
			}
			HeldSlotSaveData held = data.Held;
			if (held.Item == null)
			{
				held.Item = new SlotItemData();
			}
			quickSlotSaveData = data;
			if (quickSlotSaveData.Slots == null)
			{
				quickSlotSaveData.Slots = new List<QuickSlotStackSaveData>();
			}
			for (int i = 0; i < data.Slots.Count; i++)
			{
				QuickSlotStackSaveData quickSlotStackSaveData = data.Slots[i];
				if (quickSlotStackSaveData == null)
				{
					quickSlotStackSaveData = QuickSlotStackSaveData.Empty;
					quickSlotStackSaveData.Index = i;
					data.Slots[i] = quickSlotStackSaveData;
				}
				QuickSlotStackSaveData quickSlotStackSaveData2 = quickSlotStackSaveData;
				if (quickSlotStackSaveData2.Items == null)
				{
					quickSlotStackSaveData2.Items = new List<SlotItemData>();
				}
				for (int j = 0; j < quickSlotStackSaveData.Items.Count; j++)
				{
					List<SlotItemData> items = quickSlotStackSaveData.Items;
					int index = j;
					if (items[index] == null)
					{
						SlotItemData slotItemData = (items[index] = new SlotItemData());
					}
				}
			}
		}
	}
}
