using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nav3D.API;
using Nav3D.Common;
using Nav3D.LocalAvoidance;
using Nav3D.Pathfinding;
using UnityEngine;

namespace Nav3D.Obstacles
{
	internal class ObstacleStorage
	{
		private readonly float m_MinBucketSize;

		private readonly Dictionary<int, List<ObstacleInfoBase>> m_IDToInfo = new Dictionary<int, List<ObstacleInfoBase>>();

		private readonly ConcurrentDictionary<ObstacleInfoBase, Obstacle> m_InfoToObstacle = new ConcurrentDictionary<ObstacleInfoBase, Obstacle>();

		private readonly BoundablesSpatialHashMap<Obstacle> m_ObstacleStorage = new BoundablesSpatialHashMap<Obstacle>();

		private readonly SequentialTasksExecutor m_TasksExecutor = new SequentialTasksExecutor();

		public ObstacleStorage(float _MinBucketSize)
		{
			m_MinBucketSize = _MinBucketSize;
		}

		public void AddObstacles(List<ObstacleInfoBase> _ObstacleInfos, ObstacleAdditionProgress _AdditionProgress, Action _OnFinish = null, bool _EditMode = false)
		{
			m_TasksExecutor.AddTask(() =>
			{
				ConstructObstacles(_ObstacleInfos, _AdditionProgress, _OnFinish, _EditMode);
			});
		}

		public void AddBakedObstacles(Dictionary<ObstacleInfoBase, Obstacle> _ObstaclesDatas, Action _OnFinish)
		{
			m_TasksExecutor.AddTask(() =>
			{
				RegisterBakedObstacles(_ObstaclesDatas, _OnFinish);
			});
		}

		public void RemoveObstacles(int _ObstacleControllerID, Action _OnFinish = null)
		{
			m_TasksExecutor.AddTask(() =>
			{
				DeconstructObstacles(_ObstacleControllerID, _OnFinish);
			});
		}

		public void UpdateObstaclesCrossingTheBounds(Bounds _Bounds)
		{
			m_TasksExecutor.AddTask(() =>
			{
				if (m_ObstacleStorage.TryGetCrossingBoundables(_Bounds, out var _CrossingBoundables))
				{
					_CrossingBoundables.RemoveWhere((Obstacle _Obstacle) => _Obstacle.IsStatic);
					List<ObstacleInfoBase> list = _CrossingBoundables.Select((Obstacle _Obstacle) => _Obstacle.ObstacleInfo).ToList();
					list.ForEach((ObstacleInfoBase _Info) =>
					{
						RemoveObstacleByInfo(_Info);
					});
					ConstructObstacles(list);
				}
			});
		}

		public bool BoundsCrossStaticObstacles(Bounds _Bounds)
		{
			if (!m_ObstacleStorage.TryGetCrossingBoundables(_Bounds, out var _CrossingBoundables))
			{
				return false;
			}
			return _CrossingBoundables.Any((Obstacle _Obstacle) => _Obstacle.IsStatic);
		}

		public bool BoundsCrossObstacles(Bounds _Bounds)
		{
			HashSet<Obstacle> _CrossingBoundables;
			return m_ObstacleStorage.TryGetCrossingBoundables(_Bounds, out _CrossingBoundables);
		}

		public bool IsAnyObstacleIntersects(Vector3 _Point)
		{
			if (!m_ObstacleStorage.TryGetCrossingBoundables(_Point, out var _CrossingBoundables))
			{
				return false;
			}
			return _CrossingBoundables.Any((Obstacle _Obstacle) => _Obstacle.PointInsideOccupiedLeaf(_Point));
		}

		public int GetObstacleNodesCount(int _ObstacleControllerID)
		{
			if (!m_IDToInfo.TryGetValue(_ObstacleControllerID, out var value))
			{
				return 0;
			}
			lock (value)
			{
				return value.Select((ObstacleInfoBase _Info) => m_InfoToObstacle.TryGetValue(_Info, out var value2) ? value2.NodesCount : 0).Max();
			}
		}

		public int GetObstacleOctreeLayersMaxCount(int _ObstacleControllerID)
		{
			if (!m_IDToInfo.TryGetValue(_ObstacleControllerID, out var value))
			{
				return 0;
			}
			lock (value)
			{
				return value.Select((ObstacleInfoBase _Info) => m_InfoToObstacle.TryGetValue(_Info, out var value2) ? value2.OctreeLayersCount : 0).Max();
			}
		}

		public string GetStorageContentDescription()
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (KeyValuePair<int, List<ObstacleInfoBase>> item in m_IDToInfo)
			{
				string value = string.Format("{0}:\n{1}\n", item.Key, string.Join(", ", item.Value.Select((ObstacleInfoBase _Info) => _Info.ToString())));
				stringBuilder.Append(value);
				stringBuilder.Append("----------------\n");
			}
			return stringBuilder.ToString();
		}

		public void Uninitialize()
		{
			m_IDToInfo.Clear();
			m_InfoToObstacle.ForEach((KeyValuePair<ObstacleInfoBase, Obstacle> _Kvp) =>
			{
				_Kvp.Value.Invalidate();
			});
		}

		public List<Obstacle> GetObstaclesCrossingTheLine(Vector3 _PointA, Vector3 _PointB)
		{
			Bounds bounds = ExtensionBounds.MinMax(_PointA, _PointB);
			m_ObstacleStorage.TryGetCrossingBoundables(bounds, out var _CrossingBoundables);
			return _CrossingBoundables.Where((Obstacle _Obstacle) => _Obstacle.Bounds.IntersectSegment(new Segment3(_PointA, _PointB))).ToList();
		}

		public List<Obstacle> GetObstaclesCrossingTheBounds(Bounds _Bounds)
		{
			m_ObstacleStorage.TryGetCrossingBoundables(_Bounds, out var _CrossingBoundables);
			return _CrossingBoundables.ToList();
		}

		public List<Triangle> GetIntersectedObstaclesTriangles(Bounds _Bounds)
		{
			return GetIntersectedObstaclesTrianglesInternal(_Bounds);
		}

		public Dictionary<ObstacleInfoBase, Obstacle> GetObstacleDatas(int _ObstacleControllerID)
		{
			Dictionary<ObstacleInfoBase, Obstacle> dictionary = new Dictionary<ObstacleInfoBase, Obstacle>();
			if (!m_IDToInfo.TryGetValue(_ObstacleControllerID, out var value))
			{
				return dictionary;
			}
			foreach (ObstacleInfoBase item in value)
			{
				if (m_InfoToObstacle.TryGetValue(item, out var value2))
				{
					dictionary.Add(item, value2);
				}
			}
			return dictionary;
		}

		public Dictionary<ObstacleInfoBase, Obstacle> GetObstacleDatas()
		{
			Dictionary<ObstacleInfoBase, Obstacle> dictionary = new Dictionary<ObstacleInfoBase, Obstacle>();
			foreach (KeyValuePair<ObstacleInfoBase, Obstacle> item in m_InfoToObstacle)
			{
				dictionary.Add(item.Key, item.Value);
			}
			return dictionary;
		}

		public void RecreateObstacleTriangleStorages(float _BucketSize)
		{
			foreach (KeyValuePair<ObstacleInfoBase, Obstacle> item in m_InfoToObstacle)
			{
				item.Value.RecreateTriangleStorage(_BucketSize);
			}
		}

		private void ConstructObstacles(List<ObstacleInfoBase> _ObstaclesInfo, ObstacleAdditionProgress _AdditionProgress = null, Action _OnFinish = null, bool _EditMode = false)
		{
			_AdditionProgress?.SetStatus(ObstacleAdditionProgress.ObstacleAdditionStatus.OBSTACLES_CLUSTERIZATION);
			ClusterizeObstacles(_ObstaclesInfo, out var _ClusterizedObstacles, out var _ObsoleteInfos, _AdditionProgress);
			BuildAndRegisterObstacles(_ClusterizedObstacles, _ObsoleteInfos, _AdditionProgress, _OnFinish, _EditMode);
		}

		private void DeconstructObstacles(int _ObstacleControllerID, Action _OnFinish = null)
		{
			if (!m_IDToInfo.TryGetValue(_ObstacleControllerID, out var value))
			{
				_OnFinish?.Invoke();
				return;
			}
			ExcludeInfosByID(_ObstacleControllerID, value, out var _ExcludedInfos, out var _RemainingInfos);
			BuildAndRegisterObstacles(_RemainingInfos, _ExcludedInfos, null, _OnFinish);
		}

		private void RegisterBakedObstacles(Dictionary<ObstacleInfoBase, Obstacle> _ObstaclesDatas, Action _OnFinish, bool _EditMode = false)
		{
			RegisterObstacleData(_ObstaclesDatas);
			if (_OnFinish == null)
			{
				return;
			}
			if (!_EditMode)
			{
				ThreadDispatcher.BeginInvoke(() =>
				{
					_OnFinish();
				});
				return;
			}
			try
			{
				_OnFinish();
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}

		private void BuildAndRegisterObstacles(List<ObstacleInfoBase> _NewInfos, List<ObstacleInfoBase> _InfosToDelete, ObstacleAdditionProgress _AdditionProgress = null, Action _OnFinish = null, bool _EditMode = false)
		{
			if (!_NewInfos.Any() && !_InfosToDelete.Any())
			{
				_AdditionProgress?.SetStatus(ObstacleAdditionProgress.ObstacleAdditionStatus.FINISHED);
				return;
			}
			_AdditionProgress?.SetStatus(ObstacleAdditionProgress.ObstacleAdditionStatus.OBSTACLES_GRAPH_CONSTRUCTION);
			Dictionary<ObstacleInfoBase, Obstacle> dictionary = _NewInfos.ToDictionary((ObstacleInfoBase _Info) => _Info, (ObstacleInfoBase _Info) => new Obstacle(_Info, m_MinBucketSize));
			int num = 0;
			foreach (Obstacle obstacle in dictionary.Values)
			{
				_AdditionProgress?.CancellationToken.Register(() =>
				{
					obstacle.Invalidate();
				});
				if (_AdditionProgress != null && _AdditionProgress.CancellationToken.IsCancellationRequested)
				{
					return;
				}
				num++;
				_AdditionProgress?.SetCurrentConstructionProgress(obstacle.ConstructionProgress);
				obstacle.ConstructGraph();
				_AdditionProgress?.SetProgress(num / dictionary.Values.Count);
			}
			_AdditionProgress?.SetStatus(ObstacleAdditionProgress.ObstacleAdditionStatus.REMOVING_OBSOLETE_OBSTACLES);
			if (_AdditionProgress != null && _AdditionProgress.CancellationToken.IsCancellationRequested)
			{
				return;
			}
			foreach (ObstacleInfoBase item in _InfosToDelete)
			{
				RemoveObstacleByInfo(item);
			}
			RegisterObstacleData(dictionary);
			_AdditionProgress?.SetStatus(ObstacleAdditionProgress.ObstacleAdditionStatus.FINISHED);
			if (_OnFinish == null)
			{
				return;
			}
			if (!_EditMode)
			{
				ThreadDispatcher.BeginInvoke(() =>
				{
					_OnFinish();
				});
				return;
			}
			try
			{
				_OnFinish?.Invoke();
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}

		private void RegisterObstacleData(Dictionary<ObstacleInfoBase, Obstacle> _ObstaclesData)
		{
			foreach (KeyValuePair<ObstacleInfoBase, Obstacle> _ObstaclesDatum in _ObstaclesData)
			{
				ObstacleInfoBase key = _ObstaclesDatum.Key;
				foreach (int item in key.IDs.Distinct())
				{
					List<ObstacleInfoBase> orAdd = m_IDToInfo.GetOrAdd(item);
					lock (orAdd)
					{
						orAdd.Add(key);
					}
				}
				Obstacle value = _ObstaclesDatum.Value;
				m_ObstacleStorage.Register(value);
				m_InfoToObstacle.TryAdd(value.ObstacleInfo, value);
				PathfindingManager.Instance.UpdateAllBoundsCrossingPaths(value.Bounds);
				AgentManager.Instance.SetMovablesInBoundsObstacleDirty(value.Bounds);
			}
		}

		private void RemoveObstacleByInfo(ObstacleInfoBase _ObstacleInfo)
		{
			if (m_InfoToObstacle.TryGetValue(_ObstacleInfo, out var value))
			{
				m_InfoToObstacle.TryRemove(_ObstacleInfo, out var _);
				m_ObstacleStorage.Unregister(value);
				value.Invalidate();
			}
			foreach (int iD in _ObstacleInfo.IDs)
			{
				if (!m_IDToInfo.TryGetValue(iD, out var value3))
				{
					continue;
				}
				lock (value3)
				{
					value3.Remove(_ObstacleInfo);
					if (!value3.Any())
					{
						m_IDToInfo.Remove(iD);
					}
				}
			}
			AgentManager.Instance.SetMovablesInBoundsObstacleDirty(value.Bounds);
			PathfindingManager.Instance.UpdateAllBoundsCrossingPaths(value.Bounds);
		}

		private void ClusterizeObstacles(List<ObstacleInfoBase> _NewObstacleInfos, out List<ObstacleInfoBase> _ClusterizedObstacles, out List<ObstacleInfoBase> _ObsoleteInfos, ObstacleAdditionProgress _AdditionProgress = null)
		{
			List<ObstacleInfoBase> list = _NewObstacleInfos.Copy();
			List<ObstacleInfoBase> list2 = new List<ObstacleInfoBase>();
			HashSet<ObstacleInfoBase> hashSet = new HashSet<ObstacleInfoBase>();
			bool flag;
			do
			{
				flag = false;
				int num = 0;
				foreach (ObstacleInfoBase item in list)
				{
					num++;
					if (!m_ObstacleStorage.TryGetCrossingBoundables(item.Bounds, out var _CrossingBoundables))
					{
						continue;
					}
					foreach (Obstacle item2 in _CrossingBoundables)
					{
						if (hashSet.Add(item2.ObstacleInfo))
						{
							list2.Add(item2.ObstacleInfo);
							flag = true;
						}
					}
					_AdditionProgress?.SetProgress(num / list.Count);
					if (_AdditionProgress != null && _AdditionProgress.CancellationToken.IsCancellationRequested)
					{
						break;
					}
				}
				list.AddRange(list2);
				list2.Clear();
				list = ObstacleInfoBase.GroupInfos(list);
			}
			while (flag && (_AdditionProgress == null || !_AdditionProgress.CancellationToken.IsCancellationRequested));
			_ClusterizedObstacles = list;
			_ObsoleteInfos = hashSet.ToList();
		}

		private void ExcludeInfosByID(int _ObstacleControllerID, List<ObstacleInfoBase> _Infos, out List<ObstacleInfoBase> _ExcludedInfos, out List<ObstacleInfoBase> _RemainingInfos)
		{
			List<ObstacleInfoBase> list = new List<ObstacleInfoBase>();
			List<ObstacleInfoBase> list2 = new List<ObstacleInfoBase>();
			foreach (ObstacleInfoBase _Info in _Infos)
			{
				if (_Info is ObstacleInfoSingle obstacleInfoSingle)
				{
					list2.Add(_Info);
					if (obstacleInfoSingle.ObstacleControllerID != _ObstacleControllerID)
					{
						list.Add(obstacleInfoSingle);
					}
				}
				else if (_Info is ObstacleInfoGrouped obstacleInfoGrouped)
				{
					list2.Add(_Info);
					list.AddRange(obstacleInfoGrouped.GetInfosExceptByID(_ObstacleControllerID));
				}
			}
			_ExcludedInfos = list2;
			_RemainingInfos = ObstacleInfoBase.GroupInfos(list);
		}

		private List<Triangle> GetIntersectedObstaclesTrianglesInternal(Bounds _Bounds)
		{
			List<Triangle> list = new List<Triangle>();
			if (!m_ObstacleStorage.TryGetCrossingBoundables(_Bounds, out var _CrossingBoundables))
			{
				return list;
			}
			foreach (Obstacle item in _CrossingBoundables)
			{
				list.AddRange(item.GetIntersectedOccupiedTriangles(_Bounds));
			}
			return list;
		}
	}
}
