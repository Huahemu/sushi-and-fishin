using PaperPixelGames.Framework.Input;
using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	[CreateAssetMenu(fileName = "CustomInputBindingConnection", menuName = "SettingsGenerator/ConnectionCustom/CustomInputBindingConnection", order = 4)]
	public class CustomInputBindingConnectionSO : StringConnectionSO
	{
		public InputHandler InputHandler;

		public string BindingId;

		protected CustomInputBindingConnection _connection;

		public override IConnection<string> GetConnection()
		{
			if (_connection == null)
			{
				Create();
			}
			return _connection;
		}

		public void Create()
		{
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
