namespace PaperPixelGames.Framework.States.Game
{
	public enum MultiplayerFailureStage : byte
	{
		None = 0,
		SteamHealth = 1,
		LobbyCreate = 2,
		LobbyJoin = 3,
		LaunchPreparation = 4,
		HostStart = 5,
		ClientConnect = 6,
		ConnectionApproval = 7,
		SceneSync = 8,
		Bootstrap = 9,
		Reconnect = 10,
		Session = 11
	}
}
