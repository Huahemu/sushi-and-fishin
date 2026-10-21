using System;
using UnityEngine;

namespace PaperPixelGames.Core.Settings
{
	public static class RuntimeSettings
	{
		public static bool PushToTalkEnabled { get; private set; } = true;

		public static bool InvertXAxisEnabled { get; private set; } = false;

		public static bool InvertYAxisEnabled { get; private set; } = false;

		public static bool CameraShakeEnabled { get; private set; } = true;

		public static bool CameraBobEnabled { get; private set; } = true;

		public static bool LandingCameraMotionEnabled { get; private set; } = true;

		public static bool DynamicFovEffectsEnabled { get; private set; } = true;

		public static bool UnderwaterDepthOfFieldEnabled { get; private set; } = true;

		public static bool UnderwaterLensDistortionEnabled { get; private set; } = true;

		public static bool ViewmodelMotionEnabled { get; private set; } = true;

		public static bool DamageCameraMotionEnabled { get; private set; } = true;

		public static bool ShowHudEnabled { get; private set; } = true;

		public static bool PlayerNamesEnabled { get; private set; } = true;

		public static bool ChatLogsEnabled { get; private set; } = true;

		public static bool RestaurantChatLogsEnabled { get; private set; } = true;

		public static bool ToggleSprintEnabled { get; private set; } = true;

		public static bool PlayerCollisionEnabled { get; private set; } = false;

		public static float Sensitivity { get; private set; } = 5f;

		public static float RenderDistance { get; private set; } = 300f;

		public static float FieldOfView { get; private set; } = 60f;

		public static event Action<bool> ToggleSprintChanged;

		public static event Action<float> RenderDistanceChanged;

		public static event Action<bool> ChatLogsChanged;

		public static event Action<bool> RestaurantChatLogsChanged;

		public static event Action<bool> PlayerCollisionChanged;

		public static event Action<float> FieldOfViewChanged;

		public static event Action<bool> DynamicFovEffectsChanged;

		public static event Action<bool> UnderwaterDepthOfFieldChanged;

		public static event Action<bool> UnderwaterLensDistortionChanged;

		public static void SetSensitivity(float value)
		{
			Sensitivity = value;
		}

		public static void SetRenderDistance(float value)
		{
			if (!(Math.Abs(RenderDistance - value) < 0.001f))
			{
				RenderDistance = value;
				RenderDistanceChanged?.Invoke(value);
			}
		}

		public static void SetPushToTalk(bool value)
		{
			if (PushToTalkEnabled != value)
			{
				PushToTalkEnabled = value;
			}
		}

		public static void SetInvertXAxis(bool value)
		{
			if (InvertXAxisEnabled != value)
			{
				InvertXAxisEnabled = value;
			}
		}

		public static void SetInvertYAxis(bool value)
		{
			if (InvertYAxisEnabled != value)
			{
				InvertYAxisEnabled = value;
			}
		}

		public static void SetCameraShake(bool value)
		{
			if (CameraShakeEnabled != value)
			{
				CameraShakeEnabled = value;
			}
		}

		public static void SetCameraBob(bool value)
		{
			if (CameraBobEnabled != value)
			{
				CameraBobEnabled = value;
			}
		}

		public static void SetLandingCameraMotion(bool value)
		{
			if (LandingCameraMotionEnabled != value)
			{
				LandingCameraMotionEnabled = value;
			}
		}

		public static void SetDynamicFovEffects(bool value)
		{
			if (DynamicFovEffectsEnabled != value)
			{
				DynamicFovEffectsEnabled = value;
				DynamicFovEffectsChanged?.Invoke(value);
			}
		}

		public static void SetUnderwaterDepthOfField(bool value)
		{
			if (UnderwaterDepthOfFieldEnabled != value)
			{
				UnderwaterDepthOfFieldEnabled = value;
				UnderwaterDepthOfFieldChanged?.Invoke(value);
			}
		}

		public static void SetUnderwaterLensDistortion(bool value)
		{
			if (UnderwaterLensDistortionEnabled != value)
			{
				UnderwaterLensDistortionEnabled = value;
				UnderwaterLensDistortionChanged?.Invoke(value);
			}
		}

		public static void SetViewmodelMotion(bool value)
		{
			ViewmodelMotionEnabled = value;
		}

		public static void SetDamageCameraMotion(bool value)
		{
			DamageCameraMotionEnabled = value;
		}

		public static void SetFieldOfView(float value)
		{
			float num = Math.Clamp(value, 50f, 90f);
			if (!(Math.Abs(FieldOfView - num) < 0.001f))
			{
				FieldOfView = num;
				FieldOfViewChanged?.Invoke(num);
			}
		}

		public static void SetShowHud(bool value)
		{
			if (ShowHudEnabled != value)
			{
				ShowHudEnabled = value;
			}
		}

		public static void SetPlayerNames(bool value)
		{
			if (PlayerNamesEnabled != value)
			{
				PlayerNamesEnabled = value;
			}
		}

		public static void SetChatLogs(bool value)
		{
			if (ChatLogsEnabled != value)
			{
				ChatLogsEnabled = value;
				ChatLogsChanged?.Invoke(value);
			}
		}

		public static void SetRestaurantChatLogs(bool value)
		{
			if (RestaurantChatLogsEnabled != value)
			{
				RestaurantChatLogsEnabled = value;
				RestaurantChatLogsChanged?.Invoke(value);
			}
		}

		public static void SetToggleSprint(bool value)
		{
			if (ToggleSprintEnabled != value)
			{
				ToggleSprintEnabled = value;
				ToggleSprintChanged?.Invoke(value);
			}
		}

		public static void SetPlayerCollision(bool value, bool forceApply = false)
		{
			if (PlayerCollisionEnabled != value || forceApply)
			{
				PlayerCollisionEnabled = value;
				int num = LayerMask.NameToLayer("Player");
				int num2 = LayerMask.NameToLayer("ClientPlayer");
				if (num < 0 || num2 < 0)
				{
					Debug.LogWarning("[RuntimeSettings] Player collision setting could not be applied because the Player or ClientPlayer layer is missing.");
				}
				else
				{
					Physics.IgnoreLayerCollision(num, num2, !value);
				}
				PlayerCollisionChanged?.Invoke(value);
			}
		}
	}
}
