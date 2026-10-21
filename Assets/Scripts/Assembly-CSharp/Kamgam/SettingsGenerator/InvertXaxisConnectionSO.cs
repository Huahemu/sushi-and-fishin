using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	[CreateAssetMenu(fileName = "InvertXaxisConnection", menuName = "SettingsGenerator/ConnectionCustom/InvertXaxisConnection", order = 1)]
	public class InvertXaxisConnectionSO : BoolConnectionSO
	{
		protected InvertXaxisConnection _connection;

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
			_connection = new InvertXaxisConnection();
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
