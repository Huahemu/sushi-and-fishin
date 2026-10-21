using System;
using UnityEngine;

namespace Nav3D.LocalAvoidance.SupportingMath
{
	public class Straight : SpatialShape, ILine
	{
		private Vector3 m_Direction;

		private Vector3 m_Point;

		public Vector3 Direction => m_Direction.normalized;

		public Vector3 Point => m_Point;

		public Straight(Vector3 _Direction, Vector3 _Point)
		{
			m_Direction = _Direction;
			m_Point = _Point;
			if (_Direction == Vector3.zero)
			{
				Debug.LogWarning("Direction vector must be nonzero");
			}
		}

		public Straight(Plane _PlaneA, Plane _PlaneB)
		{
			Straight straight = _PlaneA.Intersection(_PlaneB);
			m_Direction = straight.m_Direction;
			m_Point = straight.m_Point;
		}

		public override Vector3 GetClosestPoint(Vector3 _Point)
		{
			return new Plane(m_Direction, _Point).GetClosestPoint(m_Point);
		}

		public Vector3[] Intersection(SpatialShape _Figure)
		{
			if (_Figure is Plane plane)
			{
				return plane.Intersection(this);
			}
			if (_Figure is Sphere sphere)
			{
				return sphere.Intersection(this);
			}
			throw new NotImplementedException("[Straight] Unknown intersection for type:" + _Figure.GetType().FullName);
		}

		public Vector3 ClosestPoint(Vector3 _Point)
		{
			return GetClosestPoint(_Point);
		}
	}
}
