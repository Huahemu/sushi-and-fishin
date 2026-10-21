using System;

namespace PaperPixelGames.Networking
{
	public sealed class MultiplayerDiagnosticWindow
	{
		public const double IntervalSeconds = 5.0;

		public const int EventLimit = 64;

		public const int CriticalEventLimit = 8;

		public const int SampleLimit = 4;

		private double _windowStart = double.NegativeInfinity;

		private int _events;

		private int _criticalEvents;

		private int _samples;

		public int Suppressed { get; private set; }

		public int LongFrames { get; private set; }

		public double MaxFrameSeconds { get; private set; }

		public bool TryRecord(double now, bool critical = false)
		{
			AdvanceWindow(now);
			if (critical ? (_criticalEvents >= 8) : (_events >= 64))
			{
				Suppressed++;
				return false;
			}
			if (critical)
			{
				_criticalEvents++;
			}
			else
			{
				_events++;
			}
			return true;
		}

		public bool TryRecordSample(double now)
		{
			AdvanceWindow(now);
			if (_samples >= 4)
			{
				Suppressed++;
				return false;
			}
			_samples++;
			return true;
		}

		private void AdvanceWindow(double now)
		{
			if (!(now - _windowStart < 5.0))
			{
				_windowStart = now;
				_events = (_criticalEvents = (_samples = 0));
			}
		}

		public void ObserveWriteFailure()
		{
			Suppressed++;
		}

		public void ObserveFrame(double seconds)
		{
			if (!(seconds < 0.0) && !double.IsNaN(seconds) && !double.IsInfinity(seconds))
			{
				MaxFrameSeconds = Math.Max(MaxFrameSeconds, seconds);
				if (seconds >= 0.25)
				{
					LongFrames++;
				}
			}
		}

		public void ClearSample()
		{
			int suppressed = (LongFrames = 0);
			Suppressed = suppressed;
			MaxFrameSeconds = 0.0;
		}

		public static string SafeText(string value, int limit = 512)
		{
			if (string.IsNullOrEmpty(value))
			{
				return string.Empty;
			}
			value = value.Substring(0, Math.Min(value.Length, Math.Max(0, limit)));
			return value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ')
				.Replace('"', '\'')
				.Replace('<', '[')
				.Replace('>', ']');
		}
	}
}
