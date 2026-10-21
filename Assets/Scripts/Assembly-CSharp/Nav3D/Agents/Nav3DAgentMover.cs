using System;
using Nav3D.API;
using Nav3D.Common;
using Nav3D.LocalAvoidance;
using UnityEngine;

namespace Nav3D.Agents
{
	public abstract class Nav3DAgentMover : IMovable
	{
		private bool m_IsNeighborMovablesDirty = true;

		private bool m_IsNeighborObstaclesDirty = true;

		protected MoversVOsProvider m_MoversVOsProvider;

		protected StaticTrianglesVOProvider m_TrianglesVOProvider;

		private Vector3 m_AccumulatedVelocity;

		protected Vector3 m_CurrentPosition;

		private Vector3 m_LastVelocity;

		private Vector3 m_LastNonZeroVelocity;

		protected Nav3DAgentConfig m_Config;

		protected PathfindingRequest m_Request;

		protected Action<Vector3> m_OnTargetPassed;

		protected Action m_OnLastTargetReached;

		protected readonly Log m_Log;

		protected Action<PathfindingResult> m_OnPathUpdated;

		public Nav3DAgent Agent { get; private set; }

		public abstract bool Avoiding { get; }

		public bool NeedToBeAvoided
		{
			get
			{
				if (Agent.Operates)
				{
					return !CoDirectionalAvoidanceInterrupt;
				}
				return false;
			}
		}

		public bool CoDirectionalAvoidanceInterrupt { get; private set; }

		public bool IsNeighborMoversDirty => m_IsNeighborMovablesDirty;

		public bool IsNeighborObstaclesDirty => m_IsNeighborObstaclesDirty;

		public Vector3 CurrentPosition => m_CurrentPosition;

		public float UpdateStaticObstaclesSqrDistanceThreshold => m_Config.VelocityRadiusSqr;

		protected Vector3 LastVelocity
		{
			get
			{
				return m_LastVelocity;
			}
			set
			{
				m_LastVelocity = value;
				if (value != Vector3.zero)
				{
					m_LastNonZeroVelocity = value;
				}
			}
		}

		public abstract Vector3[] CurrentPath { get; }

		public event Action<IMovable, Vector3> OnPositionChanged;

		protected Nav3DAgentMover(Vector3 _Position, Nav3DAgentConfig _Config, Nav3DAgent _Agent, Log _Log)
		{
			Agent = _Agent;
			m_CurrentPosition = _Position;
			m_Log = _Log;
			ApplyConfig(_Config);
		}

		public abstract void BeginMoveTo(PathfindingRequest _Request, Nav3DPath _Path, Action<PathfindingResult> _OnPathUpdated, Action<PathfindingResult> _OnPathfindingFail = null);

		public abstract void BeginMoveToPoints(PathfindingRequest _Request, Nav3DPath _Path, Action<PathfindingResult> _OnPathUpdated, Action<Vector3> _OnTargetPointPassed = null, Action _OnLastPointReached = null, Action<PathfindingResult> _OnPathfindingFail = null);

		public abstract void MoveToUsingExistingPath(PathfindingRequest _Request, Action<PathfindingResult> _OnPathUpdated, Action<PathfindingResult> _OnPathfindingFail = null);

		public Vector3 GetFrameVelocity()
		{
			Vector3 velocity = GetVelocity();
			m_AccumulatedVelocity = Vector3.Slerp(m_AccumulatedVelocity, velocity, 0.5f);
			return velocity;
		}

		public virtual void Uninitialize()
		{
			if (!AgentManager.Doomed)
			{
				AgentManager.Instance.UnregisterAgentMover(this);
			}
		}

		public virtual void DisposePath()
		{
		}

		public virtual void SetCurrentPosition(Vector3 _Position)
		{
			m_CurrentPosition = _Position;
			OnPositionChanged?.Invoke(this, m_CurrentPosition);
		}

		public virtual bool IsDeviatedFromPathLastFrame()
		{
			return false;
		}

		public Vector3 GetLastNonZeroVelocity()
		{
			return m_LastNonZeroVelocity;
		}

		public virtual void UpdateRivalVO(Nav3DAgentMover _RivalMover, VOAgent _RivalVO)
		{
		}

		public bool IsCollidingWithOther(Nav3DAgentMover _Other)
		{
			Vector3 vector = _Other.GetPosition() - m_CurrentPosition;
			float num = m_Config.Radius + _Other.GetRadius();
			return vector.magnitude < num;
		}

		public void SetAvoidanceInterrupt(bool _Value)
		{
			CoDirectionalAvoidanceInterrupt = _Value;
		}

		public virtual void ReinitPathFollowingData()
		{
		}

		public virtual void RestartFollowingForCurrentPath()
		{
		}

		public virtual string GetInnerStatusString()
		{
			return string.Empty;
		}

		public virtual string GetLastPathfindingStatsString()
		{
			return string.Empty;
		}

		public Vector3 GetPosition()
		{
			return m_CurrentPosition;
		}

		public Vector3 GetLastFrameVelocity()
		{
			return m_LastVelocity;
		}

		public float GetRadius()
		{
			return m_Config.Radius;
		}

		public float GetMaxSpeed()
		{
			return m_Config.FactualMaxSpeed;
		}

		public float GetORCATau()
		{
			return m_Config.ORCATau;
		}

		public float GetStaticObstaclesDangerDistance()
		{
			return m_Config.VelocityRadius;
		}

		public void SetNeighborMovablesDirty(bool _Dirty)
		{
			m_IsNeighborMovablesDirty = _Dirty;
		}

		public void SetNeighborObstaclesDirty(bool _Dirty)
		{
			m_IsNeighborObstaclesDirty = _Dirty;
		}

		private void ApplyConfig(Nav3DAgentConfig _Config)
		{
			m_Config = _Config;
			if (!AgentManager.Doomed)
			{
				AgentManager.Instance.RegisterAgentMover(this);
			}
		}

		protected abstract Vector3 GetVelocity();
	}
}
