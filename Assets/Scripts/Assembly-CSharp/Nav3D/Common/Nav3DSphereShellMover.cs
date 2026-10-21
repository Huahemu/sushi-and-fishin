using System;
using Nav3D.LocalAvoidance;
using UnityEngine;

namespace Nav3D.Common
{
	public class Nav3DSphereShellMover : IMovable
	{
		private readonly float m_Radius;

		private Vector3 m_PrevPosition;

		private Vector3 m_CurrentPosition;

		private Vector3 m_CurrentVelocity;

		private Vector3 m_LastNonZeroVelocity;

		public bool Avoiding => false;

		public bool NeedToBeAvoided => true;

		public bool IsNeighborMoversDirty => false;

		public bool IsNeighborObstaclesDirty => false;

		public event Action<IMovable, Vector3> OnPositionChanged;

		public Nav3DSphereShellMover(Vector3 _CurrentPosition, float _Radius)
		{
			m_PrevPosition = _CurrentPosition;
			m_CurrentPosition = _CurrentPosition;
			m_Radius = _Radius;
		}

		public void Initialize()
		{
			AgentManager.Instance.RegisterAgentMover(this);
		}

		public void Uninitialize()
		{
			if (!AgentManager.Doomed)
			{
				AgentManager.Instance.RegisterAgentMover(this);
			}
		}

		public void SetCurrentPosition(Vector3 _Position)
		{
			m_PrevPosition = m_CurrentPosition;
			m_CurrentPosition = _Position;
			m_CurrentVelocity = m_CurrentPosition - m_PrevPosition;
			if (m_CurrentVelocity != Vector3.zero)
			{
				m_LastNonZeroVelocity = m_CurrentVelocity;
			}
			OnPositionChanged?.Invoke(this, m_CurrentPosition);
		}

		public Vector3 GetPosition()
		{
			return m_CurrentPosition;
		}

		public Vector3 GetLastFrameVelocity()
		{
			return m_CurrentVelocity;
		}

		public Vector3 GetLastNonZeroVelocity()
		{
			return m_LastNonZeroVelocity;
		}

		public float GetRadius()
		{
			return m_Radius;
		}

		public float GetMaxSpeed()
		{
			return m_CurrentVelocity.magnitude;
		}

		public float GetORCATau()
		{
			return 1f;
		}

		public float GetStaticObstaclesDangerDistance()
		{
			return 0f;
		}

		public void SetNeighborMovablesDirty(bool _Dirty)
		{
		}

		public void SetNeighborObstaclesDirty(bool _Dirty)
		{
		}
	}
}
