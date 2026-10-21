namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::DG.Tweening;
using global::Gilzoide.UpdateManager;
using global::UnityEngine;
using global::UnityEngine.EventSystems;

public class EmoteWheelController : MonoBehaviour, IUpdatable, IManagedObject
{
	[Header("Radial Menu Reference")]
	[Tooltip("Reference to the RMF_RadialMenu component")]
	public RMF_RadialMenu radialMenu;

	[Header("Animation Settings")]
	[Tooltip("Duration for the fade in/out animation")]
	public float animationDuration = 0.2f;

	[Tooltip("Easing type for the animation")]
	public Ease animationEase = Ease.OutCubic;

	[Header("Cursor Settings")]
	[Tooltip("Should the cursor be unlocked when emote wheel is active?")]
	public bool unlockCursorOnActive = true;

	[Header("Camera Settings")]
	[Tooltip("Should the camera be locked when emote wheel is active?")]
	public bool lockCameraOnActive = true;

	[Tooltip("Reference to the PlayerHead component for camera control (must be manually assigned)")]
	public PlayerHead playerHead;

	private CanvasGroup canvasGroup;

	private bool isEmoteWheelActive;

	private int capturedIndex;

	private int hoveredIndexOnOpen = -1;

	private CursorLockMode previousCursorLockMode;

	private bool previousCursorVisible;

	private bool previousCameraLocked;

	public bool IsEmoteWheelActive => isEmoteWheelActive;

	public int CapturedIndex => capturedIndex;

	public bool IsCameraLocked
	{
		get
		{
			if (!(playerHead != null))
			{
				return false;
			}
			return playerHead.isLocked;
		}
	}

	private void Awake()
	{
		canvasGroup = GetComponent<CanvasGroup>();
		if (canvasGroup == null)
		{
			canvasGroup = base.gameObject.AddComponent<CanvasGroup>();
		}
		if (radialMenu == null)
		{
			radialMenu = GetComponent<RMF_RadialMenu>();
		}
		canvasGroup.alpha = 0f;
		canvasGroup.interactable = false;
		canvasGroup.blocksRaycasts = false;
	}

	private void OnEnable()
	{
		InputEvents.OnEmoteWheelEvent = (Action<bool>)Delegate.Combine(InputEvents.OnEmoteWheelEvent, new Action<bool>(OnEmoteWheelPressed));
	}

	private void OnDisable()
	{
		InputEvents.OnEmoteWheelEvent = (Action<bool>)Delegate.Remove(InputEvents.OnEmoteWheelEvent, new Action<bool>(OnEmoteWheelPressed));
	}

	public void ManagedUpdate()
	{
	}

	private void OnEmoteWheelPressed(bool isPressed)
	{
		if (isPressed)
		{
			ShowEmoteWheel();
		}
		else
		{
			HideEmoteWheel();
		}
	}

	private void ShowEmoteWheel()
	{
		if (isEmoteWheelActive)
		{
			return;
		}
		isEmoteWheelActive = true;
		if (radialMenu != null)
		{
			if (!radialMenu.useDeltaSelection)
			{
				hoveredIndexOnOpen = GetHoveredElementIndex();
				if (hoveredIndexOnOpen < 0)
				{
					hoveredIndexOnOpen = radialMenu.index;
				}
			}
			else
			{
				hoveredIndexOnOpen = radialMenu.index;
			}
		}
		else
		{
			hoveredIndexOnOpen = -1;
		}
		if (radialMenu != null && radialMenu.useDeltaSelection)
		{
			radialMenu.SetDeltaModeActive(active: true);
			UICursorSimple.Instance?.HideCursor();
		}
		else if (unlockCursorOnActive)
		{
			previousCursorLockMode = Cursor.lockState;
			previousCursorVisible = Cursor.visible;
			UICursorSimple.Instance?.ShowCursor();
		}
		if (lockCameraOnActive && playerHead != null)
		{
			previousCameraLocked = playerHead.isLocked;
			playerHead.isLocked = true;
		}
		canvasGroup.interactable = true;
		canvasGroup.blocksRaycasts = true;
		canvasGroup.DOFade(1f, animationDuration).SetEase(animationEase).OnComplete(() =>
		{
			canvasGroup.alpha = 1f;
		});
	}

	private void HideEmoteWheel()
	{
		if (!isEmoteWheelActive)
		{
			return;
		}
		if (radialMenu != null)
		{
			if (!radialMenu.useDeltaSelection)
			{
				int hoveredElementIndex = GetHoveredElementIndex();
				if (hoveredElementIndex >= 0)
				{
					capturedIndex = hoveredElementIndex;
				}
				else
				{
					capturedIndex = radialMenu.index;
				}
				if (hoveredIndexOnOpen >= 0 && capturedIndex == hoveredIndexOnOpen)
				{
					int lastSelectedIndex = radialMenu.GetLastSelectedIndex();
					if (lastSelectedIndex >= 0 && lastSelectedIndex < radialMenu.elements.Count)
					{
						capturedIndex = lastSelectedIndex;
					}
				}
				Cursor.lockState = CursorLockMode.Locked;
				UICursorSimple.Instance?.HideCursor();
			}
			else
			{
				capturedIndex = radialMenu.index;
				if (hoveredIndexOnOpen >= 0 && capturedIndex == hoveredIndexOnOpen)
				{
					int lastSelectedIndex2 = radialMenu.GetLastSelectedIndex();
					if (lastSelectedIndex2 >= 0 && lastSelectedIndex2 < radialMenu.elements.Count)
					{
						capturedIndex = lastSelectedIndex2;
					}
				}
			}
		}
		isEmoteWheelActive = false;
		canvasGroup.DOKill();
		if (radialMenu != null && radialMenu.useDeltaSelection)
		{
			radialMenu.SetDeltaModeActive(active: false);
			UICursorSimple.Instance?.HideCursor();
		}
		if (lockCameraOnActive && playerHead != null)
		{
			playerHead.isLocked = previousCameraLocked;
		}
		canvasGroup.interactable = false;
		canvasGroup.blocksRaycasts = false;
		canvasGroup.alpha = 0f;
		ExecuteCapturedEmote();
	}

	private void ExecuteCapturedEmote()
	{
		if (radialMenu != null && radialMenu.elements != null && capturedIndex >= 0 && capturedIndex < radialMenu.elements.Count)
		{
			RMF_RadialMenuElement rMF_RadialMenuElement = radialMenu.elements[capturedIndex];
			if (rMF_RadialMenuElement != null && rMF_RadialMenuElement.button != null)
			{
				rMF_RadialMenuElement.button.onClick?.Invoke();
				radialMenu.SetLastSelectedIndex(capturedIndex);
			}
		}
	}

	public void SetEmoteWheelActive(bool active)
	{
		if (active)
		{
			ShowEmoteWheel();
		}
		else
		{
			HideEmoteWheel();
		}
	}

	public void SetCameraLocked(bool locked)
	{
		if (playerHead != null)
		{
			playerHead.isLocked = locked;
		}
	}

	private int GetHoveredElementIndex()
	{
		if (radialMenu == null || radialMenu.elements == null)
		{
			return -1;
		}
		if (EventSystem.current != null)
		{
			PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
			pointerEventData.position = CursorPointerInput.ScreenPosition;
			List<RaycastResult> list = new List<RaycastResult>();
			EventSystem.current.RaycastAll(pointerEventData, list);
			foreach (RaycastResult item in list)
			{
				RMF_RadialMenuElement componentInParent = item.gameObject.GetComponentInParent<RMF_RadialMenuElement>();
				if (componentInParent != null && radialMenu.elements.Contains(componentInParent))
				{
					return componentInParent.assignedIndex;
				}
			}
		}
		return -1;
	}
}

}
