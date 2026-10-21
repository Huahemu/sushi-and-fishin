using System;

namespace PaperPixelGames.Core.DailyReport
{
	[Serializable]
	public sealed class DailyLogReportPayload
	{
		public CommonReportPayload Common;

		public PersonalReportPayload Personal;
	}
}
