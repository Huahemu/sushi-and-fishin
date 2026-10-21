using System;
using PaperPixelGames.Core.Kitchen.ChefDesk;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Patterns;

namespace PaperPixelGames.DevConsole
{
	public sealed class ChefDeskCommand : IDevCommand
	{
		public string Name => "chef";

		public string Description => "chef fill";

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			string[] array = DevCommandArgs.Clean(args);
			if (array.Length != 2 || !string.Equals(array[1], "fill", StringComparison.OrdinalIgnoreCase))
			{
				feedback?.Invoke(Description);
				return;
			}
			PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
			if (pPG_Player == null)
			{
				feedback?.Invoke("Local player not found.");
				return;
			}
			if (!(pPG_Player.Interactor.CurrentInUse is ChefDesk chefDesk))
			{
				feedback?.Invoke("You must be using a chef desk.");
				return;
			}
			chefDesk.ClientTryDebugFillSelectedRecipeGastroPans(out var message);
			feedback?.Invoke(message);
		}
	}
}
