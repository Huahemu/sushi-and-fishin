using System;
using System.Collections.Generic;
using System.Linq;
using Nav3D.Common;
using Nav3D.Obstacles.Serialization;
using UnityEngine;

namespace Nav3D.Obstacles
{
	public class ObstacleInfoGrouped : ObstacleInfoBase
	{
		private static readonly string EXCEPTION_UNSUPPORTED_OBSTACLE_INFO_TYPE = "[CombineObstacleInfos]: Unsupported obstacle types: A:{0}, B{1}";

		public List<int> m_IDs;

		public override List<int> IDs => m_IDs;

		public Dictionary<int, List<ObstacleInfoSingle>> ObstacleInfos { get; private set; }

		public static ObstacleInfoGrouped CombineObstacleInfos(ObstacleInfoBase _ObstacleInfoA, ObstacleInfoBase _ObstacleInfoB)
		{
			if (_ObstacleInfoA is ObstacleInfoSingle obstacleInfoSingle)
			{
				if (_ObstacleInfoB is ObstacleInfoGrouped obstacleGrouped)
				{
					return new ObstacleInfoGrouped(obstacleGrouped, obstacleInfoSingle);
				}
				if (_ObstacleInfoB is ObstacleInfoSingle obstacleB)
				{
					return new ObstacleInfoGrouped(obstacleInfoSingle, obstacleB);
				}
			}
			else if (_ObstacleInfoA is ObstacleInfoGrouped obstacleInfoGrouped)
			{
				if (_ObstacleInfoB is ObstacleInfoGrouped obstacleGroupedB)
				{
					return new ObstacleInfoGrouped(obstacleInfoGrouped, obstacleGroupedB);
				}
				if (_ObstacleInfoB is ObstacleInfoSingle obstacleSingle)
				{
					return new ObstacleInfoGrouped(obstacleInfoGrouped, obstacleSingle);
				}
			}
			throw new ArgumentException(string.Format(EXCEPTION_UNSUPPORTED_OBSTACLE_INFO_TYPE, _ObstacleInfoA.GetType().FullName, _ObstacleInfoB.GetType().FullName));
		}

		public ObstacleInfoGrouped(List<ObstacleInfoSingle> _ObstacleInfos, List<Triangle> _Triangles, Bounds _Bounds)
		{
			m_IDs = _ObstacleInfos.Select((ObstacleInfoSingle _Info) => _Info.ObstacleControllerID).ToList();
			ObstacleInfos = new Dictionary<int, List<ObstacleInfoSingle>>();
			foreach (ObstacleInfoSingle _ObstacleInfo in _ObstacleInfos)
			{
				ObstacleInfos.GetOrAdd(_ObstacleInfo.ObstacleControllerID).Add(_ObstacleInfo);
			}
			base.Triangles = _Triangles;
			base.Bounds = _Bounds;
		}

		private ObstacleInfoGrouped(ObstacleInfoSingle _ObstacleA, ObstacleInfoSingle _ObstacleB)
		{
			m_IDs = new List<int> { _ObstacleA.ObstacleControllerID, _ObstacleB.ObstacleControllerID };
			ObstacleInfos = new Dictionary<int, List<ObstacleInfoSingle>>();
			ObstacleInfos.GetOrAdd(_ObstacleA.ObstacleControllerID).Add(_ObstacleA);
			ObstacleInfos.GetOrAdd(_ObstacleB.ObstacleControllerID).Add(_ObstacleB);
			base.Triangles = new List<Triangle>(_ObstacleA.Triangles.Count + _ObstacleB.Triangles.Count);
			base.Triangles.AddRange(_ObstacleA.Triangles);
			base.Triangles.AddRange(_ObstacleB.Triangles);
			ComputeBounds();
		}

		private ObstacleInfoGrouped(ObstacleInfoGrouped _ObstacleGrouped, ObstacleInfoSingle _ObstacleSingle)
		{
			m_IDs = new List<int>(_ObstacleGrouped.m_IDs.Count + 1);
			m_IDs.AddRange(_ObstacleGrouped.m_IDs);
			m_IDs.Add(_ObstacleSingle.ObstacleControllerID);
			ObstacleInfos = new Dictionary<int, List<ObstacleInfoSingle>>(_ObstacleGrouped.ObstacleInfos.Count + 1);
			ObstacleInfos.AddRange(_ObstacleGrouped.ObstacleInfos);
			ObstacleInfos.GetOrAdd(_ObstacleSingle.ObstacleControllerID).Add(_ObstacleSingle);
			base.Triangles = new List<Triangle>(_ObstacleGrouped.Triangles.Count + _ObstacleSingle.Triangles.Count);
			base.Triangles.AddRange(_ObstacleGrouped.Triangles);
			base.Triangles.AddRange(_ObstacleSingle.Triangles);
			ComputeBounds();
		}

		private ObstacleInfoGrouped(ObstacleInfoGrouped _ObstacleGroupedA, ObstacleInfoGrouped _ObstacleGroupedB)
		{
			m_IDs = new List<int>(_ObstacleGroupedA.m_IDs.Count + _ObstacleGroupedB.m_IDs.Count);
			m_IDs.AddRange(_ObstacleGroupedA.m_IDs);
			m_IDs.AddRange(_ObstacleGroupedB.m_IDs);
			ObstacleInfos = new Dictionary<int, List<ObstacleInfoSingle>>(_ObstacleGroupedA.ObstacleInfos.Count + _ObstacleGroupedB.ObstacleInfos.Count);
			ObstacleInfos.AddRange(_ObstacleGroupedA.ObstacleInfos);
			_ObstacleGroupedB.ObstacleInfos.ForEach((KeyValuePair<int, List<ObstacleInfoSingle>> _Kvp) =>
			{
				ObstacleInfos.GetOrAdd(_Kvp.Key).AddRange(_Kvp.Value);
			});
			base.Triangles = new List<Triangle>(_ObstacleGroupedA.Triangles.Count + _ObstacleGroupedB.Triangles.Count);
			base.Triangles.AddRange(_ObstacleGroupedA.Triangles);
			base.Triangles.AddRange(_ObstacleGroupedB.Triangles);
			ComputeBounds();
		}

		public override void ReplaceID(int _OldID, int _NewID)
		{
			for (int i = 0; i < m_IDs.Count; i++)
			{
				if (m_IDs[i] == _OldID)
				{
					m_IDs[i] = _NewID;
				}
			}
			if (ObstacleInfos.TryGetValue(_OldID, out var value))
			{
				value.ForEach((ObstacleInfoSingle _Info) =>
				{
					_Info.ReplaceID(_OldID, _NewID);
				});
				ObstacleInfos.Remove(_OldID);
				ObstacleInfos.Add(_NewID, value);
			}
		}

		public List<ObstacleInfoSingle> GetInfosExceptByID(int _ObstacleControllerID)
		{
			List<ObstacleInfoSingle> list = new List<ObstacleInfoSingle>();
			foreach (KeyValuePair<int, List<ObstacleInfoSingle>> obstacleInfo in ObstacleInfos)
			{
				if (obstacleInfo.Key != _ObstacleControllerID)
				{
					list.AddRange(obstacleInfo.Value);
				}
			}
			return list;
		}

		public ObstacleInfoBaseSerializable GetSerializableInstance(int[] _SingleInfosIDs, int _ID)
		{
			return new ObstacleInfoGroupedSerializable(this, _SingleInfosIDs, _ID);
		}
	}
}
