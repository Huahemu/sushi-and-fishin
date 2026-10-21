namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;
using global::UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "New InputReader", menuName = "InputReader")]
public class InputReader : ScriptableObject, InputActions.IPlayerActions
{
	private const string BindingOverridesPlayerPrefsKey = "input.bindingOverrides";

	private InputActions _inputActions;

	private InputActionRebindingExtensions.RebindingOperation _rebindOperation;

	public static InputReader Instance { get; private set; }

	[RuntimeInitializeOnLoadMethod]
	private static void InitializeInputReader()
	{
        if (!CasinoRuntimeContext.Active) return;
		SushiAndFish.Casino.Original.CasinoResources.Load<InputReader>("InputReader");
	}

	private void OnEnable()
	{
		Instance = this;
        if (CasinoRuntimeContext.IsHostIntegration) return;
		if (_inputActions == null)
		{
			_inputActions = new InputActions();
			LoadBindingOverrides();
			_inputActions.Player.SetCallbacks(this);
			_inputActions.Player.Enable();
		}
	}

	private void OnDisable()
	{
		CancelCurrentRebind();
		_inputActions?.Player.Disable();
		if ((object)Instance == this)
		{
			Instance = null;
		}
	}

	~InputReader()
	{
		CancelCurrentRebind();
		_inputActions?.Dispose();
	}

	public string GetBindingDisplayName(string actionName, int bindingIndex)
	{
		if (!TryGetAction(actionName, out var action))
		{
			return "Unassigned";
		}
		if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
		{
			return "Unassigned";
		}
		return action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
	}

	public string GetBindingEffectivePath(string actionName, int bindingIndex)
	{
		if (!TryGetAction(actionName, out var action))
		{
			return string.Empty;
		}
		if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
		{
			return string.Empty;
		}
		return action.bindings[bindingIndex].effectivePath;
	}

	public bool ApplyBindingOverride(string actionName, int bindingIndex, string overridePath)
	{
		if (CasinoLab.CasinoLabMode.Active) return false;
		if (!TryGetAction(actionName, out var action))
		{
			return false;
		}
		if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
		{
			return false;
		}
		action.ApplyBindingOverride(bindingIndex, overridePath);
		SaveBindingOverrides();
		return true;
	}

	public bool StartInteractiveRebind(string actionName, int bindingIndex, Action<string> onComplete, Action onCancelled = null)
	{
		if (CasinoLab.CasinoLabMode.Active) return false;
		if (!TryGetAction(actionName, out var action))
		{
			return false;
		}
		if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
		{
			return false;
		}
		InputBinding inputBinding = action.bindings[bindingIndex];
		if (inputBinding.isComposite || inputBinding.isPartOfComposite)
		{
			return false;
		}
		CancelCurrentRebind();
		_inputActions.Player.Disable();
		_rebindOperation = action.PerformInteractiveRebinding(bindingIndex).WithCancelingThrough("<Keyboard>/escape").OnComplete((InputActionRebindingExtensions.RebindingOperation op) =>
		{
			SaveBindingOverrides();
			string bindingDisplayName = GetBindingDisplayName(actionName, bindingIndex);
			FinishRebindOperation();
			onComplete?.Invoke(bindingDisplayName);
		})
			.OnCancel((InputActionRebindingExtensions.RebindingOperation op) =>
			{
				FinishRebindOperation();
				onCancelled?.Invoke();
			});
		_rebindOperation.Start();
		return true;
	}

	private void FinishRebindOperation()
	{
		_rebindOperation?.Dispose();
		_rebindOperation = null;
		_inputActions?.Player.Enable();
	}

	private void CancelCurrentRebind()
	{
		if (_rebindOperation != null)
		{
			_rebindOperation.Cancel();
			_rebindOperation.Dispose();
			_rebindOperation = null;
		}
	}

	private bool TryGetAction(string actionName, out InputAction action)
	{
		action = null;
		if (_inputActions == null || string.IsNullOrWhiteSpace(actionName))
		{
			return false;
		}
		action = _inputActions.asset.FindAction(actionName);
		return action != null;
	}

	private void LoadBindingOverrides()
	{
		if (CasinoLab.CasinoLabMode.Active) return;
		string text = PlayerPrefs.GetString("input.bindingOverrides", string.Empty);
		if (!string.IsNullOrWhiteSpace(text))
		{
			_inputActions.asset.LoadBindingOverridesFromJson(text);
		}
	}

	private void SaveBindingOverrides()
	{
		if (CasinoLab.CasinoLabMode.Active) return;
		string value = _inputActions.asset.SaveBindingOverridesAsJson();
		PlayerPrefs.SetString("input.bindingOverrides", value);
		PlayerPrefs.Save();
	}

	public void OnMove(InputAction.CallbackContext context)
	{
		if (InputEvents.ActiveLayer == InputLayer.Default)
		{
			if (context.performed)
			{
				InputEvents.OnMoveEvent?.Invoke(context.ReadValue<Vector2>());
			}
			else if (context.canceled)
			{
				InputEvents.OnMoveEvent?.Invoke(Vector2.zero);
			}
		}
	}

	public void OnAim(InputAction.CallbackContext context)
	{
		if (InputEvents.ActiveLayer == InputLayer.Default || InputEvents.ActiveLayer == InputLayer.SpawnBox)
		{
			if (context.performed)
			{
				InputEvents.OnAimEvent?.Invoke(context.ReadValue<Vector2>());
			}
			else if (context.canceled)
			{
				InputEvents.OnAimEvent?.Invoke(Vector2.zero);
			}
		}
	}

	public void OnJump(InputAction.CallbackContext context)
	{
		if (InputEvents.ActiveLayer == InputLayer.Default && (context.performed || context.canceled))
		{
			InputEvents.UpdateJump(context.ReadValueAsButton());
		}
		InputEvents.OnAnyInputEvent?.Invoke(context.ReadValueAsButton());
	}

	public void OnCrouch(InputAction.CallbackContext context)
	{
		if (InputEvents.ActiveLayer == InputLayer.Default && (context.performed || context.canceled))
		{
			InputEvents.UpdateCrouch(context.ReadValueAsButton());
		}
	}

	public void OnSprint(InputAction.CallbackContext context)
	{
		if (InputEvents.ActiveLayer == InputLayer.Default && (context.performed || context.canceled))
		{
			InputEvents.UpdateSprint(context.ReadValueAsButton());
		}
	}

	public void OnInteract(InputAction.CallbackContext context)
	{
		if ((InputEvents.ActiveLayer == InputLayer.Default || InputEvents.ActiveLayer == InputLayer.SpawnBox) && (context.performed || context.canceled))
		{
			InputEvents.UpdateInteract(context.ReadValueAsButton());
		}
		InputEvents.OnAnyInputEvent?.Invoke(context.ReadValueAsButton());
	}

	public void OnZoom(InputAction.CallbackContext context)
	{
		if (InputEvents.ActiveLayer == InputLayer.Default && (context.performed || context.canceled))
		{
			InputEvents.UpdateZoom(context.ReadValueAsButton());
		}
		InputEvents.OnAnyInputEvent?.Invoke(context.ReadValueAsButton());
	}

	public void OnItemSelect(InputAction.CallbackContext context)
	{
		if (InputEvents.ActiveLayer == InputLayer.Default && context.performed)
		{
			switch (context.control.displayName)
			{
			case "1":
				InputEvents.OnItemSelectEvent?.Invoke(1);
				break;
			case "2":
				InputEvents.OnItemSelectEvent?.Invoke(2);
				break;
			case "3":
				InputEvents.OnItemSelectEvent?.Invoke(3);
				break;
			}
		}
	}

	public void OnScroll(InputAction.CallbackContext context)
	{
		if (InputEvents.ActiveLayer == InputLayer.Default && context.performed)
		{
			float num = context.ReadValue<float>();
			num = ((num > 0f) ? 1f : ((!(num < 0f)) ? 0f : (-1f)));
			InputEvents.OnScrollEvent?.Invoke((int)num);
		}
	}

	public void OnUseItem(InputAction.CallbackContext context)
	{
		if (InputEvents.ActiveLayer == InputLayer.Default && (context.performed || context.canceled))
		{
			InputEvents.UpdateUseItem(context.ReadValueAsButton());
		}
		InputEvents.OnAnyInputEvent?.Invoke(context.ReadValueAsButton());
	}

	public void OnThrowItem(InputAction.CallbackContext context)
	{
		if (InputEvents.ActiveLayer == InputLayer.Default && (context.performed || context.canceled))
		{
			InputEvents.UpdateThrowItem(context.ReadValueAsButton());
		}
	}

	public void OnConsole(InputAction.CallbackContext context)
	{
		if (context.performed)
		{
			InputEvents.TryInvokeConsole();
		}
	}

	public void OnEscapeMenu(InputAction.CallbackContext context)
	{
		if (context.performed)
		{
			InputEvents.OnEscapeMenuEvent?.Invoke();
		}
	}

	public void OnEmoteWheel(InputAction.CallbackContext context)
	{
		if (CasinoLab.CasinoLabMode.Active) return;
		if (InputEvents.ActiveLayer == InputLayer.Default && (context.performed || context.canceled))
		{
			InputEvents.UpdateEmoteWheel(context.ReadValueAsButton());
		}
	}

	public void OnF1(InputAction.CallbackContext context)
	{
		if (context.performed)
		{
			InputEvents.TryInvokeF1();
		}
	}

	public void OnF2(InputAction.CallbackContext context)
	{
		if (context.performed)
		{
			InputEvents.TryInvokeF2();
		}
	}

	public void OnF3(InputAction.CallbackContext context)
	{
		if (context.performed)
		{
			InputEvents.TryInvokeF3();
		}
	}

	public void OnF4(InputAction.CallbackContext context)
	{
		if (context.performed)
		{
			InputEvents.TryInvokeF4();
		}
	}

	public void OnPing(InputAction.CallbackContext context)
	{
		if (InputEvents.ActiveLayer == InputLayer.Default && context.performed)
		{
			InputEvents.UpdatePing(context.ReadValueAsButton());
		}
	}

	public void OnSkipUI(InputAction.CallbackContext context)
	{
		if (InputEvents.ActiveLayer == InputLayer.Cutscene && (context.performed || context.canceled))
		{
			InputEvents.UpdateSkipUI(context.ReadValueAsButton());
		}
		InputEvents.OnAnyInputEvent?.Invoke(context.ReadValueAsButton());
	}

	public void OnPushToTalk(InputAction.CallbackContext context)
	{
		if (InputEvents.ActiveLayer == InputLayer.Default)
		{
			if (context.performed || context.canceled)
			{
				InputEvents.UpdatePushToTalk(context.ReadValueAsButton());
			}
			InputEvents.OnAnyInputEvent?.Invoke(context.ReadValueAsButton());
		}
	}
}

}
