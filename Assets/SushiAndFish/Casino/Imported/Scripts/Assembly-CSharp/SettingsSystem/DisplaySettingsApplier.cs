namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

namespace SettingsSystem
{
	public class DisplaySettingsApplier : ISettingsApplier
	{
		private readonly Func<string, DropdownSettingItem> _findDropdownSetting;

		private readonly Func<bool> _isWindowedDisplay;

		public DisplaySettingsApplier(Func<string, DropdownSettingItem> findDropdownSetting, Func<bool> isWindowedDisplay)
		{
			_findDropdownSetting = findDropdownSetting;
			_isWindowedDisplay = isWindowedDisplay;
		}

		public void Apply(SettingItemBase entry)
		{
			if (entry == null || string.IsNullOrWhiteSpace(entry.key))
			{
				return;
			}
			string text = entry.key.Trim().ToLowerInvariant();
			RefreshRate refreshRate;
			if (text == "vsync" && entry is ToggleSettingItem toggleSettingItem)
			{
				QualitySettings.vSyncCount = (toggleSettingItem.value ? 1 : 0);
			}
			else if (text == "resolution")
			{
				RefreshRate currentRefreshRate = GetCurrentRefreshRate();
				int width;
				int height;
				if (entry is ResolutionSettingItem resolutionSettingItem)
				{
					Screen.SetResolution(resolutionSettingItem.width, resolutionSettingItem.height, Screen.fullScreenMode, currentRefreshRate);
				}
				else if (entry is DropdownSettingItem dropdownSettingItem && TryParseResolution(dropdownSettingItem.CurrentOption, out width, out height))
				{
					Screen.SetResolution(width, height, Screen.fullScreenMode, currentRefreshRate);
				}
			}
			else if (text == "display" && entry is DropdownSettingItem dropdownSettingItem2)
			{
				if (TryParseDisplayMode(dropdownSettingItem2.CurrentOption, out var mode))
				{
					Resolution currentResolution = Screen.currentResolution;
					RefreshRate currentRefreshRate2 = GetCurrentRefreshRate();
					Screen.SetResolution(currentResolution.width, currentResolution.height, mode, currentRefreshRate2);
				}
			}
			else if (text == "aspectratio" && entry is DropdownSettingItem dropdownSettingItem3)
			{
				if (IsWindowedDisplay() && TryParseAspectRatio(dropdownSettingItem3.CurrentOption, out var ratio))
				{
					Resolution resolution = FindHighestResolutionForAspect(ratio);
					RefreshRate currentRefreshRate3 = GetCurrentRefreshRate();
					if (Screen.fullScreenMode == FullScreenMode.FullScreenWindow)
					{
						Screen.SetResolution(resolution.width, resolution.height, FullScreenMode.Windowed, currentRefreshRate3);
					}
					Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreenMode, currentRefreshRate3);
				}
			}
			else if ((text == "hz" || text == "refreshrate") && entry is DropdownSettingItem dropdownSettingItem4 && TryParseRefreshRate(dropdownSettingItem4.CurrentOption, out refreshRate))
			{
				Resolution currentResolution2 = Screen.currentResolution;
				Screen.SetResolution(currentResolution2.width, currentResolution2.height, Screen.fullScreenMode, refreshRate);
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

		private RefreshRate GetCurrentRefreshRate()
		{
			DropdownSettingItem dropdownSettingItem = _findDropdownSetting?.Invoke("hz");
			if (dropdownSettingItem == null)
			{
				dropdownSettingItem = _findDropdownSetting?.Invoke("refreshrate");
			}
			if (dropdownSettingItem != null && TryParseRefreshRate(dropdownSettingItem.CurrentOption, out var refreshRate))
			{
				return refreshRate;
			}
			return Screen.currentResolution.refreshRateRatio;
		}

		private bool IsWindowedDisplay()
		{
			return _isWindowedDisplay?.Invoke() ?? false;
		}

		private static bool TryParseResolution(string value, out int width, out int height)
		{
			width = 0;
			height = 0;
			if (string.IsNullOrWhiteSpace(value))
			{
				return false;
			}
			string[] array = value.ToLowerInvariant().Split('x');
			if (array.Length != 2)
			{
				return false;
			}
			if (int.TryParse(array[0].Trim(), out width))
			{
				return int.TryParse(array[1].Trim(), out height);
			}
			return false;
		}

		private static bool TryParseDisplayMode(string value, out FullScreenMode mode)
		{
			mode = FullScreenMode.Windowed;
			if (string.IsNullOrWhiteSpace(value))
			{
				return false;
			}
			switch (value.Trim().ToLowerInvariant())
			{
			case "fullscreen":
			case "windowed fullscreen":
			case "borderless":
			case "borderless fullscreen":
			case "fullscreen windowed":
				mode = FullScreenMode.FullScreenWindow;
				return true;
			case "windowed":
				mode = FullScreenMode.Windowed;
				return true;
			default:
				return false;
			}
		}

		private static bool TryParseAspectRatio(string value, out float ratio)
		{
			ratio = 0f;
			if (string.IsNullOrWhiteSpace(value))
			{
				return false;
			}
			string[] array = value.Split(':');
			if (array.Length != 2)
			{
				return false;
			}
			if (!float.TryParse(array[0].Trim(), out var result))
			{
				return false;
			}
			if (!float.TryParse(array[1].Trim(), out var result2))
			{
				return false;
			}
			if (result2 <= 0f)
			{
				return false;
			}
			ratio = result / result2;
			return true;
		}

		private static Resolution FindHighestResolutionForAspect(float targetAspect)
		{
			Resolution[] resolutions = Screen.resolutions;
			if (resolutions == null || resolutions.Length == 0)
			{
				return Screen.currentResolution;
			}
			Resolution result = Screen.currentResolution;
			int num = -1;
			for (int i = 0; i < resolutions.Length; i++)
			{
				Resolution resolution = resolutions[i];
				if (!(Mathf.Abs((float)resolution.width / (float)resolution.height - targetAspect) > 0.01f))
				{
					int num2 = resolution.width * resolution.height;
					if (num2 > num)
					{
						num = num2;
						result = resolution;
					}
				}
			}
			return result;
		}

		private static bool TryParseRefreshRate(string value, out RefreshRate refreshRate)
		{
			refreshRate = new RefreshRate
			{
				numerator = 60000u,
				denominator = 1000u
			};
			if (string.IsNullOrWhiteSpace(value))
			{
				return false;
			}
			if (int.TryParse(value.Trim().ToLowerInvariant().Replace("hz", "")
				.Trim(), out var result) && result > 0)
			{
				refreshRate = new RefreshRate
				{
					numerator = (uint)(result * 1000),
					denominator = 1000u
				};
				return true;
			}
			return false;
		}
	}
}

}
