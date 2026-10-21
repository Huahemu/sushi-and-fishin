namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;
using global::UnityEngine.EventSystems;
using global::UnityEngine.InputSystem;
using global::UnityEngine.InputSystem.UI;
using global::UnityEngine.UI;

public class InputModeManager : MonoSingleton<InputModeManager>
{
	[Header("Settings")]
	[SerializeField]
	private float joystickDeadzone = 0.3f;

	[SerializeField]
	private float joystickThreshold = 0.5f;

	[Tooltip("Minimum magnitude of joystick movement to switch to controller mode")]
	[SerializeField]
	private float minJoystickMagnitude = 0.4f;

	[Header("Mouse Noise Filtering")]
	[SerializeField]
	private float mouseDeltaSwitchThreshold = 2f;

	[SerializeField]
	private float mouseScrollSwitchThreshold = 0.01f;

	[SerializeField]
	private InputMode currentInputMode;

	private InputSystemUIInputModule uiInputModule;

	private StandaloneInputModule legacyInputModule;

	private readonly HashSet<UIImportance> trackedUIImportance = new HashSet<UIImportance>();

	public static Action<InputMode> OnInputModeChanged;

	public InputMode CurrentInputMode => currentInputMode;

	protected override void OnAwake()
	{
		base.OnAwake();
		EnsureUIInputModule();
	}

	private void Start()
	{
		if (UICursor.Instance != null)
		{
			UICursor.Instance.SetInputModeEnabled(currentInputMode == InputMode.KeyboardMouse);
		}
	}

	private void Update()
	{
		CheckInputDevice();
	}

	private void EnsureUIInputModule()
	{
		if (EventSystem.current == null)
		{
			new GameObject("EventSystem").AddComponent<EventSystem>();
		}
		uiInputModule = EventSystem.current.GetComponent<InputSystemUIInputModule>();
		if (uiInputModule == null)
		{
			uiInputModule = EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
		}
		legacyInputModule = EventSystem.current.GetComponent<StandaloneInputModule>();
		if (legacyInputModule != null)
		{
			legacyInputModule.enabled = false;
		}
		uiInputModule.enabled = true;
	}

	private void CheckInputDevice()
	{
		bool flag = false;
		bool flag2 = false;
		Keyboard current = Keyboard.current;
		Mouse current2 = Mouse.current;
		Gamepad current3 = Gamepad.current;
		if (current != null && current.anyKey.wasPressedThisFrame)
		{
			flag = true;
		}
		if (current2 != null)
		{
			if (current2.leftButton.wasPressedThisFrame || current2.rightButton.wasPressedThisFrame || current2.middleButton.wasPressedThisFrame || current2.scroll.ReadValue().magnitude > mouseScrollSwitchThreshold)
			{
				flag = true;
			}
			else if (current2.delta.ReadValue().magnitude > mouseDeltaSwitchThreshold)
			{
				flag = true;
			}
		}
		if (current3 != null)
		{
			Vector2 vector = current3.leftStick.ReadValue();
			Vector2 vector2 = current3.rightStick.ReadValue();
			float magnitude = vector.magnitude;
			float magnitude2 = vector2.magnitude;
			if ((magnitude > joystickDeadzone && magnitude >= minJoystickMagnitude) || (magnitude2 > joystickDeadzone && magnitude2 >= minJoystickMagnitude))
			{
				flag2 = true;
			}
			if (current3.buttonSouth.wasPressedThisFrame || current3.buttonNorth.wasPressedThisFrame || current3.buttonEast.wasPressedThisFrame || current3.buttonWest.wasPressedThisFrame || current3.dpad.ReadValue().magnitude > joystickDeadzone || current3.leftShoulder.wasPressedThisFrame || current3.rightShoulder.wasPressedThisFrame || current3.leftTrigger.ReadValue() > joystickThreshold || current3.rightTrigger.ReadValue() > joystickThreshold || current3.startButton.wasPressedThisFrame || current3.selectButton.wasPressedThisFrame)
			{
				flag2 = true;
			}
		}
		InputMode inputMode = currentInputMode;
		if (flag)
		{
			inputMode = InputMode.KeyboardMouse;
		}
		else if (flag2)
		{
			inputMode = InputMode.Controller;
		}
		if (inputMode != currentInputMode)
		{
			SetInputMode(inputMode);
		}
	}

	private void SetInputMode(InputMode newMode)
	{
		currentInputMode = newMode;
		OnInputModeChanged?.Invoke(newMode);
		if (UICursor.Instance != null)
		{
			UICursor.Instance.SetInputModeEnabled(newMode == InputMode.KeyboardMouse);
		}
		if (uiInputModule != null)
		{
			uiInputModule.enabled = true;
		}
		if (newMode == InputMode.Controller)
		{
			StartCoroutine(SelectBestSelectableNextFrame());
		}
		else if (EventSystem.current != null)
		{
			EventSystem.current.SetSelectedGameObject(null);
		}
	}

	private IEnumerator SelectBestSelectableNextFrame()
	{
		yield return null;
		if (EventSystem.current == null)
		{
			yield break;
		}
		UIImportance mostImportantAvailable = GetMostImportantAvailable();
		if (mostImportantAvailable != null)
		{
			ForceSelect(mostImportantAvailable);
			yield break;
		}
		GameObject firstSelectedGameObject = EventSystem.current.firstSelectedGameObject;
		if (firstSelectedGameObject != null && firstSelectedGameObject.activeInHierarchy)
		{
			Selectable component = firstSelectedGameObject.GetComponent<Selectable>();
			if (component != null && component.IsInteractable())
			{
				ForceSelect(component);
				yield break;
			}
		}
		Selectable selectable = FindFirstAvailableSelectable();
		if (selectable != null)
		{
			ForceSelect(selectable);
		}
	}

	private UIImportance GetMostImportantAvailable()
	{
		UIImportance result = null;
		int num = int.MinValue;
		foreach (UIImportance item in trackedUIImportance)
		{
			if (!(item == null) && item.IsVisibleAndEnabled() && item.Importance > num)
			{
				num = item.Importance;
				result = item;
			}
		}
		return result;
	}

	private Selectable FindFirstAvailableSelectable()
	{
		Selectable[] array = UnityEngine.Object.FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		foreach (Selectable selectable in array)
		{
			if (!(selectable == null) && selectable.IsInteractable() && selectable.gameObject.activeInHierarchy && selectable.navigation.mode != Navigation.Mode.None)
			{
				return selectable;
			}
		}
		return null;
	}

	private void ForceSelect(UIImportance ui)
	{
		if (!(ui == null) && !(ui.Selectable == null))
		{
			ForceSelect(ui.Selectable);
		}
	}

	private void ForceSelect(Selectable selectable)
	{
		if (!(selectable == null) && !(EventSystem.current == null) && selectable.IsInteractable() && selectable.gameObject.activeInHierarchy)
		{
			if (selectable.navigation.mode == Navigation.Mode.None)
			{
				Navigation navigation = selectable.navigation;
				navigation.mode = Navigation.Mode.Automatic;
				selectable.navigation = navigation;
			}
			EventSystem.current.SetSelectedGameObject(null);
			selectable.Select();
		}
	}

	public bool IsControllerActive()
	{
		return currentInputMode == InputMode.Controller;
	}

	public bool IsKeyboardMouseActive()
	{
		return currentInputMode == InputMode.KeyboardMouse;
	}

	public void OnUIImportanceEnabled(UIImportance uiImportance)
	{
		if (!(uiImportance == null))
		{
			trackedUIImportance.Add(uiImportance);
			if (currentInputMode == InputMode.Controller)
			{
				StartCoroutine(TrySelectIfBetter(uiImportance));
			}
		}
	}

	private IEnumerator TrySelectIfBetter(UIImportance uiImportance)
	{
		yield return null;
		if (!(uiImportance == null) && currentInputMode == InputMode.Controller && !(EventSystem.current == null) && uiImportance.IsVisibleAndEnabled())
		{
			GameObject currentSelectedGameObject = EventSystem.current.currentSelectedGameObject;
			UIImportance uIImportance = ((currentSelectedGameObject != null) ? currentSelectedGameObject.GetComponent<UIImportance>() : null);
			if (uIImportance == null || uiImportance.Importance > uIImportance.Importance)
			{
				ForceSelect(uiImportance);
			}
		}
	}

	public void OnUIImportanceDisabled(UIImportance uiImportance)
	{
		if (!(uiImportance == null))
		{
			trackedUIImportance.Remove(uiImportance);
			if (currentInputMode == InputMode.Controller && !(EventSystem.current == null) && EventSystem.current.currentSelectedGameObject == uiImportance.gameObject)
			{
				StartCoroutine(SelectBestSelectableNextFrame());
			}
		}
	}
}

}
