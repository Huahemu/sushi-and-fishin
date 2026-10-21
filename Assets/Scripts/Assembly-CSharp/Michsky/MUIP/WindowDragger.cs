using UnityEngine;
using UnityEngine.EventSystems;

namespace Michsky.MUIP
{
	public class WindowDragger : UIBehaviour, IBeginDragHandler, IEventSystemHandler, IDragHandler
	{
		[Header("Resources")]
		public RectTransform dragArea;

		public RectTransform dragObject;

		[Header("Settings")]
		public bool topOnDrag = true;

		private Vector2 originalLocalPointerPosition;

		private Vector3 originalPanelLocalPosition;

		private RectTransform DragObjectInternal
		{
			get
			{
				if (dragObject == null)
				{
					return base.transform as RectTransform;
				}
				return dragObject;
			}
		}

		private RectTransform DragAreaInternal
		{
			get
			{
				if (dragArea == null)
				{
					RectTransform rectTransform = base.transform as RectTransform;
					while (rectTransform.parent != null && rectTransform.parent is RectTransform)
					{
						rectTransform = rectTransform.parent as RectTransform;
					}
					return rectTransform;
				}
				return dragArea;
			}
		}

		public new void Start()
		{
			if (dragArea == null)
			{
				try
				{
					Canvas canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)[0];
					dragArea = canvas.GetComponent<RectTransform>();
				}
				catch
				{
					Debug.LogError("<b>[Movable Window]</b> Drag Area has not been assigned.");
				}
			}
		}

		public void OnBeginDrag(PointerEventData data)
		{
			originalPanelLocalPosition = DragObjectInternal.localPosition;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(DragAreaInternal, data.position, data.pressEventCamera, out originalLocalPointerPosition);
			base.gameObject.transform.SetAsLastSibling();
			if (topOnDrag)
			{
				dragObject.transform.SetAsLastSibling();
			}
		}

		public void OnDrag(PointerEventData data)
		{
			if (RectTransformUtility.ScreenPointToLocalPointInRectangle(DragAreaInternal, data.position, data.pressEventCamera, out var localPoint))
			{
				Vector3 vector = localPoint - originalLocalPointerPosition;
				DragObjectInternal.localPosition = originalPanelLocalPosition + vector;
			}
			ClampToArea();
		}

		private void ClampToArea()
		{
			Vector3 localPosition = DragObjectInternal.localPosition;
			Vector3 vector = DragAreaInternal.rect.min - DragObjectInternal.rect.min;
			Vector3 vector2 = DragAreaInternal.rect.max - DragObjectInternal.rect.max;
			localPosition.x = Mathf.Clamp(DragObjectInternal.localPosition.x, vector.x, vector2.x);
			localPosition.y = Mathf.Clamp(DragObjectInternal.localPosition.y, vector.y, vector2.y);
			DragObjectInternal.localPosition = localPosition;
		}
	}
}
