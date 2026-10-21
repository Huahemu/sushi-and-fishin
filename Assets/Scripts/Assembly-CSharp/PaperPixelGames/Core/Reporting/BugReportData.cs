using System;

namespace PaperPixelGames.Core.Reporting
{
	[Serializable]
	public sealed class BugReportData
	{
		public FeedbackCategory Category;

		public string Message;

		public byte Rating;

		public bool IncludeScreenshot;

		public bool IncludeLogs;

		public bool HasScreenshot;

		public string ScreenshotFileName;

		public string ScreenshotEntityType;

		public string ScreenshotEntityId;
	}
}
