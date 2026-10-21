using System;
using System.Collections;
using Kamgam.SettingsGenerator;
using Kamgam.UGUIComponentsForSettings;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PaperPixelGames.Framework.States.MainMenu
{
	public sealed class RapidPressBindingPopup : MonoBehaviour
	{
		public const string CompletionPlayerPrefsKey = "Accessibility.RapidPressBindingPromptCompleted.v1";

		private const string RapidPressSettingId = "InputBinding.Player.RapidPress.Mouse.leftButton";

		private const string DefaultBindingPath = "<Mouse>/leftButton";

		[SerializeField]
		private InputBindingUGUI _inputBinding;

		[SerializeField]
		private BaseButton _useMouse1Button;

		[SerializeField]
		private SettingsProvider _settingsProvider;

		private bool _initialized;

		private bool _isCompleting;

		public bool IsVisible => base.gameObject.activeSelf;

		public event Action Completed;

		public void Initialize()
		{
			if (!_initialized)
			{
				_initialized = true;
				if (_inputBinding != null)
				{
					InputBindingUGUI inputBinding = _inputBinding;
					inputBinding.OnChanged = (InputBindingUGUI.OnChangedDelegate)Delegate.Combine(inputBinding.OnChanged, new InputBindingUGUI.OnChangedDelegate(OnBindingChanged));
				}
				if (_useMouse1Button != null)
				{
					_useMouse1Button.Clicked += UseMouse1AndClose;
				}
			}
		}

		public bool TryShow()
		{
			Initialize();
			if (PlayerPrefs.HasKey("Accessibility.RapidPressBindingPromptCompleted.v1"))
			{
				base.gameObject.SetActive(value: false);
				return false;
			}
			if (_inputBinding == null)
			{
				UtilityMethods.Log("[RapidPressBindingPopup] Input binding UI is not assigned.", LogType.Error);
				return false;
			}
			_isCompleting = false;
			base.gameObject.SetActive(value: true);
			StartCoroutine(FocusBindingNextFrame());
			return true;
		}

		private IEnumerator FocusBindingNextFrame()
		{
			yield return null;
			if (base.isActiveAndEnabled && !(_inputBinding == null) && !(_inputBinding.Button == null) && !(EventSystem.current == null))
			{
				EventSystem.current.SetSelectedGameObject(_inputBinding.Button.gameObject);
			}
		}

		private void OnBindingChanged(string bindingPath)
		{
			if (!_isCompleting)
			{
				Complete(bindingPath);
			}
		}

		public void UseMouse1AndClose()
		{
			if (!_isCompleting)
			{
				Complete("<Mouse>/leftButton");
			}
		}

		private void Complete(string bindingPath)
		{
			if (_settingsProvider == null || _settingsProvider.Settings == null)
			{
				UtilityMethods.Log("[RapidPressBindingPopup] Settings provider is unavailable.", LogType.Error);
				return;
			}
			SettingString settingString = _settingsProvider.Settings.GetString("InputBinding.Player.RapidPress.Mouse.leftButton");
			if (settingString == null)
			{
				UtilityMethods.Log("[RapidPressBindingPopup] Setting was not found: InputBinding.Player.RapidPress.Mouse.leftButton", LogType.Error);
				return;
			}
			bindingPath = InputBindingForInputSystem.NormalizeBindingPath(bindingPath);
			_isCompleting = true;
			settingString.SetValue(bindingPath);
			_settingsProvider.Save();
			PlayerPrefs.SetInt("Accessibility.RapidPressBindingPromptCompleted.v1", 1);
			PlayerPrefs.Save();
			base.gameObject.SetActive(value: false);
			Completed?.Invoke();
		}

		private void OnDestroy()
		{
			if (_inputBinding != null)
			{
				InputBindingUGUI inputBinding = _inputBinding;
				inputBinding.OnChanged = (InputBindingUGUI.OnChangedDelegate)Delegate.Remove(inputBinding.OnChanged, new InputBindingUGUI.OnChangedDelegate(OnBindingChanged));
			}
			if (_useMouse1Button != null)
			{
				_useMouse1Button.Clicked -= UseMouse1AndClose;
			}
		}
	}
}
