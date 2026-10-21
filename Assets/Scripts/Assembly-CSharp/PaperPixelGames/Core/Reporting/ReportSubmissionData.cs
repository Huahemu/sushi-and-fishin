using System;

namespace PaperPixelGames.Core.Reporting
{
	[Serializable]
	public sealed class ReportSubmissionData
	{
		public ReportSubmissionType Type;

		public FeedbackCategory Category;

		public string Message;

		public byte Rating;

		public bool IncludeScreenshot;

		public bool HasScreenshot;

		public string ScreenshotFileName;

		public string ScreenshotEntityType;

		public string ScreenshotEntityId;

		public string ScreenshotError;

		public int ScreenshotByteSize;

		public string BuildVersion;

		public string Platform;

		public string PlayFabId;

		public string EntityId;

		public string EntityType;

		public string ReportId;
	}
}
