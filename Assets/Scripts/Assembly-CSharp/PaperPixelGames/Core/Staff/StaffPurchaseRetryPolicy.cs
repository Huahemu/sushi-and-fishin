using System.Collections.Generic;

namespace PaperPixelGames.Core.Staff
{
	public sealed class StaffPurchaseRetryPolicy
	{
		private sealed class Failure
		{
			public StaffStatus Status;

			public double RetryAt;

			public int Balance;

			public bool Unlocked;

			public bool Enabled;

			public bool Open;
		}

		public const double RetryDelaySeconds = 30.0;

		private readonly Dictionary<(string product, bool blue), Failure> failures = new Dictionary<(string, bool), Failure>();

		public bool HasFailures => failures.Count > 0;

		public void Record(string product, bool blue, StaffStatus status, double now, int balance, bool unlocked, bool enabled, bool open)
		{
			failures[(product, blue)] = new Failure
			{
				Status = status,
				RetryAt = now + 30.0,
				Balance = balance,
				Unlocked = unlocked,
				Enabled = enabled,
				Open = open
			};
		}

		public bool IsBlocked(string product, bool blue, double now, int balance, bool unlocked, bool enabled, bool open, out StaffStatus status)
		{
			status = StaffStatus.Waiting;
			if (!failures.TryGetValue((product, blue), out var value))
			{
				return false;
			}
			if (now >= value.RetryAt || unlocked != value.Unlocked || enabled != value.Enabled || open != value.Open || (value.Status == StaffStatus.InsufficientMoney && balance != value.Balance))
			{
				Clear(product, blue);
				return false;
			}
			status = value.Status;
			return true;
		}

		public void Clear(string product, bool blue)
		{
			failures.Remove((product, blue));
		}
	}
}
