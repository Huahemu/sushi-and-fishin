using System;
using Nav3D.API;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.Agents
{
	public class PointsFollowingLogic : MovementLogic
	{
		private readonly string LOG_CTOR = "PointsFollowingLogic.ctor";

		private readonly string LOG_ON_TARGET_REACHED = "PointsFollowingLogic.DoOnTargetReached, Loop:{0}";

		private readonly Action<Vector3> m_OnPointPassed;

		private readonly Action m_OnFinished;

		private bool m_IsLastPointReached;

		private bool m_IsAnyTargetPassed;

		protected override bool TryRepositionStartIfOccupied => !m_Request.Loop;

		protected override bool TryRepositionTargetIfOccupied => m_Request.TryRepositionTargetIfOccupied;

		public PointsFollowingLogic(Transform _AgentTransform, Nav3DAgentMover _Mover, PathfindingRequest _Request, float _ReachDistance, Action<Vector3> _OnPointPassed, Action _OnFinished, Action<PathfindingResult> _OnPathUpdated, Action<PathfindingResult> _OnPathfindingFail, Log _Log = null)
			: base(_AgentTransform, _Mover, _Request, _ReachDistance, _OnPathUpdated, _OnPathfindingFail, _Log)
		{
			m_OnPointPassed = _OnPointPassed;
			m_OnFinished = _OnFinished;
			m_Log?.Write(LOG_CTOR);
		}

		public override void Init(Nav3DPath _Path)
		{
			m_Path = _Path;
			m_IsAnyTargetPassed = false;
			m_Mover.BeginMoveToPoints(m_Request, m_Path, m_OnPathUpdated, OnTargetPassed, LastPointReached, base.DoOnPathfindingFailed);
			base.Init(_Path);
		}

		public override Vector3 GetFrameVelocity()
		{
			if (!base.IsValid)
			{
				return Vector3.zero;
			}
			return m_Mover.GetFrameVelocity();
		}

		public override void DoOnDeviatedFromPath()
		{
		}

		public override void DoOnTargetReached()
		{
			m_Log?.WriteFormat(LOG_ON_TARGET_REACHED, m_Request.Loop);
			if (m_Request.Loop)
			{
				m_Mover.ReinitPathFollowingData();
				m_IsLastPointReached = false;
			}
			else
			{
				m_Mover.DisposePath();
				Dispose();
				m_OnFinished?.Invoke();
			}
		}

		private void OnTargetPassed(Vector3 _Target)
		{
			if (m_Request.Loop && m_Request.Points.Length > 2 && _Target != m_Request.Points[0] && !m_IsAnyTargetPassed)
			{
				m_Mover.RestartFollowingForCurrentPath();
				m_IsAnyTargetPassed = true;
			}
			m_OnPointPassed?.Invoke(_Target);
		}

		private void LastPointReached()
		{
			m_IsLastPointReached = true;
		}

		protected override bool CheckTargetReached()
		{
			return m_IsLastPointReached;
		}
	}
}
