using System;
using System.Collections.Generic;

namespace PaperPixelGames.Networking
{
	public sealed class LaunchPreparationWait
	{
		public const double MinimumTimeoutSeconds = 120.0;

		private readonly HashSet<ulong> _expected = new HashSet<ulong>();

		private readonly HashSet<ulong> _ready = new HashSet<ulong>();

		public IReadOnlyCollection<ulong> ReadySteamIds => _ready;

		public int ExpectedCount => _expected.Count;

		public double Deadline { get; private set; }

		public bool AllReady
		{
			get
			{
				if (_expected.Count > 0)
				{
					return _expected.IsSubsetOf(_ready);
				}
				return false;
			}
		}

		public void Begin(double now, double timeoutSeconds, IEnumerable<ulong> members, ulong hostId)
		{
			Clear();
			foreach (ulong member in members)
			{
				if (member != 0L)
				{
					_expected.Add(member);
				}
			}
			_expected.Add(hostId);
			_ready.Add(hostId);
			Deadline = now + Math.Max(120.0, timeoutSeconds);
		}

		public bool AcceptReady(ulong member, double now)
		{
			if (now < Deadline && _expected.Contains(member))
			{
				return _ready.Add(member);
			}
			return false;
		}

		public void RemoveMember(ulong member)
		{
			_expected.Remove(member);
			_ready.Remove(member);
		}

		public bool ShouldComplete(double now)
		{
			if (!AllReady)
			{
				return now >= Deadline;
			}
			return true;
		}

		public void Clear()
		{
			_expected.Clear();
			_ready.Clear();
			Deadline = 0.0;
		}
	}
}
