using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine.Localization.Settings;

namespace PaperPixelGames.Core.Localization
{
	public static class LocalizationDatabase
	{
		public const string ItemsTable = "ItemsTable";

		public const string QuestTable = "QuestTable";

		public const string EmployeeDialogueTable = "EmployeeDialogueTable";

		public const string SettingsTable = "SettingsTable";

		public const string GameStateTable = "GameStateTable";

		public const string UiTable = "UiTable";

		private static readonly IReadOnlyDictionary<string, string> SettingsTermKeys = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			{ "Window", "settings.option.window" },
			{ "Full Screen", "settings.option.full_screen" },
			{ "Exclusive (Windows)", "settings.option.exclusive_windows" },
			{ "Maximized (MacOS)", "settings.option.maximized_macos" },
			{ "Unlimited", "settings.option.unlimited" },
			{ "Full Resolution", "settings.option.full_resolution" },
			{ "Half Resolution", "settings.option.half_resolution" },
			{ "Quater Resolution", "settings.option.quarter_resolution" },
			{ "Quarter Resolution", "settings.option.quarter_resolution" },
			{ "Eighth Resolution", "settings.option.eighth_resolution" },
			{ "Ultra", "settings.option.ultra" },
			{ "High", "settings.option.high" },
			{ "Medium", "settings.option.medium" },
			{ "Low", "settings.option.low" },
			{ "Disabled", "settings.option.disabled" }
		};

		private static readonly IReadOnlyDictionary<string, string> SettingsActionKeys = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			{ "Close", "settings.binding.close" },
			{ "Crouch", "settings.binding.crouch" },
			{ "Emote Wheel", "settings.binding.emote_wheel" },
			{ "Flashlight", "settings.binding.flashlight" },
			{ "Hotkey 1", "settings.binding.hotkey_1" },
			{ "Hotkey 2", "settings.binding.hotkey_2" },
			{ "Hotkey 3", "settings.binding.hotkey_3" },
			{ "Hotkey 4", "settings.binding.hotkey_4" },
			{ "Hotkey 5", "settings.binding.hotkey_5" },
			{ "Interact", "settings.binding.interact" },
			{ "Jump", "settings.binding.jump" },
			{ "Map", "settings.binding.map" },
			{ "Move Back", "settings.binding.move_back" },
			{ "Move Forward", "settings.binding.move_forward" },
			{ "Move Left", "settings.binding.move_left" },
			{ "Move Right", "settings.binding.move_right" },
			{ "Ping", "settings.binding.ping" },
			{ "Primary Action", "settings.binding.primary_action" },
			{ "Rapid Press", "settings.binding.rapid_press" },
			{ "Push to Talk", "settings.binding.push_to_talk" },
			{ "Quick Slot", "settings.binding.quick_slot" },
			{ "Scan", "settings.binding.scan" },
			{ "Secondary Action", "settings.binding.secondary_action" },
			{ "Sprint", "settings.binding.sprint" },
			{ "Tablet", "settings.binding.tablet" },
			{ "Throw", "settings.binding.throw" },
			{ "Tool Wheel", "settings.binding.tool_wheel" }
		};

		public static string GetString(string tableName, string key, string fallback)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				return fallback ?? string.Empty;
			}
			if (!LocalizationSettings.InitializationOperation.IsDone)
			{
				return fallback ?? string.Empty;
			}
			// A missing entry can return Unity's nonempty "No translation found"
			// diagnostic. Resolve the entry first so the supplied name remains the
			// fallback when recovered runtime bundles predate newly authored items.
			string localizedString = GetStringOrFallback(tableName, key, fallback);
			if (!string.IsNullOrWhiteSpace(localizedString))
			{
				return localizedString;
			}
			return fallback ?? string.Empty;
		}

		public static string GetStringOrFallback(string tableName, string key, string fallback)
		{
			if (string.IsNullOrWhiteSpace(key) || !LocalizationSettings.InitializationOperation.IsDone)
			{
				return fallback ?? string.Empty;
			}
			LocalizedDatabase<UnityEngine.Localization.Tables.StringTable, UnityEngine.Localization.Tables.StringTableEntry>.TableEntryResult tableEntry = LocalizationSettings.StringDatabase.GetTableEntry(tableName, key);
			if (tableEntry.Entry == null || string.IsNullOrWhiteSpace(tableEntry.Entry.LocalizedValue))
			{
				return fallback ?? string.Empty;
			}
			return tableEntry.Entry.GetLocalizedString();
		}

		public static string GetFormattedString(string tableName, string key, string fallback, params object[] arguments)
		{
			string format = GetString(tableName, key, fallback);
			try
			{
				return string.Format(format, arguments ?? Array.Empty<object>());
			}
			catch (FormatException)
			{
				return fallback ?? string.Empty;
			}
		}

		public static bool ContainsLocalized(string text, string searchText)
		{
			if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(searchText))
			{
				return false;
			}
			CultureInfo cultureInfo = CultureInfo.InvariantCulture;
			string text2 = ((!LocalizationSettings.InitializationOperation.IsDone) ? null : LocalizationSettings.SelectedLocale?.Identifier.Code);
			if (!string.IsNullOrWhiteSpace(text2))
			{
				try
				{
					cultureInfo = CultureInfo.GetCultureInfo(text2);
				}
				catch (CultureNotFoundException)
				{
				}
			}
			return cultureInfo.CompareInfo.IndexOf(text, searchText.Trim(), CompareOptions.IgnoreCase) >= 0;
		}

		public static string GetSettingsTerm(string term, string fallback = null)
		{
			if (string.IsNullOrWhiteSpace(term) || !SettingsTermKeys.TryGetValue(term, out var value))
			{
				return fallback ?? term ?? string.Empty;
			}
			return GetString("SettingsTable", value, fallback ?? term);
		}

		public static bool TryGetSettingsActionKey(string text, out string key)
		{
			return SettingsActionKeys.TryGetValue(text ?? string.Empty, out key);
		}

		public static string ToSnakeCase(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return string.Empty;
			}
			StringBuilder stringBuilder = new StringBuilder(value.Length + 8);
			bool flag = false;
			for (int i = 0; i < value.Length; i++)
			{
				char c = value[i];
				if (!char.IsLetterOrDigit(c))
				{
					flag = stringBuilder.Length > 0;
					continue;
				}
				if (flag)
				{
					if (stringBuilder[stringBuilder.Length - 1] != '_')
					{
						stringBuilder.Append('_');
					}
					flag = false;
				}
				if (char.IsUpper(c) && i > 0 && (char.IsLower(value[i - 1]) || (i + 1 < value.Length && char.IsLower(value[i + 1]))) && stringBuilder.Length > 0 && stringBuilder[stringBuilder.Length - 1] != '_')
				{
					stringBuilder.Append('_');
				}
				stringBuilder.Append(char.ToLowerInvariant(c));
			}
			return stringBuilder.ToString();
		}
	}
}
