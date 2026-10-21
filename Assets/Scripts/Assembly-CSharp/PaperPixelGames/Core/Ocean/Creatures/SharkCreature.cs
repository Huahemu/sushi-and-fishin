using System;
using System.Collections.Generic;
using Nav3D.API;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking.Chat;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public sealed class SharkCreature : HostileSeaCreature, IAttachableObject
	{
		private enum SharkAttackMode : byte
		{
			BiteDrag = 0,
			TailKnockback = 1,
			CageStrike = 2
		}

		public enum BossReinforcementAttackMode : byte
		{
			Drag = 0,
			BiteOnly = 1
		}

		private const string BoidsColliderLayerName = "BoidsCollider";

		[Header("Shark Refs")]
		[SerializeField]
		private Animator _animator;

		[SerializeField]
		private List<AttachSeat> _seats = new List<AttachSeat>();

		[SerializeField]
		private AudioSource _waterSfx;

		[SerializeField]
		private AudioSource _biteSfx;

		[Header("Shark Cage")]
		[SerializeField]
		private AnimationClip _cageAttackClip;

		[SerializeField]
		[Min(0.05f)]
		private float _cageAttackFallbackDuration = 1f;

		[Header("Shark Tail Attack")]
		[SerializeField]
		private CreatureSensor _tailAttackSensor;

		[SerializeField]
		private AnimationClip _tailAttackClip;

		[SerializeField]
		[Min(0.05f)]
		private float _tailAttackFallbackDuration = 1.5f;

		private bool _postDragChaseCooldownActive;

		private uint _postDragChaseCooldownToken;

		private bool _postAttackChaseCooldownActive;

		private uint _postAttackChaseCooldownToken;

		private bool _isDragging;

		private bool _debugChaseUntilAttack;

		private byte _dragTakenHitCount;

		private float _dragEndTime;

		private float _nextDragPenaltyDamageTime;

		private float _dragEscapeProgress;

		private double _lastDragEscapePressTime;

		private uint _dragSessionId;

		private MotionNavigationType _baseMotionNavigationType;

		private bool _baseMotionTypeCached;

		private bool _isBossReinforcement;

		private BossReinforcementAttackMode _bossReinforcementAttackMode;

		private float _bossBiteCooldown = 1.5f;

		private bool _bossBiteCooldownActive;

		private uint _bossBiteCooldownToken;

		private readonly HashSet<ulong> _tailHitPlayerIds = new HashSet<ulong>();

		private readonly List<SeaArea> _layerPatrolAreas = new List<SeaArea>();

		private SharkSO _sharkSO;

		private SharkAttackMode _attackMode;

		private bool _attackHitConsumed;

		private SeaLayer _layerPatrolLayer;

		private SeaArea _pendingLayerPatrolArea;

		private int _layerPatrolAreaIndex;

		private int _layerPatrolConsecutiveFailures;

		private bool _usingLayerPatrolFallback;

		private bool _deathFilletsDropped;

		private int _deathFilletFreeFallExcludeLayerMask;

		public SharkSO SharkSO => _sharkSO;

		public bool CanProcessAttack03Hit
		{
			get
			{
				if (base.IsServer && base.IsServerSimulationActive)
				{
					return _state == HostileCreatureState.Attack;
				}
				return false;
			}
		}

		public PlayerAttachmentMode AttachmentMode => PlayerAttachmentMode.SharkDrag;

		public List<AttachSeat> Seats
		{
			get
			{
				return _seats;
			}
			set
			{
				_seats = value;
			}
		}

		protected override bool UseEmbeddedGraphic => true;

		protected override GameObject EmbeddedGraphicRoot
		{
			get
			{
				if (!(_animator != null))
				{
					return null;
				}
				return _animator.gameObject;
			}
		}

		protected override bool KeepEmbeddedGraphicRootActive => true;

		public bool ServerTryReleasePlayerForRecovery(PPG_Player player, string reason)
		{
			if (!base.IsServer || !_isDragging || player == null || _targetPlayer != player)
			{
				return false;
			}
			ForceEndDrag(escapeAfter: false, reason);
			return true;
		}

		protected override void CacheComponents()
		{
			base.CacheComponents();
			int num = LayerMask.NameToLayer("BoidsCollider");
			_deathFilletFreeFallExcludeLayerMask = ((num >= 0) ? (1 << num) : 0);
			if (_nav3DAgent != null && !_baseMotionTypeCached)
			{
				_baseMotionNavigationType = _nav3DAgent.Config.MotionNavigationType;
				_baseMotionTypeCached = true;
			}
		}

		protected override bool TryCacheHostileData()
		{
			_sharkSO = null;
			if (!base.TryCacheHostileData())
			{
				return false;
			}
			if (!(_creatureSO is SharkSO sharkSO))
			{
				Log("Shark data cache failed. CreatureSO is not SharkSO. Current=" + (_creatureSO?.GetType().Name ?? "null"), LogType.Error);
				_hostileSO = null;
				return false;
			}
			_sharkSO = sharkSO;
			return true;
		}

		protected override void OnClientVisualInitialized()
		{
			base.OnClientVisualInitialized();
			SetTailAttackSensorActive(base.IsServer && base.IsServerSimulationActive && CanUseTailKnockback());
		}

		protected override void OnServerCreatureSpawned()
		{
			base.OnServerCreatureSpawned();
			if (_tailAttackSensor != null)
			{
				_tailAttackSensor.DetectedEnter += OnTailAttackDetected;
			}
		}

		protected override void OnServerCreatureDespawned()
		{
			if (_tailAttackSensor != null)
			{
				_tailAttackSensor.DetectedEnter -= OnTailAttackDetected;
			}
			ClearTarget("CreatureDespawned");
			base.OnServerCreatureDespawned();
		}

		protected override void OnServerSimulationStarted()
		{
			if (_animator != null)
			{
				_animator.SetBool("Dead", value: false);
			}
			_isDragging = false;
			_debugChaseUntilAttack = false;
			_dragTakenHitCount = 0;
			_dragEndTime = 0f;
			_nextDragPenaltyDamageTime = 0f;
			ResetDragEscapeState(invalidateSession: true);
			_postDragChaseCooldownActive = false;
			_postDragChaseCooldownToken++;
			_postAttackChaseCooldownActive = false;
			_postAttackChaseCooldownToken++;
			_bossBiteCooldownActive = false;
			_bossBiteCooldownToken++;
			_attackMode = SharkAttackMode.BiteDrag;
			_attackHitConsumed = false;
			_tailHitPlayerIds.Clear();
			_deathFilletsDropped = false;
			RestoreMotionNavigationType();
			ResetLayerPatrolPlan();
			base.OnServerSimulationStarted();
			SetSensorActive(CanInitiateAggressiveChase());
			SetTailAttackSensorActive(CanUseTailKnockback());
			if (_isBossReinforcement)
			{
				StartBossReinforcementChase();
			}
		}

		public void ServerSetBossReinforcement(bool isBossReinforcement, BossReinforcementAttackMode attackMode = BossReinforcementAttackMode.Drag, float biteCooldown = 1.5f)
		{
			if (!base.IsServer)
			{
				return;
			}
			_isBossReinforcement = isBossReinforcement;
			_bossReinforcementAttackMode = attackMode;
			_bossBiteCooldown = Mathf.Max(0f, biteCooldown);
			if (base.IsServerSimulationActive)
			{
				SetSensorActive(CanInitiateAggressiveChase());
				SetTailAttackSensorActive(CanUseTailKnockback());
				if (_isBossReinforcement)
				{
					StartBossReinforcementChase();
				}
			}
		}

		public bool ServerStartDebugChase(PPG_Player target)
		{
			if (!base.IsServer || !base.IsServerSimulationActive || _isDead)
			{
				return false;
			}
			if (!CanTargetPlayer(target) || _nav3DAgent == null || !_nav3DAgent.enabled)
			{
				return false;
			}
			_debugChaseUntilAttack = true;
			_postAttackChaseCooldownActive = false;
			_postAttackChaseCooldownToken++;
			_postDragChaseCooldownActive = false;
			_postDragChaseCooldownToken++;
			SetTarget(target);
			SetState(HostileCreatureState.Chase);
			Log("Debug chase started. Target=" + target.name);
			return true;
		}

		public bool ServerDebugDie(string reason)
		{
			if (!base.IsServer || !base.IsSpawned || _isDead)
			{
				return false;
			}
			_debugChaseUntilAttack = false;
			SetState(HostileCreatureState.Dead);
			Log("Debug shark killed. Reason=" + reason);
			return true;
		}

		protected override void DespawnAfterDeath()
		{
			if (base.IsServer)
			{
				ServerDropDeathFillets();
				base.DespawnAfterDeath();
			}
		}

		private void ServerDropDeathFillets()
		{
			if (_deathFilletsDropped)
			{
				return;
			}
			_deathFilletsDropped = true;
			if (_sharkSO == null || _sharkSO.IngredientSO == null || _sharkSO.IngredientSO.Prefab == null)
			{
				Log("Death fillet drop skipped. Shark fillet data or prefab is missing.", LogType.Warning);
				return;
			}
			int num = Mathf.Max(0, _sharkSO.MinDeathFilletDropCount);
			int num2 = Mathf.Max(num, _sharkSO.MaxDeathFilletDropCount);
			int num3 = UnityEngine.Random.Range(num, num2 + 1);
			if (num3 <= 0)
			{
				return;
			}
			ulong preferredSimulationClientId = ((_lastDamageDealer != null) ? _lastDamageDealer.OwnerClientId : 0);
			Vector3 position = base.transform.position;
			float num4 = UnityEngine.Random.Range(0f, MathF.PI * 2f);
			for (int i = 0; i < num3; i++)
			{
				float f = num4 + MathF.PI * 2f * (float)i / (float)num3;
				Vector3 vector = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f));
				Vector3 vector2 = position + vector * UnityEngine.Random.Range(0.2f, 0.45f) + Vector3.up * UnityEngine.Random.Range(0.1f, 0.3f);
				if (!_sharkSO.IngredientSO.Prefab.TryInstantiateAndSpawn(out var instance, vector2, Quaternion.LookRotation(vector, Vector3.up)))
				{
					Log($"Death fillet spawn failed. Index={i} Count={num3}", LogType.Warning);
					continue;
				}
				if (!instance.TryGetComponent<BaseHoldable>(out var component))
				{
					Log("Death fillet prefab has no BaseHoldable. Prefab=" + _sharkSO.IngredientSO.Prefab.name, LogType.Warning);
					instance.Despawn();
					continue;
				}
				Vector3 normalized = (vector * 0.15f + Vector3.down).normalized;
				if (!component.ServerStartWorldFreeFall(vector2, normalized, preferredSimulationClientId, startedUnderwater: true, _deathFilletFreeFallExcludeLayerMask))
				{
					component.Drop(vector2, Quaternion.LookRotation(vector, Vector3.up));
					Log($"Death fillet free-fall failed. Dropped without physics. Index={i}", LogType.Warning);
				}
			}
			Log($"Death fillets dropped. Count={num3} Range={num}-{num2}");
		}

		protected override void OnServerSimulationStopped()
		{
			_debugChaseUntilAttack = false;
			ForceEndDrag(escapeAfter: false, "SimulationStopped");
			RestoreMotionNavigationType();
			_postDragChaseCooldownActive = false;
			_postDragChaseCooldownToken++;
			_postAttackChaseCooldownActive = false;
			_postAttackChaseCooldownToken++;
			_bossBiteCooldownActive = false;
			_bossBiteCooldownToken++;
			_attackMode = SharkAttackMode.BiteDrag;
			_attackHitConsumed = false;
			_tailHitPlayerIds.Clear();
			SetTailAttackSensorActive(active: false);
			_nextDragPenaltyDamageTime = 0f;
			ResetDragEscapeState(invalidateSession: true);
			ResetLayerPatrolPlan();
			base.OnServerSimulationStopped();
		}

		protected override void TickAttack()
		{
			if (_bossBiteCooldownActive)
			{
				return;
			}
			if (_isDragging)
			{
				if (_targetPlayer == null || !_targetPlayer.IsSpawned)
				{
					ForceEndDrag(escapeAfter: false, "DragTargetInvalid");
				}
				if (_isDragging)
				{
					TickDragEscapeProgress();
					TickDragPenaltyDamage();
				}
			}
			else if (_isBossReinforcement && !IsTargetValidForAttack())
			{
				PPG_Player targetPlayer = _targetPlayer;
				ClearTarget("BossAttackTargetInvalid");
				if (!StartBossReinforcementChase(targetPlayer))
				{
					SetState(HostileCreatureState.Patrol);
				}
			}
			else
			{
				base.TickAttack();
			}
		}

		protected override void TickChase()
		{
			if (_isBossReinforcement && !IsTargetValidForChase())
			{
				PPG_Player targetPlayer = _targetPlayer;
				ClearTarget("BossChaseTargetInvalid");
				if (!StartBossReinforcementChase(targetPlayer))
				{
					SetState(HostileCreatureState.Patrol);
				}
			}
			else
			{
				base.TickChase();
			}
		}

		protected override void StartChase()
		{
			_attackMode = SharkAttackMode.BiteDrag;
			if (_targetPlayer != null)
			{
				_targetPlayer.ClientOwnerPlaySharkChaseSFXRpc();
			}
			if (_nav3DAgent != null)
			{
				_nav3DAgent.Config.MotionNavigationType = MotionNavigationType.COMBINED;
			}
			if (_debugChaseUntilAttack)
			{
				StartDebugChase();
			}
			else
			{
				base.StartChase();
			}
		}

		private void StartDebugChase()
		{
			if (_nav3DAgent == null || !_nav3DAgent.enabled || !IsTargetValidForChase())
			{
				_debugChaseUntilAttack = false;
				ClearTarget("StartDebugChaseInvalidTarget");
				SetState(HostileCreatureState.Patrol);
				return;
			}
			SetChaseSpeed();
			ChangeAnim(CreatureAnim.AttackSwim);
			uint tokenAtIssue = _token;
			MarkMoveIssued();
			_nav3DAgent.FollowTarget(_targetTransform, _FollowContinuously: true, GetTargetOffsetUpdate(), GetFollowReachDistance(), () =>
			{
				IsTokenValid(tokenAtIssue);
			}, () =>
			{
				if (IsTokenValid(tokenAtIssue))
				{
					_debugChaseUntilAttack = false;
					ResetMoveTracking();
					ClearTarget("DebugChaseFail");
					SetState(HostileCreatureState.Patrol);
				}
			}, () =>
			{
				if (IsTokenValid(tokenAtIssue))
				{
					MarkMoveActive();
				}
			}, (PathfindingResult result) =>
			{
				if (IsTokenValid(tokenAtIssue))
				{
					_debugChaseUntilAttack = false;
					ResetMoveTracking();
					Log($"Debug chase pathfinding failed: {result.Result}", LogType.Warning);
					SetState(HostileCreatureState.Patrol);
				}
			}, _InterruptCurrentOrderImmediate: true);
			OnChaseStarted(_targetPlayer);
		}

		protected override void StartPatrol()
		{
			RestoreMotionNavigationType();
			_pendingLayerPatrolArea = null;
			_usingLayerPatrolFallback = false;
			base.StartPatrol();
		}

		protected override bool TryBuildLayerPatrolPath(int pointCount, bool allowRepeats, out List<Vector3> path)
		{
			if (_sharkSO != null && _sharkSO.PatrolAcrossLayerAreas && _currentSeaLayer != null)
			{
				return TryBuildNextLayerAreaPatrolPath(pointCount, allowRepeats, out path);
			}
			return base.TryBuildLayerPatrolPath(pointCount, allowRepeats, out path);
		}

		protected override void OnPatrolMoveFinished()
		{
			base.OnPatrolMoveFinished();
			if (_usingLayerPatrolFallback)
			{
				CompleteLayerPatrolFallback();
			}
			else if (!(_pendingLayerPatrolArea == null))
			{
				ServerSetCurrentSeaArea(_pendingLayerPatrolArea);
				_pendingLayerPatrolArea = null;
				_layerPatrolAreaIndex++;
				_layerPatrolConsecutiveFailures = 0;
			}
		}

		protected override void OnPatrolPathfindingFailed(PathfindingResult result)
		{
			base.OnPatrolPathfindingFailed(result);
			if (_usingLayerPatrolFallback)
			{
				CompleteLayerPatrolFallback();
			}
			else if (!(_pendingLayerPatrolArea == null))
			{
				_pendingLayerPatrolArea = null;
				_layerPatrolAreaIndex++;
				_layerPatrolConsecutiveFailures++;
			}
		}

		private bool TryBuildNextLayerAreaPatrolPath(int pointCount, bool allowRepeats, out List<Vector3> path)
		{
			path = null;
			if (!PrepareLayerPatrolAreas())
			{
				return base.TryBuildLayerPatrolPath(pointCount, allowRepeats, out path);
			}
			if (_layerPatrolConsecutiveFailures >= _layerPatrolAreas.Count)
			{
				return TryBuildLayerPatrolFallback(pointCount, allowRepeats, out path);
			}
			float minimumDistance = Mathf.Max(1f, GetPatrolReachDistance() * 2f);
			while (_layerPatrolAreaIndex < _layerPatrolAreas.Count)
			{
				SeaArea seaArea = _layerPatrolAreas[_layerPatrolAreaIndex];
				if (seaArea != null && seaArea.TryGetRandomPointOutsideRadius(base.transform.position, minimumDistance, out var point))
				{
					_pendingLayerPatrolArea = seaArea;
					_usingLayerPatrolFallback = false;
					path = new List<Vector3>(1) { point };
					return true;
				}
				_layerPatrolAreaIndex++;
				_layerPatrolConsecutiveFailures++;
				if (_layerPatrolConsecutiveFailures >= _layerPatrolAreas.Count)
				{
					return TryBuildLayerPatrolFallback(pointCount, allowRepeats, out path);
				}
			}
			RebuildLayerPatrolAreas();
			return TryBuildNextLayerAreaPatrolPath(pointCount, allowRepeats, out path);
		}

		private bool PrepareLayerPatrolAreas()
		{
			if (_layerPatrolLayer != _currentSeaLayer || _layerPatrolAreas.Count == 0)
			{
				RebuildLayerPatrolAreas();
			}
			else if (_layerPatrolAreaIndex >= _layerPatrolAreas.Count && _layerPatrolConsecutiveFailures < _layerPatrolAreas.Count)
			{
				RebuildLayerPatrolAreas();
			}
			return _layerPatrolAreas.Count > 1;
		}

		private void RebuildLayerPatrolAreas()
		{
			_layerPatrolAreas.Clear();
			_layerPatrolLayer = _currentSeaLayer;
			_layerPatrolAreaIndex = 0;
			_pendingLayerPatrolArea = null;
			if (_layerPatrolLayer == null)
			{
				return;
			}
			IReadOnlyList<SeaArea> seaAreas = _layerPatrolLayer.SeaAreas;
			for (int i = 0; i < seaAreas.Count; i++)
			{
				SeaArea seaArea = seaAreas[i];
				if (seaArea != null && seaArea.HasAnyPoint())
				{
					_layerPatrolAreas.Add(seaArea);
				}
			}
			for (int j = 0; j < _layerPatrolAreas.Count; j++)
			{
				int num = UnityEngine.Random.Range(j, _layerPatrolAreas.Count);
				List<SeaArea> layerPatrolAreas = _layerPatrolAreas;
				int index = j;
				List<SeaArea> layerPatrolAreas2 = _layerPatrolAreas;
				int index2 = num;
				SeaArea seaArea2 = _layerPatrolAreas[num];
				SeaArea seaArea3 = _layerPatrolAreas[j];
				SeaArea seaArea4 = (layerPatrolAreas[index] = seaArea2);
				seaArea4 = (layerPatrolAreas2[index2] = seaArea3);
			}
			int num2 = _layerPatrolAreas.IndexOf(_currentSeaArea);
			int num3 = _layerPatrolAreas.Count - 1;
			if (num2 >= 0 && num2 < num3)
			{
				List<SeaArea> layerPatrolAreas3 = _layerPatrolAreas;
				int index2 = num2;
				List<SeaArea> layerPatrolAreas2 = _layerPatrolAreas;
				int index = num3;
				SeaArea seaArea3 = _layerPatrolAreas[num3];
				SeaArea seaArea2 = _layerPatrolAreas[num2];
				SeaArea seaArea4 = (layerPatrolAreas3[index2] = seaArea3);
				seaArea4 = (layerPatrolAreas2[index] = seaArea2);
			}
		}

		private bool TryBuildLayerPatrolFallback(int pointCount, bool allowRepeats, out List<Vector3> path)
		{
			_pendingLayerPatrolArea = null;
			_usingLayerPatrolFallback = true;
			if (base.TryBuildLayerPatrolPath(pointCount, allowRepeats, out path))
			{
				return true;
			}
			_usingLayerPatrolFallback = false;
			_layerPatrolConsecutiveFailures = 0;
			RebuildLayerPatrolAreas();
			return false;
		}

		private void CompleteLayerPatrolFallback()
		{
			_usingLayerPatrolFallback = false;
			_layerPatrolConsecutiveFailures = 0;
			RebuildLayerPatrolAreas();
		}

		private void ResetLayerPatrolPlan()
		{
			_layerPatrolAreas.Clear();
			_layerPatrolLayer = null;
			_pendingLayerPatrolArea = null;
			_layerPatrolAreaIndex = 0;
			_layerPatrolConsecutiveFailures = 0;
			_usingLayerPatrolFallback = false;
		}

		protected override float GetAttackTriggerDistance()
		{
			float attackTriggerDistance = base.GetAttackTriggerDistance();
			if (_targetPlayer == null || !_targetPlayer.IsInCage || !CanUseCageStrike())
			{
				return attackTriggerDistance;
			}
			return Mathf.Max(attackTriggerDistance, _sharkSO.CageAttackTriggerDistance);
		}

		protected override void StartAttack()
		{
			if (!IsTargetValidForAttack())
			{
				ClearTarget("StartAttackInvalidTarget");
				SetState(HostileCreatureState.Patrol);
				return;
			}
			StopNavigation();
			RestoreMotionNavigationType();
			if (_waterSfx != null)
			{
				_waterSfx.Play();
			}
			if (_attackMode != SharkAttackMode.TailKnockback)
			{
				if (_targetPlayer.IsInCage && CanUseCageStrike())
				{
					_attackMode = SharkAttackMode.CageStrike;
				}
				else
				{
					if (!CanUseBiteDrag())
					{
						ClearTarget("NoAvailableAttack");
						SetState(HostileCreatureState.Patrol);
						return;
					}
					_attackMode = SharkAttackMode.BiteDrag;
				}
			}
			_attackHitConsumed = false;
			_tailHitPlayerIds.Clear();
			CreatureAnim anim;
			float seconds;
			switch (_attackMode)
			{
			case SharkAttackMode.TailKnockback:
				anim = CreatureAnim.TailAttack;
				seconds = GetClipDuration(_tailAttackClip, _tailAttackFallbackDuration);
				break;
			case SharkAttackMode.CageStrike:
				anim = CreatureAnim.CageAttack;
				seconds = GetClipDuration(_cageAttackClip, _cageAttackFallbackDuration);
				break;
			default:
				anim = CreatureAnim.Attack;
				seconds = ((_hostileSO != null) ? Mathf.Max(0.05f, _hostileSO.AttackTimeout) : 1.25f);
				break;
			}
			ChangeAnim(anim);
			uint tokenAtIssue = _token;
			Singleton<DelayHelper>.Instance.DelayAction(seconds, () =>
			{
				if (base.IsServer && IsTokenValid(tokenAtIssue) && _state == HostileCreatureState.Attack && !_isDragging && !_bossBiteCooldownActive)
				{
					EndAttackAndReturnToPatrol("AttackTimeout");
				}
			});
			OnAttackStarted(_targetPlayer);
		}

		protected override void OnStateChanged(HostileCreatureState previousState, HostileCreatureState newState)
		{
			base.OnStateChanged(previousState, newState);
			if (previousState == HostileCreatureState.Chase && newState != HostileCreatureState.Chase)
			{
				RestoreMotionNavigationType();
			}
			if (previousState == HostileCreatureState.Attack && newState != HostileCreatureState.Attack)
			{
				_attackMode = SharkAttackMode.BiteDrag;
				_attackHitConsumed = false;
				_tailHitPlayerIds.Clear();
			}
		}

		private void EndAttackAndReturnToPatrol(string reason)
		{
			if (!base.IsServer)
			{
				return;
			}
			Log("EndAttackAndReturnToPatrol reason=" + reason);
			PPG_Player targetPlayer = _targetPlayer;
			ClearTarget(reason);
			ResetMoveTracking();
			if (_isBossReinforcement)
			{
				if (!StartBossReinforcementChase(targetPlayer))
				{
					SetState(HostileCreatureState.Patrol);
				}
			}
			else
			{
				_debugChaseUntilAttack = false;
				BeginPostAttackChaseCooldown(reason);
				SetState(HostileCreatureState.Patrol);
			}
		}

		private void BeginPostAttackChaseCooldown(string reason)
		{
			if (!base.IsServer)
			{
				return;
			}
			float postAttackChaseCooldown = _sharkSO.PostAttackChaseCooldown;
			if (postAttackChaseCooldown <= 0f)
			{
				return;
			}
			_postAttackChaseCooldownActive = true;
			_postAttackChaseCooldownToken++;
			uint tokenAtIssue = _postAttackChaseCooldownToken;
			Log($"Post attack chase cooldown started. Duration={postAttackChaseCooldown:0.00} Reason={reason}");
			Singleton<DelayHelper>.Instance.DelayAction(postAttackChaseCooldown, () =>
			{
				if (base.IsServer && tokenAtIssue == _postAttackChaseCooldownToken)
				{
					_postAttackChaseCooldownActive = false;
					Log("Post attack chase cooldown finished.");
				}
			});
		}

		protected override void OnAttackHitPlayer(PPG_Player target)
		{
			if (!base.IsServer || target == null)
			{
				return;
			}
			_attackHitConsumed = true;
			if (target.IsInCage)
			{
				SharkCage serverActiveSharkCage = target.ServerActiveSharkCage;
				if (serverActiveSharkCage == null)
				{
					Log("Cage attack blocked because the active cage reference is missing. Target=" + target.name, LogType.Warning);
				}
				else if (serverActiveSharkCage.IsDoorOpen)
				{
					ApplyBiteDamage(target);
				}
				if (_isBossReinforcement && _bossReinforcementAttackMode == BossReinforcementAttackMode.BiteOnly)
				{
					BeginBossBiteCooldown(target);
				}
			}
			else
			{
				ApplyBiteDamage(target);
				if (_isBossReinforcement && _bossReinforcementAttackMode == BossReinforcementAttackMode.BiteOnly)
				{
					BeginBossBiteCooldown(target);
				}
				else if (!_sharkSO.DragAfterBite)
				{
					EndAttackAndReturnToPatrol("BiteOnlyAttack");
				}
				else
				{
					StartDrag(target);
				}
			}
		}

		private void ApplyBiteDamage(PPG_Player target)
		{
			if (target.DivingEquipment != null)
			{
				target.DivingEquipment.ServerConsumeOxygenRpc(GetDamageAmount(), withFeedback: true);
			}
			if (_biteSfx != null)
			{
				_biteSfx.Play();
			}
		}

		public override void Bite(Collider other)
		{
			if (_attackMode == SharkAttackMode.TailKnockback || _attackHitConsumed)
			{
				return;
			}
			if (!_isBossReinforcement || _bossReinforcementAttackMode != BossReinforcementAttackMode.BiteOnly)
			{
				base.Bite(other);
			}
			else
			{
				if (!base.IsServer || _state != HostileCreatureState.Attack || _bossBiteCooldownActive)
				{
					return;
				}
				if (!TryResolvePlayer(other, out var player) || !CanTargetPlayer(player))
				{
					EndAttackAndReturnToPatrol("BossBiteInvalidPlayer");
					return;
				}
				if (_targetPlayer != player)
				{
					SetTarget(player);
				}
				OnAttackHitPlayer(player);
			}
		}

		public bool ServerTryReceiveTailHit(Collider other)
		{
			if (!CanProcessAttack03Hit || _attackMode != SharkAttackMode.TailKnockback)
			{
				return false;
			}
			if (!TryResolvePlayer(other, out var player) || !CanTargetPlayer(player) || player.IsInCage)
			{
				return false;
			}
			if (!_tailHitPlayerIds.Add(player.NetworkObjectId))
			{
				return false;
			}
			ApplyTailDamage(player);
			if (player.Interactor == null || player.Interactor.NetworkObject == null || !player.Interactor.NetworkObject.IsSpawned)
			{
				Log("Tail knockback skipped because the target Interactor is invalid. Target=" + player.name, LogType.Warning);
				return true;
			}
			RpcParams rpcParams = base.RpcTarget.Single(player.Interactor.OwnerClientId, RpcTargetUse.Temp);
			ClientApplyTailKnockbackRpc(player.Interactor.NetworkObject.ToReference(), base.transform.position, rpcParams);
			Log("Tail knockback hit target=" + player.name);
			return true;
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientApplyTailKnockbackRpc(NetworkObjectReference interactorReference, Vector3 pushOrigin, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3829176547u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in pushOrigin);
				__endSendRpc(ref bufferWriter, 3829176547u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!interactorReference.TryGetComponent<Interactor>(out var component))
				{
					Log("Tail knockback failed because Interactor could not be resolved.", LogType.Warning);
				}
				else if (component.Player == null || component.Player.FirstPersonCharacter == null)
				{
					Log("Tail knockback failed because FirstPersonCharacter is missing.", LogType.Warning);
				}
				else
				{
					component.Player.FirstPersonCharacter.PushFromPoint(pushOrigin, _sharkSO.TailKnockbackImpulse, _sharkSO.TailKnockbackUpwardForce);
				}
			}
		}

		private void BeginBossBiteCooldown(PPG_Player bittenPlayer)
		{
			if (!base.IsServer)
			{
				return;
			}
			_bossBiteCooldownActive = true;
			_bossBiteCooldownToken++;
			uint cooldownToken = _bossBiteCooldownToken;
			StopNavigation();
			ClearTarget("BossBiteCooldown");
			ResetMoveTracking();
			Singleton<DelayHelper>.Instance.DelayAction(_bossBiteCooldown, () =>
			{
				if (base.IsServer && cooldownToken == _bossBiteCooldownToken && base.IsServerSimulationActive && !_isDead)
				{
					_bossBiteCooldownActive = false;
					_hitCooldownActive = false;
					if (!StartBossReinforcementChase(bittenPlayer))
					{
						SetState(HostileCreatureState.Patrol);
					}
				}
			});
		}

		private void StartDrag(PPG_Player target)
		{
			if (!base.IsServer)
			{
				return;
			}
			if (target == null || _targetTransform == null)
			{
				SetState(HostileCreatureState.Patrol);
				return;
			}
			if (_isTargetPlayerDead)
			{
				SetState(HostileCreatureState.Patrol);
				return;
			}
			if (_nav3DAgent == null || !_nav3DAgent.enabled)
			{
				SetState(HostileCreatureState.Patrol);
				return;
			}
			_dragTakenHitCount = 0;
			_isDragging = true;
			BeginDragEscapeSession();
			_dragEndTime = Time.time + GetMinDragDuration();
			_nextDragPenaltyDamageTime = Time.time + _sharkSO.DragPenaltyDamageInterval;
			StopNavigation();
			RestoreMotionNavigationType();
			if (!HasValidDragSeat())
			{
				Log("StartDrag failed. Seat[0] is missing or invalid.", LogType.Warning);
				ForceEndDrag(escapeAfter: true, "MissingSeat");
				return;
			}
			target.IsTrapped = true;
			target.ClientAssignParentConstraintSeatRpc(base.NetworkObject.ToReference(), 0, _dragSessionId);
			GlobalChatManager.BroadcastPlayerTrappedFromServer(this, target, "chat.trap.shark", "a shark");
			ChangeAnim(CreatureAnim.Move);
			if (_currentSeaLayer != null && _currentSeaArea == null)
			{
				_currentSeaArea = _currentSeaLayer.GetRandomSeaArea();
			}
			if (_currentSeaArea == null && !HasExternalPathProvider())
			{
				Log("StartDrag failed. No SeaArea or external path provider.", LogType.Warning);
				ForceEndDrag(escapeAfter: true, "NoDragPathProvider");
				return;
			}
			uint tokenAtIssue = _token;
			BeginTimedDragMove(tokenAtIssue);
			Singleton<DelayHelper>.Instance.DelayAction(GetMinDragDuration(), () =>
			{
				if (base.IsServer && IsTokenValid(tokenAtIssue) && _isDragging)
				{
					ForceEndDrag(escapeAfter: true, "DragDurationFinished");
				}
			});
			Log($"StartDrag target={target.name} Duration={GetMinDragDuration():0.00}");
		}

		private void BeginTimedDragMove(uint tokenAtIssue)
		{
			if (!base.IsServer || !_isDragging || !IsTokenValid(tokenAtIssue))
			{
				return;
			}
			if (_currentSeaArea == null && !HasExternalPathProvider())
			{
				Log("BeginTimedDragMove failed. No SeaArea.", LogType.Warning);
				ForceEndDrag(escapeAfter: true, "NoSeaArea");
				return;
			}
			if (!TryGetExternalPatrolPath(GetDragPathPointCount(), GetPatrolAllowRepeats(), out var path, out var _, out var _, out var _))
			{
				if (_currentSeaArea == null)
				{
					Log("BeginTimedDragMove failed. No SeaArea fallback.", LogType.Warning);
					ForceEndDrag(escapeAfter: true, "NoSeaAreaFallback");
					return;
				}
				path = _currentSeaArea.GetRandomPath(GetDragPathPointCount(), GetPatrolAllowRepeats());
			}
			if (path == null || path.Count == 0)
			{
				Log("BeginTimedDragMove failed. Drag path is null or empty.", LogType.Warning);
				ForceEndDrag(escapeAfter: true, "NoDragPath");
				return;
			}
			Vector3[] points = path.ToArray();
			MarkMoveIssued();
			_nav3DAgent.MoveToPoints(points, _Loop: false, GetPatrolStartFromClosest(), GetPatrolSkipUnpassableTargets(), GetPatrolReachDistance(), null, () =>
			{
				if (IsTokenValid(tokenAtIssue) && _isDragging)
				{
					ResetMoveTracking();
					if (Time.time >= _dragEndTime)
					{
						ForceEndDrag(escapeAfter: true, "DragDurationFinished");
					}
					else
					{
						BeginTimedDragMove(tokenAtIssue);
					}
				}
			}, () =>
			{
				if (IsTokenValid(tokenAtIssue) && _isDragging)
				{
					MarkMoveActive();
				}
			}, (PathfindingResult result) =>
			{
				if (IsTokenValid(tokenAtIssue) && _isDragging)
				{
					ResetMoveTracking();
					Log($"Drag pathfinding failed: {result.Result}", LogType.Warning);
					ForceEndDrag(escapeAfter: true, "DragPathFail");
				}
			}, _InterruptCurrentOrderImmediate: true);
		}

		public void EndDrag()
		{
			ForceEndDrag(escapeAfter: true, "EndDrag");
		}

		private void ForceEndDrag(bool escapeAfter, string reason)
		{
			if (!base.IsServer || !_isDragging)
			{
				return;
			}
			Log($"ForceEndDrag escapeAfter={escapeAfter} reason={reason}");
			PPG_Player targetPlayer = _targetPlayer;
			DetachDraggedTargetIfAny(reason);
			_isDragging = false;
			_dragTakenHitCount = 0;
			_dragEndTime = 0f;
			_nextDragPenaltyDamageTime = 0f;
			ResetDragEscapeState(invalidateSession: true);
			StopNavigation();
			RestoreMotionNavigationType();
			ClearTarget(reason);
			if (_isDead)
			{
				return;
			}
			if (_isBossReinforcement)
			{
				if (!StartBossReinforcementChase(targetPlayer))
				{
					SetState(HostileCreatureState.Patrol);
				}
			}
			else
			{
				BeginPostDragChaseCooldown(reason);
				SetState((!escapeAfter) ? HostileCreatureState.Patrol : HostileCreatureState.Escape);
			}
		}

		protected override void OnDetectedEnter(Collider other)
		{
			if (CanInitiateAggressiveChase() && !_bossBiteCooldownActive && !_postAttackChaseCooldownActive && !_postDragChaseCooldownActive)
			{
				base.OnDetectedEnter(other);
			}
		}

		protected override void OnDetectedExit(Collider other)
		{
			if (!_isBossReinforcement)
			{
				base.OnDetectedExit(other);
			}
		}

		public override void ServerCreatureSens(PPG_Player player, byte sensType)
		{
			if (base.IsServer && !_isDead && !_isDragging && !_bossBiteCooldownActive && CanInitiateAggressiveChase() && CanTargetPlayer(player))
			{
				if (_postDragChaseCooldownActive)
				{
					CancelPostDragChaseCooldown("ServerCreatureSens");
				}
				SetTarget(player);
				SetState(HostileCreatureState.Chase);
			}
		}

		private void OnTailAttackDetected(Collider other)
		{
			if (base.IsServer && base.IsServerSimulationActive && _state == HostileCreatureState.Patrol && !_isDead && !_isDragging && !_hitCooldownActive && !_postAttackChaseCooldownActive && !_postDragChaseCooldownActive && !_bossBiteCooldownActive && CanUseTailKnockback() && TryResolvePlayer(other, out var player) && CanTargetPlayer(player) && !player.IsInCage)
			{
				SetTarget(player);
				_attackMode = SharkAttackMode.TailKnockback;
				SetState(HostileCreatureState.Attack);
			}
		}

		private void SetTailAttackSensorActive(bool active)
		{
			if (_tailAttackSensor != null)
			{
				_tailAttackSensor.SetActive(active);
			}
		}

		private static float GetClipDuration(AnimationClip clip, float fallbackDuration)
		{
			if (!(clip != null))
			{
				return Mathf.Max(0.05f, fallbackDuration);
			}
			return Mathf.Max(0.05f, clip.length);
		}

		private void CancelPostDragChaseCooldown(string reason)
		{
			if (_postDragChaseCooldownActive)
			{
				_postDragChaseCooldownActive = false;
				_postDragChaseCooldownToken++;
				Log("Post drag chase cooldown cancelled. Reason=" + reason);
			}
		}

		private void BeginPostDragChaseCooldown(string reason)
		{
			if (!base.IsServer)
			{
				return;
			}
			float postDragChaseCooldown = _sharkSO.PostDragChaseCooldown;
			if (postDragChaseCooldown <= 0f)
			{
				return;
			}
			_postDragChaseCooldownActive = true;
			_postDragChaseCooldownToken++;
			uint tokenAtIssue = _postDragChaseCooldownToken;
			Log($"Post drag chase cooldown started. Duration={postDragChaseCooldown:0.00} Reason={reason}");
			Singleton<DelayHelper>.Instance.DelayAction(postDragChaseCooldown, () =>
			{
				if (base.IsServer && tokenAtIssue == _postDragChaseCooldownToken)
				{
					_postDragChaseCooldownActive = false;
					Log("Post drag chase cooldown finished.");
				}
			});
		}

		private void DetachDraggedTargetIfAny(string reason)
		{
			if (_targetPlayer == null)
			{
				Log("Detach skipped. Target is null. Reason=" + reason, LogType.Warning);
				return;
			}
			if (!_targetPlayer.IsSpawned)
			{
				Log("Detach skipped. Target is not spawned. Target=" + _targetPlayer.name + " Reason=" + reason, LogType.Warning);
				return;
			}
			_targetPlayer.IsTrapped = false;
			_targetPlayer.ClientDeAssignParentConstraintRpc();
		}

		protected override void StartEscape()
		{
			RestoreMotionNavigationType();
			base.StartEscape();
		}

		public override bool ServerTryBeginLayerRetreat(float speedMultiplier, out float estimatedDuration)
		{
			if (_isBossReinforcement)
			{
				estimatedDuration = 0f;
				return false;
			}
			return base.ServerTryBeginLayerRetreat(speedMultiplier, out estimatedDuration);
		}

		protected override void PrepareForLayerRetreat()
		{
			if (_isDragging)
			{
				ForceEndDrag(escapeAfter: false, "LayerRetreat");
			}
			_postDragChaseCooldownActive = false;
			_postDragChaseCooldownToken++;
			_postAttackChaseCooldownActive = false;
			_postAttackChaseCooldownToken++;
			_bossBiteCooldownActive = false;
			_bossBiteCooldownToken++;
			_attackHitConsumed = false;
			_tailHitPlayerIds.Clear();
			RestoreMotionNavigationType();
			SetTailAttackSensorActive(active: false);
			base.PrepareForLayerRetreat();
		}

		protected override void OnLayerRetreatStarted()
		{
			SetTailAttackSensorActive(active: false);
		}

		protected override void OnLayerRetreatCancelled()
		{
			SetSensorActive(CanInitiateAggressiveChase());
			SetTailAttackSensorActive(CanUseTailKnockback());
		}

		protected override void StartDead()
		{
			_debugChaseUntilAttack = false;
			_bossBiteCooldownActive = false;
			_bossBiteCooldownToken++;
			CleanupDragWithoutStateChange("Dead");
			RestoreMotionNavigationType();
			SetTailAttackSensorActive(active: false);
			base.StartDead();
		}

		private void CleanupDragWithoutStateChange(string reason)
		{
			if (_isDragging)
			{
				Log("CleanupDragWithoutStateChange reason=" + reason);
				DetachDraggedTargetIfAny(reason);
				_isDragging = false;
				_dragTakenHitCount = 0;
				_dragEndTime = 0f;
				_nextDragPenaltyDamageTime = 0f;
				ResetDragEscapeState(invalidateSession: true);
				StopNavigation();
				RestoreMotionNavigationType();
				ClearTarget(reason);
			}
		}

		protected override void OnTargetDrowned(PPG_Player target)
		{
			if (_isDragging)
			{
				ForceEndDrag(escapeAfter: false, "TargetDrowned");
			}
			else
			{
				base.OnTargetDrowned(target);
			}
		}

		protected override void RaiseTargetChangedEvent(PPG_Player oldTarget, PPG_Player newTarget)
		{
			base.RaiseTargetChangedEvent(oldTarget, newTarget);
			if (base.IsServer && !(base.NetworkObject == null))
			{
				ulong networkObjectId = base.NetworkObject.NetworkObjectId;
				if (oldTarget != null && oldTarget.IsSpawned)
				{
					oldTarget.ClientSetSharkTargetScreenVfxRpc(networkObjectId, active: false);
				}
				if (newTarget != null && newTarget.IsSpawned)
				{
					newTarget.ClientSetSharkTargetScreenVfxRpc(networkObjectId, active: true);
				}
			}
		}

		protected override void OnHostileHit(PPG_Player hitter, byte hitCount)
		{
			ChangeAnim(CreatureAnim.Hit);
			if (_isDragging)
			{
				if (_isBossReinforcement)
				{
					return;
				}
				_dragTakenHitCount++;
				Log($"Drag hit count {_dragTakenHitCount}/{GetDragBreakHitAmount()}");
				if (_dragTakenHitCount >= GetDragBreakHitAmount())
				{
					ChangeAnim(CreatureAnim.CriticalHit);
					ForceEndDrag(escapeAfter: true, "DragBreakByHits");
					return;
				}
			}
			Log($"Harpoon damage {_harpoonDamageTaken}/{GetHarpoonDurability()} Hits={hitCount}");
		}

		protected override bool ShouldEscapeFromHarpoonDamage()
		{
			if (_isBossReinforcement)
			{
				return false;
			}
			if (_isDragging)
			{
				return false;
			}
			return base.ShouldEscapeFromHarpoonDamage();
		}

		private bool StartBossReinforcementChase(PPG_Player excludedPlayer = null)
		{
			if (!base.IsServer || Singleton<PlayersManager>.Instance == null)
			{
				return false;
			}
			PPG_Player pPG_Player = null;
			float num = float.MaxValue;
			foreach (PPG_Player value in Singleton<PlayersManager>.Instance.Players.Values)
			{
				if (!(value == excludedPlayer) && CanTargetPlayer(value))
				{
					float sqrMagnitude = (value.transform.position - base.transform.position).sqrMagnitude;
					if (!(sqrMagnitude >= num))
					{
						pPG_Player = value;
						num = sqrMagnitude;
					}
				}
			}
			if (pPG_Player == null && CanTargetPlayer(excludedPlayer))
			{
				pPG_Player = excludedPlayer;
			}
			if (pPG_Player == null)
			{
				return false;
			}
			SetTarget(pPG_Player);
			SetState(HostileCreatureState.Chase);
			return true;
		}

		private bool HasValidDragSeat()
		{
			if (_seats != null && _seats.Count > 0 && _seats[0] != null)
			{
				return _seats[0].AttachPoint != null;
			}
			return false;
		}

		private void RestoreMotionNavigationType()
		{
			if (!(_nav3DAgent == null) && _baseMotionTypeCached)
			{
				_nav3DAgent.Config.MotionNavigationType = _baseMotionNavigationType;
			}
		}

		private byte GetDamageAmount()
		{
			if (!(_hostileSO != null))
			{
				return 10;
			}
			return _hostileSO.DamageAmount;
		}

		private byte GetTailDamageAmount()
		{
			return (byte)Mathf.Clamp(Mathf.FloorToInt((float)(int)GetDamageAmount() * _sharkSO.TailDamageMultiplier), 1, 255);
		}

		private void ApplyTailDamage(PPG_Player target)
		{
			if (target != null && target.DivingEquipment != null)
			{
				target.DivingEquipment.ServerConsumeOxygenRpc(GetTailDamageAmount(), withFeedback: true);
			}
		}

		private void TickDragPenaltyDamage()
		{
			if (!(_targetPlayer == null) && !(Time.time < _nextDragPenaltyDamageTime))
			{
				_nextDragPenaltyDamageTime = Time.time + _sharkSO.DragPenaltyDamageInterval;
				if (_targetPlayer.DivingEquipment != null)
				{
					_targetPlayer.DivingEquipment.ServerConsumeOxygenRpc(_sharkSO.DragPenaltyDamageAmount, withFeedback: true);
				}
			}
		}

		public bool ServerTryRegisterDragEscapePress(PPG_Player player, uint dragSessionId)
		{
			if (!base.IsServer || !_isDragging || _isDead || player == null || player != _targetPlayer || dragSessionId == 0 || dragSessionId != _dragSessionId)
			{
				return false;
			}
			double timeAsDouble = Time.timeAsDouble;
			if (timeAsDouble - _lastDragEscapePressTime < (double)GetDragEscapeMinimumPressInterval())
			{
				return false;
			}
			_lastDragEscapePressTime = timeAsDouble;
			_dragEscapeProgress = Mathf.Min(GetDragEscapeRequiredProgress(), _dragEscapeProgress + GetDragEscapeProgressPerPress());
			if (_dragEscapeProgress < GetDragEscapeRequiredProgress())
			{
				ChangeAnim(CreatureAnim.Hit);
				return true;
			}
			ChangeAnim(CreatureAnim.CriticalHit);
			ForceEndDrag(escapeAfter: true, "DragEscapeCompleted");
			return true;
		}

		private void TickDragEscapeProgress()
		{
			if (!(_dragEscapeProgress <= 0f))
			{
				_dragEscapeProgress = Mathf.Max(0f, _dragEscapeProgress - GetDragEscapeProgressDecayPerSecond() * Time.deltaTime);
			}
		}

		private void BeginDragEscapeSession()
		{
			_dragSessionId++;
			if (_dragSessionId == 0)
			{
				_dragSessionId = 1u;
			}
			_dragEscapeProgress = 0f;
			_lastDragEscapePressTime = double.NegativeInfinity;
		}

		private void ResetDragEscapeState(bool invalidateSession)
		{
			if (invalidateSession)
			{
				_dragSessionId++;
				if (_dragSessionId == 0)
				{
					_dragSessionId = 1u;
				}
			}
			_dragEscapeProgress = 0f;
			_lastDragEscapePressTime = double.NegativeInfinity;
		}

		private float GetDragEscapeRequiredProgress()
		{
			if (!(_sharkSO != null))
			{
				return 1f;
			}
			return Mathf.Max(0.01f, _sharkSO.DragEscapeRequiredProgress);
		}

		private float GetDragEscapeProgressPerPress()
		{
			if (!(_sharkSO != null))
			{
				return 0.14f;
			}
			return Mathf.Max(0.01f, _sharkSO.DragEscapeProgressPerPress);
		}

		private float GetDragEscapeProgressDecayPerSecond()
		{
			if (!(_sharkSO != null))
			{
				return 0.55f;
			}
			return Mathf.Max(0f, _sharkSO.DragEscapeProgressDecayPerSecond);
		}

		private float GetDragEscapeMinimumPressInterval()
		{
			if (!(_sharkSO != null))
			{
				return 0.08f;
			}
			return Mathf.Max(0.01f, _sharkSO.DragEscapeMinimumPressInterval);
		}

		private float GetMinDragDuration()
		{
			return _sharkSO.MinDragDuration;
		}

		private byte GetDragBreakHitAmount()
		{
			if (!(_hostileSO != null))
			{
				return 2;
			}
			return _hostileSO.DragBreakHitAmount;
		}

		protected override void ApplyCurrentMovementSpeed()
		{
			if (_isDragging && _nav3DAgent != null && _nav3DAgent.enabled)
			{
				_nav3DAgent.Config.Speed = GetDragSpeed();
			}
			else
			{
				base.ApplyCurrentMovementSpeed();
			}
		}

		private bool CanInitiateAggressiveChase()
		{
			if (!_isBossReinforcement)
			{
				if (_sharkSO != null)
				{
					return _sharkSO.TargetingMode == SharkTargetingMode.Aggressive;
				}
				return false;
			}
			return true;
		}

		protected override bool CanTargetPlayer(PPG_Player player)
		{
			if (!base.CanTargetPlayer(player))
			{
				return false;
			}
			if (player.IsTrapped)
			{
				if (_isDragging)
				{
					return player == _targetPlayer;
				}
				return false;
			}
			return true;
		}

		private bool CanUseBiteDrag()
		{
			if (!_isBossReinforcement)
			{
				return HasAttackCapability(SharkAttackCapabilities.BiteDrag);
			}
			return true;
		}

		private bool CanUseCageStrike()
		{
			if (!_isBossReinforcement)
			{
				return HasAttackCapability(SharkAttackCapabilities.CageStrike);
			}
			return true;
		}

		private bool CanUseTailKnockback()
		{
			if (_isBossReinforcement)
			{
				return true;
			}
			if (_sharkSO != null && _sharkSO.TargetingMode != SharkTargetingMode.Passive)
			{
				return HasAttackCapability(SharkAttackCapabilities.TailKnockback);
			}
			return false;
		}

		private bool HasAttackCapability(SharkAttackCapabilities capability)
		{
			if (_sharkSO != null)
			{
				return (_sharkSO.AttackCapabilities & capability) == capability;
			}
			return false;
		}

		protected override void ApplyAnim(CreatureAnim anim)
		{
			if (_animator == null)
			{
				Log("Animator is null.", LogType.Warning);
				return;
			}
			switch (anim)
			{
			case CreatureAnim.Idle:
				ResetLocomotionAnimFlags();
				break;
			case CreatureAnim.Move:
				ResetLocomotionAnimFlags();
				break;
			case CreatureAnim.Sprint:
				ResetLocomotionAnimFlags();
				_animator.SetBool("Run", value: true);
				break;
			case CreatureAnim.AttackSwim:
				ResetLocomotionAnimFlags();
				_animator.SetBool("AttackSwim", value: true);
				break;
			case CreatureAnim.Attack:
				ResetLocomotionAnimFlags();
				_animator.SetTrigger("Attack");
				break;
			case CreatureAnim.TailAttack:
				ResetLocomotionAnimFlags();
				_animator.SetTrigger("TailAttack");
				break;
			case CreatureAnim.CageAttack:
				ResetLocomotionAnimFlags();
				_animator.SetTrigger("CageAttack");
				break;
			case CreatureAnim.Hit:
				_animator.SetTrigger("Hit");
				break;
			case CreatureAnim.CriticalHit:
				_animator.SetTrigger("CriticalHit");
				break;
			case CreatureAnim.Dead:
				ResetLocomotionAnimFlags();
				ResetCombatAnimTriggers();
				_animator.SetBool("Dead", value: true);
				_animator.Update(0f);
				break;
			case CreatureAnim.Struggle:
			case CreatureAnim.SpecialStart:
			case CreatureAnim.SpecialEnd:
			case (CreatureAnim)11:
				break;
			}
		}

		private void ResetLocomotionAnimFlags()
		{
			if (!(_animator == null))
			{
				_animator.SetBool("Run", value: false);
				_animator.SetBool("AttackSwim", value: false);
			}
		}

		private void ResetCombatAnimTriggers()
		{
			if (!(_animator == null))
			{
				_animator.ResetTrigger("Attack");
				_animator.ResetTrigger("TailAttack");
				_animator.ResetTrigger("CageAttack");
				_animator.ResetTrigger("Hit");
				_animator.ResetTrigger("CriticalHit");
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3829176547u, __rpc_handler_3829176547, "ClientApplyTailKnockbackRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3829176547(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out Vector3 value2);
				RpcParams ext = rpcParams.Ext;
				((SharkCreature)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SharkCreature)target).ClientApplyTailKnockbackRpc(value, value2, ext);
				((SharkCreature)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "SharkCreature";
		}
	}
}
