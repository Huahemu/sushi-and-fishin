namespace PaperPixelGames.Core.NetworkDevelopment
{
	public enum BootstrapChunkResult
	{
		Ignored = 0,
		Accepted = 1,
		Duplicate = 2,
		Complete = 3,
		Rejected = 4
	}
}
