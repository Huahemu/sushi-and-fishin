namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;
using global::UnityEngine.InputSystem;

public static class CursorPointerInput
{
	public static Vector2 ScreenPosition
	{
		get
		{
			if (Mouse.current != null)
			{
				return Mouse.current.position.ReadValue();
			}
			if (Pointer.current != null)
			{
				return Pointer.current.position.ReadValue();
			}
			return Vector2.zero;
		}
	}

	public static Vector3 ScreenPosition3D => new Vector3(ScreenPosition.x, ScreenPosition.y, 0f);

	public static bool LeftClickPressedThisFrame
	{
		get
		{
			if (Mouse.current != null)
			{
				return Mouse.current.leftButton.wasPressedThisFrame;
			}
			return false;
		}
	}

	public static Vector2 MouseDelta
	{
		get
		{
			if (Mouse.current == null)
			{
				return Vector2.zero;
			}
			return Mouse.current.delta.ReadValue();
		}
	}

	public static Vector2 GamepadStick
	{
		get
		{
			if (Gamepad.current == null)
			{
				return Vector2.zero;
			}
			return Gamepad.current.leftStick.ReadValue();
		}
	}

	public static bool GamepadStickMoved => GamepadStick.sqrMagnitude > 0.0001f;
}

}
