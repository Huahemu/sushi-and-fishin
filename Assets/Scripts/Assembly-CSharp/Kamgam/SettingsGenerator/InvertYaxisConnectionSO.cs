using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	[CreateAssetMenu(fileName = "InvertYaxisConnection", menuName = "SettingsGenerator/ConnectionCustom/InvertYaxisConnection", order = 1)]
	public class InvertYaxisConnectionSO : BoolConnectionSO
	{
		protected InvertYaxisConnection _connection;

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
			_connection = new InvertYaxisConnection();
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
