using Steamworks;
using Steamworks.Data;

namespace PaperPixelGames.Networking
{
	public readonly struct LobbyJoinOutcome
	{
		public Lobby? Lobby { get; }

		public RoomEnter RoomEnter { get; }

		public bool TimedOut { get; }

		public bool Succeeded
		{
			get
			{
				if (!TimedOut && RoomEnter == RoomEnter.Success)
				{
					return Lobby.HasValue;
				}
				return false;
			}
		}

		public LobbyJoinOutcome(Lobby? lobby, RoomEnter roomEnter, bool timedOut = false)
		{
			Lobby = lobby;
			RoomEnter = roomEnter;
			TimedOut = timedOut;
		}
	}
}
