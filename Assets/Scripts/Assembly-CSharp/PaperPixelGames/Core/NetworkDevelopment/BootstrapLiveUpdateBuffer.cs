using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.NetworkDevelopment
{
	public sealed class BootstrapLiveUpdateBuffer : IDisposable
	{
		public const int DefaultMaxCount = 1024;

		public const int DefaultMaxBytes = 1048576;

		private static BootstrapLiveUpdateBuffer active;

		private static ulong serverRevision;

		private static ulong snapshotFloor;

		private readonly Queue<(Action Apply, int Bytes, ulong Revision)> pending = new Queue<(Action, int, ulong)>();

		private readonly List<(Action Apply, int Bytes, ulong Revision)> history = new List<(Action, int, ulong)>();

		private readonly bool retainHistory;

		private int historyBytes;

		private readonly int maxCount;

		private readonly int maxBytes;

		private bool replaying;

		private bool disposed;

		public int PendingCount => pending.Count;

		public int PendingBytes { get; private set; }

		public int ReplayedCount { get; private set; }

		public int PeakCount { get; private set; }

		public int PeakBytes { get; private set; }

		public int ReplayFrames { get; private set; }

		public double MaxReplayFrameMs { get; private set; }

		public string Failure { get; private set; }

		public bool HasFailure => !string.IsNullOrEmpty(Failure);

		public BootstrapLiveUpdateBuffer(int maxCount = 1024, int maxBytes = 1048576, bool retainHistory = false)
		{
			if (maxCount < 1 || maxBytes < 1)
			{
				throw new ArgumentOutOfRangeException();
			}
			this.maxCount = maxCount;
			this.maxBytes = maxBytes;
			this.retainHistory = retainHistory;
		}

		public static ulong NextServerRevision()
		{
			return checked(++serverRevision);
		}

		public static bool IsObsolete(ulong revision)
		{
			if (revision != 0L)
			{
				return revision <= snapshotFloor;
			}
			return false;
		}

		public void BeginSnapshot(ulong revision)
		{
			if (disposed || revision == 0L || revision < snapshotFloor)
			{
				throw new InvalidOperationException("Invalid bootstrap world revision.");
			}
			snapshotFloor = revision;
			pending.Clear();
			PendingBytes = 0;
			history.RemoveAll(((Action Apply, int Bytes, ulong Revision) entry) => entry.Revision <= revision);
			historyBytes = 0;
			foreach (var item in history)
			{
				pending.Enqueue(item);
				PendingBytes += item.Bytes;
				historyBytes += item.Bytes;
			}
		}

		public void Activate()
		{
			if (disposed || (active != null && active != this))
			{
				throw new InvalidOperationException("Another bootstrap update barrier is active.");
			}
			active = this;
		}

		public bool Enqueue(Action apply, int retainedBytes, ulong revision = 0uL)
		{
			if (disposed)
			{
				return false;
			}
			if (HasFailure)
			{
				return true;
			}
			if (IsObsolete(revision))
			{
				return true;
			}
			int num = (retainHistory ? history.Count : pending.Count);
			int num2 = (retainHistory ? historyBytes : PendingBytes);
			if (retainedBytes < 0 || num >= maxCount || retainedBytes > maxBytes - num2)
			{
				Failure = "Bootstrap live update backlog exceeded its bounded capacity.";
				return true;
			}
			(Action, int, ulong) item = (apply ?? throw new ArgumentNullException("apply"), retainedBytes, revision);
			pending.Enqueue(item);
			if (retainHistory)
			{
				history.Add(item);
				historyBytes += retainedBytes;
			}
			PendingBytes += retainedBytes;
			PeakCount = Math.Max(PeakCount, num + 1);
			PeakBytes = Math.Max(PeakBytes, num2 + retainedBytes);
			return true;
		}

		public static bool TryDefer<TTarget, TData>(TTarget target, TData data, Action<TTarget, TData> apply, int retainedBytes = 4096, ulong revision = 0uL) where TTarget : NetworkBehaviour
		{
			if (target == null || target.IsServer)
			{
				return false;
			}
			if (IsObsolete(revision))
			{
				return true;
			}
			BootstrapLiveUpdateBuffer bootstrapLiveUpdateBuffer = active;
			if (bootstrapLiveUpdateBuffer == null || bootstrapLiveUpdateBuffer.disposed || bootstrapLiveUpdateBuffer.replaying)
			{
				return false;
			}
			if (bootstrapLiveUpdateBuffer.HasFailure)
			{
				return true;
			}
			return Capture(bootstrapLiveUpdateBuffer, target, data, apply, retainedBytes, revision);
		}

		public static bool TryDeferArray<TTarget, TValue>(TTarget target, TValue[] data, Action<TTarget, TValue[]> apply, int bytesPerItem, ulong revision = 0uL) where TTarget : NetworkBehaviour where TValue : struct
		{
			if (target == null || target.IsServer)
			{
				return false;
			}
			if (IsObsolete(revision))
			{
				return true;
			}
			BootstrapLiveUpdateBuffer bootstrapLiveUpdateBuffer = active;
			if (bootstrapLiveUpdateBuffer == null || bootstrapLiveUpdateBuffer.disposed || bootstrapLiveUpdateBuffer.replaying)
			{
				return false;
			}
			if (bootstrapLiveUpdateBuffer.HasFailure)
			{
				return true;
			}
			long num = 512 + ((data != null) ? data.LongLength : 0) * bytesPerItem;
			if (bytesPerItem < 1 || (bootstrapLiveUpdateBuffer.retainHistory ? bootstrapLiveUpdateBuffer.history.Count : bootstrapLiveUpdateBuffer.PendingCount) >= bootstrapLiveUpdateBuffer.maxCount || num > bootstrapLiveUpdateBuffer.maxBytes - (bootstrapLiveUpdateBuffer.retainHistory ? bootstrapLiveUpdateBuffer.historyBytes : bootstrapLiveUpdateBuffer.PendingBytes))
			{
				return bootstrapLiveUpdateBuffer.Enqueue(() =>
				{
				}, int.MaxValue, 0uL);
			}
			return Capture(bootstrapLiveUpdateBuffer, target, (data == null) ? null : ((TValue[])data.Clone()), apply, (int)num, revision);
		}

		private static bool Capture<TTarget, TData>(BootstrapLiveUpdateBuffer buffer, TTarget target, TData data, Action<TTarget, TData> apply, int retainedBytes, ulong revision) where TTarget : NetworkBehaviour
		{
			ulong objectId = target.NetworkObjectId;
			return buffer.Enqueue(() =>
			{
				if (target != null && target.IsSpawned && target.NetworkObjectId == objectId)
				{
					apply(target, data);
				}
			}, retainedBytes, revision);
		}

		public void DrainFrame(Func<bool> isCurrent, Func<double> clock = null, double frameBudgetMs = 2.0, int maxSteps = 8)
		{
			if (disposed || HasFailure)
			{
				return;
			}
			if (clock == null)
			{
				clock = BootstrapApplyOperation.MonotonicSeconds;
			}
			double num = clock();
			int num2 = 0;
			ReplayFrames++;
			try
			{
				while (pending.Count > 0)
				{
					if (!isCurrent())
					{
						Dispose();
						break;
					}
					(Action, int, ulong) tuple = pending.Dequeue();
					PendingBytes -= tuple.Item2;
					replaying = true;
					try
					{
						tuple.Item1();
					}
					finally
					{
						replaying = false;
					}
					ReplayedCount++;
					if (++num2 >= Math.Max(1, maxSteps) || (clock() - num) * 1000.0 >= frameBudgetMs)
					{
						break;
					}
				}
			}
			catch (Exception ex)
			{
				Failure = "Bootstrap live update replay failed: " + ex.Message;
			}
			finally
			{
				MaxReplayFrameMs = Math.Max(MaxReplayFrameMs, Math.Max(0.0, (clock() - num) * 1000.0));
			}
		}

		public void Dispose()
		{
			disposed = true;
			pending.Clear();
			history.Clear();
			historyBytes = 0;
			PendingBytes = 0;
			if (active == this)
			{
				active = null;
			}
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		public static void Reset()
		{
			active?.Dispose();
			active = null;
			serverRevision = (snapshotFloor = 0uL);
		}
	}
}
