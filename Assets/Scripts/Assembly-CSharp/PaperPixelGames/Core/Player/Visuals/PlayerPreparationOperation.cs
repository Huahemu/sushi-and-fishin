using System;
using System.Threading;

namespace PaperPixelGames.Core.Player.Visuals
{
	public sealed class PlayerPreparationOperation : IDisposable
	{
		public const double DefaultTimeoutSeconds = 180.0;

		private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

		private readonly double deadline;

		public readonly long Attempt;

		public readonly ulong SessionEpoch;

		public readonly ulong OwnerId;

		public readonly ulong ObjectId;

		public CancellationToken Token { get; }

		public string StopReason { get; private set; }

		public PlayerPreparationOperation(long attempt, ulong epoch, ulong ownerId, ulong objectId, double now, double timeoutSeconds = 180.0)
		{
			Attempt = attempt;
			SessionEpoch = epoch;
			OwnerId = ownerId;
			ObjectId = objectId;
			deadline = now + timeoutSeconds;
			Token = cancellation.Token;
		}

		public bool Check(double now, bool current)
		{
			if (!current)
			{
				Cancel("StaleSession");
			}
			else if (now >= deadline)
			{
				Cancel("Timeout");
			}
			return !Token.IsCancellationRequested;
		}

		public void Cancel(string reason)
		{
			if (!Token.IsCancellationRequested)
			{
				StopReason = reason;
				cancellation.Cancel();
			}
		}

		public void Dispose()
		{
			Cancel("Disposed");
			cancellation.Dispose();
		}
	}
}
