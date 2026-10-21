using System;

namespace PaperPixelGames.Networking
{
	[Serializable]
	public sealed class LobbyRoomSettings
	{
		public string Name = "";

		public string Password = "";

		public bool ClearPassword;

		public LobbyVisibility Visibility;

		public int MaxMembers = 5;

		public bool JoinLocked;

		public bool RequireReady = true;
	}
}
