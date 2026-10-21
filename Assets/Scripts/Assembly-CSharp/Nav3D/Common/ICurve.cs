using UnityEngine;

namespace Nav3D.Common
{
	internal interface ICurve
	{
		Segment3[] Segments { get; }

		bool Intersects(Bounds _Bounds);
	}
}
