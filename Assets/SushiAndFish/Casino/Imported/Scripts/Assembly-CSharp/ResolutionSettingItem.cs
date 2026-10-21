namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

[CreateAssetMenu(menuName = "Game Settings/Setting Item/Resolution", fileName = "ResolutionSetting")]
public class ResolutionSettingItem : SettingItemBase
{
	public int width = 1920;

	public int height = 1080;

	public override SettingKind Kind => SettingKind.Dropdown;
}

}
