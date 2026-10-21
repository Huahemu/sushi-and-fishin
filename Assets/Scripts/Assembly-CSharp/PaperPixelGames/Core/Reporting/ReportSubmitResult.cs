namespace PaperPixelGames.Core.Reporting
{
	public struct ReportSubmitResult
	{
		public ReportSubmitStatus Status;

		public string Message;

		public ReportSubmitFailureReason FailureReason;

		public bool IsSuccess => Status == ReportSubmitStatus.Success;

		public bool IsAuthenticationFailure => FailureReason == ReportSubmitFailureReason.Authentication;

		public ReportSubmitResult(ReportSubmitStatus status, string message, ReportSubmitFailureReason failureReason = ReportSubmitFailureReason.None)
		{
			Status = status;
			Message = message;
			FailureReason = failureReason;
		}
	}
}
