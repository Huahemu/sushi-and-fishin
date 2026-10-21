using System;
using System.Linq;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;

namespace PaperPixelGames.DevConsole
{
	public sealed class TimeCommand : IDevCommand
	{
		public string Name => "time";

		public string Description => "time <hour> [minute] [checkFishSpawn] | time 0 | time 1 | time freeze | time continue\nExamples: time 13 30, time 21, time 10 30 false, time 0, time 1";

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			args = args?.Where((string x) => !string.IsNullOrWhiteSpace(x)).ToArray();
			if (args == null || args.Length == 0)
			{
				feedback?.Invoke(Description);
				return;
			}
			if (string.Equals(args[0], "tuto", StringComparison.OrdinalIgnoreCase))
			{
				args = args.Skip(1).ToArray();
			}
			if (args.Length != 0 && string.Equals(args[0], Name, StringComparison.OrdinalIgnoreCase))
			{
				args = args.Skip(1).ToArray();
			}
			if (args.Length == 0)
			{
				feedback?.Invoke(Description);
				return;
			}
			GameProgressManager instance = Singleton<GameProgressManager>.Instance;
			if (instance == null)
			{
				feedback?.Invoke("[time] GameProgressManager.Instance not found.");
				return;
			}
			CozyWeatherManager cozyWeatherManager = instance.CozyWeatherManager;
			if (cozyWeatherManager == null)
			{
				feedback?.Invoke("[time] CozyWeatherManager not found on GameProgressManager.");
				return;
			}
			string value = args[0].ToLowerInvariant();
			if (args.Length == 1 && IsPauseShortcut(value, out var pause))
			{
				cozyWeatherManager.ServerRequestSetPauseTimeRpc(pause);
				feedback?.Invoke(pause ? "[time] Time frozen." : "[time] Time continued.");
				return;
			}
			if (!int.TryParse(args[0], out var result))
			{
				feedback?.Invoke("[time] Invalid usage. Example: time 13 30");
				return;
			}
			int result2 = 0;
			if (args.Length >= 2 && !int.TryParse(args[1], out result2))
			{
				feedback?.Invoke("[time] Invalid minute. Example: time 13 30");
				return;
			}
			bool result3 = true;
			if (args.Length >= 3 && !bool.TryParse(args[2], out result3))
			{
				feedback?.Invoke("[time] Invalid checkFishSpawn. Use true/false. Example: time 10 30 false");
				return;
			}
			if (result < 0 || result > 23)
			{
				feedback?.Invoke("[time] Hour must be 0..23.");
				return;
			}
			if (result2 < 0 || result2 > 59)
			{
				feedback?.Invoke("[time] Minute must be 0..59.");
				return;
			}
			cozyWeatherManager.ServerRequestChangeTimeRpc(result, result2, result3);
			feedback?.Invoke($"[time] Requested time change: {result:00}:{result2:00} (checkFishSpawn={result3}).");
		}

		private static bool IsPauseShortcut(string value, out bool pause)
		{
			switch (value)
			{
			case "0":
			case "freeze":
			case "pause":
			case "paused":
				pause = true;
				return true;
			case "1":
			case "start":
			case "continue":
			case "continues":
			case "resume":
			case "unpause":
			case "unpaused":
				pause = false;
				return true;
			default:
				pause = false;
				return false;
			}
		}
	}
}
