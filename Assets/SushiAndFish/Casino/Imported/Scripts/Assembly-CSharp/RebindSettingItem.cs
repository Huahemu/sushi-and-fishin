namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

[CreateAssetMenu(menuName = "Game Settings/Setting Item/Rebind", fileName = "RebindSetting")]
public class RebindSettingItem : SettingItemBase
{
	[Tooltip("Input action name from InputActions, e.g. Jump")]
	public string actionName;

	[Tooltip("Binding index on the action to rebind.")]
	public int bindingIndex;

	[HideInInspector]
	public string overridePath;

	[Tooltip("If true, this setting will be applied on every scene load from saved settings")]
	public bool loadOnSceneStart;

	public override SettingKind Kind => SettingKind.Rebind;
}

}
