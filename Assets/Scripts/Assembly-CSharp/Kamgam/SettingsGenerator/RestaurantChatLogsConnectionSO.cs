using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	[CreateAssetMenu(fileName = "RestaurantChatLogsConnection", menuName = "SettingsGenerator/ConnectionCustom/RestaurantChatLogsConnection", order = 1)]
	public sealed class RestaurantChatLogsConnectionSO : BoolConnectionSO
	{
		private RestaurantChatLogsConnection _connection;

		public override IConnection<bool> GetConnection()
		{
			if (_connection == null)
			{
				_connection = new RestaurantChatLogsConnection();
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
