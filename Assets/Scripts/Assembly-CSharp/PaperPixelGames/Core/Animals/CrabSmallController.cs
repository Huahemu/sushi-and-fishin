using System;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace PaperPixelGames.Core.Animals
{
	public sealed class CrabSmallController : NetworkBehaviour
	{
		private static readonly int MoveDirectionParameter = Animator.StringToHash("MoveDirection");

		private static readonly int DodgeParameter = Animator.StringToHash("Dodge");

		private readonly NetworkVariable<int> _syncedMoveDirection = new NetworkVariable<int>(0);

		[Header("References")]
		[SerializeField]
		private Animator _animator;

		[SerializeField]
		private NavMeshAgent _agent;

		[Header("Patrol Points")]
		[Tooltip("The crab patrols between the cached world positions of these two transforms.")]
		[SerializeField]
		private Transform _firstPoint;

		[SerializeField]
		private Transform _secondPoint;

		[SerializeField]
		private bool _startTowardsSecondPoint = true;

		[Header("Movement")]
		[SerializeField]
		[Min(0f)]
		private float _moveSpeed = 0.75f;

		[SerializeField]
		[Min(0.001f)]
		private float _arrivalDistance = 0.03f;

		[SerializeField]
		[Min(0f)]
		private float _waitAtPointDuration = 0.35f;

		[SerializeField]
		[Min(0.05f)]
		private float _navMeshSampleRadius = 1f;

		[Header("Dodge Reaction")]
		[SerializeField]
		[Min(0.01f)]
		private float _dodgeDuration = 0.85f;

		[SerializeField]
		[Min(0f)]
		private float _dodgeCooldown = 0.5f;

		[SerializeField]
		private Transform _dodgeEffectPoint;

		[SerializeField]
		private ParticleSystem _dodgeVfx;

		[SerializeField]
		[Min(0f)]
		private float _dodgeVfxLifetime = 5f;

		[SerializeField]
		private AudioClipSO _dodgeSfx;

		private Vector3 _firstPosition;

		private Vector3 _secondPosition;

		private bool _movingTowardsSecondPoint;

		private bool _isDodging;

		private float _waitTimer;

		private float _dodgeTimer;

		private float _nextDodgeTime;

		private bool _hasDestination;

		private bool _authorityInitialized;

		private void Reset()
		{
			_animator = GetComponentInChildren<Animator>();
			_agent = GetComponent<NavMeshAgent>();
		}

		private void Awake()
		{
			if (_animator == null)
			{
				_animator = GetComponentInChildren<Animator>();
			}
			if (_agent == null)
			{
				_agent = GetComponent<NavMeshAgent>();
			}
			_movingTowardsSecondPoint = _startTowardsSecondPoint;
			CachePatrolPositions();
		}

		private void Start()
		{
			if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
			{
				InitializeAuthority();
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<int> syncedMoveDirection = _syncedMoveDirection;
			syncedMoveDirection.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(syncedMoveDirection.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnMoveDirectionChanged));
			ApplyMoveDirection(_syncedMoveDirection.Value);
			if (base.IsServer)
			{
				InitializeAuthority();
			}
			else
			{
				DisableReplicaAgent();
			}
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<int> syncedMoveDirection = _syncedMoveDirection;
			syncedMoveDirection.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(syncedMoveDirection.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnMoveDirectionChanged));
			base.OnNetworkDespawn();
		}

		private void Update()
		{
			if (!IsMovementAuthority())
			{
				return;
			}
			if (_isDodging)
			{
				TickDodge();
			}
			else if (_waitTimer > 0f)
			{
				_waitTimer -= Time.deltaTime;
				if (_waitTimer <= 0f)
				{
					PlayMovementAnimation();
				}
			}
			else
			{
				TickMovement();
			}
		}

		public bool TryDodge(Vector3 threatPosition)
		{
			if (!base.isActiveAndEnabled)
			{
				return false;
			}
			if (!base.IsSpawned)
			{
				if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
				{
					return false;
				}
				return TryDodgeAuthoritative(threatPosition);
			}
			if (base.IsServer)
			{
				return TryDodgeAuthoritative(threatPosition);
			}
			RequestDodgeRpc(threatPosition);
			return true;
		}

		[Rpc(SendTo.Server)]
		private void RequestDodgeRpc(Vector3 threatPosition)
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
				FastBufferWriter bufferWriter = __beginSendRpc(686134681u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in threatPosition);
				__endSendRpc(ref bufferWriter, 686134681u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				TryDodgeAuthoritative(threatPosition);
			}
		}

		private bool TryDodgeAuthoritative(Vector3 threatPosition)
		{
			if (Time.time < _nextDodgeTime)
			{
				return false;
			}
			_isDodging = true;
			_dodgeTimer = _dodgeDuration;
			_waitTimer = 0f;
			_nextDodgeTime = Time.time + _dodgeDuration + _dodgeCooldown;
			StopAgent();
			SetMoveDirection(0);
			if (base.IsSpawned)
			{
				PlayDodgeRpc(threatPosition);
			}
			else
			{
				PlayDodgePresentation(threatPosition);
			}
			return true;
		}

		[Rpc(SendTo.Everyone)]
		private void PlayDodgeRpc(Vector3 threatPosition)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1309953146u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in threatPosition);
				__endSendRpc(ref bufferWriter, 1309953146u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				PlayDodgePresentation(threatPosition);
			}
		}

		private void PlayDodgePresentation(Vector3 threatPosition)
		{
			_animator?.ResetTrigger(DodgeParameter);
			_animator?.SetTrigger(DodgeParameter);
			PlayDodgeFeedback(threatPosition);
		}

		private void TickMovement()
		{
			if (_firstPoint == null || _secondPoint == null || _moveSpeed <= 0f || !EnsureAgentOnNavMesh())
			{
				return;
			}
			if (!_hasDestination)
			{
				SetPatrolDestination();
			}
			else if (!_agent.pathPending && !(_agent.remainingDistance > Mathf.Max(_agent.stoppingDistance, _arrivalDistance)) && (!_agent.hasPath || !(_agent.velocity.sqrMagnitude > 0.0025f)))
			{
				StopAgent();
				_movingTowardsSecondPoint = !_movingTowardsSecondPoint;
				_waitTimer = _waitAtPointDuration;
				if (_waitTimer > 0f)
				{
					SetMoveDirection(0);
				}
				else
				{
					PlayMovementAnimation();
				}
			}
		}

		private void TickDodge()
		{
			_dodgeTimer -= Time.deltaTime;
			if (!(_dodgeTimer > 0f))
			{
				_isDodging = false;
				SetPatrolDestination();
				PlayMovementAnimation();
			}
		}

		private void InitializeAuthority()
		{
			if (!_authorityInitialized)
			{
				_authorityInitialized = true;
				if (_agent != null)
				{
					_agent.enabled = true;
				}
				ConfigureAgent();
				SetPatrolDestination();
				PlayMovementAnimation();
			}
		}

		private bool IsMovementAuthority()
		{
			if (base.IsSpawned)
			{
				return base.IsServer;
			}
			if (!(NetworkManager.Singleton == null))
			{
				return !NetworkManager.Singleton.IsListening;
			}
			return true;
		}

		private void DisableReplicaAgent()
		{
			_authorityInitialized = false;
			_hasDestination = false;
			if (_agent != null)
			{
				_agent.enabled = false;
			}
		}

		private void ConfigureAgent()
		{
			if (!(_agent == null))
			{
				_agent.speed = _moveSpeed;
				_agent.stoppingDistance = _arrivalDistance;
				_agent.updatePosition = true;
				_agent.updateRotation = false;
				EnsureAgentOnNavMesh();
			}
		}

		private bool EnsureAgentOnNavMesh()
		{
			if (_agent == null || !_agent.enabled)
			{
				return false;
			}
			if (_agent.isOnNavMesh)
			{
				return true;
			}
			if (!NavMesh.SamplePosition(base.transform.position, out var hit, _navMeshSampleRadius, -1))
			{
				return false;
			}
			return _agent.Warp(hit.position);
		}

		private void SetPatrolDestination()
		{
			_hasDestination = false;
			if (EnsureAgentOnNavMesh() && NavMesh.SamplePosition(_movingTowardsSecondPoint ? _secondPosition : _firstPosition, out var hit, _navMeshSampleRadius, -1))
			{
				_agent.speed = _moveSpeed;
				_agent.isStopped = false;
				_hasDestination = _agent.SetDestination(hit.position);
			}
		}

		private void StopAgent()
		{
			_hasDestination = false;
			if (!(_agent == null) && _agent.enabled && _agent.isOnNavMesh)
			{
				_agent.ResetPath();
				_agent.isStopped = true;
			}
		}

		private void CachePatrolPositions()
		{
			if (_firstPoint != null)
			{
				_firstPosition = _firstPoint.position;
			}
			if (_secondPoint != null)
			{
				_secondPosition = _secondPoint.position;
			}
		}

		private void PlayMovementAnimation()
		{
			SetMoveDirection(_movingTowardsSecondPoint ? 1 : (-1));
		}

		private void SetMoveDirection(int direction)
		{
			int num = Mathf.Clamp(direction, -1, 1);
			if (base.IsSpawned)
			{
				if (!base.IsServer)
				{
					return;
				}
				if (_syncedMoveDirection.Value != num)
				{
					_syncedMoveDirection.Value = num;
				}
			}
			ApplyMoveDirection(num);
		}

		private void OnMoveDirectionChanged(int previousValue, int currentValue)
		{
			ApplyMoveDirection(currentValue);
		}

		private void ApplyMoveDirection(int direction)
		{
			if (!(_animator == null))
			{
				_animator.SetInteger(MoveDirectionParameter, Mathf.Clamp(direction, -1, 1));
			}
		}

		private void PlayDodgeFeedback(Vector3 threatPosition)
		{
			Vector3 position = ((_dodgeEffectPoint != null) ? _dodgeEffectPoint.position : threatPosition);
			if (_dodgeVfx != null)
			{
				ParticleSystem particleSystem = UnityEngine.Object.Instantiate(_dodgeVfx, position, _dodgeVfx.transform.rotation);
				particleSystem.Play(withChildren: true);
				if (_dodgeVfxLifetime > 0f)
				{
					UnityEngine.Object.Destroy(particleSystem.gameObject, _dodgeVfxLifetime);
				}
			}
			if (_dodgeSfx != null && Singleton<SoundManager>.Instance != null)
			{
				Singleton<SoundManager>.Instance.PlaySFX(_dodgeSfx, position);
			}
		}

		private void OnDrawGizmosSelected()
		{
			if (!(_firstPoint == null) && !(_secondPoint == null))
			{
				Gizmos.color = Color.cyan;
				Gizmos.DrawWireSphere(_firstPoint.position, Mathf.Max(_arrivalDistance, 0.03f));
				Gizmos.DrawWireSphere(_secondPoint.position, Mathf.Max(_arrivalDistance, 0.03f));
				Gizmos.DrawLine(_firstPoint.position, _secondPoint.position);
			}
		}

		protected override void __initializeVariables()
		{
			if (_syncedMoveDirection == null)
			{
				throw new Exception("CrabSmallController._syncedMoveDirection cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_syncedMoveDirection.Initialize(this);
			__nameNetworkVariable(_syncedMoveDirection, "_syncedMoveDirection");
			NetworkVariableFields.Add(_syncedMoveDirection);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(686134681u, __rpc_handler_686134681, "RequestDodgeRpc", RpcInvokePermission.Everyone);
			__registerRpc(1309953146u, __rpc_handler_1309953146, "PlayDodgeRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_686134681(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				((CrabSmallController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabSmallController)target).RequestDodgeRpc(value);
				((CrabSmallController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1309953146(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				((CrabSmallController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabSmallController)target).PlayDodgeRpc(value);
				((CrabSmallController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "CrabSmallController";
		}
	}
}
