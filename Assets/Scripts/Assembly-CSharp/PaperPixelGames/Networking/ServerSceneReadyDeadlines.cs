using System;
using System.Collections.Generic;

namespace PaperPixelGames.Networking
{
	public sealed class ServerSceneReadyDeadlines
	{
		private readonly Dictionary<ulong, (ulong SteamId, double Deadline, bool Connected, bool Ready)> _pending = new Dictionary<ulong, (ulong, double, bool, bool)>();

		public bool TryApprove(ulong clientId, ulong steamId, double now, double sceneSeconds)
		{
			if (_pending.ContainsKey(clientId) || ContainsSteamId(steamId))
			{
				return false;
			}
			_pending[clientId] = (steamId, now + sceneSeconds + 30.0, false, false);
			return true;
		}

		public void Connected(ulong clientId, double now)
		{
			if (_pending.TryGetValue(clientId, out (ulong, double, bool, bool) value))
			{
				if (value.Item4)
				{
					_pending.Remove(clientId);
				}
				else
				{
					_pending[clientId] = (value.Item1, Math.Min(value.Item2, now + 30.0), true, false);
				}
			}
		}

		public void Ready(ulong clientId)
		{
			if (_pending.TryGetValue(clientId, out (ulong, double, bool, bool) value))
			{
				if (value.Item3)
				{
					_pending.Remove(clientId);
				}
				else
				{
					_pending[clientId] = (value.Item1, value.Item2, false, true);
				}
			}
		}

		public bool ContainsSteamId(ulong steamId)
		{
			foreach (var value in _pending.Values)
			{
				if (value.SteamId == steamId)
				{
					return true;
				}
			}
			return false;
		}

		public bool HasPendingCohort(IEnumerable<ulong> cohort, double now)
		{
			foreach (ulong item in cohort)
			{
				foreach (var value in _pending.Values)
				{
					if (value.SteamId == item && now < value.Deadline)
					{
						return true;
					}
				}
			}
			return false;
		}

		public bool TryTakeExpired(double now, out ulong clientId)
		{
			clientId = ulong.MaxValue;
			bool flag = false;
			foreach (KeyValuePair<ulong, (ulong, double, bool, bool)> item in _pending)
			{
				if (!(now < item.Value.Item2))
				{
					clientId = item.Key;
					flag = true;
					break;
				}
			}
			if (flag)
			{
				_pending.Remove(clientId);
			}
			return flag;
		}

		public void Remove(ulong clientId)
		{
			_pending.Remove(clientId);
		}

		public void Clear()
		{
			_pending.Clear();
		}
	}
}
