using System.Collections.Generic;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Utility;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace PaperPixelGames.Core.Restaurant.Dirt
{
	public class HoldableDirtManager : NetworkBehaviour, ISaveable<RestaurantDirtData>
	{
		[Header("NavMesh Surface Roots")]
		[SerializeField]
		private Transform _floorNavMeshRoot;

		[SerializeField]
		private Transform _carpetNavMeshRoot;

		[Header("Dirt Prefab")]
		[SerializeField]
		private List<HoldableDirt> _holdableDirtPrefabs;

		[Header("Initial Spawn")]
		[SerializeField]
		[Min(0f)]
		private int _defaultInitialDirtCount = 10;

		[Header("Spawn Settings")]
		[SerializeField]
		[Min(0.1f)]
		private float _sampleMaxDistance = 2f;

		[SerializeField]
		[Min(1f)]
		private int _maxAttempts = 40;

		[SerializeField]
		[Min(0f)]
		private float _floorSurfaceOffset = 0.02f;

		[SerializeField]
		[Min(0f)]
		private float _carpetSurfaceOffset = 0.02f;

		private readonly List<NavMeshSurface> _floorSurfaces = new List<NavMeshSurface>();

		private readonly List<NavMeshSurface> _carpetSurfaces = new List<NavMeshSurface>();

		private readonly List<Transform> _spawnedHoldableDirt = new List<Transform>();

		private bool _hasInitialSpawnRequest;

		private int _pendingInitialSpawnCount;

		private bool _initialSpawnProcessed;

		public bool LoadedFromSave { get; private set; }

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			UtilityMethods.Log($"[HoldableDirtManager] OnNetworkSpawn | IsServer:{base.IsServer} | " + $"HasInitialRequest:{_hasInitialSpawnRequest} | PendingCount:{_pendingInitialSpawnCount}");
			TryProcessInitialSpawnRequest();
		}

		public override void OnNetworkDespawn()
		{
			base.OnNetworkDespawn();
		}

		public void SpawnBatchDirtServer(int count)
		{
			UtilityMethods.Log($"[HoldableDirtManager] SpawnBatchDirtServer called | IsServer:{base.IsServer} | Count:{count}");
			if (!base.IsServer)
			{
				UtilityMethods.Log("[HoldableDirtManager] SpawnBatchDirtServer aborted: not server.", LogType.Warning);
				return;
			}
			if (count <= 0)
			{
				UtilityMethods.Log("[HoldableDirtManager] SpawnBatchDirtServer aborted: count <= 0.", LogType.Warning);
				return;
			}
			RebuildSurfaceCache();
			for (int i = 0; i < count; i++)
			{
				ServerTrySpawnDirt();
			}
		}

		[Rpc(SendTo.Server)]
		public void RequestSpawnBatchDirtRpc(int count)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3017517927u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, count);
				__endSendRpc(ref bufferWriter, 3017517927u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				SpawnBatchDirtServer(count);
			}
		}

		private void TryProcessInitialSpawnRequest()
		{
			UtilityMethods.Log("[HoldableDirtManager] TryProcessInitialSpawnRequest | " + $"IsSpawned:{base.IsSpawned} | IsServer:{base.IsServer} | " + $"HasRequest:{_hasInitialSpawnRequest} | Processed:{_initialSpawnProcessed} | " + $"PendingCount:{_pendingInitialSpawnCount}");
			if (base.IsSpawned && base.IsServer && _hasInitialSpawnRequest && !_initialSpawnProcessed)
			{
				_initialSpawnProcessed = true;
				_hasInitialSpawnRequest = false;
				SpawnBatchDirtServer(_pendingInitialSpawnCount);
			}
		}

		private void RebuildSurfaceCache()
		{
			_floorSurfaces.Clear();
			_carpetSurfaces.Clear();
			CollectSurfacesInto(_floorNavMeshRoot, _floorSurfaces);
			CollectSurfacesInto(_carpetNavMeshRoot, _carpetSurfaces);
			UtilityMethods.Log($"[HoldableDirtManager] Surface cache rebuilt | Floor:{_floorSurfaces.Count} | Carpet:{_carpetSurfaces.Count}");
		}

		private static void CollectSurfacesInto(Transform root, List<NavMeshSurface> destination)
		{
			if (root == null)
			{
				return;
			}
			NavMeshSurface[] componentsInChildren = root.GetComponentsInChildren<NavMeshSurface>(includeInactive: true);
			if (componentsInChildren == null || componentsInChildren.Length == 0)
			{
				return;
			}
			HashSet<NavMeshSurface> hashSet = new HashSet<NavMeshSurface>();
			foreach (NavMeshSurface navMeshSurface in componentsInChildren)
			{
				if (!(navMeshSurface == null) && hashSet.Add(navMeshSurface))
				{
					destination.Add(navMeshSurface);
				}
			}
		}

		private void ServerTrySpawnDirt()
		{
			if (!base.IsServer)
			{
				return;
			}
			if (_holdableDirtPrefabs == null || _holdableDirtPrefabs.Count == 0)
			{
				UtilityMethods.Log("[HoldableDirtManager] Prefab list empty. Spawn aborted.", LogType.Warning);
				return;
			}
			HoldableDirt holdableDirt = null;
			for (int i = 0; i < 10; i++)
			{
				HoldableDirt holdableDirt2 = _holdableDirtPrefabs[Random.Range(0, _holdableDirtPrefabs.Count)];
				if (holdableDirt2 != null)
				{
					holdableDirt = holdableDirt2;
					break;
				}
			}
			if (holdableDirt == null)
			{
				UtilityMethods.Log("[HoldableDirtManager] All prefabs are null. Spawn aborted.", LogType.Warning);
				return;
			}
			NavMeshSurface navMeshSurface = PickRandomValidSurface(out var isCarpet);
			if (navMeshSurface == null)
			{
				UtilityMethods.Log("[HoldableDirtManager] No valid NavMeshSurface found. Spawn aborted.", LogType.Warning);
				return;
			}
			float num = (isCarpet ? _carpetSurfaceOffset : _floorSurfaceOffset);
			if (!TryGetRandomPointOnSurface(navMeshSurface, out var point, out var upAxis))
			{
				UtilityMethods.Log("[HoldableDirtManager] Failed to find random navmesh point. Spawn aborted.", LogType.Warning);
				return;
			}
			Quaternion quaternion = Quaternion.FromToRotation(Vector3.up, upAxis);
			quaternion = Quaternion.AngleAxis(Random.Range(0f, 360f), upAxis) * quaternion;
			HoldableDirt holdableDirt3 = Object.Instantiate(holdableDirt, point + upAxis * num, quaternion);
			NetworkObject networkObject = holdableDirt3.NetworkObject;
			if (networkObject == null)
			{
				UtilityMethods.Log("[HoldableDirtManager] Dirt prefab missing NetworkObject. Spawn aborted.", LogType.Error);
				Object.Destroy(holdableDirt3.gameObject);
			}
			else
			{
				networkObject.Spawn(destroyWithScene: true);
				_spawnedHoldableDirt.Add(networkObject.transform);
				UtilityMethods.Log($"[HoldableDirtManager] Spawn successful | Position:{networkObject.transform.position} | " + $"TotalTracked:{GetTrackedDirtCount()}");
			}
		}

		private NavMeshSurface PickRandomValidSurface(out bool isCarpet)
		{
			isCarpet = false;
			int num = CountValidSurfaces(_floorSurfaces);
			int num2 = CountValidSurfaces(_carpetSurfaces);
			int num3 = num + num2;
			if (num3 <= 0)
			{
				return null;
			}
			if (Random.Range(0, num3) < num)
			{
				isCarpet = false;
				return PickRandomValidFromList(_floorSurfaces);
			}
			isCarpet = true;
			return PickRandomValidFromList(_carpetSurfaces);
		}

		private static int CountValidSurfaces(List<NavMeshSurface> surfaces)
		{
			if (surfaces == null || surfaces.Count == 0)
			{
				return 0;
			}
			int num = 0;
			for (int i = 0; i < surfaces.Count; i++)
			{
				NavMeshSurface navMeshSurface = surfaces[i];
				if (navMeshSurface != null && navMeshSurface.enabled && navMeshSurface.gameObject.activeInHierarchy)
				{
					num++;
				}
			}
			return num;
		}

		private static NavMeshSurface PickRandomValidFromList(List<NavMeshSurface> surfaces)
		{
			if (surfaces == null || surfaces.Count == 0)
			{
				return null;
			}
			for (int i = 0; i < 10; i++)
			{
				NavMeshSurface navMeshSurface = surfaces[Random.Range(0, surfaces.Count)];
				if (navMeshSurface != null && navMeshSurface.enabled && navMeshSurface.gameObject.activeInHierarchy)
				{
					return navMeshSurface;
				}
			}
			for (int j = 0; j < surfaces.Count; j++)
			{
				NavMeshSurface navMeshSurface2 = surfaces[j];
				if (navMeshSurface2 != null && navMeshSurface2.enabled && navMeshSurface2.gameObject.activeInHierarchy)
				{
					return navMeshSurface2;
				}
			}
			return null;
		}

		private bool TryGetRandomPointOnSurface(NavMeshSurface surface, out Vector3 point, out Vector3 upAxis)
		{
			point = default;
			upAxis = surface.transform.up;
			if (!TryGetSurfaceWorldBounds(surface, out var worldBounds))
			{
				return false;
			}
			int areaMask = -1;
			for (int i = 0; i < _maxAttempts; i++)
			{
				if (NavMesh.SamplePosition(new Vector3(Random.Range(worldBounds.min.x, worldBounds.max.x), Random.Range(worldBounds.min.y, worldBounds.max.y), Random.Range(worldBounds.min.z, worldBounds.max.z)), out var hit, _sampleMaxDistance, areaMask) && worldBounds.Contains(hit.position))
				{
					point = hit.position;
					return true;
				}
			}
			return false;
		}

		private bool TryGetSurfaceWorldBounds(NavMeshSurface surface, out Bounds worldBounds)
		{
			worldBounds = default;
			if (surface == null)
			{
				return false;
			}
			if (surface.collectObjects == CollectObjects.Volume)
			{
				Transform transform = surface.transform;
				Vector3 center = surface.center;
				Vector3 vector = surface.size * 0.5f;
				Vector3[] array = new Vector3[8]
				{
					new Vector3(center.x - vector.x, center.y - vector.y, center.z - vector.z),
					new Vector3(center.x - vector.x, center.y - vector.y, center.z + vector.z),
					new Vector3(center.x - vector.x, center.y + vector.y, center.z - vector.z),
					new Vector3(center.x - vector.x, center.y + vector.y, center.z + vector.z),
					new Vector3(center.x + vector.x, center.y - vector.y, center.z - vector.z),
					new Vector3(center.x + vector.x, center.y - vector.y, center.z + vector.z),
					new Vector3(center.x + vector.x, center.y + vector.y, center.z - vector.z),
					new Vector3(center.x + vector.x, center.y + vector.y, center.z + vector.z)
				};
				Bounds bounds = new Bounds(transform.TransformPoint(array[0]), Vector3.zero);
				for (int i = 1; i < array.Length; i++)
				{
					bounds.Encapsulate(transform.TransformPoint(array[i]));
				}
				worldBounds = bounds;
				return true;
			}
			Renderer[] componentsInChildren = surface.GetComponentsInChildren<Renderer>(includeInactive: true);
			if (componentsInChildren == null || componentsInChildren.Length == 0)
			{
				return false;
			}
			Bounds bounds2 = componentsInChildren[0].bounds;
			for (int j = 1; j < componentsInChildren.Length; j++)
			{
				bounds2.Encapsulate(componentsInChildren[j].bounds);
			}
			worldBounds = bounds2;
			return true;
		}

		private int GetTrackedDirtCount()
		{
			CleanupDestroyedReferences();
			int num = 0;
			for (int i = 0; i < _spawnedHoldableDirt.Count; i++)
			{
				if (_spawnedHoldableDirt[i] != null)
				{
					num++;
				}
			}
			return num;
		}

		private void CleanupDestroyedReferences()
		{
			for (int num = _spawnedHoldableDirt.Count - 1; num >= 0; num--)
			{
				if (_spawnedHoldableDirt[num] == null)
				{
					_spawnedHoldableDirt.RemoveAt(num);
				}
			}
		}

		public RestaurantDirtData Save()
		{
			int trackedDirtCount = GetTrackedDirtCount();
			RestaurantDirtData result = new RestaurantDirtData
			{
				LoadedFromSave = true,
				Amount = (byte)trackedDirtCount
			};
			UtilityMethods.Log($"[HoldableDirtManager] Save | DirtCount:{trackedDirtCount}");
			return result;
		}

		public void Load(RestaurantDirtData dirtData)
		{
			LoadedFromSave = dirtData.LoadedFromSave;
			_pendingInitialSpawnCount = (LoadedFromSave ? dirtData.Amount : _defaultInitialDirtCount);
			_hasInitialSpawnRequest = true;
			_initialSpawnProcessed = false;
			UtilityMethods.Log($"[HoldableDirtManager] Load | LoadedFromSave:{LoadedFromSave} | " + $"SavedAmount:{dirtData.Amount} | PendingInitialSpawn:{_pendingInitialSpawnCount} | " + $"IsSpawned:{base.IsSpawned} | IsServer:{base.IsServer}");
			TryProcessInitialSpawnRequest();
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3017517927u, __rpc_handler_3017517927, "RequestSpawnBatchDirtRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3017517927(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				((HoldableDirtManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HoldableDirtManager)target).RequestSpawnBatchDirtRpc(value);
				((HoldableDirtManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "HoldableDirtManager";
		}
	}
}
