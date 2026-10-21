using System;

namespace PaperPixelGames.DevConsole
{
	public sealed class CurrencyShortcutCommand : IDevCommand
	{
		private readonly SetCommand _setCommand;

		private readonly string _currency;

		public string Name => _currency;

		public string Description => _currency + " +<amount> | " + _currency + " -<amount>";

		public CurrencyShortcutCommand(string currency, SetCommand setCommand)
		{
			_currency = currency;
			_setCommand = setCommand;
		}

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			string[] array = DevCommandArgs.Clean(args);
			if (array.Length < 2)
			{
				feedback?.Invoke(Description);
				return;
			}
			if (!TryReadSignedAmount(array, out var op, out var amount))
			{
				feedback?.Invoke(Description);
				return;
			}
			_setCommand.Execute(context, new string[4] { "set", _currency, op, amount }, feedback);
		}

		private bool TryReadSignedAmount(string[] tokens, out string op, out string amount)
		{
			op = null;
			amount = null;
			string text = tokens[1].Trim();
			int result;
			if ((text.StartsWith("+") || text.StartsWith("-")) && text.Length > 1)
			{
				op = text[0].ToString();
				string text2 = text;
				amount = text2.Substring(1, text2.Length - 1);
				return int.TryParse(amount, out result);
			}
			if (tokens.Length < 3)
			{
				return false;
			}
			op = text;
			amount = tokens[2].Trim();
			return int.TryParse(amount, out result);
		}
	}
}
