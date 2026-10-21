namespace PaperPixelGames.Networking
{
	public interface IMultiplayerClock
	{
		long UtcNowUnixSeconds { get; }

		float RealtimeSeconds { get; }
	}
}
