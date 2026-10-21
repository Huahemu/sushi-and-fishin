using System;
using PaperPixelGames.Core.DailyReport;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityServiceLocator;

namespace PaperPixelGames.Core.SaveSystem
{
	internal sealed class WorldSaveCollector
	{
		public bool TryPopulateSlot(SaveSlotData slotData, int slotIndex, string hostSteamId, out string error)
		{
			error = string.Empty;
			if (slotData == null)
			{
				error = "Slot data is null.";
				return false;
			}
			GameProgressManager gameProgressManager = Singleton<GameProgressManager>.Instance;
			PlayersManager instance = Singleton<PlayersManager>.Instance;
			PPG_Player localPlayer = ((instance != null) ? instance.LocalPlayer : null);
			RestaurantManager restaurantManager = ((gameProgressManager != null) ? gameProgressManager.RestaurantManager : null);
			DailyReportBroadcaster dailyReport = ((gameProgressManager != null) ? gameProgressManager.DailyReportBroadcaster : null);
			bool hadSectionFailure = false;
			slotData.Version = 4;
			slotData.HostSteamID = hostSteamId;
			slotData.SlotID = slotIndex;
			slotData.PlayerData = SaveSection("PlayerData", () =>
			{
				if (localPlayer == null)
				{
					throw new InvalidOperationException("Local player is null.");
				}
				return localPlayer.Save();
			}, slotData.PlayerData ?? new PlayerData(), ref hadSectionFailure);
			slotData.GameProgress = SaveSection("GameProgress", () =>
			{
				if (gameProgressManager == null)
				{
					throw new InvalidOperationException("GameProgressManager is null.");
				}
				return gameProgressManager.Save();
			}, slotData.GameProgress ?? new GameProgressData(), ref hadSectionFailure);
			slotData.RestaurantData = SaveSection("RestaurantData", () =>
			{
				if (restaurantManager == null)
				{
					throw new InvalidOperationException("RestaurantManager is null.");
				}
				return restaurantManager.Save();
			}, slotData.RestaurantData ?? new RestaurantData(), ref hadSectionFailure);
			slotData.DailyRecord = SaveSection("DailyRecord", () =>
			{
				if (dailyReport == null)
				{
					throw new InvalidOperationException("DailyReportBroadcaster is null.");
				}
				return dailyReport.Save();
			}, slotData.DailyRecord ?? new DailyRecordData(), ref hadSectionFailure);
			slotData.TabletData = SaveSection("TabletData", SaveTabletData, slotData.TabletData ?? new TabletData(), ref hadSectionFailure);
			if (hadSectionFailure)
			{
				UtilityMethods.Log("[SaveManager] Save completed with partial section failures. Previous data was kept for failed sections.", LogType.Warning);
			}
			return true;
		}

		private static T SaveSection<T>(string label, Func<T> saveFunc, T fallback, ref bool hadSectionFailure) where T : class
		{
			try
			{
				T val = saveFunc();
				if (val != null)
				{
					return val;
				}
				hadSectionFailure = true;
				UtilityMethods.Log("[SaveManager] " + label + " save returned null. Keeping previous data.", LogType.Warning);
				return fallback;
			}
			catch (Exception arg)
			{
				hadSectionFailure = true;
				UtilityMethods.Log($"[SaveManager] {label} save failed. Keeping previous data. Exception={arg}", LogType.Error);
				return fallback;
			}
		}

		private static TabletData SaveTabletData()
		{
			if (Singleton<GameProgressManager>.Instance == null)
			{
				UtilityMethods.Log("[SaveManager] SaveTabletData failed. GameProgressManager is null.", LogType.Warning);
				return new TabletData();
			}
			if (!ServiceLocator.ForSceneOf(Singleton<GameProgressManager>.Instance).TryGet<TabletNetworkHandler>(out var service))
			{
				UtilityMethods.Log("[SaveManager] SaveTabletData failed. TabletNetworkHandler not found.", LogType.Warning);
				return new TabletData();
			}
			return service.Save() ?? new TabletData();
		}
	}
}
