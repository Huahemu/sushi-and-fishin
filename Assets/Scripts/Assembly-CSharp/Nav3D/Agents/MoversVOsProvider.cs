using System.Collections.Generic;
using System.Linq;
using Nav3D.API;
using Nav3D.Common;
using Nav3D.LocalAvoidance;
using Nav3D.LocalAvoidance.SupportingMath;
using UnityEngine;

namespace Nav3D.Agents
{
	public class MoversVOsProvider
	{
		private class RivalsStorage
		{
			private readonly Dictionary<Nav3DAgentMover, VOAgent> m_Rivals = new Dictionary<Nav3DAgentMover, VOAgent>();

			private bool IsEmpty { get; set; }

			public void Add(Nav3DAgentMover _RivalAgent, VOAgent _RivalVO)
			{
				m_Rivals[_RivalAgent] = _RivalVO;
				IsEmpty = false;
			}

			public void Clear()
			{
				if (!IsEmpty)
				{
					m_Rivals.Clear();
					IsEmpty = true;
				}
			}

			public bool TryGetValidVO(Nav3DAgentMover _Agent, out VOAgent _VO)
			{
				m_Rivals.TryGetValue(_Agent, out var value);
				_VO = value;
				return _VO != null;
			}
		}

		private readonly RivalsStorage m_Rivals = new RivalsStorage();

		private List<IMovable> m_NearestMovers;

		private readonly Nav3DAgentMover m_Mover;

		private readonly Nav3DAgentConfig m_Config;

		private readonly float m_VelocityRadius;

		public MoversVOsProvider(Nav3DAgentMover _Mover, Nav3DAgentConfig _Config)
		{
			m_Mover = _Mover;
			m_Config = _Config;
			m_VelocityRadius = m_Config.Radius + m_Config.MaxSpeed;
		}

		public List<VOAgent> GetVOs()
		{
			TryUpdateNeighborMovers();
			int num = (m_Config.UseConsideredAgentsNumberLimit ? m_Config.ConsideredAgentsNumberLimit : m_NearestMovers.Count);
			List<VOAgent> list = new List<VOAgent>(num);
			foreach (IMovable nearestMover in m_NearestMovers)
			{
				if (!nearestMover.NeedToBeAvoided)
				{
					continue;
				}
				Vector3 position = nearestMover.GetPosition();
				float num2 = nearestMover.GetRadius() + nearestMover.GetMaxSpeed();
				Vector3 position2 = m_Mover.GetPosition();
				float num3 = Vector3.SqrMagnitude(position - position2);
				float num4 = UtilsMath.Sqr(num2 + m_VelocityRadius);
				if (num3 > num4)
				{
					continue;
				}
				float num5 = UtilsMath.Sqr(m_Mover.GetRadius() + nearestMover.GetRadius());
				Vector3 lastFrameVelocity = m_Mover.GetLastFrameVelocity();
				Vector3 lastFrameVelocity2 = nearestMover.GetLastFrameVelocity();
				float num6 = Mathf.Acos(Mathf.Clamp01(Vector3.Dot(lastFrameVelocity.normalized, lastFrameVelocity2.normalized)));
				if (num3 < num5)
				{
					if (num6 < 0.349066f && new Nav3D.LocalAvoidance.SupportingMath.Plane(lastFrameVelocity, position2).GetSide(position))
					{
						m_Mover.SetAvoidanceInterrupt(_Value: true);
						return new List<VOAgent>();
					}
				}
				else if (num6 < 0.349066f || (nearestMover.Avoiding && !UtilsMath.VelocityTrailIntersects(position, nearestMover.GetLastFrameVelocity() * nearestMover.GetORCATau(), nearestMover.GetRadius(), position2, m_Mover.GetLastFrameVelocity() * m_Mover.GetORCATau(), m_Mover.GetRadius())))
				{
					continue;
				}
				if (nearestMover is Nav3DAgentMover nav3DAgentMover)
				{
					if (!m_Rivals.TryGetValidVO(nav3DAgentMover, out var _VO))
					{
						_VO = CreateAgentVO(m_Mover, nav3DAgentMover, m_Config.ORCATau);
						_VO.ComputeVO();
						nav3DAgentMover.UpdateRivalVO(m_Mover, _VO.Flipped);
					}
					list.Add(_VO);
				}
				if (nearestMover is Nav3DSphereShellMover otherAgent)
				{
					VOAgent vOAgent = CreateAgentVO(m_Mover, otherAgent, m_Config.ORCATau);
					vOAgent.ComputeVO();
					list.Add(vOAgent);
				}
				if (list.Count == num)
				{
					break;
				}
			}
			m_Rivals.Clear();
			return list;
		}

		public void UpdateRivalVO(Nav3DAgentMover _RivalMover, VOAgent _RivalVO)
		{
			m_Rivals.Add(_RivalMover, _RivalVO);
		}

		private void TryUpdateNeighborMovers()
		{
			if (m_Mover.IsNeighborMoversDirty)
			{
				HashSet<IMovable> agentBucketAgents = AgentManager.Instance.GetAgentBucketAgents(m_Mover);
				if (m_NearestMovers == null)
				{
					m_NearestMovers = new List<IMovable>(Mathf.Max(agentBucketAgents.Count - 1, 0));
				}
				Vector3 moverPosition = m_Mover.GetPosition();
				m_NearestMovers.Clear();
				m_NearestMovers.AddRange(from _Mover in agentBucketAgents
					where _Mover.NeedToBeAvoided && _Mover != m_Mover
					orderby (_Mover.GetPosition() - moverPosition).sqrMagnitude
					select _Mover);
				m_Mover.SetNeighborMovablesDirty(_Dirty: false);
			}
		}

		protected virtual VOAgent CreateAgentVO(IMovable _SolverAgent, IMovable _OtherAgent, float _Tau)
		{
			return new VOAgent(_SolverAgent, _OtherAgent, _Tau);
		}
	}
}
