using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nav3D.Common
{
	internal class CurvesSpatialHashMap<T> where T : ICurve
	{
		private const string INVALID_BUCKET_SIZE = "Invalid bucket size: {0}. The bucket size must be greater than zero.";

		private float m_BucketSize;

		private ConcurrentDictionary<T, HashSet<Vector3Int>> m_Curves = new ConcurrentDictionary<T, HashSet<Vector3Int>>();

		private ConcurrentDictionary<Vector3Int, ConcurrentHashSet<T>> m_Buckets = new ConcurrentDictionary<Vector3Int, ConcurrentHashSet<T>>();

		public int Count => m_Curves.Count;

		public CurvesSpatialHashMap(float _BucketSize)
		{
			m_BucketSize = _BucketSize;
		}

		public void SetBucketSize(float _BucketSize)
		{
			if (_BucketSize <= 0f)
			{
				throw new ArgumentException($"Invalid bucket size: {_BucketSize}. The bucket size must be greater than zero.");
			}
			List<T> list = new List<T>(m_Curves.Keys);
			m_Curves.Clear();
			m_Buckets.Clear();
			m_BucketSize = _BucketSize;
			foreach (T item in list)
			{
				Register(item);
			}
		}

		public void Register(T _Curve)
		{
			HashSet<Vector3Int> hashSet = new HashSet<Vector3Int>();
			int num = 0;
			_ = string.Empty;
			Segment3[] segments = _Curve.Segments;
			for (int i = 0; i < segments.Length; i++)
			{
				Segment3 line = segments[i];
				List<Vector3Int> voxels = LineVoxelizer.GetVoxels(line, m_BucketSize);
				if (voxels.Count > num)
				{
					num = voxels.Count;
					_ = $"{line.Start.ToStringExt()}, {line.End.ToStringExt()}, {voxels.Count}";
				}
				hashSet.AddRange(voxels);
			}
			m_Curves.TryAdd(_Curve, hashSet);
			hashSet.ForEach((Vector3Int _Bucket) =>
			{
				m_Buckets.GetOrAdd(_Bucket, new ConcurrentHashSet<T>()).TryAdd(_Curve);
			});
		}

		public void Update(T _Curve)
		{
			Unregister(_Curve);
			Register(_Curve);
		}

		public void Unregister(T _Curve)
		{
			if (!m_Curves.TryGetValue(_Curve, out var value))
			{
				return;
			}
			foreach (Vector3Int item in value)
			{
				if (m_Buckets.TryGetValue(item, out var value2))
				{
					value2.TryRemove(_Curve);
					if (!value2.Any())
					{
						m_Buckets.TryRemove(item, out var _);
					}
				}
			}
			m_Curves.TryRemove(_Curve, out var _);
		}

		public bool TryGetIntersectingCurves(Bounds _Bounds, out HashSet<T> _IntersectingCurves)
		{
			List<Vector3Int> source = (from _Corner in _Bounds.GetCornerPoints()
				select UtilsSpatialGrid.GetBucketIndex(_Corner, m_BucketSize)).ToList();
			HashSet<T> hashSet = new HashSet<T>();
			int num = source.Min((Vector3Int _Bucket) => _Bucket.x);
			int num2 = source.Min((Vector3Int _Bucket) => _Bucket.y);
			int num3 = source.Min((Vector3Int _Bucket) => _Bucket.z);
			int num4 = source.Max((Vector3Int _Bucket) => _Bucket.x);
			int num5 = source.Max((Vector3Int _Bucket) => _Bucket.y);
			int num6 = source.Max((Vector3Int _Bucket) => _Bucket.z);
			for (int num7 = num; num7 <= num4; num7++)
			{
				for (int num8 = num2; num8 <= num5; num8++)
				{
					for (int num9 = num3; num9 <= num6; num9++)
					{
						if (!m_Buckets.TryGetValue(new Vector3Int(num7, num8, num9), out var value))
						{
							continue;
						}
						foreach (T item in value.Where((T _Curve) => _Curve.Intersects(_Bounds)))
						{
							hashSet.Add(item);
						}
					}
				}
			}
			_IntersectingCurves = hashSet;
			return _IntersectingCurves.Any();
		}

		public void Draw()
		{
			foreach (Vector3Int key in m_Buckets.Keys)
			{
				UtilsSpatialGrid.GetBucketBounds(key, m_BucketSize).Draw();
			}
			Gizmos.color = Color.red;
			foreach (KeyValuePair<T, HashSet<Vector3Int>> curf in m_Curves)
			{
				Segment3[] segments = curf.Key.Segments;
				for (int i = 0; i < segments.Length; i++)
				{
					Segment3 segment = segments[i];
					Gizmos.DrawLine(segment.Start, segment.End);
				}
			}
		}
	}
}
