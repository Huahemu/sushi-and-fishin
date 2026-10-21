namespace PaperPixelGames.Networking
{
	public enum MultiplayerSessionPhase : byte
	{
		MenuWaiting = 0,
		PreparingLaunch = 1,
		HostStarting = 2,
		ClientConnecting = 3,
		SceneSync = 4,
		HostSceneReady = 5,
		InitialBootstrap = 6,
		Running = 7,
		Reconnecting = 8,
		ReturningToMenu = 9
	}
}
