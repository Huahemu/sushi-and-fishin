using System;
using System.Collections.Generic;
using Steamworks;

namespace PaperPixelGames.Networking
{
	public sealed class ReliableSendQueue
	{
		public delegate Result SendPacket(Peer peer, ArraySegment<byte> data, bool reliable);

		internal sealed class Pending
		{
			internal byte[] Bytes;

			internal double QueuedAt;
		}

		public sealed class Peer
		{
			internal readonly Queue<Pending> Messages = new Queue<Pending>();

			internal double NextRetryAt;

			internal bool FailureTaken;

			public ulong Id { get; }

			public int PendingBytes { get; internal set; }

			public int PendingCount => Messages.Count;

			public TransportSendFailure Failure { get; internal set; }

			public Result NativeResult { get; internal set; }

			public int FailureBytes { get; internal set; }

			internal Peer(ulong id)
			{
				Id = id;
			}

			public double OldestAge(double now)
			{
				if (Messages.Count != 0)
				{
					return Math.Max(0.0, now - Messages.Peek().QueuedAt);
				}
				return 0.0;
			}
		}

		public const int MaxMessageBytes = 524288;

		public const int DefaultPeerBytes = 2097152;

		public const int DefaultTotalBytes = 8388608;

		public const double DefaultMaxAgeSeconds = 60.0;

		private readonly Dictionary<ulong, Peer> _byId = new Dictionary<ulong, Peer>();

		private readonly List<Peer> _peers = new List<Peer>();

		private readonly int _peerBytes;

		private readonly int _totalBytes;

		private readonly int _peerMessages;

		private readonly int _totalMessages;

		private readonly int _sendsPerDrain;

		private readonly int _bytesPerDrain;

		private readonly double _maxAge;

		private readonly double _retryDelay;

		private int _cursor;

		public int PendingBytes { get; private set; }

		public int PendingCount { get; private set; }

		public long EnqueuedCount { get; private set; }

		public long RetryAcceptedCount { get; private set; }

		public ReliableSendQueue(int peerBytes = 2097152, int totalBytes = 8388608, int peerMessages = 256, int totalMessages = 1024, double maxAgeSeconds = 60.0, double retryDelaySeconds = 0.05, int sendsPerDrain = 32, int bytesPerDrain = 524288)
		{
			if (peerBytes <= 0 || totalBytes <= 0 || peerMessages <= 0 || totalMessages <= 0 || sendsPerDrain <= 0 || bytesPerDrain < 524288 || maxAgeSeconds <= 0.0 || double.IsNaN(maxAgeSeconds) || double.IsInfinity(maxAgeSeconds) || retryDelaySeconds < 0.0 || double.IsNaN(retryDelaySeconds) || double.IsInfinity(retryDelaySeconds))
			{
				throw new ArgumentOutOfRangeException("peerBytes", "Queue limits must be finite and positive; drain bytes must fit one Steam message.");
			}
			_peerBytes = peerBytes;
			_totalBytes = totalBytes;
			_peerMessages = peerMessages;
			_totalMessages = totalMessages;
			_maxAge = maxAgeSeconds;
			_retryDelay = retryDelaySeconds;
			_sendsPerDrain = sendsPerDrain;
			_bytesPerDrain = bytesPerDrain;
		}

		public Peer Open(ulong id)
		{
			if (_byId.TryGetValue(id, out var value))
			{
				Close(value);
			}
			Peer peer = new Peer(id);
			_byId.Add(id, peer);
			_peers.Add(peer);
			return peer;
		}

		public bool IsCurrent(Peer peer)
		{
			if (peer != null && _byId.TryGetValue(peer.Id, out var value))
			{
				return peer == value;
			}
			return false;
		}

		public void Close(Peer peer)
		{
			if (IsCurrent(peer))
			{
				Discard(peer);
				_byId.Remove(peer.Id);
				_peers.Remove(peer);
				if (_cursor >= _peers.Count)
				{
					_cursor = 0;
				}
			}
		}

		public void Clear()
		{
			foreach (Peer peer in _peers)
			{
				Discard(peer);
			}
			_byId.Clear();
			_peers.Clear();
			_cursor = 0;
			long enqueuedCount = (RetryAcceptedCount = 0L);
			EnqueuedCount = enqueuedCount;
		}

		public TransportSendOutcome Send(Peer peer, ArraySegment<byte> data, bool reliable, double now, SendPacket send)
		{
			if (!IsCurrent(peer))
			{
				return TransportSendOutcome.Inactive;
			}
			if (peer.Failure != TransportSendFailure.None)
			{
				return TransportSendOutcome.Failed;
			}
			if (Expired(peer, now))
			{
				return Fail(peer, TransportSendFailure.QueueExpired, Result.Timeout, data.Count);
			}
			if (data.Array == null)
			{
				return Fail(peer, TransportSendFailure.InvalidPayload, Result.InvalidParam, data.Count);
			}
			if (data.Count > 524288)
			{
				return Fail(peer, TransportSendFailure.MessageTooLarge, Result.InvalidParam, data.Count);
			}
			if (reliable && peer.PendingCount > 0)
			{
				return Enqueue(peer, data, now);
			}
			Result result = send(peer, data, reliable);
			if (!IsCurrent(peer))
			{
				return TransportSendOutcome.Inactive;
			}
			if (peer.Failure != TransportSendFailure.None)
			{
				return TransportSendOutcome.Failed;
			}
			switch (result)
			{
			case Result.OK:
				return TransportSendOutcome.Sent;
			case Result.LimitExceeded:
				if (!reliable)
				{
					return TransportSendOutcome.Dropped;
				}
				peer.NextRetryAt = now + _retryDelay;
				return Enqueue(peer, data, now);
			default:
				if (!reliable && result == Result.Ignored)
				{
					return TransportSendOutcome.Dropped;
				}
				return Fail(peer, TransportSendFailure.NativeError, result, data.Count);
			}
		}

		public void Drain(double now, SendPacket send)
		{
			int num = _sendsPerDrain;
			int num2 = _bytesPerDrain;
			int num3 = 0;
			while (_peers.Count > 0 && num > 0 && num3 < _peers.Count)
			{
				if (_cursor >= _peers.Count)
				{
					_cursor = 0;
				}
				Peer peer = _peers[_cursor++];
				num3++;
				if (peer.Failure != TransportSendFailure.None || peer.PendingCount == 0)
				{
					continue;
				}
				if (Expired(peer, now))
				{
					Fail(peer, TransportSendFailure.QueueExpired, Result.Timeout, peer.Messages.Peek().Bytes.Length);
					continue;
				}
				Pending pending = peer.Messages.Peek();
				if (now < peer.NextRetryAt || pending.Bytes.Length > num2)
				{
					continue;
				}
				num--;
				num2 -= pending.Bytes.Length;
				Result result = send(peer, new ArraySegment<byte>(pending.Bytes), reliable: true);
				if (IsCurrent(peer) && peer.Failure == TransportSendFailure.None)
				{
					switch (result)
					{
					case Result.OK:
						peer.Messages.Dequeue();
						Release(peer, pending.Bytes.Length);
						RetryAcceptedCount++;
						num3 = 0;
						break;
					case Result.LimitExceeded:
						peer.NextRetryAt = now + _retryDelay;
						break;
					default:
						Fail(peer, TransportSendFailure.NativeError, result, pending.Bytes.Length);
						break;
					}
				}
			}
		}

		public bool TryTakeFailure(out Peer peer)
		{
			foreach (Peer peer2 in _peers)
			{
				if (peer2.Failure != TransportSendFailure.None && !peer2.FailureTaken)
				{
					peer2.FailureTaken = true;
					peer = peer2;
					return true;
				}
			}
			peer = null;
			return false;
		}

		private bool Expired(Peer peer, double now)
		{
			if (peer.PendingCount > 0)
			{
				return now - peer.Messages.Peek().QueuedAt >= _maxAge;
			}
			return false;
		}

		private TransportSendOutcome Enqueue(Peer peer, ArraySegment<byte> data, double now)
		{
			if (peer.PendingCount >= _peerMessages || data.Count > _peerBytes - peer.PendingBytes)
			{
				return Fail(peer, TransportSendFailure.PeerCapacity, Result.LimitExceeded, data.Count);
			}
			if (PendingCount >= _totalMessages || data.Count > _totalBytes - PendingBytes)
			{
				return Fail(peer, TransportSendFailure.TotalCapacity, Result.LimitExceeded, data.Count);
			}
			byte[] array = ((data.Count == 0) ? Array.Empty<byte>() : new byte[data.Count]);
			Buffer.BlockCopy(data.Array, data.Offset, array, 0, data.Count);
			peer.Messages.Enqueue(new Pending
			{
				Bytes = array,
				QueuedAt = now
			});
			peer.PendingBytes += data.Count;
			PendingBytes += data.Count;
			PendingCount++;
			EnqueuedCount++;
			return TransportSendOutcome.Queued;
		}

		private TransportSendOutcome Fail(Peer peer, TransportSendFailure failure, Result result, int bytes)
		{
			peer.Failure = failure;
			peer.NativeResult = result;
			peer.FailureBytes = bytes;
			Discard(peer);
			return TransportSendOutcome.Failed;
		}

		private void Release(Peer peer, int bytes)
		{
			peer.PendingBytes -= bytes;
			PendingBytes -= bytes;
			PendingCount--;
		}

		private void Discard(Peer peer)
		{
			PendingBytes -= peer.PendingBytes;
			PendingCount -= peer.PendingCount;
			peer.Messages.Clear();
			peer.PendingBytes = 0;
		}
	}
}
