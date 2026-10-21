using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PaperPixelGames.Core.Progression;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "ProgressionLevelDataSO", menuName = "Data/Progression/Level Data")]
	public class ProgressionLevelDataSO : ScriptableObject
	{
		[Serializable]
		public class UnlockEntry
		{
			public ProgressionUnlockKey UnlockKey;

			public string DisplayName;
		}

		[Serializable]
		public class LevelEntry
		{
			[Min(1f)]
			public int Level = 1;

			[Min(0f)]
			public int RequiredTotalXp;

			public List<UnlockEntry> Unlocks = new List<UnlockEntry>();

			[Min(0f)]
			public int DiningTableUnlockCount;

			[SerializeField]
			[HideInInspector]
			private List<int> DiningTableUnlockIds = new List<int>();

			public List<int> ConveyorTableUnlockIds = new List<int>();

			public List<WholesaleSO> WholesaleUnlocks = new List<WholesaleSO>();

			public int GetDiningTableUnlockCount()
			{
				if (DiningTableUnlockCount > 0)
				{
					return DiningTableUnlockCount;
				}
				if (DiningTableUnlockIds == null)
				{
					return 0;
				}
				return DiningTableUnlockIds.Count((int x) => x >= 0);
			}

			public bool HasLegacyDiningTableUnlockId(int tableId)
			{
				if (DiningTableUnlockIds != null)
				{
					return DiningTableUnlockIds.Contains(tableId);
				}
				return false;
			}
		}

		private const int MaxGeneratedLevel = 500;

		private const double GeneratedThresholdMultiplier = 1.25;

		[SerializeField]
		private List<LevelEntry> _levels = new List<LevelEntry>();

		public int GetLevelForTotalXp(double totalXp)
		{
			totalXp = Math.Max(0.0, totalXp);
			int num = 1;
			int num2 = 1;
			double num3 = 0.0;
			foreach (LevelEntry orderedLevel in GetOrderedLevels())
			{
				if (orderedLevel != null)
				{
					int num4 = Mathf.Max(1, orderedLevel.Level);
					double num5 = Math.Max(0.0, orderedLevel.RequiredTotalXp);
					if (num4 >= num2)
					{
						num2 = num4;
						num3 = num5;
					}
					if (totalXp >= num5)
					{
						num = num4;
					}
				}
			}
			if (num2 >= 500)
			{
				return Mathf.Min(num, 500);
			}
			double num6 = num3;
			for (int i = num2 + 1; i <= 500; i++)
			{
				num6 = Math.Ceiling(num6 * 1.25);
				if (totalXp < num6)
				{
					break;
				}
				num = i;
			}
			return Mathf.Clamp(num, 1, 500);
		}

		public double GetRequiredTotalXpForLevel(int level)
		{
			level = Mathf.Clamp(level, 1, 500);
			LevelEntry levelEntry = GetOrderedLevels().LastOrDefault((LevelEntry x) => x != null && x.Level == level);
			if (levelEntry != null)
			{
				return Math.Max(0.0, levelEntry.RequiredTotalXp);
			}
			if (!TryGetHighestAuthoredLevel(out var highestLevel, out var requiredTotalXp))
			{
				return 0.0;
			}
			if (level <= highestLevel)
			{
				return 0.0;
			}
			return CalculateGeneratedRequiredTotalXp(highestLevel, requiredTotalXp, level);
		}

		public double GetRequiredTotalXpForNextLevel(int currentLevel)
		{
			currentLevel = Mathf.Max(1, currentLevel);
			if (currentLevel >= 500)
			{
				return -1.0;
			}
			LevelEntry levelEntry = GetOrderedLevels().FirstOrDefault((LevelEntry x) => x != null && x.Level > currentLevel);
			if (levelEntry != null)
			{
				return Math.Max(0.0, levelEntry.RequiredTotalXp);
			}
			if (!TryGetHighestAuthoredLevel(out var highestLevel, out var requiredTotalXp))
			{
				return -1.0;
			}
			int num = currentLevel + 1;
			if (num <= highestLevel)
			{
				return -1.0;
			}
			return CalculateGeneratedRequiredTotalXp(highestLevel, requiredTotalXp, num);
		}

		public HashSet<ProgressionUnlockKey> BuildUnlockSet(int level)
		{
			HashSet<ProgressionUnlockKey> hashSet = new HashSet<ProgressionUnlockKey>();
			level = Mathf.Max(1, level);
			foreach (LevelEntry orderedLevel in GetOrderedLevels())
			{
				if (orderedLevel == null || orderedLevel.Level > level || orderedLevel.Unlocks == null)
				{
					continue;
				}
				foreach (UnlockEntry unlock in orderedLevel.Unlocks)
				{
					if (unlock != null && unlock.UnlockKey != ProgressionUnlockKey.None)
					{
						hashSet.Add(unlock.UnlockKey);
					}
				}
			}
			return hashSet;
		}

		public HashSet<int> BuildDiningTableUnlockSet(int level)
		{
			HashSet<int> hashSet = new HashSet<int>();
			level = Mathf.Max(1, level);
			int num = 0;
			foreach (LevelEntry orderedLevel in GetOrderedLevels())
			{
				if (orderedLevel != null && orderedLevel.Level <= level)
				{
					num += Mathf.Max(0, orderedLevel.GetDiningTableUnlockCount());
				}
			}
			for (int i = 1; i <= num; i++)
			{
				hashSet.Add(i);
			}
			return hashSet;
		}

		public int GetAllowedDiningTableUnlockCount(int level)
		{
			int num = 0;
			level = Mathf.Max(1, level);
			foreach (LevelEntry orderedLevel in GetOrderedLevels())
			{
				if (orderedLevel != null && orderedLevel.Level <= level)
				{
					num += Mathf.Max(0, orderedLevel.GetDiningTableUnlockCount());
				}
			}
			return num;
		}

		public int GetRequiredLevelForDiningTableUnlockCount(int targetUnlockedCount)
		{
			if (targetUnlockedCount <= 0)
			{
				return 1;
			}
			foreach (LevelEntry orderedLevel in GetOrderedLevels())
			{
				if (orderedLevel != null && GetAllowedDiningTableUnlockCount(orderedLevel.Level) >= targetUnlockedCount)
				{
					return Mathf.Max(1, orderedLevel.Level);
				}
			}
			return -1;
		}

		public HashSet<int> BuildConveyorTableUnlockSet(int level)
		{
			HashSet<int> hashSet = new HashSet<int>();
			level = Mathf.Max(1, level);
			foreach (LevelEntry orderedLevel in GetOrderedLevels())
			{
				if (orderedLevel == null || orderedLevel.Level > level || orderedLevel.ConveyorTableUnlockIds == null)
				{
					continue;
				}
				foreach (int conveyorTableUnlockId in orderedLevel.ConveyorTableUnlockIds)
				{
					if (conveyorTableUnlockId >= 0)
					{
						hashSet.Add(conveyorTableUnlockId);
					}
				}
			}
			return hashSet;
		}

		public HashSet<string> BuildWholesaleUnlockSet(int level)
		{
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			level = Mathf.Max(1, level);
			foreach (LevelEntry orderedLevel in GetOrderedLevels())
			{
				if (orderedLevel == null || orderedLevel.Level > level || orderedLevel.WholesaleUnlocks == null)
				{
					continue;
				}
				foreach (WholesaleSO wholesaleUnlock in orderedLevel.WholesaleUnlocks)
				{
					if (wholesaleUnlock != null && !string.IsNullOrWhiteSpace(wholesaleUnlock.Id))
					{
						hashSet.Add(wholesaleUnlock.Id);
					}
				}
			}
			return hashSet;
		}

		public string GetUnlockDisplayName(ProgressionUnlockKey unlockKey)
		{
			foreach (LevelEntry orderedLevel in GetOrderedLevels())
			{
				if (orderedLevel?.Unlocks == null)
				{
					continue;
				}
				foreach (UnlockEntry unlock in orderedLevel.Unlocks)
				{
					if (unlock != null && unlock.UnlockKey == unlockKey)
					{
						return string.IsNullOrWhiteSpace(unlock.DisplayName) ? GetReadableIdentifier(unlockKey.ToString()) : unlock.DisplayName;
					}
				}
			}
			return GetReadableIdentifier(unlockKey.ToString());
		}

		public int GetRequiredLevelForUnlock(ProgressionUnlockKey unlockKey)
		{
			if (unlockKey == ProgressionUnlockKey.None)
			{
				return 1;
			}
			foreach (LevelEntry orderedLevel in GetOrderedLevels())
			{
				if (orderedLevel?.Unlocks == null)
				{
					continue;
				}
				foreach (UnlockEntry unlock in orderedLevel.Unlocks)
				{
					if (unlock != null && unlock.UnlockKey == unlockKey)
					{
						return Mathf.Max(1, orderedLevel.Level);
					}
				}
			}
			return -1;
		}

		public int GetRequiredLevelForDiningTableUnlock(int tableId)
		{
			if (tableId < 0)
			{
				return -1;
			}
			foreach (LevelEntry orderedLevel in GetOrderedLevels())
			{
				if (orderedLevel != null && orderedLevel.HasLegacyDiningTableUnlockId(tableId))
				{
					return Mathf.Max(1, orderedLevel.Level);
				}
			}
			return -1;
		}

		public int GetRequiredLevelForConveyorTableUnlock(int tableId)
		{
			if (tableId < 0)
			{
				return -1;
			}
			foreach (LevelEntry orderedLevel in GetOrderedLevels())
			{
				if (orderedLevel?.ConveyorTableUnlockIds != null && orderedLevel.ConveyorTableUnlockIds.Contains(tableId))
				{
					return Mathf.Max(1, orderedLevel.Level);
				}
			}
			return -1;
		}

		public int GetRequiredLevelForWholesaleUnlock(string wholesaleId)
		{
			if (string.IsNullOrWhiteSpace(wholesaleId))
			{
				return -1;
			}
			foreach (LevelEntry orderedLevel in GetOrderedLevels())
			{
				if (orderedLevel?.WholesaleUnlocks == null)
				{
					continue;
				}
				foreach (WholesaleSO wholesaleUnlock in orderedLevel.WholesaleUnlocks)
				{
					if (wholesaleUnlock != null && string.Equals(wholesaleUnlock.Id, wholesaleId, StringComparison.Ordinal))
					{
						return Mathf.Max(1, orderedLevel.Level);
					}
				}
			}
			return -1;
		}

		public string GetWholesaleDisplayName(string wholesaleId)
		{
			if (string.IsNullOrWhiteSpace(wholesaleId))
			{
				return string.Empty;
			}
			foreach (LevelEntry orderedLevel in GetOrderedLevels())
			{
				if (orderedLevel?.WholesaleUnlocks == null)
				{
					continue;
				}
				foreach (WholesaleSO wholesaleUnlock in orderedLevel.WholesaleUnlocks)
				{
					if (wholesaleUnlock != null && string.Equals(wholesaleUnlock.Id, wholesaleId, StringComparison.Ordinal))
					{
						return GetItemDisplayName(wholesaleUnlock, wholesaleId);
					}
				}
			}
			return wholesaleId;
		}

		private IEnumerable<LevelEntry> GetOrderedLevels()
		{
			if (_levels == null)
			{
				return Enumerable.Empty<LevelEntry>();
			}
			return from x in _levels
				where x != null
				orderby x.Level, x.RequiredTotalXp
				select x;
		}

		private bool TryGetHighestAuthoredLevel(out int highestLevel, out double requiredTotalXp)
		{
			LevelEntry levelEntry = GetOrderedLevels().LastOrDefault();
			if (levelEntry == null)
			{
				highestLevel = 1;
				requiredTotalXp = 0.0;
				return false;
			}
			highestLevel = Mathf.Max(1, levelEntry.Level);
			requiredTotalXp = Math.Max(0.0, levelEntry.RequiredTotalXp);
			return true;
		}

		private static double CalculateGeneratedRequiredTotalXp(int highestAuthoredLevel, double highestAuthoredRequiredTotalXp, int targetLevel)
		{
			targetLevel = Mathf.Clamp(targetLevel, 1, 500);
			if (targetLevel <= highestAuthoredLevel)
			{
				return highestAuthoredRequiredTotalXp;
			}
			double num = highestAuthoredRequiredTotalXp;
			for (int i = highestAuthoredLevel + 1; i <= targetLevel; i++)
			{
				num = Math.Ceiling(num * 1.25);
			}
			return num;
		}

		private static string GetItemDisplayName(ItemSO item, string fallbackId)
		{
			if (item == null)
			{
				return fallbackId;
			}
			if (!string.IsNullOrWhiteSpace(item.LocalizedName))
			{
				return item.LocalizedName;
			}
			if (!string.IsNullOrWhiteSpace(item.name))
			{
				return item.name;
			}
			return fallbackId;
		}

		private static string GetReadableIdentifier(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return string.Empty;
			}
			StringBuilder stringBuilder = new StringBuilder(value.Length + 8);
			for (int i = 0; i < value.Length; i++)
			{
				char c = value[i];
				if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(value[i - 1]))
				{
					stringBuilder.Append(' ');
				}
				stringBuilder.Append(c);
			}
			return stringBuilder.ToString();
		}
	}
}
