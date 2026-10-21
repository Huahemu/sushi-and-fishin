using System;
using PaperPixelGames.Core.Boss;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.DevConsole
{
	public sealed class BossCommand : IDevCommand
	{
		public string Name => "boss";

		public string Description => "boss defeat";

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			if (args == null || args.Length != 2 || !string.Equals(args[1], "defeat", StringComparison.OrdinalIgnoreCase))
			{
				feedback(Description);
				return;
			}
			if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
			{
				feedback("Only the server can execute 'boss defeat'.");
				return;
			}
			CrabBossController crabBossController = UnityEngine.Object.FindFirstObjectByType<CrabBossController>();
			if (crabBossController == null)
			{
				feedback("Crab boss not found in the loaded scene.");
			}
			else if (Singleton<PlayersManager>.Instance == null || Singleton<PlayersManager>.Instance.LocalPlayer == null)
			{
				feedback("Local player is unavailable. Boss defeat was not triggered.");
			}
			else
			{
				feedback(crabBossController.ServerDebugDefeat(Singleton<PlayersManager>.Instance.LocalPlayer) ? "Crab boss defeated. Reward flow was triggered." : "Crab boss could not be defeated (already defeated or not server-owned).");
			}
		}
	}
}
