namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

public static class InputEvents
{
	public static InputLayer ActiveLayer;

	public static Action<Vector2> OnMoveEvent;

	public static Action<Vector2> OnAimEvent;

	public static Action<bool> OnJumpEvent;

	public static Action<bool> OnCrouchEvent;

	public static Action<bool> OnSprintEvent;

	public static Action<bool> OnInteractEvent;

	public static Action<bool> OnThrowItemEvent;

	public static Action<bool> OnUseItemEvent;

	public static Action<bool> OnZoomEvent;

	public static Action<int> OnItemSelectEvent;

	public static Action<int> OnScrollEvent;

	public static Action OnConsoleEvent;

	public static Action OnEscapeMenuEvent;

	public static Action<bool> OnEmoteWheelEvent;

	public static Action OnF1Event;

	public static Action OnF2Event;

	public static Action OnF3Event;

	public static Action OnF4Event;

	public static Action OnPingEvent;

	public static Action<bool> OnPushToTalkEvent;

	public static Action<bool> OnAnyInputEvent;

	public static Action<bool> OnSkipUIEvent;

	private static bool _isInteractPressed;

	private static bool _isUseItemPressed;

	private static bool _isThrowItemPressed;

	private static bool _isZoomPressed;

	private static bool _isJumpPressed;

	private static bool _isCrouchPressed;

	private static bool _isSprintPressed;

	private static bool _isEmoteWheelPressed;

	private static bool _isPingPressed;

	private static bool _isSkipUIPressed;

	private static bool _isPushToTalkInputPressed;

	public static bool IsDev { get; private set; }

	public static VoiceChatInputMode ProximityVoiceChatMode { get; private set; } = VoiceChatInputMode.VoiceActivation;

	public static bool IsInteractPressed => _isInteractPressed;

	public static bool IsUseItemPressed => _isUseItemPressed;

	public static bool IsThrowItemPressed => _isThrowItemPressed;

	public static bool IsZoomPressed => _isZoomPressed;

	public static bool IsJumpPressed => _isJumpPressed;

	public static bool IsCrouchPressed => _isCrouchPressed;

	public static bool IsSprintPressed => _isSprintPressed;

	public static bool IsEmoteWheelPressed => _isEmoteWheelPressed;

	public static bool IsPingPressed => _isPingPressed;

	public static bool IsSkipUIPressed => _isSkipUIPressed;

	public static bool IsPushToTalkPressed
	{
		get
		{
			if (ProximityVoiceChatMode != VoiceChatInputMode.PushToTalk)
			{
				return true;
			}
			return _isPushToTalkInputPressed;
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void ResetInputLayers()
	{
		ActiveLayer = InputLayer.Default;
		IsDev = false;
		ProximityVoiceChatMode = VoiceChatInputMode.VoiceActivation;
		_isPushToTalkInputPressed = false;
	}

	public static void UpdateInteract(bool isPressed)
	{
		_isInteractPressed = isPressed;
		OnInteractEvent?.Invoke(isPressed);
	}

	public static void UpdateUseItem(bool isPressed)
	{
		_isUseItemPressed = isPressed;
		OnUseItemEvent?.Invoke(isPressed);
	}

	public static void UpdateThrowItem(bool isPressed)
	{
		_isThrowItemPressed = isPressed;
		OnThrowItemEvent?.Invoke(isPressed);
	}

	public static void UpdateZoom(bool isPressed)
	{
		_isZoomPressed = isPressed;
		OnZoomEvent?.Invoke(isPressed);
	}

	public static void UpdateJump(bool isPressed)
	{
		_isJumpPressed = isPressed;
		OnJumpEvent?.Invoke(isPressed);
	}

	public static void UpdateCrouch(bool isPressed)
	{
		_isCrouchPressed = isPressed;
		OnCrouchEvent?.Invoke(isPressed);
	}

	public static void UpdateSprint(bool isPressed)
	{
		_isSprintPressed = isPressed;
		OnSprintEvent?.Invoke(isPressed);
	}

	public static void UpdateEmoteWheel(bool isPressed)
	{
		_isEmoteWheelPressed = isPressed;
		OnEmoteWheelEvent?.Invoke(isPressed);
	}

	public static void UpdatePing(bool isPressed)
	{
		_isPingPressed = isPressed;
		OnPingEvent?.Invoke();
	}

	public static void UpdateSkipUI(bool isPressed)
	{
		_isSkipUIPressed = isPressed;
		OnSkipUIEvent?.Invoke(isPressed);
	}

	public static void UpdatePushToTalk(bool isPressed)
	{
		_isPushToTalkInputPressed = isPressed;
		OnPushToTalkEvent?.Invoke(IsPushToTalkPressed);
	}

	public static void SetProximityVoiceChatMode(VoiceChatInputMode mode)
	{
		if (ProximityVoiceChatMode != mode)
		{
			ProximityVoiceChatMode = mode;
			OnPushToTalkEvent?.Invoke(IsPushToTalkPressed);
		}
	}

	public static void SetProximityVoiceChatMode(string selectedOption)
	{
		if (!string.IsNullOrWhiteSpace(selectedOption))
		{
			SetProximityVoiceChatMode((!selectedOption.Trim().ToLowerInvariant().StartsWith("push")) ? VoiceChatInputMode.VoiceActivation : VoiceChatInputMode.PushToTalk);
		}
	}

	public static void SetDevMode(bool isDev)
	{
		IsDev = isDev;
	}

	public static void TryInvokeConsole()
	{
		if (IsDev)
		{
			OnConsoleEvent?.Invoke();
		}
	}

	public static void TryInvokeF1()
	{
		if (IsDev)
		{
			OnF1Event?.Invoke();
		}
	}

	public static void TryInvokeF2()
	{
		if (IsDev)
		{
			OnF2Event?.Invoke();
		}
	}

	public static void TryInvokeF3()
	{
		if (IsDev)
		{
			OnF3Event?.Invoke();
		}
	}

	public static void TryInvokeF4()
	{
		if (IsDev)
		{
			OnF4Event?.Invoke();
		}
	}
}

}
