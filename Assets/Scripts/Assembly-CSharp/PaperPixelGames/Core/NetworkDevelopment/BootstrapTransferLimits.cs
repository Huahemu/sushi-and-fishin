namespace PaperPixelGames.Core.NetworkDevelopment
{
	public static class BootstrapTransferLimits
	{
		public const int MaxSnapshotBytes = 1048576;

		public const int ChunkBytes = 16384;

		public const int InlineBytes = 16384;

		public const int WindowChunks = 2;

		public const int MaxRemoteTransfers = 4;

		public const int ChunksPerFrame = 4;
	}
}
