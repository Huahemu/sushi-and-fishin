using UnityEngine;

namespace Michsky.MUIP
{
	[RequireComponent(typeof(Animator))]
	public class ContextMenuManager : MonoBehaviour
	{
		public enum CameraSource
		{
			Main = 0,
			Custom = 1
		}

		public enum SubMenuBehaviour
		{
			Hover = 0,
			Click = 1
		}

		public enum CursorBoundHorizontal
		{
			Left = 0,
			Right = 1
		}

		public enum CursorBoundVertical
		{
			Bottom = 0,
			Top = 1
		}

		public Canvas mainCanvas;

		public Camera targetCamera;

		public GameObject contextContent;

		public Animator contextAnimator;

		public GameObject contextButton;

		public GameObject contextSeparator;

		public GameObject contextSubMenu;

		[SerializeField]
		private bool debugMode;

		public bool autoSubMenuPosition = true;

		public SubMenuBehaviour subMenuBehaviour;

		public CameraSource cameraSource;

		public CursorBoundHorizontal horizontalBound;

		public CursorBoundVertical verticalBound;

		[Range(-50f, 50f)]
		public int vBorderTop = -10;

		[Range(-50f, 50f)]
		public int vBorderBottom = 10;

		[Range(-50f, 50f)]
		public int hBorderLeft = 15;

		[Range(-50f, 50f)]
		public int hBorderRight = -15;

		private Vector2 uiPos;

		private Vector3 cursorPos;

		private Vector3 contentPos = new Vector3(0f, 0f, 0f);

		private Vector3 contextVelocity = Vector3.zero;

		private RectTransform contextRect;

		private RectTransform contentRect;

		[HideInInspector]
		public bool isOn;

		private void Awake()
		{
			if (mainCanvas == null)
			{
				mainCanvas = base.gameObject.GetComponentInParent<Canvas>();
			}
			if (contextAnimator == null)
			{
				contextAnimator = base.gameObject.GetComponent<Animator>();
			}
			if (cameraSource == CameraSource.Main)
			{
				targetCamera = Camera.main;
			}
			contextRect = base.gameObject.GetComponent<RectTransform>();
			contentRect = contextContent.GetComponent<RectTransform>();
			contentPos = new Vector3(vBorderTop, hBorderLeft, 0f);
			base.gameObject.transform.SetAsLastSibling();
			subMenuBehaviour = SubMenuBehaviour.Click;
		}

		public void CheckForBound()
		{
			if (uiPos.x <= -100f)
			{
				horizontalBound = CursorBoundHorizontal.Left;
				contentPos = new Vector3(hBorderLeft, contentPos.y, 0f);
				contentRect.pivot = new Vector2(0f, contentRect.pivot.y);
			}
			else if (uiPos.x >= 100f)
			{
				horizontalBound = CursorBoundHorizontal.Right;
				contentPos = new Vector3(hBorderRight, contentPos.y, 0f);
				contentRect.pivot = new Vector2(1f, contentRect.pivot.y);
			}
			if (uiPos.y <= -75f)
			{
				verticalBound = CursorBoundVertical.Bottom;
				contentPos = new Vector3(contentPos.x, vBorderBottom, 0f);
				contentRect.pivot = new Vector2(contentRect.pivot.x, 0f);
			}
			else if (uiPos.y >= 75f)
			{
				verticalBound = CursorBoundVertical.Top;
				contentPos = new Vector3(contentPos.x, vBorderTop, 0f);
				contentRect.pivot = new Vector2(contentRect.pivot.x, 1f);
			}
		}

		public void SetContextMenuPosition()
		{
			cursorPos = Input.mousePosition;
			if (mainCanvas.renderMode == RenderMode.ScreenSpaceCamera || mainCanvas.renderMode == RenderMode.WorldSpace)
			{
				contextRect.position = targetCamera.ScreenToWorldPoint(cursorPos);
				contextRect.localPosition = new Vector3(contextRect.localPosition.x, contextRect.localPosition.y, 0f);
				contextContent.transform.localPosition = Vector3.SmoothDamp(contextContent.transform.localPosition, contentPos, ref contextVelocity, 0f);
			}
			else if (mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
			{
				contextRect.position = cursorPos;
				contextContent.transform.position = new Vector3(cursorPos.x + contentPos.x, cursorPos.y + contentPos.y, 0f);
			}
			uiPos = contextRect.anchoredPosition;
			CheckForBound();
			if (debugMode)
			{
				PrintDebug();
			}
		}

		public void SetFixedPosition()
		{
			cursorPos = Input.mousePosition;
			SetContextMenuPosition();
			if (mainCanvas.renderMode == RenderMode.ScreenSpaceCamera || mainCanvas.renderMode == RenderMode.WorldSpace)
			{
				contextRect.position = targetCamera.ScreenToWorldPoint(cursorPos);
				contextRect.localPosition = new Vector3(contextRect.localPosition.x, contextRect.localPosition.y, 0f);
				contextContent.transform.localPosition = Vector3.SmoothDamp(contextContent.transform.localPosition, contentPos, ref contextVelocity, 0f);
			}
			else if (mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
			{
				contextRect.position = cursorPos;
				contextContent.transform.position = new Vector3(cursorPos.x + contentPos.x, cursorPos.y + contentPos.y, 0f);
			}
			uiPos = contextRect.anchoredPosition;
			CheckForBound();
			if (debugMode)
			{
				PrintDebug();
			}
		}

		private void ProcessContextRect()
		{
			if (mainCanvas.renderMode == RenderMode.ScreenSpaceCamera || mainCanvas.renderMode == RenderMode.WorldSpace)
			{
				contextRect.position = targetCamera.ScreenToWorldPoint(cursorPos);
				contextRect.localPosition = new Vector3(contextRect.localPosition.x, contextRect.localPosition.y, 0f);
				contextContent.transform.localPosition = Vector3.SmoothDamp(contextContent.transform.localPosition, contentPos, ref contextVelocity, 0f);
			}
			else if (mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
			{
				contextRect.position = cursorPos;
				contextContent.transform.position = new Vector3(cursorPos.x + contentPos.x, cursorPos.y + contentPos.y, 0f);
			}
		}

		private void PrintDebug()
		{
			Debug.Log("<b>[Context Menu]</b> UI Pos: " + uiPos.ToString() + ", H: " + horizontalBound.ToString() + ", V: " + verticalBound, this);
		}

		public void Open()
		{
			contextAnimator.Play("Menu In");
			isOn = true;
		}

		public void Close()
		{
			contextAnimator.Play("Menu Out");
			isOn = false;
		}

		public void OpenInFixedPosition()
		{
			SetFixedPosition();
			Open();
		}

		public void OpenContextMenu()
		{
			Open();
		}

		public void CloseOnClick()
		{
			Close();
		}
	}
}
