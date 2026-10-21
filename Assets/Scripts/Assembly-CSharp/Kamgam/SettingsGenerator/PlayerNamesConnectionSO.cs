using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	[CreateAssetMenu(fileName = "PlayerNamesConnection", menuName = "SettingsGenerator/ConnectionCustom/PlayerNamesConnection", order = 1)]
	public class PlayerNamesConnectionSO : BoolConnectionSO
	{
		protected PlayerNamesConnection _connection;

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
			_connection = new PlayerNamesConnection();
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
