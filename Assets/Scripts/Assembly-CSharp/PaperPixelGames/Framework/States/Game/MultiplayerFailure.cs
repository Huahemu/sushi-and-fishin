namespace PaperPixelGames.Framework.States.Game
{
	public readonly struct MultiplayerFailure
	{
		public MultiplayerFailureCode Code { get; }

		public MultiplayerFailureStage Stage { get; }

		public string LocalizationKey { get; }

		public string UserMessage { get; }

		public string DebugDetail { get; }

		public bool HasFailure => Code != MultiplayerFailureCode.None;

		public static MultiplayerFailure LobbyVersionMismatch(MultiplayerFailureStage stage)
		{
			return new MultiplayerFailure(MultiplayerFailureCode.VersionMismatch, stage, "multiplayer.error.lobby_version_mismatch", "Players in this lobby have different game versions. Everyone must use the same version to play together.");
		}

		public MultiplayerFailure(MultiplayerFailureCode code, MultiplayerFailureStage stage, string localizationKey, string userMessage, string debugDetail = "")
		{
			Code = code;
			Stage = stage;
			LocalizationKey = localizationKey ?? string.Empty;
			UserMessage = (string.IsNullOrWhiteSpace(userMessage) ? "The multiplayer session could not be completed." : userMessage);
			DebugDetail = debugDetail ?? string.Empty;
		}

		public static MultiplayerFailure Create(MultiplayerFailureCode code, MultiplayerFailureStage stage, string debugDetail = "")
		{
			string localizationKey;
			string userMessage;
			switch (code)
			{
			case MultiplayerFailureCode.SteamUnavailable:
				localizationKey = "multiplayer.error.steam_unavailable";
				userMessage = "Steam is offline. Reconnect to Steam and try again.";
				break;
			case MultiplayerFailureCode.LobbyCreateFailed:
				localizationKey = "multiplayer.error.lobby_create_failed";
				userMessage = "The Steam lobby could not be created. Please try again.";
				break;
			case MultiplayerFailureCode.LobbyJoinFailed:
				localizationKey = "multiplayer.error.lobby_join_failed";
				userMessage = "The Steam lobby could not be joined. Please try joining through the invitation again.";
				break;
			case MultiplayerFailureCode.LobbyFull:
				localizationKey = "multiplayer.error.lobby_full";
				userMessage = "The lobby is full.";
				break;
			case MultiplayerFailureCode.HostUnavailable:
				localizationKey = "multiplayer.error.host_unavailable";
				userMessage = "The host is no longer available.";
				break;
			case MultiplayerFailureCode.HostStartFailed:
				localizationKey = "multiplayer.error.host_start_failed";
				userMessage = "The game session could not be started as host.";
				break;
			case MultiplayerFailureCode.ClientStartFailed:
				localizationKey = "multiplayer.error.client_start_failed";
				userMessage = "The connection to the host could not be started.";
				break;
			case MultiplayerFailureCode.ConnectionTimedOut:
				localizationKey = "multiplayer.error.connection_timeout";
				userMessage = "The connection timed out. Please try again.";
				break;
			case MultiplayerFailureCode.VersionMismatch:
				localizationKey = "multiplayer.error.version_mismatch";
				userMessage = "Your game version does not match the host. Update the game and try again.";
				break;
			case MultiplayerFailureCode.ProtocolMismatch:
				localizationKey = "multiplayer.error.protocol_mismatch";
				userMessage = "This session uses an incompatible multiplayer protocol.";
				break;
			case MultiplayerFailureCode.IdentityMismatch:
				localizationKey = "multiplayer.error.identity_mismatch";
				userMessage = "Steam could not verify this connection.";
				break;
			case MultiplayerFailureCode.NotAuthorized:
				localizationKey = "multiplayer.error.not_authorized";
				userMessage = "You are not authorized to join this session.";
				break;
			case MultiplayerFailureCode.Banned:
				localizationKey = "multiplayer.error.banned";
				userMessage = "You have been blocked from joining this session.";
				break;
			case MultiplayerFailureCode.SessionExpired:
				localizationKey = "multiplayer.error.session_expired";
				userMessage = "This multiplayer session has expired.";
				break;
			case MultiplayerFailureCode.SessionChanged:
				localizationKey = "multiplayer.error.session_changed";
				userMessage = "The host started a different session. Please join again.";
				break;
			case MultiplayerFailureCode.LaunchPreparationFailed:
				localizationKey = "multiplayer.error.launch_preparation_failed";
				userMessage = "Game launch preparation could not be completed. Please try again.";
				break;
			case MultiplayerFailureCode.BootstrapFailed:
				localizationKey = "multiplayer.error.bootstrap_failed";
				userMessage = "The game data could not be synchronized.";
				break;
			case MultiplayerFailureCode.TransportFailure:
				localizationKey = "multiplayer.error.transport_failure";
				userMessage = "The network connection was interrupted.";
				break;
			case MultiplayerFailureCode.ReconnectTimedOut:
				localizationKey = "multiplayer.error.reconnect_timeout";
				userMessage = "The connection could not be restored.";
				break;
			default:
				code = MultiplayerFailureCode.Unknown;
				localizationKey = "multiplayer.error.unknown";
				userMessage = "The multiplayer session could not be completed.";
				break;
			}
			return new MultiplayerFailure(code, stage, localizationKey, userMessage, debugDetail);
		}
	}
}
