namespace PaperPixelGames.Core.Reporting
{
	public enum ReportSubmitFailureReason : byte
	{
		None = 0,
		Authentication = 1,
		Timeout = 2,
		Cancelled = 3,
		Network = 4,
		Unknown = 5
	}
}
