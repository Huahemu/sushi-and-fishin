using PaperPixelGames.Framework.States.Game;

namespace PaperPixelGames.Networking
{
	public readonly struct ApprovalResult
	{
		public readonly bool Approved;

		public readonly string Reason;

		public readonly MultiplayerFailureCode FailureCode;

		public ApprovalResult(bool approved, string reason, MultiplayerFailureCode failureCode)
		{
			Approved = approved;
			Reason = reason ?? string.Empty;
			FailureCode = failureCode;
		}

		public static ApprovalResult Allow()
		{
			return new ApprovalResult(approved: true, string.Empty, MultiplayerFailureCode.None);
		}

		public static ApprovalResult Deny(string reason, MultiplayerFailureCode code = MultiplayerFailureCode.NotAuthorized)
		{
			return new ApprovalResult(approved: false, reason, code);
		}
	}
}
