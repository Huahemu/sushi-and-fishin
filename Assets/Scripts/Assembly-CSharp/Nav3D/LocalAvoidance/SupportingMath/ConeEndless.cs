using System;
using UnityEngine;

namespace Nav3D.LocalAvoidance.SupportingMath
{
	public class ConeEndless : SpatialShape
	{
		protected Vector3 m_Point;

		protected Vector3 m_Direction;

		protected float m_Alpha;

		protected float m_TgAlpha;

		private float m_DirectionMagnitudeDebug;

		private float m_SphereRadiusDebug;

		public float Angle => m_Alpha;

		public ConeEndless(Vector3 _Point, Sphere _Sphere)
		{
			m_Point = _Point;
			m_Direction = _Sphere.Center - _Point;
			m_Alpha = Mathf.Asin(Mathf.Clamp(_Sphere.Radius / m_Direction.magnitude, -1f + Mathf.Epsilon, 1f - Mathf.Epsilon));
			if (float.IsNaN(m_Alpha))
			{
				Debug.LogError($"m_Alpha is NaN: _Sphere.Radius: {_Sphere.Radius}, m_Direction.magnitude: {m_Direction.magnitude}");
			}
			m_TgAlpha = Mathf.Tan(m_Alpha);
			m_DirectionMagnitudeDebug = m_Direction.magnitude;
			m_SphereRadiusDebug = _Sphere.Radius;
			m_Direction.Normalize();
		}

		public ConeEndless(Vector3 _Point, Vector3 _Direcrion, float _TgAlpha)
		{
			if (_TgAlpha < 0f)
			{
				throw new ArgumentException("Tanget must be positive");
			}
			m_Point = _Point;
			m_Direction = _Direcrion;
			m_TgAlpha = _TgAlpha;
		}

		public override Vector3 GetClosestPoint(Vector3 _Point)
		{
			Vector3 vector = _Point - m_Point;
			if (vector == Vector3.zero)
			{
				return m_Point;
			}
			double d = (double)m_Alpha - (double)(Vector3.Angle(m_Direction, vector) * (MathF.PI / 180f));
			double num = (double)vector.magnitude * Math.Cos(d);
			Vector3 normalized = Vector3.Cross(m_Direction, vector).normalized;
			return m_Point + (Quaternion.AngleAxis(m_Alpha * 57.29578f, normalized) * m_Direction).normalized * (float)num;
		}

		public virtual Vector3 GetClosestSurfaceNormal(Vector3 _Point)
		{
			float magnitude = (new Straight(m_Direction, m_Point).GetClosestPoint(_Point) - m_Point).magnitude;
			float magnitude2 = (_Point - m_Point).magnitude;
			float num = Mathf.Cos(Mathf.Acos(magnitude / magnitude2) - m_Alpha) * magnitude2 / Mathf.Cos(m_Alpha);
			Vector3 vector = m_Point + m_Direction * num;
			return (_Point - vector).normalized;
		}

		public virtual bool IsPointInside(Vector3 _Point)
		{
			return Vector3.Angle(_Point - m_Point, m_Direction) < Mathf.Atan(m_TgAlpha) * 57.29578f;
		}
	}
}
