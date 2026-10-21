using System;
using Nav3D.API;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.Agents
{
	public class TargetFollowingLogic : MovementLogic
	{
		private readonly string LOG_CTOR = "TargetFollowingLogic.ctor";

		private readonly Transform m_Target;

		private readonly Action m_OnReach;

		private readonly Action m_OnFollowingFailed;

		private readonly float m_SqrOffsetUpdate;

		private Vector3 m_LastTargetPosition;

		private readonly bool m_FollowContinuously;

		private bool m_CurrentTargetPositionReached;

		protected override bool TryRepositionStartIfOccupied => true;

		protected override bool TryRepositionTargetIfOccupied => m_FollowContinuously;

		public TargetFollowingLogic(Transform _AgentTransform, Nav3DAgentMover _Mover, PathfindingRequest _Request, Transform _Target, float _TargetOffsetUpdate, float _ReachDistance, bool _FollowContinuously, Action _OnReach, Action<PathfindingResult> _OnPathUpdated, Action _OnFollowingFailed, Action<PathfindingResult> _OnPathfindingFail, Log _Log = null)
			: base(_AgentTransform, _Mover, _Request, _ReachDistance, _OnPathUpdated, _OnPathfindingFail, _Log)
		{
			m_Target = _Target;
			m_OnReach = _OnReach;
			m_OnFollowingFailed = _OnFollowingFailed;
			m_SqrOffsetUpdate = _TargetOffsetUpdate * _TargetOffsetUpdate;
			m_FollowContinuously = _FollowContinuously;
			m_Log?.Write(LOG_CTOR);
		}

		public override void Init(Nav3DPath _Path)
		{
			m_Path = _Path;
			m_LastTargetPosition = m_Target.position;
			m_Mover.BeginMoveTo(m_Request, m_Path, m_OnPathUpdated, base.DoOnPathfindingFailed);
			base.Init(_Path);
		}

		public override void DoOnFrameStart()
		{
			if (base.IsValid)
			{
				if (m_Target == null || !m_Target.gameObject.activeInHierarchy)
				{
					Invalidate();
					m_OnFollowingFailed?.Invoke();
				}
				else if ((m_LastTargetPosition - m_Target.position).sqrMagnitude > m_SqrOffsetUpdate)
				{
					m_LastTargetPosition = m_Target.position;
					m_CurrentTargetPositionReached = false;
					m_Mover.MoveToUsingExistingPath(m_Request with
					{
						Points = new Vector3[2] { m_Mover.CurrentPosition, m_LastTargetPosition }
					}, m_OnPathUpdated, base.DoOnPathfindingFailed);
				}
			}
		}

		public override Vector3 GetFrameVelocity()
		{
			if (!base.IsValid)
			{
				return Vector3.zero;
			}
			if (m_CurrentTargetPositionReached)
			{
				return Vector3.zero;
			}
			return m_Mover.GetFrameVelocity();
		}

		public override void DoOnTargetReached()
		{
			m_CurrentTargetPositionReached = true;
			if (!m_FollowContinuously)
			{
				m_Mover.DisposePath();
				Dispose();
				m_OnReach?.Invoke();
			}
		}

		public override void DoOnDeviatedFromPath()
		{
			m_Mover.MoveToUsingExistingPath(m_Request with
			{
				Points = new Vector3[2] { m_Mover.CurrentPosition, m_LastTargetPosition }
			}, m_OnPathUpdated, base.DoOnPathfindingFailed);
		}

		protected override bool CheckTargetReached()
		{
			return (m_Mover.CurrentPosition - m_LastTargetPosition).sqrMagnitude < m_SqrTargetReachDistance;
		}
	}
}
