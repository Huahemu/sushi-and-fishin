namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

namespace SettingsSystem
{
	public class EmoteWheelSettingsApplier : ISettingsApplier
	{
		public void Apply(SettingItemBase entry)
		{
			if (!(entry == null) && !string.IsNullOrWhiteSpace(entry.key) && entry.key.Trim().ToLowerInvariant() == "emotewheelmode" && entry is ToggleSettingItem toggleSettingItem)
			{
				RMF_RadialMenu rMF_RadialMenu = Object.FindFirstObjectByType<RMF_RadialMenu>();
				if (rMF_RadialMenu != null)
				{
					rMF_RadialMenu.useSelectionFollower = (rMF_RadialMenu.useDeltaSelection = toggleSettingItem.value);
					rMF_RadialMenu.UpdateSelectionFollowerState();
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
	}
}

}
