using System;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Tablet;
using Unity.Netcode;

namespace PaperPixelGames.DevConsole
{
	public sealed class FishBookCommand : IDevCommand
	{
		public string Name => "fishbook";

		public string Description => "fishbook test";

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			if (args == null || args.Length != 2 || !string.Equals(args[1], "test", StringComparison.OrdinalIgnoreCase))
			{
				feedback?.Invoke(Description);
				return;
			}
			if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
			{
				feedback?.Invoke("Only the server can execute 'fishbook test'.");
				return;
			}
			if (!(context?.ItemDatabase?.MackerelSO is FishSO fishSO))
			{
				feedback?.Invoke("Mackerel FishSO is missing from ItemDatabase.");
				return;
			}
			TabletNetworkHandler instance = TabletNetworkHandler.Instance;
			if (instance == null)
			{
				feedback?.Invoke("TabletNetworkHandler is unavailable.");
				return;
			}
			if (!instance.ServerDebugPrepareFishBookExcept(fishSO.Id, out var unlockedFishCount))
			{
				feedback?.Invoke("FishBook test state could not be prepared.");
				return;
			}
			feedback?.Invoke($"FishBook test state prepared. Unlocked={unlockedFishCount}. " + "Catch " + fishSO.Name + " (" + fishSO.Id + ") normally to test ACH_NO_MORE_QUESTION_MARKS.");
		}
	}
}
