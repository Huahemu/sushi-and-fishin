using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	[CreateAssetMenu(fileName = "CamShakeConnection", menuName = "SettingsGenerator/ConnectionCustom/CamShakeConnection", order = 1)]
	public class CamShakeConnectionSO : BoolConnectionSO
	{
		protected CamShakeConnection _connection;

		public override IConnection<bool> GetConnection()
		{
			if (_connection == null)
			{
				Create();
			}
			return _connection;
		}

		public void Create()
		{
			_connection = new CamShakeConnection();
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
