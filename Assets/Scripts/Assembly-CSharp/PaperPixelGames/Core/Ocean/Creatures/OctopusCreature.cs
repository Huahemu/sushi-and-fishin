using System;
using Nav3D.API;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	[RequireComponent(typeof(Nav3DAgent))]
	public sealed class OctopusCreature : CatchableSeaCreature, IAnimSphereHitReceiver
	{
		[Header("Octopus Refs")]
		[SerializeField]
		private Animator _animator;

		[Header("Embedded Graphic")]
		[SerializeField]
		private GameObject _embeddedGraphicRoot;

		[Header("Debug")]
		[SerializeField]
		private bool _logOctopus;

		private readonly NetworkVariable<bool> _canStartChallenge = new NetworkVariable<bool>(value: false);

		private OctopusState _state;

		private uint _octopusToken;

		private PPG_Player _targetPlayer;

		private Transform _targetTransform;

		private float _baseNavSpeed = -1f;

		private int _hitCount;

		private bool _patrolOrderIssued;

		private bool _isFollowingTarget;

		private bool _isPreparingAttack;

		private bool _isAttackCommitted;

		private bool _attackHitConsumed;

		private bool _chaseCooldownActive;

		private uint _chaseCooldownToken;

		private bool _isLayerRetreatComplete;

		private bool _layerRetreatFallbackAttempted;

		private float _layerRetreatSpeedMultiplier = 3f;

		private Vector3 _layerRetreatTarget;

		protected override bool UseEmbeddedGraphic => true;

		protected override GameObject EmbeddedGraphicRoot => _embeddedGraphicRoot;

		private OctopusSO OctopusData => _creatureSO as OctopusSO;

		public bool IsReadyForChallenge => _canStartChallenge.Value;

		public bool CanReceiveAnimSphereHit => base.IsServer;

		private float AttackDistance
		{
			get
			{
				OctopusSO octopusData = OctopusData;
				if (!(octopusData != null))
				{
					return 1.5f;
				}
				return Mathf.Max(0.1f, octopusData.AttackDistance);
			}
		}

		private float AttackExitDistance
		{
			get
			{
				OctopusSO octopusData = OctopusData;
				float b = ((octopusData != null) ? octopusData.AttackExitDistanceOffset : 1f);
				return AttackDistance + Mathf.Max(0.01f, b);
			}
		}

		private float FollowReachDistance
		{
			get
			{
				OctopusSO octopusData = OctopusData;
				float b = ((octopusData != null) ? octopusData.MinReachDistance : 0.35f);
				return Mathf.Max(Mathf.Max(0.01f, b), AttackDistance * 0.8f);
			}
		}

		private float LoseTargetDistance
		{
			get
			{
				OctopusSO octopusData = OctopusData;
				if (!(octopusData != null))
				{
					return AttackExitDistance + 5f;
				}
				return Mathf.Max(AttackExitDistance + 1f, octopusData.LoseTargetDistance);
			}
		}

		public override bool IsServerLayerRetreatComplete
		{
			get
			{
				if (_state == OctopusState.LayerRetreat)
				{
					return _isLayerRetreatComplete;
				}
				return true;
			}
		}

		protected override void CacheComponents()
		{
			base.CacheComponents();
			if (_animator == null)
			{
				_animator = GetComponentInChildren<Animator>(includeInactive: true);
			}
			if (_embeddedGraphicRoot == null && _animator != null)
			{
				_embeddedGraphicRoot = _animator.gameObject;
			}
			CacheBaseNavSpeed();
		}

		protected override void OnGraphicSpawned(GameObject graphic)
		{
			base.OnGraphicSpawned(graphic);
			CacheGraphicReferences(graphic);
			ResetAnimatorAliveState();
		}

		protected override void OnClientVisualInitialized()
		{
			base.OnClientVisualInitialized();
			if (!(_activeGraphic == null))
			{
				CacheGraphicReferences(_activeGraphic);
				ResetAnimatorAliveState();
			}
		}

		private void CacheGraphicReferences(GameObject graphic)
		{
			if (!(graphic == null))
			{
				if (_animator == null)
				{
					_animator = graphic.GetComponentInChildren<Animator>(includeInactive: true);
				}
				if (_embeddedGraphicRoot == null)
				{
					_embeddedGraphicRoot = graphic;
				}
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (base.IsServer && !HasValidOctopusData())
			{
				base.enabled = false;
				return;
			}
			CacheBaseNavSpeed();
			ResetAnimatorAliveState();
		}

		protected override void OnServerSimulationStarted()
		{
			base.OnServerSimulationStarted();
			if (HasValidOctopusData())
			{
				_hitCount = 0;
				_canStartChallenge.Value = false;
				ClearTarget("SimulationStarted");
				ResetAttackFlags();
				ResetOctopusMovementState();
				_octopusToken++;
				_chaseCooldownActive = false;
				_chaseCooldownToken++;
				_isLayerRetreatComplete = false;
				_layerRetreatFallbackAttempted = false;
				SetState(OctopusState.Patrol, forceReEnter: true);
				OLog("Simulation started.");
			}
		}

		protected override void OnServerSimulationStopped()
		{
			StopOctopusNavigation();
			ClearTarget("SimulationStopped");
			ResetAttackFlags();
			ResetOctopusMovementState();
			_canStartChallenge.Value = false;
			_octopusToken++;
			_chaseCooldownActive = false;
			_chaseCooldownToken++;
			_isLayerRetreatComplete = false;
			_layerRetreatFallbackAttempted = false;
			_state = OctopusState.Inactive;
			OLog("Simulation stopped.");
			base.OnServerSimulationStopped();
		}

		protected override void ServerTick()
		{
			switch (_state)
			{
			case OctopusState.Inactive:
				SetState(OctopusState.Patrol);
				break;
			case OctopusState.Patrol:
				TickPatrol();
				break;
			case OctopusState.Chase:
				TickChase();
				break;
			case OctopusState.Attack:
				TickAttack();
				break;
			case OctopusState.Stunned:
				break;
			case OctopusState.LayerRetreat:
				break;
			}
		}

		protected override void OnHookedStateChanged(bool hooked)
		{
			base.OnHookedStateChanged(hooked);
			if (base.IsServer)
			{
				if (hooked)
				{
					StopOctopusNavigation();
					SetSensorActive(active: false);
					ClearTarget("Hooked");
					ResetAttackFlags();
					ResetOctopusMovementState();
				}
				else
				{
					SetSensorActive(active: true);
				}
			}
		}

		public override bool CanReceiveHit(PPG_Player player)
		{
			if (player != null && _canStartChallenge.Value)
			{
				return base.CanReceiveHit(player);
			}
			return false;
		}

		public bool CanStartCatchChallenge(PPG_Player player)
		{
			return CanReceiveHit(player);
		}

		public void ServerReceiveOctopusHarpoonHit(PPG_Player player)
		{
			if (base.IsServer && !(player == null))
			{
				HandleOctopusHarpoonHit(player);
			}
		}

		internal void ServerReceiveTridentHit(PPG_Player player, int harpoonHitCount)
		{
			if (base.IsServer && !(player == null))
			{
				HandleOctopusHarpoonHit(player, harpoonHitCount, forceHarpoonReload: false);
			}
		}

		protected override void OnServerHit(PPG_Player player)
		{
			if (base.IsServer && !(player == null))
			{
				if (_canStartChallenge.Value)
				{
					base.OnServerHit(player);
				}
				else
				{
					HandleOctopusHarpoonHit(player);
				}
			}
		}

		protected override void OnServerCatchableHit(PPG_Player player)
		{
			if (base.IsServer && !(player == null))
			{
				if (!_canStartChallenge.Value)
				{
					HandleOctopusHarpoonHit(player);
					return;
				}
				StopOctopusNavigation();
				SetSensorActive(active: false);
				ClearTarget("ChallengeStarted");
				ResetAttackFlags();
				base.OnServerCatchableHit(player);
			}
		}

		private void HandleOctopusHarpoonHit(PPG_Player player, int harpoonHitCount = 1, bool forceHarpoonReload = true)
		{
			if (!HasValidOctopusData() || base.IsHooked.Value || base.CatchState == CatchableCreatureState.Caught)
			{
				return;
			}
			RaiseCreatureHitEvent(player);
			if (_state == OctopusState.Stunned || _canStartChallenge.Value)
			{
				OLog("Hit received while already stunned. Waiting for challenge.");
				return;
			}
			OctopusSO octopusData = OctopusData;
			if (!(octopusData == null))
			{
				_hitCount += Mathf.Max(1, harpoonHitCount);
				OLog($"Hit count {_hitCount}/{octopusData.HitsToStun}");
				ResetAttackFlags();
				ChangeAnim(CreatureAnim.Hit);
				if (forceHarpoonReload)
				{
					ClientForceHarpoonReload(player);
				}
				if (_hitCount >= octopusData.HitsToStun)
				{
					EnterStun();
					return;
				}
				SetTarget(player);
				SetState(OctopusState.Chase);
			}
		}

		private void EnterStun()
		{
			if (!base.IsServer)
			{
				return;
			}
			OctopusSO octopusData = OctopusData;
			if (octopusData == null)
			{
				return;
			}
			_canStartChallenge.Value = true;
			StopOctopusNavigation();
			ClearTarget("EnterStun");
			ResetAttackFlags();
			ResetOctopusMovementState();
			SetState(OctopusState.Stunned);
			uint tokenAtIssue = _octopusToken;
			Singleton<DelayHelper>.Instance.DelayAction(octopusData.StunDuration, () =>
			{
				if (base.IsServer && IsOctopusTokenValid(tokenAtIssue) && _state == OctopusState.Stunned && _canStartChallenge.Value)
				{
					ExitStun();
				}
			});
		}

		private void ExitStun()
		{
			if (base.IsServer)
			{
				_canStartChallenge.Value = false;
				_hitCount = 0;
				ResetAttackFlags();
				ChangeAnim(CreatureAnim.Move);
				SetState(OctopusState.Patrol);
			}
		}

		private void StartStunned()
		{
			StopOctopusNavigation();
			ResetOctopusMovementState();
			ResetAttackFlags();
			ChangeAnim(CreatureAnim.CriticalHit);
		}

		protected override void TickPatrol()
		{
			if (_nav3DAgent == null || !_nav3DAgent.enabled)
			{
				return;
			}
			if (_patrolOrderIssued)
			{
				if (!IsPathInProgress())
				{
					_patrolOrderIssued = false;
				}
			}
			else if (!IsPathInProgress())
			{
				IssuePatrolMove();
				_patrolOrderIssued = true;
			}
		}

		private void TickChase()
		{
			if (!TryValidateLiveTarget(out var distance))
			{
				SetState(OctopusState.Patrol);
			}
			else if (distance >= LoseTargetDistance)
			{
				ClearTarget("ChaseTooFar");
				SetState(OctopusState.Patrol);
			}
			else if (distance <= AttackDistance)
			{
				SetState(OctopusState.Attack);
			}
			else
			{
				MaintainChaseFollow();
			}
		}

		private void StartChase()
		{
			if (_nav3DAgent == null || !_nav3DAgent.enabled)
			{
				SetState(OctopusState.Patrol);
				return;
			}
			if (!TryValidateLiveTarget(out var _))
			{
				SetState(OctopusState.Patrol);
				return;
			}
			_isFollowingTarget = false;
			SetChaseSpeed();
			ChangeAnim(CreatureAnim.AttackSwim);
			MaintainChaseFollow(forceRefresh: true);
		}

		private void MaintainChaseFollow(bool forceRefresh = false)
		{
			if (_nav3DAgent == null || !_nav3DAgent.enabled || !IsTargetValid() || (_isFollowingTarget && !forceRefresh))
			{
				return;
			}
			uint tokenAtIssue = _octopusToken;
			_isFollowingTarget = true;
			_hasActiveMoveOrder = false;
			_nav3DAgent.FollowTarget(_targetTransform, _FollowContinuously: true, GetFollowTargetUpdateInterval(), FollowReachDistance, () =>
			{
				if (IsOctopusTokenValid(tokenAtIssue))
				{
					_hasActiveMoveOrder = false;
				}
			}, () =>
			{
				if (IsOctopusTokenValid(tokenAtIssue))
				{
					_isFollowingTarget = false;
					ClearTarget("FollowFail");
					SetState(OctopusState.Patrol);
				}
			}, () =>
			{
				if (IsOctopusTokenValid(tokenAtIssue))
				{
					_hasActiveMoveOrder = true;
				}
			}, (PathfindingResult result) =>
			{
				if (IsOctopusTokenValid(tokenAtIssue))
				{
					_hasActiveMoveOrder = false;
					_isFollowingTarget = false;
					ClearTarget("PathfindingFail");
					SetState(OctopusState.Patrol);
				}
			}, _InterruptCurrentOrderImmediate: true);
		}

		private void TickAttack()
		{
			if (!TryValidateLiveTarget(out var distance))
			{
				SetState(OctopusState.Patrol);
				return;
			}
			if (distance >= LoseTargetDistance)
			{
				ClearTarget("AttackTooFar");
				SetState(OctopusState.Patrol);
				return;
			}
			if (distance > AttackExitDistance)
			{
				SetState(OctopusState.Chase);
				return;
			}
			StopOctopusNavigation();
			FaceTargetWhileAttacking();
			if (!_isAttackCommitted)
			{
				if (!_isPreparingAttack)
				{
					_isPreparingAttack = true;
					ChangeAnim(CreatureAnim.AttackSwim);
				}
				if (!(distance > AttackDistance) && IsFacingTarget(GetAttackFacingAngleThreshold()))
				{
					CommitAttack();
				}
			}
		}

		private void StartAttack()
		{
			if (_nav3DAgent == null || !_nav3DAgent.enabled)
			{
				SetState(OctopusState.Patrol);
				return;
			}
			if (!TryValidateLiveTarget(out var _))
			{
				SetState(OctopusState.Patrol);
				return;
			}
			_isFollowingTarget = false;
			_isPreparingAttack = true;
			_isAttackCommitted = false;
			_attackHitConsumed = false;
			StopOctopusNavigation();
			ChangeAnim(CreatureAnim.AttackSwim);
		}

		private void CommitAttack()
		{
			StopOctopusNavigation();
			_isPreparingAttack = false;
			_isAttackCommitted = true;
			_attackHitConsumed = false;
			ChangeAnim(CreatureAnim.Attack);
			uint tokenAtIssue = _octopusToken;
			float attackTimeout = GetAttackTimeout();
			Singleton<DelayHelper>.Instance.DelayAction(attackTimeout, () =>
			{
				if (base.IsServer && IsOctopusTokenValid(tokenAtIssue) && _state == OctopusState.Attack)
				{
					EndAttackAndReturnToPatrol("AttackTimeout");
				}
			});
		}

		private void EndAttackAndReturnToPatrol(string reason)
		{
			if (base.IsServer)
			{
				OLog("EndAttackAndReturnToPatrol reason=" + reason);
				BeginPostAttackChaseCooldown(reason);
				ClearTarget(reason);
				ResetAttackFlags();
				ResetOctopusMovementState();
				SetState(OctopusState.Patrol);
			}
		}

		private void BeginPostAttackChaseCooldown(string reason)
		{
			if (!base.IsServer)
			{
				return;
			}
			float postAttackChaseCooldown = GetPostAttackChaseCooldown();
			if (postAttackChaseCooldown <= 0f)
			{
				return;
			}
			_chaseCooldownActive = true;
			_chaseCooldownToken++;
			uint tokenAtIssue = _chaseCooldownToken;
			OLog($"Post attack chase cooldown started. Duration={postAttackChaseCooldown:0.00} Reason={reason}");
			Singleton<DelayHelper>.Instance.DelayAction(postAttackChaseCooldown, () =>
			{
				if (base.IsServer && tokenAtIssue == _chaseCooldownToken)
				{
					_chaseCooldownActive = false;
					OLog("Post attack chase cooldown finished.");
				}
			});
		}

		private bool CanStartChaseFromDetection(PPG_Player player)
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (!base.IsServerSimulationActive)
			{
				return false;
			}
			if (_chaseCooldownActive)
			{
				return false;
			}
			if (_state == OctopusState.Stunned)
			{
				return false;
			}
			if (_targetPlayer != null)
			{
				return false;
			}
			if (!CanAttackPlayer(player))
			{
				return false;
			}
			return true;
		}

		public void ReceiveAnimSphereHit(Collider hitCollider)
		{
			if (base.IsServer)
			{
				PPG_Player player;
				if (hitCollider == null)
				{
					OLog("Attack sphere miss. Collider is null.");
				}
				else if (!TryResolvePlayer(hitCollider, out player))
				{
					OLog("Attack sphere miss. Could not resolve player from collider=" + hitCollider.name);
				}
				else
				{
					TryApplyAttackToPlayer(player);
				}
			}
		}

		private void TryApplyAttackToPlayer(PPG_Player player)
		{
			if (base.IsServer && !(player == null) && _state == OctopusState.Attack && _isAttackCommitted && !_attackHitConsumed && CanAttackPlayer(player) && !(player.Interactor == null) && !(player.Interactor.NetworkObject == null))
			{
				_attackHitConsumed = true;
				RpcParams rpcParams = base.RpcTarget.Single(player.Interactor.OwnerClientId, RpcTargetUse.Temp);
				ClientAttackTriggerPlayerRpc(player.Interactor.NetworkObject.ToReference(), rpcParams);
				OLog("Attack hit player: " + player.name);
			}
		}

		protected override void OnDetectedEnter(Collider other)
		{
			if (TryResolvePlayer(other, out var player) && CanStartChaseFromDetection(player))
			{
				SetTarget(player);
				SetState(OctopusState.Chase);
			}
		}

		protected override void OnDetectedExit(Collider other)
		{
		}

		public override void ServerCreatureSens(PPG_Player player, byte sensType)
		{
			if (CanStartChaseFromDetection(player))
			{
				SetTarget(player);
				SetState(OctopusState.Chase);
			}
		}

		protected override void ApplyMovementSpeed(bool panic)
		{
			if (!(_nav3DAgent == null))
			{
				OctopusSO octopusData = OctopusData;
				if (octopusData == null)
				{
					base.ApplyMovementSpeed(panic);
					return;
				}
				float num = ((_baseNavSpeed > 0f) ? _baseNavSpeed : Mathf.Max(0.01f, (_creatureSO != null) ? _creatureSO.MoveSpeed : 1f));
				float num2 = (panic ? octopusData.ChaseSpeedMultiplier : octopusData.PatrolSpeedMultiplier);
				_nav3DAgent.Config.Speed = Mathf.Max(0.01f, num * num2);
			}
		}

		private void SetChaseSpeed()
		{
			if (!(_nav3DAgent == null))
			{
				OctopusSO octopusData = OctopusData;
				if (octopusData == null)
				{
					base.ApplyMovementSpeed(panic: true);
					return;
				}
				float num = ((_baseNavSpeed > 0f) ? _baseNavSpeed : Mathf.Max(0.01f, (_creatureSO != null) ? _creatureSO.MoveSpeed : 1f));
				_nav3DAgent.Config.Speed = Mathf.Max(0.01f, num * octopusData.ChaseSpeedMultiplier);
			}
		}

		protected override void ApplyAnim(CreatureAnim anim)
		{
			if (_animator == null)
			{
				OLog("Animator is null.", LogType.Warning);
				return;
			}
			switch (anim)
			{
			case CreatureAnim.Idle:
			case CreatureAnim.Move:
				ResetLocomotionAnimFlags();
				_animator.SetBool("Move", value: true);
				break;
			case CreatureAnim.Sprint:
				ResetLocomotionAnimFlags();
				_animator.SetBool("Move", value: true);
				break;
			case CreatureAnim.AttackSwim:
				ResetLocomotionAnimFlags();
				_animator.SetBool("Chase", value: true);
				break;
			case CreatureAnim.Attack:
				ResetLocomotionAnimFlags();
				_animator.SetTrigger("Attack");
				break;
			case CreatureAnim.Hit:
				_animator.ResetTrigger("CriticalHit");
				_animator.SetTrigger("Hit");
				break;
			case CreatureAnim.CriticalHit:
				ResetLocomotionAnimFlags();
				_animator.ResetTrigger("Hit");
				_animator.SetTrigger("CriticalHit");
				break;
			case CreatureAnim.Dead:
				ApplyDeadAnim();
				break;
			case CreatureAnim.Struggle:
				break;
			}
		}

		private void ApplyDeadAnim()
		{
			if (!(_animator == null))
			{
				ResetLocomotionAnimFlags();
				ResetCombatAnimTriggers();
				_animator.SetBool("Dead", value: true);
				_animator.Update(0f);
			}
		}

		private void ResetAnimatorAliveState()
		{
			if (!(_animator == null))
			{
				ResetLocomotionAnimFlags();
				ResetCombatAnimTriggers();
				_animator.SetBool("Dead", value: false);
			}
		}

		private void ResetLocomotionAnimFlags()
		{
			if (!(_animator == null))
			{
				_animator.SetBool("Move", value: false);
				_animator.SetBool("Chase", value: false);
			}
		}

		private void ResetCombatAnimTriggers()
		{
			if (!(_animator == null))
			{
				_animator.ResetTrigger("Attack");
				_animator.ResetTrigger("Hit");
				_animator.ResetTrigger("CriticalHit");
			}
		}

		private void SetState(OctopusState nextState, bool forceReEnter = false)
		{
			if (base.IsServer && (_state != nextState || forceReEnter))
			{
				OctopusState state = _state;
				_octopusToken++;
				if (_state != nextState)
				{
					ResetAttackFlags();
				}
				_state = nextState;
				OLog($"State {state} -> {_state} Token={_octopusToken}");
				switch (_state)
				{
				case OctopusState.Inactive:
					StopOctopusNavigation();
					break;
				case OctopusState.Patrol:
					StopOctopusNavigation();
					StartPatrol();
					break;
				case OctopusState.Chase:
					StartChase();
					break;
				case OctopusState.Attack:
					StopOctopusNavigation();
					StartAttack();
					break;
				case OctopusState.Stunned:
					StopOctopusNavigation();
					StartStunned();
					break;
				case OctopusState.LayerRetreat:
					StopOctopusNavigation();
					StartLayerRetreat();
					break;
				}
			}
		}

		private void StartPatrol()
		{
			if (!(_nav3DAgent == null) && _nav3DAgent.enabled)
			{
				_patrolOrderIssued = false;
				IssuePatrolMove();
				_patrolOrderIssued = true;
			}
		}

		private bool IsOctopusTokenValid(uint tokenAtIssue)
		{
			if (base.IsServer)
			{
				return tokenAtIssue == _octopusToken;
			}
			return false;
		}

		private void StopOctopusNavigation()
		{
			if (_nav3DAgent != null && _nav3DAgent.enabled)
			{
				_nav3DAgent.Stop();
			}
			ResetOctopusMovementState();
		}

		private void ResetOctopusMovementState()
		{
			ResetMovementState();
			_patrolOrderIssued = false;
			_isFollowingTarget = false;
		}

		private void ResetAttackFlags()
		{
			_isPreparingAttack = false;
			_isAttackCommitted = false;
			_attackHitConsumed = false;
		}

		private void SetTarget(PPG_Player player)
		{
			_targetPlayer = player;
			_targetTransform = ((player != null) ? player.transform : null);
		}

		private void ClearTarget(string reason)
		{
			OLog("ClearTarget: " + reason);
			_targetPlayer = null;
			_targetTransform = null;
			_isFollowingTarget = false;
		}

		private bool IsTargetValid()
		{
			if (_targetPlayer != null && _targetPlayer.IsSpawned && _targetTransform != null)
			{
				if (!(_currentSeaLayer == null))
				{
					return _targetPlayer.CurrentSeaLayer == _currentSeaLayer;
				}
				return true;
			}
			return false;
		}

		public override bool ServerTryBeginLayerRetreat(float speedMultiplier, out float estimatedDuration)
		{
			estimatedDuration = 0f;
			if (!base.IsServer || !_isServerSimulationActive || base.IsStandalone || _currentSeaLayer == null || base.IsHooked.Value || base.CatchState != CatchableCreatureState.Free)
			{
				return false;
			}
			if (_state == OctopusState.Inactive || _state == OctopusState.Stunned || _state == OctopusState.LayerRetreat)
			{
				return false;
			}
			if (!TryGetServerHomePosition(out _layerRetreatTarget))
			{
				return false;
			}
			float layerRetreatSpeed = GetLayerRetreatSpeed(speedMultiplier);
			estimatedDuration = Vector3.Distance(base.transform.position, _layerRetreatTarget) / layerRetreatSpeed;
			_layerRetreatSpeedMultiplier = Mathf.Max(1f, speedMultiplier);
			_isLayerRetreatComplete = false;
			_layerRetreatFallbackAttempted = false;
			StopOctopusNavigation();
			SetSensorActive(active: false);
			ClearTarget("LayerRetreat");
			ResetAttackFlags();
			_chaseCooldownActive = false;
			_chaseCooldownToken++;
			ServerSetLayerRetreatPresentationActive(active: true);
			SetState(OctopusState.LayerRetreat);
			return true;
		}

		public override void ServerCancelLayerRetreat()
		{
			if (base.IsServer && _state == OctopusState.LayerRetreat)
			{
				_isLayerRetreatComplete = false;
				_layerRetreatFallbackAttempted = false;
				ServerSetLayerRetreatPresentationActive(active: false);
				SetSensorActive(active: true);
				SetState(OctopusState.Patrol);
			}
		}

		public override void ServerForceCompleteLayerRetreat()
		{
			if (base.IsServer && _state == OctopusState.LayerRetreat)
			{
				StopOctopusNavigation();
				if (TryGetServerHomePosition(out var homePosition))
				{
					base.transform.position = homePosition;
				}
				_isLayerRetreatComplete = true;
			}
		}

		private void StartLayerRetreat()
		{
			if (_nav3DAgent == null || !_nav3DAgent.enabled)
			{
				ServerForceCompleteLayerRetreat();
				return;
			}
			SetSensorActive(active: false);
			_nav3DAgent.Config.Speed = GetLayerRetreatSpeed(_layerRetreatSpeedMultiplier);
			ChangeAnim(CreatureAnim.AttackSwim);
			IssueLayerRetreatMove(_layerRetreatTarget);
		}

		private void IssueLayerRetreatMove(Vector3 target)
		{
			uint tokenAtIssue = _octopusToken;
			_hasActiveMoveOrder = false;
			_nav3DAgent.MoveToPoints(new Vector3[1] { target }, _Loop: false, _StartFromClosest: true, _SkipUnpassableTargets: true, FollowReachDistance, null, () =>
			{
				if (IsOctopusTokenValid(tokenAtIssue) && _state == OctopusState.LayerRetreat)
				{
					StopOctopusNavigation();
					_isLayerRetreatComplete = true;
					ChangeAnim(CreatureAnim.Move);
				}
			}, () =>
			{
				if (IsOctopusTokenValid(tokenAtIssue) && _state == OctopusState.LayerRetreat)
				{
					_hasActiveMoveOrder = true;
				}
			}, (PathfindingResult result) =>
			{
				if (IsOctopusTokenValid(tokenAtIssue) && _state == OctopusState.LayerRetreat)
				{
					ResetOctopusMovementState();
					if (!_layerRetreatFallbackAttempted && _currentSeaArea != null && _currentSeaArea.HasAnyPoint())
					{
						_layerRetreatFallbackAttempted = true;
						_layerRetreatTarget = _currentSeaArea.GetRandomPoint();
						IssueLayerRetreatMove(_layerRetreatTarget);
					}
					else
					{
						OLog($"Layer retreat pathfinding failed: {result.Result}", LogType.Warning);
						ServerForceCompleteLayerRetreat();
					}
				}
			}, _InterruptCurrentOrderImmediate: true);
		}

		private float GetLayerRetreatSpeed(float speedMultiplier)
		{
			OctopusSO octopusData = OctopusData;
			float num = ((_baseNavSpeed > 0f) ? _baseNavSpeed : Mathf.Max(0.01f, (_creatureSO != null) ? _creatureSO.MoveSpeed : 1f));
			float b = ((octopusData != null) ? octopusData.PatrolSpeedMultiplier : 1f);
			return Mathf.Max(0.01f, num * Mathf.Max(0.05f, b) * Mathf.Max(1f, speedMultiplier));
		}

		private bool TryValidateLiveTarget(out float distance)
		{
			distance = float.MaxValue;
			if (!IsTargetValid())
			{
				ClearTarget("TargetInvalid");
				return false;
			}
			if (!_targetPlayer.IsUnderWater)
			{
				ClearTarget("TargetOutOfWater");
				return false;
			}
			distance = Vector3.Distance(base.transform.position, _targetTransform.position);
			return true;
		}

		private bool TryResolvePlayer(Collider other, out PPG_Player player)
		{
			player = null;
			if (other == null)
			{
				return false;
			}
			player = other.GetComponentInParent<PPG_Player>();
			return player != null;
		}

		private bool CanAttackPlayer(PPG_Player player)
		{
			if (player != null && player.IsSpawned && player.IsUnderWater && !player.DivingEquipment.IsKnocked)
			{
				if (!(_currentSeaLayer == null))
				{
					return player.CurrentSeaLayer == _currentSeaLayer;
				}
				return true;
			}
			return false;
		}

		private bool IsFacingTarget(float maxAngle)
		{
			if (!IsTargetValid())
			{
				return false;
			}
			Vector3 vector = _targetTransform.position - base.transform.position;
			vector.y = 0f;
			if (vector.sqrMagnitude <= 0.0001f)
			{
				return true;
			}
			return Vector3.Angle(base.transform.forward, vector.normalized) <= maxAngle;
		}

		private void FaceTargetWhileAttacking()
		{
			if (IsTargetValid())
			{
				Vector3 vector = _targetTransform.position - base.transform.position;
				vector.y = 0f;
				if (!(vector.sqrMagnitude <= 0.0001f))
				{
					Quaternion b = Quaternion.LookRotation(vector.normalized, Vector3.up);
					base.transform.rotation = Quaternion.Slerp(base.transform.rotation, b, Time.deltaTime * GetAttackFaceTurnSpeed());
				}
			}
		}

		private bool HasValidOctopusData()
		{
			if (OctopusData != null)
			{
				return true;
			}
			OLog("CreatureSO is not OctopusSO. Current=" + (_creatureSO?.GetType().Name ?? "null"), LogType.Error);
			return false;
		}

		private void CacheBaseNavSpeed()
		{
			if (!(_nav3DAgent == null) && !(_baseNavSpeed > 0f))
			{
				_baseNavSpeed = Mathf.Max(0.01f, _nav3DAgent.Config.Speed);
			}
		}

		private void ClientForceHarpoonReload(PPG_Player player)
		{
			if (!(player == null) && !(player.Interactor == null) && !(player.Interactor.NetworkObject == null))
			{
				RpcParams rpcParams = base.RpcTarget.Single(player.Interactor.OwnerClientId, RpcTargetUse.Temp);
				ClientFishHitRpc(player.Interactor.NetworkObject.ToReference(), rpcParams);
			}
		}

		private float GetFollowTargetUpdateInterval()
		{
			OctopusSO octopusData = OctopusData;
			if (!(octopusData != null))
			{
				return 0.12f;
			}
			return Mathf.Max(0.01f, octopusData.FollowTargetUpdateInterval);
		}

		private float GetAttackFacingAngleThreshold()
		{
			OctopusSO octopusData = OctopusData;
			if (!(octopusData != null))
			{
				return 20f;
			}
			return Mathf.Clamp(octopusData.AttackFacingAngleThreshold, 1f, 180f);
		}

		private float GetAttackFaceTurnSpeed()
		{
			OctopusSO octopusData = OctopusData;
			if (!(octopusData != null))
			{
				return 8f;
			}
			return Mathf.Max(0.01f, octopusData.AttackFaceTurnSpeed);
		}

		private float GetAttackTimeout()
		{
			OctopusSO octopusData = OctopusData;
			if (!(octopusData != null))
			{
				return 1.25f;
			}
			return Mathf.Max(0.05f, octopusData.AttackTimeout);
		}

		private float GetPostAttackChaseCooldown()
		{
			OctopusSO octopusData = OctopusData;
			if (!(octopusData != null))
			{
				return 2f;
			}
			return Mathf.Max(0f, octopusData.PostAttackChaseCooldown);
		}

		private void OLog(string message, LogType type = LogType.Log)
		{
			if (_log && _logOctopus)
			{
				UtilityMethods.Log("[OctopusCreature] " + message, type);
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientAttackTriggerPlayerRpc(NetworkObjectReference interactorRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(410386959u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 410386959u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!interactorRef.TryGetComponent<Interactor>(out var component))
			{
				UtilityMethods.Log("[OctopusCreature] ClientAttackTriggerPlayerRpc - Interactor not found.", LogType.Warning);
				return;
			}
			OctopusSO octopusData = OctopusData;
			if (octopusData == null)
			{
				UtilityMethods.Log("[OctopusCreature] ClientAttackTriggerPlayerRpc - OctopusData is null.", LogType.Warning);
				return;
			}
			if (component.Player.DivingEquipment != null)
			{
				component.Player.DivingEquipment.ServerConsumeOxygenRpc(octopusData.DamageAmount, withFeedback: true);
			}
			if (component.Player.FirstPersonCharacter != null)
			{
				component.Player.FirstPersonCharacter.PushFromPoint(base.transform.position, octopusData.KnockbackImpulse, octopusData.KnockbackUpwardForce);
			}
			component.Player.CameraShake(ShakeIntensity.Light, ShakeDuration.Long);
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientFishHitRpc(NetworkObjectReference interactorRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1383655668u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 1383655668u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!interactorRef.TryGetComponent<Interactor>(out var component))
				{
					UtilityMethods.Log("[OctopusCreature] ClientFishHitRpc - Interactor not found.", LogType.Warning);
				}
				else if (component.Player.Harpoon != null)
				{
					component.Player.Harpoon.StartReload();
				}
			}
		}

		protected override void __initializeVariables()
		{
			if (_canStartChallenge == null)
			{
				throw new Exception("OctopusCreature._canStartChallenge cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_canStartChallenge.Initialize(this);
			__nameNetworkVariable(_canStartChallenge, "_canStartChallenge");
			NetworkVariableFields.Add(_canStartChallenge);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(410386959u, __rpc_handler_410386959, "ClientAttackTriggerPlayerRpc", RpcInvokePermission.Everyone);
			__registerRpc(1383655668u, __rpc_handler_1383655668, "ClientFishHitRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_410386959(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((OctopusCreature)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((OctopusCreature)target).ClientAttackTriggerPlayerRpc(value, ext);
				((OctopusCreature)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1383655668(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((OctopusCreature)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((OctopusCreature)target).ClientFishHitRpc(value, ext);
				((OctopusCreature)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "OctopusCreature";
		}
	}
}
