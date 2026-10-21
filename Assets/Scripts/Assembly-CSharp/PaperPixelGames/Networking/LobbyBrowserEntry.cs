namespace PaperPixelGames.Networking
{
	public sealed class LobbyBrowserEntry
	{
		public ulong LobbyId;

		public string Name = "";

		public string OwnerName = "";

		public string JoinCode = "";

		public string State = "";

		public LobbyVisibility Visibility;

		public int MemberCount;

		public int MaxMembers;

		public bool HasPassword;

		public bool JoinLocked;

		public bool IsFriend;

		public bool IsCompatible;
	}
}
