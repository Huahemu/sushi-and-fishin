using PaperPixelGames.Core.Settings;

namespace Kamgam.SettingsGenerator
{
	public class CamShakeConnection : Connection<bool>
	{
		public override bool Get()
		{
			return RuntimeSettings.CameraShakeEnabled;
		}

		public override bool GetDefault()
		{
			return true;
		}

		public override void Set(bool status)
		{
			if (RuntimeSettings.CameraShakeEnabled != status)
			{
				RuntimeSettings.SetCameraShake(status);
				NotifyListenersIfChanged(status);
			}
		}
	}
}
