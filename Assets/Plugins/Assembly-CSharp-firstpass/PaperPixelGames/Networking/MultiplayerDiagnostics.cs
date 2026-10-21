using System;
using Steamworks;
using UnityEngine;

namespace PaperPixelGames.Networking
{
	public static class MultiplayerDiagnostics
	{
		private static MultiplayerDiagnosticWindow _window = new MultiplayerDiagnosticWindow();

		private static string _run = Guid.NewGuid().ToString("N");

		private static ulong _lobby;

		private static ulong _epoch;

		private static ulong _localSteam;

		private static ulong _hostSteam;

		private static long _operation;

		private static long _attempt;

		private static string _role = "menu";

		private static double _attemptStart;

		private static double _lastTick;

		private static double _nextSample;

		private static double _watchUntil;

		private static long _sentMessages;

		private static long _sentBytes;

		private static long _rejected;

		private static long _reliableRejected;

		private static long _receivedBytes;

		private static long _limitExceeded;

		private static long _noConnection;

		private static int _maxMessage;

		private static Result _lastReject;

		private static ulong _lastRejectPeer;

		public static double Now => Time.realtimeSinceStartupAsDouble;

		public static long CurrentAttempt => _attempt;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Reset()
		{
			_window = new MultiplayerDiagnosticWindow();
			_run = Guid.NewGuid().ToString("N");
			_lobby = (_epoch = (_localSteam = (_hostSteam = 0uL)));
			_operation = (_attempt = 0L);
			_role = "menu";
			_attemptStart = (_lastTick = (_nextSample = (_watchUntil = 0.0)));
			ClearTraffic();
		}

		public static void SetContext(ulong lobby, ulong epoch, ulong localSteam, ulong hostSteam, long operation)
		{
			_lobby = lobby;
			_epoch = epoch;
			_localSteam = localSteam;
			_hostSteam = hostSteam;
			_operation = operation;
		}

		public static void BeginAttempt(string role)
		{
			_attempt++;
			_role = role;
			_attemptStart = Now;
			_lastTick = Now;
			_nextSample = Now + 5.0;
			_window.ClearSample();
			ClearTraffic();
			WatchStartup();
			Record("attempt_begin", role, ulong.MaxValue);
		}

		public static void WatchStartup()
		{
			_watchUntil = Now + 600.0;
		}

		public static void Record(string name, string detail = "", ulong clientId = ulong.MaxValue, bool critical = false)
		{
			TryRecord(name, detail, clientId, critical);
		}

		private static bool TryRecord(string name, string detail, ulong clientId = ulong.MaxValue, bool critical = false, bool sample = false)
		{
			try
			{
				double now = Now;
				if (!(sample ? _window.TryRecordSample(now) : _window.TryRecord(now, critical)))
				{
					return false;
				}
				if (!Debug.unityLogger.IsLogTypeAllowed(LogType.Log))
				{
					_window.ObserveWriteFailure();
					return false;
				}
				string text = FormattableString.Invariant($"[MPDiag] utc={DateTime.UtcNow:O} run={_run} attempt={_attempt} t={now:F3} elapsed={Math.Max(0.0, now - _attemptStart):F3} role={_role} lobby={_lobby} epoch={_epoch} localSteam={_localSteam} hostSteam={_hostSteam} operation={_operation} ngoClient={clientId} event={name} detail=\"{MultiplayerDiagnosticWindow.SafeText(detail)}\"");
				Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", text);
				return true;
			}
			catch (Exception)
			{
				_window.ObserveWriteFailure();
				return false;
			}
		}

		public static void ObserveSend(Result result, int bytes, bool reliable, ulong transportId)
		{
			_sentMessages++;
			_maxMessage = Math.Max(_maxMessage, bytes);
			if (result == Result.OK)
			{
				_sentBytes += bytes;
				return;
			}
			_rejected++;
			if (reliable)
			{
				_reliableRejected++;
			}
			if (result == Result.LimitExceeded)
			{
				_limitExceeded++;
			}
			if (result == Result.NoConnection)
			{
				_noConnection++;
			}
			_lastReject = result;
			_lastRejectPeer = transportId;
		}

		public static void ObserveReceive(int bytes)
		{
			_receivedBytes += bytes;
		}

		public static bool Tick()
		{
			double now = Now;
			if (now <= _watchUntil && _lastTick > 0.0)
			{
				_window.ObserveFrame(now - _lastTick);
			}
			_lastTick = now;
			if (now > _watchUntil || now < _nextSample)
			{
				return false;
			}
			_nextSample = now + 5.0;
			Flush("periodic");
			return true;
		}

		public static void Flush(string reason)
		{
			if (TryRecord("sample", FormattableString.Invariant($"reason={reason} sendCalls={_sentMessages} acceptedBytes={_sentBytes} rejected={_rejected} reliableRejected={_reliableRejected} lastReject={_lastReject} lastRejectTransport={_lastRejectPeer} receivedBytes={_receivedBytes} maxMessageBytes={_maxMessage} limitExceeded={_limitExceeded} noConnection={_noConnection} longFrames={_window.LongFrames} maxFrameMs={_window.MaxFrameSeconds * 1000.0:F1} suppressed={_window.Suppressed}"), ulong.MaxValue, critical: false, sample: true))
			{
				_window.ClearSample();
				ClearTraffic();
			}
		}

		private static void ClearTraffic()
		{
			_sentMessages = (_sentBytes = (_rejected = (_reliableRejected = (_receivedBytes = 0L))));
			_limitExceeded = (_noConnection = 0L);
			_maxMessage = 0;
			_lastReject = Result.None;
			_lastRejectPeer = ulong.MaxValue;
		}
	}
}
