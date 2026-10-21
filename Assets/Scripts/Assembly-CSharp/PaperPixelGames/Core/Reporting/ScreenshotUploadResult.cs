namespace PaperPixelGames.Core.Reporting
{
	public sealed class ScreenshotUploadResult
	{
		public bool Success;

		public string FileName;

		public string EntityId;

		public string EntityType;

		public string ErrorMessage;

		public static ScreenshotUploadResult Fail(string error)
		{
			return new ScreenshotUploadResult
			{
				Success = false,
				ErrorMessage = error
			};
		}

		public static ScreenshotUploadResult Ok(string fileName, string entityId, string entityType)
		{
			return new ScreenshotUploadResult
			{
				Success = true,
				FileName = fileName,
				EntityId = entityId,
				EntityType = entityType
			};
		}
	}
}
