using System;
using Nav3D.API;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.Agents
{
	public abstract class MovementLogic : IDisposable
	{
		protected Nav3DPath m_Path;

		protected readonly PathfindingRequest m_Request;

		private readonly Transform m_AgentTransform;

		protected readonly float m_SqrTargetReachDistance;

		private readonly Action<PathfindingResult> m_OnPathfindingFail;

		protected readonly Nav3DAgentMover m_Mover;

		protected readonly Action<PathfindingResult> m_OnPathUpdated;

		protected readonly Log m_Log;

		public bool IsValid { get; private set; }

		protected abstract bool TryRepositionStartIfOccupied { get; }

		protected abstract bool TryRepositionTargetIfOccupied { get; }

		public Vector3[] CurrentPath => m_Mover?.CurrentPath;

		protected MovementLogic(Transform _AgentTransform, Nav3DAgentMover _Mover, PathfindingRequest _Request, float _ReachDistance, Action<PathfindingResult> _OnPathUpdated, Action<PathfindingResult> _OnPathfindingFail, Log _Log)
		{
			m_Log = _Log;
			m_Request = _Request;
			m_AgentTransform = _AgentTransform;
			m_Mover = _Mover;
			m_OnPathUpdated = _OnPathUpdated;
			m_OnPathfindingFail = _OnPathfindingFail;
			m_SqrTargetReachDistance = _ReachDistance * _ReachDistance;
			m_Mover.SetCurrentPosition(_AgentTransform.position);
		}

		public virtual void Init(Nav3DPath _Path)
		{
			IsValid = true;
		}

		public virtual void DoOnFrameStart()
		{
		}

		public virtual void DoOnFrameEnd()
		{
			m_Mover.SetCurrentPosition(m_AgentTransform.position);
			if (IsValid)
			{
				if (m_Mover.IsDeviatedFromPathLastFrame())
				{
					DoOnDeviatedFromPath();
				}
				else if (CheckTargetReached())
				{
					DoOnTargetReached();
				}
			}
		}

		public abstract void DoOnDeviatedFromPath();

		public abstract void DoOnTargetReached();

		public abstract Vector3 GetFrameVelocity();

		public virtual void Dispose()
		{
			Invalidate();
			m_Mover.DisposePath();
		}

		protected void DoOnPathfindingFailed(PathfindingResult _Result)
		{
			if (_Result.Result != PathfindingResultCode.CANCELLED)
			{
				Invalidate();
				m_OnPathfindingFail?.Invoke(_Result);
			}
		}

		protected abstract bool CheckTargetReached();

		protected void Invalidate()
		{
			IsValid = false;
		}
	}
}
