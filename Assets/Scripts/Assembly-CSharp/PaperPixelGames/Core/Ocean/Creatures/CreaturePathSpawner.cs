using System.Collections;
using System.Collections.Generic;
using Nav3D.API;
using PaperPixelGames.Core.Data;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public sealed class CreaturePathSpawner : MonoBehaviour
	{
		[Header("Spawn")]
		[SerializeField]
		private Transform _spawnPoint;

		[SerializeField]
		[Min(0f)]
		private float _spawnInterval = 0.1f;

		[SerializeField]
		private bool _spawnOnStart = true;

		[SerializeField]
		private Transform _spawnedParent;

		[SerializeField]
		[Min(0f)]
		private float _spawnPointPatrolExclusionRadius = 0.75f;

		[Header("Creature Init Compatibility")]
		[SerializeField]
		private SeaLayer _compatibilityLayer;

		[Header("Creatures")]
		[SerializeField]
		private List<CreaturePathSpawnEntry> _creatures = new List<CreaturePathSpawnEntry>();

		[Header("Debug")]
		[SerializeField]
		private bool _log;

		private int _nextRuntimeId;

		private Coroutine _spawnRoutine;

		private readonly List<SeaCreature> _spawnedCreatures = new List<SeaCreature>();

		public bool IsSpawning => _spawnRoutine != null;

		public Transform SpawnPoint => _spawnPoint;

		private void Start()
		{
			if (_spawnOnStart)
			{
				SpawnAll();
			}
		}

		[ContextMenu("Spawn All")]
		public void SpawnAll(bool spawnBossReinforcements = false)
		{
			if (_spawnRoutine != null)
			{
				StopCoroutine(_spawnRoutine);
			}
			_spawnRoutine = StartCoroutine(SpawnAllCoroutine(spawnBossReinforcements));
		}

		public bool SpawnBossSharks()
		{
			return SpawnBossCreatureEntry(shark: true);
		}

		public bool SpawnBossPiranhas()
		{
			return SpawnBossCreatureEntry(shark: false);
		}

		public bool HasBossSharkEntry()
		{
			return FindBossCreatureEntry(shark: true) != null;
		}

		public bool HasBossPiranhaEntry()
		{
			return FindBossCreatureEntry(shark: false) != null;
		}

		public bool TryGetBossPatrolPoints(int pointCount, out List<Vector3> points)
		{
			points = null;
			if (pointCount <= 0)
			{
				return false;
			}
			CreaturePathSpawnEntry creaturePathSpawnEntry = FindBossCreatureEntry(shark: true) ?? FindBossCreatureEntry(shark: false);
			if (creaturePathSpawnEntry?.PatrolArea == null)
			{
				return false;
			}
			creaturePathSpawnEntry.PatrolArea.RefreshPointsCache();
			if (!creaturePathSpawnEntry.PatrolArea.HasAnyPoint())
			{
				return false;
			}
			points = creaturePathSpawnEntry.PatrolArea.GetRandomPath(pointCount, allowRepeats: true);
			if (points != null)
			{
				return points.Count > 0;
			}
			return false;
		}

		[ContextMenu("Despawn All")]
		public void DespawnAll()
		{
			if (!IsServerReady())
			{
				Log("Despawn skipped. NetworkManager is not running as server.", LogType.Warning);
				return;
			}
			if (_spawnPoint == null)
			{
				Log("Despawn fallback. Spawn point is not assigned.", LogType.Warning);
				DespawnAllImmediate();
				return;
			}
			if (_spawnRoutine != null)
			{
				StopCoroutine(_spawnRoutine);
				_spawnRoutine = null;
			}
			RemoveInvalidSpawnedCreatures();
			for (int num = _spawnedCreatures.Count - 1; num >= 0; num--)
			{
				SeaCreature seaCreature = _spawnedCreatures[num];
				if (seaCreature == null || seaCreature.NetworkObject == null)
				{
					_spawnedCreatures.RemoveAt(num);
				}
				else if (seaCreature is HostileSeaCreature { IsDead: not false })
				{
					DespawnCreatureImmediate(seaCreature);
					_spawnedCreatures.RemoveAt(num);
				}
				else if (!TryReturnCreatureToSpawnPoint(seaCreature))
				{
					DespawnCreatureImmediate(seaCreature);
					_spawnedCreatures.RemoveAt(num);
				}
			}
		}

		[ContextMenu("Despawn All Immediate")]
		public void DespawnAllImmediate()
		{
			if (IsServerReady())
			{
				if (_spawnRoutine != null)
				{
					StopCoroutine(_spawnRoutine);
					_spawnRoutine = null;
				}
				RemoveInvalidSpawnedCreatures();
				for (int num = _spawnedCreatures.Count - 1; num >= 0; num--)
				{
					SeaCreature creature = _spawnedCreatures[num];
					DespawnCreatureImmediate(creature);
					_spawnedCreatures.RemoveAt(num);
				}
			}
		}

		public bool ValidateConfiguration(out string error)
		{
			if (_spawnPoint == null)
			{
				error = "Spawn point is missing.";
				return false;
			}
			if (!IsNonNegativeFinite(_spawnInterval))
			{
				error = "Spawn interval must be zero or greater and finite.";
				return false;
			}
			if (!IsNonNegativeFinite(_spawnPointPatrolExclusionRadius))
			{
				error = "Spawn point patrol exclusion radius must be zero or greater and finite.";
				return false;
			}
			if (_compatibilityLayer == null)
			{
				error = "Compatibility SeaLayer is missing.";
				return false;
			}
			if (_creatures == null || _creatures.Count == 0)
			{
				error = "Creature spawn entries are empty.";
				return false;
			}
			bool flag = false;
			for (int i = 0; i < _creatures.Count; i++)
			{
				CreaturePathSpawnEntry creaturePathSpawnEntry = _creatures[i];
				int num = i + 1;
				if (creaturePathSpawnEntry != null && creaturePathSpawnEntry.SpawnCount > 0)
				{
					if (creaturePathSpawnEntry.CreatureData == null)
					{
						error = $"Creature entry {num} data is missing.";
						return false;
					}
					if (creaturePathSpawnEntry.CreatureData.Prefab == null)
					{
						error = $"Creature entry {num} prefab is missing.";
						return false;
					}
					if (!creaturePathSpawnEntry.CreatureData.Prefab.TryGetComponent<SeaCreature>(out var _))
					{
						error = $"Creature entry {num} prefab does not contain SeaCreature.";
						return false;
					}
					if (creaturePathSpawnEntry.PatrolArea == null)
					{
						error = $"Creature entry {num} patrol area is missing.";
						return false;
					}
					if (!IsPositiveFinite(creaturePathSpawnEntry.ReachDistance))
					{
						error = $"Creature entry {num} reach distance must be greater than zero and finite.";
						return false;
					}
					flag = true;
				}
			}
			if (!flag)
			{
				error = "At least one creature entry must have a positive spawn count.";
				return false;
			}
			error = null;
			return true;
		}

		private IEnumerator SpawnAllCoroutine(bool spawnBossReinforcements)
		{
			if (!IsServerReady())
			{
				Log("Spawn skipped. NetworkManager is not running as server.", LogType.Warning);
				_spawnRoutine = null;
				yield break;
			}
			if (_spawnPoint == null)
			{
				Log("Spawn skipped. Spawn point is not assigned.", LogType.Warning);
				_spawnRoutine = null;
				yield break;
			}
			WaitForSeconds wait = ((_spawnInterval > 0f) ? new WaitForSeconds(_spawnInterval) : null);
			for (int entryIndex = 0; entryIndex < _creatures.Count; entryIndex++)
			{
				CreaturePathSpawnEntry entry = _creatures[entryIndex];
				if (entry == null || entry.CreatureData == null || entry.SpawnCount <= 0)
				{
					continue;
				}
				if (entry.PatrolArea == null)
				{
					Log("Entry skipped. Patrol area is not assigned. Creature=" + entry.CreatureData.name, LogType.Warning);
					continue;
				}
				for (int spawnIndex = 0; spawnIndex < entry.SpawnCount; spawnIndex++)
				{
					SpawnCreature(entry, spawnBossReinforcements);
					if (wait != null)
					{
						yield return wait;
					}
					else
					{
						yield return null;
					}
				}
			}
			_spawnRoutine = null;
		}

		private bool SpawnBossCreatureEntry(bool shark)
		{
			CreaturePathSpawnEntry creaturePathSpawnEntry = FindBossCreatureEntry(shark);
			if (creaturePathSpawnEntry == null)
			{
				Log("Boss spawn skipped. No configured " + (shark ? "shark" : "piranha") + " entry was found.", LogType.Warning);
				return false;
			}
			if (_spawnRoutine != null)
			{
				StopCoroutine(_spawnRoutine);
			}
			_spawnRoutine = StartCoroutine(SpawnEntryCoroutine(creaturePathSpawnEntry, spawnBossReinforcements: true));
			return true;
		}

		private CreaturePathSpawnEntry FindBossCreatureEntry(bool shark)
		{
			if (_creatures == null)
			{
				return null;
			}
			for (int i = 0; i < _creatures.Count; i++)
			{
				CreaturePathSpawnEntry creaturePathSpawnEntry = _creatures[i];
				if (creaturePathSpawnEntry != null && !(creaturePathSpawnEntry.CreatureData == null) && !(creaturePathSpawnEntry.CreatureData.Prefab == null) && creaturePathSpawnEntry.SpawnCount > 0 && !(creaturePathSpawnEntry.PatrolArea == null) && (shark ? creaturePathSpawnEntry.CreatureData.Prefab.TryGetComponent<SharkCreature>(out var _) : creaturePathSpawnEntry.CreatureData.Prefab.TryGetComponent<PiranhaCreature>(out var _)))
				{
					return creaturePathSpawnEntry;
				}
			}
			return null;
		}

		private IEnumerator SpawnEntryCoroutine(CreaturePathSpawnEntry entry, bool spawnBossReinforcements)
		{
			if (!IsServerReady() || _spawnPoint == null)
			{
				_spawnRoutine = null;
				yield break;
			}
			WaitForSeconds wait = ((_spawnInterval > 0f) ? new WaitForSeconds(_spawnInterval) : null);
			for (int spawnIndex = 0; spawnIndex < entry.SpawnCount; spawnIndex++)
			{
				SpawnCreature(entry, spawnBossReinforcements);
				if (spawnIndex < entry.SpawnCount - 1)
				{
					if (wait != null)
					{
						yield return wait;
					}
					else
					{
						yield return null;
					}
				}
			}
			_spawnRoutine = null;
		}

		private SeaCreature SpawnCreature(CreaturePathSpawnEntry entry, bool spawnBossReinforcements)
		{
			CreatureSO creatureData = entry.CreatureData;
			if (creatureData == null)
			{
				return null;
			}
			NetworkObject prefab = creatureData.Prefab;
			if (prefab == null)
			{
				Log("Spawn failed. Prefab is null for " + creatureData.name + ".", LogType.Warning);
				return null;
			}
			if (_compatibilityLayer == null || entry.PatrolArea == null)
			{
				Log("Spawn failed. Compatibility SeaLayer or patrol area is missing for " + creatureData.name + ".", LogType.Warning);
				return null;
			}
			NetworkObject networkObject = Object.Instantiate(prefab, _spawnPoint.position, _spawnPoint.rotation, _spawnedParent);
			if (networkObject == null)
			{
				Log("Spawn failed. Instantiate returned null for " + creatureData.name + ".", LogType.Warning);
				return null;
			}
			if (!networkObject.TryGetComponent<SeaCreature>(out var component))
			{
				Log("Spawn failed. Prefab does not contain SeaCreature: " + prefab.name, LogType.Error);
				Object.Destroy(networkObject.gameObject);
				return null;
			}
			CreaturePathAssignment creaturePathAssignment = networkObject.GetComponent<CreaturePathAssignment>();
			if (creaturePathAssignment == null)
			{
				creaturePathAssignment = networkObject.gameObject.AddComponent<CreaturePathAssignment>();
			}
			creaturePathAssignment.Assign(entry.PatrolArea, entry.Loop, entry.StartFromClosest, entry.ReachDistance, _spawnPoint, _spawnPointPatrolExclusionRadius);
			int runtimeId = _nextRuntimeId++;
			if (!component.ServerInit(_compatibilityLayer, creatureData, runtimeId, entry.PatrolArea))
			{
				Log("Spawn failed. ServerInit returned false for " + creatureData.name + ".", LogType.Warning);
				if (networkObject.IsSpawned)
				{
					networkObject.Despawn();
				}
				else
				{
					Object.Destroy(networkObject.gameObject);
				}
				return null;
			}
			component.SetServerSimulationActive(active: true);
			if (spawnBossReinforcements && component is SharkCreature sharkCreature)
			{
				sharkCreature.ServerSetBossReinforcement(isBossReinforcement: true);
			}
			_spawnedCreatures.Add(component);
			Log("Spawned " + creatureData.name + " in area " + entry.PatrolArea.name + ".");
			return component;
		}

		private bool TryReturnCreatureToSpawnPoint(SeaCreature creature)
		{
			if (creature == null || creature.NetworkObject == null)
			{
				return false;
			}
			if (!creature.TryGetComponent<Nav3DAgent>(out var component) || component == null)
			{
				return false;
			}
			creature.SetServerSimulationActive(active: false);
			component.enabled = true;
			component.Stop();
			NetworkObject networkObject = creature.NetworkObject;
			string creatureName = creature.name;
			component.MoveToPoints(new Vector3[1] { _spawnPoint.position }, _Loop: false, _StartFromClosest: true, _SkipUnpassableTargets: true, 0.35f, null, () =>
			{
				if (creature == null || networkObject == null)
				{
					RemoveInvalidSpawnedCreatures();
				}
				else
				{
					DespawnCreatureImmediate(creature);
					_spawnedCreatures.Remove(creature);
				}
			}, () =>
			{
				if (!(creature == null) && !(networkObject == null))
				{
					Log("Returning " + creatureName + " to spawn point.");
				}
			}, (PathfindingResult result) =>
			{
				if (creature == null || networkObject == null)
				{
					RemoveInvalidSpawnedCreatures();
				}
				else
				{
					Log($"Return path failed for {creatureName}. Result={result.Result}", LogType.Warning);
					DespawnCreatureImmediate(creature);
					_spawnedCreatures.Remove(creature);
				}
			}, _InterruptCurrentOrderImmediate: true);
			return true;
		}

		private void DespawnCreatureImmediate(SeaCreature creature)
		{
			if (creature == null)
			{
				return;
			}
			NetworkObject networkObject = creature.NetworkObject;
			if (!(networkObject == null))
			{
				creature.ServerPrepareForDespawn();
				if (networkObject.IsSpawned)
				{
					networkObject.Despawn();
				}
				else if (networkObject.gameObject != null)
				{
					Object.Destroy(networkObject.gameObject);
				}
			}
		}

		private void RemoveInvalidSpawnedCreatures()
		{
			for (int num = _spawnedCreatures.Count - 1; num >= 0; num--)
			{
				SeaCreature seaCreature = _spawnedCreatures[num];
				if (seaCreature == null || seaCreature.NetworkObject == null || !seaCreature.NetworkObject.IsSpawned)
				{
					_spawnedCreatures.RemoveAt(num);
				}
			}
		}

		private static bool IsServerReady()
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if (singleton != null)
			{
				return singleton.IsServer;
			}
			return false;
		}

		private static bool IsPositiveFinite(float value)
		{
			if (value > 0f && !float.IsNaN(value))
			{
				return !float.IsInfinity(value);
			}
			return false;
		}

		private static bool IsNonNegativeFinite(float value)
		{
			if (value >= 0f && !float.IsNaN(value))
			{
				return !float.IsInfinity(value);
			}
			return false;
		}

		private void Log(string message, LogType type = LogType.Log)
		{
			if (_log)
			{
				Debug.unityLogger.Log(type, "[CreaturePathSpawner:" + base.name + "] " + message);
			}
		}
	}
}
