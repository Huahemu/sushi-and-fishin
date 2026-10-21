namespace PaperPixelGames.Framework.States.Game
{
	public struct ReturnToMainMenuRequest
	{
		public ReturnToMainMenuReason Reason;

		public string DebugReason;

		public MultiplayerFailure Failure;

		public bool ShutdownNetwork;

		public bool KickRemotesIfHost;

		public bool LeaveSteamLobby;

		public bool ClearChat;

		public bool ShowErrorToast;

		public static ReturnToMainMenuRequest UserRequested()
		{
			return new ReturnToMainMenuRequest
			{
				Reason = ReturnToMainMenuReason.UserRequested,
				DebugReason = "User requested return to main menu.",
				ShutdownNetwork = true,
				KickRemotesIfHost = true,
				LeaveSteamLobby = true,
				ClearChat = true,
				ShowErrorToast = false
			};
		}

		public static ReturnToMainMenuRequest StartupFailure(string reason)
		{
			return StartupFailure(MultiplayerFailure.Create(MultiplayerFailureCode.HostStartFailed, MultiplayerFailureStage.HostStart, reason));
		}

		public static ReturnToMainMenuRequest StartupFailure(MultiplayerFailure failure)
		{
			return new ReturnToMainMenuRequest
			{
				Reason = ReturnToMainMenuReason.StartupFailure,
				DebugReason = failure.DebugDetail,
				Failure = failure,
				ShutdownNetwork = true,
				KickRemotesIfHost = true,
				LeaveSteamLobby = true,
				ClearChat = true,
				ShowErrorToast = true
			};
		}

		public static ReturnToMainMenuRequest LocalClientDisconnected(string reason)
		{
			return LocalClientDisconnected(MultiplayerFailure.Create(MultiplayerFailureCode.TransportFailure, MultiplayerFailureStage.Session, reason));
		}

		public static ReturnToMainMenuRequest LocalClientDisconnected(MultiplayerFailure failure)
		{
			return new ReturnToMainMenuRequest
			{
				Reason = ReturnToMainMenuReason.LocalClientDisconnected,
				DebugReason = failure.DebugDetail,
				Failure = failure,
				ShutdownNetwork = true,
				KickRemotesIfHost = false,
				LeaveSteamLobby = true,
				ClearChat = true,
				ShowErrorToast = true
			};
		}

		public static ReturnToMainMenuRequest JoinRequested(ulong lobbyId)
		{
			return new ReturnToMainMenuRequest
			{
				Reason = ReturnToMainMenuReason.JoinRequested,
				DebugReason = $"Steam requested joining lobby {lobbyId}.",
				ShutdownNetwork = true,
				KickRemotesIfHost = true,
				LeaveSteamLobby = true,
				ClearChat = true,
				ShowErrorToast = false
			};
		}

		public static ReturnToMainMenuRequest ConnectionFailed(string reason)
		{
			return ConnectionFailed(MultiplayerFailure.Create(MultiplayerFailureCode.Unknown, MultiplayerFailureStage.ClientConnect, reason));
		}

		public static ReturnToMainMenuRequest ConnectionFailed(MultiplayerFailure failure)
		{
			return new ReturnToMainMenuRequest
			{
				Reason = ReturnToMainMenuReason.ConnectionFailed,
				DebugReason = failure.DebugDetail,
				Failure = failure,
				ShutdownNetwork = true,
				KickRemotesIfHost = false,
				LeaveSteamLobby = true,
				ClearChat = true,
				ShowErrorToast = true
			};
		}
	}
}
