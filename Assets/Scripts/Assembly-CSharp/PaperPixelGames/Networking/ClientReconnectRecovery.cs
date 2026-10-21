using System;
using System.Linq;
using PaperPixelGames.Framework.States.Game;

namespace PaperPixelGames.Networking
{
	public sealed class ClientReconnectRecovery
	{
		public const int MaximumAttempts = 3;

		public const double ShutdownTimeoutSeconds = 30.0;

		public const double SceneResetTimeoutSeconds = 600.0;

		public const double GameplayTimeoutSeconds = 600.0;

		public ulong LobbyId { get; }

		public ulong HostId { get; }

		public ulong Epoch { get; }

		public double AdmissionDeadline { get; }

		public bool InitialJoin { get; }

		public ulong LocalSteamId { get; }

		public int Attempts { get; private set; }

		public long NetworkAttempt { get; private set; }

		public bool Cancelled { get; private set; }

		public bool StoppingNetwork { get; set; }

		public bool AttemptInterrupted { get; private set; }

		public bool AttemptInProgress { get; private set; }

		public MultiplayerFailure LastFailure { get; private set; }

		public MultiplayerFailure TerminalFailure { get; private set; }

		public double NextDelaySeconds => (Attempts == 0) ? 2 : ((Attempts == 1) ? 5 : 10);

		public ClientReconnectRecovery(ulong lobbyId, ulong hostId, ulong epoch, double now, double graceSeconds, bool initialJoin = false, ulong localSteamId = 0uL)
		{
			if (lobbyId == 0L || hostId == 0L || epoch == 0L)
			{
				throw new ArgumentException("Reconnect requires a valid lobby, host and epoch.");
			}
			LobbyId = lobbyId;
			HostId = hostId;
			Epoch = epoch;
			InitialJoin = initialJoin;
			LocalSteamId = localSteamId;
			AdmissionDeadline = now + ((double.IsNaN(graceSeconds) || double.IsInfinity(graceSeconds)) ? 45.0 : Math.Max(1.0, graceSeconds));
		}

		public bool Matches(ulong lobbyId, ulong hostId, ulong epoch)
		{
			if (!Cancelled && LobbyId == lobbyId && HostId == hostId)
			{
				return Epoch == epoch;
			}
			return false;
		}

		public bool CanStartAttempt(double now)
		{
			if (!Cancelled && !AttemptInProgress && !TerminalFailure.HasFailure && Attempts < 3)
			{
				return now < AdmissionDeadline;
			}
			return false;
		}

		public bool TryStartAttempt(double now)
		{
			if (!CanStartAttempt(now))
			{
				return false;
			}
			Attempts++;
			NetworkAttempt = 0L;
			AttemptInterrupted = false;
			AttemptInProgress = true;
			return true;
		}

		public void BindNetworkAttempt(long attempt)
		{
			NetworkAttempt = attempt;
		}

		public void ReportFailure(MultiplayerFailure failure)
		{
			if (!Cancelled && (!StoppingNetwork || !CanRetry(failure.Code)))
			{
				AttemptInterrupted = true;
				AttemptInProgress = false;
				LastFailure = failure;
				if (!TerminalFailure.HasFailure && !CanRetry(failure.Code))
				{
					TerminalFailure = failure;
				}
			}
		}

		public void Cancel()
		{
			Cancelled = true;
		}

		public static bool CanRetry(MultiplayerFailureCode code)
		{
			if (code != MultiplayerFailureCode.TransportFailure && code != MultiplayerFailureCode.ClientStartFailed)
			{
				return code == MultiplayerFailureCode.ConnectionTimedOut;
			}
			return true;
		}

		public static bool CanRecoverPhase(MultiplayerSessionPhase phase)
		{
			if (phase != MultiplayerSessionPhase.ClientConnecting && phase != MultiplayerSessionPhase.SceneSync && phase != MultiplayerSessionPhase.InitialBootstrap)
			{
				return phase == MultiplayerSessionPhase.Running;
			}
			return true;
		}

		public MultiplayerFailureCode ValidateTarget(ulong lobbyId, ulong ownerId, LobbySessionDescriptor descriptor, string releaseVersion, long now, int heartbeatMaxAge)
		{
			if (!Matches(lobbyId, descriptor.NetworkHostSteamId, descriptor.Epoch) || ownerId != HostId)
			{
				return MultiplayerFailureCode.SessionChanged;
			}
			if (!string.Equals(descriptor.ReleaseVersion, releaseVersion, StringComparison.Ordinal))
			{
				return MultiplayerFailureCode.VersionMismatch;
			}
			bool num = descriptor.State == "ingame" && descriptor.Joinable;
			bool flag = InitialJoin && LocalSteamId != 0L && descriptor.State == "host_ingame" && descriptor.LaunchCohort.Contains(LocalSteamId);
			if ((!num && !flag) || !LobbySessionMetadata.IsHeartbeatFresh(descriptor, now, heartbeatMaxAge))
			{
				return MultiplayerFailureCode.HostUnavailable;
			}
			return MultiplayerFailureCode.None;
		}
	}
}
