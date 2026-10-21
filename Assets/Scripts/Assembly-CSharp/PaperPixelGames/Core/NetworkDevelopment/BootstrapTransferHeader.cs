using System;
using Unity.Collections;
using Unity.Netcode;

namespace PaperPixelGames.Core.NetworkDevelopment
{
	public struct BootstrapTransferHeader : INetworkSerializable
	{
		public ulong Epoch;

		public ulong SnapshotId;

		public ulong Revision;

		public FixedString64Bytes HostSteamId;

		public int SlotId;

		public int TotalBytes;

		public int ChunkCount;

		public byte Mode;

		public uint Checksum;

		public JoinSessionContext Context => new JoinSessionContext
		{
			SessionEpoch = Epoch,
			SnapshotId = SnapshotId,
			WorldRevision = Revision,
			HostSteamId = HostSteamId.ToString(),
			SlotId = SlotId,
			Mode = (JoinBootstrapMode)Mode
		};

		public bool IsValid
		{
			get
			{
				if (Context.HasSnapshotIdentity && TotalBytes > 16384 && TotalBytes <= 1048576)
				{
					return ChunkCount == (TotalBytes + 16384 - 1) / 16384;
				}
				return false;
			}
		}

		public bool Matches(ulong epoch, ulong snapshotId, ulong revision)
		{
			if (Epoch == epoch && SnapshotId == snapshotId)
			{
				return Revision == revision;
			}
			return false;
		}

		public bool SameHeader(BootstrapTransferHeader other)
		{
			if (Matches(other.Epoch, other.SnapshotId, other.Revision) && HostSteamId.Equals(other.HostSteamId) && SlotId == other.SlotId && Mode == other.Mode && TotalBytes == other.TotalBytes && ChunkCount == other.ChunkCount)
			{
				return Checksum == other.Checksum;
			}
			return false;
		}

		public static BootstrapTransferHeader Create(JoinSessionContext context, byte[] blob)
		{
			if (blob == null)
			{
				throw new ArgumentNullException("blob");
			}
			if (blob.Length <= 16384 || blob.Length > 1048576)
			{
				throw new ArgumentException("Snapshot size is outside the chunked transfer range.");
			}
			BootstrapTransferHeader result = new BootstrapTransferHeader
			{
				Epoch = context.SessionEpoch,
				SnapshotId = context.SnapshotId,
				Revision = context.WorldRevision,
				HostSteamId = new FixedString64Bytes(context.HostSteamId),
				SlotId = context.SlotId,
				Mode = (byte)context.Mode,
				TotalBytes = blob.Length,
				ChunkCount = (blob.Length + 16384 - 1) / 16384,
				Checksum = BootstrapTransferChecksum.Compute(blob)
			};
			if (!result.IsValid)
			{
				throw new ArgumentException("Invalid bootstrap transfer header.");
			}
			return result;
		}

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref Epoch, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref SnapshotId, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref Revision, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref HostSteamId, default(FastBufferWriter.ForFixedStrings));
			serializer.SerializeValue(ref SlotId, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref Mode);
			serializer.SerializeValue(ref TotalBytes, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref ChunkCount, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref Checksum, default(FastBufferWriter.ForPrimitives));
		}
	}
}
