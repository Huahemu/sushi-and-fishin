using System;
using System.Collections.Generic;
using System.Linq;
using Nav3D.Obstacles;
using UnityEngine;

namespace Nav3D.Common
{
	internal class SpatialHashMap<T> where T : IMovable
	{
		private float m_BucketSize;

		private Dictionary<Vector3Int, HashSet<T>> m_Buckets = new Dictionary<Vector3Int, HashSet<T>>();

		private Dictionary<Vector3Int, HashSet<T>> m_BucketsEmbracing = new Dictionary<Vector3Int, HashSet<T>>();

		private Dictionary<T, Vector3Int> m_BucketLinks = new Dictionary<T, Vector3Int>();

		public float BucketSize => m_BucketSize;

		public SpatialHashMap(float _BucketSize, IEnumerable<T> _InitialSet)
		{
			Initialize(_BucketSize, _InitialSet);
		}

		public void Insert(T _Element)
		{
			if (!m_BucketLinks.ContainsKey(_Element))
			{
				Vector3Int bucketIndex = UtilsSpatialGrid.GetBucketIndex(_Element.GetPosition(), m_BucketSize);
				AddToBucket(bucketIndex, _Element);
				m_BucketLinks.Add(_Element, bucketIndex);
				Action<IMovable, Vector3> value = UpdatePosition;
				_Element.OnPositionChanged += value;
			}
		}

		public void Remove(T _Element)
		{
			if (m_BucketLinks.TryGetValue(_Element, out var _))
			{
				RemoveFromOldBuckets(_Element);
				m_BucketLinks.Remove(_Element);
				_Element.OnPositionChanged -= UpdatePosition;
			}
		}

		public List<T> GetMovablesInBounds(Bounds _Bounds)
		{
			List<T> list = new List<T>();
			float x = Mathf.Min(_Bounds.min.x, _Bounds.max.x);
			float x2 = Mathf.Max(_Bounds.min.x, _Bounds.max.x);
			float y = Mathf.Min(_Bounds.min.y, _Bounds.max.y);
			float y2 = Mathf.Max(_Bounds.min.y, _Bounds.max.y);
			float z = Mathf.Min(_Bounds.min.z, _Bounds.max.z);
			float z2 = Mathf.Max(_Bounds.min.z, _Bounds.max.z);
			Vector3Int bucketIndex = UtilsSpatialGrid.GetBucketIndex(new Vector3(x, y, z), m_BucketSize);
			Vector3Int bucketIndex2 = UtilsSpatialGrid.GetBucketIndex(new Vector3(x2, y2, z2), m_BucketSize);
			for (int i = bucketIndex.x; i <= bucketIndex2.x; i++)
			{
				for (int j = bucketIndex.y; j < bucketIndex2.y; j++)
				{
					for (int k = bucketIndex.z; k < bucketIndex2.z; k++)
					{
						if (m_Buckets.TryGetValue(new Vector3Int(i, j, k), out var value))
						{
							list.AddRange(value);
						}
					}
				}
			}
			return list;
		}

		public bool HasNeighbors(T _Element)
		{
			if (!m_BucketLinks.TryGetValue(_Element, out var value))
			{
				return false;
			}
			return m_BucketsEmbracing[value].Count > 1;
		}

		public HashSet<T> GetElementBucketElements(T _Element)
		{
			if (!m_BucketLinks.TryGetValue(_Element, out var value))
			{
				return new HashSet<T>();
			}
			return m_BucketsEmbracing[value];
		}

		public HashSet<T> GetBucketElements(Vector3 _InsidePoint)
		{
			Vector3Int bucketIndex = UtilsSpatialGrid.GetBucketIndex(_InsidePoint, m_BucketSize);
			if (m_BucketsEmbracing.TryGetValue(bucketIndex, out var value))
			{
				return value;
			}
			return new HashSet<T>();
		}

		public bool HasNearestObstacles(T _Element, out Bounds _DangerBounds)
		{
			Vector3Int bucketIndex = UtilsSpatialGrid.GetBucketIndex(_Element.GetPosition(), m_BucketSize);
			_DangerBounds = UtilsSpatialGrid.GetBucketBounds(bucketIndex, m_BucketSize).Enlarge(_Element.GetStaticObstaclesDangerDistance() * 2f);
			return ObstacleManager.Instance.BoundsCrossObstacles(_DangerBounds);
		}

		public void SetMovablesInBoundsObstacleDirty(Bounds _Bounds)
		{
			float x = Mathf.Min(_Bounds.min.x, _Bounds.max.x);
			float x2 = Mathf.Max(_Bounds.min.x, _Bounds.max.x);
			float y = Mathf.Min(_Bounds.min.y, _Bounds.max.y);
			float y2 = Mathf.Max(_Bounds.min.y, _Bounds.max.y);
			float z = Mathf.Min(_Bounds.min.z, _Bounds.max.z);
			float z2 = Mathf.Max(_Bounds.min.z, _Bounds.max.z);
			Vector3Int bucketIndex = UtilsSpatialGrid.GetBucketIndex(new Vector3(x, y, z), m_BucketSize);
			Vector3Int bucketIndex2 = UtilsSpatialGrid.GetBucketIndex(new Vector3(x2, y2, z2), m_BucketSize);
			for (int i = bucketIndex.x; i <= bucketIndex2.x; i++)
			{
				for (int j = bucketIndex.y; j < bucketIndex2.y; j++)
				{
					for (int k = bucketIndex.z; k < bucketIndex2.z; k++)
					{
						if (m_BucketsEmbracing.TryGetValue(new Vector3Int(i, j, k), out var value))
						{
							value.ForEach((T _Movable) =>
							{
								_Movable.SetNeighborObstaclesDirty(_Dirty: true);
							});
						}
					}
				}
			}
		}

		public void SetBucketSize(float _Size)
		{
			if (!Mathf.Approximately(_Size, m_BucketSize) && !(_Size <= 0f))
			{
				ObstacleManager.Instance.RecreateObstacleTriangleStorages(_Size);
				Initialize(_Size, new List<T>(m_BucketLinks.Keys));
			}
		}

		public void Draw()
		{
			foreach (KeyValuePair<T, Vector3Int> bucketLink in m_BucketLinks)
			{
				Bounds bucketBounds = UtilsSpatialGrid.GetBucketBounds(bucketLink.Value, m_BucketSize);
				Bounds bounds = new Bounds(bucketBounds.center, new Vector3(m_BucketSize * 3f, m_BucketSize * 3f, m_BucketSize * 3f));
				Gizmos.color = Color.green;
				bucketBounds.Draw();
				Gizmos.color = Color.magenta;
				bounds.Draw();
			}
		}

		private void Initialize(float _BucketSize, IEnumerable<T> _InitialSet)
		{
			m_Buckets.Clear();
			m_BucketsEmbracing.Clear();
			m_BucketLinks.Clear();
			m_BucketSize = _BucketSize;
			foreach (T item in _InitialSet)
			{
				Insert(item);
			}
		}

		private void AddToBucket(Vector3Int _Bucket, T _Element)
		{
			if (m_Buckets.TryGetValue(_Bucket, out var value))
			{
				value.ForEach((T _Movable) =>
				{
					_Movable.SetNeighborMovablesDirty(_Dirty: true);
				});
				value.Add(_Element);
			}
			else
			{
				m_Buckets.GetOrAdd(_Bucket).Add(_Element);
			}
			Vector3Int[] neighborBuckets = UtilsMath.NeighborBuckets;
			foreach (Vector3Int vector3Int in neighborBuckets)
			{
				Vector3Int key = _Bucket + vector3Int;
				if (m_BucketsEmbracing.TryGetValue(key, out var value2))
				{
					value2.ForEach((T _Movable) =>
					{
						_Movable.SetNeighborMovablesDirty(_Dirty: true);
					});
					value2.Add(_Element);
				}
				else
				{
					m_BucketsEmbracing.GetOrAdd(key).Add(_Element);
				}
			}
			_Element.SetNeighborObstaclesDirty(_Dirty: true);
			_Element.SetNeighborMovablesDirty(_Dirty: true);
		}

		private void RemoveFromOldBuckets(T _Element)
		{
			if (!m_BucketLinks.TryGetValue(_Element, out var value))
			{
				return;
			}
			if (m_Buckets.TryGetValue(value, out var value2))
			{
				value2.Remove(_Element);
				value2.ForEach((T _Movable) =>
				{
					_Movable.SetNeighborMovablesDirty(_Dirty: true);
				});
				if (!value2.Any())
				{
					m_Buckets.Remove(value);
				}
			}
			Vector3Int[] neighborBuckets = UtilsMath.NeighborBuckets;
			foreach (Vector3Int vector3Int in neighborBuckets)
			{
				Vector3Int key = value + vector3Int;
				if (m_BucketsEmbracing.TryGetValue(key, out var value3))
				{
					value3.Remove(_Element);
					value3.ForEach((T _Movable) =>
					{
						_Movable.SetNeighborMovablesDirty(_Dirty: true);
					});
					if (!value3.Any())
					{
						m_BucketsEmbracing.Remove(key);
					}
				}
			}
		}

		private void UpdateBucket(Vector3Int _BucketOld, Vector3Int _BucketNew, T _Element)
		{
			if (m_Buckets.TryGetValue(_BucketOld, out var value))
			{
				value.Remove(_Element);
				if (!value.Any())
				{
					m_Buckets.Remove(_BucketOld);
				}
			}
			Vector3Int[] neighborBuckets = UtilsMath.NeighborBuckets;
			foreach (Vector3Int vector3Int in neighborBuckets)
			{
				Vector3Int vector3Int2 = _BucketOld + vector3Int;
				if (!m_BucketsEmbracing.TryGetValue(vector3Int2, out var value2))
				{
					continue;
				}
				value2.Remove(_Element);
				if (!_BucketNew.IsNeighborBucket(vector3Int2))
				{
					value2.ForEach((T _Movable) =>
					{
						_Movable.SetNeighborMovablesDirty(_Dirty: true);
					});
				}
				if (!value2.Any())
				{
					m_BucketsEmbracing.Remove(vector3Int2);
				}
			}
			m_Buckets.GetOrAdd(_BucketNew).Add(_Element);
			neighborBuckets = UtilsMath.NeighborBuckets;
			foreach (Vector3Int vector3Int3 in neighborBuckets)
			{
				Vector3Int vector3Int4 = _BucketNew + vector3Int3;
				if (m_BucketsEmbracing.TryGetValue(vector3Int4, out var value3))
				{
					value3.Add(_Element);
					if (!_BucketOld.IsNeighborBucket(vector3Int4))
					{
						value3.ForEach((T _Movable) =>
						{
							_Movable.SetNeighborMovablesDirty(_Dirty: true);
						});
					}
				}
				else
				{
					m_BucketsEmbracing.GetOrAdd(vector3Int4).Add(_Element);
				}
			}
			_Element.SetNeighborMovablesDirty(_Dirty: true);
			_Element.SetNeighborObstaclesDirty(_Dirty: true);
		}

		private void UpdatePosition(IMovable _Movable, Vector3 _NewPosition)
		{
			T val = (T)_Movable;
			if (m_BucketLinks.TryGetValue(val, out var value))
			{
				Vector3Int bucketIndex = UtilsSpatialGrid.GetBucketIndex(_NewPosition, m_BucketSize);
				if (!(value == bucketIndex))
				{
					UpdateBucket(value, bucketIndex, val);
					m_BucketLinks[val] = bucketIndex;
				}
			}
		}
	}
}
