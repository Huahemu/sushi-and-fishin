namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::UnityEngine;

[CreateAssetMenu(menuName = "Game Settings/Setting Item/Dropdown", fileName = "DropdownSetting")]
public class DropdownSettingItem : SettingItemBase
{
	public List<string> options = new List<string>();

	public int index;

	public bool useDynamicOptions;

	public ScriptableObject optionsProvider;

	[Tooltip("If true, this setting will be applied on every scene load from saved settings")]
	public bool loadOnSceneStart;

	public string CurrentOption
	{
		get
		{
			if (options == null || index < 0 || index >= options.Count)
			{
				return string.Empty;
			}
			return options[index];
		}
	}

	public override SettingKind Kind => SettingKind.Dropdown;
}

}
