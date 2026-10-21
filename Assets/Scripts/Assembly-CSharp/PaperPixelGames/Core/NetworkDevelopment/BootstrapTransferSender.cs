using System;

namespace PaperPixelGames.Core.NetworkDevelopment
{
	public sealed class BootstrapTransferSender
	{
		private byte[] _blob;

		private int _window;

		public BootstrapTransferHeader Header { get; }

		public double Deadline { get; }

		public int SentChunks { get; private set; }

		public int AcknowledgedChunks { get; private set; }

		public bool Ready { get; private set; }

		public bool Complete => AcknowledgedChunks == Header.ChunkCount;

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

		public BootstrapTransferSender(JoinSessionContext context, byte[] blob, double deadline)
		{
			Header = BootstrapTransferHeader.Create(context, blob);
			_blob = blob;
			Deadline = deadline;
		}

		public bool Expired(double now)
		{
			if (!Complete)
			{
				return now >= Deadline;
			}
			return false;
		}

		public bool Acknowledge(int nextChunk, int requestedWindow)
		{
			if (requestedWindow < 1 || requestedWindow > 2 || nextChunk < 0 || nextChunk > SentChunks || (!Ready && nextChunk != 0))
			{
				return false;
			}
			if (Ready && requestedWindow != _window)
			{
				return false;
			}
			Ready = true;
			_window = requestedWindow;
			if (nextChunk <= AcknowledgedChunks)
			{
				return true;
			}
			AcknowledgedChunks = nextChunk;
			if (Complete)
			{
				_blob = null;
			}
			return true;
		}

		public bool TryTakeNext(out int index, out byte[] chunk, out uint checksum)
		{
			index = 0;
			chunk = null;
			checksum = 0u;
			if (!Ready || _blob == null || SentChunks >= Header.ChunkCount || SentChunks >= AcknowledgedChunks + _window)
			{
				return false;
			}
			index = SentChunks++;
			int num = index * 16384;
			chunk = new byte[Math.Min(16384, Header.TotalBytes - num)];
			Buffer.BlockCopy(_blob, num, chunk, 0, chunk.Length);
			checksum = BootstrapTransferChecksum.Compute(chunk);
			return true;
		}
	}
}
