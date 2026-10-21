namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

[CreateAssetMenu(menuName = "Game Settings/Setting Item/Toggle", fileName = "ToggleSetting")]
public class ToggleSettingItem : SettingItemBase
{
	public bool value;

	public bool defaultValue;

	[Tooltip("If true, this setting will be applied on every scene load from saved settings")]
	public bool loadOnSceneStart;

	public override SettingKind Kind => SettingKind.Dropdown;
}

}
