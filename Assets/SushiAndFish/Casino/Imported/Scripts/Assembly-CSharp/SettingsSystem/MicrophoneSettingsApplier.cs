namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::Dissonance;
using global::UnityEngine;

namespace SettingsSystem
{
	public class MicrophoneSettingsApplier : ISettingsApplier
	{
		private string _savedMicrophoneDeviceName;

		private Coroutine _microphoneApplyCoroutine;

		private MonoBehaviour _coroutineRunner;

		public MicrophoneSettingsApplier(MonoBehaviour coroutineRunner)
		{
			_coroutineRunner = coroutineRunner;
		}

		public void Apply(SettingItemBase entry)
		{
			if (CasinoLab.CasinoLabMode.Active) return;
			if (!(entry == null) && !string.IsNullOrWhiteSpace(entry.key) && entry.key.Trim().ToLowerInvariant() == "microphonedevice" && entry is DropdownSettingItem { CurrentOption: var currentOption })
			{
				_savedMicrophoneDeviceName = currentOption;
				ApplyMicrophoneDeviceName(currentOption);
			}
		}

		public void ApplyAll(SettingsLayout layout)
		{
			if (layout == null)
			{
				return;
			}
			foreach (SettingsLayout.Tab tab in layout.tabs)
			{
				if (tab == null)
				{
					continue;
				}
				foreach (SettingItemBase entry in tab.entries)
				{
					Apply(entry);
				}
			}
		}

		public void ApplyOnSceneLoad()
		{
			if (!string.IsNullOrEmpty(_savedMicrophoneDeviceName))
			{
				if (_microphoneApplyCoroutine != null)
				{
					_coroutineRunner.StopCoroutine(_microphoneApplyCoroutine);
				}
				_microphoneApplyCoroutine = _coroutineRunner.StartCoroutine(ApplyMicrophoneSettingsCoroutine());
			}
		}

		public void SetSavedDeviceName(string deviceName)
		{
			_savedMicrophoneDeviceName = deviceName;
		}

		public string GetSavedDeviceName()
		{
			return _savedMicrophoneDeviceName;
		}

		public void StopCoroutines()
		{
			if (_microphoneApplyCoroutine != null)
			{
				_coroutineRunner.StopCoroutine(_microphoneApplyCoroutine);
				_microphoneApplyCoroutine = null;
			}
		}

		private void ApplyMicrophoneDeviceName(string deviceName)
		{
			DissonanceComms singleton = DissonanceComms.GetSingleton();
			if (singleton == null)
			{
				Debug.LogWarning("[MicrophoneSettingsApplier] DissonanceComms not found, will retry on scene load.");
				return;
			}
			string text = deviceName?.Trim();
			if (string.IsNullOrWhiteSpace(text) || string.Equals(text, "Default", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "System Default", StringComparison.OrdinalIgnoreCase))
			{
				singleton.MicrophoneName = null;
			}
			else
			{
				singleton.MicrophoneName = text;
			}
		}

		private IEnumerator ApplyMicrophoneSettingsCoroutine()
		{
			for (int attempt = 0; attempt < 10; attempt++)
			{
				if (DissonanceComms.GetSingleton() != null)
				{
					ApplyMicrophoneDeviceName(_savedMicrophoneDeviceName);
					_microphoneApplyCoroutine = null;
					yield break;
				}
				yield return new WaitForSeconds(0.1f);
			}
			Debug.LogWarning("[MicrophoneSettingsApplier] Failed to apply microphone setting after scene load - DissonanceComms not found after multiple attempts.");
			_microphoneApplyCoroutine = null;
		}
	}
}

}
