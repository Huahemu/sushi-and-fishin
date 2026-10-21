using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	[CreateAssetMenu(fileName = "PlayingBackgroundConnection", menuName = "SettingsGenerator/ConnectionCustom/PlayingBackgroundConnection", order = 1)]
	public class PlayingBackgroundSO : BoolConnectionSO
	{
		protected PlayingBackground _connection;

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
			_connection = new PlayingBackground();
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
