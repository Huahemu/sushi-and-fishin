using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nav3D.Common
{
	internal class BoundablesSpatialHashMap<T> where T : IBoundable
	{
		private float m_BucketSize;

		private ConcurrentDictionary<T, List<Vector3Int>> m_Boundables = new ConcurrentDictionary<T, List<Vector3Int>>();

		private readonly ConcurrentDictionary<Vector3Int, ConcurrentHashSet<T>> m_Buckets = new ConcurrentDictionary<Vector3Int, ConcurrentHashSet<T>>();

		public bool TryGetCrossingBoundables(Bounds _Bounds, out HashSet<T> _CrossingBoundables)
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
						foreach (T item in value.Where((T _Boundable) => _Boundable.Bounds.Intersects(_Bounds)))
						{
							hashSet.Add(item);
						}
					}
				}
			}
			_CrossingBoundables = hashSet;
			return _CrossingBoundables.Any();
		}

		public bool TryGetCrossingBoundables(Vector3 _Point, out HashSet<T> _CrossingBoundables)
		{
			Vector3Int bucketIndex = UtilsSpatialGrid.GetBucketIndex(_Point, m_BucketSize);
			HashSet<T> hashSet = new HashSet<T>();
			if (m_Buckets.TryGetValue(bucketIndex, out var value))
			{
				foreach (T item in value.Where((T _Boundable) => _Boundable.Bounds.Contains(_Point)))
				{
					hashSet.Add(item);
				}
			}
			_CrossingBoundables = hashSet;
			return _CrossingBoundables.Any();
		}

		public bool HasCrossingBoundables(Bounds _Bounds)
		{
			_ = _Bounds;
			List<Vector3Int> source = (from _Corner in _Bounds.GetCornerPoints()
				select UtilsSpatialGrid.GetBucketIndex(_Corner, m_BucketSize)).ToList();
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
						new Vector3Int(num7, num8, num9);
						if (m_Buckets.TryGetValue(new Vector3Int(num7, num8, num9), out var value) && value.Any((T _Boundable) => _Boundable.Bounds.Intersects(_Bounds)))
						{
							return true;
						}
					}
				}
			}
			return false;
		}

		public int GetCrossingBoundablesCount(Bounds _Bounds)
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
						new Vector3Int(num7, num8, num9);
						if (!m_Buckets.TryGetValue(new Vector3Int(num7, num8, num9), out var value))
						{
							continue;
						}
						foreach (T item in value.Where((T _Boundable) => _Boundable.Bounds.Intersects(_Bounds)))
						{
							hashSet.Add(item);
						}
					}
				}
			}
			return hashSet.Count();
		}

		public bool TryGetEmbracingBoundables(Bounds _Bounds, out HashSet<T> _EmbracingBoundables)
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
						foreach (T item in value.Where((T _Boundable) => _Boundable.Bounds.Embrace(_Bounds)))
						{
							hashSet.Add(item);
						}
					}
				}
			}
			_EmbracingBoundables = hashSet;
			return _EmbracingBoundables.Any();
		}

		public bool TryGetEmbracedBoundables(Bounds _Bounds, out HashSet<T> _EmbracingBoundables)
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
						foreach (T item in value.Where((T _Boundable) => _Bounds.Embrace(_Boundable.Bounds)))
						{
							hashSet.Add(item);
						}
					}
				}
			}
			_EmbracingBoundables = hashSet;
			return _EmbracingBoundables.Any();
		}

		public void Register(T _Boundable)
		{
			Bounds bounds = _Boundable.Bounds;
			float maxSize = bounds.GetMaxSize();
			if (maxSize > m_BucketSize)
			{
				List<T> boundables = new List<T>(m_Boundables.Keys);
				Initialize(maxSize, boundables);
			}
			Vector3[] cornerPoints = bounds.GetCornerPoints();
			for (int i = 0; i < cornerPoints.Length; i++)
			{
				AddToBucket(_Boundable, UtilsSpatialGrid.GetBucketIndex(cornerPoints[i], m_BucketSize));
			}
		}

		public void Unregister(T _Boundable)
		{
			RemoveFromBuckets(_Boundable);
			CheckMaxBucketSize();
		}

		private void CheckMaxBucketSize()
		{
			float num = 0f;
			foreach (T key in m_Boundables.Keys)
			{
				float maxSize = key.Bounds.GetMaxSize();
				if (maxSize > num)
				{
					num = maxSize;
				}
			}
			if (num < m_BucketSize)
			{
				List<T> boundables = new List<T>(m_Boundables.Keys);
				Initialize(num, boundables);
			}
		}

		private void Initialize(float _BucketSize, IEnumerable<T> _Boundables = null)
		{
			m_Boundables.Clear();
			m_Buckets.Clear();
			m_BucketSize = _BucketSize;
			if (_Boundables.IsNullOrEmpty())
			{
				return;
			}
			foreach (T _Boundable in _Boundables)
			{
				Register(_Boundable);
			}
		}

		private void AddToBucket(T _Boundable, Vector3Int _Bucket)
		{
			if (m_Buckets.TryGetValue(_Bucket, out var value))
			{
				value.TryAdd(_Boundable);
			}
			else
			{
				ConcurrentHashSet<T> concurrentHashSet = new ConcurrentHashSet<T>();
				concurrentHashSet.TryAdd(_Boundable);
				m_Buckets.TryAdd(_Bucket, concurrentHashSet);
			}
			m_Boundables.GetOrAdd(_Boundable).Add(_Bucket);
		}

		private void RemoveFromBuckets(T _Boundable)
		{
			if (!m_Boundables.TryGetValue(_Boundable, out var value))
			{
				return;
			}
			foreach (Vector3Int item in value)
			{
				if (m_Buckets.TryGetValue(item, out var value2))
				{
					value2.TryRemove(_Boundable);
					if (!value2.Any())
					{
						m_Buckets.TryRemove(item, out var _);
					}
				}
			}
			m_Boundables.TryRemove(_Boundable, out var _);
		}

		private Vector3 GetBucketCenter(Vector3Int _Bucket)
		{
			return new Vector3((float)_Bucket.x * m_BucketSize, (float)_Bucket.y * m_BucketSize, (float)_Bucket.z * m_BucketSize);
		}
	}
}
