namespace PaperPixelGames.Networking
{
	public enum TransportSendOutcome
	{
		Sent = 0,
		Queued = 1,
		Dropped = 2,
		Inactive = 3,
		Failed = 4
	}
}
