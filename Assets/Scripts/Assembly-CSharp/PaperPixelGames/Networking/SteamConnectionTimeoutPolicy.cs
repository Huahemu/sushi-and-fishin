using System;

namespace PaperPixelGames.Networking
{
	public static class SteamConnectionTimeoutPolicy
	{
		public const int ConnectedTimeoutMilliseconds = 60000;

		public static int InitialTimeoutMilliseconds(double seconds)
		{
			if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0.0)
			{
				seconds = 120.0;
			}
			return (int)Math.Min(2147483647.0, Math.Ceiling(seconds * 1000.0));
		}

		public static bool Apply(double transportSeconds, Func<int> readInitial, Action<int> writeInitial, Func<int> readConnected, Action<int> writeConnected)
		{
			int num = Math.Max(readInitial(), InitialTimeoutMilliseconds(transportSeconds));
			int num2 = Math.Max(readConnected(), 60000);
			writeInitial(num);
			writeConnected(num2);
			if (readInitial() >= num)
			{
				return readConnected() >= num2;
			}
			return false;
		}
	}
}
