using System;
using PaperPixelGames.Core.Economy;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;

namespace PaperPixelGames.DevConsole
{
	public class BalanceCommand : IDevCommand
	{
		public string Name => "balance";

		public string Description => "  (balance money + <amount>  balance money - <amount>  balance scrap + <amount>  balance scrap - <amount>)\n  balance money + 500\n  balance scrap - 50";

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			if (args == null || args.Length < 4)
			{
				feedback(Description);
				return;
			}
			GameProgressManager instance = Singleton<GameProgressManager>.Instance;
			if (instance == null)
			{
				feedback("GameProgressManager not found.");
				return;
			}
			EconomyHandler economyHandler = instance.EconomyHandler;
			if (economyHandler == null)
			{
				feedback("EconomyHandler not found.");
				return;
			}
			string text = args[1].Trim().ToLowerInvariant();
			string text2 = args[2].Trim().ToLowerInvariant();
			if (!int.TryParse(args[3].Trim(), out var result) || result <= 0)
			{
				feedback("Invalid amount. Must be a positive integer.");
				return;
			}
			switch (text)
			{
			case "money":
			case "m":
				if (IsAddOp(text2))
				{
					economyHandler.EarnMoney(result, ulong.MaxValue);
					feedback($"Money earned: +{result}");
				}
				else if (IsSpendOp(text2))
				{
					economyHandler.SpendMoney(result);
					feedback($"Money spent: -{result}");
				}
				else
				{
					feedback("Unknown operation '" + text2 + "'.\n" + Description);
				}
				break;
			case "scrap":
			case "s":
				if (IsAddOp(text2))
				{
					economyHandler.EarnScrap(result, ulong.MaxValue);
					feedback($"Scrap earned: +{result}");
				}
				else if (IsSpendOp(text2))
				{
					economyHandler.SpendScrap(result);
					feedback($"Scrap spent: -{result}");
				}
				else
				{
					feedback("Unknown operation '" + text2 + "'.\n" + Description);
				}
				break;
			default:
				feedback("Unknown currency '" + text + "'.\n" + Description);
				break;
			}
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
	}
}
