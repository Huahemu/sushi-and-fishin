using UnityEngine;

namespace Nav3D.Common
{
	public interface IBoundsIntersectable
	{
		bool Intersects(Bounds _Boudns);
	}
}
