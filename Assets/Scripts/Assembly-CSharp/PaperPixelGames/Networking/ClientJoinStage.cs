namespace PaperPixelGames.Networking
{
	public enum ClientJoinStage : byte
	{
		Idle = 0,
		TransportConnecting = 1,
		AwaitingApproval = 2,
		SceneSynchronization = 3,
		Connected = 4
	}
}
