using UnityEngine;

namespace Nav3D.LocalAvoidance.SupportingMath
{
	public class Cone : ConeEndless
	{
		protected float m_Height;

		protected float m_Radius;

		public Cone(Vector3 _Point, Sphere _Sphere)
			: base(_Point, _Sphere)
		{
			float num = Vector3.Magnitude(_Sphere.Center - _Point);
			m_Height = num + _Sphere.Radius;
			m_Radius = m_TgAlpha * m_Height;
		}

		public override bool IsPointInside(Vector3 _Point)
		{
			Vector3 vector = _Point - m_Point;
			if (Vector3.Angle(vector, m_Direction) < Mathf.Atan(m_TgAlpha) * 57.29578f)
			{
				return Vector3.Project(vector, m_Direction).magnitude < m_Height;
			}
			return false;
		}
	}
}
