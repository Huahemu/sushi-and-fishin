using System;
using System.Collections;
using System.Collections.Generic;

namespace PaperPixelGames.Networking
{
	public sealed class AdmissionReservations : IReadOnlyCollection<ulong>, IEnumerable<ulong>, IEnumerable
	{
		public readonly struct Lease
		{
			internal readonly AdmissionReservations Owner;

			public readonly ulong SteamId;

			public readonly ulong ClientId;

			public readonly ulong SessionEpoch;

			public readonly ulong LobbyId;

			public readonly ulong Generation;

			public readonly double ExpiresAt;

			internal Lease(AdmissionReservations owner, ulong steamId, ulong clientId, ulong sessionEpoch, ulong lobbyId, ulong generation, double expiresAt)
			{
				Owner = owner;
				SteamId = steamId;
				ClientId = clientId;
				SessionEpoch = sessionEpoch;
				LobbyId = lobbyId;
				Generation = generation;
				ExpiresAt = expiresAt;
			}
		}

		private readonly Dictionary<ulong, Lease> entries = new Dictionary<ulong, Lease>();

		private ulong generation;

		public int Count => entries.Count;

		public bool Contains(ulong steamId)
		{
			return entries.ContainsKey(steamId);
		}

		public Lease Reserve(ulong steamId, ulong clientId, ulong sessionEpoch, ulong lobbyId, double expiresAt)
		{
			if (steamId == 0L)
			{
				throw new ArgumentOutOfRangeException("steamId");
			}
			if (sessionEpoch == 0L)
			{
				throw new ArgumentOutOfRangeException("sessionEpoch");
			}
			if (lobbyId == 0L)
			{
				throw new ArgumentOutOfRangeException("lobbyId");
			}
			if (double.IsNaN(expiresAt) || double.IsInfinity(expiresAt) || expiresAt < 0.0)
			{
				throw new ArgumentOutOfRangeException("expiresAt");
			}
			Lease lease = new Lease(this, steamId, clientId, sessionEpoch, lobbyId, checked(++generation), expiresAt);
			entries[steamId] = lease;
			return lease;
		}

		public bool Release(ulong steamId, ulong clientId)
		{
			if (!entries.TryGetValue(steamId, out var value) || value.ClientId != clientId)
			{
				return false;
			}
			return entries.Remove(steamId);
		}

		public bool TryExpire(Lease lease, double now, ulong sessionEpoch, ulong lobbyId, bool isActive, bool hasPendingSceneJoin)
		{
			if (((lease.Owner != this || lease.Generation == 0L || lease.SessionEpoch != sessionEpoch || lease.LobbyId != lobbyId || double.IsNaN(now) || double.IsInfinity(now) || now < lease.ExpiresAt) | isActive | hasPendingSceneJoin) || !entries.TryGetValue(lease.SteamId, out var value) || value.Generation != lease.Generation)
			{
				return false;
			}
			return entries.Remove(lease.SteamId);
		}

		public void Clear()
		{
			entries.Clear();
		}

		public IEnumerator<ulong> GetEnumerator()
		{
			return entries.Keys.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
