using UnityEngine;
using UnityEngine.AI;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentMovementController
	{
		private readonly Transform _transform;

		private readonly NavMeshAgent _agent;

		private readonly float _arrivalVelocityThreshold;

		public bool HasAgent => _agent != null;

		public bool AgentEnabled
		{
			get
			{
				if (_agent != null)
				{
					return _agent.enabled;
				}
				return false;
			}
		}

		public float RemainingDistance
		{
			get
			{
				if (!(_agent != null) || !_agent.enabled || !_agent.isOnNavMesh)
				{
					return -1f;
				}
				return _agent.remainingDistance;
			}
		}

		public Vector3 Destination
		{
			get
			{
				if (!(_agent != null) || !_agent.enabled || !_agent.isOnNavMesh)
				{
					return _transform.position;
				}
				return _agent.destination;
			}
		}

		public Vector3 Position => _transform.position;

		private NavMeshQueryFilter QueryFilter => new NavMeshQueryFilter
		{
			agentTypeID = ((_agent != null) ? _agent.agentTypeID : 0),
			areaMask = ((_agent != null) ? _agent.areaMask : (-1))
		};

		public ResidentMovementController(Transform transform, NavMeshAgent agent, float arrivalVelocityThreshold = 0.05f)
		{
			_transform = transform;
			_agent = agent;
			_arrivalVelocityThreshold = Mathf.Max(0f, arrivalVelocityThreshold);
		}

		public bool MoveTo(Vector3 destination, float speed)
		{
			return MoveTo(destination, speed, 1.5f);
		}

		public void SetSpeed(float speed)
		{
			if (!(_agent == null))
			{
				_agent.speed = Mathf.Max(0.05f, speed);
			}
		}

		public bool MoveTo(Vector3 destination, float speed, float destinationSampleRadius)
		{
			return MoveTo(destination, speed, destinationSampleRadius, requireCompletePath: false);
		}

		public bool MoveTo(Vector3 destination, float speed, float destinationSampleRadius, bool requireCompletePath)
		{
			if (_agent == null)
			{
				return false;
			}
			_agent.enabled = true;
			_agent.updatePosition = true;
			_agent.updateRotation = true;
			if (!_agent.isOnNavMesh && !TrySnapToNavMesh(_transform.position, 2f, out var _))
			{
				return false;
			}
			if (!TryResolveDestination(destination, destinationSampleRadius, out var resolvedDestination))
			{
				return false;
			}
			SetSpeed(speed);
			_agent.isStopped = false;
			if (requireCompletePath)
			{
				NavMeshPath navMeshPath = new NavMeshPath();
				if (!_agent.CalculatePath(resolvedDestination, navMeshPath) || navMeshPath.status != NavMeshPathStatus.PathComplete)
				{
					_agent.ResetPath();
					_agent.isStopped = true;
					return false;
				}
				return _agent.SetPath(navMeshPath);
			}
			return _agent.SetDestination(resolvedDestination);
		}

		public bool TryResolveDestination(Vector3 destination, float radius, out Vector3 resolvedDestination)
		{
			resolvedDestination = destination;
			if (!NavMesh.SamplePosition(destination, out var hit, Mathf.Max(0.05f, radius), QueryFilter))
			{
				return false;
			}
			resolvedDestination = hit.position;
			return true;
		}

		public void Stop()
		{
			if (!(_agent == null) && _agent.enabled)
			{
				if (_agent.isOnNavMesh)
				{
					_agent.ResetPath();
				}
				_agent.isStopped = true;
			}
		}

		public void ApplyRotation(Quaternion rotation)
		{
			_transform.rotation = rotation;
		}

		public void StopAtRotation(Quaternion rotation)
		{
			Stop();
			ApplyRotation(rotation);
		}

		public void StopAtPose(Vector3 position, Quaternion rotation)
		{
			Stop();
			if (_agent != null && _agent.enabled)
			{
				_agent.updatePosition = false;
				_agent.updateRotation = false;
			}
			_transform.SetPositionAndRotation(position, rotation);
		}

		public void MatchAgentPositionAtHeight(float height)
		{
			if (!(_agent == null) && _agent.enabled && _agent.isOnNavMesh)
			{
				Vector3 nextPosition = _agent.nextPosition;
				_transform.position = new Vector3(nextPosition.x, height, nextPosition.z);
			}
		}

		public void ClearPath()
		{
			if (!(_agent == null) && _agent.enabled && _agent.isOnNavMesh)
			{
				_agent.ResetPath();
			}
		}

		public bool Warp(Vector3 position, Quaternion rotation)
		{
			if (_agent == null)
			{
				_transform.SetPositionAndRotation(position, rotation);
				return true;
			}
			_agent.enabled = true;
			if (_agent.isOnNavMesh)
			{
				_agent.ResetPath();
			}
			if (NavMesh.SamplePosition(position, out var hit, 2f, QueryFilter) && _agent.Warp(hit.position) && _agent.isOnNavMesh)
			{
				_transform.rotation = rotation;
				_agent.isStopped = false;
				return true;
			}
			return false;
		}

		public bool TrySnapToNavMesh(Vector3 origin, float radius, out Vector3 snapped)
		{
			snapped = origin;
			if (!NavMesh.SamplePosition(origin, out var hit, Mathf.Max(0.05f, radius), QueryFilter))
			{
				return false;
			}
			snapped = hit.position;
			if (_agent != null)
			{
				_agent.enabled = true;
				if (_agent.Warp(snapped))
				{
					return _agent.isOnNavMesh;
				}
				return false;
			}
			_transform.position = snapped;
			return true;
		}

		public bool HasArrived()
		{
			if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
			{
				return false;
			}
			if (_agent.pathPending)
			{
				return false;
			}
			if (_agent.remainingDistance > Mathf.Max(_agent.stoppingDistance, 0.05f))
			{
				return false;
			}
			if (_agent.hasPath && _agent.velocity.sqrMagnitude > _arrivalVelocityThreshold * _arrivalVelocityThreshold)
			{
				return false;
			}
			if (_agent.hasPath)
			{
				return _agent.pathStatus != NavMeshPathStatus.PathInvalid;
			}
			return true;
		}
	}
}
