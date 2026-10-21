namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.EventSystems;
using global::UnityEngine.UI;

public class SettingsLayoutRuntimeUI : MonoBehaviour
{
	[Serializable]
	public class TabContent
	{
		public string tabName;

		public RectTransform contentRoot;
	}

	[Header("Layout Source")]
	[SerializeField]
	private SettingsLayout layout;

	[Header("Prefabs")]
	[SerializeField]
	private GameObject toggleEntryPrefab;

	[SerializeField]
	private GameObject sliderEntryPrefab;

	[SerializeField]
	private GameObject dropdownEntryPrefab;

	[SerializeField]
	private GameObject resetEntryPrefab;

	[SerializeField]
	private GameObject titleEntryPrefab;

	[Header("Tabs")]
	[SerializeField]
	private List<TabContent> tabContents = new List<TabContent>();

	[SerializeField]
	private string defaultTabName = "";

	private readonly Dictionary<string, RectTransform> _tabLookup = new Dictionary<string, RectTransform>(StringComparer.OrdinalIgnoreCase);

	private string _currentTabName;

	private void Awake()
	{
		BuildTabLookup();
	}

	private void OnEnable()
	{
		SettingItemBase.SettingsChanged += OnSettingChanged;
		ShowInitialOrCurrentTab();
	}

	private void OnDisable()
	{
		SettingItemBase.SettingsChanged -= OnSettingChanged;
	}

	private void ShowInitialOrCurrentTab()
	{
		if (layout == null)
		{
			Debug.LogError("SettingsLayoutRuntimeUI is missing a SettingsLayout reference.");
			return;
		}
		string text = ((!string.IsNullOrWhiteSpace(_currentTabName)) ? _currentTabName : ((!string.IsNullOrWhiteSpace(defaultTabName)) ? defaultTabName : ((layout.tabs.Count > 0) ? layout.tabs[0].tabName : "")));
		if (!string.IsNullOrWhiteSpace(text))
		{
			ShowTab(text);
		}
	}

	public void ShowTab(string tabName)
	{
		if (layout == null)
		{
			Debug.LogError("SettingsLayoutRuntimeUI has no SettingsLayout assigned.");
			return;
		}
		SettingsLayout.Tab tab = layout.tabs.Find((SettingsLayout.Tab t) => string.Equals(t.tabName, tabName, StringComparison.OrdinalIgnoreCase));
		if (tab == null)
		{
			Debug.LogWarning("SettingsLayoutRuntimeUI could not find tab '" + tabName + "' in layout.");
			return;
		}
		if (!_tabLookup.TryGetValue(tabName, out var value) || value == null)
		{
			Debug.LogWarning("SettingsLayoutRuntimeUI could not find content root for tab '" + tabName + "'.");
			return;
		}
		ClearChildren(value);
		_currentTabName = tab.tabName;
		bool flag = IsWindowed(FindDropdownSetting("display")?.CurrentOption);
		bool flag2 = FindToggleSetting("camSmoothingToggle")?.value ?? false;
		foreach (SettingItemBase entry in tab.entries)
		{
			if (!(entry == null) && (!IsKey(entry, "aspectratio") || flag) && (!IsKey(entry, "camSmoothingSlider") || flag2))
			{
				switch (entry.Kind)
				{
				case SettingKind.Slider:
					CreateSliderEntry(value, entry as SliderSettingItem);
					break;
				case SettingKind.Dropdown:
					CreateDropdownEntry(value, entry);
					break;
				case SettingKind.Reset:
					CreateResetEntry(value, entry as ResetSettingItem, tab);
					break;
				case SettingKind.Title:
					CreateTitleEntry(value, entry as TitleSettingItem);
					break;
				case SettingKind.Rebind:
					CreateRebindEntry(value, entry as RebindSettingItem);
					break;
				}
			}
		}
	}

	private void BuildTabLookup()
	{
		_tabLookup.Clear();
		foreach (TabContent tabContent in tabContents)
		{
			if (tabContent != null && !string.IsNullOrWhiteSpace(tabContent.tabName) && !(tabContent.contentRoot == null))
			{
				_tabLookup[tabContent.tabName] = tabContent.contentRoot;
			}
		}
	}

	private static void ClearChildren(RectTransform root)
	{
		for (int num = root.childCount - 1; num >= 0; num--)
		{
			UnityEngine.Object.Destroy(root.GetChild(num).gameObject);
		}
	}

	private void CreateSliderEntry(RectTransform parent, SliderSettingItem entry)
	{
		if (sliderEntryPrefab == null || entry == null)
		{
			return;
		}
		Transform child = UnityEngine.Object.Instantiate(sliderEntryPrefab, parent).transform.GetChild(0);
		SetLabel(child.transform, entry);
		Slider slider = child.GetComponentInChildren<Slider>(includeInactive: true);
		if (slider == null)
		{
			return;
		}
		slider.minValue = entry.min;
		slider.maxValue = entry.max;
		slider.wholeNumbers = entry.wholeNumbers;
		slider.value = entry.value;
		TMP_InputField valueInput = child.GetComponentInChildren<TMP_InputField>(includeInactive: true);
		Transform transform = child.transform.Find("ValueText");
		if (valueInput == null && transform != null)
		{
			valueInput = transform.GetComponent<TMP_InputField>();
		}
		TMP_Text valueText = ((valueInput != null) ? valueInput.textComponent : child.GetComponentInChildren<TMP_Text>(includeInactive: true));
		if (valueText == null && transform != null)
		{
			valueText = transform.GetComponent<TMP_Text>();
		}
		if (valueText == null)
		{
			TMP_Text[] componentsInChildren = child.GetComponentsInChildren<TMP_Text>(includeInactive: true);
			Transform transform2 = child.transform.Find("SettingName");
			TMP_Text[] array = componentsInChildren;
			foreach (TMP_Text tMP_Text in array)
			{
				if (transform2 == null || tMP_Text.transform != transform2)
				{
					valueText = tMP_Text;
					break;
				}
			}
		}
		UpdateValueDisplay(entry.value);
		slider.onValueChanged.AddListener((float newValue) =>
		{
			entry.value = (entry.wholeNumbers ? Mathf.Round(newValue) : newValue);
			UpdateValueDisplay(entry.value);
			NotifySettingsChanged(entry);
		});
		if (!(valueInput != null))
		{
			return;
		}
		valueInput.contentType = (entry.wholeNumbers ? TMP_InputField.ContentType.IntegerNumber : TMP_InputField.ContentType.DecimalNumber);
		valueInput.onEndEdit.AddListener((string str) =>
		{
			if (float.TryParse(str, out var result))
			{
				float num = Mathf.Clamp(result, entry.min, entry.max);
				if (entry.wholeNumbers)
				{
					num = Mathf.Round(num);
				}
				entry.value = num;
				slider.SetValueWithoutNotify(num);
				UpdateValueDisplay(num);
				NotifySettingsChanged(entry);
			}
		});
		void UpdateValueDisplay(float val)
		{
			string text = (entry.wholeNumbers ? Mathf.Round(val).ToString() : val.ToString("F1"));
			if (valueInput != null)
			{
				valueInput.text = text;
			}
			else if (valueText != null)
			{
				valueText.text = text;
			}
		}
	}

	private void CreateDropdownEntry(RectTransform parent, SettingItemBase entry)
	{
		if (dropdownEntryPrefab == null)
		{
			return;
		}
		Transform child = UnityEngine.Object.Instantiate(dropdownEntryPrefab, parent).transform.GetChild(0);
		SetLabel(child.transform, entry);
		(List<string> labels, int selectedIndex, List<object> values) options = BuildDropdownOptions(entry);
		TMP_Dropdown componentInChildren = child.GetComponentInChildren<TMP_Dropdown>(includeInactive: true);
		if (componentInChildren != null)
		{
			componentInChildren.ClearOptions();
			componentInChildren.AddOptions(options.labels);
			componentInChildren.value = options.selectedIndex;
			componentInChildren.RefreshShownValue();
			componentInChildren.onValueChanged.AddListener((int index) =>
			{
				ApplyDropdownSelection(entry, index, options);
				NotifySettingsChanged(entry);
			});
			return;
		}
		Dropdown componentInChildren2 = child.GetComponentInChildren<Dropdown>(includeInactive: true);
		if (!(componentInChildren2 == null))
		{
			componentInChildren2.ClearOptions();
			componentInChildren2.AddOptions(options.labels);
			componentInChildren2.value = options.selectedIndex;
			componentInChildren2.RefreshShownValue();
			componentInChildren2.onValueChanged.AddListener((int index) =>
			{
				ApplyDropdownSelection(entry, index, options);
				NotifySettingsChanged(entry);
			});
		}
	}

	private void CreateResetEntry(RectTransform parent, ResetSettingItem entry, SettingsLayout.Tab tab)
	{
		if (resetEntryPrefab == null || entry == null)
		{
			return;
		}
		GameObject obj = UnityEngine.Object.Instantiate(resetEntryPrefab, parent);
		SetLabel(obj.transform, entry);
		Button componentInChildren = obj.GetComponentInChildren<Button>(includeInactive: true);
		if (!(componentInChildren == null))
		{
			componentInChildren.onClick.AddListener(() =>
			{
				ResetTabToDefaults(tab);
			});
		}
	}

	private void CreateTitleEntry(RectTransform parent, TitleSettingItem entry)
	{
		if (!(titleEntryPrefab == null) && !(entry == null))
		{
			SetLabel(UnityEngine.Object.Instantiate(titleEntryPrefab, parent).transform, entry);
		}
	}

	private void CreateRebindEntry(RectTransform parent, RebindSettingItem entry)
	{
		if (dropdownEntryPrefab == null || entry == null)
		{
			return;
		}
		Transform child = UnityEngine.Object.Instantiate(dropdownEntryPrefab, parent).transform.GetChild(0);
		SetLabel(child.transform, entry);
		InputReader instance = InputReader.Instance;
		string originalLabel = ((instance != null) ? instance.GetBindingDisplayName(entry.actionName, entry.bindingIndex) : "Unassigned");
		TMP_Dropdown tmpDropdown = child.GetComponentInChildren<TMP_Dropdown>(includeInactive: true);
		if (tmpDropdown != null)
		{
			tmpDropdown.ClearOptions();
			tmpDropdown.AddOptions(new List<string> { originalLabel });
			tmpDropdown.SetValueWithoutNotify(0);
			tmpDropdown.RefreshShownValue();
			AddPointerClickHandler(tmpDropdown.gameObject, () =>
			{
				string item = "Listening...";
				tmpDropdown.ClearOptions();
				tmpDropdown.AddOptions(new List<string> { item });
				tmpDropdown.SetValueWithoutNotify(0);
				tmpDropdown.RefreshShownValue();
				if (InputReader.Instance == null || !InputReader.Instance.StartInteractiveRebind(entry.actionName, entry.bindingIndex, (string _) =>
				{
					entry.overridePath = InputReader.Instance.GetBindingEffectivePath(entry.actionName, entry.bindingIndex);
					string bindingDisplayName = InputReader.Instance.GetBindingDisplayName(entry.actionName, entry.bindingIndex);
					tmpDropdown.ClearOptions();
					tmpDropdown.AddOptions(new List<string> { bindingDisplayName });
					tmpDropdown.SetValueWithoutNotify(0);
					tmpDropdown.RefreshShownValue();
					NotifySettingsChanged(entry);
				}, () =>
				{
					string item2 = ((InputReader.Instance != null) ? InputReader.Instance.GetBindingDisplayName(entry.actionName, entry.bindingIndex) : originalLabel);
					tmpDropdown.ClearOptions();
					tmpDropdown.AddOptions(new List<string> { item2 });
					tmpDropdown.SetValueWithoutNotify(0);
					tmpDropdown.RefreshShownValue();
				}))
				{
					tmpDropdown.ClearOptions();
					tmpDropdown.AddOptions(new List<string> { originalLabel });
					tmpDropdown.SetValueWithoutNotify(0);
					tmpDropdown.RefreshShownValue();
				}
			});
			return;
		}
		Dropdown dropdown = child.GetComponentInChildren<Dropdown>(includeInactive: true);
		if (dropdown == null)
		{
			return;
		}
		dropdown.ClearOptions();
		dropdown.AddOptions(new List<string> { originalLabel });
		dropdown.SetValueWithoutNotify(0);
		dropdown.RefreshShownValue();
		AddPointerClickHandler(dropdown.gameObject, () =>
		{
			string item = "Listening...";
			dropdown.ClearOptions();
			dropdown.AddOptions(new List<string> { item });
			dropdown.SetValueWithoutNotify(0);
			dropdown.RefreshShownValue();
			if (InputReader.Instance == null || !InputReader.Instance.StartInteractiveRebind(entry.actionName, entry.bindingIndex, (string _) =>
			{
				entry.overridePath = InputReader.Instance.GetBindingEffectivePath(entry.actionName, entry.bindingIndex);
				string bindingDisplayName = InputReader.Instance.GetBindingDisplayName(entry.actionName, entry.bindingIndex);
				dropdown.ClearOptions();
				dropdown.AddOptions(new List<string> { bindingDisplayName });
				dropdown.SetValueWithoutNotify(0);
				dropdown.RefreshShownValue();
				NotifySettingsChanged(entry);
			}, () =>
			{
				string item2 = ((InputReader.Instance != null) ? InputReader.Instance.GetBindingDisplayName(entry.actionName, entry.bindingIndex) : originalLabel);
				dropdown.ClearOptions();
				dropdown.AddOptions(new List<string> { item2 });
				dropdown.SetValueWithoutNotify(0);
				dropdown.RefreshShownValue();
			}))
			{
				dropdown.ClearOptions();
				dropdown.AddOptions(new List<string> { originalLabel });
				dropdown.SetValueWithoutNotify(0);
				dropdown.RefreshShownValue();
			}
		});
	}

	private static void AddPointerClickHandler(GameObject target, Action onClick)
	{
		if (!(target == null) && onClick != null)
		{
			EventTrigger eventTrigger = target.GetComponent<EventTrigger>();
			if (eventTrigger == null)
			{
				eventTrigger = target.AddComponent<EventTrigger>();
			}
			if (eventTrigger.triggers == null)
			{
				eventTrigger.triggers = new List<EventTrigger.Entry>();
			}
			EventTrigger.Entry entry = new EventTrigger.Entry
			{
				eventID = EventTriggerType.PointerClick
			};
			entry.callback.AddListener((BaseEventData _) =>
			{
				onClick();
			});
			eventTrigger.triggers.Add(entry);
		}
	}

	private ToggleSettingItem FindToggleSetting(string key)
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
				if (entry is ToggleSettingItem toggleSettingItem && IsKey(toggleSettingItem, key))
				{
					return toggleSettingItem;
				}
			}
		}
		return null;
	}

	private (List<string> labels, int selectedIndex, List<object> values) BuildDropdownOptions(SettingItemBase entry)
	{
		List<string> list = new List<string>();
		List<object> list2 = new List<object>();
		int item = 0;
		if (entry is ToggleSettingItem toggleSettingItem)
		{
			list.Add("No");
			list.Add("Yes");
			list2.Add(false);
			list2.Add(true);
			item = (toggleSettingItem.value ? 1 : 0);
		}
		else if (entry is ResolutionSettingItem resolutionSettingItem)
		{
			Resolution[] availableResolutions = GetAvailableResolutions();
			for (int i = 0; i < availableResolutions.Length; i++)
			{
				string item2 = $"{availableResolutions[i].width}x{availableResolutions[i].height}";
				list.Add(item2);
				list2.Add(availableResolutions[i]);
				if (resolutionSettingItem.width == availableResolutions[i].width && resolutionSettingItem.height == availableResolutions[i].height)
				{
					item = i;
				}
			}
		}
		else if (entry is DropdownSettingItem { options: var list3 } dropdownSettingItem)
		{
			if (IsKey(dropdownSettingItem, "resolution"))
			{
				string currentSelection = dropdownSettingItem.CurrentOption;
				list3 = BuildResolutionOptions();
				int num = list3.FindIndex((string option) => string.Equals(option, currentSelection, StringComparison.OrdinalIgnoreCase));
				if (num >= 0)
				{
					dropdownSettingItem.index = num;
				}
				else
				{
					dropdownSettingItem.index = Mathf.Clamp(dropdownSettingItem.index, 0, Mathf.Max(0, list3.Count - 1));
				}
			}
			if (dropdownSettingItem.useDynamicOptions && dropdownSettingItem.optionsProvider is IDropdownOptionsProvider dropdownOptionsProvider)
			{
				list3 = dropdownOptionsProvider.GetOptions() ?? new List<string>();
				dropdownSettingItem.index = dropdownOptionsProvider.GetDefaultIndex(list3);
			}
			dropdownSettingItem.options = list3 ?? new List<string>();
			for (int num2 = 0; num2 < list3.Count; num2++)
			{
				list.Add(list3[num2]);
				list2.Add(list3[num2]);
				if (dropdownSettingItem.index == num2)
				{
					item = num2;
				}
			}
		}
		return (labels: list, selectedIndex: item, values: list2);
	}

	private void ApplyDropdownSelection(SettingItemBase entry, int index, (List<string> labels, int selectedIndex, List<object> values) options)
	{
		if (index >= 0 && index < options.values.Count)
		{
			object obj = options.values[index];
			if (entry is ToggleSettingItem toggleSettingItem)
			{
				toggleSettingItem.value = index == 1;
			}
			else if (entry is ResolutionSettingItem resolutionSettingItem && obj is Resolution resolution)
			{
				resolutionSettingItem.width = resolution.width;
				resolutionSettingItem.height = resolution.height;
			}
			else if (entry is DropdownSettingItem dropdownSettingItem && obj is string)
			{
				dropdownSettingItem.index = index;
			}
		}
	}

	private void ResetTabToDefaults(SettingsLayout.Tab tab)
	{
		if (layout == null || tab == null)
		{
			return;
		}
		List<SettingItemBase> list = new List<SettingItemBase>();
		foreach (SettingItemBase entry in tab.entries)
		{
			if (entry == null || entry is ResetSettingItem)
			{
				continue;
			}
			if (entry is ToggleSettingItem toggleSettingItem)
			{
				if (toggleSettingItem.value != toggleSettingItem.defaultValue)
				{
					toggleSettingItem.value = toggleSettingItem.defaultValue;
					list.Add(toggleSettingItem);
				}
			}
			else if (entry is SliderSettingItem sliderSettingItem)
			{
				float num = Mathf.Clamp(sliderSettingItem.defaultValue, sliderSettingItem.min, sliderSettingItem.max);
				if (sliderSettingItem.wholeNumbers)
				{
					num = Mathf.Round(num);
				}
				if (!Mathf.Approximately(sliderSettingItem.value, num))
				{
					sliderSettingItem.value = num;
					list.Add(sliderSettingItem);
				}
			}
			else
			{
				if (!(entry is DropdownSettingItem dropdownSettingItem) || !IsKey(dropdownSettingItem, "microphonedevice"))
				{
					continue;
				}
				if (dropdownSettingItem.useDynamicOptions && dropdownSettingItem.optionsProvider is IDropdownOptionsProvider dropdownOptionsProvider)
				{
					dropdownSettingItem.options = dropdownOptionsProvider.GetOptions() ?? dropdownSettingItem.options;
				}
				List<string> options = dropdownSettingItem.options;
				if (options != null && options.Count != 0)
				{
					int value = ((dropdownSettingItem.useDynamicOptions && dropdownSettingItem.optionsProvider is IDropdownOptionsProvider dropdownOptionsProvider2) ? dropdownOptionsProvider2.GetDefaultIndex(options) : 0);
					value = Mathf.Clamp(value, 0, options.Count - 1);
					if (dropdownSettingItem.index != value)
					{
						dropdownSettingItem.index = value;
						list.Add(dropdownSettingItem);
					}
				}
			}
		}
		foreach (SettingItemBase item in list)
		{
			NotifySettingsChanged(item);
		}
		if (!string.IsNullOrWhiteSpace(tab.tabName))
		{
			ShowTab(tab.tabName);
		}
	}

	private static void SetLabel(Transform root, SettingItemBase entry)
	{
		string text = ((entry != null) ? entry.DisplayLabel : string.Empty);
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		Transform transform = root.Find("SettingName");
		if (!(transform == null))
		{
			TMP_Text component = transform.GetComponent<TMP_Text>();
			if (component != null)
			{
				component.text = text;
			}
		}
	}

	private void NotifySettingsChanged(SettingItemBase entry)
	{
		if (!(layout == null) && !(entry == null))
		{
			layout.NotifyChanged(entry);
			entry.NotifyChanged();
		}
	}

	private void OnSettingChanged(SettingItemBase entry)
	{
		if (!(layout == null) && !(entry == null) && (IsKey(entry, "display") || IsKey(entry, "aspectratio") || IsKey(entry, "camSmoothingToggle")) && !string.IsNullOrWhiteSpace(_currentTabName))
		{
			ShowTab(_currentTabName);
		}
	}

	private DropdownSettingItem FindDropdownSetting(string key)
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

	private static bool IsKey(SettingItemBase entry, string key)
	{
		if (entry == null || string.IsNullOrWhiteSpace(entry.key))
		{
			return false;
		}
		return string.Equals(entry.key.Trim(), key, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsFullscreenExclusive(string displayMode)
	{
		if (string.IsNullOrWhiteSpace(displayMode))
		{
			return false;
		}
		string text = displayMode.Trim().ToLowerInvariant();
		if (!(text == "fullscreen"))
		{
			return text == "exclusive fullscreen";
		}
		return true;
	}

	private static bool IsFullscreenWindowed(string displayMode)
	{
		if (string.IsNullOrWhiteSpace(displayMode))
		{
			return false;
		}
		string text = displayMode.Trim().ToLowerInvariant();
		switch (text)
		{
		default:
			return text == "fullscreen windowed";
		case "windowed fullscreen":
		case "borderless":
		case "borderless fullscreen":
			return true;
		}
	}

	private static bool IsWindowed(string displayMode)
	{
		if (string.IsNullOrWhiteSpace(displayMode))
		{
			return false;
		}
		return displayMode.Trim().ToLowerInvariant() == "windowed";
	}

	private List<string> BuildResolutionOptions()
	{
		List<string> list = new List<string>();
		Resolution[] resolutions = Screen.resolutions;
		if (resolutions == null || resolutions.Length == 0)
		{
			list.Add($"{Screen.currentResolution.width}x{Screen.currentResolution.height}");
			return list;
		}
		float targetAspectRatio = GetTargetAspectRatio();
		HashSet<string> hashSet = new HashSet<string>();
		List<Resolution> list2 = new List<Resolution>();
		for (int i = 0; i < resolutions.Length; i++)
		{
			Resolution item = resolutions[i];
			if (!(Mathf.Abs((float)item.width / (float)item.height - targetAspectRatio) > 0.01f))
			{
				string item2 = $"{item.width}x{item.height}";
				if (hashSet.Add(item2))
				{
					list2.Add(item);
				}
			}
		}
		if (list2.Count == 0)
		{
			list.Add($"{Screen.currentResolution.width}x{Screen.currentResolution.height}");
			return list;
		}
		list2.Sort((Resolution a, Resolution b) =>
		{
			int value = a.width * a.height;
			return (b.width * b.height).CompareTo(value);
		});
		for (int num = 0; num < list2.Count; num++)
		{
			list.Add($"{list2[num].width}x{list2[num].height}");
		}
		return list;
	}

	private float GetTargetAspectRatio()
	{
		DropdownSettingItem dropdownSettingItem = FindDropdownSetting("display");
		if (IsFullscreenExclusive(dropdownSettingItem?.CurrentOption) || IsFullscreenWindowed(dropdownSettingItem?.CurrentOption))
		{
			return (float)Screen.currentResolution.width / (float)Screen.currentResolution.height;
		}
		if (TryParseAspectRatio(FindDropdownSetting("aspectratio")?.CurrentOption, out var ratio))
		{
			return ratio;
		}
		return (float)Screen.width / (float)Screen.height;
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

	private static Resolution[] GetAvailableResolutions()
	{
		Resolution[] resolutions = Screen.resolutions;
		if (resolutions == null || resolutions.Length == 0)
		{
			return new Resolution[1] { Screen.currentResolution };
		}
		List<Resolution> list = new List<Resolution>();
		for (int i = 0; i < resolutions.Length; i++)
		{
			Resolution item = resolutions[i];
			bool flag = false;
			for (int j = 0; j < list.Count; j++)
			{
				if (list[j].width == item.width && list[j].height == item.height)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				list.Add(item);
			}
		}
		list.Sort((Resolution a, Resolution b) =>
		{
			int value = a.width * a.height;
			return (b.width * b.height).CompareTo(value);
		});
		return list.ToArray();
	}
}

}
