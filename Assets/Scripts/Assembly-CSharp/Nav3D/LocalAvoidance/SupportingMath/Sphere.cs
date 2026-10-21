using System;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.LocalAvoidance.SupportingMath
{
	public class Sphere : SpatialShape
	{
		private Vector3 m_Center;

		private float m_Radius;

		private float m_SqrRadius;

		public Vector3 Center => m_Center;

		public float Radius
		{
			get
			{
				return m_Radius;
			}
			set
			{
				if (!(value <= 0f))
				{
					m_Radius = value;
					m_SqrRadius = 0f;
				}
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
				return m_SqrRadius = m_Radius * m_Radius;
			}
		}

		public Sphere ZeroFlipped => new Sphere(-m_Center, m_Radius);

		public Sphere(Vector3 _Center, float _Radius)
		{
			m_Center = _Center;
			m_Radius = _Radius;
		}

		public Vector3 GetClosestSurfaceNormal(Vector3 _Point)
		{
			return (_Point - m_Center).normalized;
		}

		public override Vector3 GetClosestPoint(Vector3 _Point)
		{
			if (m_Center == _Point)
			{
				throw new ArgumentException("Point must be unequal to the sphere center");
			}
			return m_Center + (_Point - m_Center).normalized * m_Radius;
		}

		public bool IsPointInside(Vector3 _Point)
		{
			return Vector3.SqrMagnitude(m_Center - _Point) <= SqrRadius + 0.1f;
		}

		public Vector3[] Intersection(ILine _Line)
		{
			if (_Line is Straight straight)
			{
				Vector3 closestPoint = straight.GetClosestPoint(m_Center);
				float sqrMagnitude = (closestPoint - m_Center).sqrMagnitude;
				float num = Mathf.Sqrt(SqrRadius - sqrMagnitude);
				return new Vector3[2]
				{
					closestPoint + straight.Direction * num,
					closestPoint - straight.Direction * num
				};
			}
			if (_Line is Circle circle)
			{
				Circle circle2 = new Circle(circle.GeneratrixPlane, this);
				Vector3 rhs = circle2.Center - circle.Center;
				float magnitude = rhs.magnitude;
				float num2 = 2f * UtilsMath.TriangleHeronSquare(circle.Radius, circle2.Radius, magnitude) / magnitude;
				float num3 = Mathf.Sqrt(circle.SqrRadius - num2 * num2);
				Vector3 vector = circle.Center + rhs.normalized * num3;
				Vector3 normalized = Vector3.Cross(circle.GeneratrixPlane.Normal, rhs).normalized;
				return new Vector3[2]
				{
					vector + normalized * num2,
					vector - normalized * num2
				};
			}
			throw new NotImplementedException("[Sphere] Unknown intersection for type:" + _Line.GetType().FullName);
		}

		public override IntersectionType CheckIntersection(ILine _Line)
		{
			if (_Line is Straight straight)
			{
				Vector3 closestPoint = straight.GetClosestPoint(m_Center);
				if (!(Vector3.SqrMagnitude(m_Center - closestPoint) < SqrRadius))
				{
					return IntersectionType.NONINTERSECTION;
				}
				return IntersectionType.INTERSECTION;
			}
			if (_Line is Circle circle)
			{
				if (this == circle.GeneratrixSphere)
				{
					return IntersectionType.BELONGING;
				}
				if (IsIntersect(circle.GeneratrixPlane))
				{
					Circle circle2 = new Circle(circle.GeneratrixPlane, this);
					float radius;
					float radius2;
					if (circle.Radius >= circle2.Radius)
					{
						radius = circle.Radius;
						radius2 = circle2.Radius;
					}
					else
					{
						radius = circle2.Radius;
						radius2 = circle.Radius;
					}
					float num = radius - radius2;
					float num2 = radius + radius2;
					float num3 = Vector3.Magnitude(circle.Center - circle2.Center);
					if (!(num3 <= num2) || !(num3 >= num))
					{
						return IntersectionType.NONINTERSECTION;
					}
					return IntersectionType.INTERSECTION;
				}
				return IntersectionType.NONINTERSECTION;
			}
			throw new NotImplementedException("[Sphere] Unknown intersection for type:" + _Line.GetType().FullName);
		}

		public override bool IsIntersect(SpatialShape _Other)
		{
			if (_Other is Sphere sphere)
			{
				return Vector3.Distance(m_Center, sphere.m_Center) < m_Radius + sphere.Radius;
			}
			if (_Other is Plane plane)
			{
				return Mathf.Abs(plane.DistanceToPoint(m_Center)) < m_Radius;
			}
			if (_Other is Circle circle)
			{
				if (Vector3.SqrMagnitude(m_Center - circle.GeneratrixSphere.m_Center) <= SqrRadius + circle.GeneratrixSphere.SqrRadius)
				{
					return Mathf.Abs(circle.GeneratrixPlane.DistanceToPoint(m_Center)) <= m_Radius;
				}
				return false;
			}
			throw new NotImplementedException("[Sphere] Unknown intersection for type:" + _Other.GetType().FullName);
		}
	}
}
