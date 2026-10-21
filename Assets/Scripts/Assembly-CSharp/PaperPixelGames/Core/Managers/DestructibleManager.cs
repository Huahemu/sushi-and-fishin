using System;
using System.Collections.Generic;
using IndieKit;
using PaperPixelGames.Core.ScrapSystem;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Managers
{
	public class DestructibleManager : Singleton<DestructibleManager>
	{
		[Serializable]
		private class DestructiblePrefab
		{
			public int Type;

			public DestructibleObject Prefab;
		}

		private const byte InvalidId = 0;

		private const string SpawnPointPrefix = "SP_Destructible_";

		[SerializeField]
		private List<DestructiblePrefab> _prefabList = new List<DestructiblePrefab>();

		[SerializeField]
		private List<DestructibleObject> _sceneObjects = new List<DestructibleObject>();

		[SerializeField]
		private List<int> _spawnIds = new List<int>();

		[SerializeField]
		private List<Transform> _spawnPoints = new List<Transform>();

		[SerializeField]
		private List<int> _spawnPrefabTypes = new List<int>();

		private readonly HashSet<int> _activeIds = new HashSet<int>(256);

		private readonly Dictionary<int, DestructibleObject> _prefabByType = new Dictionary<int, DestructibleObject>();

		private readonly Dictionary<int, DestructibleObject> _objById = new Dictionary<int, DestructibleObject>(256);

		public override SingletonScope Scope => SingletonScope.Scene;

		public static bool HasInstance => Singleton<DestructibleManager>.Instance != null;

		protected override void Awake()
		{
			base.Awake();
			if (_sceneObjects == null)
			{
				_sceneObjects = new List<DestructibleObject>();
			}
			if (_spawnIds == null)
			{
				_spawnIds = new List<int>();
			}
			if (_spawnPoints == null)
			{
				_spawnPoints = new List<Transform>();
			}
			if (_spawnPrefabTypes == null)
			{
				_spawnPrefabTypes = new List<int>();
			}
			CleanupNullsInSceneList();
			RebuildPrefabCache();
			CleanupSpawnLists();
			RebuildActiveIdsFromSpawnPoints();
			CleanupAndFixDuplicatesById();
		}

		public bool Register(DestructibleObject obj)
		{
			if (obj == null)
			{
				return false;
			}
			int id = obj.Id;
			if (id <= 0 || id > 255)
			{
				UtilityMethods.Log($"[DestructibleManager] Registration rejected. Object={obj.name} Id={id}.", LogType.Warning);
				return false;
			}
			if (_objById.TryGetValue(id, out var value) && value != null)
			{
				if (value == obj)
				{
					return true;
				}
				UtilityMethods.Log($"[DestructibleManager] Registration rejected. Duplicate Id={id} " + "Existing=" + value.name + " Incoming=" + obj.name + ".", LogType.Error);
				return false;
			}
			if (_sceneObjects == null)
			{
				_sceneObjects = new List<DestructibleObject>();
			}
			if (!_sceneObjects.Contains(obj))
			{
				_sceneObjects.Add(obj);
			}
			_objById[id] = obj;
			return true;
		}

		public void Unregister(DestructibleObject obj)
		{
			if (!(obj == null))
			{
				_sceneObjects?.Remove(obj);
				if (_objById.TryGetValue(obj.Id, out var value) && value == obj)
				{
					_objById.Remove(obj.Id);
				}
			}
		}

		public void RespawnObjects()
		{
			if (_spawnPoints == null || _spawnPoints.Count == 0)
			{
				UtilityMethods.Log("[DestructibleManager] No spawn points assigned!", LogType.Error);
				return;
			}
			if (_prefabList == null || _prefabList.Count == 0)
			{
				UtilityMethods.Log("[DestructibleManager] Prefab list is null or empty!", LogType.Error);
				return;
			}
			CleanupSpawnLists();
			if (!SpawnListsAreValid())
			{
				UtilityMethods.Log("[DestructibleManager] Spawn lists are invalid (length mismatch). Fix via RebuildSceneObjectList.", LogType.Error);
				return;
			}
			if (_sceneObjects == null)
			{
				_sceneObjects = new List<DestructibleObject>();
			}
			CleanupNullsInSceneList();
			RebuildPrefabCache();
			RebuildActiveIdsFromSpawnPoints();
			CleanupAndFixDuplicatesById();
			RebuildObjectLookup();
			for (int i = 0; i < _spawnPoints.Count; i++)
			{
				Transform transform = _spawnPoints[i];
				if (transform == null)
				{
					continue;
				}
				byte b = ToByteId(_spawnIds[i]);
				if (b == 0)
				{
					continue;
				}
				int num = _spawnPrefabTypes[i];
				if (_objById.TryGetValue(b, out var value) && value != null)
				{
					if (value.PrefabType == num)
					{
						continue;
					}
					Unregister(value);
					UnityEngine.Object.Destroy(value.gameObject);
				}
				if (!_prefabByType.TryGetValue(num, out var value2) || value2 == null)
				{
					UtilityMethods.Log($"[DestructibleManager] Missing prefab for type={num}. Cannot respawn id={b}", LogType.Error);
					continue;
				}
				DestructibleObject destructibleObject = UnityEngine.Object.Instantiate(value2, transform.position, transform.rotation, null);
				destructibleObject.SetId(b);
				destructibleObject.SetPrefabType(num);
				_sceneObjects.Add(destructibleObject);
				_objById[b] = destructibleObject;
			}
		}

		public void ApplyDamageById(byte id, Vector3 hitPoint)
		{
			TryApplyDamageById(id, hitPoint);
		}

		public bool TryApplyDamageById(byte id, Vector3 hitPoint)
		{
			if (id == 0)
			{
				UtilityMethods.Log("[DestructibleManager] ApplyDamageById called with InvalidId=0.", LogType.Warning);
				return false;
			}
			DestructibleObject destructibleObject = FindById(id);
			if (destructibleObject == null)
			{
				UtilityMethods.Log($"[DestructibleManager] Object not found! id={id}", LogType.Warning);
				return false;
			}
			destructibleObject.ApplyDamage(1f, hitPoint);
			RequestLootDrop(hitPoint);
			return true;
		}

		public void RequestLootDrop(Vector3 hitPoint)
		{
			if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost || !NetworkManager.Singleton.IsServer)
			{
				return;
			}
			if (!CheckChance())
			{
				UtilityMethods.Log("[DestructibleManager] No loot unlucky!");
				return;
			}
			ServiceLocator.ForSceneOf(this).Get<WorldPickupHandler>(out var pickupHandler);
			if (pickupHandler == null)
			{
				UtilityMethods.Log("[Scrap] <color=red>Spawn failed</color> ScrapHandler not found!");
				return;
			}
			if (UnityEngine.Random.value > 0.5f)
			{
				Singleton<DelayHelper>.Instance.DelayActionUntilEndOfFrame(() =>
				{
					pickupHandler.ServerSpawnOneScrapAtPosition(hitPoint);
				});
			}
			else
			{
				Singleton<DelayHelper>.Instance.DelayActionUntilEndOfFrame(() =>
				{
					pickupHandler.ServerSpawnOneCoinAtPosition(hitPoint);
				});
			}
			UtilityMethods.Log("[DestructibleManager] LootDropRequest");
		}

		private static byte ToByteId(int id)
		{
			return (byte)Mathf.Clamp(id, 0, 255);
		}

		private void RebuildPrefabCache()
		{
			_prefabByType.Clear();
			if (_prefabList == null)
			{
				return;
			}
			for (int i = 0; i < _prefabList.Count; i++)
			{
				DestructiblePrefab destructiblePrefab = _prefabList[i];
				if (destructiblePrefab != null && !(destructiblePrefab.Prefab == null))
				{
					_prefabByType[destructiblePrefab.Type] = destructiblePrefab.Prefab;
				}
			}
		}

		private void CleanupSpawnLists()
		{
			if (_spawnIds == null)
			{
				_spawnIds = new List<int>();
			}
			if (_spawnPoints == null)
			{
				_spawnPoints = new List<Transform>();
			}
			if (_spawnPrefabTypes == null)
			{
				_spawnPrefabTypes = new List<int>();
			}
			for (int num = _spawnPoints.Count - 1; num >= 0; num--)
			{
				if (!(_spawnPoints[num] != null))
				{
					if (num < _spawnIds.Count)
					{
						_spawnIds.RemoveAt(num);
					}
					if (num < _spawnPoints.Count)
					{
						_spawnPoints.RemoveAt(num);
					}
					if (num < _spawnPrefabTypes.Count)
					{
						_spawnPrefabTypes.RemoveAt(num);
					}
				}
			}
		}

		private bool SpawnListsAreValid()
		{
			if (_spawnIds.Count == _spawnPoints.Count)
			{
				return _spawnPoints.Count == _spawnPrefabTypes.Count;
			}
			return false;
		}

		private void RebuildActiveIdsFromSpawnPoints()
		{
			_activeIds.Clear();
			if (!SpawnListsAreValid())
			{
				return;
			}
			for (int i = 0; i < _spawnIds.Count; i++)
			{
				byte b = ToByteId(_spawnIds[i]);
				if (b != 0)
				{
					_activeIds.Add(b);
				}
			}
		}

		private void CleanupAndFixDuplicatesById()
		{
			_objById.Clear();
			if (_sceneObjects == null)
			{
				return;
			}
			for (int num = _sceneObjects.Count - 1; num >= 0; num--)
			{
				DestructibleObject destructibleObject = _sceneObjects[num];
				if (!(destructibleObject == null))
				{
					int id = destructibleObject.Id;
					DestructibleObject value;
					if (id == 0)
					{
						Unregister(destructibleObject);
						UnityEngine.Object.Destroy(destructibleObject.gameObject);
					}
					else if (!_activeIds.Contains(id))
					{
						Unregister(destructibleObject);
						UnityEngine.Object.Destroy(destructibleObject.gameObject);
					}
					else if (_objById.TryGetValue(id, out value) && value != null)
					{
						Unregister(destructibleObject);
						UnityEngine.Object.Destroy(destructibleObject.gameObject);
					}
					else
					{
						_objById[id] = destructibleObject;
					}
				}
			}
		}

		private void RebuildObjectLookup()
		{
			_objById.Clear();
			if (_sceneObjects == null)
			{
				return;
			}
			for (int i = 0; i < _sceneObjects.Count; i++)
			{
				DestructibleObject destructibleObject = _sceneObjects[i];
				if (!(destructibleObject == null))
				{
					_objById[destructibleObject.Id] = destructibleObject;
				}
			}
		}

		private bool CheckChance()
		{
			return UnityEngine.Random.value <= 0.75f;
		}

		private DestructibleObject FindById(byte id)
		{
			if (!_objById.TryGetValue(id, out var value))
			{
				return null;
			}
			return value;
		}

		private void CleanupNullsInSceneList()
		{
			if (_sceneObjects == null)
			{
				return;
			}
			for (int num = _sceneObjects.Count - 1; num >= 0; num--)
			{
				if (_sceneObjects[num] == null)
				{
					_sceneObjects.RemoveAt(num);
				}
			}
		}
	}
}
