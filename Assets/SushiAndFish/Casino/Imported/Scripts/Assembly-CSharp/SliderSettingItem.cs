namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

[CreateAssetMenu(menuName = "Game Settings/Setting Item/Slider", fileName = "SliderSetting")]
public class SliderSettingItem : SettingItemBase
{
	public float min;

	public float max = 1f;

	public bool wholeNumbers;

	public float value;

	public float defaultValue;

	[Tooltip("If true, this setting will be applied on every scene load from saved settings")]
	public bool loadOnSceneStart;

	public override SettingKind Kind => SettingKind.Slider;
}

}
