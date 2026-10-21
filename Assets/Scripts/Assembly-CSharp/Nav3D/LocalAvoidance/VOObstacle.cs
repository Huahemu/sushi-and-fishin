using Nav3D.Common;
using Nav3D.LocalAvoidance.SupportingMath;
using UnityEngine;

namespace Nav3D.LocalAvoidance
{
	public class VOObstacle : IVO
	{
		private readonly IMovable m_Solver;

		private readonly Triangle m_Triangle;

		private ConeArced m_VoCone;

		private readonly float m_Tau;

		public VOObstacle(IMovable _SolverAgent, Triangle _Triangle, float _Tau = 2f)
		{
			m_Solver = _SolverAgent;
			m_Triangle = _Triangle;
			m_Tau = _Tau;
		}

		public Nav3D.LocalAvoidance.SupportingMath.Plane GetORCA()
		{
			ComputeVO();
			Vector3 lastFrameVelocity = m_Solver.GetLastFrameVelocity();
			Vector3 u = GetU(lastFrameVelocity);
			Vector3 point = lastFrameVelocity + u;
			return new Nav3D.LocalAvoidance.SupportingMath.Plane(m_Triangle.Normal, point);
		}

		private Vector3 GetU(Vector3 _InnerPoint)
		{
			return m_VoCone.GetClosestPoint(_InnerPoint) - _InnerPoint;
		}

		private void ComputeVO()
		{
			Vector3 position = m_Solver.GetPosition();
			Vector3 center = m_Triangle.Plane.ClosestPointOnPlane(position + m_Solver.GetLastFrameVelocity() * m_Tau) - position;
			float num = m_Solver.GetRadius() * 2f;
			if (center.sqrMagnitude < UtilsMath.Sqr(num))
			{
				center = position + center.normalized * (num + 1E-05f);
			}
			Vector3 zero = Vector3.zero;
			Sphere sphere = new Sphere(center, num);
			m_VoCone = new ConeArced(zero, sphere, m_Tau);
		}
	}
}
