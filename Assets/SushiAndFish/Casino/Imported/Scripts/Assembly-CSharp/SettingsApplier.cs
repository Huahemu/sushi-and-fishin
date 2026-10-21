namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.SettingsSystem;
using global::UnityEngine;
using global::UnityEngine.SceneManagement;

public class SettingsApplier : MonoBehaviour
{
	[SerializeField]
	private SettingsLayout layout;

	[SerializeField]
	private string saveFileName = "settings.json";

	[SerializeField]
	private GraphicsSettings graphicsSettings;

	private SettingsPersistence _persistence;

	private DisplaySettingsApplier _displayApplier;

	private SoundSettingsApplier _soundApplier;

	private MicrophoneSettingsApplier _microphoneApplier;

	private InputSettingsApplier _inputApplier;

	private FramerateSettingsApplier _framerateApplier;

	private EmoteWheelSettingsApplier _emoteWheelApplier;

	private GraphicsSettingsApplier _graphicsApplier;

	private bool _skipWindowModeAndResolution;

	private void Awake()
	{
		_persistence = new SettingsPersistence(layout, saveFileName);
		_displayApplier = new DisplaySettingsApplier((string key) => FindDropdownSetting(key), () => IsWindowedDisplay());
		_soundApplier = new SoundSettingsApplier();
		_microphoneApplier = new MicrophoneSettingsApplier(this);
		_inputApplier = new InputSettingsApplier();
		_framerateApplier = new FramerateSettingsApplier();
		_emoteWheelApplier = new EmoteWheelSettingsApplier();
		_graphicsApplier = new GraphicsSettingsApplier(graphicsSettings);
		_skipWindowModeAndResolution = BuildTypeDetector.IsLocalBuild();
	}

	private void OnEnable()
	{
		// The host owns display, quality, exposure, audio and input preferences.
		// Applying the embedded game's saved settings rewrites host Volumes too.
		if (CasinoRuntimeContext.IsHostIntegration) { enabled = false; return; }
		LoadSettings();
		SettingsLayout.SettingsChanged += OnSettingsChanged;
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private void OnDisable()
	{
		SettingsLayout.SettingsChanged -= OnSettingsChanged;
		SceneManager.sceneLoaded -= OnSceneLoaded;
		_microphoneApplier?.StopCoroutines();
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		ApplySettingsWithLoadOnSceneStart();
		_microphoneApplier?.ApplyOnSceneLoad();
	}

	private void OnSettingsChanged(SettingsLayout source, SettingItemBase entry)
	{
		if (!(source != layout))
		{
			string key = entry?.key?.Trim().ToLowerInvariant();
			if (!_skipWindowModeAndResolution || !IsWindowModeOrResolutionKey(key))
			{
				_displayApplier.Apply(entry);
			}
			_soundApplier.Apply(entry);
			_microphoneApplier.Apply(entry);
			_inputApplier.Apply(entry);
			_framerateApplier.Apply(entry);
			_emoteWheelApplier.Apply(entry);
			_graphicsApplier.Apply(entry);
			SaveSettings();
		}
	}

	private void ApplySettingsWithLoadOnSceneStart()
	{
		if (layout == null)
		{
			return;
		}
		Dictionary<string, SettingsPersistence.SettingSaveEntry> dictionary = _persistence.Load();
		if (dictionary.Count == 0)
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
				if (!(entry == null) && !string.IsNullOrWhiteSpace(entry.key) && ShouldLoadOnSceneStart(entry) && dictionary.TryGetValue(entry.key, out var value))
				{
					_persistence.ApplySavedValue(entry, value);
					ApplySettingByType(entry);
				}
			}
		}
	}

	private bool ShouldLoadOnSceneStart(SettingItemBase item)
	{
		if (!(item is SliderSettingItem { loadOnSceneStart: var loadOnSceneStart }))
		{
			if (!(item is DropdownSettingItem { loadOnSceneStart: var loadOnSceneStart2 }))
			{
				if (!(item is ToggleSettingItem { loadOnSceneStart: var loadOnSceneStart3 }))
				{
					if (!(item is RebindSettingItem { loadOnSceneStart: var loadOnSceneStart4 }))
					{
						return false;
					}
					return loadOnSceneStart4;
				}
				return loadOnSceneStart3;
			}
			return loadOnSceneStart2;
		}
		return loadOnSceneStart;
	}

	private void ApplySettingByType(SettingItemBase item)
	{
		if (item == null || string.IsNullOrWhiteSpace(item.key))
		{
			return;
		}
		if (item is RebindSettingItem)
		{
			_inputApplier.Apply(item);
			return;
		}
		string key = item.key.Trim().ToLowerInvariant();
		if (IsDisplayKey(key))
		{
			if (!_skipWindowModeAndResolution || !IsWindowModeOrResolutionKey(key))
			{
				_displayApplier.Apply(item);
			}
		}
		else if (IsSoundKey(key))
		{
			_soundApplier.Apply(item);
		}
		else if (IsMicrophoneKey(key))
		{
			_microphoneApplier.Apply(item);
		}
		else if (IsInputKey(key))
		{
			_inputApplier.Apply(item);
		}
		else if (IsFramerateKey(key))
		{
			_framerateApplier.Apply(item);
		}
		else if (IsEmoteWheelKey(key))
		{
			_emoteWheelApplier.Apply(item);
		}
		else if (IsGraphicsKey(key))
		{
			_graphicsApplier.Apply(item);
		}
	}

	private static bool IsDisplayKey(string key)
	{
		switch (key)
		{
		default:
			return key == "refreshrate";
		case "vsync":
		case "resolution":
		case "display":
		case "aspectratio":
		case "hz":
			return true;
		}
	}

	private static bool IsSoundKey(string key)
	{
		switch (key)
		{
		default:
			return key == "proximitychatvolume";
		case "mastervolume":
		case "musicvolume":
		case "sfxvolume":
			return true;
		}
	}

	private static bool IsMicrophoneKey(string key)
	{
		return key == "microphonedevice";
	}

	private static bool IsInputKey(string key)
	{
		if (!(key == "inputvolume"))
		{
			return key == "proximityvoicechatmode";
		}
		return true;
	}

	private static bool IsFramerateKey(string key)
	{
		if (!(key == "maxframerate"))
		{
			return key == "framerate";
		}
		return true;
	}

	private static bool IsEmoteWheelKey(string key)
	{
		return key == "emotewheelmode";
	}

	private static bool IsGraphicsKey(string key)
	{
		switch (key)
		{
		default:
			return key == "filmgrain";
		case "quality":
		case "qualitylevel":
		case "renderscale":
		case "render scale":
		case "hdr":
		case "brightness":
			return true;
		}
	}

	private void SaveSettings()
	{
		if (layout == null)
		{
			return;
		}
		Dictionary<string, SettingsPersistence.SettingSaveEntry> dictionary = new Dictionary<string, SettingsPersistence.SettingSaveEntry>();
		HashSet<SettingItemBase> hashSet = new HashSet<SettingItemBase>();
		foreach (SettingsLayout.Tab tab in layout.tabs)
		{
			if (tab == null)
			{
				continue;
			}
			foreach (SettingItemBase entry in tab.entries)
			{
				if (!(entry == null) && hashSet.Add(entry))
				{
					SettingsPersistence.SettingSaveEntry value = _persistence.BuildSaveEntry(entry);
					dictionary[entry.key] = value;
				}
			}
		}
		_persistence.Save(dictionary);
	}

	private void LoadSettings()
	{
		if (layout == null)
		{
			return;
		}
		Dictionary<string, SettingsPersistence.SettingSaveEntry> dictionary = _persistence.Load();
		if (dictionary.Count == 0)
		{
			ApplyDefaultSettings();
			ApplyAllSettings();
			SaveSettings();
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
				if (!(entry == null) && !string.IsNullOrWhiteSpace(entry.key) && dictionary.TryGetValue(entry.key, out var value))
				{
					_persistence.ApplySavedValue(entry, value);
					if (IsMicrophoneKey(entry.key) && value != null && !string.IsNullOrWhiteSpace(value.stringValue))
					{
						_microphoneApplier.SetSavedDeviceName(value.stringValue);
					}
				}
			}
		}
		ApplyAllSettings();
		if (!string.IsNullOrEmpty(_microphoneApplier.GetSavedDeviceName()))
		{
			_microphoneApplier.ApplyOnSceneLoad();
		}
	}

	private void ApplyAllSettings()
	{
		_soundApplier.ApplyAll(layout);
		_microphoneApplier.ApplyAll(layout);
		_inputApplier.ApplyAll(layout);
		_framerateApplier.ApplyAll(layout);
		ApplyAllDisplaySettings(layout);
		_graphicsApplier?.ApplyAllSettings();
	}

	private void ApplyAllDisplaySettings(SettingsLayout layout)
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
				if (!(entry == null) && !string.IsNullOrWhiteSpace(entry.key))
				{
					string key = entry.key.Trim().ToLowerInvariant();
					if (IsDisplayKey(key) && (!_skipWindowModeAndResolution || !IsWindowModeOrResolutionKey(key)))
					{
						_displayApplier.Apply(entry);
					}
				}
			}
		}
	}

	private static bool IsWindowModeOrResolutionKey(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return false;
		}
		switch (key)
		{
		default:
			return key == "refreshrate";
		case "resolution":
		case "display":
		case "aspectratio":
		case "hz":
			return true;
		}
	}

	private void ApplyDefaultSettings()
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
				if (!(entry == null))
				{
					if (entry is ToggleSettingItem toggleSettingItem)
					{
						toggleSettingItem.value = toggleSettingItem.defaultValue;
					}
					else if (entry is SliderSettingItem sliderSettingItem)
					{
						sliderSettingItem.value = sliderSettingItem.defaultValue;
					}
					if (entry is DropdownSettingItem dropdownSettingItem && IsKey(dropdownSettingItem, "display"))
					{
						SetDisplayDefault(dropdownSettingItem);
					}
					if (entry is DropdownSettingItem dropdownSettingItem2 && IsKey(dropdownSettingItem2, "resolution"))
					{
						SetResolutionDefault(dropdownSettingItem2);
					}
					if (entry is ResolutionSettingItem resolutionSettingItem && IsKey(resolutionSettingItem, "resolution"))
					{
						resolutionSettingItem.width = Screen.currentResolution.width;
						resolutionSettingItem.height = Screen.currentResolution.height;
					}
					if (entry is DropdownSettingItem dropdownSettingItem3 && (IsKey(dropdownSettingItem3, "maxframerate") || IsKey(dropdownSettingItem3, "framerate")))
					{
						SetFramerateDefault(dropdownSettingItem3);
					}
				}
			}
		}
	}

	private static void SetRefreshRateDefault(DropdownSettingItem dropdown)
	{
		if (dropdown == null)
		{
			return;
		}
		RefreshRate refreshRateRatio = Screen.currentResolution.refreshRateRatio;
		float currentHz = (float)refreshRateRatio.numerator / (float)refreshRateRatio.denominator;
		List<string> list = dropdown.options ?? new List<string>();
		int num = list.FindIndex((string o) => TryParseRefreshRate(o, out var refreshRate2) && Mathf.Approximately((float)refreshRate2.numerator / (float)refreshRate2.denominator, currentHz));
		if (num < 0)
		{
			int num2 = 0;
			float num3 = float.MaxValue;
			for (int num4 = 0; num4 < list.Count; num4++)
			{
				if (TryParseRefreshRate(list[num4], out var refreshRate))
				{
					float num5 = Mathf.Abs((float)refreshRate.numerator / (float)refreshRate.denominator - currentHz);
					if (num5 < num3)
					{
						num3 = num5;
						num2 = num4;
					}
				}
			}
			num = num2;
		}
		dropdown.index = ((num >= 0) ? num : 0);
	}

	private static void SetFramerateDefault(DropdownSettingItem dropdown)
	{
		if (dropdown == null)
		{
			return;
		}
		int currentFramerate = Application.targetFrameRate;
		List<string> list = dropdown.options ?? new List<string>();
		if (currentFramerate == -1)
		{
			int num = list.FindIndex((string o) =>
			{
				string text = o.Trim().ToLowerInvariant();
				return text == "unlimited" || text == "uncapped" || text == "off";
			});
			dropdown.index = ((num >= 0) ? num : 0);
			return;
		}
		int num2 = list.FindIndex((string o) => TryParseFramerate(o, out var framerate2) && framerate2 == currentFramerate);
		if (num2 < 0)
		{
			int num3 = 0;
			int num4 = int.MaxValue;
			for (int num5 = 0; num5 < list.Count; num5++)
			{
				if (TryParseFramerate(list[num5], out var framerate) && framerate > 0)
				{
					int num6 = Mathf.Abs(framerate - currentFramerate);
					if (num6 < num4)
					{
						num4 = num6;
						num3 = num5;
					}
				}
			}
			num2 = num3;
		}
		dropdown.index = ((num2 >= 0) ? num2 : 0);
	}

	private static void SetDisplayDefault(DropdownSettingItem dropdown)
	{
		if (!(dropdown == null))
		{
			int num = (dropdown.options ?? new List<string>()).FindIndex((string o) => string.Equals(o, "Fullscreen", StringComparison.OrdinalIgnoreCase) || string.Equals(o, "Windowed Fullscreen", StringComparison.OrdinalIgnoreCase) || string.Equals(o, "Fullscreen Windowed", StringComparison.OrdinalIgnoreCase) || string.Equals(o, "Borderless", StringComparison.OrdinalIgnoreCase) || string.Equals(o, "Borderless Fullscreen", StringComparison.OrdinalIgnoreCase));
			dropdown.index = ((num >= 0) ? num : 0);
		}
	}

	private static void SetResolutionDefault(DropdownSettingItem dropdown)
	{
		if (!(dropdown == null))
		{
			string label = $"{Screen.currentResolution.width}x{Screen.currentResolution.height}";
			if (dropdown.options == null)
			{
				dropdown.options = new List<string>();
			}
			int num = dropdown.options.FindIndex((string o) => string.Equals(o, label, StringComparison.OrdinalIgnoreCase));
			if (num < 0)
			{
				dropdown.options.Insert(0, label);
				num = 0;
			}
			dropdown.index = num;
		}
	}

	public DropdownSettingItem FindDropdownSetting(string key)
	{
		if (layout == null)
		{
			return null;
		}
		foreach (SettingsLayout.Tab tab in layout.tabs)
		{
			if (tab == null)
			{
				continue;
			}
			foreach (SettingItemBase entry in tab.entries)
			{
				if (entry is DropdownSettingItem dropdownSettingItem && IsKey(dropdownSettingItem, key))
				{
					return dropdownSettingItem;
				}
			}
		}
		return null;
	}

	private bool IsWindowedDisplay()
	{
		DropdownSettingItem dropdownSettingItem = FindDropdownSetting("display");
		if (dropdownSettingItem == null || string.IsNullOrWhiteSpace(dropdownSettingItem.CurrentOption))
		{
			return false;
		}
		return string.Equals(dropdownSettingItem.CurrentOption.Trim(), "Windowed", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsKey(SettingItemBase entry, string key)
	{
		if (entry == null || string.IsNullOrWhiteSpace(entry.key))
		{
			return false;
		}
		return string.Equals(entry.key.Trim(), key, StringComparison.OrdinalIgnoreCase);
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
