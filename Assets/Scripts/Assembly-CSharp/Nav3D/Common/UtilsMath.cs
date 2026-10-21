using System;
using System.Collections.Generic;
using System.Linq;
using Nav3D.LocalAvoidance.SupportingMath;
using UnityEngine;

namespace Nav3D.Common
{
	public static class UtilsMath
	{
		public const float PLANE_BOUNDARY_THRESHOLD = 1E-06f;

		private static System.Random m_Random = new System.Random();

		public static readonly Vector3Int[] NeighborBuckets = new Vector3Int[27]
		{
			new Vector3Int(-1, -1, -1),
			new Vector3Int(-1, -1, 0),
			new Vector3Int(-1, -1, 1),
			new Vector3Int(-1, 0, -1),
			new Vector3Int(-1, 0, 0),
			new Vector3Int(-1, 0, 1),
			new Vector3Int(-1, 1, -1),
			new Vector3Int(-1, 1, 0),
			new Vector3Int(-1, 1, 1),
			new Vector3Int(0, -1, -1),
			new Vector3Int(0, -1, 0),
			new Vector3Int(0, -1, 1),
			new Vector3Int(0, 0, -1),
			new Vector3Int(0, 0, 0),
			new Vector3Int(0, 0, 1),
			new Vector3Int(0, 1, -1),
			new Vector3Int(0, 1, 0),
			new Vector3Int(0, 1, 1),
			new Vector3Int(1, -1, -1),
			new Vector3Int(1, -1, 0),
			new Vector3Int(1, -1, 1),
			new Vector3Int(1, 0, -1),
			new Vector3Int(1, 0, 0),
			new Vector3Int(1, 0, 1),
			new Vector3Int(1, 1, -1),
			new Vector3Int(1, 1, 0),
			new Vector3Int(1, 1, 1)
		};

		public static readonly Vector3Int[] BucketFacesNormals = new Vector3Int[6]
		{
			Vector3Int.left,
			Vector3Int.right,
			Vector3Int.up,
			Vector3Int.down,
			Vector3Int.forward,
			Vector3Int.back
		};

		public static Vector3 RandomNormal => GetRandomVector().normalized;

		public static float Sqr(float _Value)
		{
			return _Value * _Value;
		}

		public static Vector3 WeightedVector3Sum2(Vector3 _Vector1, Vector3 _Vector2, float _Weight1, float _Weight2)
		{
			return Vector3.Slerp(_Vector1, _Vector2, _Weight2 / (_Weight1 + _Weight2));
		}

		public static Vector3 WeightedVector3Sum3(Vector3 _Vector1, Vector3 _Vector2, Vector3 _Vector3, float _Weight1, float _Weight2, float _Weight3, out Vector3 _V23)
		{
			float num = _Weight2 + _Weight3;
			Vector3 a = (_V23 = Vector3.Slerp(_Vector2, _Vector3, _Weight3 / num));
			float num2 = num / 2f;
			return Vector3.Slerp(a, _Vector1, _Weight1 / (_Weight1 + num2));
		}

		public static Vector3[] GetSphereSurfacePoints(Vector3 _Center, float _Radius, int _Count)
		{
			Vector3[] array = new Vector3[_Count];
			float num = (1f + Mathf.Sqrt(5f)) / 2f;
			float num2 = MathF.PI * 2f * num;
			for (int i = 0; i < _Count; i++)
			{
				float num3 = (float)i / (float)_Count;
				float f = Mathf.Acos(1f - 2f * num3);
				float f2 = num2 * (float)i;
				float x = Mathf.Sin(f) * Mathf.Cos(f2);
				float y = Mathf.Sin(f) * Mathf.Sin(f2);
				float z = Mathf.Cos(f);
				array[i] = new Vector3(x, y, z);
			}
			return array.Select((Vector3 _Direction) => _Center + _Direction * _Radius).ToArray();
		}

		public static Vector3[] GetSpaceCirclePoints(Vector3 _Center, float _Radius, Vector3 _Normal, int _Count)
		{
			Quaternion quaternion = Quaternion.FromToRotation(Vector3.up, _Normal.normalized);
			Vector3[] array = new Vector3[_Count];
			for (int i = 0; i < _Count; i++)
			{
				float f = (float)i / (float)(_Count - 1) * 2f * MathF.PI;
				Vector3 vector = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f)) * _Radius;
				vector = quaternion * vector;
				vector += _Center;
				array[i] = vector;
			}
			return array;
		}

		public static float TriangleHeronSquare(float _A, float _B, float _C)
		{
			float num = 0.5f * (_A + _B + _C);
			return Mathf.Sqrt(num * (num - _A) * (num - _B) * (num - _C));
		}

		public static Vector3 GetRandomOrthogonal(Vector3 _Vector)
		{
			UnityEngine.Random.InitState(100);
			return Vector3.Cross(_Vector, RandomNormal);
		}

		public static Vector3 GetRandomVector(float _Magnitude = 1f)
		{
			return new Vector3(SystemRandomRange(0f - _Magnitude, _Magnitude), SystemRandomRange(0f - _Magnitude, _Magnitude), SystemRandomRange(0f - _Magnitude, _Magnitude));
		}

		public static float GetRandomNormalValue(float _Min, float _Max)
		{
			float num = (_Max - _Min) * 0.5f;
			return Mathf.Clamp(_Min + num + (float)GenerateGaussianNoise().Z0 * num, _Min, _Max);
		}

		public static float GetRandomUniformValue(float _Min, float _Max)
		{
			return UnityEngine.Random.Range(_Min, _Max);
		}

		public static Segment3[] PointSequenceToSegments(Vector3[] _Sequence)
		{
			Segment3[] array = new Segment3[_Sequence.Length - 1];
			for (int i = 0; i < _Sequence.Length - 1; i++)
			{
				array[i] = new Segment3(_Sequence[i], _Sequence[i + 1]);
			}
			return array.ToArray();
		}

		public static Vector3 GetClosestArrayPoint(Vector3[] _Points, Vector3 _Point)
		{
			if (!_Points.Any())
			{
				return _Point;
			}
			return _Points.MinBy((Vector3 _CurrPoint) => (_CurrPoint - _Point).sqrMagnitude);
		}

		public static Vector3 GetClosestPointOnCurve(Vector3[] _Curve, Vector3 _Point)
		{
			if (!_Curve.Any())
			{
				return _Point;
			}
			List<Vector3> list = new List<Vector3>();
			for (int i = 1; i < _Curve.Length; i++)
			{
				list.Add(new Segment3(_Curve[i - 1], _Curve[i]).GetClosestPoint(_Point));
			}
			return list.MinBy((Vector3 _SegPoint) => (_SegPoint - _Point).sqrMagnitude);
		}

		private static float SystemRandomRange(float _Min, float _Max)
		{
			float num = _Max - _Min;
			float num2 = Mathf.Abs(_Min);
			return (float)m_Random.NextDouble() * num - num2;
		}

		private static (double Z0, double Z1) GenerateGaussianNoise()
		{
			double num;
			double num2;
			double num3;
			do
			{
				num = UnityEngine.Random.Range(-1f, 1f);
				num2 = UnityEngine.Random.Range(-1f, 1f);
				num3 = num * num + num2 * num2;
			}
			while (num3 > 1.0 || num3 == 0.0);
			double num4 = Math.Sqrt(-2.0 * Math.Log(num3) / num3);
			double item = num * num4;
			double item2 = num2 * num4;
			return (Z0: item, Z1: item2);
		}

		public static bool VelocityTrailIntersects(Vector3 _Position1, Vector3 _FrameVelocity1, float _Radius1, Vector3 _Position2, Vector3 _FrameVelocity2, float _Radius2)
		{
			Vector3 normalized = Vector3.Cross(_FrameVelocity1, _FrameVelocity2).normalized;
			float num = _Radius1 + _Radius2;
			float num2 = Sqr(num);
			if (normalized == Vector3.zero)
			{
				Vector3 vector = _Position1 + _FrameVelocity1;
				Vector3 rhs = _Position2 - _Position1;
				if (new Nav3D.LocalAvoidance.SupportingMath.Plane(Vector3.Cross(Vector3.Cross(_FrameVelocity1, rhs), _FrameVelocity1), _Position1).DistanceToPoint(_Position2) >= num)
				{
					return false;
				}
				Nav3D.LocalAvoidance.SupportingMath.Plane plane = new Nav3D.LocalAvoidance.SupportingMath.Plane(_FrameVelocity1, _Position1);
				Vector3 vector2 = _Position2 + _FrameVelocity2;
				bool side = plane.GetSide(_Position2);
				bool side2 = plane.GetSide(vector2);
				if (side != side2)
				{
					return true;
				}
				Nav3D.LocalAvoidance.SupportingMath.Plane plane2 = new Nav3D.LocalAvoidance.SupportingMath.Plane(-_FrameVelocity1, vector);
				bool side3 = plane2.GetSide(_Position2);
				bool side4 = plane2.GetSide(vector2);
				if (side3 != side4)
				{
					return true;
				}
				if (side & side3)
				{
					return true;
				}
				if (side)
				{
					float sqrMagnitude = (vector - _Position2).sqrMagnitude;
					float sqrMagnitude2 = (vector - vector2).sqrMagnitude;
					if (sqrMagnitude < sqrMagnitude2)
					{
						return sqrMagnitude < num2;
					}
					return sqrMagnitude2 < num2;
				}
				float sqrMagnitude3 = (_Position1 - _Position2).sqrMagnitude;
				float sqrMagnitude4 = (_Position1 - vector2).sqrMagnitude;
				if (sqrMagnitude3 < sqrMagnitude4)
				{
					return sqrMagnitude3 < num2;
				}
				return sqrMagnitude4 < num2;
			}
			Vector3 a = new Nav3D.LocalAvoidance.SupportingMath.Plane(Vector3.Cross(_FrameVelocity1, normalized), _Position1).Intersection(new Straight(_FrameVelocity2, _Position2)).First();
			Vector3 vector3 = new Nav3D.LocalAvoidance.SupportingMath.Plane(Vector3.Cross(_FrameVelocity2, normalized), _Position2).Intersection(new Straight(_FrameVelocity1, _Position1)).First();
			if (Vector3.Distance(a, vector3) >= _Radius1 + _Radius2)
			{
				return false;
			}
			Vector3 vector4 = _Position1 + _FrameVelocity1;
			Vector3 vector5 = _Position2 + _FrameVelocity2;
			Nav3D.LocalAvoidance.SupportingMath.Plane plane3 = new Nav3D.LocalAvoidance.SupportingMath.Plane(_FrameVelocity1, _Position1);
			Nav3D.LocalAvoidance.SupportingMath.Plane plane4 = new Nav3D.LocalAvoidance.SupportingMath.Plane(_FrameVelocity2, _Position2);
			Nav3D.LocalAvoidance.SupportingMath.Plane plane5 = new Nav3D.LocalAvoidance.SupportingMath.Plane(-_FrameVelocity2, vector5);
			if (!plane3.GetSide(vector3))
			{
				if (!plane4.GetSide(vector3))
				{
					return (_Position1 - _Position2).sqrMagnitude < num2;
				}
				if (!plane5.GetSide(vector3))
				{
					return (_Position1 - vector5).sqrMagnitude < num2;
				}
				return (_Position1 - new Straight(_FrameVelocity2, _Position2).ClosestPoint(_Position1)).sqrMagnitude < num2;
			}
			if (!new Nav3D.LocalAvoidance.SupportingMath.Plane(-_FrameVelocity1, vector4).GetSide(vector3))
			{
				if (!plane4.GetSide(vector3))
				{
					return (vector4 - _Position2).sqrMagnitude < num2;
				}
				if (!plane5.GetSide(vector3))
				{
					return (vector4 - vector5).sqrMagnitude < num2;
				}
				return (vector4 - new Straight(_FrameVelocity2, _Position2).ClosestPoint(vector4)).sqrMagnitude < num2;
			}
			if (!plane4.GetSide(vector3))
			{
				return (_Position2 - new Straight(_FrameVelocity1, _Position1).ClosestPoint(_Position2)).sqrMagnitude < num2;
			}
			if (!plane5.GetSide(vector3))
			{
				return (vector5 - new Straight(_FrameVelocity1, _Position1).ClosestPoint(vector5)).sqrMagnitude < num2;
			}
			return true;
		}
	}
}
