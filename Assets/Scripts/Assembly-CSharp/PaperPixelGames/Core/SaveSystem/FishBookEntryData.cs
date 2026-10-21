using System;
using System.Collections.Generic;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public class FishBookEntryData
	{
		public string FishID;

		public bool IsUnlocked;

		public int TotalCaughtAmount;

		public List<FishLeaderboardEntryData> LeaderboardEntries = new List<FishLeaderboardEntryData>();
	}
}
