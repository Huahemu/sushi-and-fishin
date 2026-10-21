using UnityEngine;

namespace PaperPixelGames.Networking
{
	public sealed class UnityMultiplayerClock : IMultiplayerClock
	{
		public long UtcNowUnixSeconds => LobbySessionMetadata.UtcNowUnixSeconds;

		public float RealtimeSeconds => Time.realtimeSinceStartup;
	}
}
