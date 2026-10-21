using System;
using System.Collections.Generic;

namespace PaperPixelGames.Networking
{
	public readonly struct LobbySessionDescriptor
	{
		public string State { get; }

		public ulong Epoch { get; }

		public ulong NetworkHostSteamId { get; }

		public bool Joinable { get; }

		public long HeartbeatUnixSeconds { get; }

		public IReadOnlyCollection<ulong> LaunchCohort { get; }

		public string ReleaseVersion { get; }

		public LobbySessionDescriptor(string state, ulong epoch, ulong networkHostSteamId, bool joinable, long heartbeatUnixSeconds, IReadOnlyCollection<ulong> launchCohort, string releaseVersion = "")
		{
			State = state ?? string.Empty;
			Epoch = epoch;
			NetworkHostSteamId = networkHostSteamId;
			Joinable = joinable;
			HeartbeatUnixSeconds = heartbeatUnixSeconds;
			LaunchCohort = launchCohort ?? Array.Empty<ulong>();
			ReleaseVersion = releaseVersion ?? string.Empty;
		}
	}
}
