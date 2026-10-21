using System;
using PaperPixelGames.Core.Localization;
using UnityEngine.Localization.Settings;

namespace PaperPixelGames.Core.PlayerTools
{
	public static class FishingRodText
	{
		public static string Get(string key, string english, string turkish, params object[] args)
		{
			bool flag = LocalizationSettings.InitializationOperation.IsDone && (LocalizationSettings.SelectedLocale?.Identifier.Code.StartsWith("tr", StringComparison.OrdinalIgnoreCase) ?? false);
			string stringOrFallback = LocalizationDatabase.GetStringOrFallback("UiTable", "gameplay.fishing.polish." + key, flag ? turkish : english);
			if (args.Length != 0)
			{
				return string.Format(stringOrFallback, args);
			}
			return stringOrFallback;
		}
	}
}
