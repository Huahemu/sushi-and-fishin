using System;
using Nav3D.Common;
using Nav3D.LocalAvoidance;
using UnityEngine;

namespace Nav3D.Agents
{
	public class Nav3DEvaderMover : IMovable
	{
		private readonly float m_Radius;

		private readonly float m_MaxSpeed;

		private readonly float m_ORCATau;

		private readonly float m_SpeedDecayFactor;

		private readonly float m_VelocityRadius;

		private Vector3 m_CurrentPosition;

		private Vector3 m_LastVelocity;

		private Vector3 m_LastNonZeroVelocity;

		private bool m_IsNeighborMovablesDirty = true;

		private bool m_IsNeighborObstaclesDirty = true;

		private EvaderLocalAvoidanceSolver m_LocalAvoidanceSolver;

		public bool Avoiding => true;

		public bool NeedToBeAvoided => false;

		public bool IsNeighborMoversDirty => m_IsNeighborMovablesDirty;

		public bool IsNeighborObstaclesDirty => m_IsNeighborObstaclesDirty;

		public event Action<IMovable, Vector3> OnPositionChanged;

		public Nav3DEvaderMover(Vector3 _CurrentPosition, float _Radius, float _MaxSpeed, float _ORCATau, float _SpeedDecayFactor)
		{
			m_CurrentPosition = _CurrentPosition;
			m_Radius = _Radius;
			m_MaxSpeed = _MaxSpeed;
			m_ORCATau = _ORCATau;
			m_SpeedDecayFactor = _SpeedDecayFactor;
			m_VelocityRadius = m_Radius + m_MaxSpeed;
		}

		public void SetCurrentPosition(Vector3 _Position)
		{
			m_CurrentPosition = _Position;
			OnPositionChanged?.Invoke(this, m_CurrentPosition);
		}

		public Vector3 GetVelocity()
		{
			m_LastVelocity = m_LocalAvoidanceSolver.ResolveVelocity();
			if (m_LastVelocity != Vector3.zero)
			{
				m_LastNonZeroVelocity = m_LastVelocity;
			}
			return m_LastVelocity;
		}

		public void Initialize()
		{
			m_LocalAvoidanceSolver = new EvaderLocalAvoidanceSolver(this, m_Radius, m_MaxSpeed, m_ORCATau, m_SpeedDecayFactor);
			AgentManager.Instance.RegisterAgentMover(this);
		}

		public void Uninitialize()
		{
			if (!AgentManager.Doomed)
			{
				AgentManager.Instance.UnregisterAgentMover(this);
			}
		}

		public Vector3 GetPosition()
		{
			return m_CurrentPosition;
		}

		public Vector3 GetLastFrameVelocity()
		{
			return m_LastVelocity;
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
			return m_MaxSpeed;
		}

		public float GetORCATau()
		{
			return m_ORCATau;
		}

		public float GetStaticObstaclesDangerDistance()
		{
			return m_VelocityRadius;
		}

		public void SetNeighborMovablesDirty(bool _Dirty)
		{
			m_IsNeighborMovablesDirty = _Dirty;
		}

		public void SetNeighborObstaclesDirty(bool _Dirty)
		{
			m_IsNeighborObstaclesDirty = _Dirty;
		}
	}
}
