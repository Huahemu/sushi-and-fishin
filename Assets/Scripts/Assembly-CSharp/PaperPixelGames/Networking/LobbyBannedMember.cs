using System;

namespace PaperPixelGames.Networking
{
	[Serializable]
	public sealed class LobbyBannedMember
	{
		public ulong SteamId;

		public string Name;
	}
}
