using UnityEngine;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	public class TooltipManager : MonoBehaviour
	{
		public enum CameraSource
		{
			Main = 0,
			Custom = 1
		}

		public enum TransitionMode
		{
			Damp = 0,
			Snap = 1
		}

		public Canvas mainCanvas;

		public GameObject tooltipObject;

		public GameObject tooltipContent;

		public Camera targetCamera;

		[Range(0.01f, 0.5f)]
		public float tooltipSmoothness = 0.1f;

		[Range(5f, 10f)]
		public float dampSpeed = 10f;

		public float preferredWidth = 375f;

		public bool allowUpdate = true;

		public bool checkDispose = true;

		public CameraSource cameraSource;

		public TransitionMode transitionMode;

		[Range(-50f, 50f)]
		public int vBorderTop = -15;

		[Range(-50f, 50f)]
		public int vBorderBottom = 10;

		[Range(-50f, 50f)]
		public int hBorderLeft = 20;

		[Range(-50f, 50f)]
		public int hBorderRight = -15;

		[SerializeField]
		private int xLeft = -400;

		[SerializeField]
		private int xRight = 400;

		[SerializeField]
		private int yTop = -325;

		[SerializeField]
		private int yBottom = 325;

		[HideInInspector]
		public LayoutElement contentLE;

		[HideInInspector]
		public TooltipContent currentTooltip;

		private Vector2 uiPos;

		private Vector3 cursorPos;

		private Vector3 contentPos = new Vector3(0f, 0f, 0f);

		private Vector3 tooltipVelocity = Vector3.zero;

		private RectTransform contentRect;

		private RectTransform tooltipRect;

		private void Awake()
		{
			RectTransform component = base.gameObject.GetComponent<RectTransform>();
			if (component == null)
			{
				Debug.LogError("<b>[Tooltip]</b> Rect Transform is missing from the object.", this);
				return;
			}
			component.anchorMin = new Vector2(0f, 0f);
			component.anchorMax = new Vector2(1f, 1f);
			component.offsetMin = new Vector2(0f, 0f);
			component.offsetMax = new Vector2(0f, 0f);
			tooltipContent.GetComponent<RectTransform>().pivot = new Vector2(0f, tooltipContent.GetComponent<RectTransform>().pivot.y);
			tooltipContent.GetComponent<RectTransform>().pivot = new Vector2(tooltipContent.GetComponent<RectTransform>().pivot.x, 0f);
			if (mainCanvas == null)
			{
				mainCanvas = base.gameObject.GetComponentInParent<Canvas>();
			}
			if (cameraSource == CameraSource.Main)
			{
				targetCamera = Camera.main;
			}
			contentRect = tooltipContent.GetComponentInParent<RectTransform>();
			tooltipRect = tooltipObject.GetComponent<RectTransform>();
			contentPos = new Vector3(vBorderTop, hBorderLeft, 0f);
			base.gameObject.transform.SetAsLastSibling();
		}

		private void Update()
		{
			if (allowUpdate)
			{
				if (checkDispose && currentTooltip != null && !currentTooltip.gameObject.activeInHierarchy)
				{
					currentTooltip.ProcessExit();
				}
				CheckForPosition();
			}
		}

		private void CheckForPosition()
		{
			cursorPos = Input.mousePosition;
			uiPos = tooltipRect.anchoredPosition;
			CheckForBounds();
			if (mainCanvas.renderMode == RenderMode.ScreenSpaceCamera || mainCanvas.renderMode == RenderMode.WorldSpace)
			{
				RectTransformUtility.ScreenPointToLocalPointInRectangle(tooltipRect.parent.GetComponent<RectTransform>(), cursorPos, targetCamera, out var localPoint);
				tooltipRect.localPosition = localPoint;
				if (transitionMode == TransitionMode.Damp)
				{
					tooltipContent.transform.localPosition = Vector3.SmoothDamp(tooltipContent.transform.localPosition, contentPos, ref tooltipVelocity, tooltipSmoothness, dampSpeed * 1000f, Time.unscaledDeltaTime);
				}
				else
				{
					tooltipContent.transform.localPosition = contentPos;
				}
			}
			else if (mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
			{
				tooltipRect.position = cursorPos;
				if (transitionMode == TransitionMode.Damp)
				{
					tooltipContent.transform.position = Vector3.SmoothDamp(tooltipContent.transform.position, cursorPos + contentPos, ref tooltipVelocity, tooltipSmoothness, dampSpeed * 1000f, Time.unscaledDeltaTime);
				}
				else
				{
					tooltipContent.transform.position = cursorPos + contentPos;
				}
			}
		}

		private void CheckForBounds()
		{
			if (uiPos.x <= (float)xLeft)
			{
				contentPos = new Vector3(hBorderLeft, contentPos.y, 0f);
				contentRect.pivot = new Vector2(0f, contentRect.pivot.y);
			}
			else if (uiPos.x >= (float)xRight)
			{
				contentPos = new Vector3(hBorderRight, contentPos.y, 0f);
				contentRect.pivot = new Vector2(1f, contentRect.pivot.y);
			}
			if (uiPos.y <= (float)yTop)
			{
				contentPos = new Vector3(contentPos.x, vBorderBottom, 0f);
				contentRect.pivot = new Vector2(contentRect.pivot.x, 0f);
			}
			else if (uiPos.y >= (float)yBottom)
			{
				contentPos = new Vector3(contentPos.x, vBorderTop, 0f);
				contentRect.pivot = new Vector2(contentRect.pivot.x, 1f);
			}
		}
	}
}
