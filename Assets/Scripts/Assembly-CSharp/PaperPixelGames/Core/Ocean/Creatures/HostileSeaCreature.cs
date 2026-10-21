using System.Collections.Generic;
using Nav3D.API;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	[RequireComponent(typeof(Nav3DAgent))]
	public abstract class HostileSeaCreature : SeaCreature, IAnimSphereHitReceiver
	{
		[Header("Hostile Refs")]
		[SerializeField]
		protected Nav3DAgent _nav3DAgent;

		[SerializeField]
		protected CreatureSensor _creatureSensor;

		protected HostileCreatureState _state;

		protected uint _token;

		protected HostileSO _hostileSO;

		protected PPG_Player _targetPlayer;

		protected PPG_Player _lastDamageDealer;

		protected Transform _targetTransform;

		protected bool _isDead;

		protected bool _hitCooldownActive;

		protected bool _isTargetPlayerDead;

		protected byte _hitCount;

		protected int _harpoonDamageTaken;

		protected int _harpoonDamageSinceEscape;

		protected bool _moveOrderIssued;

		protected bool _hasActiveMoveOrder;

		protected bool _isMoving;

		protected bool _isHitSlowed;

		private uint _hitSlowToken;

		private bool _isLayerRetreatComplete;

		private bool _layerRetreatFallbackAttempted;

		private float _layerRetreatSpeedMultiplier = 3f;

		private Vector3 _layerRetreatTarget;

		private EventBinding<PlayerDrownedEvent> _playerDrownedBinding;

		public HostileCreatureState State => _state;

		public PPG_Player TargetPlayer => _targetPlayer;

		public bool IsDead => _isDead;

		public bool HasTarget => _targetPlayer != null;

		public bool CanReceiveAnimSphereHit => base.IsServer;

		public override bool IsServerLayerRetreatComplete
		{
			get
			{
				if (_state == HostileCreatureState.LayerRetreat)
				{
					return _isLayerRetreatComplete;
				}
				return true;
			}
		}

		protected override bool TryGetScanIndicatorIconColorOverride(out Color color)
		{
			color = Color.red;
			return true;
		}

		public void ReceiveAnimSphereHit(Collider hitCollider)
		{
			Bite(hitCollider);
		}

		protected override void CacheComponents()
		{
			base.CacheComponents();
			if (_nav3DAgent == null)
			{
				TryGetComponent<Nav3DAgent>(out _nav3DAgent);
			}
			if (_creatureSensor == null)
			{
				TryGetComponent<CreatureSensor>(out _creatureSensor);
			}
		}

		private void Update()
		{
			if (base.IsServer && _isServerSimulationActive)
			{
				ServerTick();
			}
		}

		protected override void OnServerInitialized()
		{
			base.OnServerInitialized();
			ConfigureSensor();
			TryCacheHostileData();
		}

		protected virtual bool TryCacheHostileData()
		{
			_hostileSO = null;
			if (_creatureSO == null)
			{
				Log("Hostile data cache failed. CreatureSO is null.", LogType.Warning);
				return false;
			}
			if (!(_creatureSO is HostileSO hostileSO))
			{
				Log("Hostile data cache failed. CreatureSO is not HostileSO. Current=" + _creatureSO.GetType().Name, LogType.Error);
				return false;
			}
			_hostileSO = hostileSO;
			return true;
		}

		protected override void OnClientVisualInitialized()
		{
			base.OnClientVisualInitialized();
			ConfigureSensor();
		}

		protected override void OnServerCreatureSpawned()
		{
			base.OnServerCreatureSpawned();
			if (_creatureSensor != null)
			{
				_creatureSensor.DetectedEnter += OnDetectedEnter;
				_creatureSensor.DetectedExit += OnDetectedExit;
			}
			_playerDrownedBinding = new EventBinding<PlayerDrownedEvent>(OnPlayerDrowned);
			EventBus<PlayerDrownedEvent>.Register(_playerDrownedBinding);
		}

		protected override void OnServerCreatureDespawned()
		{
			if (_creatureSensor != null)
			{
				_creatureSensor.DetectedEnter -= OnDetectedEnter;
				_creatureSensor.DetectedExit -= OnDetectedExit;
			}
			EventBus<PlayerDrownedEvent>.Deregister(_playerDrownedBinding);
			base.OnServerCreatureDespawned();
		}

		protected override void OnServerSimulationStarted()
		{
			base.OnServerSimulationStarted();
			if (!TryCacheHostileData())
			{
				SetSensorActive(active: false);
				Log("Hostile simulation start blocked. Missing HostileSO data.", LogType.Error);
				return;
			}
			if (_nav3DAgent != null)
			{
				_nav3DAgent.enabled = true;
			}
			SetSensorActive(active: true);
			_isDead = false;
			_hitCooldownActive = false;
			_isTargetPlayerDead = false;
			_hitCount = 0;
			_harpoonDamageTaken = 0;
			_harpoonDamageSinceEscape = 0;
			_isHitSlowed = false;
			_hitSlowToken++;
			_isLayerRetreatComplete = false;
			_layerRetreatFallbackAttempted = false;
			ClearTarget("SimulationStarted");
			ResetMoveTracking();
			_token++;
			SetState(HostileCreatureState.Patrol);
			Log("Hostile simulation started.");
		}

		protected override void OnServerSimulationStopped()
		{
			StopNavigation();
			SetSensorActive(active: false);
			ClearTarget("SimulationStopped");
			ResetMoveTracking();
			_hitCooldownActive = false;
			_isTargetPlayerDead = false;
			_isHitSlowed = false;
			_hitSlowToken++;
			_isLayerRetreatComplete = false;
			_layerRetreatFallbackAttempted = false;
			_token++;
			SetState(HostileCreatureState.Inactive);
			Log("Hostile simulation stopped.");
			base.OnServerSimulationStopped();
		}

		protected virtual void ServerTick()
		{
			switch (_state)
			{
			case HostileCreatureState.Inactive:
				break;
			case HostileCreatureState.Patrol:
				TickPatrol();
				break;
			case HostileCreatureState.Chase:
				TickChase();
				break;
			case HostileCreatureState.Attack:
				TickAttack();
				break;
			case HostileCreatureState.Escape:
				break;
			case HostileCreatureState.Dead:
				break;
			case HostileCreatureState.LayerRetreat:
				break;
			}
		}

		protected virtual void TickPatrol()
		{
			if (!(_nav3DAgent == null) && _nav3DAgent.enabled && (!(_currentSeaLayer == null) || HasExternalPathProvider()) && !_moveOrderIssued && !_hasActiveMoveOrder)
			{
				StartPatrol();
			}
		}

		protected virtual void TickChase()
		{
			if (!IsTargetValidForChase())
			{
				ClearTarget("InvalidChaseTarget");
				SetState(HostileCreatureState.Patrol);
				return;
			}
			float attackTriggerDistance = GetAttackTriggerDistance();
			if ((_targetTransform.position - base.transform.position).sqrMagnitude <= attackTriggerDistance * attackTriggerDistance)
			{
				SetState(HostileCreatureState.Attack);
			}
		}

		protected virtual void TickAttack()
		{
			if (!IsTargetValidForAttack())
			{
				ClearTarget("InvalidAttackTarget");
				SetState(HostileCreatureState.Patrol);
			}
		}

		protected virtual void SetState(HostileCreatureState nextState)
		{
			if (base.IsServer && (!_isDead || nextState == HostileCreatureState.Dead || nextState == HostileCreatureState.Inactive) && _state != nextState)
			{
				HostileCreatureState state = _state;
				_state = nextState;
				_token++;
				ResetMoveTracking();
				Log($"State {state} -> {_state} Token={_token}");
				switch (_state)
				{
				case HostileCreatureState.Inactive:
					StopNavigation();
					break;
				case HostileCreatureState.Patrol:
					StopNavigation();
					ClearTarget("EnterPatrol");
					StartPatrol();
					break;
				case HostileCreatureState.Chase:
					StartChase();
					break;
				case HostileCreatureState.Attack:
					StopNavigation();
					StartAttack();
					break;
				case HostileCreatureState.Escape:
					StopNavigation();
					ClearTarget("EnterEscape");
					StartEscape();
					break;
				case HostileCreatureState.Dead:
					StopNavigation();
					StartDead();
					break;
				case HostileCreatureState.LayerRetreat:
					StopNavigation();
					StartLayerRetreat();
					break;
				}
				OnStateChanged(state, _state);
			}
		}

		protected virtual void OnStateChanged(HostileCreatureState previousState, HostileCreatureState newState)
		{
		}

		protected bool IsTokenValid(uint tokenAtIssue)
		{
			if (base.IsServer)
			{
				return tokenAtIssue == _token;
			}
			return false;
		}

		protected virtual void StartPatrol()
		{
			if (_nav3DAgent == null || !_nav3DAgent.enabled || (_currentSeaLayer == null && !HasExternalPathProvider()))
			{
				return;
			}
			bool loop = GetPatrolLoop();
			bool startFromClosest = GetPatrolStartFromClosest();
			float reachDistance = GetPatrolReachDistance();
			if ((!TryGetExternalPatrolPath(GetPatrolPathPointCount(), GetPatrolAllowRepeats(), out var path, out loop, out startFromClosest, out reachDistance) && !TryBuildLayerPatrolPath(GetPatrolPathPointCount(), GetPatrolAllowRepeats(), out path)) || path == null || path.Count == 0)
			{
				return;
			}
			SetPatrolSpeed();
			ChangeAnim(CreatureAnim.Move);
			Vector3[] points = path.ToArray();
			uint tokenAtIssue = _token;
			MarkMoveIssued();
			_nav3DAgent.MoveToPoints(points, loop, startFromClosest, GetPatrolSkipUnpassableTargets(), reachDistance, null, () =>
			{
				if (IsTokenValid(tokenAtIssue))
				{
					ResetMoveTracking();
					OnPatrolMoveFinished();
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
					ResetMoveTracking();
					Log($"Patrol pathfinding failed: {result.Result}", LogType.Warning);
					OnPatrolPathfindingFailed(result);
				}
			}, _InterruptCurrentOrderImmediate: true);
		}

		protected virtual bool TryBuildLayerPatrolPath(int pointCount, bool allowRepeats, out List<Vector3> path)
		{
			path = null;
			if (_currentSeaLayer == null)
			{
				return false;
			}
			if (_currentSeaArea == null)
			{
				_currentSeaArea = _currentSeaLayer.GetRandomSeaArea();
			}
			if (_currentSeaArea == null)
			{
				return false;
			}
			path = _currentSeaArea.GetRandomPath(pointCount, allowRepeats);
			if (path != null)
			{
				return path.Count > 0;
			}
			return false;
		}

		protected virtual void OnPatrolMoveFinished()
		{
		}

		protected virtual void OnPatrolPathfindingFailed(PathfindingResult result)
		{
		}

		protected virtual void StartChase()
		{
			if (_nav3DAgent == null || !_nav3DAgent.enabled)
			{
				SetState(HostileCreatureState.Patrol);
				return;
			}
			if (!IsTargetValidForChase())
			{
				ClearTarget("StartChaseInvalidTarget");
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
					ResetMoveTracking();
					ClearTarget("ChaseFail");
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
					ResetMoveTracking();
					Log($"Chase pathfinding failed: {result.Result}", LogType.Warning);
					SetState(HostileCreatureState.Patrol);
				}
			}, _InterruptCurrentOrderImmediate: true);
			Singleton<DelayHelper>.Instance.DelayAction(GetChaseDuration(), () =>
			{
				if (base.IsServer && IsTokenValid(tokenAtIssue) && _state == HostileCreatureState.Chase)
				{
					ClearTarget("ChaseTimeout");
					SetState(HostileCreatureState.Patrol);
				}
			});
			OnChaseStarted(_targetPlayer);
		}

		protected virtual void StartAttack()
		{
			if (!IsTargetValidForAttack())
			{
				ClearTarget("StartAttackInvalidTarget");
				SetState(HostileCreatureState.Patrol);
			}
			else
			{
				ChangeAnim(CreatureAnim.Attack);
				OnAttackStarted(_targetPlayer);
			}
		}

		protected virtual void StartEscape()
		{
			_harpoonDamageSinceEscape = 0;
			if (_nav3DAgent == null || !_nav3DAgent.enabled)
			{
				SetState(HostileCreatureState.Patrol);
				return;
			}
			SetEscapeSpeed();
			ChangeAnim(CreatureAnim.Sprint);
			if (!TryGetExternalEscapeTarget(out var target))
			{
				if (_currentSeaLayer == null)
				{
					SetState(HostileCreatureState.Patrol);
					return;
				}
				if (_currentSeaArea == null)
				{
					_currentSeaArea = _currentSeaLayer.GetRandomSeaArea();
				}
				if (_currentSeaArea == null)
				{
					SetState(HostileCreatureState.Patrol);
					return;
				}
				target = _currentSeaArea.GetFarthestPointFrom(base.transform.position);
			}
			uint tokenAtIssue = _token;
			MarkMoveIssued();
			_nav3DAgent.MoveToPoints(new Vector3[1] { target }, _Loop: false, _StartFromClosest: true, _SkipUnpassableTargets: true, GetPatrolReachDistance(), null, () =>
			{
				if (IsTokenValid(tokenAtIssue))
				{
					ResetMoveTracking();
					ClearTarget("EscapeFinished");
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
					ResetMoveTracking();
					Log($"Escape pathfinding failed: {result.Result}", LogType.Warning);
					ClearTarget("EscapePathFail");
					SetState(HostileCreatureState.Patrol);
				}
			}, _InterruptCurrentOrderImmediate: true);
			OnEscapeStarted();
		}

		protected virtual void StartDead()
		{
			_isDead = true;
			_hitCooldownActive = false;
			_isTargetPlayerDead = false;
			ClearTarget("Dead");
			ChangeAnim(CreatureAnim.Dead);
			OnDeadStarted();
			RaiseCreatureDiedEvent(_lastDamageDealer);
			Singleton<GameProgressManager>.Instance?.ServerGrantProgressionXp(ProgressionXpSource.HostileSeaCreatureKilled, (_hostileSO != null) ? _hostileSO.Id : null, (_lastDamageDealer != null) ? new ulong?(_lastDamageDealer.OwnerClientId) : ((ulong?)null));
			float deathDespawnDelay = GetDeathDespawnDelay();
			if (deathDespawnDelay <= 0f)
			{
				DespawnAfterDeath();
				return;
			}
			uint tokenAtIssue = _token;
			Singleton<DelayHelper>.Instance.DelayAction(deathDespawnDelay, () =>
			{
				if (base.IsServer && IsTokenValid(tokenAtIssue) && _isDead)
				{
					DespawnAfterDeath();
				}
			});
		}

		protected virtual void DespawnAfterDeath()
		{
			if (base.IsServer && base.NetworkObject != null && base.NetworkObject.IsSpawned)
			{
				base.NetworkObject.Despawn();
			}
		}

		protected virtual void OnDetectedEnter(Collider other)
		{
			if (base.IsServer && _isServerSimulationActive && !_isDead && !_hitCooldownActive && !(_targetPlayer != null) && TryResolvePlayer(other, out var player) && CanTargetPlayer(player))
			{
				SetTarget(player);
				SetState(HostileCreatureState.Chase);
			}
		}

		protected virtual void OnDetectedExit(Collider other)
		{
			if (base.IsServer && _state == HostileCreatureState.Chase && !(_targetPlayer == null))
			{
				PPG_Player pPG_Player = ((other != null) ? other.GetComponentInParent<PPG_Player>() : null);
				if (!(pPG_Player == null) && !(pPG_Player != _targetPlayer))
				{
					ClearTarget("DetectedExit");
					SetState(HostileCreatureState.Patrol);
				}
			}
		}

		public virtual void Bite(Collider other)
		{
			if (!base.IsServer || _state != HostileCreatureState.Attack || _hitCooldownActive)
			{
				return;
			}
			if (!TryResolvePlayer(other, out var player))
			{
				ClearTarget("BiteNoPlayer");
				SetState(HostileCreatureState.Patrol);
				return;
			}
			if (!CanTargetPlayer(player))
			{
				ClearTarget("BiteInvalidPlayer");
				SetState(HostileCreatureState.Patrol);
				return;
			}
			if (_targetPlayer != player)
			{
				SetTarget(player);
			}
			OnAttackHitPlayer(player);
			BeginHitCooldown();
		}

		protected override void OnServerHit(PPG_Player player)
		{
			ProcessServerHit(player, 1);
		}

		protected virtual void OnServerHarpoonHit(PPG_Player player, byte damage)
		{
			ProcessServerHit(player, damage);
		}

		private void ProcessServerHit(PPG_Player player, byte damage)
		{
			if (!base.IsServer || _isDead)
			{
				return;
			}
			_lastDamageDealer = player;
			base.OnServerHit(player);
			_hitCount++;
			_harpoonDamageTaken += Mathf.Max(1, damage);
			_harpoonDamageSinceEscape += Mathf.Max(1, damage);
			if (ShouldDieFromHarpoonDamage())
			{
				SetState(HostileCreatureState.Dead);
				return;
			}
			BeginHitSlow();
			OnHostileHit(player, _hitCount);
			if (ShouldEscapeFromHarpoonDamage())
			{
				SetState(HostileCreatureState.Escape);
			}
		}

		protected virtual void OnPlayerDrowned(PlayerDrownedEvent eventData)
		{
			if (base.IsServer && !(eventData._drownedPlayer != _targetPlayer))
			{
				_isTargetPlayerDead = true;
				OnTargetDrowned(_targetPlayer);
				ClearTarget("TargetDrowned");
				SetState(HostileCreatureState.Patrol);
			}
		}

		protected virtual void SetTarget(PPG_Player player)
		{
			PPG_Player targetPlayer = _targetPlayer;
			_targetPlayer = player;
			_targetTransform = ResolveTargetTransform(player);
			_isTargetPlayerDead = false;
			if (targetPlayer != _targetPlayer)
			{
				RaiseTargetChangedEvent(targetPlayer, _targetPlayer);
			}
			Log("Target set: " + player.name);
		}

		protected virtual void ClearTarget(string reason)
		{
			PPG_Player targetPlayer = _targetPlayer;
			if (_targetPlayer != null)
			{
				Log("ClearTarget reason=" + reason + " target=" + _targetPlayer.name);
			}
			else
			{
				Log("ClearTarget reason=" + reason + " target=<null>");
			}
			_targetPlayer = null;
			_targetTransform = null;
			_isTargetPlayerDead = false;
			if (targetPlayer != null)
			{
				RaiseTargetChangedEvent(targetPlayer, null);
			}
		}

		protected virtual Transform ResolveTargetTransform(PPG_Player player)
		{
			if (player == null)
			{
				return null;
			}
			return player.transform;
		}

		protected virtual bool TryResolvePlayer(Collider other, out PPG_Player player)
		{
			player = null;
			if (other == null)
			{
				return false;
			}
			if (!other.CompareTag("Player"))
			{
				return false;
			}
			player = other.GetComponent<PPG_Player>();
			return player != null;
		}

		protected virtual bool CanTargetPlayer(PPG_Player player)
		{
			if (player == null)
			{
				return false;
			}
			if (!player.IsSpawned)
			{
				return false;
			}
			if (!player.IsUnderWater)
			{
				return false;
			}
			if (player.DivingEquipment.IsKnocked)
			{
				return false;
			}
			if (_currentSeaLayer != null && player.CurrentSeaLayer != _currentSeaLayer)
			{
				return false;
			}
			return true;
		}

		public void ServerReceiveHarpoonHit(PPG_Player player, byte damage)
		{
			if (base.IsServer && !(player == null) && !_isDead)
			{
				OnServerHarpoonHit(player, (byte)Mathf.Max(1, damage));
			}
		}

		protected virtual bool IsTargetValidForChase()
		{
			if (_targetPlayer != null && _targetTransform != null)
			{
				return CanTargetPlayer(_targetPlayer);
			}
			return false;
		}

		protected virtual bool IsTargetValidForAttack()
		{
			return IsTargetValidForChase();
		}

		protected virtual void BeginHitCooldown()
		{
			_hitCooldownActive = true;
			float chaseCooldown = GetChaseCooldown();
			if (chaseCooldown <= 0f)
			{
				_hitCooldownActive = false;
				return;
			}
			uint tokenAtIssue = _token;
			Singleton<DelayHelper>.Instance.DelayAction(chaseCooldown, () =>
			{
				if (base.IsServer && IsTokenValid(tokenAtIssue))
				{
					_hitCooldownActive = false;
				}
			});
		}

		protected virtual void StopNavigation()
		{
			if (_nav3DAgent != null && _nav3DAgent.enabled)
			{
				_nav3DAgent.Stop();
			}
			ResetMoveTracking();
		}

		protected virtual void ResetMoveTracking()
		{
			_moveOrderIssued = false;
			_hasActiveMoveOrder = false;
			_isMoving = false;
		}

		protected virtual void MarkMoveIssued()
		{
			_moveOrderIssued = true;
			_hasActiveMoveOrder = false;
			_isMoving = false;
		}

		protected virtual void MarkMoveActive()
		{
			_moveOrderIssued = true;
			_hasActiveMoveOrder = true;
			_isMoving = true;
		}

		protected virtual void ConfigureSensor()
		{
			if (!(_creatureSensor == null) && !(_creatureSO == null))
			{
				_creatureSensor.SetSensor(_creatureSO);
			}
		}

		protected virtual void SetSensorActive(bool active)
		{
			if (!(_creatureSensor == null))
			{
				_creatureSensor.SetActive(active);
			}
		}

		protected virtual void SetPatrolSpeed()
		{
			if (!(_nav3DAgent == null))
			{
				_nav3DAgent.Config.Speed = GetPatrolSpeed();
			}
		}

		protected virtual void SetChaseSpeed()
		{
			if (!(_nav3DAgent == null))
			{
				_nav3DAgent.Config.Speed = GetChaseSpeed();
			}
		}

		protected virtual void SetEscapeSpeed()
		{
			if (!(_nav3DAgent == null))
			{
				_nav3DAgent.Config.Speed = GetEscapeSpeed();
			}
		}

		protected virtual int GetPatrolPathPointCount()
		{
			if (!(_hostileSO != null))
			{
				return 4;
			}
			return Mathf.Max(1, _hostileSO.PatrolPathPointCount);
		}

		protected virtual bool GetPatrolAllowRepeats()
		{
			if (_hostileSO != null)
			{
				return _hostileSO.PatrolAllowRepeats;
			}
			return false;
		}

		protected virtual bool GetPatrolLoop()
		{
			if (_hostileSO != null)
			{
				return _hostileSO.PatrolLoop;
			}
			return false;
		}

		protected virtual bool GetPatrolStartFromClosest()
		{
			if (!(_hostileSO == null))
			{
				return _hostileSO.PatrolStartFromClosest;
			}
			return true;
		}

		protected virtual bool GetPatrolSkipUnpassableTargets()
		{
			if (!(_hostileSO == null))
			{
				return _hostileSO.PatrolSkipUnpassableTargets;
			}
			return true;
		}

		protected virtual float GetPatrolReachDistance()
		{
			if (!(_hostileSO != null))
			{
				return 0.35f;
			}
			return Mathf.Max(0.01f, _hostileSO.PatrolReachDistance);
		}

		protected virtual float GetPatrolSpeed()
		{
			float num = ((_hostileSO != null) ? _hostileSO.Speed : 2f);
			return Mathf.Max(0.01f, num * GetCurrentSpeedMultiplier());
		}

		protected virtual float GetChaseSpeed()
		{
			float num = ((_hostileSO != null) ? _hostileSO.ChaseSpeed : 4f);
			return Mathf.Max(0.01f, num * GetCurrentSpeedMultiplier());
		}

		protected virtual float GetEscapeSpeed()
		{
			float num = ((_hostileSO != null) ? _hostileSO.EscapeSpeed : 4f);
			return Mathf.Max(0.01f, num * GetCurrentSpeedMultiplier());
		}

		protected virtual float GetDragSpeed()
		{
			float num = ((_hostileSO != null) ? _hostileSO.DragSpeed : 3f);
			return Mathf.Max(0.01f, num * GetCurrentSpeedMultiplier());
		}

		protected virtual int GetDragPathPointCount()
		{
			if (!(_hostileSO != null))
			{
				return 4;
			}
			return Mathf.Max(1, _hostileSO.DragPathPointCount);
		}

		protected virtual float GetFollowReachDistance()
		{
			if (!(_hostileSO != null))
			{
				return 0.9f;
			}
			return Mathf.Max(0.05f, _hostileSO.FollowReachDistance);
		}

		protected virtual float GetAttackTriggerDistance()
		{
			float followReachDistance = GetFollowReachDistance();
			float b = ((_hostileSO != null) ? _hostileSO.AttackTriggerDistance : 1.5f);
			return Mathf.Max(followReachDistance, b);
		}

		protected virtual float GetTargetOffsetUpdate()
		{
			if (!(_hostileSO != null))
			{
				return 0.2f;
			}
			return Mathf.Max(0.01f, _hostileSO.TargetOffsetUpdate);
		}

		protected virtual float GetChaseDuration()
		{
			if (!(_hostileSO != null))
			{
				return 8f;
			}
			return Mathf.Max(0.1f, _hostileSO.ChaseDuration);
		}

		protected virtual float GetChaseCooldown()
		{
			if (!(_hostileSO != null))
			{
				return 2f;
			}
			return Mathf.Max(0f, _hostileSO.ChaseCooldown);
		}

		protected virtual float GetDeathDespawnDelay()
		{
			if (!(_hostileSO != null))
			{
				return 10f;
			}
			return Mathf.Max(0f, _hostileSO.DeathDespawnDelay);
		}

		protected virtual int GetEscapeHarpoonDamage()
		{
			if (!(_hostileSO != null))
			{
				return 3;
			}
			return _hostileSO.EscapeHarpoonDamage;
		}

		protected virtual int GetHarpoonDurability()
		{
			if (!(_hostileSO != null))
			{
				return 9;
			}
			return _hostileSO.HarpoonDurability;
		}

		protected virtual bool ShouldEscapeFromHarpoonDamage()
		{
			int escapeHarpoonDamage = GetEscapeHarpoonDamage();
			if (escapeHarpoonDamage > 0)
			{
				return _harpoonDamageSinceEscape >= escapeHarpoonDamage;
			}
			return false;
		}

		protected virtual bool ShouldDieFromHarpoonDamage()
		{
			int harpoonDurability = GetHarpoonDurability();
			if (harpoonDurability > 0)
			{
				return _harpoonDamageTaken >= harpoonDurability;
			}
			return false;
		}

		protected virtual void BeginHitSlow()
		{
			float hitSlowDuration = GetHitSlowDuration();
			float hitSlowMultiplier = GetHitSlowMultiplier();
			if (hitSlowDuration <= 0f || hitSlowMultiplier >= 1f)
			{
				return;
			}
			_isHitSlowed = true;
			_hitSlowToken++;
			ApplyCurrentMovementSpeed();
			uint tokenAtIssue = _hitSlowToken;
			Singleton<DelayHelper>.Instance.DelayAction(hitSlowDuration, () =>
			{
				if (base.IsServer && tokenAtIssue == _hitSlowToken)
				{
					_isHitSlowed = false;
					ApplyCurrentMovementSpeed();
				}
			});
		}

		protected virtual void ApplyCurrentMovementSpeed()
		{
			if (!(_nav3DAgent == null) && _nav3DAgent.enabled)
			{
				switch (_state)
				{
				case HostileCreatureState.Patrol:
					SetPatrolSpeed();
					break;
				case HostileCreatureState.Chase:
					SetChaseSpeed();
					break;
				case HostileCreatureState.Escape:
					SetEscapeSpeed();
					break;
				case HostileCreatureState.Attack:
					break;
				}
			}
		}

		protected virtual float GetCurrentSpeedMultiplier()
		{
			if (!_isHitSlowed)
			{
				return 1f;
			}
			return GetHitSlowMultiplier();
		}

		protected virtual float GetHitSlowDuration()
		{
			if (!(_hostileSO != null))
			{
				return 0.75f;
			}
			return Mathf.Max(0f, _hostileSO.HitSlowDuration);
		}

		protected virtual float GetHitSlowMultiplier()
		{
			if (!(_hostileSO != null))
			{
				return 0.45f;
			}
			return Mathf.Clamp(_hostileSO.HitSlowMultiplier, 0.05f, 1f);
		}

		public override bool ServerTryBeginLayerRetreat(float speedMultiplier, out float estimatedDuration)
		{
			estimatedDuration = 0f;
			if (!base.IsServer || !_isServerSimulationActive || _isDead || base.IsStandalone || _currentSeaLayer == null)
			{
				return false;
			}
			if (_state == HostileCreatureState.Inactive || _state == HostileCreatureState.Dead || _state == HostileCreatureState.LayerRetreat)
			{
				return false;
			}
			if (!TryGetServerHomePosition(out _layerRetreatTarget))
			{
				return false;
			}
			PrepareForLayerRetreat();
			_isHitSlowed = false;
			_hitSlowToken++;
			float num = Mathf.Max(0.01f, GetPatrolSpeed() * Mathf.Max(1f, speedMultiplier));
			estimatedDuration = Vector3.Distance(base.transform.position, _layerRetreatTarget) / num;
			_isLayerRetreatComplete = false;
			_layerRetreatFallbackAttempted = false;
			_layerRetreatSpeedMultiplier = Mathf.Max(1f, speedMultiplier);
			SetSensorActive(active: false);
			ClearTarget("LayerRetreat");
			ServerSetLayerRetreatPresentationActive(active: true);
			SetState(HostileCreatureState.LayerRetreat);
			return true;
		}

		public override void ServerCancelLayerRetreat()
		{
			if (base.IsServer && _state == HostileCreatureState.LayerRetreat)
			{
				_isLayerRetreatComplete = false;
				_layerRetreatFallbackAttempted = false;
				ServerSetLayerRetreatPresentationActive(active: false);
				SetSensorActive(active: true);
				SetState(HostileCreatureState.Patrol);
				OnLayerRetreatCancelled();
			}
		}

		public override void ServerForceCompleteLayerRetreat()
		{
			if (base.IsServer && _state == HostileCreatureState.LayerRetreat)
			{
				StopNavigation();
				if (TryGetServerHomePosition(out var homePosition))
				{
					base.transform.position = homePosition;
				}
				_isLayerRetreatComplete = true;
			}
		}

		protected virtual void PrepareForLayerRetreat()
		{
			StopNavigation();
		}

		protected virtual void OnLayerRetreatStarted()
		{
		}

		protected virtual void OnLayerRetreatCancelled()
		{
		}

		private void StartLayerRetreat()
		{
			if (_nav3DAgent == null || !_nav3DAgent.enabled)
			{
				ServerForceCompleteLayerRetreat();
				return;
			}
			SetSensorActive(active: false);
			_nav3DAgent.Config.Speed = Mathf.Max(0.01f, GetPatrolSpeed() * GetLayerRetreatSpeedMultiplier());
			ChangeAnim(CreatureAnim.Sprint);
			OnLayerRetreatStarted();
			IssueLayerRetreatMove(_layerRetreatTarget);
		}

		private void IssueLayerRetreatMove(Vector3 target)
		{
			uint tokenAtIssue = _token;
			MarkMoveIssued();
			_nav3DAgent.MoveToPoints(new Vector3[1] { target }, _Loop: false, _StartFromClosest: true, _SkipUnpassableTargets: true, GetPatrolReachDistance(), null, () =>
			{
				if (IsTokenValid(tokenAtIssue) && _state == HostileCreatureState.LayerRetreat)
				{
					StopNavigation();
					_isLayerRetreatComplete = true;
					ChangeAnim(CreatureAnim.Move);
				}
			}, () =>
			{
				if (IsTokenValid(tokenAtIssue) && _state == HostileCreatureState.LayerRetreat)
				{
					MarkMoveActive();
				}
			}, (PathfindingResult result) =>
			{
				if (IsTokenValid(tokenAtIssue) && _state == HostileCreatureState.LayerRetreat)
				{
					ResetMoveTracking();
					if (!_layerRetreatFallbackAttempted && _currentSeaArea != null && _currentSeaArea.HasAnyPoint())
					{
						_layerRetreatFallbackAttempted = true;
						_layerRetreatTarget = _currentSeaArea.GetRandomPoint();
						IssueLayerRetreatMove(_layerRetreatTarget);
					}
					else
					{
						Log($"Layer retreat pathfinding failed: {result.Result}", LogType.Warning);
						ServerForceCompleteLayerRetreat();
					}
				}
			}, _InterruptCurrentOrderImmediate: true);
		}

		protected float GetLayerRetreatSpeedMultiplier()
		{
			return Mathf.Max(1f, _layerRetreatSpeedMultiplier);
		}

		protected bool HasExternalPathProvider()
		{
			ICreaturePathProvider component;
			return TryGetComponent<ICreaturePathProvider>(out component);
		}

		protected bool TryGetExternalPatrolPath(int pointCount, bool allowRepeats, out List<Vector3> path, out bool loop, out bool startFromClosest, out float reachDistance)
		{
			path = null;
			loop = GetPatrolLoop();
			startFromClosest = GetPatrolStartFromClosest();
			reachDistance = GetPatrolReachDistance();
			if (!TryGetComponent<ICreaturePathProvider>(out var component))
			{
				return false;
			}
			if (!component.TryGetPatrolPath(pointCount, allowRepeats, out path))
			{
				return false;
			}
			if (component.TryGetPatrolLoop(out var loop2))
			{
				loop = loop2;
			}
			if (component.TryGetPatrolStartFromClosest(out var startFromClosest2))
			{
				startFromClosest = startFromClosest2;
			}
			if (component.TryGetPatrolReachDistance(out var reachDistance2))
			{
				reachDistance = reachDistance2;
			}
			if (path != null)
			{
				return path.Count > 0;
			}
			return false;
		}

		private bool TryGetExternalEscapeTarget(out Vector3 target)
		{
			target = default;
			if (!TryGetComponent<ICreaturePathProvider>(out var component))
			{
				return false;
			}
			return component.TryGetFarthestPointFrom(base.transform.position, out target);
		}

		protected virtual void OnChaseStarted(PPG_Player target)
		{
		}

		protected virtual void OnAttackStarted(PPG_Player target)
		{
		}

		protected virtual void OnAttackHitPlayer(PPG_Player target)
		{
		}

		protected virtual void OnEscapeStarted()
		{
		}

		protected virtual void OnDeadStarted()
		{
		}

		protected virtual void OnTargetDrowned(PPG_Player target)
		{
		}

		protected virtual void OnHostileHit(PPG_Player hitter, byte hitCount)
		{
		}

		protected virtual void RaiseTargetChangedEvent(PPG_Player oldTarget, PPG_Player newTarget)
		{
			EventBus<HostileCreatureTargetChangedEvent>.Raise(new HostileCreatureTargetChangedEvent(this, oldTarget, newTarget));
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
			return "HostileSeaCreature";
		}
	}
}
