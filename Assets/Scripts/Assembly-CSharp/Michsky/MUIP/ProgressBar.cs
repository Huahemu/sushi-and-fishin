using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	public class ProgressBar : MonoBehaviour
	{
		[Serializable]
		public class ProgressBarEvent : UnityEvent<float>
		{
		}

		public float currentPercent;

		[Range(0f, 100f)]
		public int speed;

		public float minValue;

		public float maxValue = 100f;

		public float valueLimit = 100f;

		public Image loadingBar;

		public TextMeshProUGUI textPercent;

		public bool isOn;

		public bool restart;

		public bool invert;

		public bool addPrefix;

		public bool addSuffix = true;

		public string prefix = "";

		public string suffix = "%";

		public bool isLooped;

		[Range(0f, 5f)]
		public int decimals;

		public ProgressBarEvent onValueChanged;

		[HideInInspector]
		public Slider eventSource;

		private void Start()
		{
			UpdateUI();
			InitializeEvents();
		}

		private void Update()
		{
			if (isOn)
			{
				if (currentPercent <= maxValue && !invert)
				{
					currentPercent += (float)speed * Time.unscaledDeltaTime;
				}
				else if (currentPercent >= minValue && invert)
				{
					currentPercent -= (float)speed * Time.unscaledDeltaTime;
				}
				if (currentPercent >= maxValue && speed != 0 && restart && !invert)
				{
					currentPercent = 0f;
				}
				else if (currentPercent <= minValue && speed != 0 && restart && invert)
				{
					currentPercent = maxValue;
				}
				else if (currentPercent >= maxValue && speed != 0 && !restart && !invert)
				{
					currentPercent = maxValue;
				}
				else if (currentPercent <= minValue && speed != 0 && !restart && invert)
				{
					currentPercent = minValue;
				}
				UpdateUI();
			}
		}

		public void UpdateUI()
		{
			loadingBar.fillAmount = currentPercent / maxValue;
			if (addSuffix)
			{
				textPercent.text = currentPercent.ToString("F" + decimals) + suffix;
			}
			else
			{
				textPercent.text = currentPercent.ToString("F" + decimals);
			}
			if (addPrefix)
			{
				textPercent.text = prefix + textPercent.text;
			}
			if (eventSource != null)
			{
				eventSource.value = currentPercent;
			}
		}

		public void InitializeEvents()
		{
			if (Application.isPlaying && onValueChanged.GetPersistentEventCount() != 0)
			{
				if (eventSource == null)
				{
					eventSource = base.gameObject.AddComponent(typeof(Slider)) as Slider;
				}
				eventSource.transition = Selectable.Transition.None;
				eventSource.minValue = minValue;
				eventSource.maxValue = maxValue;
				eventSource.onValueChanged.AddListener(onValueChanged.Invoke);
			}
		}

		public void ClearEvents()
		{
			eventSource.onValueChanged.RemoveAllListeners();
		}

		public void ChangeValue(float newValue)
		{
			currentPercent = newValue;
			UpdateUI();
		}

		public void SetValue(float newValue)
		{
			currentPercent = newValue;
			UpdateUI();
		}

		public void SetValue(float newValue, string newPrefix = null, string newSuffix = null, bool updateUI = true)
		{
			currentPercent = newValue;
			prefix = newPrefix;
			suffix = newSuffix;
			if (updateUI)
			{
				UpdateUI();
			}
		}
	}
}
