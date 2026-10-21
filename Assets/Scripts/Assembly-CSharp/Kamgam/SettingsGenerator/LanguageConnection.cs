using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Kamgam.SettingsGenerator
{
	public class LanguageConnection : ConnectionWithOptions<string>
	{
		protected List<string> _labels;

		private bool _localeChangeBound;

		private int _localeSetRequestVersion;

		private bool _destroyed;

		public override List<string> GetOptionLabels()
		{
			if (_labels == null || _labels.Count != LocalizationLanguageCatalog.DropdownLanguages.Count)
			{
				_labels = new List<string>(LocalizationLanguageCatalog.DropdownLanguages.Count);
				_ = LocalizationSettings.SelectedLocale;
				BindLocaleChange();
				foreach (LocalizationLanguageCatalog.LanguageDefinition dropdownLanguage in LocalizationLanguageCatalog.DropdownLanguages)
				{
					_labels.Add(dropdownLanguage.NativeName);
				}
			}
			return _labels;
		}

		public override void SetOptionLabels(List<string> optionLabels)
		{
		}

		public override void RefreshOptionLabels()
		{
			_labels = null;
			GetOptionLabels();
		}

		public override int Get()
		{
			Locale selectedLocale = LocalizationSettings.SelectedLocale;
			if (selectedLocale == null)
			{
				return 0;
			}
			IReadOnlyList<LocalizationLanguageCatalog.LanguageDefinition> dropdownLanguages = LocalizationLanguageCatalog.DropdownLanguages;
			for (int i = 0; i < dropdownLanguages.Count; i++)
			{
				if (dropdownLanguages[i].LocaleCode == selectedLocale.Identifier.Code)
				{
					return i;
				}
			}
			return 0;
		}

		public override void Set(int index)
		{
			IReadOnlyList<LocalizationLanguageCatalog.LanguageDefinition> dropdownLanguages = LocalizationLanguageCatalog.DropdownLanguages;
			if (index < 0 || index >= dropdownLanguages.Count)
			{
				Debug.LogWarning($"[LocalizationSettings] Invalid language option index. Index={index} Options={dropdownLanguages.Count}");
				return;
			}
			int requestVersion = ++_localeSetRequestVersion;
			if (LocalizationSettings.InitializationOperation.IsDone)
			{
				ApplyLocale(index, dropdownLanguages[index].LocaleCode, requestVersion);
			}
			else
			{
				SetLocaleWhenReady(index, dropdownLanguages[index].LocaleCode, requestVersion);
			}
		}

		private async void SetLocaleWhenReady(int index, string localeCode, int requestVersion)
		{
			try
			{
				if (!LocalizationSettings.InitializationOperation.IsDone)
				{
					await LocalizationSettings.InitializationOperation.Task;
				}
				if (!_destroyed && requestVersion == _localeSetRequestVersion)
				{
					ApplyLocale(index, localeCode, requestVersion);
				}
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}

		private void ApplyLocale(int index, string localeCode, int requestVersion)
		{
			if (_destroyed || requestVersion != _localeSetRequestVersion)
			{
				return;
			}
			Locale locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
			if (locale == null)
			{
				Debug.LogWarning("[LocalizationSettings] Supported locale is missing. Code=" + localeCode);
				return;
			}
			if (LocalizationSettings.SelectedLocale != locale)
			{
				LocalizationSettings.SelectedLocale = locale;
			}
			NotifyListenersIfChanged(index);
		}

		private void BindLocaleChange()
		{
			if (!_localeChangeBound)
			{
				LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
				_localeChangeBound = true;
			}
		}

		private void OnSelectedLocaleChanged(Locale locale)
		{
			NotifyListenersIfChanged(Get());
		}

		public override void Destroy()
		{
			_destroyed = true;
			_localeSetRequestVersion++;
			if (_localeChangeBound)
			{
				LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
				_localeChangeBound = false;
			}
			base.Destroy();
		}
	}
}
