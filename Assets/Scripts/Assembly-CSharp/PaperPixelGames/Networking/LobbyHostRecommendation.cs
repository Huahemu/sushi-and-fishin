using Steamworks;

namespace PaperPixelGames.Networking
{
	public readonly struct LobbyHostRecommendation
	{
		public ulong LobbyId { get; }

		public SteamId RecommendedHostId { get; }

		public int WorstPingMs { get; }

		public int AveragePingMs { get; }

		public int StableFps { get; }

		public LobbyHostRecommendation(ulong lobbyId, SteamId recommendedHostId, int worstPingMs, int averagePingMs, int stableFps)
		{
			LobbyId = lobbyId;
			RecommendedHostId = recommendedHostId;
			WorstPingMs = worstPingMs;
			AveragePingMs = averagePingMs;
			StableFps = stableFps;
		}
	}
}
