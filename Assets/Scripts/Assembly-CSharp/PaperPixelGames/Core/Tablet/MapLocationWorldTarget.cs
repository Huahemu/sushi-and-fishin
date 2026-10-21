using UnityEngine;

namespace PaperPixelGames.Core.Tablet
{
	public sealed class MapLocationWorldTarget : MonoBehaviour
	{
		private Vector2 _worldXZ;

		private Transform _player;

		public void Initialize(Vector2 worldXZ, Transform player)
		{
			_worldXZ = worldXZ;
			_player = player;
			ApplyPosition();
		}

		private void Update()
		{
			ApplyPosition();
		}

		private void ApplyPosition()
		{
			if (!(_player == null))
			{
				base.transform.position = new Vector3(_worldXZ.x, _player.position.y, _worldXZ.y);
			}
		}
	}
}
