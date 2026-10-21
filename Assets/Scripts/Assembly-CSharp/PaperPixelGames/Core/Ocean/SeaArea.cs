using System.Collections.Generic;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean
{
	public sealed class SeaArea : MonoBehaviour
	{
		[Header("Identity")]
		[SerializeField]
		private byte _index;

		[Header("Debug")]
		[SerializeField]
		private bool _log;

		private readonly List<Transform> _cachedPoints = new List<Transform>();

		private bool _cacheDirty = true;

		public byte Index => _index;

		public int PointCount
		{
			get
			{
				EnsureCache();
				return _cachedPoints.Count;
			}
		}

		private void Awake()
		{
			RefreshPointsCache();
		}

		private void OnEnable()
		{
			_cacheDirty = true;
		}

		private void OnTransformChildrenChanged()
		{
			_cacheDirty = true;
		}

		public byte GetRandomPointIndex()
		{
			EnsureCache();
			if (_cachedPoints.Count == 0)
			{
				return 0;
			}
			return (byte)Mathf.Clamp(Random.Range(0, _cachedPoints.Count), 0, 255);
		}

		public Vector3 GetPointByIndex(byte index)
		{
			EnsureCache();
			if (_cachedPoints.Count == 0)
			{
				return base.transform.position;
			}
			int index2 = Mathf.Clamp(index, 0, _cachedPoints.Count - 1);
			Transform transform = _cachedPoints[index2];
			if (!(transform != null))
			{
				return base.transform.position;
			}
			return transform.position;
		}

		public Vector3 GetRandomPoint()
		{
			return GetPointByIndex(GetRandomPointIndex());
		}

		public bool TryGetRandomPointOutsideRadius(Vector3 referencePosition, float minimumDistance, out Vector3 point)
		{
			EnsureCache();
			point = default;
			float num = Mathf.Max(0f, minimumDistance);
			float num2 = num * num;
			int num3 = 0;
			for (int i = 0; i < _cachedPoints.Count; i++)
			{
				Transform transform = _cachedPoints[i];
				if (!(transform == null) && (transform.position - referencePosition).sqrMagnitude > num2)
				{
					num3++;
				}
			}
			if (num3 == 0)
			{
				return false;
			}
			int num4 = Random.Range(0, num3);
			for (int j = 0; j < _cachedPoints.Count; j++)
			{
				Transform transform2 = _cachedPoints[j];
				if (!(transform2 == null) && !((transform2.position - referencePosition).sqrMagnitude <= num2))
				{
					if (num4 <= 0)
					{
						point = transform2.position;
						return true;
					}
					num4--;
				}
			}
			return false;
		}

		public Vector3 GetFarthestPointFrom(Vector3 referencePosition)
		{
			EnsureCache();
			if (_cachedPoints.Count == 0)
			{
				return base.transform.position;
			}
			float num = float.MinValue;
			Vector3 position = base.transform.position;
			for (int i = 0; i < _cachedPoints.Count; i++)
			{
				Transform transform = _cachedPoints[i];
				if (!(transform == null))
				{
					float sqrMagnitude = (transform.position - referencePosition).sqrMagnitude;
					if (!(sqrMagnitude <= num))
					{
						num = sqrMagnitude;
						position = transform.position;
					}
				}
			}
			return position;
		}

		public Vector3 GetClosestPointTo(Vector3 referencePosition)
		{
			EnsureCache();
			if (_cachedPoints.Count == 0)
			{
				return base.transform.position;
			}
			float num = float.MaxValue;
			Vector3 position = base.transform.position;
			for (int i = 0; i < _cachedPoints.Count; i++)
			{
				Transform transform = _cachedPoints[i];
				if (!(transform == null))
				{
					float sqrMagnitude = (transform.position - referencePosition).sqrMagnitude;
					if (!(sqrMagnitude >= num))
					{
						num = sqrMagnitude;
						position = transform.position;
					}
				}
			}
			return position;
		}

		public List<Vector3> GetRandomPath(int pointCount, bool allowRepeats = false)
		{
			EnsureCache();
			if (pointCount <= 0)
			{
				return new List<Vector3>(0);
			}
			if (_cachedPoints.Count == 0)
			{
				return new List<Vector3>(1) { base.transform.position };
			}
			if (!allowRepeats)
			{
				return GetRandomPathWithoutRepeats(pointCount);
			}
			return GetRandomPathWithRepeats(pointCount);
		}

		public bool TryGetRandomPath(int pointCount, bool allowRepeats, out List<Vector3> path)
		{
			path = GetRandomPath(pointCount, allowRepeats);
			if (path != null)
			{
				return path.Count > 0;
			}
			return false;
		}

		public bool HasAnyPoint()
		{
			EnsureCache();
			return _cachedPoints.Count > 0;
		}

		public bool TryGetWorldBounds(out Bounds bounds)
		{
			EnsureCache();
			bounds = default;
			Transform transform = null;
			for (int i = 0; i < _cachedPoints.Count; i++)
			{
				if (!(_cachedPoints[i] == null))
				{
					transform = _cachedPoints[i];
					break;
				}
			}
			if (transform == null)
			{
				return false;
			}
			bounds = new Bounds(transform.position, Vector3.zero);
			for (int j = 0; j < _cachedPoints.Count; j++)
			{
				Transform transform2 = _cachedPoints[j];
				if (transform2 != null)
				{
					bounds.Encapsulate(transform2.position);
				}
			}
			return true;
		}

		public void RefreshPointsCache()
		{
			_cachedPoints.Clear();
			int childCount = base.transform.childCount;
			if (childCount > 255)
			{
				Log($"SeaArea has {childCount} points. byte index supports max {256} points. Extra points can still be used internally, but network-safe index access should stay under byte range.", LogType.Warning);
			}
			for (int i = 0; i < childCount; i++)
			{
				Transform child = base.transform.GetChild(i);
				if (child != null)
				{
					_cachedPoints.Add(child);
				}
			}
			_cacheDirty = false;
		}

		private List<Vector3> GetRandomPathWithRepeats(int pointCount)
		{
			List<Vector3> list = new List<Vector3>(pointCount);
			for (int i = 0; i < pointCount; i++)
			{
				Transform transform = _cachedPoints[Random.Range(0, _cachedPoints.Count)];
				if (transform != null)
				{
					list.Add(transform.position);
				}
			}
			return list;
		}

		private List<Vector3> GetRandomPathWithoutRepeats(int pointCount)
		{
			int num = Mathf.Min(pointCount, _cachedPoints.Count);
			List<Vector3> list = new List<Vector3>(num);
			List<int> list2 = new List<int>(_cachedPoints.Count);
			for (int i = 0; i < _cachedPoints.Count; i++)
			{
				list2.Add(i);
			}
			for (int j = 0; j < list2.Count; j++)
			{
				int num2 = Random.Range(j, list2.Count);
				int index = j;
				List<int> list3 = list2;
				int index2 = num2;
				int num3 = list2[num2];
				int num4 = list2[j];
				int num5 = (list2[index] = num3);
				num5 = (list3[index2] = num4);
			}
			for (int k = 0; k < num; k++)
			{
				Transform transform = _cachedPoints[list2[k]];
				if (transform != null)
				{
					list.Add(transform.position);
				}
			}
			return list;
		}

		private void EnsureCache()
		{
			if (_cacheDirty)
			{
				RefreshPointsCache();
			}
		}

		private void Log(string message, LogType type = LogType.Log)
		{
			if (_log)
			{
				UtilityMethods.Log("[SeaArea:" + base.name + "] " + message, type);
			}
		}
	}
}
