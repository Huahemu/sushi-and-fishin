using UnityEngine;
using UnityEngine.AI;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentMovementController
	{
        // Notify the actor-owned talk hold before another owner changes navigation.
        public event System.Action BeforeExternalControl;
		private readonly Transform _transform;

		private readonly NavMeshAgent _agent;

		private readonly float _arrivalVelocityThreshold;
        private Vector3 _requestedMoveGoal;
        private bool _hasMoveGoal;
        public bool HasMoveGoal => _hasMoveGoal;
        public Vector3 RequestedMoveGoal => _requestedMoveGoal;

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
            BeforeExternalControl?.Invoke();
			if (!(_agent == null))
			{
				_agent.speed = Mathf.Max(0.05f, speed);
			}
		}

		public bool MoveTo(Vector3 destination, float speed, float destinationSampleRadius)
		{
			return MoveTo(destination, speed, destinationSampleRadius, requireCompletePath: true);
		}

		public bool MoveTo(Vector3 destination, float speed, float destinationSampleRadius, bool requireCompletePath)
            =>MoveTo(destination,speed,destinationSampleRadius,requireCompletePath,false);
        public bool MoveTo(Vector3 destination,float speed,float destinationSampleRadius,bool requireCompletePath,bool originalSeaSwimHeight)
		{
            BeforeExternalControl?.Invoke();
            _hasMoveGoal=false;
			if (_agent == null || !Finite(destination)) return false;
            // A normal activity may never repair a detached body by snapping.
            // Initial/reset placement remains in the explicit existing Warp API.
            _agent.enabled=true;
            if(!_agent.isOnNavMesh)return false;
            Vector3 bodyAt=_transform.position,navAt=_agent.nextPosition;
            if(originalSeaSwimHeight)bodyAt.y=navAt.y=0;
            if(Vector3.Distance(bodyAt,navAt)>.25f)return false;
            _agent.ResetPath();_agent.isStopped=true;
			_agent.updatePosition = true;
			_agent.updateRotation = true;
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
                if(navMeshPath.corners.Length==0||Vector3.Distance(navMeshPath.corners[0],_agent.nextPosition)>.25f||
                    Vector3.Distance(navMeshPath.corners[navMeshPath.corners.Length-1],resolvedDestination)>.15f){_agent.ResetPath();_agent.isStopped=true;return false;}
                bool accepted=_agent.SetPath(navMeshPath);
                if(accepted){_requestedMoveGoal=resolvedDestination;_hasMoveGoal=true;}return accepted;
			}
            bool result=_agent.SetDestination(resolvedDestination);
            if(result){_requestedMoveGoal=resolvedDestination;_hasMoveGoal=true;}return result;
		}

		public bool TryResolveDestination(Vector3 destination, float radius, out Vector3 resolvedDestination)
		{
			resolvedDestination = destination;
            if(!Finite(destination)||float.IsNaN(radius)||float.IsInfinity(radius))return false;
			if (!NavMesh.SamplePosition(destination, out var hit, Mathf.Max(0.05f, radius), QueryFilter))
			{
				return false;
			}
			resolvedDestination = hit.position;
			return true;
		}

		public void Stop()
		{
            BeforeExternalControl?.Invoke();
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
            BeforeExternalControl?.Invoke();
			_transform.rotation = rotation;
		}

		public void StopAtRotation(Quaternion rotation)
		{
			Stop();
			ApplyRotation(rotation);
		}

		public void StopAtPose(Vector3 position, Quaternion rotation)
		{
            BeforeExternalControl?.Invoke();
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
            BeforeExternalControl?.Invoke();
			if (!(_agent == null) && _agent.enabled && _agent.isOnNavMesh)
			{
				Vector3 nextPosition = _agent.nextPosition;
				_transform.position = new Vector3(nextPosition.x, height, nextPosition.z);
			}
		}

		public void ClearPath()
		{
            BeforeExternalControl?.Invoke();
			if (!(_agent == null) && _agent.enabled && _agent.isOnNavMesh)
			{
				_agent.ResetPath();
			}
		}

		public bool Warp(Vector3 position, Quaternion rotation)
		{
            BeforeExternalControl?.Invoke();
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
            BeforeExternalControl?.Invoke();
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

        // Restore only the approach captured before the local authored pose.
        // This does not use the legacy two-metre general snap.
        public bool TryRestoreFromActivityPose(Vector3 approach,Quaternion rotation,float originalAnchorDomain)
        {
            BeforeExternalControl?.Invoke();
            if(!_agent||originalAnchorDomain<0||Vector3.Distance(_transform.position,approach)>originalAnchorDomain)return false;
            if(!NavMesh.SamplePosition(approach,out var exact,.05f,QueryFilter)||Vector3.Distance(exact.position,approach)>.05f)return false;
            _agent.enabled=true;_agent.updatePosition=true;_agent.updateRotation=true;
            if(!_agent.Warp(exact.position)||!_agent.isOnNavMesh)return false;
            MatchAgentPositionAtHeight(_agent.nextPosition.y);_transform.rotation=rotation;_agent.ResetPath();_agent.isStopped=true;
            _requestedMoveGoal=exact.position;_hasMoveGoal=true;
            return Vector3.Distance(_transform.position,approach)<=.15f&&Vector3.Distance(_transform.position,_agent.nextPosition)<=.15f;
        }

        // Arrival at the end of an accepted partial (pre-merge original) path.
        public bool HasReachedPathEnd()
        {
            if(!_hasMoveGoal||!_agent||!_agent.enabled||!_agent.isOnNavMesh||_agent.pathPending)return false;
            if(Vector3.Distance(_transform.position,_agent.nextPosition)>.25f)return false;
            if(!_agent.hasPath)return true;
            float remaining=_agent.remainingDistance;
            if(float.IsNaN(remaining)||float.IsInfinity(remaining)||remaining>Mathf.Max(_agent.stoppingDistance,.05f)+.1f)return false;
            return _agent.velocity.sqrMagnitude<=Mathf.Max(_arrivalVelocityThreshold,.05f)*Mathf.Max(_arrivalVelocityThreshold,.05f);
        }

        // A body left by an earlier StopAtPose (restaurant chair, authored
        // pose, proxy pose) keeps updatePosition=false and is rejected by
        // MoveTo forever. The pre-merge MoveTo re-attached it; do the same
        // only within a short radius, and only when the caller owns the walk.
        public bool TryReattachDetachedBody(float radius)
        {
            BeforeExternalControl?.Invoke();
            if(!_agent||!_agent.enabled||_agent.updatePosition)return false;
            if(_agent.isOnNavMesh&&Vector3.Distance(_transform.position,_agent.nextPosition)<=.25f)return true;
            if(!NavMesh.SamplePosition(_transform.position,out var hit,Mathf.Max(.05f,radius),QueryFilter)||!_agent.Warp(hit.position)||!_agent.isOnNavMesh)return false;
            _transform.position=hit.position;_agent.updatePosition=true;_agent.updateRotation=true;
            return Vector3.Distance(_transform.position,_agent.nextPosition)<=.25f;
        }

        static bool Finite(Vector3 value)=>!float.IsNaN(value.x)&&!float.IsNaN(value.y)&&!float.IsNaN(value.z)&&
            !float.IsInfinity(value.x)&&!float.IsInfinity(value.y)&&!float.IsInfinity(value.z);

        public bool HasArrived()=>HasArrived(false);
        // Only the original SeaSwim height blend may deliberately separate the
        // rendered Y from its same-type navigation surface. Ground uses XYZ.
        public bool HasArrived(bool originalSeaSwimHeight)
        {
            if(!_hasMoveGoal||!_agent||!_agent.enabled||!_agent.isOnNavMesh||_agent.pathPending)return false;
            float remaining=_agent.remainingDistance;
            if(float.IsNaN(remaining)||float.IsInfinity(remaining)||remaining>Mathf.Max(_agent.stoppingDistance,.05f))return false;
            if(_agent.pathStatus!=NavMeshPathStatus.PathComplete||_agent.velocity.sqrMagnitude>_arrivalVelocityThreshold*_arrivalVelocityThreshold)return false;
            float tolerance=Mathf.Max(_agent.stoppingDistance+.05f,.15f);
            Vector3 body=_transform.position,nav=_agent.nextPosition,goal=_requestedMoveGoal,destination=_agent.destination;
            if(originalSeaSwimHeight){body.y=nav.y=goal.y=destination.y=0;}
            return Finite(body)&&Finite(nav)&&Finite(goal)&&Finite(destination)&&Vector3.Distance(body,nav)<=.25f&&
                Vector3.Distance(body,goal)<=tolerance&&Vector3.Distance(nav,goal)<=tolerance&&Vector3.Distance(destination,goal)<=tolerance;
        }
    }
}
