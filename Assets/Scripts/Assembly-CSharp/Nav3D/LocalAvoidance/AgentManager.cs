using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Nav3D.Agents;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.LocalAvoidance
{
	public class AgentManager : MonoBehaviour
	{
		private enum AgentOperationType
		{
			ADD = 0,
			REMOVE = 1
		}

		private float m_DangerDistance = float.MinValue;

		private float m_DangerDistanceSqr = float.MinValue;

		private readonly HashSet<Nav3DAgentBase> m_Agents = new HashSet<Nav3DAgentBase>();

		private readonly ConcurrentQueue<(Nav3DAgentBase, AgentOperationType)> m_AgentsOperations = new ConcurrentQueue<(Nav3DAgentBase, AgentOperationType)>();

		private SpatialHashMap<IMovable> m_AgentsStorage;

		public static AgentManager Instance => Singleton<AgentManager>.Instance;

		public static bool Doomed { get; private set; }

		private float DangerDistance
		{
			get
			{
				return m_DangerDistance;
			}
			set
			{
				if (m_DangerDistance != value)
				{
					m_DangerDistance = value;
					m_DangerDistanceSqr = m_DangerDistance * m_DangerDistance;
					m_AgentsStorage.SetBucketSize(m_DangerDistance * 2f);
				}
			}
		}

		public float DangerDistanceSqr => m_DangerDistanceSqr;

		private float BucketSize => m_DangerDistance * 2f;

		public static DateTime Now { get; private set; }

		public float StorageBucketSize => m_AgentsStorage.BucketSize;

		public void Initialize(List<Nav3DAgentMover> _Agents)
		{
			m_AgentsStorage = new SpatialHashMap<IMovable>(BucketSize, _Agents);
		}

		public void Uninitialize(bool _NeedDestroy = true)
		{
			if (_NeedDestroy)
			{
				UtilsCommon.SmartDestroy(this);
			}
		}

		public void RegisterAgent(Nav3DAgentBase _Agent)
		{
			m_AgentsOperations.Enqueue((_Agent, AgentOperationType.ADD));
		}

		public void UnregisterAgent(Nav3DAgentBase _Agent)
		{
			m_AgentsOperations.Enqueue((_Agent, AgentOperationType.REMOVE));
		}

		public void RegisterAgentMover(IMovable _Agent)
		{
			m_AgentsStorage.Insert(_Agent);
			UpdateCellSize((_Agent.GetRadius() + _Agent.GetMaxSpeed()) * 2f);
		}

		public void UnregisterAgentMover(IMovable _Agent)
		{
			m_AgentsStorage?.Remove(_Agent);
		}

		public HashSet<IMovable> GetAgentBucketAgents(IMovable _Agent)
		{
			return m_AgentsStorage.GetElementBucketElements(_Agent);
		}

		public HashSet<IMovable> GetBucketMovables(Vector3 _InsidePoint)
		{
			return m_AgentsStorage.GetBucketElements(_InsidePoint);
		}

		public IEnumerable<IMovable> GetMovablesInBounds(Bounds _Bounds)
		{
			return m_AgentsStorage.GetMovablesInBounds(_Bounds);
		}

		public bool HasNeighbors(IMovable _Movable)
		{
			return m_AgentsStorage.HasNeighbors(_Movable);
		}

		public bool HasNearestObstacles(IMovable _Movable, out Bounds _DangerBounds)
		{
			return m_AgentsStorage.HasNearestObstacles(_Movable, out _DangerBounds);
		}

		public void SetMovablesInBoundsObstacleDirty(Bounds _Bounds)
		{
			m_AgentsStorage.SetMovablesInBoundsObstacleDirty(_Bounds);
		}

		private void UpdateCellSize(float _Value)
		{
			if (DangerDistance < _Value)
			{
				DangerDistance = _Value;
			}
		}

		private void Awake()
		{
			Doomed = false;
		}

		private void OnDestroy()
		{
			Doomed = true;
			Uninitialize(_NeedDestroy: false);
		}

		private void Update()
		{
			Now = DateTime.Now;
		}

		private void FixedUpdate()
		{
			while (m_AgentsOperations.Any())
			{
				if (m_AgentsOperations.TryDequeue(out var result))
				{
					if (result.Item2 == AgentOperationType.ADD)
					{
						m_Agents.Add(result.Item1);
					}
					else
					{
						m_Agents.Remove(result.Item1);
					}
				}
			}
			foreach (Nav3DAgentBase agent in m_Agents)
			{
				agent.DoFixedUpdate();
			}
		}
	}
}
