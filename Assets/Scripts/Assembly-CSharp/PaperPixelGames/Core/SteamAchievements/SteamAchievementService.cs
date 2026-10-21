using System.Collections.Generic;
using PaperPixelGames.Framework.Utility;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace PaperPixelGames.Core.SteamAchievements
{
	public static class SteamAchievementService
	{
		private static readonly HashSet<string> SessionUnlocked = new HashSet<string>();

		private static readonly HashSet<string> PendingUnlocks = new HashSet<string>();

		private static bool _initialized;

		public static void Initialize()
		{
			if (!_initialized)
			{
				_initialized = true;
				SteamUserStats.OnUserStatsReceived += OnUserStatsReceived;
			}
		}

		public static void Unlock(SteamAchievementId id)
		{
			Unlock(SteamAchievementIds.ToApiName(id));
		}

		public static void Unlock(string achievementApiName)
		{
			if (string.IsNullOrWhiteSpace(achievementApiName) || SessionUnlocked.Contains(achievementApiName))
			{
				return;
			}
			if (!SteamClient.IsValid)
			{
				UtilityMethods.Log("[SteamAchievement] Steam client is not valid. Skipped: " + achievementApiName, LogType.Warning);
				return;
			}
			if (!SteamUserStats.StatsRecieved)
			{
				PendingUnlocks.Add(achievementApiName);
				UtilityMethods.Log("[SteamAchievement] Stats not marked ready yet. Trying unlock anyway: " + achievementApiName, LogType.Warning);
			}
			UnlockNow(achievementApiName);
		}

		private static void OnUserStatsReceived(SteamId steamId, Result result)
		{
			if (SteamClient.IsValid && (ulong)steamId == (ulong)SteamClient.SteamId && result == Result.OK)
			{
				FlushPendingUnlocks();
			}
		}

		private static void FlushPendingUnlocks()
		{
			if (PendingUnlocks.Count != 0)
			{
				string[] array = new string[PendingUnlocks.Count];
				PendingUnlocks.CopyTo(array);
				PendingUnlocks.Clear();
				for (int i = 0; i < array.Length; i++)
				{
					UnlockNow(array[i]);
				}
			}
		}

		private static void UnlockNow(string achievementApiName)
		{
			if (!SessionUnlocked.Contains(achievementApiName))
			{
				Achievement achievement = new Achievement(achievementApiName);
				if (achievement.State)
				{
					SessionUnlocked.Add(achievementApiName);
					return;
				}
				if (!achievement.Trigger())
				{
					UtilityMethods.Log("[SteamAchievement] Unlock failed: " + achievementApiName, LogType.Warning);
					return;
				}
				SteamUserStats.StoreStats();
				SessionUnlocked.Add(achievementApiName);
				UtilityMethods.Log("[SteamAchievement] Unlocked: " + achievementApiName);
			}
		}
	}
}
