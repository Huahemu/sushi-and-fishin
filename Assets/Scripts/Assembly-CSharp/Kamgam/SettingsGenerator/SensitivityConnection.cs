using PaperPixelGames.Core.Settings;
using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	public class SensitivityConnection : Connection<float>
	{
		private readonly Vector2 _inputRange;

		public SensitivityConnection(Vector2 inputRange)
		{
			_inputRange = inputRange;
		}

		public override float Get()
		{
			return RuntimeSettings.Sensitivity;
		}

		public override float GetDefault()
		{
			return Mathf.Clamp(1f, _inputRange.x, _inputRange.y);
		}

		public override void Set(float sensitivity)
		{
			float num = Mathf.Clamp(sensitivity, _inputRange.x, _inputRange.y);
			if (!Mathf.Approximately(RuntimeSettings.Sensitivity, num))
			{
				RuntimeSettings.SetSensitivity(num);
				NotifyListenersIfChanged(num);
			}
		}
	}
}
