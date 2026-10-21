using System.Collections.Generic;
using System.Linq;

namespace PaperPixelGames.Networking
{
	public sealed class LobbyRoomSnapshot
	{
		public ulong LobbyId;

		public ulong OwnerSteamId;

		public string Name = "";

		public string JoinCode = "";

		public string State = "waiting";

		public LobbyVisibility Visibility;

		public int MaxMembers = 5;

		public bool HasPassword;

		public bool JoinLocked;

		public bool RequireReady;

		public bool IsOwner;

		public bool IsAdmitted;

		public bool LocalReady;

		public bool AllReady;

		public bool CanEdit;

		public List<LobbyRoomMember> Members = new List<LobbyRoomMember>();

		public List<LobbyBannedMember> Bans = new List<LobbyBannedMember>();

		public bool BlocksSingleplayer
		{
			get
			{
				if (LobbyId != 0L)
				{
					if (IsOwner)
					{
						return Members.Count > 1;
					}
					return true;
				}
				return false;
			}
		}

		public bool HasSamePresentation(LobbyRoomSnapshot other)
		{
			if (other != null && LobbyId == other.LobbyId && OwnerSteamId == other.OwnerSteamId && Name == other.Name && JoinCode == other.JoinCode && State == other.State && Visibility == other.Visibility && MaxMembers == other.MaxMembers && HasPassword == other.HasPassword && JoinLocked == other.JoinLocked && RequireReady == other.RequireReady && IsOwner == other.IsOwner && IsAdmitted == other.IsAdmitted && LocalReady == other.LocalReady && AllReady == other.AllReady && CanEdit == other.CanEdit && Members.Count == other.Members.Count && Members.Zip(other.Members, (LobbyRoomMember a, LobbyRoomMember b) => a.SteamId == b.SteamId && a.Name == b.Name && a.IsOwner == b.IsOwner && a.IsLocal == b.IsLocal && a.IsReady == b.IsReady && a.IsAdmitted == b.IsAdmitted && a.IsFriend == b.IsFriend).All((bool equal) => equal) && Bans.Count == other.Bans.Count)
			{
				return Bans.Zip(other.Bans, (LobbyBannedMember a, LobbyBannedMember b) => a.SteamId == b.SteamId && a.Name == b.Name).All((bool equal) => equal);
			}
			return false;
		}
	}
}
