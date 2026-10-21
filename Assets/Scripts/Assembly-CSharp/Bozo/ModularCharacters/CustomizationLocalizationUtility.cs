using System.Text;
using PaperPixelGames.Core.Localization;

namespace Bozo.ModularCharacters
{
	internal static class CustomizationLocalizationUtility
	{
		private const string KeyPrefix = "customization.adjustment.";

		public static string GetAdjustmentLabel(string identifier, string fallback)
		{
			if (string.IsNullOrWhiteSpace(identifier))
			{
				return fallback ?? string.Empty;
			}
			string key = "customization.adjustment." + ToSnakeCase(identifier);
			return LocalizationDatabase.GetString("UiTable", key, fallback);
		}

		private static string ToSnakeCase(string value)
		{
			StringBuilder stringBuilder = new StringBuilder(value.Length + 8);
			bool flag = true;
			for (int i = 0; i < value.Length; i++)
			{
				char c = value[i];
				if (!char.IsLetterOrDigit(c))
				{
					if (!flag)
					{
						stringBuilder.Append('_');
						flag = true;
					}
					continue;
				}
				if (char.IsUpper(c) && i > 0 && !flag && (char.IsLower(value[i - 1]) || char.IsDigit(value[i - 1])))
				{
					stringBuilder.Append('_');
				}
				stringBuilder.Append(char.ToLowerInvariant(c));
				flag = false;
			}
			if (stringBuilder.Length > 0 && stringBuilder[stringBuilder.Length - 1] == '_')
			{
				stringBuilder.Length--;
			}
			return stringBuilder.ToString();
		}
	}
}
