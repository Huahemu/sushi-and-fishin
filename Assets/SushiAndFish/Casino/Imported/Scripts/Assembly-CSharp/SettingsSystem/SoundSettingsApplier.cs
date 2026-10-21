namespace SushiAndFish.Casino.Original
{
using global::FMODUnity;

namespace SettingsSystem
{
	public class SoundSettingsApplier : ISettingsApplier
	{
		public void Apply(SettingItemBase entry)
		{
			if (!(entry == null) && !string.IsNullOrWhiteSpace(entry.key))
			{
				string text = entry.key.Trim().ToLowerInvariant();
				if (text == "mastervolume" && entry is SliderSettingItem sliderSettingItem)
				{
					RuntimeManager.StudioSystem.setParameterByName("Master", sliderSettingItem.value);
				}
				else if (text == "musicvolume" && entry is SliderSettingItem sliderSettingItem2)
				{
					RuntimeManager.StudioSystem.setParameterByName("Music", sliderSettingItem2.value);
				}
				else if (text == "sfxvolume" && entry is SliderSettingItem sliderSettingItem3)
				{
					RuntimeManager.StudioSystem.setParameterByName("SFX", sliderSettingItem3.value);
				}
				else if (text == "proximitychatvolume" && entry is SliderSettingItem sliderSettingItem4)
				{
					RuntimeManager.StudioSystem.setParameterByName("VOIP", sliderSettingItem4.value);
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
