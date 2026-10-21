using System.Collections.Generic;

namespace PaperPixelGames.Core.Staff
{
	public sealed class StaffReservationService
	{
		private readonly Dictionary<string, int> owners = new Dictionary<string, int>();

		public bool TryReserve(string resource, int employee)
		{
			if (owners.TryGetValue(resource, out var value))
			{
				return value == employee;
			}
			owners.Add(resource, employee);
			return true;
		}

		public bool IsReservedByOther(string resource, int employee)
		{
			if (owners.TryGetValue(resource, out var value))
			{
				return value != employee;
			}
			return false;
		}

		public void Release(string resource, int employee)
		{
			if (owners.TryGetValue(resource, out var value) && value == employee)
			{
				owners.Remove(resource);
			}
		}

		public void ReleaseAll(int employee)
		{
			List<string> list = new List<string>();
			foreach (KeyValuePair<string, int> owner in owners)
			{
				if (owner.Value == employee)
				{
					list.Add(owner.Key);
				}
			}
			foreach (string item in list)
			{
				owners.Remove(item);
			}
		}
	}
}
