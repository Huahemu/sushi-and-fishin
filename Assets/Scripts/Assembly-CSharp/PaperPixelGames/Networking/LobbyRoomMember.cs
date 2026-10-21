namespace PaperPixelGames.Networking
{
	public sealed class LobbyRoomMember
	{
		public ulong SteamId;

		public string Name;

		public bool IsOwner;

		public bool IsLocal;

		public bool IsReady;

		public bool IsAdmitted;

		public bool IsFriend;
	}
}
