using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Boss.Attacks
{
	public sealed class CrabBossCreatureSpawnAttack : CrabBossAttackBase
	{
		private enum SpawnChoice : byte
		{
			Shark = 0,
			Piranha = 1,
			Jellyfish = 2
		}

		[Flags]
		private enum SpawnPhaseMask : byte
		{
			None = 0,
			Phase1 = 1,
			Phase2 = 2,
			Phase3 = 4,
			All = Phase1 | Phase2 | Phase3
		}

		private sealed class SpawnedInstanceRecord
		{
			public readonly NetworkObject Instance;

			public readonly SpawnChoice Choice;

			public SpawnedInstanceRecord(NetworkObject instance, SpawnChoice choice)
			{
				Instance = instance;
				Choice = choice;
			}
		}

		[Header("Spawn Setup")]
		[SerializeField]
		private Transform _spawnPoint;

		[SerializeField]
		private Transform _patrolPointsRoot;

		[SerializeField]
		private Transform _spawnedParent;

		[SerializeField]
		[Min(0f)]
		private float _spawnPointPatrolExclusionRadius = 0.75f;

		[Header("Creature Phase Availability")]
		[SerializeField]
		private SpawnPhaseMask _sharkSpawnPhases = SpawnPhaseMask.Phase3;

		[SerializeField]
		private SpawnPhaseMask _piranhaSpawnPhases = SpawnPhaseMask.All;

		[SerializeField]
		private SpawnPhaseMask _jellyfishSpawnPhases = SpawnPhaseMask.All;

		[Header("Hostile Creatures")]
		[SerializeField]
		private CreatureSO _sharkCreature;

		[SerializeField]
		private CreatureSO _piranhaCreature;

		[SerializeField]
		private SharkCreature.BossReinforcementAttackMode _bossSharkAttackMode = SharkCreature.BossReinforcementAttackMode.BiteOnly;

		[SerializeField]
		[Min(0f)]
		private float _bossSharkBiteCooldown = 1.5f;

		[SerializeField]
		[Min(1f)]
		private int _maxActiveSharks = 2;

		[SerializeField]
		[Min(1f)]
		private int _piranhaSpawnCount = 10;

		[SerializeField]
		[Min(1f)]
		private int _maxActivePiranhas = 15;

		[SerializeField]
		[Min(0f)]
		private float _piranhaSpawnInterval = 0.1f;

		[SerializeField]
		private bool _patrolLoop = true;

		[SerializeField]
		private bool _patrolStartFromClosest;

		[SerializeField]
		[Min(0.01f)]
		private float _patrolReachDistance = 0.35f;

		[Header("Jellyfish")]
		[SerializeField]
		private NetworkObject _jellyfishPrefab;

		[SerializeField]
		private BoxCollider _jellyfishSpawnArea;

		[SerializeField]
		[Min(1f)]
		private int _jellyfishSpawnCount = 7;

		[SerializeField]
		[Min(1f)]
		private int _maxActiveJellyfish = 14;

		[Header("Timing")]
		[SerializeField]
		[Min(0.01f)]
		private float _activeDuration = 8f;

		[Header("Debug")]
		[SerializeField]
		private bool _log;

		private readonly List<SpawnedInstanceRecord> _spawnedInstances = new List<SpawnedInstanceRecord>();

		private readonly List<SpawnChoice> _remainingSpawnChoices = new List<SpawnChoice>();

		private Coroutine _serverRoutine;

		private Coroutine _serverPiranhaSpawnRoutine;

		private int _nextRuntimeId;

		private int _spawnChoicePhase;

		private bool _spawnTriggered;

		public override bool ServerCanSelectAttack()
		{
			if (!base.ServerCanSelectAttack())
			{
				return false;
			}
			PruneSpawnedInstances();
			return HasAnySpawnCapacity();
		}

		public override void OnNetworkDespawn()
		{
			if (base.IsServer)
			{
				StopServerRoutine();
				DespawnSpawnedInstances();
			}
			base.OnNetworkDespawn();
		}

		protected override void OnServerAttackStarted(CrabBossAttackCommand command)
		{
			StopServerRoutine();
			_spawnTriggered = false;
			PruneSpawnedInstances();
			if (!HasAnySpawnCapacity())
			{
				_spawnTriggered = true;
				Log($"Skipped. All boss creature limits are full. Token={command.Token}.");
				CompleteAttack(command.Token);
			}
			else
			{
				Log($"Started. Waiting for SpawnCreature animation event. Token={command.Token}.");
			}
		}

		public void AnimationEventSpawnCreature()
		{
			if (base.IsSpawned && base.IsServer)
			{
				ServerSpawnRandomCreature(base.Boss.LocalAttackAnimationToken);
			}
		}

		private void ServerSpawnRandomCreature(uint token)
		{
			if (!IsTokenActive(token) || _spawnTriggered || base.Boss == null || !base.Boss.IsAttackRunning || base.Boss.CurrentAttackId != CrabBossAttackId.CreatureSpawn || base.Boss.CurrentAttackToken != base.ActiveToken)
			{
				return;
			}
			_spawnTriggered = true;
			if (!TryGetNextSpawnChoice(out var choice))
			{
				Log($"Spawn skipped. All boss creature limits are full. Token={base.ActiveToken}.");
				CompleteAttack(base.ActiveToken);
				return;
			}
			bool flag = choice switch
			{
				SpawnChoice.Shark => SpawnBossHostile(_sharkCreature, _spawnPoint, SpawnChoice.Shark), 
				SpawnChoice.Piranha => StartPiranhaWave(base.ActiveToken), 
				_ => SpawnJellyfishWave(), 
			};
			Log($"Random spawn selected. Choice={choice} Token={base.ActiveToken}.");
			if (!flag)
			{
				CompleteAttack(base.ActiveToken);
			}
			else
			{
				_serverRoutine = StartCoroutine(ServerRunWave(base.ActiveToken));
			}
		}

		private bool SpawnBossHostile(CreatureSO creatureData, Transform spawnTransform, SpawnChoice choice)
		{
			if (creatureData == null || creatureData.Prefab == null || spawnTransform == null)
			{
				return false;
			}
			NetworkObject networkObject = UnityEngine.Object.Instantiate(creatureData.Prefab, spawnTransform.position, spawnTransform.rotation, _spawnedParent);
			if (networkObject == null)
			{
				return false;
			}
			if (!networkObject.TryGetComponent<SeaCreature>(out var component))
			{
				UnityEngine.Object.Destroy(networkObject.gameObject);
				return false;
			}
			CreaturePathAssignment creaturePathAssignment = networkObject.GetComponent<CreaturePathAssignment>();
			if (creaturePathAssignment == null)
			{
				creaturePathAssignment = networkObject.gameObject.AddComponent<CreaturePathAssignment>();
			}
			if (!creaturePathAssignment.Assign(_patrolPointsRoot, _patrolLoop, _patrolStartFromClosest, _patrolReachDistance, _spawnPoint, _spawnPointPatrolExclusionRadius))
			{
				UnityEngine.Object.Destroy(networkObject.gameObject);
				return false;
			}
			if (!component.ServerInitStandalone(creatureData, _nextRuntimeId++))
			{
				if (networkObject.IsSpawned)
				{
					networkObject.Despawn();
				}
				else
				{
					UnityEngine.Object.Destroy(networkObject.gameObject);
				}
				return false;
			}
			if (choice == SpawnChoice.Shark && component is SharkCreature sharkCreature)
			{
				sharkCreature.ServerSetBossReinforcement(isBossReinforcement: true, _bossSharkAttackMode, _bossSharkBiteCooldown);
			}
			else if (choice == SpawnChoice.Piranha && component is PiranhaCreature piranhaCreature)
			{
				piranhaCreature.ServerSetBossReinforcement(isBossReinforcement: true);
			}
			component.SetServerSimulationActive(active: true);
			_spawnedInstances.Add(new SpawnedInstanceRecord(networkObject, choice));
			Log("Spawned standalone boss creature. Creature=" + creatureData.name + ".");
			return true;
		}

		private bool StartPiranhaWave(uint token)
		{
			int b = Mathf.Max(0, _maxActivePiranhas - GetActiveCount(SpawnChoice.Piranha));
			int num = Mathf.Min(_piranhaSpawnCount, b);
			if (num <= 0)
			{
				return false;
			}
			_serverPiranhaSpawnRoutine = StartCoroutine(ServerSpawnPiranhaWave(token, num));
			return true;
		}

		private IEnumerator ServerSpawnPiranhaWave(uint token, int spawnCount)
		{
			int spawnedCount = 0;
			for (int i = 0; i < spawnCount; i++)
			{
				if (!IsTokenActive(token))
				{
					break;
				}
				if (GetActiveCount(SpawnChoice.Piranha) >= _maxActivePiranhas)
				{
					break;
				}
				if (TryGetRandomPatrolPoint(out var point) && SpawnBossHostile(_piranhaCreature, point, SpawnChoice.Piranha))
				{
					spawnedCount++;
				}
				if (i < spawnCount - 1 && _piranhaSpawnInterval > 0f)
				{
					yield return new WaitForSeconds(_piranhaSpawnInterval);
				}
			}
			_serverPiranhaSpawnRoutine = null;
			Log($"Spawned {spawnedCount}/{spawnCount} piranhas.");
		}

		private bool SpawnJellyfishWave()
		{
			if (_jellyfishSpawnArea == null)
			{
				return false;
			}
			int b = Mathf.Max(0, _maxActiveJellyfish - GetActiveCount(SpawnChoice.Jellyfish));
			int num = Mathf.Min(_jellyfishSpawnCount, b);
			if (num <= 0)
			{
				return false;
			}
			int num2 = 0;
			for (int i = 0; i < num; i++)
			{
				Vector3 randomPointInside = GetRandomPointInside(_jellyfishSpawnArea);
				NetworkObject networkObject = UnityEngine.Object.Instantiate(_jellyfishPrefab, randomPointInside, _jellyfishSpawnArea.transform.rotation, _spawnedParent);
				if (!(networkObject == null))
				{
					networkObject.Spawn(destroyWithScene: true);
					_spawnedInstances.Add(new SpawnedInstanceRecord(networkObject, SpawnChoice.Jellyfish));
					num2++;
					RegisterJellyfishIfNeeded(networkObject);
					if (networkObject.TryGetComponent<JellyfishTrap>(out var component))
					{
						component.ServerSetCanBeKilledByHarpoon(canBeKilled: true);
					}
				}
			}
			Log($"Spawned {num2}/{num} jellyfish.");
			return num2 > 0;
		}

		protected override void OnServerAttackEnded(CrabBossAttackCommand command, CrabBossAttackEndReason reason)
		{
			StopServerRoutine();
			_spawnTriggered = false;
			Log($"Ended. Token={command.Token} Reason={reason}.");
		}

		protected override void OnServerForceCleanup(uint activeToken)
		{
			StopServerRoutine();
			DespawnSpawnedInstances();
			_remainingSpawnChoices.Clear();
			_spawnChoicePhase = 0;
			_spawnTriggered = false;
		}

		protected override bool ValidateSpecificSettings(out string error)
		{
			if (base.AttackId != CrabBossAttackId.CreatureSpawn)
			{
				error = "Attack ID must be CreatureSpawn.";
				return false;
			}
			if (_spawnPoint == null)
			{
				error = "Boss creature spawn point is missing.";
				return false;
			}
			if (_patrolPointsRoot == null || _patrolPointsRoot.childCount == 0)
			{
				error = "Boss creature patrol points root needs at least one child point.";
				return false;
			}
			if (_spawnedParent == null)
			{
				error = "Boss creature spawned parent is missing.";
				return false;
			}
			if (!IsNonNegativeFinite(_spawnPointPatrolExclusionRadius))
			{
				error = "Spawn point patrol exclusion radius must be zero or greater and finite.";
				return false;
			}
			if (!HasUsablePatrolPoint())
			{
				error = "Boss creature patrol points need at least one finite point outside the spawn exclusion radius.";
				return false;
			}
			if (!IsValidPhaseMask(_sharkSpawnPhases))
			{
				error = "Shark spawn phases must contain at least one valid boss phase.";
				return false;
			}
			if (!IsValidPhaseMask(_piranhaSpawnPhases))
			{
				error = "Piranha spawn phases must contain at least one valid boss phase.";
				return false;
			}
			if (!IsValidPhaseMask(_jellyfishSpawnPhases))
			{
				error = "Jellyfish spawn phases must contain at least one valid boss phase.";
				return false;
			}
			if (!ValidateCreatureData<SharkCreature>(_sharkCreature, "Shark", out error))
			{
				return false;
			}
			if (!ValidateCreatureData<PiranhaCreature>(_piranhaCreature, "Piranha", out error))
			{
				return false;
			}
			if (!IsNonNegativeFinite(_bossSharkBiteCooldown))
			{
				error = "Boss shark bite cooldown must be zero or greater and finite.";
				return false;
			}
			if (_maxActiveSharks <= 0)
			{
				error = "Maximum active shark count must be greater than zero.";
				return false;
			}
			if (_piranhaSpawnCount <= 0)
			{
				error = "Piranha spawn count must be greater than zero.";
				return false;
			}
			if (_maxActivePiranhas <= 0)
			{
				error = "Maximum active piranha count must be greater than zero.";
				return false;
			}
			if (!IsNonNegativeFinite(_piranhaSpawnInterval))
			{
				error = "Piranha spawn interval must be zero or greater and finite.";
				return false;
			}
			if (!IsPositiveFinite(_patrolReachDistance))
			{
				error = "Patrol reach distance must be greater than zero and finite.";
				return false;
			}
			if (_jellyfishPrefab == null)
			{
				error = "Jellyfish network prefab is missing.";
				return false;
			}
			if (_jellyfishSpawnArea == null)
			{
				error = "Jellyfish spawn area BoxCollider is missing.";
				return false;
			}
			if (_jellyfishSpawnCount <= 0)
			{
				error = "Jellyfish spawn count must be greater than zero.";
				return false;
			}
			if (_maxActiveJellyfish <= 0)
			{
				error = "Maximum active jellyfish count must be greater than zero.";
				return false;
			}
			if (!IsPositiveFinite(_activeDuration))
			{
				error = "Active duration must be greater than zero and finite.";
				return false;
			}
			if (base.Boss.TryGetAttackTimeout(base.AttackId, out var timeout) && (!IsPositiveFinite(timeout) || _activeDuration >= timeout))
			{
				error = "CreatureSpawn timeout must be greater than active duration.";
				return false;
			}
			error = null;
			return true;
		}

		private IEnumerator ServerRunWave(uint token)
		{
			yield return new WaitForSeconds(_activeDuration);
			if (IsTokenActive(token))
			{
				_serverRoutine = null;
				CompleteAttack(token);
			}
		}

		private bool TryGetRandomPatrolPoint(out Transform point)
		{
			point = null;
			if (_patrolPointsRoot == null || _patrolPointsRoot.childCount == 0)
			{
				return false;
			}
			point = _patrolPointsRoot.GetChild(UnityEngine.Random.Range(0, _patrolPointsRoot.childCount));
			return point != null;
		}

		private bool HasUsablePatrolPoint()
		{
			if (_patrolPointsRoot == null || _spawnPoint == null)
			{
				return false;
			}
			float num = _spawnPointPatrolExclusionRadius * _spawnPointPatrolExclusionRadius;
			for (int i = 0; i < _patrolPointsRoot.childCount; i++)
			{
				float sqrMagnitude = (_patrolPointsRoot.GetChild(i).position - _spawnPoint.position).sqrMagnitude;
				if (!float.IsNaN(sqrMagnitude) && !float.IsInfinity(sqrMagnitude) && (_spawnPointPatrolExclusionRadius <= 0f || sqrMagnitude > num))
				{
					return true;
				}
			}
			return false;
		}

		private bool TryGetNextSpawnChoice(out SpawnChoice choice)
		{
			PruneSpawnedInstances();
			int num = ((base.Boss != null) ? base.Boss.CurrentPhase : 0);
			if (_spawnChoicePhase != num)
			{
				_remainingSpawnChoices.Clear();
				_spawnChoicePhase = num;
				AddChoiceIfAvailable(SpawnChoice.Shark);
				AddChoiceIfAvailable(SpawnChoice.Piranha);
				AddChoiceIfAvailable(SpawnChoice.Jellyfish);
				List<SpawnChoice> list = new List<SpawnChoice>();
				AddNewlyUnlockedChoiceIfAvailable(SpawnChoice.Shark, list);
				AddNewlyUnlockedChoiceIfAvailable(SpawnChoice.Piranha, list);
				AddNewlyUnlockedChoiceIfAvailable(SpawnChoice.Jellyfish, list);
				if (list.Count > 0)
				{
					int index = UnityEngine.Random.Range(0, list.Count);
					choice = list[index];
					_remainingSpawnChoices.Remove(choice);
					return true;
				}
			}
			for (int num2 = _remainingSpawnChoices.Count - 1; num2 >= 0; num2--)
			{
				if (!HasSpawnCapacity(_remainingSpawnChoices[num2]))
				{
					_remainingSpawnChoices.RemoveAt(num2);
				}
			}
			if (_remainingSpawnChoices.Count == 0)
			{
				AddChoiceIfAvailable(SpawnChoice.Shark);
				AddChoiceIfAvailable(SpawnChoice.Piranha);
				AddChoiceIfAvailable(SpawnChoice.Jellyfish);
			}
			if (_remainingSpawnChoices.Count == 0)
			{
				choice = SpawnChoice.Shark;
				return false;
			}
			int index2 = UnityEngine.Random.Range(0, _remainingSpawnChoices.Count);
			choice = _remainingSpawnChoices[index2];
			_remainingSpawnChoices.RemoveAt(index2);
			return true;
		}

		private void AddNewlyUnlockedChoiceIfAvailable(SpawnChoice choice, List<SpawnChoice> choices)
		{
			if (HasSpawnCapacity(choice) && !(base.Boss == null))
			{
				int num = base.Boss.CurrentPhase - 1;
				if (num < 1 || !IsChoiceEnabledForPhase(choice, num))
				{
					choices.Add(choice);
				}
			}
		}

		private void AddChoiceIfAvailable(SpawnChoice choice)
		{
			if (HasSpawnCapacity(choice))
			{
				_remainingSpawnChoices.Add(choice);
			}
		}

		private bool HasAnySpawnCapacity()
		{
			if (!HasSpawnCapacity(SpawnChoice.Shark) && !HasSpawnCapacity(SpawnChoice.Piranha))
			{
				return HasSpawnCapacity(SpawnChoice.Jellyfish);
			}
			return true;
		}

		private bool HasSpawnCapacity(SpawnChoice choice)
		{
			if (!IsChoiceEnabledForCurrentPhase(choice))
			{
				return false;
			}
			int num = choice switch
			{
				SpawnChoice.Shark => _maxActiveSharks, 
				SpawnChoice.Piranha => _maxActivePiranhas, 
				_ => _maxActiveJellyfish, 
			};
			return GetActiveCount(choice) < num;
		}

		private bool IsChoiceEnabledForCurrentPhase(SpawnChoice choice)
		{
			if (base.Boss == null || base.Boss.CurrentPhase < 1 || base.Boss.CurrentPhase > 3)
			{
				return false;
			}
			return IsChoiceEnabledForPhase(choice, base.Boss.CurrentPhase);
		}

		private bool IsChoiceEnabledForPhase(SpawnChoice choice, int phase)
		{
			if (phase < 1 || phase > 3)
			{
				return false;
			}
			SpawnPhaseMask spawnPhaseMask = (SpawnPhaseMask)(1 << phase - 1);
			return ((uint)(choice switch
			{
				SpawnChoice.Shark => (int)_sharkSpawnPhases, 
				SpawnChoice.Piranha => (int)_piranhaSpawnPhases, 
				_ => (int)_jellyfishSpawnPhases, 
			}) & (uint)spawnPhaseMask) != 0;
		}

		private int GetActiveCount(SpawnChoice choice)
		{
			int num = 0;
			for (int i = 0; i < _spawnedInstances.Count; i++)
			{
				SpawnedInstanceRecord spawnedInstanceRecord = _spawnedInstances[i];
				if (spawnedInstanceRecord != null && spawnedInstanceRecord.Choice == choice && !(spawnedInstanceRecord.Instance == null) && spawnedInstanceRecord.Instance.IsSpawned)
				{
					num++;
				}
			}
			return num;
		}

		private void PruneSpawnedInstances()
		{
			for (int num = _spawnedInstances.Count - 1; num >= 0; num--)
			{
				SpawnedInstanceRecord spawnedInstanceRecord = _spawnedInstances[num];
				if (spawnedInstanceRecord == null || spawnedInstanceRecord.Instance == null || !spawnedInstanceRecord.Instance.IsSpawned)
				{
					_spawnedInstances.RemoveAt(num);
				}
			}
		}

		private static Vector3 GetRandomPointInside(BoxCollider area)
		{
			Vector3 vector = area.size * 0.5f;
			Vector3 position = area.center + new Vector3(UnityEngine.Random.Range(0f - vector.x, vector.x), UnityEngine.Random.Range(0f - vector.y, vector.y), UnityEngine.Random.Range(0f - vector.z, vector.z));
			return area.transform.TransformPoint(position);
		}

		private bool IsTokenActive(uint token)
		{
			if (base.IsServer && token != 0 && token == base.ActiveToken && base.Boss != null && base.Boss.IsAttackRunning)
			{
				return base.Boss.CurrentAttackToken == token;
			}
			return false;
		}

		private void StopServerRoutine()
		{
			if (_serverRoutine != null)
			{
				StopCoroutine(_serverRoutine);
				_serverRoutine = null;
			}
			if (_serverPiranhaSpawnRoutine != null)
			{
				StopCoroutine(_serverPiranhaSpawnRoutine);
				_serverPiranhaSpawnRoutine = null;
			}
		}

		private void DespawnSpawnedInstances()
		{
			for (int num = _spawnedInstances.Count - 1; num >= 0; num--)
			{
				NetworkObject networkObject = _spawnedInstances[num]?.Instance;
				if (!(networkObject == null))
				{
					networkObject.transform.DOKill();
					UnregisterJellyfishIfNeeded(networkObject);
					if (networkObject.TryGetComponent<SeaCreature>(out var component))
					{
						component.ServerPrepareForDespawn();
					}
					if (networkObject.IsSpawned)
					{
						networkObject.Despawn();
					}
					else
					{
						UnityEngine.Object.Destroy(networkObject.gameObject);
					}
				}
			}
			_spawnedInstances.Clear();
		}

		private static bool ValidateCreatureData<TCreature>(CreatureSO creatureData, string displayName, out string error) where TCreature : SeaCreature
		{
			if (creatureData == null)
			{
				error = displayName + " CreatureSO is missing.";
				return false;
			}
			if (creatureData.Prefab == null)
			{
				error = displayName + " network prefab is missing.";
				return false;
			}
			if (!creatureData.Prefab.TryGetComponent<TCreature>(out var _))
			{
				error = displayName + " prefab does not contain " + typeof(TCreature).Name + ".";
				return false;
			}
			error = null;
			return true;
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

		private static bool IsValidPhaseMask(SpawnPhaseMask phaseMask)
		{
			if (phaseMask != SpawnPhaseMask.None)
			{
				return (phaseMask & ~SpawnPhaseMask.All) == 0;
			}
			return false;
		}

		private static void RegisterJellyfishIfNeeded(NetworkObject instance)
		{
			if (!(instance == null) && !(Singleton<SeaCreatureManager>.Instance == null) && instance.TryGetComponent<JellyfishTrap>(out var component))
			{
				Singleton<SeaCreatureManager>.Instance.RegisterJellyfishTrap(component);
			}
		}

		private static void UnregisterJellyfishIfNeeded(NetworkObject instance)
		{
			if (!(instance == null) && !(Singleton<SeaCreatureManager>.Instance == null) && instance.TryGetComponent<JellyfishTrap>(out var component))
			{
				Singleton<SeaCreatureManager>.Instance.UnregisterJellyfishTrap(component);
			}
		}

		private void Log(string message)
		{
			if (_log)
			{
				UtilityMethods.Log("[CrabBoss][CreatureSpawnAttack] " + message);
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "CrabBossCreatureSpawnAttack";
		}
	}
}
