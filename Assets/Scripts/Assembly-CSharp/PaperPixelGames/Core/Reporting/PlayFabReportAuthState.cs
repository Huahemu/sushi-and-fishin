namespace PaperPixelGames.Core.Reporting
{
	public enum PlayFabReportAuthState : byte
	{
		Uninitialized = 0,
		Initialized = 1,
		Authenticating = 2,
		Retrying = 3,
		Authenticated = 4,
		Failed = 5
	}
}
