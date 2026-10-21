using System;
using UnityEngine;

namespace Nav3D.LocalAvoidance.SupportingMath
{
	public class Circle : SpatialShape, ILine
	{
		private Plane m_Plane;

		private Sphere m_Sphere;

		private Vector3? m_Center;

		private float m_Radius;

		private float m_SqrRadius;

		private const int VISUALISATION_SAMPLES_NUM = 16;

		public Plane GeneratrixPlane => m_Plane;

		public Sphere GeneratrixSphere => m_Sphere;

		public Vector3 Center
		{
			get
			{
				if (m_Center.HasValue)
				{
					return m_Center.Value;
				}
				Vector3? vector = (m_Center = m_Plane.GetClosestPoint(m_Sphere.Center));
				return vector.Value;
			}
		}

		public float Radius
		{
			get
			{
				if (m_Radius != 0f)
				{
					return m_Radius;
				}
				return m_Radius = Mathf.Sqrt(m_Sphere.SqrRadius - (m_Sphere.Center - Center).sqrMagnitude);
			}
		}

		public float SqrRadius
		{
			get
			{
				if (m_SqrRadius != 0f)
				{
					return m_SqrRadius;
				}
				return m_SqrRadius = Radius * Radius;
			}
		}

		public Circle(Plane _Plane, Sphere _Sphere)
		{
			if (Mathf.Abs(_Plane.DistanceToPoint(_Sphere.Center)) >= _Sphere.Radius)
			{
				throw new ArgumentException("[Circle] No intersection");
			}
			m_Plane = _Plane;
			m_Sphere = _Sphere;
		}

		public override Vector3 GetClosestPoint(Vector3 _Point)
		{
			(Vector3 Point, float Distance) surfaceClosestPoint = m_Plane.GetSurfaceClosestPoint(_Point);
			(Vector3, float) surfaceClosestPoint2 = m_Plane.GetSurfaceClosestPoint(m_Sphere.Center);
			float num = Mathf.Sqrt(m_Sphere.SqrRadius - surfaceClosestPoint2.Item2 * surfaceClosestPoint2.Item2);
			Vector3 normalized = (surfaceClosestPoint.Point - surfaceClosestPoint2.Item1).normalized;
			return surfaceClosestPoint2.Item1 + normalized * num;
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
			throw new NotImplementedException("[Circle] Unknown intersection for type:" + _Figure.GetType().FullName);
		}

		public Vector3 ClosestPoint(Vector3 _Point)
		{
			return GetClosestPoint(_Point);
		}
	}
}
