using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Economy;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.DevConsole
{
	public sealed class SetCommand : IDevCommand
	{
		private enum ToolKey
		{
			DivingEquipment = 0,
			Harpoon = 1,
			FishingRod = 2,
			Mop = 3,
			Compass = 4,
			Scooter = 5
		}

		private readonly List<Canvas> _hiddenCanvases = new List<Canvas>();

		public string Name => "set";

		public string Description => "\nSet Commands\n  set pos <x> <y> <z> [updateGround]\n  set tp <x> <y> <z> [updateGround]\n  set tp <special_keyword>\n  set spawn <id>\n  set spawn <index>\n  set spawn <type> <index>\n  set spawn deadfish <fishIndex>\n  set money <+/-/add/spend> <amount>\n  set scrap <+/-/add/spend> <amount>\n  set oxygen <+/-/add/consume> <amount>\n  set oxygen loss <true/false>\n  set ui <true/false>\n  set time <hour> [minute] [checkFishSpawn]\n  set quest complete\n  set group complete\n  set tool <name> unlock [true/false]\n  set tool <name> lock\n  set tool <name> level <0-4>\n  set tool <name> loss <true/false>\n  set tool off\n\nTypes\n  w = Wholesale\n  r = RawIngredient\n  p = ProcessedIngredient\n  s = ServedDish\n  d = Decoration\n";

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
			case "pos":
			case "tp":
			case "teleport":
				ExecuteTeleport(array, feedback);
				break;
			case "spawn":
				ExecuteSpawn(context, array, feedback);
				break;
			case "money":
			case "cash":
				ExecuteCurrency("money", array, feedback);
				break;
			case "scrap":
				ExecuteCurrency("scrap", array, feedback);
				break;
			case "balance":
				ExecuteBalanceAlias(array, feedback);
				break;
			case "oxygen":
			case "o2":
				ExecuteOxygen(array, feedback);
				break;
			case "ui":
				ExecuteUi(array, feedback);
				break;
			case "time":
				ExecuteTime(array, feedback);
				break;
			case "quest":
				ExecuteQuest(array, feedback);
				break;
			case "group":
				ExecuteGroup(array, feedback);
				break;
			case "tool":
				ExecuteTool(array, feedback);
				break;
			default:
				feedback?.Invoke("Unknown set command: '" + text + "'\n" + Description);
				break;
			}
		}

		private void ExecuteUi(string[] tokens, Action<string> feedback)
		{
			if (tokens.Length < 2 || !TryParseToggle(tokens[1], out var value))
			{
				feedback?.Invoke("Usage: set ui <on/off>");
			}
			else if (value)
			{
				int num = RestoreHiddenCanvases();
				Singleton<HintsManager>.Instance?.SetVisibilitySuppressed(suppressed: false);
				feedback?.Invoke($"UI restored. Canvas count: {num}");
			}
			else
			{
				Singleton<HintsManager>.Instance?.SetVisibilitySuppressed(suppressed: true);
				int num2 = HideActiveCanvases();
				feedback?.Invoke($"UI hidden. Canvas count: {num2}");
			}
		}

		private int HideActiveCanvases()
		{
			RestoreHiddenCanvases();
			Canvas[] array = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
			foreach (Canvas canvas in array)
			{
				if (!(canvas == null) && canvas.enabled && canvas.gameObject.activeInHierarchy && !(canvas.GetComponentInParent<DevConsole>(includeInactive: true) != null))
				{
					_hiddenCanvases.Add(canvas);
					canvas.enabled = false;
				}
			}
			return _hiddenCanvases.Count;
		}

		private int RestoreHiddenCanvases()
		{
			int num = 0;
			for (int num2 = _hiddenCanvases.Count - 1; num2 >= 0; num2--)
			{
				Canvas canvas = _hiddenCanvases[num2];
				if (canvas == null)
				{
					_hiddenCanvases.RemoveAt(num2);
				}
				else
				{
					canvas.enabled = true;
					num++;
					_hiddenCanvases.RemoveAt(num2);
				}
			}
			return num;
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

		private void ExecuteTeleport(string[] tokens, Action<string> feedback)
		{
			if (tokens.Length < 2)
			{
				feedback?.Invoke("Usage: set pos <x> <y> <z> [updateGround] | set tp <special_keyword>");
				return;
			}
			bool result = true;
			Vector3 value;
			if (tokens.Length == 2)
			{
				if (!SpecialTeleportTargets.TryGetTarget(tokens[1], out var target))
				{
					feedback?.Invoke("Unknown teleport keyword: " + tokens[1] + ". Available: " + SpecialTeleportTargets.KeywordList);
				}
				else
				{
					TeleportLocalPlayer(target.Position, result, feedback, tokens[1], target.LookAt);
				}
			}
			else if (tokens.Length < 4)
			{
				feedback?.Invoke("Usage: set pos <x> <y> <z> [updateGround] | set tp <special_keyword>");
			}
			else if (!TryParseVector3(tokens, 1, out value))
			{
				feedback?.Invoke("Invalid position values. Example: set pos 10 2 -5");
			}
			else if (tokens.Length >= 5 && !bool.TryParse(tokens[4], out result))
			{
				feedback?.Invoke("Invalid updateGround. Use true / false.");
			}
			else
			{
				TeleportLocalPlayer(value, result, feedback);
			}
		}

		private void TeleportLocalPlayer(Vector3 position, bool updateGround, Action<string> feedback, string keyword = null, Vector3? lookAt = null)
		{
			if (!TryGetLocalPlayer(out var player, out var error))
			{
				feedback?.Invoke(error);
				return;
			}
			player.TeleportPlayer(position, updateGround);
			if (lookAt.HasValue && player.FirstPersonCharacter != null)
			{
				player.FirstPersonCharacter.LookAtInstant(lookAt.Value);
			}
			if (string.IsNullOrWhiteSpace(keyword))
			{
				feedback?.Invoke($"Teleported to {position} (updateGround: {updateGround}).");
			}
			else if (lookAt.HasValue)
			{
				feedback?.Invoke($"Teleported to {keyword}: {position} lookAt: {lookAt.Value} (updateGround: {updateGround}).");
			}
			else
			{
				feedback?.Invoke($"Teleported to {keyword}: {position} (updateGround: {updateGround}).");
			}
		}

		private void ExecuteSpawn(DevConsoleContext context, string[] tokens, Action<string> feedback)
		{
			if (!RequireServer(feedback, "spawn"))
			{
				return;
			}
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
				feedback?.Invoke("Usage: set spawn <id/index/type> [index]");
				return;
			}
			ItemDatabaseSO itemDatabase = context.ItemDatabase;
			string text = tokens[1].Trim();
			ItemType type;
			int result3;
			if (IsDeadFishToken(text))
			{
				if (tokens.Length < 3 || !int.TryParse(tokens[2], out var result))
				{
					feedback?.Invoke("Usage: set spawn deadfish <fishIndex>");
				}
				else
				{
					TrySpawnDeadFish(context, result, feedback);
				}
			}
			else if (tokens.Length >= 3 && TryParseTypeLetter(text, out type))
			{
				if (!int.TryParse(tokens[2], out var result2))
				{
					feedback?.Invoke("Invalid index. Example: set spawn w 0");
					return;
				}
				List<ItemSO> list = itemDatabase.GetItemsByItemType(type).ToList();
				if (list == null || list.Count == 0)
				{
					feedback?.Invoke($"No items found for type: {type}");
				}
				else if (result2 < 0 || result2 >= list.Count)
				{
					feedback?.Invoke($"Index out of range. Valid range: 0..{list.Count - 1}");
				}
				else
				{
					TrySpawnItem(list[result2], feedback);
				}
			}
			else if (int.TryParse(text, out result3))
			{
				List<ItemSO> list2 = BuildAutoSpawnList(itemDatabase);
				if (list2.Count == 0)
				{
					feedback?.Invoke("Auto spawn list is empty.");
				}
				else if (result3 < 0 || result3 >= list2.Count)
				{
					feedback?.Invoke($"Index out of range. Valid range: 0..{list2.Count - 1}");
				}
				else
				{
					TrySpawnItem(list2[result3], feedback);
				}
			}
			else
			{
				ItemSO itemSO = itemDatabase.FindById(text);
				if (itemSO == null)
				{
					feedback?.Invoke("Item not found by id: " + text);
				}
				else
				{
					TrySpawnItem(itemSO, feedback);
				}
			}
		}

		private void ExecuteCurrency(string currency, string[] tokens, Action<string> feedback)
		{
			if (!RequireServer(feedback, currency))
			{
				return;
			}
			if (tokens.Length < 3)
			{
				feedback?.Invoke("Usage: set " + currency + " <+/-/add/spend> <amount>");
				return;
			}
			GameProgressManager instance = Singleton<GameProgressManager>.Instance;
			if (instance == null)
			{
				feedback?.Invoke("GameProgressManager not found.");
				return;
			}
			EconomyHandler economyHandler = instance.EconomyHandler;
			if (economyHandler == null)
			{
				feedback?.Invoke("EconomyHandler not found.");
				return;
			}
			string text = tokens[1].Trim().ToLowerInvariant();
			if (!int.TryParse(tokens[2], out var result) || result <= 0)
			{
				feedback?.Invoke("Invalid amount. Must be a positive integer.");
			}
			else if (currency == "money")
			{
				if (IsAddOp(text))
				{
					economyHandler.EarnMoney(result, ulong.MaxValue);
					feedback?.Invoke($"Money earned: +{result}");
				}
				else if (IsSpendOp(text))
				{
					economyHandler.SpendMoney(result);
					feedback?.Invoke($"Money spent: -{result}");
				}
				else
				{
					feedback?.Invoke("Unknown money operation: '" + text + "'");
				}
			}
			else if (currency == "scrap")
			{
				if (IsAddOp(text))
				{
					economyHandler.EarnScrap(result, ulong.MaxValue);
					feedback?.Invoke($"Scrap earned: +{result}");
				}
				else if (IsSpendOp(text))
				{
					economyHandler.SpendScrap(result);
					feedback?.Invoke($"Scrap spent: -{result}");
				}
				else
				{
					feedback?.Invoke("Unknown scrap operation: '" + text + "'");
				}
			}
		}

		private void ExecuteBalanceAlias(string[] tokens, Action<string> feedback)
		{
			if (tokens.Length < 4)
			{
				feedback?.Invoke("Usage: set balance <money/scrap> <+/-/add/spend> <amount>");
				return;
			}
			string text = tokens[1].ToLowerInvariant();
			switch (text)
			{
			default:
				feedback?.Invoke("Unknown currency. Use money or scrap.");
				break;
			case "money":
			case "m":
			case "scrap":
			case "s":
			{
				string text2 = ((text == "money" || text == "m") ? "money" : "scrap");
				string[] tokens2 = new string[3]
				{
					text2,
					tokens[2],
					tokens[3]
				};
				ExecuteCurrency(text2, tokens2, feedback);
				break;
			}
			}
		}

		private void ExecuteOxygen(string[] tokens, Action<string> feedback)
		{
			if (tokens.Length < 2)
			{
				feedback?.Invoke("Usage: set oxygen <+/-/add/consume/loss> <amount/true/false>");
				return;
			}
			if (!TryGetLocalPlayer(out var player, out var error))
			{
				feedback?.Invoke(error);
				return;
			}
			if (player.DivingEquipment == null)
			{
				feedback?.Invoke("LocalPlayer DivingEquipment not found.");
				return;
			}
			string text = tokens[1].Trim().ToLowerInvariant();
			if (text == "loss")
			{
				if (tokens.Length < 3 || !bool.TryParse(tokens[2], out var result))
				{
					feedback?.Invoke("Usage: set oxygen loss <true/false>");
					return;
				}
				player.DivingEquipment.SetDebugPreventOxygenLoss(result);
				feedback?.Invoke($"DivingEquipment PreventOxygenLoss set to {result}.");
				return;
			}
			if (tokens.Length < 3)
			{
				feedback?.Invoke("Usage: set oxygen <+/-/add/consume> <amount>");
				return;
			}
			if (!TryParseByte(tokens[2], out var result2))
			{
				feedback?.Invoke("Invalid oxygen amount. Must be 1..255.");
				return;
			}
			switch (text)
			{
			case "+":
			case "add":
			case "apply":
			case "boost":
				player.DivingEquipment.ApplyOxygen(result2);
				feedback?.Invoke($"Oxygen applied: +{result2}");
				break;
			case "-":
			case "consume":
			case "use":
				player.DivingEquipment.ConsumeOxygen(result2, withFeedback: true);
				feedback?.Invoke($"Oxygen consumed: -{result2}");
				break;
			default:
				feedback?.Invoke("Unknown oxygen operation: '" + text + "'");
				break;
			}
		}

		private void ExecuteTime(string[] tokens, Action<string> feedback)
		{
			GameProgressManager instance = Singleton<GameProgressManager>.Instance;
			if (instance == null)
			{
				feedback?.Invoke("GameProgressManager not found.");
				return;
			}
			CozyWeatherManager cozyWeatherManager = instance.CozyWeatherManager;
			if (cozyWeatherManager == null)
			{
				feedback?.Invoke("CozyWeatherManager not found on GameProgressManager.");
				return;
			}
			if (tokens.Length < 2 || !int.TryParse(tokens[1], out var result))
			{
				feedback?.Invoke("Usage: set time <hour> [minute] [checkFishSpawn]");
				return;
			}
			int result2 = 0;
			if (tokens.Length >= 3 && !int.TryParse(tokens[2], out result2))
			{
				feedback?.Invoke("Invalid minute. Example: set time 10 30");
				return;
			}
			bool result3 = true;
			if (tokens.Length >= 4 && !bool.TryParse(tokens[3], out result3))
			{
				feedback?.Invoke("Invalid checkFishSpawn. Use true / false.");
				return;
			}
			if (result < 0 || result > 23)
			{
				feedback?.Invoke("Hour must be 0..23.");
				return;
			}
			if (result2 < 0 || result2 > 59)
			{
				feedback?.Invoke("Minute must be 0..59.");
				return;
			}
			cozyWeatherManager.ServerRequestChangeTimeRpc(result, result2, result3);
			feedback?.Invoke($"Requested time change: {result:00}:{result2:00} (checkFishSpawn={result3}).");
		}

		private void ExecuteQuest(string[] tokens, Action<string> feedback)
		{
			if (tokens.Length < 2 || !string.Equals(tokens[1], "complete", StringComparison.OrdinalIgnoreCase))
			{
				feedback?.Invoke("Usage: set quest complete");
				return;
			}
			QuestManager instance = QuestManager.Instance;
			if (instance == null)
			{
				feedback?.Invoke("QuestManager not found.");
				return;
			}
			instance.DebugTryCompleteCurrentMainQuest(out var message);
			feedback?.Invoke(message);
		}

		private void ExecuteGroup(string[] tokens, Action<string> feedback)
		{
			if (tokens.Length < 2 || !string.Equals(tokens[1], "complete", StringComparison.OrdinalIgnoreCase))
			{
				feedback?.Invoke("Usage: set group complete");
				return;
			}
			QuestManager instance = QuestManager.Instance;
			if (instance == null)
			{
				feedback?.Invoke("QuestManager not found.");
				return;
			}
			instance.DebugTryCompleteCurrentMainGroup(out var message);
			feedback?.Invoke(message);
		}

		private void ExecuteTool(string[] tokens, Action<string> feedback)
		{
			if (!RequireServer(feedback, "tool"))
			{
				return;
			}
			if (tokens.Length < 2)
			{
				feedback?.Invoke("Usage: set tool <name> <operation>");
				return;
			}
			if (!TryGetLocalPlayer(out var player, out var error))
			{
				feedback?.Invoke(error);
				return;
			}
			if (tokens[1].Trim().ToLowerInvariant() == "off")
			{
				player.DisableActiveTool();
				feedback?.Invoke("Active tool disabled.");
				return;
			}
			if (tokens.Length < 3)
			{
				feedback?.Invoke("Usage: set tool <name> <unlock/lock/level/loss>");
				return;
			}
			if (!TryResolveTool(tokens[1], out var tool, out var label))
			{
				feedback?.Invoke("Unknown tool name: '" + tokens[1] + "'");
				return;
			}
			string text = tokens[2].Trim().ToLowerInvariant();
			switch (text)
			{
			case "unlock":
			case "unlocked":
			{
				bool result3 = true;
				if (tokens.Length >= 4 && !bool.TryParse(tokens[3], out result3))
				{
					feedback?.Invoke("Invalid unlock value. Use true / false.");
				}
				else if (!TrySetToolUnlocked(player, tool, result3, out error))
				{
					feedback?.Invoke(error);
				}
				else
				{
					feedback?.Invoke($"{label} unlock set to {result3}.");
				}
				break;
			}
			case "lock":
			case "locked":
				if (!TrySetToolUnlocked(player, tool, unlocked: false, out error))
				{
					feedback?.Invoke(error);
				}
				else
				{
					feedback?.Invoke(label + " locked.");
				}
				break;
			case "level":
			{
				byte result;
				if (tokens.Length < 4)
				{
					feedback?.Invoke("Usage: set tool <name> level <0-4>");
				}
				else if (!byte.TryParse(tokens[3], out result) || result > 4)
				{
					feedback?.Invoke("Invalid level. Use 0..4.");
				}
				else if (!TrySetToolLevel(player, tool, result, out error))
				{
					feedback?.Invoke(error);
				}
				else
				{
					feedback?.Invoke($"{label} level set to {result}.");
				}
				break;
			}
			case "loss":
			{
				if (tokens.Length < 4 || !bool.TryParse(tokens[3], out var result2))
				{
					feedback?.Invoke("Usage: set tool diving loss <true/false>");
					break;
				}
				if (tool != ToolKey.DivingEquipment)
				{
					feedback?.Invoke("loss is only supported for DivingEquipment.");
					break;
				}
				player.DivingEquipment.SetDebugPreventOxygenLoss(result2);
				feedback?.Invoke($"DivingEquipment PreventOxygenLoss set to {result2}.");
				break;
			}
			default:
				feedback?.Invoke("Unknown tool operation: '" + text + "'");
				break;
			}
		}

		private bool TrySetToolUnlocked(PPG_Player player, ToolKey tool, bool unlocked, out string error)
		{
			error = null;
			if (!TryGetRelay(player, out var relay, out error))
			{
				return false;
			}
			ToolDataNet value = relay.ToolDataNV.Value;
			switch (tool)
			{
			case ToolKey.Harpoon:
				value.HarpoonUnlocked = unlocked;
				break;
			case ToolKey.FishingRod:
				value.FishingRodUnlocked = unlocked;
				break;
			case ToolKey.Mop:
				value.MopUnlocked = unlocked;
				break;
			case ToolKey.Compass:
				value.CompassUnlocked = unlocked;
				break;
			case ToolKey.Scooter:
				value.ScooterUnlocked = unlocked;
				break;
			default:
				error = $"Unsupported tool: {tool}";
				return false;
			}
			value.SyncRevision++;
			relay.ToolDataNV.Value = value;
			return true;
		}

		private bool TrySetToolLevel(PPG_Player player, ToolKey tool, byte level, out string error)
		{
			error = null;
			if (!TryGetRelay(player, out var relay, out error))
			{
				return false;
			}
			ToolDataNet value = relay.ToolDataNV.Value;
			switch (tool)
			{
			case ToolKey.DivingEquipment:
				value.DivingEquipmentLevel = level;
				break;
			case ToolKey.Harpoon:
				value.HarpoonLevel = level;
				break;
			case ToolKey.FishingRod:
				value.FishingRodLevel = level;
				break;
			case ToolKey.Mop:
				value.MopLevel = level;
				break;
			case ToolKey.Compass:
				value.CompassLevel = level;
				break;
			case ToolKey.Scooter:
				value.ScooterLevel = level;
				break;
			default:
				error = $"Unsupported tool: {tool}";
				return false;
			}
			value.SyncRevision++;
			relay.ToolDataNV.Value = value;
			return true;
		}

		private bool TryGetRelay(PPG_Player player, out RpcRelay relay, out string error)
		{
			relay = null;
			error = null;
			if (player == null)
			{
				error = "Player is null.";
				return false;
			}
			FieldInfo field = typeof(PPG_Player).GetField("_relay", BindingFlags.Instance | BindingFlags.NonPublic);
			if (field == null)
			{
				error = "PPG_Player._relay field not found.";
				return false;
			}
			relay = field.GetValue(player) as RpcRelay;
			if (relay == null)
			{
				error = "PPG_Player._relay is null.";
				return false;
			}
			return true;
		}

		private bool TryResolveTool(string raw, out ToolKey tool, out string label)
		{
			tool = ToolKey.DivingEquipment;
			label = null;
			if (string.IsNullOrWhiteSpace(raw))
			{
				return false;
			}
			switch (raw.Trim().ToLowerInvariant())
			{
			case "diving":
			case "dive":
			case "oxygen":
			case "divingequipment":
				tool = ToolKey.DivingEquipment;
				label = "DivingEquipment";
				return true;
			case "harpoon":
			case "spear":
				tool = ToolKey.Harpoon;
				label = "Harpoon";
				return true;
			case "rod":
			case "fishing":
			case "fishingrod":
				tool = ToolKey.FishingRod;
				label = "FishingRod";
				return true;
			case "mop":
				tool = ToolKey.Mop;
				label = "Mop";
				return true;
			case "compass":
				tool = ToolKey.Compass;
				label = "Compass";
				return true;
			case "scooter":
			case "seascooter":
				tool = ToolKey.Scooter;
				label = "Scooter";
				return true;
			default:
				return false;
			}
		}

		private bool RequireServer(Action<string> feedback, string commandName)
		{
			if (NetworkManager.Singleton == null)
			{
				feedback?.Invoke("NetworkManager not found.");
				return false;
			}
			if (!NetworkManager.Singleton.IsServer)
			{
				feedback?.Invoke("Only the server can execute '" + commandName + "' commands.");
				return false;
			}
			return true;
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

		private bool TryParseVector3(string[] tokens, int startIndex, out Vector3 value)
		{
			value = default;
			if (tokens.Length <= startIndex + 2)
			{
				return false;
			}
			if (!float.TryParse(tokens[startIndex], out var result))
			{
				return false;
			}
			if (!float.TryParse(tokens[startIndex + 1], out var result2))
			{
				return false;
			}
			if (!float.TryParse(tokens[startIndex + 2], out var result3))
			{
				return false;
			}
			value = new Vector3(result, result2, result3);
			return true;
		}

		private bool TryParseByte(string value, out byte result)
		{
			result = 0;
			if (!int.TryParse(value, out var result2))
			{
				return false;
			}
			if (result2 <= 0 || result2 > 255)
			{
				return false;
			}
			result = (byte)result2;
			return true;
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

		private bool IsDeadFishToken(string token)
		{
			if (string.IsNullOrWhiteSpace(token))
			{
				return false;
			}
			string text = token.Trim().ToLowerInvariant();
			if (!(text == "df") && !(text == "deadfish"))
			{
				return text == "fish";
			}
			return true;
		}

		private bool IsAddOp(string op)
		{
			switch (op)
			{
			default:
				return op == "give";
			case "+":
			case "add":
			case "earn":
				return true;
			}
		}

		private bool IsSpendOp(string op)
		{
			switch (op)
			{
			default:
				return op == "remove";
			case "-":
			case "spend":
			case "take":
				return true;
			}
		}

		private bool TryParseToggle(string raw, out bool value)
		{
			value = false;
			if (string.IsNullOrWhiteSpace(raw))
			{
				return false;
			}
			switch (raw.Trim().ToLowerInvariant())
			{
			case "1":
			case "on":
			case "true":
			case "show":
			case "enable":
			case "enabled":
				value = true;
				return true;
			case "0":
			case "off":
			case "false":
			case "hide":
			case "disable":
			case "disabled":
				value = false;
				return true;
			default:
				return false;
			}
		}

		private List<ItemSO> BuildAutoSpawnList(ItemDatabaseSO db)
		{
			List<ItemSO> list = new List<ItemSO>();
			list.AddRange(db.GetItemsByItemType(ItemType.Wholesale));
			list.AddRange(db.GetItemsByItemType(ItemType.RawIngredient));
			list.AddRange(db.GetItemsByItemType(ItemType.ProcessedIngredient));
			list.AddRange(db.GetItemsByItemType(ItemType.ServedDish));
			list.AddRange(db.GetItemsByItemType(ItemType.Decoration));
			list.RemoveAll((ItemSO x) => x == null);
			return list;
		}

		private void TrySpawnItem(ItemSO item, Action<string> feedback)
		{
			if (item == null)
			{
				feedback?.Invoke("Resolved item is null.");
				return;
			}
			if (!(item is HoldableSO holdableSO))
			{
				feedback?.Invoke($"Item is not HoldableSO: {item.name} (Type: {item.ItemType})");
				return;
			}
			if (Singleton<QuickSlotHandler>.Instance == null)
			{
				feedback?.Invoke("QuickSlotHandler not found.");
				return;
			}
			Singleton<QuickSlotHandler>.Instance.SpawnItem(holdableSO, select: true);
			feedback?.Invoke($"Spawned: {holdableSO.name} (Type: {item.ItemType}, Id: {item.Id})");
		}

		private void TrySpawnDeadFish(DevConsoleContext context, int fishIndex, Action<string> feedback)
		{
			if (context.ItemDatabase == null || context.ItemDatabase.CreatureItems == null)
			{
				feedback?.Invoke("CreatureItems database is missing.");
				return;
			}
			if (fishIndex < 0 || fishIndex >= context.ItemDatabase.CreatureItems.Count)
			{
				feedback?.Invoke($"Fish index out of range. Valid range: 0..{context.ItemDatabase.CreatureItems.Count - 1}");
				return;
			}
			FishSO fishSO = context.ItemDatabase.CreatureItems[fishIndex] as FishSO;
			if (fishSO == null)
			{
				feedback?.Invoke($"Creature at index {fishIndex} is not FishSO.");
				return;
			}
			if (fishSO.DeadPrefab == null)
			{
				feedback?.Invoke("DeadPrefab is missing on fish: " + fishSO.name);
				return;
			}
			if (!TryGetLocalPlayer(out var player, out var error))
			{
				feedback?.Invoke(error);
				return;
			}
			Vector3 position = Vector3.zero;
			if (context.SpawnOrigin != null)
			{
				position = context.SpawnOrigin.position + context.SpawnOrigin.forward * 1.5f;
			}
			Singleton<SeaCreatureManager>.Instance.SpawnDeadFish(fishSO, position, player.OwnerClientId, player.NetworkObject.ToReference(), select: true, canHold: true);
			feedback?.Invoke("Spawned dead fish: " + fishSO.name + " | Id: " + fishSO.Id);
		}
	}
}
