using System;
using System.Collections;
using Kamgam.SettingsGenerator;
using PaperPixelGames.Core.Settings;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PaperPixelGames.Framework.States.MainMenu
{
	public sealed class MotionSicknessPopup : MonoBehaviour
	{
		public const string CompletionPlayerPrefsKey = "Accessibility.MotionSicknessPromptCompleted.v1";

		[SerializeField]
		private BaseButton _reduceMotionButton;

		[SerializeField]
		private BaseButton _keepDefaultsButton;

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
				if (_reduceMotionButton != null)
				{
					_reduceMotionButton.Clicked += OnReduceMotionSelected;
				}
				if (_keepDefaultsButton != null)
				{
					_keepDefaultsButton.Clicked += OnKeepDefaultsSelected;
				}
			}
		}

		public bool TryShow()
		{
			Initialize();
			if (PlayerPrefs.HasKey("Accessibility.MotionSicknessPromptCompleted.v1"))
			{
				base.gameObject.SetActive(value: false);
				return false;
			}
			_isCompleting = false;
			base.gameObject.SetActive(value: true);
			FocusDefaultChoice();
			return true;
		}

		public void FocusDefaultChoice()
		{
			if (base.isActiveAndEnabled)
			{
				StartCoroutine(SelectDefaultChoiceNextFrame());
			}
		}

		private IEnumerator SelectDefaultChoiceNextFrame()
		{
			yield return null;
			if (base.isActiveAndEnabled && !(_keepDefaultsButton == null) && !(EventSystem.current == null))
			{
				EventSystem.current.SetSelectedGameObject(_keepDefaultsButton.gameObject);
			}
		}

		private void OnReduceMotionSelected()
		{
			if (!_isCompleting)
			{
				if (!CameraComfortPreset.TryApplyReduceMotion(_settingsProvider, reduceMotionEnabled: true, out var error))
				{
					UtilityMethods.Log("[MotionSicknessPopup] Reduce Motion could not be applied. " + error, LogType.Error);
				}
				else
				{
					CompletePrompt();
				}
			}
		}

		private void OnKeepDefaultsSelected()
		{
			if (!_isCompleting)
			{
				CompletePrompt();
			}
		}

		private void CompletePrompt()
		{
			_isCompleting = true;
			PlayerPrefs.SetInt("Accessibility.MotionSicknessPromptCompleted.v1", 1);
			PlayerPrefs.Save();
			base.gameObject.SetActive(value: false);
			Completed?.Invoke();
		}

		private void OnDestroy()
		{
			if (_reduceMotionButton != null)
			{
				_reduceMotionButton.Clicked -= OnReduceMotionSelected;
			}
			if (_keepDefaultsButton != null)
			{
				_keepDefaultsButton.Clicked -= OnKeepDefaultsSelected;
			}
		}
	}
}
