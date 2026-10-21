namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::Dissonance;
using global::UnityEngine;

[CreateAssetMenu(menuName = "Game Settings/Dropdown Provider/Microphone Devices", fileName = "MicrophoneDevicesProvider")]
public class MicrophoneDevicesProvider : ScriptableObject, IDropdownOptionsProvider
{
	public bool includeDefaultOption = true;

	public string defaultLabel = "System Default";

	public List<string> GetOptions()
	{
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		DissonanceComms dissonanceComms = UnityEngine.Object.FindFirstObjectByType<DissonanceComms>();
		if (dissonanceComms != null)
		{
			dissonanceComms.GetMicrophoneDevices(list2);
		}
		else
		{
			string[] devices = UnityEngine.Microphone.devices;
			if (devices != null && devices.Length != 0)
			{
				list2.AddRange(devices);
			}
		}
		if (includeDefaultOption)
		{
			list.Add(defaultLabel);
		}
		for (int i = 0; i < list2.Count; i++)
		{
			string text = list2[i];
			if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text, defaultLabel, StringComparison.OrdinalIgnoreCase) && !list.Contains(text))
			{
				list.Add(text);
			}
		}
		return list;
	}

	public int GetDefaultIndex(List<string> options)
	{
		if (options == null || options.Count == 0)
		{
			return 0;
		}
		if (includeDefaultOption)
		{
			int num = options.FindIndex((string option) => string.Equals(option, defaultLabel, StringComparison.OrdinalIgnoreCase));
			if (num >= 0)
			{
				return num;
			}
		}
		return 0;
	}
}

}
