using System;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.LocalAvoidance.SupportingMath
{
	public class Plane : SpatialShape
	{
		private float m_A;

		private float m_B;

		private float m_C;

		private float m_Distance;

		private Vector3 m_Normal;

		private Vector3 m_BelongingPoint;

		public Vector3 ProjPoint;

		public float Distance => m_Distance;

		public Vector3 Normal => m_Normal;

		public Vector3 BelongingPoint => m_BelongingPoint;

		public Plane(Vector3 _Normal, Vector3 _Point)
		{
			m_Normal = _Normal.normalized;
			m_Distance = 0f - Vector3.Dot(m_Normal, _Point);
			m_BelongingPoint = _Point;
		}

		public float DistanceToPoint(Vector3 _Point)
		{
			return Vector3.Dot(m_Normal, _Point) + m_Distance;
		}

		public void Translate(Vector3 _Translation)
		{
			m_BelongingPoint += _Translation;
			m_Distance = 0f - Vector3.Dot(m_Normal, m_BelongingPoint);
		}

		public (Vector3 Point, float Distance) GetSurfaceClosestPoint(Vector3 _Point)
		{
			float num = DistanceToPoint(_Point);
			return (Point: _Point - m_Normal * num, Distance: num);
		}

		public bool GetSide(Vector3 _Point)
		{
			return (double)Vector3.Dot(m_Normal, _Point) + (double)m_Distance > 0.0;
		}

		public bool IsBelongsHalfPlane(Vector3 _Point)
		{
			return (double)Vector3.Dot(m_Normal, _Point) + (double)m_Distance > -9.999999974752427E-07;
		}

		public bool IsPointBelongs(Vector3 _Point)
		{
			return Mathf.Approximately(DistanceToPoint(_Point), 0f);
		}

		public Straight Intersection(Plane _Other)
		{
			Vector3 direction = Vector3.Cross(Normal, _Other.Normal);
			float f = Mathf.Acos(Mathf.Abs(Vector3.Dot(Normal, _Other.Normal)));
			Vector3 belongingPoint = BelongingPoint;
			Vector3 closestPoint = _Other.GetClosestPoint(belongingPoint);
			Vector3 normalized = (GetClosestPoint(closestPoint) - belongingPoint).normalized;
			float num = Vector3.Distance(belongingPoint, closestPoint) / Mathf.Sin(f);
			Vector3 point = belongingPoint + normalized * num;
			return new Straight(direction, point);
		}

		public Vector3[] Intersection(ILine _Line)
		{
			if (_Line is Straight straight)
			{
				if (IntersectionWithLine(straight.Point, straight.Direction, out var _Point))
				{
					return new Vector3[1] { _Point };
				}
				return new Vector3[1] { Vector3.zero };
			}
			if (_Line is Circle circle)
			{
				Straight straight2 = Intersection(circle.GeneratrixPlane);
				(Vector3, float) surfaceClosestPoint = GetSurfaceClosestPoint(circle.GeneratrixSphere.Center);
				Vector3 item = surfaceClosestPoint.Item1;
				float num = Mathf.Sqrt(circle.GeneratrixSphere.SqrRadius - surfaceClosestPoint.Item2);
				return new Vector3[2]
				{
					item + straight2.Direction * num,
					item - straight2.Direction * num
				};
			}
			throw new NotImplementedException("[Plane] Unknown intersection for type:" + _Line.GetType().FullName);
		}

		public static bool IsPlanesParallel(Plane _PlaneA, Plane _PlaneB)
		{
			return Vector3.Cross(_PlaneA.Normal, _PlaneB.Normal) == Vector3.zero;
		}

		public override Vector3 GetClosestPoint(Vector3 _Point)
		{
			return GetSurfaceClosestPoint(_Point).Point;
		}

		public override IntersectionType CheckIntersection(ILine _Line)
		{
			if (_Line is Straight straight)
			{
				if (Mathf.Approximately(0f, Vector3.Dot(Normal, straight.Direction)))
				{
					if (!Mathf.Approximately(0f, DistanceToPoint(straight.Point)))
					{
						return IntersectionType.NONINTERSECTION;
					}
					return IntersectionType.BELONGING;
				}
				return IntersectionType.INTERSECTION;
			}
			if (_Line is Circle circle)
			{
				if (circle.GeneratrixPlane.IsPointBelongs(m_BelongingPoint) && IsPlanesParallel(this, circle.GeneratrixPlane))
				{
					return IntersectionType.BELONGING;
				}
				Vector3 closestPoint = Intersection(circle.GeneratrixPlane).GetClosestPoint(circle.GeneratrixSphere.Center);
				if (!circle.GeneratrixSphere.IsPointInside(closestPoint))
				{
					return IntersectionType.NONINTERSECTION;
				}
				return IntersectionType.INTERSECTION;
			}
			throw new NotImplementedException("[Plane] Unknown intersection for type:" + _Line.GetType().FullName);
		}

		private bool IntersectionWithSegment(Vector3 _Start, Vector3 _End, out Vector3 _Point)
		{
			Vector3 vector = _End - _Start;
			float num = Vector3.Dot(vector, Normal);
			if (Math.Abs(num) < float.Epsilon)
			{
				_Point = Vector3.zero;
				return false;
			}
			float num2 = (0f - Vector3.Dot(_Start, Normal) - m_Distance) / num;
			if (num2 < 0f || num2 > 1f)
			{
				_Point = Vector3.zero;
				return false;
			}
			_Point = _Start + vector * num2;
			return true;
		}

		private bool IntersectionWithLine(Vector3 _Origin, Vector3 _Direction, out Vector3 _Point)
		{
			float num = Vector3.Dot(_Direction, Normal);
			if (Mathf.Abs(num) < float.Epsilon)
			{
				_Point = Vector3.zero;
				return false;
			}
			float num2 = (0f - Vector3.Dot(_Origin, Normal) - m_Distance) / num;
			_Point = _Origin + _Direction * num2;
			return true;
		}
	}
}
