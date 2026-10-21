using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Surf
{
	public sealed class SurfboardBootstrap : MonoBehaviour
	{
		public SurfboardWorld worldPrefab;

		private SurfboardWorld _world;

		private void Update()
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if (!(_world != null) && !(worldPrefab == null) && !(singleton == null) && !singleton.ShutdownInProgress && singleton.IsListening && singleton.IsServer)
			{
				_world = Object.Instantiate(worldPrefab);
				_world.NetworkObject.Spawn();
			}
		}

		private void OnDestroy()
		{
			if (_world != null && _world.IsSpawned && _world.IsServer)
			{
				_world.NetworkObject.Despawn();
			}
		}
	}
}
