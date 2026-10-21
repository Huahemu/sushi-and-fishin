using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;

namespace PaperPixelGames.DevConsole
{
	public sealed class TrophyCommand : IDevCommand
	{
		public string Name => "trophy";

		public string Description => "trophy <CreatureItems index>";

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			string[] array = DevCommandArgs.Clean(args);
			if (array.Length != 2 || !int.TryParse(array[1], out var result))
			{
				feedback?.Invoke(Description);
				return;
			}
			if (context?.ItemDatabase == null)
			{
				feedback?.Invoke("ItemDatabase is missing on DevConsole.");
				return;
			}
			List<ItemSO> creatureItems = context.ItemDatabase.CreatureItems;
			if (creatureItems == null || result < 0 || result >= creatureItems.Count)
			{
				int num = ((creatureItems == null) ? (-1) : (creatureItems.Count - 1));
				feedback?.Invoke($"Creature index is out of range. Valid range: 0..{num}");
				return;
			}
			if (!(creatureItems[result] is FishSO fishSO))
			{
				feedback?.Invoke($"CreatureItems[{result}] is not a FishSO.");
				return;
			}
			if (!fishSO.CanDisplayAsTrophy || fishSO.TrophyGraphicPrefab == null)
			{
				feedback?.Invoke($"CreatureItems[{result}] {fishSO.Name} cannot be displayed as a trophy.");
				return;
			}
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (restaurantManager?.FishTrophy == null)
			{
				feedback?.Invoke("Fish trophy is not available.");
				return;
			}
			restaurantManager.FishTrophy.ClientTrySetDebugFish(fishSO, result, out var message);
			feedback?.Invoke(message);
		}
	}
}
