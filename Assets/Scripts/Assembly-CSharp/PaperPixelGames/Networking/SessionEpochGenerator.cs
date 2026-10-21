using System;
using System.Threading;

namespace PaperPixelGames.Networking
{
	public static class SessionEpochGenerator
	{
		private static long _sequence;

		public static ulong Next(ulong localSteamId)
		{
			long ticks = DateTime.UtcNow.Ticks;
			ulong num = (ulong)Interlocked.Increment(ref _sequence);
			ulong num2 = (ulong)(ticks ^ ((long)localSteamId * -7046029254386353131L)) ^ num;
			if (num2 != 0L)
			{
				return num2;
			}
			return 1uL;
		}
	}
}
