using Nav3D.Common;
using Nav3D.LocalAvoidance.SupportingMath;
using UnityEngine;

namespace Nav3D.LocalAvoidance
{
	public class VOAgent : SpatialShape, IVO
	{
		private Vector3 m_DeltaVOpt;

		private Vector3 m_DebugU;

		private ConeArced m_VoCone;

		protected readonly IMovable m_SolverAgent;

		private readonly IMovable m_OtherAgent;

		private readonly float m_Tau;

		public VOAgent Flipped => new VOAgent(m_VoCone.ZeroFlipped, m_OtherAgent, m_SolverAgent);

		public VOAgent(IMovable _SolverAgent, IMovable _OtherAgent, float _Tau = 2f)
		{
			m_SolverAgent = _SolverAgent;
			m_OtherAgent = _OtherAgent;
			m_Tau = _Tau;
		}

		public VOAgent(ConeArced _VOCone, IMovable _SolverAgent, IMovable _OtherAgent)
		{
			m_VoCone = _VOCone;
			m_SolverAgent = _SolverAgent;
			m_OtherAgent = _OtherAgent;
		}

		public virtual void ComputeVO()
		{
			Vector3 vector = m_OtherAgent.GetPosition() - m_SolverAgent.GetPosition();
			float num = m_SolverAgent.GetRadius() + m_OtherAgent.GetRadius();
			if (vector.sqrMagnitude < UtilsMath.Sqr(num))
			{
				vector = CorrectDeltaPosWithPushingOut(vector, num);
			}
			Vector3 zero = Vector3.zero;
			Sphere sphere = new Sphere(vector, num);
			m_VoCone = new ConeArced(zero, sphere, m_Tau);
		}

		public Nav3D.LocalAvoidance.SupportingMath.Plane GetORCA()
		{
			Vector3 deltaVOpt = GetDeltaVOpt();
			Vector3 vector = (m_DebugU = GetU(deltaVOpt));
			Vector3 closestSurfaceNormal = m_VoCone.GetClosestSurfaceNormal(deltaVOpt);
			Vector3 point = m_SolverAgent.GetLastFrameVelocity() + (m_OtherAgent.Avoiding ? 0.5f : 1f) * vector;
			return new Nav3D.LocalAvoidance.SupportingMath.Plane(closestSurfaceNormal, point);
		}

		protected virtual Vector3 CorrectDeltaPosWithPushingOut(Vector3 _DeltaPos, float _RadiusSum)
		{
			return m_SolverAgent.GetPosition() + _DeltaPos.normalized * (_RadiusSum * 1.00001f);
		}

		private Vector3 GetDeltaVOpt()
		{
			Vector3 deltaVOpt = m_SolverAgent.GetLastFrameVelocity() - m_OtherAgent.GetLastFrameVelocity();
			return m_DeltaVOpt = deltaVOpt;
		}

		private Vector3 GetU(Vector3 _InnerPoint)
		{
			return m_VoCone.GetClosestPoint(_InnerPoint) - _InnerPoint;
		}
	}
}
