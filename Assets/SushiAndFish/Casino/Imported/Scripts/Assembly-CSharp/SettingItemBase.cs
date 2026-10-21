namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

public abstract class SettingItemBase : ScriptableObject
{
	public string key;

	public string label;

	public abstract SettingKind Kind { get; }

	public string DisplayLabel
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(label))
			{
				return label;
			}
			return key;
		}
	}

	public static event Action<SettingItemBase> SettingsChanged;

	public void NotifyChanged()
	{
		SettingsChanged?.Invoke(this);
	}
}

}
