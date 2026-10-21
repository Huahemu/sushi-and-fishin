using System.Collections.Generic;

namespace PaperPixelGames.Core.Staff
{
	public sealed class StaffNotificationPolicy
	{
		private sealed class State
		{
			public StaffStatus Status;

			public double Since;

			public double LastToast = double.NegativeInfinity;

			public readonly HashSet<StaffStatus> Reported = new HashSet<StaffStatus>();
		}

		private readonly Dictionary<int, State> states = new Dictionary<int, State>();

		private double lastToast = double.NegativeInfinity;

		public bool TryNotify(int id, StaffStatus status, double now)
		{
			if (!states.TryGetValue(id, out var value))
			{
				Dictionary<int, State> dictionary = states;
				State obj = new State
				{
					Status = status,
					Since = now
				};
				value = obj;
				dictionary[id] = obj;
			}
			if (value.Status != status)
			{
				value.Status = status;
				value.Since = now;
			}
			if (status != StaffStatus.MissingWorkplace && status != StaffStatus.InsufficientMoney && status != StaffStatus.ProductsLocked && status != StaffStatus.WaitingForIngredients && status != StaffStatus.ConveyorDisabled && status != StaffStatus.NoTray && status != StaffStatus.MarketClosed && status != StaffStatus.ProductUnavailable && status != StaffStatus.PurchaseFailed && status != StaffStatus.StorageFull)
			{
				if (now - value.Since >= 15.0)
				{
					value.Reported.Clear();
				}
				return false;
			}
			if (now - value.Since < 15.0 || value.Reported.Contains(status) || now - value.LastToast < 60.0 || now - lastToast < 10.0)
			{
				return false;
			}
			value.Reported.Add(status);
			value.LastToast = (lastToast = now);
			return true;
		}

		public void Remove(int id)
		{
			states.Remove(id);
		}

		public void Clear()
		{
			states.Clear();
			lastToast = double.NegativeInfinity;
		}
	}
}
