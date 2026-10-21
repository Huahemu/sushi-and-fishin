using System;
using System.Collections.Generic;

namespace PaperPixelGames.Core.Staff
{
	public sealed class StaffTaskBoard
	{
		private readonly Dictionary<string, double> firstSeen = new Dictionary<string, double>();

		public double Score(string key, double now, double deadline, double rolePriority)
		{
			if (!firstSeen.TryGetValue(key, out var value))
			{
				value = (firstSeen[key] = now);
			}
			return ((deadline > 0.0) ? (100000.0 / Math.Max(1.0, deadline - now)) : 0.0) + Math.Max(0.0, now - value) + rolePriority;
		}

		public void Complete(string key)
		{
			firstSeen.Remove(key);
		}

		public void Retain(ISet<string> active)
		{
			List<string> list = new List<string>();
			foreach (string key in firstSeen.Keys)
			{
				if (!active.Contains(key))
				{
					list.Add(key);
				}
			}
			foreach (string item in list)
			{
				firstSeen.Remove(item);
			}
		}
	}
}
