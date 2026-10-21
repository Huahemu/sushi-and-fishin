namespace SushiAndFish.Casino.Original
{
using global::System;
using global::Dissonance;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.UI;

public class VoiceVolumeEntry : MonoBehaviour
{
	[SerializeField]
	private TMP_Text nameLabel;

	[SerializeField]
	private Slider volumeSlider;

	private VoicePlayerState _state;

	private string _steamId;

	private Action<string, float> _onVolumeChanged;

	public void Setup(VoicePlayerState state, string displayName, string steamId, float initialVolume, Action<string, float> onVolumeChanged)
	{
		_state = state;
		_steamId = steamId ?? "";
		_onVolumeChanged = onVolumeChanged;
		if (nameLabel != null)
		{
			nameLabel.text = (string.IsNullOrEmpty(displayName) ? "Player" : displayName);
		}
		if (volumeSlider != null)
		{
			volumeSlider.SetValueWithoutNotify(initialVolume);
			volumeSlider.onValueChanged.RemoveAllListeners();
			volumeSlider.onValueChanged.AddListener(OnSliderChanged);
		}
		ApplyVolume(initialVolume);
	}

	public void UpdateIdentity(string displayName, string steamId)
	{
		_steamId = steamId ?? "";
		if (nameLabel != null)
		{
			nameLabel.text = (string.IsNullOrEmpty(displayName) ? "Player" : displayName);
		}
	}

	private void OnSliderChanged(float value)
	{
		ApplyVolume(value);
		_onVolumeChanged?.Invoke(_steamId, value);
	}

	private void ApplyVolume(float value)
	{
		if (_state != null)
		{
			_state.Volume = value;
		}
	}

	private void OnDestroy()
	{
		if (volumeSlider != null)
		{
			volumeSlider.onValueChanged.RemoveAllListeners();
		}
	}
}

}
