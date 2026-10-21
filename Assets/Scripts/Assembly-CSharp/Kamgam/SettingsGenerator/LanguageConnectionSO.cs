using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	[CreateAssetMenu(fileName = "LanguageConnection", menuName = "SettingsGenerator/ConnectionCustom/LanguageConnection", order = 4)]
	public class LanguageConnectionSO : OptionConnectionSO
	{
		protected LanguageConnection _connection;

		public override IConnectionWithOptions<string> GetConnection()
		{
			if (_connection == null)
			{
				Create();
			}
			return _connection;
		}

		public void Create()
		{
			_connection = new LanguageConnection();
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
