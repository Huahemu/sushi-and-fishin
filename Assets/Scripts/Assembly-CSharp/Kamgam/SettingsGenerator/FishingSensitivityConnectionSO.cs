using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	public sealed class FishingSensitivityConnectionSO : FloatConnectionSO
	{
		private sealed class FishingSensitivityConnection : Connection<float>
		{
			public override float Get()
			{
				return PlayerPrefs.GetFloat("Fishing.Sensitivity", 1f);
			}

			public override float GetDefault()
			{
				return 1f;
			}

			public override void Set(float value)
			{
				value = Mathf.Clamp(value, 0.25f, 3f);
				PlayerPrefs.SetFloat("Fishing.Sensitivity", value);
				PlayerPrefs.Save();
				NotifyListenersIfChanged(value);
			}
		}

		private IConnection<float> _connection;

		public override IConnection<float> GetConnection()
		{
			return _connection ?? (_connection = new FishingSensitivityConnection());
		}

		public override void DestroyConnection()
		{
			_connection?.Destroy();
			_connection = null;
		}
	}
}
