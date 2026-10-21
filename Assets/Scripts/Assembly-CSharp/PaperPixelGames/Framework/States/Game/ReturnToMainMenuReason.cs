namespace PaperPixelGames.Framework.States.Game
{
	public enum ReturnToMainMenuReason
	{
		UserRequested = 0,
		StartupFailure = 1,
		LocalClientDisconnected = 2,
		HostDisconnected = 3,
		ConnectionFailed = 4,
		LeaveForeignLobby = 5,
		JoinRequested = 6
	}
}
