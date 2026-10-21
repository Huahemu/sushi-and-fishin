using PaperPixelGames.Core.Settings;
using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	public class RenderDistanceConnection : Connection<float>
	{
		private readonly Vector2 _inputRange;

		private readonly float _defaultValue;

		public RenderDistanceConnection(Vector2 inputRange, float defaultValue)
		{
			_inputRange = inputRange;
			_defaultValue = defaultValue;
		}

		public override float Get()
		{
			return RuntimeSettings.RenderDistance;
		}

		public override float GetDefault()
		{
			return Mathf.Clamp(_defaultValue, _inputRange.x, _inputRange.y);
		}

		public override void Set(float renderDistance)
		{
			float num = Mathf.Clamp(renderDistance, _inputRange.x, _inputRange.y);
			if (!Mathf.Approximately(RuntimeSettings.RenderDistance, num))
			{
				RuntimeSettings.SetRenderDistance(num);
				NotifyListenersIfChanged(num);
			}
		}
	}
}
