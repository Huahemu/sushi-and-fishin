using System;
using System.Collections.Generic;
using System.Linq;
using Nav3D.LocalAvoidance.SupportingMath;
using UnityEngine;

namespace Nav3D.Common
{
	public static class ExtensionBounds
	{
		private const string UNION_NO_PARAMS_ERROR = "There is no any parameters for the Bounds.Union() method.";

		public static string ToStringExt(this Bounds _Bounds)
		{
			return "Center: " + _Bounds.center.ToStringExt() + ", Size: " + _Bounds.size.ToStringExt();
		}

		public static void Draw(this Bounds _Bounds)
		{
			Gizmos.DrawWireCube(_Bounds.center, _Bounds.size);
		}

		public static void DrawSolid(this Bounds _Bounds)
		{
			Gizmos.DrawCube(_Bounds.center, _Bounds.size);
		}

		public static float GetMaxSize(this Bounds _Bounds)
		{
			return Mathf.Max(_Bounds.size.x, _Bounds.size.y, _Bounds.size.z);
		}

		public static float GetMinSize(this Bounds _Bounds)
		{
			return Mathf.Min(_Bounds.size.x, _Bounds.size.y, _Bounds.size.z);
		}

		public static bool DoesNotIntersects(this Bounds _Bounds, Bounds _Other)
		{
			Vector3 min = _Bounds.min;
			Vector3 max = _Bounds.max;
			Vector3 min2 = _Other.min;
			Vector3 max2 = _Other.max;
			if (!(min.x > max2.x) && !(max.x < min2.x) && !(min.y > max2.y) && !(max.y < min2.y) && !(min.z > max2.z))
			{
				return max.z < min2.z;
			}
			return true;
		}

		public static Bounds TrianglesBounds(List<Triangle> _Triangles)
		{
			Bounds bounds = _Triangles.First().Bounds;
			if (_Triangles.Count == 1)
			{
				return bounds;
			}
			for (int i = 1; i < _Triangles.Count; i++)
			{
				bounds = bounds.Union(_Triangles[i].Bounds);
			}
			return bounds;
		}

		public static Bounds PointBounds(Vector3[] _Points)
		{
			float num = float.MaxValue;
			float num2 = float.MaxValue;
			float num3 = float.MaxValue;
			float num4 = float.MinValue;
			float num5 = float.MinValue;
			float num6 = float.MinValue;
			for (int i = 0; i < _Points.Length; i++)
			{
				Vector3 vector = _Points[i];
				num = Mathf.Min(num, vector.x);
				num2 = Mathf.Min(num2, vector.y);
				num3 = Mathf.Min(num3, vector.z);
				num4 = Mathf.Max(num4, vector.x);
				num5 = Mathf.Max(num5, vector.y);
				num6 = Mathf.Max(num6, vector.z);
			}
			float num7 = num4 - num;
			float num8 = num5 - num2;
			float num9 = num6 - num3;
			Vector3 center = new Vector3(num + num7 * 0.5f, num2 + num8 * 0.5f, num3 + num9 * 0.5f);
			Vector3 size = new Vector3(num7, num8, num9);
			return new Bounds(center, size);
		}

		public static bool Embrace(this Bounds _Bounds, Bounds _OtherBounds)
		{
			return _OtherBounds.GetCornerPoints().All((Vector3 _Point) => _Bounds.Contains(_Point));
		}

		public static Bounds MinMax(Vector3 _A, Vector3 _B)
		{
			float num = Mathf.Min(_A.x, _B.x);
			float num2 = Mathf.Min(_A.y, _B.y);
			float num3 = Mathf.Min(_A.z, _B.z);
			float num4 = Mathf.Max(_A.x, _B.x);
			float num5 = Mathf.Max(_A.y, _B.y);
			float num6 = Mathf.Max(_A.z, _B.z);
			float num7 = num4 - num;
			float num8 = num5 - num2;
			float num9 = num6 - num3;
			Vector3 center = new Vector3(num + num7 * 0.5f, num2 + num8 * 0.5f, num3 + num9 * 0.5f);
			Vector3 size = new Vector3(num7, num8, num9);
			return new Bounds(center, size);
		}

		public static Bounds Intersection(this Bounds _BoundsA, Bounds _BoundsB)
		{
			Vector3 vector = new Vector3(Mathf.Min(_BoundsA.min.x, _BoundsA.max.x), Mathf.Min(_BoundsA.min.y, _BoundsA.max.y), Mathf.Min(_BoundsA.min.z, _BoundsA.max.z));
			Vector3 vector2 = new Vector3(Mathf.Max(_BoundsA.min.x, _BoundsA.max.x), Mathf.Max(_BoundsA.min.y, _BoundsA.max.y), Mathf.Max(_BoundsA.min.z, _BoundsA.max.z));
			Vector3 vector3 = new Vector3(Mathf.Min(_BoundsB.min.x, _BoundsB.max.x), Mathf.Min(_BoundsB.min.y, _BoundsB.max.y), Mathf.Min(_BoundsB.min.z, _BoundsB.max.z));
			Vector3 vector4 = new Vector3(Mathf.Max(_BoundsB.min.x, _BoundsB.max.x), Mathf.Max(_BoundsB.min.y, _BoundsB.max.y), Mathf.Max(_BoundsB.min.z, _BoundsB.max.z));
			if (!_BoundsA.Intersects(_BoundsB))
			{
				return new Bounds(Vector3.zero, Vector3.zero);
			}
			if (_BoundsA.Contains(vector3) && _BoundsA.Contains(vector4))
			{
				return _BoundsB;
			}
			if (_BoundsB.Contains(vector) && _BoundsB.Contains(vector2))
			{
				return _BoundsA;
			}
			if (_BoundsB.Contains(vector))
			{
				Vector3 vector5 = vector;
				Vector3 vector6 = default;
				vector6.x = Mathf.Min(vector4.x, vector2.x);
				vector6.y = Mathf.Min(vector4.y, vector2.y);
				vector6.z = Mathf.Min(vector4.z, vector2.z);
				return new Bounds(vector5 + (vector6 - vector5) * 0.5f, new Vector3(vector6.x - vector5.x, vector6.y - vector5.y, vector6.z - vector5.z));
			}
			if (_BoundsB.Contains(vector2))
			{
				Vector3 vector7 = vector2;
				Vector3 vector8 = default;
				vector8.x = Mathf.Max(vector.x, vector3.x);
				vector8.y = Mathf.Max(vector.y, vector3.y);
				vector8.z = Mathf.Max(vector.z, vector3.z);
				return new Bounds(vector8 + (vector7 - vector8) * 0.5f, new Vector3(vector7.x - vector8.x, vector7.y - vector8.y, vector7.z - vector8.z));
			}
			if (_BoundsA.Contains(vector3))
			{
				Vector3 vector9 = vector3;
				Vector3 vector10 = default;
				vector10.x = Mathf.Min(vector2.x, vector4.x);
				vector10.y = Mathf.Min(vector2.y, vector4.y);
				vector10.z = Mathf.Min(vector2.z, vector4.z);
				return new Bounds(vector9 + (vector10 - vector9) * 0.5f, new Vector3(vector10.x - vector9.x, vector10.y - vector9.y, vector10.z - vector9.z));
			}
			if (_BoundsA.Contains(vector4))
			{
				Vector3 vector11 = vector4;
				Vector3 vector12 = default;
				vector12.x = Mathf.Max(vector.x, vector3.x);
				vector12.y = Mathf.Max(vector.y, vector3.y);
				vector12.z = Mathf.Max(vector.z, vector3.z);
				return new Bounds(vector12 + (vector11 - vector12) * 0.5f, new Vector3(vector11.x - vector12.x, vector11.y - vector12.y, vector11.z - vector12.z));
			}
			Vector3 vector13 = default;
			vector13.x = Mathf.Max(vector.x, vector3.x);
			vector13.y = Mathf.Max(vector.y, vector3.y);
			vector13.z = Mathf.Max(vector.z, vector3.z);
			Vector3 vector14 = default;
			vector14.x = Mathf.Min(vector2.x, vector4.x);
			vector14.y = Mathf.Min(vector2.y, vector4.y);
			vector14.z = Mathf.Min(vector2.z, vector4.z);
			return new Bounds(vector13 + (vector14 - vector13) * 0.5f, new Vector3(vector14.x - vector13.x, vector14.y - vector13.y, vector14.z - vector13.z));
		}

		public static Bounds Union(this Bounds _BoundsA, Bounds _BoundsB)
		{
			_BoundsA.Encapsulate(_BoundsB.min);
			_BoundsA.Encapsulate(_BoundsB.max);
			return _BoundsA;
		}

		public static Bounds Union(params Bounds[] _Params)
		{
			if (!_Params.Any())
			{
				throw new Exception("There is no any parameters for the Bounds.Union() method.");
			}
			if (_Params.Length == 1)
			{
				return _Params.First();
			}
			Bounds bounds = _Params.First();
			for (int i = 1; i < _Params.Length; i++)
			{
				bounds = bounds.Union(_Params[i]);
			}
			return bounds;
		}

		public static Vector3[] GetCornerPoints(this Bounds _Bounds)
		{
			Vector3 center = _Bounds.center;
			Vector3 extents = _Bounds.extents;
			return new Vector3[8]
			{
				center + Vector3.Scale(extents, new Vector3(-1f, -1f, -1f)),
				center + Vector3.Scale(extents, new Vector3(-1f, -1f, 1f)),
				center + Vector3.Scale(extents, new Vector3(-1f, 1f, -1f)),
				center + Vector3.Scale(extents, new Vector3(1f, -1f, -1f)),
				center + Vector3.Scale(extents, new Vector3(-1f, 1f, 1f)),
				center + Vector3.Scale(extents, new Vector3(1f, -1f, 1f)),
				center + Vector3.Scale(extents, new Vector3(1f, 1f, -1f)),
				center + Vector3.Scale(extents, new Vector3(1f, 1f, 1f))
			};
		}

		public static Bounds CeilSizeToMultiple(this Bounds _Bounds, float _Multiple)
		{
			float x = _Bounds.size.x;
			float y = _Bounds.size.y;
			float z = _Bounds.size.z;
			int num = (int)Mathf.Ceil(x / _Multiple);
			int num2 = (int)Mathf.Ceil(y / _Multiple);
			int num3 = (int)Mathf.Ceil(z / _Multiple);
			x = (float)(num + 2) * _Multiple;
			y = (float)(num2 + 2) * _Multiple;
			z = (float)(num3 + 2) * _Multiple;
			return new Bounds(size: new Vector3(x, y, z), center: _Bounds.center);
		}

		public static Bounds Enlarge(this Bounds _Bounds, float _Enlargement)
		{
			return new Bounds(_Bounds.center, _Bounds.size + new Vector3(_Enlargement, _Enlargement, _Enlargement));
		}

		public static Vector3 GetClosestFaceCenterPlusNormal(this Bounds _Bounds, Vector3 _Point, out Vector3 _Normal)
		{
			float num = Mathf.Min(_Bounds.min.x, _Bounds.max.x);
			float num2 = Mathf.Max(_Bounds.min.x, _Bounds.max.x);
			float num3 = Mathf.Min(_Bounds.min.y, _Bounds.max.y);
			float num4 = Mathf.Max(_Bounds.min.y, _Bounds.max.y);
			float num5 = Mathf.Min(_Bounds.min.z, _Bounds.max.z);
			float num6 = Mathf.Max(_Bounds.min.z, _Bounds.max.z);
			Vector3 center = _Bounds.center;
			float x;
			float num7;
			int num8;
			if (_Point.x >= num2)
			{
				x = num2;
				num7 = _Point.x - num2;
				num8 = 1;
			}
			else if (_Point.x <= num)
			{
				x = num;
				num7 = num - _Point.x;
				num8 = -1;
			}
			else if (num2 - _Point.x <= _Point.x - num)
			{
				x = num2;
				num7 = num2 - _Point.x;
				num8 = 1;
			}
			else
			{
				x = num;
				num7 = _Point.x - num;
				num8 = -1;
			}
			float y;
			float num9;
			int num10;
			if (_Point.y >= num4)
			{
				y = num4;
				num9 = _Point.y - num4;
				num10 = 1;
			}
			else if (_Point.y <= num3)
			{
				y = num3;
				num9 = num3 - _Point.y;
				num10 = -1;
			}
			else if (num4 - _Point.y <= _Point.y - num3)
			{
				y = num4;
				num9 = num4 - _Point.y;
				num10 = 1;
			}
			else
			{
				y = num3;
				num9 = _Point.y - num3;
				num10 = -1;
			}
			float z;
			float num11;
			int num12;
			if (_Point.z >= num6)
			{
				z = num6;
				num11 = _Point.z - num6;
				num12 = 1;
			}
			else if (_Point.z <= num5)
			{
				z = num5;
				num11 = num5 - _Point.z;
				num12 = -1;
			}
			else if (num6 - _Point.z <= _Point.z - num5)
			{
				z = num6;
				num11 = num6 - _Point.z;
				num12 = 1;
			}
			else
			{
				z = num5;
				num11 = _Point.z - num5;
				num12 = -1;
			}
			if (num7 <= num9)
			{
				if (num7 <= num11)
				{
					_Normal = new Vector3(num8, 0f, 0f);
					return new Vector3(x, center.y, center.z);
				}
				_Normal = new Vector3(0f, 0f, num12);
				return new Vector3(center.x, center.y, z);
			}
			if (num9 <= num11)
			{
				_Normal = new Vector3(0f, num10, 0f);
				return new Vector3(center.x, y, center.z);
			}
			_Normal = new Vector3(0f, 0f, num12);
			return new Vector3(center.x, center.y, z);
		}

		public static bool IntersectSegment(this Bounds _Bounds, Segment3 _Segment3)
		{
			if (_Bounds.Contains(_Segment3.Start) || _Bounds.Contains(_Segment3.End))
			{
				return true;
			}
			float x = _Segment3.Start.x;
			float y = _Segment3.Start.y;
			float z = _Segment3.Start.z;
			float x2 = _Segment3.End.x;
			float y2 = _Segment3.End.y;
			float z2 = _Segment3.End.z;
			float num = Mathf.Min(_Bounds.min.x, _Bounds.max.x);
			float num2 = Mathf.Max(_Bounds.min.x, _Bounds.max.x);
			float num3 = Mathf.Min(_Bounds.min.y, _Bounds.max.y);
			float num4 = Mathf.Max(_Bounds.min.y, _Bounds.max.y);
			float num5 = Mathf.Min(_Bounds.min.z, _Bounds.max.z);
			float num6 = Mathf.Max(_Bounds.min.z, _Bounds.max.z);
			Nav3D.LocalAvoidance.SupportingMath.Plane plane = new Nav3D.LocalAvoidance.SupportingMath.Plane(Vector3.right, new Vector3(num, 0f, 0f));
			Nav3D.LocalAvoidance.SupportingMath.Plane plane2 = new Nav3D.LocalAvoidance.SupportingMath.Plane(Vector3.left, new Vector3(num2, 0f, 0f));
			Nav3D.LocalAvoidance.SupportingMath.Plane plane3 = new Nav3D.LocalAvoidance.SupportingMath.Plane(Vector3.up, new Vector3(0f, num3, 0f));
			Nav3D.LocalAvoidance.SupportingMath.Plane plane4 = new Nav3D.LocalAvoidance.SupportingMath.Plane(Vector3.down, new Vector3(0f, num4, 0f));
			Nav3D.LocalAvoidance.SupportingMath.Plane plane5 = new Nav3D.LocalAvoidance.SupportingMath.Plane(Vector3.forward, new Vector3(0f, 0f, num5));
			Nav3D.LocalAvoidance.SupportingMath.Plane plane6 = new Nav3D.LocalAvoidance.SupportingMath.Plane(Vector3.back, new Vector3(0f, 0f, num6));
			if (((x <= num && x2 >= num) || (x >= num && x2 <= num)) && _Segment3.IntersectionWithPlane(plane, out var _Point) && _Point.y <= num4 && _Point.y >= num3 && _Point.z <= num6 && _Point.z >= num5)
			{
				return true;
			}
			if (((x <= num2 && x2 >= num2) || (x >= num2 && x2 <= num2)) && _Segment3.IntersectionWithPlane(plane2, out var _Point2) && _Point2.y <= num4 && _Point2.y >= num3 && _Point2.z <= num6 && _Point2.z >= num5)
			{
				return true;
			}
			if (((y <= num3 && y2 >= num3) || (y >= num3 && y2 <= num3)) && _Segment3.IntersectionWithPlane(plane3, out var _Point3) && _Point3.x <= num2 && _Point3.x >= num && _Point3.z <= num6 && _Point3.z >= num5)
			{
				return true;
			}
			if (((y <= num4 && y2 >= num4) || (y >= num4 && y2 <= num4)) && _Segment3.IntersectionWithPlane(plane4, out var _Point4) && _Point4.x <= num2 && _Point4.x >= num && _Point4.z <= num6 && _Point4.z >= num5)
			{
				return true;
			}
			if (((z <= num5 && z2 >= num5) || (z >= num5 && z2 <= num5)) && _Segment3.IntersectionWithPlane(plane5, out var _Point5) && _Point5.x <= num2 && _Point5.x >= num && _Point5.y <= num4 && _Point5.y >= num3)
			{
				return true;
			}
			if (((z <= num6 && z2 >= num6) || (z >= num6 && z2 <= num6)) && _Segment3.IntersectionWithPlane(plane6, out var _Point6) && _Point6.x <= num2 && _Point6.x >= num && _Point6.y <= num4 && _Point6.y >= num3)
			{
				return true;
			}
			return false;
		}

		public static List<Vector3> GetIntersection(this Bounds _Bounds, Segment3 _Segment3)
		{
			List<Vector3> list = new List<Vector3>();
			float num = Mathf.Min(_Bounds.min.x, _Bounds.max.x);
			float num2 = Mathf.Max(_Bounds.min.x, _Bounds.max.x);
			float num3 = Mathf.Min(_Bounds.min.y, _Bounds.max.y);
			float num4 = Mathf.Max(_Bounds.min.y, _Bounds.max.y);
			float num5 = Mathf.Min(_Bounds.min.z, _Bounds.max.z);
			float num6 = Mathf.Max(_Bounds.min.z, _Bounds.max.z);
			Nav3D.LocalAvoidance.SupportingMath.Plane plane = new Nav3D.LocalAvoidance.SupportingMath.Plane(Vector3.right, new Vector3(num, 0f, 0f));
			Nav3D.LocalAvoidance.SupportingMath.Plane plane2 = new Nav3D.LocalAvoidance.SupportingMath.Plane(Vector3.left, new Vector3(num2, 0f, 0f));
			Nav3D.LocalAvoidance.SupportingMath.Plane plane3 = new Nav3D.LocalAvoidance.SupportingMath.Plane(Vector3.up, new Vector3(0f, num3, 0f));
			Nav3D.LocalAvoidance.SupportingMath.Plane plane4 = new Nav3D.LocalAvoidance.SupportingMath.Plane(Vector3.down, new Vector3(0f, num4, 0f));
			Nav3D.LocalAvoidance.SupportingMath.Plane plane5 = new Nav3D.LocalAvoidance.SupportingMath.Plane(Vector3.forward, new Vector3(0f, 0f, num5));
			Nav3D.LocalAvoidance.SupportingMath.Plane plane6 = new Nav3D.LocalAvoidance.SupportingMath.Plane(Vector3.back, new Vector3(0f, 0f, num6));
			Straight line = new Straight(_Segment3.DirectionMagn, _Segment3.Origin);
			if ((_Segment3.Start.x <= num && _Segment3.End.x >= num) || (_Segment3.Start.x >= num && _Segment3.End.x <= num))
			{
				Vector3 item = plane.Intersection(line).First();
				if (item.y <= num4 && item.y >= num3 && item.z <= num6 && item.z >= num5)
				{
					list.Add(item);
				}
			}
			if ((_Segment3.Start.x <= num2 && _Segment3.End.x >= num2) || (_Segment3.Start.x >= num2 && _Segment3.End.x <= num2))
			{
				Vector3 item2 = plane2.Intersection(line).First();
				if (item2.y <= num4 && item2.y >= num3 && item2.z <= num6 && item2.z >= num5)
				{
					list.Add(item2);
				}
			}
			if ((_Segment3.Start.y <= num3 && _Segment3.End.y >= num3) || (_Segment3.Start.y >= num3 && _Segment3.End.y <= num3))
			{
				Vector3 item3 = plane3.Intersection(line).First();
				if (item3.x <= num2 && item3.x >= num && item3.z <= num6 && item3.z >= num5)
				{
					list.Add(item3);
				}
			}
			if ((_Segment3.Start.y <= num4 && _Segment3.End.y >= num4) || (_Segment3.Start.y >= num4 && _Segment3.End.y <= num4))
			{
				Vector3 item4 = plane4.Intersection(line).First();
				if (item4.x <= num2 && item4.x >= num && item4.z <= num6 && item4.z >= num5)
				{
					list.Add(item4);
				}
			}
			if ((_Segment3.Start.z <= num5 && _Segment3.End.z >= num5) || (_Segment3.Start.z >= num5 && _Segment3.End.z <= num5))
			{
				Vector3 item5 = plane5.Intersection(line).First();
				if (item5.x <= num2 && item5.x >= num && item5.y <= num4 && item5.y >= num3)
				{
					list.Add(item5);
				}
			}
			if ((_Segment3.Start.z <= num6 && _Segment3.End.z >= num6) || (_Segment3.Start.z >= num6 && _Segment3.End.z <= num6))
			{
				Vector3 item6 = plane6.Intersection(line).First();
				if (item6.x <= num2 && item6.x >= num && item6.y <= num4 && item6.y >= num3)
				{
					list.Add(item6);
				}
			}
			return list;
		}
	}
}
