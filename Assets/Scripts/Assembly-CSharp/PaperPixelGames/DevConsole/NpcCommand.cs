using System;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Framework.Patterns;

namespace PaperPixelGames.DevConsole
{
	public sealed class NpcCommand : IDevCommand
	{
		public string Name => "npc";

		public string Description => "npc speed | npc speed up";

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			if (args == null || args.Length < 2 || !Is(args[1], "speed"))
			{
				feedback?.Invoke(Description);
				return;
			}
			float num = 1f;
			if (args.Length >= 3 && Is(args[2], "up"))
			{
				num = 2f;
			}
			if (Singleton<ResidentManager>.Instance == null)
			{
				feedback?.Invoke("ResidentManager not found.");
				return;
			}
			Singleton<ResidentManager>.Instance.SetDebugSpeedMultiplier(num);
			feedback?.Invoke((num > 1f) ? $"NPC speed multiplier set to {num:0.##}x." : "NPC speed multiplier reset to default.");
		}

		private static bool Is(string a, string b)
		{
			return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
		}
	}
}
