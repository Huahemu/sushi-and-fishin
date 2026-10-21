using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nav3D.Common
{
	public class IntersectablesSpatialHashMap<T> where T : IBoundable, IBoundsIntersectable
	{
		private int m_ElementsCount;

		private int m_BucketsCount;

		private int m_BucketsPerElement;

		private int m_ElementsPerBucket;

		private float m_BucketSize;

		private ConcurrentDictionary<T, HashSet<Vector3Int>> m_Elements;

		private ConcurrentDictionary<Vector3Int, HashSet<T>> m_Buckets;

		public int Count => m_Elements.Count;

		public List<T> Elements => m_Elements.Select((KeyValuePair<T, HashSet<Vector3Int>> _Element) => _Element.Key).ToList();

		public IntersectablesSpatialHashMap(float _BucketSize, int _ElementsCount = 31, int _BucketsCount = 31, int _BucketsPerElement = 4, int _ElementsPerBucket = 10)
		{
			m_BucketSize = _BucketSize;
			m_ElementsCount = _ElementsCount;
			m_BucketsCount = _BucketsCount;
			m_BucketsPerElement = _BucketsPerElement;
			m_ElementsPerBucket = _ElementsPerBucket;
			m_Elements = new ConcurrentDictionary<T, HashSet<Vector3Int>>(Environment.ProcessorCount - 1, m_ElementsCount);
			m_Buckets = new ConcurrentDictionary<Vector3Int, HashSet<T>>(Environment.ProcessorCount - 1, m_BucketsCount);
		}

		public List<T> GetIntersectedElements(Bounds _Bounds)
		{
			List<T> list = new List<T>();
			Vector3[] cornerPoints = _Bounds.GetCornerPoints();
			Vector3Int bucketFromPoint = GetBucketFromPoint(cornerPoints[0]);
			Vector3Int bucketFromPoint2 = GetBucketFromPoint(cornerPoints[7]);
			for (int i = bucketFromPoint.x; i <= bucketFromPoint2.x; i++)
			{
				for (int j = bucketFromPoint.y; j <= bucketFromPoint2.y; j++)
				{
					for (int k = bucketFromPoint.z; k <= bucketFromPoint2.z; k++)
					{
						Vector3Int key = new Vector3Int(i, j, k);
						if (!m_Buckets.TryGetValue(key, out var value))
						{
							continue;
						}
						foreach (T item in value)
						{
							if (item.Intersects(_Bounds))
							{
								list.Add(item);
							}
						}
					}
				}
			}
			return list;
		}

		public bool IsIntersect(Bounds _Bounds)
		{
			Vector3[] cornerPoints = _Bounds.GetCornerPoints();
			Vector3Int bucketFromPoint = GetBucketFromPoint(cornerPoints[0]);
			Vector3Int bucketFromPoint2 = GetBucketFromPoint(cornerPoints[7]);
			for (int i = bucketFromPoint.x; i <= bucketFromPoint2.x; i++)
			{
				for (int j = bucketFromPoint.y; j <= bucketFromPoint2.y; j++)
				{
					for (int k = bucketFromPoint.z; k <= bucketFromPoint2.z; k++)
					{
						Vector3Int key = new Vector3Int(i, j, k);
						if (!m_Buckets.TryGetValue(key, out var value))
						{
							continue;
						}
						foreach (T item in value)
						{
							if (item.Intersects(_Bounds))
							{
								return true;
							}
						}
					}
				}
			}
			return false;
		}

		public void Register(T _Element)
		{
			Vector3[] cornerPoints = _Element.Bounds.GetCornerPoints();
			Vector3 point = cornerPoints[0];
			Vector3 point2 = cornerPoints[7];
			Vector3Int bucketFromPoint = GetBucketFromPoint(point);
			Vector3Int bucketFromPoint2 = GetBucketFromPoint(point2);
			for (int i = bucketFromPoint.x; i <= bucketFromPoint2.x; i++)
			{
				for (int j = bucketFromPoint.y; j <= bucketFromPoint2.y; j++)
				{
					for (int k = bucketFromPoint.z; k <= bucketFromPoint2.z; k++)
					{
						Vector3Int bucket = new Vector3Int(i, j, k);
						Bounds bucketBounds = GetBucketBounds(bucket);
						if (_Element.Intersects(bucketBounds))
						{
							AddToBucket(_Element, bucket);
						}
					}
				}
			}
		}

		public void Unregister(T _Element)
		{
			if (!m_Elements.TryGetValue(_Element, out var value))
			{
				return;
			}
			foreach (Vector3Int item in value)
			{
				if (m_Buckets.TryGetValue(item, out var value2))
				{
					value2.Remove(_Element);
					if (!value2.Any())
					{
						m_Buckets.TryRemove(item, out var _);
					}
				}
			}
			m_Elements.TryRemove(_Element, out var _);
		}

		private void AddToBucket(T _Element, Vector3Int _Bucket)
		{
			HashSet<T> orAdd = m_Buckets.GetOrAdd(_Bucket, new HashSet<T>());
			lock (orAdd)
			{
				orAdd.Add(_Element);
			}
			HashSet<Vector3Int> orAdd2 = m_Elements.GetOrAdd(_Element, new HashSet<Vector3Int>());
			lock (orAdd2)
			{
				orAdd2.Add(_Bucket);
			}
		}

		private Bounds GetBucketBounds(Vector3Int _Bucket)
		{
			Vector3 center = new Vector3((float)_Bucket.x * m_BucketSize, (float)_Bucket.y * m_BucketSize, (float)_Bucket.z * m_BucketSize);
			Vector3 size = new Vector3(m_BucketSize, m_BucketSize, m_BucketSize);
			return new Bounds(center, size);
		}

		private Vector3Int GetBucketFromPoint(Vector3 _Point)
		{
			int x = (int)((Mathf.Abs(_Point.x) / m_BucketSize + 0.5f) * Mathf.Sign(_Point.x));
			int y = (int)((Mathf.Abs(_Point.y) / m_BucketSize + 0.5f) * Mathf.Sign(_Point.y));
			int z = (int)((Mathf.Abs(_Point.z) / m_BucketSize + 0.5f) * Mathf.Sign(_Point.z));
			return new Vector3Int(x, y, z);
		}
	}
}
