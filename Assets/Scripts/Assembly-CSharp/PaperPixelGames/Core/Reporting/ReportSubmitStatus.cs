namespace PaperPixelGames.Core.Reporting
{
	public enum ReportSubmitStatus : byte
	{
		Success = 0,
		ValidationFailed = 1,
		NotAuthenticated = 2,
		ServiceMissing = 3,
		AlreadySubmitting = 4,
		CooldownActive = 5,
		Failed = 6
	}
}
