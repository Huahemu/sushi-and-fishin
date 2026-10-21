using System;
using System.Linq;

namespace PaperPixelGames.DevConsole
{
	internal static class DevCommandArgs
	{
		public static string[] Clean(string[] args)
		{
			return args?.Where((string x) => !string.IsNullOrWhiteSpace(x)).ToArray() ?? Array.Empty<string>();
		}
	}
}
