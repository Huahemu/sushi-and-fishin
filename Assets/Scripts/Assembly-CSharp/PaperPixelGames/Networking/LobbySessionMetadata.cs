using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using PaperPixelGames.Core;
using Steamworks;
using Steamworks.Data;

namespace PaperPixelGames.Networking
{
	public static class LobbySessionMetadata
	{
		[Serializable]
		private sealed class SessionSnapshot
		{
			public int v;

			public string s;

			public ulong e;

			public ulong h;

			public bool j;

			public long b;

			public string c;

			public string r;
		}

		public const int DefaultHeartbeatFutureSkewSeconds = 30;

		public const string StateKey = "session_state";

		public const string EpochKey = "session_epoch";

		public const string NetworkHostKey = "network_host_steam_id";

		public const string JoinableKey = "joinable";

		public const string LaunchCohortKey = "launch_cohort";

		public const string HeartbeatKey = "server_heartbeat";

		public const string ReleaseVersionKey = "release_version";

		public const string ReleaseVersionMemberKey = "release_version";

		public const string SnapshotKey = "session_descriptor";

		private const int SnapshotVersion = 1;

		public static long UtcNowUnixSeconds
		{
			get
			{
				if (SteamClient.IsValid)
				{
					try
					{
						DateTime dateTime = DateTime.SpecifyKind(SteamUtils.SteamServerTime, DateTimeKind.Utc);
						if (dateTime.Year >= 2020)
						{
							return new DateTimeOffset(dateTime).ToUnixTimeSeconds();
						}
					}
					catch
					{
					}
				}
				return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			}
		}

		public static bool Publish(Lobby lobby, string state, ulong epoch, ulong networkHostSteamId, bool joinable, IEnumerable<ulong> launchCohort, long heartbeatUnixSeconds = 0L)
		{
			if (!SteamClient.IsValid || (ulong)lobby.Id == 0L || (ulong)lobby.Owner.Id != (ulong)SteamClient.SteamId)
			{
				return false;
			}
			long heartbeatUnixSeconds2 = ((heartbeatUnixSeconds > 0) ? heartbeatUnixSeconds : UtcNowUnixSeconds);
			string text = state ?? "waiting";
			string value = SerializeCohort(launchCohort);
			string value2 = ReleaseVersion.Value;
			lobby.SetData("session_state", text);
			lobby.SetData("session_epoch", epoch.ToString(CultureInfo.InvariantCulture));
			lobby.SetData("network_host_steam_id", networkHostSteamId.ToString(CultureInfo.InvariantCulture));
			lobby.SetData("joinable", joinable ? "1" : "0");
			lobby.SetData("launch_cohort", value);
			lobby.SetData("release_version", value2);
			lobby.SetData("server_heartbeat", heartbeatUnixSeconds2.ToString(CultureInfo.InvariantCulture));
			lobby.SetData("session_descriptor", SerializeSnapshot(new LobbySessionDescriptor(text, epoch, networkHostSteamId, joinable, heartbeatUnixSeconds2, ParseCohort(value), value2)));
			return true;
		}

		public static bool PublishHeartbeat(Lobby lobby, ulong expectedEpoch)
		{
			if (!TryRead(lobby, out var descriptor) || descriptor.Epoch != expectedEpoch)
			{
				return false;
			}
			if (!SteamClient.IsValid || (ulong)lobby.Owner.Id != (ulong)SteamClient.SteamId)
			{
				return false;
			}
			long utcNowUnixSeconds = UtcNowUnixSeconds;
			lobby.SetData("server_heartbeat", utcNowUnixSeconds.ToString(CultureInfo.InvariantCulture));
			lobby.SetData("session_descriptor", SerializeSnapshot(new LobbySessionDescriptor(descriptor.State, descriptor.Epoch, descriptor.NetworkHostSteamId, descriptor.Joinable, utcNowUnixSeconds, descriptor.LaunchCohort, descriptor.ReleaseVersion)));
			return true;
		}

		public static bool TryRead(Lobby lobby, out LobbySessionDescriptor descriptor)
		{
			descriptor = default;
			if ((ulong)lobby.Id == 0L)
			{
				return false;
			}
			string data = lobby.GetData("session_descriptor");
			if (!string.IsNullOrWhiteSpace(data))
			{
				return TryDeserializeSnapshot(data, out descriptor);
			}
			string data2 = lobby.GetData("session_state");
			if (!ulong.TryParse(lobby.GetData("session_epoch"), NumberStyles.None, CultureInfo.InvariantCulture, out var result) || !ulong.TryParse(lobby.GetData("network_host_steam_id"), NumberStyles.None, CultureInfo.InvariantCulture, out var result2) || !long.TryParse(lobby.GetData("server_heartbeat"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result3))
			{
				return false;
			}
			bool joinable = string.Equals(lobby.GetData("joinable"), "1", StringComparison.Ordinal);
			IReadOnlyCollection<ulong> launchCohort = ParseCohort(lobby.GetData("launch_cohort"));
			string text = lobby.GetData("release_version")?.Trim();
			descriptor = new LobbySessionDescriptor(data2, result, result2, joinable, result3, launchCohort, text);
			if (result != 0L && result2 != 0L)
			{
				return !string.IsNullOrWhiteSpace(text);
			}
			return false;
		}

		public static string SerializeSnapshot(LobbySessionDescriptor descriptor)
		{
			if (descriptor.Epoch == 0L || descriptor.NetworkHostSteamId == 0L || string.IsNullOrWhiteSpace(descriptor.ReleaseVersion))
			{
				return string.Empty;
			}
			return JsonConvert.SerializeObject(new SessionSnapshot
			{
				v = 1,
				s = descriptor.State,
				e = descriptor.Epoch,
				h = descriptor.NetworkHostSteamId,
				j = descriptor.Joinable,
				b = descriptor.HeartbeatUnixSeconds,
				c = SerializeCohort(descriptor.LaunchCohort),
				r = descriptor.ReleaseVersion
			});
		}

		public static bool TryDeserializeSnapshot(string value, out LobbySessionDescriptor descriptor)
		{
			descriptor = default;
			if (string.IsNullOrWhiteSpace(value) || value.Length > 4096)
			{
				return false;
			}
			try
			{
				SessionSnapshot sessionSnapshot = JsonConvert.DeserializeObject<SessionSnapshot>(value);
				if (sessionSnapshot == null || sessionSnapshot.v != 1 || sessionSnapshot.e == 0L || sessionSnapshot.h == 0L || sessionSnapshot.b <= 0 || string.IsNullOrWhiteSpace(sessionSnapshot.s) || string.IsNullOrWhiteSpace(sessionSnapshot.r))
				{
					return false;
				}
				descriptor = new LobbySessionDescriptor(sessionSnapshot.s, sessionSnapshot.e, sessionSnapshot.h, sessionSnapshot.j, sessionSnapshot.b, ParseCohort(sessionSnapshot.c), sessionSnapshot.r.Trim());
				return true;
			}
			catch
			{
				descriptor = default;
				return false;
			}
		}

		public static void ClearSession(Lobby lobby)
		{
			if (SteamClient.IsValid && (ulong)lobby.Id != 0L && (ulong)lobby.Owner.Id == (ulong)SteamClient.SteamId)
			{
				lobby.SetData("session_state", "waiting");
				lobby.SetData("session_epoch", string.Empty);
				lobby.SetData("network_host_steam_id", string.Empty);
				lobby.SetData("joinable", "0");
				lobby.SetData("launch_cohort", string.Empty);
				lobby.SetData("server_heartbeat", string.Empty);
				lobby.SetData("session_descriptor", string.Empty);
			}
		}

		public static bool IsHeartbeatFresh(LobbySessionDescriptor descriptor, long nowUnixSeconds, int maxAgeSeconds = 30, int maxFutureSkewSeconds = 30)
		{
			if (descriptor.HeartbeatUnixSeconds <= 0 || nowUnixSeconds <= 0)
			{
				return false;
			}
			long num = nowUnixSeconds - descriptor.HeartbeatUnixSeconds;
			if (num <= Math.Max(1, maxAgeSeconds))
			{
				return num >= -Math.Max(0, maxFutureSkewSeconds);
			}
			return false;
		}

		public static string SerializeCohort(IEnumerable<ulong> ids)
		{
			if (ids != null)
			{
				return string.Join(",", from id in ids.Where((ulong id) => id != 0).Distinct()
					orderby id
					select id);
			}
			return string.Empty;
		}

		public static IReadOnlyCollection<ulong> ParseCohort(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return Array.Empty<ulong>();
			}
			HashSet<ulong> hashSet = new HashSet<ulong>();
			string[] array = value.Split(',');
			for (int i = 0; i < array.Length; i++)
			{
				if (ulong.TryParse(array[i], NumberStyles.None, CultureInfo.InvariantCulture, out var result) && result != 0L)
				{
					hashSet.Add(result);
				}
			}
			return hashSet.ToArray();
		}
	}
}
