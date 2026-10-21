namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

namespace SettingsSystem
{
	public class FramerateSettingsApplier : ISettingsApplier
	{
		public void Apply(SettingItemBase entry)
		{
			if (!(entry == null) && !string.IsNullOrWhiteSpace(entry.key))
			{
				string text = entry.key.Trim().ToLowerInvariant();
				if ((text == "maxframerate" || text == "framerate") && entry is DropdownSettingItem dropdownSettingItem && TryParseFramerate(dropdownSettingItem.CurrentOption, out var framerate))
				{
					Application.targetFrameRate = framerate;
				}
			}
		}

		public void ApplyAll(SettingsLayout layout)
		{
			if (layout == null)
			{
				return;
			}
			foreach (SettingsLayout.Tab tab in layout.tabs)
			{
				if (tab == null)
				{
					continue;
				}
				foreach (SettingItemBase entry in tab.entries)
				{
					Apply(entry);
				}
			}
		}

		private static bool TryParseFramerate(string value, out int framerate)
		{
			framerate = -1;
			if (string.IsNullOrWhiteSpace(value))
			{
				return false;
			}
			string text = value.Trim().ToLowerInvariant();
			switch (text)
			{
			case "unlimited":
			case "uncapped":
			case "off":
				framerate = -1;
				return true;
			default:
			{
				if (int.TryParse(text, out var result) && result > 0)
				{
					framerate = result;
					return true;
				}
				return false;
			}
			}
		}
	}
}

}
