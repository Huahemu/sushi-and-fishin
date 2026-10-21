using System.Collections.Generic;
using Nav3D.API;
using PaperPixelGames.Core.Player;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	[RequireComponent(typeof(Nav3DAgent))]
	public class WanderingSeaCreature : SeaCreature
	{
		[Header("Wandering Refs")]
		[SerializeField]
		protected Nav3DAgent _nav3DAgent;

		[SerializeField]
		protected CreatureSensor _creatureSensor;

		[Header("Turn Settings")]
		[SerializeField]
		[Min(0.1f)]
		private float _normalMaxRotationPerTick = 8f;

		[SerializeField]
		[Range(0.01f, 1f)]
		private float _normalRotationVectorLerpFactor = 0.2f;

		[SerializeField]
		[Min(0.1f)]
		private float _panicMaxRotationPerTick = 30f;

		[SerializeField]
		[Range(0.01f, 1f)]
		private float _panicRotationVectorLerpFactor = 0.5f;

		[Header("Patrol Path")]
		[SerializeField]
		[Min(1f)]
		protected int _patrolPathPointCount = 4;

		[SerializeField]
		protected bool _patrolAllowRepeats;

		[SerializeField]
		protected bool _patrolLoop;

		[SerializeField]
		protected bool _patrolStartFromClosest = true;

		[SerializeField]
		protected bool _patrolSkipUnpassableTargets = true;

		[SerializeField]
		[Min(0.01f)]
		protected float _patrolReachDistance = 0.35f;

		[Header("Stuck Check")]
		[SerializeField]
		[Min(0.05f)]
		private float _stuckCheckInterval = 0.5f;

		[SerializeField]
		[Min(0.001f)]
		private float _stuckDistanceThreshold = 0.05f;

		[SerializeField]
		[Min(0.1f)]
		private float _maxStuckTime = 2f;

		[Header("Panic Direction")]
		[SerializeField]
		private bool _panicUseThreatLookDirection = true;

		[SerializeField]
		[Min(0.5f)]
		private float _panicLookDirectionDistance = 8f;

		[SerializeField]
		private bool _panicPreferCurrentArea = true;

		private WanderingCreatureState _currentState;

		private uint _stateToken;

		private Vector3 _lastPosition;

		private float _stuckTimer;

		private float _stuckCheckElapsed;

		private float _stuckDistanceThresholdSqr;

		private float _defaultNavSpeed = -1f;

		private int _threatTriggerCount;

		private bool _movementIssued;

		private bool _hasCachedThreatLookDirection;

		private Vector3 _cachedThreatLookOrigin;

		private Vector3 _cachedThreatLookDirection;

		protected bool _isMoving;

		protected bool _isPanic;

		protected bool _hasActiveMoveOrder;

		private float _serverSimulationTickInterval;

		private float _serverSimulationTickElapsed;

		public WanderingCreatureState CurrentState => _currentState;

		public bool IsMoving => _isMoving;

		public bool IsPanic => _isPanic;

		public bool HasActiveMoveOrder => _hasActiveMoveOrder;

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
			CacheRuntimeValues();
		}

		private void Update()
		{
			if (base.IsServer && _isServerSimulationActive)
			{
				_serverSimulationTickElapsed += Time.deltaTime;
				if (!(_serverSimulationTickInterval > 0f) || !(_serverSimulationTickElapsed < _serverSimulationTickInterval))
				{
					float elapsedSeconds = Mathf.Max(Time.deltaTime, _serverSimulationTickElapsed);
					_serverSimulationTickElapsed = 0f;
					ServerTick();
					UpdateStuckCheck(elapsedSeconds);
				}
			}
		}

		public void SetServerSimulationTickInterval(float intervalSeconds)
		{
			if (!base.IsServer)
			{
				return;
			}
			float num = Mathf.Max(0f, intervalSeconds);
			if (!Mathf.Approximately(_serverSimulationTickInterval, num))
			{
				_serverSimulationTickInterval = num;
				if (_serverSimulationTickInterval <= 0f)
				{
					_serverSimulationTickElapsed = 0f;
				}
			}
		}

		protected override void OnServerInitialized()
		{
			base.OnServerInitialized();
			CacheRuntimeValues();
			ConfigureSensor();
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
		}

		protected override void OnServerCreatureDespawned()
		{
			if (_creatureSensor != null)
			{
				_creatureSensor.DetectedEnter -= OnDetectedEnter;
				_creatureSensor.DetectedExit -= OnDetectedExit;
			}
			base.OnServerCreatureDespawned();
		}

		protected override void OnServerSimulationStarted()
		{
			base.OnServerSimulationStarted();
			_serverSimulationTickElapsed = 0f;
			CacheRuntimeValues();
			if (_nav3DAgent != null)
			{
				_nav3DAgent.enabled = true;
			}
			ConfigureSensor();
			SetSensorActive(active: true);
			ResetMovementState();
			_threatTriggerCount = 0;
			_isPanic = false;
			ApplyTurnSettings(panic: false);
			SwitchState(WanderingCreatureState.Patrol);
			Log("Wandering simulation started.");
		}

		public override void ServerPrepareForDespawn()
		{
			if (base.IsServer)
			{
				bool isServerSimulationActive = base.IsServerSimulationActive;
				base.ServerPrepareForDespawn();
				if (!isServerSimulationActive)
				{
					ServerStopWanderingRuntime();
				}
			}
		}

		protected override void OnServerSimulationStopped()
		{
			ServerStopWanderingRuntime();
			Log("Wandering simulation stopped.");
			base.OnServerSimulationStopped();
		}

		protected override void OnLayerActivityChanged(bool locallyVisible)
		{
			base.OnLayerActivityChanged(locallyVisible);
			if (base.IsClient && !base.IsServer)
			{
				SetClientRuntimeComponentsActive(locallyVisible);
			}
		}

		private void ServerStopWanderingRuntime()
		{
			if (base.IsServer)
			{
				_serverSimulationTickElapsed = 0f;
				if (_nav3DAgent != null && _nav3DAgent.enabled)
				{
					_nav3DAgent.Stop();
				}
				if (_nav3DAgent != null)
				{
					_nav3DAgent.enabled = false;
				}
				SetSensorActive(active: false);
				ResetMovementState();
				_threatTriggerCount = 0;
				_isPanic = false;
				_movementIssued = false;
				_hasCachedThreatLookDirection = false;
				SwitchState(WanderingCreatureState.Inactive);
			}
		}

		private void SetClientRuntimeComponentsActive(bool active)
		{
			if (_nav3DAgent != null)
			{
				if (!active && _nav3DAgent.enabled)
				{
					_nav3DAgent.Stop();
				}
				if (_nav3DAgent.enabled != active)
				{
					_nav3DAgent.enabled = active;
				}
			}
			if (_creatureSensor != null && _creatureSensor.enabled != active)
			{
				_creatureSensor.enabled = active;
			}
		}

		protected virtual void ServerTick()
		{
			switch (_currentState)
			{
			case WanderingCreatureState.Inactive:
				break;
			case WanderingCreatureState.Patrol:
				TickPatrol();
				break;
			case WanderingCreatureState.PanicMove:
				TickPanic();
				break;
			}
		}

		protected virtual void TickPatrol()
		{
			if (_threatTriggerCount > 0)
			{
				SwitchState(WanderingCreatureState.PanicMove);
			}
			else if (!_movementIssued)
			{
				if (!IsPathInProgress())
				{
					IssuePatrolMove();
				}
				_movementIssued = true;
			}
			else if (!IsPathInProgress())
			{
				_movementIssued = false;
			}
		}

		protected virtual void TickPanic()
		{
			if (!_movementIssued)
			{
				IssuePanicMove();
				_movementIssued = true;
			}
		}

		protected virtual void SwitchState(WanderingCreatureState nextState)
		{
			if (base.IsServer && _currentState != nextState)
			{
				WanderingCreatureState currentState = _currentState;
				_currentState = nextState;
				_stateToken++;
				_movementIssued = false;
				Log($"State {currentState} -> {_currentState} Token={_stateToken}");
			}
		}

		protected bool IsTokenValid(uint tokenAtIssue)
		{
			if (base.IsServer && base.IsSpawned && base.NetworkObject != null && base.NetworkObject.IsSpawned && base.IsServerSimulationActive)
			{
				return tokenAtIssue == _stateToken;
			}
			return false;
		}

		protected virtual void IssuePatrolMove()
		{
			if (!CanIssueMoveOrder())
			{
				return;
			}
			ApplyMovementSpeed(panic: false);
			ApplyTurnSettings(panic: false);
			_isPanic = false;
			bool loop = _patrolLoop;
			bool startFromClosest = _patrolStartFromClosest;
			float reachDistance = _patrolReachDistance;
			if (!TryGetExternalPatrolPath(_patrolPathPointCount, _patrolAllowRepeats, out var path, out loop, out startFromClosest, out reachDistance))
			{
				if (_currentSeaArea == null)
				{
					_currentSeaArea = ResolveFallbackSeaArea();
				}
				if (_currentSeaArea == null)
				{
					return;
				}
				path = _currentSeaArea.GetRandomPath(_patrolPathPointCount, _patrolAllowRepeats);
			}
			if (path == null || path.Count == 0)
			{
				return;
			}
			Vector3[] points = path.ToArray();
			uint tokenAtIssue = _stateToken;
			ResetMovementState();
			_nav3DAgent.MoveToPoints(points, loop, startFromClosest, _patrolSkipUnpassableTargets, reachDistance, null, () =>
			{
				if (IsTokenValid(tokenAtIssue))
				{
					ResetMovementState();
				}
			}, () =>
			{
				if (IsTokenValid(tokenAtIssue))
				{
					_hasActiveMoveOrder = true;
					_isMoving = true;
					ChangeAnim(CreatureAnim.Move);
				}
			}, (PathfindingResult result) =>
			{
				if (IsTokenValid(tokenAtIssue))
				{
					ResetMovementState();
					_movementIssued = false;
					Log($"Patrol pathfinding failed: {result.Result}", LogType.Warning);
				}
			}, _InterruptCurrentOrderImmediate: true);
		}

		protected virtual void IssuePanicMove()
		{
			if (!CanIssueMoveOrder())
			{
				_threatTriggerCount = 0;
				SwitchState(WanderingCreatureState.Patrol);
				return;
			}
			SetPanicStatus(active: true);
			if (!TryGetExternalPanicTarget(out var target) && !TryGetLookDirectionPanicTarget(out target))
			{
				if (_currentSeaLayer == null)
				{
					_threatTriggerCount = 0;
					SwitchState(WanderingCreatureState.Patrol);
					return;
				}
				if (_currentSeaArea == null)
				{
					_currentSeaArea = _currentSeaLayer.GetRandomSeaArea();
				}
				if (_currentSeaArea == null)
				{
					_threatTriggerCount = 0;
					SwitchState(WanderingCreatureState.Patrol);
					return;
				}
				if (_threatTriggerCount <= 1)
				{
					target = _currentSeaArea.GetFarthestPointFrom(base.transform.position);
				}
				else
				{
					_currentSeaArea = _currentSeaLayer.GetRandomSeaArea();
					if (_currentSeaArea == null)
					{
						CompletePanicMove();
						return;
					}
					List<Vector3> randomPath = _currentSeaArea.GetRandomPath(1);
					if (randomPath == null || randomPath.Count == 0)
					{
						_currentSeaArea = ResolveFallbackSeaArea();
						_movementIssued = false;
						return;
					}
					target = randomPath[0];
				}
			}
			uint tokenAtIssue = _stateToken;
			ResetMovementState();
			_nav3DAgent.MoveToPoints(new Vector3[1] { target }, _Loop: false, _StartFromClosest: true, _SkipUnpassableTargets: true, _patrolReachDistance, null, () =>
			{
				if (IsTokenValid(tokenAtIssue))
				{
					CompletePanicMove();
				}
			}, () =>
			{
				if (IsTokenValid(tokenAtIssue))
				{
					_hasActiveMoveOrder = true;
					_isMoving = true;
					ChangeAnim(CreatureAnim.Sprint);
				}
			}, (PathfindingResult result) =>
			{
				if (IsTokenValid(tokenAtIssue))
				{
					Log($"Panic pathfinding failed: {result.Result}", LogType.Warning);
					CompletePanicMove();
				}
			}, _InterruptCurrentOrderImmediate: true);
		}

		private void CompletePanicMove()
		{
			if (base.IsServer)
			{
				_hasCachedThreatLookDirection = false;
				_threatTriggerCount = 0;
				SetPanicStatus(active: false);
				ResetMovementState();
				SwitchState(WanderingCreatureState.Patrol);
			}
		}

		protected virtual bool CanIssueMoveOrder()
		{
			if (base.IsServer && _isServerSimulationActive && _nav3DAgent != null)
			{
				return _nav3DAgent.enabled;
			}
			return false;
		}

		public virtual bool IsPathInProgress()
		{
			return _hasActiveMoveOrder;
		}

		public void ChangePoint()
		{
			if (base.IsServer)
			{
				_threatTriggerCount = 1;
				SwitchState(WanderingCreatureState.PanicMove);
			}
		}

		public void ChangeArea()
		{
			if (base.IsServer)
			{
				_threatTriggerCount = 2;
				SwitchState(WanderingCreatureState.PanicMove);
			}
		}

		public void ServerIncrementThreat()
		{
			if (base.IsServer)
			{
				_threatTriggerCount++;
				SwitchState(WanderingCreatureState.PanicMove);
			}
		}

		public void ServerStartPanicFrom(PPG_Player threat)
		{
			if (base.IsServer && !(threat == null))
			{
				CacheThreatLookDirection(threat);
				_threatTriggerCount = Mathf.Max(1, _threatTriggerCount + 1);
				SwitchState(WanderingCreatureState.PanicMove);
			}
		}

		protected virtual void SetPanicStatus(bool active)
		{
			_isPanic = active;
			ApplyMovementSpeed(active);
			ApplyTurnSettings(active);
			ChangeAnim((!active) ? CreatureAnim.Move : CreatureAnim.Sprint);
		}

		protected virtual void OnDetectedEnter(Collider other)
		{
			if (base.IsServer && _isServerSimulationActive && !_isPanic && IsThreat(other))
			{
				CacheThreatLookDirection(other);
				ServerIncrementThreat();
			}
		}

		protected virtual void OnDetectedExit(Collider other)
		{
		}

		private void CacheThreatLookDirection(Collider other)
		{
			_hasCachedThreatLookDirection = false;
			if (!_panicUseThreatLookDirection || other == null)
			{
				return;
			}
			PPG_Player componentInParent = other.GetComponentInParent<PPG_Player>();
			if (componentInParent == null)
			{
				return;
			}
			Transform transform = ResolvePlayerLookTransform(componentInParent);
			if (!(transform == null))
			{
				Vector3 forward = transform.forward;
				forward.y = 0f;
				if (!(forward.sqrMagnitude <= 0.001f))
				{
					_cachedThreatLookOrigin = transform.position;
					_cachedThreatLookDirection = forward.normalized;
					_hasCachedThreatLookDirection = true;
				}
			}
		}

		protected virtual void CacheThreatLookDirection(PPG_Player player)
		{
			_hasCachedThreatLookDirection = false;
			if (!(player == null))
			{
				Transform transform = null;
				transform = ((!(player.RigBones != null) || player.RigBones.TpsRig == null || !(player.RigBones.TpsRig.Head != null)) ? player.transform : player.RigBones.TpsRig.Head);
				Vector3 forward = transform.forward;
				forward.y = 0f;
				if (!(forward.sqrMagnitude <= 0.001f))
				{
					_cachedThreatLookOrigin = transform.position;
					_cachedThreatLookDirection = forward.normalized;
					_hasCachedThreatLookDirection = true;
				}
			}
		}

		private Transform ResolvePlayerLookTransform(PPG_Player player)
		{
			if (player == null)
			{
				return null;
			}
			if (player.RigBones != null && player.RigBones.TpsRig != null && player.RigBones.TpsRig.Head != null)
			{
				return player.RigBones.TpsRig.Head;
			}
			return player.transform;
		}

		private bool TryGetLookDirectionPanicTarget(out Vector3 target)
		{
			target = default;
			if (!_panicUseThreatLookDirection)
			{
				return false;
			}
			if (!_hasCachedThreatLookDirection)
			{
				return false;
			}
			if (_currentSeaLayer == null)
			{
				return false;
			}
			Vector3 referencePosition = _cachedThreatLookOrigin + _cachedThreatLookDirection * _panicLookDirectionDistance;
			SeaArea seaArea = null;
			seaArea = ((!_panicPreferCurrentArea || !(_currentSeaArea != null)) ? _currentSeaLayer.GetRandomSeaArea() : _currentSeaArea);
			if (seaArea == null)
			{
				return false;
			}
			target = seaArea.GetClosestPointTo(referencePosition);
			_currentSeaArea = seaArea;
			return true;
		}

		protected virtual bool IsThreat(Collider other)
		{
			if (other == null)
			{
				return false;
			}
			if (other.CompareTag("Player"))
			{
				return true;
			}
			if (other.CompareTag("Hunter"))
			{
				return true;
			}
			return false;
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

		protected virtual void ApplyMovementSpeed(bool panic)
		{
			if (!(_nav3DAgent == null))
			{
				if (_creatureSO != null)
				{
					_nav3DAgent.Config.Speed = (panic ? Mathf.Max(0.01f, _creatureSO.PanicSpeed) : Mathf.Max(0.01f, _creatureSO.MoveSpeed));
				}
				else if (_defaultNavSpeed > 0f)
				{
					_nav3DAgent.Config.Speed = (panic ? (_defaultNavSpeed * 1.35f) : _defaultNavSpeed);
				}
			}
		}

		protected virtual SeaArea ResolveFallbackSeaArea()
		{
			if (_currentSeaLayer == null)
			{
				return null;
			}
			SeaArea seaAreaByIndex = _currentSeaLayer.GetSeaAreaByIndex(_seaAreaIndex.Value);
			if (!(seaAreaByIndex != null))
			{
				return _currentSeaLayer.GetRandomSeaArea();
			}
			return seaAreaByIndex;
		}

		protected bool TryGetExternalPatrolPath(int pointCount, bool allowRepeats, out List<Vector3> path, out bool loop, out bool startFromClosest, out float reachDistance)
		{
			path = null;
			loop = _patrolLoop;
			startFromClosest = _patrolStartFromClosest;
			reachDistance = _patrolReachDistance;
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

		private bool TryGetExternalPanicTarget(out Vector3 target)
		{
			target = default;
			if (!TryGetComponent<ICreaturePathProvider>(out var component))
			{
				return false;
			}
			if (_threatTriggerCount <= 1)
			{
				return component.TryGetFarthestPointFrom(base.transform.position, out target);
			}
			return component.TryGetRandomPoint(out target);
		}

		protected virtual void ResetMovementState()
		{
			_hasActiveMoveOrder = false;
			_isMoving = false;
			_movementIssued = false;
			_stuckTimer = 0f;
			_stuckCheckElapsed = 0f;
			_lastPosition = base.transform.position;
		}

		public override void ServerCreatureSens(PPG_Player player, byte sensType)
		{
			base.ServerCreatureSens(player, sensType);
			if (base.IsServer && _isServerSimulationActive && !_isPanic && !(player == null))
			{
				CacheThreatLookDirection(player);
				ServerIncrementThreat();
			}
		}

		private void UpdateStuckCheck(float elapsedSeconds)
		{
			if (!_hasActiveMoveOrder || _nav3DAgent == null || !_nav3DAgent.enabled)
			{
				_stuckTimer = 0f;
				_stuckCheckElapsed = 0f;
				_lastPosition = base.transform.position;
				return;
			}
			_stuckCheckElapsed += Mathf.Max(0f, elapsedSeconds);
			if (_stuckCheckElapsed < _stuckCheckInterval)
			{
				return;
			}
			if ((base.transform.position - _lastPosition).sqrMagnitude <= _stuckDistanceThresholdSqr)
			{
				_stuckTimer += _stuckCheckElapsed;
			}
			else
			{
				_stuckTimer = 0f;
			}
			_lastPosition = base.transform.position;
			_stuckCheckElapsed = 0f;
			if (!(_stuckTimer < _maxStuckTime))
			{
				Log($"Stuck detected. State={_currentState}, MovementIssued={_movementIssued}, HasActiveMove={_hasActiveMoveOrder}", LogType.Warning);
				if (_nav3DAgent != null)
				{
					_nav3DAgent.Stop();
				}
				ResetMovementState();
				if (_currentState != WanderingCreatureState.Inactive)
				{
					SwitchState(WanderingCreatureState.Patrol);
				}
			}
		}

		private void CacheRuntimeValues()
		{
			_stuckDistanceThresholdSqr = _stuckDistanceThreshold * _stuckDistanceThreshold;
			if (_nav3DAgent != null && _defaultNavSpeed < 0f)
			{
				_defaultNavSpeed = _nav3DAgent.Config.Speed;
			}
		}

		private void ApplyTurnSettings(bool panic)
		{
			if (!(_nav3DAgent == null))
			{
				_nav3DAgent.Config.MaxAgentDegreesRotationPerTick = (panic ? _panicMaxRotationPerTick : _normalMaxRotationPerTick);
				_nav3DAgent.Config.RotationVectorLerpFactor = (panic ? _panicRotationVectorLerpFactor : _normalRotationVectorLerpFactor);
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
			return "WanderingSeaCreature";
		}
	}
}
