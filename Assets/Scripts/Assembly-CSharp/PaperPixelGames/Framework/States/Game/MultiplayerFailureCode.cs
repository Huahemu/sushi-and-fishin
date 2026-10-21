namespace PaperPixelGames.Framework.States.Game
{
	public enum MultiplayerFailureCode : byte
	{
		None = 0,
		SteamUnavailable = 1,
		LobbyCreateFailed = 2,
		LobbyJoinFailed = 3,
		LobbyFull = 4,
		HostUnavailable = 5,
		HostStartFailed = 6,
		ClientStartFailed = 7,
		ConnectionTimedOut = 8,
		VersionMismatch = 9,
		ProtocolMismatch = 10,
		IdentityMismatch = 11,
		NotAuthorized = 12,
		Banned = 13,
		SessionExpired = 14,
		SessionChanged = 15,
		LaunchPreparationFailed = 16,
		BootstrapFailed = 17,
		TransportFailure = 18,
		ReconnectTimedOut = 19,
		Unknown = byte.MaxValue
	}
}
