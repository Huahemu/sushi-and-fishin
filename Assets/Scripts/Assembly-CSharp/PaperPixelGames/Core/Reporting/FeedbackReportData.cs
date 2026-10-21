using System;

namespace PaperPixelGames.Core.Reporting
{
	[Serializable]
	public class FeedbackReportData
	{
		public FeedbackCategory Category;

		public string Message;

		public byte Rating;
	}
}
