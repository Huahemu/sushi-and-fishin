namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.UI;

public class LobbyModeDropdownButton : MonoBehaviour
{
	[SerializeField]
	private DropdownSettingItem lobbyModeSetting;

	[SerializeField]
	private SettingsLayout settingsLayout;

	[SerializeField]
	private Image targetImage;

	[SerializeField]
	private Sprite friendsOnlySprite;

	[SerializeField]
	private Sprite privateSprite;

	[Header("Mode Panel")]
	[SerializeField]
	private CanvasGroup modePanel;

	[SerializeField]
	private TextMeshProUGUI modeLabel;

	[SerializeField]
	private float fadeDuration = 0.25f;

	[SerializeField]
	private float visibleTime = 1.5f;

	private int _modeIndex;

	private Coroutine _fadeRoutine;

	private void Awake()
	{
		InitFromSetting();
		ApplyVisual(updateSetting: false);
	}

	private void OnEnable()
	{
		SettingItemBase.SettingsChanged += OnSettingChanged;
		InitFromSetting();
		ApplyVisual(updateSetting: false);
	}

	private void OnDisable()
	{
		SettingItemBase.SettingsChanged -= OnSettingChanged;
	}

	public void OnClick()
	{
		_modeIndex = (_modeIndex + 1) % 2;
		ApplyVisual(updateSetting: true);
	}

	private void InitFromSetting()
	{
		if (!(lobbyModeSetting == null))
		{
			_modeIndex = Mathf.Clamp(lobbyModeSetting.index, 0, 1);
		}
	}

	private void ApplyVisual(bool updateSetting)
	{
		if (targetImage != null)
		{
			targetImage.sprite = ((_modeIndex == 1) ? privateSprite : friendsOnlySprite);
		}
		string text = ((_modeIndex == 1) ? "Invite Only" : "Open To Friends");
		ShowModePanel(text);
		if (updateSetting && !(lobbyModeSetting == null) && _modeIndex >= 0 && _modeIndex < lobbyModeSetting.options.Count)
		{
			lobbyModeSetting.index = _modeIndex;
			if (settingsLayout != null)
			{
				settingsLayout.NotifyChanged(lobbyModeSetting);
			}
			lobbyModeSetting.NotifyChanged();
		}
	}

	private void ShowModePanel(string text)
	{
		if (!(modePanel == null) && !(modeLabel == null))
		{
			modeLabel.text = text;
			if (_fadeRoutine != null)
			{
				StopCoroutine(_fadeRoutine);
			}
			_fadeRoutine = StartCoroutine(FadePanelRoutine());
		}
	}

	private IEnumerator FadePanelRoutine()
	{
		if (!(modePanel == null))
		{
			modePanel.gameObject.SetActive(value: true);
			float t = 0f;
			while (t < fadeDuration)
			{
				t += Time.deltaTime;
				float alpha = ((fadeDuration > 0f) ? Mathf.Clamp01(t / fadeDuration) : 1f);
				modePanel.alpha = alpha;
				yield return null;
			}
			modePanel.alpha = 1f;
			if (visibleTime > 0f)
			{
				yield return new WaitForSeconds(visibleTime);
			}
			t = 0f;
			while (t < fadeDuration)
			{
				t += Time.deltaTime;
				float alpha2 = ((fadeDuration > 0f) ? (1f - Mathf.Clamp01(t / fadeDuration)) : 0f);
				modePanel.alpha = alpha2;
				yield return null;
			}
			modePanel.alpha = 0f;
			modePanel.gameObject.SetActive(value: false);
			_fadeRoutine = null;
		}
	}

	private void OnSettingChanged(SettingItemBase entry)
	{
		if (!(entry == null) && !string.IsNullOrWhiteSpace(entry.key) && string.Equals(entry.key.Trim(), "lobbymode", StringComparison.OrdinalIgnoreCase))
		{
			InitFromSetting();
			ApplyVisual(updateSetting: false);
		}
	}
}

}
