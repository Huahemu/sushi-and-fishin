using System;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public class FishLeaderboardEntryData
	{
		public string PlayerName;

		public float BestWeightKg;

		public ulong SteamID;
	}
}
