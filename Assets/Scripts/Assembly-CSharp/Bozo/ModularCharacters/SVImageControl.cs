using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bozo.ModularCharacters
{
	public class SVImageControl : MonoBehaviour, IDragHandler, IEventSystemHandler, IPointerClickHandler
	{
		[SerializeField]
		private Image PickerImage;

		private RawImage SVImage;

		private ColorPickerControl CC;

		private RectTransform rect;

		private RectTransform pickerTransform;

		private void Awake()
		{
			SVImage = GetComponent<RawImage>();
			CC = Object.FindFirstObjectByType<ColorPickerControl>();
			rect = GetComponent<RectTransform>();
			pickerTransform = PickerImage.GetComponent<RectTransform>();
			pickerTransform = PickerImage.GetComponent<RectTransform>();
			pickerTransform.position = new Vector2(0f - rect.sizeDelta.x * 0.5f, 0f - rect.sizeDelta.y * 0.5f);
		}

		private void UpdateColor(PointerEventData eventData)
		{
			Vector3 vector = rect.InverseTransformPoint(eventData.position);
			float num = rect.sizeDelta.x * 0.5f;
			float num2 = rect.sizeDelta.y * 0.5f;
			if (vector.x < 0f - num)
			{
				vector.x = 0f - num;
			}
			if (vector.x > num)
			{
				vector.x = num;
			}
			if (vector.y < 0f - num2)
			{
				vector.y = 0f - num2;
			}
			if (vector.y > num2)
			{
				vector.y = num2;
			}
			float num3 = vector.x + num;
			float num4 = vector.y + num2;
			float s = num3 / rect.sizeDelta.x;
			float num5 = num4 / rect.sizeDelta.y;
			PickerImage.color = Color.HSVToRGB(0f, 0f, 1f - num5);
			CC.SetSV(s, num5);
		}

		public void setPickerPosition(float x, float y)
		{
			if (!rect)
			{
				rect = GetComponent<RectTransform>();
			}
			if (!pickerTransform)
			{
				pickerTransform = PickerImage.GetComponent<RectTransform>();
			}
			float x2 = Mathf.Lerp((0f - rect.sizeDelta.x) / 2f, rect.sizeDelta.x / 2f, x);
			float y2 = Mathf.Lerp((0f - rect.sizeDelta.y) / 2f, rect.sizeDelta.y / 2f, y);
			Vector2 vector = new Vector2(x2, y2);
			pickerTransform.localPosition = vector;
		}

		public void OnDrag(PointerEventData eventData)
		{
			UpdateColor(eventData);
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			UpdateColor(eventData);
		}
	}
}
