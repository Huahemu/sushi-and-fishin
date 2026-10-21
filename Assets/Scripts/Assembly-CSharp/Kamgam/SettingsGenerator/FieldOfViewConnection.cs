using PaperPixelGames.Core.Settings;
using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	public sealed class FieldOfViewConnection : Connection<float>
	{
		private readonly Vector2 _inputRange;

		private readonly float _defaultValue;

		public FieldOfViewConnection(Vector2 inputRange, float defaultValue)
		{
			_inputRange = inputRange;
			_defaultValue = defaultValue;
		}

		public override float Get()
		{
			return RuntimeSettings.FieldOfView;
		}

		public override float GetDefault()
		{
			return Mathf.Clamp(_defaultValue, _inputRange.x, _inputRange.y);
		}

		public override void Set(float value)
		{
			float num = Mathf.Clamp(value, _inputRange.x, _inputRange.y);
			if (!Mathf.Approximately(RuntimeSettings.FieldOfView, num))
			{
				RuntimeSettings.SetFieldOfView(num);
				NotifyListenersIfChanged(num);
			}
		}
	}
}
