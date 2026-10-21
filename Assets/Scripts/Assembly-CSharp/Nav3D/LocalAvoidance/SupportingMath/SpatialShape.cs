using System;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.LocalAvoidance.SupportingMath
{
	public abstract class SpatialShape
	{
		public virtual Vector3 GetClosestPoint(Vector3 _Point)
		{
			throw new NotImplementedException("[SpatialShape] GetClosestPoint() is not implemented");
		}

		public virtual bool IsIntersect(SpatialShape _Other)
		{
			throw new NotImplementedException("[SpatialShape] IsIntersect() is not implemented");
		}

		public virtual IntersectionType CheckIntersection(ILine _Line)
		{
			throw new NotImplementedException("[SpatialShape] CheckIntersection() is not implemented");
		}
	}
}
