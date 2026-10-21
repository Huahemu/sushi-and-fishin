using System;
using System.Linq;
using System.Text;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;

namespace PaperPixelGames.DevConsole
{
	public sealed class GetCommand : IDevCommand
	{
		public string Name => "get";

		public string Description => "\nGet Commands\n  get tool\n  get tools\n  get quest\n  get tuto\n  get item count <type/all>\n  get spawn count <type/all>\n  get fish count\n\nTypes\n  w = Wholesale\n  r = RawIngredient\n  p = ProcessedIngredient\n  s = ServedDish\n  d = Decoration\n  all = All spawnable item types\n";

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			string[] array = NormalizeArgs(args);
			if (array.Length == 0)
			{
				feedback?.Invoke(Description);
				return;
			}
			string text = array[0].ToLowerInvariant();
			switch (text)
			{
			case "tool":
			case "tools":
				ExecuteToolStatus(feedback);
				break;
			case "quest":
			case "tuto":
			case "tutorial":
				ExecuteQuestStatus(feedback);
				break;
			case "item":
			case "items":
			case "spawn":
				ExecuteItemInfo(context, array, feedback);
				break;
			case "fish":
			case "fishes":
				ExecuteFishInfo(context, array, feedback);
				break;
			case "help":
				feedback?.Invoke(Description);
				break;
			default:
				feedback?.Invoke("Unknown get command: '" + text + "'\n" + Description);
				break;
			}
		}

		private string[] NormalizeArgs(string[] args)
		{
			if (args == null)
			{
				return Array.Empty<string>();
			}
			string[] array = args.Where((string x) => !string.IsNullOrWhiteSpace(x)).ToArray();
			if (array.Length != 0 && string.Equals(array[0], Name, StringComparison.OrdinalIgnoreCase))
			{
				array = array.Skip(1).ToArray();
			}
			return array;
		}

		private void ExecuteToolStatus(Action<string> feedback)
		{
			if (!TryGetLocalPlayer(out var player, out var error))
			{
				feedback?.Invoke(error);
				return;
			}
			ToolDataNet toolsData = player.ToolsData;
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Tool Status");
			stringBuilder.AppendLine($"  Revision: {toolsData.SyncRevision}");
			stringBuilder.AppendLine($"  Harpoon         | unlocked: {toolsData.HarpoonUnlocked} | level: {toolsData.HarpoonLevel}");
			stringBuilder.AppendLine($"  FishingRod      | unlocked: {toolsData.FishingRodUnlocked} | level: {toolsData.FishingRodLevel}");
			stringBuilder.AppendLine($"  Mop             | unlocked: {toolsData.MopUnlocked} | level: {toolsData.MopLevel}");
			stringBuilder.AppendLine($"  Compass         | unlocked: {toolsData.CompassUnlocked} | level: {toolsData.CompassLevel}");
			stringBuilder.AppendLine($"  Scooter         | unlocked: {toolsData.ScooterUnlocked} | level: {toolsData.ScooterLevel}");
			feedback?.Invoke(stringBuilder.ToString());
		}

		private void ExecuteQuestStatus(Action<string> feedback)
		{
			QuestManager instance = QuestManager.Instance;
			if (instance == null)
			{
				feedback?.Invoke("QuestManager not found.");
			}
			else
			{
				feedback?.Invoke(instance.DebugBuildCurrentQuestStatus());
			}
		}

		private void ExecuteItemInfo(DevConsoleContext context, string[] tokens, Action<string> feedback)
		{
			if (context == null)
			{
				feedback?.Invoke("DevConsoleContext is missing.");
				return;
			}
			if (context.ItemDatabase == null)
			{
				feedback?.Invoke("ItemDatabase is missing on DevConsole.");
				return;
			}
			if (tokens.Length < 2)
			{
				feedback?.Invoke("Usage: get item count <type/all>");
				return;
			}
			if (tokens[1].Trim().ToLowerInvariant() != "count")
			{
				feedback?.Invoke("Usage: get item count <type/all>");
				return;
			}
			if (tokens.Length < 3)
			{
				feedback?.Invoke("Usage: get item count <type/all>");
				return;
			}
			string text = tokens[2].Trim().ToLowerInvariant();
			if (text == "all")
			{
				feedback?.Invoke(BuildAllItemCounts(context.ItemDatabase));
				return;
			}
			if (!TryParseTypeLetter(text, out var type))
			{
				feedback?.Invoke("Invalid type. Use w, r, p, s, d, or all.");
				return;
			}
			int num = context.ItemDatabase.GetItemsByItemType(type).ToList()?.Count ?? 0;
			feedback?.Invoke($"{type} count: {num}");
		}

		private void ExecuteFishInfo(DevConsoleContext context, string[] tokens, Action<string> feedback)
		{
			if (context == null)
			{
				feedback?.Invoke("DevConsoleContext is missing.");
				return;
			}
			if (context.ItemDatabase == null)
			{
				feedback?.Invoke("ItemDatabase is missing on DevConsole.");
				return;
			}
			if (tokens.Length >= 2 && tokens[1].Trim().ToLowerInvariant() != "count")
			{
				feedback?.Invoke("Usage: get fish count");
				return;
			}
			int num = context.ItemDatabase.CreatureItems?.Count ?? 0;
			int num2 = 0;
			if (context.ItemDatabase.CreatureItems != null)
			{
				foreach (ItemSO creatureItem in context.ItemDatabase.CreatureItems)
				{
					if (creatureItem is FishSO)
					{
						num2++;
					}
				}
			}
			feedback?.Invoke($"Fish count: {num2} / CreatureItems total: {num}");
		}

		private string BuildAllItemCounts(ItemDatabaseSO db)
		{
			StringBuilder stringBuilder = new StringBuilder();
			AppendItemCount(stringBuilder, db, ItemType.Wholesale);
			AppendItemCount(stringBuilder, db, ItemType.RawIngredient);
			AppendItemCount(stringBuilder, db, ItemType.ProcessedIngredient);
			AppendItemCount(stringBuilder, db, ItemType.ServedDish);
			AppendItemCount(stringBuilder, db, ItemType.Decoration);
			return stringBuilder.ToString();
		}

		private void AppendItemCount(StringBuilder sb, ItemDatabaseSO db, ItemType type)
		{
			int num = db.GetItemsByItemType(type)?.Count ?? 0;
			sb.AppendLine($"{type}: {num}");
		}

		private bool TryParseTypeLetter(string token, out ItemType type)
		{
			type = ItemType.Empty;
			if (string.IsNullOrWhiteSpace(token))
			{
				return false;
			}
			switch (token.Trim().ToLowerInvariant())
			{
			case "w":
			case "wholesale":
				type = ItemType.Wholesale;
				return true;
			case "r":
			case "raw":
			case "rawingredient":
				type = ItemType.RawIngredient;
				return true;
			case "p":
			case "processed":
			case "processedingredient":
				type = ItemType.ProcessedIngredient;
				return true;
			case "s":
			case "served":
			case "serveddish":
				type = ItemType.ServedDish;
				return true;
			case "d":
			case "decoration":
				type = ItemType.Decoration;
				return true;
			default:
				return false;
			}
		}

		private bool TryGetLocalPlayer(out PPG_Player player, out string error)
		{
			player = null;
			error = null;
			PlayersManager instance = Singleton<PlayersManager>.Instance;
			if (instance == null)
			{
				error = "PlayersManager not found.";
				return false;
			}
			player = instance.LocalPlayer;
			if (player == null)
			{
				error = "LocalPlayer not found.";
				return false;
			}
			return true;
		}
	}
}
