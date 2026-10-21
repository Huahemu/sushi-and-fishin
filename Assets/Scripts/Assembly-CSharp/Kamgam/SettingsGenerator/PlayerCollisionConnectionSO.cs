using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	[CreateAssetMenu(fileName = "PlayerCollisionConnection", menuName = "SettingsGenerator/ConnectionCustom/PlayerCollisionConnection", order = 1)]
	public class PlayerCollisionConnectionSO : BoolConnectionSO
	{
		protected PlayerCollisionConnection _connection;

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
			_connection = new PlayerCollisionConnection();
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
