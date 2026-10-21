using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	[CreateAssetMenu(fileName = "SprintToggleConnection", menuName = "SettingsGenerator/ConnectionCustom/SprintToggleConnection", order = 1)]
	public class SprintToggleConnectionSO : BoolConnectionSO
	{
		protected SprintToggleConnection _connection;

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
			_connection = new SprintToggleConnection();
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
