namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

[CreateAssetMenu(menuName = "Game Settings/Sound Settings", fileName = "SoundSettings")]
public class SoundSettings : ScriptableObject
{
	public SliderSettingItem masterVol;

	public SliderSettingItem musicVol;

	public SliderSettingItem sFXVol;

	public SliderSettingItem proxChatVol;

	public static event Action<SoundSettings> SettingsChanged;

	private void NotifyChanged()
	{
		SettingsChanged?.Invoke(this);
	}

	public void TriggerSettingsChanged()
	{
		NotifyChanged();
	}
}

}
