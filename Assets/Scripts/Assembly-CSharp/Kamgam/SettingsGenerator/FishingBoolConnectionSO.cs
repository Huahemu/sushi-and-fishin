using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	public sealed class FishingBoolConnectionSO : BoolConnectionSO
	{
		private sealed class FishingBoolConnection : Connection<bool>
		{
			private readonly string _key;

			public FishingBoolConnection(string key)
			{
				_key = key;
			}

			public override bool Get()
			{
				return PlayerPrefs.GetInt(_key, 0) != 0;
			}

			public override bool GetDefault()
			{
				return false;
			}

			public override void Set(bool value)
			{
				PlayerPrefs.SetInt(_key, value ? 1 : 0);
				PlayerPrefs.Save();
				NotifyListenersIfChanged(value);
			}
		}

		public string PreferenceKey;

		private IConnection<bool> _connection;

		public override IConnection<bool> GetConnection()
		{
			return _connection ?? (_connection = new FishingBoolConnection(PreferenceKey));
		}

		public override void DestroyConnection()
		{
			_connection?.Destroy();
			_connection = null;
		}
	}
}
