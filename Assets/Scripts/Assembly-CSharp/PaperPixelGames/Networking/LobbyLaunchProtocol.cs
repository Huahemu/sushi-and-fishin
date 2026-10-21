using System;
using Newtonsoft.Json;

namespace PaperPixelGames.Networking
{
	public static class LobbyLaunchProtocol
	{
		private const string Prefix = "launch:";

		private const int MaxPayloadLength = 2048;

		public static string Serialize(LobbyLaunchMessage message)
		{
			if (message == null || message.Epoch == 0L || message.HostSteamId == 0L)
			{
				return string.Empty;
			}
			return "launch:" + JsonConvert.SerializeObject(message);
		}

		public static bool TryParse(string payload, out LobbyLaunchMessage message)
		{
			message = null;
			if (string.IsNullOrWhiteSpace(payload) || payload.Length > 2048 || !payload.StartsWith("launch:", StringComparison.Ordinal))
			{
				return false;
			}
			try
			{
				message = JsonConvert.DeserializeObject<LobbyLaunchMessage>(payload.Substring("launch:".Length));
				return message != null && message.ProtocolVersion == 1 && Enum.IsDefined(typeof(LobbyLaunchMessageKind), message.Kind) && message.Epoch != 0L && message.HostSteamId != 0;
			}
			catch
			{
				message = null;
				return false;
			}
		}

		public static bool IsPrepareForRemoteClient(LobbyLaunchMessage message, ulong senderSteamId, ulong lobbyOwnerSteamId, ulong localSteamId, ulong lastHandledEpoch)
		{
			if (message != null && message.Kind == LobbyLaunchMessageKind.Prepare && localSteamId != 0L && localSteamId != lobbyOwnerSteamId && senderSteamId == lobbyOwnerSteamId && message.HostSteamId == lobbyOwnerSteamId)
			{
				return message.Epoch != lastHandledEpoch;
			}
			return false;
		}

		public static bool IsClosureForSession(LobbyLaunchMessage message, ulong senderSteamId, ulong localSteamId, ulong expectedHost, ulong expectedEpoch)
		{
			if (message != null && message.Kind == LobbyLaunchMessageKind.Closed && message.ProtocolVersion == 1 && expectedEpoch != 0L && expectedHost != 0L && localSteamId != 0L && localSteamId != expectedHost && senderSteamId == expectedHost && message.HostSteamId == expectedHost)
			{
				return message.Epoch == expectedEpoch;
			}
			return false;
		}
	}
}
