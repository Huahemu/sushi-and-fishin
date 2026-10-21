using System;
using System.Text;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Framework.Patterns;

namespace PaperPixelGames.DevConsole
{
	public sealed class ProgressionCommand : IDevCommand
	{
		public string Name => "progression";

		public string Description => "progression status | progression xp <amount> | progression source <source> [contextId]";

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			ProgressionManager progressionManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.ProgressionManager : null);
			if (progressionManager == null)
			{
				feedback?.Invoke("ProgressionManager not found.");
			}
			else if (args == null || args.Length <= 1 || Is(args[1], "status"))
			{
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.AppendLine("Progression Status");
				stringBuilder.AppendLine($"  Level: {progressionManager.CurrentLevel}");
				stringBuilder.AppendLine($"  Current XP: {progressionManager.CurrentXp}");
				stringBuilder.AppendLine($"  Total XP: {progressionManager.TotalXp}");
				stringBuilder.AppendLine($"  Next Level Total XP: {progressionManager.NextLevelRequiredTotalXp}");
				feedback?.Invoke(stringBuilder.ToString());
			}
			else if (Is(args[1], "xp"))
			{
				if (args.Length < 3 || !int.TryParse(args[2], out var result))
				{
					feedback?.Invoke("Usage: progression xp <amount>");
					return;
				}
				bool flag = progressionManager.ServerGrantXp(ProgressionXpSource.None, result);
				feedback?.Invoke(flag ? $"Added {result} progression XP." : "XP add failed.");
			}
			else if (Is(args[1], "source"))
			{
				if (args.Length < 3 || !Enum.TryParse<ProgressionXpSource>(args[2], ignoreCase: true, out var result2))
				{
					feedback?.Invoke("Usage: progression source <source> [contextId]");
					return;
				}
				string contextId = ((args.Length >= 4) ? args[3] : null);
				bool flag2 = progressionManager.ServerGrantXp(result2, contextId);
				feedback?.Invoke(flag2 ? $"Granted XP for {result2}." : $"No XP granted for {result2}.");
			}
			else
			{
				feedback?.Invoke(Description);
			}
		}

		private static bool Is(string a, string b)
		{
			return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
		}
	}
}
