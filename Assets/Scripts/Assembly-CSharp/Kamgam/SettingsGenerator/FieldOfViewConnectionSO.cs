using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	[CreateAssetMenu(fileName = "FieldOfViewConnection", menuName = "SettingsGenerator/ConnectionCustom/Field Of View", order = 11)]
	public sealed class FieldOfViewConnectionSO : FloatConnectionSO
	{
		public Vector2 InputRange = new Vector2(50f, 90f);

		public float DefaultValue = 60f;

		private FieldOfViewConnection _connection;

		public override IConnection<float> GetConnection()
		{
			if (_connection == null)
			{
				_connection = new FieldOfViewConnection(InputRange, DefaultValue);
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
