using System.Collections.Generic;
using System.Linq;
using Nav3D.Common;
using Nav3D.LocalAvoidance;
using Nav3D.LocalAvoidance.SupportingMath;
using UnityEngine;

namespace Nav3D.Agents
{
	public class EvaderLocalAvoidanceSolver
	{
		private readonly StaticTrianglesVOProvider m_TrianglesVOProvider;

		private readonly HashSet<IMovable> m_NearestMovers = new HashSet<IMovable>();

		private readonly Nav3DEvaderMover m_Mover;

		private readonly float m_Radius;

		private readonly float m_MaxSpeed;

		private readonly float m_ORCATau;

		private readonly float m_SpeedDecayFactor;

		public EvaderLocalAvoidanceSolver(Nav3DEvaderMover _Mover, float _Radius, float _MaxSpeed, float _ORCATau, float _SpeedDecayFactor)
		{
			m_Radius = _Radius;
			m_MaxSpeed = _MaxSpeed;
			m_ORCATau = _ORCATau;
			m_SpeedDecayFactor = _SpeedDecayFactor;
			m_Mover = _Mover;
			m_TrianglesVOProvider = new StaticTrianglesVOProvider(_Mover, _Radius, _MaxSpeed, _ORCATau, _ReturnIntersecting: true);
		}

		public Vector3 ResolveVelocity()
		{
			Vector3 position = m_Mover.GetPosition();
			TryUpdateNeighborMovers();
			Vector3 zero = Vector3.zero;
			List<IVO> list = new List<IVO>();
			foreach (IMovable nearestMover in m_NearestMovers)
			{
				if (nearestMover != m_Mover)
				{
					float radius = nearestMover.GetRadius();
					float num = m_Radius + radius;
					Vector3 vector = position - nearestMover.GetPosition();
					float magnitude = vector.magnitude;
					if (!(magnitude > num))
					{
						zero += vector * (num / magnitude);
						VOAgent vOAgent = new VOAgent(m_Mover, nearestMover, m_ORCATau);
						vOAgent.ComputeVO();
						list.Add(vOAgent);
					}
				}
			}
			IVO vO = m_TrianglesVOProvider.GetVO();
			if (vO != null)
			{
				Nav3D.LocalAvoidance.SupportingMath.Plane oRCA = vO.GetORCA();
				float num2 = m_Radius - oRCA.DistanceToPoint(position);
				zero += oRCA.Normal * ((Mathf.Sign(num2) > 0f) ? num2 : (0f - num2));
				list.Add(vO);
			}
			Vector3 result = Vector3.zero;
			if (list.Any())
			{
				int count = list.Count;
				List<Nav3D.LocalAvoidance.SupportingMath.Plane> planeConstraints = new List<Nav3D.LocalAvoidance.SupportingMath.Plane>(list.Select((IVO _VO) => _VO.GetORCA()));
				result = LPSolver.Instance.SolveMax(planeConstraints, new Sphere(Vector3.zero, m_MaxSpeed), (zero / count).normalized * m_MaxSpeed * (m_MaxSpeed / m_Radius));
			}
			else
			{
				Vector3 lastFrameVelocity = m_Mover.GetLastFrameVelocity();
				if (lastFrameVelocity != Vector3.zero)
				{
					result = Vector3.Lerp(lastFrameVelocity, Vector3.zero, m_SpeedDecayFactor);
				}
			}
			return result;
		}

		private void TryUpdateNeighborMovers()
		{
			if (m_Mover.IsNeighborMoversDirty)
			{
				m_NearestMovers.Clear();
				m_NearestMovers.AddRange(AgentManager.Instance.GetBucketMovables(m_Mover.GetPosition()));
				m_Mover.SetNeighborMovablesDirty(_Dirty: false);
			}
		}
	}
}
