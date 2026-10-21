using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;

namespace PaperPixelGames.Framework.Network
{
	public sealed class NetworkObjectPool : MonoBehaviour
	{
		[Serializable]
		private struct PoolConfigObject
		{
			public GameObject Prefab;

			public int PrewarmCount;
		}

		private sealed class PooledPrefabInstanceHandler : INetworkPrefabInstanceHandler
		{
			private readonly GameObject _prefab;

			private readonly NetworkObjectPool _pool;

			public PooledPrefabInstanceHandler(GameObject prefab, NetworkObjectPool pool)
			{
				_prefab = prefab;
				_pool = pool;
			}

			NetworkObject INetworkPrefabInstanceHandler.Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
			{
				return _pool.GetNetworkObject(_prefab, position, rotation);
			}

			void INetworkPrefabInstanceHandler.Destroy(NetworkObject networkObject)
			{
				_pool.ReturnNetworkObject(networkObject, _prefab);
			}
		}

		[SerializeField]
		private List<PoolConfigObject> _pooledPrefabs = new List<PoolConfigObject>();

		[Header("Pool Limits")]
		[SerializeField]
		[Min(1f)]
		private int _defaultMaxSize = 64;

		private readonly HashSet<GameObject> _prefabs = new HashSet<GameObject>();

		private readonly Dictionary<GameObject, ObjectPool<NetworkObject>> _pools = new Dictionary<GameObject, ObjectPool<NetworkObject>>();

		private bool _initialized;

		public static NetworkObjectPool Singleton { get; private set; }

		private void Awake()
		{
			if (Singleton != null && Singleton != this)
			{
				UnityEngine.Object.Destroy(base.gameObject);
				return;
			}
			Singleton = this;
			UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
			TryInitialize();
		}

		private void OnDestroy()
		{
			if (Singleton == this)
			{
				Singleton = null;
			}
			Cleanup();
		}

		private void TryInitialize()
		{
			if (!_initialized)
			{
				NetworkManager singleton = NetworkManager.Singleton;
				if (!(singleton == null))
				{
					InitializeInternal(singleton);
					_initialized = true;
				}
			}
		}

		private void InitializeInternal(NetworkManager nm)
		{
			_prefabs.Clear();
			_pools.Clear();
			for (int i = 0; i < _pooledPrefabs.Count; i++)
			{
				PoolConfigObject poolConfigObject = _pooledPrefabs[i];
				if (!(poolConfigObject.Prefab == null) && !_prefabs.Contains(poolConfigObject.Prefab))
				{
					RegisterPrefabInternal(nm, poolConfigObject.Prefab, poolConfigObject.PrewarmCount);
				}
			}
		}

		public NetworkObject GetNetworkObject(GameObject prefab, Vector3 position, Quaternion rotation)
		{
			if (prefab == null)
			{
				return null;
			}
			if (!_pools.TryGetValue(prefab, out var value))
			{
				Debug.LogWarning("[NetworkObjectPool] Prefab not registered: " + prefab.name);
				return null;
			}
			NetworkObject networkObject = value.Get();
			networkObject.transform.SetPositionAndRotation(position, rotation);
			return networkObject;
		}

		public void ReturnNetworkObject(NetworkObject networkObject, GameObject prefab)
		{
			if (!(networkObject == null) && !(prefab == null) && _pools.TryGetValue(prefab, out var value))
			{
				value.Release(networkObject);
			}
		}

		private void RegisterPrefabInternal(NetworkManager nm, GameObject prefab, int prewarmCount)
		{
			_prefabs.Add(prefab);
			int maxSize = Mathf.Max(_defaultMaxSize, prewarmCount);
			_pools[prefab] = new ObjectPool<NetworkObject>(CreateFunc, OnGet, OnRelease, OnDestroyPooled, collectionCheck: true, prewarmCount, maxSize);
			if (prewarmCount > 0)
			{
				List<NetworkObject> list = new List<NetworkObject>(prewarmCount);
				for (int i = 0; i < prewarmCount; i++)
				{
					list.Add(_pools[prefab].Get());
				}
				for (int j = 0; j < list.Count; j++)
				{
					_pools[prefab].Release(list[j]);
				}
			}
			nm.PrefabHandler.AddHandler(prefab, new PooledPrefabInstanceHandler(prefab, this));
			NetworkObject CreateFunc()
			{
				GameObject obj = UnityEngine.Object.Instantiate(prefab);
				obj.SetActive(value: false);
				return obj.GetComponent<NetworkObject>();
			}
			static void OnDestroyPooled(NetworkObject no)
			{
				if (no != null)
				{
					UnityEngine.Object.Destroy(no.gameObject);
				}
			}
			static void OnGet(NetworkObject no)
			{
				no.gameObject.SetActive(value: true);
			}
			static void OnRelease(NetworkObject no)
			{
				no.transform.SetParent(null);
				no.gameObject.SetActive(value: false);
			}
		}

		private void Cleanup()
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if (singleton != null)
			{
				foreach (GameObject prefab in _prefabs)
				{
					singleton.PrefabHandler.RemoveHandler(prefab);
				}
			}
			foreach (KeyValuePair<GameObject, ObjectPool<NetworkObject>> pool in _pools)
			{
				pool.Value.Clear();
			}
			_pools.Clear();
			_prefabs.Clear();
			_initialized = false;
		}
	}
}
