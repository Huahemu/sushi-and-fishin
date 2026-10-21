namespace PaperPixelGames.Networking
{
	public static class LobbyModerationPolicy
	{
		public static bool CanProcessKick(bool isServer, ulong senderClientId, ulong targetClientId, ulong serverClientId)
		{
			if (isServer && senderClientId == serverClientId)
			{
				return targetClientId != serverClientId;
			}
			return false;
		}
	}
}
