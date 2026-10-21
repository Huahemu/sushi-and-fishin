namespace PaperPixelGames.Networking
{
	public enum TransportSendFailure
	{
		None = 0,
		InvalidPayload = 1,
		MessageTooLarge = 2,
		PeerCapacity = 3,
		TotalCapacity = 4,
		QueueExpired = 5,
		NativeError = 6
	}
}
