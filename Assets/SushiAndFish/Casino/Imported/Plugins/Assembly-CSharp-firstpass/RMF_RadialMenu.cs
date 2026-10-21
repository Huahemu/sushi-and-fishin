namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.EventSystems;

[AddComponentMenu("Radial Menu Framework/RMF Core Script")]
public class RMF_RadialMenu : MonoBehaviour
{
	[HideInInspector]
	public RectTransform rt;

	[Tooltip("Adjusts the radial menu for use with a gamepad or joystick. You might need to edit this script if you're not using the default horizontal and vertical input axes.")]
	public bool useGamepad;

	[Tooltip("With lazy selection, you only have to point your mouse (or joystick) in the direction of an element to select it, rather than be moused over the element entirely.")]
	public bool useLazySelection;

	[Tooltip("If set to true, uses cursor delta movement instead of absolute cursor position. Cursor will be locked.")]
	public bool useDeltaSelection;

	[Tooltip("Sensitivity multiplier for delta movement. Lower values = slower movement. Typical range: 0.01 to 0.5. Start with 0.1 and adjust.")]
	public float deltaSensitivity = 0.1f;

	[Tooltip("How fast the pull position returns to center when cursor isn't moved (0-1). Higher values = faster return.")]
	[Range(0f, 1f)]
	public float elasticReturnSpeed = 0.1f;

	[Tooltip("Maximum distance the pull position can be from center (in screen pixels).")]
	public float maxPullDistance = 200f;

	[Tooltip("Minimum accumulated input magnitude required before selection can change. Prevents accidental selection changes from small movements.")]
	public float substantialInputThreshold = 30f;

	[Tooltip("If set to true, a pointer with a graphic of your choosing will aim in the direction of your mouse. You will need to specify the container for the selection follower.")]
	public bool useSelectionFollower;

	[Tooltip("If using the selection follower, this must point to the rect transform of the selection follower's container.")]
	public RectTransform selectionFollowerContainer;

	[Tooltip("Line renderer to visualize the pull direction. If null, will try to find one on this object or create one.")]
	public LineRenderer pullLineRenderer;

	[Tooltip("This is the text object that will display the labels of the radial elements when they are being hovered over. If you don't want a label, leave this blank.")]
	public TextMeshProUGUI textLabel;

	[Tooltip("This is the list of radial menu elements. This is order-dependent. The first element in the list will be the first element created, and so on.")]
	public List<RMF_RadialMenuElement> elements = new List<RMF_RadialMenuElement>();

	[Tooltip("Controls the total angle offset for all elements. For example, if set to 45, all elements will be shifted +45 degrees. Good values are generally 45, 90, or 180")]
	public float globalOffset;

	[HideInInspector]
	public float currentAngle;

	[HideInInspector]
	public int index;

	private int elementCount;

	private float angleOffset;

	private int previousActiveIndex;

	private PointerEventData pointer;

	private Vector2 deltaPullPosition = Vector2.zero;

	private Vector2 lastMousePosition = Vector2.zero;

	private bool isDeltaModeActive;

	private Canvas parentCanvas;

	private int lastSelectedIndex;

	private Vector2 initialDeltaPosition = Vector2.zero;

	private float accumulatedInputMagnitude;

	private bool hasSubstantialInput;

	private float savedSelectionFollowerAngle;

	public void SetLastSelectedIndex(int selectedIndex)
	{
		if (selectedIndex >= 0 && selectedIndex < elements.Count)
		{
			lastSelectedIndex = selectedIndex;
		}
	}

	public int GetLastSelectedIndex()
	{
		return lastSelectedIndex;
	}

	private void Awake()
	{
		pointer = new PointerEventData(EventSystem.current);
		rt = GetComponent<RectTransform>();
		if (rt == null)
		{
			Debug.LogError("Radial Menu: Rect Transform for radial menu " + base.gameObject.name + " could not be found. Please ensure this is an object parented to a canvas.");
		}
		if (useSelectionFollower && selectionFollowerContainer == null)
		{
			Debug.LogError("Radial Menu: Selection follower container is unassigned on " + base.gameObject.name + ", which has the selection follower enabled.");
		}
		elementCount = elements.Count;
		angleOffset = 360f / (float)elementCount;
		for (int i = 0; i < elementCount; i++)
		{
			if (elements[i] == null)
			{
				Debug.LogError("Radial Menu: element " + i + " in the radial menu " + base.gameObject.name + " is null!");
			}
			else
			{
				elements[i].parentRM = this;
				elements[i].setAllAngles(angleOffset * (float)i + globalOffset, angleOffset);
				elements[i].assignedIndex = i;
			}
		}
		if (useDeltaSelection)
		{
			parentCanvas = GetComponentInParent<Canvas>();
			if (parentCanvas == null)
			{
				Debug.LogWarning("Radial Menu: No Canvas found in parent hierarchy. Delta selection may not work correctly.");
			}
			SetupPullLineRenderer();
		}
		UpdateSelectionFollowerState();
	}

	private void SetupPullLineRenderer()
	{
		if (pullLineRenderer == null)
		{
			pullLineRenderer = GetComponent<LineRenderer>();
			if (pullLineRenderer == null)
			{
				GameObject gameObject = new GameObject("PullLine");
				gameObject.transform.SetParent(base.transform, worldPositionStays: false);
				gameObject.transform.localPosition = Vector3.zero;
				gameObject.transform.localRotation = Quaternion.identity;
				gameObject.transform.localScale = Vector3.one;
				pullLineRenderer = gameObject.AddComponent<LineRenderer>();
			}
		}
		if (pullLineRenderer != null)
		{
			pullLineRenderer.positionCount = 2;
			pullLineRenderer.useWorldSpace = false;
			pullLineRenderer.startWidth = 3f;
			pullLineRenderer.endWidth = 1f;
			Shader shader = Shader.Find("UI/Default") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
			if (shader != null)
			{
				pullLineRenderer.material = new Material(shader);
			}
			pullLineRenderer.startColor = new Color(1f, 1f, 1f, 0.8f);
			pullLineRenderer.endColor = new Color(1f, 1f, 1f, 0.3f);
			pullLineRenderer.sortingOrder = 100;
			pullLineRenderer.enabled = false;
			pullLineRenderer.SetPosition(0, Vector3.zero);
			pullLineRenderer.SetPosition(1, Vector3.zero);
		}
	}

	private void Start()
	{
		if (useGamepad)
		{
			EventSystem.current.SetSelectedGameObject(base.gameObject, null);
			if (useSelectionFollower && selectionFollowerContainer != null)
			{
				selectionFollowerContainer.rotation = Quaternion.Euler(0f, 0f, 0f - globalOffset);
			}
		}
		UpdateSelectionFollowerState();
	}

	public void SetDeltaModeActive(bool active)
	{
		isDeltaModeActive = active;
		if (active && useDeltaSelection)
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
			RestoreDeltaPositionToLastSelected();
			initialDeltaPosition = deltaPullPosition;
			accumulatedInputMagnitude = 0f;
			hasSubstantialInput = false;
			if (angleOffset != 0f && elements != null && lastSelectedIndex >= 0 && lastSelectedIndex < elements.Count)
			{
				index = lastSelectedIndex;
				previousActiveIndex = lastSelectedIndex;
				if (elements[lastSelectedIndex] != null)
				{
					selectButton(lastSelectedIndex);
				}
			}
			if (useSelectionFollower && selectionFollowerContainer != null)
			{
				float z = Mathf.Atan2(deltaPullPosition.y, deltaPullPosition.x) * 57.29578f + 270f;
				selectionFollowerContainer.rotation = Quaternion.Euler(0f, 0f, z);
				savedSelectionFollowerAngle = z;
			}
			lastMousePosition = Vector2.zero;
			if (pullLineRenderer != null)
			{
				pullLineRenderer.enabled = true;
			}
		}
		else
		{
			if (angleOffset != 0f && elements != null && index >= 0 && index < elements.Count)
			{
				lastSelectedIndex = index;
			}
			if (useSelectionFollower && selectionFollowerContainer != null)
			{
				savedSelectionFollowerAngle = selectionFollowerContainer.rotation.eulerAngles.z;
			}
			if (pullLineRenderer != null)
			{
				pullLineRenderer.enabled = false;
			}
		}
	}

	private void RestoreDeltaPositionToLastSelected()
	{
		if (angleOffset == 0f || elements == null || lastSelectedIndex < 0 || lastSelectedIndex >= elements.Count)
		{
			deltaPullPosition = Vector2.zero;
			return;
		}
		float num = angleOffset * (float)lastSelectedIndex + angleOffset / 2f;
		float f = (90f - globalOffset + angleOffset / 2f - num) * (MathF.PI / 180f);
		Vector2 vector = new Vector2(Mathf.Cos(f), Mathf.Sin(f));
		float num2 = maxPullDistance * 0.6f;
		deltaPullPosition = vector * num2;
	}

	private void Update()
	{
		bool gamepadStickMoved = CursorPointerInput.GamepadStickMoved;
		float num;
		if (useDeltaSelection && isDeltaModeActive && !useGamepad)
		{
			Vector2 mouseDelta = CursorPointerInput.MouseDelta;
			float magnitude = mouseDelta.magnitude;
			accumulatedInputMagnitude += magnitude * deltaSensitivity;
			if (!hasSubstantialInput && accumulatedInputMagnitude >= substantialInputThreshold)
			{
				hasSubstantialInput = true;
			}
			if (hasSubstantialInput)
			{
				deltaPullPosition += mouseDelta * deltaSensitivity;
			}
			else
			{
				deltaPullPosition = initialDeltaPosition;
			}
			if (deltaPullPosition.magnitude > maxPullDistance)
			{
				deltaPullPosition = deltaPullPosition.normalized * maxPullDistance;
			}
			num = Mathf.Atan2(deltaPullPosition.y, deltaPullPosition.x) * 57.29578f;
			UpdatePullLine();
		}
		else if (!useGamepad)
		{
			Vector3 screenPosition3D = CursorPointerInput.ScreenPosition3D;
			num = Mathf.Atan2(screenPosition3D.y - rt.position.y, screenPosition3D.x - rt.position.x) * 57.29578f;
		}
		else
		{
			Vector2 gamepadStick = CursorPointerInput.GamepadStick;
			num = Mathf.Atan2(gamepadStick.y, gamepadStick.x) * 57.29578f;
		}
		if (!useGamepad)
		{
			currentAngle = normalizeAngle(0f - num + 90f - globalOffset + angleOffset / 2f);
		}
		else if (gamepadStickMoved)
		{
			currentAngle = normalizeAngle(0f - num + 90f - globalOffset + angleOffset / 2f);
		}
		if (angleOffset != 0f)
		{
			int num2 = (int)(currentAngle / angleOffset);
			if (!isDeltaModeActive || hasSubstantialInput)
			{
				index = num2;
			}
			if (elements[index] != null)
			{
				selectButton(index);
			}
		}
		if (useSelectionFollower && selectionFollowerContainer != null)
		{
			bool flag = false;
			if (useDeltaSelection && isDeltaModeActive && !useGamepad)
			{
				flag = hasSubstantialInput;
			}
			else if (!useGamepad | gamepadStickMoved)
			{
				flag = true;
			}
			if (flag)
			{
				selectionFollowerContainer.rotation = Quaternion.Euler(0f, 0f, num + 270f);
				savedSelectionFollowerAngle = num + 270f;
			}
		}
	}

	private void UpdatePullLine()
	{
		if (!(pullLineRenderer == null) && pullLineRenderer.enabled)
		{
			Vector3 zero = Vector3.zero;
			Vector3 position = Vector3.zero;
			if (parentCanvas != null && rt != null)
			{
				float scaleFactor = parentCanvas.scaleFactor;
				position = new Vector3(deltaPullPosition.x / scaleFactor, deltaPullPosition.y / scaleFactor, 0f);
			}
			else if (rt != null)
			{
				position = new Vector3(deltaPullPosition.x, deltaPullPosition.y, 0f);
			}
			pullLineRenderer.SetPosition(0, zero);
			pullLineRenderer.SetPosition(1, position);
		}
	}

	private void selectButton(int i)
	{
		if (i < 0 || i >= elements.Count || elements[i] == null)
		{
			return;
		}
		for (int j = 0; j < elements.Count; j++)
		{
			if (elements[j] != null && elements[j].active)
			{
				elements[j].unHighlightThisElement(pointer);
			}
		}
		if (!elements[i].active)
		{
			elements[i].highlightThisElement(pointer);
		}
		else
		{
			elements[i].highlightThisElement(pointer);
		}
		previousActiveIndex = i;
	}

	private float normalizeAngle(float angle)
	{
		angle %= 360f;
		if (angle < 0f)
		{
			angle += 360f;
		}
		return angle;
	}

	public void UpdateSelectionFollowerState()
	{
		if (selectionFollowerContainer != null)
		{
			selectionFollowerContainer.gameObject.SetActive(useSelectionFollower);
		}
	}
}

}
