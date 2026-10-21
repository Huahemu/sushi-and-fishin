using System;
using System.Globalization;

namespace PaperPixelGames.Core.NetworkDevelopment
{
	public static class BootstrapDevelopmentDelay
	{
		public static double InitialSeconds => 0.0;

		public static double Parse(string[] arguments)
		{
			if (arguments == null)
			{
				return 0.0;
			}
			for (int i = 0; i + 1 < arguments.Length; i++)
			{
				if (arguments[i] == "-ppgInitialBootstrapDelaySeconds" && double.TryParse(arguments[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && !double.IsNaN(result) && !double.IsInfinity(result))
				{
					return Math.Max(0.0, Math.Min(180.0, result));
				}
			}
			return 0.0;
		}
	}
}
