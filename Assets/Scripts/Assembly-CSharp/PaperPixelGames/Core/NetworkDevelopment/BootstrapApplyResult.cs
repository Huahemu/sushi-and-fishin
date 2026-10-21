namespace PaperPixelGames.Core.NetworkDevelopment
{
	public struct BootstrapApplyResult
	{
		public bool Success;

		public bool Retryable;

		public string FailReason;

		public JoinSessionContext Context;
	}
}
