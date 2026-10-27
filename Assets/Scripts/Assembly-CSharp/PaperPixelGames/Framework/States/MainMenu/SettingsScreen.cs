using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using Kamgam.LocalizationForSettings;
using Kamgam.SettingsGenerator;
using Kamgam.UGUIComponentsForSettings;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Settings;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace PaperPixelGames.Framework.States.MainMenu
{
	public class SettingsScreen : BaseScreen
	{
		[Header("Layout")]
		[SerializeField]
		private RectTransform _contentRect;

		[SerializeField]
		private ScrollRect _scrollRect;

		[SerializeField]
		private BaseButton _backButton;

		[Header("Scroll")]
		[SerializeField]
		private float _scrollDuration = 0.35f;

		[SerializeField]
		private Ease _scrollEase = Ease.InOutQuad;

		[Header("Category Buttons")]
		[SerializeField]
		private BaseButton _gameplayButton;

		[SerializeField]
		private BaseButton _comfortButton;

		[SerializeField]
		private BaseButton _soundButton;

		[SerializeField]
		private BaseButton _graphicsButton;

		[SerializeField]
		private BaseButton _controlsButton;

		[Header("Category Containers")]
		[SerializeField]
		private RectTransform _gameplayContainer;

		[SerializeField]
		private RectTransform _comfortContainer;

		[SerializeField]
		private RectTransform _soundContainer;

		[SerializeField]
		private RectTransform _graphicsContainer;

		[SerializeField]
		private RectTransform _controlsContainer;

		[Header("Camera Comfort")]
		[SerializeField]
		private ToggleUGUI _reduceMotionToggle;

		private SushiAndFish.FishingIntegration.TestingCheatSettings _testingCheats;

		private Tween _scrollTween;

		private LocalizationProvider _kamgamLocalizationProvider;

		private ILocalization _kamgamLocalization;

		private readonly List<LocalizeStringEvent> _comfortSettingLocalizers = new List<LocalizeStringEvent>();

		private readonly List<SettingBool> _reduceMotionSettings = new List<SettingBool>();

		private Settings _settings;

		private bool _isUpdatingReduceMotion;

		private DropDownUGUIResolver _languageResolver;

		private TMP_Dropdown _languageDropdown;

		private bool _isRefreshingLanguageDropdown;

		private int _localizationUiRefreshVersion;

		private int _controlsLayoutRefreshFrames;

		protected override void OnInitialize()
		{
			InitializeKamgamLocalizationBridge();
			BindLanguageDropdown();
			ConfigureComfortLabels();
			InitializeReduceMotionPreset();
			_testingCheats = GetComponent<SushiAndFish.FishingIntegration.TestingCheatSettings>();
			if (_testingCheats == null) _testingCheats = gameObject.AddComponent<SushiAndFish.FishingIntegration.TestingCheatSettings>();
			_testingCheats.Build(_gameplayContainer);
			ReflowCategoryLayout();
			if (_backButton != null)
			{
				_backButton.Clicked += OnBackButtonClick;
			}
			if (_gameplayButton != null)
			{
				_gameplayButton.Clicked += OnGameplayButtonClick;
			}
			if (_comfortButton != null)
			{
				_comfortButton.Clicked += OnComfortButtonClick;
			}
			if (_soundButton != null)
			{
				_soundButton.Clicked += OnSoundButtonClick;
			}
			if (_graphicsButton != null)
			{
				_graphicsButton.Clicked += OnGraphicsButtonClick;
			}
			if (_controlsButton != null)
			{
				_controlsButton.Clicked += OnControlsButtonClick;
			}
		}

		private void OnDisable()
		{
			if (_scrollTween != null && _scrollTween.IsActive())
			{
				_scrollTween.Kill();
			}
			_scrollTween = null;
		}

		private void OnDestroy()
		{
			_localizationUiRefreshVersion++;
			UnbindLanguageDropdown();
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			UnbindReduceMotionPreset();
		}

		protected override void OnShow()
		{
			_testingCheats?.Refresh();
			RefreshLanguageDropdown();
			RefreshKamgamLocalization();
			RefreshComfortSettingLabels();
			RefreshReduceMotionToggle();
			ReflowCategoryLayout();
			_controlsLayoutRefreshFrames = 3;
		}

		private void LateUpdate()
		{
			if (_controlsLayoutRefreshFrames <= 0) return;
			_controlsLayoutRefreshFrames--;
			ReflowCategoryLayout();
		}

		private void RefreshLanguageDropdown()
		{
			DropDownUGUIResolver languageResolver = GetLanguageResolver();
			if (languageResolver == null)
			{
				return;
			}
			SettingsProvider settingsProvider = languageResolver.SettingsProvider;
			if (settingsProvider == null || !settingsProvider.HasSettings())
			{
				return;
			}
			_isRefreshingLanguageDropdown = true;
			try
			{
				settingsProvider.Settings.RefreshSettingOptionConnectionAndResolvers<LanguageConnection>();
				languageResolver.Refresh();
				SyncLanguageDropdownSelection();
			}
			finally
			{
				_isRefreshingLanguageDropdown = false;
			}
		}

		private void BindLanguageDropdown()
		{
			DropDownUGUIResolver languageResolver = GetLanguageResolver();
			_languageDropdown = ((languageResolver != null) ? languageResolver.DropDownUGUI.DropDown : null);
			if (!(_languageDropdown == null))
			{
				_languageDropdown.onValueChanged.RemoveListener(OnLanguageDropdownValueChanged);
				_languageDropdown.onValueChanged.AddListener(OnLanguageDropdownValueChanged);
				SyncLanguageDropdownSelection();
			}
		}

		private void UnbindLanguageDropdown()
		{
			if (_languageDropdown != null)
			{
				_languageDropdown.onValueChanged.RemoveListener(OnLanguageDropdownValueChanged);
			}
			_languageDropdown = null;
			_languageResolver = null;
		}

		private DropDownUGUIResolver GetLanguageResolver()
		{
			if (_languageResolver != null)
			{
				return _languageResolver;
			}
			DropDownUGUIResolver[] componentsInChildren = GetComponentsInChildren<DropDownUGUIResolver>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				if (componentsInChildren[i] != null && componentsInChildren[i].ID == "language")
				{
					_languageResolver = componentsInChildren[i];
					return _languageResolver;
				}
			}
			return null;
		}

		private void OnLanguageDropdownValueChanged(int index)
		{
			if (!_isRefreshingLanguageDropdown)
			{
				DropDownUGUIResolver languageResolver = GetLanguageResolver();
				SettingsProvider settingsProvider = ((languageResolver != null) ? languageResolver.SettingsProvider : null);
				if (((settingsProvider != null && settingsProvider.HasSettings()) ? settingsProvider.Settings.GetOption("language") : null)?.GetConnectionInterface() is LanguageConnection languageConnection)
				{
					languageConnection.Set(index);
				}
			}
		}

		private void SyncLanguageDropdownSelection()
		{
			if (_languageDropdown == null)
			{
				return;
			}
			Locale selectedLocale = LocalizationSettings.SelectedLocale;
			if (selectedLocale == null)
			{
				return;
			}
			IReadOnlyList<LocalizationLanguageCatalog.LanguageDefinition> dropdownLanguages = LocalizationLanguageCatalog.DropdownLanguages;
			for (int i = 0; i < dropdownLanguages.Count; i++)
			{
				if (!(dropdownLanguages[i].LocaleCode != selectedLocale.Identifier.Code))
				{
					DropDownUGUIResolver languageResolver = GetLanguageResolver();
					SettingsProvider settingsProvider = ((languageResolver != null) ? languageResolver.SettingsProvider : null);
					SettingOption settingOption = ((settingsProvider != null && settingsProvider.HasSettings()) ? settingsProvider.Settings.GetOption("language") : null);
					if (settingOption != null && settingOption.GetValue() != i)
					{
						settingOption.SetValue(i, propagateChange: false);
					}
					_languageDropdown.SetValueWithoutNotify(i);
					break;
				}
			}
		}

		private void InitializeKamgamLocalizationBridge()
		{
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
			_kamgamLocalizationProvider = Resources.Load<LocalizationProvider>("LocalizationProvider");
			if (_kamgamLocalizationProvider == null || !_kamgamLocalizationProvider.HasLocalization())
			{
				UtilityMethods.Log("[SettingsLocalization] Kamgam LocalizationProvider could not be loaded.", LogType.Warning);
				return;
			}
			_kamgamLocalization = _kamgamLocalizationProvider.GetLocalization();
			_kamgamLocalization.SetDynamicLocalizationCallback(LocalizeKamgamTerm);
			RefreshKamgamLocalization();
		}

		private string LocalizeKamgamTerm(string term, string language)
		{
			string fallback = ((_kamgamLocalization != null) ? _kamgamLocalization.Get(term, ignoreDynamic: true) : term);
			return LocalizationDatabase.GetSettingsTerm(term, fallback);
		}

		private void OnSelectedLocaleChanged(Locale locale)
		{
			SyncLanguageDropdownSelection();
			RefreshLocalizedSettingsUiNextFrame();
		}

		private async void RefreshLocalizedSettingsUiNextFrame()
		{
			int requestVersion = ++_localizationUiRefreshVersion;
			await Task.Yield();
			if (!(this == null) && requestVersion == _localizationUiRefreshVersion)
			{
				RefreshKamgamLocalization();
				RefreshComfortSettingLabels();
			}
		}

		private void RefreshKamgamLocalization()
		{
			_kamgamLocalization?.TriggerLanguageChangeEvent();
		}

		private void ConfigureComfortLabels()
		{
			SetAllNamedLabels((_comfortButton != null) ? (_comfortButton.transform as RectTransform) : null, "settings.camera_comfort.tab", "Camera Comfort");
			SetNamedLabel(FindDescendant(_comfortContainer, "Header"), "settings.camera_comfort.header", "Camera Comfort");
			SetNamedLabel(FindDescendant(_comfortContainer, "ReduceMotionToggle"), "settings.camera_comfort.reduce_motion", "Reduce Motion");
			SetNamedLabel(FindDescendant(_comfortContainer, "CameraShakeToggle"), "settings.general.camera_shake", "Camera Shake");
			SetNamedLabel(FindDescendant(_comfortContainer, "CameraBobToggle"), "settings.general.camera_bob", "Camera Bob");
			SetNamedLabel(FindDescendant(_comfortContainer, "LandingCameraMotionToggle"), "settings.general.landing_camera_motion", "Landing Camera Motion");
			SetNamedLabel(FindDescendant(_comfortContainer, "DynamicFovEffectsToggle"), "settings.general.dynamic_fov_effects", "Dynamic FOV Effects");
			SetNamedLabel(FindDescendant(_comfortContainer, "UnderwaterDepthOfFieldToggle"), "settings.general.underwater_depth_of_field", "Underwater Depth of Field");
			SetNamedLabel(FindDescendant(_comfortContainer, "UnderwaterLensDistortionToggle"), "settings.camera_comfort.underwater_lens_distortion", "Underwater Lens Distortion");
			SetNamedLabel(FindDescendant(_comfortContainer, "ViewmodelMotionToggle"), "settings.camera_comfort.viewmodel_motion", "Viewmodel Motion");
			SetNamedLabel(FindDescendant(_comfortContainer, "DamageCameraMotionToggle"), "settings.camera_comfort.damage_camera_motion", "Damage Camera Movement");
			SetNamedLabel(FindDescendant(_comfortContainer, "FieldOfView"), "settings.general.field_of_view", "Field of View");
		}

		private void SetNamedLabel(RectTransform root, string localizationKey, string fallback)
		{
			if (!(root == null))
			{
				TMP_Text text = ResolveSettingLabel(root.gameObject, null);
				SetRuntimeLocalizedLabel(text, localizationKey, fallback);
			}
		}

		private void SetAllNamedLabels(RectTransform root, string localizationKey, string fallback)
		{
			if (!(root == null))
			{
				TMP_Text[] componentsInChildren = root.GetComponentsInChildren<TMP_Text>(includeInactive: true);
				foreach (TMP_Text text in componentsInChildren)
				{
					SetRuntimeLocalizedLabel(text, localizationKey, fallback);
				}
			}
		}

		private void InitializeReduceMotionPreset()
		{
			if (_reduceMotionToggle == null)
			{
				return;
			}
			ToggleUGUIResolver toggleUGUIResolver = ((_comfortContainer != null) ? _comfortContainer.GetComponentInChildren<ToggleUGUIResolver>(includeInactive: true) : null);
			if (toggleUGUIResolver == null || toggleUGUIResolver.SettingsProvider == null || !toggleUGUIResolver.SettingsProvider.HasSettings())
			{
				return;
			}
			_settings = toggleUGUIResolver.SettingsProvider.Settings;
			foreach (string settingId in CameraComfortPreset.SettingIds)
			{
				SettingBool settingBool = _settings.GetBool(settingId);
				if (settingBool != null)
				{
					settingBool.OnValueChanged += OnComfortSettingChanged;
					_reduceMotionSettings.Add(settingBool);
				}
			}
			ToggleUGUI reduceMotionToggle = _reduceMotionToggle;
			reduceMotionToggle.OnValueChanged = (ToggleUGUI.ValueChangedDelegate)Delegate.Combine(reduceMotionToggle.OnValueChanged, new ToggleUGUI.ValueChangedDelegate(OnReduceMotionChanged));
			RefreshReduceMotionToggle();
		}

		private void UnbindReduceMotionPreset()
		{
			if (_reduceMotionToggle != null)
			{
				ToggleUGUI reduceMotionToggle = _reduceMotionToggle;
				reduceMotionToggle.OnValueChanged = (ToggleUGUI.ValueChangedDelegate)Delegate.Remove(reduceMotionToggle.OnValueChanged, new ToggleUGUI.ValueChangedDelegate(OnReduceMotionChanged));
			}
			foreach (SettingBool reduceMotionSetting in _reduceMotionSettings)
			{
				reduceMotionSetting.OnValueChanged -= OnComfortSettingChanged;
			}
			_reduceMotionSettings.Clear();
		}

		private void OnReduceMotionChanged(bool enabled)
		{
			if (_isUpdatingReduceMotion || _settings == null)
			{
				return;
			}
			_isUpdatingReduceMotion = true;
			try
			{
				bool value = !enabled;
				foreach (SettingBool reduceMotionSetting in _reduceMotionSettings)
				{
					reduceMotionSetting.SetValue(value);
					_settings.RefreshRegisteredResolvers(reduceMotionSetting.GetID());
				}
			}
			finally
			{
				_isUpdatingReduceMotion = false;
			}
			RefreshReduceMotionToggle();
		}

		private void OnComfortSettingChanged(bool value)
		{
			if (!_isUpdatingReduceMotion)
			{
				RefreshReduceMotionToggle();
			}
		}

		private void RefreshReduceMotionToggle()
		{
			if (_reduceMotionToggle == null || _reduceMotionSettings.Count == 0)
			{
				return;
			}
			bool value = CameraComfortPreset.IsReduceMotionEnabled(_reduceMotionSettings);
			_isUpdatingReduceMotion = true;
			try
			{
				_reduceMotionToggle.Value = value;
			}
			finally
			{
				_isUpdatingReduceMotion = false;
			}
		}

		private static TMP_Text ResolveSettingLabel(GameObject root, TMP_Text preferredLabel)
		{
			if (preferredLabel != null)
			{
				return preferredLabel;
			}
			LocalizeStringEvent[] componentsInChildren = root.GetComponentsInChildren<LocalizeStringEvent>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				if (componentsInChildren[i].TryGetComponent<TMP_Text>(out var component))
				{
					return component;
				}
			}
			TMP_Text[] componentsInChildren2 = root.GetComponentsInChildren<TMP_Text>(includeInactive: true);
			TMP_Text[] array = componentsInChildren2;
			foreach (TMP_Text tMP_Text in array)
			{
				if (tMP_Text.name == "Label")
				{
					return tMP_Text;
				}
			}
			if (componentsInChildren2.Length == 0)
			{
				return null;
			}
			return componentsInChildren2[0];
		}

		private void SetRuntimeLocalizedLabel(TMP_Text text, string key, string fallback)
		{
			if (!(text == null))
			{
				text.text = fallback;
				LocalizeStringEvent localizeStringEvent = text.GetComponent<LocalizeStringEvent>();
				if (localizeStringEvent == null)
				{
					localizeStringEvent = text.gameObject.AddComponent<LocalizeStringEvent>();
				}
				localizeStringEvent.StringReference = new LocalizedString("SettingsTable", key);
				if (localizeStringEvent.OnUpdateString.GetPersistentEventCount() == 0)
				{
					localizeStringEvent.OnUpdateString.AddListener(text.SetText);
				}
				if (!_comfortSettingLocalizers.Contains(localizeStringEvent))
				{
					_comfortSettingLocalizers.Add(localizeStringEvent);
				}
				localizeStringEvent.RefreshString();
			}
		}

		private void RefreshComfortSettingLabels()
		{
			foreach (LocalizeStringEvent comfortSettingLocalizer in _comfortSettingLocalizers)
			{
				if (comfortSettingLocalizer != null)
				{
					comfortSettingLocalizer.RefreshString();
				}
			}
		}

		private static RectTransform FindDescendant(RectTransform root, string objectName)
		{
			if (root == null)
			{
				return null;
			}
			RectTransform[] componentsInChildren = root.GetComponentsInChildren<RectTransform>(includeInactive: true);
			foreach (RectTransform rectTransform in componentsInChildren)
			{
				if (rectTransform.name == objectName)
				{
					return rectTransform;
				}
			}
			return null;
		}

		private void ReflowCategoryLayout()
		{
			_testingCheats?.RefreshLayout();
			ReflowControlsLayout();
			if (!(_gameplayContainer == null) && !(_comfortContainer == null) && !(_graphicsContainer == null) && !(_soundContainer == null) && !(_controlsContainer == null))
			{
				SetCategoryY(_comfortContainer, _gameplayContainer.anchoredPosition.y - _gameplayContainer.sizeDelta.y);
				SetCategoryY(_graphicsContainer, _comfortContainer.anchoredPosition.y - _comfortContainer.sizeDelta.y);
				SetCategoryY(_soundContainer, _graphicsContainer.anchoredPosition.y - _graphicsContainer.sizeDelta.y);
				SetCategoryY(_controlsContainer, _soundContainer.anchoredPosition.y - _soundContainer.sizeDelta.y);
				if (_contentRect != null)
				{
					Vector2 sizeDelta = _contentRect.sizeDelta;
					sizeDelta.y = 0f - _controlsContainer.anchoredPosition.y + _controlsContainer.sizeDelta.y;
					_contentRect.sizeDelta = sizeDelta;
				}
			}
		}

		// The keyboard list intentionally ignores its parent's layout. Position it
		// after all control options, including the fishing options added in 0.3.6.
		private void ReflowControlsLayout()
		{
			if (_controlsContainer == null) return;
			var content = _controlsContainer.Find("Content") as RectTransform;
			var keyboard = content != null ? content.Find("KeyboardLayout") as RectTransform : null;
			if (content == null || keyboard == null || !content.TryGetComponent<VerticalLayoutGroup>(out var layout)) return;
			layout.spacing = 70f;
			float optionHeight = layout.padding.top + layout.padding.bottom;
			int optionCount = 0;
			foreach (RectTransform row in content)
			{
				if (row == keyboard || !row.gameObject.activeSelf) continue;
				optionHeight += row.rect.height;
				optionCount++;
				var label = row.Find("Label")?.GetComponent<TMP_Text>();
				if (label == null) continue;
				var rect = label.rectTransform;
				rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
				rect.pivot = new Vector2(1f, .5f);
				rect.sizeDelta = new Vector2(440f, 90f);
				rect.anchoredPosition = new Vector2(-32f, 0f);
				label.enableWordWrapping = true;
				label.fontSize = 34f;
			}
			optionHeight += Mathf.Max(0, optionCount - 1) * layout.spacing;
			LayoutRebuilder.ForceRebuildLayoutImmediate(content);
			LayoutRebuilder.ForceRebuildLayoutImmediate(keyboard);
			var keyboardPosition = keyboard.anchoredPosition;
			keyboardPosition.y = -optionHeight - 55f;
			keyboard.anchoredPosition = keyboardPosition;
			var size = _controlsContainer.sizeDelta;
			float bottom = optionHeight + 55f + keyboard.rect.height;
			var corners = new Vector3[4];
			foreach (var rect in keyboard.GetComponentsInChildren<RectTransform>())
			{
				rect.GetWorldCorners(corners);
				foreach (var corner in corners)
					bottom = Mathf.Max(bottom, -_controlsContainer.InverseTransformPoint(corner).y);
			}
			size.y = bottom + 60f;
			_controlsContainer.sizeDelta = size;
		}

		private static void SetCategoryY(RectTransform category, float y)
		{
			Vector2 anchoredPosition = category.anchoredPosition;
			anchoredPosition.y = y;
			category.anchoredPosition = anchoredPosition;
		}

		public void OnGameplayButtonClick()
		{
			ScrollTo(_gameplayContainer);
		}

		public void OnComfortButtonClick()
		{
			ScrollTo(_comfortContainer);
		}

		public void OnSoundButtonClick()
		{
			ScrollTo(_soundContainer);
		}

		public void OnGraphicsButtonClick()
		{
			ScrollTo(_graphicsContainer);
		}

		public void OnControlsButtonClick()
		{
			ScrollTo(_controlsContainer);
		}

		private void OnBackButtonClick()
		{
			Hide();
		}

		public void ScrollTo(RectTransform target)
		{
			if (_scrollRect == null || target == null)
			{
				return;
			}
			RectTransform rectTransform = ((_contentRect != null) ? _contentRect : _scrollRect.content);
			if (rectTransform == null)
			{
				return;
			}
			RectTransform rectTransform2 = ((_scrollRect.viewport != null) ? _scrollRect.viewport : ((RectTransform)_scrollRect.transform));
			Canvas.ForceUpdateCanvases();
			float height = rectTransform.rect.height;
			float height2 = rectTransform2.rect.height;
			float num = height - height2;
			if (num <= 0.0001f)
			{
				_scrollRect.verticalNormalizedPosition = 1f;
				return;
			}
			float num2 = Mathf.Clamp01((0f - rectTransform.InverseTransformPoint(target.position).y) / num);
			float endValue = 1f - num2;
			if (_scrollTween != null && _scrollTween.IsActive())
			{
				_scrollTween.Kill();
			}
			_scrollTween = DOTween.To(() => _scrollRect.verticalNormalizedPosition, (float x) =>
			{
				_scrollRect.verticalNormalizedPosition = x;
			}, endValue, _scrollDuration).SetEase(_scrollEase).SetUpdate(isIndependentUpdate: true)
				.SetLink(base.gameObject);
		}
	}
}
