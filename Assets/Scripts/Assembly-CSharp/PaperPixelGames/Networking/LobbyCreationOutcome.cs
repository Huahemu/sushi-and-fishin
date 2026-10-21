using Steamworks;
using Steamworks.Data;

namespace PaperPixelGames.Networking
{
	public readonly struct LobbyCreationOutcome
	{
		public Lobby? Lobby { get; }

		public Result SteamResult { get; }

		public bool Succeeded
		{
			get
			{
				if (SteamResult == Result.OK)
				{
					return Lobby.HasValue;
				}
				return false;
			}
		}

		public LobbyCreationOutcome(Lobby? lobby, Result steamResult)
		{
			Lobby = lobby;
			SteamResult = steamResult;
		}
	}
}
