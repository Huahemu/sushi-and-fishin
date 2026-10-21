using System;
using System.Collections.Generic;

namespace PaperPixelGames.Networking
{
	public sealed class HostPreparationWait
	{
		public const double PreparationSeconds = 600.0;

		public const double MetadataRefreshSeconds = 120.0;

		public const double RefreshIntervalSeconds = 5.0;

		private readonly ulong _localId;

		private readonly string _release;

		private double _staleReadyDeadline = double.PositiveInfinity;

		private double _nextRefresh;

		private bool _warned;

		public ulong LobbyId { get; }

		public ulong HostId { get; }

		public ulong Epoch { get; private set; }

		public double StartedAt { get; }

		public double Deadline { get; }

		public HostPreparationWait(double now, ulong lobbyId, ulong hostId, ulong epoch, ulong localId, string release, double timeoutSeconds)
		{
			StartedAt = now;
			LobbyId = lobbyId;
			HostId = hostId;
			Epoch = epoch;
			_localId = localId;
			_release = release;
			Deadline = now + timeoutSeconds;
			_nextRefresh = now;
		}

		public bool ShouldRefresh(double now)
		{
			if (now < _nextRefresh || now >= Deadline || now >= _staleReadyDeadline)
			{
				return false;
			}
			_nextRefresh = now + 5.0;
			return true;
		}

		public HostPreparationStatus Evaluate(double now, ulong lobbyId, ulong ownerId, LobbySessionDescriptor? snapshot, long utcNow, int heartbeatMaxAge)
		{
			if (lobbyId != LobbyId)
			{
				return HostPreparationStatus.SessionChanged;
			}
			if (ownerId == 0L || ownerId != HostId || ownerId == _localId)
			{
				return HostPreparationStatus.HostUnavailable;
			}
			if (snapshot.HasValue)
			{
				LobbySessionDescriptor value = snapshot.Value;
				if (value.NetworkHostSteamId != HostId)
				{
					return HostPreparationStatus.HostUnavailable;
				}
				if (Epoch != 0L && value.Epoch != Epoch)
				{
					return HostPreparationStatus.SessionChanged;
				}
				if (!string.Equals(value.ReleaseVersion, _release, StringComparison.Ordinal))
				{
					return HostPreparationStatus.VersionMismatch;
				}
				if (Epoch == 0L)
				{
					Epoch = value.Epoch;
				}
				if (value.State == "host_ingame" || value.State == "ingame")
				{
					if (!((value.State == "ingame") ? value.Joinable : Contains(value.LaunchCohort, _localId)))
					{
						return HostPreparationStatus.AdmissionClosed;
					}
					if (now < Deadline && now < _staleReadyDeadline && LobbySessionMetadata.IsHeartbeatFresh(value, utcNow, heartbeatMaxAge))
					{
						return HostPreparationStatus.Ready;
					}
					if (double.IsPositiveInfinity(_staleReadyDeadline))
					{
						_staleReadyDeadline = now + 120.0;
					}
				}
			}
			if (now >= Deadline || now >= _staleReadyDeadline)
			{
				return HostPreparationStatus.TimedOut;
			}
			if (!_warned && now - StartedAt >= 60.0)
			{
				_warned = true;
				return HostPreparationStatus.Warning;
			}
			return HostPreparationStatus.Waiting;
		}

		private static bool Contains(IEnumerable<ulong> ids, ulong id)
		{
			foreach (ulong id2 in ids)
			{
				if (id2 == id)
				{
					return true;
				}
			}
			return false;
		}
	}
}
