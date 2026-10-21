using System;

namespace PaperPixelGames.Core.DailyReport
{
	[Serializable]
	public sealed class MvpPayload
	{
		public ulong SteamId;

		public string PlayerName;

		public int Score;
	}
}
