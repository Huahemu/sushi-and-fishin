using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Staff;

namespace PaperPixelGames.Core.Tablet
{
	public static class TabletLocalization
	{
		public static string Get(string key, string fallback)
		{
			return LocalizationDatabase.GetString("UiTable", key, fallback);
		}

		public static string Format(string key, string fallback, params object[] arguments)
		{
			return LocalizationDatabase.GetFormattedString("UiTable", key, fallback, arguments);
		}

		public static string GetStaffRoleName(StaffRole role, string fallback)
		{
			return Get("tablet.staff.role." + role.ToString().ToLowerInvariant(), fallback);
		}

		public static string GetStaffStatLabel(string fallback)
		{
			if (string.IsNullOrWhiteSpace(fallback))
			{
				return string.Empty;
			}
			string text = fallback.Trim().ToLowerInvariant().Replace(" ", "_");
			return Get("tablet.staff.stat." + text, fallback);
		}

		public static string GetHabitatName(string fallback)
		{
			if (string.IsNullOrWhiteSpace(fallback))
			{
				return string.Empty;
			}
			string text = fallback.Trim().ToLowerInvariant().Replace(" ", "_");
			return Get("tablet.map.habitat_name." + text, fallback);
		}
	}
}
