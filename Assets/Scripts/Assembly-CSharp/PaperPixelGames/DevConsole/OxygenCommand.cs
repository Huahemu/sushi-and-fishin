using System;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.PlayerTools;
using PaperPixelGames.Framework.Patterns;

namespace PaperPixelGames.DevConsole
{
	public class OxygenCommand : IDevCommand
	{
		public string Name => "oxygen";

		public string Description => "oxygen +<amount> | oxygen -<amount> | oxygen loss on | oxygen loss off";

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			if (args == null || args.Length < 2)
			{
				feedback(Description);
				return;
			}
			PlayersManager instance = Singleton<PlayersManager>.Instance;
			if (instance == null)
			{
				feedback("PlayersManager not found.");
				return;
			}
			PPG_Player localPlayer = instance.LocalPlayer;
			if (localPlayer == null)
			{
				feedback("LocalPlayer not found.");
				return;
			}
			DivingEquipment divingEquipment = localPlayer.DivingEquipment;
			if (divingEquipment == null)
			{
				feedback("LocalPlayer DivingEquipment not found.");
				return;
			}
			string text = args[1].Trim().ToLowerInvariant();
			if (text == "loss")
			{
				if (args.Length < 3 || !TryParseToggle(args[2], out var value))
				{
					feedback("Usage: oxygen loss <on/off>");
					return;
				}
				divingEquipment.SetDebugPreventOxygenLoss(value);
				feedback($"DivingEquipment PreventOxygenLoss set to {value}.");
				return;
			}
			if (!TryReadAmount(args, out var amount, out var normalizedOp))
			{
				feedback("Invalid amount. Must be 1..255");
				return;
			}
			switch (normalizedOp)
			{
			case "+":
			case "add":
			case "apply":
			case "boost":
				divingEquipment.ApplyOxygen(amount);
				feedback($"Oxygen applied: +{amount}");
				break;
			case "-":
			case "consume":
			case "use":
				divingEquipment.ConsumeOxygen(amount, withFeedback: true);
				feedback($"Oxygen consumed: -{amount}");
				break;
			default:
				feedback("Unknown operation: '" + text + "'\n\n" + Description);
				break;
			}
		}

		private bool TryReadAmount(string[] args, out byte amount, out string normalizedOp)
		{
			amount = 0;
			normalizedOp = null;
			if (args == null || args.Length < 2)
			{
				return false;
			}
			string text = args[1].Trim();
			if ((text.StartsWith("+") || text.StartsWith("-")) && text.Length > 1)
			{
				normalizedOp = text[0].ToString();
				string text2 = text;
				return TryParseByte(text2.Substring(1, text2.Length - 1), out amount);
			}
			if (args.Length < 3)
			{
				return false;
			}
			normalizedOp = text.ToLowerInvariant();
			return TryParseByte(args[2], out amount);
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
			case "enable":
			case "enabled":
				value = true;
				return true;
			case "0":
			case "off":
			case "false":
			case "disable":
			case "disabled":
				value = false;
				return true;
			default:
				return false;
			}
		}
	}
}
