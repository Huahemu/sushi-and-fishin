using System;

namespace PaperPixelGames.Networking
{
	[Serializable]
	public sealed class LobbyLaunchMessage
	{
		public const int CurrentVersion = 1;

		public int ProtocolVersion = 1;

		public LobbyLaunchMessageKind Kind;

		public ulong Epoch;

		public ulong HostSteamId;

		public int GameVersion;

		public string ReleaseVersion = string.Empty;

		public long DeadlineUnixSeconds;
	}
}
