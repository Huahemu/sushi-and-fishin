using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace PaperPixelGames.Core.Localization
{
	[Serializable]
	public sealed class TraditionalChineseLocaleSelector : IStartupLocaleSelector
	{
		private const string HongKongLocaleCode = "zh-HK";

		private const string TraditionalChineseLocaleCode = "zh-TW";

		private const string CommandLineArgumentPrefix = "-language=";

		public Locale GetStartupLocale(ILocalesProvider availableLocales)
		{
			bool flag = string.Equals(CultureInfo.CurrentUICulture.Name, "zh-HK", StringComparison.OrdinalIgnoreCase);
			if (!IsHongKongCommandLineLocale() && !flag && Application.systemLanguage != SystemLanguage.ChineseTraditional)
			{
				return null;
			}
			Locale locale = availableLocales.GetLocale(new LocaleIdentifier("zh-TW"));
			if (locale == null)
			{
				Debug.LogWarning("[LocalizationSettings] Traditional Chinese startup locale is missing. Code=zh-TW");
			}
			return locale;
		}

		private static bool IsHongKongCommandLineLocale()
		{
			string[] commandLineArgs = Environment.GetCommandLineArgs();
			foreach (string text in commandLineArgs)
			{
				if (text.StartsWith("-language=", StringComparison.OrdinalIgnoreCase))
				{
					return string.Equals(text.Substring("-language=".Length), "zh-HK", StringComparison.OrdinalIgnoreCase);
				}
			}
			return false;
		}
	}
}
