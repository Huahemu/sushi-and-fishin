using UnityEngine;
using UnityEngine.EventSystems;

namespace PaperPixelGames.Core.Tablet
{
	public class MapZoomer : MonoBehaviour, IScrollHandler, IEventSystemHandler
	{
		[Header("ZOOM")]
		[SerializeField]
		private float _minScale = 0.15f;

		[SerializeField]
		private float _initialScale = 0.3f;

		[SerializeField]
		private float _maxScale = 0.5f;

		[SerializeField]
		private float _zoomSpeed = 0.01f;

		[SerializeField]
		private RectTransform _content;

		private float Scale
		{
			get
			{
				return _content.localScale.x;
			}
			set
			{
				_content.localScale = new Vector3(value, value, 1f);
			}
		}

		public void Initialize()
		{
			RectTransform content = _content;
			Vector2 anchorMin = (_content.anchorMax = new Vector2(0.5f, 0.5f));
			content.anchorMin = anchorMin;
			_content.pivot = new Vector2(0.5f, 0.5f);
			float num = Mathf.Clamp(_initialScale, _minScale, _maxScale);
			_content.localScale = new Vector3(num, num, 1f);
			_content.anchoredPosition = Vector2.zero;
		}

		public void ApplySettings(MapZoomSettings settings, bool resetView)
		{
			if (!(_content == null))
			{
				_minScale = Mathf.Max(0.01f, settings.MinScale);
				_maxScale = Mathf.Max(_minScale, settings.MaxScale);
				_initialScale = Mathf.Clamp(settings.InitialScale, _minScale, _maxScale);
				_zoomSpeed = Mathf.Max(0.001f, settings.ZoomSpeed);
				float num = (resetView ? _initialScale : Mathf.Clamp(Scale, _minScale, _maxScale));
				_content.localScale = new Vector3(num, num, 1f);
				if (resetView)
				{
					_content.anchoredPosition = Vector2.zero;
				}
			}
		}

		public void ResetAndCenter(MapZoomSettings settings, Vector2? normalizedFocus)
		{
			ApplySettings(settings, resetView: true);
			if (normalizedFocus.HasValue)
			{
				Canvas.ForceUpdateCanvases();
				Vector2 vector = (normalizedFocus.Value - new Vector2(0.5f, 0.5f)) * _content.rect.size;
				_content.anchoredPosition = -vector * Scale;
			}
		}

		public void OnScroll(PointerEventData e)
		{
			if (!(_content == null))
			{
				float scale = Scale;
				float num = Mathf.Pow(1f + _zoomSpeed, e.scrollDelta.y);
				float num2 = Mathf.Clamp(scale * num, _minScale, _maxScale);
				if (!Mathf.Approximately(num2, scale))
				{
					Vector2 zero = Vector2.zero;
					Vector2 anchoredPosition = _content.anchoredPosition;
					float num3 = scale;
					float num4 = num2;
					Vector2 anchoredPosition2 = anchoredPosition + (zero - anchoredPosition) * (1f - num4 / num3);
					Scale = num4;
					_content.anchoredPosition = anchoredPosition2;
				}
			}
		}
	}
}
