namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::System.IO;
using global::UnityEngine;

namespace SettingsSystem
{
	public class SettingsPersistence
	{
		[Serializable]
		private class SettingsSaveData
		{
			public List<SettingSaveEntry> entries;

			public long timestamp;

			public string version;
		}

		[Serializable]
		public class SettingSaveEntry
		{
			public string key;

			public string type;

			public bool boolValue;

			public float floatValue;

			public int index;

			public string stringValue;

			public int width;

			public int height;
		}

		private readonly string _saveFilePath;

		private readonly SettingsLayout _layout;

		public SettingsPersistence(SettingsLayout layout, string saveFileName)
		{
			_layout = layout;
			_saveFilePath = Path.Combine(SushiAndFish.Casino.Original.CasinoPaths.PersistentDataPath, saveFileName);
		}

		public void Save(Dictionary<string, SettingSaveEntry> entries)
		{
			if (!(_layout == null))
			{
				string contents = JsonUtility.ToJson(new SettingsSaveData
				{
					entries = new List<SettingSaveEntry>(entries.Values),
					timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
					version = "1.0"
				}, prettyPrint: true);
				File.WriteAllText(_saveFilePath, contents);
			}
		}

		public Dictionary<string, SettingSaveEntry> Load()
		{
			Dictionary<string, SettingSaveEntry> dictionary = new Dictionary<string, SettingSaveEntry>(StringComparer.OrdinalIgnoreCase);
			if (!File.Exists(_saveFilePath))
			{
				return dictionary;
			}
			try
			{
				SettingsSaveData settingsSaveData = JsonUtility.FromJson<SettingsSaveData>(File.ReadAllText(_saveFilePath));
				if (settingsSaveData?.entries == null)
				{
					return dictionary;
				}
				foreach (SettingSaveEntry entry in settingsSaveData.entries)
				{
					if (!string.IsNullOrWhiteSpace(entry.key))
					{
						dictionary[entry.key] = entry;
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogError("[SettingsPersistence] Failed to load settings: " + ex.Message);
			}
			return dictionary;
		}

		public SettingSaveEntry BuildSaveEntry(SettingItemBase item)
		{
			SettingSaveEntry settingSaveEntry = new SettingSaveEntry
			{
				key = item.key
			};
			if (item is ToggleSettingItem toggleSettingItem)
			{
				settingSaveEntry.type = "toggle";
				settingSaveEntry.boolValue = toggleSettingItem.value;
			}
			else if (item is SliderSettingItem sliderSettingItem)
			{
				settingSaveEntry.type = "slider";
				settingSaveEntry.floatValue = sliderSettingItem.value;
			}
			else if (item is DropdownSettingItem dropdownSettingItem)
			{
				settingSaveEntry.type = "dropdown";
				settingSaveEntry.index = dropdownSettingItem.index;
				settingSaveEntry.stringValue = dropdownSettingItem.CurrentOption;
				if (string.IsNullOrWhiteSpace(settingSaveEntry.stringValue) && dropdownSettingItem.options != null && dropdownSettingItem.index >= 0 && dropdownSettingItem.index < dropdownSettingItem.options.Count)
				{
					settingSaveEntry.stringValue = dropdownSettingItem.options[dropdownSettingItem.index];
				}
			}
			else if (item is ResolutionSettingItem resolutionSettingItem)
			{
				settingSaveEntry.type = "resolution";
				settingSaveEntry.width = resolutionSettingItem.width;
				settingSaveEntry.height = resolutionSettingItem.height;
			}
			else if (item is RebindSettingItem rebindSettingItem)
			{
				settingSaveEntry.type = "rebind";
				settingSaveEntry.index = rebindSettingItem.bindingIndex;
				settingSaveEntry.stringValue = rebindSettingItem.overridePath;
			}
			return settingSaveEntry;
		}

		public void ApplySavedValue(SettingItemBase item, SettingSaveEntry saved)
		{
			if (item is ToggleSettingItem toggleSettingItem)
			{
				toggleSettingItem.value = saved.boolValue;
			}
			else if (item is SliderSettingItem sliderSettingItem)
			{
				sliderSettingItem.value = saved.floatValue;
			}
			else if (item is ResolutionSettingItem resolutionSettingItem)
			{
				resolutionSettingItem.width = saved.width;
				resolutionSettingItem.height = saved.height;
			}
			else if (item is DropdownSettingItem { options: var list } dropdownSettingItem)
			{
				if (dropdownSettingItem.useDynamicOptions && dropdownSettingItem.optionsProvider is IDropdownOptionsProvider dropdownOptionsProvider)
				{
					list = dropdownOptionsProvider.GetOptions() ?? list;
				}
				dropdownSettingItem.options = list ?? dropdownSettingItem.options;
				if (string.Equals(saved.type, "toggle", StringComparison.OrdinalIgnoreCase) && string.Equals(item.key?.Trim(), "devconsole", StringComparison.OrdinalIgnoreCase) && list != null && list.Count > 0)
				{
					dropdownSettingItem.index = Mathf.Clamp(saved.boolValue ? 1 : 0, 0, list.Count - 1);
					return;
				}
				if (!string.IsNullOrWhiteSpace(saved.stringValue) && list != null)
				{
					int num = list.FindIndex((string o) => string.Equals(o, saved.stringValue, StringComparison.OrdinalIgnoreCase));
					if (num >= 0)
					{
						dropdownSettingItem.index = num;
						return;
					}
				}
				if (list != null && list.Count > 0)
				{
					dropdownSettingItem.index = Mathf.Clamp(saved.index, 0, list.Count - 1);
				}
			}
			else if (item is RebindSettingItem rebindSettingItem)
			{
				rebindSettingItem.bindingIndex = ((saved.index >= 0) ? saved.index : rebindSettingItem.bindingIndex);
				rebindSettingItem.overridePath = saved.stringValue;
			}
		}
	}
}

}
