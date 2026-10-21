using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	[CreateAssetMenu(fileName = "CameraComfortBoolConnection", menuName = "SettingsGenerator/ConnectionCustom/Camera Comfort Bool", order = 10)]
	public sealed class CameraComfortBoolConnectionSO : BoolConnectionSO
	{
		public CameraComfortBoolSetting Setting;

		public bool DefaultValue;

		private CameraComfortBoolConnection _connection;

		public override IConnection<bool> GetConnection()
		{
			if (_connection == null)
			{
				_connection = new CameraComfortBoolConnection(Setting, DefaultValue);
			}
			return _connection;
		}

		public override void DestroyConnection()
		{
			if (_connection != null)
			{
				_connection.Destroy();
			}
			_connection = null;
		}
	}
}
