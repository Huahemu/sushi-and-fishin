namespace PaperPixelGames.Core.NetworkDevelopment
{
	public sealed class PendingBootstrapModulePayload
	{
		public ushort ModuleId;

		public byte[] Payload;

		public int RetryCount;

		public bool IsRequired;
	}
}
