using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nav3D.Common;
using Nav3D.Obstacles.Serialization;
using UnityEngine;

namespace Nav3D.Obstacles
{
	public class Fork : Node
	{
		private readonly Dictionary<ForkChildOctIndex, Node> m_ChildrenMap = new Dictionary<ForkChildOctIndex, Node>();

		public Dictionary<ForkChildOctIndex, Node> ChildrenMap => m_ChildrenMap;

		public Fork(Octree _Octree, SpatialGrid _SpatialGrid, TriangleStorage _ObstacleTriangles, float _Size, Vector3Int _Index, int _GridLayer, bool _Occupied, Func<int> _GetID, int _MaxGridLayer, bool _NeedCheckMaxLayer, CancellationToken _CancellationToken, int _ParallelFactor)
			: base(_Octree, _Size, _Index, _GridLayer, _Occupied, _GetID)
		{
			if (!_CancellationToken.IsCancellationRequested)
			{
				ProduceChildren(_MaxGridLayer, _GetID, _SpatialGrid, _ObstacleTriangles, _CancellationToken, _NeedCheckMaxLayer, _ParallelFactor);
			}
		}

		public Fork(int _ID, Vector3Int _Index, float _Size, int _GridLayer, bool _Occupied)
			: base(_ID, _Index, _Size, _GridLayer, _Occupied)
		{
		}

		public override Leaf GetClosestOrEmbracingLeaf(Vector3 _Point)
		{
			Vector3 center = base.Bounds.center;
			if (_Point.z >= center.z)
			{
				if (_Point.y >= center.y)
				{
					if (_Point.x >= center.x)
					{
						if (!m_ChildrenMap.TryGetValue(ForkChildOctIndex.I111, out var value))
						{
							return null;
						}
						return value.GetClosestOrEmbracingLeaf(_Point);
					}
					if (!m_ChildrenMap.TryGetValue(ForkChildOctIndex.I011, out var value2))
					{
						return null;
					}
					return value2.GetClosestOrEmbracingLeaf(_Point);
				}
				if (_Point.x >= center.x)
				{
					if (!m_ChildrenMap.TryGetValue(ForkChildOctIndex.I101, out var value3))
					{
						return null;
					}
					return value3.GetClosestOrEmbracingLeaf(_Point);
				}
				if (!m_ChildrenMap.TryGetValue(ForkChildOctIndex.I001, out var value4))
				{
					return null;
				}
				return value4.GetClosestOrEmbracingLeaf(_Point);
			}
			if (_Point.y >= center.y)
			{
				if (_Point.x >= center.x)
				{
					if (!m_ChildrenMap.TryGetValue(ForkChildOctIndex.I110, out var value5))
					{
						return null;
					}
					return value5.GetClosestOrEmbracingLeaf(_Point);
				}
				if (!m_ChildrenMap.TryGetValue(ForkChildOctIndex.I010, out var value6))
				{
					return null;
				}
				return value6.GetClosestOrEmbracingLeaf(_Point);
			}
			if (_Point.x >= center.x)
			{
				if (!m_ChildrenMap.TryGetValue(ForkChildOctIndex.I100, out var value7))
				{
					return null;
				}
				return value7.GetClosestOrEmbracingLeaf(_Point);
			}
			if (!m_ChildrenMap.TryGetValue(ForkChildOctIndex.I000, out var value8))
			{
				return null;
			}
			return value8.GetClosestOrEmbracingLeaf(_Point);
		}

		public override void GetSerializableInstances(List<NodeSerializable> _Nodes, ObstacleSerializingProgress _Progress)
		{
			base.GetSerializableInstances(_Nodes, _Progress);
			if (_Progress.CancellationToken.IsCancellationRequested)
			{
				return;
			}
			_Progress.SetNodesPackingProgress(_Nodes.Count, m_Octree.NodeCount);
			foreach (KeyValuePair<ForkChildOctIndex, Node> item in m_ChildrenMap)
			{
				item.Value.GetSerializableInstances(_Nodes, _Progress);
			}
		}

		public void SetChildren(Dictionary<int, Node> _NodeMap, (ForkChildOctIndex OctIndex, int ID)[] _ChildrenData)
		{
			for (int i = 0; i < _ChildrenData.Length; i++)
			{
				(ForkChildOctIndex, int) tuple = _ChildrenData[i];
				if (_NodeMap.TryGetValue(tuple.Item2, out var value))
				{
					m_ChildrenMap.Add(tuple.Item1, value);
				}
			}
		}

		private void ProduceChildren(int _MaxGridLayer, Func<int> _GetID, SpatialGrid _SpatialGrid, TriangleStorage _ObstacleTriangles, CancellationToken _CancellationToken, bool _NeedCheckMaxLayer, int _ParallelFactor)
		{
			bool flag;
			if (flag = _ParallelFactor > 0)
			{
				_ParallelFactor--;
			}
			List<Task> list = new List<Task>();
			float childrenSize = m_Octree.GetBucketSizeOnLayer(m_GridLayer + 1);
			ConcurrentDictionary<ForkChildOctIndex, Node> tmpChildrenMap = new ConcurrentDictionary<ForkChildOctIndex, Node>();
			(ForkChildOctIndex, Vector3Int)[] childrenIndices = UtilsSpatialGrid.GetChildrenIndices1(m_Index);
			for (int i = 0; i < childrenIndices.Length; i++)
			{
				(ForkChildOctIndex OctIndex, Vector3Int GridIndex) childIndexData = childrenIndices[i];
				Bounds childBounds = UtilsSpatialGrid.GetBucketBounds(childIndexData.GridIndex, childrenSize);
				if (!m_Octree.ObstacleInfo.Bounds.Intersects(childBounds))
				{
					_SpatialGrid.SubtractLeafFromProgress(m_GridLayer + 1, _Processed: false);
					continue;
				}
				bool needCheckMaxLayer = _NeedCheckMaxLayer && ObstacleParticularResolutionManager.Instance.HasCrossingBoundables(childBounds);
				int maxLayer = _MaxGridLayer;
				if (needCheckMaxLayer && m_Octree.TryGetEmbracingRegionsMinRes(childBounds, out var _BucketSize, out var _RegionsCount) && ObstacleParticularResolutionManager.Instance.GetCrossingBoundablesCount(childBounds) == _RegionsCount)
				{
					maxLayer = m_Octree.GetLayersCount(_BucketSize, out var _) - 1;
					needCheckMaxLayer = false;
				}
				if (!flag)
				{
					Node value = Node.Create(m_Octree, _SpatialGrid, _ObstacleTriangles, childrenSize, childIndexData.GridIndex, m_GridLayer + 1, IsOccupy(_ObstacleTriangles, childBounds), _GetID, maxLayer, needCheckMaxLayer, _CancellationToken, _ParallelFactor);
					m_ChildrenMap.Add(childIndexData.OctIndex, value);
					continue;
				}
				list.Add(Task.Run(() =>
				{
					Node value2 = Node.Create(m_Octree, _SpatialGrid, _ObstacleTriangles, childrenSize, childIndexData.GridIndex, m_GridLayer + 1, IsOccupy(_ObstacleTriangles, childBounds), _GetID, maxLayer, needCheckMaxLayer, _CancellationToken, _ParallelFactor);
					tmpChildrenMap.TryAdd(childIndexData.OctIndex, value2);
				}, _CancellationToken));
			}
			if (flag)
			{
				Task.WaitAll(list.ToArray(), _CancellationToken);
				m_ChildrenMap.AddRange(tmpChildrenMap);
			}
		}

		protected override NodeSerializable GetSerializableInstance()
		{
			byte map = 0;
			int[] childrenIDs = m_ChildrenMap.OrderBy((KeyValuePair<ForkChildOctIndex, Node> _KVP) => _KVP.Key).Select((KeyValuePair<ForkChildOctIndex, Node> _KVP) =>
			{
				map += (byte)_KVP.Key;
				return _KVP.Value.ID;
			}).ToArray();
			return new ForkSerializable(m_ID, m_Index, m_Size, (byte)m_GridLayer, m_Occupied, childrenIDs, map);
		}
	}
}
