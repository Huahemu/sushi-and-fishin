namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::UnityEngine;

[CreateAssetMenu(menuName = "Game Settings/Settings Layout", fileName = "SettingsLayout")]
public class SettingsLayout : ScriptableObject
{
	[Serializable]
	public class Tab
	{
		public string tabName = "General";

		public List<SettingItemBase> entries = new List<SettingItemBase>();
	}

	public List<Tab> tabs = new List<Tab>();

	public static event Action<SettingsLayout, SettingItemBase> SettingsChanged;

	public void NotifyChanged(SettingItemBase entry)
	{
		SettingsChanged?.Invoke(this, entry);
	}
}

}
