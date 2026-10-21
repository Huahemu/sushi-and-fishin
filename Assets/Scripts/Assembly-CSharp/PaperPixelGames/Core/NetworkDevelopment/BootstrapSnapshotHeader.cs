using Unity.Collections;

namespace PaperPixelGames.Core.NetworkDevelopment
{
	public struct BootstrapSnapshotHeader
	{
		public ushort Version;

		public byte Mode;

		public FixedString64Bytes HostSteamId;

		public int SlotId;

		public ulong SessionEpoch;

		public ulong SnapshotId;

		public ulong WorldRevision;

		public ushort ModuleCount;
	}
}
