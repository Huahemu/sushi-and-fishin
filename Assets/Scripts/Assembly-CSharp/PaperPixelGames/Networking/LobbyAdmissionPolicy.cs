using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PaperPixelGames.Networking
{
	public sealed class LobbyAdmissionPolicy
	{
		private readonly HashSet<ulong> _admitted = new HashSet<ulong>();

		private readonly HashSet<ulong> _banned = new HashSet<ulong>();

		private readonly Dictionary<ulong, double> _kickedUntil = new Dictionary<ulong, double>();

		private readonly Dictionary<ulong, double> _nextAttempt = new Dictionary<ulong, double>();

		private byte[] _passwordDigest = Array.Empty<byte>();

		private byte[] _salt = Array.Empty<byte>();

		public IReadOnlyCollection<ulong> AdmittedIds => _admitted;

		public IReadOnlyCollection<ulong> BannedIds => _banned;

		public bool HasPassword => _passwordDigest.Length != 0;

		public void Reset(IEnumerable<ulong> bans = null)
		{
			_admitted.Clear();
			_banned.Clear();
			_kickedUntil.Clear();
			_nextAttempt.Clear();
			ClearPassword();
			if (bans != null)
			{
				_banned.UnionWith(bans.Where((ulong id) => id != 0));
			}
		}

		public void SetPassword(string password)
		{
			ClearPassword();
			if (!string.IsNullOrEmpty(password))
			{
				_salt = new byte[32];
				using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
				{
					randomNumberGenerator.GetBytes(_salt);
				}
				_passwordDigest = Digest(password);
			}
		}

		private void ClearPassword()
		{
			Array.Clear(_passwordDigest, 0, _passwordDigest.Length);
			Array.Clear(_salt, 0, _salt.Length);
			_passwordDigest = Array.Empty<byte>();
			_salt = Array.Empty<byte>();
		}

		private byte[] Digest(string password)
		{
			using HMACSHA256 hMACSHA = new HMACSHA256(_salt);
			return hMACSHA.ComputeHash(Encoding.UTF8.GetBytes(password ?? ""));
		}

		public bool IsAdmitted(ulong id)
		{
			if (id != 0L && _admitted.Contains(id))
			{
				return !_banned.Contains(id);
			}
			return false;
		}

		public void AdmitOwner(ulong id)
		{
			if (id != 0L)
			{
				_admitted.Add(id);
			}
		}

		public void Depart(ulong id)
		{
			_admitted.Remove(id);
		}

		public void Kick(ulong id, double now)
		{
			_admitted.Remove(id);
			_kickedUntil[id] = now + 30.0;
		}

		public void Ban(ulong id)
		{
			_admitted.Remove(id);
			_banned.Add(id);
		}

		public void Unban(ulong id)
		{
			_banned.Remove(id);
		}

		public LobbyRoomResult Evaluate(ulong authenticatedId, string password, bool friendsOnly, bool isFriend, bool locked, double now)
		{
			if (authenticatedId == 0L)
			{
				return LobbyRoomResult.Fail("Steam identity is unavailable.");
			}
			if (_banned.Contains(authenticatedId))
			{
				return LobbyRoomResult.Fail("You are banned from this host's lobbies.");
			}
			if (_kickedUntil.TryGetValue(authenticatedId, out var value) && now < value)
			{
				return LobbyRoomResult.Fail("You were removed. Wait 30 seconds before rejoining.");
			}
			if (IsAdmitted(authenticatedId))
			{
				return LobbyRoomResult.Ok();
			}
			if (locked)
			{
				return LobbyRoomResult.Fail("This lobby is locked.");
			}
			if (friendsOnly && !isFriend)
			{
				return LobbyRoomResult.Fail("This lobby is for the host's Steam friends.");
			}
			if (_nextAttempt.TryGetValue(authenticatedId, out var value2) && now < value2)
			{
				return LobbyRoomResult.Fail("Too many attempts. Please wait a moment.");
			}
			_nextAttempt[authenticatedId] = now + 2.0;
			if (HasPassword)
			{
				if (string.IsNullOrEmpty(password))
				{
					return LobbyRoomResult.Fail("This lobby requires a password.");
				}
				byte[] array = Digest(password);
				int num = array.Length ^ _passwordDigest.Length;
				for (int i = 0; i < array.Length && i < _passwordDigest.Length; i++)
				{
					num |= array[i] ^ _passwordDigest[i];
				}
				Array.Clear(array, 0, array.Length);
				if (num != 0)
				{
					return LobbyRoomResult.Fail("Incorrect lobby password.");
				}
			}
			_admitted.Add(authenticatedId);
			return LobbyRoomResult.Ok();
		}
	}
}
