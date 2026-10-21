using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	public class RadialSlider : MonoBehaviour, IPointerDownHandler, IEventSystemHandler, IPointerUpHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler
	{
		[Serializable]
		public class SliderEvent : UnityEvent<float>
		{
		}

		public enum StartPoint
		{
			Left = 0,
			Right = 1,
			Top = 2,
			Down = 3
		}

		private const string PREFS_UI_SAVE_NAME = "Radial";

		public float currentValue = 50f;

		public Image sliderImage;

		public Transform indicatorPivot;

		public TextMeshProUGUI valueText;

		public float minValue;

		public float maxValue = 100f;

		[Range(0f, 8f)]
		public int decimals;

		public bool isPercent;

		public StartPoint startPoint;

		public bool rememberValue;

		public string sliderTag;

		[SerializeField]
		public SliderEvent onValueChanged = new SliderEvent();

		public UnityEvent onPointerEnter;

		public UnityEvent onPointerExit;

		private GraphicRaycaster graphicRaycaster;

		private RectTransform hitRectTransform;

		private bool isPointerDown;

		private float currentAngle;

		private float currentAngleOnPointerDown;

		private float valueDisplayPrecision;

		public float SliderAngle
		{
			get
			{
				return currentAngle;
			}
			set
			{
				currentAngle = Mathf.Clamp(value, 0f, 360f);
			}
		}

		public float SliderValue
		{
			get
			{
				return (float)(long)(SliderValueRaw * valueDisplayPrecision) / valueDisplayPrecision;
			}
			set
			{
				SliderValueRaw = value;
			}
		}

		public float SliderValueRaw
		{
			get
			{
				return SliderAngle / 360f * maxValue;
			}
			set
			{
				SliderAngle = value * 360f / maxValue;
			}
		}

		private void Awake()
		{
			graphicRaycaster = GetComponentInParent<GraphicRaycaster>();
			if (graphicRaycaster == null)
			{
				Debug.LogWarning("<b>[Radial Slider]</b> Could not find GraphicRaycaster component in parent.", this);
			}
		}

		private void Start()
		{
			valueDisplayPrecision = Mathf.Pow(10f, decimals);
			if (rememberValue)
			{
				LoadState();
			}
			else
			{
				SliderAngle = currentValue * 3.6f;
			}
			SliderValue = currentValue;
			onValueChanged.Invoke(SliderValueRaw);
			UpdateUI();
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			hitRectTransform = eventData.pointerCurrentRaycast.gameObject.GetComponent<RectTransform>();
			isPointerDown = true;
			currentAngleOnPointerDown = SliderAngle;
			HandleSliderMouseInput(eventData, allowValueWrap: true);
		}

		public void OnPointerUp(PointerEventData eventData)
		{
			if (HasValueChanged())
			{
				SaveState();
			}
			hitRectTransform = null;
			isPointerDown = false;
		}

		public void OnDrag(PointerEventData eventData)
		{
			if (currentValue >= minValue)
			{
				HandleSliderMouseInput(eventData, allowValueWrap: false);
			}
			else if (currentValue <= minValue)
			{
				SliderValueRaw = minValue;
			}
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			onPointerEnter.Invoke();
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			onPointerExit.Invoke();
		}

		public void LoadState()
		{
			currentAngle = PlayerPrefs.GetFloat(sliderTag + "Radial");
		}

		public void SaveState()
		{
			if (rememberValue)
			{
				PlayerPrefs.SetFloat(sliderTag + "Radial", currentAngle);
			}
		}

		public void UpdateUI()
		{
			if (SliderValueRaw >= minValue)
			{
				float fillAmount = SliderAngle / 360f;
				indicatorPivot.transform.localEulerAngles = new Vector3(180f, 0f, SliderAngle);
				sliderImage.fillAmount = fillAmount;
				valueText.text = string.Format("{0}{1}", SliderValue, isPercent ? "%" : "");
				currentValue = SliderValue;
			}
		}

		private bool HasValueChanged()
		{
			return SliderAngle != currentAngleOnPointerDown;
		}

		private void HandleSliderMouseInput(PointerEventData eventData, bool allowValueWrap)
		{
			if (!isPointerDown)
			{
				return;
			}
			RectTransformUtility.ScreenPointToLocalPointInRectangle(hitRectTransform, eventData.position, eventData.pressEventCamera, out var localPoint);
			float num = Mathf.Atan2(0f - localPoint.y, localPoint.x) * 57.29578f + 180f;
			if (!allowValueWrap)
			{
				currentAngle = SliderAngle;
				if (Mathf.Abs(num - currentAngle) >= 180f)
				{
					num = ((currentAngle < num) ? 0f : 360f);
				}
			}
			SliderAngle = num;
			UpdateUI();
			if (HasValueChanged())
			{
				onValueChanged.Invoke(SliderValueRaw);
			}
		}
	}
}
