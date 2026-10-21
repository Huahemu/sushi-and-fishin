namespace PaperPixelGames.Networking
{
	public static class MultiplayerFaultInjection
	{
		public static bool IsEnabled => false;

		public static int SteamDelayMilliseconds => 0;

		public static void Configure(int steamDelayMilliseconds = 0, bool dropNextSteamCompletion = false, bool forceNextTransportDisconnect = false, bool forceNextBootstrapFailure = false, bool forceNextClientStartFailure = false)
		{
		}

		public static bool ConsumeDroppedSteamCompletion()
		{
			return false;
		}

		public static bool ConsumeForcedTransportDisconnect()
		{
			return false;
		}

		public static bool ConsumeForcedBootstrapFailure()
		{
			return false;
		}

		public static bool ConsumeForcedClientStartFailure()
		{
			return false;
		}

		public static bool HasClientStartFailureArgument(string[] arguments)
		{
			return false;
		}

		public static void Reset()
		{
		}
	}
}
