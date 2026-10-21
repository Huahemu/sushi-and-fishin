using System.Collections.Generic;
using System.Linq;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.Obstacles
{
	public class TriangleStorage
	{
		private IntersectablesSpatialHashMap<Triangle> m_SpatialStorage;

		public static TriangleStorage EMPTY => new TriangleStorage
		{
			m_SpatialStorage = new IntersectablesSpatialHashMap<Triangle>(1f)
		};

		private TriangleStorage()
		{
		}

		public TriangleStorage(List<Triangle> _Triangles, GraphConstructionProgress _Progress = null, float? _BucketSize = null)
		{
			float num = _Triangles.Max((Triangle _Triangle) => _Triangle.Bounds.GetMaxSize());
			float num2 = _Triangles.Average((Triangle _Triangle) => _Triangle.Bounds.GetMinSize());
			float num3 = _BucketSize ?? num;
			int elementsPerBucket = Mathf.CeilToInt(num3 / num2);
			m_SpatialStorage = new IntersectablesSpatialHashMap<Triangle>(num3, _Triangles.Count, _Triangles.Count, 8, elementsPerBucket);
			int num4 = 0;
			int num5 = ((_Triangles.Count < 100) ? 1 : ((_Triangles.Count < 1000) ? 10 : ((_Triangles.Count < 10000) ? 100 : 1000)));
			for (int num6 = 0; num6 <= _Triangles.Count - 1; num6++)
			{
				m_SpatialStorage.Register(_Triangles[num6]);
				num4++;
				if (num4 == num5)
				{
					if (_Progress != null && _Progress.CancellationToken.IsCancellationRequested)
					{
						break;
					}
					_Progress?.SetTrianglesStorageProgress(m_SpatialStorage.Count, _Triangles.Count);
					num4 = 0;
				}
			}
		}

		public bool IsIntersect(Bounds _Bounds)
		{
			return m_SpatialStorage.IsIntersect(_Bounds);
		}

		public List<Triangle> GetIntersectedTriangles(Bounds _Bounds)
		{
			return m_SpatialStorage.GetIntersectedElements(_Bounds);
		}
	}
}
