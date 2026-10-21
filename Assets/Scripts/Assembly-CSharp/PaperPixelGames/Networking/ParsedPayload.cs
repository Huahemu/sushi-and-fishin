namespace PaperPixelGames.Networking
{
	public readonly struct ParsedPayload
	{
		public readonly ushort ProtocolVersion;

		public readonly ulong PlatformId;

		public readonly int ClientVersion;

		public readonly string ReleaseVersion;

		public readonly ulong SessionEpoch;

		public ParsedPayload(ushort protocolVersion, ulong platformId, int clientVersion, string releaseVersion, ulong sessionEpoch = 0uL)
		{
			ProtocolVersion = protocolVersion;
			PlatformId = platformId;
			ClientVersion = clientVersion;
			ReleaseVersion = releaseVersion ?? string.Empty;
			SessionEpoch = sessionEpoch;
		}
	}
}
