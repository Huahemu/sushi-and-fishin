using UnityEngine;

namespace Nav3D.Common
{
	public class Triangle : IBoundable, IBoundsIntersectable
	{
		private Plane? m_V1Solid;

		private Plane? m_V2Solid;

		private Plane? m_V3Solid;

		public Vector3 V1 { get; }

		public Vector3 V2 { get; }

		public Vector3 V3 { get; }

		public Vector3 Normal { get; }

		public Bounds Bounds { get; }

		public Plane Plane { get; }

		private Vector3 V12 { get; }

		private Vector3 V31 { get; }

		private Vector3 V23 { get; }

		private Vector3 Center => (V1 + V2 + V3) / 3f;

		public Triangle(Vector3 _V1, Vector3 _V2, Vector3 _V3)
		{
			V1 = _V1;
			V2 = _V2;
			V3 = _V3;
			V12 = V2 - V1;
			V23 = V3 - V2;
			V31 = V1 - V3;
			Normal = Vector3.Cross(V12, V23).normalized;
			float num = float.PositiveInfinity;
			float num2 = float.NegativeInfinity;
			float num3 = float.PositiveInfinity;
			float num4 = float.NegativeInfinity;
			float num5 = float.PositiveInfinity;
			float num6 = float.NegativeInfinity;
			if (V1.x < num)
			{
				num = V1.x;
			}
			if (V1.x > num2)
			{
				num2 = V1.x;
			}
			if (V2.x < num)
			{
				num = V2.x;
			}
			if (V2.x > num2)
			{
				num2 = V2.x;
			}
			if (V3.x < num)
			{
				num = V3.x;
			}
			if (V3.x > num2)
			{
				num2 = V3.x;
			}
			if (V1.y < num3)
			{
				num3 = V1.y;
			}
			if (V1.y > num4)
			{
				num4 = V1.y;
			}
			if (V2.y < num3)
			{
				num3 = V2.y;
			}
			if (V2.y > num4)
			{
				num4 = V2.y;
			}
			if (V3.y < num3)
			{
				num3 = V3.y;
			}
			if (V3.y > num4)
			{
				num4 = V3.y;
			}
			if (V1.z < num5)
			{
				num5 = V1.z;
			}
			if (V1.z > num6)
			{
				num6 = V1.z;
			}
			if (V2.z < num5)
			{
				num5 = V2.z;
			}
			if (V2.z > num6)
			{
				num6 = V2.z;
			}
			if (V3.z < num5)
			{
				num5 = V3.z;
			}
			if (V3.z > num6)
			{
				num6 = V3.z;
			}
			Bounds = ExtensionBounds.MinMax(new Vector3(num, num3, num5), new Vector3(num2, num4, num6));
			Plane = new Plane(V1, V2, V3);
			m_V1Solid = (m_V2Solid = (m_V3Solid = null));
		}

		public void InitSolidHunkTriangles()
		{
			m_V1Solid = new Plane(V1 + Normal, V2, V1);
			m_V2Solid = new Plane(V2 + Normal, V3, V2);
			m_V3Solid = new Plane(V3 + Normal, V1, V3);
		}

		public bool FartherFromPoint(Vector3 _Point, float _DistSqrThreshold)
		{
			if ((_Point - V1).sqrMagnitude < _DistSqrThreshold)
			{
				return false;
			}
			if ((_Point - V2).sqrMagnitude < _DistSqrThreshold)
			{
				return false;
			}
			if ((_Point - V3).sqrMagnitude < _DistSqrThreshold)
			{
				return false;
			}
			return true;
		}

		public bool InsideOfSolidHunk(Vector3 _Point)
		{
			Plane? v1Solid = m_V1Solid;
			Plane plane2;
			if (!v1Solid.HasValue)
			{
				Plane? plane = (m_V1Solid = new Plane(V1 + Normal, V2, V1));
				plane2 = plane.Value;
			}
			else
			{
				plane2 = v1Solid.GetValueOrDefault();
			}
			Plane plane3 = plane2;
			v1Solid = m_V2Solid;
			Plane plane4;
			if (!v1Solid.HasValue)
			{
				Plane? plane = (m_V2Solid = new Plane(V2 + Normal, V3, V2));
				plane4 = plane.Value;
			}
			else
			{
				plane4 = v1Solid.GetValueOrDefault();
			}
			Plane plane5 = plane4;
			v1Solid = m_V3Solid;
			Plane plane6;
			if (!v1Solid.HasValue)
			{
				Plane? plane = (m_V3Solid = new Plane(V3 + Normal, V1, V3));
				plane6 = plane.Value;
			}
			else
			{
				plane6 = v1Solid.GetValueOrDefault();
			}
			Plane plane7 = plane6;
			if (plane3.GetSide(_Point) && plane5.GetSide(_Point))
			{
				return plane7.GetSide(_Point);
			}
			return false;
		}

		public void Visualize(bool DrawNormal = false)
		{
			Gizmos.DrawLine(V1, V2);
			Gizmos.DrawLine(V1, V3);
			Gizmos.DrawLine(V2, V3);
			if (DrawNormal)
			{
				float num = Mathf.Max(Vector3.Distance(V1, V2), Vector3.Distance(V1, V3), Vector3.Distance(V2, V3));
				Gizmos.DrawLine(Center, Center + Normal.normalized * num);
			}
		}

		public bool Intersects(Vector3 _SphereCenter, float _Radius)
		{
			return (ClosestPointOnTriangle(_SphereCenter) - _SphereCenter).sqrMagnitude <= _Radius * _Radius;
		}

		public Vector3 ClosestPointOnTriangle(Vector3 _Point)
		{
			Vector3 vector = V2 - V1;
			Vector3 vector2 = V3 - V1;
			Vector3 rhs = _Point - V1;
			float num = Vector3.Dot(vector, rhs);
			float num2 = Vector3.Dot(vector2, rhs);
			if (num <= 0f && num2 <= 0f)
			{
				return V1;
			}
			Vector3 rhs2 = _Point - V2;
			float num3 = Vector3.Dot(vector, rhs2);
			float num4 = Vector3.Dot(vector2, rhs2);
			if (num3 >= 0f && num4 <= num3)
			{
				return V2;
			}
			float num5 = num * num4 - num3 * num2;
			if (num5 <= 0f && num >= 0f && num3 <= 0f)
			{
				float num6 = num / (num - num3);
				return V1 + num6 * vector;
			}
			Vector3 rhs3 = _Point - V3;
			float num7 = Vector3.Dot(vector, rhs3);
			float num8 = Vector3.Dot(vector2, rhs3);
			if (num8 >= 0f && num7 <= num8)
			{
				return V3;
			}
			float num9 = num7 * num2 - num * num8;
			if (num9 <= 0f && num2 >= 0f && num8 <= 0f)
			{
				float num10 = num2 / (num2 - num8);
				return V1 + num10 * vector2;
			}
			float num11 = num3 * num8 - num7 * num4;
			if (num11 <= 0f && num4 - num3 >= 0f && num7 - num8 >= 0f)
			{
				float num12 = (num4 - num3) / (num4 - num3 + (num7 - num8));
				return V2 + num12 * (V3 - V2);
			}
			float num13 = 1f / (num11 + num9 + num5);
			float num14 = num9 * num13;
			float num15 = num5 * num13;
			return V1 + vector * num14 + vector2 * num15;
		}

		public bool Intersects(Bounds _Bounds)
		{
			if (_Bounds.DoesNotIntersects(Bounds))
			{
				return false;
			}
			return TriangleCubeIntersectionChecker.Inside(this, _Bounds);
		}

		public string ToStringExt()
		{
			return "V1: " + V1.ToStringExt() + ", V2: " + V2.ToStringExt() + ", V3: " + V3.ToStringExt();
		}
	}
}
