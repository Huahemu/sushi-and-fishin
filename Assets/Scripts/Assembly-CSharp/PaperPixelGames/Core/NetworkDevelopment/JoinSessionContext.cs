using System;

namespace PaperPixelGames.Core.NetworkDevelopment
{
	[Serializable]
	public struct JoinSessionContext
	{
		public string HostSteamId;

		public int SlotId;

		public JoinBootstrapMode Mode;

		public ulong SessionEpoch;

		public ulong SnapshotId;

		public ulong WorldRevision;

		public bool HasSnapshotIdentity
		{
			get
			{
				if (IsValid && SnapshotId != 0L && WorldRevision != 0L)
				{
					if (Mode != JoinBootstrapMode.Initial)
					{
						return Mode == JoinBootstrapMode.Late;
					}
					return true;
				}
				return false;
			}
		}

		public bool IsValid
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(HostSteamId) && SlotId >= 0)
				{
					return SessionEpoch != 0;
				}
				return false;
			}
		}

		public bool SameSnapshot(JoinSessionContext other)
		{
			if (HasSnapshotIdentity && other.HasSnapshotIdentity && SessionEpoch == other.SessionEpoch && SnapshotId == other.SnapshotId && WorldRevision == other.WorldRevision && Mode == other.Mode && HostSteamId == other.HostSteamId)
			{
				return SlotId == other.SlotId;
			}
			return false;
		}
	}
}
