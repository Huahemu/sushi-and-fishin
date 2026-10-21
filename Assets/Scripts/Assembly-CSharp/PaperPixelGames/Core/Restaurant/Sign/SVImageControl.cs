using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Restaurant.Sign
{
	public class SVImageControl : MonoBehaviour, IDragHandler, IEventSystemHandler, IPointerClickHandler
	{
		[SerializeField]
		private Image _pickerImage;

		[Space]
		[SerializeField]
		private RawImage _svImage;

		[Space]
		[SerializeField]
		private ColorPickerController _colorPickerController;

		[Space]
		[SerializeField]
		private RectTransform _rectTransform;

		[SerializeField]
		private RectTransform _pickerTransform;

		public void OnDrag(PointerEventData eventData)
		{
			UpdateColur(eventData);
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			UpdateColur(eventData);
		}

		private void Awake()
		{
			_pickerTransform.position = new Vector2(0f - _rectTransform.sizeDelta.x * 0.5f, 0f - _rectTransform.sizeDelta.y * 0.5f);
		}

		private void UpdateColur(PointerEventData eventData)
		{
			Vector3 localPosition = _rectTransform.InverseTransformPoint(eventData.position);
			float num = _rectTransform.sizeDelta.x * 0.5f;
			float num2 = _rectTransform.sizeDelta.y * 0.5f;
			if (localPosition.x < 0f - num)
			{
				localPosition.x = 0f - num;
			}
			else if (localPosition.x > num)
			{
				localPosition.x = num;
			}
			if (localPosition.y < 0f - num2)
			{
				localPosition.y = 0f - num2;
			}
			else if (localPosition.y > num2)
			{
				localPosition.y = num2;
			}
			float num3 = localPosition.x + num;
			float num4 = localPosition.y + num2;
			float s = num3 / _rectTransform.sizeDelta.x;
			float num5 = num4 / _rectTransform.sizeDelta.y;
			_pickerTransform.localPosition = localPosition;
			_pickerImage.color = Color.HSVToRGB(0f, 0f, 1f - num5);
			if (_colorPickerController != null)
			{
				_colorPickerController.SetSV(s, num5);
			}
		}

		public void UpdatePickerTransformPosition(float saturation, float value)
		{
			float num = _rectTransform.sizeDelta.x * 0.5f;
			float num2 = _rectTransform.sizeDelta.y * 0.5f;
			float x = saturation * _rectTransform.sizeDelta.x - num;
			float y = value * _rectTransform.sizeDelta.y - num2;
			_pickerTransform.localPosition = new Vector3(x, y, 0f);
		}
	}
}
