namespace PaperPixelGames.Core.NetworkDevelopment
{
	public enum BootstrapRevisionDecision
	{
		Accept = 0,
		Duplicate = 1,
		Stale = 2,
		Reject = 3
	}
}
