using System;

namespace PaperPixelGames.Core.DailyReport
{
	[Serializable]
	public sealed class BiggestCatchPayload
	{
		public string FishId;

		public float Kg;

		public string PlayerName;
	}
}
