using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	[CreateAssetMenu(fileName = "ShowHudConnection", menuName = "SettingsGenerator/ConnectionCustom/ShowHudConnection", order = 1)]
	public class ShowHudConnectionSO : BoolConnectionSO
	{
		protected ShowHudConnection _connection;

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
			_connection = new ShowHudConnection();
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
