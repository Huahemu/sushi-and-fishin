using System;
using UnityEngine;

namespace Nav3D.LocalAvoidance.SupportingMath
{
	public class ConeArced : Cone
	{
		private readonly Sphere m_BaseSphere;

		private Sphere m_SecantSphere;

		private readonly double m_SmallBaseRadius;

		private readonly Vector3 m_SmallBasePoint;

		private readonly double m_TopToBaseGeneratrixLength;

		private readonly double m_TopToBaseGeneratrixSqrLength;

		private float m_Tau;

		public float Tau
		{
			get
			{
				return m_Tau;
			}
			set
			{
				m_Tau = value;
				m_SecantSphere = new Sphere(m_Point + (m_BaseSphere.Center - m_Point) / m_Tau, m_BaseSphere.Radius / m_Tau);
			}
		}

		public ConeArced ZeroFlipped => new ConeArced(-m_Point, m_BaseSphere.ZeroFlipped, m_Tau);

		public Sphere SecantSphere => m_SecantSphere;

		public ConeArced(Vector3 _Point, Sphere _Sphere, float _Tau)
			: base(_Point, _Sphere)
		{
			m_BaseSphere = _Sphere;
			m_SecantSphere = new Sphere(_Point + (_Sphere.Center - _Point) / _Tau, _Sphere.Radius / _Tau);
			float num = MathF.PI / 2f - m_Alpha;
			m_SmallBaseRadius = (double)m_SecantSphere.Radius * Math.Sin(num);
			m_SmallBasePoint = m_SecantSphere.Center + (_Point - m_SecantSphere.Center).normalized * m_SecantSphere.Radius * Mathf.Cos(num);
			m_TopToBaseGeneratrixLength = m_SmallBaseRadius / Math.Sin(m_Alpha);
			m_TopToBaseGeneratrixSqrLength = m_TopToBaseGeneratrixLength * m_TopToBaseGeneratrixLength;
			m_Tau = _Tau;
		}

		public override Vector3 GetClosestPoint(Vector3 _InnerPoint)
		{
			Vector3 vector = base.GetClosestPoint(_InnerPoint);
			if (float.IsNaN(vector.x))
			{
				vector = _InnerPoint;
			}
			if ((double)(m_Point - vector).sqrMagnitude <= m_TopToBaseGeneratrixSqrLength)
			{
				return m_SecantSphere.GetClosestPoint(_InnerPoint);
			}
			return vector;
		}

		public override Vector3 GetClosestSurfaceNormal(Vector3 _InnerPoint)
		{
			Vector3 closestPoint = base.GetClosestPoint(_InnerPoint);
			if ((double)(m_Point - closestPoint).sqrMagnitude < m_TopToBaseGeneratrixSqrLength)
			{
				return m_SecantSphere.GetClosestSurfaceNormal(_InnerPoint);
			}
			return base.GetClosestSurfaceNormal(_InnerPoint);
		}

		public override bool IsPointInside(Vector3 _Point)
		{
			Vector3 closestPoint = base.GetClosestPoint(_Point);
			if ((double)(m_Point - closestPoint).sqrMagnitude < m_TopToBaseGeneratrixSqrLength)
			{
				return Vector3.Distance(m_SecantSphere.Center, _Point) < m_SecantSphere.Radius;
			}
			return base.IsPointInside(_Point);
		}
	}
}
