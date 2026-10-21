namespace PaperPixelGames.Core.Player
{
	public interface IPlayerTrapRecoveryOwner
	{
		bool ServerTryReleasePlayerForRecovery(PPG_Player player, string reason);
	}
}
