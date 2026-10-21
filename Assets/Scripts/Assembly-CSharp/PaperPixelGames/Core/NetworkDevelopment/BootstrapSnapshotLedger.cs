using System;
using System.Collections.Generic;

namespace PaperPixelGames.Core.NetworkDevelopment
{
	public sealed class BootstrapSnapshotLedger
	{
		private sealed class Entry
		{
			public JoinSessionContext Context;

			public byte[] Blob;

			public bool Deferred;
		}

		private readonly Dictionary<ulong, Entry> entries = new Dictionary<ulong, Entry>();

		private ulong nextId;

		public ulong AllocateId()
		{
			return checked(++nextId);
		}

		public void Store(ulong clientId, JoinSessionContext context, byte[] blob)
		{
			if (!context.HasSnapshotIdentity || blob == null)
			{
				throw new ArgumentException("Invalid snapshot ledger entry.");
			}
			entries[clientId] = new Entry
			{
				Context = context,
				Blob = blob
			};
		}

		public bool TryGet(ulong clientId, out JoinSessionContext context, out byte[] blob)
		{
			context = default;
			blob = null;
			if (!entries.TryGetValue(clientId, out var value) || value.Deferred)
			{
				return false;
			}
			context = value.Context;
			blob = value.Blob;
			return true;
		}

		public bool Matches(ulong clientId, ulong epoch, ulong snapshotId, ulong revision)
		{
			if (TryGet(clientId, out var context, out var _) && context.SessionEpoch == epoch && context.SnapshotId == snapshotId)
			{
				return context.WorldRevision == revision;
			}
			return false;
		}

		public void Defer(ulong clientId)
		{
			if (entries.TryGetValue(clientId, out var value))
			{
				value.Deferred = true;
				value.Blob = null;
			}
		}

		public bool NeedsRefresh(ulong clientId)
		{
			if (entries.TryGetValue(clientId, out var value))
			{
				return value.Deferred;
			}
			return false;
		}

		public void Acknowledge(ulong clientId)
		{
			if (entries.TryGetValue(clientId, out var value))
			{
				value.Blob = null;
			}
		}

		public void Remove(ulong clientId)
		{
			entries.Remove(clientId);
		}

		public void Reset()
		{
			entries.Clear();
			nextId = 0uL;
		}
	}
}
