using System;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;

namespace PaperPixelGames.DevConsole
{
	public sealed class BeltCommand : IDevCommand
	{
		public string Name => "belt";

		public string Description => "belt full";

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			string[] array = DevCommandArgs.Clean(args);
			if (array.Length != 2 || !string.Equals(array[1], "full", StringComparison.OrdinalIgnoreCase))
			{
				feedback?.Invoke(Description);
				return;
			}
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (restaurantManager == null)
			{
				feedback?.Invoke("Restaurant manager not found.");
				return;
			}
			restaurantManager.ClientTryDebugFillConveyorBelt(out var message);
			feedback?.Invoke(message);
		}
	}
}
