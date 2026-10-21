using System;
using System.Linq;
using PaperPixelGames.Framework.States.Game;

namespace PaperPixelGames.Networking
{
	public sealed class ConnectionApprovalPolicy
	{
		public ApprovalResult Evaluate(ConnectionApprovalContext ctx, out ParsedPayload parsed)
		{
			parsed = default;
			if (!MultiplayerConnectionPayload.TryParse(ctx.Request.Payload, out parsed))
			{
				return ApprovalResult.Deny("Malformed connection payload", MultiplayerFailureCode.ProtocolMismatch);
			}
			if (parsed.ProtocolVersion != 5)
			{
				return ApprovalResult.Deny("Protocol version mismatch", MultiplayerFailureCode.ProtocolMismatch);
			}
			if (ctx.AuthenticatedPlatformId == 0L)
			{
				return ApprovalResult.Deny("Steam identity unavailable", MultiplayerFailureCode.IdentityMismatch);
			}
			if (parsed.PlatformId != ctx.AuthenticatedPlatformId)
			{
				return ApprovalResult.Deny("Steam identity mismatch", MultiplayerFailureCode.IdentityMismatch);
			}
			if (ctx.ServerSessionEpoch != 0L && parsed.SessionEpoch != ctx.ServerSessionEpoch)
			{
				return ApprovalResult.Deny("Session epoch mismatch", MultiplayerFailureCode.SessionChanged);
			}
			if (parsed.ClientVersion != ctx.ServerVersion)
			{
				return ApprovalResult.Deny("Game version mismatch", MultiplayerFailureCode.VersionMismatch);
			}
			if (!string.Equals(parsed.ReleaseVersion, ctx.ServerReleaseVersion, StringComparison.Ordinal))
			{
				return ApprovalResult.Deny("Release version mismatch", MultiplayerFailureCode.VersionMismatch);
			}
			bool flag = ctx.AuthenticatedPlatformId == ctx.HostPlatformId;
			if (!flag && ctx.RequireLobbyMembership && !ctx.IsCurrentLobbyMember)
			{
				return ApprovalResult.Deny("Steam user is not a member of the active lobby");
			}
			if (ctx.BannedIds != null && ctx.BannedIds.Contains(parsed.PlatformId))
			{
				return ApprovalResult.Deny("Banned", MultiplayerFailureCode.Banned);
			}
			if (!flag && ctx.RequireRoomAdmission && !ctx.IsRoomAdmitted)
			{
				return ApprovalResult.Deny("Lobby access has not been approved by the host");
			}
			if (!flag && ctx.IsFriendsOnly && !ctx.IsHostFriend && !ctx.IsRoomAdmitted)
			{
				return ApprovalResult.Deny("This lobby is for the host's Steam friends");
			}
			if (!flag && ctx.ActivePlatformIds.Contains(parsed.PlatformId))
			{
				return ApprovalResult.Deny("Duplicate Steam identity", MultiplayerFailureCode.IdentityMismatch);
			}
			bool flag2 = ctx.ReservedPlatformIds.Contains(parsed.PlatformId);
			int num = ((ctx.OccupiedPlatformSlotCount >= 0) ? ctx.OccupiedPlatformSlotCount : (ctx.ConnectedClientCount + ctx.ReservedSlotCount));
			if (!flag && !flag2 && num >= ctx.MaxMembers)
			{
				return ApprovalResult.Deny("Server is full", MultiplayerFailureCode.LobbyFull);
			}
			return ApprovalResult.Allow();
		}
	}
}
