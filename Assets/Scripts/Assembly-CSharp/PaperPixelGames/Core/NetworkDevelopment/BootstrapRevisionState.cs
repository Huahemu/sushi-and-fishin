namespace PaperPixelGames.Core.NetworkDevelopment
{
	public sealed class BootstrapRevisionState
	{
		public JoinSessionContext Current { get; private set; }

		public BootstrapRevisionDecision Inspect(JoinSessionContext next, ulong epoch, bool started)
		{
			if (!next.HasSnapshotIdentity || next.SessionEpoch != epoch)
			{
				return BootstrapRevisionDecision.Reject;
			}
			if (!Current.HasSnapshotIdentity)
			{
				if (!started)
				{
					return BootstrapRevisionDecision.Accept;
				}
				return BootstrapRevisionDecision.Reject;
			}
			if (next.SessionEpoch != Current.SessionEpoch || next.HostSteamId != Current.HostSteamId || next.SlotId != Current.SlotId)
			{
				return BootstrapRevisionDecision.Reject;
			}
			if (next.SnapshotId < Current.SnapshotId)
			{
				return BootstrapRevisionDecision.Stale;
			}
			if (next.SnapshotId == Current.SnapshotId)
			{
				if (!next.SameSnapshot(Current))
				{
					return BootstrapRevisionDecision.Reject;
				}
				return BootstrapRevisionDecision.Duplicate;
			}
			if (started || Current.Mode != JoinBootstrapMode.Initial || next.Mode != JoinBootstrapMode.Late || next.WorldRevision <= Current.WorldRevision)
			{
				return BootstrapRevisionDecision.Reject;
			}
			return BootstrapRevisionDecision.Accept;
		}

		public void Commit(JoinSessionContext context)
		{
			Current = context;
		}

		public void Reset()
		{
			Current = default;
		}
	}
}
