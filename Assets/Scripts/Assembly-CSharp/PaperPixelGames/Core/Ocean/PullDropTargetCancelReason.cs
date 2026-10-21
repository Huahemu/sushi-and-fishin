namespace PaperPixelGames.Core.Ocean
{
	public enum PullDropTargetCancelReason : byte
	{
		Unknown = 0,
		PrimaryFailed = 1,
		PrimaryCancelled = 2,
		TargetReset = 3
	}
}
