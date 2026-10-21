using System;

namespace PaperPixelGames.Networking
{
	[Serializable]
	public sealed class LobbyMenuData
	{
		public string LobbyName = string.Empty;

		public LobbyMenuData(string name)
		{
			LobbyName = name + " Lobby";
		}
	}
}
