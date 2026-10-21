using Nav3D.LocalAvoidance.SupportingMath;
using UnityEngine;

namespace Nav3D.Common
{
	public struct Segment3
	{
		public Vector3 Start { get; }

		public Vector3 End { get; }

		public Vector3 Origin { get; }

		public Vector3 DirectionNormal { get; }

		public Vector3 DirectionMagn { get; }

		public Segment3(Vector3 _Start, Vector3 _End)
		{
			Start = _Start;
			End = _End;
			Origin = _Start;
			DirectionMagn = _End - _Start;
			DirectionNormal = DirectionMagn.normalized;
		}

		public bool IntersectionWithPlane(Nav3D.LocalAvoidance.SupportingMath.Plane _Plane, out Vector3 _Point)
		{
			float num = Vector3.Dot(DirectionMagn, _Plane.Normal);
			if (Mathf.Abs(num) < float.Epsilon)
			{
				_Point = Vector3.zero;
				return false;
			}
			float num2 = (0f - Vector3.Dot(Origin, _Plane.Normal) - _Plane.Distance) / num;
			if (num2 < 0f || num2 > 1f)
			{
				_Point = Vector3.zero;
				return false;
			}
			_Point = (Origin + DirectionMagn * num2).RoundWith5Precision();
			return true;
		}

		public Vector3 GetClosestPoint(Vector3 _Point)
		{
			Vector3 vector = End - Start;
			Vector3 lhs = _Point - Start;
			float sqrMagnitude = vector.sqrMagnitude;
			if (sqrMagnitude == 0f)
			{
				return Start;
			}
			float value = Vector3.Dot(lhs, vector) / sqrMagnitude;
			value = Mathf.Clamp01(value);
			return Start + value * vector;
		}
	}
}
