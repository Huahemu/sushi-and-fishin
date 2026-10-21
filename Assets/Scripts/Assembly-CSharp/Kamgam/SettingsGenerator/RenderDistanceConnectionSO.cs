using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	[CreateAssetMenu(fileName = "RenderDistanceConnection", menuName = "SettingsGenerator/ConnectionCustom/RenderDistanceConnection", order = 4)]
	public class RenderDistanceConnectionSO : FloatConnectionSO
	{
		public Vector2 InputRange = new Vector2(50f, 1000f);

		public float DefaultValue = 300f;

		protected RenderDistanceConnection _connection;

		public override IConnection<float> GetConnection()
		{
			if (_connection == null)
			{
				Create();
			}
			return _connection;
		}

		public void Create()
		{
			_connection = new RenderDistanceConnection(InputRange, DefaultValue);
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
