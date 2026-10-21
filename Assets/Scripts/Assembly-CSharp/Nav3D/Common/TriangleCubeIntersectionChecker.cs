using System;
using UnityEngine;

namespace Nav3D.Common
{
	public static class TriangleCubeIntersectionChecker
	{
		private const long INSIDE = 0L;

		private const long OUTSIDE = 1L;

		public static bool Inside(Triangle _Triangle, Bounds _Cube)
		{
			Vector3 center = _Cube.center;
			Vector3 size = _Cube.size;
			DVector3 dVector = new DVector3(((double)_Triangle.V1.x - (double)center.x) / (double)size.x, ((double)_Triangle.V1.y - (double)center.y) / (double)size.y, ((double)_Triangle.V1.z - (double)center.z) / (double)size.z);
			DVector3 dVector2 = new DVector3(((double)_Triangle.V2.x - (double)center.x) / (double)size.x, ((double)_Triangle.V2.y - (double)center.y) / (double)size.y, ((double)_Triangle.V2.z - (double)center.z) / (double)size.z);
			DVector3 dVector3 = new DVector3(((double)_Triangle.V3.x - (double)center.x) / (double)size.x, ((double)_Triangle.V3.y - (double)center.y) / (double)size.y, ((double)_Triangle.V3.z - (double)center.z) / (double)size.z);
			dVector = new DVector3(Math.Round(dVector.x, 5), Math.Round(dVector.y, 5), Math.Round(dVector.z, 5));
			dVector2 = new DVector3(Math.Round(dVector2.x, 5), Math.Round(dVector2.y, 5), Math.Round(dVector2.z, 5));
			dVector3 = new DVector3(Math.Round(dVector3.x, 5), Math.Round(dVector3.y, 5), Math.Round(dVector3.z, 5));
			DVector3 lhs = dVector2 - dVector;
			DVector3 rhs = dVector3 - dVector2;
			return InsideInternal(dVector, dVector2, dVector3, DVector3.Cross(lhs, rhs).normalized);
		}

		private static bool InsideInternal(DVector3 _V1, DVector3 _V2, DVector3 _V3, DVector3 _Normal)
		{
			long num;
			if ((num = FacePlane(_V1)) == 0L)
			{
				return true;
			}
			long num2;
			if ((num2 = FacePlane(_V2)) == 0L)
			{
				return true;
			}
			long num3;
			if ((num3 = FacePlane(_V3)) == 0L)
			{
				return true;
			}
			if ((num & num2 & num3) != 0L)
			{
				return false;
			}
			num |= Bevel2D(_V1) << 8;
			num2 |= Bevel2D(_V2) << 8;
			num3 |= Bevel2D(_V3) << 8;
			if ((num & num2 & num3) != 0L)
			{
				return false;
			}
			num |= Bevel3D(_V1) << 24;
			num2 |= Bevel3D(_V2) << 24;
			num3 |= Bevel3D(_V3) << 24;
			if ((num & num2 & num3) != 0L)
			{
				return false;
			}
			if ((num & num2) == 0L && CheckLine(_V1, _V2, num | num2) == 0L)
			{
				return true;
			}
			if ((num & num3) == 0L && CheckLine(_V1, _V3, num | num3) == 0L)
			{
				return true;
			}
			if ((num2 & num3) == 0L && CheckLine(_V2, _V3, num2 | num3) == 0L)
			{
				return true;
			}
			double num4 = _Normal.x * _V1.x + _Normal.y * _V1.y + _Normal.z * _V1.z;
			double num5;
			if (Math.Abs(num5 = _Normal.x + _Normal.y + _Normal.z) > (double)Mathf.Epsilon)
			{
				DVector3 point = default;
				point.x = (point.y = (point.z = num4 / num5));
				if (Math.Abs(point.x) <= 0.5 && PointTriangleIntersection(point, _V1, _V2, _V3) == 0L)
				{
					return true;
				}
			}
			if (Math.Abs(num5 = _Normal.x + _Normal.y - _Normal.z) > (double)Mathf.Epsilon)
			{
				DVector3 point2 = default;
				point2.z = 0.0 - (point2.x = (point2.y = num4 / num5));
				if (Math.Abs(point2.x) <= 0.5 && PointTriangleIntersection(point2, _V1, _V2, _V3) == 0L)
				{
					return true;
				}
			}
			if (Math.Abs(num5 = _Normal.x - _Normal.y + _Normal.z) > (double)Mathf.Epsilon)
			{
				DVector3 point3 = default;
				point3.y = 0.0 - (point3.x = (point3.z = num4 / num5));
				if (Math.Abs(point3.x) <= 0.5 && PointTriangleIntersection(point3, _V1, _V2, _V3) == 0L)
				{
					return true;
				}
			}
			if (Math.Abs(num5 = _Normal.x - _Normal.y - _Normal.z) > (double)Mathf.Epsilon)
			{
				DVector3 point4 = default;
				point4.y = (point4.z = 0.0 - (point4.x = num4 / num5));
				if (Math.Abs(point4.x) <= 0.5 && PointTriangleIntersection(point4, _V1, _V2, _V3) == 0L)
				{
					return true;
				}
			}
			return false;
		}

		private static long FacePlane(DVector3 _Point)
		{
			long num = 0L;
			if (_Point.x > 0.5)
			{
				num |= 1;
			}
			if (_Point.x < -0.5)
			{
				num |= 2;
			}
			if (_Point.y > 0.5)
			{
				num |= 4;
			}
			if (_Point.y < -0.5)
			{
				num |= 8;
			}
			if (_Point.z > 0.5)
			{
				num |= 0x10;
			}
			if (_Point.z < -0.5)
			{
				num |= 0x20;
			}
			return num;
		}

		private static long Bevel2D(DVector3 _Point)
		{
			long num = 0L;
			if (_Point.x + _Point.y > 1.0)
			{
				num |= 1;
			}
			if (_Point.x - _Point.y > 1.0)
			{
				num |= 2;
			}
			if (0.0 - _Point.x + _Point.y > 1.0)
			{
				num |= 4;
			}
			if (0.0 - _Point.x - _Point.y > 1.0)
			{
				num |= 8;
			}
			if (_Point.x + _Point.z > 1.0)
			{
				num |= 0x10;
			}
			if (_Point.x - _Point.z > 1.0)
			{
				num |= 0x20;
			}
			if (0.0 - _Point.x + _Point.z > 1.0)
			{
				num |= 0x40;
			}
			if (0.0 - _Point.x - _Point.z > 1.0)
			{
				num |= 0x80;
			}
			if (_Point.y + _Point.z > 1.0)
			{
				num |= 0x100;
			}
			if (_Point.y - _Point.z > 1.0)
			{
				num |= 0x200;
			}
			if (0.0 - _Point.y + _Point.z > 1.0)
			{
				num |= 0x400;
			}
			if (0.0 - _Point.y - _Point.z > 1.0)
			{
				num |= 0x800;
			}
			return num;
		}

		private static long Bevel3D(DVector3 _Point)
		{
			long num = 0L;
			if (_Point.x + _Point.y + _Point.z > 1.5)
			{
				num |= 1;
			}
			if (_Point.x + _Point.y - _Point.z > 1.5)
			{
				num |= 2;
			}
			if (_Point.x - _Point.y + _Point.z > 1.5)
			{
				num |= 4;
			}
			if (_Point.x - _Point.y - _Point.z > 1.5)
			{
				num |= 8;
			}
			if (0.0 - _Point.x + _Point.y + _Point.z > 1.5)
			{
				num |= 0x10;
			}
			if (0.0 - _Point.x + _Point.y - _Point.z > 1.5)
			{
				num |= 0x20;
			}
			if (0.0 - _Point.x - _Point.y + _Point.z > 1.5)
			{
				num |= 0x40;
			}
			if (0.0 - _Point.x - _Point.y - _Point.z > 1.5)
			{
				num |= 0x80;
			}
			return num;
		}

		private static long CheckLine(DVector3 _Point1, DVector3 _Point2, long _OutcodeDiff)
		{
			if ((1 & _OutcodeDiff) != 0L && CheckPoint(_Point1, _Point2, (0.5 - _Point1.x) / (_Point2.x - _Point1.x), 62L) == 0L)
			{
				return 0L;
			}
			if ((2 & _OutcodeDiff) != 0L && CheckPoint(_Point1, _Point2, (-0.5 - _Point1.x) / (_Point2.x - _Point1.x), 61L) == 0L)
			{
				return 0L;
			}
			if ((4 & _OutcodeDiff) != 0L && CheckPoint(_Point1, _Point2, (0.5 - _Point1.y) / (_Point2.y - _Point1.y), 59L) == 0L)
			{
				return 0L;
			}
			if ((8 & _OutcodeDiff) != 0L && CheckPoint(_Point1, _Point2, (-0.5 - _Point1.y) / (_Point2.y - _Point1.y), 55L) == 0L)
			{
				return 0L;
			}
			if ((0x10 & _OutcodeDiff) != 0L && CheckPoint(_Point1, _Point2, (0.5 - _Point1.z) / (_Point2.z - _Point1.z), 47L) == 0L)
			{
				return 0L;
			}
			if ((0x20 & _OutcodeDiff) != 0L && CheckPoint(_Point1, _Point2, (-0.5 - _Point1.z) / (_Point2.z - _Point1.z), 31L) == 0L)
			{
				return 0L;
			}
			return 1L;
		}

		private static long CheckPoint(DVector3 _Point1, DVector3 _Point2, double _Alpha, long _Mask)
		{
			DVector3 point = default;
			point.x = LERP(_Alpha, _Point1.x, _Point2.x);
			point.y = LERP(_Alpha, _Point1.y, _Point2.y);
			point.z = LERP(_Alpha, _Point1.z, _Point2.z);
			return FacePlane(point) & _Mask;
		}

		private static double Max(double _A, double _B, double _C)
		{
			if (!(_A > _B))
			{
				if (!(_B > _C))
				{
					return _C;
				}
				return _B;
			}
			if (!(_A > _C))
			{
				return _C;
			}
			return _A;
		}

		private static double Min(double _A, double _B, double _C)
		{
			if (!(_A < _B))
			{
				if (!(_B < _C))
				{
					return _C;
				}
				return _B;
			}
			if (!(_A < _C))
			{
				return _C;
			}
			return _A;
		}

		private static long PointTriangleIntersection(DVector3 _Point, DVector3 _V1, DVector3 _V2, DVector3 _V3)
		{
			if (_Point.x > Max(_V1.x, _V2.x, _V3.x))
			{
				return 1L;
			}
			if (_Point.y > Max(_V1.y, _V2.y, _V3.y))
			{
				return 1L;
			}
			if (_Point.z > Max(_V1.z, _V2.z, _V3.z))
			{
				return 1L;
			}
			if (_Point.x < Min(_V1.x, _V2.x, _V3.x))
			{
				return 1L;
			}
			if (_Point.y < Min(_V1.y, _V2.y, _V3.y))
			{
				return 1L;
			}
			if (_Point.z < Min(_V1.z, _V2.z, _V3.z))
			{
				return 1L;
			}
			DVector3 rhs = _Point - _V1;
			long num = SIGN3(DVector3.Cross(_V2 - _V1, rhs));
			long num2 = SIGN3(DVector3.Cross(_Rhs: _Point - _V2, _Lhs: _V3 - _V2));
			long num3 = SIGN3(DVector3.Cross(_Rhs: _Point - _V3, _Lhs: _V1 - _V3));
			if ((num & num2 & num3) != 0L)
			{
				return 0L;
			}
			return 1L;
		}

		private static double LERP(double A, double B, double C)
		{
			return B + A * (C - B);
		}

		private static long SIGN3(DVector3 A)
		{
			return ((A.x < (double)Mathf.Epsilon) ? 4 : 0) | ((A.x > (double)(0f - Mathf.Epsilon)) ? 32 : 0) | ((A.y < (double)Mathf.Epsilon) ? 2 : 0) | ((A.y > (double)(0f - Mathf.Epsilon)) ? 16 : 0) | ((A.z < (double)Mathf.Epsilon) ? 1 : 0) | ((A.z > (double)(0f - Mathf.Epsilon)) ? 8 : 0);
		}
	}
}
