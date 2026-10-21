using UnityEngine;

namespace Nav3D.LocalAvoidance.SupportingMath
{
	public interface ILine
	{
		Vector3[] Intersection(SpatialShape _Figure);

		Vector3 ClosestPoint(Vector3 _Point);
	}
}
