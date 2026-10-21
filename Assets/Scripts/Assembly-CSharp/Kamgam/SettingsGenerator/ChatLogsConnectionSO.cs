using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	[CreateAssetMenu(fileName = "ChatLogsConnection", menuName = "SettingsGenerator/ConnectionCustom/ChatLogsConnection", order = 1)]
	public sealed class ChatLogsConnectionSO : BoolConnectionSO
	{
		private ChatLogsConnection _connection;

		public override IConnection<bool> GetConnection()
		{
			if (_connection == null)
			{
				_connection = new ChatLogsConnection();
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
