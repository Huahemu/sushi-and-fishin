using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	[RequireComponent(typeof(Slider))]
	public class SliderManager : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler
	{
		[Serializable]
		public class SliderEvent : UnityEvent<float>
		{
		}

		public enum DecimalFormat
		{
			F0 = 0,
			F1 = 1,
			F2 = 2,
			F3 = 3,
			F4 = 4,
			F5 = 5
		}

		public Slider mainSlider;

		public TextMeshProUGUI valueText;

		public TextMeshProUGUI popupValueText;

		public bool enableSaving;

		public bool invokeOnAwake = true;

		public string sliderTag = "My Slider";

		public bool usePercent;

		public bool showValue = true;

		public bool showPopupValue = true;

		public bool useRoundValue;

		public DecimalFormat decimalFormat = DecimalFormat.F1;

		public float minValue;

		public float maxValue;

		[SerializeField]
		public SliderEvent onValueChanged = new SliderEvent();

		[Space(8f)]
		public SliderEvent sliderEvent;

		[HideInInspector]
		public Animator sliderAnimator;

		[HideInInspector]
		public float saveValue;

		private void Awake()
		{
			if (enableSaving)
			{
				if (!PlayerPrefs.HasKey(sliderTag + "MUIPSliderValue"))
				{
					saveValue = mainSlider.value;
				}
				else
				{
					saveValue = PlayerPrefs.GetFloat(sliderTag + "MUIPSliderValue");
				}
				mainSlider.value = saveValue;
				mainSlider.onValueChanged.AddListener(delegate
				{
					saveValue = mainSlider.value;
					PlayerPrefs.SetFloat(sliderTag + "MUIPSliderValue", saveValue);
				});
			}
			mainSlider.onValueChanged.AddListener(delegate
			{
				sliderEvent.Invoke(mainSlider.value);
				UpdateUI();
			});
			if (sliderAnimator == null && showPopupValue)
			{
				try
				{
					sliderAnimator = base.gameObject.GetComponent<Animator>();
				}
				catch
				{
					showPopupValue = false;
				}
			}
			if (invokeOnAwake)
			{
				sliderEvent.Invoke(mainSlider.value);
			}
			UpdateUI();
		}

		public void UpdateUI()
		{
			if (useRoundValue)
			{
				if (usePercent)
				{
					if (valueText != null)
					{
						valueText.text = Mathf.Round(mainSlider.value * 1f) + "%";
					}
					if (popupValueText != null)
					{
						popupValueText.text = Mathf.Round(mainSlider.value * 1f) + "%";
					}
				}
				else
				{
					if (valueText != null)
					{
						valueText.text = Mathf.Round(mainSlider.value * 1f).ToString();
					}
					if (popupValueText != null)
					{
						popupValueText.text = Mathf.Round(mainSlider.value * 1f).ToString();
					}
				}
			}
			else if (usePercent)
			{
				if (valueText != null)
				{
					valueText.text = mainSlider.value.ToString(decimalFormat.ToString()) + "%";
				}
				if (popupValueText != null)
				{
					popupValueText.text = mainSlider.value.ToString(decimalFormat.ToString()) + "%";
				}
			}
			else
			{
				if (valueText != null)
				{
					valueText.text = mainSlider.value.ToString(decimalFormat.ToString());
				}
				if (popupValueText != null)
				{
					popupValueText.text = mainSlider.value.ToString(decimalFormat.ToString());
				}
			}
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			if (showPopupValue && sliderAnimator != null)
			{
				sliderAnimator.Play("Value In");
			}
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			if (showPopupValue && sliderAnimator != null)
			{
				sliderAnimator.Play("Value Out");
			}
		}
	}
}
