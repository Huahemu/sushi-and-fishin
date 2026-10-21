using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class FixedScrollHandleVisual : UIBehaviour, IPointerDownHandler, IEventSystemHandler, IBeginDragHandler, IDragHandler
{
	[Header("References")]
	[SerializeField]
	private ScrollRect _scrollRect;

	[SerializeField]
	private RectTransform _track;

	[SerializeField]
	private RectTransform _handle;

	[Header("Handle")]
	[SerializeField]
	[Min(1f)]
	private float _handleHeight = 48f;

	[SerializeField]
	private bool _allowDrag = true;

	[Header("Editor Debug")]
	[SerializeField]
	private bool _useDebugSlider;

	[SerializeField]
	[Range(0f, 1f)]
	private float _debugNormalizedPosition = 1f;

	private Camera _eventCamera;

	private float _dragOffsetFromHandleTop;

	protected override void OnEnable()
	{
		base.OnEnable();
		if (_scrollRect != null)
		{
			_scrollRect.onValueChanged.AddListener(OnScrollChanged);
		}
		ApplyDebugPositionIfNeeded();
		ApplyHandleSize();
		UpdateHandlePosition();
	}

	protected override void OnDisable()
	{
		if (_scrollRect != null)
		{
			_scrollRect.onValueChanged.RemoveListener(OnScrollChanged);
		}
		base.OnDisable();
	}

	protected override void OnRectTransformDimensionsChange()
	{
		base.OnRectTransformDimensionsChange();
		ApplyHandleSize();
		UpdateHandlePosition();
	}

	private void Update()
	{
		if (!Application.isPlaying)
		{
			ApplyDebugPositionIfNeeded();
			ApplyHandleSize();
			UpdateHandlePosition();
		}
	}

	private void OnScrollChanged(Vector2 value)
	{
		if (!_useDebugSlider)
		{
			_debugNormalizedPosition = value.y;
		}
		UpdateHandlePosition();
	}

	private void ApplyDebugPositionIfNeeded()
	{
		if (!(_scrollRect == null) && _useDebugSlider)
		{
			_scrollRect.verticalNormalizedPosition = _debugNormalizedPosition;
		}
	}

	private void ApplyHandleSize()
	{
		if (!(_handle == null))
		{
			_handle.anchorMin = new Vector2(0f, 1f);
			_handle.anchorMax = new Vector2(1f, 1f);
			_handle.pivot = new Vector2(0.5f, 1f);
			Vector2 sizeDelta = _handle.sizeDelta;
			sizeDelta.x = 0f;
			sizeDelta.y = _handleHeight;
			_handle.sizeDelta = sizeDelta;
		}
	}

	private void UpdateHandlePosition()
	{
		if (!(_scrollRect == null) && !(_track == null) && !(_handle == null))
		{
			float height = _track.rect.height;
			float height2 = _handle.rect.height;
			float num = Mathf.Max(0f, height - height2);
			if (!HasScrollableContent())
			{
				Vector2 anchoredPosition = _handle.anchoredPosition;
				anchoredPosition.x = 0f;
				anchoredPosition.y = 0f;
				_handle.anchoredPosition = anchoredPosition;
			}
			else
			{
				float num2 = Mathf.Clamp01(_scrollRect.verticalNormalizedPosition);
				Vector2 anchoredPosition2 = _handle.anchoredPosition;
				anchoredPosition2.x = 0f;
				anchoredPosition2.y = (0f - (1f - num2)) * num;
				_handle.anchoredPosition = anchoredPosition2;
			}
		}
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		if (_allowDrag && !(_scrollRect == null) && !(_track == null) && !(_handle == null) && HasScrollableContent())
		{
			_eventCamera = eventData.pressEventCamera;
			if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_track, eventData.position, _eventCamera, out var localPoint))
			{
				float num = _track.rect.yMax - localPoint.y;
				float num2 = 0f - _handle.anchoredPosition.y;
				_dragOffsetFromHandleTop = num - num2;
				SetScrollFromPointer(eventData);
			}
		}
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		OnPointerDown(eventData);
	}

	public void OnDrag(PointerEventData eventData)
	{
		if (_allowDrag)
		{
			SetScrollFromPointer(eventData);
		}
	}

	private void SetScrollFromPointer(PointerEventData eventData)
	{
		if (_scrollRect == null || _track == null || _handle == null || !HasScrollableContent() || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_track, eventData.position, _eventCamera, out var localPoint))
		{
			return;
		}
		float height = _track.rect.height;
		float height2 = _handle.rect.height;
		float num = height - height2;
		if (!(num <= 0.001f))
		{
			float num2 = Mathf.Clamp01((_track.rect.yMax - localPoint.y - _dragOffsetFromHandleTop) / num);
			_scrollRect.verticalNormalizedPosition = 1f - num2;
			if (!_useDebugSlider)
			{
				_debugNormalizedPosition = _scrollRect.verticalNormalizedPosition;
			}
			UpdateHandlePosition();
		}
	}

	private bool HasScrollableContent()
	{
		if (_scrollRect == null)
		{
			return false;
		}
		RectTransform content = _scrollRect.content;
		if (content == null)
		{
			return false;
		}
		if (content.childCount == 0)
		{
			return false;
		}
		RectTransform rectTransform = _scrollRect.viewport;
		if (rectTransform == null)
		{
			rectTransform = _scrollRect.transform as RectTransform;
		}
		if (rectTransform == null)
		{
			return false;
		}
		float height = content.rect.height;
		float height2 = rectTransform.rect.height;
		return height > height2 + 1f;
	}
}
