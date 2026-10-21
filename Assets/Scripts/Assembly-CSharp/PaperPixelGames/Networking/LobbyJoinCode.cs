using System;
using System.Text;

namespace PaperPixelGames.Networking
{
	public static class LobbyJoinCode
	{
		private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

		public static string Encode(ulong lobbyId)
		{
			if (lobbyId == 0L)
			{
				return "";
			}
			StringBuilder stringBuilder = new StringBuilder();
			do
			{
				stringBuilder.Insert(0, "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"[(int)(lobbyId % 36)]);
				lobbyId /= 36;
			}
			while (lobbyId != 0L);
			return "SUSHI-" + stringBuilder;
		}

		public static bool TryDecode(string code, out ulong lobbyId)
		{
			lobbyId = 0uL;
			if (string.IsNullOrWhiteSpace(code) || code.Length > 40)
			{
				return false;
			}
			string text = code.Trim().ToUpperInvariant();
			if (text.StartsWith("SUSHI-", StringComparison.Ordinal))
			{
				text = text.Substring(6);
			}
			text = text.Replace("-", "").Replace(" ", "");
			if (text.Length == 0 || text.Length > 13)
			{
				return false;
			}
			try
			{
				string text2 = text;
				foreach (char value in text2)
				{
					int num = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".IndexOf(value);
					if (num < 0)
					{
						lobbyId = 0uL;
						return false;
					}
					lobbyId = checked(lobbyId * 36 + (ulong)num);
				}
				return lobbyId != 0;
			}
			catch (OverflowException)
			{
				lobbyId = 0uL;
				return false;
			}
		}
	}
}
