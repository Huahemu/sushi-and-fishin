using System;
using System.Collections.Generic;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.NetworkDevelopment
{
	public static class NetworkUtility
	{
		public static bool CanWriteServerState(this NetworkBehaviour behaviour)
		{
			if (behaviour == null || !behaviour.IsServer || !behaviour.IsSpawned)
			{
				return false;
			}
			NetworkManager networkManager = behaviour.NetworkManager;
			if (networkManager != null && networkManager.IsListening)
			{
				return !networkManager.ShutdownInProgress;
			}
			return false;
		}

		public static bool TryGetComponent<T>(this NetworkObjectReference reference, out T component, NetworkManager manager = null) where T : Component
		{
			component = null;
			manager = ((manager != null) ? manager : NetworkManager.Singleton);
			if (manager == null || !manager.IsListening || manager.ShutdownInProgress || manager.SpawnManager == null || manager.SpawnManager.SpawnedObjects == null || !reference.TryGet(out var networkObject, manager) || networkObject == null)
			{
				return false;
			}
			return networkObject.TryGetComponent<T>(out component);
		}

		public static NetworkObjectReference ToReference(this NetworkObject obj)
		{
			if (!(obj != null))
			{
				return default;
			}
			return new NetworkObjectReference(obj);
		}

		public static NetworkBehaviour GetNetworkBehaviourAtIndex(this NetworkObject networkObject, ushort index)
		{
			List<NetworkBehaviour> list = new List<NetworkBehaviour>();
			networkObject.GetComponentsInChildren(includeInactive: true, list);
			if (index >= list.Count)
			{
				return null;
			}
			return list[index];
		}

		public static bool TryInstantiateAndSpawn(this NetworkObject prefab, out NetworkObject instance, Vector3 position, Quaternion rotation, Transform parent = null, bool destroyWithScene = false, ulong? ownerClientId = null)
		{
			instance = null;
			if (prefab == null)
			{
				UtilityMethods.Log("[NetUtility] Tried to instantiate+spawn but prefab is NULL!");
				return false;
			}
			if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer)
			{
				UtilityMethods.Log("[NetUtility] Tried to instantiate+spawn " + prefab.name + ", but not running as Server!");
				return false;
			}
			try
			{
				GameObject gameObject = UnityEngine.Object.Instantiate(prefab.gameObject, position, rotation, parent);
				if (!gameObject.TryGetComponent<NetworkObject>(out instance))
				{
					UtilityMethods.Log("[NetUtility] Instantiated " + prefab.name + " but it has no NetworkObject on the root!");
					UnityEngine.Object.Destroy(gameObject);
					return false;
				}
				if (ownerClientId.HasValue)
				{
					instance.SpawnWithOwnership(ownerClientId.Value, destroyWithScene);
				}
				else
				{
					instance.Spawn(destroyWithScene);
				}
				UtilityMethods.Log("[NetUtility] Instantiated and spawned " + instance.name + " successfully.");
				return true;
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[NetUtility] Failed to instantiate+spawn " + prefab.name + ". Exception: " + ex.Message);
				if (instance != null)
				{
					UnityEngine.Object.Destroy(instance.gameObject);
				}
				return false;
			}
		}
	}
}
