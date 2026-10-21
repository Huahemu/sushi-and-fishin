using System;
using System.Collections.Generic;
using Unity.Netcode;

namespace PaperPixelGames.Networking
{
	public sealed class ConnectionApprovalContext
	{
		public NetworkManager.ConnectionApprovalRequest Request { get; set; }

		public int ConnectedClientCount { get; set; }

		public int ReservedSlotCount { get; set; }

		public int OccupiedPlatformSlotCount { get; set; } = -1;

		public int MaxMembers { get; set; }

		public bool IsFriendsOnly { get; set; }

		public ulong HostPlatformId { get; set; }

		public ulong AuthenticatedPlatformId { get; set; }

		public IReadOnlyCollection<ulong> ActivePlatformIds { get; set; } = Array.Empty<ulong>();

		public IReadOnlyCollection<ulong> ReservedPlatformIds { get; set; } = Array.Empty<ulong>();

		public bool IsCurrentLobbyMember { get; set; }

		public bool RequireLobbyMembership { get; set; }

		public bool RequireRoomAdmission { get; set; }

		public bool IsRoomAdmitted { get; set; }

		public bool IsHostFriend { get; set; }

		public ISet<ulong> BannedIds { get; set; } = new HashSet<ulong>();

		public int ServerVersion { get; set; }

		public string ServerReleaseVersion { get; set; } = string.Empty;

		public ulong ServerSessionEpoch { get; set; }
	}
}
