using System;

namespace PaperPixelGames.Core.NetworkDevelopment
{
	public sealed class BootstrapTransferReceiver
	{
		private byte[] _blob;

		private ulong _receivedMask;

		public BootstrapTransferHeader Header { get; private set; }

		public int NextChunk { get; private set; }

		public int ReceivedBytes { get; private set; }

		public bool Complete { get; private set; }

		public bool Receiving
		{
			get
			{
				if (_blob != null)
				{
					return !Complete;
				}
				return false;
			}
		}

		public int RetainedBytes
		{
			get
			{
				byte[] blob = _blob;
				if (blob == null)
				{
					return 0;
				}
				return blob.Length;
			}
		}

		public bool Begin(BootstrapTransferHeader header)
		{
			if (!header.IsValid)
			{
				return false;
			}
			Reset();
			Header = header;
			_blob = new byte[header.TotalBytes];
			return true;
		}

		public BootstrapChunkResult Accept(ulong epoch, ulong snapshotId, ulong revision, int index, byte[] chunk, uint checksum)
		{
			if (Header.SnapshotId == 0L || !Header.Matches(epoch, snapshotId, revision))
			{
				return BootstrapChunkResult.Ignored;
			}
			if (Complete)
			{
				return BootstrapChunkResult.Ignored;
			}
			if (_blob == null || index < 0 || index >= Header.ChunkCount || chunk == null || chunk.Length != Math.Min(16384, Header.TotalBytes - index * 16384) || BootstrapTransferChecksum.Compute(chunk) != checksum)
			{
				return Reject();
			}
			int num = index * 16384;
			ulong num2 = (ulong)(1L << index);
			if ((_receivedMask & num2) != 0L)
			{
				for (int i = 0; i < chunk.Length; i++)
				{
					if (_blob[num + i] != chunk[i])
					{
						return Reject();
					}
				}
				return BootstrapChunkResult.Duplicate;
			}
			if (index >= NextChunk + 2)
			{
				return Reject();
			}
			Buffer.BlockCopy(chunk, 0, _blob, num, chunk.Length);
			_receivedMask |= num2;
			ReceivedBytes += chunk.Length;
			while (NextChunk < Header.ChunkCount && (_receivedMask & (ulong)(1L << NextChunk)) != 0L)
			{
				NextChunk++;
			}
			if (NextChunk != Header.ChunkCount)
			{
				return BootstrapChunkResult.Accepted;
			}
			if (BootstrapTransferChecksum.Compute(_blob) != Header.Checksum)
			{
				return Reject();
			}
			Complete = true;
			return BootstrapChunkResult.Complete;
		}

		public byte[] TakeCompletedBlob()
		{
			if (!Complete)
			{
				return null;
			}
			byte[] blob = _blob;
			_blob = null;
			return blob;
		}

		private BootstrapChunkResult Reject()
		{
			Reset();
			return BootstrapChunkResult.Rejected;
		}

		public void Reset()
		{
			_blob = null;
			_receivedMask = 0uL;
			Header = default;
			int nextChunk = (ReceivedBytes = 0);
			NextChunk = nextChunk;
			Complete = false;
		}
	}
}
