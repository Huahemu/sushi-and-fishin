using System.Collections.Generic;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public sealed class CreaturePathAssignment : MonoBehaviour, ICreaturePathProvider
	{
		private SeaArea _area;

		private readonly List<Vector3> _standalonePoints = new List<Vector3>();

		private Transform _excludedPatrolTarget;

		private bool _loop = true;

		private bool _startFromClosest;

		private float _reachDistance = 0.35f;

		private float _excludedPatrolTargetRadius = 0.5f;

		private float _excludedPatrolTargetRadiusSqr = 0.25f;

		public void Assign(SeaArea area, bool loop, bool startFromClosest, float reachDistance, Transform excludedPatrolTarget, float excludedPatrolTargetRadius)
		{
			_area = area;
			_standalonePoints.Clear();
			_excludedPatrolTarget = excludedPatrolTarget;
			_loop = loop;
			_startFromClosest = startFromClosest;
			_reachDistance = Mathf.Max(0.01f, reachDistance);
			_excludedPatrolTargetRadius = Mathf.Max(0f, excludedPatrolTargetRadius);
			_excludedPatrolTargetRadiusSqr = _excludedPatrolTargetRadius * _excludedPatrolTargetRadius;
			if (_area != null)
			{
				_area.RefreshPointsCache();
			}
		}

		public bool Assign(Transform patrolPointsRoot, bool loop, bool startFromClosest, float reachDistance, Transform excludedPatrolTarget, float excludedPatrolTargetRadius)
		{
			_area = null;
			_standalonePoints.Clear();
			_excludedPatrolTarget = excludedPatrolTarget;
			_loop = loop;
			_startFromClosest = startFromClosest;
			_reachDistance = Mathf.Max(0.01f, reachDistance);
			_excludedPatrolTargetRadius = Mathf.Max(0f, excludedPatrolTargetRadius);
			_excludedPatrolTargetRadiusSqr = _excludedPatrolTargetRadius * _excludedPatrolTargetRadius;
			if (patrolPointsRoot == null)
			{
				return false;
			}
			for (int i = 0; i < patrolPointsRoot.childCount; i++)
			{
				Transform child = patrolPointsRoot.GetChild(i);
				if (!(child == null) && !IsExcludedPatrolPoint(child.position))
				{
					_standalonePoints.Add(child.position);
				}
			}
			return _standalonePoints.Count > 0;
		}

		public bool TryGetPatrolPath(int pointCount, bool allowRepeats, out List<Vector3> path)
		{
			path = null;
			if (_area == null)
			{
				path = GetStandalonePath(pointCount, allowRepeats);
				if (path != null)
				{
					return path.Count > 0;
				}
				return false;
			}
			int pointCount2 = (allowRepeats ? Mathf.Max(pointCount * 4, pointCount) : Mathf.Max(pointCount, _area.PointCount));
			List<Vector3> randomPath = _area.GetRandomPath(pointCount2, allowRepeats);
			if (randomPath == null || randomPath.Count == 0)
			{
				return false;
			}
			path = new List<Vector3>(Mathf.Min(pointCount, randomPath.Count));
			for (int i = 0; i < randomPath.Count; i++)
			{
				if (path.Count >= pointCount)
				{
					break;
				}
				Vector3 vector = randomPath[i];
				if (!IsExcludedPatrolPoint(vector))
				{
					path.Add(vector);
				}
			}
			if (path != null)
			{
				return path.Count > 0;
			}
			return false;
		}

		public bool TryGetRandomPoint(out Vector3 point)
		{
			point = default;
			if (_area == null)
			{
				if (_standalonePoints.Count == 0)
				{
					return false;
				}
				point = _standalonePoints[Random.Range(0, _standalonePoints.Count)];
				return true;
			}
			if (!_area.HasAnyPoint())
			{
				return false;
			}
			int num = Mathf.Max(_area.PointCount * 2, 4);
			for (int i = 0; i < num; i++)
			{
				point = _area.GetRandomPoint();
				if (!IsExcludedPatrolPoint(point))
				{
					return true;
				}
			}
			return false;
		}

		public bool TryGetFarthestPointFrom(Vector3 referencePosition, out Vector3 point)
		{
			point = default;
			if (_area == null)
			{
				if (_standalonePoints.Count == 0)
				{
					return false;
				}
				float num = float.MinValue;
				for (int i = 0; i < _standalonePoints.Count; i++)
				{
					float sqrMagnitude = (_standalonePoints[i] - referencePosition).sqrMagnitude;
					if (!(sqrMagnitude <= num))
					{
						num = sqrMagnitude;
						point = _standalonePoints[i];
					}
				}
				return true;
			}
			if (!_area.HasAnyPoint())
			{
				return false;
			}
			point = _area.GetFarthestPointFrom(referencePosition);
			if (!IsExcludedPatrolPoint(point))
			{
				return true;
			}
			return TryGetRandomPoint(out point);
		}

		public bool TryGetPatrolLoop(out bool loop)
		{
			loop = _loop;
			return true;
		}

		public bool TryGetPatrolStartFromClosest(out bool startFromClosest)
		{
			startFromClosest = _startFromClosest;
			return true;
		}

		public bool TryGetPatrolReachDistance(out float reachDistance)
		{
			reachDistance = _reachDistance;
			return true;
		}

		private bool IsExcludedPatrolPoint(Vector3 point)
		{
			if (_excludedPatrolTarget == null || _excludedPatrolTargetRadius <= 0f)
			{
				return false;
			}
			return (point - _excludedPatrolTarget.position).sqrMagnitude <= _excludedPatrolTargetRadiusSqr;
		}

		private List<Vector3> GetStandalonePath(int pointCount, bool allowRepeats)
		{
			if (pointCount <= 0 || _standalonePoints.Count == 0)
			{
				return null;
			}
			if (allowRepeats)
			{
				List<Vector3> list = new List<Vector3>(pointCount);
				for (int i = 0; i < pointCount; i++)
				{
					list.Add(_standalonePoints[Random.Range(0, _standalonePoints.Count)]);
				}
				return list;
			}
			int num = Mathf.Min(pointCount, _standalonePoints.Count);
			List<int> list2 = new List<int>(_standalonePoints.Count);
			for (int j = 0; j < _standalonePoints.Count; j++)
			{
				list2.Add(j);
			}
			for (int k = 0; k < list2.Count; k++)
			{
				int num2 = Random.Range(k, list2.Count);
				int index = k;
				List<int> list3 = list2;
				int index2 = num2;
				int num3 = list2[num2];
				int num4 = list2[k];
				int num5 = (list2[index] = num3);
				num5 = (list3[index2] = num4);
			}
			List<Vector3> list4 = new List<Vector3>(num);
			for (int l = 0; l < num; l++)
			{
				list4.Add(_standalonePoints[list2[l]]);
			}
			return list4;
		}
	}
}
