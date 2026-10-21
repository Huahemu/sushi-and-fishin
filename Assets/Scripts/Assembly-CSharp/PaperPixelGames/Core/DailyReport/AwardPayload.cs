using System;

namespace PaperPixelGames.Core.DailyReport
{
	[Serializable]
	public sealed class AwardPayload
	{
		public ushort AwardId;

		public ulong WinnerSteamId;
	}
}
