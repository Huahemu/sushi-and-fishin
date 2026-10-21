using System;
using System.Collections.Generic;

namespace PaperPixelGames.Core.DailyReport
{
	[Serializable]
	public sealed class PersonalReportPayload
	{
		public ulong SteamId;

		public string PlayerName;

		public int Earnings;

		public int CustomersServed;

		public int SushiPrepared;

		public int FishCaught;

		public BiggestCatchPayload BiggestCatch;

		public float DeepestDepthMeters;

		public int DrownCount;

		public List<ushort> AwardIds = new List<ushort>();

		public int MvpScore;
	}
}
