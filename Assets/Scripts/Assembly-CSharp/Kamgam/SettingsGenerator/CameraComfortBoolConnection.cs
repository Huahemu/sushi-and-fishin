using PaperPixelGames.Core.Settings;

namespace Kamgam.SettingsGenerator
{
	public sealed class CameraComfortBoolConnection : Connection<bool>
	{
		private readonly CameraComfortBoolSetting _setting;

		private readonly bool _defaultValue;

		public CameraComfortBoolConnection(CameraComfortBoolSetting setting, bool defaultValue)
		{
			_setting = setting;
			_defaultValue = defaultValue;
		}

		public override bool Get()
		{
			return _setting switch
			{
				CameraComfortBoolSetting.CameraBob => RuntimeSettings.CameraBobEnabled, 
				CameraComfortBoolSetting.LandingCameraMotion => RuntimeSettings.LandingCameraMotionEnabled, 
				CameraComfortBoolSetting.DynamicFovEffects => RuntimeSettings.DynamicFovEffectsEnabled, 
				CameraComfortBoolSetting.UnderwaterDepthOfField => RuntimeSettings.UnderwaterDepthOfFieldEnabled, 
				CameraComfortBoolSetting.UnderwaterLensDistortion => RuntimeSettings.UnderwaterLensDistortionEnabled, 
				CameraComfortBoolSetting.ViewmodelMotion => RuntimeSettings.ViewmodelMotionEnabled, 
				CameraComfortBoolSetting.DamageCameraMotion => RuntimeSettings.DamageCameraMotionEnabled, 
				_ => _defaultValue, 
			};
		}

		public override bool GetDefault()
		{
			return _defaultValue;
		}

		public override void Set(bool value)
		{
			if (Get() != value)
			{
				switch (_setting)
				{
				case CameraComfortBoolSetting.CameraBob:
					RuntimeSettings.SetCameraBob(value);
					break;
				case CameraComfortBoolSetting.LandingCameraMotion:
					RuntimeSettings.SetLandingCameraMotion(value);
					break;
				case CameraComfortBoolSetting.DynamicFovEffects:
					RuntimeSettings.SetDynamicFovEffects(value);
					break;
				case CameraComfortBoolSetting.UnderwaterDepthOfField:
					RuntimeSettings.SetUnderwaterDepthOfField(value);
					break;
				case CameraComfortBoolSetting.UnderwaterLensDistortion:
					RuntimeSettings.SetUnderwaterLensDistortion(value);
					break;
				case CameraComfortBoolSetting.ViewmodelMotion:
					RuntimeSettings.SetViewmodelMotion(value);
					break;
				case CameraComfortBoolSetting.DamageCameraMotion:
					RuntimeSettings.SetDamageCameraMotion(value);
					break;
				}
				NotifyListenersIfChanged(value);
			}
		}
	}
}
